// FiefEconomyPricingService.cs
// 封地建筑成本与收益锚定。
using System;

namespace BladeHex.Strategic.Economy;

public static class FiefEconomyPricingService
{
    public static readonly EconomyPriceAnchor DefaultAnchor = EquipmentPriceAnchorService.CreateDefaultAnchor();

    public static int GetBuildCost(FiefBuilding.BuildingType type, int configCost, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        double cycles = type switch
        {
            FiefBuilding.BuildingType.LordManor => 0,
            FiefBuilding.BuildingType.Farmland => 3.0,
            FiefBuilding.BuildingType.Market => 12.0,
            FiefBuilding.BuildingType.Barracks => 10.0,
            FiefBuilding.BuildingType.Smithy => 9.0,
            FiefBuilding.BuildingType.WoodFence => 1.5,
            FiefBuilding.BuildingType.StoneWall => 5.0,
            FiefBuilding.BuildingType.Fortification => 13.0,
            FiefBuilding.BuildingType.Gate => 3.0,
            FiefBuilding.BuildingType.Barricade => 2.0,
            FiefBuilding.BuildingType.ArrowTower => 7.0,
            FiefBuilding.BuildingType.WatchTower => 4.0,
            FiefBuilding.BuildingType.MagicTower => 20.0,
            FiefBuilding.BuildingType.TrapPit => 2.0,
            FiefBuilding.BuildingType.BlacksmithWorkshop => 22.0,
            FiefBuilding.BuildingType.TanneryWorkshop => 16.0,
            FiefBuilding.BuildingType.TextileWorkshop => 14.0,
            FiefBuilding.BuildingType.BrewWorkshop => 12.0,
            _ => 5.0,
        };
        int anchored = Round(anchor.DiscretionaryGoldPerQuest * cycles, minimum: type == FiefBuilding.BuildingType.LordManor ? 0 : 25);
        // JSON cost 作为策划下限/兼容参考，避免现有建筑突然过低。
        return Math.Max(anchored, Math.Max(0, configCost));
    }

    public static int GetDailyIncome(FiefData fief, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        double populationIncome = fief.Population * fief.Prosperity / 120.0;
        double marketIncome = fief.GetBuildingCount(FiefBuilding.BuildingType.Market) * anchor.SustainableNetGoldPerDay * 0.8;
        return Math.Max(0, (int)Math.Round(populationIncome + marketIncome));
    }

    public static int GetDailyFood(FiefData fief)
    {
        int farmland = fief.GetBuildingCount(FiefBuilding.BuildingType.Farmland);
        return farmland * 5;
    }

    private static int Round(double value, int minimum)
    {
        if (minimum == 0 && value <= 0) return 0;
        int rounded = value < 500
            ? (int)(Math.Round(value / 10.0) * 10)
            : (int)(Math.Round(value / 25.0) * 25);
        return Math.Max(minimum, rounded);
    }
}
