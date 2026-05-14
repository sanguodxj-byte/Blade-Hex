// ChunkAStar.cs — Chunk 感知 A* 寻路（两层策略）
// Layer 1: Chunk 级粗粒度寻路  Layer 2: Tile 级细粒度寻路
// 优化: PriorityQueue 替代线性扫描, 路径缓存, 道路偏好启发式
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// Chunk 感知 A* — 活跃 chunk 内精确寻路，超出范围时 chunk 级引导到边界
/// 性能优化版: 使用 .NET PriorityQueue (O(log n) 出队) 替代 List 线性扫描 (O(n))
/// </summary>
[GlobalClass]
public partial class ChunkAStar : RefCounted
{
    /// <summary>导航模式</summary>
    public enum NavigationMode
    {
        Land,       // 陆地模式（默认）— 水域不可通行
        Sea,        // 海上模式（有船）— 水域可通行，陆地不可通行（除海岸）
    }

    /// <summary>当前导航模式</summary>
    public NavigationMode Mode { get; set; } = NavigationMode.Land;
    /// <summary>Tile 级别寻路最大迭代次数</summary>
    public int MaxTileIterations { get; set; } = 8192;

    /// <summary>Chunk 级别寻路最大迭代次数</summary>
    public int MaxChunkIterations { get; set; } = 256;

    /// <summary>道路偏好权重 — 启发式中对道路方向的额外吸引力 (0=无偏好, 1=强偏好)</summary>
    public float RoadPreferenceFactor { get; set; } = 0.3f;

    /// <summary>预计算代价网格引用（由外部注入）</summary>
    public PathfindingCostGrid? CostGrid { get; set; }

    // ========== 路径缓存 ==========

    /// <summary>路径缓存: (起点chunk, 终点chunk) → 路径</summary>
    private readonly Dictionary<(Vector2I, Vector2I), CachedPath> _pathCache = new();

    /// <summary>缓存最大条目数</summary>
    private const int MaxCacheEntries = 64;

    /// <summary>缓存有效帧数 (超过后失效)</summary>
    private const int CacheValidFrames = 300; // ~5 秒 @ 60fps

    private int _frameCounter = 0;

    private struct CachedPath
    {
        public Vector2I[] Path;
        public int CachedAtFrame;
    }

    /// <summary>每帧调用一次，推进缓存时钟</summary>
    public void Tick() => _frameCounter++;

    /// <summary>清除路径缓存 (chunk 加载/卸载时调用)</summary>
    public void InvalidateCache() => _pathCache.Clear();

    // ========== 主接口 — 像素坐标 ==========

    /// <summary>
    /// 像素坐标寻路，返回像素路径点数组。
    /// 修复: 路径起点使用实际像素位置而非六边形中心，避免回弹。
    /// 修复: 远距离目标不在活跃 chunk 时，自动寻路到已加载区域边界。
    /// </summary>
    public Vector2[] FindPathPixels(Vector2 fromPixel, Vector2 toPixel, ChunkManager mgr)
    {
        var fromAxial = HexOverworldTile.PixelToAxial(fromPixel.X, fromPixel.Y);
        var toAxial = HexOverworldTile.PixelToAxial(toPixel.X, toPixel.Y);

        var axialPath = FindPathAxial(fromAxial, toAxial, mgr);
        if (axialPath.Length == 0) return [];

        // 转换为像素路径
        var pixelPath = new Vector2[axialPath.Length];
        for (int i = 0; i < axialPath.Length; i++)
            pixelPath[i] = HexOverworldTile.AxialToPixel(axialPath[i].X, axialPath[i].Y);

        // 修复回弹: 用实际起始像素位置替换第一个路径点（六边形中心）
        // 这样玩家不会先回到当前格中心再出发
        if (pixelPath.Length > 1)
            pixelPath[0] = fromPixel;

        return pixelPath;
    }

    // ========== 主接口 — 轴向坐标 ==========

