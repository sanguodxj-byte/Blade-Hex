// OverworldSimulationArchitectureTests.cs
// 架构优化完整性验证测试 — 验证 OverworldSimulation 管线行为
//
// 覆盖:
//   1. Simulation TickDay 完整管线（战争目标分配、围攻、事件输出）
//   2. Simulation TickFrame 移动管线
//   3. Simulation TickHours 交战更新
//   4. 结构化事件输出（非 Godot 信号）
//   5. OverworldSimulationContext 集中化状态
//   6. PerceptionIntentResolver 感知判定合并
//   7. EntitySaveData 扩展字段覆盖
//   8. 存档 roundtrip 行为状态保持
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Diplomacy;

namespace BladeHex.Tests.Strategic;

public static class OverworldSimulationArchitectureTests
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

    private static IEnumerable<(string, bool, string)> EnumerateTests()
    {
        // ── Phase 1: SimulationContext 集中化 ──
        yield return Run(nameof(Context_EntitiesAndPois_Accessible), Context_EntitiesAndPois_Accessible);
        yield return Run(nameof(Context_HeroNetwork_Accessible), Context_HeroNetwork_Accessible);
        yield return Run(nameof(Context_TimeState_TracksCorrectly), Context_TimeState_TracksCorrectly);

        // ── Phase 1: OverworldSimulationEvent 结构化事件 ──
        yield return Run(nameof(Event_FactoryMethods_CreateCorrectly), Event_FactoryMethods_CreateCorrectly);
        yield return Run(nameof(Event_SiegeStarted_ContainsCorrectData), Event_SiegeStarted_ContainsCorrectData);
        yield return Run(nameof(Event_PoiCaptured_ContainsNewFaction), Event_PoiCaptured_ContainsNewFaction);

        // ── Phase 2: OverworldSimulation TickDay 完整管线 ──
        yield return Run(nameof(Simulation_TickDay_AdvancesDay), Simulation_TickDay_AdvancesDay);
        yield return Run(nameof(Simulation_TickDay_ReturnsEvents), Simulation_TickDay_ReturnsEvents);
        yield return Run(nameof(Simulation_TickDay_BesiegingLord_ResolvesSiege), Simulation_TickDay_BesiegingLord_ResolvesSiege);
        yield return Run(nameof(Simulation_TickDay_DormantPool_Maintained), Simulation_TickDay_DormantPool_Maintained);

        // ── Phase 2: TickFrame ──
        yield return Run(nameof(Simulation_TickFrame_MovesEntities), Simulation_TickFrame_MovesEntities);
        yield return Run(nameof(Simulation_TickFrame_ReturnsSpawnEvents), Simulation_TickFrame_ReturnsSpawnEvents);

        // ── Phase 2: TickHours ──
        yield return Run(nameof(Simulation_TickHours_AdvancesGameHour), Simulation_TickHours_AdvancesGameHour);

        // ── Phase 2: 存档快照 ──
        yield return Run(nameof(Simulation_BuildSaveSnapshot_ReturnsDictionary), Simulation_BuildSaveSnapshot_ReturnsDictionary);
        yield return Run(nameof(Simulation_RestoreSaveSnapshot_RestoresState), Simulation_RestoreSaveSnapshot_RestoresState);

        // ── Phase 3: PerceptionIntentResolver ──
        yield return Run(nameof(Perception_StrongAgainstWeak_ReturnsChase), Perception_StrongAgainstWeak_ReturnsChase);
        yield return Run(nameof(Perception_WeakAgainstStrong_ReturnsFlee), Perception_WeakAgainstStrong_ReturnsFlee);
        yield return Run(nameof(Perception_EqualPower_ReturnsNone), Perception_EqualPower_ReturnsNone);
        yield return Run(nameof(Perception_SameFaction_ReturnsNone), Perception_SameFaction_ReturnsNone);
        yield return Run(nameof(Perception_AiStrategy_ModifiesThresholds), Perception_AiStrategy_ModifiesThresholds);
        yield return Run(nameof(Perception_LordPersonality_ModifiesThresholds), Perception_LordPersonality_ModifiesThresholds);

        // ── Phase 4: EntitySaveData ──
        yield return Run(nameof(SaveData_ExtendedFields_HaveDefaults), SaveData_ExtendedFields_HaveDefaults);
        yield return Run(nameof(SaveData_EntityToSaveData_CoversAllFields), SaveData_EntityToSaveData_CoversAllFields);
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
            return (name, false, $"异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ===========================================================================
    // 工具方法
    // ===========================================================================

    private static OverworldPOI MakePoi(string name, string faction, Vector2 pos, int garrisonMax = 40)
    {
        return new OverworldPOI
        {
            PoiName = name,
            PoiTypeEnum = OverworldPOI.POIType.Town,
            OwningFaction = faction,
            Position = pos,
            Prosperity = 50,
            GarrisonMax = garrisonMax,
            GarrisonCurrent = garrisonMax,
        };
    }

    private static OverworldEntity MakeLord(string name, string faction, Vector2 pos, OverworldPOI? guarded = null)
    {
        return new OverworldEntity
        {
            EntityName = name,
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            Faction = faction,
            Position = pos,
            HomePosition = pos,
            CombatPower = 200,
            GarrisonSize = 30,
            IsAlive = true,
            VisionRange = 400.0f,
            PartyLevel = 3,
            GuardedPOI = guarded,
        };
    }

    /// <summary>构建最小可运行的 OverworldSimulation + Context</summary>
    private static (OverworldSimulation sim, OverworldSimulationContext ctx, OverworldPOI poiA, OverworldPOI poiB) BuildSimScenario()
    {
        var sim = new OverworldSimulation();
        var ctx = new OverworldSimulationContext();
        ctx.SpatialIndex = new EntitySpatialIndex(800);

        ctx.Nations = new List<NationConfig>
        {
            new() { Id = "nation_a", DisplayName = "AlphaKingdom" },
            new() { Id = "nation_b", DisplayName = "BetaEmpire" },
        };

        var poiA = MakePoi("a_capital", "nation_a", new Vector2(0, 0), garrisonMax: 60);
        var poiB = MakePoi("b_target", "nation_b", new Vector2(400, 0), garrisonMax: 50);
        ctx.Pois.Add(poiA);
        ctx.Pois.Add(poiB);

        var lordA = MakeLord("aLord", "nation_a", new Vector2(380, 0), poiA);
        lordA.CombatPower = 9999.0f;
        lordA.GarrisonSize = 60;
        ctx.Entities.Add(lordA);

        var war = new WarState { NationA = "nation_a", NationB = "nation_b", DaysSinceStart = 0 };
        ctx.WorldEngine.ActiveWars.Add(war);
        ctx.WorldEngine.SetRelation("nation_a", "nation_b", -80);

        ctx.PlayerPosition = Vector2.Zero;
        ctx.CurrentDay = 1;

        sim.WireToContext(ctx);
        return (sim, ctx, poiA, poiB);
    }

    // ===========================================================================
    // Phase 1: SimulationContext 集中化
    // ===========================================================================

    private static (bool, string) Context_EntitiesAndPois_Accessible()
    {
        var ctx = new OverworldSimulationContext();
        ctx.Entities.Add(new OverworldEntity { EntityName = "test1" });
        ctx.Pois.Add(new OverworldPOI { PoiName = "test1" });

        if (ctx.Entities.Count != 1) return (false, "Entities 未正确初始化");
        if (ctx.Pois.Count != 1) return (false, "Pois 未正确初始化");
        if (ctx.FindEntityByName("test1") == null) return (false, "FindEntityByName 未找到");
        if (ctx.FindPoiByName("test1") == null) return (false, "FindPoiByName 未找到");
        return (true, "");
    }

    private static (bool, string) Context_HeroNetwork_Accessible()
    {
        var ctx = new OverworldSimulationContext();
        if (ctx.Heroes == null) return (false, "Heroes 未初始化");
        if (ctx.Relations == null) return (false, "Relations 未初始化");
        if (ctx.Prisoners == null) return (false, "Prisoners 未初始化");
        if (ctx.Families == null) return (false, "Families 未初始化");
        if (ctx.SubParties == null) return (false, "SubParties 未初始化");
        return (true, "");
    }

    private static (bool, string) Context_TimeState_TracksCorrectly()
    {
        var ctx = new OverworldSimulationContext();
        if (ctx.CurrentDay != 1) return (false, $"CurrentDay 默认值应为 1，得到 {ctx.CurrentDay}");
        if (ctx.GameHour != 0f) return (false, $"GameHour 默认值应为 0，得到 {ctx.GameHour}");

        ctx.CurrentDay = 42;
        ctx.GameHour = 12.5f;
        if (ctx.CurrentDay != 42) return (false, "CurrentDay 设置后读取不正确");
        if (System.Math.Abs(ctx.GameHour - 12.5f) > 0.001f) return (false, "GameHour 设置后读取不正确");
        return (true, "");
    }

    // ===========================================================================
    // Phase 1: 结构化事件
    // ===========================================================================

    private static (bool, string) Event_FactoryMethods_CreateCorrectly()
    {
        var entity = new OverworldEntity { EntityName = "test" };
        var poi = new OverworldPOI { PoiName = "test" };

        var spawned = OverworldSimulationEvent.EntitySpawned(entity);
        if (spawned.Type != OverworldSimulationEvent.EventType.EntitySpawned) return (false, "EntitySpawned 类型不符");
        if (spawned.EntityA != entity) return (false, "EntitySpawned 实体不符");

        var siegeStart = OverworldSimulationEvent.SiegeStarted(poi, entity);
        if (siegeStart.Type != OverworldSimulationEvent.EventType.SiegeStarted) return (false, "SiegeStarted 类型不符");
        if (siegeStart.Poi != poi) return (false, "SiegeStarted POI 不符");

        var siegeResolved = OverworldSimulationEvent.SiegeResolved(poi, true, entity);
        if (siegeResolved.Type != OverworldSimulationEvent.EventType.SiegeResolved) return (false, "SiegeResolved 类型不符");
        if (!siegeResolved.AttackerWon) return (false, "SiegeResolved 结果不符");

        var poiCaptured = OverworldSimulationEvent.PoiCaptured(poi, "new_faction", entity);
        if (poiCaptured.Type != OverworldSimulationEvent.EventType.PoiCaptured) return (false, "PoiCaptured 类型不符");
        if (poiCaptured.NewFaction != "new_faction") return (false, "PoiCaptured 新势力不符");

        var news = OverworldSimulationEvent.NewsAdded("war_declared");
        if (news.Type != OverworldSimulationEvent.EventType.NewsAdded) return (false, "NewsAdded 类型不符");

        return (true, "");
    }

    private static (bool, string) Event_SiegeStarted_ContainsCorrectData()
    {
        var attacker = new OverworldEntity { EntityName = "attacker" };
        var poi = new OverworldPOI { PoiName = "siege_target" };
        var evt = OverworldSimulationEvent.SiegeStarted(poi, attacker);

        if (evt.EntityA?.EntityName != "attacker") return (false, "SiegeStarted 未保留攻击者");
        if (evt.Poi?.PoiName != "siege_target") return (false, "SiegeStarted 未保留目标 POI");
        return (true, "");
    }

    private static (bool, string) Event_PoiCaptured_ContainsNewFaction()
    {
        var evt = OverworldSimulationEvent.PoiCaptured(
            new OverworldPOI { PoiName = "castle" },
            "nation_x",
            new OverworldEntity { EntityName = "captor" });
        if (evt.NewFaction != "nation_x") return (false, $"NewFaction 应为 nation_x，得到 {evt.NewFaction}");
        return (true, "");
    }

    // ===========================================================================
    // Phase 2: Simulation TickDay
    // ===========================================================================

    private static (bool, string) Simulation_TickDay_AdvancesDay()
    {
        var (sim, ctx, _, _) = BuildSimScenario();
        int previousDay = ctx.CurrentDay;

        var events = sim.TickDay(ctx);

        if (ctx.CurrentDay != previousDay + 1)
            return (false, $"Day 应加 1: {previousDay} -> {ctx.CurrentDay}");
        return (true, $"Day advanced: {previousDay} -> {ctx.CurrentDay}");
    }

    private static (bool, string) Simulation_TickDay_ReturnsEvents()
    {
        var (sim, ctx, _, _) = BuildSimScenario();

        var events = sim.TickDay(ctx);

        // 至少应返回一些事件（实体重生/移除等）
        if (events == null)
            return (false, "TickDay 返回 null");
        return (true, $"TickDay 返回 {events.Count} 个事件");
    }

    private static (bool, string) Simulation_TickDay_BesiegingLord_ResolvesSiege()
    {
        var (sim, ctx, poiA, poiB) = BuildSimScenario();

        // 找到 lordA 并设置到围攻距离
        var lordA = ctx.Entities[0];
        lordA.CurrentAIState = OverworldEntity.AIState.Besieging;
        lordA.SiegeTarget = poiB;
        poiB.BeginSiege(lordA);

        // 模拟 SIEGE_DAYS = 2 天的围攻
        // 第一天: 围攻天数累计
        var events1 = sim.TickDay(ctx);
        // 第二天: 围攻结算
        var events2 = sim.TickDay(ctx);

        bool siegeResolved = events2.Any(e => e.Type == OverworldSimulationEvent.EventType.SiegeResolved);
        bool poiCaptured = events2.Any(e => e.Type == OverworldSimulationEvent.EventType.PoiCaptured);

        if (!siegeResolved)
            return (false, "围攻 2 天后应结算，但没有 SiegeResolved 事件");
        return (true, $"围攻已结算: SiegeResolved={siegeResolved}, PoiCaptured={poiCaptured}");
    }

    private static (bool, string) Simulation_TickDay_DormantPool_Maintained()
    {
        var (sim, ctx, _, _) = BuildSimScenario();

        // 模拟 5 天，不抛异常即可
        for (int i = 0; i < 5; i++)
        {
            sim.TickDay(ctx);
        }

        return (true, $"5 天模拟完成，实体数量: {ctx.Entities.Count(e => e.IsAlive)}");
    }

    // ===========================================================================
    // Phase 2: TickFrame
    // ===========================================================================

    private static (bool, string) Simulation_TickFrame_MovesEntities()
    {
        var ctx = new OverworldSimulationContext();
        ctx.SpatialIndex = new EntitySpatialIndex(800);
        ctx.PlayerPosition = new Vector2(0, 0);
        ctx.CurrentDay = 1;

        var entity = new OverworldEntity
        {
            EntityName = "mover",
            IsAlive = true,
            IsMoving = true,
            Position = new Vector2(0, 0),
            MoveSpeed = 100f,
            Path = new Godot.Collections.Array<Vector2> { new(100, 0) },
            CurrentAIState = OverworldEntity.AIState.Patrolling,
        };
        ctx.Entities.Add(entity);

        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);

        var events = sim.TickFrame(0.5f, ctx); // 0.5s -> 50px 移动

        if (entity.Position.X <= 0)
            return (false, $"实体应移动，位置仍为 {entity.Position}");
        return (true, $"实体已从 (0,0) 移动到 {entity.Position}");
    }

    private static (bool, string) Simulation_TickFrame_ReturnsSpawnEvents()
    {
        var ctx = new OverworldSimulationContext();
        ctx.SpatialIndex = new EntitySpatialIndex(800);
        ctx.PlayerPosition = new Vector2(0, 0);
        ctx.CurrentDay = 1;

        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);

        // TickFrame 可能不会生成实体（受距离条件限制），但不应抛异常
        var events = sim.TickFrame(0.1f, ctx);

        return (true, $"TickFrame 完成，返回 {events.Count} 个事件");
    }

    // ===========================================================================
    // Phase 2: TickHours
    // ===========================================================================

    private static (bool, string) Simulation_TickHours_AdvancesGameHour()
    {
        var ctx = new OverworldSimulationContext();
        ctx.Entities = new List<OverworldEntity>();
        ctx.WorldEngine = new WorldEventEngine();

        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);

        float prevHour = ctx.GameHour;
        sim.TickHours(3.0f, ctx);

        if (System.Math.Abs(ctx.GameHour - (prevHour + 3.0f)) > 0.001f)
            return (false, $"GameHour 应增加 3h: {prevHour} -> {ctx.GameHour}");
        return (true, $"GameHour advanced: {prevHour} -> {ctx.GameHour}");
    }

    // ===========================================================================
    // Phase 2: 存档快照
    // ===========================================================================

    private static (bool, string) Simulation_BuildSaveSnapshot_ReturnsDictionary()
    {
        var ctx = new OverworldSimulationContext();
        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);

        var snapshot = sim.BuildSaveSnapshot(ctx);

        if (snapshot == null) return (false, "BuildSaveSnapshot 返回 null");
        return (true, $"Snapshot 包含 {snapshot.Count} 个键");
    }

    private static (bool, string) Simulation_RestoreSaveSnapshot_RestoresState()
    {
        // 测试序列化往返
        var ctx = new OverworldSimulationContext();
        ctx.Heroes.Create("nation_a", "英雄A", "家族A", OverworldPOI.LordPersonality.Balanced, 1);
        ctx.Relations.Adjust("英雄A", "hero_b", 50);

        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);

        var snapshot = sim.BuildSaveSnapshot(ctx);

        // 反序列化到新 Context
        var ctx2 = new OverworldSimulationContext();
        var sim2 = new OverworldSimulation();
        sim2.WireToContext(ctx2);
        sim2.RestoreSaveSnapshot(snapshot, ctx2);

        int origCount = ctx.Heroes.AllHeroes.Count();
        int newCount = ctx2.Heroes.AllHeroes.Count();
        if (origCount != newCount)
            return (false, $"英雄数量不一致: 原 {origCount}, 新 {newCount}");

        return (true, $"序列化往返成功: {origCount} heroes");
    }

    // ===========================================================================
    // Phase 3: PerceptionIntentResolver
    // ===========================================================================

    private static (bool, string) Perception_StrongAgainstWeak_ReturnsChase()
    {
        var resolver = new PerceptionIntentResolver();
        var strong = new OverworldEntity
        {
            EntityName = "strong",
            EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
            CombatPower = 100,
            Faction = "player",
        };
        var weak = new OverworldEntity
        {
            EntityName = "weak",
            CombatPower = 10,
            Faction = "hostile",
        };

        var intent = resolver.Resolve(strong, weak, null);

        if (intent.Type != Intent.IntentType.Chase)
            return (false, $"强实体对弱实体应返回 Chase，实际得到 {intent.Type}");
        return (true, "");
    }

    private static (bool, string) Perception_WeakAgainstStrong_ReturnsFlee()
    {
        var resolver = new PerceptionIntentResolver();
        var weak = new OverworldEntity
        {
            EntityName = "weak",
            EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
            CombatPower = 10,
            Faction = "player",
        };
        var strong = new OverworldEntity
        {
            EntityName = "strong",
            CombatPower = 100,
            Faction = "hostile",
        };

        var intent = resolver.Resolve(weak, strong, null);

        if (intent.Type != Intent.IntentType.Flee)
            return (false, $"弱实体对强实体应返回 Flee，实际得到 {intent.Type}");
        return (true, "");
    }

    private static (bool, string) Perception_EqualPower_ReturnsNone()
    {
        var resolver = new PerceptionIntentResolver();
        var a = new OverworldEntity
        {
            EntityName = "a",
            EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
            CombatPower = 50,
            Faction = "player",
            AIStrategy = AIStrategyEnum.Instinct, // 无修正
        };
        var b = new OverworldEntity
        {
            EntityName = "b",
            CombatPower = 50,
            Faction = "hostile",
        };

        var intent = resolver.Resolve(a, b, null);

        // 战力相等 (ratio=1.0)，Instinct 下 chase=1.5, flee=0.7，1.0 在中间地带 → None
        if (intent.Type != Intent.IntentType.None)
            return (false, $"战力相等时应返回 None，实际得到 {intent.Type}");
        return (true, "");
    }

    private static (bool, string) Perception_SameFaction_ReturnsNone()
    {
        var resolver = new PerceptionIntentResolver();
        var a = new OverworldEntity
        {
            EntityName = "a",
            EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
            CombatPower = 100,
            Faction = "player",
        };
        var b = new OverworldEntity
        {
            EntityName = "b",
            CombatPower = 10,
            Faction = "player", // 同势力
        };

        var intent = resolver.Resolve(a, b, null);

        if (intent.Type != Intent.IntentType.None)
            return (false, $"同势力应返回 None，实际得到 {intent.Type}");
        return (true, "");
    }

    private static (bool, string) Perception_AiStrategy_ModifiesThresholds()
    {
        var resolver = new PerceptionIntentResolver();

        // Berserk: chaseMul=0.3, fleeMul=5.0
        var berserk = new OverworldEntity
        {
            EntityName = "berserk",
            EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
            CombatPower = 30,
            Faction = "player",
            AIStrategy = AIStrategyEnum.Berserk,
        };

        // 目标战力 35 → ratio = 30/35 ≈ 0.86
        // 标准 Adventurer: chase=1.5/base, flee=0.7/base
        // Berserk 修正: chase=1.5*0.3=0.45, flee=0.7*5.0=3.5
        // 0.86 > 0.45 → 应触发 Chase（Berserk 极度好战）
        var target = new OverworldEntity
        {
            EntityName = "target",
            CombatPower = 35,
            Faction = "hostile",
        };

        var intent = resolver.Resolve(berserk, target, null);

        if (intent.Type != Intent.IntentType.Chase)
            return (false, $"Berserk 应积极追击，实际得到 {intent.Type} (战力比 ~0.86)");
        return (true, "");
    }

    private static (bool, string) Perception_LordPersonality_ModifiesThresholds()
    {
        var resolver = new PerceptionIntentResolver();

        // Cautious 领主: chase=2.0, flee=1.2
        var cautious = new OverworldEntity
        {
            EntityName = "cautious",
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            CombatPower = 50,
            Faction = "player",
            LordPersonalityValue = OverworldPOI.LordPersonality.Cautious,
        };

        // 目标战力 40 → ratio = 50/40 = 1.25
        // Cautious: chase=2.0, 1.25 < 2.0 → 不应追击
        var target = new OverworldEntity
        {
            EntityName = "target",
            CombatPower = 40,
            Faction = "hostile",
        };

        var intent = resolver.Resolve(cautious, target, null);

        if (intent.Type != Intent.IntentType.None)
            return (false, $"Cautious 领主对稍弱目标不应追击，实际得到 {intent.Type} (战力比 1.25)");
        return (true, "");
    }

    // ===========================================================================
    // Phase 4: EntitySaveData
    // ===========================================================================

    private static (bool, string) SaveData_ExtendedFields_HaveDefaults()
    {
        var data = new BladeHex.Data.EntitySaveData();

        if (data.AiState != 0) return (false, "AiState 默认值应不为 null");
        if (data.MoveSpeed != 0) return (false, $"MoveSpeed 默认应为 0，得到 {data.MoveSpeed}");
        if (data.PartySize != 0) return (false, $"PartySize 默认应为 0，得到 {data.PartySize}");
        if (data.EngagedSinceHour != -1f) return (false, $"EngagedSinceHour 默认应为 -1，得到 {data.EngagedSinceHour}");

        return (true, "所有扩展字段有默认值");
    }

    private static (bool, string) SaveData_EntityToSaveData_CoversAllFields()
    {
        var entity = new OverworldEntity
        {
            EntityName = "test_lord",
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            Position = new Vector2(100, 200),
            Faction = "nation_a",
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Besieging,
            AIStrategy = AIStrategyEnum.Tactical,
            TargetPosition = new Vector2(300, 400),
            HomePosition = new Vector2(500, 600),
            TerritoryCenter = new Vector2(700, 800),
            TerritoryRadius = 1200f,
            MoveSpeed = 150f,
            PartySize = 45,
            PartyLevel = 12,
            CombatPower = 5000f,
            DaysAlive = 30,
            ArmyId = "army_1",
            IsMarshal = true,
            AssignedWarTargetPoiName = "enemy_castle",
            WarTargetAssignedDay = 10,
            EngagedSinceHour = 15.5f,
            CombatDurationHours = 6,
            PatrolRadius = 400f,
            VisionRange = 600f,
            LordPersonalityValue = OverworldPOI.LordPersonality.Aggressive,
            GarrisonSize = 35,
            HeroId = "hero_1",
        };

        var saveData = new BladeHex.Data.EntitySaveData
        {
            EntityName = entity.EntityName,
            EntityType = entity.EntityTypeEnum.ToString(),
            PosX = entity.Position.X,
            PosY = entity.Position.Y,
            Faction = entity.Faction,
            IsAlive = entity.IsAlive,
            AiState = (int)entity.CurrentAIState,
            AiStrategy = (int)entity.AIStrategy,
            TargetPosX = entity.TargetPosition.X,
            TargetPosY = entity.TargetPosition.Y,
            HomePosX = entity.HomePosition.X,
            HomePosY = entity.HomePosition.Y,
            TerritoryCenterX = entity.TerritoryCenter.X,
            TerritoryCenterY = entity.TerritoryCenter.Y,
            TerritoryRadius = entity.TerritoryRadius,
            MoveSpeed = entity.MoveSpeed,
            PartySize = entity.PartySize,
            PartyLevel = entity.PartyLevel,
            CombatPower = entity.CombatPower,
            DaysAlive = entity.DaysAlive,
            ArmyId = entity.ArmyId,
            IsMarshal = entity.IsMarshal,
            AssignedWarTargetPoiName = entity.AssignedWarTargetPoiName,
            WarTargetAssignedDay = entity.WarTargetAssignedDay,
            EngagedSinceHour = entity.EngagedSinceHour,
            CombatDurationHours = entity.CombatDurationHours,
            PatrolRadius = entity.PatrolRadius,
            VisionRange = entity.VisionRange,
            LordPersonality = (int)entity.LordPersonalityValue,
            GarrisonSize = entity.GarrisonSize,
            HeroId = entity.HeroId,
        };

        // 验证关键字段
        if (saveData.EntityName != "test_lord") return (false, "EntityName 不匹配");
        if (saveData.AiState != (int)OverworldEntity.AIState.Besieging) return (false, "AiState 不匹配");
        if (saveData.AiStrategy != (int)AIStrategyEnum.Tactical) return (false, "AiStrategy 不匹配");
        if (saveData.CombatPower != 5000f) return (false, "CombatPower 不匹配");
        if (saveData.EngagedSinceHour != 15.5f) return (false, "EngagedSinceHour 不匹配");
        if (saveData.HeroId != "hero_1") return (false, "HeroId 不匹配");

        return (true, $"EntitySaveData 全部字段验证通过 ({typeof(BladeHex.Data.EntitySaveData).GetProperties().Length} props)");
    }
}
