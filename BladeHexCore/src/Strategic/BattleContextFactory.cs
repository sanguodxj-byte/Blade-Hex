// BattleContextFactory.cs
// 战斗上下文工厂 — 收敛大地图实体/POI 到战斗场景的上下文创建
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗上下文工厂。
///
/// 职责边界：
/// - 大地图场景只提供触发源、玩家位置、网格与随机种子。
/// - 工厂负责推导遭遇坐标、地形、部署描述与上下文元数据。
/// - CombatScene 只消费 BattleContext，不反查 OverworldEntity。
/// </summary>
public static class BattleContextFactory
{
    public static BattleContext CreatePlayerVsEntity(
        OverworldEntity defender,
        HexOverworldGrid? grid,
        Vector2 playerPixelPosition,
        int seed = 0)
    {
        var coord = HexOverworldTile.PixelToAxial(playerPixelPosition.X, playerPixelPosition.Y);
        var context = BattleContext.CreateFromEncounter(
            attacker: null,
            defender: defender,
            poi: null,
            grid: grid,
            coord: coord);

        context.Seed = seed != 0 ? seed : (int)GD.Randi();
        context.EncounterPosition = new Vector2I((int)defender.Position.X, (int)defender.Position.Y);

        if (grid != null)
        {
            var tile = grid.GetTileAtPixel(defender.Position.X, defender.Position.Y);
            if (tile != null)
                context.Terrain = tile.Terrain;
        }

        return context;
    }

    public static BattleContext CreatePlayerInitiatedEntityBattle(
        OverworldEntity defender,
        HexOverworldGrid? grid,
        Vector2 playerPixelPosition,
        IEnumerable<OverworldEntity> nearbyEntities,
        WorldEventEngine? engine = null,
        Hero.HeroRelationMatrix? relationMatrix = null,
        string playerFaction = OverworldHostility.DefaultPlayerFaction,
        int seed = 0)
    {
        var context = CreatePlayerVsEntity(defender, grid, playerPixelPosition, seed);
        var playerSide = new List<OverworldEntity>();
        var enemySide = new List<OverworldEntity> { defender };
        var playerProxy = new OverworldEntity
        {
            EntityName = "Player",
            Faction = OverworldHostility.NormalizePlayerFaction(playerFaction),
            IsAlive = true,
            IsHostileToPlayer = false,
            Position = playerPixelPosition,
        };

        foreach (var entity in nearbyEntities.Distinct())
        {
            if (!CanPullIntoPlayerBattle(entity, defender.Position))
                continue;

            if (entity == defender)
                continue;

            bool? joinsPlayer = DeterminePlayerBattleSide(entity, defender, playerProxy, engine, relationMatrix);
            if (joinsPlayer == true)
                playerSide.Add(entity);
            else if (joinsPlayer == false)
                enemySide.Add(entity);
        }

        var opportunity = new JoinOpportunity
        {
            Type = WarBattleType.FieldBattle,
            Attacker = playerProxy,
            DefenderEntity = defender,
            WorldPosition = defender.Position,
            Distance = playerPixelPosition.DistanceTo(defender.Position),
            Attackers = playerSide,
            Defenders = enemySide,
            AttackerTotalPower = playerSide.Sum(EffectivePower),
            DefenderTotalPower = enemySide.Sum(EffectivePower),
        };

        context.WarJoinOppRef = opportunity;
        context.PlayerJoinedAsAttacker = true;
        context.JoinedAttackers = playerSide.Distinct().ToList();
        context.JoinedDefenders = enemySide.Distinct().ToList();
        context.AttackerDeployment = BattlefieldInterventionService.BuildDeployment(playerSide, isPlayerSide: true);
        context.DefenderDeployment = BattlefieldInterventionService.BuildDeployment(enemySide, isPlayerSide: false);

        return context;
    }