    /// <summary>
    /// 轴向坐标寻路，返回轴向坐标路径。
    /// 修复: 远距离目标不在活跃 chunk 时，保证返回到边界的有效路径（而非空数组）。
    /// 玩家到达边界后，新 chunk 加载，下次 MoveTo 会继续寻路。
    /// </summary>
    public Vector2I[] FindPathAxial(Vector2I fromAxial, Vector2I toAxial, ChunkManager mgr)
    {
        if (fromAxial == toAxial) return [fromAxial];

        bool startLoaded = mgr.IsLoaded(fromAxial.X, fromAxial.Y);
        bool endLoaded = mgr.IsLoaded(toAxial.X, toAxial.Y);

        if (!startLoaded) return [];

        // 两端都在活跃 chunk 内 → 直接 tile 级 A*
        if (endLoaded)
        {
            // 尝试缓存命中
            var cacheKey = (fromAxial, toAxial);
            if (_pathCache.TryGetValue(cacheKey, out var cached) &&
                (_frameCounter - cached.CachedAtFrame) < CacheValidFrames)
            {
                return cached.Path;
            }

            var path = TileLevelAStar(fromAxial, toAxial, mgr);

            // 缓存结果 (仅缓存较长路径，短路径计算成本低)
            if (path.Length > 10)
            {
                if (_pathCache.Count >= MaxCacheEntries)
                    EvictOldestCache();
                _pathCache[cacheKey] = new CachedPath { Path = path, CachedAtFrame = _frameCounter };
            }

            return path;
        }

        // 终点不在活跃 chunk → chunk 级寻路找方向，tile 级寻路到边界
        var boundaryTarget = FindBoundaryTarget(fromAxial, toAxial, mgr);
        if (boundaryTarget == fromAxial)
        {
            // FindBoundaryTarget 失败时，尝试直接朝目标方向走到活跃区域边缘
            boundaryTarget = FindDirectionalBoundary(fromAxial, toAxial, mgr);
            if (boundaryTarget == fromAxial) return [fromAxial];
        }

        var boundaryPath = TileLevelAStar(fromAxial, boundaryTarget, mgr);

        // 如果 tile 级 A* 也失败（被地形阻挡），尝试找附近的可达边界点
        if (boundaryPath.Length == 0)
        {
            var altBoundary = FindAlternativeBoundary(fromAxial, toAxial, mgr);
            if (altBoundary != fromAxial)
                boundaryPath = TileLevelAStar(fromAxial, altBoundary, mgr);
        }

        return boundaryPath;
    }

    // ========== Layer 2: Tile 级 A* (PriorityQueue 优化) ==========

    /// <summary>活跃 chunk 内精确 tile 级 A* — 使用 PriorityQueue 实现 O(log n) 出队</summary>
    private Vector2I[] TileLevelAStar(Vector2I start, Vector2I target, ChunkManager mgr)
    {
        var startTile = mgr.GetTile(start.X, start.Y);
        if (startTile == null) return [];

        // 起点不可通行时（玩家被放置在不可通行 tile 上），找最近可通行点作为实际起点
        if (!startTile.IsPassable)
        {
            var altStart = FindNearestPassable(start, mgr);
            if (altStart == start) return []; // 周围全不可通行
            // 返回从当前位置到可通行起点的路径 + 从可通行起点到目标的路径
            var pathFromAlt = TileLevelAStarInternal(altStart, target, mgr);
            if (pathFromAlt.Length == 0) return [start, altStart]; // 至少能脱困
            // 在路径前插入起点
            var combined = new Vector2I[pathFromAlt.Length + 1];
            combined[0] = start;
            Array.Copy(pathFromAlt, 0, combined, 1, pathFromAlt.Length);
            return combined;
        }

        return TileLevelAStarInternal(start, target, mgr);
    }

