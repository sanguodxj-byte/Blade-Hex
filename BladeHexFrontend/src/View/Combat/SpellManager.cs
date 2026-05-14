using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

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

        // 3. 魔力
        if (caster.Data.CurrentMana < spell.ManaCost)
            return new Godot.Collections.Dictionary { { "can_cast", false }, { "reason", $"魔力不足（需要{spell.ManaCost}，当前{caster.Data.CurrentMana}）" } };

        return new Godot.Collections.Dictionary { { "can_cast", true }, { "reason", "" } };
    }

    public Godot.Collections.Dictionary CastSpell(Unit caster, SpellData spell, Vector2I targetCell, HexGrid grid)
    {
        var check = CanCastSpell(caster, spell);
        if (!check["can_cast"].AsBool()) return new Godot.Collections.Dictionary { { "success", false }, { "reason", check["reason"] } };

        caster.Data!.CurrentMana -= spell.ManaCost;
        caster.Data!.SpellCooldowns[spell.SpellId] = spell.CooldownTurns;

        var cellsArray = SpellShapeResolver.GetCellsInShape((int)spell.shape, targetCell, caster.GridPos, spell.ShapeSize, pos => grid.GetCell(pos.X, pos.Y) != null);
        var targetCells = new Godot.Collections.Array<Vector2I>(cellsArray);
        var results = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        switch (spell.resolutionType)
        {
            case SpellData.ResolutionType.AttackRoll:
                results = ResolveAttackSpell(caster, spell, targetCells, grid);
                break;
            case SpellData.ResolutionType.Save:
                results = ResolveSaveSpell(caster, spell, targetCells, grid);
                break;
            case SpellData.ResolutionType.AutoHit:
                results = ResolveAutoSpell(caster, spell, targetCells, grid);
                break;
        }

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

            int roll = RPGRuleEngine.RollDice(1, 20);
            int mod = GetCastingModifier(caster);
            int prof = RPGRuleEngine.GetProficiencyBonus(caster.Data!.Level);
            int total = roll + mod + prof;

            bool isHit = total >= target.Model.GetAc() || roll == 20;
            bool isCrit = roll == 20;

            if (isHit)
            {
                int damage = RPGRuleEngine.RollDice(spell.DamageDiceCount, spell.DamageDiceSides) + mod;
                if (isCrit) damage *= 2;
                target.TakeDamage(damage);
                EmitSignal(SignalName.SpellHit, target, damage, spell.DamageType);
                results.Add(new Godot.Collections.Dictionary { { "target", target }, { "hit", true }, { "critical", isCrit }, { "damage", damage } });
            }
            else
            {
                EmitSignal(SignalName.SpellMissed, target);
                results.Add(new Godot.Collections.Dictionary { { "target", target }, { "hit", false }, { "damage", 0 } });
            }
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

            int abilityScore = GetSaveAbilityScore(target, spell.saveType);
            int prof = RPGRuleEngine.GetProficiencyBonus(target.Data!.Level);
            bool saved = RPGRuleEngine.MakeSave(abilityScore, prof, false, dc)["success"].AsBool();

            int damage = RPGRuleEngine.RollDice(spell.DamageDiceCount, spell.DamageDiceSides);
            if (saved) damage = Math.Max(1, damage / 2);

            target.TakeDamage(damage);
            EmitSignal(SignalName.SpellHit, target, damage, spell.DamageType);
            results.Add(new Godot.Collections.Dictionary { { "target", target }, { "hit", true }, { "saved", saved }, { "damage", damage } });
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

            if (spell.HealDiceCount > 0)
            {
                int heal = RPGRuleEngine.RollDice(spell.HealDiceCount, spell.HealDiceSides) + GetCastingModifier(caster) + spell.HealBonus;
                int actual = target.Heal(heal);
                EmitSignal(SignalName.SpellHealed, target, actual);
                results.Add(new Godot.Collections.Dictionary { { "target", target }, { "healed", true }, { "amount", actual } });
            }
            else if (spell.DamageDiceCount > 0)
            {
                int damage = RPGRuleEngine.RollDice(spell.DamageDiceCount, spell.DamageDiceSides) + GetCastingModifier(caster);
                target.TakeDamage(damage);
                EmitSignal(SignalName.SpellHit, target, damage, spell.DamageType);
                results.Add(new Godot.Collections.Dictionary { { "target", target }, { "hit", true }, { "damage", damage } });
            }
        }
        return results;
    }

    public int GetSpellDc(Unit caster)
    {
        int score = GetCastingAbilityScore(caster);
        int prof = RPGRuleEngine.GetProficiencyBonus(caster.Data!.Level);
        return 8 + RPGRuleEngine.GetStatModifier(score) + prof;
    }

    private int GetCastingModifier(Unit caster) => RPGRuleEngine.GetStatModifier(GetCastingAbilityScore(caster));

    private int GetCastingAbilityScore(Unit caster)
    {
        if (caster.Data == null) return 10;
        string ability = caster.Data.CastingAbility ?? "intel";
        return ability switch
        {
            "intel" => caster.Data.Intel,
            "cha" => caster.Data.Cha,
            "wis" => caster.Data.Wis,
            _ => caster.Data.Intel
        };
    }

    private int GetSaveAbilityScore(Unit target, SpellData.SaveType saveType) => saveType switch
    {
        SpellData.SaveType.StrSave => target.Data?.Str ?? 10,
        SpellData.SaveType.DexSave => target.Data?.Dex ?? 10,
        SpellData.SaveType.ConSave => target.Data?.Con ?? 10,
        SpellData.SaveType.IntSave => target.Data?.Intel ?? 10,
        SpellData.SaveType.WisSave => target.Data?.Wis ?? 10,
        SpellData.SaveType.ChaSave => target.Data?.Cha ?? 10,
        _ => 10
    };

    public int GetMaxMana(Unit unit)
    {
        if (unit.Data == null) return 0;
        int baseMana = 10 + unit.Data.Level * 2;
        string ability = unit.Data.CastingAbility ?? "intel";
        int score = ability switch
        {
            "intel" => unit.Data.Intel,
            "cha" => unit.Data.Cha,
            "wis" => unit.Data.Wis,
            _ => unit.Data.Intel
        };
        return baseMana + RPGRuleEngine.GetStatModifier(score) * 5;
    }
}
