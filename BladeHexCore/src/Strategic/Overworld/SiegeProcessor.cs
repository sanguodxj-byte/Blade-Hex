// SiegeProcessor.cs
// 围攻/回援/招募处理器 — 处理 POI 围攻结算、回援检查、招募
// 从 OverworldEntityManager 拆出的 Core 层组件
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 围攻处理器 — 管理围攻结算、回援检查、招募逻辑
/// </summary>
public class SiegeProcessor
{
    private const float SIEGE_APPROACH_DIST = 600.0f;
    private const float REINFORCE_MAX_DIST = 800.0f;

    private HexOverworldGrid? _hexGrid;
    private HexOverworldAStar? _hexAstar;
    private static readonly Random _random = new();

    public void SetNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        _hexGrid = grid;
        _hexAstar = astar;
    }

    /// <summary>处理所有围攻结算</summary>
    public void ProcessSieges(List<OverworldEntity> entities, ISiegeSignals signals)
    {
        var toResolve = new List<(OverworldEntity entity, OverworldPOI target)>();
        foreach (var entity in entities)
        {
            if (entity.IsAlive && entity.CurrentAIState == OverworldEntity.AIState.Besieging && entity.SiegeTarget != null)
            {
                if (entity.SiegeTarget.SiegeDays >= 2)
                    toResolve.Add((entity, entity.SiegeTarget));
            }
        }

        foreach (var (entity, target) in toResolve)
        {
            if (!entity.IsAlive) { target.EndSiege(); continue; }

            var result = OverworldAIResolver.ResolveSiege(entity, target);

            if ((bool)result["attacker_won"])
            {
                string oldFaction = target.OwningFaction;
                target.OwningFaction = entity.Faction;
                target.EndSiege();
                signals?.OnSiegeResolved(target, true, entity);
                signals?.OnPoiCaptured(target, entity.Faction, entity);

                entity.CurrentAIState = OverworldEntity.AIState.Idle;
                entity.GuardedPOI = target;
                entity.HomePosition = target.Position;
                entity.SiegeTarget = null;
            }
            else
            {
                target.EndSiege();
                signals?.OnSiegeResolved(target, false, entity);

                if ((bool)result["attacker_destroyed"]) entity.IsAlive = false;
                else
                {
                    entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
                    entity.SiegeTarget = null;
                }
            }
        }
    }

    /// <summary>处理回援检查</summary>
    public void ProcessReinforcementChecks(List<OverworldEntity> entities, List<OverworldPOI> pois, ISiegeSignals signals)
    {
        foreach (var poi in pois)
        {
            if (!poi.NeedsReinforcement() || poi.OwningFaction != "kingdom") continue;

            OverworldEntity? nearestLord = null;
            float nearestDist = 99999.0f;

            foreach (var entity in entities)
            {
                if (!entity.IsAlive || entity.EntityTypeEnum != OverworldEntity.EntityType.LordArmy || entity.Faction != "kingdom")
                    continue;
                if (entity.CurrentAIState == OverworldEntity.AIState.Besieging || entity.CurrentAIState == OverworldEntity.AIState.Reinforcing)
                    continue;

                float dist = entity.Position.DistanceTo(poi.Position);
                if (dist < nearestDist) { nearestDist = dist; nearestLord = entity; }
            }

            if (nearestLord != null && nearestDist < REINFORCE_MAX_DIST)
            {
                nearestLord.CurrentAIState = OverworldEntity.AIState.Reinforcing;
                nearestLord.ReinforceTarget = poi;
                nearestLord.TargetPosition = poi.Position;
                if (_hexGrid != null && _hexAstar != null)
                {
                    var newPath = _hexAstar.FindPathPixels(nearestLord.Position, poi.Position);
                    if (newPath.Length > 0)
                    {
                        nearestLord.Path.Clear();
                        foreach (var p in newPath) nearestLord.Path.Add(p);
                        nearestLord.IsMoving = true;
                    }
                }
                signals?.OnReinforcementArrived(poi, nearestLord);
            }
        }
    }

    /// <summary>处理招募结算</summary>
    public void ProcessRecruitment(List<OverworldEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsAlive || entity.EntityTypeEnum != OverworldEntity.EntityType.LordArmy) continue;
            if (entity.CurrentAIState == OverworldEntity.AIState.Idle && entity.GuardedPOI != null)
            {
                var poi = entity.GuardedPOI;
                if (poi.PoiTypeEnum == OverworldPOI.POIType.Castle && poi.OwningFaction == entity.Faction)
                {
                    entity.GarrisonSize = Math.Min(entity.GarrisonSize + 2, 80);
                    entity.CombatPower = entity.GarrisonSize * entity.PartyLevel * 1.5f;
                }
            }
        }
    }
}

/// <summary>
/// 围攻信号接口 — 由 Frontend 层的 OverworldEntityManager 实现，用于转发 Godot 信号
/// </summary>
public interface ISiegeSignals
{
    void OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker);
    void OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor);
    void OnReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer);
}