    /// <summary>Tile 级 A* 内部实现（起点已确认可通行）</summary>
    private Vector2I[] TileLevelAStarInternal(Vector2I start, Vector2I target, ChunkManager mgr)
    {
        var targetTile = mgr.GetTile(target.X, target.Y);
        if (targetTile == null || !targetTile.IsPassable)
        {
            target = FindNearestPassable(target, mgr);
            if (target == start) return [start];
        }

        // 使用 PriorityQueue 替代 List + 线性扫描
        var openQueue = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        var closedSet = new HashSet<Vector2I>();

        float startH = HexHeuristic(start, target);
        openQueue.Enqueue(start, startH);

        int iteration = 0;
        while (openQueue.Count > 0 && iteration < MaxTileIterations)
        {
            iteration++;

            var current = openQueue.Dequeue();

            // 跳过已处理节点 (PriorityQueue 可能有重复条目)
            if (closedSet.Contains(current)) continue;
            closedSet.Add(current);

            if (current == target)
                return ReconstructPath(cameFrom, current);

            float currentG = gScore.GetValueOrDefault(current, float.MaxValue);

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);

                if (closedSet.Contains(neighbor)) continue;

                var nTile = mgr.GetTile(neighbor.X, neighbor.Y);
                if (nTile == null) continue;

                // 根据导航模式判断通行性和代价
                float moveCost;
                if (Mode == NavigationMode.Sea)
                {
                    // 海上模式：水域可通行，陆地不可通行（除海岸）
                    if (!TerrainCostTable.IsSeaPassable(nTile)) continue;
                    moveCost = TerrainCostTable.GetSeaMoveCost(nTile);
                }
                else
                {
                    // 陆地模式：正常通行判定
                    if (!nTile.IsPassable) continue;
                    // 陆地模式下浅水视为不可通行（防止路径穿越海域）
                    if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater) continue;

                    // 优先从预计算代价网格读取（O(1) 数组索引），回退到 tile 查询
                    if (CostGrid != null)
                    {
                        float gridCost = CostGrid.GetCost(neighbor.X, neighbor.Y);
                        moveCost = gridCost > 0f ? gridCost : nTile.MoveCost;
                    }
                    else
                    {
                        moveCost = nTile.MoveCost;
                    }
                }
                float tentativeG = currentG + moveCost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    // 启发式: 基础 hex 距离 + 道路偏好修正
                    float h = HexHeuristic(neighbor, target);

                    // 道路偏好: 如果邻居是道路，轻微降低启发式估计
                    // 这让 A* 更倾向于探索道路方向
                    if (nTile.IsRoad && RoadPreferenceFactor > 0f)
                        h *= (1.0f - RoadPreferenceFactor * 0.5f);

