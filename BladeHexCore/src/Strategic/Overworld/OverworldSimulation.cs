// OverworldSimulation.cs
// 大地图模拟引擎 — Core 层统一驱动战略模拟
//
// 设计目标:
//   - 集中管理每日/每帧/每小时 Tick 顺序和不变量
//   - 替代 OverworldEntityManager.OnDayPassed/TickMovement/TickGameHour 的隐式调度
//   - 返回结构化事件列表，不直接发射 Godot 信号
//   - 测试可直接调用 simulation.TickDay() 验证完整模拟管线
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.Diplomacy;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Kingdom;
using BladeHex.Strategic.SubParty;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图模拟引擎 — 统一驱动每日 Tick、每帧 Tick、交战小时 Tick。
/// 所有结果通过结构化事件列表返回，不直接发射 Godot 信号。
/// </summary>
public sealed class OverworldSimulation
{
    // ========================================
    // 处理器（Core 层现有组件）
    // ========================================

    private readonly DailyDecisionProcessor _dailyProcessor = new();
    private readonly MovementProcessor _movementProcessor = new();
    private readonly BattleResolver _battleResolver = new();
    private readonly OverworldEncounterDirector _encounterDirector = new();

    /// <summary>BattleResolver 只读访问 — 供 View 层通过 BattlefieldRegistry 查询多方战场</summary>
    public BattleResolver BattleResolver => _battleResolver;
    private readonly SiegeProcessor _siegeProcessor = new();
    private static readonly Random _respawnRng = new();

    /// <summary>子队伍战败后归队事件 — 由 Frontend Adapter 订阅以合并回 PartyRoster</summary>
    public event Action<SubParty.SubParty>? SubPartyRejoined;

    /// <summary>上次实体位置快照（用于帧级移动后增量更新空间索引）</summary>
    private Dictionary<OverworldEntity, Vector2> _lastFramePositions = new();

    /// <summary>待分发的战斗结算事件队列（由 BattleResolver.CombatResolved 填充，TickHours 消费）</summary>
    private readonly List<OverworldSimulationEvent> _pendingCombatEvents = new();

    /// <summary>是否已完成 wiring（防止重复订阅 CombatResolved）</summary>
    private bool _wired = false;

    // ========================================
    // 初始化：将 Simulation 与 Context 中各组件关联
    // ========================================

    /// <summary>
    /// 将 OverworldSimulation 绑定到 Context 中的导航、军队注册表等引用。
    /// 应在 Context 字段设置后、首次 Tick 前调用。
    /// </summary>
    public void WireToContext(OverworldSimulationContext ctx)
    {
        // ── 幂等守卫：WireToContext 可能在 _EnterTree / SetHexNavigation / SetChunkNavigation /
        //    SetZoneOfControl 多次调用，但事件订阅只能绑定一次。
        bool alreadyWired = _wired;
        _wired = true;

        _dailyProcessor.SetWorldEventEngine(ctx.WorldEngine);
        _dailyProcessor.SetArmyRegistry(ctx.Armies);
        _dailyProcessor.SetHeroRelationMatrix(ctx.Relations);
        _siegeProcessor.SetArmyRegistry(ctx.Armies);
        _battleResolver.SetArmyRegistry(ctx.Armies);
        ctx.EncounterSpawner.PlayerFaction = OverworldHostility.NormalizePlayerFaction(ctx.PlayerFaction);
        ctx.EncounterSpawner.WorldEngineRef = ctx.WorldEngine;

        if (ctx.HexGrid != null && ctx.HexAStar != null)
        {
            _dailyProcessor.SetNavigation(ctx.HexGrid, ctx.HexAStar);
            _siegeProcessor.SetNavigation(ctx.HexGrid, ctx.HexAStar);
        }

        if (ctx.ChunkManager != null && ctx.ChunkAStar != null)
        {
            _dailyProcessor.SetChunkNavigation(ctx.ChunkManager, ctx.ChunkAStar);
            _siegeProcessor.SetChunkNavigation(ctx.ChunkManager, ctx.ChunkAStar);
            _encounterSpawnerCtx = ctx.EncounterSpawner;
            ctx.TerrainQuery = OverworldTerrainQuery.ForActiveChunks(ctx.ChunkManager);
            ctx.EncounterSpawner.ChunkManagerRef = ctx.ChunkManager;
            ctx.EncounterSpawner.TerrainQueryRef = ctx.TerrainQuery;
            ctx.EncounterSpawner.EnableAmbientSpawns = false;
            _movementProcessor.ChunkManagerRef = ctx.ChunkManager;
            _movementProcessor.TerrainQueryRef = ctx.TerrainQuery;
        }

        if (ctx.ZocManager != null)
        {
            _movementProcessor.ZocManagerRef = ctx.ZocManager;
        }

        _movementProcessor.WeatherSpeedFactor = ctx.WeatherSpeedFactor;

        // 休眠实体交战一次性结算回调
        ctx.EncounterSpawner.DormantPool.DormantEngagementResolver = entity =>
            ResolveDormantEngagement(entity, ctx);

        // 战斗结算事件收集 — 仅在首次 wiring 时订阅
        if (!alreadyWired)
        {
            _battleResolver.CombatResolved += (attacker, defender, attackerWon) =>
            {
                _pendingCombatEvents.Add(OverworldSimulationEvent.AiBattleOccurred(attacker, defender, attackerWon));
            };
        }
    }

