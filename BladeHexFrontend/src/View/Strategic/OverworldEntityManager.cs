using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Events;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Kingdom;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图实体管理器 — Frontend Adapter
/// 
/// 架构优化后职责：
/// - Godot Node 身份 + 信号声明
/// - 持有 OverworldSimulationContext 和 OverworldSimulation
/// - 委托模拟逻辑给 Core 层 OverworldSimulation
/// - 将 Core 事件转发为 Godot 信号
/// - 保留玩家交互查询、可视化缓存、战斗结果消费等 Frontend 专属方法
/// 
/// 核心更新方法（OnDayPassed/TickMovement/TickGameHour）委托给 Simulation。
/// </summary>
[GlobalClass]
public partial class OverworldEntityManager : Node, ISiegeSignals
{
    // ========================================
    // 信号（保留 — Frontend 消费者依赖这些信号）
    // ========================================

    [Signal] public delegate void EntityRemovedEventHandler(OverworldEntity entity);
    [Signal] public delegate void EntitySpawnedEventHandler(OverworldEntity entity);
    [Signal] public delegate void VillageAttackedEventHandler(OverworldPOI village, OverworldEntity attacker);
    [Signal] public delegate void SiegeStartedEventHandler(OverworldPOI siegeTarget, OverworldEntity attacker);
    [Signal] public delegate void SiegeResolvedEventHandler(OverworldPOI siegeTarget, bool attackerWon, OverworldEntity attacker);
    [Signal] public delegate void ReinforcementArrivedEventHandler(OverworldPOI targetPoi, OverworldEntity reinforcer);
    [Signal] public delegate void AiBattleOccurredEventHandler(OverworldEntity attacker, OverworldEntity defender, bool attackerWon);
    [Signal] public delegate void PoiCapturedEventHandler(OverworldPOI poi, string newFaction, OverworldEntity captor);

    /// <summary>当 Chunk 遭遇被触发时发射</summary>
    [Signal] public delegate void ChunkEncounterTriggeredEventHandler(Vector2I worldCoord);

    // ========================================
    // Core 模拟引擎
    // ========================================

    /// <summary>模拟上下文 — 集中保存运行状态</summary>
    public OverworldSimulationContext SimCtx { get; } = new();

    /// <summary>模拟引擎 — 驱动每日/帧/小时 Tick</summary>
    public OverworldSimulation Simulation { get; } = new();

    /// <summary>事件调度器 — 将 Core 事件转发为 Godot 信号</summary>
    private SimulationEventDispatcher? _eventDispatcher;

    // ========================================
    // 便捷访问（向后兼容）
    // ========================================

    public List<OverworldEntity> Entities => SimCtx.Entities;
    public List<OverworldPOI> Pois => SimCtx.Pois;
    public EntitySpatialIndex Spatial => SimCtx.SpatialIndex!;
    public ArmyRegistry Armies => SimCtx.Armies;
    public BladeHex.Strategic.Hero.HeroRegistry Heroes => SimCtx.Heroes;
    public BladeHex.Strategic.Hero.HeroRelationMatrix Relations => SimCtx.Relations;
    public BladeHex.Strategic.Hero.PrisonerLedger Prisoners => SimCtx.Prisoners;
    public FamilyRegistry Families => SimCtx.Families;
    public BladeHex.Strategic.SubParty.SubPartyRegistry SubParties => SimCtx.SubParties;
    public WorldEventEngine WorldEngine => SimCtx.WorldEngine;
    public EconomyEventEngine EconomyEvents => SimCtx.EconomyEvents;
    public int CurrentDay => SimCtx.CurrentDay;
    public float CurrentGameHour => SimCtx.GameHour;

    /// <summary>
    /// NPC 国家的 Fief 容器。
    /// </summary>
    public List<FiefData> NpcFiefs => SimCtx.NpcFiefs;

    /// <summary>
    /// 玩家王国(M7 引入)。null = 玩家尚未创建王国。
    /// </summary>
    public PlayerKingdom? PlayerKingdom
    {
        get => SimCtx.PlayerKingdom;
        set => SimCtx.PlayerKingdom = value;
    }

    /// <summary>
    /// 开国前的占领列表(M7 引入)。
    /// </summary>
    public List<string> PendingConquests => SimCtx.PendingConquests;

    public List<NationConfig> Nations
    {
        get => SimCtx.Nations;
        set => SimCtx.Nations = value;
    }

