// HealingService.cs
// 药师所设施规则：治疗、净化、净化药水购买。
using System;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 药师所规则服务。只处理 Core 数据，不依赖 Godot UI 节点。
/// </summary>
public static class HealingService
{
    [Obsolete("Use FacilityPricingService.GetHealCost(..., 0.5f) instead.")] public const int MinorHealCost = 15;
    [Obsolete("Use FacilityPricingService.GetHealCost(..., 1.0f) instead.")] public const int MajorHealCost = 40;
    [Obsolete("Use FacilityPricingService.GetPurifyCost(...) instead.")] public const int PurifyCost = 60;
    [Obsolete("Use FacilityPricingService.GetHolyWaterCost(...) instead.")] public const int HolyWaterCost = 25;

    public static int CountInjuredMembers(PartyRoster? roster)
    {
        if (roster == null) return 0;
        int count = 0;
        foreach (var unit in roster.Members)
        {
            if (PartyRoster.GetCurrentHp(unit) < CombatStats.GetMaxHp(unit)) count++;
        }
        return count;
    }

    public static int CountNegativeEffects(PartyRoster? roster)
    {
        if (roster == null) return 0;
        int count = 0;
        foreach (var unit in roster.Members)
        {
            count += unit.Runtime.ActiveStatusEffects.FindAll(e => e.IsNegative).Count;
            count += unit.Runtime.ActiveBuffs.FindAll(e => e.IsNegative).Count;
        }
        return count;
    }

    public static FacilityServiceResult HealToRatio(PartyRoster? roster, float ratio, int cost, Func<int, bool> spendGold)
    {
        if (roster == null || roster.Count == 0)
            return FacilityServiceResult.Fail("没有可治疗的队伍。");

        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法治疗。");

        int healedMembers = 0;
        int totalHealed = 0;
        foreach (var unit in roster.Members)
        {
            int maxHp = CombatStats.GetMaxHp(unit);
            int current = PartyRoster.GetCurrentHp(unit);
            int target = ratio >= 1.0f ? maxHp : Math.Max(current, (int)Math.Ceiling(maxHp * ratio));
            target = Math.Clamp(target, 0, maxHp);
            if (target > current)
            {
                PartyRoster.SetCurrentHp(unit, target);
                totalHealed += target - current;
                healedMembers++;
            }
        }

        string label = ratio >= 1.0f ? "深度治疗" : "轻度治疗";
        return FacilityServiceResult.Ok(
            $"{label}完成，治疗 {healedMembers} 名成员，恢复 {totalHealed} 点生命。",
            goldSpent: cost,
            affectedMembers: healedMembers,
            amountChanged: totalHealed);
    }

    public static FacilityServiceResult PurifyAll(PartyRoster? roster, Func<int, bool> spendGold)
    {
        if (roster == null || roster.Count == 0)
            return FacilityServiceResult.Fail("没有可净化的队伍。");

        if (CountNegativeEffects(roster) <= 0)
            return FacilityServiceResult.Fail("队伍没有负面状态，无需净化。");

        int cost = FacilityPricingService.GetPurifyCost(roster);
        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法净化。");

        int removed = 0;
        foreach (var unit in roster.Members)
        {
            removed += unit.Runtime.ActiveStatusEffects.RemoveAll(e => e.IsNegative);
            removed += unit.Runtime.ActiveBuffs.RemoveAll(e => e.IsNegative);
        }

        return FacilityServiceResult.Ok(
            $"净化完成，移除了 {removed} 个负面状态。",
            goldSpent: cost,
            affectedMembers: roster.Count,
            amountChanged: removed);
    }

    public static ConsumableData CreateFallbackHolyWater() => new()
    {
        ItemId = "holy_water",
        ItemName = "净化药水",
        Description = "对亡灵造成2d6奥术伤害。",
        Price = 50,
        consumableType = ConsumableData.ConsumableType.HolyWater,
        DamageDiceCount = 2,
        DamageDiceSides = 6,
        DamageType = "magic",
        ThrowRange = 4,
    };
}