    // 缓存 encounterSpawner 引用以便 WireToContext 中使用
    private EncounterEntitySpawner? _encounterSpawnerCtx;

    // ========================================
    // 每日 Tick — 封装 OnDayPassed 完整顺序
    // ========================================

    /// <summary>推进一天，返回本日产生的事件列表</summary>
    public List<OverworldSimulationEvent> TickDay(OverworldSimulationContext ctx)
    {
        var events = new List<OverworldSimulationEvent>();

        ctx.CurrentDay++;
        SyncNavigationContext(ctx);

        // ── 英雄网络每日 Tick 与重生处理 ──
        var respawns = HeroTickProcessor.Tick(
            ctx.Heroes,
            ctx.Prisoners,
            ctx.Relations,
            ctx.CurrentDay,
            ctx.Families,
            ctx.Nations,
            ctx.Pois,
            new SpecialCharacterGenerator.GenerationConfig(),
            ctx.WorldEngine);

        // M6: NPC Workshop 每日产出 → MarketStock
        NpcWorkshopBootstrap.ProcessNpcWorkshops(ctx.NpcFiefs, ctx.Pois, ctx.CurrentDay);

        foreach (var hero in respawns)
        {
            hero.State = CapturedState.Free;
            hero.CapturedDay = 0;

            var boundPoi = ctx.FindPoiByName(hero.BoundPoiName);
            Vector2 spawnPos = boundPoi != null ? boundPoi.Position : ctx.PlayerPosition + new Vector2(200, 200);

            var newEntity = new OverworldEntity
            {
                EntityName = hero.DisplayName,
                EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
                Position = spawnPos + new Vector2(_respawnRng.Next(-50, 50), _respawnRng.Next(-50, 50)),
                HomePosition = spawnPos,
                TerritoryCenter = spawnPos,
                TerritoryRadius = 1200f,
                MoveSpeed = 100f,
                PartySize = 40,
                PartyLevel = 30,
                CombatPower = 40 * 30 * 1.5f,
                Faction = hero.FactionId,
                IsHostileToPlayer = hero.FactionId == "hostile",
                VisionRange = 800f,
                PatrolRadius = 600f,
                CurrentAIState = OverworldEntity.AIState.Patrolling,
                IsAlive = true,
                LordPersonalityValue = hero.Personality,
                GarrisonSize = 40,
                GuardedPOI = boundPoi,
                IsNamedCharacter = true,
                FamilyName = hero.FamilyName,
                BoundPoiName = hero.BoundPoiName,
                HeroId = hero.HeroId,
            };

            ctx.Entities.Add(newEntity);
            ctx.SpatialIndex?.Add(newEntity);
            hero.CurrentEntityName = newEntity.EntityName;
            events.Add(OverworldSimulationEvent.EntitySpawned(newEntity));
        }

        // ── 更新实体 LOD 状态 ──
        EntityLodController.Update(ctx.Entities, ctx.PlayerPosition);

        // ── 确保 WorldEventEngine 引用就绪 ──
        _dailyProcessor.SetWorldEventEngine(ctx.WorldEngine);

        // ── 推进世界事件引擎每日 Tick ──
        var tickCtx = new WorldTickContext
        {
            CurrentDay = ctx.CurrentDay,
            Season = 0,
            Nations = ctx.Nations,
            Pois = ctx.Pois,
            PlayerPosition = ctx.PlayerPosition,
        };
        ctx.WorldEngine.Tick(tickCtx);
        ctx.EconomyEvents.Tick(tickCtx, ctx.WorldEngine);

        // ── 周末/月末外交检查 ──
        if (ctx.CurrentDay % 7 == 0)
        {
            WarSystemMVP.OnWeekEnd(ctx.CurrentDay, ctx.WorldEngine, ctx.Nations, ctx.WorldEngine.FactionRelations);
        }
        if (ctx.CurrentDay % 30 == 0)
        {
            WarSystemMVP.OnMonthEnd(ctx.CurrentDay, ctx.WorldEngine, ctx.WorldEngine.FactionRelations, ctx.Relations, ctx.Entities);
        }

        // ── 军团系统 ──
        MarshalSelector.SelectMarshalsForWars(ctx.WorldEngine, ctx.Entities, ctx.Pois, ctx.Armies, ctx.CurrentDay);
        ArmyTickProcessor.Tick(ctx.Armies, ctx.Entities, ctx.Pois, ctx.CurrentDay, ctx.WorldEngine, ctx.Relations);

        // ── 子队伍 AI（含归队处理） ──
        var rejoiners = SubPartyTickProcessor.Tick(ctx.SubParties, ctx.Entities, ctx.Pois, ctx.PlayerPosition, ctx.CurrentDay);
        foreach (var sp in rejoiners)
        {
            SubPartyRejoined?.Invoke(sp);

            if (sp.OverworldEntityRef != null)
            {
                sp.OverworldEntityRef.IsAlive = false;
                ctx.SpatialIndex?.Remove(sp.OverworldEntityRef, sp.OverworldEntityRef.Position);
                ctx.Entities.Remove(sp.OverworldEntityRef);
                events.Add(OverworldSimulationEvent.EntityRemoved(sp.OverworldEntityRef));
            }
            ctx.SubParties.Remove(sp.SubPartyId);
        }

        // ── POI 每日更新 ──
        foreach (var poi in ctx.Pois)
            poi.OnDayPassed();

        // ── 实体接触检测 → 交战 ──
        _battleResolver.SetHeroNetwork(ctx.Heroes, ctx.Prisoners, ctx.Relations);
        _battleResolver.SetPois(ctx.Pois);
        _battleResolver.ProcessEntityInteractions(ctx.Entities, ctx.WorldEngine, ctx.PlayerPosition, ctx.SpatialIndex, ctx.GameHour);

        // ── 每日决策 ──
        _dailyProcessor.ProcessDailyDecisions(ctx.Entities, ctx.Pois, ctx.CurrentDay);

        // ── 围攻结算 ──
        _siegeProcessor.SetHeroNetwork(ctx.Heroes, ctx.Prisoners, ctx.Relations);
        _siegeProcessor.SetPois(ctx.Pois);
        var siegeEvents = RunSiegeCycle(ctx);
        events.AddRange(siegeEvents);

        // ── 回援检查 ──
        _siegeProcessor.ProcessReinforcementChecks(ctx.Entities, ctx.Pois, new SiegeSignalCollector(events), ctx.SpatialIndex);

        // ── 招募结算 ──
        _siegeProcessor.ProcessRecruitment(ctx.Entities);

        // ── 清理死亡实体 ──
        CollectDeadAndRemoved(ctx, events);

        // ── 重建网格索引 ──
        ctx.SpatialIndex?.Rebuild(ctx.Entities);

        // ── 不活跃池每日维护 ──
        ctx.EncounterSpawner.DormantPool.OnDayPassed(ctx.CurrentDay);

        return events;
    }

