// RestService.cs
// 休息设施规则：短休息、长休息、HP/MP 恢复。
using System;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 休息规则服务。只处理 Core 数据，不依赖 Godot UI 节点。
/// </summary>
public static class RestService
{
    [Obsolete("Use FacilityPricingService.GetLongRestCost(...) instead.")] public const int LongRestCost = 10;

    public static int CountMembersNeedingRest(PartyRoster? roster)
    {
        if (roster == null) return 0;
        int count = 0;
        foreach (var unit in roster.Members)
        {
            if (PartyRoster.GetCurrentHp(unit) < CombatStats.GetMaxHp(unit) ||
                unit.CurrentMana < CombatStats.GetMaxMana(unit))
                count++;
        }
        return count;
    }

    public static FacilityServiceResult ShortRest(PartyRoster? roster)
    {
        if (roster == null || roster.Count == 0)
            return FacilityServiceResult.Fail("没有可休息的队伍。");

        int restored = RestoreMana(roster, 0.5f, full: false);
        return FacilityServiceResult.Ok(
            $"短休息完成，恢复 {restored} 点法力。",
            affectedMembers: roster.Count,
            amountChanged: restored);
    }

    public static FacilityServiceResult LongRest(PartyRoster? roster, Func<int, bool> spendGold)
    {
        if (roster == null || roster.Count == 0)
            return FacilityServiceResult.Fail("没有可休息的队伍。");

        if (CountMembersNeedingRest(roster) <= 0)
            return FacilityServiceResult.Fail("队伍状态良好，无需长休息。");

        int cost = FacilityPricingService.GetLongRestCost(roster);
        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法长休息。");

        int healed = RestoreHpFull(roster);
        int mana = RestoreMana(roster, 1.0f, full: true);
        return FacilityServiceResult.Ok(
            $"长休息完成，恢复 {healed} 点生命和 {mana} 点法力。",
            goldSpent: cost,
            affectedMembers: roster.Count,
            amountChanged: healed + mana);
    }

    private static int RestoreHpFull(PartyRoster roster)
    {
        int total = 0;
        foreach (var unit in roster.Members)
        {
            int max = CombatStats.GetMaxHp(unit);
            int current = PartyRoster.GetCurrentHp(unit);
            if (current < max)
            {
                PartyRoster.SetCurrentHp(unit, max);
                total += max - current;
            }
        }
        return total;
    }

    private static int RestoreMana(PartyRoster roster, float ratio, bool full)
    {
        int total = 0;
        foreach (var unit in roster.Members)
        {
            int max = CombatStats.GetMaxMana(unit);
            int target = full ? max : Math.Max(unit.CurrentMana, (int)Math.Ceiling(max * ratio));
            target = Math.Clamp(target, 0, max);
            if (target > unit.CurrentMana)
            {
                total += target - unit.CurrentMana;
                unit.CurrentMana = target;
            }
        }
        return total;
    }
}
