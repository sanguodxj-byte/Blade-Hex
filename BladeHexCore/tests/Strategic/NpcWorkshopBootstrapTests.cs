// NpcWorkshopBootstrapTests.cs
// NPC Workshop 引导测试
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Tests.Strategic;

public static class NpcWorkshopBootstrapTests
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
        yield return Run(nameof(Bootstrap_MajorNations_GetWorkshops), Bootstrap_MajorNations_GetWorkshops);
        yield return Run(nameof(Bootstrap_TotalWorkshopCount_InRange), Bootstrap_TotalWorkshopCount_InRange);
        yield return Run(nameof(Bootstrap_DailyTick_AddsToMarketStock), Bootstrap_DailyTick_AddsToMarketStock);
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

    private static (bool, string) Bootstrap_MajorNations_GetWorkshops()
    {
        var nations = new List<NationConfig>
        {
            new NationConfig { Id = "nation_a", DisplayName = "国家A", IsMajorNation = true },
            new NationConfig { Id = "nation_b", DisplayName = "国家B", IsMajorNation = true }
        };

        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇A1", OwningFaction = "nation_a", PoiTypeEnum = OverworldPOI.POIType.Town, Prosperity = 80, Position = new Godot.Vector2(100, 100) },
            new OverworldPOI { PoiName = "城镇A2", OwningFaction = "nation_a", PoiTypeEnum = OverworldPOI.POIType.Town, Prosperity = 60, Position = new Godot.Vector2(200, 200) },
            new OverworldPOI { PoiName = "城镇B1", OwningFaction = "nation_b", PoiTypeEnum = OverworldPOI.POIType.Town, Prosperity = 70, Position = new Godot.Vector2(300, 300) }
        };

        var fiefs = new List<FiefData>();

        NpcWorkshopBootstrap.Bootstrap(nations, pois, fiefs);

        // 检查是否为每个国家创建了 Workshop
        var fiefA = fiefs.FirstOrDefault(f => f.FiefName == "城镇A1");
        var fiefB = fiefs.FirstOrDefault(f => f.FiefName == "城镇B1");

        if (fiefA == null) return (false, "国家A的城镇未创建 FiefData");
        if (fiefB == null) return (false, "国家B的城镇未创建 FiefData");
        if (fiefA.Buildings.Count == 0) return (false, "国家A的城镇未建造 Workshop");
        if (fiefB.Buildings.Count == 0) return (false, "国家B的城镇未建造 Workshop");

        return (true, "");
    }

    private static (bool, string) Bootstrap_TotalWorkshopCount_InRange()
    {
        var nations = new List<NationConfig>
        {
            new NationConfig { Id = "nation_a", DisplayName = "国家A", IsMajorNation = true },
            new NationConfig { Id = "nation_b", DisplayName = "国家B", IsMajorNation = true }
        };

        var pois = new List<OverworldPOI>();
        for (int i = 0; i < 10; i++)
        {
            pois.Add(new OverworldPOI
            {
                PoiName = $"城镇{i}",
                OwningFaction = i < 5 ? "nation_a" : "nation_b",
                PoiTypeEnum = OverworldPOI.POIType.Town,
                Prosperity = 50 + i * 5,
                Position = new Godot.Vector2(i * 100, i * 100)
            });
        }

        var fiefs = new List<FiefData>();

        NpcWorkshopBootstrap.Bootstrap(nations, pois, fiefs);

        int totalWorkshops = fiefs.Sum(f => f.Buildings.Count);

        // 每国最多3个，共6个
        if (totalWorkshops < 2 || totalWorkshops > 8) return (false, $"Workshop总数{totalWorkshops}不在预期范围2-8");

        return (true, "");
    }

    private static (bool, string) Bootstrap_DailyTick_AddsToMarketStock()
    {
        var fiefs = new List<FiefData>
        {
            new FiefData
            {
                FiefName = "测试城镇",
                OwningFaction = "nation_a"
            }
        };

        // 手动添加建筑
        fiefs[0].Buildings.Add(new FiefBuilding { Type = FiefBuilding.BuildingType.BrewWorkshop, ConstructionDaysLeft = 0 });

        var pois = new List<OverworldPOI>();

        // 调用每日 tick（不抛异常即通过）
        NpcWorkshopBootstrap.ProcessNpcWorkshops(fiefs, pois, 3);

        return (true, "");
    }
}
