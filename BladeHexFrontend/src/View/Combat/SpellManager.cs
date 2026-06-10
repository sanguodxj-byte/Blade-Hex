using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 法术管理器 — 法术施放、魔力管理、冷却、法术解析
/// </summary>
[GlobalClass]
public partial class SpellManager : Node
{
    [Signal] public delegate void SpellCastEventHandler(Unit caster, SpellData spell, Godot.Collections.Array<Vector2I> targets);
    [Signal] public delegate void SpellHitEventHandler(Unit target, int damage, string damageType);
    [Signal] public delegate void SpellMissedEventHandler(Unit target);
    [Signal] public delegate void SpellHealedEventHandler(Unit target, int amount);

    public Godot.Collections.Dictionary CanCastSpell(Unit caster, SpellData spell)
    {
        if (caster.Data == null) return new Godot.Collections.Dictionary { { "can_cast", false }, { "reason", "无单位数据" } };

        // 1. 需要法术触媒
        var weapon = caster.Model.GetMainHand() as WeaponData;
        var offHand = caster.Model.GetOffHand() as WeaponData;
        bool hasCatalyst = (weapon != null && weapon.IsCatalyst) || (offHand != null && offHand.IsCatalyst);
        
        if (!hasCatalyst) return new Godot.Collections.Dictionary { { "can_cast", false }, { "reason", "需要装备法杖或魔导书" } };

        // 2. 冷却中
        if (caster.Data.SpellCooldowns.ContainsKey(spell.SpellId))
        {
            int cd = caster.Data.SpellCooldowns[spell.SpellId].AsInt32();
            if (cd > 0)
                return new Godot.Collections.Dictionary { { "can_cast", false }, { "reason", $"法术冷却中（{cd}回合）" } };
        }

		// 3. 魔力 — v1 职业被动: CareerNextSpellFreeMana 豁免法力消耗
		bool freeMana = caster.Data?.Runtime?.CareerNextSpellFreeMana == true;
		int effectiveManaCost = SkillTreeKeystoneResolver.ApplySpellManaCost(caster.Data!, spell.ManaCost);
		int hpCost = SkillTreeKeystoneResolver.GetSpellHpCost(caster.Data!, spell.ManaCost);
		if (!freeMana && caster.Data!.CurrentMana < effectiveManaCost)
			return new Godot.Collections.Dictionary { { "can_cast", false }, { "reason", $"魔力不足（需要{effectiveManaCost}，当前{caster.Data.CurrentMana}）" } };
        if (hpCost > 0 && caster.CurrentHp <= hpCost)
            return new Godot.Collections.Dictionary { { "can_cast", false }, { "reason", $"生命不足（需要{hpCost}，当前{caster.CurrentHp}）" } };

        return new Godot.Collections.Dictionary { { "can_cast", true }, { "reason", "" } };
    }

