using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 被动技能查询接口 — 提供所有被动技能的加成查询
/// 供 CombatResolver / Unit / CombatManager 调用
/// </summary>
public static class PassiveSkillResolver
{
    // ============================================================================
    // 被动伤害加成
    // ============================================================================

    /// <summary>暴击伤害倍率（默认x2，critical_x3/critical_master → x3）</summary>
    public static int GetCritMultiplier(Unit unit)
    {
        if (unit.HasSkillEffect("critical_x3") || unit.HasSkillEffect("critical_master")) return 3;
        return 2;
    }

    /// <summary>近战伤害加成（被动）</summary>
    public static int GetPassiveMeleeDamageBonus(Unit unit)
    {
        int bonus = 0;
        if (unit.HasSkillEffect("weapon_mastery"))
        {
            if (unit.Data != null)
            {
                int strMod = RPGRuleEngine.GetStatModifier(unit.Data.Str);
                if (strMod > 0) bonus += (int)(strMod * 0.5f);
            }
        }
        // v0.6 11.2 节点 melee_damage 累加值（NodeMeleeDamage）
        if (unit.SkillTree != null)
            bonus += unit.SkillTree.GetMeleeDamageBonus();
        return bonus;
    }

    /// <summary>远程伤害加成（被动 + 节点）</summary>
    public static int GetPassiveRangedDamageBonus(Unit unit)
    {
        int bonus = 0;
        if (unit.SkillTree != null)
            bonus += unit.SkillTree.GetRangedDamageBonus();
        return bonus;
    }

    /// <summary>近战伤害总倍率修正</summary>
    public static float GetPassiveMeleeDamageMultiplier(Unit unit)
    {
        if (unit.HasSkillEffect("berserk_power")) return 1.5f;
        if (unit.HasSkillEffect("last_stand"))
        {
            if (unit.Data != null && unit.CurrentHp > 0)
            {
                float hpPct = (float)unit.CurrentHp / unit.Model.GetMaxHp();
                if (hpPct < 0.25f) return 1.5f;
            }
        }
        return 1.0f;
    }

    /// <summary>物理伤害减免</summary>
    public static int GetPassiveDamageReduction(Unit unit, string damageType = "physical")
    {
        int reduction = 0;
        // v0.6 11.8 con_b05 铁壁的 -3 已下沉到 BattleUnitModel.ApplyDamage 的入口，
        // 这里不再重复计入 DamageInput.DamageReduction。
        if (unit.HasSkillEffect("diamond_body")) reduction += 3;
        return reduction;
    }

    // ============================================================================
    // 被动AC加成
    // ============================================================================

    /// <summary>被动AC加成</summary>
    public static int GetPassiveAcBonus(Unit unit)
    {
        int bonus = 0;
        if (unit.HasSkillEffect("hold_ground"))
        {
            if (!unit.HasMoved) bonus += 2;
        }
        if (unit.HasSkillEffect("dodge_master"))
        {
            if (unit.Data != null)
            {
                int dexMod = RPGRuleEngine.GetStatModifier(unit.Data.Dex);
                if (dexMod > 0) bonus += dexMod;
            }
        }
        if (unit.HasSkillEffect("last_stand"))
        {
            if (unit.Data != null && unit.CurrentHp > 0)
            {
                float hpPct = (float)unit.CurrentHp / unit.Model.GetMaxHp();
                if (hpPct < 0.25f) bonus += 5;
            }
        }
        if (unit.HasSkillEffect("heavy_armor"))
        {
            bonus += 3;
        }
        return bonus;
    }

    /// <summary>远程AC加成</summary>
    public static int GetPassiveRangedAcBonus(Unit unit)
    {
        if (unit.HasSkillEffect("ghost_step")) return 2;
        return 0;
    }

    // ============================================================================
    // 被动命中加成
    // ============================================================================

    public static int GetPassiveMeleeHitBonus(Unit unit) => unit.HasSkillEffect("melee_hit_plus_1") ? 1 : 0;
    public static int GetPassiveRangedHitBonus(Unit unit) => unit.HasSkillEffect("ranged_hit_plus_1") ? 1 : 0;

    // ============================================================================
    // 法术相关被动
    // ============================================================================

    public static int GetPassiveSpellDcBonus(Unit unit)
    {
        int bonus = 0;
        if (unit.HasSkillEffect("spell_mastery")) bonus += 2;
        if (unit.HasSkillEffect("absolute_focus")) bonus += 4;
        return bonus;
    }

