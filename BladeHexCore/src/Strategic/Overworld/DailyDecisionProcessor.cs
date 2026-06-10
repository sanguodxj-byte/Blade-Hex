// DailyDecisionProcessor.cs
// 每日决策处理器 — 处理 AI 实体的每日行动决策
// 从 OverworldEntityManager 拆出的 Core 层组件
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Army;

namespace BladeHex.Strategic;

/// <summary>
/// 每日决策处理器 — 管理 AI 实体的每日行动决策
/// </summary>
public class DailyDecisionProcessor
{
    private readonly OverworldEntityNavigator _navigator = new();
    private List<OverworldEntity>? _allEntities; // 当前 tick 的实体列表引用
    private static readonly Random _random = new();
    private WorldEventEngine? _worldEventEngine;
    private ArmyRegistry? _armyRegistry;
    private readonly PerceptionIntentResolver _perceptionResolver = new();
    private Hero.HeroRelationMatrix? _heroRelationMatrix;

    /// <summary>
    /// 安全的实体有效性检查 — 以 IsAlive 为权威生命周期标志，
    /// 不依赖 GodotObject.IsInstanceValid（测试场景实体不在场景树中会返回 false）。
    /// </summary>
    private static bool IsEntityAlive(OverworldEntity? e)
        => e != null && e.IsAlive;

    /// <summary>
    /// AIStrategy → 巡逻半径倍率（越大跑得越远）
    /// </summary>
    private static float GetPatrolRadiusMultiplier(AIStrategyEnum strategy) => strategy switch
    {
        AIStrategyEnum.Reckless    => 1.4f,
        AIStrategyEnum.Berserk     => 1.5f,
        AIStrategyEnum.Cunning     => 0.8f,
        AIStrategyEnum.Intimidate  => 1.2f,
        AIStrategyEnum.Tactical    => 1.1f,
        AIStrategyEnum.Territorial => 0.7f,
        AIStrategyEnum.Cautious    => 0.6f,
        AIStrategyEnum.Instinct    => 1.0f,
        _ => 1.0f
    };

    public void SetWorldEventEngine(WorldEventEngine engine)
    {
        _worldEventEngine = engine;
    }

    public void SetArmyRegistry(ArmyRegistry registry)
    {
        _armyRegistry = registry;
    }

    public void SetHeroRelationMatrix(Hero.HeroRelationMatrix matrix)
    {
        _heroRelationMatrix = matrix;
    }

