// DailyDecisionProcessor.cs
// 每日决策处理器 — 处理 AI 实体的每日行动决策
// 从 OverworldEntityManager 拆出的 Core 层组件
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 每日决策处理器 — 管理 AI 实体的每日行动决策
/// </summary>
public class DailyDecisionProcessor
{
    private HexOverworldGrid? _hexGrid;
    private HexOverworldAStar? _hexAstar;
    private ChunkManager? _chunkManager;
    private ChunkAStar? _chunkAstar;
    private List<OverworldEntity>? _allEntities; // 当前 tick 的实体列表引用
    private static readonly Random _random = new();

    public void SetNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        _hexGrid = grid;
        _hexAstar = astar;
    }

    /// <summary>设置 Chunk 模式寻路（优先于 HexAStar）</summary>
    public void SetChunkNavigation(ChunkManager mgr, ChunkAStar astar)
    {
        _chunkManager = mgr;
        _chunkAstar = astar;
    }

    /// <summary>处理所有实体的每日决策</summary>
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

    private void DecideDailyAction(OverworldEntity entity, List<OverworldPOI> pois, int currentDay)
    {
        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.Adventurer: DecideAdventurer(entity); break;
            case OverworldEntity.EntityType.RaidingParty: DecideRaidingParty(entity, pois); break;
            case OverworldEntity.EntityType.Caravan: DecideCaravan(entity); break;
            case OverworldEntity.EntityType.EpicMonster: DecideEpicMonster(entity); break;
            case OverworldEntity.EntityType.LordArmy: DecideLordArmy(entity, pois, currentDay); break;
        }
    }

    private void DecideAdventurer(OverworldEntity entity)
    {
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Idle:
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                float dist = (float)(_random.NextDouble() * entity.PatrolRadius);
                Vector2 target = entity.HomePosition + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                StartMoveTo(entity, target);
                if (entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
                break;
            case OverworldEntity.AIState.Patrolling:
            case OverworldEntity.AIState.Fleeing:
                if (!entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Idle;
                break;
            case OverworldEntity.AIState.Chasing:
                if (entity.ChaseTarget != null && GodotObject.IsInstanceValid(entity.ChaseTarget) && entity.ChaseTarget.IsAlive)
                {
                    entity.TargetPosition = entity.ChaseTarget.Position;
                    StartMoveTo(entity, entity.ChaseTarget.Position);
                }
                else { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.Idle; }
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
            case OverworldEntity.AIState.Fleeing:
                if (entity.IsMoving) return;
                entity.CurrentAIState = OverworldEntity.AIState.Returning;
                StartMoveTo(entity, entity.HomePosition);
                break;
            case OverworldEntity.AIState.Chasing:
                if (entity.ChaseTarget != null && GodotObject.IsInstanceValid(entity.ChaseTarget) && entity.ChaseTarget.IsAlive)
                    StartMoveTo(entity, entity.ChaseTarget.Position);
                else { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget; }
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
        else if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing) { if (!entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Idle; }
    }

    private void DecideEpicMonster(OverworldEntity entity)
    {
        var intruder = FindIntruderInTerritory(entity);
        if (intruder != null) { entity.IsAggressive = true; entity.CurrentAIState = OverworldEntity.AIState.Chasing; entity.ChaseTarget = intruder; StartMoveTo(entity, intruder.Position); return; }
        entity.IsAggressive = false;
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Idle:
            case OverworldEntity.AIState.Patrolling:
                if (!entity.IsInTerritory(entity.Position) || !entity.IsMoving)
                {
                    float angle = (float)(_random.NextDouble() * Math.PI * 2);
                    float dist = (float)(_random.NextDouble() * entity.TerritoryRadius * 0.6f);
                    Vector2 target = entity.TerritoryCenter + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                    StartMoveTo(entity, target);
                    if (entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
                }
                break;
            case OverworldEntity.AIState.Chasing:
                if (entity.ChaseTarget != null && GodotObject.IsInstanceValid(entity.ChaseTarget) && entity.ChaseTarget.IsAlive)
                {
                    if (entity.IsInTerritory(entity.ChaseTarget.Position)) StartMoveTo(entity, entity.ChaseTarget.Position);
                    else { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.Idle; }
                }
                else { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.Idle; }
                break;
        }
    }

    public void DecideLordArmy(OverworldEntity entity, List<OverworldPOI> pois, int currentDay)
    {
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Reinforcing: DecideLordReinforcing(entity); return;
            case OverworldEntity.AIState.Besieging: return;
            case OverworldEntity.AIState.Fleeing: if (!entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Idle; return;
            case OverworldEntity.AIState.Chasing: DecideLordChasing(entity); return;
            case OverworldEntity.AIState.Recruiting: if (entity.DaysAlive % 5 == 0) entity.CurrentAIState = OverworldEntity.AIState.Idle; return;
        }
        // 巡逻
        if (entity.GuardedPOI != null)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float d = (float)(_random.NextDouble() * entity.PatrolRadius * 0.5f);
            StartMoveTo(entity, entity.GuardedPOI.Position + new Vector2(Mathf.Cos(angle) * d, Mathf.Sin(angle) * d));
            if (entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Idle)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float d = (float)(_random.NextDouble() * entity.PatrolRadius);
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

    private void DecideLordChasing(OverworldEntity entity)
    {
        if (entity.ChaseTarget != null && GodotObject.IsInstanceValid(entity.ChaseTarget) && entity.ChaseTarget.IsAlive)
        {
            if (entity.Position.DistanceTo(entity.ChaseTarget.Position) > entity.VisionRange * 1.5f)
            { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.Idle; }
            else StartMoveTo(entity, entity.ChaseTarget.Position);
        }
        else { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.Idle; }
    }

    private OverworldEntity? FindIntruderInTerritory(OverworldEntity monster)
    {
        if (_allEntities == null) return null;
        foreach (var entity in _allEntities)
        {
            if (entity == monster || !entity.IsAlive) continue;
            if (!monster.IsInTerritory(entity.Position)) continue;
            if (entity.Faction != monster.Faction && (entity.Faction == "hostile" || monster.Faction == "hostile" || entity.IsHostileToPlayer))
                return entity;
        }
        return null;
    }

    private void StartMoveTo(OverworldEntity entity, Vector2 target)
    {
        // 优先使用 Chunk 模式寻路
        if (_chunkManager != null && _chunkAstar != null)
        {
            var newPath = _chunkAstar.FindPathPixels(entity.Position, target, _chunkManager);
            if (newPath.Length > 0)
            {
                entity.Path.Clear();
                foreach (var p in newPath) entity.Path.Add(p);
                entity.IsMoving = true;
                entity.TargetPosition = target;
            }
            else
            {
                entity.IsMoving = false;
                entity.Path.Clear();
                if (entity.CurrentAIState == OverworldEntity.AIState.Patrolling || entity.CurrentAIState == OverworldEntity.AIState.MovingToTarget)
                    entity.CurrentAIState = OverworldEntity.AIState.Idle;
            }
            return;
        }

        // 回退到 HexAStar（旧路径模式）
        if (_hexGrid == null || _hexAstar == null) return;
        var hexPath = _hexAstar.FindPathPixels(entity.Position, target);
        if (hexPath.Length > 0)
        {
            entity.Path.Clear();
            foreach (var p in hexPath) entity.Path.Add(p);
            entity.IsMoving = true;
            entity.TargetPosition = target;
        }
        else
        {
            entity.IsMoving = false;
            entity.Path.Clear();
            if (entity.CurrentAIState == OverworldEntity.AIState.Patrolling || entity.CurrentAIState == OverworldEntity.AIState.MovingToTarget)
                entity.CurrentAIState = OverworldEntity.AIState.Idle;
        }
    }

    private void RemoveEntity(List<OverworldEntity> entities, OverworldEntity entity)
    {
        entities.Remove(entity);
        entity.IsAlive = false;
    }
}