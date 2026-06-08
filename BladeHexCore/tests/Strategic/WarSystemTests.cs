// WarSystemTests.cs
// 战争闭环 MVP Core 层单元测试套件
// 覆盖:WarObjectivePlanner, InfluenceTracker, PoiTransferService,
//       KingdomDecisionService, PlayerNationResolver, WarBattleJoinService
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class WarSystemTests
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
        // InfluenceTracker
        yield return Run(nameof(Influence_AddAndGet_RespectsBounds), Influence_AddAndGet_RespectsBounds);
        yield return Run(nameof(Influence_TrySpend_FailsWhenInsufficient), Influence_TrySpend_FailsWhenInsufficient);
        yield return Run(nameof(Influence_SerializeRoundtrip), Influence_SerializeRoundtrip);

        // WarObjectivePlanner
        yield return Run(nameof(Planner_DoesNotSelectOwnPois), Planner_DoesNotSelectOwnPois);
        yield return Run(nameof(Planner_LimitsToFiveTargets), Planner_LimitsToFiveTargets);
        yield return Run(nameof(Planner_RemovesCapturedPoiOnRefresh), Planner_RemovesCapturedPoiOnRefresh);
        yield return Run(nameof(Planner_OnlyRefreshesEveryFiveDays), Planner_OnlyRefreshesEveryFiveDays);

        // PoiTransferService
        yield return Run(nameof(PoiTransfer_AppliesAllSideEffects), PoiTransfer_AppliesAllSideEffects);
        yield return Run(nameof(PoiTransfer_RaisesEvent), PoiTransfer_RaisesEvent);
        yield return Run(nameof(PoiTransfer_EmitsNewsAndInfluence_WhenPlayerNearby), PoiTransfer_EmitsNewsAndInfluence_WhenPlayerNearby);
        yield return Run(nameof(PoiTransfer_SkipsInfluence_WhenPlayerFar), PoiTransfer_SkipsInfluence_WhenPlayerFar);

        // KingdomDecisionService
        yield return Run(nameof(Decision_DeclareWar_FailsWhenInfluenceLow), Decision_DeclareWar_FailsWhenInfluenceLow);
        yield return Run(nameof(Decision_DeclareWar_FailsWhenRelationTooHigh), Decision_DeclareWar_FailsWhenRelationTooHigh);
        yield return Run(nameof(Decision_DeclareWar_SucceedsAndChargesInfluence), Decision_DeclareWar_SucceedsAndChargesInfluence);
        yield return Run(nameof(Decision_DeclareWar_FailsWhenAlreadyAtWar), Decision_DeclareWar_FailsWhenAlreadyAtWar);
        yield return Run(nameof(Decision_MakePeace_FailsWhenNotAtWar), Decision_MakePeace_FailsWhenNotAtWar);
        yield return Run(nameof(Decision_MakePeace_SucceedsAndRemovesWar), Decision_MakePeace_SucceedsAndRemovesWar);

        // PlayerNationResolver
        yield return Run(nameof(Resolver_ReturnsNullWhenAllReputationsLow), Resolver_ReturnsNullWhenAllReputationsLow);
        yield return Run(nameof(Resolver_DoesNotSwitchBeforeStabilityWindow), Resolver_DoesNotSwitchBeforeStabilityWindow);
        yield return Run(nameof(Resolver_SwitchesAfterSevenDayStability), Resolver_SwitchesAfterSevenDayStability);

        // WarBattleJoinService
        yield return Run(nameof(JoinService_ReturnsNull_WhenPlayerFar), JoinService_ReturnsNull_WhenPlayerFar);
        yield return Run(nameof(JoinService_DetectsSiege_WhenPlayerNear), JoinService_DetectsSiege_WhenPlayerNear);
        yield return Run(nameof(JoinService_DetectsFieldBattle), JoinService_DetectsFieldBattle);
        yield return Run(nameof(JoinService_IgnoresAllyLords), JoinService_IgnoresAllyLords);

        // WarLordOrders + DecideLordArmy 集成
        yield return Run(nameof(LordOrders_AssignsTargetWhenInRange), LordOrders_AssignsTargetWhenInRange);
        yield return Run(nameof(LordOrders_RespectsLockoutWindow), LordOrders_RespectsLockoutWindow);
        yield return Run(nameof(LordOrders_RejectsTargetTooFar), LordOrders_RejectsTargetTooFar);
        yield return Run(nameof(LordOrders_LimitsAssignmentsPerObjective), LordOrders_LimitsAssignmentsPerObjective);
        yield return Run(nameof(LordOrders_PrefersClosestAmongSamePriority), LordOrders_PrefersClosestAmongSamePriority);
        yield return Run(nameof(LordOrders_HighPriorityBeatsCloserNormal), LordOrders_HighPriorityBeatsCloserNormal);

        // Planner 攻方亡国边界
        yield return Run(nameof(Planner_AttackerHasNoPois_ReturnsEmpty), Planner_AttackerHasNoPois_ReturnsEmpty);

        // BattleResolver — 玩家位置 nullable 边界
        yield return Run(nameof(BattleResolver_NoPlayerPosition_DoesNotAwardInfluence), BattleResolver_NoPlayerPosition_DoesNotAwardInfluence);
        yield return Run(nameof(BattleResolver_PlayerAtOrigin_AwardsInfluence), BattleResolver_PlayerAtOrigin_AwardsInfluence);

        // 序列化
        yield return Run(nameof(WarState_SerializeRoundtrip), WarState_SerializeRoundtrip);

        // KingdomDecisionService — 缺失路径
        yield return Run(nameof(Decision_DeclareWar_InvalidNation_EmptyString), Decision_DeclareWar_InvalidNation_EmptyString);
        yield return Run(nameof(Decision_DeclareWar_InvalidNation_SameNation), Decision_DeclareWar_InvalidNation_SameNation);
        yield return Run(nameof(Decision_MakePeace_InvalidNation_Empty), Decision_MakePeace_InvalidNation_Empty);
        yield return Run(nameof(Decision_MakePeace_FailsWhenInfluenceLow), Decision_MakePeace_FailsWhenInfluenceLow);

        // WorldEventEngine — 外交工具 & 联盟互斥
        yield return Run(nameof(Engine_AreAtWar_ReverseOrder), Engine_AreAtWar_ReverseOrder);
        yield return Run(nameof(Engine_AreAllied_ReturnsTrue), Engine_AreAllied_ReturnsTrue);
        yield return Run(nameof(Engine_AdjustRelation_ClampsToBounds), Engine_AdjustRelation_ClampsToBounds);
        yield return Run(nameof(Engine_NewsQueue_CapsAt50), Engine_NewsQueue_CapsAt50);
        yield return Run(nameof(Engine_GetVisibleNews_DistanceDelay), Engine_GetVisibleNews_DistanceDelay);
        yield return Run(nameof(Engine_OnLairCleared_ReducesThreat), Engine_OnLairCleared_ReducesThreat);
        yield return Run(nameof(Engine_OnMonsterDefeatedByNation_ReducesThreat), Engine_OnMonsterDefeatedByNation_ReducesThreat);
        yield return Run(nameof(Engine_SerializeAlliances_Roundtrip), Engine_SerializeAlliances_Roundtrip);
        yield return Run(nameof(Engine_DeserializeNull_IsNoop), Engine_DeserializeNull_IsNoop);

        // BattleResolver — AreHostile 战争状态 + subparty 新闻
        yield return Run(nameof(BattleResolver_AreHostile_ViaWarState), BattleResolver_AreHostile_ViaWarState);
        yield return Run(nameof(BattleResolver_SubpartyVictory_EmitsNews), BattleResolver_SubpartyVictory_EmitsNews);

        // WarBattleJoinService — 边界
        yield return Run(nameof(JoinService_NullInputs_ReturnsNull), JoinService_NullInputs_ReturnsNull);
        yield return Run(nameof(JoinService_NeutralFaction_Skipped), JoinService_NeutralFaction_Skipped);
        yield return Run(nameof(JoinService_ArmyJoin_HigherPriority_ThanSiege), JoinService_ArmyJoin_HigherPriority_ThanSiege);

        // WarObjectivePlanner — 守方亡国 + 首次刷新
        yield return Run(nameof(Planner_DefenderHasNoPois_ReturnsEmpty), Planner_DefenderHasNoPois_ReturnsEmpty);
        yield return Run(nameof(Planner_FirstRefresh_OnDay1_WithEmptyObjectives), Planner_FirstRefresh_OnDay1_WithEmptyObjectives);

        // WarLordOrders — 空目标 + 失效目标重新分配
        yield return Run(nameof(LordOrders_EmptyObjectives_ClearsAssignment), LordOrders_EmptyObjectives_ClearsAssignment);
        yield return Run(nameof(LordOrders_ExpiredTarget_ReassignsAfterLockout), LordOrders_ExpiredTarget_ReassignsAfterLockout);
    }

    // ============================================================================
    // 工具方法
    // ============================================================================

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    private static OverworldPOI MakePoi(string name, string faction, Vector2 pos, int prosperity = 50, int garrisonMax = 40)
    {
        return new OverworldPOI
        {
            PoiName = name,
            PoiTypeEnum = OverworldPOI.POIType.Town,
            OwningFaction = faction,
            Position = pos,
            Prosperity = prosperity,
            GarrisonMax = garrisonMax,
            GarrisonCurrent = garrisonMax,
        };
    }

    private static OverworldEntity MakeLord(string name, string faction, Vector2 pos)
    {
        return new OverworldEntity
        {
            EntityName = name,
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            Faction = faction,
            Position = pos,
            HomePosition = pos,
            CombatPower = 100,
            GarrisonSize = 30,
            IsAlive = true,
            VisionRange = 400.0f,
        };
    }

    // ============================================================================
    // InfluenceTracker
    // ============================================================================

    private static (bool, string) Influence_AddAndGet_RespectsBounds()
    {
        var t = new InfluenceTracker();
        if (t.Get("a") != 0) return (false, "初始值应为 0");

        t.Add("a", 50, "test");
        if (t.Get("a") != 50) return (false, $"+50 后应为 50,得 {t.Get("a")}");

        t.Add("a", 200, "test");
        if (t.Get("a") != 200) return (false, $"上限应为 200,得 {t.Get("a")}");

        t.Add("a", -300, "test");
        if (t.Get("a") != 0) return (false, $"下限应为 0,得 {t.Get("a")}");

        if (t.Get("") != 0) return (false, "空 nationId 应返回 0");

        return (true, "");
    }

    private static (bool, string) Influence_TrySpend_FailsWhenInsufficient()
    {
        var t = new InfluenceTracker();
        t.Add("a", 30, "init");

        if (t.TrySpend("a", 50, "test")) return (false, "余额不足应失败");
        if (t.Get("a") != 30) return (false, "失败时不应扣费");

        if (!t.TrySpend("a", 20, "test")) return (false, "余额足够应成功");
        if (t.Get("a") != 10) return (false, $"成功后应扣到 10,得 {t.Get("a")}");

        return (true, "");
    }

    private static (bool, string) Influence_SerializeRoundtrip()
    {
        var t = new InfluenceTracker();
        t.Add("a", 50, "init");
        t.Add("b", 100, "init");
        var data = t.Serialize();

        var t2 = new InfluenceTracker();
        t2.Deserialize(data);

        if (t2.Get("a") != 50) return (false, $"a 应为 50,得 {t2.Get("a")}");
        if (t2.Get("b") != 100) return (false, $"b 应为 100,得 {t2.Get("b")}");
        return (true, "");
    }

    // ============================================================================
    // WarObjectivePlanner
    // ============================================================================

    private static (bool, string) Planner_DoesNotSelectOwnPois()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("a1", "nation_a", new Vector2(0, 0)),
            MakePoi("a2", "nation_a", new Vector2(100, 0)),
            MakePoi("b1", "nation_b", new Vector2(500, 0)),
            MakePoi("b2", "nation_b", new Vector2(600, 0)),
        };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);

        foreach (var name in war.ObjectivesA)
        {
            var p = pois.FirstOrDefault(x => x.PoiName == name);
            if (p == null) return (false, $"目标 {name} 不存在");
            if (p.OwningFaction != "nation_b") return (false, $"A 选了非 B 方 POI: {name}");
        }
        foreach (var name in war.ObjectivesB)
        {
            var p = pois.FirstOrDefault(x => x.PoiName == name);
            if (p == null) return (false, $"目标 {name} 不存在");
            if (p.OwningFaction != "nation_a") return (false, $"B 选了非 A 方 POI: {name}");
        }
        return (true, "");
    }

    private static (bool, string) Planner_LimitsToFiveTargets()
    {
        var pois = new List<OverworldPOI>();
        // A 方 1 个,B 方 10 个
        pois.Add(MakePoi("a1", "nation_a", new Vector2(0, 0)));
        for (int i = 0; i < 10; i++)
            pois.Add(MakePoi($"b{i}", "nation_b", new Vector2(500 + i * 50, 0)));

        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);

        if (war.ObjectivesA.Count > 5)
            return (false, $"目标数应 ≤ 5,得 {war.ObjectivesA.Count}");
        if (war.ObjectivesA.Count == 0)
            return (false, "应至少选出 1 个目标");
        return (true, "");
    }

    private static (bool, string) Planner_RemovesCapturedPoiOnRefresh()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("a1", "nation_a", new Vector2(0, 0)),
            MakePoi("b1", "nation_b", new Vector2(500, 0)),
            MakePoi("b2", "nation_b", new Vector2(600, 0)),
        };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);
        if (!war.ObjectivesA.Contains("b1") && !war.ObjectivesA.Contains("b2"))
            return (false, "首次刷新应包含 b1/b2");

        // 模拟 b1 被夺取
        pois[1].OwningFaction = "nation_a";

        // 不到 5 天调用应仅清理无效目标(不强行重新选)
        ctx.CurrentDay = 3;
        WarObjectivePlanner.RefreshObjectives(war, ctx);
        if (war.ObjectivesA.Contains("b1"))
            return (false, "已被攻陷的 b1 应从目标中移除");

        return (true, "");
    }

    private static (bool, string) Planner_OnlyRefreshesEveryFiveDays()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("a1", "nation_a", new Vector2(0, 0)),
            MakePoi("b1", "nation_b", new Vector2(500, 0)),
        };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);
        int firstRefreshDay = war.LastObjectiveRefreshDay;
        if (firstRefreshDay != 1) return (false, $"首次刷新应记录 day=1,得 {firstRefreshDay}");

        // 第 3 天,不应刷新
        ctx.CurrentDay = 3;
        WarObjectivePlanner.RefreshObjectives(war, ctx);
        if (war.LastObjectiveRefreshDay != 1)
            return (false, "未到 5 天间隔不应刷新 LastObjectiveRefreshDay");

        // 第 6 天,应刷新
        ctx.CurrentDay = 6;
        WarObjectivePlanner.RefreshObjectives(war, ctx);
        if (war.LastObjectiveRefreshDay != 6)
            return (false, $"已超过 5 天应刷新到 day=6,得 {war.LastObjectiveRefreshDay}");

        return (true, "");
    }

    // ============================================================================
    // PoiTransferService
    // ============================================================================

    private static (bool, string) PoiTransfer_AppliesAllSideEffects()
    {
        var poi = MakePoi("test1", "nation_a", new Vector2(0, 0), prosperity: 80, garrisonMax: 100);
        poi.GarrisonCurrent = 100;
        poi.BeginSiege(MakeLord("attacker", "nation_b", Vector2.Zero));

        PoiTransferService.Apply(poi, "nation_b", null, currentDay: 5, engine: null, playerNearby: false);

        if (poi.OwningFaction != "nation_b") return (false, "OwningFaction 未切换");
        if (poi.GarrisonCurrent != 25) return (false, $"驻军应重置为 100/4=25,得 {poi.GarrisonCurrent}");
        if (poi.Prosperity != 50) return (false, $"繁荣度应 -30 = 50,得 {poi.Prosperity}");
        if (poi.IsUnderSiege) return (false, "围攻状态未解除");

        // 繁荣度下限 0
        var poi2 = MakePoi("test2", "nation_a", Vector2.Zero, prosperity: 10);
        PoiTransferService.Apply(poi2, "nation_b", null, 1, null);
        if (poi2.Prosperity != 0) return (false, $"繁荣度应被下限截断为 0,得 {poi2.Prosperity}");

        return (true, "");
    }

    private static (bool, string) PoiTransfer_RaisesEvent()
    {
        PoiTransferEvent? captured = null;
        Action<PoiTransferEvent> handler = e => captured = e;
        PoiTransferService.PoiTransferred += handler;

        try
        {
            var poi = MakePoi("test_evt", "nation_a", Vector2.Zero);
            PoiTransferService.Apply(poi, "nation_b", null, 7, null);

            if (captured == null) return (false, "事件未触发");
            if (captured.OldFaction != "nation_a") return (false, $"OldFaction 错: {captured.OldFaction}");
            if (captured.NewFaction != "nation_b") return (false, $"NewFaction 错: {captured.NewFaction}");
            if (captured.Day != 7) return (false, $"Day 错: {captured.Day}");
            if (captured.Poi != poi) return (false, "Poi 引用不一致");
        }
        finally
        {
            PoiTransferService.PoiTransferred -= handler;
        }
        return (true, "");
    }

    private static (bool, string) PoiTransfer_EmitsNewsAndInfluence_WhenPlayerNearby()
    {
        var engine = new WorldEventEngine();
        var poi = MakePoi("captured1", "nation_a", new Vector2(100, 100));

        PoiTransferService.Apply(poi, "nation_b", null, 1, engine, playerNearby: true);

        if (!engine.NewsQueue.Any(n => n.Type == "poi_captured"))
            return (false, "应记录 poi_captured 新闻");
        if (engine.Influence.Get("nation_b") != 30)
            return (false, $"newFaction 影响力应 +30,得 {engine.Influence.Get("nation_b")}");
        return (true, "");
    }

    private static (bool, string) PoiTransfer_SkipsInfluence_WhenPlayerFar()
    {
        var engine = new WorldEventEngine();
        var poi = MakePoi("captured2", "nation_a", new Vector2(100, 100));

        PoiTransferService.Apply(poi, "nation_b", null, 1, engine, playerNearby: false);

        if (!engine.NewsQueue.Any(n => n.Type == "poi_captured"))
            return (false, "新闻仍应记录");
        if (engine.Influence.Get("nation_b") != 0)
            return (false, $"玩家不在场,影响力不应增长,得 {engine.Influence.Get("nation_b")}");
        return (true, "");
    }

    // ============================================================================
    // KingdomDecisionService
    // ============================================================================

    private static (bool, string) Decision_DeclareWar_FailsWhenInfluenceLow()
    {
        var engine = new WorldEventEngine();
        engine.SetRelation("a", "b", -50);

        var result = KingdomDecisionService.TryDeclareWar("a", "b", engine);

        if (result != DecisionResult.InsufficientInfluence)
            return (false, $"应返回 InsufficientInfluence,得 {result}");
        if (engine.AreAtWar("a", "b"))
            return (false, "失败时不应建立战争");
        return (true, "");
    }

    private static (bool, string) Decision_DeclareWar_FailsWhenRelationTooHigh()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 100, "init");
        engine.SetRelation("a", "b", 0); // 高于 -30

        var result = KingdomDecisionService.TryDeclareWar("a", "b", engine);

        if (result != DecisionResult.RelationTooHigh)
            return (false, $"应返回 RelationTooHigh,得 {result}");
        if (engine.Influence.Get("a") != 100)
            return (false, "失败时不应扣影响力");
        return (true, "");
    }

    private static (bool, string) Decision_DeclareWar_SucceedsAndChargesInfluence()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 100, "init");
        engine.SetRelation("a", "b", -50);

        var result = KingdomDecisionService.TryDeclareWar("a", "b", engine);

        if (result != DecisionResult.Success)
            return (false, $"应返回 Success,得 {result}");
        if (!engine.AreAtWar("a", "b"))
            return (false, "ActiveWars 未建立");
        if (engine.Influence.Get("a") != 50)
            return (false, $"影响力应扣 50 = 50,得 {engine.Influence.Get("a")}");
        if (engine.GetRelation("a", "b") != -80)
            return (false, "宣战后关系应降到 -80");
        return (true, "");
    }

    private static (bool, string) Decision_DeclareWar_FailsWhenAlreadyAtWar()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 200, "init");
        engine.ActiveWars.Add(new WarState { NationA = "a", NationB = "b" });

        var result = KingdomDecisionService.TryDeclareWar("a", "b", engine);
        if (result != DecisionResult.AlreadyAtWar)
            return (false, $"应返回 AlreadyAtWar,得 {result}");
        if (engine.Influence.Get("a") != 200)
            return (false, "失败时不应扣影响力");
        return (true, "");
    }

    private static (bool, string) Decision_MakePeace_FailsWhenNotAtWar()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 200, "init");

        var result = KingdomDecisionService.TryMakePeace("a", "b", engine);
        if (result != DecisionResult.NotAtWar)
            return (false, $"应返回 NotAtWar,得 {result}");
        if (engine.Influence.Get("a") != 200)
            return (false, "失败时不应扣影响力");
        return (true, "");
    }

    private static (bool, string) Decision_MakePeace_SucceedsAndRemovesWar()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 200, "init");
        engine.ActiveWars.Add(new WarState { NationA = "a", NationB = "b", DaysSinceStart = 10 });

        var result = KingdomDecisionService.TryMakePeace("a", "b", engine);

        if (result != DecisionResult.Success)
            return (false, $"应返回 Success,得 {result}");
        if (engine.AreAtWar("a", "b"))
            return (false, "战争应被移除");
        if (engine.Influence.Get("a") != 120)
            return (false, $"影响力应扣 80,得 {engine.Influence.Get("a")}");
        return (true, "");
    }

    // ============================================================================
    // PlayerNationResolver
    // ============================================================================

    private static (bool, string) Resolver_ReturnsNullWhenAllReputationsLow()
    {
        var rep = new ReputationTracker();
        rep.AddReputation("a", 10);
        rep.AddReputation("b", 20);

        var resolver = new PlayerNationResolver();
        for (int day = 1; day <= 10; day++)
        {
            var result = resolver.GetCurrent(rep, day);
            if (result != null)
                return (false, $"全部声望<30 时应返回 null,得 {result}");
        }
        return (true, "");
    }

    private static (bool, string) Resolver_DoesNotSwitchBeforeStabilityWindow()
    {
        var rep = new ReputationTracker();
        rep.AddReputation("a", 50);
        var resolver = new PlayerNationResolver();

        // 第 1 天首次进入候选
        if (resolver.GetCurrent(rep, 1) != null)
            return (false, "稳定窗口前不应返回当前国");

        // 第 5 天还没满 7 天
        if (resolver.GetCurrent(rep, 5) != null)
            return (false, "5 天后仍未满稳定窗口,不应切换");

        return (true, "");
    }

    private static (bool, string) Resolver_SwitchesAfterSevenDayStability()
    {
        var rep = new ReputationTracker();
        rep.AddReputation("a", 50);
        var resolver = new PlayerNationResolver();

        resolver.GetCurrent(rep, 1);
        // 在第 8 天(>= 1 + 7),应切换为 a
        var result = resolver.GetCurrent(rep, 8);
        if (result != "a")
            return (false, $"7 天稳定后应切换为 a,得 {result}");

        return (true, "");
    }

    // ============================================================================
    // WarBattleJoinService
    // ============================================================================

    private static (bool, string) JoinService_ReturnsNull_WhenPlayerFar()
    {
        var poi = MakePoi("siegePoi", "nation_a", new Vector2(0, 0));
        var attacker = MakeLord("attk", "nation_b", new Vector2(20, 20));
        poi.BeginSiege(attacker);

        var pois = new List<OverworldPOI> { poi };
        var ents = new List<OverworldEntity> { attacker };

        var result = WarBattleJoinService.Query(new Vector2(5000, 5000), ents, pois);
        if (result != null) return (false, "玩家远离时不应返回 opportunity");
        return (true, "");
    }

    private static (bool, string) JoinService_DetectsSiege_WhenPlayerNear()
    {
        var poi = MakePoi("siegePoi", "nation_a", new Vector2(100, 100));
        var attacker = MakeLord("attk", "nation_b", new Vector2(120, 120));
        poi.BeginSiege(attacker);

        var pois = new List<OverworldPOI> { poi };
        var ents = new List<OverworldEntity> { attacker };

        // 玩家就在 POI 旁边
        var result = WarBattleJoinService.Query(new Vector2(150, 150), ents, pois);
        if (result == null) return (false, "玩家在围攻附近应返回 opportunity");
        if (result.Type != WarBattleType.Siege) return (false, $"应识别为 Siege,得 {result.Type}");
        if (result.DefenderPoi != poi) return (false, "DefenderPoi 不一致");
        if (result.Attacker != attacker) return (false, "Attacker 不一致");
        return (true, "");
    }

    private static (bool, string) JoinService_DetectsFieldBattle()
    {
        var lordA = MakeLord("a1", "nation_a", new Vector2(100, 100));
        var lordB = MakeLord("b1", "nation_b", new Vector2(180, 100)); // 距离 80,< 150
        lordA.CurrentAIState = OverworldEntity.AIState.Engaged;
        lordB.CurrentAIState = OverworldEntity.AIState.Engaged;
        lordA.EngagedWith = lordB;
        lordB.EngagedWith = lordA;

        var ents = new List<OverworldEntity> { lordA, lordB };
        var pois = new List<OverworldPOI>();
        var engine = new WorldEventEngine();
        engine.ActiveWars.Add(new WarState { NationA = "nation_a", NationB = "nation_b" });

        var result = WarBattleJoinService.Query(new Vector2(140, 100), ents, pois, engine: engine);
        if (result == null) return (false, "应检测到正在交战的野战机会");
        if (result.Type != WarBattleType.FieldBattle) return (false, $"应为 FieldBattle,得 {result.Type}");
        return (true, "");
    }

    private static (bool, string) JoinService_IgnoresAllyLords()
    {
        // 同 faction 的两支领主不应触发 FieldBattle
        var lordA1 = MakeLord("a1", "nation_a", new Vector2(100, 100));
        var lordA2 = MakeLord("a2", "nation_a", new Vector2(180, 100));

        var ents = new List<OverworldEntity> { lordA1, lordA2 };
        var pois = new List<OverworldPOI>();

        var result = WarBattleJoinService.Query(new Vector2(140, 100), ents, pois);
        if (result != null) return (false, "同 faction 不应触发野战");
        return (true, "");
    }

    // ============================================================================
    // WarLordOrders
    // ============================================================================

    private static (bool, string) LordOrders_AssignsTargetWhenInRange()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("a_capital", "nation_a", new Vector2(0, 0)),
            MakePoi("b_target", "nation_b", new Vector2(500, 0)), // 距离 lord 500px
        };
        var lord = MakeLord("aLord", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { lord };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };
        WarObjectivePlanner.RefreshObjectives(war, ctx);

        WarLordOrders.AssignLordToObjective(lord, war, war.ObjectivesA, allLords, currentDay: 1, allPois: pois);

        if (string.IsNullOrEmpty(lord.AssignedWarTargetPoiName))
            return (false, "应分配目标");
        if (lord.AssignedWarTargetPoiName != "b_target")
            return (false, $"应分配 b_target,得 {lord.AssignedWarTargetPoiName}");
        if (lord.WarTargetAssignedDay != 1)
            return (false, $"应记录 day=1,得 {lord.WarTargetAssignedDay}");

        return (true, "");
    }

    private static (bool, string) LordOrders_RespectsLockoutWindow()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("a_capital", "nation_a", new Vector2(0, 0)),
            MakePoi("b_a", "nation_b", new Vector2(400, 0)),
            MakePoi("b_b", "nation_b", new Vector2(450, 0)),
        };
        var lord = MakeLord("aLord", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { lord };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        war.ObjectivesA = new List<string> { "b_a", "b_b" };

        // 第 1 天分配
        WarLordOrders.AssignLordToObjective(lord, war, war.ObjectivesA, allLords, 1, pois);
        string firstTarget = lord.AssignedWarTargetPoiName;
        if (string.IsNullOrEmpty(firstTarget)) return (false, "首次应分配");

        // 第 3 天再调用 — 锁定 5 天内不该换
        WarLordOrders.AssignLordToObjective(lord, war, war.ObjectivesA, allLords, 3, pois);
        if (lord.AssignedWarTargetPoiName != firstTarget)
            return (false, $"锁定期内不应换目标,first={firstTarget} now={lord.AssignedWarTargetPoiName}");
        if (lord.WarTargetAssignedDay != 1)
            return (false, "锁定期内不应更新 WarTargetAssignedDay");

        return (true, "");
    }

    private static (bool, string) LordOrders_RejectsTargetTooFar()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("b_far", "nation_b", new Vector2(3000, 0)), // 远超 1500px
        };
        var lord = MakeLord("aLord", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { lord };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var objectives = new List<string> { "b_far" };

        WarLordOrders.AssignLordToObjective(lord, war, objectives, allLords, 1, pois);

        if (!string.IsNullOrEmpty(lord.AssignedWarTargetPoiName))
            return (false, $"距离 > 1500px 不应分配,得 {lord.AssignedWarTargetPoiName}");
        return (true, "");
    }

    private static (bool, string) LordOrders_LimitsAssignmentsPerObjective()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("b_only", "nation_b", new Vector2(500, 0)),
        };

        // 三支领主在同一位置
        var l1 = MakeLord("L1", "nation_a", new Vector2(0, 0));
        var l2 = MakeLord("L2", "nation_a", new Vector2(0, 0));
        var l3 = MakeLord("L3", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { l1, l2, l3 };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var objectives = new List<string> { "b_only" };

        WarLordOrders.AssignLordToObjective(l1, war, objectives, allLords, 1, pois);
        WarLordOrders.AssignLordToObjective(l2, war, objectives, allLords, 1, pois);
        WarLordOrders.AssignLordToObjective(l3, war, objectives, allLords, 1, pois);

        int assigned = allLords.Count(l => l.AssignedWarTargetPoiName == "b_only");
        if (assigned > 2)
            return (false, $"单目标应最多分配 2 个领主,得 {assigned}");
        return (true, "");
    }

    // ============================================================================
    // 技术债修复回归测试
    // ============================================================================

    private static (bool, string) LordOrders_PrefersClosestAmongSamePriority()
    {
        // 同优先级目标:lord 应选距离更近的
        // 注意 objectives 顺序决定优先级,我们手工塞入,让 b_far 与 b_near 都是 High(idx 0/1)
        var pois = new List<OverworldPOI>
        {
            MakePoi("b_far", "nation_b", new Vector2(1000, 0)),
            MakePoi("b_near", "nation_b", new Vector2(300, 0)),
        };
        var lord = MakeLord("L", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { lord };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };

        // b_far 在前(idx=0,优先级 2),b_near 在后(idx=1,也是优先级 2,因为前两个都 High)
        var objectives = new List<string> { "b_far", "b_near" };

        WarLordOrders.AssignLordToObjective(lord, war, objectives, allLords, 1, pois);

        if (lord.AssignedWarTargetPoiName != "b_near")
            return (false, $"同优先级应选距离更近,期望 b_near 实际 {lord.AssignedWarTargetPoiName}");
        return (true, "");
    }

    private static (bool, string) LordOrders_HighPriorityBeatsCloserNormal()
    {
        // High 优先级目标即便较远,也应优先于较近的 Normal 目标
        var pois = new List<OverworldPOI>
        {
            MakePoi("h_far", "nation_b", new Vector2(800, 0)),  // High,远
            MakePoi("n_near", "nation_b", new Vector2(200, 0)), // Normal,近
            MakePoi("h_filler", "nation_b", new Vector2(900, 0)), // 填位让 High slot 占满
        };
        var lord = MakeLord("L", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { lord };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };

        // 前 2 个 High: h_far + h_filler;之后 Normal: n_near
        var objectives = new List<string> { "h_far", "h_filler", "n_near" };

        WarLordOrders.AssignLordToObjective(lord, war, objectives, allLords, 1, pois);

        if (lord.AssignedWarTargetPoiName != "h_far")
            return (false, $"High 应优先于 Normal,期望 h_far 实际 {lord.AssignedWarTargetPoiName}");
        return (true, "");
    }

    private static (bool, string) Planner_AttackerHasNoPois_ReturnsEmpty()
    {
        // 攻方 POI 全数被夺,Planner 应放弃攻势(避免距离=0 全选 bug)
        var pois = new List<OverworldPOI>
        {
            MakePoi("b1", "nation_b", new Vector2(0, 0)),
            MakePoi("b2", "nation_b", new Vector2(100, 0)),
            MakePoi("b3", "nation_b", new Vector2(200, 0)),
        };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);

        if (war.ObjectivesA.Count != 0)
            return (false, $"攻方亡国时不应制定攻势,得 {war.ObjectivesA.Count} 个目标");
        return (true, "");
    }

    private static (bool, string) BattleResolver_NoPlayerPosition_DoesNotAwardInfluence()
    {
        // 不传 playerPosition (null) 时,即使敌国领主被击败也不发放影响力
        var engine = new WorldEventEngine();
        engine.SetRelation("a", "hostile", -50); // a 与 hostile 关系 ≤ -30 是发影响力的前置

        // 注意:BattleResolver.AreHostile 当前只把 faction == "hostile" 视为敌对
        var lordA = MakeLord("aLord", "a", new Vector2(0, 0));
        lordA.CombatPower = 9999;
        lordA.PartySize = 30;
        var lordB = MakeLord("bLord", "hostile", new Vector2(50, 0));
        lordB.CombatPower = 0.5f; // < 1 触发 destroyed
        lordB.PartySize = 1;
        var entities = new List<OverworldEntity> { lordA, lordB };

        var resolver = new BladeHex.Strategic.BattleResolver();
        resolver.ProcessEntityInteractions(entities, engine /* playerPosition 默认 null */);

        if (engine.Influence.Get("a") != 0)
            return (false, $"未提供 playerPosition 时不应发放影响力,得 {engine.Influence.Get("a")}");
        return (true, "");
    }

    private static (bool, string) BattleResolver_PlayerAtOrigin_AwardsInfluence()
    {
        // 玩家在 (0,0) 时,敌对领主被击败应发放影响力(修复 default(Vector2) 误判 bug)
        // 注意:BattleResolver.AreHostile 当前只识别 faction == "hostile"
        var engine = new WorldEventEngine();
        engine.SetRelation("a", "hostile", -50);

        var lordA = MakeLord("aLord", "a", new Vector2(0, 0));
        lordA.CombatPower = 9999;
        lordA.PartySize = 30;
        var lordB = MakeLord("bLord", "hostile", new Vector2(50, 0));
        lordB.CombatPower = 0.5f;
        lordB.PartySize = 1;
        var entities = new List<OverworldEntity> { lordA, lordB };

        var resolver = new BladeHex.Strategic.BattleResolver();
        resolver.ProcessEntityInteractions(entities, engine, new Vector2(0, 0));
        resolver.UpdateEngagements(entities, 30.0f, engine, new Vector2(0, 0));

        // a 击败 hostile 领主,a 与 hostile 关系 -50 ≤ -30 → +5 影响力
        int aInfluence = engine.Influence.Get("a");
        if (aInfluence < 5)
            return (false, $"玩家在 (0,0) 应触发影响力发放,a 应 ≥ 5,得 {aInfluence};lordB.IsAlive={lordB.IsAlive} state={lordB.CurrentAIState} CP={lordB.CombatPower}");
        return (true, "");
    }

    // ============================================================================
    // WarState 序列化
    // ============================================================================

    private static (bool, string) WarState_SerializeRoundtrip()
    {
        var engine = new WorldEventEngine();
        var war = new WarState
        {
            NationA = "a",
            NationB = "b",
            DaysSinceStart = 12,
            WarScoreA = 35,
            ObjectivesA = new List<string> { "x1", "x2" },
            ObjectivesB = new List<string> { "y1" },
            LastObjectiveRefreshDay = 6,
        };
        engine.ActiveWars.Add(war);
        engine.Influence.Add("a", 75, "init");

        var data = engine.Serialize();

        var engine2 = new WorldEventEngine();
        engine2.Deserialize(data);

        if (engine2.ActiveWars.Count != 1) return (false, "活跃战争数不一致");
        var w2 = engine2.ActiveWars[0];
        if (w2.WarScoreA != 35) return (false, $"WarScoreA 错: {w2.WarScoreA}");
        if (w2.ObjectivesA.Count != 2 || w2.ObjectivesA[0] != "x1")
            return (false, "ObjectivesA 序列化丢失");
        if (w2.ObjectivesB.Count != 1 || w2.ObjectivesB[0] != "y1")
            return (false, "ObjectivesB 序列化丢失");
        if (w2.LastObjectiveRefreshDay != 6)
            return (false, $"LastObjectiveRefreshDay 错: {w2.LastObjectiveRefreshDay}");
        if (engine2.Influence.Get("a") != 75)
            return (false, $"Influence 序列化失败: {engine2.Influence.Get("a")}");
        return (true, "");
    }

    // ============================================================================
    // KingdomDecisionService — 缺失路径补全
    // ============================================================================

    private static (bool, string) Decision_DeclareWar_InvalidNation_EmptyString()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 200, "init");

        // 空字符串
        var r1 = KingdomDecisionService.TryDeclareWar("", "b", engine);
        if (r1 != DecisionResult.InvalidNation) return (false, $"空 myNation 应返回 InvalidNation,得 {r1}");

        // null (作为空)
        var r2 = KingdomDecisionService.TryDeclareWar("a", "", engine);
        if (r2 != DecisionResult.InvalidNation) return (false, $"空 targetNation 应返回 InvalidNation,得 {r2}");

        // 不应扣影响力
        if (engine.Influence.Get("a") != 200) return (false, "InvalidNation 不应扣影响力");

        return (true, "");
    }

    private static (bool, string) Decision_DeclareWar_InvalidNation_SameNation()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 200, "init");

        var result = KingdomDecisionService.TryDeclareWar("a", "a", engine);
        if (result != DecisionResult.InvalidNation)
            return (false, $"自己向自己宣战应返回 InvalidNation,得 {result}");
        if (engine.Influence.Get("a") != 200)
            return (false, "InvalidNation 不应扣影响力");

        return (true, "");
    }

    private static (bool, string) Decision_MakePeace_InvalidNation_Empty()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 200, "init");

        var r1 = KingdomDecisionService.TryMakePeace("", "b", engine);
        if (r1 != DecisionResult.InvalidNation) return (false, $"空 myNation 应返回 InvalidNation,得 {r1}");

        var r2 = KingdomDecisionService.TryMakePeace("a", "", engine);
        if (r2 != DecisionResult.InvalidNation) return (false, $"空 targetNation 应返回 InvalidNation,得 {r2}");

        if (engine.Influence.Get("a") != 200) return (false, "InvalidNation 不应扣影响力");

        return (true, "");
    }

    private static (bool, string) Decision_MakePeace_FailsWhenInfluenceLow()
    {
        var engine = new WorldEventEngine();
        engine.Influence.Add("a", 30, "init"); // 不足 80
        engine.ActiveWars.Add(new WarState { NationA = "a", NationB = "b", DaysSinceStart = 10 });

        var result = KingdomDecisionService.TryMakePeace("a", "b", engine);
        if (result != DecisionResult.InsufficientInfluence)
            return (false, $"应返回 InsufficientInfluence,得 {result}");
        if (engine.AreAtWar("a", "b") == false)
            return (false, "失败时不应移除战争");
        if (engine.Influence.Get("a") != 30)
            return (false, "失败时不应扣影响力");

        return (true, "");
    }

    // ============================================================================
    // WorldEventEngine — 外交工具 & 联盟互斥 & 新闻系统
    // ============================================================================

    private static (bool, string) Engine_AreAtWar_ReverseOrder()
    {
        var engine = new WorldEventEngine();
        engine.ActiveWars.Add(new WarState { NationA = "a", NationB = "b" });

        // 正序和反序都应识别
        if (!engine.AreAtWar("a", "b")) return (false, "正序应识别战争");
        if (!engine.AreAtWar("b", "a")) return (false, "反序也应识别战争");
        if (engine.AreAtWar("a", "c"))  return (false, "无关国家不应识别");

        return (true, "");
    }

    private static (bool, string) Engine_AreAllied_ReturnsTrue()
    {
        var engine = new WorldEventEngine();
        engine.ActiveAlliances.Add(new AllianceState { NationA = "a", NationB = "b" });

        if (!engine.AreAllied("a", "b")) return (false, "正序应识别联盟");
        if (!engine.AreAllied("b", "a")) return (false, "反序也应识别联盟");
        if (engine.AreAllied("a", "c"))  return (false, "无关国家不应识别");

        return (true, "");
    }

    private static (bool, string) Engine_AdjustRelation_ClampsToBounds()
    {
        var engine = new WorldEventEngine();
        engine.SetRelation("a", "b", 50);

        // 向上超 100
        engine.AdjustRelation("a", "b", 200);
        if (engine.GetRelation("a", "b") != 100)
            return (false, $"上调应 clamp 到 100,得 {engine.GetRelation("a", "b")}");

        // 向下超 -100
        engine.AdjustRelation("a", "b", -300);
        if (engine.GetRelation("a", "b") != -100)
            return (false, $"下调应 clamp 到 -100,得 {engine.GetRelation("a", "b")}");

        return (true, "");
    }

    private static (bool, string) Engine_NewsQueue_CapsAt50()
    {
        var engine = new WorldEventEngine();
        for (int i = 0; i < 60; i++)
            engine.AddNews("test", $"新闻 {i}", Vector2.Zero);

        if (engine.NewsQueue.Count != 50)
            return (false, $"新闻队列应 ≤ 50,得 {engine.NewsQueue.Count}");

        // 最旧的消息应被移除，第一条应是 "新闻 10"
        if (engine.NewsQueue[0].Description != "新闻 10")
            return (false, $"最旧的新闻应被截断,首条应为 '新闻 10',得 '{engine.NewsQueue[0].Description}'");

        return (true, "");
    }

    private static (bool, string) Engine_GetVisibleNews_DistanceDelay()
    {
        var engine = new WorldEventEngine();
        engine.CurrentDay = 10;

        // 全局事件 (Location = Zero): 无延迟
        engine.AddNews("global", "全局新闻", Vector2.Zero);

        // 远处事件 (8000px): 延迟 = 8000/2000 = 4 天,Day=8,需要 CurrentDay >= 8+4=12 才可见
        engine.AddNews("local", "远处新闻", new Vector2(8000, 0));
        engine.NewsQueue[engine.NewsQueue.Count - 1].Day = 8;

        // 近处事件 (200px): 延迟 = 0 天,立即可见
        engine.AddNews("near", "近处新闻", new Vector2(200, 0));

        var visible = engine.GetVisibleNews(new Vector2(0, 0), maxCount: 10);

        // 全局 + 近处应可见, 远处因延迟不可见 (day 10 - day 8 = 2 < 延迟 4)
        if (visible.Count != 2)
            return (false, $"应可见 2 条 (全局+近处),得 {visible.Count}");
        if (visible.Any(n => n.Type == "local"))
            return (false, "远处新闻因延迟不应可见");

        return (true, "");
    }

    private static (bool, string) Engine_OnLairCleared_ReducesThreat()
    {
        var engine = new WorldEventEngine();
        engine.ThreatLevel = 0.5f;

        engine.OnLairCleared();
        if (Math.Abs(engine.ThreatLevel - 0.47f) > 0.001f)
            return (false, $"清除巢穴后威胁应 -0.03,得 {engine.ThreatLevel}");
        if (engine.DaysSinceLastLairCleared != 0)
            return (false, "DaysSinceLastLairCleared 应重置为 0");

        // 下限 0
        engine.ThreatLevel = 0.01f;
        engine.OnLairCleared();
        if (engine.ThreatLevel < 0.0f)
            return (false, $"威胁不应低于 0,得 {engine.ThreatLevel}");

        return (true, "");
    }

    private static (bool, string) Engine_OnMonsterDefeatedByNation_ReducesThreat()
    {
        var engine = new WorldEventEngine();
        engine.ThreatLevel = 0.5f;

        engine.OnMonsterDefeatedByNation();
        if (Math.Abs(engine.ThreatLevel - 0.49f) > 0.001f)
            return (false, $"击败怪物后威胁应 -0.01,得 {engine.ThreatLevel}");

        return (true, "");
    }

    private static (bool, string) Engine_SerializeAlliances_Roundtrip()
    {
        var engine = new WorldEventEngine();
        engine.ActiveAlliances.Add(new AllianceState { NationA = "x", NationB = "y" });
        engine.SetRelation("x", "y", 60);

        var data = engine.Serialize();
        var engine2 = new WorldEventEngine();
        engine2.Deserialize(data);

        if (!engine2.AreAllied("x", "y"))
            return (false, "联盟序列化/反序列化失败");
        if (engine2.GetRelation("x", "y") != 60)
            return (false, $"外交关系序列化失败,得 {engine2.GetRelation("x", "y")}");

        return (true, "");
    }

    private static (bool, string) Engine_DeserializeNull_IsNoop()
    {
        var engine = new WorldEventEngine();
        engine.ThreatLevel = 0.42f;

        // Deserialize(null) 不应抛异常也不改变状态
        engine.Deserialize(null!);
        if (Math.Abs(engine.ThreatLevel - 0.42f) > 0.001f)
            return (false, "Deserialize(null) 不应改变状态");

        return (true, "");
    }

    // ============================================================================
    // BattleResolver — AreHostile 战争状态 + subparty 大捷新闻
    // ============================================================================

    private static (bool, string) BattleResolver_AreHostile_ViaWarState()
    {
        // 两国在 ActiveWars 中,但 faction 都不是 "hostile",应通过 AreAtWar 识别为敌对
        var engine = new WorldEventEngine();
        engine.ActiveWars.Add(new WarState { NationA = "nation_a", NationB = "nation_b" });

        var lordA = MakeLord("aLord", "nation_a", new Vector2(0, 0));
        lordA.CombatPower = 9999;
        lordA.PartySize = 30;
        var lordB = MakeLord("bLord", "nation_b", new Vector2(50, 0));
        lordB.CombatPower = 0.5f;
        lordB.PartySize = 1;

        var entities = new List<OverworldEntity> { lordA, lordB };
        var resolver = new BladeHex.Strategic.BattleResolver();
        resolver.ProcessEntityInteractions(entities, engine);
        resolver.UpdateEngagements(entities, 30.0f, engine);

        // 如果 AreHostile 正确识别战争状态,lordA 应击败 lordB (CP 9999 vs 0.5)
        if (lordB.IsAlive && lordB.CurrentAIState != OverworldEntity.AIState.Fleeing)
            return (false, $"战争状态下两国领主应自动交战,lordB.IsAlive={lordB.IsAlive} state={lordB.CurrentAIState}");

        return (true, "");
    }

    private static (bool, string) BattleResolver_SubpartyVictory_EmitsNews()
    {
        var engine = new WorldEventEngine();

        // 玩家阵营的 NPC 部下击败 hostile 敌人
        var playerSub = MakeLord("部下A", "player", new Vector2(100, 0));
        playerSub.CombatPower = 9999;
        playerSub.PartySize = 30;
        playerSub.HeroId = "companion_1"; // 不是 "player"
        var enemy = MakeLord("匪徒", "hostile", new Vector2(150, 0));
        enemy.CombatPower = 0.5f;
        enemy.PartySize = 1;

        var entities = new List<OverworldEntity> { playerSub, enemy };
        var resolver = new BladeHex.Strategic.BattleResolver();
        resolver.ProcessEntityInteractions(entities, engine);
        resolver.UpdateEngagements(entities, 30.0f, engine);

        if (!engine.NewsQueue.Any(n => n.Type == "subparty_victory"))
            return (false, "玩家部下图剿匪应推送 subparty_victory 新闻");

        return (true, "");
    }

    // ============================================================================
    // WarBattleJoinService — 边界补全
    // ============================================================================

    private static (bool, string) JoinService_NullInputs_ReturnsNull()
    {
        // entities=null
        var r1 = WarBattleJoinService.Query(Vector2.Zero, null!, new List<OverworldPOI>());
        if (r1 != null) return (false, "entities=null 应返回 null");

        // pois=null
        var r2 = WarBattleJoinService.Query(Vector2.Zero, new List<OverworldEntity>(), null!);
        if (r2 != null) return (false, "pois=null 应返回 null");

        return (true, "");
    }

    private static (bool, string) JoinService_NeutralFaction_Skipped()
    {
        // neutral 势力的领主不应触发 FieldBattle
        var neutralA = MakeLord("n1", "neutral", new Vector2(100, 100));
        var lordB = MakeLord("b1", "nation_b", new Vector2(180, 100));

        var ents = new List<OverworldEntity> { neutralA, lordB };
        var result = WarBattleJoinService.Query(new Vector2(140, 100), ents, new List<OverworldPOI>());
        if (result != null)
            return (false, $"neutral 势力不应触发 FieldBattle,得 {result?.Type}");

        return (true, "");
    }

    private static (bool, string) JoinService_ArmyJoin_HigherPriority_ThanSiege()
    {
        // 同时存在 ArmyJoin 和 Siege 机会,应优先返回 ArmyJoin
        var registry = new BladeHex.Strategic.Army.ArmyRegistry();
        var marshal = MakeLord("M", "player_faction", new Vector2(100, 100));
        var army = registry.Create(marshal, "poi", 1)!;
        army.State = BladeHex.Strategic.Army.ArmyState.Forming;

        var siegePoi = MakePoi("siegePoi", "nation_a", new Vector2(150, 100));
        var attacker = MakeLord("attk", "nation_b", new Vector2(160, 100));
        siegePoi.BeginSiege(attacker);

        var result = WarBattleJoinService.Query(
            new Vector2(120, 100), // 距元帅 20px,距 siegePoi ~30px
            new List<OverworldEntity> { marshal, attacker },
            new List<OverworldPOI> { siegePoi },
            "player_faction",
            registry);

        if (result == null) return (false, "应返回 opportunity");
        if (result.Type != WarBattleType.ArmyJoin)
            return (false, $"ArmyJoin 应优先于 Siege,得 {result.Type}");

        return (true, "");
    }

    // ============================================================================
    // WarObjectivePlanner — 守方亡国 + 首次刷新
    // ============================================================================

    private static (bool, string) Planner_DefenderHasNoPois_ReturnsEmpty()
    {
        // 守方 POI 全被夺,不应选出攻势目标
        var pois = new List<OverworldPOI>
        {
            MakePoi("a1", "nation_a", new Vector2(0, 0)),
            MakePoi("a2", "nation_a", new Vector2(100, 0)),
            // nation_b 无任何 POI
        };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);

        if (war.ObjectivesA.Count != 0)
            return (false, $"守方亡国时 ObjectivesA 应为空,得 {war.ObjectivesA.Count}");

        return (true, "");
    }

    private static (bool, string) Planner_FirstRefresh_OnDay1_WithEmptyObjectives()
    {
        // LastObjectiveRefreshDay=0 且 ObjectivesA/B 全空 → 即使 Day=1 (1-0<5) 也应触发首次刷新
        var pois = new List<OverworldPOI>
        {
            MakePoi("a1", "nation_a", new Vector2(0, 0)),
            MakePoi("b1", "nation_b", new Vector2(500, 0)),
        };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        // 默认 LastObjectiveRefreshDay=0, ObjectivesA/B 为空
        var ctx = new WorldTickContext { CurrentDay = 1, Pois = pois };

        WarObjectivePlanner.RefreshObjectives(war, ctx);

        if (war.ObjectivesA.Count == 0)
            return (false, "首次刷新应选出目标");
        if (war.LastObjectiveRefreshDay != 1)
            return (false, $"首次刷新后 LastObjectiveRefreshDay 应为 1,得 {war.LastObjectiveRefreshDay}");

        return (true, "");
    }

    // ============================================================================
    // WarLordOrders — 空目标 + 失效目标重新分配
    // ============================================================================

    private static (bool, string) LordOrders_EmptyObjectives_ClearsAssignment()
    {
        var lord = MakeLord("L", "nation_a", new Vector2(0, 0));
        lord.AssignedWarTargetPoiName = "old_target";
        lord.WarTargetAssignedDay = 1;

        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        WarLordOrders.AssignLordToObjective(lord, war, new List<string>(), new List<OverworldEntity> { lord }, 5, new List<OverworldPOI>());

        if (!string.IsNullOrEmpty(lord.AssignedWarTargetPoiName))
            return (false, $"空 objectives 应清空分配,得 '{lord.AssignedWarTargetPoiName}'");

        return (true, "");
    }

    private static (bool, string) LordOrders_ExpiredTarget_ReassignsAfterLockout()
    {
        var pois = new List<OverworldPOI>
        {
            MakePoi("a_cap", "nation_a", new Vector2(0, 0)),
            MakePoi("b_old", "nation_b", new Vector2(400, 0)),
            MakePoi("b_new", "nation_b", new Vector2(300, 0)),
        };
        var lord = MakeLord("L", "nation_a", new Vector2(0, 0));
        var allLords = new List<OverworldEntity> { lord };
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };

        // Day 1: 分配 b_old
        WarLordOrders.AssignLordToObjective(lord, war, new List<string> { "b_old" }, allLords, 1, pois);
        if (lord.AssignedWarTargetPoiName != "b_old") return (false, "首次应分配 b_old");

        // Day 3: 锁定期内,目标仍在列表中,不应换
        WarLordOrders.AssignLordToObjective(lord, war, new List<string> { "b_old", "b_new" }, allLords, 3, pois);
        if (lord.AssignedWarTargetPoiName != "b_old")
            return (false, "锁定期内不应换目标");

        // Day 7: 锁定期过后 (7-1=6 >= 5),b_old 从目标列表中移除,应重新分配 b_new
        WarLordOrders.AssignLordToObjective(lord, war, new List<string> { "b_new" }, allLords, 7, pois);
        if (lord.AssignedWarTargetPoiName != "b_new")
            return (false, $"锁定期后且旧目标失效,应重新分配 b_new,得 '{lord.AssignedWarTargetPoiName}'");
        if (lord.WarTargetAssignedDay != 7)
            return (false, $"重新分配后 WarTargetAssignedDay 应更新为 7,得 {lord.WarTargetAssignedDay}");

        return (true, "");
    }
}
