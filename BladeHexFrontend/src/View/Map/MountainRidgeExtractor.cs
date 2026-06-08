// MountainRidgeExtractor.cs
// 山脉主山脊线提取器 — 从山地连通块中提取连续的山脊骨架。
//
// 算法流程：
//   1. 对每个山地连通块，找到像素距离最远的两个 hex（直径端点）
//   2. 在这两个端点之间跑 A*（代价 = 1 - elevation），得到高程最优路径 = 主山脊
//   3. 沿山脊路径每 32px 采样一个绘制点
//   4. 每个采样点沿路径法线方向随机偏移 ±10px，打破机械感
//   5. 用预计算的距离场约束采样点不超出山地边界过远
//
// 输出：List<RidgeSamplePoint>，每个点包含像素位置、scale 建议、是否主峰标记。
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 山脊采样点 — 最终传递给精灵放置的数据
/// </summary>
public sealed class RidgeSamplePoint
{
    /// <summary>采样点世界像素坐标</summary>
    public Vector2 Position;
    /// <summary>该点所属的 hex 坐标（用于查地形数据）</summary>
    public Vector2I HexCoord;
    /// <summary>建议缩放倍率（基于距离场 + 局部宽度）</summary>
    public float SuggestedScale;
    /// <summary>是否是主峰（路径上高程最高点附近）</summary>
    public bool IsMainPeak;
    /// <summary>该点到最近非山地边界的像素距离</summary>
    public float DistToEdgePixels;
}

/// <summary>
/// 山脉主山脊线提取器
/// </summary>
public static class MountainRidgeExtractor
{
    // ========================================
    // 配置常量
    // ========================================

    /// <summary>沿山脊路径的采样间距（像素）</summary>
    private const float SampleSpacingPx = 32.0f;

    /// <summary>法线方向最大随机偏移（像素）</summary>
    private const float NormalJitterPx = 10.0f;

    /// <summary>A* 中高程权重：cost = 1.0 - elevation * Weight</summary>
    private const float ElevationWeight = 0.8f;

    /// <summary>距离场安全边距（像素）：采样点底座半径不超过 distToEdge + Margin</summary>
    private const float DistanceFieldMargin = 60.0f;

    /// <summary>最小山脉 hex 数才执行骨架提取；太小的直接用中心点</summary>
    private const int MinPatchSizeForRidge = 4;

    // ========================================
    // 多走向分支检测配置
    // ========================================

    /// <summary>分支检测：2-ring 邻居搜索半径（hex 步数）</summary>
    private const int BranchSearchRadius = 2;

    /// <summary>分支触发阈值：邻居 elevation > 主脊当前点 × 此比例时才视为分支起点</summary>
    private const float BranchElevationRatio = 0.85f;

    /// <summary>分支 A* 最大长度（hex 步数），防止分支过长抢夺主干视觉权重</summary>
    private const int MaxBranchLength = 12;

    /// <summary>分支采样间距（像素），比主脊略密以表现细节</summary>
    private const float BranchSampleSpacingPx = 28.0f;

    /// <summary>主脊采样点最小边界距离（像素）：低于此值的采样点被移除，防止纹理溢出</summary>
    private const float MinRidgeSampleEdgeDistPx = 30.0f;

