// CareerSkillResolver.cs
// 职业技能被动效果查询接口 — 供 CombatResolver / Unit 调用
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 职业技能被动效果查询 — 类似 PassiveSkillResolver，专用于职业称号技能
/// </summary>
public static class CareerSkillResolver
{
    // ============================================================================
    // 职业技能标识符查询
    // ============================================================================

    public static string? GetCareerSkillId(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill?.EffectId;
    }

    public static bool HasCareerSkill(Unit unit, string effectId)
    {
        return unit.HasCareerSkillEffect(effectId);
    }

    // ============================================================================
    // 被动效果 — 闪避/防御类
    // ============================================================================

    /// <summary>游侠-散射回避：被远程命中后位移1格重新判定</summary>
    public static bool HasEvadeVolley(Unit unit) => HasCareerSkill(unit, "ranger_evade_volley");

    /// <summary>贤者-预知回避：每回合首次被攻击可消耗1法力使攻击劣势</summary>
    public static bool HasForewarning(Unit unit) => HasCareerSkill(unit, "sage_forewarning");

    public static int GetForewarningManaCost(Unit unit)
    {
        if (!HasForewarning(unit)) return 0;
        return 1;
    }

    /// <summary>铁壁守护-殉道守护：替周围友军承受50%致命伤害</summary>
    public static bool HasMartyrsGuard(Unit unit) => HasCareerSkill(unit, "ironbulwark_martyrs_guard");

    public static float GetMartyrDamageShare() => 0.5f;

    /// <summary>毁灭之主-毁灭预兆：AC固定为10</summary>
    public static bool HasFixedAc(Unit unit) => HasCareerSkill(unit, "lordofruin_harbinger");

    public static int GetFixedAcValue() => 10;

    /// <summary>铁血暴君-暴君之怒：暴击阈值固定为20</summary>
    public static bool HasFixedCritThreshold(Unit unit) => HasCareerSkill(unit, "irontyrant_wrath");

    public static int GetFixedCritThreshold() => 20;

    // ============================================================================
    // 被动效果 — 伤害/攻击类
    // ============================================================================

    /// <summary>处刑者-终结宣告：攻击HP≤30%目标暴击阈值降低</summary>
    public static bool HasDeathSentence(Unit unit) => HasCareerSkill(unit, "executioner_death_sentence");

    public static int GetDeathSentenceCritReduction(Unit target)
    {
        float hpPct = (float)target.CurrentHp / target.Model.GetMaxHp();
        int reduction = 0;
        if (hpPct <= 0.3f) reduction += 3;
        // 流血/中毒目标额外-1
        // 这里简化处理，实际需要查 status effect
        return reduction;
    }

    public static float GetDeathSentenceCritDamageBonus() => 0.5f;

    /// <summary>碎颅者-弱点粉碎：攻击HP<100%目标额外伤害</summary>
    public static bool HasCrushWeakPoint(Unit unit) => HasCareerSkill(unit, "skullcrusher_crush_weakpoint");

    public static int GetCrushWeakPointBonus(Unit attacker, Unit target)
    {
        if (target.CurrentHp >= target.Model.GetMaxHp()) return 0;
        float lostPercent = 1.0f - (float)target.CurrentHp / target.Model.GetMaxHp();
        return (int)(lostPercent * 10);
    }

    public static int GetCrushWeakPointDrPenetration() => 3;

    /// <summary>秘典守护-知识壁垒：法术伤害INT检定减半</summary>
    public static bool HasBulwarkOfLore(Unit unit) => HasCareerSkill(unit, "arcanewarden_bulwark_of_lore");

    /// <summary>暴君之怒：伤害不受debuff降低</summary>
    public static bool HasUndiminishedDamage(Unit unit) => HasCareerSkill(unit, "irontyrant_wrath");

    // ============================================================================
    // 被动效果 — 士气/指挥类
    // ============================================================================

    /// <summary>霸主-恐惧光环：周围2格敌方每回合士气-3</summary>
    public static bool HasAuraOfDread(Unit unit) => HasCareerSkill(unit, "overlord_aura_of_dread");

    public static int GetAuraOfDreadRange() => 2;
    public static int GetAuraOfDreadMoraleDrain() => 3;

    /// <summary>恐惧魔将-铁腕统御：周围2格友军免疫恐惧</summary>
    public static bool HasIronGrip(Unit unit) => HasCareerSkill(unit, "dreadgeneral_iron_grip");

    public static int GetIronGripRange() => 2;

    // ============================================================================
    // 被动效果 — 移动/机动类
    // ============================================================================

    /// <summary>暮光行者-暮光步：首次移动不触发借机攻击</summary>
    public static bool HasTwilightStride(Unit unit) => HasCareerSkill(unit, "twilight_walker_stride");

    /// <summary>独行术-孤影行者：无友军时AC+2移动-1AP</summary>
    public static bool HasLoneOperative(Unit unit) => HasCareerSkill(unit, "loneshadow_lone_operative");

    public static int GetLoneOperativeAcPenaltyPerAlly() => 1;
    public static int GetLoneOperativeMinAc() => 8;
    public static int GetLoneOperativeSoloAcBonus() => 2;

    /// <summary>荒野酋长-野性直觉：野外地形移动力+1</summary>
    public static bool HasFeralInstinct(Unit unit) => HasCareerSkill(unit, "warchief_feral_instinct");

