using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// AI实体行为管理器 —— AI 决策、移动、交互、战斗结算
/// </summary>
[GlobalClass]
public partial class OverworldEntityManager : Node
{
    // ========================================
    // 信号
    // ========================================

    [Signal] public delegate void EntityRemovedEventHandler(OverworldEntity entity);
    [Signal] public delegate void VillageAttackedEventHandler(OverworldPOI village, OverworldEntity attacker);
    [Signal] public delegate void SiegeStartedEventHandler(OverworldPOI siegeTarget, OverworldEntity attacker);
    [Signal] public delegate void SiegeResolvedEventHandler(OverworldPOI siegeTarget, bool attackerWon, OverworldEntity attacker);
    [Signal] public delegate void ReinforcementArrivedEventHandler(OverworldPOI targetPoi, OverworldEntity reinforcer);
    [Signal] public delegate void AiBattleOccurredEventHandler(OverworldEntity attacker, OverworldEntity defender, bool attackerWon);
    [Signal] public delegate void PoiCapturedEventHandler(OverworldPOI poi, string newFaction, OverworldEntity captor);

    // ========================================
    // 成员变量
    // ========================================

    public List<OverworldEntity> Entities = new();
    public List<OverworldPOI> Pois = new();

    private HexOverworldGrid? _hexGrid;
    private HexOverworldAStar? _hexAstar;
    private Vector2 _playerPosition = Vector2.Zero;
    private int _currentDay = 1;

    private static readonly Random _random = new();

    // 交互距离阈值
    private const float INTERACTION_DIST = 500.0f;
    private const float SIEGE_APPROACH_DIST = 600.0f;
    private const float CHASE_SPEED_MULT = 1.1f;

    // ========================================
    // 初始化与数据加载
    // ========================================

    public void SetHexNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        _hexGrid = grid;
        _hexAstar = astar;
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