                    openQueue.Enqueue(neighbor, tentativeG + h);
                }
            }
        }

        return [];
    }

    // ========== Layer 1: Chunk 级 A* ==========

    /// <summary>通过 chunk 级 A* 找到朝目标方向的活跃边界瓦片</summary>
    private Vector2I FindBoundaryTarget(Vector2I fromAxial, Vector2I toAxial, ChunkManager mgr)
    {
        var fromChunk = ChunkData.WorldToChunk(fromAxial.X, fromAxial.Y);
        var toChunk = ChunkData.WorldToChunk(toAxial.X, toAxial.Y);

        // Chunk 级 A* 找粗路径
        var chunkPath = ChunkLevelAStar(fromChunk, toChunk, mgr);
        if (chunkPath.Length < 2) return fromAxial;

        // 取路径中最后一个活跃 chunk 的边界方向瓦片
        Vector2I lastActiveChunk = fromChunk;
        for (int i = 1; i < chunkPath.Length; i++)
        {
            if (mgr.ActiveChunks.ContainsKey(chunkPath[i]))
                lastActiveChunk = chunkPath[i];
            else
                break;
        }

        // 找到 lastActiveChunk 朝向下一个 chunk 方向的边界瓦片
        int nextIdx = Array.IndexOf(chunkPath, lastActiveChunk) + 1;
        if (nextIdx >= chunkPath.Length) nextIdx = chunkPath.Length - 1;

        var nextChunk = chunkPath[nextIdx];
        return FindEdgeTile(lastActiveChunk, nextChunk, mgr);
    }

    /// <summary>Chunk 级 A* — 使用 PriorityQueue 优化</summary>
    private Vector2I[] ChunkLevelAStar(Vector2I startChunk, Vector2I targetChunk, ChunkManager mgr)
    {
        if (startChunk == targetChunk) return [startChunk];

        var openQueue = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [startChunk] = 0f };
        var closedSet = new HashSet<Vector2I>();

        openQueue.Enqueue(startChunk, ChunkHeuristic(startChunk, targetChunk));

        int iteration = 0;
        while (openQueue.Count > 0 && iteration < MaxChunkIterations)
        {
            iteration++;

            var current = openQueue.Dequeue();

            if (closedSet.Contains(current)) continue;
            closedSet.Add(current);

            if (current == targetChunk)
                return ReconstructPath(cameFrom, current);

            float currentG = gScore.GetValueOrDefault(current, float.MaxValue);

            // Chunk 的 6 个邻居
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);

                if (closedSet.Contains(neighbor)) continue;

                float cost = mgr.ActiveChunks.ContainsKey(neighbor) ? 1f : 2f;
                float tentativeG = currentG + cost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    openQueue.Enqueue(neighbor, tentativeG + ChunkHeuristic(neighbor, targetChunk));
                }
            }
        }

        // 未找到完整路径，返回已探索的最佳方向
        return [startChunk, targetChunk];
    }

    // ========== 辅助方法 ==========

    /// <summary>找到 fromChunk 朝向 toChunk 方向的边界可通行瓦片</summary>
    private Vector2I FindEdgeTile(Vector2I fromChunk, Vector2I toChunk, ChunkManager mgr)
    {
        var chunkOrigin = ChunkData.ChunkToWorld(fromChunk.X, fromChunk.Y);
        var dirVec = toChunk - fromChunk;

        // 确定边界侧（根据方向选择 chunk 边缘的瓦片行/列）
        int bestQ = chunkOrigin.X + ChunkData.ChunkSize / 2;
        int bestR = chunkOrigin.Y + ChunkData.ChunkSize / 2;

        if (dirVec.X > 0) bestQ = chunkOrigin.X + ChunkData.ChunkSize - 1;
        else if (dirVec.X < 0) bestQ = chunkOrigin.X;

        if (dirVec.Y > 0) bestR = chunkOrigin.Y + ChunkData.ChunkSize - 1;
        else if (dirVec.Y < 0) bestR = chunkOrigin.Y;

        // 优先寻找边界上的道路瓦片
        var roadTile = FindEdgeRoadTile(fromChunk, dirVec, mgr);
        if (roadTile.HasValue) return roadTile.Value;

        // 如果该瓦片可通行，直接返回
        var tile = mgr.GetTile(bestQ, bestR);
        if (tile != null && tile.IsPassable)
            return new Vector2I(bestQ, bestR);

        // 否则在附近找可通行瓦片
        return FindNearestPassable(new Vector2I(bestQ, bestR), mgr);
    }

    /// <summary>在 chunk 边界上寻找道路瓦片（优先走道路出 chunk）</summary>
    private Vector2I? FindEdgeRoadTile(Vector2I fromChunk, Vector2I dirVec, ChunkManager mgr)
    {
        var chunkOrigin = ChunkData.ChunkToWorld(fromChunk.X, fromChunk.Y);

        // 确定扫描的边界行/列
        int fixedQ = -1, fixedR = -1;
        if (dirVec.X > 0) fixedQ = chunkOrigin.X + ChunkData.ChunkSize - 1;
        else if (dirVec.X < 0) fixedQ = chunkOrigin.X;
        if (dirVec.Y > 0) fixedR = chunkOrigin.Y + ChunkData.ChunkSize - 1;
        else if (dirVec.Y < 0) fixedR = chunkOrigin.Y;

        // 扫描边界寻找道路
        if (fixedQ >= 0)
        {
            for (int r = chunkOrigin.Y; r < chunkOrigin.Y + ChunkData.ChunkSize; r++)
            {
                var tile = mgr.GetTile(fixedQ, r);
                if (tile != null && tile.IsRoad && tile.IsPassable)
                    return new Vector2I(fixedQ, r);
            }
        }
        else if (fixedR >= 0)
        {
            for (int q = chunkOrigin.X; q < chunkOrigin.X + ChunkData.ChunkSize; q++)
            {
                var tile = mgr.GetTile(q, fixedR);
                if (tile != null && tile.IsRoad && tile.IsPassable)
                    return new Vector2I(q, fixedR);
            }
        }

        return null;
    }

    /// <summary>BFS 找最近可通行瓦片（考虑导航模式：陆地模式下浅水视为不可通行）</summary>
    private Vector2I FindNearestPassable(Vector2I coord, ChunkManager mgr)
    {
        var visited = new HashSet<Vector2I> { coord };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(coord);

        int maxSearch = 64;
        while (queue.Count > 0 && maxSearch-- > 0)
        {
            var current = queue.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (visited.Contains(neighbor)) continue;
                visited.Add(neighbor);

                var tile = mgr.GetTile(neighbor.X, neighbor.Y);
                if (tile != null && IsTileNavigable(tile))
                    return neighbor;

                if (tile != null)
                    queue.Enqueue(neighbor);
            }
        }

        return coord;
    }

    /// <summary>判断 tile 在当前导航模式下是否可通行</summary>
    private bool IsTileNavigable(HexOverworldTile tile)
    {
        if (Mode == NavigationMode.Sea)
            return TerrainCostTable.IsSeaPassable(tile);
        // 陆地模式：IsPassable 且非浅水
        return tile.IsPassable && tile.Terrain != HexOverworldTile.TerrainType.ShallowWater;
    }

    /// <summary>
    /// 当 FindBoundaryTarget 失败时，直接朝目标方向搜索活跃区域边缘的可通行瓦片。
    /// 沿从起点到目标的方向射线，找到最后一个在活跃 chunk 内的可通行瓦片。
    /// </summary>
    private Vector2I FindDirectionalBoundary(Vector2I fromAxial, Vector2I toAxial, ChunkManager mgr)
    {
        // 计算方向向量（归一化到 hex 步进）
        var fromCube = HexOverworldTile.AxialToCube(fromAxial.X, fromAxial.Y);
        var toCube = HexOverworldTile.AxialToCube(toAxial.X, toAxial.Y);
        var diff = toCube - fromCube;

        // 找到最接近目标方向的 hex 方向 (0-5)
        int bestDir = 0;
        float bestDot = float.MinValue;
        for (int d = 0; d < 6; d++)
        {
            var dirCube = HexOverworldTile.CubeDirections[d];
            float dot = diff.X * dirCube.X + diff.Y * dirCube.Y + diff.Z * dirCube.Z;
            if (dot > bestDot) { bestDot = dot; bestDir = d; }
        }

        // 沿该方向步进，找到最后一个活跃且可通行的瓦片
        Vector2I lastValid = fromAxial;
        Vector2I current = fromAxial;
        int maxSteps = ChunkData.ChunkSize * 3; // 最多走 3 个 chunk 距离

        for (int step = 0; step < maxSteps; step++)
        {
            var next = HexOverworldTile.GetNeighbor(current.X, current.Y, bestDir);

            if (!mgr.IsLoaded(next.X, next.Y)) break;

            var tile = mgr.GetTile(next.X, next.Y);
            if (tile == null) break;
            if (tile.IsPassable) lastValid = next;

            current = next;
        }

        return lastValid;
    }

    /// <summary>
    /// 当主边界目标不可达时，在活跃区域边缘搜索替代的可通行边界瓦片。
    /// 在目标方向的扇形范围内搜索。
    /// </summary>
    private Vector2I FindAlternativeBoundary(Vector2I fromAxial, Vector2I toAxial, ChunkManager mgr)
    {
        // 尝试 6 个方向中最接近目标方向的 3 个
        var fromCube = HexOverworldTile.AxialToCube(fromAxial.X, fromAxial.Y);
        var toCube = HexOverworldTile.AxialToCube(toAxial.X, toAxial.Y);
        var diff = toCube - fromCube;

        // 按方向与目标的匹配度排序
        var directions = new List<(int dir, float dot)>();
        for (int d = 0; d < 6; d++)
        {
            var dirCube = HexOverworldTile.CubeDirections[d];
            float dot = diff.X * dirCube.X + diff.Y * dirCube.Y + diff.Z * dirCube.Z;
            directions.Add((d, dot));
        }
        directions.Sort((a, b) => b.dot.CompareTo(a.dot));

        // 尝试前 3 个方向
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int dir = directions[attempt].dir;
            Vector2I current = fromAxial;
            Vector2I lastValid = fromAxial;

            for (int step = 0; step < ChunkData.ChunkSize * 2; step++)
            {
                var next = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (!mgr.IsLoaded(next.X, next.Y)) break;

                var tile = mgr.GetTile(next.X, next.Y);
                if (tile == null) break;
                if (tile.IsPassable) lastValid = next;
                current = next;
            }

            if (lastValid != fromAxial)
                return lastValid;
        }

        return fromAxial;
    }

    /// <summary>淘汰最旧的缓存条目</summary>
    private void EvictOldestCache()
    {
        var oldest = default(KeyValuePair<(Vector2I, Vector2I), CachedPath>);
        int oldestFrame = int.MaxValue;

        foreach (var kv in _pathCache)
        {
            if (kv.Value.CachedAtFrame < oldestFrame)
            {
                oldestFrame = kv.Value.CachedAtFrame;
                oldest = kv;
            }
        }

        if (oldest.Key != default)
            _pathCache.Remove(oldest.Key);
    }

    /// <summary>Hex 距离启发式（tile 级）</summary>
    private static float HexHeuristic(Vector2I a, Vector2I b)
    {
        return HexOverworldTile.HexDistance(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Hex 距离启发式（chunk 级）</summary>
    private static float ChunkHeuristic(Vector2I a, Vector2I b)
    {
        return HexOverworldTile.HexDistance(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>路径重建</summary>
    private static Vector2I[] ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
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
}