    public float CurrentGameHourVal => SimCtx.GameHour;

    /// <summary>
    /// SubParty 战败后归队事件。
    /// </summary>
    public event System.Action<BladeHex.Strategic.SubParty.SubParty>? SubPartyRejoined;

    /// <summary>发现日志</summary>
    public BladeHex.Strategic.Encyclopedia.DiscoveryJournal Journal { get; set; } = new();

    /// <summary>当前活跃的遭遇标记</summary>
    public List<EncounterMarkerData> ActiveEncounterMarkers { get; private set; } = new();

    // ========================================
    // 成员变量（Frontend 专属）
    // ========================================

    /// <summary>同步玩家队伍的总战力到遭遇生成器</summary>
    public float PlayerCombatPower
    {
        get => SimCtx.EncounterSpawner.PlayerCombatPower;
        set => SimCtx.EncounterSpawner.PlayerCombatPower = value;
    }

    private Vector2 _playerPosition = Vector2.Zero;

    private const float SIEGE_APPROACH_DIST = 600.0f;
    private const float ENCOUNTER_MARKER_DIST = 300.0f;

    private (Vector2 pos, float range, List<OverworldEntity> result) _lastVisibleQuery = (Vector2.Zero, 0f, new List<OverworldEntity>());

    // ========================================
    // 生命周期
    // ========================================

    public override void _EnterTree()
    {
        EventBus.Instance?.Subscribe(EventBus.Signals.DayPassed, OnDayPassedEvent);

        // 初始化 SpatialIndex（默认即可用）
        if (SimCtx.SpatialIndex == null)
            SimCtx.SpatialIndex = new EntitySpatialIndex(800);

        // WIRE: 连接 Simulation 到 Context
        Simulation.WireToContext(SimCtx);

        // 初始化事件调度器
        _eventDispatcher = new SimulationEventDispatcher(this, SimCtx.WorldEngine);

        // 订阅子队伍归队事件
        Simulation.SubPartyRejoined += sp =>
        {
            SubPartyRejoined?.Invoke(sp);
        };

        // 绑定外交求和的静态委托到事件总线
        BladeHex.Strategic.Diplomacy.WarSystemMVP.AiProposedPeaceToPlayer = (proposer, warDays) =>
        {
            var data = new Godot.Collections.Dictionary
            {
                { "proposer", proposer },
                { "target", "player" },
                { "war_days", warDays }
            };
            EventBus.Instance?.Publish("ai_propose_peace_to_player", data);
        };
    }

    public override void _ExitTree()
    {
        EventBus.Instance?.Unsubscribe(EventBus.Signals.DayPassed, OnDayPassedEvent);
        BladeHex.Strategic.Diplomacy.WarSystemMVP.AiProposedPeaceToPlayer = null;
    }

    private void OnDayPassedEvent(Godot.Collections.Dictionary _) => OnDayPassed();

    // ========================================
    // 初始化与数据加载
    // ========================================

