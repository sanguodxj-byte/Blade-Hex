// LosCoreTests.cs
// Pure-rule LOS tests. Use TerrainBattleField to construct controlled scenarios.
//
// Note (vision system removed): GetPathPenalty replaces the old binary
// HasLos check. Tests focus on accumulated accuracy penalty for terrain
// + intervening units; HasLos is kept only for hard blocker checks (e.g.
// physical wall blocking a spell line) and tested separately.
using System.Collections.Generic;
using BladeHex.Combat;
using BladeHex.Combat.Headless;
using BladeHex.Data;
using Godot;

namespace BladeHex.Combat.Tests;

public static class LosCoreTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;
        foreach (var (name, run) in EnumerateTests())
        {
            var (ok, msg) = run();
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else    { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string, System.Func<(bool, string)>)> EnumerateTests()
    {
        yield return ("Penalty_FlatField_Zero",                 Penalty_FlatField_Zero);
        yield return ("Penalty_BlockerTile_Penalizes",          Penalty_BlockerTile_Penalizes);
        yield return ("Penalty_FullCoverTile_PenalizesSame",    Penalty_FullCoverTile_PenalizesSame);
        yield return ("Penalty_HalfCoverTile_HalfPenalty",      Penalty_HalfCoverTile_HalfPenalty);
        yield return ("Penalty_Stacking_TwoBlockers",           Penalty_Stacking_TwoBlockers);
        yield return ("Penalty_HighGroundIgnoresTerrain",       Penalty_HighGroundIgnoresTerrain);
        yield return ("Penalty_UnitInPath_Penalizes",           Penalty_UnitInPath_Penalizes);
        yield return ("Penalty_UnitInPath_StacksWithCover",     Penalty_UnitInPath_StacksWithCover);

        yield return ("HasLos_FlatField_True",                  HasLos_FlatField_True);
        yield return ("HasLos_BlockedByMountain",               HasLos_BlockedByMountain);
        yield return ("HighGround_AdvantageWhenHigher",         HighGround_AdvantageWhenHigher);
        yield return ("HighGround_DisadvantageWhenLower",       HighGround_DisadvantageWhenLower);
        yield return ("River_CrossingPenaltyDetected",          River_CrossingPenaltyDetected);
    }

    // ========================================================================
    // GetPathPenalty
    // ========================================================================

    private static (bool, string) Penalty_FlatField_Zero()
    {
        var field = new FlatBattleField();
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (p == 0, $"expected 0 penalty on flat field, got {p}");
    }

    private static (bool, string) Penalty_BlockerTile_Penalizes()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(2, 0)).BlocksLos = true;
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (p == -LosCore.BlockerPenalty, $"expected -{LosCore.BlockerPenalty}, got {p}");
    }

    private static (bool, string) Penalty_FullCoverTile_PenalizesSame()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(2, 0)).CoverLevel = 2;
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (p == -LosCore.BlockerPenalty, $"full cover should equal blocker penalty, got {p}");
    }

    private static (bool, string) Penalty_HalfCoverTile_HalfPenalty()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(2, 0)).CoverLevel = 1;
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (p == -LosCore.HalfCoverPenalty, $"expected -{LosCore.HalfCoverPenalty}, got {p}");
    }

    private static (bool, string) Penalty_Stacking_TwoBlockers()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(1, 0)).BlocksLos = true;
        field.SetTile(new Vector2I(3, 0)).CoverLevel = 1;
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        int expected = -LosCore.BlockerPenalty - LosCore.HalfCoverPenalty;
        return (p == expected, $"expected {expected}, got {p}");
    }

    private static (bool, string) Penalty_HighGroundIgnoresTerrain()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(0, 0)).Elevation = 3;
        var blocker = field.SetTile(new Vector2I(2, 0));
        blocker.Elevation = 1;
        blocker.BlocksLos = true;
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (p == 0, $"high-ground attacker should bypass terrain blocker, got {p}");
    }

    private static (bool, string) Penalty_UnitInPath_Penalizes()
    {
        var field = new FlatBattleField();
        var occupied = new HashSet<Vector2I> { new(2, 0) };
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field,
            occupied.Contains);
        return (p == -LosCore.UnitInPathPenalty, $"expected -{LosCore.UnitInPathPenalty}, got {p}");
    }

    private static (bool, string) Penalty_UnitInPath_StacksWithCover()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(2, 0)).CoverLevel = 1;
        var occupied = new HashSet<Vector2I> { new(3, 0) };
        int p = LosCore.GetPathPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field,
            occupied.Contains);
        int expected = -LosCore.HalfCoverPenalty - LosCore.UnitInPathPenalty;
        return (p == expected, $"expected {expected}, got {p}");
    }

    // ========================================================================
    // HasLos (hard blocker check, kept for special cases like spell walls)
    // ========================================================================

#pragma warning disable CS0618  // testing the deprecated method intentionally
    private static (bool, string) HasLos_FlatField_True()
    {
        var field = new FlatBattleField();
        bool los = LosCore.HasLos(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (los, "expected LOS=true on flat field");
    }

    private static (bool, string) HasLos_BlockedByMountain()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(2, 0)).BlocksLos = true;
        bool los = LosCore.HasLos(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (!los, "expected LOS=false through mountain");
    }
#pragma warning restore CS0618

    // ========================================================================
    // High ground / river
    // ========================================================================

    private static (bool, string) HighGround_AdvantageWhenHigher()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(0, 0)).Elevation = 3;
        field.SetTile(new Vector2I(5, 0)).Elevation = 1;
        var hg = LosCore.GetHighGroundBonus(new Vector2I(0, 0), new Vector2I(5, 0), field);
        if (!hg.Advantage) return (false, "expected attacker advantage");
        if (hg.RangeBonus != 2) return (false, $"expected RangeBonus=2, got {hg.RangeBonus}");
        return (true, "");
    }

    private static (bool, string) HighGround_DisadvantageWhenLower()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(0, 0)).Elevation = 1;
        field.SetTile(new Vector2I(5, 0)).Elevation = 3;
        var hg = LosCore.GetHighGroundBonus(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (hg.Disadvantage, "expected attacker disadvantage");
    }

    private static (bool, string) River_CrossingPenaltyDetected()
    {
        var field = new TerrainBattleField();
        field.SetTile(new Vector2I(2, 0)).TerrainType = BattleCellData.TerrainType.ShallowWater;
        bool penalty = LosCore.HasRiverCrossingPenalty(new Vector2I(0, 0), new Vector2I(5, 0), field);
        return (penalty, "expected river-crossing penalty when path passes through shallow water");
    }
}
