using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Events;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 战斗结算器 — Frontend 适配层
/// 从 Node 层收集数据，委托 CombatRuleEngine (Core) 执行纯规则计算，
/// 然后应用结果到 Node（HP 同步、VFX、EventBus）。
/// </summary>
public static class CombatResolver
{
    // 剑舞者额外攻击递归守卫
    private static bool _isBladeDancerExtraResolving = false;

    // ============================================================================
    // 主攻击解析
    // ============================================================================

    /// <summary>完整攻击结算管道</summary>
    /// <param name="attackerAllies">
    /// 攻击者同阵营单位数组（用于包围、指挥光环命中加成）。null = 跳过这些加成。
    /// </param>
    /// <param name="defenderAllies">
    /// 防御者同阵营单位数组（用于指挥光环 AC 加成）。null = 跳过该加成。
    /// </param>
    public static Godot.Collections.Dictionary ResolveAttack(
        Unit attacker, Unit defender, HexGrid? grid = null,
        bool isCharge = false, bool isAoo = false,
        int accuracyMod = 0, float damageMultiplier = 1.0f,
        Unit[]? attackerAllies = null,
        float nodePassiveScale = 1.0f,
        Unit[]? defenderAllies = null,
        float extraCritChance = 0f,
        float targetAcMultiplier = 1.0f)
    {
        var weapon = attacker.Model.GetMainHand() as WeaponData;
        bool isChargeValid = isCharge && weapon != null && !weapon.IsRanged && !weapon.IsCatalyst;
        var result = new Godot.Collections.Dictionary
        {
            { "attacker", attacker },
            { "defender", defender },
            { "hit", false },
            { "critical", false },
            { "fumble", false },
            { "graze", false },
            { "damage", 0 },
            { "roll", 0 },
            { "attack_bonus", 0 },
            { "total_attack", 0 },
            { "target_ac", 0 },
            { "modifiers", new Godot.Collections.Dictionary() },
            { "removes_effects", new string[] { } },
            { "advantage", false },
            { "disadvantage", false },
            { "is_counter", false },
            { "is_flanking", false },
            { "flank_direction", "front" },
            { "is_charge", isChargeValid }
        };

        var modifiers = (Godot.Collections.Dictionary)result["modifiers"];

        if (!BuffTargetingRules.IsDirectlyTargetable(defender))
        {
            result["blocked_by_buff"] = true;
            result["reason"] = "目标不可被直接指定";
            return result;
        }

        // ===== 1. 收集修正（Node 层数据采集）=====
        if (BuffAttackHooks.TryResolvePhantomInterception(defender, result))
            return result;

        if (CombatAttackRules.IsMeleeElevationBlocked(attacker, defender, grid))
        {
            result["blocked_by_elevation"] = true;
            result["reason"] = CombatAttackRules.MeleeElevationBlockedReason;
            return result;
        }

        bool isMeleeAttack = weapon == null || !weapon.IsRanged;
        bool isRangedAttack = !isMeleeAttack;
        int attackDistance = HexUtils.Distance(attacker.GridPos.X, attacker.GridPos.Y, defender.GridPos.X, defender.GridPos.Y);

        int attackBonus = attacker.Model.GetAttackBonus();
        if (attacker.Data != null)
            attackBonus = SkillTreeKeystoneResolver.ApplyAttackBonus(
                attacker.Data, attackBonus, isMeleeAttack, isRangedAttack, attackDistance);
        if (defender.Data != null)
            attackBonus = SkillTreeKeystoneResolver.ApplyIncomingAttackBonus(defender.Data, attackBonus, isRangedAttack);
        int commandAuraAttackBonus = PassiveSkillResolver.GetIncomingCommandAuraAttackBonus(attacker, attackerAllies);
        if (commandAuraAttackBonus != 0)
        {
            attackBonus += commandAuraAttackBonus;
            modifiers["command_aura_attack"] = commandAuraAttackBonus;
        }
        result["attack_bonus"] = attackBonus;

        bool hasAdvantage = false;
        bool hasDisadvantage = false;
        if (isRangedAttack && defender.Data != null
            && BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(defender.Data, "ranged_hit_taken") < 0f)
        {
            hasDisadvantage = true;
            modifiers["ranged_hit_taken"] = true;
        }

        // 高地优势
        if (grid != null)
        {
            var hgResult = LineOfSight.GetHighGroundBonus(attacker.GridPos, defender.GridPos, grid);
            if (hgResult.Advantage) { hasAdvantage = true; modifiers["high_ground"] = true; }
            if (hgResult.Disadvantage) { hasDisadvantage = true; modifiers["low_ground"] = true; }
        }

        // 冲锋优势 — 仅重战士(Juggernaut)职业冲锋有优势
        bool isJuggernautCharge = isChargeValid
            && CareerSkillResolver.HasV1CareerSkill(attacker, "juggernaut_charge_damage");
        if (isJuggernautCharge) { hasAdvantage = true; modifiers["charge"] = true; }

        // v0.8 E5: buff attack_advantage
        if (attacker.Data != null)
        {
            var advMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(attacker.Data, "attack_advantage");
            if (advMod.OverrideValue.HasValue && advMod.OverrideValue.Value >= 1f)
            { hasAdvantage = true; modifiers["career_advantage"] = true; }
        }

        // v0.8 E6: attacker_disadvantage_while_phantom
        if (defender.Data != null)
        {
            var phantomMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(defender.Data, "attacker_disadvantage_while_phantom");
            if (phantomMod.OverrideValue.HasValue && phantomMod.OverrideValue.Value >= 1f)
            { hasDisadvantage = true; modifiers["phantom_disadvantage"] = true; }
        }

        // 掩体惩罚（远程攻击时）—— 视野系统已移除，改为路径上累计命中惩罚

        // 弹药检查（远程武器消耗弹药）
        if (weapon != null && weapon.NeedsAmmo && !weapon.ConsumeAmmo())
        {
            result["hit"] = false;
            result["out_of_ammo"] = true;
            return result;
        }

        if (weapon != null && weapon.IsRanged && grid != null)
        {
            int losPenalty = LineOfSight.GetPathPenalty(
                attacker.GridPos, defender.GridPos, grid, attacker, defender);
            if (losPenalty < 0)
            {
                losPenalty = CareerPassiveHooks.ModifyCoverPenalty(attacker, losPenalty);
                accuracyMod += losPenalty;
                modifiers["los_path_penalty"] = losPenalty;
            }
        }

        // 高度差命中修正：每级 ±5%（高打低加，低打高减）
        if (grid != null)
        {
            var atkCell = grid.GetCell(attacker.GridPos.X, attacker.GridPos.Y);
            var defCell = grid.GetCell(defender.GridPos.X, defender.GridPos.Y);
            if (atkCell != null && defCell != null)
            {
                int elevDiff = atkCell.Elevation - defCell.Elevation; // 正=高打低
                if (elevDiff != 0)
                {
                    int elevMod = elevDiff; // 每级 ±1 点（对应 ±5% 命中率）
                    accuracyMod += elevMod;
                    modifiers["elevation_mod"] = elevMod;
                }
            }
        }

        // 渡河惩罚
        if (grid != null && LineOfSight.HasRiverCrossingPenalty(attacker.GridPos, defender.GridPos, grid))
        {
            hasDisadvantage = true; modifiers["river_crossing"] = true;
        }

        // 早期包夹检测（用于命中加成 — 真正伤害倍率在第 5 步重新计算）
        bool earlyIsFlanking = false;
        if (!isAoo)
        {
            var earlyFlank = FacingSystem.GetFlankingBonus(attacker.GridPos, defender);
            earlyIsFlanking = earlyFlank.DamageMultiplier > 1.0f;
        }
        if (earlyIsFlanking)
        {
            int flankHitBonus = BladeHex.Combat.Abilities.UnitAbilities.GetTotalFlankingHitBonus(attacker.Data);
            if (flankHitBonus != 0)
            {
                accuracyMod += flankHitBonus;
                modifiers["flank_hit_bonus"] = flankHitBonus;
            }
        }

        // 包围加成 (v0.6 8.2 + v1 5.2)：每多 1 个不同方向友军贴脸，命中 +1，
        // 4+ 时目标 AC -2 且伤害 +10%。
        int surroundAcReduction = 0;
        float surroundDamageBonus = 0f;
        if (attackerAllies != null && attackerAllies.Length > 0 && !isAoo)
        {
            var sb = FacingSystem.GetSurroundingBonus(defender, attackerAllies);
            if (sb.HitBonus != 0)
            {
                accuracyMod += sb.HitBonus;
                modifiers["surround_hit"] = sb.HitBonus;
            }
            if (sb.AcReduction > 0)  modifiers["surround_ac_reduction"] = sb.AcReduction;
            if (sb.DamageBonus > 0f) modifiers["surround_damage_bonus"]  = sb.DamageBonus;
            surroundAcReduction = sb.AcReduction;
            surroundDamageBonus = sb.DamageBonus;
        }

        // 伤势惩罚 (v0.6 2.3)：HP 低于 50% / 25% 时全检定 -1 / -2，应用到攻击命中。
        if (attacker.Data != null)
        {
            float atkHpPct = attacker.Model.GetMaxHp() > 0
                ? (float)attacker.CurrentHp / attacker.Model.GetMaxHp()
                : 1.0f;
            var wound = RPGRuleEngine.GetWoundPenalty(atkHpPct);
            int penalty = wound.ContainsKey("all_checks") ? wound["all_checks"].AsInt32() : 0;
            if (penalty != 0)
            {
                accuracyMod += penalty;
                modifiers["wound_penalty"] = penalty;
            }
        }

        // 暴击阈值由 WIS 暴击曲线决定（见 CombatStats.GetCritThreshold）。
        // v0.6 文档没有"士气进一步降低暴击阈值"的设计，移除旧版双重叠加避免
        // 高 WIS + 高士气角色暴击爆炸。
        int critThreshold = attacker.Model.GetCritThreshold();

        // v0.8: 铁血暴君-暴君之怒 → 暴击阈值固定为 20
        if (CareerSkillResolver.HasFixedCritThreshold(attacker))
            critThreshold = CareerSkillResolver.GetFixedCritThreshold();

        // v0.8: 处刑者-终结宣告 → 目标 HP≤30% 时额外降低暴击阈值
        if (CareerSkillResolver.HasDeathSentence(attacker))
            critThreshold -= CareerSkillResolver.GetDeathSentenceCritReduction(defender);

        // ===== 2. 委托 Core 层执行攻击检定 =====
        // 节点暴击率：技能盘 critical_rate 节点等独立暴击概率（v0.6 11.5）
        float bonusCritChance = 0f;
        if (attacker.Data != null)
            bonusCritChance += SkillTreeKeystoneResolver.GetBonusCritChance(
                attacker.Data, weapon, isRangedAttack, attacker.HasMoved);
        if (attacker.Data != null)
            bonusCritChance += GetAttackerCriticalRateBuffBonus(attacker, defender);
        if (attacker.Data != null && defender.Data != null)
            bonusCritChance += GetMarkedTargetCritBonus(attacker, defender);
        bonusCritChance += extraCritChance;
        // v1 职业被动: 暴击率加成 (苦修者/审判官/风语者)
        bonusCritChance += CareerPassiveHooks.ModifyCritRateBonus(attacker, 0f);
        // v1 职业被动: 孤刃之誓 — 未受伤害时暴击率 +15%
        bonusCritChance += CareerPassiveHooks.GetLoneBladeUnharmedCritBonus(attacker);
        // v1 职业被动: 影匿者 — 阴影斗篷进攻 (斗篷激活时远程命中 +2)
        {
            var (shroudHit, _) = CareerPassiveHooks.GetShadowShroudOffenseBonus(attacker);
            if (shroudHit != 0 && weapon != null && weapon.IsRanged)
                accuracyMod += shroudHit;
        }
        // v1 职业被动: 命中加成 (战誓者/孤刃之誓/猎人对受伤)
        accuracyMod += CareerPassiveHooks.ModifyHitBonus(attacker, 0);
        accuracyMod += CareerPassiveHooks.ModifyHitBonusVsDefender(attacker, defender, 0);
        // v1 职业被动: 魔王 — 敌人命中减益 (检查防御者周围是否有魔王)
        accuracyMod += CareerPassiveHooks.GetOverlordAccuracyDebuff(defender, attacker);
        // v1 职业被动: 影匿者 — 阴影斗篷防御 (远程攻击命中 -2)
        if (weapon != null && weapon.IsRanged)
            accuracyMod += CareerPassiveHooks.GetShadowShroudDefenseBonus(defender);

        int commandAuraAcBonus = PassiveSkillResolver.GetIncomingCommandAuraAcBonus(defender, defenderAllies);
        if (commandAuraAcBonus != 0)
            modifiers["command_aura_ac"] = commandAuraAcBonus;

        int baseTargetAc = CareerPassiveHooks.GetSorcererAcCurseReduction(attacker, defender.GetEffectiveAc(attacker) + commandAuraAcBonus) - surroundAcReduction;
        if (targetAcMultiplier != 1.0f)
        {
            baseTargetAc = Math.Max(0, (int)MathF.Ceiling(baseTargetAc * targetAcMultiplier));
            modifiers["target_ac_multiplier"] = targetAcMultiplier;
        }

        var attackInput = new CombatRuleEngine.AttackInput
        {
            AttackBonus = attackBonus,
            TargetAc = baseTargetAc,
            CritThreshold = critThreshold,
            HasAdvantage = hasAdvantage,
            HasDisadvantage = hasDisadvantage,
            AccuracyMod = accuracyMod,
            CoverAcBonus = 0,           // 视野系统已移除：掩体惩罚已折叠到 AccuracyMod
            BonusCritChance = bonusCritChance,
        };
        if (attacker.Data != null)
            SkillTreeKeystoneResolver.ApplyAttackRollRules(attacker.Data, ref attackInput, isMeleeAttack);

        var rollResult = CombatRuleEngine.RollAttack(in attackInput);

        // 写回结果字典
        result["roll"] = rollResult.NaturalRoll;
        result["target_ac"] = rollResult.FinalTargetAc;
        result["total_attack"] = rollResult.TotalAttack;
        result["hit_chance_percent"] = rollResult.HitChancePercent;
        result["critical"] = rollResult.IsCritical;
        // v1 职业被动: 战争之风 — 移动5格后必定暴击覆盖
        if (CareerPassiveHooks.IsStormBannerGuaranteedCrit(attacker))
            result["critical"] = true;
        result["fumble"] = rollResult.IsFumble;
        result["hit"] = rollResult.IsHit;
        result["graze"] = rollResult.IsGraze;
        result["advantage"] = hasAdvantage && !hasDisadvantage;
        result["disadvantage"] = hasDisadvantage && !hasAdvantage;
        if (accuracyMod != 0) modifiers["skill_mod"] = accuracyMod;

        // v1 职业被动: 孤星之影 — 对锁定目标强制命中/暴击
        if (CareerPassiveHooks.IsLoneShadowForceHit(attacker, defender))
        {
            result["hit"] = true;
            if (CareerPassiveHooks.IsLoneShadowForceCrit(attacker, defender))
                result["critical"] = true;
        }

        if (attacker.Data != null)
        {
            var forceHit = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(attacker.Data, "force_attack_hit");
            if (forceHit.FlatBonus > 0f || forceHit.OverrideValue.GetValueOrDefault() > 0f)
            {
                result["hit"] = true;
                modifiers["force_attack_hit"] = true;
            }

            var forceCrit = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(attacker.Data, "force_attack_crit");
            if (forceCrit.FlatBonus > 0f || forceCrit.OverrideValue.GetValueOrDefault() > 0f)
            {
                result["hit"] = true;
                result["critical"] = true;
                modifiers["force_attack_crit"] = true;
            }
        }

        // v1 职业被动: 铁血之令 — 未命中转为暴击 (消耗一层)
        if (!result["hit"].AsBool() && !rollResult.IsFumble
            && attacker.Data?.Runtime?.CareerIronEdictPendingCount > 0)
        {
            attacker.Data.Runtime.CareerIronEdictPendingCount--;
            result["hit"] = true;
            result["critical"] = true;
        }

        // v1 职业被动: 浪客 — 未命中返还 AP
        if (!result["hit"].AsBool())
        {
            CareerPassiveHooks.OnMissApRefund(attacker);
            RemoveRangedActionRefundWindow(attacker, weapon);
        }

        if (!result["hit"].AsBool()) return result;

        // 最终暴击判定: 整合所有强制暴击覆盖
        bool finalCritical = result["critical"].AsBool();

        // ===== 3. 收集伤害修正（Node 层数据采集）=====
        var damageInfo = attacker.Model.RollDamage();
        int baseDamage = (int)damageInfo["total"];

        // 箭筒伤害加成
        if (weapon != null && weapon.IsRanged && !weapon.IsThrowing)
        {
            var offHand = attacker.Data?.PrimaryOffHand;
            if (offHand != null && offHand.IsQuiver)
                baseDamage += offHand.QuiverDamageBonus;
        }

        // 偷袭
        int sneakDice = PassiveSkillResolver.GetSneakAttackDice(attacker, hasAdvantage && !hasDisadvantage);
        int sneakDamage = sneakDice > 0 ? RPGRuleEngine.RollDice(sneakDice, PassiveSkillResolver.GetSneakAttackSides()) : 0;

        // 包夹
        float flankMult = 1.0f;
        if (!isAoo)
        {
            var flankBonus = FacingSystem.GetFlankingBonus(attacker.GridPos, defender);
            flankMult = flankBonus.DamageMultiplier;

            if (flankMult > 1.0f)
            {
                result["is_flanking"] = true;
                result["flank_direction"] = flankMult < 1.5f ? "flank" : "rear";
            }

            // v1 职业被动: 刺客 — 背面攻击伤害 ×2
            flankMult = CareerPassiveHooks.ModifyBackstabMultiplier(attacker, flankMult);
        }

        // 冲锋伤害倍率 — 仅重战士(Juggernaut)职业冲锋有额外伤害
        float chargeMult = 1.0f;
        if (isJuggernautCharge)
        {
            var chargeBonus = FacingSystem.GetChargeBonus(attacker, true);
            chargeMult = chargeBonus.DamageMultiplier;
        }

        // ===== 4. 委托 Core 层计算伤害 =====
        // v0.6 11.4.1 节点平伤 AP 归一化: NodeDamage * WeaponAP / 4
        // 防止低 AP 多段武器从 +1 melee_damage 节点过度获利。
        // v0.6 11.4.2 / 11.8: AOE / 多段技能再乘 nodePassiveScale (一般 0.5)
        int weaponApForNode = weapon?.ApCost ?? 4;
        int rawPassiveMelee  = PassiveSkillResolver.GetPassiveMeleeDamageBonus(attacker);
        int rawPassiveRanged = PassiveSkillResolver.GetPassiveRangedDamageBonus(attacker);
        int passiveMelee  = (int)(((rawPassiveMelee  * weaponApForNode) / 4) * nodePassiveScale);
        int passiveRanged = (int)(((rawPassiveRanged * weaponApForNode) / 4) * nodePassiveScale);
        bool isMelee = isMeleeAttack;
        int passiveDamageBonus = isMelee ? passiveMelee : passiveRanged;
        // 远程伤害也要进入伤害计算（CombatRuleEngine 没有专门的 PassiveRangedBonus 字段，
        // 直接折算入 BaseDamage 上层）
        if (!isMelee && passiveRanged != 0) baseDamage += passiveRanged;

        // v1 职业被动: 近战伤害 flat 加成 (守护骑士/魔剑士)
        int careerMeleeBonus = isMelee ? CareerPassiveHooks.ModifyMeleeDamageBonus(attacker, defender, 0) : 0;

        // v1 职业被动: 武圣叠层倍率
        float grandmasterMult = isMelee ? CareerPassiveHooks.GetGrandmasterDamageMultiplier(attacker) : 1.0f;

        // 下一击伤害倍率 (战斗牧师+10% 等)
        float nextAttackMult = 1.0f;
        var attackerRt = attacker.Data?.Runtime;
        if (attackerRt != null && attackerRt.CareerNextAttackDamageMultiplier > 0.001f)
        {
            nextAttackMult = 1.0f + attackerRt.CareerNextAttackDamageMultiplier;
            attackerRt.CareerNextAttackDamageMultiplier = 0f;
        }

        float buffDamageMultiplier = 1.0f;
        if (attacker.Data != null)
        {
            buffDamageMultiplier *= BladeHex.Combat.Buff.BuffSystem.ResolveMultiplier(attacker.Data, "damage");
            buffDamageMultiplier *= BladeHex.Combat.Buff.BuffSystem.ResolveMultiplier(attacker.Data, isMelee ? "melee_damage" : "ranged_damage");
        }

        float critTakenMultiplier = 1.0f;
        if (defender.Data != null)
            critTakenMultiplier += BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(defender.Data, "crit_taken");

        var damageInput = new CombatRuleEngine.DamageInput
        {
            BaseDamage = baseDamage,
            IsGraze = rollResult.IsGraze,
            IsCritical = finalCritical,
            CritMultiplier = PassiveSkillResolver.GetCritMultiplier(attacker),
            CritDamageTakenMultiplier = defender.Model.GetCritDamageTakenMultiplier() * Math.Max(0.2f, critTakenMultiplier),
            SneakDamage = sneakDamage,
            PassiveMeleeBonus = isMelee ? passiveDamageBonus + careerMeleeBonus : 0,
            PassiveMeleeMultiplier = PassiveSkillResolver.GetPassiveMeleeDamageMultiplier(attacker)
                * (isMelee ? CareerPassiveHooks.ModifyMeleeDamageMultiplier(attacker, 1.0f) : 1.0f)
                * grandmasterMult,
            IsMelee = isMelee,
            FlankMultiplier = flankMult * (1.0f + surroundDamageBonus),
            ChargeMultiplier = chargeMult,
            MountBonus = (attacker.Data != null && attacker.Data.IsMounted) ? 2 : 0,
            DamageReduction = PassiveSkillResolver.GetPassiveDamageReduction(defender, "physical")
                + BladeHex.Combat.Abilities.UnitAbilities.GetTotalFlatDamageReduction(defender.Data),
            FinalMultiplier = CareerPassiveHooks.GetAllyDamageAuraMultiplier(attacker)
                * (!isMelee ? CareerPassiveHooks.GetArcaneArcherDamageMultiplier(attacker) : 1.0f) // 诗人+5% | 秘射手叠伤
                * CareerPassiveHooks.GetVoidKnightApDamageMultiplier(attacker) // 深渊骑士: 每 AP +8%
                * (!isMelee ? CareerPassiveHooks.GetShadowShroudOffenseBonus(attacker).damageMult : 1.0f) // 影匿者: 远程伤害+15%
                * nextAttackMult // 战斗牧师+10% 等下一击倍率
                * buffDamageMultiplier // buff +%伤害(haste/battle_fury 等 Increased/More 乘区)
                * (attacker.Data != null
                    ? SkillTreeKeystoneResolver.GetDamageFinalMultiplier(
                        attacker.Data, weapon, isMelee, !isMelee, attackDistance, finalCritical, attacker.HasMoved)
                    : 1.0f),
        };

        var dmgCalc = CombatRuleEngine.CalculateDamage(in damageInput);
        if (attacker.Data != null)
            SkillTreeKeystoneResolver.ConsumeAttackDamageTriggers(attacker.Data, isMelee);
        int damage = dmgCalc.FinalDamage;
        if (dmgCalc.DamageReductionApplied > 0) result["damage_reduction"] = dmgCalc.DamageReductionApplied;

        // v0.8: 碎颅者-弱点粉碎 → 目标非满HP时追加伤害
        if (CareerSkillResolver.HasCrushWeakPoint(attacker))
        {
            int crushBonus = CareerSkillResolver.GetCrushWeakPointBonus(attacker, defender);
            if (crushBonus > 0)
            {
                damage += crushBonus;
                result["crush_weak_bonus"] = crushBonus;
            }
        }

        // v0.8: 铁血暴君-暴君之怒 → 伤害不受 debuff 降低（无视防御方伤害减免）
        if (CareerSkillResolver.HasUndiminishedDamage(attacker))
        {
            // 重新计算：跳过所有被动伤害减免
            int reducedDmg = Math.Max(1, damage);
            int rawDmg = dmgCalc.FinalDamage;
            // 如果 damage 有被 DamageReduction 降低，把差值加回来
            // dmgCalc.DamageReductionApplied 已经记录了减免量
            if (dmgCalc.DamageReductionApplied > 0)
            {
                damage = rawDmg;
                result["undiminished_damage"] = true;
            }
        }

        // ===== 5. 装甲穿透结算（委托 BattleUnitModel.ApplyDamage）=====
        // v1 职业被动: 守卫 — 本回合首次受伤害 -30%
        // v1 职业被动: 山岳之王 — 不动如山状态下受到伤害 -60%
        {
            float dmgMult = CareerPassiveHooks.ModifyIncomingDamageMultiplier(defender, 1.0f, out bool guardApplied, isMelee);
            if (Math.Abs(dmgMult - 1.0f) > 0.001f)
            {
                damage = (int)(damage * dmgMult);
                if (guardApplied)
                    modifiers["guardian_reduction"] = true;
            }
        }

        var weaponSubtype = weapon?.Subtype ?? WeaponData.WeaponSubtype.Unarmed;
        var weaponWeight = weapon?.Weight ?? WeaponData.WeightCategory.Medium;
        // STR 穿甲加成 v0.6 6.3: floor(sqrt(STR/4))
        int strPenBonus = attacker.Data != null
            ? (int)System.Math.Floor(System.Math.Sqrt(CombatStats.GetEffectiveStr(attacker.Data) / 4.0))
            : 0;
        // v0.8: buff dr_pen_bonus 加成（职业技能提供的穿甲加成）
        if (attacker.Data != null)
            strPenBonus += (int)BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(attacker.Data, "dr_pen_bonus");
        // v1 职业被动: 荒芜化身→相邻敌人100%护甲穿透
        strPenBonus = CareerPassiveHooks.ModifyArmorPenBonus(attacker, defender, strPenBonus);
        // v0.7 中型武器 Lv.7+ 精通: 装甲伤害 ×1.2（原 Lv.5 阈值按比例上调）
        bool mediumLv5 = false;
        if (weapon != null && weapon.Weight == WeaponData.WeightCategory.Medium && attacker.Data != null)
        {
            int masteryLv = attacker.Data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            if (masteryLv >= 7) mediumLv5 = true;
        }

        // v0.6 6.2 盾牌对远程攻击的有效伤害减免与扣耐久已下沉至 Core 层的 BattleUnitModel.ApplyDamage
        bool isRanged = weapon != null && weapon.IsRanged;

        // v1 职业被动: 誓盾卫 — 相邻友军暴击免伤 (将伤害设为 0)
        if (finalCritical && CareerPassiveHooks.ShouldNegateAdjacentAllyCrit(defender))
        {
            damage = 0;
            result["critical_negated_by_oathshield"] = true;
            result["damage"] = 0;
        }

        int preDamageHp = defender.CurrentHp;
        var dmgResult = defender.Model.ApplyDamage(
            source: DamageSource.WeaponAttack,
            amount: damage,
            damageType: weapon?.WeaponDamageType ?? WeaponData.DamageType.Slash,
            naturalRoll: rollResult.NaturalRoll,
            weaponWeight: weaponWeight,
            attackerMastery: attacker.Data?.WeaponMastery,
            weaponSubtype: weaponSubtype,
            strPenBonus: strPenBonus,
            mediumLv5Mastery: mediumLv5,
            isRanged: isRanged);

        if (dmgResult.ShieldAbsorbed > 0)
        {
            modifiers["shield_ranged_absorbed"] = dmgResult.ShieldAbsorbed;
        }
        if (dmgResult.RedirectedHpDamage > 0)
        {
            modifiers["damage_redirected"] = dmgResult.RedirectedHpDamage;
            result["redirected_damage"] = dmgResult.RedirectedHpDamage;
            result["redirected_to_unit_id"] = dmgResult.RedirectedToUnitId;
        }

        result["armor_penetrated"] = dmgResult.IsPenetrated;
        result["armor_damage"] = dmgResult.DrDamage;
        if (dmgResult.IsPenetrated && weapon?.WeaponDamageType == WeaponData.DamageType.Crush
            && defender.Data != null && defender.Data.CurrentDr <= 0)
            result["crush_bonus"] = true;

        int hpDamage = dmgResult.HpDamage;

        // v1 职业被动: 战法师 — 法力护盾自动抵消伤害
        int beforeManaShield = hpDamage;
        hpDamage = CareerPassiveHooks.ApplyManaShield(defender, hpDamage);
        if (hpDamage < beforeManaShield)
        {
            int absorbed = beforeManaShield - hpDamage;
            modifiers["mana_shield_absorbed"] = absorbed;
        }

        // ===== 6. 应用最终倍率（如 AoO 半伤）=====
        if (damageMultiplier != 1.0f)
        {
            hpDamage = Math.Max(1, (int)(hpDamage * damageMultiplier));
            int extra = hpDamage - dmgResult.HpDamage;
            if (extra > 0)
                ApplyDirectHpLoss(defender, extra);
        }

        // ===== 7. 先知: 致命伤害保护 =====
        if (hpDamage >= preDamageHp && preDamageHp > 0)
        {
            CareerPassiveHooks.ApplyProphetProtection(defender, ref hpDamage);
        }

        // ===== 8. 死亡豁免 =====
        if (hpDamage >= preDamageHp && PassiveSkillResolver.HasDeathSave(defender))
        {
            if (PassiveSkillResolver.RollDeathSave(defender))
            {
                hpDamage = Math.Max(0, preDamageHp - 1);
                defender.Model.CurrentHp = Math.Max(1, defender.Model.CurrentHp);
                defender.CurrentHp = Math.Max(1, defender.CurrentHp);
                result["death_saved"] = true;
            }
        }

        // ===== 8. 同步 HP + 触发表现（Node 层副作用）=====
        defender.CurrentHp = defender.Model.CurrentHp;
        defender.UpdateHpBar();
        defender.UpdateArmorBar();
        result["damage"] = hpDamage;
        defender.ApplyRedirectedDamage(dmgResult);

        if (hpDamage > 0)
            hpDamage = ApplyMartyrOathRedirect(defender, hpDamage, defenderAllies, result);

        // v1 职业被动: 血契之环 — 受到 HP 伤害时等额回法力
        if (hpDamage > 0)
            CareerPassiveHooks.OnHpDamageTaken(defender, hpDamage);

        if (hpDamage > 0 || dmgResult.DrDamage > 0)
        {
            if (defender.RenderBus != null) defender.RenderBus.NotifyHit(defender);
            Events.EventBus.Instance?.PublishUnitDamaged(defender, hpDamage, defender.CurrentHp);
            _ = defender.HandleDeathAnimIfDead();
        }

        // v1 职业被动: 幻术师 — 受击消耗 1 层幻影 (所有伤害类型)
        if (result["hit"].AsBool())
        {
            CareerPassiveHooks.OnDefenderHit(defender);
        }
        bool wasKillAfterHit = defender.CurrentHp <= 0;
        TryApplyKillFullApRefund(attacker, wasKillAfterHit, result);
        if (wasKillAfterHit && attacker.Data != null)
            SkillTreeKeystoneResolver.OnEnemyKilled(attacker.Data);

        if (attacker.Data != null)
        {
            SkillTreeKeystoneResolver.OnAttackResolved(attacker.Data, finalCritical);
            int leech = SkillTreeKeystoneResolver.ApplyBloodOathLeech(attacker.Data, hpDamage, isMelee);
            if (leech > 0 && attacker.CurrentHp > 0)
            {
                int maxHp = attacker.GetMaxHp();
                int beforeLeech = attacker.CurrentHp;
                int healedHp = Math.Min(maxHp + maxHp / 2, attacker.CurrentHp + leech);
                attacker.CurrentHp = healedHp;
                attacker.Model.CurrentHp = healedHp;
                attacker.Data.Runtime.CurrentHp = healedHp;
                attacker.UpdateHpBar();
                result["blood_oath_leech"] = healedHp - beforeLeech;
            }
        }

        // v1 职业被动: 近战命中后钩子 (武圣叠层/魔武者循环/鏖战骑士免费法术/战争之风消耗)
        if (result["hit"].AsBool() && isMelee)
        {
            bool wasKill = defender.CurrentHp <= 0;
            CareerPassiveHooks.OnMeleeHit(attacker, defender, wasKill);

            // 剑舞者: 近战命中后对另一个相邻敌方发动额外攻击 (防递归)
            if (!_isBladeDancerExtraResolving)
            {
                var extraTarget = CareerPassiveHooks.TryGetBladeDancerExtraTarget(attacker, defender);
                if (extraTarget != null)
                {
                    _isBladeDancerExtraResolving = true;
                    try
                    {
                        var extraResult = ResolveAttack(attacker, extraTarget, grid, attackerAllies: attackerAllies, defenderAllies: defenderAllies);
                        // 无论额外攻击是否命中，消耗本回合剑舞者额外攻击次数
                        CareerPassiveHooks.MarkBladeDancerExtraAttackUsed(attacker);
                        result["blade_dancer_extra_target"] = extraTarget;
                        result["blade_dancer_extra_hit"] = extraResult.ContainsKey("hit") && extraResult["hit"].AsBool();
                        result["blade_dancer_extra_damage"] = extraResult.ContainsKey("damage") ? extraResult["damage"].AsInt32() : 0;
                        result["blade_dancer_extra_crit"] = extraResult.ContainsKey("critical") && extraResult["critical"].AsBool();
                    }
                    finally
                    {
                        _isBladeDancerExtraResolving = false;
                    }
                }
            }
        }

        // v1 职业被动: 秘射手 — 远程命中后跟踪目标叠层
        if (result["hit"].AsBool() && !isMelee)
        {
            CareerPassiveHooks.OnArcaneArcherHit(attacker, defender);
            CareerPassiveHooks.ConsumeShadowShroudOffense(attacker); // 影匿者: 消耗斗篷远程进攻
            TryApplyRangedActionRefund(attacker, weapon, wasKillAfterHit, result);
        }

        // v1 职业被动: 钢弦骑士 — 近战命中后标记免费切换
        if (result["hit"].AsBool() && isMelee)
        {
            CareerPassiveHooks.ApplySteelstringKnightFreeSwitch(attacker);
        }

        ConsumeTargetedNextAttackBuff(attacker, defender);

        // v1 职业被动: 征服者 — 移动≥5 后近战命中所有相邻敌人
        if (result["hit"].AsBool() && isMelee && CareerPassiveHooks.ConsumeConquerorAoe(attacker))
        {
            result["conqueror_aoe"] = true;
        }

        // v0.8 E3-B: 决斗家-以伤换伤 → 被近战命中后100%伤害反击
        if (hpDamage > 0 && !result.ContainsKey("is_counter") && defender.CurrentHp > 0 && isMelee)
        {
            if (CareerSkillResolver.HasRiposte(defender))
            {
                var counterResult = ResolveCounterAttack(defender, attacker.GridPos);
                if (counterResult.ContainsKey("hit") && counterResult["hit"].AsBool())
                {
                    int counterDmg = counterResult["damage"].AsInt32();
                    float mult = CareerSkillResolver.GetRiposteDamageMultiplier();
                    counterDmg = Math.Max(1, (int)(counterDmg * mult));
                    if (counterDmg > 0 && attacker.CurrentHp > 0)
                    {
                        ApplyDirectHpLoss(attacker, counterDmg);
                        Events.EventBus.Instance?.PublishUnitDamaged(attacker, counterDmg, attacker.CurrentHp);
                        result["riposte_damage"] = counterDmg;
                    }
                }
            }
        }

        // v0.8 E3-C: 铁壁守护-殉道守护 → 标记分担伤害，调用方负责选择实际代受者。
        if (hpDamage > 0 && defender.CurrentHp > 0
            && CareerSkillResolver.HasMartyrsGuard(defender))
        {
            float share = CareerSkillResolver.GetMartyrDamageShare();
            int sharedDmg = Math.Max(1, (int)(hpDamage * share));
            result["martyrs_guard_share"] = sharedDmg;
            result["martyrs_guard_defender"] = defender;
        }

        // ===== 9. 装备能力钩子（OnDealDamage / Reflect）=====
        ApplyEquipmentAbilityEffects(attacker, defender, hpDamage, dmgResult.DrDamage, dmgResult.ReflectDamageToAttacker);
        TryApplyNextHitPoison(attacker, defender, result);

        return result;
    }