    public Godot.Collections.Dictionary CastSpell(Unit caster, SpellData spell, Vector2I targetCell, HexGrid grid)
    {
        // v1 职业被动: 万象代价/静默之刃 → 检查是否可施法
        if (!CareerPassiveHooks.CanCastSpell(caster))
            return new Godot.Collections.Dictionary { { "success", false }, { "reason", "无法施法" } };

        var check = CanCastSpell(caster, spell);
        if (!check["can_cast"].AsBool()) return new Godot.Collections.Dictionary { { "success", false }, { "reason", check["reason"] } };

        // 唤星者: 目标格高度需低于施法者当前格高度
        if (caster.HasCareerSkillEffect("starcaller_height_spell") && grid != null)
        {
            var casterCell = grid.GetCell(caster.GridPos.X, caster.GridPos.Y);
            var targetHexCell = grid.GetCell(targetCell.X, targetCell.Y);
            if (casterCell != null && targetHexCell != null && targetHexCell.Elevation >= casterCell.Elevation)
                return new Godot.Collections.Dictionary { { "success", false }, { "reason", "目标高度需低于施法者" } };
        }

        // 先解析目标并检查有效性（不消耗资源）
        var cellsArray = SpellShapeResolver.GetCellsInShape((int)spell.shape, targetCell, caster.GridPos, spell.ShapeSize, pos => grid!.GetCell(pos.X, pos.Y) != null);
        var targetCells = new Godot.Collections.Array<Vector2I>(cellsArray);
        if (SpellTargetRules.RequiresValidUnitTarget(spell) && !HasAnyValidTarget(caster, spell, targetCells, grid!))
            return new Godot.Collections.Dictionary { { "success", false }, { "reason", "没有有效目标。" } };

        // 目标有效后再消耗资源
        int effectiveManaCost = SkillTreeKeystoneResolver.ApplySpellManaCost(caster.Data!, spell.ManaCost);
        int hpCost = SkillTreeKeystoneResolver.GetSpellHpCost(caster.Data!, spell.ManaCost);
        int actualManaCost = CareerPassiveHooks.ModifySpellManaCost(caster, effectiveManaCost);
        caster.Model.CurrentMana = System.Math.Max(0, caster.Model.CurrentMana - actualManaCost);
        caster.Data!.CurrentMana = caster.Model.CurrentMana;
        if (hpCost > 0)
            caster.SetHp(System.Math.Max(1, caster.CurrentHp - hpCost));
        // v1 职业被动: 血契之环 — 消耗法力时等额回血
        if (actualManaCost > 0)
            CareerPassiveHooks.OnManaSpent(caster, actualManaCost);
        caster.Data!.SpellCooldowns[spell.SpellId] = spell.CooldownTurns;

        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        switch (spell.resolutionType)
        {
            case SpellData.ResolutionType.AttackRoll:
                results = ResolveAttackSpell(caster, spell, targetCells, grid!);
                break;
            case SpellData.ResolutionType.Save:
                results = ResolveSaveSpell(caster, spell, targetCells, grid!);
                break;
            case SpellData.ResolutionType.AutoHit:
                results = ResolveAutoSpell(caster, spell, targetCells, grid!);
                break;
        }

        // v1 职业被动: 施法后钩子 (焰风之怒/幻术师/鏖战骑士/魔武者)
        CareerPassiveHooks.OnSpellCast(caster);

        EmitSignal(SignalName.SpellCast, caster, spell, targetCells);
        return new Godot.Collections.Dictionary { { "success", true }, { "results", results }, { "target_cells", targetCells } };
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> ResolveAttackSpell(Unit caster, SpellData spell, Godot.Collections.Array<Vector2I> targetCells, HexGrid grid)
    {
        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var pos in targetCells)
        {
            var cell = grid.GetCell(pos.X, pos.Y);
            if (cell?.Occupant is not Unit target) continue;
            if (!IsValidTarget(caster, target, spell)) continue;

            // 法术设计不采用攻击检定 vs AC，始终命中
            int mod = GetCastingModifier(caster);
            float spellDamageMultiplier = GetSkillTreeSpellDamageMultiplier(caster);

            var result = new Godot.Collections.Dictionary { { "target", target }, { "hit", true }, { "critical", false } };
            if (spell.DamageDiceCount > 0 && spell.DamageDiceSides > 0)
            {
                int damage = RPGRuleEngine.RollDice(spell.DamageDiceCount, spell.DamageDiceSides) + mod;
                // v1 职业被动: 天选者 — 法术暴击检定 (伤害修正前)
                bool spellCrit = CareerPassiveHooks.RollSpellCritical(caster);
                if (spellCrit)
                {
                    damage = (int)(damage * PassiveSkillResolver.GetCritMultiplier(caster));
                    result["critical"] = true;
                }
                if (caster.Data != null)
                    damage = Math.Max(1, (int)(damage * SkillTreeKeystoneResolver.GetSpellDamageFinalMultiplier(
                        caster.Data, GetEquippedCatalyst(caster), caster.HasMoved)));
                damage = Math.Max(1, (int)(damage * spellDamageMultiplier));
                // v1 职业被动: 法术伤害加成 (施法者 + 唤星者高度差)
                damage = CareerPassiveHooks.ModifySpellDamageAgainstTarget(caster, target, grid, damage);
                // v1 职业被动: 敌法师等法术减伤 (防御者)
                damage = CareerPassiveHooks.ModifyIncomingSpellDamage(target, damage);
                target.TakeDamage(damage);
                EmitSignal(SignalName.SpellHit, target, damage, spell.DamageType);
                result["damage"] = damage;
                // v1 职业被动: 幻术师等受击消耗
                CareerPassiveHooks.OnDefenderHit(target);
                // v1 职业被动: 血契之环 — 受到 HP 伤害时等额回法力
                if (damage > 0)
                    CareerPassiveHooks.OnHpDamageTaken(target, damage);
            }
            ApplyStatusIfNeeded(caster, spell, target, result);
            results.Add(result);
        }
        return results;
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> ResolveSaveSpell(Unit caster, SpellData spell, Godot.Collections.Array<Vector2I> targetCells, HexGrid grid)
    {
        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        int dc = GetSpellDc(caster);
        foreach (var pos in targetCells)
        {
            var cell = grid.GetCell(pos.X, pos.Y);
            if (cell?.Occupant is not Unit target) continue;
            if (!IsValidTarget(caster, target, spell)) continue;

            int abilityScore = GetSaveAbilityScore(target, spell.saveType);
            int bonus = CombatStats.GetSaveBonus(target.Data)
                + PassiveSkillResolver.GetRoyalPresenceAuraSaveBonus(target, target.CombatManager?.AllUnits);
            bool saved = RPGRuleEngine.MakeSave(abilityScore, bonus, false, dc)["success"].AsBool();

            var result = new Godot.Collections.Dictionary { { "target", target }, { "hit", true }, { "saved", saved } };
            if (spell.DamageDiceCount > 0 && spell.DamageDiceSides > 0)
            {
                int damage = RPGRuleEngine.RollDice(spell.DamageDiceCount, spell.DamageDiceSides);
                // v1 职业被动: 天选者 — 法术暴击检定 (伤害修正前)
                bool spellCrit = CareerPassiveHooks.RollSpellCritical(caster);
                if (spellCrit)
                {
                    damage = (int)(damage * PassiveSkillResolver.GetCritMultiplier(caster));
                    result["critical"] = true;
                }
                if (saved) damage = Math.Max(1, damage / 2);
                if (caster.Data != null)
                    damage = Math.Max(1, (int)(damage * SkillTreeKeystoneResolver.GetSpellDamageFinalMultiplier(
                        caster.Data, GetEquippedCatalyst(caster), caster.HasMoved)));
                damage = Math.Max(1, (int)(damage * GetSkillTreeSpellDamageMultiplier(caster)));
                // v1 职业被动: 法术伤害加成 (施法者 + 唤星者高度差)
                damage = CareerPassiveHooks.ModifySpellDamageAgainstTarget(caster, target, grid, damage);
                // v1 职业被动: 敌法师等法术减伤 (防御者)
                damage = CareerPassiveHooks.ModifyIncomingSpellDamage(target, damage);

                target.TakeDamage(damage);
                EmitSignal(SignalName.SpellHit, target, damage, spell.DamageType);
                result["damage"] = damage;
                // v1 职业被动: 幻术师等受击消耗
                CareerPassiveHooks.OnDefenderHit(target);
                // v1 职业被动: 血契之环 — 受到 HP 伤害时等额回法力
                if (damage > 0)
                    CareerPassiveHooks.OnHpDamageTaken(target, damage);
            }
            if (!saved)
                ApplyStatusIfNeeded(caster, spell, target, result);
            results.Add(result);
        }
        return results;
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> ResolveAutoSpell(Unit caster, SpellData spell, Godot.Collections.Array<Vector2I> targetCells, HexGrid grid)
    {
        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var pos in targetCells)
        {
            var cell = grid.GetCell(pos.X, pos.Y);
            if (cell?.Occupant is not Unit target) continue;
            if (!IsValidTarget(caster, target, spell)) continue;

            if (spell.DamageDiceCount > 0)
            {
                int damage = RPGRuleEngine.RollDice(spell.DamageDiceCount, spell.DamageDiceSides)
                           + GetCastingModifier(caster);
                // v1 职业被动: 天选者 — 法术暴击检定 (伤害修正前)
                bool spellCrit = CareerPassiveHooks.RollSpellCritical(caster);
                if (spellCrit)
                {
                    damage = (int)(damage * PassiveSkillResolver.GetCritMultiplier(caster));
                }
                if (caster.Data != null)
                    damage = Math.Max(1, (int)(damage * SkillTreeKeystoneResolver.GetSpellDamageFinalMultiplier(
                        caster.Data, GetEquippedCatalyst(caster), caster.HasMoved)));
                damage = Math.Max(1, (int)(damage * GetSkillTreeSpellDamageMultiplier(caster)));
                // v1 职业被动: 法术伤害加成 (施法者 + 唤星者高度差)
                damage = CareerPassiveHooks.ModifySpellDamageAgainstTarget(caster, target, grid, damage);
                // v1 职业被动: 敌法师等法术减伤 (防御者)
                damage = CareerPassiveHooks.ModifyIncomingSpellDamage(target, damage);
                target.TakeDamage(damage);
                EmitSignal(SignalName.SpellHit, target, damage, spell.DamageType);
                results.Add(new Godot.Collections.Dictionary { { "target", target }, { "hit", true }, { "damage", damage }, { "critical", spellCrit } });
                // v1 职业被动: 幻术师等受击消耗
                CareerPassiveHooks.OnDefenderHit(target);
                // v1 职业被动: 血契之环 — 受到 HP 伤害时等额回法力
                if (damage > 0)
                    CareerPassiveHooks.OnHpDamageTaken(target, damage);
            }

            if (spell.HealDiceCount > 0)
            {
                int heal = RPGRuleEngine.RollDice(spell.HealDiceCount, spell.HealDiceSides)
                         + GetCastingModifier(caster) + spell.HealBonus;
                heal = Math.Max(1, (int)(heal * GetSkillTreeHealMultiplier(caster)));
                Unit healTarget = spell.DamageDiceCount > 0 ? caster : target;
                int actual = healTarget.Heal(heal, caster);
                EmitSignal(SignalName.SpellHealed, healTarget, actual);
                results.Add(new Godot.Collections.Dictionary { { "target", healTarget }, { "healed", true }, { "amount", actual } });
            }

            if (!string.IsNullOrEmpty(spell.AppliedStatusEffect) && target.Data != null)
                ApplyStatusIfNeeded(caster, spell, target, results);
        }
        return results;
    }

    private static bool IsValidTarget(Unit caster, Unit target, SpellData spell)
        => caster != null
        && target != null
        && target.CurrentHp > 0
        && SpellTargetRules.IsValidTarget(caster.Data, target.Data, spell);

    private static WeaponData? GetEquippedCatalyst(Unit caster)
    {
        if (caster.Model.GetMainHand() is WeaponData main && main.IsCatalyst)
            return main;
        if (caster.Model.GetOffHand() is WeaponData off && off.IsCatalyst)
            return off;
        return null;
    }

    private static bool HasAnyValidTarget(Unit caster, SpellData spell, Godot.Collections.Array<Vector2I> targetCells, HexGrid grid)
    {
        foreach (var pos in targetCells)
        {
            var cell = grid.GetCell(pos.X, pos.Y);
            if (cell?.Occupant is Unit target && IsValidTarget(caster, target, spell))
                return true;
        }
        return false;
    }

    private static void ApplyStatusIfNeeded(Unit caster, SpellData spell, Unit target, Godot.Collections.Dictionary result)
    {
        if (string.IsNullOrEmpty(spell.AppliedStatusEffect) || target.Data == null) return;
        if (PassiveSkillResolver.IsFearEffect(spell.AppliedStatusEffect)
            && !PassiveSkillResolver.CanApplyFearEffect(target, target.CombatManager?.AllUnits))
            return;

        BladeHex.Combat.Buff.BuffSystem.Apply(
            target.Data,
            spell.AppliedStatusEffect,
            Math.Max(1, spell.StatusDuration),
            sourceUnitId: caster.Data?.CharacterId ?? -1);
        result["status"] = spell.AppliedStatusEffect;
        result["duration"] = Math.Max(1, spell.StatusDuration);
    }

    private static void ApplyStatusIfNeeded(Unit caster, SpellData spell, Unit target, Godot.Collections.Array<Godot.Collections.Dictionary> results)
    {
        var result = new Godot.Collections.Dictionary { { "target", target } };
        ApplyStatusIfNeeded(caster, spell, target, result);
        if (result.ContainsKey("status"))
            results.Add(result);
    }

    public int GetSpellDc(Unit caster)
    {
        int score = GetCastingAbilityScore(caster);
        return 8 + RPGRuleEngine.GetStatModifier(score);
    }

    private int GetCastingModifier(Unit caster) => RPGRuleEngine.GetStatModifier(GetCastingAbilityScore(caster));

    private static float GetSkillTreeSpellDamageMultiplier(Unit caster)
        => Math.Max(0.0f, 1.0f + (caster.SkillTree?.GetSpellDamagePercentBonus() ?? 0.0f));

    private static float GetSkillTreeHealMultiplier(Unit caster)
        => Math.Max(0.0f, 1.0f + (caster.SkillTree?.GetHealPercentBonus() ?? 0.0f));

    private int GetCastingAbilityScore(Unit caster)
    {
        if (caster.Data == null) return 10;
        string ability = caster.Data.CastingAbility ?? "intel";
        return ability switch
        {
            "intel" => CombatStats.GetEffectiveInt(caster.Data),
            "cha" => CombatStats.GetEffectiveCha(caster.Data),
            "wis" => CombatStats.GetEffectiveWis(caster.Data),
            _ => CombatStats.GetEffectiveInt(caster.Data)
        };
    }

    private int GetSaveAbilityScore(Unit target, SpellData.SaveType saveType) => saveType switch
    {
        SpellData.SaveType.StrSave => CombatStats.GetEffectiveStr(target.Data),
        SpellData.SaveType.DexSave => CombatStats.GetEffectiveDex(target.Data),
        SpellData.SaveType.ConSave => CombatStats.GetEffectiveCon(target.Data),
        SpellData.SaveType.IntSave => CombatStats.GetEffectiveInt(target.Data),
        SpellData.SaveType.WisSave => CombatStats.GetEffectiveWis(target.Data),
        SpellData.SaveType.ChaSave => CombatStats.GetEffectiveCha(target.Data),
        _ => 10
    };

    public int GetMaxMana(Unit unit)
    {
        if (unit.Data == null) return 0;
        int baseMana = 10 + unit.Data.Level * 2;
        string ability = unit.Data.CastingAbility ?? "intel";
        int score = ability switch
        {
            "intel" => CombatStats.GetEffectiveInt(unit.Data),
            "cha" => CombatStats.GetEffectiveCha(unit.Data),
            "wis" => CombatStats.GetEffectiveWis(unit.Data),
            _ => CombatStats.GetEffectiveInt(unit.Data)
        };
        return baseMana + RPGRuleEngine.GetStatModifier(score) * 5;
    }
}
