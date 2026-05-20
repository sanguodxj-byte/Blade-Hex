using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Events;

namespace BladeHex.Strategic;

/// <summary>
/// 遭遇标记管理器（原 OverworldEntityManager 简化版，docs/26+27 Chunk 模式）
/// 
/// 按 Chunk 模式重构：
/// - AI 实体移动由 Core 层 DailyDecisionProcessor / MovementProcessor 处理（全图模式保留）
/// - Chunk 模式下新增遭遇标记检测（CheckChunkEncounters）
/// </summary>
[GlobalClass]
public partial class OverworldEntityManager : Node, ISiegeSignals
{
    // ========================================
    // 信号
    // ========================================

    [Signal] public delegate void EntityRemovedEventHandler(OverworldEntity entity);
    [Signal] public delegate void EntitySpawnedEventHandler(OverworldEntity entity);
    [Signal] public delegate void VillageAttackedEventHandler(OverworldPOI village, OverworldEntity attacker);
    [Signal] public delegate void SiegeStartedEventHandler(OverworldPOI siegeTarget, OverworldEntity attacker);
    [Signal] public delegate void SiegeResolvedEventHandler(OverworldPOI siegeTarget, bool attackerWon, OverworldEntity attacker);
    [Signal] public delegate void ReinforcementArrivedEventHandler(OverworldPOI targetPoi, OverworldEntity reinforcer);
    [Signal] public delegate void AiBattleOccurredEventHandler(OverworldEntity attacker, OverworldEntity defender, bool attackerWon);
    [Signal] public delegate void PoiCapturedEventHandler(OverworldPOI poi, string newFaction, OverworldEntity captor);

    /// <summary>当 Chunk 遭遇被触发时发射（用 Vector2I 坐标，侧通过 check_chunk_encounters 获取完整数据）</summary>
    [Signal] public delegate void ChunkEncounterTriggeredEventHandler(Vector2I worldCoord);

    // ========================================
    // Core 层处理器（全图模式保留）
    // ========================================

    private readonly DailyDecisionProcessor _dailyProcessor = new();
    private readonly MovementProcessor _movementProcessor = new();
    private readonly BattleResolver _battleResolver = new();
    private readonly SiegeProcessor _siegeProcessor = new();

    // ========================================
    // 遭遇实体生成器（Chunk 模式新增）
    // ========================================

    private readonly EncounterEntitySpawner _encounterSpawner = new();

    // ========================================
    // 成员变量
    // ========================================

    public List<OverworldEntity> Entities = new();
    public List<OverworldPOI> Pois = new();

    private HexOverworldGrid? _hexGrid;
    private HexOverworldAStar? _hexAstar;
    private ChunkManager? _chunkManager;
    private ChunkAStar? _chunkAstar;
    private Vector2 _playerPosition = Vector2.Zero;
    private int _currentDay = 1;

    private const float SIEGE_APPROACH_DIST = 600.0f;
    private const float ENCOUNTER_MARKER_DIST = 300.0f;

    /// <summary>当前活跃的遭遇标记（地图上的静态标记点）</summary>
    public List<EncounterMarkerData> ActiveEncounterMarkers { get; private set; } = new();

    // ========================================
    // 生命周期
    // ========================================

    public override void _EnterTree()
    {
        EventBus.Instance?.Subscribe(EventBus.Signals.DayPassed, OnDayPassedEvent);
    }

    public override void _ExitTree()
    {
        EventBus.Instance?.Unsubscribe(EventBus.Signals.DayPassed, OnDayPassedEvent);
    }

    private void OnDayPassedEvent(Godot.Collections.Dictionary _) => OnDayPassed();

    // ========================================
    // 初始化与数据加载
    // ========================================

