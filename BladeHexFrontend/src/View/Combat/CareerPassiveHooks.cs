// CareerPassiveHooks.cs
// v1 职业技能被动钩子 — 集中处理所有常驻被动效果
//
// 设计原则:
//   - 每个钩子方法在战斗管线中明确的位置被调用
//   - 使用 unit.HasCareerSkillEffect(effectId) 判断当前职业
//   - 所有一至四属性被动效果均通过本系统处理
//   - 每个钩子只修改自身职责范围内的值, 不做副作用操作
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// v1 职业技能被动钩子系统
/// 调用位置: CombatResolver, MoveCommand, SpellManager, CombatManager
/// </summary>
public static class CareerPassiveHooks
{
    // ========================================================================
    // 全局战斗状态（由 CombatManager 设置）
    // ========================================================================

    /// <summary>当前战斗所有单位的引用列表，供 IsSolo / WarKing 等需要全局扫描的钩子使用</summary>
    private static List<Unit>? _allUnits;

    /// <summary>
    /// 战斗开始时由 CombatManager.StartCombat() 调用。
    /// </summary>
    public static void SetCombatState(List<Unit> allUnits)
    {
        _allUnits = allUnits;
    }

    /// <summary>
    /// 战斗结束时由 CombatManager.EndCombat() 调用，清除引用避免泄漏。
    /// </summary>
    public static void ClearCombatState()
    {
        _allUnits = null;
    }

    // ========================================================================
    // 近战伤害倍率修正 (CombatResolver 伤害计算前调用)
    // ========================================================================

    /// <summary>
    /// 根据攻击者的 v1 职业被动, 修正近战伤害倍率
    /// 在 CombatRuleEngine.CalculateDamage 之前调用
    /// </summary>
    public static float ModifyMeleeDamageMultiplier(Unit attacker, float currentMultiplier)
    {
        float mult = currentMultiplier;

        // 战士: 近战武器伤害 ×1.2
        if (attacker.HasCareerSkillEffect("warrior_melee_damage"))
            mult *= 1.2f;

        // 重战士: 移动至少 3 格后近战伤害 ×1.5
        if (attacker.HasCareerSkillEffect("juggernaut_charge_damage"))
        {
            int moved = attacker.Data?.Runtime.CareerMovedCellsThisTurn ?? 0;
            if (moved >= 3)
                mult *= 1.5f;
        }

        // 山岳之王: 不动如山状态下近战伤害 +60%
        if (attacker.Data?.Runtime.CareerMountainThroneTurns > 0)
        {
            mult *= 1.6f;
        }

        // 守护骑士: 对 HP ≤ 30% 的敌人伤害 +50%
        // (damageMultiplier is applied per-attacker, so we need defender context)
        // Handled separately in ModifyDamageByTarget

        // 荒原之心: 狂暴状态下近战伤害 ×1.5
        if (attacker.Data?.Runtime?.CareerWarchiefDamageBonusTurns > 0)
        {
            mult *= 1.5f;
        }

        // 战争领主: 自身和相邻友军近战伤害 +30%
        if (HasAuraSourceWithin(attacker, "war_king_melee_leader", 1))
            mult *= 1.30f;

        // 铁铸领主: 自身和相邻友军近战伤害 +30%
        if (HasAuraSourceWithin(attacker, "ironbound_lord_melee_ac", 1))
            mult *= 1.30f;

        // 征讨者: 周围 2 格内每有一个友军(含自身)伤害 +5%(最多 +20%)
        if (HasAuraSourceWithin(attacker, "warlord_ally_per_ally_damage", 2))
        {
            int allies = CountAlliesWithin(attacker, 2);
            mult *= 1.0f + Math.Min(allies * 0.05f, 0.20f);
        }

        // 魔武者: 施法命中后, 下一次近战伤害 +8%/层(最多 3 层); 触发后清空法术层
        if (attacker.HasCareerSkillEffect("spellweaver_melee_spell_cycle")
            && attacker.Data?.Runtime?.CareerSpellweaverSpellStacks > 0)
        {
            mult *= GetSpellweaverSpellToMeleeMultiplier(attacker);
            attacker.Data.Runtime.CareerSpellweaverSpellStacks = 0;
        }

        return mult;
    }

    /// <summary>
    /// 根据攻击者和防御者状态修正 flat 伤害加成
    /// 在 CombatRuleEngine.CalculateDamage 之前调用
    /// </summary>
    public static int ModifyMeleeDamageBonus(Unit attacker, Unit defender, int currentBonus)
    {
        int bonus = currentBonus;

        // 守护骑士: 对 HP ≤ 30% 的敌人伤害 +50% (以基础伤害的 50% 计入)
        if (attacker.HasCareerSkillEffect("executioner_low_hp_damage"))
        {
            float defHpPct = defender.Model.GetMaxHp() > 0
                ? (float)defender.CurrentHp / defender.Model.GetMaxHp()
                : 1f;
            if (defHpPct <= 0.3f)
            {
                // +50% damage is applied as multiplier in ModifyMeleeDamageMultiplier,
                // but we also give a flat +50% of base weapon damage here
                var weapon = attacker.Model.GetMainHand() as WeaponData;
                int baseWeaponDmg = weapon != null
                    ? weapon.DamageDiceCount * (1 + weapon.DamageDiceSides) / 2
                    : 2; // unarmed avg
                bonus += (int)(baseWeaponDmg * 0.5f);
            }
        }

        // 魔剑士: 消耗法力 5%, 造成等额额外伤害
        if (attacker.HasCareerSkillEffect("spellsword_mana_burn") && attacker.Data != null)
        {
            int currentMana = attacker.Data.CurrentMana;
            int manaCost = Math.Max(1, (int)(CombatStats.GetMaxMana(attacker.Data) * 0.05f));
            int actualCost = Math.Min(manaCost, currentMana);
            if (actualCost > 0)
            {
                attacker.Data.CurrentMana -= actualCost;
                bonus += (int)(actualCost * 1.0f); // convert_ratio = 1.0
            }
        }

        return bonus;
    }

    /// <summary>
    /// 武圣: 每层 +5% 近战伤害 (最多 +50%)
    /// </summary>
    public static float GetGrandmasterDamageMultiplier(Unit attacker)
    {
        if (!attacker.HasCareerSkillEffect("grandmaster_stacking_damage"))
            return 1.0f;
        int stacks = attacker.Data?.Runtime.CareerGrandmasterStacks ?? 0;
        return 1.0f + Math.Min(stacks, 10) * 0.05f;
    }

    // ========================================================================
    // 命中/暴击修正 (CombatResolver 攻击检定前调用)
    // ========================================================================