    // ========================================
    // 每帧 Tick — 封装 TickMovement 完整顺序
    // ========================================

    /// <summary>每帧移动更新，返回本帧产生的事件列表</summary>
    public List<OverworldSimulationEvent> TickFrame(float delta, OverworldSimulationContext ctx)
    {
        var events = new List<OverworldSimulationEvent>();
        SyncNavigationContext(ctx);
        _movementProcessor.WeatherSpeedFactor = ctx.WeatherSpeedFactor;

        // 1. 遭遇实体生成器 Tick（生成 + 追击 AI）
        var newSpawned = ctx.EncounterSpawner.Tick(delta, ctx.PlayerPosition, ctx.Entities,
            ctx.PlayerLevel, ctx.CurrentDay, ctx.Pois);

        foreach (var e in newSpawned)
        {
            ctx.Entities.Add(e);
            ctx.SpatialIndex?.Add(e);
            events.Add(OverworldSimulationEvent.EntitySpawned(e));
        }

        // 2. 帧级战术感知：持续刷新追击/逃跑路径，而不是只在每日 Tick 反应
        _dailyProcessor.ProcessFrameTactics(ctx.Entities, ctx.SpatialIndex);
        BattlefieldInterventionService.ProcessAiBattlefieldResponses(
            ctx.Entities,
            _battleResolver,
            ctx.WorldEngine,
            ctx.Relations,
            ctx.GameHour,
            onFlee: _dailyProcessor.RefreshFleeMoveForExternalIntent);

        // 3. 推进所有实体移动
        _lastFramePositions.Clear();
        foreach (var e in ctx.Entities)
        {
            if (e.IsAlive)
                _lastFramePositions[e] = e.Position;
        }

        _movementProcessor.TickMovement(delta, ctx.Entities, entity => HandleEntityReachedDestination(entity, ctx, events));

        // 增量更新空间索引
        foreach (var e in ctx.Entities)
        {
            if (e.IsAlive && _lastFramePositions.TryGetValue(e, out var oldPos))
            {
                ctx.SpatialIndex?.Update(e, oldPos);
            }
        }

        // 4. 实体接触检测与远距收容
        _battleResolver.ProcessEntityInteractions(ctx.Entities, ctx.WorldEngine, ctx.PlayerPosition, ctx.SpatialIndex, ctx.GameHour);

        DeactivateDistantEntities(ctx, events);

        return events;
    }

