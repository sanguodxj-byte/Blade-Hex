using System.Collections.Generic;
using System.Linq;
using System;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

public static class BattlefieldInterventionService
{
    public const float PlayerJoinPullRadius = 420f;
    public const float AiBattlefieldSenseRadius = 520f;

    public static void ExpandOpportunityParticipants(
        JoinOpportunity opportunity,
        IEnumerable<OverworldEntity> entities,
        WorldEventEngine? engine = null,
        Hero.HeroRelationMatrix? relationMatrix = null,
        float radius = PlayerJoinPullRadius)
    {
        if (opportunity.Type != WarBattleType.FieldBattle)
            return;

        EnsurePrimaryParticipants(opportunity);
        Vector2 center = opportunity.HasWorldPosition
            ? opportunity.WorldPosition
            : GetBattleCenter(opportunity);

        foreach (var entity in entities)
        {
            if (!CanConsiderForBattlefield(entity)) continue;
            if (IsAlreadyInOpportunity(opportunity, entity)) continue;
            if (entity.Position.DistanceTo(center) > radius) continue;

            bool? side = DetermineSide(entity, opportunity.Attackers, opportunity.Defenders, engine, relationMatrix);
            if (side == true)
                opportunity.Attackers.Add(entity);
            else if (side == false)
                opportunity.Defenders.Add(entity);
        }

        RefreshTotals(opportunity);
    }

    public static void ProcessAiBattlefieldResponses(
        List<OverworldEntity> entities,
        BattleResolver resolver,
        WorldEventEngine? engine = null,
        Hero.HeroRelationMatrix? relationMatrix = null,
        float currentGameHour = 0f,
        float radius = AiBattlefieldSenseRadius,
        Action<OverworldEntity>? onFlee = null)
    {
        foreach (var battlefield in resolver.Battlefields.ToList())
        {
            if (battlefield.IsResolved || battlefield.Attackers.Count == 0 || battlefield.Defenders.Count == 0)
                continue;

            foreach (var entity in entities)
            {
                if (!CanConsiderForBattlefield(entity)) continue;
                if (!CanAiReact(entity)) continue;
                if (battlefield.AllParticipants.Contains(entity)) continue;
                if (entity.Position.DistanceTo(battlefield.Position) > radius) continue;

                bool? side = DetermineSide(entity, battlefield.Attackers, battlefield.Defenders, engine, relationMatrix);
                if (!side.HasValue)
                    continue;

                float ownSidePower = side.Value ? battlefield.AttackerPower() : battlefield.DefenderPower();
                float enemySidePower = side.Value ? battlefield.DefenderPower() : battlefield.AttackerPower();
                float powerAfterJoin = ownSidePower + EffectivePower(entity);

                if (ShouldJoin(entity, powerAfterJoin, enemySidePower))
                    resolver.JoinExistingBattlefield(entity, battlefield, side.Value, engine, currentGameHour);
                else
                {
                    FleeFromBattlefield(entity, battlefield, engine, relationMatrix);
                    onFlee?.Invoke(entity);
                }
            }
        }
    }

    public static BattleUnitDeployment[] BuildDeployment(IEnumerable<OverworldEntity> entities, bool isPlayerSide)
    {
        var result = new List<BattleUnitDeployment>();
        foreach (var entity in entities.Distinct())
        {
            if (!entity.IsAlive) continue;
            foreach (var deployment in EntityCombatBridge.GetDeployment(entity, isPlayerSide))
            {
                deployment.IsPlayerControlled = isPlayerSide;
                result.Add(deployment);
            }
        }
        return result.ToArray();
    }

    private static bool CanConsiderForBattlefield(OverworldEntity entity)
        => entity.IsAlive
           && entity.Lod != OverworldEntity.EntityLod.Hibernated
           && !IsNeutralFaction(entity.Faction);

    private static bool CanAiReact(OverworldEntity entity)
        => entity.CurrentAIState != OverworldEntity.AIState.Engaged
           && entity.CurrentAIState != OverworldEntity.AIState.Besieging
           && entity.CurrentAIState != OverworldEntity.AIState.Recruiting
           && entity.CurrentAIState != OverworldEntity.AIState.Escorting;

