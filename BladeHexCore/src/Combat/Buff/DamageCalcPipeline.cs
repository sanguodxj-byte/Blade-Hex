using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Buff;

/// <summary>
/// 多乘区伤害计算管线（未接入 — 目前无任何调用方）。
///
/// 注意：实战与模拟的伤害都走 CombatRuleEngine.CalculateDamage，本管线尚未替代它。
/// buff 的伤害百分比乘区当前由 CombatResolver / HeadlessCombatLoop 调用
/// BuffSystem.ResolveMultiplier("damage") 折进 DamageInput.FinalMultiplier。
/// 本类保留作为未来"带 DamageBreakdown 明细 UI"的备选实现，接入前不要假设它已生效。
///
/// 支持:Base(加法) → Increased(加法合并后乘) → More(各自独立相乘) → FinalMult(最终乘)，
/// 每一步记录来源，输出 DamageBreakdown 供 UI 显示。
/// </summary>
public static class DamageCalcPipeline
{
    /// <summary>伤害计算输入上下文</summary>
    public class DamageContext
    {
        // 攻击者
        public UnitData? Attacker;
        public WeaponData? Weapon;
        public int AttackerLevel;

        // 防御者
        public UnitData? Defender;

        // 基础骰结果(已掷好)
        public int WeaponDiceResult;
        public int StatModBonus;         // 属性修正(STR/DEX mod)

        // 战斗情境
        public bool IsCritical;
        public bool IsCharge;
        public bool IsFlanking;
        public bool IsSneakAttack;
        public bool IsRanged;
        public string DamageType = "";   // "slash", "pierce", "fire"...

        // 额外固定加值(技能被动、装备附魔等)
        public List<(string source, int value)> ExtraFlatDamage = new();

        // 额外乘区(由调用方根据情境添加)
        public List<(string source, float percent)> ExtraIncreased = new();
        public List<(string source, float multiplier)> ExtraMore = new();
        public List<(string source, float multiplier)> ExtraFinal = new();
    }

    /// <summary>
    /// 执行完整的多乘区伤害计算管线。
    /// </summary>
    public static DamageBreakdown Calculate(DamageContext ctx)
    {
        var breakdown = new DamageBreakdown();
        breakdown.IsCritical = ctx.IsCritical;
        breakdown.DamageType = ctx.DamageType;
        breakdown.AttackerName = ctx.Attacker?.UnitName ?? "";
        breakdown.DefenderName = ctx.Defender?.UnitName ?? "";

        // ============================================================
        // 阶段 1: 基础伤害(加法)
        // ============================================================
        int baseDmg = 0;

        // 武器骰
        baseDmg += ctx.WeaponDiceResult;
        breakdown.BaseContributions.Add(("武器骰", ctx.WeaponDiceResult));

        // 属性修正
        if (ctx.StatModBonus != 0)
        {
            baseDmg += ctx.StatModBonus;
            breakdown.BaseContributions.Add(("属性修正", ctx.StatModBonus));
        }

        // 额外固定加值
        foreach (var (src, val) in ctx.ExtraFlatDamage)
        {
            baseDmg += val;
            breakdown.BaseContributions.Add((src, val));
        }

        // Buff 的 Base 层 damage 修正
        if (ctx.Attacker != null)
        {
            var buffFlat = BuffSystem.ResolveStatModifiers(ctx.Attacker, "damage",
                ctx.IsRanged ? "ranged_only" : "melee_only");
            if (buffFlat.FlatBonus != 0)
            {
                int flat = (int)buffFlat.FlatBonus;
                baseDmg += flat;
                breakdown.BaseContributions.Add(("Buff加值", flat));
            }
        }

        baseDmg = Math.Max(1, baseDmg);
        breakdown.BaseDamage = baseDmg;

        // ============================================================
        // 阶段 2: 增伤区(Increased) — 所有增伤%加法合并后 ×(1+sum)
        // ============================================================
        float totalIncreased = 0f;

        // 来自 Buff 的 Increased 层
        if (ctx.Attacker != null)
        {
            var buffInc = BuffSystem.ResolveStatModifiers(ctx.Attacker, "damage",
                ctx.IsRanged ? "ranged_only" : "melee_only");
            if (buffInc.IncreasedPercent != 0)
            {
                totalIncreased += buffInc.IncreasedPercent;
                breakdown.IncreasedSources.Add(("Buff增伤", buffInc.IncreasedPercent));
            }
        }

        // 额外增伤(情境)
        foreach (var (src, pct) in ctx.ExtraIncreased)
        {
            totalIncreased += pct;
            breakdown.IncreasedSources.Add((src, pct));
        }

        // 包夹增伤
        if (ctx.IsFlanking)
        {
            totalIncreased += 0.15f;
            breakdown.IncreasedSources.Add(("包夹", 0.15f));
        }

        breakdown.TotalIncreasedPercent = totalIncreased;
        int afterIncreased = (int)(baseDmg * (1f + totalIncreased));
        breakdown.AfterIncreased = afterIncreased;

        // ============================================================
        // 阶段 3: 更多伤害区(More) — 各自独立相乘
        // ============================================================
        float moreMult = 1f;

        // 暴击
        if (ctx.IsCritical)
        {
            moreMult *= 2.0f;
            breakdown.MoreMultipliers.Add(("暴击", 2.0f));
        }

        // 冲锋
        if (ctx.IsCharge)
        {
            moreMult *= 1.3f;
            breakdown.MoreMultipliers.Add(("冲锋", 1.3f));
        }

        // 偷袭
        if (ctx.IsSneakAttack)
        {
            moreMult *= 1.5f;
            breakdown.MoreMultipliers.Add(("偷袭", 1.5f));
        }

        // Buff 的 More 层
        if (ctx.Attacker != null)
        {
            var buffMore = BuffSystem.ResolveStatModifiers(ctx.Attacker, "damage",
                ctx.IsRanged ? "ranged_only" : "melee_only");
            if (buffMore.MoreMultiplier != 1f)
            {
                moreMult *= buffMore.MoreMultiplier;
                breakdown.MoreMultipliers.Add(("Buff更多", buffMore.MoreMultiplier));
            }
        }

        // 额外 More
        foreach (var (src, mult) in ctx.ExtraMore)
        {
            moreMult *= mult;
            breakdown.MoreMultipliers.Add((src, mult));
        }

        int afterMore = (int)(afterIncreased * moreMult);
        breakdown.AfterMore = afterMore;

        // ============================================================
        // 阶段 4: 最终修正(FinalMult) — 抗性/弱点/减伤
        // ============================================================
        float finalMult = 1f;

        // 防御者的 FinalMult 层(抗性/弱点)
        if (ctx.Defender != null)
        {
            var defMods = BuffSystem.ResolveStatModifiers(ctx.Defender, $"resist_{ctx.DamageType}");
            if (defMods.FinalMultiplier != 1f)
            {
                finalMult *= defMods.FinalMultiplier;
                breakdown.FinalMultipliers.Add(($"抗性({ctx.DamageType})", defMods.FinalMultiplier));
            }
        }

        // 额外 Final
        foreach (var (src, mult) in ctx.ExtraFinal)
        {
            finalMult *= mult;
            breakdown.FinalMultipliers.Add((src, mult));
        }

        int finalDamage = Math.Max(0, (int)(afterMore * finalMult));
        breakdown.FinalDamage = finalDamage;

        return breakdown;
    }
}
