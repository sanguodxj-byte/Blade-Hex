// EconomyEventTests.cs
// 经济事件引擎测试
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class EconomyEventTests
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
        yield return Run(nameof(War_AddsWeaponPriceMultiplier), War_AddsWeaponPriceMultiplier);
        yield return Run(nameof(War_AddsFoodPriceMultiplier), War_AddsFoodPriceMultiplier);
        yield return Run(nameof(War_AddsHorsePriceMultiplier), War_AddsHorsePriceMultiplier);
        yield return Run(nameof(Siege_AddsAllCommodityMultiplier_x14), Siege_AddsAllCommodityMultiplier_x14);
        yield return Run(nameof(HeroCaptured_AppliesInflation_For7Days), HeroCaptured_AppliesInflation_For7Days);
        yield return Run(nameof(EventDecay_AfterCooldown), EventDecay_AfterCooldown);
        yield return Run(nameof(MultipleEvents_MultiplyTogether), MultipleEvents_MultiplyTogether);
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

    private static (bool, string) War_AddsWeaponPriceMultiplier()
    {
        var engine = new EconomyEventEngine();
        var poi = new OverworldPOI { PoiName = "测试城镇", OwningFaction = "nation_a" };
        var worldEngine = new WorldEventEngine();

        // 模拟战争状态 - 直接添加到ActiveWars
        worldEngine.ActiveWars.Add(new WarState { NationA = "nation_a", NationB = "nation_b", DaysSinceStart = 0 });

        var ctx = new WorldTickContext
        {
            CurrentDay = 1,
            Pois = new List<OverworldPOI> { poi }
        };

        engine.Tick(ctx, worldEngine);

        float mult = engine.GetPriceMultiplierFor(poi, "weapon");
        if (Math.Abs(mult - 1.3f) > 0.01f)
            return (false, $"武器价格乘数应为1.3，实际{mult}");

        return (true, "");
    }

    private static (bool, string) War_AddsFoodPriceMultiplier()
    {
        var engine = new EconomyEventEngine();
        var poi = new OverworldPOI { PoiName = "测试城镇", OwningFaction = "nation_a" };
        var worldEngine = new WorldEventEngine();
        worldEngine.ActiveWars.Add(new WarState { NationA = "nation_a", NationB = "nation_b", DaysSinceStart = 0 });

        var ctx = new WorldTickContext
        {
            CurrentDay = 1,
            Pois = new List<OverworldPOI> { poi }
        };

        engine.Tick(ctx, worldEngine);

        float mult = engine.GetPriceMultiplierFor(poi, "food");
        if (Math.Abs(mult - 1.2f) > 0.01f)
            return (false, $"食物价格乘数应为1.2，实际{mult}");

        return (true, "");
    }

    private static (bool, string) War_AddsHorsePriceMultiplier()
    {
        var engine = new EconomyEventEngine();
        var poi = new OverworldPOI { PoiName = "测试城镇", OwningFaction = "nation_a" };
        var worldEngine = new WorldEventEngine();
        worldEngine.ActiveWars.Add(new WarState { NationA = "nation_a", NationB = "nation_b", DaysSinceStart = 0 });

        var ctx = new WorldTickContext
        {
            CurrentDay = 1,
            Pois = new List<OverworldPOI> { poi }
        };

        engine.Tick(ctx, worldEngine);

        float mult = engine.GetPriceMultiplierFor(poi, "horse");
        if (Math.Abs(mult - 1.5f) > 0.01f)
            return (false, $"马匹价格乘数应为1.5，实际{mult}");

        return (true, "");
    }

    private static (bool, string) Siege_AddsAllCommodityMultiplier_x14()
    {
        var engine = new EconomyEventEngine();
        var poi = new OverworldPOI
        {
            PoiName = "被围城镇",
            OwningFaction = "nation_a",
            IsUnderSiege = true,
            SiegeDays = 5
        };

        var ctx = new WorldTickContext
        {
            CurrentDay = 1,
            Pois = new List<OverworldPOI> { poi }
        };

        engine.Tick(ctx, new WorldEventEngine());

        float mult = engine.GetPriceMultiplierFor(poi, "all");
        if (Math.Abs(mult - 1.4f) > 0.01f)
            return (false, $"围城价格乘数应为1.4，实际{mult}");

        return (true, "");
    }

    private static (bool, string) HeroCaptured_AppliesInflation_For7Days()
    {
        var engine = new EconomyEventEngine();

        engine.TriggerCapturedInflation("nation_a", 1);

        var poi = new OverworldPOI { PoiName = "测试城镇", OwningFaction = "nation_a" };
        float mult = engine.GetPriceMultiplierFor(poi, "all");

        if (Math.Abs(mult - 1.05f) > 0.01f)
            return (false, $"领主被俘通胀应为1.05，实际{mult}");

        // 验证7天后过期
        engine.ActiveEvents.Clear();
        engine.TriggerCapturedInflation("nation_a", 1);

        var ctx = new WorldTickContext { CurrentDay = 8, Pois = new List<OverworldPOI>() };
        engine.Tick(ctx, new WorldEventEngine());

        mult = engine.GetPriceMultiplierFor(poi, "all");
        if (Math.Abs(mult - 1.0f) > 0.01f)
            return (false, $"7天后乘数应为1.0，实际{mult}");

        return (true, "");
    }

    private static (bool, string) EventDecay_AfterCooldown()
    {
        var engine = new EconomyEventEngine();
        var poi = new OverworldPOI { PoiName = "测试城镇", OwningFaction = "nation_a" };

        // 添加一个短期事件
        engine.ActiveEvents.Add(new EconomyEvent(
            EconomyEventType.War,
            poi.PoiName,
            "nation_a",
            "weapon",
            1.3f,
            5 // 第5天过期
        ));

        var ctx = new WorldTickContext { CurrentDay = 6, Pois = new List<OverworldPOI>() };
        engine.Tick(ctx, new WorldEventEngine());

        float mult = engine.GetPriceMultiplierFor(poi, "weapon");
        if (Math.Abs(mult - 1.0f) > 0.01f)
            return (false, $"过期后乘数应为1.0，实际{mult}");

        return (true, "");
    }

    private static (bool, string) MultipleEvents_MultiplyTogether()
    {
        var engine = new EconomyEventEngine();
        var poi = new OverworldPOI { PoiName = "测试城镇", OwningFaction = "nation_a" };

        // 添加多个事件
        engine.ActiveEvents.Add(new EconomyEvent(EconomyEventType.War, "", "nation_a", "weapon", 1.3f, 100));
        engine.ActiveEvents.Add(new EconomyEvent(EconomyEventType.Siege, poi.PoiName, "", "all", 1.4f, 100));

        float mult = engine.GetPriceMultiplierFor(poi, "weapon");
        float expected = 1.3f * 1.4f;

        if (Math.Abs(mult - expected) > 0.01f)
            return (false, $"复合乘数应为{expected:F2}，实际{mult:F2}");

        return (true, "");
    }
}
