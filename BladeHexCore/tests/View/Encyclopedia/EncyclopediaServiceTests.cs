// EncyclopediaServiceTests.cs
// 百科服务测试
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Encyclopedia;
using BladeHex.Strategic.Hero;

namespace BladeHex.Tests.View.Encyclopedia;

public static class EncyclopediaServiceTests
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
        yield return Run(nameof(Encyclopedia_GetAllFamilies_GroupsByName), Encyclopedia_GetAllFamilies_GroupsByName);
        yield return Run(nameof(Encyclopedia_GetAllFactions_FromConfig), Encyclopedia_GetAllFactions_FromConfig);
        yield return Run(nameof(Encyclopedia_GetAllHeroes_ReturnsList), Encyclopedia_GetAllHeroes_ReturnsList);
        yield return Run(nameof(Encyclopedia_GetAllKnownPois_FiltersCorrectly), Encyclopedia_GetAllKnownPois_FiltersCorrectly);
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

    private static (bool, string) Encyclopedia_GetAllFamilies_GroupsByName()
    {
        var registry = new HeroRegistry();

        // 添加测试英雄
        registry.Create("faction1", "英雄1", "家族A", OverworldPOI.LordPersonality.Balanced, 1);
        registry.Create("faction1", "英雄2", "家族A", OverworldPOI.LordPersonality.Balanced, 1);
        registry.Create("faction2", "英雄3", "家族B", OverworldPOI.LordPersonality.Balanced, 1);

        var families = EncyclopediaService.GetAllFamilies(registry);

        if (families.Count != 2)
            return (false, $"应有2个家族，实际{families.Count}");

        if (!families.ContainsKey("家族A"))
            return (false, "缺少家族A");

        if (families["家族A"].Count != 2)
            return (false, $"家族A应有2个成员，实际{families["家族A"].Count}");

        return (true, "");
    }

    private static (bool, string) Encyclopedia_GetAllFactions_FromConfig()
    {
        var nations = new List<NationConfig>
        {
            new NationConfig { Id = "n1", DisplayName = "国家1" },
            new NationConfig { Id = "n2", DisplayName = "国家2" }
        };

        var factions = EncyclopediaService.GetAllFactions(nations);

        if (factions.Count != 2)
            return (false, $"应有2个势力，实际{factions.Count}");

        return (true, "");
    }

    private static (bool, string) Encyclopedia_GetAllHeroes_ReturnsList()
    {
        var registry = new HeroRegistry();
        registry.Create("faction1", "英雄1", "家族A", OverworldPOI.LordPersonality.Balanced, 1);
        registry.Create("faction2", "英雄2", "家族B", OverworldPOI.LordPersonality.Balanced, 1);

        var heroes = EncyclopediaService.GetAllHeroes(registry);

        if (heroes.Count != 2)
            return (false, $"应有2个英雄，实际{heroes.Count}");

        return (true, "");
    }

    private static (bool, string) Encyclopedia_GetAllKnownPois_FiltersCorrectly()
    {
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇", PoiTypeEnum = OverworldPOI.POIType.Town },
            new OverworldPOI { PoiName = "城堡", PoiTypeEnum = OverworldPOI.POIType.Castle },
            new OverworldPOI { PoiName = "村庄", PoiTypeEnum = OverworldPOI.POIType.Village },
            new OverworldPOI { PoiName = "港口", PoiTypeEnum = OverworldPOI.POIType.Town, IsPortCity = true },
            new OverworldPOI { PoiName = "巢穴", PoiTypeEnum = OverworldPOI.POIType.Lair }
        };

        var knownPois = EncyclopediaService.GetAllKnownPois(pois);

        if (knownPois.Count != 4)
            return (false, $"应有4个可查阅据点，实际{knownPois.Count}");

        return (true, "");
    }
}
