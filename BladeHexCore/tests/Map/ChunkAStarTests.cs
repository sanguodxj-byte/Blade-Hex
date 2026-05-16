// ChunkAStarTests.cs
// 跨 chunk 寻路单元测试 — 服务于架构优化 spec R7。
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树
//   - 手工构造 ChunkManager + ActiveChunks，避免触发 ChunkGenerator
//   - 每个 Test_xxx 方法返回 (bool ok, string description)
//
// 覆盖关键路径：
//   - 同一 chunk 内寻路（路径长度 == hex 距离 + 1）
//   - 跨相邻 chunk 寻路（终点在已加载 chunk 内）
//   - 终点未加载 chunk → 返回到边界的部分路径
//   - 起点未加载 → 空路径
//   - 起点终点相同 → 返回 [start]
//   - 海上模式：陆地不可通行
//   - 缓存命中（同一查询第二次结果一致）
using System.Collections.Generic;
using Godot;

namespace BladeHex.Map.Tests;

public static class ChunkAStarTests
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
        yield return Run(nameof(FindPath_SameTile_ReturnsSingle), FindPath_SameTile_ReturnsSingle);
        yield return Run(nameof(FindPath_WithinSingleChunk), FindPath_WithinSingleChunk);
        yield return Run(nameof(FindPath_AcrossTwoChunks), FindPath_AcrossTwoChunks);
        yield return Run(nameof(FindPath_StartNotLoaded_ReturnsEmpty), FindPath_StartNotLoaded_ReturnsEmpty);
        yield return Run(nameof(FindPath_TargetUnloaded_ReturnsBoundaryPath), FindPath_TargetUnloaded_ReturnsBoundaryPath);
        yield return Run(nameof(FindPath_SeaMode_LandImpassable), FindPath_SeaMode_LandImpassable);
        yield return Run(nameof(FindPath_CacheConsistency), FindPath_CacheConsistency);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (System.Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    // ============================================================================
    // 测试用例
    // ============================================================================

    private static (bool, string) FindPath_SameTile_ReturnsSingle()
    {
        var mgr = MakeManagerWithChunk(0, 0);
        var aStar = new ChunkAStar();
        var coord = new Vector2I(5, 5);
        var path = aStar.FindPathAxial(coord, coord, mgr);
        if (path.Length != 1) return (false, $"expected 1, got {path.Length}");
        if (path[0] != coord) return (false, $"expected {coord}, got {path[0]}");
        return (true, "");
    }

    private static (bool, string) FindPath_WithinSingleChunk()
    {
        var mgr = MakeManagerWithChunk(0, 0);
        var aStar = new ChunkAStar();
        var start = new Vector2I(2, 2);
        var target = new Vector2I(8, 2);
        var path = aStar.FindPathAxial(start, target, mgr);

        if (path.Length == 0) return (false, "path is empty");
        if (path[0] != start) return (false, $"path[0]={path[0]}, want {start}");
        if (path[^1] != target) return (false, $"path[^1]={path[^1]}, want {target}");

        int distance = HexOverworldTile.HexDistance(start.X, start.Y, target.X, target.Y);
        if (path.Length != distance + 1)
            return (false, $"length mismatch: got {path.Length}, expected {distance + 1}");
        return (true, "");
    }

    private static (bool, string) FindPath_AcrossTwoChunks()
    {
        // Chunk(0,0): tiles q∈[0,15], r∈[0,15]
        // Chunk(1,0): tiles q∈[16,31], r∈[0,15]
        // 两个相邻 chunk 都加载，跨 chunk 寻路应能直达
        var mgr = MakeManagerWithChunks(new[] { new Vector2I(0, 0), new Vector2I(1, 0) });
        var aStar = new ChunkAStar();

        var start = new Vector2I(8, 5);   // 在 chunk(0,0) 中
        var target = new Vector2I(20, 5); // 在 chunk(1,0) 中
        var path = aStar.FindPathAxial(start, target, mgr);

        if (path.Length == 0) return (false, "path is empty");
        if (path[^1] != target) return (false, $"path should reach target {target}, got {path[^1]}");
        return (true, "");
    }

    private static (bool, string) FindPath_StartNotLoaded_ReturnsEmpty()
    {
        var mgr = MakeManagerWithChunk(0, 0);
        var aStar = new ChunkAStar();
        // 起点 (100, 100) 在未加载的 chunk
        var path = aStar.FindPathAxial(new Vector2I(100, 100), new Vector2I(5, 5), mgr);
        if (path.Length != 0) return (false, $"expected empty, got length {path.Length}");
        return (true, "");
    }

    private static (bool, string) FindPath_TargetUnloaded_ReturnsBoundaryPath()
    {
        // 仅加载 chunk(0,0)，目标在远处未加载 chunk
        var mgr = MakeManagerWithChunk(0, 0);
        var aStar = new ChunkAStar();
        var start = new Vector2I(8, 8);
        var target = new Vector2I(100, 100);
        var path = aStar.FindPathAxial(start, target, mgr);

        // 目标未加载时应该返回到边界的部分路径，或者至少 [start]
        // 不应该是空数组（fallback 行为）
        if (path.Length == 0)
            return (false, "expected boundary path or fallback, got empty");
        if (path[0] != start)
            return (false, $"path should start at {start}, got {path[0]}");
        return (true, "");
    }

    private static (bool, string) FindPath_SeaMode_LandImpassable()
    {
        // 默认 chunk 全是 Plains（陆地），海上模式下不可通行
        var mgr = MakeManagerWithChunk(0, 0);
        var aStar = new ChunkAStar { Mode = ChunkAStar.NavigationMode.Sea };

        var start = new Vector2I(2, 2);
        var target = new Vector2I(8, 2);
        var path = aStar.FindPathAxial(start, target, mgr);

        // 全是陆地，海上模式下应该寻路失败
        // 不强求空数组（实现可能返回 [start]），但路径不应该到达 target
        if (path.Length > 0 && path[^1] == target)
            return (false, $"sea mode should not traverse plains, but got full path of length {path.Length}");
        return (true, "");
    }

    private static (bool, string) FindPath_CacheConsistency()
    {
        // 同一查询多次调用，结果应一致
        var mgr = MakeManagerWithChunks(new[] { new Vector2I(0, 0), new Vector2I(1, 0) });
        var aStar = new ChunkAStar();

        var start = new Vector2I(2, 5);
        var target = new Vector2I(28, 5); // 路径较长，触发缓存（>10）

        var path1 = aStar.FindPathAxial(start, target, mgr);
        var path2 = aStar.FindPathAxial(start, target, mgr);

        if (path1.Length == 0 || path2.Length == 0)
            return (false, "path is empty");
        if (path1.Length != path2.Length)
            return (false, $"cached path length differs: {path1.Length} vs {path2.Length}");
        for (int i = 0; i < path1.Length; i++)
        {
            if (path1[i] != path2[i])
                return (false, $"path differs at index {i}: {path1[i]} vs {path2[i]}");
        }
        return (true, "");
    }

    // ============================================================================
    // 工具方法
    // ============================================================================

    /// <summary>构造一个 ChunkManager，仅包含一个全平原 chunk。</summary>
    private static ChunkManager MakeManagerWithChunk(int chunkQ, int chunkR)
    {
        return MakeManagerWithChunks(new[] { new Vector2I(chunkQ, chunkR) });
    }

    /// <summary>构造一个 ChunkManager，包含指定的全平原 chunks。</summary>
    private static ChunkManager MakeManagerWithChunks(Vector2I[] chunkCoords)
    {
        var mgr = new ChunkManager();
        foreach (var coord in chunkCoords)
        {
            var chunk = MakePlainsChunk(coord.X, coord.Y);
            mgr.ActiveChunks[coord] = chunk;
            mgr.GeneratedChunkCoords.Add(coord);
        }
        return mgr;
    }

    /// <summary>构造一个全平原 ChunkData。</summary>
    private static ChunkData MakePlainsChunk(int chunkQ, int chunkR)
    {
        var chunk = new ChunkData
        {
            ChunkCoord = new Vector2I(chunkQ, chunkR),
            IsGenerated = true,
            IsActive = true,
        };
        var origin = ChunkData.ChunkToWorld(chunkQ, chunkR);
        for (int dq = 0; dq < ChunkData.ChunkSize; dq++)
        {
            for (int dr = 0; dr < ChunkData.ChunkSize; dr++)
            {
                int worldQ = origin.X + dq;
                int worldR = origin.Y + dr;
                var tile = HexOverworldTile.Create(
                    worldQ, worldR,
                    HexOverworldTile.TerrainType.Plains,
                    elev: 0.5f, moist: 0.5f, temp: 0.5f);
                chunk.Tiles[new Vector2I(worldQ, worldR)] = tile;
            }
        }
        return chunk;
    }
}
