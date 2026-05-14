// HexOverworldAStar.cs
// 六边形大地图A*寻路系统 — 用于实体移动寻路和道路/河流自动生成
// 优化: PriorityQueue 替代线性扫描, 道路偏好启发式, 早期终止优化
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 六边形大地图 A* 寻路 — 支持地形消耗权重和可配置的通行性穿越
/// 性能优化版: 使用 .NET PriorityQueue (O(log n) 出队) 替代 List 线性扫描 (O(n))
/// </summary>
[GlobalClass]
public partial class HexOverworldAStar : RefCounted
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>引用的网格数据 (不拥有)</summary>
    public HexOverworldGrid? Grid { get; set; }

    /// <summary>是否允许穿越不可通行地形 (生成阶段使用)</summary>
    public bool IgnorePassability { get; set; } = false;

    /// <summary>不可通行地形的惩罚权重</summary>
    public float ImpassablePenalty { get; set; } = 20.0f;

    /// <summary>道路偏好因子 (0=无偏好, 越大越偏好道路)</summary>
    public float RoadPreference { get; set; } = 0.3f;

    // ========================================
    // 构造
    // ========================================

    public HexOverworldAStar() { }

    public HexOverworldAStar(HexOverworldGrid? grid)
    {
        Grid = grid;
    }

    // ========================================
    // A* 寻路 — 主接口 (PriorityQueue 优化)
    // ========================================

    /// <summary>
    /// 在六边形网格上执行A*寻路 — 使用 PriorityQueue 实现 O(log n) 出队
    /// 返回轴向坐标路径数组 (含起点和终点), 不可达返回空
    /// </summary>
    public Vector2I[] FindPath(Vector2I start, Vector2I target)
    {
        if (Grid == null) return [];
        if (!Grid.HasTile(start.X, start.Y) || !Grid.HasTile(target.X, target.Y))
            return [];

        if (start == target) return [start];

        var openQueue = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0.0f };
        var closedSet = new HashSet<Vector2I>();

        openQueue.Enqueue(start, Heuristic(start, target));

        int maxIterations = Grid.TileCount() + 1;
        int iteration = 0;

        while (openQueue.Count > 0 && iteration < maxIterations)
        {
            iteration++;

            var current = openQueue.Dequeue();

            // 跳过已处理节点
            if (closedSet.Contains(current)) continue;
            closedSet.Add(current);

            if (current == target)
                return ReconstructPath(cameFrom, current);

            float currentG = gScore.GetValueOrDefault(current, 999999.0f);

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);

                if (closedSet.Contains(neighbor)) continue;
                if (!Grid.HasTile(neighbor.X, neighbor.Y)) continue;

                var nTile = Grid.GetTile(neighbor.X, neighbor.Y);
                if (nTile == null) continue;

                // 陆地寻路时浅水视为不可通行（防止路径穿越海域）
                if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater) continue;

                float cost = GetMoveCost(nTile);
                if (cost < 0.0f) continue;

                float tentativeG = currentG + cost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, 999999.0f))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    float h = Heuristic(neighbor, target);
                    // 道路偏好: 在道路上时轻微降低启发式
                    if (nTile.IsRoad && RoadPreference > 0f)
                        h *= (1.0f - RoadPreference * 0.5f);

                    openQueue.Enqueue(neighbor, tentativeG + h);
                }
            }
        }

        return [];
    }

    /// <summary>
    /// 寻路到像素坐标 (返回像素坐标路径)
    /// 修复: 路径起点使用实际像素位置而非六边形中心，避免回弹。
    /// 修复: 起点不可通行时（玩家被放置在不可通行 tile），先脱困到最近可通行点。
    /// </summary>
    public Vector2[] FindPathPixels(Vector2 startPx, Vector2 targetPx)
    {
        if (Grid == null) return [];

        var startTile = Grid.GetTileAtPixel(startPx.X, startPx.Y);
        var targetTile = Grid.GetTileAtPixel(targetPx.X, targetPx.Y);
        if (startTile == null || targetTile == null) return [];

        // 起点不可通行时，找最近可通行点作为实际起点
        var actualStart = startTile;
        if (!actualStart.IsPassable && !IgnorePassability)
        {
            actualStart = FindNearestPassable(startTile);
            if (actualStart == null) return [];
        }

        var actualTarget = targetTile;
        if (!actualTarget.IsPassable && !IgnorePassability)
        {
            actualTarget = FindNearestPassable(targetTile);
            if (actualTarget == null) return [];
        }

        var hexPath = FindPath(actualStart.Coord, actualTarget.Coord);
        if (hexPath.Length == 0) return [];

        var pxPath = new Vector2[hexPath.Length];
        for (int i = 0; i < hexPath.Length; i++)
        {
            var tile = Grid.GetTile(hexPath[i].X, hexPath[i].Y);
            pxPath[i] = tile?.PixelPos ?? Vector2.Zero;
        }

        // 修复回弹: 用实际起始像素位置替换第一个路径点（六边形中心）
        if (pxPath.Length > 1)
            pxPath[0] = startPx;

        return pxPath;
    }

    /// <summary>寻路并返回方向信息 (用于生成器)</summary>
    public Godot.Collections.Array FindPathWithDirections(Vector2I start, Vector2I target)
    {
        var path = FindPath(start, target);
        var result = new Godot.Collections.Array();
        if (path.Length == 0) return result;

        for (int i = 0; i < path.Length; i++)
        {
            var entry = new Godot.Collections.Dictionary
            {
                ["coord"] = path[i],
                ["direction_to_next"] = i < path.Length - 1 ? GetDirection(path[i], path[i + 1]) : -1,
            };
            result.Add(entry);
        }
        return result;
    }

    // ========================================
    // 最低成本路径 (河流生成) — PriorityQueue 优化
    // ========================================

    /// <summary>找到从 start 到 target 的最低高程路径 (河流用)</summary>
    public Vector2I[] FindLowestElevationPath(Vector2I start, Vector2I target)
    {
        if (Grid == null) return [];
        if (!Grid.HasTile(start.X, start.Y) || !Grid.HasTile(target.X, target.Y))
            return [];

        var openQueue = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0.0f };
        var closedSet = new HashSet<Vector2I>();

        openQueue.Enqueue(start, Heuristic(start, target));

        int maxIterations = Grid.TileCount() + 1;
        int iteration = 0;

        while (openQueue.Count > 0 && iteration < maxIterations)
        {
            iteration++;

            var current = openQueue.Dequeue();

            if (closedSet.Contains(current)) continue;
            closedSet.Add(current);

            if (current == target)
                return ReconstructPath(cameFrom, current);

            float currentG = gScore.GetValueOrDefault(current, 999999.0f);

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);

                if (closedSet.Contains(neighbor)) continue;
                if (!Grid.HasTile(neighbor.X, neighbor.Y)) continue;

                var nTile = Grid.GetTile(neighbor.X, neighbor.Y);
                if (nTile == null) continue;

                float elevCost = nTile.Elevation * 10.0f;
                if (nTile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                    nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                    elevCost *= 0.3f;

                float distCost = 1.0f;
                if (nTile.Terrain == HexOverworldTile.TerrainType.Mountain)
                    elevCost += 30.0f;

                float tentativeG = currentG + elevCost + distCost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, 999999.0f))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    openQueue.Enqueue(neighbor, tentativeG + Heuristic(neighbor, target));
                }
            }
        }

        return [];
    }

    // ========================================
    // Dijkstra (道路生成) — PriorityQueue 优化
    // ========================================

    /// <summary>从一个出发点找到多个目标点的最短路径树 — 使用 PriorityQueue</summary>
    public Godot.Collections.Dictionary FindPathsToMultiple(Vector2I start, Vector2I[] targets)
    {
        var result = new Godot.Collections.Dictionary();
        if (Grid == null) return result;

        var distances = new Dictionary<Vector2I, float> { [start] = 0.0f };
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var visited = new HashSet<Vector2I>();
        var remaining = new HashSet<Vector2I>(targets);

        var openQueue = new PriorityQueue<Vector2I, float>();
        openQueue.Enqueue(start, 0.0f);

        int maxIterations = Grid.TileCount() + 1;
        int iteration = 0;

        while (openQueue.Count > 0 && remaining.Count > 0 && iteration < maxIterations)
        {
            iteration++;

            var current = openQueue.Dequeue();

            if (visited.Contains(current)) continue;
            visited.Add(current);

            remaining.Remove(current);

            float currentDist = distances.GetValueOrDefault(current, 999999.0f);

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (!Grid.HasTile(neighbor.X, neighbor.Y) || visited.Contains(neighbor))
                    continue;

                var nTile = Grid.GetTile(neighbor.X, neighbor.Y);
                if (nTile == null) continue;
                // 陆地寻路时浅水视为不可通行
                if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater) continue;

                float cost = GetMoveCost(nTile);
                if (cost < 0.0f) continue;

                float newDist = currentDist + cost;
                if (newDist < distances.GetValueOrDefault(neighbor, 999999.0f))
                {
                    distances[neighbor] = newDist;
                    cameFrom[neighbor] = current;
                    openQueue.Enqueue(neighbor, newDist);
                }
            }
        }

        foreach (var t in targets)
        {
            if (cameFrom.ContainsKey(t))
            {
                var path = ReconstructPath(cameFrom, t);
                var pathArray = new Godot.Collections.Array();
                foreach (var coord in path) pathArray.Add(coord);
                result[t] = pathArray;
            }
        }

        return result;
    }

    // ========================================
    // 内部方法
    // ========================================

    private float Heuristic(Vector2I a, Vector2I b)
    {
        return HexOverworldTile.CubeDistance(
            HexOverworldTile.AxialToCube(a.X, a.Y),
            HexOverworldTile.AxialToCube(b.X, b.Y)
        );
    }

    private float GetMoveCost(HexOverworldTile tile)
    {
        if (IgnorePassability)
            return tile.IsPassable ? tile.MoveCost : ImpassablePenalty;
        if (!tile.IsPassable) return -1.0f;
        return tile.MoveCost;
    }

    private Vector2I[] ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
    {
        var path = new List<Vector2I> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path.ToArray();
    }

    private int GetDirection(Vector2I a, Vector2I b)
    {
        var diffCube = HexOverworldTile.AxialToCube(b.X, b.Y) - HexOverworldTile.AxialToCube(a.X, a.Y);
        for (int i = 0; i < 6; i++)
            if (diffCube == HexOverworldTile.CubeDirections[i])
                return i;
        return -1;
    }

    private HexOverworldTile? FindNearestPassable(HexOverworldTile tile)
    {
        var visited = new HashSet<Vector2I> { tile.Coord };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(tile.Coord);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                var nCoord = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (visited.Contains(nCoord) || !Grid!.HasTile(nCoord.X, nCoord.Y))
                    continue;
                visited.Add(nCoord);
                var nTile = Grid.GetTile(nCoord.X, nCoord.Y);
                if (nTile != null && nTile.IsPassable)
                    return nTile;
                queue.Enqueue(nCoord);
            }
        }

        return null;
    }
}
