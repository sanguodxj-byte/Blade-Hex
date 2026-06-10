// AttackResult.cs
// CombatResolver.ResolveAttack 的强类型返回值 — 替代 Godot.Collections.Dictionary。
//
// 设计原则：
//   - 所有字段为 readonly，通过 With* 方法或构造器初始化
//   - 隐式转换到 Godot.Collections.Dictionary，保证向后兼容
//   - 隐式转换到 bool，等价于 Hit
//   - ResolveAttack 内部使用字典，最终转换为 AttackResult 返回

using System;
using System.Collections.Generic;
using Godot;
using CombatUnit = BladeHex.Combat.Unit;

namespace BladeHex.Combat;

/// <summary>
/// CombatResolver.ResolveAttack 的强类型返回值。
/// </summary>
public sealed class AttackResult
{
    // =========================================================================
    // 核心字段
    // =========================================================================

    public bool Hit { get; set; }
    public bool Critical { get; set; }
    public bool Fumble { get; set; }
    public bool Graze { get; set; }
    public int Damage { get; set; }
    public int Roll { get; set; }
    public int AttackBonus { get; set; }
    public int TotalAttack { get; set; }
    public int TargetAc { get; set; }
    public int HitChancePercent { get; set; }

    // =========================================================================
    // 修饰符 / 状态
    // =========================================================================

    public bool Advantage { get; set; }
    public bool Disadvantage { get; set; }
    public bool IsCounter { get; set; }
    public bool IsFlanking { get; set; }
    public string FlankDirection { get; set; } = "front";
    public bool IsCharge { get; set; }

    // =========================================================================
    // 阻止 / 特殊
    // =========================================================================

    public bool BlockedByBuff { get; set; }
    public bool BlockedByElevation { get; set; }
    public string? Reason { get; set; }
    public bool OutOfAmmo { get; set; }

    // =========================================================================
    // 装甲 / 伤害详情
    // =========================================================================

    public bool ArmorPenetrated { get; set; }
    public int ArmorDamage { get; set; }
    public bool ShieldAbsorbed { get; set; }
    public int DamageReduction { get; set; }
    public bool CrushBonus { get; set; }
    public int CrushWeakBonus { get; set; }
    public bool UndiminishedDamage { get; set; }
    public bool CriticalNegatedByOathshield { get; set; }

    // =========================================================================
    // 重定向 / 转移
    // =========================================================================

    public int RedirectedDamage { get; set; }
    public long RedirectedToUnitId { get; set; }

    // =========================================================================
    // 死亡 / 生存
    // =========================================================================

    public bool DeathSaved { get; set; }
    public int BloodOathLeech { get; set; }

    // =========================================================================
    // 剑舞者额外攻击
    // =========================================================================

    public CombatUnit? BladeDancerExtraTarget { get; set; }
    public bool BladeDancerExtraHit { get; set; }
    public int BladeDancerExtraDamage { get; set; }
    public bool BladeDancerExtraCrit { get; set; }

    // =========================================================================
    // 反击 / 殉道
    // =========================================================================

    public int RiposteDamage { get; set; }
    public int MartyrsGuardShare { get; set; }
    public CombatUnit? MartyrsGuardDefender { get; set; }

    // =========================================================================
    // 殉道之誓
    // =========================================================================

    public int MartyrOathRedirect { get; set; }
    public CombatUnit? MartyrOathGuardian { get; set; }
    public int MartyrOathRestored { get; set; }

    // =========================================================================
    // 附加效果
    // =========================================================================

    public bool NextHitPoisonApplied { get; set; }
    public int NextHitPoisonDuration { get; set; }
    public int RefundRangedActionAp { get; set; }
    public bool DexGiantKillMoveRefresh { get; set; }
    public bool KillFullApRefund { get; set; }
    public bool ConquerorAoe { get; set; }

    // =========================================================================
    // 扩展字典（用于 modifiers 等嵌套数据）
    // =========================================================================

    public Godot.Collections.Dictionary Modifiers { get; set; } = new();
    public Godot.Collections.Dictionary Extensions { get; set; } = new();

    // =========================================================================
    // 从 Dictionary 构造
    // =========================================================================