    /// <summary>每个主脊采样点最多触发的分支数</summary>
    private const int MaxBranchesPerSpinePoint = 2;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 从一个山地连通块中提取主山脊采样点。
    /// </summary>
    /// <param name="patchTiles">属于同一连通块的所有山地 tile</param>
    /// <param name="tileLookup">全局 tile 快查字典</param>
    /// <param name="distToEdgeMap">预计算的每个 hex 到非山地边界的 BFS 距离（hex 步数）</param>
    /// <param name="nearestEdgeCoordMap">预计算的每个 hex 最近的边界 hex 坐标</param>
    /// <param name="worldSeed">确定性随机种子</param>
    /// <returns>有序的山脊采样点列表</returns>
    public static List<RidgeSamplePoint> ExtractRidge(
        IReadOnlyList<HexOverworldTile> patchTiles,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        Dictionary<Vector2I, int> distToEdgeMap,
        Dictionary<Vector2I, Vector2I> nearestEdgeCoordMap,
        int worldSeed)
    {
        var result = new List<RidgeSamplePoint>();
        if (patchTiles == null || patchTiles.Count == 0) return result;

        // 小山脉直接返回中心点
        if (patchTiles.Count < MinPatchSizeForRidge)
        {
            var center = FindCentroid(patchTiles);
            var sp = CreateSamplePoint(center, tileLookup, distToEdgeMap, nearestEdgeCoordMap, true);
            if (sp != null) result.Add(sp);
            return result;
        }

        // Step 1: 找最远 hex 对（直径端点）
        var (endA, endB) = FindDiameterEndpoints(patchTiles);

        // Step 2: A* 求主山脊路径
        var ridgePath = AStarRidgePath(endA, endB, tileLookup, patchTiles);
        if (ridgePath.Count == 0)
        {
            // A* 失败回退：直线插值
            ridgePath = FallbackLinearPath(endA, endB, tileLookup);
        }

        if (ridgePath.Count == 0)
        {
            var center = FindCentroid(patchTiles);
            var sp = CreateSamplePoint(center, tileLookup, distToEdgeMap, nearestEdgeCoordMap, true);
            if (sp != null) result.Add(sp);
            return result;
        }

        // Step 3-5: 沿路径采样 + 法线偏移 + 距离场约束
        result = SampleAlongPath(ridgePath, tileLookup, distToEdgeMap, nearestEdgeCoordMap, worldSeed,
                                 spacingOverride: SampleSpacingPx);

        // Step 6: 多走向分支检测 — 在主脊上寻找次级山脊并追加采样点
        var branchPoints = ExtractBranches(ridgePath, result, patchTiles, tileLookup,
                                           distToEdgeMap, nearestEdgeCoordMap, worldSeed);
        result.AddRange(branchPoints);

        return result;
    }

    // ========================================
    // Step 1: 最远 Hex 对（双 BFS 求图直径）
    // ========================================

    /// <summary>
    /// 用两次 BFS 近似求连通块的图直径端点。
    /// 第一次从任意点出发找最远点 A，第二次从 A 出发找最远点 B。
    /// (A, B) 即为近似直径端点。
    /// </summary>
    private static (Vector2I a, Vector2I b) FindDiameterEndpoints(IReadOnlyList<HexOverworldTile> patchTiles)
    {
        // 构建局部邻接集合
        var coordSet = new HashSet<Vector2I>(patchTiles.Count);
        foreach (var t in patchTiles) coordSet.Add(t.Coord);

        // 第一次 BFS：从第一个 tile 出发
        var startCoord = patchTiles[0].Coord;
        var farA = BfsFarthest(startCoord, coordSet);

        // 第二次 BFS：从 farA 出发
        var farB = BfsFarthest(farA, coordSet);

        return (farA, farB);
    }

    private static Vector2I BfsFarthest(Vector2I origin, HashSet<Vector2I> validCoords)
    {
        var visited = new HashSet<Vector2I> { origin };
        var queue = new Queue<(Vector2I coord, int dist)>();
        queue.Enqueue((origin, 0));

        Vector2I farthest = origin;
        int maxDist = 0;

        while (queue.Count > 0)
        {
            var (curr, dist) = queue.Dequeue();
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = curr;
            }

            foreach (var n in HexNeighbors(curr))
            {
                if (validCoords.Contains(n) && visited.Add(n))
                    queue.Enqueue((n, dist + 1));
            }
        }