    public static BattleContext CreatePlayerJoinedFieldBattle(
        JoinOpportunity opportunity,
        bool joinAttacker,
        HexOverworldGrid? grid,
        Vector2 playerPixelPosition,
        IEnumerable<OverworldEntity> nearbyEntities,
        WorldEventEngine? engine = null,
        Hero.HeroRelationMatrix? relationMatrix = null,
        int seed = 0)
    {
        BattlefieldInterventionService.ExpandOpportunityParticipants(
            opportunity,
            nearbyEntities,
            engine,
            relationMatrix);
        NormalizeOpportunityParticipants(opportunity);

        var playerSide = joinAttacker ? opportunity.Attackers : opportunity.Defenders;
        var enemySide = joinAttacker ? opportunity.Defenders : opportunity.Attackers;
        var playerPrimary = playerSide.FirstOrDefault();
        var enemyPrimary = enemySide.FirstOrDefault();

        var coord = HexOverworldTile.PixelToAxial(playerPixelPosition.X, playerPixelPosition.Y);
        var context = BattleContext.CreateFromEncounter(
            attacker: playerPrimary,
            defender: enemyPrimary,
            poi: null,
            grid: grid,
            coord: coord);

        context.WarJoinOppRef = opportunity;
        context.PlayerJoinedAsAttacker = joinAttacker;
        context.SourceBattlefieldId = opportunity.BattlefieldId;
        context.JoinedAttackers = opportunity.Attackers.Distinct().ToList();
        context.JoinedDefenders = opportunity.Defenders.Distinct().ToList();
        context.AttackerDeployment = BattlefieldInterventionService.BuildDeployment(playerSide, isPlayerSide: true);
        context.DefenderDeployment = BattlefieldInterventionService.BuildDeployment(enemySide, isPlayerSide: false);
        context.Seed = seed != 0 ? seed : (int)GD.Randi();
        context.EncounterPosition = new Vector2I(
            (int)(opportunity.HasWorldPosition ? opportunity.WorldPosition.X : playerPixelPosition.X),
            (int)(opportunity.HasWorldPosition ? opportunity.WorldPosition.Y : playerPixelPosition.Y));

        if (grid != null)
        {
            var terrainPos = opportunity.HasWorldPosition ? opportunity.WorldPosition : playerPixelPosition;
            var tile = grid.GetTileAtPixel(terrainPos.X, terrainPos.Y);
            if (tile != null)
                context.Terrain = tile.Terrain;
        }

        return context;
    }

    private static bool CanPullIntoPlayerBattle(OverworldEntity entity, Vector2 center)
        => entity.IsAlive
           && entity.Lod != OverworldEntity.EntityLod.Hibernated
           && !IsNeutralFaction(entity.Faction)
           && entity.Position.DistanceTo(center) <= BattlefieldInterventionService.PlayerJoinPullRadius;

    private static void NormalizeOpportunityParticipants(JoinOpportunity opportunity)
    {
        var attackers = opportunity.Attackers.Distinct().ToList();
        var defenders = opportunity.Defenders.Distinct().ToList();

        attackers = attackers
            .Where(entity => entity != opportunity.DefenderEntity)
            .ToList();
        if (opportunity.Attacker != null && !attackers.Contains(opportunity.Attacker))
            attackers.Add(opportunity.Attacker);

        var attackerSet = attackers.ToHashSet();
        defenders = defenders
            .Where(entity => entity == opportunity.DefenderEntity || !attackerSet.Contains(entity))
            .ToList();
        if (opportunity.DefenderEntity != null && !defenders.Contains(opportunity.DefenderEntity))
            defenders.Add(opportunity.DefenderEntity);

        opportunity.Attackers = attackers;
        opportunity.Defenders = defenders;
    }

    private static bool? DeterminePlayerBattleSide(
        OverworldEntity entity,
        OverworldEntity defender,
        OverworldEntity playerProxy,
        WorldEventEngine? engine,
        Hero.HeroRelationMatrix? relationMatrix)
    {
        bool alliedWithPlayer = AreAllied(entity, playerProxy, engine);
        bool alliedWithDefender = AreAllied(entity, defender, engine);
        bool hostileToPlayer = OverworldHostility.AreHostileToPlayer(entity, playerProxy, engine, relationMatrix);
        bool hostileToDefender = OverworldHostility.AreHostile(entity, defender, engine, relationMatrix);

        if ((alliedWithPlayer || hostileToDefender) && !hostileToPlayer)
            return true;
        if ((alliedWithDefender || hostileToPlayer) && !hostileToDefender)
            return false;
        if (hostileToPlayer && !hostileToDefender)
            return false;
        if (!hostileToPlayer && hostileToDefender)
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

    private static float EffectivePower(OverworldEntity entity)
        => entity.CombatPower * System.Math.Max(1, entity.PartySize);
}
