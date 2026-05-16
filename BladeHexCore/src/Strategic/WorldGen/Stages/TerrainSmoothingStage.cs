// TerrainSmoothingStage.cs
// 世界生成阶段 2：消除零散小块 + 强制逻辑过渡。
//
// 抽取自 WorldCreator.SmoothIsolatedTerrainPatches + 4 个静态 helper。
// RNG：无（纯几何/集合运算）。
using System;
using System.Collections.Generic;
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 2：地形平滑 — 消除零散小块、修正非法邻接、清理孤立水域/陆地碎片。
/// </summary>
public sealed class TerrainSmoothingStage : IWorldStage
{
    public string Name => "平滑地形";
    public float ProgressWeight => 10f;

    public void Execute(WorldBuildContext ctx)
    {
        var chunks = ctx.Chunks;

        // 收集所有瓦片
        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in chunks.Values)
            foreach (var kvp in chunk.Tiles)
                allTiles[kvp.Key] = kvp.Value;

        // ========================================
        // Pass 1: 消除小于 50 瓦片的孤立区域
        // ========================================
        var exemptTerrains = new HashSet<HexOverworldTile.TerrainType>
        {
            HexOverworldTile.TerrainType.DeepWater,
            HexOverworldTile.TerrainType.ShallowWater,
            HexOverworldTile.TerrainType.Mountain,
            HexOverworldTile.TerrainType.MountainSnow,
            HexOverworldTile.TerrainType.River,
            HexOverworldTile.TerrainType.Road,
        };

        const int MinClusterSize = 50;

        var terrainGroups = new Dictionary<HexOverworldTile.TerrainType, HashSet<Vector2I>>();
        foreach (var (coord, tile) in allTiles)
        {
            if (exemptTerrains.Contains(tile.Terrain)) continue;
            if (!terrainGroups.ContainsKey(tile.Terrain))
                terrainGroups[tile.Terrain] = new HashSet<Vector2I>();
            terrainGroups[tile.Terrain].Add(coord);
        }

