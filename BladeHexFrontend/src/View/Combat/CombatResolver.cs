using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Events;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 战斗结算器 — Frontend 适配层
/// 从 Node 层收集数据，委托 CombatRuleEngine (Core) 执行纯规则计算，
/// 然后应用结果到 Node（HP 同步、VFX、EventBus）。
/// </summary>
public static class CombatResolver
{
    // ============================================================================
    // 主攻击解析
    // ============================================================================

    /// <summary>完整攻击结算管道</summary>
    /// <param name="attackerAllies">
    /// 攻击者同阵营单位数组（用于包围加成计算）。null = 跳过包围加成。
    /// </param>
    public static Godot.Collections.Dictionary ResolveAttack(
        Unit attacker, Unit defender, HexGrid? grid = null,
        bool isCharge = false, bool isAoo = false,
        int accuracyMod = 0, float damageMultiplier = 1.0f,
        Unit[]? attackerAllies = null,
        float nodePassiveScale = 1.0f)
    {
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
            { "is_charge", isCharge }
        };

        var modifiers = (Godot.Collections.Dictionary)result["modifiers"];

        // ===== 1. 收集修正（Node 层数据采集）=====
        int attackBonus = attacker.Model.GetAttackBonus();
        result["attack_bonus"] = attackBonus;

        bool hasAdvantage = false;
        bool hasDisadvantage = false;

        // 高地优势
        if (grid != null)
        {
            var hgResult = LineOfSight.GetHighGroundBonus(attacker.GridPos, defender.GridPos, grid);
            if (hgResult.Advantage) { hasAdvantage = true; modifiers["high_ground"] = true; }
            if (hgResult.Disadvantage) { hasDisadvantage = true; modifiers["low_ground"] = true; }
        }

        // 冲锋优势
        if (isCharge) { hasAdvantage = true; modifiers["charge"] = true; }

        // 士气效果
        var moraleEffects = MoraleSystem.GetMoraleEffects(attacker);
        if (moraleEffects.FumbleRate > 0) { hasDisadvantage = true; modifiers["low_morale"] = true; }
        if (moraleEffects.HitBonus != 0)
        {
            accuracyMod += moraleEffects.HitBonus;
            modifiers["morale_hit"] = moraleEffects.HitBonus;
        }

        // 掩体惩罚（远程攻击时）—— 视野系统已移除，改为路径上累计命中惩罚
        var weapon = attacker.Model.GetMainHand() as WeaponData;

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

        // ===== 2. 委托 Core 层执行攻击检定 =====
        // 节点暴击率：技能盘 critical_rate 节点等独立暴击概率（v0.6 11.5）
        float bonusCritChance = 0f;
        if (attacker.SkillTree != null)
            bonusCritChance += attacker.SkillTree.GetCriticalRateBonus();

        var attackInput = new CombatRuleEngine.AttackInput
        {
            AttackBonus = attackBonus,
            TargetAc = defender.GetEffectiveAc(attacker) - surroundAcReduction,
            CritThreshold = critThreshold,
            HasAdvantage = hasAdvantage,
            HasDisadvantage = hasDisadvantage,
            AccuracyMod = accuracyMod,
            CoverAcBonus = 0,           // 视野系统已移除：掩体惩罚已折叠到 AccuracyMod
            BonusCritChance = bonusCritChance,
        };

        var rollResult = CombatRuleEngine.RollAttack(in attackInput);

        // 写回结果字典
        result["roll"] = rollResult.NaturalRoll;
        result["target_ac"] = rollResult.FinalTargetAc;
        result["total_attack"] = rollResult.TotalAttack;
        result["hit_chance_percent"] = rollResult.HitChancePercent;
        result["critical"] = rollResult.IsCritical;
        result["fumble"] = rollResult.IsFumble;
        result["hit"] = rollResult.IsHit;
        result["graze"] = rollResult.IsGraze;
        result["advantage"] = hasAdvantage && !hasDisadvantage;
        result["disadvantage"] = hasDisadvantage && !hasAdvantage;
        if (accuracyMod != 0) modifiers["skill_mod"] = accuracyMod;