    /// <summary>
    /// 处理刚激活的 chunks：填充遭遇槽、孵化可移动野怪实体，并返回结构化生成事件。
    /// </summary>
    public List<OverworldSimulationEvent> ProcessActivatedChunks(
        OverworldSimulationContext ctx,
        IEnumerable<ChunkData> chunks,
        EncounterSpawner encounterSpawner,
        float dangerLevel,
        int daysElapsed,
        int seed)
    {
        return ProcessActivatedChunks(ctx, chunks, encounterSpawner, _ => dangerLevel, daysElapsed, seed);
    }

    /// <summary>
    /// 处理刚激活的 chunks：按 chunk 危险度填充遭遇槽、孵化可移动野怪实体。
    /// </summary>
    public List<OverworldSimulationEvent> ProcessActivatedChunks(
        OverworldSimulationContext ctx,
        IEnumerable<ChunkData> chunks,
        EncounterSpawner encounterSpawner,
        Func<ChunkData, float> dangerResolver,
        int daysElapsed,
        int seed)
    {
        var events = new List<OverworldSimulationEvent>();
        int remainingBatchSpawns = Math.Max(0, ctx.EncounterSpawner.MaxChunkSlotSpawnsPerActivationBatch);
        int remainingTotalSpawns = Math.Max(0, ctx.EncounterSpawner.MaxChunkSlotWildMonsterEntities - CountActiveChunkSlotWildMonsters(ctx.Entities));

        foreach (var chunk in chunks)
        {
            var plan = _encounterDirector.BuildChunkPlan(
                chunk,
                dangerResolver(chunk),
                ctx,
                remainingBatchSpawns,
                remainingTotalSpawns);

            encounterSpawner.PopulateEncounterSlots(chunk, plan.DangerLevel, ctx.PlayerLevel, daysElapsed, seed);

            if (plan.MaxWildMonsterSpawns <= 0)
                continue;

            var spawned = ctx.EncounterSpawner.SpawnWildMonstersFromSlots(
                chunk,
                encounterSpawner,
                plan.DangerLevel,
                ctx.PlayerLevel,
                ctx.PlayerPosition,
                maxSpawns: plan.MaxWildMonsterSpawns);

            foreach (var entity in spawned)
            {
                ctx.Entities.Add(entity);
                ctx.SpatialIndex?.Add(entity);
                events.Add(OverworldSimulationEvent.EntitySpawned(entity));
            }

            remainingBatchSpawns -= spawned.Count;
            remainingTotalSpawns -= spawned.Count;
        }

        return events;
    }

