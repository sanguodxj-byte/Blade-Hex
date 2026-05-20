// SmithyService.cs
// 铁匠铺设施规则：装备修理、武器磨砺、防具加固。
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 铁匠铺规则服务。只处理 Core 数据，不依赖 Godot UI 节点。
/// </summary>
public static class SmithyService
{
    [Obsolete("Use FacilityPricingService.GetSharpenCost(...) instead.")] public const int SharpenCost = 50;
    [Obsolete("Use FacilityPricingService.GetReinforceCost(...) instead.")] public const int ReinforceCost = 80;

    public static int CalculateRepairCost(PartyRoster? roster)
    {
        int cost = 0;
        foreach (var armor in EnumerateAllArmorPieces(roster))
            cost += FacilityPricingService.GetRepairCost(armor);

        return cost;
    }

    public static int CountDamagedArmorPieces(PartyRoster? roster)
    {
        int count = 0;
        foreach (var armor in EnumerateAllArmorPieces(roster))
        {
            if (armor.CurrentArmorPoints < GetArmorMaxPoints(armor)) count++;
        }
        return count;
    }

    public static FacilityServiceResult RepairAll(PartyRoster? roster, Func<int, bool> spendGold)
    {
        if (roster == null || roster.Count == 0)
            return FacilityServiceResult.Fail("没有可修理装备的队伍。");

        int cost = CalculateRepairCost(roster);
        if (cost <= 0 || CountDamagedArmorPieces(roster) == 0)
            return FacilityServiceResult.Fail("所有装备耐久完好，无需修理。");

        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法修理装备。");

        int repairedPieces = 0;
        int restoredPoints = 0;
        foreach (var armor in EnumerateAllArmorPieces(roster))
        {
            int before = armor.CurrentArmorPoints;
            int max = GetArmorMaxPoints(armor);
            armor.MaxArmorPoints = max;
            armor.CurrentArmorPoints = max;
            if (max > before)
            {
                repairedPieces++;
                restoredPoints += max - before;
            }
        }

        return FacilityServiceResult.Ok(
            $"已修理 {repairedPieces} 件装备，恢复 {restoredPoints} 点装甲耐久。",
            goldSpent: cost,
            affectedItems: repairedPieces,
            amountChanged: restoredPoints);
    }

    public static FacilityServiceResult SharpenLeaderWeapon(PartyRoster? roster, Func<int, bool> spendGold)
    {
        var weapon = roster?.Leader?.PrimaryMainHand;
        if (weapon == null)
            return FacilityServiceResult.Fail("队长未装备主手武器，无法磨砺。");

        int cost = FacilityPricingService.GetSharpenCost(weapon);
        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法磨砺武器。");

        weapon.BonusDamage += 1;
        return FacilityServiceResult.Ok(
            $"{weapon.ItemName} 已磨砺，伤害永久 +1。",
            goldSpent: cost,
            affectedItems: 1,
            amountChanged: 1);
    }

    public static FacilityServiceResult ReinforceLeaderArmor(PartyRoster? roster, Func<int, bool> spendGold)
    {
        var armor = roster?.Leader?.Armor;
        if (armor == null)
            return FacilityServiceResult.Fail("队长未装备身甲，无法加固。");

        int cost = FacilityPricingService.GetReinforceCost(armor);
        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法加固防具。");

        armor.DrThreshold += 1;
        armor.MaxArmorPoints = Math.Max(armor.MaxArmorPoints, armor.DrThreshold * 10);
        armor.CurrentArmorPoints = armor.MaxArmorPoints;

        return FacilityServiceResult.Ok(
            $"{armor.ItemName} 已加固，装甲阈值永久 +1。",
            goldSpent: cost,
            affectedItems: 1,
            amountChanged: 1);
    }

    private static int GetArmorMaxPoints(ArmorData armor)
    {
        int max = armor.MaxArmorPoints > 0 ? armor.MaxArmorPoints : armor.DrThreshold * 10;
        if (max <= 0 && armor.DrThreshold > 0)
        {
            armor.InitializeArmorPoints();
            max = armor.MaxArmorPoints;
        }
        return Math.Max(max, armor.DrThreshold * 10);
    }

    private static IEnumerable<ArmorData> EnumerateAllArmorPieces(PartyRoster? roster)
    {
        if (roster == null) yield break;
        foreach (var unit in roster.Members)
        {
            foreach (var armor in EnumerateArmorPieces(unit))
                yield return armor;
        }
    }

    private static IEnumerable<ArmorData> EnumerateArmorPieces(UnitData unit)
    {
        if (unit.Armor != null) yield return unit.Armor;
        if (unit.Shield != null) yield return unit.Shield;
        if (unit.Helmet != null) yield return unit.Helmet;
        if (unit.Gauntlets != null) yield return unit.Gauntlets;
        if (unit.Boots != null) yield return unit.Boots;
    }
}
