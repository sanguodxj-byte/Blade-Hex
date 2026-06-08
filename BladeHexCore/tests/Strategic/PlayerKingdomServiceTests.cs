// PlayerKingdomServiceTests.cs
// 玩家王国服务测试
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Kingdom;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class PlayerKingdomServiceTests
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
        yield return Run(nameof(CanFoundKingdom_RequiresOccupiedCastle), CanFoundKingdom_RequiresOccupiedCastle);
        yield return Run(nameof(CanFoundKingdom_RequiresInfluence100), CanFoundKingdom_RequiresInfluence100);
        yield return Run(nameof(CanFoundKingdom_RequiresLevel20), CanFoundKingdom_RequiresLevel20);
        yield return Run(nameof(Found_CreatesNationConfig), Found_CreatesNationConfig);
        yield return Run(nameof(Found_AddsPlayerToFamily), Found_AddsPlayerToFamily);
        yield return Run(nameof(Found_CompanionsAutoJoinFamily), Found_CompanionsAutoJoinFamily);
        yield return Run(nameof(GrantFief_AssignsCompanionAsLord), GrantFief_AssignsCompanionAsLord);
        yield return Run(nameof(GrantFief_FailsForNonCompanion), GrantFief_FailsForNonCompanion);
        yield return Run(nameof(RevokeFief_ReturnsLordToPlayer), RevokeFief_ReturnsLordToPlayer);
        yield return Run(nameof(Disband_ClearsAllKingdomData), Disband_ClearsAllKingdomData);

        // 补全测试覆盖
        yield return Run(nameof(GrantFief_FailsWhenPoiNotControlled), GrantFief_FailsWhenPoiNotControlled);
        yield return Run(nameof(GrantFief_FailsWhenGrantingToSelf), GrantFief_FailsWhenGrantingToSelf);
        yield return Run(nameof(RevokeFief_FailsWhenPoiNotControlled), RevokeFief_FailsWhenPoiNotControlled);
        yield return Run(nameof(ChangeLaw_UpdatesAndEmitsNews), ChangeLaw_UpdatesAndEmitsNews);
        yield return Run(nameof(PlayerKingdom_SerializeRoundtrip), PlayerKingdom_SerializeRoundtrip);
        yield return Run(nameof(PlayerKingdom_ControlsPoi_And_Counts), PlayerKingdom_ControlsPoi_And_Counts);
        yield return Run(nameof(Found_SetsDiplomaticRelations), Found_SetsDiplomaticRelations);
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

    private static (bool, string) CanFoundKingdom_RequiresOccupiedCastle()
    {
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城镇", PoiTypeEnum = OverworldPOI.POIType.Town }
        };
        var influence = new InfluenceTracker();
        influence.Add("player", 200, "test");
        var pending = new List<string> { "城镇" };

        var (ok, reason) = PlayerKingdomService.CanFoundKingdom(pois, influence, 25, pending);
        if (ok) return (false, "没有城堡不应满足条件");
        if (!reason.Contains("城堡")) return (false, "错误信息应提及城堡");

        return (true, "");
    }

    private static (bool, string) CanFoundKingdom_RequiresInfluence100()
    {
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城堡", PoiTypeEnum = OverworldPOI.POIType.Castle }
        };
        var influence = new InfluenceTracker();
        influence.Add("player", 50, "test");
        var pending = new List<string> { "城堡" };

        var (ok, reason) = PlayerKingdomService.CanFoundKingdom(pois, influence, 25, pending);
        if (ok) return (false, "影响力不足不应满足条件");
        if (!reason.Contains("影响力")) return (false, "错误信息应提及影响力");

        return (true, "");
    }

    private static (bool, string) CanFoundKingdom_RequiresLevel20()
    {
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "城堡", PoiTypeEnum = OverworldPOI.POIType.Castle }
        };
        var influence = new InfluenceTracker();
        influence.Add("player", 200, "test");
        var pending = new List<string> { "城堡" };

        var (ok, reason) = PlayerKingdomService.CanFoundKingdom(pois, influence, 15, pending);
        if (ok) return (false, "等级不足不应满足条件");
        if (!reason.Contains("等级")) return (false, "错误信息应提及等级");

        return (true, "");
    }

    private static (bool, string) Found_CreatesNationConfig()
    {
        var heroRegistry = new HeroRegistry();
        var familyRegistry = new FamilyRegistry();
        var nations = new List<NationConfig>();
        var worldEngine = new WorldEventEngine();
        var influence = new InfluenceTracker();

        var capital = new OverworldPOI { PoiName = "都城", Position = Vector2.Zero };
        var pending = new List<string> { "都城" };

        var kingdom = PlayerKingdomService.Found(
            "测试王国", "测试家族", capital, Colors.Blue, 1,
            heroRegistry, familyRegistry, nations, worldEngine, influence, pending);

        if (kingdom == null) return (false, "王国未创建");
        if (!nations.Any(n => n.Id == "player")) return (false, "NationConfig 未添加");
        if (kingdom.DisplayName != "测试王国") return (false, "国名不正确");

        return (true, "");
    }

    private static (bool, string) Found_AddsPlayerToFamily()
    {
        var heroRegistry = new HeroRegistry();
        heroRegistry.Create("player", "玩家", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);
        var familyRegistry = new FamilyRegistry();
        var nations = new List<NationConfig>();
        var worldEngine = new WorldEventEngine();
        var influence = new InfluenceTracker();

        var capital = new OverworldPOI { PoiName = "都城", Position = Vector2.Zero };
        var pending = new List<string> { "都城" };

        var kingdom = PlayerKingdomService.Found(
            "测试王国", "测试家族", capital, Colors.Blue, 1,
            heroRegistry, familyRegistry, nations, worldEngine, influence, pending);

        var family = familyRegistry.Get("测试家族");
        if (family == null) return (false, "家族未创建");
        if (!family.MemberHeroIds.Contains("player")) return (false, "玩家未加入家族");

        return (true, "");
    }

    private static (bool, string) Found_CompanionsAutoJoinFamily()
    {
        var heroRegistry = new HeroRegistry();
        heroRegistry.Create("player", "玩家", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);
        heroRegistry.Create("player", "同伴A", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);
        heroRegistry.Create("player", "同伴B", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);
        var familyRegistry = new FamilyRegistry();
        var nations = new List<NationConfig>();
        var worldEngine = new WorldEventEngine();
        var influence = new InfluenceTracker();

        var capital = new OverworldPOI { PoiName = "都城", Position = Vector2.Zero };
        var pending = new List<string> { "都城" };

        var kingdom = PlayerKingdomService.Found(
            "测试王国", "测试家族", capital, Colors.Blue, 1,
            heroRegistry, familyRegistry, nations, worldEngine, influence, pending);

        var family = familyRegistry.Get("测试家族");
        if (family == null) return (false, "家族未创建");
        if (family.MemberHeroIds.Count < 3) return (false, $"家族成员不足，预期≥3，实际{family.MemberHeroIds.Count}");

        return (true, "");
    }

    private static (bool, string) GrantFief_AssignsCompanionAsLord()
    {
        var heroRegistry = new HeroRegistry();
        var hero = heroRegistry.Create("player", "同伴A", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);

        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A" },
            LordHeroIds = new List<string> { "player" }
        };

        var result = PlayerKingdomService.GrantFief(kingdom, "城堡A", hero.HeroId, heroRegistry);
        if (!result) return (false, "分封失败");
        if (!kingdom.LordHeroIds.Contains(hero.HeroId)) return (false, "同伴未加入领主列表");

        return (true, "");
    }

    private static (bool, string) GrantFief_FailsForNonCompanion()
    {
        var heroRegistry = new HeroRegistry();
        heroRegistry.Create("enemy", "敌人", "敌家族", OverworldPOI.LordPersonality.Balanced, 1);

        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A" },
            LordHeroIds = new List<string> { "player" }
        };

        var result = PlayerKingdomService.GrantFief(kingdom, "城堡A", "hero_1", heroRegistry);
        if (result) return (false, "非玩家阵营不应分封成功");

        return (true, "");
    }

    private static (bool, string) RevokeFief_ReturnsLordToPlayer()
    {
        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A" },
            LordHeroIds = new List<string> { "player", "hero_1" }
        };

        var result = PlayerKingdomService.RevokeFief(kingdom, "城堡A", "hero_1");
        if (!result) return (false, "收回失败");
        if (kingdom.LordHeroIds.Contains("hero_1")) return (false, "同伴未从领主列表移除");

        return (true, "");
    }

    private static (bool, string) Disband_ClearsAllKingdomData()
    {
        var familyRegistry = new FamilyRegistry();
        familyRegistry.Create("测试家族", "player", "player", new List<string> { "player" }, 1);
        var nations = new List<NationConfig>
        {
            new NationConfig { Id = "player", DisplayName = "测试王国" }
        };
        var worldEngine = new WorldEventEngine();

        var kingdom = new PlayerKingdom
        {
            KingdomId = "player",
            DisplayName = "测试王国",
            FamilyName = "测试家族"
        };

        PlayerKingdomService.Disband(kingdom, nations, familyRegistry, worldEngine);

        if (nations.Any(n => n.Id == "player")) return (false, "NationConfig 未移除");

        return (true, "");
    }

    // ============================================================================
    // 补全测试覆盖
    // ============================================================================

    private static (bool, string) GrantFief_FailsWhenPoiNotControlled()
    {
        var heroRegistry = new HeroRegistry();
        var hero = heroRegistry.Create("player", "同伴A", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);

        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A" },
            LordHeroIds = new List<string> { "player" }
        };

        // 尝试分封一个不在 ControlledPoiNames 中的 POI
        var result = PlayerKingdomService.GrantFief(kingdom, "不存在的城堡", hero.HeroId, heroRegistry);
        if (result) return (false, "分封非控制 POI 应失败");

        return (true, "");
    }

    private static (bool, string) GrantFief_FailsWhenGrantingToSelf()
    {
        var heroRegistry = new HeroRegistry();
        heroRegistry.Create("player", "玩家", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);

        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A" },
            LordHeroIds = new List<string> { "player" }
        };

        // 玩家不能分封给自己
        var result = PlayerKingdomService.GrantFief(kingdom, "城堡A", "player", heroRegistry);
        if (result) return (false, "玩家不能分封给自己");

        return (true, "");
    }

    private static (bool, string) RevokeFief_FailsWhenPoiNotControlled()
    {
        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A" },
            LordHeroIds = new List<string> { "player", "hero_1" }
        };

        // 尝试收回一个不在 ControlledPoiNames 中的 POI
        var result = PlayerKingdomService.RevokeFief(kingdom, "不存在的城堡", "hero_1");
        if (result) return (false, "收回非控制 POI 应失败");

        return (true, "");
    }

    private static (bool, string) ChangeLaw_UpdatesAndEmitsNews()
    {
        var worldEngine = new WorldEventEngine();
        var kingdom = new PlayerKingdom
        {
            DisplayName = "测试王国",
            Laws = new KingdomLaws()
        };

        var newLaws = new KingdomLaws
        {
            TaxRate = TaxLaw.High,
            Conscription = ConscriptionLaw.Major
        };

        PlayerKingdomService.ChangeLaw(kingdom, newLaws, worldEngine);

        if (kingdom.Laws.TaxRate != TaxLaw.High)
            return (false, $"法律未更新: TaxRate 应为 High,得 {kingdom.Laws.TaxRate}");
        if (kingdom.Laws.Conscription != ConscriptionLaw.Major)
            return (false, $"法律未更新: Conscription 应为 Major,得 {kingdom.Laws.Conscription}");

        // 验证深拷贝:修改 newLaws 不应影响 kingdom.Laws
        newLaws.TaxRate = TaxLaw.Low;
        if (kingdom.Laws.TaxRate != TaxLaw.High)
            return (false, "法律应为深拷贝,修改原对象不应影响王国法律");

        if (!worldEngine.NewsQueue.Any(n => n.Type == "law_changed"))
            return (false, "法律变更应推送 law_changed 新闻");

        return (true, "");
    }

    private static (bool, string) PlayerKingdom_SerializeRoundtrip()
    {
        var kingdom = new PlayerKingdom
        {
            KingdomId = "player",
            DisplayName = "测试王国",
            FamilyName = "测试家族",
            BannerColor = new Color(0.5f, 0.3f, 0.1f),
            CapitalPoiName = "都城",
            FoundedDay = 42,
            ControlledPoiNames = new List<string> { "都城", "城堡A" },
            LordHeroIds = new List<string> { "player", "hero_1" },
            Laws = new KingdomLaws
            {
                TaxRate = TaxLaw.High,
                Conscription = ConscriptionLaw.Major,
                Religion = ReligionLaw.StateReligion,
                Trade = TradeLaw.Protected
            }
        };

        var data = kingdom.Serialize();
        var restored = PlayerKingdom.Deserialize(data);

        if (restored.KingdomId != "player") return (false, "KingdomId 不一致");
        if (restored.DisplayName != "测试王国") return (false, "DisplayName 不一致");
        if (restored.FamilyName != "测试家族") return (false, "FamilyName 不一致");
        if (restored.CapitalPoiName != "都城") return (false, "CapitalPoiName 不一致");
        if (restored.FoundedDay != 42) return (false, $"FoundedDay 不一致: {restored.FoundedDay}");
        if (restored.ControlledPoiNames.Count != 2) return (false, "ControlledPoiNames 数量不一致");
        if (!restored.ControlledPoiNames.Contains("城堡A")) return (false, "ControlledPoiNames 缺失 城堡A");
        if (restored.LordHeroIds.Count != 2) return (false, "LordHeroIds 数量不一致");
        if (restored.Laws.TaxRate != TaxLaw.High) return (false, "Laws.TaxRate 不一致");
        if (restored.Laws.Conscription != ConscriptionLaw.Major) return (false, "Laws.Conscription 不一致");
        if (restored.Laws.Religion != ReligionLaw.StateReligion) return (false, "Laws.Religion 不一致");
        if (restored.Laws.Trade != TradeLaw.Protected) return (false, "Laws.Trade 不一致");

        return (true, "");
    }

    private static (bool, string) PlayerKingdom_ControlsPoi_And_Counts()
    {
        var kingdom = new PlayerKingdom
        {
            ControlledPoiNames = new List<string> { "城堡A", "城镇B" },
            LordHeroIds = new List<string> { "player", "hero_1", "hero_2" }
        };

        if (!kingdom.ControlsPoi("城堡A")) return (false, "应控制 城堡A");
        if (kingdom.ControlsPoi("不存在的")) return (false, "不应控制不存在的 POI");
        if (kingdom.PoiCount != 2) return (false, $"PoiCount 应为 2,得 {kingdom.PoiCount}");
        if (kingdom.LordCount != 3) return (false, $"LordCount 应为 3,得 {kingdom.LordCount}");

        return (true, "");
    }

    private static (bool, string) Found_SetsDiplomaticRelations()
    {
        var heroRegistry = new HeroRegistry();
        var familyRegistry = new FamilyRegistry();
        var nations = new List<NationConfig>
        {
            new NationConfig { Id = "nation_a" },
            new NationConfig { Id = "nation_b" }
        };
        var worldEngine = new WorldEventEngine();
        var influence = new InfluenceTracker();

        var capital = new OverworldPOI { PoiName = "都城", Position = Vector2.Zero };
        var pending = new List<string> { "都城" };

        PlayerKingdomService.Found(
            "测试王国", "测试家族", capital, Colors.Blue, 1,
            heroRegistry, familyRegistry, nations, worldEngine, influence, pending);

        // 验证玩家与其他国家建立了外交关系
        if (worldEngine.GetRelation("player", "nation_a") != 0)
            return (false, "应与 nation_a 建立中立关系 (0)");
        if (worldEngine.GetRelation("player", "nation_b") != 0)
            return (false, "应与 nation_b 建立中立关系 (0)");

        return (true, "");
    }
}
