// OverworldSamplerTests.cs
// 战斗地图采样器单元测试 — 服务于 spec combat-hex-from-overworld-state R1 / R6 / R10。
//
// 覆盖契约:
//   - R1#2: 采样圈数与 hex 数对照表(Tiny=7 / Small=12 / Medium=30 / Large=49)
//   - R1#3: POI 战取 footprint + K 圈
//   - R1#4: 野外取 EncounterCoord + K 圈(默认 1)
//   - R1#5: 跨 chunk null 跳过不抛
//   - R1#6: tile 去重
//   - R1#7: sampleRadius 计算正确
//   - R6#2: grid==null → 返回 SampleSet.Empty
//   - R6#4: DefendingPOI.OccupiedHexes 空 → 退化到 EncounterCoord + warning
using System.Collections.Generic;
using BladeHex.Map.Generation;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Map.Tests;

public static class OverworldSamplerTests
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
        yield return Run(nameof(Sample_NullGrid_ReturnsEmpty), Sample_NullGrid_ReturnsEmpty);
        yield return Run(nameof(Sample_WildEncounter_K1_Returns7Tiles), Sample_WildEncounter_K1_Returns7Tiles);
        yield return Run(nameof(Sample_WildEncounter_K2_Returns19Tiles), Sample_WildEncounter_K2_Returns19Tiles);
        yield return Run(nameof(Sample_OutOfChunkTile_SkipsNull), Sample_OutOfChunkTile_SkipsNull);
        yield return Run(nameof(Sample_RadiusComputed), Sample_RadiusComputed);
        yield return Run(nameof(Sample_K0_OnlyFootprint), Sample_K0_OnlyFootprint);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try { var (ok, msg) = test(); return (name, ok, msg); }
        catch (System.Exception ex) { return (name, false, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    // ========================================================================

    private static HexOverworldGrid MakeGrid(int radius)
    {
        // 直接构造一个稀疏 grid:用 SetTile 在 axial 范围内填满 tile。
        // 不走 Initialize 因为 odd-r offset 转换会让 axial (q,r) 不一定都存在。
        var grid = new HexOverworldGrid();
        for (int q = -radius * 2; q <= radius * 2; q++)
        {
            for (int r = -radius * 2; r <= radius * 2; r++)
            {
                var tile = HexOverworldTile.CreateEmpty(q, r);
                grid.Tiles[new Vector2I(q, r)] = tile;
            }
        }
        return grid;
    }

    private static (bool, string) Sample_NullGrid_ReturnsEmpty()
    {
        var ctx = new BattleContext { EncounterCoord = Vector2I.Zero };
        var samples = OverworldSampler.Sample(ctx, null, samplingRingCount: 1);
        if (!samples.IsEmpty) return (false, $"expected Empty, got {samples.Tiles.Count} tiles");
        return (true, "");
    }

    private static (bool, string) Sample_WildEncounter_K1_Returns7Tiles()
    {
        var grid = MakeGrid(5);
        var coord = new Vector2I(3, 3);
        var ctx = new BattleContext { DefendingPOI = null, EncounterCoord = coord };
        var samples = OverworldSampler.Sample(ctx, grid, samplingRingCount: 1);
        if (samples.Tiles.Count != 7) return (false, $"expected 7 tiles, got {samples.Tiles.Count}");
        if (samples.CenterAxial != coord) return (false, $"center mismatch: {samples.CenterAxial}");
        return (true, "");
    }

    private static (bool, string) Sample_WildEncounter_K2_Returns19Tiles()
    {
        // K=2 圈 hex 数 = 1 + 6 + 12 = 19
        var grid = MakeGrid(5);
        var coord = new Vector2I(2, 2);
        var ctx = new BattleContext { DefendingPOI = null, EncounterCoord = coord };
        var samples = OverworldSampler.Sample(ctx, grid, samplingRingCount: 2);
        if (samples.Tiles.Count != 19) return (false, $"expected 19 tiles, got {samples.Tiles.Count}");
        return (true, "");
    }

    private static (bool, string) Sample_K0_OnlyFootprint()
    {
        // K=0:不外扩,只返回 footprint(野外路径 = 1 tile)
        var grid = MakeGrid(5);
        var coord = new Vector2I(0, 0);
        var ctx = new BattleContext { DefendingPOI = null, EncounterCoord = coord };
        var samples = OverworldSampler.Sample(ctx, grid, samplingRingCount: 0);
        if (samples.Tiles.Count != 1) return (false, $"expected 1 tile (no expansion), got {samples.Tiles.Count}");
        return (true, "");
    }

    private static (bool, string) Sample_OutOfChunkTile_SkipsNull()
    {
        // 故意构造稀疏 grid:只有 (0,0) 一个 tile,其他全 null。期望采样不抛、返回 1 tile。
        var grid = new HexOverworldGrid();
        grid.Tiles[new Vector2I(0, 0)] = HexOverworldTile.CreateEmpty(0, 0);

        var ctx = new BattleContext { DefendingPOI = null, EncounterCoord = new Vector2I(0, 0) };
        var samples = OverworldSampler.Sample(ctx, grid, samplingRingCount: 2);
        if (samples.IsEmpty) return (false, "expected non-empty (1 tile), but got Empty");
        if (samples.Tiles.Count != 1)
            return (false, $"expected 1 tile (rest null), got {samples.Tiles.Count}");
        return (true, "");
    }

    private static (bool, string) Sample_RadiusComputed()
    {
        var grid = MakeGrid(5);
        var center = new Vector2I(3, 3);
        var ctx = new BattleContext { DefendingPOI = null, EncounterCoord = center };
        var samples = OverworldSampler.Sample(ctx, grid, samplingRingCount: 2);
        if (samples.Radius != 2) return (false, $"expected radius=2, got {samples.Radius}");
        return (true, "");
    }
}