        return farthest;
    }

    // ========================================
    // Step 2: A* 主山脊路径
    // ========================================

    /// <summary>
    /// A* 寻路：从 start 到 end，优先走高程高的 hex（模拟山脊走向）。
    /// 代价函数：g + h，其中移动代价 = 1.0 - elevation * ElevationWeight
    /// </summary>
    private static List<Vector2I> AStarRidgePath(
        Vector2I start, Vector2I end,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        IReadOnlyList<HexOverworldTile> patchTiles)
    {
        var coordSet = new HashSet<Vector2I>(patchTiles.Count);
        foreach (var t in patchTiles) coordSet.Add(t.Coord);

        // 开放集 / 关闭集
        var openSet = new SortedSet<(float fScore, Vector2I coord)>(
            Comparer<(float, Vector2I)>.Create((a, b) =>
            {
                int cmp = a.Item1.CompareTo(b.Item1);
                if (cmp != 0) return cmp;
                cmp = a.Item2.X.CompareTo(b.Item2.X);
                if (cmp != 0) return cmp;
                return a.Item2.Y.CompareTo(b.Item2.Y);
            }));

        var gScore = new Dictionary<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var closedSet = new HashSet<Vector2I>();

        gScore[start] = 0f;
        openSet.Add((Heuristic(start, end), start));

        while (openSet.Count > 0)
        {
            var (_, current) = openSet.Min;
            openSet.Remove(openSet.Min);

            if (current == end)
                return ReconstructPath(cameFrom, start, end);

            closedSet.Add(current);

            foreach (var neighbor in HexNeighbors(current))
            {
                if (!coordSet.Contains(neighbor)) continue;
                if (closedSet.Contains(neighbor)) continue;

                // 移动代价：低高程 = 高代价（迫使路径走山脊）
                float elev = 0.5f;
                if (tileLookup.TryGetValue(neighbor, out var nTile))
                    elev = nTile.Elevation;
                float moveCost = 1.0f - elev * ElevationWeight;

                float tentativeG = gScore.GetValueOrDefault(current, float.MaxValue) + moveCost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    // 更新前先移除旧条目
                    float oldF = gScore.GetValueOrDefault(neighbor, float.MaxValue) + Heuristic(neighbor, end);
                    openSet.Remove((oldF, neighbor));

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float fScore = tentativeG + Heuristic(neighbor, end);
                    openSet.Add((fScore, neighbor));
                }
            }
        }

        // 无法到达
        return new List<Vector2I>();
    }

    /// <summary>六边形轴向坐标的 Manhattan-like 启发式</summary>
    private static float Heuristic(Vector2I a, Vector2I b)
    {
        // axial 坐标下的 hex 距离
        int dx = a.X - b.X;
        int dy = a.Y - b.Y;
        int dz = -dx - dy;
        return (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz)) * 0.5f;
    }

    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I start, Vector2I end)
    {
        var path = new List<Vector2I> { end };
        var current = end;
        while (current != start)
        {
            if (!cameFrom.TryGetValue(current, out var prev))
                break; // 不应发生
            path.Add(prev);
            current = prev;
        }
        path.Reverse();
        return path;
    }

    /// <summary>A* 失败时的直线回退路径</summary>
    private static List<Vector2I> FallbackLinearPath(
        Vector2I start, Vector2I end,
        Dictionary<Vector2I, HexOverworldTile> tileLookup)
    {
        var path = new List<Vector2I> { start };
        var current = start;
        int maxSteps = 200; // 防死循环
        int steps = 0;

        while (current != end && steps++ < maxSteps)
        {
            Vector2I best = current;
            float bestDist = float.MaxValue;
            foreach (var n in HexNeighbors(current))
            {
                if (!tileLookup.ContainsKey(n)) continue;
                float d = Heuristic(n, end);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
            if (best == current) break; // 卡住
            path.Add(best);
            current = best;
        }

        return path;
    }

    // ========================================
    // Step 3-5: 沿路径采样 + 法线偏移 + 距离场约束
    // ========================================

    private static List<RidgeSamplePoint> SampleAlongPath(
        List<Vector2I> ridgePath,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        Dictionary<Vector2I, int> distToEdgeMap,
        Dictionary<Vector2I, Vector2I> nearestEdgeCoordMap,
        int worldSeed,
        float spacingOverride = SampleSpacingPx)
    {
        float sampleSpacing = spacingOverride;
        var points = new List<RidgeSamplePoint>();
        if (ridgePath.Count == 0) return points;

        // 将 hex 路径转为像素路径
        var pixelPath = new List<Vector2>(ridgePath.Count);
        foreach (var coord in ridgePath)
        {
            if (tileLookup.TryGetValue(coord, out var tile))
                pixelPath.Add(tile.PixelPos);
            else
                pixelPath.Add(HexToPixelApprox(coord));
        }

        // 计算路径总长度
        float totalLength = 0f;
        for (int i = 1; i < pixelPath.Count; i++)
            totalLength += pixelPath[i].DistanceTo(pixelPath[i - 1]);

        // 找路径上高程最高的点索引（主峰标记）
        int peakIndex = 0;
        float peakElev = -1f;
        for (int i = 0; i < ridgePath.Count; i++)
        {
            if (tileLookup.TryGetValue(ridgePath[i], out var tile) && tile.Elevation > peakElev)
            {
                peakElev = tile.Elevation;
                peakIndex = i;
            }
        }

        // 沿路径等距采样
        float accumulated = 0f;
        int segIndex = 0;
        float segConsumed = 0f;

        // 始终添加起点
        AddSampleAtPixel(pixelPath[0], ridgePath[0], tileLookup, distToEdgeMap, nearestEdgeCoordMap,
                         worldSeed, isMainPeak: peakIndex == 0, points);

        float sampleAccum = sampleSpacing;
        while (sampleAccum <= totalLength && segIndex < pixelPath.Count - 1)
        {
            float segLen = pixelPath[segIndex + 1].DistanceTo(pixelPath[segIndex]);
            float remaining = segLen - segConsumed;

            if (remaining >= sampleAccum - accumulated)
            {
                // 在当前段内可以放一个采样点
                float t = (sampleAccum - accumulated + segConsumed) / segLen;
                t = Mathf.Clamp(t, 0f, 1f);
                Vector2 samplePos = pixelPath[segIndex].Lerp(pixelPath[segIndex + 1], t);

                // 确定最近的 hex 坐标
                int nearestIdx = Mathf.RoundToInt(segIndex + t);
                nearestIdx = Mathf.Clamp(nearestIdx, 0, ridgePath.Count - 1);
                var nearestCoord = ridgePath[nearestIdx];

                bool isPeak = nearestIdx == peakIndex;
                AddSampleAtPixel(samplePos, nearestCoord, tileLookup, distToEdgeMap, nearestEdgeCoordMap,
                                 worldSeed, isMainPeak: isPeak, points);

                accumulated = sampleAccum;
                sampleAccum += sampleSpacing;
                segConsumed = (sampleAccum - accumulated) > 0 ? 0 : segConsumed;
                // 不推进 segIndex，可能同一段还能放更多点
                // 但需要更新 segConsumed
                segConsumed = (sampleAccum - (accumulated - sampleSpacing + segLen));
                // 简化：重新计算
                float usedInSeg = sampleAccum - (accumulated - sampleSpacing);
                segConsumed = usedInSeg;
                if (segConsumed >= segLen)
                {
                    segConsumed -= segLen;
                    segIndex++;
                    accumulated += segLen;
                }
            }
            else
            {
                // 当前段不够，跳到下一段
                accumulated += remaining;
                segConsumed = 0f;
                segIndex++;
            }
        }

        // 始终添加终点
        if (pixelPath.Count > 1)
        {
            var lastCoord = ridgePath[ridgePath.Count - 1];
            bool isPeak = peakIndex == ridgePath.Count - 1;
            // 避免与上一个采样点太近
            if (points.Count == 0 || pixelPath[pixelPath.Count - 1].DistanceTo(points[points.Count - 1].Position) > sampleSpacing * 0.5f)
            {
                AddSampleAtPixel(pixelPath[pixelPath.Count - 1], lastCoord, tileLookup, distToEdgeMap, nearestEdgeCoordMap,
                                 worldSeed, isMainPeak: isPeak, points);
            }
        }

        // 过滤掉离边界太近的主脊采样点，防止纹理溢出到非山地区域（与分支过滤保持一致）
        points.RemoveAll(sp => sp.DistToEdgePixels < MinRidgeSampleEdgeDistPx);

        return points;
    }

    /// <summary>
    /// 在指定像素位置创建一个采样点，应用法线偏移和距离场约束。
    /// </summary>
    private static void AddSampleAtPixel(
        Vector2 basePos, Vector2I hexCoord,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        Dictionary<Vector2I, int> distToEdgeMap,
        Dictionary<Vector2I, Vector2I> nearestEdgeCoordMap,
        int worldSeed, bool isMainPeak,
        List<RidgeSamplePoint> output)
    {
        // 确定性哈希
        uint salt = (uint)((Mathf.RoundToInt(basePos.X) * 73856093) ^ (Mathf.RoundToInt(basePos.Y) * 19349663) ^ (uint)worldSeed);
        uint h1 = Hash(salt);
        uint h2 = Hash(salt + 0xABCDu);

        // 法线偏移：用相邻路径点的切线估算法线方向
        // 这里简化为纯随机角度偏移（因为单点没有切线信息）
        float jitterAngle = (h1 / (float)uint.MaxValue) * Mathf.Tau;
        float jitterMag = (h2 / (float)uint.MaxValue) * NormalJitterPx;
        Vector2 offset = new Vector2(Mathf.Cos(jitterAngle) * jitterMag, Mathf.Sin(jitterAngle) * jitterMag);
        Vector2 finalPos = basePos + offset;

        // 距离场查询
        float distToEdgePx = ComputeDistToEdgePixels(finalPos, hexCoord, tileLookup, distToEdgeMap, nearestEdgeCoordMap);

        // 距离场约束：如果偏移后离边界太近，把偏移量缩减
        if (distToEdgePx < DistanceFieldMargin)
        {
            float shrink = Mathf.Max(0f, distToEdgePx / DistanceFieldMargin);
            finalPos = basePos + offset * shrink;
            distToEdgePx = ComputeDistToEdgePixels(finalPos, hexCoord, tileLookup, distToEdgeMap, nearestEdgeCoordMap);
        }

        // 建议 scale：基于距离场的自然缩放
        // distToEdgePx 越大 → 越靠近山脉中心 → 允许更大的山峰
        float suggestedScale = Mathf.Clamp(distToEdgePx / 40.0f, 1.5f, 8.0f);
        if (isMainPeak) suggestedScale = Mathf.Max(suggestedScale, 5.0f);

        var sp = new RidgeSamplePoint
        {
            Position = finalPos,
            HexCoord = hexCoord,
            SuggestedScale = suggestedScale,
            IsMainPeak = isMainPeak,
            DistToEdgePixels = distToEdgePx,
        };
        output.Add(sp);
    }

    /// <summary>
    /// 计算某像素位置到最近非山地边界的物理距离（像素）。
    /// 利用预计算的 hex 级 BFS 数据做近似。
    /// </summary>
    private static float ComputeDistToEdgePixels(
        Vector2 pos, Vector2I hexCoord,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        Dictionary<Vector2I, int> distToEdgeMap,
        Dictionary<Vector2I, Vector2I> nearestEdgeCoordMap)
    {
        // 方法：取该 hex 的最近边界 hex，算像素距离再减去 hex 内半径修正
        if (nearestEdgeCoordMap.TryGetValue(hexCoord, out var edgeCoord) &&
            tileLookup.TryGetValue(edgeCoord, out var edgeTile))
        {
            float rawDist = pos.DistanceTo(edgeTile.PixelPos);
            // 减去 hex 内切圆半径作为修正（边界 hex 本身也有大小）
            // HexInradius = HexSize * sqrt(3)/2 ≈ 156 * 0.866 ≈ 135px
            const float HexInradius = 135.0f;
            return Mathf.Max(0f, rawDist - HexInradius);
        }

        // 回退：用 BFS 距离 × hex 间距估算
        int bfsDist = distToEdgeMap.GetValueOrDefault(hexCoord, 1);
        return bfsDist * 156.0f; // hex 间距 ≈ 156px
    }

    // ========================================
    // 工具方法
    // ========================================

    private static HexOverworldTile? FindCentroid(IReadOnlyList<HexOverworldTile> tiles)
    {
        if (tiles.Count == 0) return null;
        // 简单取平均坐标最近的 tile
        float avgX = 0, avgY = 0;
        foreach (var t in tiles) { avgX += t.Coord.X; avgY += t.Coord.Y; }
        avgX /= tiles.Count; avgY /= tiles.Count;

        HexOverworldTile? best = null;
        float bestDist = float.MaxValue;
        foreach (var t in tiles)
        {
            float d = (t.Coord.X - avgX) * (t.Coord.X - avgX) + (t.Coord.Y - avgY) * (t.Coord.Y - avgY);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    private static RidgeSamplePoint? CreateSamplePoint(
        HexOverworldTile? tile,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        Dictionary<Vector2I, int> distToEdgeMap,
        Dictionary<Vector2I, Vector2I> nearestEdgeCoordMap,
        bool isMainPeak)
    {
        if (tile == null) return null;
        float distPx = ComputeDistToEdgePixels(tile.PixelPos, tile.Coord, tileLookup, distToEdgeMap, nearestEdgeCoordMap);
        float scale = Mathf.Clamp(distPx / 40.0f, 1.5f, 8.0f);
        if (isMainPeak) scale = Mathf.Max(scale, 5.0f);

        return new RidgeSamplePoint
        {
            Position = tile.PixelPos,
            HexCoord = tile.Coord,
            SuggestedScale = scale,
            IsMainPeak = isMainPeak,
            DistToEdgePixels = distPx,
        };
    }

    /// <summary>axial 坐标 → 像素坐标的近似转换（无 tile 数据时的回退）</summary>
    private static Vector2 HexToPixelApprox(Vector2I coord)
    {
        const float size = 156.0f;
        float x = size * (3.0f / 2.0f * coord.X);
        float y = size * (Mathf.Sqrt(3.0f) * (coord.Y + coord.X / 2.0f));
        return new Vector2(x, y);
    }

    /// <summary>Axial 坐标的 6 个邻居</summary>
    private static IEnumerable<Vector2I> HexNeighbors(Vector2I coord)
    {
        yield return new Vector2I(coord.X + 1, coord.Y);
        yield return new Vector2I(coord.X - 1, coord.Y);
        yield return new Vector2I(coord.X, coord.Y + 1);
        yield return new Vector2I(coord.X, coord.Y - 1);
        yield return new Vector2I(coord.X + 1, coord.Y - 1);
        yield return new Vector2I(coord.X - 1, coord.Y + 1);
    }

    // ========================================
    // Step 6: 多走向分支检测
    // ========================================

    /// <summary>
    /// 在主脊路径上检测次级山脊分支。
    /// 对主脊每个采样点的 2-ring 邻居，若存在高海拔且不在主脊上的 hex，
    /// 则以该 hex 为起点向远离主脊方向跑短 A*，生成次级山脊采样点。
    /// </summary>
    private static List<RidgeSamplePoint> ExtractBranches(
        List<Vector2I> mainRidgePath,
        List<RidgeSamplePoint> mainSpinePoints,
        IReadOnlyList<HexOverworldTile> patchTiles,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        Dictionary<Vector2I, int> distToEdgeMap,
        Dictionary<Vector2I, Vector2I> nearestEdgeCoordMap,
        int worldSeed)
    {
        var branchResults = new List<RidgeSamplePoint>();
        if (mainRidgePath.Count < 3) return branchResults;

        // 构建主脊坐标集合（用于排除）
        var spineSet = new HashSet<Vector2I>(mainRidgePath);

        // 构建 patch 坐标集合（用于限制搜索范围）
        var patchSet = new HashSet<Vector2I>(patchTiles.Count);
        foreach (var t in patchTiles) patchSet.Add(t.Coord);

        // 已用作分支起点的 hex（避免重复）
        var usedBranchOrigins = new HashSet<Vector2I>();

        // 遍历主脊路径上的每个 hex（不是采样点，是原始路径 hex）
        for (int i = 0; i < mainRidgePath.Count; i++)
        {
            var spineCoord = mainRidgePath[i];
            if (!tileLookup.TryGetValue(spineCoord, out var spineTile)) continue;
            float spineElev = spineTile.Elevation;

            // 在 2-ring 内搜索分支候选
            var candidates = new List<(Vector2I coord, float elev)>();
            foreach (var ring1 in HexNeighbors(spineCoord))
            {
                if (!patchSet.Contains(ring1)) continue;
                if (spineSet.Contains(ring1)) continue;
                if (usedBranchOrigins.Contains(ring1)) continue;
                if (!tileLookup.TryGetValue(ring1, out var t1)) continue;
                if (t1.Elevation >= spineElev * BranchElevationRatio)
                    candidates.Add((ring1, t1.Elevation));

                // 2-ring：ring1 的邻居中不在主脊且不在 ring1 本身的
                foreach (var ring2 in HexNeighbors(ring1))
                {
                    if (ring2 == spineCoord) continue;
                    if (!patchSet.Contains(ring2)) continue;
                    if (spineSet.Contains(ring2)) continue;
                    if (usedBranchOrigins.Contains(ring2)) continue;
                    if (!tileLookup.TryGetValue(ring2, out var t2)) continue;
                    if (t2.Elevation >= spineElev * BranchElevationRatio)
                        candidates.Add((ring2, t2.Elevation));
                }
            }

            // 按高程降序取 Top-K
            candidates.Sort((a, b) => b.elev.CompareTo(a.elev));
            int branchesAdded = 0;

            foreach (var (branchOrigin, _) in candidates)
            {
                if (branchesAdded >= MaxBranchesPerSpinePoint) break;
                if (usedBranchOrigins.Contains(branchOrigin)) continue;

                // 确定分支终点：从 origin 出发，BFS 找最远的非主脊 hex（限 MaxBranchLength 步）
                var branchEnd = BfsFarthestWithinRadius(branchOrigin, patchSet, spineSet, MaxBranchLength);
                if (branchEnd == branchOrigin) continue; // 没有有效分支

                // 短 A* 提取分支路径
                var branchPath = AStarBranchPath(branchOrigin, branchEnd, tileLookup, patchSet, spineSet, MaxBranchLength);
                if (branchPath.Count < 2) continue;

                // 标记这些 hex 已使用
                foreach (var bp in branchPath) usedBranchOrigins.Add(bp);

                // 采样分支路径（使用更密的间距）
                var branchSamples = SampleAlongPath(branchPath, tileLookup, distToEdgeMap, nearestEdgeCoordMap,
                                                    worldSeed, spacingOverride: BranchSampleSpacingPx);

                // 分支采样点 scale 整体缩小（次级山脊不应比主脊大）
                foreach (var sp in branchSamples)
                {
                    sp.SuggestedScale *= 0.65f;
                    sp.IsMainPeak = false; // 分支上没有主峰
                }

                // 过滤掉离边界太近的分支采样点，防止纹理溢出到非山地区域
                branchSamples.RemoveAll(sp => sp.DistToEdgePixels < 30f);

                branchResults.AddRange(branchSamples);
                branchesAdded++;
            }
        }

        return branchResults;
    }

    /// <summary>
    /// 从 origin 出发，在 validCoords 内 BFS 找最远点，但不穿越 excludeCoords，
    /// 且最大搜索半径为 maxSteps。
    /// </summary>
    private static Vector2I BfsFarthestWithinRadius(
        Vector2I origin, HashSet<Vector2I> validCoords,
        HashSet<Vector2I> excludeCoords, int maxSteps)
    {
        var visited = new HashSet<Vector2I> { origin };
        var queue = new Queue<(Vector2I coord, int dist)>();
        queue.Enqueue((origin, 0));

        Vector2I farthest = origin;
        int maxDist = 0;

        while (queue.Count > 0)
        {
            var (curr, dist) = queue.Dequeue();
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = curr;
            }
            if (dist >= maxSteps) continue;

            foreach (var n in HexNeighbors(curr))
            {
                if (!validCoords.Contains(n)) continue;
                if (excludeCoords.Contains(n)) continue;
                if (visited.Add(n))
                    queue.Enqueue((n, dist + 1));
            }
        }

        return farthest;
    }

    /// <summary>
    /// 分支专用 A*：从 start 到 end，限制最大步数，且不穿越主脊 hex。
    /// </summary>
    private static List<Vector2I> AStarBranchPath(
        Vector2I start, Vector2I end,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        HashSet<Vector2I> validCoords,
        HashSet<Vector2I> spineExclude,
        int maxSteps)
    {
        var openSet = new SortedSet<(float fScore, Vector2I coord)>(
            Comparer<(float, Vector2I)>.Create((a, b) =>
            {
                int cmp = a.Item1.CompareTo(b.Item1);
                if (cmp != 0) return cmp;
                cmp = a.Item2.X.CompareTo(b.Item2.X);
                if (cmp != 0) return cmp;
                return a.Item2.Y.CompareTo(b.Item2.Y);
            }));

        var gScore = new Dictionary<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var closedSet = new HashSet<Vector2I>();

        gScore[start] = 0f;
        openSet.Add((Heuristic(start, end), start));

        while (openSet.Count > 0)
        {
            var (_, current) = openSet.Min;
            openSet.Remove(openSet.Min);

            if (current == end)
                return ReconstructPath(cameFrom, start, end);

            closedSet.Add(current);

            // 限制分支长度
            float currentG = gScore.GetValueOrDefault(current, float.MaxValue);
            if (currentG >= maxSteps) continue;

            foreach (var neighbor in HexNeighbors(current))
            {
                if (!validCoords.Contains(neighbor)) continue;
                if (spineExclude.Contains(neighbor)) continue;
                if (closedSet.Contains(neighbor)) continue;

                float elev = 0.5f;
                if (tileLookup.TryGetValue(neighbor, out var nTile))
                    elev = nTile.Elevation;
                float moveCost = 1.0f - elev * ElevationWeight;

                float tentativeG = currentG + moveCost;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    float oldF = gScore.GetValueOrDefault(neighbor, float.MaxValue) + Heuristic(neighbor, end);
                    openSet.Remove((oldF, neighbor));

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float fScore = tentativeG + Heuristic(neighbor, end);
                    openSet.Add((fScore, neighbor));
                }
            }
        }

        return new List<Vector2I>();
    }

    /// <summary>FNV-1a-like 32-bit hash</summary>
    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
}