    public static int GetPassiveSpellPenetration(Unit unit) => unit.HasSkillEffect("spell_penetration") ? 2 : 0;
    public static int GetPassiveAoeRangeBonus(Unit unit) => unit.HasSkillEffect("range_expand") ? 1 : 0;

    // ============================================================================
    // 特殊被动
    // ============================================================================

    public static int GetSneakAttackDice(Unit unit, bool hasAdvantage)
    {
        if (!hasAdvantage) return 0;
        int dice = 0;
        if (unit.HasSkillEffect("sneak_attack")) dice += 2;
        if (unit.HasSkillEffect("deadly_blow")) dice += 3;
        return dice;
    }

    public static int GetSneakAttackSides() => 6;

    public static bool HasAutoCounter(Unit unit) => unit.HasSkillEffect("counter_attack");
    public static bool HasDeathSave(Unit unit) => unit.HasSkillEffect("iron_will");

    public static bool RollDeathSave(Unit unit)
    {
        if (unit.Data == null) return false;
        int conScore = unit.Data.Con;
        int prof = RPGRuleEngine.GetProficiencyBonus(unit.Data.Level);
        var result = RPGRuleEngine.MakeSave(conScore, prof, false, 15);
        return (bool)result["success"];
    }

    public static bool HasQuickCast(Unit unit) => unit.HasSkillEffect("quick_cast");
    public static bool HasPiercingShot(Unit unit) => unit.HasSkillEffect("piercing_shot");

    // ============================================================================
    // 奥术共鸣
    // ============================================================================

    public static float GetArcaneResonanceBonus(Unit unit)
    {
        if (!unit.HasSkillEffect("arcane_resonance") || unit.Data == null) return 0.0f;
        // 使用 Variant 字典访问
        var stacksVar = unit.Data.Get("_arcane_resonance_stacks");
        int stacks = stacksVar.VariantType != Variant.Type.Nil ? stacksVar.AsInt32() : 0;
        return Math.Min(stacks, 2) * 0.2f;
    }

    public static void IncrementArcaneResonance(Unit unit)
    {
        if (unit.HasSkillEffect("arcane_resonance") && unit.Data != null)
        {
            var stacksVar = unit.Data.Get("_arcane_resonance_stacks");
            int current = stacksVar.VariantType != Variant.Type.Nil ? stacksVar.AsInt32() : 0;
            unit.Data.Set("_arcane_resonance_stacks", Math.Min(current + 1, 2));
        }
    }

    public static void ResetArcaneResonance(Unit unit)
    {
        unit.Data?.Set("_arcane_resonance_stacks", 0);
    }

    // ============================================================================
    // 治疗被动
    // ============================================================================

    public static int GetPassiveHealBonus(Unit unit)
    {
        if (unit.HasSkillEffect("nature_affinity")) return RPGRuleEngine.RollDice(1, 4);
        return 0;
    }

    // ============================================================================
    // 远程被动
    // ============================================================================

    public static int GetPassiveRangedDamageBonus(Unit unit, bool hasHighGround = false)
    {
        if (unit.HasSkillEffect("sniper") && hasHighGround) return 1;
        return 0;
    }

    public static int GetPassiveRangeBonus(Unit unit) => unit.HasSkillEffect("sniper") ? 2 : 0;

    // ============================================================================
    // 光环与誓言
    // ============================================================================

    public static Godot.Collections.Dictionary GetCommandAuraBonus(Unit unit)
    {
        if (unit.HasSkillEffect("command_aura"))
            return new Godot.Collections.Dictionary { { "attack_bonus", 1 }, { "ac_bonus", 1 } };
        return new Godot.Collections.Dictionary();
    }

    public static float GetVowOfVengeanceBonus(Unit unit, Unit target)
    {
        if (!unit.HasSkillEffect("vow_of_vengeance") || unit.Data == null) return 1.0f;
        var markedId = unit.Data.Get("_vengeance_target_id");
        if (markedId.VariantType == Variant.Type.Nil || markedId.AsInt64() == -1) return 1.0f;
        if (target.GetInstanceId() == (ulong)markedId.AsInt64()) return 1.25f;
        return 1.0f;
    }

    public static void SetVengeanceTarget(Unit unit, Unit target)
    {
        unit.Data?.Set("_vengeance_target_id", (long)target.GetInstanceId());
    }

