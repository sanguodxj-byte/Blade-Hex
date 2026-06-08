// CareerSkillResolver.cs
// 职业技能被动效果查询接口 — v0.8: 全部改读 buff 而非职业称号
//             v1.0: 新增 v1 分阶职业常驻被动查询接口（直接读 CareerSkillRegistry）
//
// 与 PassiveSkillResolver 的区别:
//   - CareerSkillResolver: 查询职业技能效果（v0.8=临时buff, v1=常驻被动/主动）
//   - PassiveSkillResolver: 查询技能树的永久被动效果（装备即生效，无持续时间）
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat.Buff;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 职业技能效果查询接口
/// v0.8 路径: 检查对应 buff 是否激活（因 v0.8 所有技能都是主动大招）
/// v1.0 路径: 直接检查角色当前职业称号对应的 CareerSkillData (常驻被动/主动)
///   v1 查询通过 unit.HasCareerSkillEffect(effectId) 走 CharacterSkillTree.HasCareerSkill
/// </summary>
public static class CareerSkillResolver
{
    // ============================================================================
    // v1.0 职业技能查询（常驻被动/主动 — 读 CareerSkillRegistry）
    // ============================================================================

    /// <summary>获取当前职业对应的技能数据 (v1)</summary>
    public static CareerSkillData? GetV1CareerSkill(Unit unit)
        => unit.GetCareerSkill();

    /// <summary>当前职业是否拥有指定 effectId 的 v1 技能</summary>
    public static bool HasV1CareerSkill(Unit unit, string effectId)
        => unit.HasCareerSkillEffect(effectId);

    /// <summary>当前职业是否为被动 (1-4 属性)</summary>
    public static bool IsPassiveCareer(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill != null && skill.IsPassive;
    }

    /// <summary>当前职业是否为五属性主动</summary>
    public static bool IsFiveAttributeActive(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill != null && skill.IsFiveAttribute && skill.IsActive;
    }

    /// <summary>当前职业是否为万象（六属性）</summary>
    public static bool IsParagon(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill != null && skill.IsSixAttribute;
    }

    /// <summary>获取当前职业的属性数量</summary>
    public static int GetV1AttributeCount(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill?.AttributeCount ?? 0;
    }

    /// <summary>v1 职业是否有主动按钮可显示</summary>
    public static bool HasV1ActiveSkillInUI(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill != null && skill.IsActive && skill.ShowInCombatUi;
    }

    // ============================================================================
    // v0.8 职业技能标识符查询（保留：工具方法）
    // ============================================================================

    public static string? GetCareerSkillId(Unit unit)
    {
        var skill = unit.GetCareerSkill();
        return skill?.EffectId;
    }

    /// <summary>v0.8 兼容: 检查是否拥有某个效果(读 buff)</summary>
    public static bool HasCareerSkill(Unit unit, string effectId)
    {
        return unit.HasCareerSkillEffect(effectId);
    }

    // ============================================================================
    // 被动效果 — 闪避/防御类（全部改读 buff）
    // ============================================================================

    /// <summary>游侠-箭雨：被远程命中后位移1格重新判定</summary>
    public static bool HasEvadeVolley(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "volley");

    /// <summary>贤者-预知回避：每回合首次被攻击可消耗1法力使攻击劣势</summary>
    public static bool HasForewarning(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "forewarning");

    public static int GetForewarningManaCost(Unit unit)
    {
        if (!HasForewarning(unit)) return 0;
        return 1;
    }

    /// <summary>铁壁守护-殉道守护：替周围友军承受50%致命伤害</summary>
    public static bool HasMartyrsGuard(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "martyrs_guard");

    public static float GetMartyrDamageShare() => 0.5f;

    /// <summary>毁灭之主-毁灭预兆：AC固定为10</summary>
    public static bool HasFixedAc(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "harbinger");

    public static int GetFixedAcValue() => 10;

    /// <summary>铁血暴君-暴君之怒：暴击阈值固定为20</summary>
    public static bool HasFixedCritThreshold(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "tyrant_wrath");

    public static int GetFixedCritThreshold() => 20;

    // ============================================================================
    // 被动效果 — 伤害/攻击类
    // ============================================================================

    /// <summary>处刑者-终结宣告：攻击HP≤30%目标暴击阈值降低</summary>
    public static bool HasDeathSentence(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "death_sentence");

    public static int GetDeathSentenceCritReduction(Unit target)
    {
        float hpPct = (float)target.CurrentHp / target.Model.GetMaxHp();
        int reduction = 0;
        if (hpPct <= 0.3f) reduction += 3;
        return reduction;
    }

    public static float GetDeathSentenceCritDamageBonus() => 0.5f;

    /// <summary>碎颅者-弱点粉碎：攻击HP<100%目标额外伤害</summary>
    public static bool HasCrushWeakPoint(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "crush_weak");

    public static int GetCrushWeakPointBonus(Unit attacker, Unit target)
    {
        if (target.CurrentHp >= target.Model.GetMaxHp()) return 0;
        float lostPercent = 1.0f - (float)target.CurrentHp / target.Model.GetMaxHp();
        return (int)(lostPercent * 10);
    }