        int mergedCount = 0;
        foreach (var (terrainType, tileSet) in terrainGroups)
        {
            var visited = new HashSet<Vector2I>();
            foreach (var start in tileSet)
            {
                if (visited.Contains(start)) continue;

                var cluster = new List<Vector2I>();
                var queue = new Queue<Vector2I>();
                queue.Enqueue(start);
                visited.Add(start);

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    cluster.Add(cur);

                    for (int d = 0; d < 6; d++)
                    {
                        var nb = HexOverworldTile.GetNeighbor(cur.X, cur.Y, d);
                        if (!visited.Contains(nb) && tileSet.Contains(nb))
                        {
                            visited.Add(nb);
                            queue.Enqueue(nb);
                        }
                    }
                }

                if (cluster.Count < MinClusterSize)
                {
                    var replacement = FindDominantNeighborTerrain(cluster, allTiles, terrainType);
                    foreach (var pos in cluster)
                    {
                        if (allTiles.TryGetValue(pos, out var tile))
                        {
                            tile.Terrain = replacement;
                            tile.Elevation = AdjustElevationForTerrain(replacement, tile.Elevation);
                        }
                    }
                    mergedCount += cluster.Count;
                }
            }
        }

        // ========================================
        // Pass 2: 强制逻辑过渡 — 修正非法邻接
        // ========================================
        int fixedCount = 0;
        for (int pass = 0; pass < 3; pass++)
        {
            int fixedThisPass = 0;
            foreach (var (coord, tile) in allTiles)
            {
                if (exemptTerrains.Contains(tile.Terrain)) continue;

                for (int d = 0; d < 6; d++)
                {
                    var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
                    if (!allTiles.TryGetValue(nb, out var nbTile)) continue;

                    if (IsIllegalAdjacency(tile.Terrain, nbTile.Terrain))
                    {
                        int mySupport = CountSameTerrainNeighbors(coord, tile.Terrain, allTiles);
                        int nbSupport = CountSameTerrainNeighbors(nb, nbTile.Terrain, allTiles);

                        if (mySupport <= nbSupport)
                        {
                            tile.Terrain = GetTransitionTerrain(tile.Terrain, nbTile.Terrain);
                            fixedThisPass++;
                        }
                    }
                }
            }
            fixedCount += fixedThisPass;
            if (fixedThisPass == 0) break;
        }

        // 同步回 chunk
        SyncTilesBackToChunks(chunks, allTiles);

        // ========================================
        // Pass 3: 水体平滑 — 消除孤立水坑和陆地碎片
        // ========================================
        int waterFixed = 0;

        // 3a: 消除孤立小水体
        {
            var waterVisited = new HashSet<Vector2I>();
            var waterTypes = new HashSet<HexOverworldTile.TerrainType>
            {
                HexOverworldTile.TerrainType.DeepWater,
                HexOverworldTile.TerrainType.ShallowWater,
            };

            foreach (var (coord, tile) in allTiles)
            {
                if (waterVisited.Contains(coord)) continue;
                if (!waterTypes.Contains(tile.Terrain)) { waterVisited.Add(coord); continue; }

                var waterCluster = new List<Vector2I>();
                var wQueue = new Queue<Vector2I>();
                wQueue.Enqueue(coord);
                waterVisited.Add(coord);

                while (wQueue.Count > 0)
                {
                    var cur = wQueue.Dequeue();
                    waterCluster.Add(cur);
                    for (int d = 0; d < 6; d++)
                    {
                        var nb = HexOverworldTile.GetNeighbor(cur.X, cur.Y, d);
                        if (waterVisited.Contains(nb)) continue;
                        if (!allTiles.TryGetValue(nb, out var nbTile)) continue;
                        if (!waterTypes.Contains(nbTile.Terrain)) continue;
                        waterVisited.Add(nb);
                        wQueue.Enqueue(nb);
                    }
                }

                if (waterCluster.Count < 8)
                {
                    foreach (var pos in waterCluster)
                    {
                        if (allTiles.TryGetValue(pos, out var wTile))
                        {
                            wTile.Terrain = HexOverworldTile.TerrainType.Grassland;
                            wTile.Elevation = 0.35f;
                        }
                    }
                    waterFixed += waterCluster.Count;
                }
            }
        }

        // 3b: 消除水体中的孤立陆地碎片
        {
            var landVisited = new HashSet<Vector2I>();
            foreach (var (coord, tile) in allTiles)
            {
                if (landVisited.Contains(coord)) continue;
                if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                    tile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                {
                    landVisited.Add(coord);
                    continue;
                }

                var landCluster = new List<Vector2I>();
                var lQueue = new Queue<Vector2I>();
                lQueue.Enqueue(coord);
                landVisited.Add(coord);

                while (lQueue.Count > 0)
                {
                    var cur = lQueue.Dequeue();
                    landCluster.Add(cur);
                    for (int d = 0; d < 6; d++)
                    {
                        var nb = HexOverworldTile.GetNeighbor(cur.X, cur.Y, d);
                        if (landVisited.Contains(nb)) continue;
                        if (!allTiles.TryGetValue(nb, out var nbTile)) continue;
                        if (nbTile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                            nbTile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                            continue;
                        landVisited.Add(nb);
                        lQueue.Enqueue(nb);
                    }
                }

                if (landCluster.Count < 5)
                {
                    int waterNeighborCount = 0;
                    int totalNeighborCount = 0;
                    foreach (var pos in landCluster)
                    {
                        for (int d = 0; d < 6; d++)
                        {
                            var nb = HexOverworldTile.GetNeighbor(pos.X, pos.Y, d);
                            if (landCluster.Contains(nb)) continue;
                            totalNeighborCount++;
                            if (allTiles.TryGetValue(nb, out var nbTile) &&
                                (nbTile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                                 nbTile.Terrain == HexOverworldTile.TerrainType.ShallowWater))
                                waterNeighborCount++;
                        }
                    }

                    if (totalNeighborCount > 0 && (float)waterNeighborCount / totalNeighborCount > 0.6f)
                    {
                        foreach (var pos in landCluster)
                        {
                            if (allTiles.TryGetValue(pos, out var lTile))
                            {
                                lTile.Terrain = HexOverworldTile.TerrainType.ShallowWater;
                                lTile.Elevation = 0.25f;
                            }
                        }
                        waterFixed += landCluster.Count;
                    }
                }
            }
        }

        // 再次同步回 chunk
        SyncTilesBackToChunks(chunks, allTiles);

        GD.Print($"[TerrainSmoothingStage] 合并 {mergedCount} 个零散瓦片, 修正 {fixedCount} 个非法邻接, 水体修正 {waterFixed} 个");
    }

    private static void SyncTilesBackToChunks(
        Dictionary<Vector2I, ChunkData> chunks,
        Dictionary<Vector2I, HexOverworldTile> allTiles)
    {
        foreach (var chunk in chunks.Values)
        {
            foreach (var (coord, tile) in chunk.Tiles)
            {
                if (allTiles.TryGetValue(coord, out var updated))
                    tile.Terrain = updated.Terrain;
            }
        }
    }

    // ========================================
    // Helpers（与原 WorldCreator 实现等价）
    // ========================================

    private static bool IsIllegalAdjacency(HexOverworldTile.TerrainType a, HexOverworldTile.TerrainType b)
    {
        if ((int)a > (int)b) (a, b) = (b, a);

        if (a == HexOverworldTile.TerrainType.Sand &&
            (b == HexOverworldTile.TerrainType.Snow || b == HexOverworldTile.TerrainType.Ice ||
             b == HexOverworldTile.TerrainType.Taiga || b == HexOverworldTile.TerrainType.Bog ||
             b == HexOverworldTile.TerrainType.DenseForest))
            return true;

        if ((a == HexOverworldTile.TerrainType.Snow || a == HexOverworldTile.TerrainType.Ice) &&
            (b == HexOverworldTile.TerrainType.Jungle || b == HexOverworldTile.TerrainType.Swamp ||
             b == HexOverworldTile.TerrainType.Savanna || b == HexOverworldTile.TerrainType.Sand))
            return true;

        if (a == HexOverworldTile.TerrainType.Jungle &&
            (b == HexOverworldTile.TerrainType.Taiga || b == HexOverworldTile.TerrainType.Bog ||
             b == HexOverworldTile.TerrainType.Rocky))
            return true;

        if (a == HexOverworldTile.TerrainType.Savanna &&
            (b == HexOverworldTile.TerrainType.Taiga || b == HexOverworldTile.TerrainType.Bog ||
             b == HexOverworldTile.TerrainType.Snow || b == HexOverworldTile.TerrainType.Ice))
            return true;

        return false;
    }

    private static HexOverworldTile.TerrainType GetTransitionTerrain(
        HexOverworldTile.TerrainType from, HexOverworldTile.TerrainType to)
    {
        bool fromHot = from == HexOverworldTile.TerrainType.Sand ||
                       from == HexOverworldTile.TerrainType.Savanna ||
                       from == HexOverworldTile.TerrainType.Jungle ||
                       from == HexOverworldTile.TerrainType.Swamp;
        bool toCold = to == HexOverworldTile.TerrainType.Snow ||
                      to == HexOverworldTile.TerrainType.Ice ||
                      to == HexOverworldTile.TerrainType.Taiga ||
                      to == HexOverworldTile.TerrainType.Bog ||
                      to == HexOverworldTile.TerrainType.Rocky;

        if (fromHot && toCold) return HexOverworldTile.TerrainType.Plains;
        if (!fromHot && !toCold) return HexOverworldTile.TerrainType.Grassland;

        if (from == HexOverworldTile.TerrainType.Sand) return HexOverworldTile.TerrainType.Grassland;
        if (from == HexOverworldTile.TerrainType.Savanna) return HexOverworldTile.TerrainType.Plains;
        if (from == HexOverworldTile.TerrainType.Jungle) return HexOverworldTile.TerrainType.Forest;

        return HexOverworldTile.TerrainType.Grassland;
    }

    private static int CountSameTerrainNeighbors(
        Vector2I coord, HexOverworldTile.TerrainType terrain,
        Dictionary<Vector2I, HexOverworldTile> allTiles)
    {
        int count = 0;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
            if (allTiles.TryGetValue(nb, out var nbTile) && nbTile.Terrain == terrain)
                count++;
        }
        return count;
    }

    private static float AdjustElevationForTerrain(HexOverworldTile.TerrainType terrain, float currentElev)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.DeepWater => Math.Min(currentElev, 0.25f),
            HexOverworldTile.TerrainType.ShallowWater => Math.Min(currentElev, 0.33f),
            HexOverworldTile.TerrainType.Mountain or HexOverworldTile.TerrainType.MountainSnow
                => Math.Max(currentElev, 0.8f),
            HexOverworldTile.TerrainType.Hills => Math.Max(currentElev, 0.66f),
            _ => currentElev,
        };
    }

    private static HexOverworldTile.TerrainType FindDominantNeighborTerrain(
        List<Vector2I> cluster, Dictionary<Vector2I, HexOverworldTile> allTiles,
        HexOverworldTile.TerrainType excludeType)
    {
        var clusterSet = new HashSet<Vector2I>(cluster);
        var counts = new Dictionary<HexOverworldTile.TerrainType, int>();

        foreach (var pos in cluster)
        {
            for (int d = 0; d < 6; d++)
            {
                var nb = HexOverworldTile.GetNeighbor(pos.X, pos.Y, d);
                if (clusterSet.Contains(nb)) continue;
                if (!allTiles.TryGetValue(nb, out var nbTile)) continue;
                if (nbTile.Terrain == excludeType) continue;

                counts[nbTile.Terrain] = counts.GetValueOrDefault(nbTile.Terrain, 0) + 1;
            }
        }

        if (counts.Count == 0) return HexOverworldTile.TerrainType.Grassland;

        var best = HexOverworldTile.TerrainType.Grassland;
        int bestCount = 0;
        foreach (var kvp in counts)
        {
            if (kvp.Value > bestCount) { bestCount = kvp.Value; best = kvp.Key; }
        }
        return best;
    }
}