    public void SetHexNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        SimCtx.HexGrid = grid;
        SimCtx.HexAStar = astar;
        Simulation.WireToContext(SimCtx);
    }

    /// <summary>设置 Chunk 模式寻路</summary>
    public void SetChunkNavigation(ChunkManager mgr, ChunkAStar astar)
    {
        SimCtx.ChunkManager = mgr;
        SimCtx.ChunkAStar = astar;
        SimCtx.EncounterSpawner.ChunkManagerRef = mgr;
        Simulation.WireToContext(SimCtx);
    }

    /// <summary>设置 POI 控制区管理器</summary>
    public void SetZoneOfControl(ZoneOfControlManager zoc)
    {
        SimCtx.ZocManager = zoc;
        Simulation.WireToContext(SimCtx);
    }

    public void LoadWorld(Godot.Collections.Array worldPois, Godot.Collections.Array worldEntities)
    {
        SimCtx.Pois.Clear();
        foreach (var poi in worldPois) SimCtx.Pois.Add((OverworldPOI)poi);

        SimCtx.Entities.Clear();
        foreach (var entity in worldEntities) SimCtx.Entities.Add((OverworldEntity)entity);

        if (SimCtx.SpatialIndex == null)
            SimCtx.SpatialIndex = new EntitySpatialIndex(800);
        SimCtx.SpatialIndex.Rebuild(SimCtx.Entities);

        SimCtx.Heroes.PopulateFromEntities(SimCtx.Entities, SimCtx.CurrentDay, SimCtx.Relations);

        // M6: 为 NPC 主要国家自动建造 Workshop
        if (SimCtx.NpcFiefs.Count == 0 && SimCtx.Nations.Count > 0)
        {
            BladeHex.Strategic.Economy.NpcWorkshopBootstrap.Bootstrap(SimCtx.Nations, SimCtx.Pois, SimCtx.NpcFiefs);
        }
    }

    public void UpdatePlayerPosition(Vector2 pos)
    {
        _playerPosition = pos;
        SimCtx.PlayerPosition = pos;
    }

    /// <summary>
    /// 推进游戏小时数（委托给 Simulation）
    /// </summary>
    public void TickGameHour(float deltaHours)
    {
        var events = Simulation.TickHours(deltaHours, SimCtx);
        _eventDispatcher?.Dispatch(events);
    }

    // ========================================
    // 核心更新逻辑 — 委托给 Simulation
    // ========================================

    /// <summary>每帧更新：委托给 Simulation.TickFrame()</summary>
    public void TickMovement(float delta)
    {
        var events = Simulation.TickFrame(delta, SimCtx);
        _eventDispatcher?.Dispatch(events);
        InvalidateVisibleCache();
    }

    /// <summary>收容距离玩家过远的实体到不活跃池（移至 OverworldSimulation，此处保留兼容调用）</summary>
    private const float DEACTIVATION_DISTANCE = 2500.0f;

    private void DeactivateDistantEntities()
    {
        // 已迁移至 OverworldSimulation.DeactivateDistantEntities
        // 此处保留空实现确保旧代码兼容
    }

    /// <summary>玩家等级（影响敌方等级缩放，由 OverworldScene2D 设置）</summary>
    public int PlayerLevel
    {
        get => SimCtx.PlayerLevel;
        set => SimCtx.PlayerLevel = value;
    }

    /// <summary>玩家种族（影响 Adventurer 同/异族判定）</summary>
    public int PlayerRaceId
    {
        get => SimCtx.PlayerRaceId;
        set => SimCtx.PlayerRaceId = value;
    }

    /// <summary>玩家当前战略阵营（用于外交敌对判定）。</summary>
    public string PlayerFaction
    {
        get => SimCtx.PlayerFaction;
        set
        {
            SimCtx.PlayerFaction = OverworldHostility.NormalizePlayerFaction(value);
            SimCtx.EncounterSpawner.PlayerFaction = SimCtx.PlayerFaction;
            SimCtx.EncounterSpawner.WorldEngineRef = SimCtx.WorldEngine;
        }
    }

    /// <summary>
    /// 检查玩家是否触发遭遇
    /// </summary>
    public OverworldEntity? CheckPlayerEntityEncounter()
    {
        return SimCtx.EncounterSpawner.CheckEncounter(_playerPosition, Entities);
    }

    /// <summary>每日更新：委托给 Simulation.TickDay()，然后分发事件</summary>
    public void OnDayPassed()
    {
        var events = Simulation.TickDay(SimCtx);
        _eventDispatcher?.Dispatch(events);
        InvalidateVisibleCache();
    }

    // ========================================
    // ISiegeSignals 实现 — 保留用于与 SiegeProcessor 的直接交互
    // （实际事件转发已通过 SimulationEventDispatcher 处理）
    // ========================================

    void ISiegeSignals.OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
        => EmitSignal(SignalName.SiegeResolved, target, attackerWon, attacker);

    void ISiegeSignals.OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
    {
        EmitSignal(SignalName.PoiCaptured, poi, newFaction, captor);

        // M7: 处理玩家征服
        if (newFaction == "player")
        {
            if (PlayerKingdom != null)
            {
                if (!PlayerKingdom.ControlledPoiNames.Contains(poi.PoiName))
                {
                    PlayerKingdom.ControlledPoiNames.Add(poi.PoiName);
                    GD.Print($"[PlayerKingdom] 王国新增领土: {poi.PoiName}");
                }
            }
            else
            {
                if (!PendingConquests.Contains(poi.PoiName))
                {
                    PendingConquests.Add(poi.PoiName);
                    GD.Print($"[PlayerKingdom] 新增待征服领土: {poi.PoiName}");
                }
            }
        }
    }

    void ISiegeSignals.OnReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer)
        => EmitSignal(SignalName.ReinforcementArrived, targetPoi, reinforcer);

    // ========================================
    // 玩家交互查询（Frontend 专属）
    // ========================================

    /// <summary>检测玩家附近的实体（敌方/友方/中立都触发，由交互系统按阵营生成不同选项）</summary>
    public OverworldEntity? CheckPlayerEncounters(Vector2 playerPos)
    {
        OverworldEntity? closest = null;
        float closestDist = 80.0f; // 需要几乎撞在一起才触发
        foreach (var entity in Entities)
        {
            if (!entity.IsAlive) continue;
            float d = playerPos.DistanceTo(entity.Position);
            if (d < closestDist) { closestDist = d; closest = entity; }
        }
        return closest;
    }

    /// <summary>检测玩家进入的 POI</summary>
    public OverworldPOI? CheckPlayerPoiEnter(Vector2 playerPos, bool isMoving = true)
    {
        OverworldPOI? closest = null;
        float closestDist = 60.0f;
        foreach (var poi in Pois)
        {
            float d = playerPos.DistanceTo(poi.Position);
            if (d < closestDist) { closestDist = d; closest = poi; }
        }
        return closest;
    }

    /// <summary>
    /// 获取玩家视野内的实体。
    /// 注意:为减少重复查询开销,玩家位置变化 &lt; 100px 时返回上次结果的同一引用,
    /// 调用方不应 mutate 返回 list;若有大事件 (实体生成/销毁) 发生应主动 InvalidateVisibleCache()。
    /// </summary>
    public List<OverworldEntity> GetVisibleEntities(Vector2 playerPos, float visionRange)
    {
        if (_lastVisibleQuery.range > 0f && 
            Mathf.Abs(_lastVisibleQuery.range - visionRange) < 0.001f && 
            playerPos.DistanceTo(_lastVisibleQuery.pos) < 100f)
        {
            return _lastVisibleQuery.result;
        }

        var visible = new List<OverworldEntity>(Spatial.QueryRadius(playerPos, visionRange));
        _lastVisibleQuery = (playerPos, visionRange, visible);
        return visible;
    }

    /// <summary>使可见实体缓存失效。在实体生成/销毁后调用,确保下一次 GetVisibleEntities 重新查询。</summary>
    public void InvalidateVisibleCache()
    {
        _lastVisibleQuery = (Vector2.Zero, 0f, new List<OverworldEntity>());
    }

    /// <summary>获取玩家视野内的 POI</summary>
    public List<OverworldPOI> GetVisiblePois(Vector2 playerPos, float visionRange)
    {
        var visible = new List<OverworldPOI>();
        foreach (var poi in Pois)
            if (playerPos.DistanceTo(poi.Position) <= visionRange) visible.Add(poi);
        return visible;
    }

    // ========================================
    // 到达目的地回调
    // ========================================

    private void OnEntityReachedDestination(OverworldEntity entity)
    {
        if (entity.CurrentAIState == OverworldEntity.AIState.Besieging && entity.SiegeTarget != null)
        {
            if (entity.Position.DistanceTo(entity.SiegeTarget.Position) < SIEGE_APPROACH_DIST)
            {
                if (!entity.SiegeTarget.IsUnderSiege)
                {
                    entity.SiegeTarget.BeginSiege(entity);
                    EmitSignal(SignalName.SiegeStarted, entity.SiegeTarget, entity);
                }
            }
        }
    }

    // ========================================
    // 战斗结果消费 — 战略层更新
    // ========================================

    /// <summary>
    /// 消耗战斗结果 — 更新战略层实体和POI状态
    /// 由 CombatManager 战斗结束时通过 EventBus 调用
    /// </summary>
    public void OnBattleOutcome(Godot.Collections.Dictionary outcomeData)
    {
        var outcome = BattleOutcome.Deserialize(outcomeData);
        if (outcome == null) return;

        GD.Print($"[OverworldEntityManager] 战斗结束: {outcome.BattleType}, 攻击方胜利={outcome.AttackerWon}");

        // 战后关系下调
        if (!string.IsNullOrEmpty(outcome.AttackerEntityName))
        {
            var attacker = FindEntityByName(outcome.AttackerEntityName);
            if (attacker != null)
            {
                string defenderFaction = "neutral";
                if (!string.IsNullOrEmpty(outcome.PoiName))
                {
                    var poi = FindPOIByName(outcome.PoiName);
                    if (poi != null) defenderFaction = poi.OwningFaction;
                }
                else
                {
                    var closestEnemy = Entities.FirstOrDefault(e => 
                        e != attacker && 
                        e.Faction != attacker.Faction && 
                        e.Position.DistanceTo(attacker.Position) <= 600f);
                    if (closestEnemy != null)
                    {
                        defenderFaction = closestEnemy.Faction;
                    }
                }

                if (defenderFaction != "neutral")
                {
                    BladeHex.Strategic.Diplomacy.CombatResultProcessor.ProcessCombatRelations(
                        attacker.Faction, defenderFaction, outcome.AttackerWon, WorldEngine);
                }
            }
        }

        // 更新攻击方实体
        if (!string.IsNullOrEmpty(outcome.AttackerEntityName))
        {
            var attacker = FindEntityByName(outcome.AttackerEntityName);
            // 找一个"胜者"用于关系传播/俘虏归属。outcome 不带防守方实体名,
            // 战斗失败时降级为 null(EntityCombatBridge 会将其作为普通战死处理)。
            OverworldEntity? winner = null;
            if (attacker != null && !outcome.AttackerWon)
            {
                // 简化:寻找最近的敌对 LordArmy 当 winner
                foreach (var e in Entities)
                {
                    if (e == attacker || !e.IsAlive) continue;
                    if (e.Faction != attacker.Faction &&
                        e.EntityTypeEnum == OverworldEntity.EntityType.LordArmy)
                    {
                        if (winner == null ||
                            attacker.Position.DistanceTo(e.Position) <
                            attacker.Position.DistanceTo(winner.Position))
                        {
                            winner = e;
                        }
                    }
                }
            }
            EntityCombatBridge.ApplyBattleOutcome(
                attacker!, outcome, winner,
                WorldEngine, SimCtx.Heroes, SimCtx.Prisoners, SimCtx.Relations, Pois);
        }

        // 更新被围攻的POI
        if (!string.IsNullOrEmpty(outcome.PoiName) && outcome.PoiCaptured)
        {
            var poi = FindPOIByName(outcome.PoiName);
            poi?.ApplySiegeOutcome(outcome);
        }

        // 移除被摧毁的实体
        Entities.RemoveAll(e => !e.IsAlive);
    }

    private OverworldEntity? FindEntityByName(string name)
    {
        foreach (var entity in Entities)
            if (entity.EntityName == name) return entity;
        return null;
    }

    private OverworldPOI? FindPOIByName(string name)
    {
        foreach (var poi in Pois)
            if (poi.PoiName == name) return poi;
        return null;
    }

    /// <summary>移除实体（兼容）</summary>
    public void RemoveEntity(OverworldEntity entity)
    {
        if (entity == null) return;
        entity.IsAlive = false;
        Entities.Remove(entity);
        SimCtx.SpatialIndex?.Remove(entity, entity.Position);
        EmitSignal(SignalName.EntityRemoved, entity);
    }

    /// <summary>将实体直接存入不活跃池（用于初始化阶段收容特殊角色）</summary>
    public void StoreToDormantPool(OverworldEntity entity)
    {
        SimCtx.EncounterSpawner.DormantPool.Store(entity, SimCtx.CurrentDay);
    }

    /// <summary>移除已卸载 chunk 的遭遇标记（兼容 stub）</summary>
    public void RemoveEncounterMarkersForUnloadedChunks(Godot.Collections.Array chunkKeys)
    {
        // 当前遭遇系统已迁移到 EncounterEntitySpawner（实体级），不再使用静态标记
        // 此方法保留为空实现，避免 调用报错
    }

    /// <summary>当前活跃的遭遇标记像素位置（兼容 stub）</summary>
    public Godot.Collections.Array ActiveEncounterMarkersGd
    {
        get
        {
            var arr = new Godot.Collections.Array();
            foreach (var m in ActiveEncounterMarkers)
            {
                arr.Add(new Godot.Collections.Dictionary
                {
                    { "pixel_position", m.PixelPosition },
                    { "type", (int)m.Type },
                });
            }
            return arr;
        }
    }

    public Godot.Collections.Dictionary SerializeArmies() => SimCtx.Armies.Serialize();
    public void DeserializeArmies(Godot.Collections.Dictionary data) => SimCtx.Armies.Deserialize(data, Entities);

    public Godot.Collections.Dictionary SerializeHeroNetwork()
        => Simulation.BuildSaveSnapshot(SimCtx);

    public void DeserializeHeroNetwork(Godot.Collections.Dictionary data)
        => Simulation.RestoreSaveSnapshot(data, SimCtx);
}