    public static int GetCrushWeakPointDrPenetration() => 3;

    /// <summary>秘典守护-知识壁垒：法术伤害INT检定减半</summary>
    public static bool HasBulwarkOfLore(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "bulwark_lore");

    /// <summary>暴君之怒：伤害不受debuff降低</summary>
    public static bool HasUndiminishedDamage(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "tyrant_wrath");

    // ============================================================================
    // 被动效果 — 士气/指挥类
    // ============================================================================

    /// <summary>恐惧魔将-铁腕统御：周围2格友军免疫恐惧</summary>
    public static bool HasIronGrip(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "iron_grip");

    public static int GetIronGripRange() => 2;

    // ============================================================================
    // 被动效果 — 移动/机动类
    // ============================================================================

    /// <summary>暮光行者-暮光步：首次移动不触发借机攻击</summary>
    public static bool HasTwilightStride(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "twilight_stride");

    /// <summary>独行术-孤影行者：无友军时AC+2移动-1AP</summary>
    public static bool HasLoneOperative(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "lone_op");

    public static int GetLoneOperativeAcPenaltyPerAlly() => 1;
    public static int GetLoneOperativeMinAc() => 8;
    public static int GetLoneOperativeSoloAcBonus() => 2;

    /// <summary>荒野酋长-野性直觉：野外地形移动力+1</summary>
    public static bool HasFeralInstinct(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "feral_self");

    // ============================================================================
    // 被动效果 — 法术类
    // ============================================================================

    /// <summary>毁灭之主-毁灭预兆：每失去10%HP法术DC+1</summary>
    public static int GetHarbingerDcBonus(Unit unit)
    {
        if (unit.Data == null || !BuffSystem.HasBuff(unit.Data, "harbinger")) return 0;
        float lostPercent = 1.0f - (float)unit.CurrentHp / unit.Model.GetMaxHp();
        int bonus = (int)(lostPercent * 10);
        return Math.Min(bonus, 5);
    }

    /// <summary>战法师-法力护盾：消耗法力减伤</summary>
    public static bool HasManaShieldPassive(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "mana_shield");

    public static int GetManaShieldReduction(Unit unit)
    {
        if (unit.Data == null) return 0;
        int intMod = RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(unit.Data));
        return Math.Max(1, intMod * 2);
    }

    /// <summary>万法通识-万灵使者：所有大节点数值+5%</summary>
    public static bool HasJackOfAllTrades(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "jack_of_all");

    public static float GetJackOfAllTradesBonus() => 0.20f;

    // ============================================================================
    // 被动效果 — 野蛮直觉/自动选择
    // ============================================================================

    /// <summary>野蛮直觉-怒涛战神：自动攻击HP最低敌人</summary>
    public static bool HasSavageInstinct(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "savage");

    public static float GetSavageInstinctExecuteThreshold() => 0.3f;

    // ============================================================================
    // 被动效果 — 反击类
    // ============================================================================

    /// <summary>决斗家-以伤换伤：被近战命中后100%伤害反击</summary>
    public static bool HasRiposte(Unit unit)
        => unit.Data != null && (BuffSystem.HasBuff(unit.Data, "riposte") || unit.HasCareerSkillEffect("duelist_riposte_counter"));

    public static float GetRiposteDamageMultiplier() => 1.0f;
    public static int GetRiposteApRecoveryOnCrit() => 2;

    /// <summary>老兵-临危不乱：HP首次降至50%以下触发AC+3反击×1.5</summary>
    public static bool HasOldTimer(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "old_timer");

    // ============================================================================
    // 被动效果 — 战歌 (诗人) —— 保留原有称号检查不变
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
    // 被动效果 — 灵风大师/风之眷顾 —— 改读 buff 叠层
    // ============================================================================

    public static bool HasWindFavor(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "wind_favor");

    public static int GetWindFavorRangeBonus(Unit unit)
    {
        if (unit.Data == null) return 0;
        var buff = unit.Model.FindBuff("wind_favor");
        return buff?.CurrentStacks ?? 0;
    }

    public static void AddWindStack(Unit unit)
    {
        if (unit.Data == null) return;
        BladeHex.Combat.Buff.BuffSystem.IncrementStacks(unit.Data, "wind_favor");
    }

    public static void ClearWindStacks(Unit unit)
    {
        if (unit.Data == null) return;
        BladeHex.Combat.Buff.BuffSystem.SetStacks(unit.Data, "wind_favor", 1);
    }

    // ============================================================================
    // 被动效果 — 大贤者信息优势
    // ============================================================================

    public static bool HasOmnibus(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "omnibus");

    // ============================================================================
    // 被动效果 — 镜像分身
    // ============================================================================

    public static bool HasMirrorImage(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "mirror_image");

    // ============================================================================
    // 被动效果 — 无声击
    // ============================================================================

    public static bool HasSilentStrike(Unit unit)
        => unit.Data != null && BuffSystem.HasBuff(unit.Data, "silent_strike");
}
