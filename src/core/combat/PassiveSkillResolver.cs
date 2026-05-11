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

    /// <summary>暴击伤害倍率（默认x2，critical_x3 → x3）</summary>
    public static int GetCritMultiplier(Unit unit)
    {
        if (unit.HasSkillEffect("critical_x3")) return 3;
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
                float hpPct = (float)unit.CurrentHp / unit.GetMaxHp();
                if (hpPct < 0.25f) return 1.5f;
            }
        }
        return 1.0f;
    }

    /// <summary>物理伤害减免</summary>
    public static int GetPassiveDamageReduction(Unit unit, string damageType = "physical")
    {
        int reduction = 0;
        if (damageType == "physical" && unit.HasSkillEffect("iron_wall")) reduction += 3;
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
                float hpPct = (float)unit.CurrentHp / unit.GetMaxHp();
                if (hpPct < 0.25f) bonus += 5;
            }
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
                int heal = Math.Max(1, (int)(ally.GetMaxHp() * 0.1f));
                ally.CurrentHp = Math.Min(ally.CurrentHp + heal, ally.GetMaxHp());
            }
        }
    }

    public static int GetRoyalPresenceSaveBonus(Unit unit) => unit.HasSkillEffect("royal_presence") ? 2 : 0;
    public static int GetLifeSpringHeal(Unit unit) => unit.HasSkillEffect("life_spring") ? RPGRuleEngine.RollDice(1, 6) : 0;

    // ============================================================================
    // Keystone 惩罚
    // ============================================================================

    public static int GetKeystoneAcPenalty(Unit unit) => unit.HasSkillEffect("berserk_power") ? 3 : 0;

    public static float GetKeystoneHpModifier(Unit unit)
    {
        float mod = 1.0f;
        if (unit.HasSkillEffect("ghost_step")) mod *= 0.8f;
        if (unit.HasSkillEffect("royal_presence")) mod *= 0.8f;
        return mod;
    }

    public static int GetKeystoneSpeedPenalty(Unit unit)
    {
        int penalty = 0;
        if (unit.HasSkillEffect("diamond_body")) penalty += 2;
        if (unit.HasSkillEffect("life_spring")) penalty += 1;
        return penalty;
    }

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
    // 非战斗被动（经济/商店）
    // ============================================================================

    /// <summary>商店折扣（diplomacy: 八折）</summary>
    public static float GetShopDiscount(Unit unit)
    {
        if (unit.HasSkillEffect("diplomacy")) return 0.8f;
        return 1.0f;
    }

    /// <summary>招募折扣（diplomacy: 八五折）</summary>
    public static float GetRecruitDiscount(Unit unit)
    {
        if (unit.HasSkillEffect("diplomacy")) return 0.85f;
        return 1.0f;
    }

    /// <summary>额外金币倍率（merchant_empire）</summary>
    public static float GetGoldBonusMultiplier(Unit unit)
    {
        if (unit.HasSkillEffect("merchant_empire")) return 1.0f;
        return 1.0f;
    }

    /// <summary>稀有物品概率加成（merchant_empire: +15%）</summary>
    public static float GetRareItemChanceBonus(Unit unit)
    {
        if (unit.HasSkillEffect("merchant_empire")) return 0.15f;
        return 0.0f;
    }
}