        if (rollResult.IsFumble || !rollResult.IsHit) return result;

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
        }

        // 冲锋
        float chargeMult = 1.0f;
        if (isCharge)
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
        bool isMelee = weapon == null || !weapon.IsRanged;
        int passiveDamageBonus = isMelee ? passiveMelee : passiveRanged;
        // 远程伤害也要进入伤害计算（CombatRuleEngine 没有专门的 PassiveRangedBonus 字段，
        // 直接折算入 BaseDamage 上层）
        if (!isMelee && passiveRanged != 0) baseDamage += passiveRanged;

        var damageInput = new CombatRuleEngine.DamageInput
        {
            BaseDamage = baseDamage,
            IsGraze = rollResult.IsGraze,
            IsCritical = rollResult.IsCritical,
            CritMultiplier = PassiveSkillResolver.GetCritMultiplier(attacker),
            CritDamageTakenMultiplier = defender.Model.GetCritDamageTakenMultiplier(),
            SneakDamage = sneakDamage,
            PassiveMeleeBonus = isMelee ? passiveDamageBonus : 0,
            PassiveMeleeMultiplier = PassiveSkillResolver.GetPassiveMeleeDamageMultiplier(attacker),
            IsMelee = isMelee,
            FlankMultiplier = flankMult * (1.0f + surroundDamageBonus),
            ChargeMultiplier = chargeMult,
            MountBonus = (attacker.Data != null && attacker.Data.IsMounted) ? 2 : 0,
            DamageReduction = PassiveSkillResolver.GetPassiveDamageReduction(defender, "physical")
                + BladeHex.Combat.Abilities.UnitAbilities.GetTotalFlatDamageReduction(defender.Data),
            FinalMultiplier = 1.0f, // damageMultiplier 在穿甲后应用
        };

        var dmgCalc = CombatRuleEngine.CalculateDamage(in damageInput);
        int damage = dmgCalc.FinalDamage;
        if (dmgCalc.DamageReductionApplied > 0) result["damage_reduction"] = dmgCalc.DamageReductionApplied;

        // ===== 5. 装甲穿透结算（委托 BattleUnitModel.ApplyDamage）=====
        var weaponSubtype = weapon?.Subtype ?? WeaponData.WeaponSubtype.Unarmed;
        var weaponWeight = weapon?.Weight ?? WeaponData.WeightCategory.Medium;
        // STR 穿甲加成 v0.6 6.3: floor(sqrt(STR/4))
        int strPenBonus = attacker.Data != null
            ? (int)System.Math.Floor(System.Math.Sqrt(attacker.Data.Str / 4.0))
            : 0;
        // v0.6 6.9 中型武器 Lv.5+ 精通: 装甲伤害 ×1.2
        bool mediumLv5 = false;
        if (weapon != null && weapon.Weight == WeaponData.WeightCategory.Medium && attacker.Data != null)
        {
            int masteryLv = attacker.Data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            if (masteryLv >= 5) mediumLv5 = true;
        }

        // v0.6 6.2 盾牌对远程攻击的有效伤害减免
        int finalDamage = damage;
        bool isRanged = weapon != null && weapon.IsRanged;
        if (isRanged && defender.Data?.Shield != null
            && defender.Data.Shield.RangedDamageMultiplier < 1.0f
            && defender.Data.Shield.CurrentArmorPoints > 0)
        {
            int reduced = (int)(finalDamage * defender.Data.Shield.RangedDamageMultiplier);
            int absorbed = finalDamage - reduced;
            int actualAbsorb = System.Math.Min(absorbed, defender.Data.Shield.CurrentArmorPoints);
            defender.Data.Shield.CurrentArmorPoints -= actualAbsorb;
            if (defender.Data.Shield.CurrentArmorPoints <= 0) defender.Data.Shield = null;
            finalDamage = reduced + (absorbed - actualAbsorb);
            modifiers["shield_ranged_absorbed"] = actualAbsorb;
        }

        int preDamageHp = defender.CurrentHp;
        var dmgResult = defender.Model.ApplyDamage(
            source: DamageSource.WeaponAttack,
            amount: finalDamage,
            damageType: weapon?.WeaponDamageType ?? WeaponData.DamageType.Slash,
            naturalRoll: rollResult.NaturalRoll,
            weaponWeight: weaponWeight,
            attackerMastery: attacker.Data?.WeaponMastery,
            weaponSubtype: weaponSubtype,
            weaponPen: weapon?.WeaponPen ?? 0,
            strPenBonus: strPenBonus,
            mediumLv5Mastery: mediumLv5);

        result["armor_penetrated"] = dmgResult.IsPenetrated;
        result["armor_damage"] = dmgResult.DrDamage;
        if (dmgResult.IsPenetrated && weapon?.WeaponDamageType == WeaponData.DamageType.Crush
            && defender.Data != null && defender.Data.CurrentDr <= 0)
            result["crush_bonus"] = true;

        int hpDamage = dmgResult.HpDamage;

        // ===== 6. 应用最终倍率（如 AoO 半伤）=====
        if (damageMultiplier != 1.0f)
        {
            hpDamage = Math.Max(1, (int)(hpDamage * damageMultiplier));
            int extra = hpDamage - dmgResult.HpDamage;
            if (extra > 0)
                defender.Model.CurrentHp = Math.Max(0, defender.Model.CurrentHp - extra);
        }

        // ===== 7. 死亡豁免 =====
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

        if (hpDamage > 0 || dmgResult.DrDamage > 0)
        {
            if (defender.RenderBus != null) defender.RenderBus.NotifyHit(defender);
            Events.EventBus.Instance?.PublishUnitDamaged(defender, hpDamage, defender.CurrentHp);
            _ = defender.HandleDeathAnimIfDead();
        }

        // ===== 9. 装备能力钩子（OnDealDamage / Reflect）=====
        ApplyEquipmentAbilityEffects(attacker, defender, hpDamage, dmgResult.DrDamage, dmgResult.ReflectDamageToAttacker);

        return result;
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
            attacker.Model.CurrentHp = Math.Max(0, attacker.Model.CurrentHp - actualReflect);
            attacker.CurrentHp = attacker.Model.CurrentHp;
            attacker.UpdateHpBar();
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
                defender.Model.CurrentHp = Math.Max(0, defender.Model.CurrentHp - actualExtra);
                defender.CurrentHp = defender.Model.CurrentHp;
                defender.UpdateHpBar();
                Events.EventBus.Instance?.PublishUnitDamaged(defender, actualExtra, defender.CurrentHp);
                _ = defender.HandleDeathAnimIfDead();
            }
        }
    }

    // ============================================================================
    // 借机攻击
    // ============================================================================

    public static Godot.Collections.Dictionary ResolveAttackOfOpportunity(Unit attacker, Unit mover)
    {
        var result = ResolveAttack(attacker, mover, isAoo: true, damageMultiplier: 0.5f);
        if (attacker.Data != null) attacker.Data.Runtime.AooUsedThisTurn = true;
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
            int strMod = defender.Data != null ? RPGRuleEngine.GetStatModifier(defender.Data.Str) : 0;
            finalDmg = CombatRuleEngine.CalculateCounterDamage(
                weapon.DamageDiceCount, weapon.DamageDiceSides, strMod, mult);
        }
        else
        {
            finalDmg = CombatRuleEngine.CalculateCounterDamage(1, 3, 0, mult);
        }

        if (defender.Data != null) defender.Data.Runtime.CounterUsedThisTurn = true;

        return new Godot.Collections.Dictionary { { "hit", true }, { "damage", finalDmg }, { "multiplier", mult } };
    }

    // ============================================================================
    // 预览（供 UI 使用）
    // ============================================================================

    public static float GetHitChancePreview(Unit attacker, Unit defender, HexGrid? grid = null)
    {
        int attackBonus = attacker.Model.GetAttackBonus();
        int targetAc = defender.GetEffectiveAc(attacker);
        bool hasAdvantage = false;
        bool hasDisadvantage = false;

        if (grid != null)
        {
            var hg = LineOfSight.GetHighGroundBonus(attacker.GridPos, defender.GridPos, grid);
            if (hg.Advantage) hasAdvantage = true;
            if (hg.Disadvantage) hasDisadvantage = true;
        }

        return RPGRuleEngine.CalculateHitChance(attackBonus, targetAc, hasAdvantage, hasDisadvantage);
    }

    public static Godot.Collections.Dictionary GetDamagePreview(Unit attacker)
    {
        var weapon = attacker.Model.GetMainHand() as WeaponData;
        if (weapon == null) return new Godot.Collections.Dictionary { { "min", 1 }, { "max", 3 }, { "avg", 2 } };

        int statMod = attacker.Data != null ? RPGRuleEngine.GetStatModifier(attacker.Data.Str) : 0;
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