    public static AttackResult FromDictionary(Godot.Collections.Dictionary dict)
    {
        var r = new AttackResult
        {
            Hit = dict.GetBool("hit"),
            Critical = dict.GetBool("critical"),
            Fumble = dict.GetBool("fumble"),
            Graze = dict.GetBool("graze"),
            Damage = dict.GetInt("damage"),
            Roll = dict.GetInt("roll"),
            AttackBonus = dict.GetInt("attack_bonus"),
            TotalAttack = dict.GetInt("total_attack"),
            TargetAc = dict.GetInt("target_ac"),
            HitChancePercent = dict.GetInt("hit_chance_percent"),
            Advantage = dict.GetBool("advantage"),
            Disadvantage = dict.GetBool("disadvantage"),
            IsCounter = dict.GetBool("is_counter"),
            IsFlanking = dict.GetBool("is_flanking"),
            FlankDirection = dict.GetStr("flank_direction") ?? "front",
            IsCharge = dict.GetBool("is_charge"),
            BlockedByBuff = dict.GetBool("blocked_by_buff"),
            BlockedByElevation = dict.GetBool("blocked_by_elevation"),
            Reason = dict.GetStr("reason"),
            OutOfAmmo = dict.GetBool("out_of_ammo"),
            ArmorPenetrated = dict.GetBool("armor_penetrated"),
            ArmorDamage = dict.GetInt("armor_damage"),
            ShieldAbsorbed = dict.GetInt("shield_ranged_absorbed") > 0,
            DamageReduction = dict.GetInt("damage_reduction"),
            CrushBonus = dict.GetBool("crush_bonus"),
            CrushWeakBonus = dict.GetInt("crush_weak_bonus"),
            UndiminishedDamage = dict.GetBool("undiminished_damage"),
            CriticalNegatedByOathshield = dict.GetBool("critical_negated_by_oathshield"),
            RedirectedDamage = dict.GetInt("redirected_damage"),
            RedirectedToUnitId = dict.GetLong("redirected_to_unit_id"),
            DeathSaved = dict.GetBool("death_saved"),
            BloodOathLeech = dict.GetInt("blood_oath_leech"),
            BladeDancerExtraTarget = dict.GetUnit("blade_dancer_extra_target"),
            BladeDancerExtraHit = dict.GetBool("blade_dancer_extra_hit"),
            BladeDancerExtraDamage = dict.GetInt("blade_dancer_extra_damage"),
            BladeDancerExtraCrit = dict.GetBool("blade_dancer_extra_crit"),
            RiposteDamage = dict.GetInt("riposte_damage"),
            MartyrsGuardShare = dict.GetInt("martyrs_guard_share"),
            MartyrsGuardDefender = dict.GetUnit("martyrs_guard_defender"),
            MartyrOathRedirect = dict.GetInt("martyr_oath_redirect"),
            MartyrOathGuardian = dict.GetUnit("martyr_oath_guardian"),
            MartyrOathRestored = dict.GetInt("martyr_oath_restored"),
            NextHitPoisonApplied = dict.GetBool("next_hit_poison_applied"),
            NextHitPoisonDuration = dict.GetInt("next_hit_poison_duration"),
            RefundRangedActionAp = dict.GetInt("refund_ranged_action_ap"),
            DexGiantKillMoveRefresh = dict.GetBool("dex_giant_kill_move_refresh"),
            KillFullApRefund = dict.GetBool("kill_full_ap_refund"),
            ConquerorAoe = dict.GetBool("conqueror_aoe"),
        };

        // 保留 modifiers
        if (dict.ContainsKey("modifiers"))
        {
            var modsVal = dict["modifiers"];
            if (modsVal.VariantType == Variant.Type.Dictionary)
                r.Modifiers = (Godot.Collections.Dictionary)modsVal;
        }

        return r;
    }

    // =========================================================================
    // 隐式转换到 Godot.Collections.Dictionary（向后兼容）
    // =========================================================================