    private static int ApplyMartyrOathRedirect(
        Unit defender,
        int hpDamage,
        Unit[]? defenderAllies,
        Godot.Collections.Dictionary result)
    {
        if (defender.Data == null || hpDamage <= 0) return hpDamage;

        var candidates = defender.IsPlayerSide
            ? defender.CombatManager?.PlayerUnits
            : defender.CombatManager?.EnemyUnits;
        if (candidates == null && defenderAllies != null)
            candidates = defenderAllies.Where(u => u.IsPlayerSide == defender.IsPlayerSide).ToList();
        if (candidates == null) return hpDamage;

        Unit? martyr = null;
        foreach (var unit in candidates)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit == defender || unit.CurrentHp <= 0 || unit.Data == null)
                continue;
            if (!SkillTreeKeystoneResolver.HasMartyrOath(unit.Data))
                continue;
            if (unit.DistanceTo(defender) > 3)
                continue;

            martyr = unit;
            break;
        }
        if (martyr == null) return hpDamage;

        int redirected = Math.Max(1, hpDamage / 2);
        int beforeRestore = defender.CurrentHp;
        int maxHp = defender.GetMaxHp();
        defender.CurrentHp = Math.Min(maxHp, defender.CurrentHp + redirected);
        defender.Model.CurrentHp = defender.CurrentHp;
        defender.Data.Runtime.CurrentHp = defender.CurrentHp;
        defender.UpdateHpBar();

        martyr.ApplyRedirectedHpDamage(redirected, bypassMitigation: true);
        result["martyr_oath_redirect"] = redirected;
        result["martyr_oath_guardian"] = martyr;
        result["martyr_oath_restored"] = defender.CurrentHp - beforeRestore;
        int remainingDamage = Math.Max(0, hpDamage - redirected);
        result["damage"] = remainingDamage;
        return remainingDamage;
    }

    private static void TryApplyNextHitPoison(Unit attacker, Unit defender, Godot.Collections.Dictionary result)
    {
        if (attacker.Data == null || defender?.Data == null) return;
        var poisonBuff = BladeHex.Combat.Buff.BuffModifierReader.FirstBuffWithTruthy(attacker.Data, "next_hit_poison_duration");
        if (poisonBuff == null) return;

        int duration = Math.Max(1, (int)BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(poisonBuff, "next_hit_poison_duration", 3f));
        BladeHex.Combat.Buff.BuffSystem.Apply(defender.Data, "poison", duration, (int)attacker.GetInstanceId(), "skill_tree_next_hit_poison");
        BladeHex.Combat.Buff.BuffSystem.RemoveBuffInstance(attacker.Data, poisonBuff);
        result["next_hit_poison_applied"] = true;
        result["next_hit_poison_duration"] = duration;
    }

    private static float GetMarkedTargetCritBonus(Unit attacker, Unit defender)
    {
        if (defender.Data == null) return 0f;
        long attackerId = (long)attacker.GetInstanceId();
        float bonus = 0f;
        foreach (var buff in defender.Data.Runtime.ActiveBuffs)
        {
            bool markedByAttacker = buff.SourceUnitId == attackerId
                || (TryGetTaggedBuffSourceId(buff, "marker", out long markerSourceId) && markerSourceId == attackerId);
            if (!markedByAttacker)
            {
                float markerId = BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(buff, "marker_id", -1f);
                markedByAttacker = (long)markerId == attackerId;
            }
            if (!markedByAttacker) continue;

            bonus += BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(buff, "critical_rate_taken");
        }
        return bonus;
    }

    private static float GetAttackerCriticalRateBuffBonus(Unit attacker, Unit defender)
    {
        if (attacker.Data == null) return 0f;

        long defenderId = (long)defender.GetInstanceId();
        float bonus = 0f;
        foreach (var buff in attacker.Data.Runtime.ActiveBuffs)
        {
            if (TryGetTaggedBuffSourceId(buff, "marked_target", out long sourceTargetId))
            {
                if (sourceTargetId != defenderId)
                    continue;
            }
            else
            {
                float markedTarget = BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(buff, "marked_target", -1f);
                if (markedTarget >= 0f && (long)markedTarget != defenderId)
                    continue;
            }

            bonus += BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(buff, "critical_rate");
        }
        return bonus;
    }

    private static void ConsumeTargetedNextAttackBuff(Unit attacker, Unit defender)
    {
        if (attacker.Data == null) return;

        long defenderId = (long)defender.GetInstanceId();
        foreach (var buff in attacker.Data.Runtime.ActiveBuffs.ToArray())
        {
            if (TryGetTaggedBuffSourceId(buff, "marked_target", out long sourceTargetId))
            {
                if (sourceTargetId != defenderId)
                    continue;
            }
            else
            {
                float markedTarget = BladeHex.Combat.Buff.BuffModifierReader.SumOrDefault(buff, "marked_target", -1f);
                if (markedTarget < 0f || (long)markedTarget != defenderId)
                    continue;
            }

            BladeHex.Combat.Buff.BuffSystem.RemoveBuffInstance(attacker.Data, buff);
        }
    }

    private static bool TryGetTaggedBuffSourceId(BladeHex.Combat.Buff.BuffInstance buff, string tag, out long id)
    {
        id = -1;
        string prefix = tag + ":";
        if (string.IsNullOrEmpty(buff.Source) || !buff.Source.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        return long.TryParse(buff.Source.AsSpan(prefix.Length), out id);
    }

    private static void RemoveRangedActionRefundWindow(Unit attacker, WeaponData? weapon)
    {
        if (weapon == null || !weapon.IsRanged || attacker.Data == null) return;
        var refundBuff = BladeHex.Combat.Buff.BuffModifierReader.FirstBuffWithTruthy(attacker.Data, "refund_ranged_action");
        if (refundBuff == null) return;
        BladeHex.Combat.Buff.BuffSystem.RemoveBuffInstance(attacker.Data, refundBuff);
    }

    private static void TryApplyRangedActionRefund(Unit attacker, WeaponData? weapon, bool wasKill, Godot.Collections.Dictionary result)
    {
        if (weapon == null || !weapon.IsRanged || attacker.Data == null) return;
        var refundBuff = BladeHex.Combat.Buff.BuffModifierReader.FirstBuffWithTruthy(attacker.Data, "refund_ranged_action");
        if (refundBuff == null) return;

        int apRefund = Math.Max(1, weapon.ApCost);
        attacker.CurrentAp = Math.Min(attacker.CurrentAp + apRefund, attacker.GetMaxAp());
        result["refund_ranged_action_ap"] = apRefund;
        if (wasKill)
        {
            attacker.CurrentAp = attacker.GetMaxAp();
            result["dex_giant_kill_move_refresh"] = true;
        }
        BladeHex.Combat.Buff.BuffSystem.ConsumeModifierStack(attacker.Data, refundBuff, "refund_ranged_action");
    }

    private static void TryApplyKillFullApRefund(Unit attacker, bool wasKill, Godot.Collections.Dictionary result)
    {
        if (!wasKill || attacker.Data == null) return;
        var refundBuff = BladeHex.Combat.Buff.BuffModifierReader.FirstBuffWithTruthy(attacker.Data, "kill_full_ap_refund");
        if (refundBuff == null) return;

        attacker.CurrentAp = attacker.GetMaxAp();
        result["kill_full_ap_refund"] = true;
    }

    /// <summary>
    /// 应用装备能力效果：攻击方 OnDealDamage（如 lifesteal）+ 防御方反弹（如 thorns）
    /// </summary>
    private static void ApplyEquipmentAbilityEffects(Unit attacker, Unit defender, int hpDamage, int drDamage, int reflectDamage)
    {
        // 1) 反伤：来自防御方的 OnTakeDamage（已在 ApplyDamage 中聚合）
        if (reflectDamage > 0 && attacker.CurrentHp > 0)
        {
            int actualReflect = Math.Max(0, Math.Min(reflectDamage, attacker.CurrentHp));
            ApplyDirectHpLoss(attacker, actualReflect);
            Events.EventBus.Instance?.PublishUnitDamaged(attacker, actualReflect, attacker.CurrentHp);
        }

        // 2) OnDealDamage：触发攻击方装备能力（lifesteal 等）
        if (hpDamage > 0)
        {
            var ctx = new BladeHex.Combat.Abilities.DealDamageContext
            {
                Attacker = attacker.Model,
                Defender = defender.Model,
                HpDamageDealt = hpDamage,
                DrDamageDealt = drDamage,
            };
            foreach (var ab in BladeHex.Combat.Abilities.UnitAbilities.GetAll(attacker.Data))
                ab.OnDealDamage(ctx);

            if (ctx.HealAmount > 0)
                attacker.Heal(ctx.HealAmount);

            // 3) 应用条件型/附加伤害（如词缀的 vs_undead +1d6）
            foreach (var dmgEvent in ctx.ExtraDamageEvents)
            {
                if (dmgEvent.Damage <= 0 || defender.CurrentHp <= 0) continue;
                int actualExtra = Math.Max(0, Math.Min(dmgEvent.Damage, defender.CurrentHp));
                ApplyDirectHpLoss(defender, actualExtra);
                Events.EventBus.Instance?.PublishUnitDamaged(defender, actualExtra, defender.CurrentHp);
                _ = defender.HandleDeathAnimIfDead();
            }
        }
    }

    private static void ApplyDirectHpLoss(Unit unit, int hpDamage)
    {
        if (hpDamage <= 0) return;

        bool hasDeathImmunity = unit.Data != null
            && BladeHex.Combat.Buff.BuffModifierReader.FirstBuffWithTruthy(unit.Data, "death_immunity") != null;
        unit.Model.CurrentHp = hasDeathImmunity
            ? Math.Max(1, unit.Model.CurrentHp - hpDamage)
            : Math.Max(0, unit.Model.CurrentHp - hpDamage);
        unit.CurrentHp = unit.Model.CurrentHp;
        if (unit.Data != null) unit.Data.Runtime.CurrentHp = unit.CurrentHp;
        unit.UpdateHpBar();
    }

    // ============================================================================
    // 借机攻击
    // ============================================================================

    public static Godot.Collections.Dictionary ResolveAttackOfOpportunity(Unit attacker, Unit mover, HexGrid? grid = null)
    {
        var result = ResolveAttack(attacker, mover, grid, isAoo: true, damageMultiplier: 0.5f);
        if (attacker.Data != null
            && (!result.ContainsKey("blocked_by_elevation") || !result["blocked_by_elevation"].AsBool()))
        {
            attacker.Model.AooUsedThisTurn = true;
        }
        return result;
    }

    // ============================================================================
    // 反击
    // ============================================================================

    public static Godot.Collections.Dictionary ResolveCounterAttack(Unit defender, Vector2I attackerPos)
    {
        float mult = FacingSystem.GetCounterAttackMultiplier(defender, attackerPos);
        if (mult <= 0.0f) return new Godot.Collections.Dictionary { { "hit", false }, { "damage", 0 } };

        var weapon = defender.Model.GetMainHand() as WeaponData;
        int finalDmg;
        if (weapon != null)
        {
            int strMod = defender.Data != null ? RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveStr(defender.Data)) : 0;
            finalDmg = CombatRuleEngine.CalculateCounterDamage(
                weapon.DamageDiceCount, weapon.DamageDiceSides, strMod, mult);
        }
        else
        {
            finalDmg = CombatRuleEngine.CalculateCounterDamage(1, 3, 0, mult);
        }

        return new Godot.Collections.Dictionary { { "hit", true }, { "damage", finalDmg }, { "multiplier", mult } };
    }

    // ============================================================================
    // 预览（供 UI 使用）
    // ============================================================================

    public static float GetHitChancePreview(Unit attacker, Unit defender, HexGrid? grid = null, bool hasFlanking = false, bool hasSneak = false)
    {
        int attackBonus = attacker.Model.GetAttackBonus();
        int targetAc = defender.GetEffectiveAc(attacker);
        bool hasAdvantage = false;
        bool hasDisadvantage = false;
        int accuracyMod = 0;

        if (grid != null)
        {
            // 高地优势/劣势
            var hg = LineOfSight.GetHighGroundBonus(attacker.GridPos, defender.GridPos, grid);
            if (hg.Advantage) hasAdvantage = true;
            if (hg.Disadvantage) hasDisadvantage = true;

            // 射线/路径上的命中惩罚 (掩体/障碍物阻挡)
            var weapon = attacker.Model.GetMainHand() as WeaponData;
            if (weapon != null && weapon.IsRanged)
            {
                int losPenalty = LineOfSight.GetPathPenalty(
                    attacker.GridPos, defender.GridPos, grid, attacker, defender);
                if (losPenalty < 0)
                {
                    losPenalty = CareerPassiveHooks.ModifyCoverPenalty(attacker, losPenalty);
                    accuracyMod += losPenalty;
                }
            }

            //高度差命中修正（正=高打低加分，负=仰攻减分）
            var atkCell = grid.GetCell(attacker.GridPos.X, attacker.GridPos.Y);
            var defCell = grid.GetCell(defender.GridPos.X, defender.GridPos.Y);
            if (atkCell != null && defCell != null)
            {
                int elevDiff = atkCell.Elevation - defCell.Elevation;
                if (elevDiff != 0)
                {
                    accuracyMod += elevDiff; // 每级 ±1 命中点
                }
            }

            // 渡河惩罚
            if (LineOfSight.HasRiverCrossingPenalty(attacker.GridPos, defender.GridPos, grid))
            {
                hasDisadvantage = true;
            }
        }

        // 包夹加成
        if (hasFlanking)
        {
            int flankHitBonus = BladeHex.Combat.Abilities.UnitAbilities.GetTotalFlankingHitBonus(attacker.Data);
            if (flankHitBonus != 0)
            {
                accuracyMod += flankHitBonus;
            }
        }

        // 伏击加成
        if (hasSneak)
        {
            hasAdvantage = true;
        }

        // 伤势惩罚
        if (attacker.Data != null)
        {
            float atkHpPct = attacker.Model.GetMaxHp() > 0
                ? (float)attacker.CurrentHp / attacker.Model.GetMaxHp()
                : 1.0f;
            var wound = RPGRuleEngine.GetWoundPenalty(atkHpPct);
            int penalty = wound.ContainsKey("all_checks") ? wound["all_checks"].AsInt32() : 0;
            if (penalty != 0)
            {
                accuracyMod += penalty;
            }
        }

        return RPGRuleEngine.CalculateHitChance(attackBonus + accuracyMod, targetAc, hasAdvantage, hasDisadvantage);
    }

    public static Godot.Collections.Dictionary GetDamagePreview(Unit attacker)
    {
        var weapon = attacker.Model.GetMainHand() as WeaponData;
        if (weapon == null) return new Godot.Collections.Dictionary { { "min", 1 }, { "max", 3 }, { "avg", 2 } };

        int statMod = attacker.Data != null ? RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveStr(attacker.Data)) : 0;
        var (min, max, avg) = CombatRuleEngine.GetWeaponDamageRange(
            weapon.DamageDiceCount, weapon.DamageDiceSides, statMod);

        return new Godot.Collections.Dictionary
        {
            { "min", min },
            { "max", max },
            { "avg", avg }
        };
    }
}
