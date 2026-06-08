// FamilyRegistryTests.cs
// 家族注册表测试
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;

namespace BladeHex.Tests.Strategic;

public static class FamilyRegistryTests
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
        yield return Run(nameof(FamilyRegistry_Create_AddsAllMembers), FamilyRegistry_Create_AddsAllMembers);
        yield return Run(nameof(FamilyRegistry_PatriarchDied_PromotesHighestLevelMember), FamilyRegistry_PatriarchDied_PromotesHighestLevelMember);
        yield return Run(nameof(FamilyRegistry_PatriarchDied_NoMembers_RemovesFamily), FamilyRegistry_PatriarchDied_NoMembers_RemovesFamily);
        yield return Run(nameof(FamilyRegistry_AddMember_IncrementsCount), FamilyRegistry_AddMember_IncrementsCount);
        yield return Run(nameof(FamilyRegistry_SerializeRoundtrip), FamilyRegistry_SerializeRoundtrip);
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

    private static (bool, string) FamilyRegistry_Create_AddsAllMembers()
    {
        var registry = new FamilyRegistry();
        var members = new List<string> { "hero_1", "hero_2", "hero_3" };

        registry.Create("铁锤", "dwarves", "hero_1", members, 1);

        var family = registry.Get("铁锤");
        if (family == null) return (false, "家族未创建");
        if (family.MemberHeroIds.Count != 3) return (false, $"成员数量应为3，实际{family.MemberHeroIds.Count}");
        if (family.PatriarchHeroId != "hero_1") return (false, "首领ID不正确");

        return (true, "");
    }

    private static (bool, string) FamilyRegistry_PatriarchDied_PromotesHighestLevelMember()
    {
        var registry = new FamilyRegistry();
        var heroRegistry = new HeroRegistry();

        // 创建英雄
        var hero1 = heroRegistry.Create("dwarves", "英雄1", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);
        var hero2 = heroRegistry.Create("dwarves", "英雄2", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);
        var hero3 = heroRegistry.Create("dwarves", "英雄3", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);

        var members = new List<string> { hero1.HeroId, hero2.HeroId, hero3.HeroId };
        registry.Create("铁锤", "dwarves", hero1.HeroId, members, 1);

        // 首领死亡
        registry.OnPatriarchDied(hero1.HeroId, heroRegistry, 30);

        var family = registry.Get("铁锤");
        if (family == null) return (false, "家族不应消失");
        if (family.PatriarchHeroId == hero1.HeroId) return (false, "首领应已更换");
        if (string.IsNullOrEmpty(family.PatriarchHeroId)) return (false, "新首领不应为空");

        return (true, "");
    }

    private static (bool, string) FamilyRegistry_PatriarchDied_NoMembers_RemovesFamily()
    {
        var registry = new FamilyRegistry();
        var heroRegistry = new HeroRegistry();

        var hero1 = heroRegistry.Create("dwarves", "英雄1", "铁锤", OverworldPOI.LordPersonality.Balanced, 1);

        var members = new List<string> { hero1.HeroId };
        registry.Create("铁锤", "dwarves", hero1.HeroId, members, 1);

        // 首领死亡（无其他成员）
        registry.OnPatriarchDied(hero1.HeroId, heroRegistry, 30);

        var family = registry.Get("铁锤");
        if (family != null) return (false, "家族应已消亡");

        return (true, "");
    }

    private static (bool, string) FamilyRegistry_AddMember_IncrementsCount()
    {
        var registry = new FamilyRegistry();
        var members = new List<string> { "hero_1" };
        registry.Create("铁锤", "dwarves", "hero_1", members, 1);

        registry.AddMember("铁锤", "hero_2");

        var family = registry.Get("铁锤");
        if (family == null) return (false, "家族未创建");
        if (family.MemberHeroIds.Count != 2) return (false, $"成员数量应为2，实际{family.MemberHeroIds.Count}");

        return (true, "");
    }

    private static (bool, string) FamilyRegistry_SerializeRoundtrip()
    {
        var registry = new FamilyRegistry();
        var members = new List<string> { "hero_1", "hero_2", "hero_3" };
        registry.Create("铁锤", "dwarves", "hero_1", members, 1);

        var data = registry.Serialize();
        var newRegistry = FamilyRegistry.Deserialize(data);

        var family = newRegistry.Get("铁锤");
        if (family == null) return (false, "反序列化后家族丢失");
        if (family.FamilyName != "铁锤") return (false, "家族名称不一致");
        if (family.MemberHeroIds.Count != 3) return (false, "成员数量不一致");

        return (true, "");
    }
}
