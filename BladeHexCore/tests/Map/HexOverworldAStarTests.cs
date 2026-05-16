// HexOverworldAStarTests.cs
// 大地图 A* 寻路单元测试 — 服务于架构优化 spec R7。
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树
//   - 用 HexOverworldGrid.Initialize 构造小地图，按 axial 设置地形
//   - 每个 Test_xxx 方法返回 (bool ok, string description)
//
// 覆盖关键路径：
//   - 直线可达：起点终点同地形，路径长度 == 距离 + 1
//   - 阻挡绕路：山脉成墙，路径仍可达且更长
//   - 完全阻断：山脉围困目标，FindPath 返回空
//   - 起点终点相同：返回 [start]
//   - 起点/终点越界：返回空
//   - 地形代价：道路偏好下选道路而非草原
//   - IgnorePassability：强制穿越不可通行地形（生成阶段）
using System.Collections.Generic;
using Godot;

namespace BladeHex.Map.Tests;

public static class HexOverworldAStarTests
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
        yield return Run(nameof(FindPath_Identity_ReturnsSingleStart), FindPath_Identity_ReturnsSingleStart);
        yield return Run(nameof(FindPath_StraightLine_OnPlains), FindPath_StraightLine_OnPlains);
        yield return Run(nameof(FindPath_OutOfBounds_ReturnsEmpty), FindPath_OutOfBounds_ReturnsEmpty);
        yield return Run(nameof(FindPath_BlockedByMountains_ReturnsEmpty), FindPath_BlockedByMountains_ReturnsEmpty);
        yield return Run(nameof(FindPath_DetourAroundObstacle), FindPath_DetourAroundObstacle);
        yield return Run(nameof(FindPath_PrefersRoadOverPlain), FindPath_PrefersRoadOverPlain);
        yield return Run(nameof(FindPath_IgnorePassability_CrossesMountain), FindPath_IgnorePassability_CrossesMountain);
        yield return Run(nameof(FindPath_NoGrid_ReturnsEmpty), FindPath_NoGrid_ReturnsEmpty);
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

    private static (bool, string) FindPath_Identity_ReturnsSingleStart()
    {
        var (grid, _) = MakePlainsGrid(5, 5);
        var aStar = new HexOverworldAStar(grid);
        var start = new Vector2I(0, 0);
        var path = aStar.FindPath(start, start);
        if (path.Length != 1) return (false, $"expected length 1, got {path.Length}");
        if (path[0] != start) return (false, $"expected {start}, got {path[0]}");
        return (true, "");
    }

    private static (bool, string) FindPath_StraightLine_OnPlains()
    {
        var (grid, _) = MakePlainsGrid(8, 8);
        var aStar = new HexOverworldAStar(grid);
        var start = new Vector2I(0, 0);
        var target = new Vector2I(3, 0);
        var path = aStar.FindPath(start, target);

        if (path.Length == 0) return (false, "path is empty");
        if (path[0] != start) return (false, $"path[0] should be start, got {path[0]}");
        if (path[^1] != target) return (false, $"path[^1] should be target, got {path[^1]}");

        // 距离 q=0 → q=3，应该是 4 个格子（3 步）
        int distance = HexOverworldTile.HexDistance(start.X, start.Y, target.X, target.Y);
        if (path.Length != distance + 1)
            return (false, $"expected path length {distance + 1}, got {path.Length}");
        return (true, "");
    }

    private static (bool, string) FindPath_OutOfBounds_ReturnsEmpty()
    {
        var (grid, _) = MakePlainsGrid(5, 5);
        var aStar = new HexOverworldAStar(grid);
        var path = aStar.FindPath(new Vector2I(0, 0), new Vector2I(99, 99));
        if (path.Length != 0) return (false, $"expected empty, got length {path.Length}");
        return (true, "");
    }

    private static (bool, string) FindPath_BlockedByMountains_ReturnsEmpty()
    {
        // 用 1×1 网格周围全部山脉围困，无法到达
        var (grid, _) = MakePlainsGrid(5, 5);
        // 将目标 (3, 0) 周围 6 邻居全部设为山脉（不可通行）
        var target = new Vector2I(3, 0);
        for (int dir = 0; dir < 6; dir++)
        {
            var n = HexOverworldTile.GetNeighbor(target.X, target.Y, dir);
            var tile = grid.GetTile(n.X, n.Y);
            if (tile != null) tile.SetTerrain(HexOverworldTile.TerrainType.Mountain);
        }

        var aStar = new HexOverworldAStar(grid);
        var path = aStar.FindPath(new Vector2I(0, 0), target);
        if (path.Length != 0) return (false, $"expected empty (target surrounded), got length {path.Length}");
        return (true, "");
    }

    private static (bool, string) FindPath_DetourAroundObstacle()
    {
        // 在直线路径上放一道山墙，路径应绕过去
        var (grid, _) = MakePlainsGrid(8, 8);
        // 把 q=2 列设为山脉（部分），强制绕路
        var blockA = grid.GetTile(2, 0);
        if (blockA != null) blockA.SetTerrain(HexOverworldTile.TerrainType.Mountain);

        var aStar = new HexOverworldAStar(grid);
        var start = new Vector2I(0, 0);
        var target = new Vector2I(4, 0);
        var path = aStar.FindPath(start, target);

        if (path.Length == 0) return (false, "path should not be empty after small detour");
        if (path[^1] != target) return (false, $"path should reach target, got {path[^1]}");

        // 路径不应该穿越被设为山脉的格子
        foreach (var node in path)
        {
            var t = grid.GetTile(node.X, node.Y);
            if (t != null && !t.IsPassable)
                return (false, $"path crosses impassable tile {node}");
        }
        return (true, "");
    }

    private static (bool, string) FindPath_PrefersRoadOverPlain()
    {
        // 在两条等距路径中，一条是道路，一条是平原。期望选择道路（移动代价 0.2 vs 1.0）
        var (grid, _) = MakePlainsGrid(10, 10);

        // 将一行格子全部标记为道路（IsRoad 是叠加层 flag，与 Terrain enum 分离）
        for (int q = 0; q <= 5; q++)
        {
            var tile = grid.GetTile(q, 1);
            if (tile != null)
            {
                tile.SetTerrain(HexOverworldTile.TerrainType.Road);
                tile.IsRoad = true;
            }
        }

        var aStar = new HexOverworldAStar(grid);
        var start = new Vector2I(0, 1);
        var target = new Vector2I(5, 1);
        var path = aStar.FindPath(start, target);

        if (path.Length == 0) return (false, "path is empty");

        // 路径上的中间格子应大多在 r=1（道路）
        int roadCount = 0;
        foreach (var node in path)
        {
            var t = grid.GetTile(node.X, node.Y);
            if (t != null && t.IsRoad) roadCount++;
        }
        if (roadCount < path.Length / 2)
            return (false, $"expected path to mostly use road, only {roadCount}/{path.Length} road tiles");
        return (true, "");
    }

    private static (bool, string) FindPath_IgnorePassability_CrossesMountain()
    {
        // 整条直线全是山脉。普通寻路返回空；IgnorePassability=true 时仍可达
        var (grid, _) = MakePlainsGrid(5, 5);
        for (int q = 0; q <= 4; q++)
        {
            var tile = grid.GetTile(q, 0);
            if (tile != null) tile.SetTerrain(HexOverworldTile.TerrainType.Mountain);
        }

        var aStarStrict = new HexOverworldAStar(grid);
        var pathStrict = aStarStrict.FindPath(new Vector2I(0, 0), new Vector2I(4, 0));
        if (pathStrict.Length != 0)
            return (false, $"strict mode should fail, got path length {pathStrict.Length}");

        var aStarLoose = new HexOverworldAStar(grid) { IgnorePassability = true };
        var pathLoose = aStarLoose.FindPath(new Vector2I(0, 0), new Vector2I(4, 0));
        if (pathLoose.Length == 0)
            return (false, "IgnorePassability mode should find a path");
        return (true, "");
    }

    private static (bool, string) FindPath_NoGrid_ReturnsEmpty()
    {
        var aStar = new HexOverworldAStar();
        var path = aStar.FindPath(new Vector2I(0, 0), new Vector2I(1, 0));
        if (path.Length != 0) return (false, $"expected empty (no grid), got {path.Length}");
        return (true, "");
    }

    // ============================================================================
    // 工具方法
    // ============================================================================

    /// <summary>构造一个 width × height 的全平原网格</summary>
    private static (HexOverworldGrid grid, int tileCount) MakePlainsGrid(int width, int height)
    {
        var grid = new HexOverworldGrid();
        grid.Initialize(width, height);
        // 把所有 tile 显式设为 Plains 以保证 IsPassable / MoveCost 一致
        foreach (var t in grid.Tiles.Values)
            t.SetTerrain(HexOverworldTile.TerrainType.Plains);
        return (grid, grid.TileCount());
    }
}
