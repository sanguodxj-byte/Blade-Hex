// LosCore.cs
// Pure-Core line-of-sight, cover, high-ground, and river-cross helpers.
// Operates on IBattleField so headless simulation and the live combat
// scene share the same logic.
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Combat;

/// <summary>Result of a high-ground check between attacker and defender.</summary>
public struct HighGroundResultCore
{
    public bool Advantage;
    public bool Disadvantage;
    public int RangeBonus;
}

/// <summary>
/// Pure LOS / cover / elevation / terrain rules, decoupled from any rendering
/// layer. The Frontend <c>LineOfSight</c> wrapper forwards to this and supplies
/// a <c>HexGrid</c>-backed <see cref="IBattleField"/>; headless simulation
/// supplies its own implementation.
/// </summary>
public static class LosCore
{
    // ========================================================================
    // Penalties applied per intervening tile.
    // ========================================================================

    /// <summary>Penalty per tile that fully blocks line of sight or has full cover.</summary>
    public const int BlockerPenalty = 4;

    /// <summary>Penalty per tile with half cover.</summary>
    public const int HalfCoverPenalty = 2;

    /// <summary>Penalty per intervening unit (friend or foe).</summary>
    public const int UnitInPathPenalty = 2;

    /// <summary>
    /// Sum of accuracy penalties caused by tiles and units between
    /// <paramref name="from"/> and <paramref name="to"/>. Returns a non-positive
    /// integer; pass directly to <c>CombatRuleEngine.AttackInput.AccuracyMod</c>.
    ///
    /// High-ground attackers (elevation &gt; tile elevation + 1) ignore terrain
    /// penalties on tiles they shoot over, but unit penalties still apply
    /// because intervening units block the projectile path regardless of height.
    /// </summary>
    /// <param name="from">Attacker tile.</param>
    /// <param name="to">Defender tile.</param>
    /// <param name="field">Battlefield data source.</param>
    /// <param name="isOccupiedByUnit">
    /// Predicate returning true if a tile holds a living unit (friend or foe)
    /// other than the attacker/defender themselves. Pass null to disable
    /// unit-blocking detection (use this if your sim doesn't track positions).
    /// </param>
    public static int GetPathPenalty(
        Vector2I from, Vector2I to,
        IBattleField field,
        System.Func<Vector2I, bool>? isOccupiedByUnit = null)
    {
        if (from == to) return 0;

        var line = GetHexLine(from, to);
        int fromElev = field.GetElevation(from);
        int toElev = field.GetElevation(to);
        int penalty = 0;

        // 高打低：免除所有地形障碍物检测（视野无遮挡）
        bool highGroundBypass = fromElev > toElev;

        for (int i = 1; i < line.Count - 1; i++)
        {
            var pos = line[i];
            if (!field.IsValid(pos)) continue;

            // Terrain penalty (waived if attacker has high ground over target).
            if (!highGroundBypass)
            {
                int cellElev = field.GetElevation(pos);
                bool seesOverTerrain = fromElev > cellElev + 1;
                if (!seesOverTerrain)
                {
                    if (field.BlocksLineOfSight(pos)) penalty -= BlockerPenalty;
                    else
                    {
                        int cover = field.GetCoverLevel(pos);
                        if (cover >= 2)      penalty -= BlockerPenalty;
                        else if (cover == 1) penalty -= HalfCoverPenalty;
                    }
                }
            }

            // Unit-in-path penalty (always applies, even with high ground).
            if (isOccupiedByUnit != null && isOccupiedByUnit(pos))
                penalty -= UnitInPathPenalty;
        }
        return penalty;
    }

    /// <summary>
    /// True iff the line is unobstructed (no full blockers, no full cover).
    /// </summary>
    /// <remarks>
    /// Deprecated for the main combat path: use <see cref="GetPathPenalty"/>
    /// instead. Kept for callers that need a binary check (e.g. spell line
    /// templates that physically cannot reach through a wall).
    /// </remarks>
    [System.Obsolete("Vision system removed: use GetPathPenalty for combat. HasLos kept only for hard-block checks (spell walls etc).")]
    public static bool HasLos(Vector2I from, Vector2I to, IBattleField field)
    {
        if (from == to) return true;

        var line = GetHexLine(from, to);
        int fromElev = field.GetElevation(from);

        for (int i = 1; i < line.Count - 1; i++)
        {
            var pos = line[i];
            if (!field.IsValid(pos)) continue;

            // Observer can shoot over obstacles >=2 elevation levels lower.
            int cellElev = field.GetElevation(pos);
            if (fromElev > cellElev + 1) continue;

            if (field.BlocksLineOfSight(pos)) return false;
            if (field.GetCoverLevel(pos) >= 2) return false;
        }
        return true;
    }

    /// <summary>Cover on the defender's tile. 0=none, 1=half, 2=full.</summary>
    public static int GetCoverLevel(Vector2I targetPos, IBattleField field)
    {
        if (!field.IsValid(targetPos)) return 0;
        return field.GetCoverLevel(targetPos);
    }

    /// <summary>Attacker high ground vs defender low ground = advantage; reversed = disadvantage.</summary>
    public static HighGroundResultCore GetHighGroundBonus(Vector2I attackerPos, Vector2I defenderPos, IBattleField field)
    {
        if (!field.IsValid(attackerPos) || !field.IsValid(defenderPos))
            return new HighGroundResultCore();

        int aElev = field.GetElevation(attackerPos);
        int dElev = field.GetElevation(defenderPos);
        int diff = aElev - dElev;

        if (diff >= 2)  return new HighGroundResultCore { Advantage = true,  RangeBonus = 2 };
        if (diff == 1)  return new HighGroundResultCore { Advantage = true,  RangeBonus = 1 };
        if (diff == -1) return new HighGroundResultCore { Disadvantage = true, RangeBonus = -1 };
        if (diff <= -2) return new HighGroundResultCore { Disadvantage = true, RangeBonus = -2 };
        return default;
    }

    /// <summary>True if any tile along the line is shallow water or swamp.</summary>
    public static bool HasRiverCrossingPenalty(Vector2I attackerPos, Vector2I defenderPos, IBattleField field)
    {
        var line = GetHexLine(attackerPos, defenderPos);
        foreach (var pos in line)
        {
            if (!field.IsValid(pos)) continue;
            var t = field.GetTerrainType(pos);
            if (t == BattleCellData.TerrainType.ShallowWater
             || t == BattleCellData.TerrainType.Swamp) return true;
        }
        return false;
    }

    /// <summary>Discrete hex line between two axial coordinates. Pure: no field needed.</summary>
    public static List<Vector2I> GetHexLine(Vector2I from, Vector2I to)
    {
        var result = new List<Vector2I>();
        int dist = HexUtils.Distance(from.X, from.Y, to.X, to.Y);
        if (dist <= 0) { result.Add(from); return result; }

        for (int i = 0; i <= dist; i++)
        {
            float t = (float)i / dist;
            float q = Mathf.Lerp(from.X, to.X, t);
            float r = Mathf.Lerp(from.Y, to.Y, t);
            result.Add(HexUtils.HexRound(new Vector2(q, r)));
        }
        return result;
    }
}