    private static int CountActiveChunkSlotWildMonsters(IEnumerable<OverworldEntity> entities)
    {
        return entities.Count(e =>
            e.IsAlive
            && e.TempEncounterEnemies != null
            && e.TempEncounterEnemies.Length > 0);
    }

    /// <summary>上次运行感知意图判定的累计小时数</summary>
    private float _lastIntentHour = -1f;

    // ========================================
    // 小时 Tick — 封装 TickGameHour
    // ========================================

    /// <summary>推进游戏小时并更新交战状态 + 每小时感知意图判定，返回事件列表</summary>
    public List<OverworldSimulationEvent> TickHours(float deltaHours, OverworldSimulationContext ctx)
    {
        SyncNavigationContext(ctx);
        ctx.GameHour += deltaHours;
        _battleResolver.UpdateEngagements(ctx.Entities, ctx.GameHour, ctx.WorldEngine, ctx.PlayerPosition);

        // 每小时感知意图判定：扫描威胁 → 设定追/逃意图 → 立即发起移动
        if (_lastIntentHour < 0f || (int)ctx.GameHour > (int)_lastIntentHour)
        {
            _dailyProcessor.ProcessHourlyIntent(ctx.Entities, ctx.SpatialIndex);
            _lastIntentHour = ctx.GameHour;
        }

        // 消费待分发的战斗结算事件
        var events = new List<OverworldSimulationEvent>(_pendingCombatEvents);
        _pendingCombatEvents.Clear();
        return events;
    }

    // ========================================
    // 内部方法
    // ========================================

    private List<OverworldSimulationEvent> RunSiegeCycle(OverworldSimulationContext ctx)
    {
        var events = new List<OverworldSimulationEvent>();
        var collector = new SiegeSignalCollector(events);

        _siegeProcessor.ProcessSieges(ctx.Entities, collector, ctx.CurrentDay, ctx.WorldEngine, ctx.PlayerPosition);

        return events;
    }

    private void SyncNavigationContext(OverworldSimulationContext ctx)
    {
        _dailyProcessor.SetPlayerPosition(ctx.PlayerPosition);
        _siegeProcessor.SetPlayerPosition(ctx.PlayerPosition);
    }

    private void HandleEntityReachedDestination(OverworldEntity entity, OverworldSimulationContext ctx, List<OverworldSimulationEvent> events)
    {
        const float SIEGE_APPROACH_DIST = 600.0f;

        _dailyProcessor.OnEntityReachedDestination(entity);

        if (entity.CurrentAIState == OverworldEntity.AIState.Besieging && entity.SiegeTarget != null)
        {
            if (entity.Position.DistanceTo(entity.SiegeTarget.Position) < SIEGE_APPROACH_DIST)
            {
                if (!entity.SiegeTarget.IsUnderSiege)
                {
                    entity.SiegeTarget.BeginSiege(entity);
                    events.Add(OverworldSimulationEvent.SiegeStarted(entity.SiegeTarget, entity));
                }
            }
        }
    }

    /// <summary>收容距离玩家过远的实体到不活跃池</summary>
    private const float DEACTIVATION_DISTANCE = 2500.0f;

    private void DeactivateDistantEntities(OverworldSimulationContext ctx, List<OverworldSimulationEvent> events)
    {
        var toRemove = new List<OverworldEntity>();

        foreach (var entity in ctx.Entities)
        {
            if (!entity.IsAlive) continue;
            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing) continue;
            if (entity.CurrentAIState == OverworldEntity.AIState.Besieging) continue;
            if (entity.CurrentAIState == OverworldEntity.AIState.Engaged) continue;

            float dist = entity.Position.DistanceTo(ctx.PlayerPosition);
            if (dist > DEACTIVATION_DISTANCE)
            {
                ctx.EncounterSpawner.DormantPool.Store(entity, ctx.CurrentDay);
                toRemove.Add(entity);
            }
        }