    public void SetNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        _navigator.SetHexNavigation(grid, astar);
    }

    /// <summary>设置 Chunk 模式寻路（优先于 HexAStar）</summary>
    public void SetChunkNavigation(ChunkManager mgr, ChunkAStar astar)
    {
        _navigator.SetChunkNavigation(mgr, astar);
    }

    public void SetPlayerPosition(Vector2 playerPosition)
    {
        _navigator.SetPlayerPosition(playerPosition);
    }

    /// <summary>处理所有实体的每日决策（纯策略层面：巡逻/掠夺/围攻/招募等，不含感知追逃）</summary>
    public void ProcessDailyDecisions(List<OverworldEntity> entities, List<OverworldPOI> pois, int currentDay)
    {
        _allEntities = entities; // 缓存引用供 FindIntruderInTerritory 使用

        var toRemove = new List<OverworldEntity>();
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) { toRemove.Add(entity); continue; }
            entity.OnDayPassed();
            DecideDailyAction(entity, pois, currentDay);
            if (entity.EntityTypeEnum == OverworldEntity.EntityType.RaidingParty && entity.DaysAlive > 21)
                toRemove.Add(entity);
        }
        foreach (var e in toRemove) RemoveEntity(entities, e);
        _allEntities = null;
    }

    /// <summary>
    /// 每小时感知意图判定 — 扫描威胁 → 设定追/逃意图 → 立即发起移动。
    /// 替代旧的每日 RunPerceptionPhase，让实体对周围环境做出近实时反应。
    /// </summary>
    public void ProcessHourlyIntent(List<OverworldEntity> entities, EntitySpatialIndex? spatialIndex = null)
    {
        _allEntities = entities;
        RunPerceptionPhase(entities, spatialIndex);

        // 感知意图已更新 → 为所有 Chasing/Fleeing 实体立即发起移动
        foreach (var entity in entities)
        {
            if (!entity.IsAlive || entity.Lod == OverworldEntity.EntityLod.Hibernated)
                continue;

            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing)
                RefreshChaseMove(entity);
            else if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing)
                RefreshFleeMove(entity);
        }

        _allEntities = null;
    }

    /// <summary>
    /// 帧级战术刷新：让 Chasing/Fleeing 在每日决策之外持续更新目标路径。
    /// 这提供接近“骑砍式”的大地图即时追逃：看见弱敌会继续追，看见强敌会持续拉开距离。
    /// </summary>
    public void ProcessFrameTactics(List<OverworldEntity> entities, EntitySpatialIndex? spatialIndex = null)
    {
        _allEntities = entities;
        RunPerceptionPhase(entities, spatialIndex);

        foreach (var entity in entities)
        {
            if (!entity.IsAlive || entity.Lod == OverworldEntity.EntityLod.Hibernated)
                continue;

            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing)
                RefreshChaseMove(entity);
            else if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing)
                RefreshFleeMove(entity);
        }

        _allEntities = null;
    }

    private void DecideDailyAction(OverworldEntity entity, List<OverworldPOI> pois, int currentDay)
    {
        // 交战中 → 不执行任何决策，等待 BattleResolver 结算
        if (entity.CurrentAIState == OverworldEntity.AIState.Engaged)
            return;

        // 追击 → 移动由 ProcessHourlyIntent/ProcessFrameTactics 处理，此处做状态清理与目标同步
        if (entity.CurrentAIState == OverworldEntity.AIState.Chasing)
        {
            // 通用：目标死亡/丢失 → 回 Idle
            if (entity.ChaseTarget == null || !IsEntityAlive(entity.ChaseTarget))
            {
                entity.ChaseTarget = null;
                entity.CurrentTacticalTarget = null;
                entity.CurrentAIState = OverworldEntity.AIState.Idle;
            }
            // EpicMonster 专属：目标离开领地 → 放弃追击
            else if (entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster
                     && !entity.IsInTerritory(entity.ChaseTarget.Position))
            {
                entity.ChaseTarget = null;
                entity.CurrentAIState = OverworldEntity.AIState.Idle;
            }
            else
            {
                // 保持 TargetPosition 与追击目标同步（安全网，实际移动由小时级驱动）
                entity.TargetPosition = entity.ChaseTarget.Position;
            }
            return;
        }

        // 逃跑 → 处理类型特有的状态转换（掠夺队→返回、怪物→巡逻），移动由小时级处理
        if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing)
        {
            HandleFleeingStateTransition(entity);
            return;
        }

        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.Adventurer: DecideAdventurer(entity); break;
            case OverworldEntity.EntityType.RaidingParty: DecideRaidingParty(entity, pois); break;
            case OverworldEntity.EntityType.Caravan: DecideCaravan(entity); break;
            case OverworldEntity.EntityType.EpicMonster: DecideEpicMonster(entity); break;
            case OverworldEntity.EntityType.LordArmy: DecideLordArmy(entity, pois, currentDay); break;
            case OverworldEntity.EntityType.BanditParty:
            case OverworldEntity.EntityType.RobberParty:
            case OverworldEntity.EntityType.PirateCrew: DecideBanditLike(entity); break;
        }
    }

    /// <summary>
    /// 处理 Fleeing 状态的类型特有转换（不处理移动 — 移动由 ProcessHourlyIntent 负责）
    /// </summary>
    private void HandleFleeingStateTransition(OverworldEntity entity)
    {
        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.RaidingParty:
                // 掠夺队逃跑 → 放弃任务直接返回基地
                entity.CurrentAIState = OverworldEntity.AIState.Returning;
                StartMoveTo(entity, entity.HomePosition);
                break;

            case OverworldEntity.EntityType.EpicMonster:
                // 史诗怪物逃跑 → 威胁消失后恢复巡逻
                if (!entity.IsMoving)
                    entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
                break;

            // Adventurer / LordArmy / Caravan / BanditLike：
            // 无类型特有转换，保持 Fleeing 直到 ProcessHourlyIntent 或 ProcessFrameTactics 处理
        }
    }

    private void DecideAdventurer(OverworldEntity entity)
    {
        float patrolMult = GetPatrolRadiusMultiplier(entity.AIStrategy);
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Idle:
                TryStartRandomPatrol(entity, entity.HomePosition, entity.PatrolRadius * patrolMult);
                break;
            case OverworldEntity.AIState.Patrolling:
                if (!entity.IsMoving)
                    TryContinuePatrol(entity);
                break;
        }
    }

    private void DecideRaidingParty(OverworldEntity entity, List<OverworldPOI> pois)
    {
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.MovingToTarget:
                if (entity.IsMoving) return;
                RaidSettlementOrVillage(entity, pois);
                entity.CurrentAIState = OverworldEntity.AIState.Returning;
                StartMoveTo(entity, entity.HomePosition);
                break;
            case OverworldEntity.AIState.Returning:
                if (entity.IsMoving) return;
                entity.SourceSettlement?.OnRaidPartyDestroyed();
                entity.IsAlive = false;
                break;
        }
    }

    private void RaidSettlementOrVillage(OverworldEntity entity, List<OverworldPOI> pois)
    {
        foreach (var poi in pois)
        {
            if (poi.Position.DistanceTo(entity.Position) < 150.0f)
            {
                if (poi.PoiTypeEnum == OverworldPOI.POIType.Village || poi.PoiTypeEnum == OverworldPOI.POIType.Town)
                {
                    var result = OverworldAIResolver.ResolveRaid(entity, poi);
                    poi.OnAttacked(entity, 0);
                    if ((bool)result["raider_destroyed"]) entity.IsAlive = false;
                    break;
                }
            }
        }
    }

    private void DecideCaravan(OverworldEntity entity)
    {
        if (entity.CurrentAIState == OverworldEntity.AIState.MovingToTarget)
        {
            if (entity.IsMoving) return;
            if (entity.DestinationTown != null)
            {
                entity.ProsperityContribution = true;
                entity.DestinationTown.Prosperity = Math.Min(100, entity.DestinationTown.Prosperity + 2);
            }
            var tmp = entity.OriginTown; entity.OriginTown = entity.DestinationTown; entity.DestinationTown = tmp;
            if (entity.DestinationTown != null) { entity.TargetPosition = entity.DestinationTown.Position; StartMoveTo(entity, entity.TargetPosition); }
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Idle)
        {
            if (entity.DestinationTown != null) { entity.TargetPosition = entity.DestinationTown.Position; StartMoveTo(entity, entity.TargetPosition); entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget; }
        }
    }

    private void DecideEpicMonster(OverworldEntity entity)
    {
        // Fleeing 已由 DecideDailyAction 顶层 + HandleFleeingStateTransition 处理

        // ── 领地回归优先 ── 怪物身处领地外时，放弃一切行为、径直返回领地中心
        if (!entity.IsInTerritory(entity.Position))
        {
            entity.ChaseTarget = null;
            entity.IsAggressive = false;
            entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
            if (!entity.IsMoving)
                StartMoveTo(entity, entity.TerritoryCenter);
            return;
        }

        // ── 领地内入侵者检测（每日级，补充小时级感知判定） ──
        var intruder = FindIntruderInTerritory(entity);
        if (intruder != null)
        {
            entity.IsAggressive = true;
            entity.CurrentAIState = OverworldEntity.AIState.Chasing;
            entity.ChaseTarget = intruder;
            StartMoveTo(entity, intruder.Position);
            return;
        }
        entity.IsAggressive = false;

        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Idle:
            case OverworldEntity.AIState.Patrolling:
                if (!entity.IsMoving)
                {
                    float angle = (float)(_random.NextDouble() * Math.PI * 2);
                    float dist = (float)(_random.NextDouble() * entity.TerritoryRadius * 0.6f);
                    Vector2 target = entity.TerritoryCenter + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                    StartMoveTo(entity, target);
                    if (entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
                }
                break;
        }
    }

    /// <summary>
    /// 通用匪类决策 — BanditParty(山贼) / RobberParty(劫匪) / PirateCrew(海寇)
    /// 无源聚落，以 HomePosition 为据点巡逻；追击/逃跑由小时级 ProcessHourlyIntent 处理。
    /// </summary>
    private void DecideBanditLike(OverworldEntity entity)
    {
        float patrolMult = GetPatrolRadiusMultiplier(entity.AIStrategy);
        if (entity.IsMoving && entity.CurrentAIState != OverworldEntity.AIState.Idle)
            return; // 正在移动中，等待到达

        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Idle:
                TryStartRandomPatrol(entity, entity.HomePosition, entity.PatrolRadius * patrolMult);
                break;

            case OverworldEntity.AIState.Patrolling:
                TryContinuePatrol(entity);
                break;
        }
    }

    public void DecideLordArmy(OverworldEntity entity, List<OverworldPOI> pois, int currentDay)
    {
        // 0. 检查是否在 Army 中
        var army = _armyRegistry?.GetByLord(entity);
        if (army != null)
        {
            if (entity.IsMarshal)
                DecideMarshal(entity, army, pois, currentDay);
            else
                DecideFollower(entity, army);
            return;
        }

        // 1. 现有高级AI状态直接返回
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Reinforcing: DecideLordReinforcing(entity); return;
            case OverworldEntity.AIState.Besieging:
                if (entity.SiegeTarget != null && entity.SiegeTarget.OwningFaction == entity.Faction)
                {
                    entity.SiegeTarget.EndSiege();
                    entity.SiegeTarget = null;
                    entity.CurrentAIState = OverworldEntity.AIState.Idle;
                }
                return;
            case OverworldEntity.AIState.Recruiting: if (entity.DaysAlive % 5 == 0) entity.CurrentAIState = OverworldEntity.AIState.Idle; return;
        }

        // 2. 战争目标指派逻辑
        if (_worldEventEngine != null)
        {
            var war = _worldEventEngine.ActiveWars.FirstOrDefault(w => w.NationA == entity.Faction || w.NationB == entity.Faction);
            if (war != null)
            {
                var objectives = war.NationA == entity.Faction ? war.ObjectivesA : war.ObjectivesB;
                WarLordOrders.AssignLordToObjective(entity, war, objectives, _allEntities ?? new List<OverworldEntity>(), currentDay, pois);

                // 如果被指派了战争目标
                if (!string.IsNullOrEmpty(entity.AssignedWarTargetPoiName))
                {
                    var targetPoi = pois.FirstOrDefault(p => p.PoiName == entity.AssignedWarTargetPoiName);
                    if (targetPoi != null)
                    {
                        if (targetPoi.OwningFaction == entity.Faction)
                        {
                            // 已被己方夺取，清空切回 Idle
                            entity.AssignedWarTargetPoiName = "";
                            entity.CurrentAIState = OverworldEntity.AIState.Idle;
                        }
                        else
                        {
                            float dist = entity.Position.DistanceTo(targetPoi.Position);
                            if (dist < 600.0f)
                            {
                                // 进入围攻状态
                                entity.CurrentAIState = OverworldEntity.AIState.Besieging;
                                entity.SiegeTarget = targetPoi;
                                targetPoi.BeginSiege(entity);
                                entity.IsMoving = false;
                                entity.Path.Clear();
                                return;
                            }
                            else
                            {
                                entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
                                StartMoveTo(entity, targetPoi.Position);
                                return;
                            }
                        }
                    }
                }

                // 3. 视野内拦截敌国领主
                OverworldEntity? enemyLord = null;
                float nearestDist = 99999.0f;
                if (_allEntities != null)
                {
                    foreach (var other in _allEntities)
                    {
                        if (other == entity || !other.IsAlive || other.EntityTypeEnum != OverworldEntity.EntityType.LordArmy)
                            continue;

                        bool isEnemy = (war.NationA == entity.Faction && other.Faction == war.NationB) ||
                                       (war.NationB == entity.Faction && other.Faction == war.NationA);

                        if (isEnemy && entity.IsInVision(other.Position))
                        {
                            float d = entity.Position.DistanceTo(other.Position);
                            if (d < nearestDist)
                            {
                                nearestDist = d;
                                enemyLord = other;
                            }
                        }
                    }
                }

                if (enemyLord != null)
                {
                    entity.CurrentAIState = OverworldEntity.AIState.Chasing;
                    entity.ChaseTarget = enemyLord;
                    StartMoveTo(entity, enemyLord.Position);
                    return;
                }
            }
        }

        // 4. 默认巡逻或 Idle 状态推进（受 AIStrategy 影响）
        float patrolMult = GetPatrolRadiusMultiplier(entity.AIStrategy);
        if (entity.GuardedPOI != null)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float d = (float)(_random.NextDouble() * entity.PatrolRadius * 0.5f * patrolMult);
            StartMoveTo(entity, entity.GuardedPOI.Position + new Vector2(Mathf.Cos(angle) * d, Mathf.Sin(angle) * d));
            if (entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Idle)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float d = (float)(_random.NextDouble() * entity.PatrolRadius * patrolMult);
            StartMoveTo(entity, entity.HomePosition + new Vector2(Mathf.Cos(angle) * d, Mathf.Sin(angle) * d));
            if (entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        }
        else if (!entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Idle;
    }

    private void DecideLordReinforcing(OverworldEntity entity)
    {
        if (entity.IsMoving) return;
        if (entity.ReinforceTarget != null) entity.GuardedPOI = entity.ReinforceTarget;
        entity.ReinforceTarget = null;
        entity.CurrentAIState = OverworldEntity.AIState.Idle;
    }

    // DecideLordChasing 已移除 — 追击移动由 ProcessHourlyIntent → RefreshChaseMove 处理

    private OverworldEntity? FindIntruderInTerritory(OverworldEntity monster)
    {
        if (_allEntities == null) return null;
        foreach (var entity in _allEntities)
        {
            if (entity == monster || !entity.IsAlive) continue;
            if (!monster.IsInTerritory(entity.Position)) continue;
            if (OverworldHostility.AreHostile(monster, entity, _worldEventEngine, _heroRelationMatrix))
                return entity;
        }
        return null;
    }

    public void OnEntityReachedDestination(OverworldEntity entity)
    {
        if (!entity.IsAlive || entity.Lod == OverworldEntity.EntityLod.Hibernated)
            return;

        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Patrolling:
                TryContinuePatrol(entity);
                break;
            case OverworldEntity.AIState.Chasing:
                RefreshChaseMove(entity);
                break;
            case OverworldEntity.AIState.Fleeing:
                if (entity.Position.DistanceTo(entity.HomePosition) > 50.0f)
                    StartMoveTo(entity, entity.HomePosition);
                else
                    entity.CurrentAIState = OverworldEntity.AIState.Idle;
                break;
        }
    }

    public void RefreshFleeMoveForExternalIntent(OverworldEntity entity)
    {
        if (!entity.IsAlive || entity.Lod == OverworldEntity.EntityLod.Hibernated)
            return;
        if (entity.CurrentAIState != OverworldEntity.AIState.Fleeing)
            return;

        RefreshFleeMove(entity);
    }

    private void TryContinuePatrol(OverworldEntity entity)
    {
        var center = entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster && entity.TerritoryCenter != Vector2.Zero
            ? entity.TerritoryCenter
            : entity.HomePosition;

        float radius = entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster
            ? entity.TerritoryRadius * 0.6f
            : entity.PatrolRadius * GetPatrolRadiusMultiplier(entity.AIStrategy);

        TryStartRandomPatrol(entity, center, radius);
    }

    private void TryStartRandomPatrol(OverworldEntity entity, Vector2 center, float radius)
    {
        radius = Math.Max(50.0f, radius);
        Vector2 previousTarget = entity.TargetPosition;

        for (int attempt = 0; attempt < 4; attempt++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float dist = (float)(_random.NextDouble() * radius);
            Vector2 target = center + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

            if (target.DistanceTo(entity.Position) < 25.0f)
                continue;

            StartMoveTo(entity, target);
            if (entity.IsMoving)
            {
                entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
                return;
            }
        }

        entity.TargetPosition = previousTarget;
        entity.CurrentAIState = OverworldEntity.AIState.Idle;
    }

    private void RefreshChaseMove(OverworldEntity entity)
    {
        if (IsEntityAlive(entity.ChaseTarget))
        {
            StartMoveTo(entity, entity.ChaseTarget.Position);
            return;
        }

        entity.ChaseTarget = null;
        entity.CurrentTacticalTarget = null;
        entity.CurrentAIState = OverworldEntity.AIState.Idle;
        entity.LastIntentSummary = "";
    }

    private void RefreshFleeMove(OverworldEntity entity)
    {
        var threat = entity.CurrentTacticalTarget;
        if (!IsEntityAlive(threat))
        {
            // 没有明确威胁时，回撤至 HomePosition，避免原地卡住。
            if (entity.Position.DistanceTo(entity.HomePosition) > 50.0f)
                StartMoveTo(entity, entity.HomePosition);
            else
            {
                entity.CurrentAIState = OverworldEntity.AIState.Idle;
                entity.LastIntentSummary = "";
            }
            return;
        }

        float dist = entity.Position.DistanceTo(threat.Position);
        if (dist > entity.VisionRange * 1.8f)
        {
            entity.CurrentTacticalTarget = null;
            entity.CurrentAIState = OverworldEntity.AIState.Idle;
            entity.LastIntentSummary = "";
            return;
        }

        Vector2 away = entity.Position - threat.Position;
        if (away.LengthSquared() < 0.01f)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            away = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        else
        {
            away = away.Normalized();
        }

        float fleeDistance = Math.Max(420.0f, entity.VisionRange * 0.9f);
        Vector2 fallbackAnchor = entity.EntityTypeEnum == OverworldEntity.EntityType.Caravan && entity.OriginTown != null
            ? entity.OriginTown.Position
            : entity.HomePosition;
        Vector2 target = entity.Position + away * fleeDistance;

        // 如果据点方向也远离威胁，则更偏向回据点，形成“逃回安全地”的骑砍式行为。
        if (fallbackAnchor != Vector2.Zero && fallbackAnchor.DistanceTo(threat.Position) > entity.Position.DistanceTo(threat.Position))
            target = fallbackAnchor;

        entity.TargetPosition = target;
        entity.LastIntentSummary = $"逃离 {threat.EntityName}";
        StartMoveTo(entity, target);
    }

    private void StartMoveTo(OverworldEntity entity, Vector2 target)
    {
        if (!_navigator.StartMoveTo(entity, target))
        {
            // 导航失败时不重置状态 — 保留原始意图（如 Patrolling/Fleeing），
            // 让下一个 tick 周期重试。避免每日决策被立即覆盖。
        }
    }

    private void RemoveEntity(List<OverworldEntity> entities, OverworldEntity entity)
    {
        entities.Remove(entity);
        entity.IsAlive = false;
    }

    /// <summary>
    /// 感知意图阶段 — 对所有活跃实体统一执行感知→评估→追/逃意图设定。
    /// 替换旧行为评估器 + BattleResolver 视野检测的双路径。
    /// </summary>
    private void RunPerceptionPhase(List<OverworldEntity> entities, EntitySpatialIndex? spatialIndex = null)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;
            if (entity.Lod == OverworldEntity.EntityLod.Hibernated) continue;

            // 已在 Chasing/Fleeing/Engaged → 跳过（已是有效决策）
            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing
                || entity.CurrentAIState == OverworldEntity.AIState.Fleeing
                || entity.CurrentAIState == OverworldEntity.AIState.Engaged)
                continue;

            // 只在 Idle/Patrolling 状态下进行感知评估
            if (entity.CurrentAIState != OverworldEntity.AIState.Idle
                && entity.CurrentAIState != OverworldEntity.AIState.Patrolling)
                continue;

            // 领主正在执行战争目标 → 不覆盖
            if (entity.EntityTypeEnum == OverworldEntity.EntityType.LordArmy
                && !string.IsNullOrEmpty(entity.AssignedWarTargetPoiName))
                continue;

            // 史诗怪物在领地外 → 不执行行为评估
            if (entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster
                && entity.TerritoryCenter != Godot.Vector2.Zero
                && !entity.IsInTerritory(entity.Position))
                continue;

            var (bestTarget, intent) = _perceptionResolver.ResolveBest(entity, entities, _worldEventEngine, _heroRelationMatrix, spatialIndex);
            if (bestTarget == null || intent.Type == Intent.IntentType.None)
                continue;

            OverworldIntentApplier.Apply(entity, intent);
        }
    }

    private void DecideMarshal(OverworldEntity entity, Army.Army army, List<OverworldPOI> pois, int currentDay)
    {
        switch (army.State)
        {
            case Army.ArmyState.Forming:
                // 如果实体已在围攻/交战状态 → 不覆盖（避免兵力不集而先冲锋）
                if (entity.CurrentAIState == OverworldEntity.AIState.Besieging ||
                    entity.CurrentAIState == OverworldEntity.AIState.Engaged)
                    return;
                entity.CurrentAIState = OverworldEntity.AIState.Idle;
                entity.IsMoving = false;
                entity.Path.Clear();
                break;

            case Army.ArmyState.Marching:
                // 朝目标行军
                var targetPoi = pois.FirstOrDefault(p => p.PoiName == army.TargetPoiName);
                if (targetPoi != null)
                {
                    float dist = entity.Position.DistanceTo(targetPoi.Position);
                    if (dist < 600.0f)
                    {
                        // 进入围攻状态
                        army.State = Army.ArmyState.Besieging;
                        entity.CurrentAIState = OverworldEntity.AIState.Besieging;
                        entity.SiegeTarget = targetPoi;
                        targetPoi.BeginSiege(entity);
                        entity.IsMoving = false;
                        entity.Path.Clear();
                    }
                    else
                    {
                        entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
                        StartMoveTo(entity, targetPoi.Position);
                    }
                }
                else
                {
                    // 目标丢失，解散
                    army.State = Army.ArmyState.Disbanding;
                }
                break;

            case Army.ArmyState.Besieging:
                // 原地围攻
                entity.CurrentAIState = OverworldEntity.AIState.Besieging;
                entity.IsMoving = false;
                entity.Path.Clear();
                break;
        }
    }

    private void DecideFollower(OverworldEntity entity, Army.Army army)
    {
        var marshal = army.Marshal;
        if (marshal == null || !marshal.IsAlive)
        {
            return;
        }

        // 计算 follower 的位置偏置环形排列
        int index = army.Members.Where(m => m != marshal).ToList().IndexOf(entity);
        if (index < 0) index = 0;

        float angle = (index * 60.0f) * Mathf.Pi / 180.0f;
        Vector2 offset = new Vector2(Mathf.Cos(angle) * 80.0f, Mathf.Sin(angle) * 80.0f);

        entity.Position = marshal.Position + offset;
        entity.IsMoving = false;
        entity.Path.Clear();
        entity.CurrentAIState = OverworldEntity.AIState.Escorting;
    }
}