    // ============================================================================
    // 被动效果 — 法术类
    // ============================================================================

    /// <summary>毁灭之主-毁灭预兆：每失去10%HP法术DC+1</summary>
    public static int GetHarbingerDcBonus(Unit unit)
    {
        if (!HasCareerSkill(unit, "lordofruin_harbinger")) return 0;
        float lostPercent = 1.0f - (float)unit.CurrentHp / unit.Model.GetMaxHp();
        int bonus = (int)(lostPercent * 10);
        return Math.Min(bonus, 5);
    }

    /// <summary>战法师-法力护盾：消耗法力减伤</summary>
    public static bool HasManaShieldPassive(Unit unit) => HasCareerSkill(unit, "battlemage_mana_shield");

    public static int GetManaShieldReduction(Unit unit)
    {
        if (unit.Data == null) return 0;
        int intMod = RPGRuleEngine.GetStatModifier(unit.Data.Intel);
        return Math.Max(1, intMod * 2);
    }

    /// <summary>万法通识-万灵使者：所有大节点数值+5%</summary>
    public static bool HasJackOfAllTrades(Unit unit) => HasCareerSkill(unit, "emissary_jack_of_all_trades");

    public static float GetJackOfAllTradesBonus() => 0.05f;

    // ============================================================================
    // 被动效果 — 野蛮直觉/自动选择
    // ============================================================================

    /// <summary>野蛮直觉-怒涛战神：自动攻击HP最低敌人</summary>
    public static bool HasSavageInstinct(Unit unit) => HasCareerSkill(unit, "wrathavatar_savage_instinct");

    public static float GetSavageInstinctExecuteThreshold() => 0.2f;

    // ============================================================================
    // 被动效果 — 反击类
    // ============================================================================

    /// <summary>决斗家-以伤换伤：被近战命中后100%伤害反击</summary>
    public static bool HasRiposte(Unit unit) => HasCareerSkill(unit, "duelist_riposte");

    public static float GetRiposteDamageMultiplier() => 1.0f;
    public static int GetRiposteApRecoveryOnCrit() => 2;

    /// <summary>老兵-临危不乱：HP首次降至50%以下触发AC+3反击×1.5</summary>
    public static bool HasOldTimer(Unit unit) => HasCareerSkill(unit, "veteran_old_timer");

    // ============================================================================
    // 被动效果 — 战歌 (诗人)
    // ============================================================================

    public static bool HasBattleHymn(Unit unit) => HasCareerSkill(unit, "bard_battle_hymn");

    public enum HymnType { March, Bulwark, Fury }

    /// <summary>获取/设置当前战歌类型（存于 UnitData 自定义属性）</summary>
    public static HymnType GetCurrentHymn(Unit unit)
    {
        if (unit.Data == null) return HymnType.March;
        var v = unit.Data.Get("_career_hymn_type");
        if (v.VariantType == Variant.Type.Int)
            return (HymnType)v.AsInt32();
        return HymnType.March;
    }

    public static void SetCurrentHymn(Unit unit, HymnType type)
    {
        unit.Data?.Set("_career_hymn_type", (int)type);
    }

    public static int GetHymnAcBonus(Unit unit)
    {
        if (!HasBattleHymn(unit)) return 0;
        return GetCurrentHymn(unit) == HymnType.Bulwark ? 1 : 0;
    }

    public static float GetHymnDamageBonus(Unit unit)
    {
        if (!HasBattleHymn(unit)) return 0.0f;
        return GetCurrentHymn(unit) == HymnType.Fury ? 0.1f : 0.0f;
    }

    public static int GetHymnMoveApReduction(Unit unit)
    {
        if (!HasBattleHymn(unit)) return 0;
        return GetCurrentHymn(unit) == HymnType.March ? 1 : 0;
    }

    // ============================================================================
    // 被动效果 — 灵风大师/风之眷顾
    // ============================================================================

    public static bool HasWindFavor(Unit unit) => HasCareerSkill(unit, "zephyrmaster_wind_favor");

    public static int GetWindFavorRangeBonus(Unit unit)
    {
        if (!HasWindFavor(unit) || unit.Data == null) return 0;
        var v = unit.Data.Get("_wind_stacks");
        int stacks = v.VariantType != Variant.Type.Nil ? v.AsInt32() : 0;
        return stacks;
    }

    public static void AddWindStack(Unit unit)
    {
        if (!HasWindFavor(unit) || unit.Data == null) return;
        var v = unit.Data.Get("_wind_stacks");
        int current = v.VariantType != Variant.Type.Nil ? v.AsInt32() : 0;
        unit.Data.Set("_wind_stacks", Math.Min(current + 1, 3));
    }

    public static void ClearWindStacks(Unit unit)
    {
        if (HasWindFavor(unit))
            unit.Data?.Set("_wind_stacks", 0);
    }

    // ============================================================================
    // 被动效果 — 大贤者信息优势
    // ============================================================================

    public static bool HasOmnibus(Unit unit) => HasCareerSkill(unit, "archsage_omnibus");

    // ============================================================================
    // 被动效果 — 镜像分身
    // ============================================================================

    public static bool HasMirrorImage(Unit unit) => HasCareerSkill(unit, "illusionist_mirror_image");

    // ============================================================================
    // 被动效果 — 无声击
    // ============================================================================

    public static bool HasSilentStrike(Unit unit) => HasCareerSkill(unit, "silentdeath_silent_strike");
}