    public void SetHexNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        _hexGrid = grid;
        _hexAstar = astar;
        _dailyProcessor.SetNavigation(grid, astar);
        _siegeProcessor.SetNavigation(grid, astar);
    }

    /// <summary>设置 Chunk 模式寻路（chunk 模式专用）</summary>
    public void SetChunkNavigation(ChunkManager mgr, ChunkAStar astar)
    {
        _chunkManager = mgr;
        _chunkAstar = astar;
        _encounterSpawner.ChunkManagerRef = mgr;
        _dailyProcessor.SetChunkNavigation(mgr, astar);
    }

    public void LoadWorld(Godot.Collections.Array worldPois, Godot.Collections.Array worldEntities)
    {
        Pois.Clear();
        foreach (var poi in worldPois) Pois.Add((OverworldPOI)poi);

        Entities.Clear();
        foreach (var entity in worldEntities) Entities.Add((OverworldEntity)entity);
    }

    public void UpdatePlayerPosition(Vector2 pos) => _playerPosition = pos;

    // ========================================
    // 核心更新逻辑
    // ========================================

    /// <summary>每帧更新：委托给 Core 层 MovementProcessor + 驱动遭遇实体生成器</summary>
    public void TickMovement(float delta)
    {
        // 1. 生成新的敌对实体 + 更新追击 AI
        var newSpawned = _encounterSpawner.Tick(delta, _playerPosition, Entities, PlayerLevel, _currentDay, Pois);
        foreach (var e in newSpawned)
        {
            Entities.Add(e);
            EmitSignal(SignalName.EntitySpawned, e);
        }

        // 2. 推进所有实体移动
        _movementProcessor.TickMovement(delta, Entities, OnEntityReachedDestination);

        // 3. 收容离开活跃区域的实体到不活跃池
        DeactivateDistantEntities();
    }

    /// <summary>收容距离玩家过远的实体到不活跃池</summary>
    private const float DEACTIVATION_DISTANCE = 2500.0f; // 约 16 格 × 156px

    private void DeactivateDistantEntities()
    {
        var toRemove = new List<OverworldEntity>();

        foreach (var entity in Entities)
        {
            if (!entity.IsAlive) continue;
            // 不收容正在追击玩家的实体（避免追着追着消失）
            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing) continue;
            // 不收容围攻中的实体
            if (entity.CurrentAIState == OverworldEntity.AIState.Besieging) continue;

            float dist = entity.Position.DistanceTo(_playerPosition);
            if (dist > DEACTIVATION_DISTANCE)
            {
                _encounterSpawner.DormantPool.Store(entity, _currentDay);
                toRemove.Add(entity);
            }
        }

        foreach (var entity in toRemove)
        {
            Entities.Remove(entity);
            EmitSignal(SignalName.EntityRemoved, entity);
        }
    }

    /// <summary>玩家等级（影响敌方等级缩放，由 OverworldScene3D 设置）</summary>
    public int PlayerLevel { get; set; } = 1;

    /// <summary>玩家种族（影响 Adventurer 同/异族判定，由 OverworldScene3D 设置）</summary>
    public int PlayerRaceId
    {
        get => _encounterSpawner.PlayerRaceId;
        set => _encounterSpawner.PlayerRaceId = value;
    }

    /// <summary>
    /// 检查玩家是否触发遭遇（有敌对实体距离玩家足够近）
    /// 替代旧的 check_chunk_encounters 调用
    /// </summary>
    public OverworldEntity? CheckPlayerEntityEncounter()
    {
        return _encounterSpawner.CheckEncounter(_playerPosition, Entities);
    }

    /// <summary>每日更新：委托给 Core 层处理器</summary>
    public void OnDayPassed()
    {
        _currentDay++;

        // 1. 更新所有 POI
        foreach (var poi in Pois) poi.OnDayPassed();

        // 2. 检测实体间交互（BattleResolver）
        _battleResolver.ProcessEntityInteractions(Entities);

        // 3. 每日决策（DailyDecisionProcessor）
        _dailyProcessor.ProcessDailyDecisions(Entities, Pois, _currentDay);

        // 4. 围攻结算（SiegeProcessor）
        _siegeProcessor.ProcessSieges(Entities, this);

        // 5. 回援检查（SiegeProcessor）
        _siegeProcessor.ProcessReinforcementChecks(Entities, Pois, this);

        // 6. 招募结算（SiegeProcessor）
        _siegeProcessor.ProcessRecruitment(Entities);

        // 7. 清理死亡实体
        Entities.RemoveAll(e => !e.IsAlive);

        // 8. 不活跃池每日维护（淘汰过期实体）
        _encounterSpawner.DormantPool.OnDayPassed(_currentDay);
    }

    // ========================================
    // ISiegeSignals 实现 — 转发为 Godot 信号
    // ========================================

    void ISiegeSignals.OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
        => EmitSignal(SignalName.SiegeResolved, target, attackerWon, attacker);

    void ISiegeSignals.OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
        => EmitSignal(SignalName.PoiCaptured, poi, newFaction, captor);

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

    /// <summary>获取玩家视野内的实体</summary>
    public List<OverworldEntity> GetVisibleEntities(Vector2 playerPos, float visionRange)
    {
        var visible = new List<OverworldEntity>();
        foreach (var entity in Entities)
            if (entity.IsAlive && playerPos.DistanceTo(entity.Position) <= visionRange) visible.Add(entity);
        return visible;
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

        // 更新攻击方实体
        if (!string.IsNullOrEmpty(outcome.AttackerEntityName))
        {
            var attacker = FindEntityByName(outcome.AttackerEntityName);
            attacker?.ApplyBattleOutcome(outcome);
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
        EmitSignal(SignalName.EntityRemoved, entity);
    }

    /// <summary>将实体直接存入不活跃池（用于初始化阶段收容特殊角色）</summary>
    public void StoreToDormantPool(OverworldEntity entity)
    {
        _encounterSpawner.DormantPool.Store(entity, _currentDay);
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
}