    public static implicit operator Godot.Collections.Dictionary(AttackResult r)
    {
        var dict = new Godot.Collections.Dictionary();

        // 核心
        dict["hit"] = r.Hit;
        dict["critical"] = r.Critical;
        dict["fumble"] = r.Fumble;
        dict["graze"] = r.Graze;
        dict["damage"] = r.Damage;
        dict["roll"] = r.Roll;
        dict["attack_bonus"] = r.AttackBonus;
        dict["total_attack"] = r.TotalAttack;
        dict["target_ac"] = r.TargetAc;
        dict["hit_chance_percent"] = r.HitChancePercent;

        // 修饰符
        dict["advantage"] = r.Advantage;
        dict["disadvantage"] = r.Disadvantage;
        dict["is_counter"] = r.IsCounter;
        dict["is_flanking"] = r.IsFlanking;
        dict["flank_direction"] = r.FlankDirection;
        dict["is_charge"] = r.IsCharge;

        // 阻止
        if (r.BlockedByBuff) dict["blocked_by_buff"] = true;
        if (r.BlockedByElevation) dict["blocked_by_elevation"] = true;
        if (r.Reason != null) dict["reason"] = r.Reason;
        if (r.OutOfAmmo) dict["out_of_ammo"] = true;

        // 装甲
        dict["armor_penetrated"] = r.ArmorPenetrated;
        dict["armor_damage"] = r.ArmorDamage;
        if (r.ShieldAbsorbed) dict["shield_ranged_absorbed"] = 1;
        if (r.DamageReduction > 0) dict["damage_reduction"] = r.DamageReduction;
        if (r.CrushBonus) dict["crush_bonus"] = true;
        if (r.CrushWeakBonus > 0) dict["crush_weak_bonus"] = r.CrushWeakBonus;
        if (r.UndiminishedDamage) dict["undiminished_damage"] = true;
        if (r.CriticalNegatedByOathshield) dict["critical_negated_by_oathshield"] = true;

        // 重定向
        if (r.RedirectedDamage > 0)
        {
            dict["redirected_damage"] = r.RedirectedDamage;
            dict["redirected_to_unit_id"] = r.RedirectedToUnitId;
        }

        // 死亡/生存
        if (r.DeathSaved) dict["death_saved"] = true;
        if (r.BloodOathLeech > 0) dict["blood_oath_leech"] = r.BloodOathLeech;

        // 剑舞者
        if (r.BladeDancerExtraTarget != null)
        {
            dict["blade_dancer_extra_target"] = r.BladeDancerExtraTarget;
            dict["blade_dancer_extra_hit"] = r.BladeDancerExtraHit;
            dict["blade_dancer_extra_damage"] = r.BladeDancerExtraDamage;
            dict["blade_dancer_extra_crit"] = r.BladeDancerExtraCrit;
        }

        // 反击/殉道
        if (r.RiposteDamage > 0) dict["riposte_damage"] = r.RiposteDamage;
        if (r.MartyrsGuardShare > 0)
        {
            dict["martyrs_guard_share"] = r.MartyrsGuardShare;
            if (r.MartyrsGuardDefender != null) dict["martyrs_guard_defender"] = r.MartyrsGuardDefender;
        }

        // 殉道之誓
        if (r.MartyrOathRedirect > 0)
        {
            dict["martyr_oath_redirect"] = r.MartyrOathRedirect;
            if (r.MartyrOathGuardian != null) dict["martyr_oath_guardian"] = r.MartyrOathGuardian;
            dict["martyr_oath_restored"] = r.MartyrOathRestored;
        }

        // 附加效果
        if (r.NextHitPoisonApplied)
        {
            dict["next_hit_poison_applied"] = true;
            dict["next_hit_poison_duration"] = r.NextHitPoisonDuration;
        }
        if (r.RefundRangedActionAp > 0) dict["refund_ranged_action_ap"] = r.RefundRangedActionAp;
        if (r.DexGiantKillMoveRefresh) dict["dex_giant_kill_move_refresh"] = true;
        if (r.KillFullApRefund) dict["kill_full_ap_refund"] = true;
        if (r.ConquerorAoe) dict["conqueror_aoe"] = true;

        // Modifiers
        dict["modifiers"] = r.Modifiers;
        dict["removes_effects"] = new string[] { };

        return dict;
    }

    // =========================================================================
    // 便捷方法
    // =========================================================================

    /// <summary>命中且造成伤害</summary>
    public bool DidDamage => Hit && Damage > 0;

    /// <summary>命中且击杀（需调用方结合 target.CurrentHp 判断）</summary>
    public bool WasKillingBlow(Unit target) => Hit && Damage > 0 && target.CurrentHp <= 0;