    public static void OnVengeanceTargetKilled(Unit avenger, IEnumerable<Unit> allAllies)
    {
        if (!avenger.HasSkillEffect("vow_of_vengeance")) return;
        foreach (var ally in allAllies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int heal = Math.Max(1, (int)(ally.Model.GetMaxHp() * 0.1f));
                ally.Heal(heal);
            }
        }
    }

    public static int GetRoyalPresenceSaveBonus(Unit unit) => unit.HasSkillEffect("royal_presence") ? 2 : 0;
    public static int GetLifeSpringHeal(Unit unit) => unit.HasSkillEffect("life_spring") ? RPGRuleEngine.RollDice(1, 6) : 0;

    // ============================================================================
    // Keystone 惩罚 — 见文件末尾扩展版本
    // ============================================================================

    // ============================================================================
    // 灵魂守护
    // ============================================================================

    /// <summary>灵魂守护：友军死亡时触发恢复（每场战斗一次）</summary>
    public static int TriggerSoulGuardian(Unit guardian, Unit dyingAlly)
    {
        if (!guardian.HasSkillEffect("soul_guardian") || guardian.Data == null)
            return 0;
        var usedVar = guardian.Data.Get("_soul_guardian_used");
        if (usedVar.VariantType != Variant.Type.Nil && usedVar.AsBool())
            return 0;
        guardian.Data.Set("_soul_guardian_used", true);
        int wisMod = RPGRuleEngine.GetStatModifier(guardian.Data.Wis);
        int heal = RPGRuleEngine.RollDice(1, 10) + wisMod;
        return Math.Max(1, heal);
    }

    // ============================================================================
    // 非战斗被动（经济/商店）— 见文件末尾扩展版本
    // ============================================================================

    // ============================================================================
    // 重甲精通（heavy_armor）
    // ============================================================================

    /// <summary>重甲速度惩罚</summary>
    public static int GetHeavyArmorSpeedPenalty(Unit unit) => unit.HasSkillEffect("heavy_armor") ? 1 : 0;

    // ============================================================================
    // 嗜血（bloodthirst）
    // ============================================================================

    /// <summary>击杀后额外行动标记</summary>
    public static bool HasBloodthirstExtraAction(Unit unit) => unit.HasSkillEffect("bloodthirst");

    // ============================================================================
    // 坚守（fortify）
    // ============================================================================

    /// <summary>受伤时临时AC+2（通过status effect触发）</summary>
    public static bool HasFortify(Unit unit) => unit.HasSkillEffect("fortify");

    public static int GetFortifyAcBonus(Unit unit)
    {
        if (!unit.HasSkillEffect("fortify") || unit.Data == null) return 0;
        var var_ = unit.Data.Get("_fortify_active");
        return (var_.VariantType != Variant.Type.Nil && var_.AsBool()) ? 2 : 0;
    }

    public static void ActivateFortify(Unit unit)
    {
        if (unit.HasSkillEffect("fortify"))
            unit.Data?.Set("_fortify_active", true);
    }

    public static void DeactivateFortify(Unit unit)
    {
        unit.Data?.Set("_fortify_active", false);
    }

    // ============================================================================
    // 不屈（unyielding）
    // ============================================================================

    /// <summary>HP<25%时受到伤害减半</summary>
    public static float GetUnyieldingDamageReduction(Unit unit)
    {
        if (!unit.HasSkillEffect("unyielding") || unit.Data == null) return 1.0f;
        float hpPct = (float)unit.CurrentHp / unit.Model.GetMaxHp();
        return hpPct < 0.25f ? 0.5f : 1.0f;
    }

    // ============================================================================
    // 不灭之躯（immortal_body）
    // ============================================================================

    /// <summary>是否拥有不灭之躯</summary>
    public static bool HasImmortalBody(Unit unit) => unit.HasSkillEffect("immortal_body");

    /// <summary>不灭之躯CON检定存活</summary>
    public static bool RollImmortalBodySave(Unit unit)
    {
        if (!unit.HasSkillEffect("immortal_body") || unit.Data == null) return false;
        // 每场战斗只能触发一次
        var usedVar = unit.Data.Get("_immortal_body_used");
        if (usedVar.VariantType != Variant.Type.Nil && usedVar.AsBool()) return false;
        int conScore = unit.Data.Con;
        int prof = RPGRuleEngine.GetProficiencyBonus(unit.Data.Level);
        var result = RPGRuleEngine.MakeSave(conScore, prof, true, 15);
        if ((bool)result["success"])
        {
            unit.Data.Set("_immortal_body_used", true);
            return true;
        }
        return false;
    }

    // ============================================================================
    // 巨人之力（giant_strength）
    // ============================================================================

    /// <summary>近战伤害+3</summary>
    public static int GetGiantStrengthDamageBonus(Unit unit) => unit.HasSkillEffect("giant_strength") ? 3 : 0;

    /// <summary>单手武器是否视为双手</summary>
    public static bool TreatsOneHandedAsTwoHanded(Unit unit) => unit.HasSkillEffect("giant_strength");

    // ============================================================================
    // 法术命中（spell_hit_plus_1）
    // ============================================================================

    public static int GetPassiveSpellHitBonus(Unit unit) => unit.HasSkillEffect("spell_hit_plus_1") ? 1 : 0;

    // ============================================================================
    // 法术反射（spell_reflect）
    // ============================================================================

    /// <summary>是否拥有法术反射（每回合1次）</summary>
    public static bool HasSpellReflect(Unit unit) => unit.HasSkillEffect("spell_reflect");

    /// <summary>尝试反射法术（每回合1次）</summary>
    public static bool TryReflectSpell(Unit unit)
    {
        if (!unit.HasSkillEffect("spell_reflect") || unit.Data == null) return false;
        var usedVar = unit.Data.Get("_spell_reflect_used_this_turn");
        if (usedVar.VariantType != Variant.Type.Nil && usedVar.AsBool()) return false;
        unit.Data.Set("_spell_reflect_used_this_turn", true);
        return true;
    }

    public static void ResetSpellReflect(Unit unit)
    {
        unit.Data?.Set("_spell_reflect_used_this_turn", false);
    }

    // ============================================================================
    // 知识之力（knowledge_power）
    // ============================================================================

    /// <summary>法术伤害+INT修正</summary>
    public static int GetKnowledgePowerBonus(Unit unit)
    {
        if (!unit.HasSkillEffect("knowledge_power") || unit.Data == null) return 0;
        return RPGRuleEngine.GetStatModifier(unit.Data.Intel);
    }

    // ============================================================================
    // 命运之眼（fate_eye）
    // ============================================================================

    /// <summary>是否可重骰失败豁免</summary>
    public static bool HasFateEyeReroll(Unit unit) => unit.HasSkillEffect("fate_eye");

    /// <summary>尝试消耗命运之眼重骰</summary>
    public static bool TryFateEyeReroll(Unit unit)
    {
        if (!unit.HasSkillEffect("fate_eye") || unit.Data == null) return false;
        var usedVar = unit.Data.Get("_fate_eye_used_this_turn");
        if (usedVar.VariantType != Variant.Type.Nil && usedVar.AsBool()) return false;
        unit.Data.Set("_fate_eye_used_this_turn", true);
        return true;
    }

    public static void ResetFateEye(Unit unit)
    {
        unit.Data?.Set("_fate_eye_used_this_turn", false);
    }

    // ============================================================================
    // 生命精通（life_mastery）
    // ============================================================================

    /// <summary>治疗效果+50%</summary>
    public static float GetDivineHandHealMultiplier(Unit unit) => unit.HasSkillEffect("life_mastery") ? 1.5f : 1.0f;

    // ============================================================================
    // 闪电反射（lightning_reflex）
    // ============================================================================

    /// <summary>先攻+5</summary>
    public static int GetLightningReflexInitiativeBonus(Unit unit) => unit.HasSkillEffect("lightning_reflex") ? 5 : 0;

    /// <summary>首轮攻击是否优势</summary>
    public static bool HasLightningReflexFirstAttackAdvantage(Unit unit)
    {
        if (!unit.HasSkillEffect("lightning_reflex") || unit.Data == null) return false;
        var usedVar = unit.Data.Get("_lightning_reflex_first_attack_used");
        return usedVar.VariantType == Variant.Type.Nil || !usedVar.AsBool();
    }

    public static void ConsumeLightningReflexAdvantage(Unit unit)
    {
        unit.Data?.Set("_lightning_reflex_first_attack_used", true);
    }

    public static void ResetLightningReflex(Unit unit)
    {
        unit.Data?.Set("_lightning_reflex_first_attack_used", false);
    }

    // ============================================================================
    // 威慑（intimidate）被动残留
    // ============================================================================

    /// <summary>威慑：目标攻击-2持续3回合（通过status effect实现，此处提供查询）</summary>
    public static int GetIntimidatedAttackPenalty(Unit unit)
    {
        if (unit.Data == null) return 0;
        foreach (var eff in unit.Data.Runtime.ActiveStatusEffects)
        {
            if (eff.Id == "intimidated") return 2;
        }
        return 0;
    }

    // ============================================================================
    // 外交官（diplomat）— 扩展商店折扣
    // ============================================================================

    /// <summary>综合商店折扣（diplomat: 15%off）</summary>
    public static float GetDiplomatDiscount(Unit unit)
    {
        if (unit.HasSkillEffect("diplomat")) return 0.85f;
        return 1.0f;
    }

    /// <summary>额外金币倍率（merchant_empire: +20%）</summary>
    public static float GetGoldBonusMultiplier(Unit unit)
    {
        if (unit.HasSkillEffect("merchant_empire")) return 1.2f;
        return 1.0f;
    }

    /// <summary>稀有物品概率加成（merchant_empire: +15%）</summary>
    public static float GetRareItemChanceBonus(Unit unit)
    {
        if (unit.HasSkillEffect("merchant_empire")) return 0.15f;
        return 0.0f;
    }

    // ============================================================================
    // 巨人之力额外：近战伤害倍率
    // ============================================================================

    /// <summary>近战伤害固定加成（汇总所有来源）</summary>
    public static int GetTotalPassiveMeleeDamageFlatBonus(Unit unit)
    {
        int bonus = GetPassiveMeleeDamageBonus(unit);
        bonus += GetGiantStrengthDamageBonus(unit);
        return bonus;
    }

    // ============================================================================
    // Keystone速度惩罚（扩展）
    // ============================================================================

    public static int GetKeystoneSpeedPenalty(Unit unit)
    {
        int penalty = 0;
        if (unit.HasSkillEffect("diamond_body")) penalty += 2;
        if (unit.HasSkillEffect("life_spring")) penalty += 1;
        if (unit.HasSkillEffect("immortal_body")) penalty += 2;
        return penalty;
    }

    // ============================================================================
    // Keystone AC惩罚（扩展）
    // ============================================================================

    public static int GetKeystoneAcPenalty(Unit unit)
    {
        int penalty = 0;
        if (unit.HasSkillEffect("berserk_power")) penalty += 3;
        if (unit.HasSkillEffect("lightning_reflex")) penalty += 1;
        return penalty;
    }

    // ============================================================================
    // Keystone HP修正（扩展）
    // ============================================================================

    public static float GetKeystoneHpModifier(Unit unit)
    {
        float mod = 1.0f;
        if (unit.HasSkillEffect("ghost_step")) mod *= 0.8f;
        if (unit.HasSkillEffect("royal_presence")) mod *= 0.8f;
        if (unit.HasSkillEffect("soul_guardian")) mod *= 0.9f;
        return mod;
    }

    // ============================================================================
    // Keystone法力惩罚（fate_eye）
    // ============================================================================

    public static int GetKeystoneMaxManaPenalty(Unit unit)
    {
        if (unit.HasSkillEffect("fate_eye")) return 10;
        return 0;
    }

    // ============================================================================
    // 先攻加成（汇总）
    // ============================================================================

    public static int GetPassiveInitiativeBonus(Unit unit)
    {
        return GetLightningReflexInitiativeBonus(unit);
    }

    // ============================================================================
    // 非战斗被动 — 外交官扩展
    // ============================================================================

    /// <summary>商店折扣（合并diplomacy和diplomat）</summary>
    public static float GetShopDiscount(Unit unit)
    {
        if (unit.HasSkillEffect("diplomacy")) return 0.8f;
        if (unit.HasSkillEffect("diplomat")) return 0.85f;
        return 1.0f;
    }

    /// <summary>招募折扣（合并diplomacy和diplomat）</summary>
    public static float GetRecruitDiscount(Unit unit)
    {
        if (unit.HasSkillEffect("diplomacy")) return 0.85f;
        if (unit.HasSkillEffect("diplomat")) return 0.85f;
        return 1.0f;
    }

    // ============================================================================
    // 速度总惩罚
    // ============================================================================

    /// <summary>总速度惩罚（汇总所有来源）</summary>
    public static int GetTotalSpeedPenalty(Unit unit)
    {
        return GetKeystoneSpeedPenalty(unit) + GetHeavyArmorSpeedPenalty(unit);
    }
}
