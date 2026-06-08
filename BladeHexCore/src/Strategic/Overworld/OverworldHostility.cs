using System;
using System.Collections.Generic;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// Shared overworld hostility rules used by perception, encounters, and contact battles.
/// </summary>
public static class OverworldHostility
{
    private static readonly HashSet<string> IntrinsicHostileFactions = new(StringComparer.OrdinalIgnoreCase)
    {
        "hostile",
        "hostile_lord",
        "monster",
        "bandit",
        "pirate",
        "raider",
        "robber",
        "outlaw",
    };

    public static bool AreHostile(
        OverworldEntity a,
        OverworldEntity b,
        WorldEventEngine? engine = null,
        Hero.HeroRelationMatrix? relationMatrix = null)
    {
        if (a == b) return false;
        if (a.Faction == b.Faction) return false;

        if (engine != null)
        {
            if (engine.AreAllied(a.Faction, b.Faction)) return false;
            if (engine.AreAtWar(a.Faction, b.Faction)) return true;
        }

        if (IsPlayerFaction(a.Faction))
            return b.IsHostileToPlayer || IsIntrinsicHostileFaction(b.Faction);
        if (IsPlayerFaction(b.Faction))
            return a.IsHostileToPlayer || IsIntrinsicHostileFaction(a.Faction);

        if (IsIntrinsicHostileFaction(a.Faction) || IsIntrinsicHostileFaction(b.Faction))
            return true;

        if (engine != null && relationMatrix != null &&
            a.IsNamedCharacter && b.IsNamedCharacter &&
            !string.IsNullOrEmpty(a.HeroId) && !string.IsNullOrEmpty(b.HeroId))
        {
            int factionRelation = engine.GetRelation(a.Faction, b.Faction);
            int personalRelation = relationMatrix.Get(a.HeroId, b.HeroId);
            float weight = Diplomacy.DiplomacyBalanceConfig.Load().PersonalRelationWeight;
            int effectiveRelation = (int)(factionRelation + personalRelation * weight);

            if (effectiveRelation <= -60)
                return true;
        }

        return false;
    }

    public static bool IsIntrinsicHostileFaction(string faction)
        => IntrinsicHostileFactions.Contains(faction ?? "");

    private static bool IsPlayerFaction(string faction)
        => string.Equals(faction, "player", StringComparison.OrdinalIgnoreCase);
}