    /// <summary>每帧更新：处理实体移动</summary>
    public void TickMovement(float delta)
    {
        foreach (var entity in Entities)
        {
            if (!entity.IsMoving || !entity.IsAlive) continue;

            if (entity.Path.Count == 0)
            {
                entity.IsMoving = false;
                OnEntityReachedDestination(entity);
                continue;
            }

            Vector2 targetPos = entity.Path[0];
            Vector2 dir = (targetPos - entity.Position).Normalized();
            float dist = entity.Position.DistanceTo(targetPos);
            float speed = entity.MoveSpeed;

            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing)
                speed *= CHASE_SPEED_MULT;

            float step = speed * delta;

            if (step >= dist)
            {
                entity.Position = targetPos;
                entity.Path.RemoveAt(0);
                if (entity.Path.Count == 0)
                {
                    entity.IsMoving = false;
                    OnEntityReachedDestination(entity);
                }
            }
            else
            {
                entity.Position += dir * step;
            }
        }
    }

    /// <summary>每日更新：处理 AI 决策与全局系统结算</summary>
    public void OnDayPassed()
    {
        _currentDay++;

        // 1. 更新所有 POI
        foreach (var poi in Pois) poi.OnDayPassed();

        // 2. 检测实体间交互
        ProcessEntityInteractions();

        // 3. 每个实体做每日决策
        var toRemove = new List<OverworldEntity>();
        foreach (var entity in Entities)
        {
            if (!entity.IsAlive)
            {
                toRemove.Add(entity);
                continue;
            }

            entity.OnDayPassed();
            DecideDailyAction(entity);

            // 掠夺队存活过久消亡
            if (entity.EntityTypeEnum == OverworldEntity.EntityType.RaidingParty && entity.DaysAlive > 21)
                toRemove.Add(entity);
        }

        // 4. 清理
        foreach (var e in toRemove) RemoveEntity(e);

        // 5. 围攻结算
        ProcessSieges();

        // 6. 回援检查
        ProcessReinforcementChecks();

        // 7. 生成新掠夺队
        SpawnNewRaidingParties();

        // 8. 招募结算
        ProcessRecruitment();
    }

    // ========================================
    // 交互逻辑
    // ========================================

    private void ProcessEntityInteractions()
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            var a = Entities[i];
            if (!a.IsAlive) continue;

            for (int j = i + 1; j < Entities.Count; j++)
            {
                var b = Entities[j];
                if (!b.IsAlive) continue;

                float dist = a.Position.DistanceTo(b.Position);

                if (dist < INTERACTION_DIST)
                    CheckEntityPairInteraction(a, b);
                else if (dist < a.VisionRange)
                    CheckVisionDetection(a, b);

                if (dist < b.VisionRange)
                    CheckVisionDetection(b, a);
            }
        }
    }

    private void CheckEntityPairInteraction(OverworldEntity a, OverworldEntity b)
    {
        if (a.Faction == b.Faction) return;
        if (!AreHostile(a, b)) return;

        var result = OverworldAIResolver.ResolveBattle(a, b);
        EmitSignal(SignalName.AiBattleOccurred, a, b, (bool)result["attacker_won"]);

        if ((bool)result["attacker_destroyed"]) a.IsAlive = false;
        if ((bool)result["defender_destroyed"]) b.IsAlive = false;

        // 败方逃跑
        if (!(bool)result["attacker_won"] && a.IsAlive)
        {
            a.CurrentAIState = OverworldEntity.AIState.Fleeing;
            StartMoveTo(a, a.HomePosition);
        }
        if ((bool)result["attacker_won"] && b.IsAlive)
        {
            b.CurrentAIState = OverworldEntity.AIState.Fleeing;
            StartMoveTo(b, b.HomePosition);
        }
    }

    private void CheckVisionDetection(OverworldEntity detector, OverworldEntity target)
    {
        if (!AreHostile(detector, target)) return;
        if (detector.CurrentAIState == OverworldEntity.AIState.Fleeing || detector.CurrentAIState == OverworldEntity.AIState.Returning)
            return;

        float powerRatio = detector.EvaluatePowerRatio(target);

        if (powerRatio > 1.5f)
        {
            if (detector.CurrentAIState == OverworldEntity.AIState.Idle || detector.CurrentAIState == OverworldEntity.AIState.Patrolling)
            {
                detector.CurrentAIState = OverworldEntity.AIState.Chasing;
                detector.ChaseTarget = target;
                detector.TargetPosition = target.Position;
                StartMoveTo(detector, target.Position);
            }
        }
        else if (powerRatio < 0.7f)
        {
            if (detector.CurrentAIState == OverworldEntity.AIState.Idle || detector.CurrentAIState == OverworldEntity.AIState.Patrolling)
            {
                detector.CurrentAIState = OverworldEntity.AIState.Fleeing;
                StartMoveTo(detector, detector.HomePosition);
            }
        }
    }

    private bool AreHostile(OverworldEntity a, OverworldEntity b)
    {
        if (a.Faction == b.Faction) return false;
        // 简单敌对判定
        if (a.Faction == "hostile" || b.Faction == "hostile") return true;
        return false; // TODO: 完善势力关系表
    }

    // ========================================
    // 围攻/回援逻辑
    // ========================================

    private void ProcessSieges()
    {
        var toResolve = new List<Tuple<OverworldEntity, OverworldPOI>>();
        foreach (var entity in Entities)
        {
            if (entity.IsAlive && entity.CurrentAIState == OverworldEntity.AIState.Besieging && entity.SiegeTarget != null)
            {
                if (entity.SiegeTarget.SiegeDays >= 2)
                    toResolve.Add(new Tuple<OverworldEntity, OverworldPOI>(entity, entity.SiegeTarget));
            }
        }

        foreach (var siege in toResolve)
        {
            var entity = siege.Item1;
            var target = siege.Item2;

            if (!entity.IsAlive) { target.EndSiege(); continue; }

            var result = OverworldAIResolver.ResolveSiege(entity, target);

            if ((bool)result["attacker_won"])
            {
                string oldFaction = target.OwningFaction;
                target.OwningFaction = entity.Faction;
                target.EndSiege();
                EmitSignal(SignalName.SiegeResolved, target, true, entity);
                EmitSignal(SignalName.PoiCaptured, target, entity.Faction, entity);

                entity.CurrentAIState = OverworldEntity.AIState.Idle;
                entity.GuardedPOI = target;
                entity.HomePosition = target.Position;
                entity.SiegeTarget = null;
            }
            else
            {
                target.EndSiege();
                EmitSignal(SignalName.SiegeResolved, target, false, entity);

                if ((bool)result["attacker_destroyed"]) entity.IsAlive = false;
                else
                {
                    entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
                    entity.SiegeTarget = null;
                    StartMoveTo(entity, entity.HomePosition);
                }
            }
        }
    }

    private void ProcessReinforcementChecks()
    {
        foreach (var poi in Pois)
        {
            if (!poi.NeedsReinforcement() || poi.OwningFaction != "kingdom") continue;

            OverworldEntity? nearestLord = null;
            float nearestDist = 99999.0f;

            foreach (var entity in Entities)
            {
                if (!entity.IsAlive || entity.EntityTypeEnum != OverworldEntity.EntityType.LordArmy || entity.Faction != "kingdom")
                    continue;

                if (entity.CurrentAIState == OverworldEntity.AIState.Besieging || entity.CurrentAIState == OverworldEntity.AIState.Reinforcing)
                    continue;

                float dist = entity.Position.DistanceTo(poi.Position);
                if (dist < nearestDist) { nearestDist = dist; nearestLord = entity; }
            }

            if (nearestLord != null && nearestDist < 800.0f)
            {
                nearestLord.CurrentAIState = OverworldEntity.AIState.Reinforcing;
                nearestLord.ReinforceTarget = poi;
                nearestLord.TargetPosition = poi.Position;
                StartMoveTo(nearestLord, poi.Position);
                GD.Print($"[回援] {nearestLord.EntityName} 前往支援 {poi.PoiName}");
            }
        }
    }

    private void ProcessRecruitment()
    {
        foreach (var entity in Entities)
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

    // ========================================
    // AI 决策入口
    // ========================================

    private void DecideDailyAction(OverworldEntity entity)
    {
        switch (entity.EntityTypeEnum)
        {
            case OverworldEntity.EntityType.Adventurer: DecideAdventurer(entity); break;
            case OverworldEntity.EntityType.RaidingParty: DecideRaidingParty(entity); break;
            case OverworldEntity.EntityType.Caravan: DecideCaravan(entity); break;
            case OverworldEntity.EntityType.EpicMonster: DecideEpicMonster(entity); break;
            case OverworldEntity.EntityType.LordArmy: DecideLordArmy(entity); break;
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
                else
                {
                    entity.ChaseTarget = null;
                    entity.CurrentAIState = OverworldEntity.AIState.Idle;
                }
                break;
        }
    }

    private void DecideRaidingParty(OverworldEntity entity)
    {
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.MovingToTarget:
                if (entity.IsMoving) return;
                RaidSettlementOrVillage(entity);
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
                else
                {
                    entity.ChaseTarget = null;
                    entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
                }
                break;
        }
    }

    private void RaidSettlementOrVillage(OverworldEntity entity)
    {
        foreach (var poi in Pois)
        {
            if (poi.Position.DistanceTo(entity.Position) < 150.0f)
            {
                if (poi.PoiTypeEnum == OverworldPOI.POIType.Village || poi.PoiTypeEnum == OverworldPOI.POIType.Town)
                {
                    var result = OverworldAIResolver.ResolveRaid(entity, poi);
                    EmitSignal(SignalName.VillageAttacked, poi, entity);
                    poi.OnAttacked(entity, _currentDay);
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
            var tmp = entity.OriginTown;
            entity.OriginTown = entity.DestinationTown;
            entity.DestinationTown = tmp;
            if (entity.DestinationTown != null)
            {
                entity.TargetPosition = entity.DestinationTown.Position;
                StartMoveTo(entity, entity.TargetPosition);
            }
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Idle)
        {
            if (entity.DestinationTown != null)
            {
                entity.TargetPosition = entity.DestinationTown.Position;
                StartMoveTo(entity, entity.TargetPosition);
                entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
            }
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing)
        {
            if (!entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Idle;
        }
    }

    private void DecideEpicMonster(OverworldEntity entity)
    {
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
                    if (entity.IsInTerritory(entity.ChaseTarget.Position))
                        StartMoveTo(entity, entity.ChaseTarget.Position);
                    else
                    {
                        entity.ChaseTarget = null;
                        entity.CurrentAIState = OverworldEntity.AIState.Idle;
                    }
                }
                else
                {
                    entity.ChaseTarget = null;
                    entity.CurrentAIState = OverworldEntity.AIState.Idle;
                }
                break;
        }
    }

    private OverworldEntity? FindIntruderInTerritory(OverworldEntity monster)
    {
        foreach (var entity in Entities)
        {
            if (entity == monster || !entity.IsAlive) continue;
            if (!monster.IsInTerritory(entity.Position)) continue;
            if (AreHostile(monster, entity)) return entity;
        }
        return null;
    }

    private void DecideLordArmy(OverworldEntity entity)
    {
        switch (entity.CurrentAIState)
        {
            case OverworldEntity.AIState.Reinforcing: DecideLordReinforcing(entity); return;
            case OverworldEntity.AIState.Besieging: DecideLordBesieging(entity); return;
            case OverworldEntity.AIState.Fleeing: if (!entity.IsMoving) entity.CurrentAIState = OverworldEntity.AIState.Idle; return;
            case OverworldEntity.AIState.Chasing: DecideLordChasing(entity); return;
            case OverworldEntity.AIState.Recruiting: DecideLordRecruiting(entity); return;
        }

        // 回防检查
        var threatened = FindThreatenedFriendlyPoi(entity);
        if (threatened != null)
        {
            entity.CurrentAIState = OverworldEntity.AIState.Reinforcing;
            entity.ReinforceTarget = threatened;
            StartMoveTo(entity, threatened.Position);
            return;
        }

        // 攻击检查
        if (ShouldLordAttack(entity))
        {
            var target = FindAttackTarget(entity);
            if (target != null)
            {
                entity.CurrentAIState = OverworldEntity.AIState.Besieging;
                entity.SiegeTarget = target;
                target.BeginSiege(entity);
                StartMoveTo(entity, target.Position);
                EmitSignal(SignalName.SiegeStarted, target, entity);
                return;
            }
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
        else if (!entity.IsMoving)
        {
            entity.CurrentAIState = OverworldEntity.AIState.Idle;
        }
    }

    private void DecideLordReinforcing(OverworldEntity entity)
    {
        if (entity.IsMoving) return;
        if (entity.ReinforceTarget != null)
        {
            float d = entity.Position.DistanceTo(entity.ReinforceTarget.Position);
            if (d < SIEGE_APPROACH_DIST)
            {
                EmitSignal(SignalName.ReinforcementArrived, entity.ReinforceTarget, entity);
                if (entity.ReinforceTarget.IsUnderSiege && entity.ReinforceTarget.SiegeBy != null)
                {
                    var besieger = entity.ReinforceTarget.SiegeBy;
                    var result = OverworldAIResolver.ResolveBattle(entity, besieger);
                    EmitSignal(SignalName.AiBattleOccurred, entity, besieger, (bool)result["attacker_won"]);

                    if ((bool)result["defender_destroyed"])
                    {
                        besieger.IsAlive = false;
                        entity.ReinforceTarget.EndSiege();
                    }
                    else if ((bool)result["attacker_destroyed"])
                    {
                        entity.IsAlive = false;
                    }
                    else if (besieger.CombatPower < entity.CombatPower * 0.5f)
                    {
                        besieger.CurrentAIState = OverworldEntity.AIState.Fleeing;
                        StartMoveTo(besieger, besieger.HomePosition);
                        entity.ReinforceTarget.EndSiege();
                    }
                }
                entity.GuardedPOI = entity.ReinforceTarget;
                entity.ReinforceTarget = null;
            }
        }
        entity.CurrentAIState = OverworldEntity.AIState.Idle;
    }

    private void DecideLordBesieging(OverworldEntity entity)
    {
        if (entity.SiegeTarget == null || !entity.SiegeTarget.IsUnderSiege)
        {
            entity.SiegeTarget = null;
            entity.CurrentAIState = OverworldEntity.AIState.Idle;
        }
    }

    private void DecideLordChasing(OverworldEntity entity)
    {
        if (entity.ChaseTarget != null && GodotObject.IsInstanceValid(entity.ChaseTarget) && entity.ChaseTarget.IsAlive)
        {
            if (entity.Position.DistanceTo(entity.ChaseTarget.Position) > entity.VisionRange * 1.5f)
            {
                entity.ChaseTarget = null;
                entity.CurrentAIState = OverworldEntity.AIState.Idle;
            }
            else StartMoveTo(entity, entity.ChaseTarget.Position);
        }
        else { entity.ChaseTarget = null; entity.CurrentAIState = OverworldEntity.AIState.Idle; }
    }

    private void DecideLordRecruiting(OverworldEntity entity)
    {
        if (entity.DaysAlive % 5 == 0) entity.CurrentAIState = OverworldEntity.AIState.Idle;
    }

    private bool ShouldLordAttack(OverworldEntity entity)
    {
        return entity.LordPersonalityValue switch
        {
            OverworldPOI.LordPersonality.Aggressive => entity.CombatPower > 15.0f,
            OverworldPOI.LordPersonality.Balanced => entity.CombatPower > 25.0f,
            OverworldPOI.LordPersonality.Cautious => entity.CombatPower > 40.0f,
            _ => entity.CombatPower > 25.0f
        };
    }

    private OverworldPOI? FindThreatenedFriendlyPoi(OverworldEntity entity)
    {
        OverworldPOI? closest = null;
        float closestDist = 99999.0f;
        foreach (var poi in Pois)
        {
            if (poi.OwningFaction != entity.Faction) continue;
            if (!poi.IsUnderSiege && !poi.NeedsReinforcement()) continue;

            float d = entity.Position.DistanceTo(poi.Position);
            float maxD = entity.LordPersonalityValue switch
            {
                OverworldPOI.LordPersonality.Cautious => 400.0f,
                OverworldPOI.LordPersonality.Aggressive => 900.0f,
                _ => 600.0f
            };

            if (d < Math.Min(closestDist, maxD)) { closestDist = d; closest = poi; }
        }
        return closest;
    }

    private OverworldPOI? FindAttackTarget(OverworldEntity entity)
    {
        OverworldPOI? closest = null;
        float closestDist = 99999.0f;
        const float MAX_ATTACK_DIST = 2000.0f;

        foreach (var poi in Pois)
        {
            if (poi.OwningFaction == entity.Faction) continue;
            if (poi.PoiTypeEnum != OverworldPOI.POIType.Settlement && poi.PoiTypeEnum != OverworldPOI.POIType.Lair) continue;
            if (poi.IsUnderSiege) continue;

            float d = entity.Position.DistanceTo(poi.Position);
            if (d > MAX_ATTACK_DIST) continue;

            float defPower = poi.GetDefensePower();
            if (defPower > entity.CombatPower * 0.8f)
            {
                if (entity.LordPersonalityValue == OverworldPOI.LordPersonality.Cautious) continue;
                if (entity.LordPersonalityValue == OverworldPOI.LordPersonality.Aggressive && defPower > entity.CombatPower * 1.2f) continue;
                if (entity.LordPersonalityValue == OverworldPOI.LordPersonality.Balanced && defPower > entity.CombatPower) continue;
            }

            if (d < closestDist) { closestDist = d; closest = poi; }
        }
        return closest;
    }

    // ========================================
    // 辅助方法
    // ========================================

    private void StartMoveTo(OverworldEntity entity, Vector2 target)
    {
        if (_hexGrid == null || _hexAstar == null) return;
        var newPath = _hexAstar.FindPathPixels(entity.Position, target);
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
    }

    private void RemoveEntity(OverworldEntity entity)
    {
        Entities.Remove(entity);
        entity.IsAlive = false;
        EmitSignal(SignalName.EntityRemoved, entity);
    }

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

    private void SpawnNewRaidingParties()
    {
        // TODO: 等待 Phase 5.2 WorldGenerator 迁移后完善集成
        // 这里需要调用 WorldGenerator 的私有方法，可能需要通过工厂模式重构
    }

    public OverworldEntity? CheckPlayerEncounters(Vector2 playerPos)
    {
        OverworldEntity? closest = null;
        float closestDist = 700.0f;
        foreach (var entity in Entities)
        {
            if (!entity.IsAlive || entity.EntityTypeEnum == OverworldEntity.EntityType.Caravan) continue;
            float d = playerPos.DistanceTo(entity.Position);
            if (d < closestDist) { closestDist = d; closest = entity; }
        }
        return closest;
    }

    public OverworldPOI? CheckPlayerPoiEnter(Vector2 playerPos)
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

    public List<OverworldEntity> GetVisibleEntities(Vector2 playerPos, float visionRange)
    {
        var visible = new List<OverworldEntity>();
        foreach (var entity in Entities)
            if (entity.IsAlive && playerPos.DistanceTo(entity.Position) <= visionRange) visible.Add(entity);
        return visible;
    }

    public List<OverworldPOI> GetVisiblePois(Vector2 playerPos, float visionRange)
    {
        var visible = new List<OverworldPOI>();
        foreach (var poi in Pois)
            if (playerPos.DistanceTo(poi.Position) <= visionRange) visible.Add(poi);
        return visible;
    }
}