    /// <summary>命中且造成伤害且目标已死亡</summary>
    public bool WasKillingBlowOnTarget(Unit target) => Hit && Damage > 0 && target.CurrentHp <= 0;

    /// <summary>设置 extension 值</summary>
    public void SetExt(string key, Variant value)
    {
        Extensions[key] = value;
    }

    /// <summary>获取 extension 值</summary>
    public Variant GetExt(string key)
    {
        return Extensions.ContainsKey(key) ? Extensions[key] : default;
    }

    /// <summary>是否被阻止（buff 或高地）</summary>
    public bool IsBlocked => BlockedByBuff || BlockedByElevation;

    /// <summary>检查任意 key（向后兼容 ContainsKey 模式）</summary>
    public bool ContainsKey(string key)
    {
        return key switch
        {
            "hit" => true,
            "critical" => true,
            "fumble" => true,
            "graze" => true,
            "damage" => true,
            "roll" => true,
            "attack_bonus" => true,
            "total_attack" => true,
            "target_ac" => true,
            "hit_chance_percent" => true,
            "advantage" => true,
            "disadvantage" => true,
            "is_counter" => true,
            "is_flanking" => true,
            "flank_direction" => true,
            "is_charge" => true,
            "armor_penetrated" => true,
            "armor_damage" => true,
            "modifiers" => true,
            "removes_effects" => true,
            "blocked_by_buff" => BlockedByBuff,
            "blocked_by_elevation" => BlockedByElevation,
            "reason" => Reason != null,
            "out_of_ammo" => OutOfAmmo,
            "shield_ranged_absorbed" => ShieldAbsorbed,
            "damage_reduction" => DamageReduction > 0,
            "crush_bonus" => CrushBonus,
            "crush_weak_bonus" => CrushWeakBonus > 0,
            "undiminished_damage" => UndiminishedDamage,
            "critical_negated_by_oathshield" => CriticalNegatedByOathshield,
            "redirected_damage" => RedirectedDamage > 0,
            "redirected_to_unit_id" => RedirectedDamage > 0,
            "death_saved" => DeathSaved,
            "blood_oath_leech" => BloodOathLeech > 0,
            "blade_dancer_extra_target" => BladeDancerExtraTarget != null,
            "blade_dancer_extra_hit" => BladeDancerExtraHit,
            "blade_dancer_extra_damage" => BladeDancerExtraDamage > 0,
            "blade_dancer_extra_crit" => BladeDancerExtraCrit,
            "riposte_damage" => RiposteDamage > 0,
            "martyrs_guard_share" => MartyrsGuardShare > 0,
            "martyrs_guard_defender" => MartyrsGuardDefender != null,
            "martyr_oath_redirect" => MartyrOathRedirect > 0,
            "martyr_oath_guardian" => MartyrOathGuardian != null,
            "martyr_oath_restored" => MartyrOathRestored > 0,
            "next_hit_poison_applied" => NextHitPoisonApplied,
            "next_hit_poison_duration" => NextHitPoisonApplied,
            "refund_ranged_action_ap" => RefundRangedActionAp > 0,
            "dex_giant_kill_move_refresh" => DexGiantKillMoveRefresh,
            "kill_full_ap_refund" => KillFullApRefund,
            "conqueror_aoe" => ConquerorAoe,
            _ => Extensions.ContainsKey(key),
        };
    }
}

/// <summary>
/// Dictionary 辅助方法 — 避免 GD0302 泛型约束问题。
/// </summary>
internal static class AttackResultDictHelpers
{
    public static bool GetBool(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) && dict[key].AsBool();
    public static int GetInt(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) ? dict[key].AsInt32() : 0;
    public static long GetLong(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) ? dict[key].AsInt64() : 0;
    public static float GetFloat(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) ? dict[key].AsSingle() : 0f;
    public static double GetDouble(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) ? dict[key].AsDouble() : 0.0;
    public static string? GetStr(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) ? dict[key].AsString() : null;
    public static CombatUnit? GetUnit(this Godot.Collections.Dictionary dict, string key)
        => dict.ContainsKey(key) ? dict[key].AsGodotObject() as CombatUnit : null;
}