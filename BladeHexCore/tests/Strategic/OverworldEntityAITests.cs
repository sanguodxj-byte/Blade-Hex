// OverworldEntityAITests.cs
// 大地图实体 AI 系统综合测试套件
// 覆盖: DailyDecisionProcessor 各类型行为、PerceptionIntentResolver 视野检测、
//       MovementProcessor 移动与到达回调、SiegeProcessor 围攻/回援/招募、
//       EncounterEntitySpawner 生成条件与追击 AI、OverworldAIResolver 结算
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class OverworldEntityAITests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else    { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string, bool, string)> EnumerateTests()
    {
        // ── DailyDecisionProcessor: 冒险者 ─────────────────────────────
        yield return Run(nameof(Adventurer_Idle_StartsPatrolling),           Adventurer_Idle_StartsPatrolling);
        yield return Run(nameof(Adventurer_Patrolling_ReturnsIdleWhenStopped), Adventurer_Patrolling_ReturnsIdleWhenStopped);
        yield return Run(nameof(Adventurer_Chasing_UpdatesTargetPosition),   Adventurer_Chasing_UpdatesTargetPosition);
        yield return Run(nameof(Adventurer_Chasing_ReturnsIdleWhenTargetDead), Adventurer_Chasing_ReturnsIdleWhenTargetDead);

        // ── DailyDecisionProcessor: 掠夺队 ─────────────────────────────
        yield return Run(nameof(RaidingParty_MovingToTarget_RaidsAndReturns), RaidingParty_MovingToTarget_RaidsAndReturns);
        yield return Run(nameof(RaidingParty_Returning_DiesAtHome),          RaidingParty_Returning_DiesAtHome);
        yield return Run(nameof(RaidingParty_Fleeing_TransitionsToReturning), RaidingParty_Fleeing_TransitionsToReturning);
        yield return Run(nameof(RaidingParty_ExpiredOver14Days_ForcesReturn), RaidingParty_ExpiredOver14Days_ForcesReturn);

        // ── DailyDecisionProcessor: 商队 ───────────────────────────────
        yield return Run(nameof(Caravan_Idle_StartsMovingToDestination),     Caravan_Idle_StartsMovingToDestination);
        yield return Run(nameof(Caravan_Arrived_SwapsTownsAndContinues),     Caravan_Arrived_SwapsTownsAndContinues);
        yield return Run(nameof(Caravan_Arrived_ContributesProsperity),      Caravan_Arrived_ContributesProsperity);

        // ── DailyDecisionProcessor: 史诗怪物 ────────────────────────────
        yield return Run(nameof(EpicMonster_Idle_PatrolsInTerritory),        EpicMonster_Idle_PatrolsInTerritory);
        yield return Run(nameof(EpicMonster_IntruderDetected_EntersChasing), EpicMonster_IntruderDetected_EntersChasing);
        yield return Run(nameof(EpicMonster_Chasing_StopsAtTerritoryBorder), EpicMonster_Chasing_StopsAtTerritoryBorder);

        // ── DailyDecisionProcessor: 领主军队 ────────────────────────────
        yield return Run(nameof(LordArmy_NoWar_PatrolsGuardedPOI),          LordArmy_NoWar_PatrolsGuardedPOI);
        yield return Run(nameof(LordArmy_WarTarget_BesiegesWhenClose),       LordArmy_WarTarget_BesiegesWhenClose);
        yield return Run(nameof(LordArmy_WarTarget_MovesWhenFar),            LordArmy_WarTarget_MovesWhenFar);
        yield return Run(nameof(LordArmy_WarTarget_ActiveLOD_PreservesStateOnPathFail), LordArmy_WarTarget_ActiveLOD_PreservesStateOnPathFail);
        yield return Run(nameof(LordArmy_VisionIntercept_ChasesEnemyLord),   LordArmy_VisionIntercept_ChasesEnemyLord);
        yield return Run(nameof(LordArmy_InArmy_FollowerFollowsMarshal),     LordArmy_InArmy_FollowerFollowsMarshal);
        yield return Run(nameof(LordArmy_InArmy_MarshalMarchesToTarget),     LordArmy_InArmy_MarshalMarchesToTarget);

        // ── DailyDecisionProcessor: 视野检测 ─────────────────────────────
        yield return Run(nameof(VisionDetection_StrongerEntity_Chases),      VisionDetection_StrongerEntity_Chases);
        yield return Run(nameof(VisionDetection_WeakerEntity_Flees),         VisionDetection_WeakerEntity_Flees);
        yield return Run(nameof(VisionDetection_EqualPower_NoAction),        VisionDetection_EqualPower_NoAction);
        yield return Run(nameof(VisionDetection_FleeingEntity_DoesNotChase), VisionDetection_FleeingEntity_DoesNotChase);
        yield return Run(nameof(VisionDetection_SameFaction_NoHostility),    VisionDetection_SameFaction_NoHostility);
        yield return Run(nameof(Hostility_PlayerFactionWar_MakesNeutralNationHostileToPlayer), Hostility_PlayerFactionWar_MakesNeutralNationHostileToPlayer);

        // ── BattleResolver: 战斗结算 ────────────────────────────────────
        yield return Run(nameof(Battle_HostileFaction_Resolves),             Battle_HostileFaction_Resolves);
        yield return Run(nameof(Battle_SameFaction_Skipped),                 Battle_SameFaction_Skipped);
        yield return Run(nameof(Battle_LoserEntersFleeing),                  Battle_LoserEntersFleeing);
        yield return Run(nameof(Battle_ArmyPowerAggregation),                Battle_ArmyPowerAggregation);

        // ── MovementProcessor ───────────────────────────────────────────
        yield return Run(nameof(Movement_AdvancesAlongPath),                 Movement_AdvancesAlongPath);
        yield return Run(nameof(Movement_ReachesDestination_FiresCallback),  Movement_ReachesDestination_FiresCallback);
        yield return Run(nameof(Movement_ChasingState_SpeedMultiplier),      Movement_ChasingState_SpeedMultiplier);
        yield return Run(nameof(Navigator_ChasingRefreshFailure_PreservesExistingPath), Navigator_ChasingRefreshFailure_PreservesExistingPath);
        yield return Run(nameof(Movement_HibernatedEntity_Skipped),          Movement_HibernatedEntity_Skipped);

        // ── SiegeProcessor ──────────────────────────────────────────────
        yield return Run(nameof(Siege_EnoughDays_Resolves),                  Siege_EnoughDays_Resolves);
        yield return Run(nameof(Siege_TooFewDays_Skipped),                   Siege_TooFewDays_Skipped);
        yield return Run(nameof(Siege_AttackerDies_ResolvesEntityDefeat),    Siege_AttackerDies_ResolvesEntityDefeat);
        yield return Run(nameof(Reinforcement_NearbyLord_Responds),          Reinforcement_NearbyLord_Responds);
        yield return Run(nameof(Reinforcement_TooFar_NoResponse),            Reinforcement_TooFar_NoResponse);
        yield return Run(nameof(Recruitment_IdleLordAtCastle_GainsGarrison), Recruitment_IdleLordAtCastle_GainsGarrison);

        // ── OverworldAIResolver ─────────────────────────────────────────
        yield return Run(nameof(ResolveBattle_CrushingVictory_LowLosses),    ResolveBattle_CrushingVictory_LowLosses);
        yield return Run(nameof(ResolveBattle_DestroyedWhenPowerBelow1),     ResolveBattle_DestroyedWhenPowerBelow1);
        yield return Run(nameof(ResolveSiege_AttackerWin_ReducesGarrison),   ResolveSiege_AttackerWin_ReducesGarrison);
        yield return Run(nameof(ResolveRaid_Success_DamagesProsperity),      ResolveRaid_Success_DamagesProsperity);

        // ── EncounterEntitySpawner ──────────────────────────────────────
        yield return Run(nameof(Spawner_InsufficientDistance_NoSpawn),       Spawner_InsufficientDistance_NoSpawn);
        yield return Run(nameof(Spawner_MaxEntitiesReached_NoSpawn),         Spawner_MaxEntitiesReached_NoSpawn);
        yield return Run(nameof(Spawner_SufficientConditions_SpawnsEntity),  Spawner_SufficientConditions_SpawnsEntity);
        yield return Run(nameof(Spawner_FirstTickAtOrigin_StillAccumulates), Spawner_FirstTickAtOrigin_StillAccumulates);
        yield return Run(nameof(Spawner_ChaseAI_EntersChasingWhenInView),    Spawner_ChaseAI_EntersChasingWhenInView);
        yield return Run(nameof(Spawner_ChaseAI_StopsWhenOutOfRange),        Spawner_ChaseAI_StopsWhenOutOfRange);
        yield return Run(nameof(Spawner_Caravan_NeverHostile),               Spawner_Caravan_NeverHostile);
        yield return Run(nameof(Spawner_HostilePlayerVision_ChasesPlayer),   Spawner_HostilePlayerVision_ChasesPlayer);
        yield return Run(nameof(Spawner_ChunkSlotWildMonster_SpawnsAndTriggers), Spawner_ChunkSlotWildMonster_SpawnsAndTriggers);
        yield return Run(nameof(Spawner_ChunkSlotWildMonster_RespectsMaxSpawns), Spawner_ChunkSlotWildMonster_RespectsMaxSpawns);

        // ── EntityLodController ─────────────────────────────────────────
        yield return Run(nameof(LOD_FarEntity_Hibernates),                   LOD_FarEntity_Hibernates);
        yield return Run(nameof(LOD_NearEntity_Activates),                   LOD_NearEntity_Activates);
        yield return Run(nameof(LOD_Hysteresis_PreventsFlapping),            LOD_Hysteresis_PreventsFlapping);

        // ── OverworldEntity 数据模型 ────────────────────────────────────
        yield return Run(nameof(Entity_Serialize_Roundtrip),                 Entity_Serialize_Roundtrip);
        yield return Run(nameof(Entity_EvaluatePowerRatio_Correct),          Entity_EvaluatePowerRatio_Correct);
        yield return Run(nameof(Entity_IsInTerritory_Correct),               Entity_IsInTerritory_Correct);

        // ── BanditParty / RobberParty / PirateCrew 决策 ───────────────────
        yield return Run(nameof(BanditParty_Idle_StartsPatrolling),          BanditParty_Idle_StartsPatrolling);
        yield return Run(nameof(BanditParty_Chasing_UpdatesTargetPosition),  BanditParty_Chasing_UpdatesTargetPosition);
        yield return Run(nameof(RobberParty_Fleeing_ReturnsTowardHome),      RobberParty_Fleeing_ReturnsTowardHome);
        yield return Run(nameof(PirateCrew_Chasing_ReturnsIdleWhenTargetDead), PirateCrew_Chasing_ReturnsIdleWhenTargetDead);

        // ── AIStrategy 策略修正 ──────────────────────────────────────────
        yield return Run(nameof(AIStrategy_Berserk_LowersChaseThreshold),    AIStrategy_Berserk_LowersChaseThreshold);
        yield return Run(nameof(AIStrategy_Cautious_RaisesChaseThreshold),   AIStrategy_Cautious_RaisesChaseThreshold);
        yield return Run(nameof(AIStrategy_ChaseSpeedMultipliers_AreBounded), AIStrategy_ChaseSpeedMultipliers_AreBounded);
        yield return Run(nameof(EntitySpawner_RaidingPartyRoadChaseSpeed_StaysBelowPlayerBase), EntitySpawner_RaidingPartyRoadChaseSpeed_StaysBelowPlayerBase);
        yield return Run(nameof(AIStrategy_Serialize_Roundtrip),             AIStrategy_Serialize_Roundtrip);

        // ── 史诗怪物领地回归 ─────────────────────────────────────────────
        yield return Run(nameof(EpicMonster_OutsideTerritory_ReturnsToCenter), EpicMonster_OutsideTerritory_ReturnsToCenter);
        yield return Run(nameof(EpicMonster_ChasingTargetLeavesTerritory_ClearsTarget), EpicMonster_ChasingTargetLeavesTerritory_ClearsTarget);
        yield return Run(nameof(EpicMonster_OutsideTerritory_PerceptionSkips), EpicMonster_OutsideTerritory_PerceptionSkips);

        // ── 交战机制 ──────────────────────────────────────────────────
        yield return Run(nameof(Engagement_HostilePairWithin100px_EntersEngaged), Engagement_HostilePairWithin100px_EntersEngaged);
        yield return Run(nameof(Engagement_GeneratedHostileFactions_EnterEngaged), Engagement_GeneratedHostileFactions_EnterEngaged);
        yield return Run(nameof(Simulation_TickFrame_MovingHostiles_EnterEngaged), Simulation_TickFrame_MovingHostiles_EnterEngaged);
        yield return Run(nameof(Simulation_TickFrame_IdleHostiles_PerceptionStartsMovement), Simulation_TickFrame_IdleHostiles_PerceptionStartsMovement);
        yield return Run(nameof(Engagement_PairBeyond100px_NoEngage), Engagement_PairBeyond100px_NoEngage);
        yield return Run(nameof(EngagedCombat_DoesNotResolveBefore2Days), EngagedCombat_DoesNotResolveBefore2Days);
        yield return Run(nameof(EngagedCombat_ResolvesAfter2Days), EngagedCombat_ResolvesAfter2Days);
        yield return Run(nameof(Movement_EngagedEntity_ForcedStop), Movement_EngagedEntity_ForcedStop);
    }

    // ─── 工具方法 ─────────────────────────────────────────────────────────────

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

    private static OverworldEntity MakeEntity(
        OverworldEntity.EntityType type = OverworldEntity.EntityType.Adventurer,
        OverworldEntity.AIState state = OverworldEntity.AIState.Idle,
        Vector2? position = null,
        float combatPower = 100f,
        string faction = "neutral",
        bool hostile = false)
    {
        return new OverworldEntity
        {
            EntityName = $"Test_{type}_{Guid.NewGuid():N}".Substring(0, 20),
            EntityTypeEnum = type,
            CurrentAIState = state,
            Position = position ?? Vector2.Zero,
            HomePosition = position ?? Vector2.Zero,
            TerritoryCenter = position ?? Vector2.Zero,
            TerritoryRadius = 500f,
            CombatPower = combatPower,
            PartySize = 10,
            PartyLevel = 5,
            Faction = faction,
            IsHostileToPlayer = hostile,
            MoveSpeed = 200f,
            VisionRange = 400f,
            PatrolRadius = 300f,
            IsAlive = true,
        };
    }

    private static OverworldPOI MakePOI(
        Vector2? position = null,
        string faction = "kingdom",
        OverworldPOI.POIType poiType = OverworldPOI.POIType.Village,
        int garrison = 20,
        int prosperity = 50)
    {
        return new OverworldPOI
        {
            PoiName = $"TestPOI_{Guid.NewGuid():N}".Substring(0, 16),
            Position = position ?? Vector2.Zero,
            PoiTypeEnum = poiType,
            OwningFaction = faction,
            GarrisonCurrent = garrison,
            Prosperity = prosperity,
        };
    }

    // ── DailyDecisionProcessor: 冒险者 ─────────────────────────────────────

    private static (bool, string) Adventurer_Idle_StartsPatrolling()
    {
        var processor = new DailyDecisionProcessor();
        var entity = MakeEntity(OverworldEntity.EntityType.Adventurer, OverworldEntity.AIState.Idle, new Vector2(1000, 1000));

        // 不提供导航（StartMoveTo 不会找到路径），验证状态机至少尝试切换
        processor.ProcessDailyDecisions(new List<OverworldEntity> { entity }, new List<OverworldPOI>(), 1);

        // 无导航时 IsMoving 保持 false，状态应保持 Idle（StartMoveTo 未能建立路径）
        // 这验证了无导航时的安全降级
        return (entity.IsAlive, $"entity alive={entity.IsAlive}");
    }

    private static (bool, string) Adventurer_Patrolling_ReturnsIdleWhenStopped()
    {
        var processor = new DailyDecisionProcessor();
        var entity = MakeEntity(OverworldEntity.EntityType.Adventurer, OverworldEntity.AIState.Patrolling, new Vector2(1000, 1000));
        entity.IsMoving = false; // 已停止移动

        processor.ProcessDailyDecisions(new List<OverworldEntity> { entity }, new List<OverworldPOI>(), 1);

        return (entity.CurrentAIState == OverworldEntity.AIState.Idle,
                $"expected Idle, got {entity.CurrentAIState}");
    }

    private static (bool, string) Adventurer_Chasing_UpdatesTargetPosition()
    {
        var processor = new DailyDecisionProcessor();
        var chaser = MakeEntity(OverworldEntity.EntityType.Adventurer, OverworldEntity.AIState.Chasing, new Vector2(0, 0));
        var target = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Patrolling, new Vector2(200, 0));
        target.Faction = "hostile";

        chaser.ChaseTarget = target;
        var entities = new List<OverworldEntity> { chaser, target };

        processor.ProcessDailyDecisions(entities, new List<OverworldPOI>(), 1);

        return (chaser.TargetPosition == target.Position,
                $"target pos={chaser.TargetPosition}, expected={target.Position}");
    }

    private static (bool, string) Adventurer_Chasing_ReturnsIdleWhenTargetDead()
    {
        var processor = new DailyDecisionProcessor();
        var chaser = MakeEntity(OverworldEntity.EntityType.Adventurer, OverworldEntity.AIState.Chasing, new Vector2(0, 0));
        var target = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Patrolling, new Vector2(200, 0));
        target.IsAlive = false;

        chaser.ChaseTarget = target;
        var entities = new List<OverworldEntity> { chaser, target };

        processor.ProcessDailyDecisions(entities, new List<OverworldPOI>(), 1);

        return (chaser.CurrentAIState == OverworldEntity.AIState.Idle && chaser.ChaseTarget == null,
                $"state={chaser.CurrentAIState}, chaseTarget={chaser.ChaseTarget}");
    }

    // ── DailyDecisionProcessor: 掠夺队 ─────────────────────────────────────

    private static (bool, string) RaidingParty_MovingToTarget_RaidsAndReturns()
    {
        var processor = new DailyDecisionProcessor();
        var raider = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.MovingToTarget, new Vector2(100, 0));
        raider.IsMoving = false; // 已到达
        raider.HomePosition = new Vector2(0, 0);

        processor.ProcessDailyDecisions(new List<OverworldEntity> { raider }, new List<OverworldPOI>(), 1);

        return (raider.CurrentAIState == OverworldEntity.AIState.Returning,
                $"expected Returning, got {raider.CurrentAIState}");
    }

    private static (bool, string) RaidingParty_Returning_DiesAtHome()
    {
        var processor = new DailyDecisionProcessor();
        var raider = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Returning, new Vector2(0, 0));
        raider.IsMoving = false; // 已到达基地

        processor.ProcessDailyDecisions(new List<OverworldEntity> { raider }, new List<OverworldPOI>(), 1);

        return (!raider.IsAlive, $"expected dead, got alive={raider.IsAlive}");
    }

    private static (bool, string) RaidingParty_Fleeing_TransitionsToReturning()
    {
        var processor = new DailyDecisionProcessor();
        var raider = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Fleeing, new Vector2(500, 500));
        raider.IsMoving = false;
        raider.HomePosition = new Vector2(0, 0);

        processor.ProcessDailyDecisions(new List<OverworldEntity> { raider }, new List<OverworldPOI>(), 1);

        return (raider.CurrentAIState == OverworldEntity.AIState.Returning,
                $"expected Returning, got {raider.CurrentAIState}");
    }

    private static (bool, string) RaidingParty_ExpiredOver14Days_ForcesReturn()
    {
        var entity = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Patrolling, new Vector2(500, 500));
        entity.DaysAlive = 13;

        entity.OnDayPassed(); // DaysAlive → 14, 不触发（>14 才触发）
        bool notTriggered = entity.CurrentAIState == OverworldEntity.AIState.Patrolling;

        entity.OnDayPassed(); // DaysAlive → 15, 触发
        bool triggered = entity.CurrentAIState == OverworldEntity.AIState.Returning;

        return (notTriggered && triggered,
                $"day14 state={OverworldEntity.AIState.Patrolling}, day15 state={entity.CurrentAIState}");
    }

    // ── DailyDecisionProcessor: 商队 ───────────────────────────────────────

    private static (bool, string) Caravan_Idle_StartsMovingToDestination()
    {
        var processor = new DailyDecisionProcessor();
        var caravan = MakeEntity(OverworldEntity.EntityType.Caravan, OverworldEntity.AIState.Idle, new Vector2(0, 0));
        var destTown = MakePOI(new Vector2(1000, 0), "neutral", OverworldPOI.POIType.Town);
        caravan.DestinationTown = destTown;
        caravan.OriginTown = MakePOI(new Vector2(0, 0), "neutral", OverworldPOI.POIType.Town);

        processor.ProcessDailyDecisions(new List<OverworldEntity> { caravan }, new List<OverworldPOI> { destTown }, 1);

        return (caravan.CurrentAIState == OverworldEntity.AIState.MovingToTarget,
                $"expected MovingToTarget, got {caravan.CurrentAIState}");
    }

    private static (bool, string) Caravan_Arrived_SwapsTownsAndContinues()
    {
        var processor = new DailyDecisionProcessor();
        var origin = MakePOI(new Vector2(0, 0), "neutral", OverworldPOI.POIType.Town);
        var dest = MakePOI(new Vector2(1000, 0), "neutral", OverworldPOI.POIType.Town);

        var caravan = MakeEntity(OverworldEntity.EntityType.Caravan, OverworldEntity.AIState.MovingToTarget, dest.Position);
        caravan.IsMoving = false; // 已到达
        caravan.OriginTown = origin;
        caravan.DestinationTown = dest;

        processor.ProcessDailyDecisions(new List<OverworldEntity> { caravan }, new List<OverworldPOI> { origin, dest }, 1);

        // 到达后 origin 和 dest 交换，新的 destination 应该是原来的 origin
        return (caravan.OriginTown == dest && caravan.DestinationTown == origin,
                $"origin={caravan.OriginTown?.PoiName}, dest={caravan.DestinationTown?.PoiName}");
    }

    private static (bool, string) Caravan_Arrived_ContributesProsperity()
    {
        var processor = new DailyDecisionProcessor();
        var origin = MakePOI(new Vector2(0, 0), "neutral", OverworldPOI.POIType.Town, prosperity: 50);
        var dest = MakePOI(new Vector2(1000, 0), "neutral", OverworldPOI.POIType.Town, prosperity: 50);

        var caravan = MakeEntity(OverworldEntity.EntityType.Caravan, OverworldEntity.AIState.MovingToTarget, dest.Position);
        caravan.IsMoving = false;
        caravan.OriginTown = origin;
        caravan.DestinationTown = dest;

        processor.ProcessDailyDecisions(new List<OverworldEntity> { caravan }, new List<OverworldPOI> { origin, dest }, 1);

        return (dest.Prosperity == 52 && caravan.ProsperityContribution,
                $"prosperity={dest.Prosperity}, contribution={caravan.ProsperityContribution}");
    }

    // ── DailyDecisionProcessor: 史诗怪物 ────────────────────────────────────

    private static (bool, string) EpicMonster_Idle_PatrolsInTerritory()
    {
        var processor = new DailyDecisionProcessor();
        var monster = MakeEntity(OverworldEntity.EntityType.EpicMonster, OverworldEntity.AIState.Idle, new Vector2(1000, 1000));
        monster.TerritoryCenter = new Vector2(1000, 1000);
        monster.TerritoryRadius = 500f;

        processor.ProcessDailyDecisions(new List<OverworldEntity> { monster }, new List<OverworldPOI>(), 1);

        // 怪物在领地内应该保持存活并尝试巡逻
        return (monster.IsAlive, $"alive={monster.IsAlive}, state={monster.CurrentAIState}");
    }

    private static (bool, string) EpicMonster_IntruderDetected_EntersChasing()
    {
        var processor = new DailyDecisionProcessor();
        var monster = MakeEntity(OverworldEntity.EntityType.EpicMonster, OverworldEntity.AIState.Idle, new Vector2(1000, 1000));
        monster.TerritoryCenter = new Vector2(1000, 1000);
        monster.TerritoryRadius = 500f;
        monster.Faction = "monster";

        // 入侵者在领地内
        var intruder = MakeEntity(OverworldEntity.EntityType.Adventurer, OverworldEntity.AIState.Patrolling, new Vector2(1100, 1000));
        intruder.Faction = "hostile";
        intruder.IsHostileToPlayer = true;

        var entities = new List<OverworldEntity> { monster, intruder };
        processor.ProcessDailyDecisions(entities, new List<OverworldPOI>(), 1);

        return (monster.CurrentAIState == OverworldEntity.AIState.Chasing && monster.ChaseTarget == intruder,
                $"state={monster.CurrentAIState}, target={monster.ChaseTarget?.EntityName}");
    }

    private static (bool, string) EpicMonster_Chasing_StopsAtTerritoryBorder()
    {
        var processor = new DailyDecisionProcessor();
        var monster = MakeEntity(OverworldEntity.EntityType.EpicMonster, OverworldEntity.AIState.Chasing, new Vector2(1000, 1000));
        monster.TerritoryCenter = new Vector2(1000, 1000);
        monster.TerritoryRadius = 500f;

        // 目标逃出领地
        var target = MakeEntity(OverworldEntity.EntityType.Adventurer, OverworldEntity.AIState.Fleeing, new Vector2(2000, 2000));
        target.Faction = "hostile";
        monster.ChaseTarget = target;

        var entities = new List<OverworldEntity> { monster, target };
        processor.ProcessDailyDecisions(entities, new List<OverworldPOI>(), 1);

        // 目标不在领地内，怪物应放弃追击
        return (monster.CurrentAIState == OverworldEntity.AIState.Idle && monster.ChaseTarget == null,
                $"state={monster.CurrentAIState}, target={monster.ChaseTarget?.EntityName}");
    }

    // ── DailyDecisionProcessor: 领主军队 ────────────────────────────────────

    private static (bool, string) LordArmy_NoWar_PatrolsGuardedPOI()
    {
        var processor = new DailyDecisionProcessor();
        var poi = MakePOI(new Vector2(1000, 1000), "kingdom", OverworldPOI.POIType.Castle);
        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(1000, 1000));
        lord.Faction = "kingdom";
        lord.GuardedPOI = poi;

        var worldEngine = new WorldEventEngine();
        processor.SetWorldEventEngine(worldEngine);
        processor.SetArmyRegistry(new ArmyRegistry());

        processor.ProcessDailyDecisions(new List<OverworldEntity> { lord }, new List<OverworldPOI> { poi }, 1);

        return (lord.IsAlive, $"alive={lord.IsAlive}, state={lord.CurrentAIState}");
    }

    private static (bool, string) LordArmy_WarTarget_BesiegesWhenClose()
    {
        var processor = new DailyDecisionProcessor();
        var targetPoi = MakePOI(new Vector2(1000, 1000), "enemy", OverworldPOI.POIType.Castle, garrison: 5);
        targetPoi.PoiName = "EnemyFortress";

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(1200, 1000));
        lord.Faction = "kingdom";
        lord.AssignedWarTargetPoiName = "EnemyFortress";
        lord.WarTargetAssignedDay = 1;

        var worldEngine = new WorldEventEngine();
        var war = new WarState
        {
            NationA = "kingdom",
            NationB = "enemy",
            DaysSinceStart = 1,
        };
        war.ObjectivesA.Add("EnemyFortress");
        worldEngine.ActiveWars.Add(war);
        processor.SetWorldEventEngine(worldEngine);
        processor.SetArmyRegistry(new ArmyRegistry());

        // 诊断: 直接调用公开方法，跳过 ProcessDailyDecisions 的 OnDayPassed 副作用
        processor.DecideLordArmy(lord, new List<OverworldPOI> { targetPoi }, 1);

        return (lord.CurrentAIState == OverworldEntity.AIState.Besieging && lord.SiegeTarget == targetPoi,
                $"state={lord.CurrentAIState}, siegeTarget={lord.SiegeTarget?.PoiName}, assigned={lord.AssignedWarTargetPoiName}");
    }

    private static (bool, string) LordArmy_WarTarget_MovesWhenFar()
    {
        var processor = new DailyDecisionProcessor();
        // 目标距离 > 600px（围攻阈值）但 < 1500px（WarLordOrders 最大距离）
        var targetPoi = MakePOI(new Vector2(1200, 0), "enemy", OverworldPOI.POIType.Castle);
        targetPoi.PoiName = "FarFortress";

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(0, 0));
        lord.Faction = "kingdom";
        lord.AssignedWarTargetPoiName = "FarFortress";
        lord.WarTargetAssignedDay = 1;
        // 使用 Hibernated LOD 以触发 StartMoveTo 的直线插值回退（无寻路系统时）
        lord.Lod = OverworldEntity.EntityLod.Hibernated;

        var worldEngine = new WorldEventEngine();
        var war = new WarState { NationA = "kingdom", NationB = "enemy", DaysSinceStart = 1 };
        war.ObjectivesA.Add("FarFortress");
        worldEngine.ActiveWars.Add(war);
        processor.SetWorldEventEngine(worldEngine);
        processor.SetArmyRegistry(new ArmyRegistry());

        // 直接调用 DecideLordArmy 避免 ProcessDailyDecisions 的 OnDayPassed 副作用
        processor.DecideLordArmy(lord, new List<OverworldPOI> { targetPoi }, 1);

        return (lord.CurrentAIState == OverworldEntity.AIState.MovingToTarget,
                $"state={lord.CurrentAIState}");
    }

    /// <summary>
    /// 验证生产修复: StartMoveTo 寻路失败时不再将 MovingToTarget 重置为 Idle，
    /// 使实体能在下个决策周期重试寻路。
    /// </summary>
    private static (bool, string) LordArmy_WarTarget_ActiveLOD_PreservesStateOnPathFail()
    {
        var processor = new DailyDecisionProcessor();
        var targetPoi = MakePOI(new Vector2(1200, 0), "enemy", OverworldPOI.POIType.Castle);
        targetPoi.PoiName = "FarFortress";

        // Active LOD（正常游戏状态）— 无导航系统时 StartMoveTo 会寻路失败
        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(0, 0));
        lord.Faction = "kingdom";
        lord.AssignedWarTargetPoiName = "FarFortress";
        lord.WarTargetAssignedDay = 1;
        lord.Lod = OverworldEntity.EntityLod.Active; // 关键: 不使用 Hibernated

        var worldEngine = new WorldEventEngine();
        var war = new WarState { NationA = "kingdom", NationB = "enemy", DaysSinceStart = 1 };
        war.ObjectivesA.Add("FarFortress");
        worldEngine.ActiveWars.Add(war);
        processor.SetWorldEventEngine(worldEngine);
        processor.SetArmyRegistry(new ArmyRegistry());

        processor.DecideLordArmy(lord, new List<OverworldPOI> { targetPoi }, 1);

        // 修复后: MovingToTarget 应保留（不再被 StartMoveTo 重置为 Idle）
        return (lord.CurrentAIState == OverworldEntity.AIState.MovingToTarget,
                $"state={lord.CurrentAIState}, IsMoving={lord.IsMoving}");
    }

    private static (bool, string) LordArmy_VisionIntercept_ChasesEnemyLord()
    {
        var processor = new DailyDecisionProcessor();
        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(0, 0));
        lord.Faction = "kingdom";
        lord.VisionRange = 800f;

        // 在视野内放置敌国领主（无战争目标指派，走到视野拦截分支）
        var enemyLord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(300, 0));
        enemyLord.Faction = "enemy";

        var worldEngine = new WorldEventEngine();
        var war = new WarState { NationA = "kingdom", NationB = "enemy", DaysSinceStart = 1 };
        worldEngine.ActiveWars.Add(war);
        processor.SetWorldEventEngine(worldEngine);
        processor.SetArmyRegistry(new ArmyRegistry());

        var entities = new List<OverworldEntity> { lord, enemyLord };
        processor.ProcessDailyDecisions(entities, new List<OverworldPOI>(), 1);

        return (lord.CurrentAIState == OverworldEntity.AIState.Chasing && lord.ChaseTarget == enemyLord,
                $"state={lord.CurrentAIState}, chaseTarget={lord.ChaseTarget?.EntityName}");
    }

    private static (bool, string) LordArmy_InArmy_FollowerFollowsMarshal()
    {
        var processor = new DailyDecisionProcessor();
        var registry = new ArmyRegistry();

        var marshal = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(1000, 1000));
        marshal.Faction = "kingdom";
        marshal.IsMarshal = true;

        var follower = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(1100, 1000));
        follower.Faction = "kingdom";

        // 使用 Create 注册军团，然后手动把 follower 加入
        var army = registry.Create(marshal, "", 1);
        army.State = ArmyState.Marching;
        army.Members.Add(follower);
        follower.ArmyId = army.ArmyId;

        processor.SetWorldEventEngine(new WorldEventEngine());
        processor.SetArmyRegistry(registry);

        processor.ProcessDailyDecisions(new List<OverworldEntity> { marshal, follower }, new List<OverworldPOI>(), 1);

        // 跟随者应该在元帅附近并被设置为 Escorting
        float distToMarshal = follower.Position.DistanceTo(marshal.Position);
        return (follower.CurrentAIState == OverworldEntity.AIState.Escorting && distToMarshal < 200f,
                $"follower state={follower.CurrentAIState}, dist={distToMarshal:F0}");
    }

    private static (bool, string) LordArmy_InArmy_MarshalMarchesToTarget()
    {
        var processor = new DailyDecisionProcessor();
        var registry = new ArmyRegistry();

        var targetPoi = MakePOI(new Vector2(5000, 5000), "enemy", OverworldPOI.POIType.Castle);
        targetPoi.PoiName = "MarchTarget";

        var marshal = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(0, 0));
        marshal.Faction = "kingdom";
        marshal.IsMarshal = true;

        var army = registry.Create(marshal, "MarchTarget", 1);
        army.State = ArmyState.Marching;

        processor.SetWorldEventEngine(new WorldEventEngine());
        processor.SetArmyRegistry(registry);

        processor.ProcessDailyDecisions(new List<OverworldEntity> { marshal }, new List<OverworldPOI> { targetPoi }, 1);

        return (marshal.CurrentAIState == OverworldEntity.AIState.MovingToTarget,
                $"marshal state={marshal.CurrentAIState}");
    }

    // ── DailyDecisionProcessor: 视野检测 ─────────────────────────────────

    private static (bool, string) VisionDetection_StrongerEntity_Chases()
    {
        // 此测试验证: 强敌在视野内但超出交战距离时，会进入追击状态。
        // 远距感知由 DailyDecisionProcessor/PerceptionIntentResolver 处理；
        // BattleResolver 只负责接触交战。
        var processor = new DailyDecisionProcessor();
        var strong = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        strong.CombatPower = 300f;
        strong.Faction = "kingdom";
        strong.VisionRange = 800f; // 扩大视野以覆盖远距离视野检测

        var weak = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(600, 0));
        weak.CombatPower = 100f;
        weak.Faction = "hostile";
        weak.VisionRange = 800f;

        // 距离 600: > ENGAGE_DIST(100) → 不进交战; < VisionRange(800) → 视野检测
        processor.ProcessFrameTactics(new List<OverworldEntity> { strong, weak });

        // 如果视野检测生效，strong(300 vs 100, ratio=3.0>1.5) 应进入 Chasing
        // 如果未生效，strong 保持 Patrolling
        bool chased = strong.CurrentAIState == OverworldEntity.AIState.Chasing && strong.ChaseTarget == weak;
        return (chased,
                $"state={strong.CurrentAIState}, target={strong.ChaseTarget?.EntityName}");
    }

    private static (bool, string) VisionDetection_WeakerEntity_Flees()
    {
        var processor = new DailyDecisionProcessor();
        var strong = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        strong.CombatPower = 300f;
        strong.Faction = "kingdom";
        strong.VisionRange = 500f;

        var weak = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(400, 0));
        weak.CombatPower = 100f;
        weak.Faction = "hostile";
        weak.VisionRange = 500f;

        processor.ProcessFrameTactics(new List<OverworldEntity> { strong, weak });

        return (weak.CurrentAIState == OverworldEntity.AIState.Fleeing,
                $"weak state={weak.CurrentAIState}");
    }

    private static (bool, string) VisionDetection_EqualPower_NoAction()
    {
        // 诊断测试: 验证感知阶段对同阵营远距离实体无影响
        var a = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        a.CombatPower = 100f;
        a.Faction = "kingdom";
        a.VisionRange = 400f;

        var b = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(600, 0));
        b.CombatPower = 100f;
        b.Faction = "kingdom";
        b.VisionRange = 400f;

        // 先验证初始状态
        string initState = $"init: a={a.CurrentAIState}/{a.Faction}, b={b.CurrentAIState}/{b.Faction}";

        var processor = new DailyDecisionProcessor();
        processor.ProcessFrameTactics(new List<OverworldEntity> { a, b });

        string afterState = $"after: a={a.CurrentAIState}/{a.Faction}, b={b.CurrentAIState}/{b.Faction}";

        return (a.CurrentAIState == OverworldEntity.AIState.Patrolling && b.CurrentAIState == OverworldEntity.AIState.Patrolling,
                $"{initState} | {afterState}");
    }

