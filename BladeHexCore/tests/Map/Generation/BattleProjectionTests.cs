// BattleProjectionTests.cs
// 战斗投影单元测试 — 服务于 spec combat-hex-from-overworld-state R2 / R8#1。
//
// 覆盖契约:
//   - R2#3: 单 tile 投影到原点
//   - R2#4: water sample 用 effScale = scale × 0.6
//   - R2#5: 相同输入产出 byte-identical 输出(确定性 + axial 字典序排序)
//   - R8#1: 多次调用排序稳定
using System.Collections.Generic;
using BladeHex.Map.Generation;
using Godot;

namespace BladeHex.Map.Tests;

public static class BattleProjectionTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Project_EmptySampleSet_ReturnsEmptyList), Project_EmptySampleSet_ReturnsEmptyList);
        yield return Run(nameof(Project_SingleTile_ReturnsOrigin), Project_SingleTile_ReturnsOrigin);
        yield return Run(nameof(Project_LandSamples_ScaleProportionalToRadius), Project_LandSamples_ScaleProportionalToRadius);
        yield return Run(nameof(Project_WaterSample_UsesReducedScale), Project_WaterSample_UsesReducedScale);
        yield return Run(nameof(Project_SameInputBitIdentical), Project_SameInputBitIdentical);
        yield return Run(nameof(Project_SortedByAxialAsc), Project_SortedByAxialAsc);
        yield return Run(nameof(Project_BridgeSampleMarkedAsBridge), Project_BridgeSampleMarkedAsBridge);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try { var (ok, msg) = test(); return (name, ok, msg); }
        catch (System.Exception ex) { return (name, false, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    // ========================================================================

    private static HexOverworldTile MakeTile(Vector2I coord, HexOverworldTile.TerrainType terrain, bool isRoad = false, bool isRiver = false)
    {
        var t = HexOverworldTile.CreateEmpty(coord.X, coord.Y);
        t.Terrain = terrain;
        t.IsRoad = isRoad;
        t.IsRiver = isRiver;
        return t;
    }

    private static (bool, string) Project_EmptySampleSet_ReturnsEmptyList()
    {
        var samples = SampleSet.Empty;
        var result = BattleProjection.Project(samples, battleHexRadius: 7);
        if (result.Count != 0) return (false, $"expected empty list, got {result.Count}");
        return (true, "");
    }

    private static (bool, string) Project_SingleTile_ReturnsOrigin()
    {
        var center = new Vector2I(5, 5);
        var samples = new SampleSet
        {
            Tiles = new[] { MakeTile(center, HexOverworldTile.TerrainType.Plains) },
            CenterAxial = center,
            Radius = 0,  // 单 tile sampleRadius=0
        };
        var result = BattleProjection.Project(samples, battleHexRadius: 7);
        if (result.Count != 1) return (false, $"expected 1 projection, got {result.Count}");
        if (result[0].BattleAxial != Vector2I.Zero) return (false, $"expected (0,0), got {result[0].BattleAxial}");
        return (true, "");
    }

    private static (bool, string) Project_LandSamples_ScaleProportionalToRadius()
    {
        // sampleRadius=2, battleHexRadius=10 → scale=5
        // tile 在 sample (1,0) 应投影到 battle (5,0)
        var center = new Vector2I(0, 0);
        var samples = new SampleSet
        {
            Tiles = new[]
            {
                MakeTile(center, HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(1, 0), HexOverworldTile.TerrainType.Forest),
                MakeTile(new Vector2I(2, 0), HexOverworldTile.TerrainType.Hills),
            },
            CenterAxial = center,
            Radius = 2,
        };
        var result = BattleProjection.Project(samples, battleHexRadius: 10);
        // 找到 (1,0) 的投影
        bool found = false;
        foreach (var p in result)
        {
            if (p.Tile.Coord == new Vector2I(1, 0))
            {
                if (p.BattleAxial != new Vector2I(5, 0))
                    return (false, $"expected (5,0) for (1,0)*5, got {p.BattleAxial}");
                found = true;
                break;
            }
        }
        if (!found) return (false, "no projection for (1,0)");
        return (true, "");
    }

    private static (bool, string) Project_WaterSample_UsesReducedScale()
    {
        // sampleRadius=2, battleHexRadius=10 → scale=5
        // water tile 在 sample (1,0) 应投影到 battle round(1*5*0.6)=(3,0),不是 (5,0)
        var center = new Vector2I(0, 0);
        var samples = new SampleSet
        {
            Tiles = new[]
            {
                MakeTile(center, HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(1, 0), HexOverworldTile.TerrainType.DeepWater),
            },
            CenterAxial = center,
            Radius = 2,
        };
        var result = BattleProjection.Project(samples, battleHexRadius: 10);
        foreach (var p in result)
        {
            if (p.Tile.Coord == new Vector2I(1, 0))
            {
                if (!p.IsWater) return (false, "DeepWater tile not marked as water");
                if (p.BattleAxial != new Vector2I(3, 0))
                    return (false, $"expected (3,0) for water 1*5*0.6, got {p.BattleAxial}");
                return (true, "");
            }
        }
        return (false, "water projection not found");
    }

    private static (bool, string) Project_SameInputBitIdentical()
    {
        var center = new Vector2I(2, 2);
        var samples = new SampleSet
        {
            Tiles = new[]
            {
                MakeTile(new Vector2I(2, 2), HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(3, 2), HexOverworldTile.TerrainType.Forest),
                MakeTile(new Vector2I(2, 3), HexOverworldTile.TerrainType.ShallowWater),
                MakeTile(new Vector2I(1, 2), HexOverworldTile.TerrainType.Hills),
            },
            CenterAxial = center,
            Radius = 1,
        };
        var first = BattleProjection.Project(samples, battleHexRadius: 7);
        for (int i = 0; i < 5; i++)
        {
            var again = BattleProjection.Project(samples, battleHexRadius: 7);
            if (again.Count != first.Count)
                return (false, $"iteration {i}: count mismatch {again.Count} vs {first.Count}");
            for (int j = 0; j < first.Count; j++)
            {
                if (again[j].BattleAxial != first[j].BattleAxial)
                    return (false, $"iteration {i} idx {j}: axial mismatch");
            }
        }
        return (true, "");
    }

    private static (bool, string) Project_SortedByAxialAsc()
    {
        var center = new Vector2I(0, 0);
        // 故意打乱 input 顺序
        var samples = new SampleSet
        {
            Tiles = new[]
            {
                MakeTile(new Vector2I(2, 1), HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(0, 0), HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(-1, 2), HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(1, -1), HexOverworldTile.TerrainType.Plains),
            },
            CenterAxial = center,
            Radius = 2,
        };
        var result = BattleProjection.Project(samples, battleHexRadius: 7);
        // 验证 (X ASC, Y ASC) 字典序
        for (int i = 1; i < result.Count; i++)
        {
            var prev = result[i - 1].BattleAxial;
            var cur = result[i].BattleAxial;
            bool ordered = prev.X < cur.X || (prev.X == cur.X && prev.Y <= cur.Y);
            if (!ordered) return (false, $"unsorted at idx {i}: prev={prev}, cur={cur}");
        }
        return (true, "");
    }

    private static (bool, string) Project_BridgeSampleMarkedAsBridge()
    {
        // IsBridge = IsRoad ∧ Terrain ∈ 水
        var center = new Vector2I(0, 0);
        var samples = new SampleSet
        {
            Tiles = new[]
            {
                MakeTile(new Vector2I(0, 0), HexOverworldTile.TerrainType.Plains),
                MakeTile(new Vector2I(1, 0), HexOverworldTile.TerrainType.River, isRoad: true),
            },
            CenterAxial = center,
            Radius = 1,
        };
        var result = BattleProjection.Project(samples, battleHexRadius: 7);
        foreach (var p in result)
        {
            if (p.Tile.Coord == new Vector2I(1, 0))
            {
                if (!p.IsBridge) return (false, "IsRoad+River tile should be IsBridge=true");
                return (true, "");
            }
        }
        return (false, "bridge projection not found");
    }
}
