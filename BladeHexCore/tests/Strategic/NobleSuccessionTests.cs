// NobleSuccessionTests.cs
// 贵族补员测试
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class NobleSuccessionTests
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
        yield return Run(nameof(Succession_NationLordsBelow50Percent_GeneratesSuccessor), Succession_NationLordsBelow50Percent_GeneratesSuccessor);
        yield return Run(nameof(Succession_RespectsMaxCap), Succession_RespectsMaxCap);
        yield return Run(nameof(Succession_InheritsFamilyName), Succession_InheritsFamilyName);
        yield return Run(nameof(Succession_PushesNewsEvent), Succession_PushesNewsEvent);
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

    private static (bool, string) Succession_NationLordsBelow50Percent_GeneratesSuccessor()
    {
        var heroRegistry = new HeroRegistry();
        var familyRegistry = new FamilyRegistry();
        var worldEngine = new WorldEventEngine();

        // 创建国家配置
        var nation = new NationConfig { Id = "test_nation", DisplayName = "测试国", IsMajorNation = true };

        // 创建家族
        var hero1 = heroRegistry.Create("test_nation", "英雄1", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);
        familyRegistry.Create("铁锤", "test_nation", hero1.HeroId, new List<string> { hero1.HeroId }, 1);

        // 只有 1 个领主（低于 15 的 50% = 7）
        var nations = new List<NationConfig> { nation };
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇", OwningFaction = "test_nation", PoiTypeEnum = OverworldPOI.POIType.Town, Position = new Godot.Vector2(100, 100) }
        };
        var config = new SpecialCharacterGenerator.GenerationConfig { MajorNationLordsMin = 15, MajorNationLordsMax = 25 };

        int beforeCount = heroRegistry.GetByFaction("test_nation").Count;

        NobleSuccessionService.Audit(heroRegistry, familyRegistry, nations, pois, config, 30, worldEngine);

        int afterCount = heroRegistry.GetByFaction("test_nation").Count;

        if (afterCount <= beforeCount) return (false, $"补员后数量应增加，前{beforeCount}后{afterCount}");

        return (true, "");
    }

    private static (bool, string) Succession_RespectsMaxCap()
    {
        var heroRegistry = new HeroRegistry();
        var familyRegistry = new FamilyRegistry();
        var worldEngine = new WorldEventEngine();

        var nation = new NationConfig { Id = "test_nation", DisplayName = "测试国", IsMajorNation = true };

        // 创建家族
        var hero1 = heroRegistry.Create("test_nation", "英雄1", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);
        familyRegistry.Create("铁锤", "test_nation", hero1.HeroId, new List<string> { hero1.HeroId }, 1);

        var nations = new List<NationConfig> { nation };
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇", OwningFaction = "test_nation", PoiTypeEnum = OverworldPOI.POIType.Town, Position = new Godot.Vector2(100, 100) }
        };
        var config = new SpecialCharacterGenerator.GenerationConfig { MajorNationLordsMin = 15, MajorNationLordsMax = 25 };

        NobleSuccessionService.Audit(heroRegistry, familyRegistry, nations, pois, config, 30, worldEngine);

        int count = heroRegistry.GetByFaction("test_nation").Count;
        int maxCap = (int)(config.MajorNationLordsMax * 1.2); // 30

        if (count > maxCap) return (false, $"补员后不应超过上限{maxCap}，实际{count}");

        return (true, "");
    }

    private static (bool, string) Succession_InheritsFamilyName()
    {
        var heroRegistry = new HeroRegistry();
        var familyRegistry = new FamilyRegistry();
        var worldEngine = new WorldEventEngine();

        var nation = new NationConfig { Id = "test_nation", DisplayName = "测试国", IsMajorNation = true };

        var hero1 = heroRegistry.Create("test_nation", "英雄1", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);
        familyRegistry.Create("铁锤", "test_nation", hero1.HeroId, new List<string> { hero1.HeroId }, 1);

        var nations = new List<NationConfig> { nation };
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇", OwningFaction = "test_nation", PoiTypeEnum = OverworldPOI.POIType.Town, Position = new Godot.Vector2(100, 100) }
        };
        var config = new SpecialCharacterGenerator.GenerationConfig { MajorNationLordsMin = 15, MajorNationLordsMax = 25 };

        NobleSuccessionService.Audit(heroRegistry, familyRegistry, nations, pois, config, 30, worldEngine);

        // 检查新领主是否继承了家族姓氏
        var newLords = heroRegistry.GetByFaction("test_nation").Where(h => h.HeroId != hero1.HeroId).ToList();
        if (newLords.Count == 0) return (false, "未生成新领主");

        bool hasFamilyName = newLords.Any(h => h.FamilyName == "铁锤");
        if (!hasFamilyName) return (false, "新领主未继承家族姓氏");

        return (true, "");
    }

    private static (bool, string) Succession_PushesNewsEvent()
    {
        var heroRegistry = new HeroRegistry();
        var familyRegistry = new FamilyRegistry();
        var worldEngine = new WorldEventEngine();

        var nation = new NationConfig { Id = "test_nation", DisplayName = "测试国", IsMajorNation = true };

        var hero1 = heroRegistry.Create("test_nation", "英雄1", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);
        familyRegistry.Create("铁锤", "test_nation", hero1.HeroId, new List<string> { hero1.HeroId }, 1);

        var nations = new List<NationConfig> { nation };
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇", OwningFaction = "test_nation", PoiTypeEnum = OverworldPOI.POIType.Town, Position = new Godot.Vector2(100, 100) }
        };
        var config = new SpecialCharacterGenerator.GenerationConfig { MajorNationLordsMin = 15, MajorNationLordsMax = 25 };

        int newsBefore = worldEngine.NewsQueue.Count;

        NobleSuccessionService.Audit(heroRegistry, familyRegistry, nations, pois, config, 30, worldEngine);

        int newsAfter = worldEngine.NewsQueue.Count;

        if (newsAfter <= newsBefore) return (false, "补员后应产生新闻");

        return (true, "");
    }
}
