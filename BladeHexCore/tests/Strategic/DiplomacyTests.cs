using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.Diplomacy;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class DiplomacyTests
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
        yield return Run(nameof(Relation_Clamped_Within_Bounds), Relation_Clamped_Within_Bounds);
        yield return Run(nameof(Relation_Symmetry), Relation_Symmetry);
        yield return Run(nameof(Truce_Symmetry), Truce_Symmetry);
        yield return Run(nameof(Truce_Expiry), Truce_Expiry);
        yield return Run(nameof(DeclareWar_InTruce_Fails), DeclareWar_InTruce_Fails);
        yield return Run(nameof(DeclareWar_Cooldown), DeclareWar_Cooldown);
        yield return Run(nameof(ProposePeace_Cooldown), ProposePeace_Cooldown);
        yield return Run(nameof(ProposePeace_Acceptance_Decisions), ProposePeace_Acceptance_Decisions);
        yield return Run(nameof(Serialization_Roundtrip), Serialization_Roundtrip);
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
            return (name, false, $"Exception: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static (bool, string) Relation_Clamped_Within_Bounds()
    {
        var map = new FactionRelationMap();
        map.SetRelation("a", "b", 150);
        if (map.GetRelation("a", "b") != 100)
            return (false, $"上限应为 100，得 {map.GetRelation("a", "b")}");

        map.SetRelation("a", "b", -150);
        if (map.GetRelation("a", "b") != -100)
            return (false, $"下限应为 -100，得 {map.GetRelation("a", "b")}");

        return (true, "");
    }

    private static (bool, string) Relation_Symmetry()
    {
        var map = new FactionRelationMap();
        map.SetRelation("a", "b", 45);
        if (map.GetRelation("b", "a") != 45)
            return (false, $"对称关系不对，B对A应为 45，得 {map.GetRelation("b", "a")}");

        map.AdjustRelation("b", "a", -10);
        if (map.GetRelation("a", "b") != 35)
            return (false, $"调整B对A，A对B应随之改变为 35，得 {map.GetRelation("a", "b")}");

        return (true, "");
    }

    private static (bool, string) Truce_Symmetry()
    {
        var map = new FactionRelationMap();
        map.SetTruce("a", "b", 30, 10); // 第10天停战30天

        if (!map.IsInTruce("a", "b", 15)) return (false, "第15天应处于停战中");
        if (!map.IsInTruce("b", "a", 15)) return (false, "对称查询第15天应处于停战中");
        if (map.GetTruceRemainingDays("a", "b", 15) != 25)
            return (false, $"剩余天数应为 25，得 {map.GetTruceRemainingDays("a", "b", 15)}");

        return (true, "");
    }

    private static (bool, string) Truce_Expiry()
    {
        var map = new FactionRelationMap();
        map.SetTruce("a", "b", 30, 10); // 第10天停战30天，过期日为 40

        if (!map.IsInTruce("a", "b", 39)) return (false, "第39天应处于停战中");
        if (map.IsInTruce("a", "b", 40)) return (false, "第40天应已停战过期");

        map.TickTruces(40);
        if (map.GetTruceRemainingDays("a", "b", 40) != 0) return (false, "已过期的停战未被清理");

        return (true, "");
    }

    private static (bool, string) DeclareWar_InTruce_Fails()
    {
        var engine = new WorldEventEngine { CurrentDay = 1 };
        engine.Influence.Add("nation_a", 100, "init");
        
        var relationMap = new FactionRelationMap();
        relationMap.SetRelation("nation_a", "nation_b", -50);
        
        // 设置停战
        relationMap.SetTruce("nation_a", "nation_b", 30, 1);

        // 宣战，应该因为在 Truce 中而失败
        var result = DiplomacyService.DeclareWar("nation_a", "nation_b", engine, relationMap);
        if (result != DiplomacyResult.InTruce)
            return (false, $"在停战期内宣战应返回 InTruce，实际为 {result}");

        if (engine.AreAtWar("nation_a", "nation_b"))
            return (false, "不应成功建立战争状态");

        return (true, "");
    }

    private static (bool, string) DeclareWar_Cooldown()
    {
        var engine = new WorldEventEngine { CurrentDay = 1 };
        engine.Influence.Add("nation_a", 200, "init");
        
        var relationMap = new FactionRelationMap();
        relationMap.SetRelation("nation_a", "nation_b", -50);

        // 第一次宣战应成功
        var result = DiplomacyService.DeclareWar("nation_a", "nation_b", engine, relationMap);
        if (result != DiplomacyResult.Success)
            return (false, $"首次宣战应成功，得 {result}");

        // 模拟战争结束后，尝试再次宣战（处于冷却期内）
        var war = engine.ActiveWars.First();
        engine.ActiveWars.Remove(war);

        var result2 = DiplomacyService.DeclareWar("nation_a", "nation_b", engine, relationMap);
        if (result2 != DiplomacyResult.InCooldown)
            return (false, $"冷却期内宣战应返回 InCooldown，实际为 {result2}");

        return (true, "");
    }

    private static (bool, string) ProposePeace_Cooldown()
    {
        var engine = new WorldEventEngine { CurrentDay = 1 };
        engine.Influence.Add("proposer", 200, "init");

        var relationMap = new FactionRelationMap();
        relationMap.SetRelation("proposer", "target", -50);
        
        engine.ActiveWars.Add(new WarState { NationA = "proposer", NationB = "target", DaysSinceStart = 0 });

        var relations = new HeroRelationMatrix();
        var entities = new List<OverworldEntity>();

        // 模拟求和被拒绝，应该启动求和冷却
        var result = DiplomacyService.ProposePeace("proposer", "target", engine, relationMap, relations, entities, skipAiCheck: false);
        // 因为 entities 里没有领主且没有持续时间，平均关系 0，持续时间 0，接受率较小。如果随机到了未接受，就会返回 Failed
        if (result != DiplomacyResult.Failed && result != DiplomacyResult.Success)
            return (false, $"求和结果不符合预期，实际为 {result}");

        if (result == DiplomacyResult.Failed)
        {
            // 验证是否处于议和冷却
            if (!relationMap.IsProposePeaceInCooldown("proposer", "target", 1))
                return (false, "求和失败后未正确进入求和冷却");
        }

        return (true, "");
    }

    private static (bool, string) ProposePeace_Acceptance_Decisions()
    {
        var relationMap = new FactionRelationMap();
        var relations = new HeroRelationMatrix();
        var entities = new List<OverworldEntity>();

        // 模拟 target 势力有领主
        var targetLord = new OverworldEntity
        {
            Faction = "target",
            IsNamedCharacter = true,
            HeroId = "lord_target_1",
            IsAlive = true
        };
        entities.Add(targetLord);

        // 设置极佳的个人关系
        relations.Set("lord_target_1", "player", 80);

        // 设置国家关系为 -30
        relationMap.SetRelation("player", "target", -30);

        // 测试和谈接受几率评估：有效关系为 -30 + 80 * 0.5 = 10，战争进行了 40 天
        // 概率应该为 20 + 10 * 0.5 + 40 * 0.5 = 45%
        // 如果战争进行了 160 天，概率应为 20 + 5 + 80 = 105% (钳制到 100%)
        // 在 skipAiCheck=true 时应必定为 Success
        var engine = new WorldEventEngine { CurrentDay = 1 };
        engine.ActiveWars.Add(new WarState { NationA = "player", NationB = "target", DaysSinceStart = 160 });
        engine.Influence.Add("player", 100, "init");

        // 160天必定成功
        var result = DiplomacyService.ProposePeace("player", "target", engine, relationMap, relations, entities, skipAiCheck: false);
        if (result != DiplomacyResult.Success)
            return (false, $"160天超长战争且领主友好，应该 100% 接受议和。实际为: {result}");

        return (true, "");
    }

    private static (bool, string) Serialization_Roundtrip()
    {
        var map = new FactionRelationMap();
        map.SetRelation("a", "b", 45);
        map.SetTruce("a", "b", 30, 10);
        map.SetDeclareWarCooldown("a", "b", 10, 10);

        var data = map.Serialize();

        var map2 = new FactionRelationMap();
        map2.Deserialize(data);

        if (map2.GetRelation("a", "b") != 45) return (false, $"关系值序列化失败，得 {map2.GetRelation("a", "b")}");
        if (!map2.IsInTruce("a", "b", 15)) return (false, "停战序列化失败");
        if (!map2.IsDeclareWarInCooldown("a", "b", 15)) return (false, "宣战冷却序列化失败");

        return (true, "");
    }
}