    private static void EnsurePrimaryParticipants(JoinOpportunity opportunity)
    {
        if (opportunity.Attacker != null && !opportunity.Attackers.Contains(opportunity.Attacker))
            opportunity.Attackers.Add(opportunity.Attacker);
        if (opportunity.DefenderEntity != null && !opportunity.Defenders.Contains(opportunity.DefenderEntity))
            opportunity.Defenders.Add(opportunity.DefenderEntity);
    }

    private static bool IsAlreadyInOpportunity(JoinOpportunity opportunity, OverworldEntity entity)
        => opportunity.Attackers.Contains(entity) || opportunity.Defenders.Contains(entity);

    private static bool? DetermineSide(
        OverworldEntity entity,
        IReadOnlyList<OverworldEntity> attackers,
        IReadOnlyList<OverworldEntity> defenders,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix)
    {
        bool hostileToAttackers = attackers.Any(e => OverworldHostility.AreHostile(entity, e, engine, relationMatrix));
        bool hostileToDefenders = defenders.Any(e => OverworldHostility.AreHostile(entity, e, engine, relationMatrix));
        bool alliedWithAttackers = attackers.Any(e => AreAllied(entity, e, engine));
        bool alliedWithDefenders = defenders.Any(e => AreAllied(entity, e, engine));

        if ((alliedWithAttackers || hostileToDefenders) && !hostileToAttackers)
            return true;
        if ((alliedWithDefenders || hostileToAttackers) && !hostileToDefenders)
            return false;
        if (hostileToAttackers && !hostileToDefenders)
            return false;
        if (!hostileToAttackers && hostileToDefenders)
            return true;

        return null;
    }

    private static bool AreAllied(OverworldEntity a, OverworldEntity b, WorldEventEngine? engine)
    {
        if (IsNeutralFaction(a.Faction) || IsNeutralFaction(b.Faction))
            return false;
        if (a.Faction == b.Faction)
            return true;
        return engine != null && engine.AreAllied(a.Faction, b.Faction);
    }

    private static bool IsNeutralFaction(string faction)
        => string.Equals(faction, "neutral", System.StringComparison.OrdinalIgnoreCase);

    private static bool ShouldJoin(OverworldEntity entity, float ownSidePower, float enemySidePower)
    {
        float ratio = ownSidePower / System.Math.Max(enemySidePower, 1f);
        float joinThreshold = entity.AIStrategy switch
        {
            AIStrategyEnum.Reckless => 0.7f,
            AIStrategyEnum.Berserk => 0.35f,
            AIStrategyEnum.Cautious => 1.25f,
            AIStrategyEnum.Tactical => 0.9f,
            _ => 1.0f,
        };

        return ratio >= joinThreshold;
    }

    private static void FleeFromBattlefield(
        OverworldEntity entity,
        Battlefield battlefield,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix)
    {
        entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
        entity.ChaseTarget = null;
        entity.CurrentTacticalTarget = GetNearestEnemyOnBattlefield(entity, battlefield, engine, relationMatrix);
        entity.LastIntentSummary = "逃离战场";
    }

    private static OverworldEntity? GetNearestEnemyOnBattlefield(
        OverworldEntity entity,
        Battlefield battlefield,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix)
    {
        return battlefield.AllParticipants
            .Where(p => OverworldHostility.AreHostile(entity, p, engine, relationMatrix))
            .OrderBy(p => p.Position.DistanceTo(entity.Position))
            .FirstOrDefault();
    }

    private static float EffectivePower(OverworldEntity entity)
        => entity.CombatPower * System.Math.Max(1, entity.PartySize);

    private static Vector2 GetBattleCenter(JoinOpportunity opportunity)
    {
        var participants = opportunity.AllParticipants().ToList();
        if (participants.Count == 0)
            return Vector2.Zero;

        Vector2 sum = Vector2.Zero;
        foreach (var participant in participants)
            sum += participant.Position;
        return sum / participants.Count;
    }

    private static void RefreshTotals(JoinOpportunity opportunity)
    {
        opportunity.AttackerTotalPower = opportunity.Attackers.Sum(EffectivePower);
        opportunity.DefenderTotalPower = opportunity.Defenders.Sum(EffectivePower);
    }
}
