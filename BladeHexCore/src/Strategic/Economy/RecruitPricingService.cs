// RecruitPricingService.cs
// 招募价格与工资定价服务。
using System;
using BladeHex.Data;

namespace BladeHex.Strategic.Economy;

public static class RecruitPricingService
{
    public static readonly EconomyPriceAnchor DefaultAnchor = EquipmentPriceAnchorService.CreateDefaultAnchor();

    public static int GetRecruitCost(UnitData unit, int poiTier = 1, int prosperity = 50, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int level = Math.Max(1, unit.Level);
        double equipmentValue = EstimateEquipmentValue(unit, anchor);
        double tierMultiplier = 1.0 + Math.Clamp(poiTier, 0, 3) * 0.15;
        double prosperityMultiplier = 1.0 + Math.Clamp(prosperity, 0, 100) * 0.002;
        double cost = anchor.DiscretionaryGoldPerQuest * (1.1 + level * 0.55) * tierMultiplier * prosperityMultiplier
            + equipmentValue * 0.12;
        return Round(cost, minimum: 35);
    }

    public static int GetWeeklyWage(UnitData unit, int poiTier = 1, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int level = Math.Max(1, unit.Level);
        double wage = anchor.SustainableNetGoldPerDay * (0.18 + level * 0.13) * 7.0 * (1.0 + Math.Clamp(poiTier, 0, 3) * 0.08);
        return Round(wage, minimum: 8);
    }

    public static int GetDailyWage(UnitData unit, EconomyPriceAnchor? anchor = null)
    {
        int weekly = GetWeeklyWage(unit, anchor: anchor);
        return Math.Max(1, (int)Math.Ceiling(weekly / 7.0));
    }

    private static double EstimateEquipmentValue(UnitData unit, EconomyPriceAnchor anchor)
    {
        double total = 0;
        Add(unit.PrimaryMainHand);
        Add(unit.SecondaryMainHand);
        Add(unit.PrimaryOffHand);
        Add(unit.SecondaryOffHand);
        Add(unit.Armor);
        Add(unit.Shield);
        Add(unit.Helmet);
        Add(unit.Boots);
        Add(unit.Gauntlets);
        Add(unit.Accessory1);
        Add(unit.Accessory2);
        foreach (var weapon in unit.ExtraWeaponSlots) Add(weapon);
        return total;

        void Add(ItemData? item)
        {
            if (item != null) total += TradePricingService.GetBasePrice(item, anchor);
        }
    }

    private static int Round(double value, int minimum)
    {
        int rounded = value < 100
            ? (int)(Math.Round(value / 5.0) * 5)
            : (int)(Math.Round(value / 10.0) * 10);
        return Math.Max(minimum, rounded);
    }
}