    /// <summary>
    /// 修正命中加成
    /// </summary>
    public static int ModifyHitBonus(Unit attacker, int currentHitBonus)
    {
        int bonus = currentHitBonus;

        // 战誓者: 移动 3 格后近战命中 +2
        if (attacker.HasCareerSkillEffect("champion_move_3_boost_ally"))
        {
            int moved = attacker.Data?.Runtime.CareerMovedCellsThisTurn ?? 0;
            if (moved >= 3)
                bonus += 2;
        }

        // 孤刃之誓: 周围 2 格无友军时命中 +3
        if (attacker.HasCareerSkillEffect("lone_blade_isolated_buff"))
        {
            if (IsSolo(attacker, range: 2))
                bonus += 3;
        }

        // 战争领主: 自身和相邻友军近战攻击命中 +2
        if (HasAuraSourceWithin(attacker, "war_king_melee_leader", 1))
            bonus += 2;

        return bonus;
    }

    /// <summary>
    /// 对受伤敌人的命中加成 (需要 defender 信息)
    /// </summary>
    public static int ModifyHitBonusVsDefender(Unit attacker, Unit defender, int currentHitBonus)
    {
        int bonus = currentHitBonus;

        // 猎人: 对受伤敌人命中 +30%
        if (attacker.HasCareerSkillEffect("falconer_injured_accuracy"))
        {
            if (defender.CurrentHp < defender.GetMaxHp())
                bonus += 6; // +30% = +6 (每5% = +1)
        }

        return bonus;
    }

    /// <summary>
    /// 修正暴击率百分比加成
    /// </summary>
    public static float ModifyCritRateBonus(Unit attacker, float currentCritRate)
    {
        float bonus = currentCritRate;

        // 审判官: AP 为最大值时暴击率 +20%
        if (attacker.HasCareerSkillEffect("hawkeye_full_ap_crit"))
        {
            float maxAp = attacker.GetMaxAp();
            if (maxAp > 0 && Math.Abs(attacker.CurrentAp - maxAp) < 0.01f)
                bonus += 0.20f;
        }

        // 苦修者: HP < 80% 时暴击率 +5%
        if (attacker.HasCareerSkillEffect("veteran_low_hp_crit"))
        {
            float hpPct = attacker.Model.GetMaxHp() > 0
                ? (float)attacker.CurrentHp / attacker.Model.GetMaxHp()
                : 1f;
            if (hpPct < 0.8f)
                bonus += 0.05f;
        }

        // 风语者: 每移动 1 格 +1% 暴击率 (最多 +50%)
        if (attacker.HasCareerSkillEffect("windwalker_fixed_move_crit"))
        {
            int critBonus = attacker.Data?.Runtime.CareerWindwalkerCritBonus ?? 0;
            bonus += Math.Min(critBonus, 50) * 0.01f;
        }

        return bonus;
    }

    /// <summary>
    /// 孤刃之誓: 本回合未受伤害时暴击率 +15%
    /// </summary>
    public static float GetLoneBladeUnharmedCritBonus(Unit attacker)
    {
        if (!attacker.HasCareerSkillEffect("lone_blade_isolated_buff"))
            return 0f;
        if (IsSolo(attacker, range: 2))
            return 0.15f;
        return 0f;
    }

    // ========================================================================
    // 攻击命中后钩子 (CombatResolver 命中后调用)
    // ========================================================================

    /// <summary>
    /// 近战命中后处理钩子 — 用于叠层、触发免费法术等
    /// </summary>
    public static void OnMeleeHit(Unit attacker, Unit defender, bool wasKill)
    {
        if (attacker.Data?.Runtime == null) return;

        var rt = attacker.Data.Runtime;

        // 武圣: 近战命中后积累层数
        if (attacker.HasCareerSkillEffect("grandmaster_stacking_damage"))
        {
            rt.CareerGrandmasterStacks = Math.Min(rt.CareerGrandmasterStacks + 1, 10);
        }

        // 魔武者: 近战命中后积累近战层 (法术层不增)
        if (attacker.HasCareerSkillEffect("spellweaver_melee_spell_cycle"))
        {
            rt.CareerSpellweaverMeleeStacks = Math.Min(rt.CareerSpellweaverMeleeStacks + 1, 3);
        }

        // 鏖战骑士: 近战命中后, 下一次法术不消耗 AP
        if (attacker.HasCareerSkillEffect("arcane_war_knight_cycle"))
        {
            rt.CareerArcaneWarFreeSpellPending = true;
            rt.CareerNextSpellFreeAp = true;
        }

        // 战争之风: 移动 5 格后的必定暴击已消耗
        if (attacker.HasCareerSkillEffect("storm_banner_move_5_crit") && rt.CareerNextAttackGuaranteedCrit)
        {
            rt.CareerNextAttackGuaranteedCrit = false;
        }

        // 击杀触发
        if (wasKill)
        {
            // 天启骑士: 击杀后免费法术
            if (attacker.HasCareerSkillEffect("doom_knight_kill_free_spell"))
            {
                rt.CareerNextSpellFreeAp = true;
                rt.CareerNextSpellFreeMana = true;
            }

            // 战争之风: 击杀后恢复该次攻击消耗的 AP
            if (attacker.HasCareerSkillEffect("storm_banner_move_5_crit"))
            {
                var weapon = attacker.Model.GetMainHand() as WeaponData;
                int apRefund = weapon?.ApCost ?? 4;
                attacker.CurrentAp = Math.Min(attacker.CurrentAp + apRefund, attacker.GetMaxAp());
            }

            // 战争领主: 击杀后相邻友军恢复 2 AP
            if (attacker.HasCareerSkillEffect("war_king_melee_leader") && _allUnits != null)
            {
                foreach (var ally in _allUnits)
                {
                    if (ally == attacker) continue;
                    if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
                    if (ally.IsPlayerSide != attacker.IsPlayerSide) continue; // 同阵营
                    if (attacker.DistanceTo(ally) <= 1) // 相邻
                    {
                        ally.CurrentAp = Math.Min(ally.CurrentAp + 2, ally.GetMaxAp());
                    }
                }
            }
        }
    }

    // ========================================================================
    // 受伤害修正 (CombatResolver 伤害应用前调用)
    // ========================================================================

    /// <summary>
    /// 修正防御者受到的伤害倍率 (在 armor penetration 之后)
    /// </summary>
    public static float ModifyIncomingDamageMultiplier(Unit defender, float currentMultiplier, out bool guardianApplied, bool isMelee = true)
    {
        float mult = currentMultiplier;
        guardianApplied = false;

        if (defender.Data?.Runtime == null) return mult;

        var rt = defender.Data.Runtime;

        // 守卫: 每回合首次受到的伤害 -30%
        if (defender.HasCareerSkillEffect("guardian_first_hit_reduction") && !rt.CareerGuardianFirstHitUsedThisTurn)
        {
            mult *= 0.7f;
            rt.CareerGuardianFirstHitUsedThisTurn = true;
            guardianApplied = true;
        }

        // 山岳之王: 不动如山状态下受到伤害 -60%
        if (rt.CareerMountainThroneTurns > 0)
        {
            mult *= 0.4f;
        }

        // 磐石守护: 自身和相邻友军近战伤害 -30%; 防御者 HP<50% 时提高到 -50%
        if (isMelee && HasAuraSourceWithin(defender, "stone_saint_melee_reduction", 1))
        {
            float hpPct = defender.Model.GetMaxHp() > 0
                ? (float)defender.CurrentHp / defender.Model.GetMaxHp()
                : 1f;
            mult *= hpPct < 0.5f ? 0.5f : 0.7f;
        }

        // 守御者: 本回合未移动的友军(自身或相邻守御者光环)受到近战伤害 -20%
        if (isMelee && rt.CareerMovedCellsThisTurn == 0
            && HasAuraSourceWithin(defender, "iron_commander_adjacent_ac", 1))
        {
            mult *= 0.8f;
        }

        return mult;
    }

