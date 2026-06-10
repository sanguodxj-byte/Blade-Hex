// SiegeProcessor.cs
// 围攻/回援/招募处理器 — 处理 POI 围攻结算、回援检查、招募
// 从 OverworldEntityManager 拆出的 Core 层组件
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Army;

namespace BladeHex.Strategic;

/// <summary>
/// 围攻处理器 — 管理围攻结算、回援检查、招募逻辑
/// </summary>
public class SiegeProcessor
{
    private const float SIEGE_APPROACH_DIST = 600.0f;
    private const float REINFORCE_MAX_DIST = 800.0f;

    private readonly OverworldEntityNavigator _navigator = new();
    private ArmyRegistry? _armyRegistry;
    private static readonly Random _random = new();

    public void SetArmyRegistry(ArmyRegistry registry)
    {
        _armyRegistry = registry;
    }

    private BladeHex.Strategic.Hero.HeroRegistry? _heroRegistry;
    private BladeHex.Strategic.Hero.PrisonerLedger? _prisonerLedger;
    private BladeHex.Strategic.Hero.HeroRelationMatrix? _relationMatrix;
    private List<OverworldPOI> _pois = new();

    public void SetHeroNetwork(
        BladeHex.Strategic.Hero.HeroRegistry heroes, 
        BladeHex.Strategic.Hero.PrisonerLedger ledger, 
        BladeHex.Strategic.Hero.HeroRelationMatrix relations)
    {
        _heroRegistry = heroes;
        _prisonerLedger = ledger;
        _relationMatrix = relations;
    }

    public void SetPois(List<OverworldPOI> pois)
    {
        _pois = pois;
    }

    private void ResolveEntityDefeat(OverworldEntity loser, OverworldEntity winner, WorldEventEngine? engine)
    {
        BladeHex.Strategic.Hero.HeroDefeatResolver.Resolve(
            loser, winner, engine, _heroRegistry, _prisonerLedger, _relationMatrix, _pois);
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

    /// <summary>处理所有围攻结算</summary>
    public void ProcessSieges(List<OverworldEntity> entities, ISiegeSignals signals, int currentDay, WorldEventEngine? engine, Vector2 playerPosition = default)
    {
        var toResolve = new List<(OverworldEntity entity, OverworldPOI target)>();
        var queuedTargets = new HashSet<OverworldPOI>();
        foreach (var entity in entities)
        {
            if (entity.IsAlive && entity.CurrentAIState == OverworldEntity.AIState.Besieging && entity.SiegeTarget != null)
            {
                if (ShouldResolveSiege(entity, entity.SiegeTarget))
                    QueueSiege(toResolve, queuedTargets, entity, entity.SiegeTarget);
            }
        }

        // POI is the authoritative "under siege" record. Daily/army decisions can
        // transiently overwrite the attacker's state; do not let that orphan a siege.
        foreach (var poi in _pois)
        {
            if (!poi.IsUnderSiege || poi.SiegeBy == null || !poi.SiegeBy.IsAlive)
                continue;
            if (queuedTargets.Contains(poi))
                continue;

            var attacker = poi.SiegeBy;
            if (attacker.CurrentAIState != OverworldEntity.AIState.Engaged)
            {
                attacker.CurrentAIState = OverworldEntity.AIState.Besieging;
                attacker.SiegeTarget = poi;
                attacker.IsMoving = false;
                attacker.Path.Clear();
            }

            if (ShouldResolveSiege(attacker, poi))
                QueueSiege(toResolve, queuedTargets, attacker, poi);
        }

        foreach (var (entity, target) in toResolve)
        {
            if (!entity.IsAlive) { target.EndSiege(); continue; }

            var army = _armyRegistry?.GetByLord(entity);
            float? armyTotalPower = army?.AggregateCombatPower;
            var result = OverworldAIResolver.ResolveSiege(entity, target, armyTotalPower);

            if ((bool)result["attacker_won"])
            {
                string oldFaction = target.OwningFaction;
                
                // 改为调用 PoiTransferService 进行易手和广播
                bool playerNearby = playerPosition != default && playerPosition.DistanceTo(target.Position) <= 600.0f;
                PoiTransferService.Apply(target, entity.Faction, entity, currentDay, engine, playerNearby);

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

                var defenderFaction = target.OwningFaction;
                var dummyWinner = new OverworldEntity { EntityName = "守军", Faction = defenderFaction, HeroId = "defender_garrison" };

                if ((bool)result["attacker_destroyed"]) ResolveEntityDefeat(entity, dummyWinner, engine);
                else
                {
                    entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
                    entity.SiegeTarget = null;
                }

                // 战败损失传播给全军成员
                if (army != null)
                {
                    foreach (var m in army.Members.ToList())
                    {
                        if (m == entity) continue;
                        m.CombatPower = Math.Max(0.0f, m.CombatPower * 0.5f);
                        m.GarrisonSize = Math.Max(0, (int)(m.GarrisonSize * 0.5f));
                        m.PartySize = Math.Max(0, (int)(m.PartySize * 0.5f));
                        if (m.CombatPower < 1.0f || m.PartySize <= 0)
                        {
                            ResolveEntityDefeat(m, dummyWinner, engine);
                        }
                        else
                        {
                            m.CurrentAIState = OverworldEntity.AIState.Fleeing;
                        }
                    }
                }
            }
        }
    }

    private bool ShouldResolveSiege(OverworldEntity entity, OverworldPOI target)
    {
        var army = _armyRegistry?.GetByLord(entity);
        if (army != null && army.State != Army.ArmyState.Forming)
        {
            if (!entity.IsMarshal) return false;

            int requiredDays = 4;
            float defPower = target.GarrisonCurrent;
            if (army.AggregateCombatPower / Math.Max(1f, defPower) < 1.5f)
                requiredDays = 2;

            return target.SiegeDays >= requiredDays;
        }

        // No army, or a just-created Forming army whose lord was already sieging.
        return target.SiegeDays >= 2;
    }

    private static void QueueSiege(
        List<(OverworldEntity entity, OverworldPOI target)> toResolve,
        HashSet<OverworldPOI> queuedTargets,
        OverworldEntity entity,
        OverworldPOI target)
    {
        if (queuedTargets.Add(target))
            toResolve.Add((entity, target));
    }

    /// <summary>处理回援检查</summary>
    public void ProcessReinforcementChecks(List<OverworldEntity> entities, List<OverworldPOI> pois, ISiegeSignals signals, EntitySpatialIndex? index = null)
    {
        foreach (var poi in pois)
        {
            if (!poi.NeedsReinforcement()) continue;
            if (string.IsNullOrEmpty(poi.OwningFaction) || poi.OwningFaction == "neutral" || poi.OwningFaction == "hostile") continue;

            OverworldEntity? nearestLord = null;
            float nearestDist = 99999.0f;

            IEnumerable<OverworldEntity> candidates = index != null 
                ? index.QueryRadius(poi.Position, REINFORCE_MAX_DIST)
                : entities;

            foreach (var entity in candidates)
            {
                if (!entity.IsAlive || entity.EntityTypeEnum != OverworldEntity.EntityType.LordArmy || entity.Faction != poi.OwningFaction)
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
                _navigator.StartMoveTo(nearestLord, poi.Position);
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