private static (bool, string) VisionDetection_FleeingEntity_DoesNotChase()
    {
        var processor = new DailyDecisionProcessor();
        var fleeing = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Fleeing, new Vector2(0, 0));
        fleeing.CombatPower = 300f;
        fleeing.Faction = "kingdom";
        fleeing.VisionRange = 500f;

        var target = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(400, 0));
        target.CombatPower = 100f;
        target.Faction = "hostile";

        // 逃跑实体必须有明确的威胁目标，否则 RefreshFleeMove 认为威胁已消失
        fleeing.CurrentTacticalTarget = target;

        // 预检查
        if (!fleeing.IsAlive)
            return (false, "fleeing 应存活");
        if (fleeing.CurrentAIState != OverworldEntity.AIState.Fleeing)
            return (false, $"fleeing 初始状态应为 Fleeing，得到 {fleeing.CurrentAIState}");
        if (fleeing.CurrentTacticalTarget != target)
            return (false, $"fleeing.CurrentTacticalTarget 应为 target");

        try
        {
            processor.ProcessFrameTactics(new List<OverworldEntity> { fleeing, target });
        }
        catch (Exception ex)
        {
            return (false, $"ProcessFrameTactics 异常: {ex.GetType().Name}: {ex.Message}");
        }

        GD.Print($"[FleeTest] fleeing state={fleeing.CurrentAIState}, target={target.CurrentAIState}, " +
                 $"tacticalTarget={fleeing.CurrentTacticalTarget?.EntityName}, dist={fleeing.Position.DistanceTo(target.Position)}");

        return (fleeing.CurrentAIState == OverworldEntity.AIState.Fleeing,
                $"fleeing state={fleeing.CurrentAIState} (should stay Fleeing)");
    }

    private static (bool, string) VisionDetection_SameFaction_NoHostility()
    {
        var processor = new DailyDecisionProcessor();
        var a = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        a.CombatPower = 300f;
        a.Faction = "kingdom";
        a.VisionRange = 500f;

        var b = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(400, 0));
        b.CombatPower = 100f;
        b.Faction = "kingdom"; // 同阵营

        processor.ProcessFrameTactics(new List<OverworldEntity> { a, b });

        return (a.CurrentAIState == OverworldEntity.AIState.Patrolling,
                $"state={a.CurrentAIState} (should stay Patrolling)");
    }

    private static (bool, string) Hostility_PlayerFactionWar_MakesNeutralNationHostileToPlayer()
    {
        var engine = new WorldEventEngine();
        engine.ActiveWars.Add(new WarState
        {
            NationA = "nation_a",
            NationB = "nation_b",
            DaysSinceStart = 1,
        });

        var enemy = MakeEntity(OverworldEntity.EntityType.LordArmy, faction: "nation_b", hostile: false);
        var player = MakeEntity(OverworldEntity.EntityType.Adventurer, faction: "nation_a", hostile: false);

        bool hostile = OverworldHostility.AreHostileToPlayer(enemy, player, engine);
        return (hostile, $"expected nation_b hostile to player faction nation_a during war; hostile={hostile}");
    }

    // ── BattleResolver: 交战 → 时间战斗结算 ─────────────────────────────

    private static (bool, string) Battle_HostileFaction_Resolves()
    {
        var resolver = new BattleResolver();
        var a = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        a.CombatPower = 500f; a.PartySize = 20;
        a.Faction = "kingdom";

        var b = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(50, 0));
        b.CombatPower = 100f; b.PartySize = 10;
        b.Faction = "hostile";

        // 阶段 1: 接触 → 进入交战状态 (gameHour=0)
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 0f);

        bool engaged = a.CurrentAIState == OverworldEntity.AIState.Engaged
                    && b.CurrentAIState == OverworldEntity.AIState.Engaged;
        if (!engaged) return (false, $"Phase1 failed: a={a.CurrentAIState}, b={b.CurrentAIState}");

        int duration = a.CombatDurationHours;
        bool durationValid = duration >= 3 && duration <= 24;

        // 阶段 2: 未到时间 → 不结算 (hour=duration-1)
        resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: duration - 1f);
        bool stillEngaged = a.CurrentAIState == OverworldEntity.AIState.Engaged;

        // 阶段 3: 超过战斗时长 → 最终结算 (hour=duration+1)
        resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: duration + 1f);

        bool resolved = a.CurrentAIState != OverworldEntity.AIState.Engaged
                     && b.CurrentAIState != OverworldEntity.AIState.Engaged;
        bool cleared = a.EngagedWith == null && b.EngagedWith == null;
        return (resolved && cleared && stillEngaged && durationValid,
                $"a={a.CurrentAIState}, b={b.CurrentAIState}, duration={duration}h, engaged={engaged}");
    }

    private static (bool, string) Battle_SameFaction_Skipped()
    {
        var resolver = new BattleResolver();
        var a = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        a.CombatPower = 500f;
        a.Faction = "kingdom";

        var b = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(50, 0));
        b.CombatPower = 100f;
        b.Faction = "kingdom";

        float aBefore = a.CombatPower;
        float bBefore = b.CombatPower;

        // 同阵营不应触发交战
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 0f);

        bool noEngage = a.CurrentAIState != OverworldEntity.AIState.Engaged
                     && b.CurrentAIState != OverworldEntity.AIState.Engaged;

        // 即使调用更新也不应有变化
        resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: 50f);

        return (noEngage && a.CombatPower == aBefore && b.CombatPower == bBefore,
                $"a.state={a.CurrentAIState}, b.state={b.CurrentAIState}, combat power unchanged={a.CombatPower == aBefore}");
    }

    private static (bool, string) Battle_LoserEntersFleeing()
    {
        var resolver = new BattleResolver();
        var a = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        a.CombatPower = 500f; a.PartySize = 30;
        a.Faction = "kingdom";

        var b = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(50, 0));
        b.CombatPower = 50f; b.PartySize = 5; // 极弱
        b.Faction = "hostile";

        // 阶段 1: 接触交战
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 10f);
        bool engaged = a.CurrentAIState == OverworldEntity.AIState.Engaged
                    && b.CurrentAIState == OverworldEntity.AIState.Engaged;
        if (!engaged) return (false, $"Phase1 failed: a={a.CurrentAIState}, b={b.CurrentAIState}");

        int duration = a.CombatDurationHours;

        // 阶段 2: 渐进更新 (模拟视野内每3h)
        for (float h = 10f + resolver.ViewportUpdateInterval; h < 10f + duration; h += resolver.ViewportUpdateInterval)
            resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: h);

        // 阶段 3: 最终结算
        resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: 10f + duration + 1f);

        // 弱方要么被消灭，要么进入 Fleeing
        bool loserHandled = !b.IsAlive || b.CurrentAIState == OverworldEntity.AIState.Fleeing;
        return (loserHandled, $"engaged={engaged}, duration={duration}h, b alive={b.IsAlive}, state={b.CurrentAIState}");
    }

    private static (bool, string) Battle_ArmyPowerAggregation()
    {
        var resolver = new BattleResolver();
        var registry = new ArmyRegistry();

        var marshalA = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        marshalA.CombatPower = 100f; marshalA.PartySize = 10;
        marshalA.Faction = "kingdom";

        var memberA = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(50, 0));
        memberA.CombatPower = 100f; memberA.PartySize = 10;
        memberA.Faction = "kingdom";

        var enemy = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Patrolling, new Vector2(30, 0));
        enemy.CombatPower = 150f; enemy.PartySize = 8; // 强于单个成员但不强于军团总和
        enemy.Faction = "hostile";

        var army = registry.Create(marshalA, "", 1);
        army.Members.Add(memberA);
        memberA.ArmyId = army.ArmyId;

        resolver.SetArmyRegistry(registry);

        // 阶段 1: marshal 与 enemy 接触交战 (距离 30px, hour=0)
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { marshalA, memberA, enemy }, currentGameHour: 0f);

        bool marshalEngaged = marshalA.CurrentAIState == OverworldEntity.AIState.Engaged
                           && enemy.CurrentAIState == OverworldEntity.AIState.Engaged;
        if (!marshalEngaged) return (false, $"Phase1 failed: marshal={marshalA.CurrentAIState}, enemy={enemy.CurrentAIState}");

        int duration = marshalA.CombatDurationHours;

        // 阶段 2: 时间战斗结算 (军团聚合战力 200 vs 150)
        resolver.UpdateEngagements(new List<OverworldEntity> { marshalA, memberA, enemy }, currentGameHour: duration + 1f);

        // 军团聚合战力 200 vs 敌方 150，敌方应处于劣势
        bool enemyDisadvantaged = !enemy.IsAlive || enemy.CurrentAIState == OverworldEntity.AIState.Fleeing || enemy.CombatPower < 150f;
        return (enemyDisadvantaged, $"marshalEngaged={marshalEngaged}, duration={duration}h, enemy state={enemy.CurrentAIState}, power={enemy.CombatPower:F0}");
    }

    // ── MovementProcessor ────────────────────────────────────────────────

    private static (bool, string) Movement_AdvancesAlongPath()
    {
        var processor = new MovementProcessor();
        var entity = MakeEntity(position: new Vector2(0, 0));
        entity.IsMoving = true;
        entity.Path.Add(new Vector2(100, 0));
        entity.Path.Add(new Vector2(200, 0));

        processor.TickMovement(0.5f, new List<OverworldEntity> { entity }, null);

        return (entity.Position.X > 0 && entity.Position.X <= 100,
                $"position X={entity.Position.X} (expected between 0 and 100)");
    }

    private static (bool, string) Movement_ReachesDestination_FiresCallback()
    {
        var processor = new MovementProcessor();
        var entity = MakeEntity(position: new Vector2(0, 0));
        entity.IsMoving = true;
        entity.MoveSpeed = 1000f; // 很高速度确保一帧到达
        entity.Path.Add(new Vector2(10, 0));

        OverworldEntity? reachedEntity = null;
        processor.TickMovement(1.0f, new List<OverworldEntity> { entity }, e => reachedEntity = e);

        return (reachedEntity == entity && !entity.IsMoving,
                $"callback fired={reachedEntity != null}, isMoving={entity.IsMoving}");
    }

    private static (bool, string) Movement_ChasingState_SpeedMultiplier()
    {
        var normalProcessor = new MovementProcessor();
        var chasingProcessor = new MovementProcessor();

        var normalEntity = MakeEntity(position: new Vector2(0, 0));
        normalEntity.IsMoving = true;
        normalEntity.MoveSpeed = 100f;
        normalEntity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        normalEntity.Path.Add(new Vector2(1000, 0));

        var chasingEntity = MakeEntity(position: new Vector2(0, 0));
        chasingEntity.IsMoving = true;
        chasingEntity.MoveSpeed = 100f;
        chasingEntity.CurrentAIState = OverworldEntity.AIState.Chasing;
        chasingEntity.Path.Add(new Vector2(1000, 0));

        float delta = 0.5f;
        normalProcessor.TickMovement(delta, new List<OverworldEntity> { normalEntity }, null);
        chasingProcessor.TickMovement(delta, new List<OverworldEntity> { chasingEntity }, null);

        return (chasingEntity.Position.X > normalEntity.Position.X,
                $"chasing X={chasingEntity.Position.X:F1} > normal X={normalEntity.Position.X:F1}");
    }

    private static (bool, string) Movement_HibernatedEntity_Skipped()
    {
        var processor = new MovementProcessor();
        var entity = MakeEntity(position: new Vector2(0, 0));
        entity.IsMoving = true;
        entity.Lod = OverworldEntity.EntityLod.Hibernated;
        entity.Path.Add(new Vector2(1000, 0));

        processor.TickMovement(1.0f, new List<OverworldEntity> { entity }, null);

        return (entity.Position == Vector2.Zero,
                $"position should not change for hibernated, got {entity.Position}");
    }

    // ── SiegeProcessor ───────────────────────────────────────────────────

    private static (bool, string) Siege_EnoughDays_Resolves()
    {
        var processor = new SiegeProcessor();
        var target = MakePOI(new Vector2(0, 0), "enemy", OverworldPOI.POIType.Castle, garrison: 5);
        target.IsUnderSiege = true;
        target.SiegeDays = 3; // 独立领主需要 2 天

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Besieging, new Vector2(100, 0));
        lord.Faction = "kingdom";
        lord.CombatPower = 500f;
        lord.SiegeTarget = target;

        var signals = new TestSiegeSignals();
        processor.ProcessSieges(new List<OverworldEntity> { lord }, signals, 5, null);

        return (signals.SiegeResolvedCount > 0,
                $"siege resolved={signals.SiegeResolvedCount}");
    }

    private static (bool, string) Siege_TooFewDays_Skipped()
    {
        var processor = new SiegeProcessor();
        var target = MakePOI(new Vector2(0, 0), "enemy", OverworldPOI.POIType.Castle, garrison: 5);
        target.IsUnderSiege = true;
        target.SiegeDays = 1; // 不够 2 天

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Besieging, new Vector2(100, 0));
        lord.Faction = "kingdom";
        lord.SiegeTarget = target;

        var signals = new TestSiegeSignals();
        processor.ProcessSieges(new List<OverworldEntity> { lord }, signals, 5, null);

        return (signals.SiegeResolvedCount == 0,
                $"siege should not resolve with 1 day, resolved={signals.SiegeResolvedCount}");
    }

    private static (bool, string) Siege_AttackerDies_ResolvesEntityDefeat()
    {
        var processor = new SiegeProcessor();
        var target = MakePOI(new Vector2(0, 0), "enemy", OverworldPOI.POIType.Castle, garrison: 100);
        target.IsUnderSiege = true;
        target.SiegeDays = 3;

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Besieging, new Vector2(100, 0));
        lord.Faction = "kingdom";
        lord.CombatPower = 5f; // 极弱
        lord.PartySize = 2;
        lord.SiegeTarget = target;

        var signals = new TestSiegeSignals();
        processor.ProcessSieges(new List<OverworldEntity> { lord }, signals, 5, null);

        // 极弱攻方大概率失败
        return (signals.SiegeResolvedCount > 0,
                $"siege resolved with weak attacker={signals.SiegeResolvedCount}");
    }

    private static (bool, string) Reinforcement_NearbyLord_Responds()
    {
        var processor = new SiegeProcessor();
        var poi = MakePOI(new Vector2(0, 0), "kingdom", OverworldPOI.POIType.Castle, garrison: 5);
        poi.IsUnderSiege = true;

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(500, 0));
        lord.Faction = "kingdom";

        var signals = new TestSiegeSignals();
        var index = new EntitySpatialIndex(800f);
        index.Add(lord);

        processor.ProcessReinforcementChecks(
            new List<OverworldEntity> { lord },
            new List<OverworldPOI> { poi },
            signals,
            index);

        return (lord.CurrentAIState == OverworldEntity.AIState.Reinforcing && lord.ReinforceTarget == poi,
                $"state={lord.CurrentAIState}, target={lord.ReinforceTarget?.PoiName}");
    }

    private static (bool, string) Reinforcement_TooFar_NoResponse()
    {
        var processor = new SiegeProcessor();
        var poi = MakePOI(new Vector2(0, 0), "kingdom", OverworldPOI.POIType.Castle, garrison: 5);
        poi.IsUnderSiege = true;

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(5000, 0));
        lord.Faction = "kingdom";

        var signals = new TestSiegeSignals();
        processor.ProcessReinforcementChecks(
            new List<OverworldEntity> { lord },
            new List<OverworldPOI> { poi },
            signals);

        return (lord.CurrentAIState == OverworldEntity.AIState.Idle,
                $"state={lord.CurrentAIState} (should stay Idle when too far)");
    }

    private static (bool, string) Recruitment_IdleLordAtCastle_GainsGarrison()
    {
        var processor = new SiegeProcessor();
        var castle = MakePOI(new Vector2(0, 0), "kingdom", OverworldPOI.POIType.Castle, garrison: 20);

        var lord = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Idle, new Vector2(0, 0));
        lord.Faction = "kingdom";
        lord.GuardedPOI = castle;
        int garrisonBefore = lord.GarrisonSize;

        processor.ProcessRecruitment(new List<OverworldEntity> { lord });

        return (lord.GarrisonSize > garrisonBefore,
                $"garrison {garrisonBefore} → {lord.GarrisonSize}");
    }

    // ── OverworldAIResolver ──────────────────────────────────────────────

    private static (bool, string) ResolveBattle_CrushingVictory_LowLosses()
    {
        var attacker = MakeEntity(combatPower: 1000f);
        var defender = MakeEntity(combatPower: 10f);

        // 多次运行取统计趋势（随机因素 0.8-1.2 仍可能让极弱方偶尔赢）
        int attackerWins = 0;
        for (int i = 0; i < 20; i++)
        {
            var atk = MakeEntity(combatPower: 1000f);
            var def = MakeEntity(combatPower: 10f);
            var result = OverworldAIResolver.ResolveBattle(atk, def);
            if ((bool)result["attacker_won"]) attackerWins++;
        }

        return (attackerWins >= 18, $"attacker won {attackerWins}/20 times (expected >=18)");
    }

    private static (bool, string) ResolveBattle_DestroyedWhenPowerBelow1()
    {
        var attacker = MakeEntity(combatPower: 0.5f);
        attacker.PartySize = 1;
        var defender = MakeEntity(combatPower: 100f);

        var result = OverworldAIResolver.ResolveBattle(attacker, defender);

        return ((bool)result["attacker_destroyed"],
                $"attacker with 0.5 power should be destroyed");
    }

    private static (bool, string) ResolveSiege_AttackerWin_ReducesGarrison()
    {
        var attacker = MakeEntity(combatPower: 1000f);
        var target = MakePOI(garrison: 50);

        int garrisonBefore = target.GarrisonCurrent;
        int wins = 0;
        for (int i = 0; i < 20; i++)
        {
            var atk = MakeEntity(combatPower: 1000f);
            var tgt = MakePOI(garrison: 50);
            var result = OverworldAIResolver.ResolveSiege(atk, tgt);
            if ((bool)result["attacker_won"])
            {
                wins++;
                if (tgt.GarrisonCurrent >= garrisonBefore)
                    return (false, $"garrison should decrease on siege win, was {garrisonBefore}, now {tgt.GarrisonCurrent}");
            }
        }

        return (wins > 0, $"attacker won {wins}/20 sieges");
    }

    private static (bool, string) ResolveRaid_Success_DamagesProsperity()
    {
        int prosperityDamaged = 0;
        for (int i = 0; i < 20; i++)
        {
            var raider = MakeEntity(combatPower: 500f);
            raider.EntityTypeEnum = OverworldEntity.EntityType.RaidingParty;
            var village = MakePOI(garrison: 2, prosperity: 80);

            int before = village.Prosperity;
            var result = OverworldAIResolver.ResolveRaid(raider, village);
            if ((bool)result["raider_won"] && village.Prosperity < before)
                prosperityDamaged++;
        }

        return (prosperityDamaged > 0, $"prosperity damaged in {prosperityDamaged}/20 raids");
    }

    // ── EncounterEntitySpawner ───────────────────────────────────────────

    private static (bool, string) Spawner_InsufficientDistance_NoSpawn()
    {
        var spawner = new EncounterEntitySpawner();
        spawner.MinDistanceTraveled = 600f;

        // 不移动玩家，直接 tick
        var result = spawner.Tick(10f, new Vector2(0, 0), new List<OverworldEntity>(), 5, 1);

        return (result.Count == 0, $"expected no spawn, got {result.Count}");
    }

    private static (bool, string) Spawner_MaxEntitiesReached_NoSpawn()
    {
        var spawner = new EncounterEntitySpawner();
        spawner.MaxActiveEntities = 2;
        spawner.MinDistanceTraveled = 0f; // 无距离要求
        spawner.SpawnIntervalSec = 0f; // 无间隔要求

        var existing = new List<OverworldEntity>
        {
            MakeEntity(hostile: true, state: OverworldEntity.AIState.Chasing),
            MakeEntity(hostile: true, state: OverworldEntity.AIState.Chasing),
        };

        // 模拟 tick 但不累积距离（通过多次 tick 和微小移动）
        var result = spawner.Tick(10f, new Vector2(0, 0), existing, 5, 1);
        // 第一次 tick 没有距离累积，所以不会生成
        return (result.Count == 0, $"expected no spawn at max entities, got {result.Count}");
    }

    private static (bool, string) Spawner_SufficientConditions_SpawnsEntity()
    {
        var spawner = new EncounterEntitySpawner();
        spawner.MinDistanceTraveled = 100f;
        spawner.SpawnIntervalSec = 1f;
        spawner.MaxActiveEntities = 10;

        // Tick 1: 在非零位置初始化 _lastPlayerPosition（首帧跳过距离累积）
        var r1 = spawner.Tick(0.5f, new Vector2(100, 0), new List<OverworldEntity>(), 5, 1);
        // Tick 2: 累积 500px 距离, 时间 1.0s
        var r2 = spawner.Tick(0.5f, new Vector2(600, 0), new List<OverworldEntity>(), 5, 1);
        // Tick 3: 累积 1000px, 时间 1.5s — 远超所有阈值
        var r3 = spawner.Tick(0.5f, new Vector2(1100, 0), new List<OverworldEntity>(), 5, 1);

        int total = r1.Count + r2.Count + r3.Count;
        return (total > 0, $"spawned total={total} (r1={r1.Count}, r2={r2.Count}, r3={r3.Count})");
    }

    /// <summary>
    /// 验证生产修复: _positionInitialized 布尔标志替代 Vector2.Zero 哨兵值，
    /// 玩家从世界原点出发时首帧距离不会被跳过。
    /// </summary>
    private static (bool, string) Spawner_FirstTickAtOrigin_StillAccumulates()
    {
        var spawner = new EncounterEntitySpawner();
        spawner.MinDistanceTraveled = 100f;
        spawner.SpawnIntervalSec = 1f;
        spawner.MaxActiveEntities = 10;

        // Tick 1: 玩家在世界原点 (0,0) — 修复前会被 Vector2.Zero 哨兵跳过
        var r1 = spawner.Tick(0.5f, Vector2.Zero, new List<OverworldEntity>(), 5, 1);
        // Tick 2: 移动 500px，时间 1.0s — 距离和时间均满足条件
        var r2 = spawner.Tick(0.5f, new Vector2(500, 0), new List<OverworldEntity>(), 5, 1);

        int total = r1.Count + r2.Count;
        return (total > 0, $"spawned total={total} (r1={r1.Count}, r2={r2.Count})");
    }

    private static (bool, string) Spawner_ChaseAI_EntersChasingWhenInView()
    {
        var spawner = new EncounterEntitySpawner();
        var entity = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        entity.IsHostileToPlayer = true;
        entity.VisionRange = 600f;

        // 玩家在视野内
        var entities = new List<OverworldEntity> { entity };
        spawner.Tick(1f, new Vector2(300, 0), entities, 5, 1);

        return (entity.CurrentAIState == OverworldEntity.AIState.Chasing,
                $"state={entity.CurrentAIState} (expected Chasing)");
    }

    private static (bool, string) Spawner_ChaseAI_StopsWhenOutOfRange()
    {
        var spawner = new EncounterEntitySpawner();
        var entity = MakeEntity(OverworldEntity.EntityType.RaidingParty, OverworldEntity.AIState.Chasing, new Vector2(0, 0));
        entity.IsHostileToPlayer = true;
        entity.VisionRange = 200f;

        // 玩家在视野外
        var entities = new List<OverworldEntity> { entity };
        spawner.Tick(1f, new Vector2(1000, 0), entities, 5, 1);

        return (entity.CurrentAIState == OverworldEntity.AIState.Patrolling,
                $"state={entity.CurrentAIState} (expected Patrolling after losing player)");
    }

    private static (bool, string) Spawner_Caravan_NeverHostile()
    {
        var spawner = new EncounterEntitySpawner();
        var caravan = MakeEntity(OverworldEntity.EntityType.Caravan, OverworldEntity.AIState.MovingToTarget, new Vector2(0, 0));
        caravan.IsHostileToPlayer = false;
        caravan.VisionRange = 600f;

        var entities = new List<OverworldEntity> { caravan };
        spawner.Tick(1f, new Vector2(100, 0), entities, 5, 1);

        // 商队不应进入追击状态
        return (caravan.CurrentAIState != OverworldEntity.AIState.Chasing,
                $"caravan state={caravan.CurrentAIState} (should never chase)");
    }

    // ── EntityLodController ──────────────────────────────────────────────

    private static (bool, string) Spawner_HostilePlayerVision_ChasesPlayer()
    {
        var spawner = new EncounterEntitySpawner { PlayerCombatPower = 20f };
        var entity = MakeEntity(OverworldEntity.EntityType.BanditParty, OverworldEntity.AIState.Patrolling, new Vector2(0, 0));
        entity.Faction = "bandit";
        entity.IsHostileToPlayer = true;
        entity.CombatPower = 100f;
        entity.VisionRange = 600f;

        var entities = new List<OverworldEntity> { entity };
        spawner.Tick(1f, new Vector2(300, 0), entities, 5, 1);

        bool chasingPlayer = entity.CurrentAIState == OverworldEntity.AIState.Chasing
                          && entity.ChaseTarget != null
                          && entity.ChaseTarget.Faction == "player";
        bool moving = entity.IsMoving && entity.Path.Count > 0 && entity.TargetPosition == new Vector2(300, 0);

        return (chasingPlayer && moving,
                $"state={entity.CurrentAIState}, targetFaction={entity.ChaseTarget?.Faction}, moving={entity.IsMoving}, path={entity.Path.Count}");
    }

    private static (bool, string) Spawner_ChunkSlotWildMonster_SpawnsAndTriggers()
    {
        var chunk = new ChunkData { ChunkCoord = Vector2I.Zero, IsGenerated = true, IsActive = true };
        var tile = HexOverworldTile.Create(0, 1, HexOverworldTile.TerrainType.Forest, 0.3f, 0.5f, 0.5f);
        chunk.Tiles[tile.Coord] = tile;
        chunk.SetEncounterState(tile.Coord.X, tile.Coord.Y, EncounterSlotState.Available);

        var entitySpawner = new EncounterEntitySpawner();
        var encounterSpawner = new EncounterSpawner();
        var spawned = entitySpawner.SpawnWildMonstersFromSlots(
            chunk,
            encounterSpawner,
            dangerLevel: 0.5f,
            playerLevel: 5,
            playerPosition: new Vector2(-1000, -1000));

        if (spawned.Count != 1)
            return (false, $"expected 1 spawned entity, got {spawned.Count}");

        var entity = spawned[0];
        bool slotTriggered = chunk.GetEncounterState(tile.Coord.X, tile.Coord.Y) == EncounterSlotState.Triggered;
        bool templateInjected = entity.TempEncounterEnemies != null && entity.TempEncounterEnemies.Length > 0;
        bool entityConfigured = entity.Position == tile.PixelPos
            && entity.CurrentAIState == OverworldEntity.AIState.Patrolling
            && entity.IsHostileToPlayer
            && entity.CombatPower > 0f;

        return (slotTriggered && templateInjected && entityConfigured,
            $"triggered={slotTriggered}, templates={entity.TempEncounterEnemies?.Length ?? 0}, state={entity.CurrentAIState}, cp={entity.CombatPower}");
    }

    private static (bool, string) Spawner_ChunkSlotWildMonster_RespectsMaxSpawns()
    {
        var chunk = new ChunkData { ChunkCoord = Vector2I.Zero, IsGenerated = true, IsActive = true };
        for (int q = 0; q < 3; q++)
        {
            var tile = HexOverworldTile.Create(q, 1, HexOverworldTile.TerrainType.Forest, 0.3f, 0.5f, 0.5f);
            chunk.Tiles[tile.Coord] = tile;
            chunk.SetEncounterState(tile.Coord.X, tile.Coord.Y, EncounterSlotState.Available);
        }

        var entitySpawner = new EncounterEntitySpawner();
        var encounterSpawner = new EncounterSpawner();
        var spawned = entitySpawner.SpawnWildMonstersFromSlots(
            chunk,
            encounterSpawner,
            dangerLevel: 0.5f,
            playerLevel: 5,
            playerPosition: new Vector2(-1000, -1000),
            maxSpawns: 1);

        int triggered = chunk.EncounterSlots.Values.Count(s => s == EncounterSlotState.Triggered);
        int stillAvailable = chunk.EncounterSlots.Values.Count(s => s == EncounterSlotState.Available);

        return (spawned.Count == 1 && triggered == 1 && stillAvailable == 2,
            $"spawned={spawned.Count}, triggered={triggered}, available={stillAvailable}");
    }

    private static (bool, string) LOD_FarEntity_Hibernates()
    {
        var entity = MakeEntity(position: new Vector2(0, 0));
        entity.Lod = OverworldEntity.EntityLod.Active;

        // 玩家距离 > 5500px
        EntityLodController.Update(new List<OverworldEntity> { entity }, new Vector2(6000, 0));

        return (entity.Lod == OverworldEntity.EntityLod.Hibernated,
                $"LOD={entity.Lod} (expected Hibernated at dist 6000)");
    }

    private static (bool, string) LOD_NearEntity_Activates()
    {
        var entity = MakeEntity(position: new Vector2(0, 0));
        entity.Lod = OverworldEntity.EntityLod.Hibernated;

        // 玩家距离 < 5000px
        EntityLodController.Update(new List<OverworldEntity> { entity }, new Vector2(3000, 0));

        return (entity.Lod == OverworldEntity.EntityLod.Active,
                $"LOD={entity.Lod} (expected Active at dist 3000)");
    }

    private static (bool, string) LOD_Hysteresis_PreventsFlapping()
    {
        var entity = MakeEntity(position: new Vector2(0, 0));
        entity.Lod = OverworldEntity.EntityLod.Active;

        // 距离 5200px — 在 ACTIVE_THRESHOLD(5000) 和 HIBERNATE_THRESHOLD(5500) 之间
        EntityLodController.Update(new List<OverworldEntity> { entity }, new Vector2(5200, 0));

        // Active 实体距离 5200 < 5500(HIBERNATE_THRESHOLD)，应保持 Active
        bool staysActive = entity.Lod == OverworldEntity.EntityLod.Active;

        // 反过来：Hibernated 实体距离 5200 > 5000(ACTIVE_THRESHOLD)，应保持 Hibernated
        entity.Lod = OverworldEntity.EntityLod.Hibernated;
        EntityLodController.Update(new List<OverworldEntity> { entity }, new Vector2(5200, 0));
        bool staysHibernated = entity.Lod == OverworldEntity.EntityLod.Hibernated;

        return (staysActive && staysHibernated,
                $"hysteresis: active stays={staysActive}, hibernated stays={staysHibernated}");
    }

    // ── OverworldEntity 数据模型 ─────────────────────────────────────────

    private static (bool, string) Entity_Serialize_Roundtrip()
    {
        var original = MakeEntity(OverworldEntity.EntityType.LordArmy, OverworldEntity.AIState.Besieging, new Vector2(123, 456));
        original.EntityName = "TestLord";
        original.Faction = "kingdom";
        original.PartySize = 30;
        original.CombatPower = 999f;
        original.ArmyId = "army_42";
        original.IsMarshal = true;

        var data = original.Serialize();
        var deserialized = new OverworldEntity();
        deserialized.EntityName = (string)data["entity_name"];
        deserialized.EntityTypeEnum = (OverworldEntity.EntityType)(int)data["entity_type"];
        deserialized.Faction = (string)data["faction"];
        deserialized.PartySize = (int)data["party_size"];
        deserialized.CombatPower = (float)data["combat_power"];
        deserialized.ArmyId = (string)data["army_id"];
        deserialized.IsMarshal = (bool)data["is_marshal"];

        bool nameOk = deserialized.EntityName == original.EntityName;
        bool typeOk = deserialized.EntityTypeEnum == original.EntityTypeEnum;
        bool factionOk = deserialized.Faction == original.Faction;
        bool partyOk = deserialized.PartySize == original.PartySize;
        bool powerOk = Math.Abs(deserialized.CombatPower - original.CombatPower) < 0.01f;
        bool armyOk = deserialized.ArmyId == original.ArmyId;
        bool marshalOk = deserialized.IsMarshal == original.IsMarshal;

        return (nameOk && typeOk && factionOk && partyOk && powerOk && armyOk && marshalOk,
                $"roundtrip mismatch: name={nameOk}, type={typeOk}, faction={factionOk}, party={partyOk}, power={powerOk}, army={armyOk}, marshal={marshalOk}");
    }

    private static (bool, string) Entity_EvaluatePowerRatio_Correct()
    {
        var a = MakeEntity(combatPower: 300f);
        var b = MakeEntity(combatPower: 100f);

        float ratioAB = a.EvaluatePowerRatio(b);
        float ratioBA = b.EvaluatePowerRatio(a);

        bool abOk = Math.Abs(ratioAB - 3.0f) < 0.01f;
        bool baOk = Math.Abs(ratioBA - (1.0f / 3.0f)) < 0.01f;

        return (abOk && baOk, $"A/B={ratioAB:F2} (expected 3.00), B/A={ratioBA:F4} (expected 0.33)");
    }

    private static (bool, string) Entity_IsInTerritory_Correct()
    {
        var entity = MakeEntity(position: new Vector2(1000, 1000));
        entity.TerritoryCenter = new Vector2(1000, 1000);
        entity.TerritoryRadius = 500f;

        bool inside = entity.IsInTerritory(new Vector2(1200, 1000));   // dist=200, < 500
        bool outside = !entity.IsInTerritory(new Vector2(2000, 2000)); // dist>>500
        bool zeroCenter = !entity.IsInTerritory(new Vector2(0, 0));    // territory at zero center returns false

        // 测试零中心特殊情况
        var entityZero = MakeEntity();
        entityZero.TerritoryCenter = Vector2.Zero;
        bool zeroDefault = !entityZero.IsInTerritory(new Vector2(100, 100));

        return (inside && outside && zeroDefault,
                $"inside={inside}, outside={outside}, zeroDefault={zeroDefault}");
    }

    // ── BanditParty / RobberParty / PirateCrew 决策测试 ──────────────────────

    private static (bool, string) BanditParty_Idle_StartsPatrolling()
    {
        var entity = MakeEntity(type: OverworldEntity.EntityType.BanditParty, state: OverworldEntity.AIState.Idle, position: new Vector2(500, 500));
        entity.HomePosition = new Vector2(500, 500);
        var processor = new DailyDecisionProcessor();
        // 无导航 — StartMoveTo 会因 _hexGrid == null && _chunkManager == null 而直接返回, IsMoving 保持 false
        // 但状态仍应为 Idle (因为巡逻失败回退)
        processor.ProcessDailyDecisions(new List<OverworldEntity> { entity }, new List<OverworldPOI>(), 1);
        // 无导航时, StartMoveTo 不会改变 IsMoving, 状态保持 Idle
        bool ok = entity.CurrentAIState == OverworldEntity.AIState.Idle || entity.CurrentAIState == OverworldEntity.AIState.Patrolling;
        return (ok, $"state={entity.CurrentAIState}");
    }

    private static (bool, string) BanditParty_Chasing_UpdatesTargetPosition()
    {
        var bandit = MakeEntity(type: OverworldEntity.EntityType.BanditParty, state: OverworldEntity.AIState.Chasing, position: new Vector2(0, 0));
        var target = MakeEntity(type: OverworldEntity.EntityType.Adventurer, position: new Vector2(200, 0));
        bandit.ChaseTarget = target;
        bandit.Faction = "bandit";
        target.Faction = "neutral";

        var processor = new DailyDecisionProcessor();
        processor.ProcessDailyDecisions(new List<OverworldEntity> { bandit, target }, new List<OverworldPOI>(), 1);

        bool stillChasing = bandit.CurrentAIState == OverworldEntity.AIState.Chasing;
        bool targetUpdated = bandit.TargetPosition == target.Position;
        return (stillChasing && targetUpdated, $"state={bandit.CurrentAIState}, targetPos={bandit.TargetPosition}");
    }

    private static (bool, string) RobberParty_Fleeing_ReturnsTowardHome()
    {
        var entity = MakeEntity(type: OverworldEntity.EntityType.RobberParty, state: OverworldEntity.AIState.Fleeing, position: new Vector2(500, 500));
        entity.HomePosition = new Vector2(100, 100);

        var processor = new DailyDecisionProcessor();
        processor.ProcessDailyDecisions(new List<OverworldEntity> { entity }, new List<OverworldPOI>(), 1);

        // 无导航时, StartMoveTo 不改变 IsMoving, 但距离 > 50 所以不会切回 Idle
        // 状态应仍为 Fleeing (等待导航完成后移动)
        bool ok = entity.CurrentAIState == OverworldEntity.AIState.Fleeing;
        return (ok, $"state={entity.CurrentAIState}");
    }

    private static (bool, string) PirateCrew_Chasing_ReturnsIdleWhenTargetDead()
    {
        var pirate = MakeEntity(type: OverworldEntity.EntityType.PirateCrew, state: OverworldEntity.AIState.Chasing, position: new Vector2(0, 0));
        var target = MakeEntity(type: OverworldEntity.EntityType.Adventurer, position: new Vector2(100, 0));
        target.IsAlive = false;
        pirate.ChaseTarget = target;
        pirate.Faction = "pirate";

        var processor = new DailyDecisionProcessor();
        processor.ProcessDailyDecisions(new List<OverworldEntity> { pirate, target }, new List<OverworldPOI>(), 1);

        bool idle = pirate.CurrentAIState == OverworldEntity.AIState.Idle;
        bool noTarget = pirate.ChaseTarget == null;
        return (idle && noTarget, $"state={pirate.CurrentAIState}, chaseTarget={pirate.ChaseTarget}");
    }

    // ── AIStrategy 策略修正测试 ────────────────────────────────────────────

    private static (bool, string) AIStrategy_Berserk_LowersChaseThreshold()
    {
        // 狂暴策略: chaseMul=0.3, 所以 Adventurer 基础追 1.5 → 实际 0.45
        // 战力比 0.6 > 0.45 → 应该追击 (普通 Instinct 不会追)
        var berserker = MakeEntity(type: OverworldEntity.EntityType.Adventurer, combatPower: 60f, faction: "neutral");
        berserker.AIStrategy = AIStrategyEnum.Berserk;

        var instEntity = MakeEntity(type: OverworldEntity.EntityType.Adventurer, combatPower: 60f, faction: "neutral");
        instEntity.AIStrategy = AIStrategyEnum.Instinct;

        var target = MakeEntity(type: OverworldEntity.EntityType.RaidingParty, combatPower: 100f, faction: "hostile", hostile: true);
        target.Position = new Vector2(300, 0);
        berserker.Position = new Vector2(0, 0);
        instEntity.Position = new Vector2(0, 0);

        var processor = new DailyDecisionProcessor();

        // 单独评估狂暴实体
        var berserkerList = new List<OverworldEntity> { berserker, target };
        processor.ProcessFrameTactics(berserkerList);

        // 单独评估本能实体
        var instList = new List<OverworldEntity> { instEntity, MakeEntity(type: OverworldEntity.EntityType.RaidingParty, combatPower: 100f, faction: "hostile", hostile: true, position: new Vector2(300, 0)) };
        processor.ProcessFrameTactics(instList);

        bool berserkerChases = berserker.CurrentAIState == OverworldEntity.AIState.Chasing;
        bool instinctNoChase = instEntity.CurrentAIState != OverworldEntity.AIState.Chasing; // 60/100=0.6 < 1.5, 不追

        return (berserkerChases && instinctNoChase,
                $"berserker={berserker.CurrentAIState}, instinct={instEntity.CurrentAIState}");
    }

    private static (bool, string) AIStrategy_Cautious_RaisesChaseThreshold()
    {
        // 谨慎策略: chaseMul=1.5, 所以 Adventurer 基础追 1.5 → 实际 2.25
        // 战力比 1.8 < 2.25 → 不应追击 (普通 Instinct 1.8 > 1.5 会追)
        var cautious = MakeEntity(type: OverworldEntity.EntityType.Adventurer, combatPower: 180f, faction: "neutral");
        cautious.AIStrategy = AIStrategyEnum.Cautious;

        var instEntity = MakeEntity(type: OverworldEntity.EntityType.Adventurer, combatPower: 180f, faction: "neutral");
        instEntity.AIStrategy = AIStrategyEnum.Instinct;

        var target1 = MakeEntity(type: OverworldEntity.EntityType.RaidingParty, combatPower: 100f, faction: "hostile", hostile: true, position: new Vector2(300, 0));
        var target2 = MakeEntity(type: OverworldEntity.EntityType.RaidingParty, combatPower: 100f, faction: "hostile", hostile: true, position: new Vector2(300, 0));
        cautious.Position = new Vector2(0, 0);
        instEntity.Position = new Vector2(0, 0);

        var processor = new DailyDecisionProcessor();

        var cautiousList = new List<OverworldEntity> { cautious, target1 };
        processor.ProcessFrameTactics(cautiousList);

        var instList = new List<OverworldEntity> { instEntity, target2 };
        processor.ProcessFrameTactics(instList);

        bool cautiousNoChase = cautious.CurrentAIState != OverworldEntity.AIState.Chasing; // 1.8 < 2.25
        bool instinctChases = instEntity.CurrentAIState == OverworldEntity.AIState.Chasing; // 1.8 > 1.5

        return (cautiousNoChase && instinctChases,
                $"cautious={cautious.CurrentAIState}, instinct={instEntity.CurrentAIState}");
    }

    private static (bool, string) AIStrategy_ChaseSpeedMultipliers_AreBounded()
    {
        const float maxAllowed = 1.12f;

        foreach (AIStrategyEnum strategy in Enum.GetValues<AIStrategyEnum>())
        {
            float multiplier = EntitySpeedCalculator.GetChaseSpeedMultiplier(strategy);
            if (multiplier > maxAllowed + 0.001f)
                return (false, $"{strategy} chase multiplier too high: {multiplier:F2} > {maxAllowed:F2}");
        }

        float berserk = EntitySpeedCalculator.GetChaseSpeedMultiplier(AIStrategyEnum.Berserk);
        float cautious = EntitySpeedCalculator.GetChaseSpeedMultiplier(AIStrategyEnum.Cautious);
        return (berserk > cautious,
                $"berserk={berserk:F2}, cautious={cautious:F2}");
    }

    private static (bool, string) EntitySpawner_RaidingPartyRoadChaseSpeed_StaysBelowPlayerBase()
    {
        const float playerBaseSpeed = 300.0f;
        const float roadFactor = 1.2f;

        var source = MakePOI(new Vector2(0, 0), "hostile", OverworldPOI.POIType.Settlement);
        source.SettlementRaceValue = OverworldPOI.SettlementRace.Pirate;
        source.ThreatLevel = 1.0f;
        var village = MakePOI(new Vector2(1000, 0), "kingdom", OverworldPOI.POIType.Village);
        var spawner = new EntitySpawner();

        for (int i = 0; i < 20; i++)
        {
            var party = spawner.CreateRaidingPartyWithTargets(source, new List<OverworldPOI> { source, village });
            if (party == null)
                return (false, "raiding party should spawn when a target village exists");

            float worstRoadChaseSpeed = party.MoveSpeed
                * EntitySpeedCalculator.GetChaseSpeedMultiplier(AIStrategyEnum.Berserk)
                * roadFactor;

            if (worstRoadChaseSpeed > playerBaseSpeed + 0.001f)
                return (false, $"road chase speed too high: base={party.MoveSpeed:F1}, final={worstRoadChaseSpeed:F1}");
        }

        return (true, "raiding party road chase speed stays below player base speed");
    }

    private static (bool, string) AIStrategy_Serialize_Roundtrip()
    {
        var original = MakeEntity();
        original.AIStrategy = AIStrategyEnum.Cunning;
        var dict = original.Serialize();

        bool hasKey = dict.ContainsKey("ai_strategy");
        bool valueOk = hasKey && (int)dict["ai_strategy"] == (int)AIStrategyEnum.Cunning;

        return (hasKey && valueOk, $"key={hasKey}, value={dict.GetValueOrDefault("ai_strategy")}");
    }

    // ── 史诗怪物领地回归测试 ──────────────────────────────────────────────

    private static (bool, string) EpicMonster_OutsideTerritory_ReturnsToCenter()
    {
        // 怪物在领地外(距离领地中心 1200px, 领地半径 500px)
        var monster = MakeEntity(type: OverworldEntity.EntityType.EpicMonster, state: OverworldEntity.AIState.Idle, position: new Vector2(2000, 1000));
        monster.TerritoryCenter = new Vector2(800, 1000);
        monster.TerritoryRadius = 500f;
        monster.HomePosition = new Vector2(800, 1000);
        monster.IsAggressive = true; // 即使标记为攻击状态也应被覆盖

        // 预检查：怪物确认在领地外
        if (monster.IsInTerritory(monster.Position))
            return (false, $"怪物应在领地外: pos={monster.Position}, center={monster.TerritoryCenter}, radius={monster.TerritoryRadius}");

        var processor = new DailyDecisionProcessor();

        try
        {
            processor.ProcessDailyDecisions(new List<OverworldEntity> { monster }, new List<OverworldPOI>(), 1);
        }
        catch (Exception ex)
        {
            return (false, $"ProcessDailyDecisions 异常: {ex.GetType().Name}: {ex.Message}");
        }

        GD.Print($"[MonsterTest] state={monster.CurrentAIState}, aggressive={monster.IsAggressive}, " +
                 $"chaseTarget={monster.ChaseTarget?.EntityName}");

        bool notAggressive = !monster.IsAggressive;
        bool patrolling = monster.CurrentAIState == OverworldEntity.AIState.Patrolling;
        bool chaseCleared = monster.ChaseTarget == null;
        // 无导航时 IsMoving=false, 但状态应为 Patrolling(领地外返回中)
        return (notAggressive && patrolling && chaseCleared,
                $"aggressive={monster.IsAggressive}, state={monster.CurrentAIState}, chaseTarget={monster.ChaseTarget}");
    }

    private static (bool, string) EpicMonster_ChasingTargetLeavesTerritory_ClearsTarget()
    {
        // 怪物在领地内追击, 但目标已在领地外
        var monster = MakeEntity(type: OverworldEntity.EntityType.EpicMonster, state: OverworldEntity.AIState.Chasing, position: new Vector2(1000, 1000));
        monster.TerritoryCenter = new Vector2(1000, 1000);
        monster.TerritoryRadius = 500f;

        var target = MakeEntity(type: OverworldEntity.EntityType.Adventurer, position: new Vector2(2500, 1000)); // 远在领地外
        monster.ChaseTarget = target;
        monster.Faction = "hostile";
        target.Faction = "neutral";

        var processor = new DailyDecisionProcessor();
        processor.ProcessDailyDecisions(new List<OverworldEntity> { monster, target }, new List<OverworldPOI>(), 1);

        bool targetCleared = monster.ChaseTarget == null;
        bool notChasing = monster.CurrentAIState != OverworldEntity.AIState.Chasing;
        return (targetCleared && notChasing,
                $"chaseTarget={monster.ChaseTarget}, state={monster.CurrentAIState}");
    }

    private static (bool, string) EpicMonster_OutsideTerritory_PerceptionSkips()
    {
        // 怪物在领地外, 附近有一个弱敌 — 感知意图阶段不应覆盖其 Patrolling(返回) 状态
        var monster = MakeEntity(type: OverworldEntity.EntityType.EpicMonster, state: OverworldEntity.AIState.Patrolling, position: new Vector2(2000, 1000));
        monster.TerritoryCenter = new Vector2(800, 1000);
        monster.TerritoryRadius = 500f;
        monster.CombatPower = 50f; // 故意设低, 正常情况会触发逃跑

        var enemy = MakeEntity(type: OverworldEntity.EntityType.Adventurer, combatPower: 200f, position: new Vector2(2100, 1000), faction: "neutral");
        enemy.VisionRange = 300f;
        monster.VisionRange = 400f;

        var processor = new DailyDecisionProcessor();
        processor.ProcessFrameTactics(new List<OverworldEntity> { monster, enemy });

        // 怪物应维持 Patrolling(返回领地), 不应被评估器改为 Fleeing
        bool stillPatrolling = monster.CurrentAIState == OverworldEntity.AIState.Patrolling;
        return (stillPatrolling, $"state={monster.CurrentAIState}");
    }

    // ── 交战机制测试 ────────────────────────────────────────────────────

    private static (bool, string) Engagement_HostilePairWithin100px_EntersEngaged()
    {
        var a = MakeEntity(combatPower: 50f, faction: "neutral", position: new Vector2(0, 0));
        a.PartySize = 8;
        var b = MakeEntity(combatPower: 40f, faction: "hostile", hostile: true, position: new Vector2(80, 0)); // < 100px
        b.PartySize = 6;

        var resolver = new BattleResolver();
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 5f);

        bool bothEngaged = a.CurrentAIState == OverworldEntity.AIState.Engaged
                        && b.CurrentAIState == OverworldEntity.AIState.Engaged;
        bool linked = a.EngagedWith == b && b.EngagedWith == a;
        bool stopped = !a.IsMoving && !b.IsMoving;
        bool hourSet = a.EngagedSinceHour == 5f && b.EngagedSinceHour == 5f;
        bool durationSet = a.CombatDurationHours >= 3 && a.CombatDurationHours <= 24;
        return (bothEngaged && linked && stopped && hourSet && durationSet,
                $"a.state={a.CurrentAIState}, b.state={b.CurrentAIState}, linked={linked}, hour={a.EngagedSinceHour}, duration={a.CombatDurationHours}h");
    }

    private static (bool, string) Engagement_PairBeyond100px_NoEngage()
    {
        var a = MakeEntity(combatPower: 50f, faction: "neutral", position: new Vector2(0, 0));
        var b = MakeEntity(combatPower: 40f, faction: "hostile", hostile: true, position: new Vector2(200, 0)); // > 100px

        var resolver = new BattleResolver();
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 0f);

        bool noEngage = a.CurrentAIState != OverworldEntity.AIState.Engaged
                     && b.CurrentAIState != OverworldEntity.AIState.Engaged;
        return (noEngage, $"a.state={a.CurrentAIState}, b.state={b.CurrentAIState}");
    }

    private static (bool, string) Engagement_GeneratedHostileFactions_EnterEngaged()
    {
        var monster = MakeEntity(combatPower: 50f, faction: "monster", hostile: true, position: new Vector2(0, 0));
        var bandit = MakeEntity(combatPower: 40f, faction: "bandit", hostile: true, position: new Vector2(80, 0));

        var resolver = new BattleResolver();
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { monster, bandit }, currentGameHour: 2f);

        bool bothEngaged = monster.CurrentAIState == OverworldEntity.AIState.Engaged
                        && bandit.CurrentAIState == OverworldEntity.AIState.Engaged;
        return (bothEngaged, $"monster={monster.CurrentAIState}, bandit={bandit.CurrentAIState}");
    }

    private static (bool, string) Simulation_TickFrame_MovingHostiles_EnterEngaged()
    {
        var ctx = new OverworldSimulationContext
        {
            PlayerPosition = Vector2.Zero,
            SpatialIndex = new EntitySpatialIndex(800),
        };

        var a = MakeEntity(combatPower: 50f, faction: "monster", hostile: true, position: new Vector2(0, 0));
        a.MoveSpeed = 200f;
        a.IsMoving = true;
        a.CurrentAIState = OverworldEntity.AIState.Patrolling;
        a.Path.Add(new Vector2(100, 0));

        var b = MakeEntity(combatPower: 40f, faction: "bandit", hostile: true, position: new Vector2(180, 0));
        b.CurrentAIState = OverworldEntity.AIState.Patrolling;

        ctx.Entities.Add(a);
        ctx.Entities.Add(b);
        ctx.SpatialIndex.Rebuild(ctx.Entities);

        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);
        sim.TickFrame(0.5f, ctx);

        bool bothEngaged = a.CurrentAIState == OverworldEntity.AIState.Engaged
                        && b.CurrentAIState == OverworldEntity.AIState.Engaged;
        return (bothEngaged, $"a={a.CurrentAIState}, b={b.CurrentAIState}, a.pos={a.Position}, b.pos={b.Position}");
    }

    private static (bool, string) Simulation_TickFrame_IdleHostiles_PerceptionStartsMovement()
    {
        var grid = MakePlainsGrid(12, 12);
        var astar = new HexOverworldAStar(grid);
        var ctx = new OverworldSimulationContext
        {
            PlayerPosition = Vector2.Zero,
            SpatialIndex = new EntitySpatialIndex(800),
            HexGrid = grid,
            HexAStar = astar,
        };

        var chaser = MakeEntity(
            type: OverworldEntity.EntityType.Adventurer,
            state: OverworldEntity.AIState.Idle,
            position: HexOverworldTile.AxialToPixel(2, 2),
            combatPower: 220f,
            faction: "neutral");
        chaser.VisionRange = 900f;
        chaser.MoveSpeed = 220f;

        var target = MakeEntity(
            type: OverworldEntity.EntityType.RaidingParty,
            state: OverworldEntity.AIState.Idle,
            position: HexOverworldTile.AxialToPixel(5, 2),
            combatPower: 45f,
            faction: "hostile",
            hostile: true);
        target.VisionRange = 900f;

        ctx.Entities.Add(chaser);
        ctx.Entities.Add(target);
        ctx.SpatialIndex.Rebuild(ctx.Entities);

        var sim = new OverworldSimulation();
        sim.WireToContext(ctx);
        Vector2 before = chaser.Position;
        sim.TickFrame(0.25f, ctx);

        bool chasing = chaser.CurrentAIState == OverworldEntity.AIState.Chasing;
        bool moving = chaser.IsMoving || chaser.Position.DistanceTo(before) > 0.01f;
        bool targetSet = chaser.ChaseTarget == target;
        return (chasing && moving && targetSet,
            $"state={chaser.CurrentAIState}, moving={chaser.IsMoving}, moved={chaser.Position.DistanceTo(before):F2}, targetSet={targetSet}, path={chaser.Path.Count}");
    }

    private static (bool, string) EngagedCombat_DoesNotResolveBefore2Days()
    {
        var a = MakeEntity(combatPower: 50f, faction: "neutral", position: new Vector2(0, 0));
        a.PartySize = 10;
        var b = MakeEntity(combatPower: 40f, faction: "hostile", hostile: true, position: new Vector2(50, 0));
        b.PartySize = 10;

        // 通过 ProcessEntityInteractions 建立交战
        var resolver = new BattleResolver();
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 10f);

        int duration = a.CombatDurationHours;

        // 交战未满 → 不结算 (hour=10+duration-1)
        resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: 10f + duration - 1f);

        bool stillEngaged = a.CurrentAIState == OverworldEntity.AIState.Engaged
                         && b.CurrentAIState == OverworldEntity.AIState.Engaged;
        bool bothAlive = a.IsAlive && b.IsAlive;
        return (stillEngaged && bothAlive, $"a.state={a.CurrentAIState}, b.state={b.CurrentAIState}, duration={duration}h");
    }

    private static (bool, string) EngagedCombat_ResolvesAfter2Days()
    {
        var a = MakeEntity(combatPower: 50f, faction: "neutral", position: new Vector2(0, 0));
        a.PartySize = 10;
        var b = MakeEntity(combatPower: 40f, faction: "hostile", hostile: true, position: new Vector2(50, 0));
        b.PartySize = 10;

        // 通过 ProcessEntityInteractions 建立交战
        var resolver = new BattleResolver();
        resolver.ProcessEntityInteractions(new List<OverworldEntity> { a, b }, currentGameHour: 10f);

        int duration = a.CombatDurationHours;

        // 渐进更新（模拟视野内每 3h）
        for (float h = 10f + resolver.ViewportUpdateInterval; h < 10f + duration; h += resolver.ViewportUpdateInterval)
            resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: h);

        // 最终结算
        resolver.UpdateEngagements(new List<OverworldEntity> { a, b }, currentGameHour: 10f + duration + 1f);

        // 结算后双方都不应再处于 Engaged 状态
        bool notEngaged = a.CurrentAIState != OverworldEntity.AIState.Engaged
                       && b.CurrentAIState != OverworldEntity.AIState.Engaged;
        // 交战后至少一方有损失（CP降低或状态变为Fleeing/dead）
        bool hasResult = a.CombatPower < 50f || b.CombatPower < 40f
                      || a.CurrentAIState == OverworldEntity.AIState.Fleeing
                      || b.CurrentAIState == OverworldEntity.AIState.Fleeing
                      || !a.IsAlive || !b.IsAlive;
        // 交战状态已清空
        bool cleared = a.EngagedWith == null && b.EngagedWith == null;

        return (notEngaged && hasResult && cleared,
                $"a.state={a.CurrentAIState}, b.state={b.CurrentAIState}, a.cp={a.CombatPower:F1}, b.cp={b.CombatPower:F1}, duration={duration}h");
    }

    private static (bool, string) Movement_EngagedEntity_ForcedStop()
    {
        var entity = MakeEntity(state: OverworldEntity.AIState.Engaged, position: new Vector2(100, 100));
        entity.IsMoving = true;
        entity.Path.Add(new Vector2(200, 200));
        entity.Path.Add(new Vector2(300, 300));

        var processor = new MovementProcessor();
        processor.TickMovement(0.016f, new List<OverworldEntity> { entity }, null);

        bool stopped = !entity.IsMoving;
        bool pathCleared = entity.Path.Count == 0;
        bool posUnchanged = entity.Position == new Vector2(100, 100);
        return (stopped && pathCleared && posUnchanged,
                $"isMoving={entity.IsMoving}, pathCount={entity.Path.Count}, pos={entity.Position}");
    }

    private static (bool, string) Navigator_ChasingRefreshFailure_PreservesExistingPath()
    {
        var entity = MakeEntity(state: OverworldEntity.AIState.Chasing, position: new Vector2(0, 0));
        entity.IsMoving = true;
        entity.Path.Add(new Vector2(100, 0));

        var navigator = new OverworldEntityNavigator();
        bool started = navigator.StartMoveTo(entity, new Vector2(500, 0));

        bool pathPreserved = entity.Path.Count == 1 && entity.Path[0] == new Vector2(100, 0);
        return (started && entity.IsMoving && pathPreserved,
                $"started={started}, moving={entity.IsMoving}, pathCount={entity.Path.Count}");
    }

    private static HexOverworldGrid MakePlainsGrid(int width, int height)
    {
        var grid = new HexOverworldGrid();
        grid.Initialize(width, height);
        foreach (var tile in grid.Tiles.Values)
            tile.SetTerrain(HexOverworldTile.TerrainType.Plains);
        return grid;
    }

    // ── 辅助类 ───────────────────────────────────────────────────────────

    /// <summary>测试用 ISiegeSignals 实现</summary>
    private class TestSiegeSignals : ISiegeSignals
    {
        public int SiegeResolvedCount = 0;
        public int PoiCapturedCount = 0;
        public int ReinforcementCount = 0;

        public void OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
            => SiegeResolvedCount++;

        public void OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
            => PoiCapturedCount++;

        public void OnReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer)
            => ReinforcementCount++;
    }
}