    /// <summary>
    /// 战法师: 自动消耗法力抵消伤害 (最后一道防线)
    /// 返回抵消后的伤害值
    /// </summary>
    public static int ApplyManaShield(Unit defender, int incomingDamage)
    {
        if (!defender.HasCareerSkillEffect("battlemage_mana_shield"))
            return incomingDamage;
        if (defender.Data == null) return incomingDamage;

        int maxShield = Math.Max(1, RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(defender.Data)) * 2);
        int remainingDamage = incomingDamage;
        int manaPerTrigger = 1; // mana_per_trigger = 1
        int manaToDamageRatio = 1; // mana_to_damage_ratio = 1

        int manaAvailable = defender.Data.CurrentMana;
        int maxAbsorb = Math.Min(manaAvailable / manaPerTrigger * manaToDamageRatio, maxShield);
        int absorbed = Math.Min(maxAbsorb, remainingDamage);
        if (absorbed > 0)
        {
            int manaCost = (absorbed + manaToDamageRatio - 1) / manaToDamageRatio * manaPerTrigger;
            defender.Data.CurrentMana -= manaCost;
            remainingDamage -= absorbed;
        }

        return remainingDamage;
    }

    // ========================================================================
    // 移动类钩子 (MoveCommand 移动完成后调用)
    // ========================================================================

    /// <summary>
    /// 记录移动并触发移动相关被动效果
    /// 在 ConsumeAp 之后、返回 CommandResult 之前调用
    /// </summary>
    public static void OnMoveCompleted(Unit unit, int cellsMoved, float actualApCost)
    {
        if (unit.Data?.Runtime == null) return;

        var rt = unit.Data.Runtime;

        // 累计移动格数
        rt.CareerMovedCellsThisTurn += cellsMoved;
        rt.CareerMovedCellsThisCombat += cellsMoved;

        // 战争之风: 移动 5 格后本回合下一次攻击必定暴击
        if (unit.HasCareerSkillEffect("storm_banner_move_5_crit")
            && rt.CareerMovedCellsThisTurn >= 5
            && !rt.CareerNextAttackGuaranteedCrit)
        {
            rt.CareerNextAttackGuaranteedCrit = true;
        }

        // 风语者: 每移动 1 格, 本场战斗暴击率 +1% (最多 +50%)
        if (unit.HasCareerSkillEffect("windwalker_fixed_move_crit"))
        {
            rt.CareerWindwalkerCritBonus = Math.Min(rt.CareerWindwalkerCritBonus + cellsMoved, 50);
        }

        // 荒原之心: 累计移动超过 10 格, 触发狂暴 (3 回合 +50% 近战伤害, 仅触发一次)
        if (unit.HasCareerSkillEffect("warchief_same_height_charge")
            && rt.CareerMovedCellsThisCombat > 10
            && !rt.CareerWarchiefDamageBonusTriggered)
        {
            rt.CareerWarchiefDamageBonusTurns = 3;
            rt.CareerWarchiefDamageBonusTriggered = true;
        }

        // 征服者: 累计移动 ≥5 标记 AOE 近战待定
        CheckConquerorAoe(unit);

        // 战斗牧师: 移动后相邻友军下一击 +10%
        ApplyCrusaderMoveBuff(unit);
    }

    /// <summary>
    /// 修正移动 AP 消耗 (风语者/灵风秘庭固定 1 AP/格)
    /// 返回修正后的 AP 消耗值
    /// </summary>
    public static float ModifyMoveApCost(Unit unit, float baseApCost, int cellsMoved)
    {
        // 山岳之王: 相邻敌人移动 +1 AP/格 (对所有移动者生效, 包括风语者)
        float mtPenalty = 0f;
        if (_allUnits != null)
        {
            foreach (var other in _allUnits)
            {
                if (other == unit) continue;
                if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
                if (other.IsPlayerSide == unit.IsPlayerSide) continue; // 必须是敌方
                if (other.Data?.Runtime?.CareerMountainThroneTurns > 0 && unit.DistanceTo(other) <= 1)
                {
                    mtPenalty = cellsMoved * 1f;
                    break;
                }
            }
        }

        // 风语者: 移动消耗固定为 1
        if (unit.HasCareerSkillEffect("windwalker_fixed_move_crit"))
            return cellsMoved * 1f + mtPenalty;

        // 灵风秘庭: 移动消耗固定为 1
        if (unit.HasCareerSkillEffect("zephyr_master_move_spell"))
            return cellsMoved * 1f + mtPenalty;

        return baseApCost + mtPenalty;
    }

    // ========================================================================
    // 法术类钩子 (SpellManager 调用)
    // ========================================================================

    /// <summary>
    /// 修正法术伤害
    /// </summary>
    public static int ModifySpellDamage(Unit caster, int baseDamage)
    {
        int damage = baseDamage;
        if (caster.Data?.Runtime == null) return damage;

        // 法师: 法术伤害 +15%
        if (caster.HasCareerSkillEffect("mage_spell_damage"))
            damage = (int)(damage * 1.15f);

        // 魔武者: 近战命中后, 下一次法术伤害 +8%/层(最多 3 层); 触发后清空近战层
        if (caster.HasCareerSkillEffect("spellweaver_melee_spell_cycle")
            && caster.Data.Runtime.CareerSpellweaverMeleeStacks > 0)
        {
            damage = (int)(damage * GetSpellweaverMeleeToSpellMultiplier(caster));
            caster.Data.Runtime.CareerSpellweaverMeleeStacks = 0;
        }

        // 贤者: 法力值阈值加成
        if (caster.HasCareerSkillEffect("sage_mana_threshold_damage"))
        {
            float manaPct = caster.Data != null && CombatStats.GetMaxMana(caster.Data) > 0
                ? (float)caster.Data.CurrentMana / CombatStats.GetMaxMana(caster.Data)
                : 0f;
            if (manaPct >= 1f)
                damage = (int)(damage * 1.5f);
            else if (manaPct > 0.5f)
                damage = (int)(damage * 1.3f);
        }

        // 毁灭王冠: 基础 +40%, 每损失 10% HP 额外 +5% (最多 +30%)
        if (caster.HasCareerSkillEffect("crown_of_ruin_low_hp_spell"))
        {
            float hpPct = caster.Model.GetMaxHp() > 0
                ? (float)caster.CurrentHp / caster.Model.GetMaxHp()
                : 1f;
            float lostPct = 1f - hpPct;
            int tenPctSteps = (int)(lostPct * 10);
            int perLostBonus = Math.Min(tenPctSteps * 5, 30);
            int totalPct = 40 + perLostBonus;
            damage = (int)(damage * (1.0f + totalPct / 100.0f));
        }

        // 焰风之怒: 本回合法术伤害累计加成 (每层 +30%)
        if (caster.HasCareerSkillEffect("tempest_wrath_spell_chain") && caster.Data?.Runtime != null)
        {
            float bonus = caster.Data.Runtime.CareerSpellDamageBonusThisTurn;
            if (bonus > 0f)
                damage = (int)(damage * (1.0f + bonus));
        }

        // 灵风秘庭: 每移动 1 格 +10% (最多 +50%)
        if (caster.HasCareerSkillEffect("zephyr_master_move_spell") && caster.Data?.Runtime != null)
        {
            int moved = caster.Data.Runtime.CareerMovedCellsThisTurn;
            int cappedMoved = Math.Min(moved, 5); // 最多 50%
            float cellBonus = cappedMoved * 0.10f;
            if (cellBonus > 0f)
                damage = (int)(damage * (1.0f + cellBonus));
        }

        // 天选者: 法术可以暴击 (由 RollSpellCritical 处理)
        // (see RollSpellCritical below)

        // 秘院贤师: 自身和相邻友军法术伤害 +30%
        if (caster.HasCareerSkillEffect("archsage_ally_spell_damage"))
            damage = (int)(damage * 1.30f);
        else if (_allUnits != null)
        {
            // 扫描相邻友军中的秘院贤师
            foreach (var ally in _allUnits)
            {
                if (ally == caster) continue;
                if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
                if (ally.IsPlayerSide != caster.IsPlayerSide) continue;
                if (!ally.HasCareerSkillEffect("archsage_ally_spell_damage")) continue;
                if (caster.DistanceTo(ally) <= 1)
                {
                    damage = (int)(damage * 1.30f);
                    break;
                }
            }
        }

        return damage;
    }

    /// <summary>
    /// 天选者: 法术暴击检定 (仅天选者的法术可以暴击)
    /// 在 SpellManager 中 damage 计算后、伤害修正前调用
    /// </summary>
    public static bool RollSpellCritical(Unit caster)
    {
        if (!caster.HasCareerSkillEffect("chosen_one_spell_can_crit"))
            return false;
        if (caster.Data == null) return false;
        int threshold = caster.Model.GetCritThreshold();
        int roll = RPGRuleEngine.RollDice(1, 20);
        return roll >= threshold;
    }

    /// <summary>
    /// 修正法术伤害 — 带目标/网格上下文 (唤星者高度差增伤)
    /// 先调用 ModifySpellDamage (通用加成)，再处理唤星者高度差增伤
    /// </summary>
    public static int ModifySpellDamageAgainstTarget(Unit caster, Unit target, HexGrid grid, int baseDamage)
    {
        // 先执行通用法术伤害修正
        int damage = ModifySpellDamage(caster, baseDamage);

        // 唤星者: 高度差增伤 (每低 1 高度 +20%)
        if (caster.HasCareerSkillEffect("starcaller_height_spell") && grid != null)
        {
            var atkCell = grid.GetCell(caster.GridPos.X, caster.GridPos.Y);
            var tgtCell = grid.GetCell(target.GridPos.X, target.GridPos.Y);
            if (atkCell != null && tgtCell != null)
            {
                int heightDiff = atkCell.Elevation - tgtCell.Elevation; // 正 = 施法者更高
                if (heightDiff > 0)
                {
                    float mult = 1.0f + heightDiff * 0.20f;
                    damage = (int)(damage * mult);
                }
            }
        }

        return damage;
    }

    /// <summary>
    /// 修正法术伤害 — 根据 mana cost 豁免 (秘院贤师每回合第一次免费)
    /// </summary>
    public static int ModifySpellManaCost(Unit caster, int baseCost)
    {
        // 通用: CareerNextSpellFreeMana (由天启骑士击杀/秘院贤师回合开始设置)
        if (caster.Data?.Runtime?.CareerNextSpellFreeMana == true)
        {
            caster.Data.Runtime.CareerNextSpellFreeMana = false;
            return 0;
        }
        return baseCost;
    }

    /// <summary>
    /// 法术释放后钩子 — 用于叠层等
    /// </summary>
    public static void OnSpellCast(Unit caster)
    {
        if (caster.Data?.Runtime == null) return;

        var rt = caster.Data.Runtime;

        // 魔武者: 法术后积累法术层
        if (caster.HasCareerSkillEffect("spellweaver_melee_spell_cycle"))
        {
            rt.CareerSpellweaverSpellStacks = Math.Min(rt.CareerSpellweaverSpellStacks + 1, 3);
            // 消耗近战层的免费法术
            rt.CareerArcaneWarFreeSpellPending = false;
        }

        // 焰风之怒: 每次成功释放法术后 +30%
        if (caster.HasCareerSkillEffect("tempest_wrath_spell_chain"))
        {
            rt.CareerSpellDamageBonusThisTurn += 0.30f;
        }

        // 幻术师: 释放法术后获得 1 层幻影
        if (caster.HasCareerSkillEffect("illusionist_phantom_ac"))
        {
            rt.CareerIllusionStacks = Math.Min(rt.CareerIllusionStacks + 1, 10);
        }

        // 鏖战骑士: 释放法术后下一次近战伤害 +40%
        if (caster.HasCareerSkillEffect("arcane_war_knight_cycle"))
        {
            rt.CareerNextMeleeDamageMultiplier = 0.40f;
        }
    }

    /// <summary>
    /// 检查是否可以施法 (被沉默、万象代价等)
    /// </summary>
    public static bool CanCastSpell(Unit caster)
    {
        if (caster.Data?.Runtime == null) return true;

        // 万象代价: 无法施法
        if ((caster.Data.Runtime.CareerParagonCostsMask & (1 << 1)) != 0)
            return false;

        // 静默之刃: 相邻敌方使自身沉默
        if (_allUnits != null)
        {
            foreach (var other in _allUnits)
            {
                if (other == caster) continue;
                if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
                if (other.IsPlayerSide == caster.IsPlayerSide) continue; // 必须是敌方
                if (!other.HasCareerSkillEffect("silent_edge_adjacent_silence")) continue;
                if (caster.DistanceTo(other) <= 1)
                    return false; // 被沉默
            }
        }

        return true;
    }

    /// <summary>
    /// 单位回合结束时调用 — 用于敌法师等条件触发的效果
    /// </summary>
    public static void OnTurnEnd(Unit unit)
    {
        if (unit.Data?.Runtime == null) return;

        var rt = unit.Data.Runtime;

        // 敌法师: 若以满 AP 和满法力结束回合, 消耗所有法力, 本场免疫法术伤害
        if (unit.HasCareerSkillEffect("antimage_full_ap_mana_immune"))
        {
            float maxAp = unit.GetMaxAp();
            int maxMana = CombatStats.GetMaxMana(unit.Data);
            bool fullAp = maxAp > 0 && Math.Abs(unit.CurrentAp - maxAp) < 0.01f;
            bool fullMana = maxMana > 0 && unit.Data.CurrentMana >= maxMana;
            if (fullAp && fullMana)
            {
                rt.CareerAntiMagicActive = true;
                unit.Data.CurrentMana = 0; // 消耗所有法力
            }
        }

        // 游骑兵: 奔袭 — 本回合移动 ≥ 5 格时，相邻 1 格友军获得免费移动
        if (unit.HasCareerSkillEffect("outrider_benxi") && rt.CareerMovedCellsThisTurn >= 5)
        {
            if (_allUnits != null)
            {
                foreach (var ally in _allUnits)
                {
                    if (ally == unit) continue;
                    if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
                    if (ally.IsPlayerSide != unit.IsPlayerSide) continue;
                    if (unit.DistanceTo(ally) <= 1)
                    {
                        if (ally.Data?.Runtime != null)
                        {
                            ally.Data.Runtime.CareerFreeMoveCellsRemaining = 5;
                            ally.Data.Runtime.CareerFreeMoveNoAooCellsRemaining = 5;
                        }
                        break; // 只给一个相邻友军
                    }
                }
            }
        }

        // 影匿者: 回合结束时激活阴影斗篷
        ActivateShadowShroud(unit);
    }

    /// <summary>
    /// 检查是否可以移动 (万象代价 + 孤星之影控制区)
    /// </summary>
    public static bool CanMove(Unit unit)
    {
        if (unit.Data?.Runtime == null) return true;

        // 万象代价: 无法移动
        if ((unit.Data.Runtime.CareerParagonCostsMask & (1 << 0)) != 0)
            return false;

        // 山岳之王: 不动如山状态下无法移动
        if (unit.Data.Runtime.CareerMountainThroneTurns > 0)
            return false;

        // 孤星之影: 被锁定的目标无法离开控制区 (6 格内不能移动)
        if (IsLoneShadowBlockedFromLeaving(unit))
            return false;

        return true;
    }

    /// <summary>
    /// 检查是否可以攻击 (万象代价)
    /// </summary>
    public static bool CanAttack(Unit unit)
    {
        if (unit.Data?.Runtime == null) return true;

        // 万象代价: 无法攻击
        if ((unit.Data.Runtime.CareerParagonCostsMask & (1 << 2)) != 0)
            return false;

        return true;
    }

    // ========================================================================
    // 被动 AC 修正
    // ========================================================================

    /// <summary>
    /// 从 v1 职业被动获取 AC 加成
    /// </summary>
    public static int GetPassiveAcBonus(Unit unit)
    {
        int bonus = 0;

        // 铁幕领主: 自身和相邻友军 AC +20%(向上取整)
        if (HasAuraSourceWithin(unit, "iron_sovereign_aura_ac_pct", 1))
        {
            int baseAc = unit.Model.GetAc();
            bonus += (int)Math.Ceiling(baseAc * 0.20f);
        }

        // 幻术师: 每层幻影 AC +1
        if (unit.HasCareerSkillEffect("illusionist_phantom_ac"))
        {
            int stacks = unit.Data?.Runtime.CareerIllusionStacks ?? 0;
            bonus += stacks;
        }

        // 守御者: 自身和相邻友军 AC +2
        if (HasAuraSourceWithin(unit, "iron_commander_adjacent_ac", 1))
        {
            bonus += 2;
        }

        // 铁铸领主: 自身和相邻友军 AC +2
        if (HasAuraSourceWithin(unit, "ironbound_lord_melee_ac", 1))
        {
            bonus += 2;
        }

        return bonus;
    }

    /// <summary>
    /// 魔武者: 获取近战→法术循环加成倍率
    /// </summary>
    public static float GetSpellweaverMeleeToSpellMultiplier(Unit caster)
    {
        if (!caster.HasCareerSkillEffect("spellweaver_melee_spell_cycle"))
            return 1.0f;
        int stacks = caster.Data?.Runtime.CareerSpellweaverMeleeStacks ?? 0;
        return 1.0f + stacks * 0.08f;
    }

    /// <summary>
    /// 魔武者: 获取法术→近战循环加成倍率
    /// </summary>
    public static float GetSpellweaverSpellToMeleeMultiplier(Unit caster)
    {
        if (!caster.HasCareerSkillEffect("spellweaver_melee_spell_cycle"))
            return 1.0f;
        int stacks = caster.Data?.Runtime.CareerSpellweaverSpellStacks ?? 0;
        return 1.0f + stacks * 0.08f;
    }

    // ========================================================================
    // 孤星之影: 强制命中/暴击判定
    // ========================================================================

    /// <summary>
    /// 孤星之影: 对锁定目标强制命中
    /// </summary>
    public static bool IsLoneShadowForceHit(Unit attacker, Unit defender)
    {
        if (attacker.Data?.Runtime == null) return false;
        ulong lockedId = attacker.Data.Runtime.CareerLoneShadowLockedTarget;
        if (lockedId == 0) return false;
        return (ulong)defender.GetInstanceId() == lockedId;
    }

    /// <summary>
    /// 孤星之影: 对锁定目标强制暴击
    /// </summary>
    public static bool IsLoneShadowForceCrit(Unit attacker, Unit defender)
    {
        return IsLoneShadowForceHit(attacker, defender);
    }

    /// <summary>
    /// 孤星之影: 被锁定的目标无法离开控制区
    /// </summary>
    public static bool IsLoneShadowBlockedFromLeaving(Unit unit)
    {
        // 检查是否有任意敌人锁定自己
        if (_allUnits == null) return false;
        ulong myId = (ulong)unit.GetInstanceId();
        foreach (var other in _allUnits)
        {
            if (other == unit) continue;
            if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
            if (other.IsPlayerSide == unit.IsPlayerSide) continue; // 必须是敌方
            if (other.Data?.Runtime?.CareerLoneShadowLockedTarget == myId)
            {
                // 检查是否在控制区范围内 (6 格)
                if (other.DistanceTo(unit) <= 6)
                    return true; // 不能离开
            }
        }
        return false;
    }

    // ========================================================================
    // 通用受击钩子 (CombatResolver / SpellManager 通用)
    // ========================================================================

    /// <summary>
    /// 单位受到任何来源伤害后调用 — 用于幻术师叠层消耗等
    /// </summary>
    public static void OnDefenderHit(Unit defender)
    {
        if (defender.Data?.Runtime == null) return;

        // 幻术师: 每受击 1 次消耗 1 层幻影
        if (defender.HasCareerSkillEffect("illusionist_phantom_ac"))
        {
            if (defender.Data.Runtime.CareerIllusionStacks > 0)
                defender.Data.Runtime.CareerIllusionStacks--;
        }
    }

    /// <summary>
    /// 修正防御者受到的法术伤害 — 敌法师/元素领主法术抗性
    /// </summary>
    public static int ModifyIncomingSpellDamage(Unit defender, int incomingDamage)
    {
        if (defender.Data?.Runtime == null) return incomingDamage;

        var rt = defender.Data.Runtime;

        // 敌法师: CareerAntiMagicActive → 本场免疫法术伤害
        if (defender.HasCareerSkillEffect("antimage_full_ap_mana_immune") && rt.CareerAntiMagicActive)
        {
            return 0; // 免疫法术伤害 (标记由回合结束/战斗结束管理)
        }

        // 钢铁魔战: 本回合未移动时免疫法术伤害
        if (defender.HasCareerSkillEffect("ironweaver_stationary_spell_immune")
            && rt.CareerMovedCellsThisTurn == 0)
        {
            return 0;
        }

        return incomingDamage;
    }

    /// <summary>
    /// 誓盾卫: 检查防御者相邻是否有誓盾卫友军 → 暴击免伤
    /// 在 CombatResolver 最终暴击判定后、ApplyDamage 前调用
    /// </summary>
    public static bool ShouldNegateAdjacentAllyCrit(Unit defender)
    {
        if (_allUnits == null) return false;
        if (defender.Data?.Runtime == null) return false;

        foreach (var ally in _allUnits)
        {
            if (ally == defender) continue;
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
            if (ally.IsPlayerSide != defender.IsPlayerSide) continue;
            if (!ally.HasCareerSkillEffect("oathshield_adjacent_crit_negate")) continue;
            if (defender.DistanceTo(ally) <= 1)
                return true;
        }
        return false;
    }

    // ========================================================================
    // 血契之环: 法力/生命互换
    // ========================================================================

    /// <summary>
    /// 血契之环: 消耗法力时等额回血
    /// 在 SpellManager / CombatManager / MagicSkillHandlers 扣法力后调用
    /// </summary>
    public static void OnManaSpent(Unit unit, int spentMana)
    {
        if (spentMana <= 0) return;
        if (!unit.HasCareerSkillEffect("blood_pact_hp_mana_exchange")) return;
        if (unit.Data?.Runtime == null) return;

        int maxHp = unit.Model.GetMaxHp();
        int healAmount = System.Math.Min(spentMana, System.Math.Max(0, maxHp - unit.CurrentHp));
        if (healAmount > 0)
        {
            unit.Heal(healAmount);
        }
    }

    /// <summary>
    /// 血契之环: 受到 HP 伤害时等额回法力
    /// 在 CombatResolver / SpellManager 同步 HP 后调用
    /// </summary>
    public static void OnHpDamageTaken(Unit unit, int hpDamage)
    {
        if (hpDamage <= 0) return;
        if (!unit.HasCareerSkillEffect("blood_pact_hp_mana_exchange")) return;
        if (unit.Data?.Runtime == null) return;

        int maxMana = CombatStats.GetMaxMana(unit.Data);
        int manaGain = System.Math.Min(hpDamage, System.Math.Max(0, maxMana - unit.Data.CurrentMana));
        if (manaGain > 0)
        {
            unit.Data.CurrentMana += manaGain;
        }
    }

    // ========================================================================
    // 护甲穿透修正
    // ========================================================================

    /// <summary>
    /// 荒芜化身: 本场战斗紧邻敌人护甲 100% 穿透
    /// 秘院贤师/石像守护: 等被动穿透效果
    /// </summary>
    public static int ModifyArmorPenBonus(Unit attacker, Unit defender, int currentPenBonus)
    {
        int bonus = currentPenBonus;

        if (attacker.Data?.Runtime?.CareerWrathArmorPenActive == true)
        {
            // 相邻敌人 → 100% 穿透
            if (attacker.DistanceTo(defender) <= 1)
                bonus += 999; // 确保穿透一切护甲
        }

        // 碎石骑士: 穿甲翻倍
        if (attacker.HasCareerSkillEffect("skullcrusher_armor_pierce_double"))
        {
            bonus *= 2;
        }

        return bonus;
    }

    // ========================================================================
    // 暴击检定修正
    // ========================================================================

    /// <summary>
    /// 战争之风: 移动 5 格后命中判定时强制暴击
    /// </summary>
    public static bool IsStormBannerGuaranteedCrit(Unit attacker)
    {
        if (!attacker.HasCareerSkillEffect("storm_banner_move_5_crit"))
            return false;
        return attacker.Data?.Runtime.CareerNextAttackGuaranteedCrit == true;
    }

    // ========================================================================
    // 辅助工具
    // ========================================================================

    /// <summary>
    /// 光环判定：unit 自身或其指定范围内的同阵营友军是否拥有 effectId 职业被动。
    /// 用于"自身和相邻友军"类光环被动（战争领主/铁铸领主/守御者/磐石守护等）。
    /// </summary>
    private static bool HasAuraSourceWithin(Unit unit, string effectId, int range)
    {
        if (unit.HasCareerSkillEffect(effectId)) return true;
        if (_allUnits == null) return false;
        foreach (var other in _allUnits)
        {
            if (other == unit) continue;
            if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
            if (other.IsPlayerSide != unit.IsPlayerSide) continue;
            if (!other.HasCareerSkillEffect(effectId)) continue;
            if (unit.DistanceTo(other) <= range) return true;
        }
        return false;
    }

    /// <summary>
    /// 统计 unit 周围指定范围内的同阵营友军数量（含自身）。
    /// 用于征讨者"每个友军 +5%"类被动。
    /// </summary>
    private static int CountAlliesWithin(Unit unit, int range)
    {
        int count = 1; // 含自身
        if (_allUnits == null) return count;
        foreach (var other in _allUnits)
        {
            if (other == unit) continue;
            if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
            if (other.IsPlayerSide != unit.IsPlayerSide) continue;
            if (unit.DistanceTo(other) <= range) count++;
        }
        return count;
    }

    /// <summary>
    /// 判断单位周围指定范围内是否有友军
    /// </summary>
    private static bool IsSolo(Unit unit, int range)
    {
        if (_allUnits == null) return false; // 保守: 无战斗状态时不触发
        if (unit.Data?.Runtime == null) return false;

        foreach (var other in _allUnits)
        {
            if (other == unit) continue;
            if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
            if (other.IsPlayerSide != unit.IsPlayerSide) continue; // 只检查同阵营
            if (unit.DistanceTo(other) <= range) return false; // 有友军在范围内
        }
        return true; // 范围内无友军
    }

    // ========================================================================
    // 剑舞者: 额外攻击目标查找
    // ========================================================================

    /// <summary>
    /// 剑舞者近战命中后, 查找另一个相邻的敌方单位作为额外攻击目标
    /// 选择规则: 最低 HP → 最大 InstanceId (确定性兜底)
    /// 限制: 每回合最多触发 1 次
    /// </summary>
    public static Unit? TryGetBladeDancerExtraTarget(Unit attacker, Unit primaryTarget)
    {
        if (!attacker.HasCareerSkillEffect("blade_dancer_extra_attack"))
            return null;
        if (attacker.Data?.Runtime == null) return null;
        if (_allUnits == null) return null;

        // 每回合 1 次限制
        if (attacker.Data.Runtime.CareerBladeDancerExtraAttackUsedThisTurn)
            return null;

        Unit? best = null;
        int bestHp = int.MaxValue;
        ulong bestId = ulong.MinValue;

        foreach (var other in _allUnits)
        {
            if (other == attacker || other == primaryTarget) continue;
            if (!GodotObject.IsInstanceValid(other) || other.CurrentHp <= 0) continue;
            if (other.IsPlayerSide == attacker.IsPlayerSide) continue; // 必须是敌方
            if (attacker.DistanceTo(other) > 1) continue; // 必须相邻

            ulong otherId = (ulong)other.GetInstanceId();
            if (other.CurrentHp < bestHp || (other.CurrentHp == bestHp && otherId > bestId))
            {
                best = other;
                bestHp = other.CurrentHp;
                bestId = otherId;
            }
        }
        return best;
    }

    /// <summary>
    /// 标记剑舞者本回合额外攻击已使用 (由 CombatManager 在额外攻击完成后调用)
    /// </summary>
    public static void MarkBladeDancerExtraAttackUsed(Unit attacker)
    {
        if (attacker.Data?.Runtime == null) return;
        attacker.Data.Runtime.CareerBladeDancerExtraAttackUsedThisTurn = true;
    }

    // ========================================================================
    // 远程掩体命中惩罚修正 (CombatResolver 命中计算前调用)
    // ========================================================================

    /// <summary>
    /// 游侠: 远程攻击无视一半障碍物命中减益
    /// 在 LineOfSight.GetPathPenalty 之后, accuracyMod 累加后调用
    /// </summary>
    public static int ModifyCoverPenalty(Unit attacker, int losPenalty)
    {
        if (losPenalty >= 0) return losPenalty;

        // 游侠: 远程攻击无视一半障碍物命中减益
        if (attacker.HasCareerSkillEffect("ranger_ranged_cover_half"))
        {
            // cover_penalty_reduction = 0.5f, 减半 -> 向上取整以保证不出现 0 值溢出
            // losPenalty 为负数, *0.5 后 CeilToInt 示例: -2→-1, -4→-2
            return Mathf.CeilToInt(losPenalty * 0.5f);
        }

        // 鹰眼守卫: 无视所有掩体命中减益
        if (attacker.HasCareerSkillEffect("hawkeye_guard_cover_ignore"))
        {
            return 0;
        }

        return losPenalty;
    }

    // ========================================================================
    // 背面攻击倍率修正 (CombatResolver 伤害计算前调用)
    // ========================================================================

    /// <summary>
    /// 刺客: 背面攻击伤害 ×2
    /// 在 FacingSystem.GetFlankingBonus 之后, flankMult 传入 DamageInput 之前调用
    /// </summary>
    public static float ModifyBackstabMultiplier(Unit attacker, float flankMult)
    {
        if (flankMult < 1.5f) return flankMult; // 不是背面攻击
        if (attacker.HasCareerSkillEffect("assassin_backstab_double"))
            return flankMult * 2.0f;
        return flankMult;
    }

    // ========================================================================
    // 友军伤害光环 (CombatResolver 伤害计算前调用)
    // ========================================================================

    /// <summary>
    /// 诗人: 周围 2 格内友军伤害 +5%
    /// 扫描 _allUnits 查找范围内拥有 bard_ally_damage_aura 的诗人友军
    /// </summary>
    public static float GetAllyDamageAuraMultiplier(Unit attacker)
    {
        if (_allUnits == null) return 1.0f;

        foreach (var ally in _allUnits)
        {
            if (ally == attacker) continue;
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
            if (ally.IsPlayerSide != attacker.IsPlayerSide) continue;
            if (!ally.HasCareerSkillEffect("bard_ally_damage_aura")) continue;
            if (attacker.DistanceTo(ally) <= 2)
                return 1.05f;
        }
        return 1.0f;
    }

    // ========================================================================
    // 远程叠伤 (CombatResolver 伤害计算 + 命中后调用)
    // ========================================================================

    /// <summary>
    /// 秘射手: 对同一目标连续远程攻击叠伤，每层 +10%(最大 +30%, 换目标归零)
    /// 在 CombatResolver 伤害计算前调用，获取当前层数对应的伤害倍率
    /// </summary>
    public static float GetArcaneArcherDamageMultiplier(Unit attacker)
    {
        if (!attacker.HasCareerSkillEffect("arcane_archer_sniper_stack")) return 1.0f;
        int stacks = attacker.Data?.Runtime.CareerArcaneArcherStacks ?? 0;
        return 1.0f + Math.Min(stacks, 3) * 0.10f;
    }

    /// <summary>
    /// 秘射手: 命中后跟踪目标。同一目标叠层，换目标归零
    /// 在 CombatResolver ApplyDamage 后调用
    /// </summary>
    public static void OnArcaneArcherHit(Unit attacker, Unit defender)
    {
        if (!attacker.HasCareerSkillEffect("arcane_archer_sniper_stack")) return;
        var rt = attacker.Data?.Runtime;
        if (rt == null) return;

        ulong defId = (ulong)defender.GetInstanceId();
        if (rt.CareerArcaneArcherLastTargetId == defId)
        {
            // 同一目标 → 叠层
            rt.CareerArcaneArcherStacks = Math.Min(rt.CareerArcaneArcherStacks + 1, 3);
        }
        else
        {
            // 换目标 → 归零
            rt.CareerArcaneArcherLastTargetId = defId;
            rt.CareerArcaneArcherStacks = 1;
        }
    }

    // ========================================================================
    // 未命中返还 AP (CombatResolver 命中判定后调用)
    // ========================================================================

    /// <summary>
    /// 浪客: 未命中返还 AP
    /// </summary>
    public static void OnMissApRefund(Unit attacker)
    {
        if (attacker.Data?.Runtime == null) return;
        if (!attacker.HasCareerSkillEffect("rogue_miss_ap_refund")) return;

        int weaponAp = attacker.Model.GetMainHand() is WeaponData w ? w.ApCost : 4;
        attacker.CurrentAp = Math.Min(attacker.CurrentAp + weaponAp, attacker.GetMaxAp());
    }

    // ========================================================================
    // 降 AC 诅咒 (CombatResolver AC 计算时调用)
    // ========================================================================

    /// <summary>
    /// 术士: 降 AC 诅咒 — 被攻击时目标 AC -2
    /// 在 defender.GetEffectiveAc 后调用
    /// </summary>
    public static int GetSorcererAcCurseReduction(Unit attacker, int currentAc)
    {
        if (!attacker.HasCareerSkillEffect("sorcerer_ac_curse")) return currentAc;
        return currentAc - 2;
    }

    // ========================================================================
    // 虚空骑士: 额外 AP 伤害 (CombatResolver 伤害计算前调用)
    // ========================================================================

    /// <summary>
    /// 深渊骑士: 每剩余 1 AP 伤害 +8% (上限 +40%)
    /// 在 FinalMultiplier 中组合
    /// </summary>
    public static float GetVoidKnightApDamageMultiplier(Unit attacker)
    {
        if (!attacker.HasCareerSkillEffect("void_knight_extra_ap_damage")) return 1.0f;
        int ap = Mathf.RoundToInt(attacker.CurrentAp);
        float bonus = Math.Min(ap, 5) * 0.08f;
        return 1.0f + bonus;
    }

    // ========================================================================
    // 先知: 致死伤害保护 (ModifyIncomingDamageMultiplier 中调用)
    // ========================================================================

    /// <summary>
    /// 先知: 周围 2 格内友军受到致命伤害时 -20%
    /// 在被击中后的伤害减免阶段调用
    /// </summary>
    public static void ApplyProphetProtection(Unit defender, ref int hpDamage)
    {
        if (_allUnits == null || hpDamage <= 0 || defender.CurrentHp > hpDamage) return;

        foreach (var ally in _allUnits)
        {
            if (ally == defender) continue;
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
            if (ally.IsPlayerSide != defender.IsPlayerSide) continue;
            if (!ally.HasCareerSkillEffect("prophet_fatal_ally_protect")) continue;
            if (defender.DistanceTo(ally) > 2) continue;

            // 先知在范围内，减少 20% 致命伤害
            hpDamage = (int)(hpDamage * 0.8f);
            if (hpDamage < 1) hpDamage = 1;
            return;
        }
    }

    // ========================================================================
    // 征服者: 移动 5+ → AOE 近战 (OnMoveCompleted / CombatResolver 调用)
    // ========================================================================

    /// <summary>
    /// 征服者: 累计移动 ≥5 格时标记 pending
    /// </summary>
    private static void CheckConquerorAoe(Unit unit)
    {
        var rt = unit.Data?.Runtime;
        if (rt == null) return;
        if (!unit.HasCareerSkillEffect("conqueror_move_5_aoe_melee")) return;
        if (rt.CareerMovedCellsThisTurn >= 5 && !rt.CareerConquerorAoeMeleePending)
            rt.CareerConquerorAoeMeleePending = true;
    }

    /// <summary>
    /// 征服者: 使用 AOE 近战后消费标记
    /// </summary>
    public static bool ConsumeConquerorAoe(Unit attacker)
    {
        if (attacker.Data?.Runtime == null) return false;
        if (!attacker.HasCareerSkillEffect("conqueror_move_5_aoe_melee")) return false;
        if (!attacker.Data.Runtime.CareerConquerorAoeMeleePending) return false;
        attacker.Data.Runtime.CareerConquerorAoeMeleePending = false;
        return true;
    }

    // ========================================================================
    // 魔王: 敌人命中减益 (命中计算前调用)
    // ========================================================================

    /// <summary>
    /// 魔王: 周围 2 格内敌人命中 -10%; 受伤的敌人 -20%
    /// 被攻击时检查攻击者(敌人)与魔王距离
    /// 返回命中减益值 (整数命中点数)
    /// </summary>
    public static int GetOverlordAccuracyDebuff(Unit defender, Unit attacker)
    {
        if (_allUnits == null) return 0;

        foreach (var ally in _allUnits)
        {
            if (ally == defender) continue;
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
            if (ally.IsPlayerSide != defender.IsPlayerSide) continue;
            if (!ally.HasCareerSkillEffect("overlord_enemy_accuracy_debuff")) continue;
            // 攻击者(敌人)在魔王 2 格范围内才受减益
            if (attacker.DistanceTo(ally) > 2) continue;

            // 受到攻击的 defender 是敌人 (ally.IsPlayerSide != attacker.IsPlayerSide)
            // 范围 2 内有魔王 ally，减命中
            bool isInjured = ally.CurrentHp < ally.GetMaxHp();
            // -10% = -2 命中点数; -20% = -4 命中点数
            return isInjured ? -4 : -2;
        }
        return 0;
    }

    // ========================================================================
    // 战斗牧师: 移动→相邻友军下一击 +10% (OnMoveCompleted / CombatResolver)
    // ========================================================================

    /// <summary>
    /// 战斗牧师: 移动后范围内友军下一击伤害 +10%
    /// 在 OnMoveCompleted 中调用
    /// </summary>
    private static void ApplyCrusaderMoveBuff(Unit unit)
    {
        if (!unit.HasCareerSkillEffect("crusader_move_ally_damage")) return;
        if (_allUnits == null) return;

        foreach (var ally in _allUnits)
        {
            if (ally == unit) continue;
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0) continue;
            if (ally.IsPlayerSide != unit.IsPlayerSide) continue;
            if (unit.DistanceTo(ally) > 1) continue;

            // 标记友军下一击 +10%
            var allyRt = ally.Data?.Runtime;
            if (allyRt == null) continue;
            allyRt.CareerNextAttackDamageMultiplier = Math.Max(
                allyRt.CareerNextAttackDamageMultiplier, 0.10f);
        }
    }

    // ========================================================================
    // 影匿者: 阴影斗篷 (OnTurnEnd / CombatResolver)
    // ========================================================================

    /// <summary>
    /// 影匿者: 回合结束时激活阴影斗篷
    /// </summary>
    public static void ActivateShadowShroud(Unit unit)
    {
        if (!unit.HasCareerSkillEffect("shadow_shroud_cover_cloak")) return;
        if (unit.Data?.Runtime == null) return;
        unit.Data.Runtime.CareerShadowShroudActive = true;
    }

    /// <summary>
    /// 影匿者: 斗篷激活时敌人远程命中 -2 (命中点数)
    /// 在攻击者的 accuracyMod 累加后调用
    /// </summary>
    public static int GetShadowShroudDefenseBonus(Unit defender)
    {
        if (defender.Data?.Runtime?.CareerShadowShroudActive != true) return 0;
        return -2; // 敌人远程命中 -2
    }

    /// <summary>
    /// 影匿者: 斗篷激活时下一次远程攻击命中 +2, 伤害 +15%
    /// </summary>
    public static (int hitBonus, float damageMult) GetShadowShroudOffenseBonus(Unit attacker)
    {
        if (attacker.Data?.Runtime?.CareerShadowShroudActive != true) return (0, 1.0f);
        return (2, 1.15f);
    }

    /// <summary>
    /// 影匿者: 使用远程攻击后消耗斗篷进攻加成
    /// </summary>
    public static void ConsumeShadowShroudOffense(Unit attacker)
    {
        if (attacker.Data?.Runtime?.CareerShadowShroudActive == true)
            attacker.Data.Runtime.CareerShadowShroudActive = false;
    }

    // ========================================================================
    // 钢弦骑士: 近战攻击后免费切换+远程 (OnMeleeHit / 武器切换系统)
    // ========================================================================

    /// <summary>
    /// 钢弦骑士: 近战命中后设置免费切换+远程待定
    /// </summary>
    public static void ApplySteelstringKnightFreeSwitch(Unit attacker)
    {
        if (!attacker.HasCareerSkillEffect("steelstring_knight_switch_free")) return;
        if (attacker.Data?.Runtime == null) return;
        attacker.Data.Runtime.CareerWeaponSwitchAndRangedFreePending = true;
    }
}