        foreach (var entity in toRemove)
        {
            ctx.Entities.Remove(entity);
            ctx.SpatialIndex?.Remove(entity, entity.Position);
            events.Add(OverworldSimulationEvent.EntityRemoved(entity));
        }
    }

    private void CollectDeadAndRemoved(OverworldSimulationContext ctx, List<OverworldSimulationEvent> events)
    {
        var dead = new List<OverworldEntity>();
        foreach (var e in ctx.Entities)
        {
            if (!e.IsAlive)
                dead.Add(e);
        }
        foreach (var e in dead)
        {
            ctx.Entities.Remove(e);
            ctx.SpatialIndex?.Remove(e, e.Position);
            events.Add(OverworldSimulationEvent.EntityRemoved(e));
        }
    }

    private void ResolveDormantEngagement(OverworldEntity entity, OverworldSimulationContext ctx)
    {
        _battleResolver.ResolveDormantEngagement(entity, ctx.GameHour, ctx.WorldEngine);
    }

    // ========================================
    // 存储快照
    // ========================================

    /// <summary>构建当前模拟状态的序列化快照</summary>
    public Godot.Collections.Dictionary BuildSaveSnapshot(OverworldSimulationContext ctx)
    {
        var dict = new Godot.Collections.Dictionary
        {
            { "heroes", ctx.Heroes.Serialize() },
            { "relations", ctx.Relations.Serialize() },
            { "prisoners", ctx.Prisoners.Serialize() },
            { "subparties", ctx.SubParties.Serialize() },
            { "economy_events", ctx.EconomyEvents.Serialize() },
            { "families", ctx.Families.Serialize() },
        };

        if (ctx.PlayerKingdom != null)
            dict["player_kingdom"] = ctx.PlayerKingdom.Serialize();

        if (ctx.PendingConquests.Count > 0)
        {
            var pendingArr = new Godot.Collections.Array();
            foreach (var name in ctx.PendingConquests)
                pendingArr.Add(name);
            dict["pending_conquests"] = pendingArr;
        }

        return dict;
    }

    /// <summary>从序列化快照恢复模拟状态</summary>
    public void RestoreSaveSnapshot(Godot.Collections.Dictionary data, OverworldSimulationContext ctx)
    {
        if (data == null) return;

        if (data.ContainsKey("heroes"))
            ctx.Heroes.Deserialize(data["heroes"].AsGodotDictionary());
        if (data.ContainsKey("relations"))
            ctx.Relations.Deserialize(data["relations"].AsGodotDictionary());
        if (data.ContainsKey("prisoners"))
            ctx.Prisoners.Deserialize(data["prisoners"].AsGodotDictionary());
        if (data.ContainsKey("subparties"))
            ctx.SubParties.Deserialize(data["subparties"].AsGodotDictionary());
        if (data.ContainsKey("families"))
        {
            var familiesDict = data["families"].AsGodotDictionary();
            if (familiesDict.Count > 0)
            {
                var newRegistry = FamilyRegistry.Deserialize(familiesDict);
                foreach (var family in newRegistry.AllFamilies)
                    ctx.Families.Create(family.FamilyName, family.FactionId, family.PatriarchHeroId, family.MemberHeroIds, family.FoundedDay);
            }
        }
        if (data.ContainsKey("player_kingdom"))
            ctx.PlayerKingdom = PlayerKingdom.Deserialize(data["player_kingdom"].AsGodotDictionary());
        if (data.ContainsKey("pending_conquests"))
        {
            ctx.PendingConquests.Clear();
            var arr = (Godot.Collections.Array)data["pending_conquests"];
            foreach (var item in arr)
                ctx.PendingConquests.Add(item.AsString());
        }
        if (data.ContainsKey("economy_events"))
            ctx.EconomyEvents.Deserialize(data["economy_events"].AsGodotArray());
    }
}

/// <summary>
/// 围攻信号收集器 — 实现 ISiegeSignals 接口，将围攻信号转为结构化事件
/// 替代 Frontend OverworldEntityManager 的直接信号发射
/// </summary>
internal sealed class SiegeSignalCollector : ISiegeSignals
{
    private readonly List<OverworldSimulationEvent> _events;

    public SiegeSignalCollector(List<OverworldSimulationEvent> events)
    {
        _events = events;
    }

    public void OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
    {
        _events.Add(OverworldSimulationEvent.SiegeResolved(target, attackerWon, attacker));
    }

    public void OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
    {
        _events.Add(OverworldSimulationEvent.PoiCaptured(poi, newFaction, captor));
    }

    public void OnReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer)
    {
        _events.Add(OverworldSimulationEvent.ReinforcementArrived(targetPoi, reinforcer));
    }
}
