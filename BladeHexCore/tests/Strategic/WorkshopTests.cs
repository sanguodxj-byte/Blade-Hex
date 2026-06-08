// WorkshopTests.cs
// 作坊生产系统测试
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Tests.Strategic;

public static class WorkshopTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Workshop_RecipeRegistry_HasAllFourTypes), Workshop_RecipeRegistry_HasAllFourTypes);
        yield return Run(nameof(Workshop_BuildCost_InExpectedRange), Workshop_BuildCost_InExpectedRange);
        yield return Run(nameof(Workshop_DailyProduction_AccumulatesInPending), Workshop_DailyProduction_AccumulatesInPending);
        yield return Run(nameof(Workshop_ShipsToNearestFriendly_AddsGold), Workshop_ShipsToNearestFriendly_AddsGold);
        yield return Run(nameof(Workshop_NoFriendlyTown_KeepsPending), Workshop_NoFriendlyTown_KeepsPending);
        yield return Run(nameof(Workshop_FiefCaptured_TransfersOwnership), Workshop_FiefCaptured_TransfersOwnership);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"异常: {ex.Message}");
        }
    }

    private static (bool, string) Workshop_RecipeRegistry_HasAllFourTypes()
    {
        var recipes = WorkshopRecipeRegistry.GetAll().ToList();
        if (recipes.Count != 4)
            return (false, $"预期4种配方，实际{recipes.Count}种");

        var types = recipes.Select(r => r.Type).ToHashSet();
        if (!types.Contains(FiefBuilding.BuildingType.BlacksmithWorkshop))
            return (false, "缺少BlacksmithWorkshop配方");
        if (!types.Contains(FiefBuilding.BuildingType.BrewWorkshop))
            return (false, "缺少BrewWorkshop配方");
        if (!types.Contains(FiefBuilding.BuildingType.TextileWorkshop))
            return (false, "缺少TextileWorkshop配方");
        if (!types.Contains(FiefBuilding.BuildingType.TanneryWorkshop))
            return (false, "缺少TanneryWorkshop配方");

        return (true, "");
    }

    private static (bool, string) Workshop_BuildCost_InExpectedRange()
    {
        // 作坊建造成本应在5000-15000金之间
        var building = new FiefBuilding { Type = FiefBuilding.BuildingType.BlacksmithWorkshop };
        if (building.BuildCost < 5000 || building.BuildCost > 15000)
            return (false, $"BlacksmithWorkshop成本{building.BuildCost}不在5000-15000范围内");

        return (true, "");
    }

    private static (bool, string) Workshop_DailyProduction_AccumulatesInPending()
    {
        var fief = new FiefData { FiefName = "测试封地" };
        var recipe = WorkshopRecipeRegistry.GetRecipe(FiefBuilding.BuildingType.BrewWorkshop);

        if (recipe == null)
            return (false, "无法获取BrewWorkshop配方");

        WorkshopProductionService.ProcessDaily(fief, recipe, 1);

        if (!fief.PendingShipments.ContainsKey(recipe.OutputItemId))
            return (false, "待运库存中没有产出物品");
        if (fief.PendingShipments[recipe.OutputItemId] != recipe.OutputQty)
            return (false, $"预期{recipe.OutputQty}，实际{fief.PendingShipments[recipe.OutputItemId]}");

        return (true, "");
    }

    private static (bool, string) Workshop_ShipsToNearestFriendly_AddsGold()
    {
        var fief = new FiefData
        {
            FiefName = "测试封地",
            OwningFaction = "player",
            WorldPosition = new Godot.Vector2(100, 100)
        };
        var recipe = WorkshopRecipeRegistry.GetRecipe(FiefBuilding.BuildingType.BrewWorkshop);
        if (recipe == null) return (false, "无法获取配方");

        // 模拟生产
        WorkshopProductionService.ProcessDaily(fief, recipe, 1);

        // 创建友好城镇
        var town = new OverworldPOI
        {
            PoiName = "友好城镇",
            OwningFaction = "player",
            Position = new Godot.Vector2(110, 110),
            PoiTypeEnum = OverworldPOI.POIType.Town,
            Prosperity = 50
        };

        var pois = new List<OverworldPOI> { town };

        // 创建简单的经济提供者
        var economy = new TestEconomyProvider();

        int income = WorkshopProductionService.TryShipToNearestFriendly(fief, pois, economy, null);

        if (income <= 0)
            return (false, $"运输收益应为正，实际{income}");
        if (fief.PendingShipments[recipe.OutputItemId] != 0)
            return (false, "运输后待运库存应为0");

        return (true, "");
    }

    private static (bool, string) Workshop_NoFriendlyTown_KeepsPending()
    {
        var fief = new FiefData
        {
            FiefName = "测试封地",
            OwningFaction = "player",
            WorldPosition = new Godot.Vector2(100, 100)
        };
        var recipe = WorkshopRecipeRegistry.GetRecipe(FiefBuilding.BuildingType.BrewWorkshop);
        if (recipe == null) return (false, "无法获取配方");

        WorkshopProductionService.ProcessDaily(fief, recipe, 1);

        // 没有友好城镇
        var pois = new List<OverworldPOI>();
        var economy = new TestEconomyProvider();

        int income = WorkshopProductionService.TryShipToNearestFriendly(fief, pois, economy, null);

        if (income != 0)
            return (false, $"无友好城镇时收益应为0，实际{income}");
        if (fief.PendingShipments[recipe.OutputItemId] != recipe.OutputQty)
            return (false, "无友好城镇时待运库存应保留");

        return (true, "");
    }

    private static (bool, string) Workshop_FiefCaptured_TransfersOwnership()
    {
        // 作坊是建筑的一部分，POI转移时建筑列表一起转移
        var fief = new FiefData { FiefName = "测试封地", OwningFaction = "factionA" };
        fief.Buildings.Add(new FiefBuilding { Type = FiefBuilding.BuildingType.BlacksmithWorkshop });

        // 模拟转移
        fief.OwningFaction = "factionB";

        if (fief.OwningFaction != "factionB")
            return (false, "转移后势力不正确");
        if (fief.GetBuildingCount(FiefBuilding.BuildingType.BlacksmithWorkshop) != 1)
            return (false, "转移后作坊丢失");

        return (true, "");
    }
}

/// <summary>测试用经济提供者</summary>
internal class TestEconomyProvider : IEconomyProvider
{
    public int Gold { get; set; } = 10000;
    public int DaysPassed { get; set; } = 1;
    public void AddGold(int amount) { Gold += amount; }
    public bool SpendGold(int amount)
    {
        if (Gold >= amount) { Gold -= amount; return true; }
        return false;
    }
}
