// TerrainGenerationTest.cs
// 地形生成统计测试 — 输出地形分布、碎片化指数、最大连通区域等
// 用于校正噪声参数和阈值使地形接近真实
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using Godot;

namespace BladeHex.Tests;

/// <summary>
/// 地形生成质量分析器 — 无需 GUI，纯逻辑测试
/// </summary>
public static class TerrainGenerationTest
{
    /// <summary>
    /// 运行完整地形生成分析，输出统计结果
    /// </summary>
    public static string RunAnalysis(int seed = 12345, int chunksW = 21, int chunksH = 12)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== 地形生成分析 (seed={seed}, {chunksW}×{chunksH} chunks = {chunksW * 16}×{chunksH * 16} tiles) ===");
        sb.AppendLine();

        // 生成地形
        var generator = new ChunkGenerator();
        int worldW = chunksW * ChunkData.ChunkSize;
        int worldH = chunksH * ChunkData.ChunkSize;
        generator.Initialize(seed, worldW, worldH);

        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        for (int cq = 0; cq < chunksW; cq++)
        {
            for (int cr = 0; cr < chunksH; cr++)
            {
                var chunk = generator.Generate(cq, cr);
                foreach (var kvp in chunk.Tiles)
                    allTiles[kvp.Key] = kvp.Value;
            }
        }

        int totalTiles = allTiles.Count;
        sb.AppendLine($"总瓦片数: {totalTiles}");
        sb.AppendLine();

        // 1. 地形类型分布
        sb.AppendLine("--- 1. 地形类型分布 ---");
        var terrainCounts = new Dictionary<HexOverworldTile.TerrainType, int>();
        foreach (var tile in allTiles.Values)
        {
            if (!terrainCounts.ContainsKey(tile.Terrain))
                terrainCounts[tile.Terrain] = 0;
            terrainCounts[tile.Terrain]++;
        }

        var sorted = terrainCounts.OrderByDescending(kv => kv.Value);
        foreach (var kv in sorted)
        {
            float pct = 100.0f * kv.Value / totalTiles;
            sb.AppendLine($"  {kv.Key,-16} {kv.Value,7} ({pct:F1}%)");
        }

        // 2. 宏观生态分布
        sb.AppendLine();
        sb.AppendLine("--- 2. 宏观生态(BiomeType)分布 ---");
        var biomeCounts = new Dictionary<BiomeType, int>();
        foreach (var tile in allTiles.Values)
        {
            var biome = TerrainToBiome.Map(tile.Terrain);
            if (!biomeCounts.ContainsKey(biome))
                biomeCounts[biome] = 0;
            biomeCounts[biome]++;
        }
        foreach (var kv in biomeCounts.OrderByDescending(kv => kv.Value))
        {
            float pct = 100.0f * kv.Value / totalTiles;
            sb.AppendLine($"  {kv.Key,-12} {kv.Value,7} ({pct:F1}%)");
        }

        // 3. 碎片化指数 — 计算每种陆地地形的平均连通区域大小
        sb.AppendLine();
        sb.AppendLine("--- 3. 碎片化分析（连通区域统计） ---");
        var fragmentStats = ComputeFragmentation(allTiles);
        sb.AppendLine($"  陆地连通区域总数: {fragmentStats.TotalClusters}");
        sb.AppendLine($"  最大连通区域: {fragmentStats.MaxClusterSize} tiles");
        sb.AppendLine($"  平均连通区域: {fragmentStats.AvgClusterSize:F1} tiles");
        sb.AppendLine($"  中位数连通区域: {fragmentStats.MedianClusterSize} tiles");
        sb.AppendLine($"  <10 tile 碎片数: {fragmentStats.TinyFragments} ({100f * fragmentStats.TinyFragments / Math.Max(fragmentStats.TotalClusters, 1):F1}%)");
        sb.AppendLine($"  <50 tile 碎片数: {fragmentStats.SmallFragments} ({100f * fragmentStats.SmallFragments / Math.Max(fragmentStats.TotalClusters, 1):F1}%)");

        // 4. 按地形类型的碎片化
        sb.AppendLine();
        sb.AppendLine("--- 4. 各地形类型碎片化 ---");
        var perTerrainFrags = ComputePerTerrainFragmentation(allTiles);
        sb.AppendLine($"  {"Terrain",-16} {"Clusters",8} {"AvgSize",8} {"MaxSize",8} {"<10",6}");
        foreach (var kv in perTerrainFrags.OrderByDescending(kv => terrainCounts.GetValueOrDefault(kv.Key, 0)))
        {
            var f = kv.Value;
            if (f.TotalClusters == 0) continue;
            sb.AppendLine($"  {kv.Key,-16} {f.TotalClusters,8} {f.AvgClusterSize,8:F0} {f.MaxClusterSize,8} {f.TinyFragments,6}");
        }

        // 5. 纬度带温度/湿度分布
        sb.AppendLine();
        sb.AppendLine("--- 5. 纬度带分布 ---");
        int bands = 6;
        int bandHeight = worldH / bands;
        for (int b = 0; b < bands; b++)
        {
            int rStart = b * bandHeight;
            int rEnd = (b + 1) * bandHeight;
            float avgTemp = 0, avgMoist = 0, avgElev = 0;
            int count = 0;
            var bandTerrains = new Dictionary<HexOverworldTile.TerrainType, int>();

            foreach (var (coord, tile) in allTiles)
            {
                if (coord.Y >= rStart && coord.Y < rEnd)
                {
                    avgTemp += tile.Temperature;
                    avgMoist += tile.Moisture;
                    avgElev += tile.Elevation;
                    count++;
                    if (!bandTerrains.ContainsKey(tile.Terrain))
                        bandTerrains[tile.Terrain] = 0;
                    bandTerrains[tile.Terrain]++;
                }
            }
            if (count > 0)
            {
                avgTemp /= count;
                avgMoist /= count;
                avgElev /= count;
            }

            // Top 3 terrains in this band
            var top3 = bandTerrains.OrderByDescending(kv => kv.Value).Take(3);
            string top3Str = string.Join(", ", top3.Select(kv => $"{kv.Key}({100f * kv.Value / count:F0}%)"));

            sb.AppendLine($"  Band {b} (r={rStart}-{rEnd}): temp={avgTemp:F2} moist={avgMoist:F2} elev={avgElev:F2} | {top3Str}");
        }

        sb.AppendLine();
        sb.AppendLine("=== 分析完成 ===");
        return sb.ToString();
    }

    // ========================================
    // 碎片化计算
    // ========================================

    public struct FragmentationStats
    {
        public int TotalClusters;
        public int MaxClusterSize;
        public float AvgClusterSize;
        public int MedianClusterSize;
        public int TinyFragments;  // < 10
        public int SmallFragments; // < 50
    }

    private static FragmentationStats ComputeFragmentation(Dictionary<Vector2I, HexOverworldTile> tiles)
    {
        var visited = new HashSet<Vector2I>();
        var clusterSizes = new List<int>();

        foreach (var (coord, tile) in tiles)
        {
            if (visited.Contains(coord)) continue;
            if (!TerrainToBiome.IsLandTerrain(tile.Terrain))
            {
                visited.Add(coord);
                continue;
            }

            // BFS same-terrain cluster
            int size = BfsCluster(coord, tile.Terrain, tiles, visited);
            clusterSizes.Add(size);
        }

        if (clusterSizes.Count == 0)
            return new FragmentationStats();

        clusterSizes.Sort();
        return new FragmentationStats
        {
            TotalClusters = clusterSizes.Count,
            MaxClusterSize = clusterSizes[^1],
            AvgClusterSize = (float)clusterSizes.Sum() / clusterSizes.Count,
            MedianClusterSize = clusterSizes[clusterSizes.Count / 2],
            TinyFragments = clusterSizes.Count(s => s < 10),
            SmallFragments = clusterSizes.Count(s => s < 50),
        };
    }

    private static Dictionary<HexOverworldTile.TerrainType, FragmentationStats> ComputePerTerrainFragmentation(
        Dictionary<Vector2I, HexOverworldTile> tiles)
    {
        var result = new Dictionary<HexOverworldTile.TerrainType, FragmentationStats>();
        var visited = new HashSet<Vector2I>();

        // Group clusters by terrain type
        var clustersByTerrain = new Dictionary<HexOverworldTile.TerrainType, List<int>>();

        foreach (var (coord, tile) in tiles)
        {
            if (visited.Contains(coord)) continue;
            if (!TerrainToBiome.IsLandTerrain(tile.Terrain))
            {
                visited.Add(coord);
                continue;
            }

            int size = BfsCluster(coord, tile.Terrain, tiles, visited);
            if (!clustersByTerrain.ContainsKey(tile.Terrain))
                clustersByTerrain[tile.Terrain] = new List<int>();
            clustersByTerrain[tile.Terrain].Add(size);
        }

        foreach (var (terrain, sizes) in clustersByTerrain)
        {
            sizes.Sort();
            result[terrain] = new FragmentationStats
            {
                TotalClusters = sizes.Count,
                MaxClusterSize = sizes[^1],
                AvgClusterSize = (float)sizes.Sum() / sizes.Count,
                MedianClusterSize = sizes[sizes.Count / 2],
                TinyFragments = sizes.Count(s => s < 10),
                SmallFragments = sizes.Count(s => s < 50),
            };
        }

        return result;
    }

    private static int BfsCluster(Vector2I start, HexOverworldTile.TerrainType targetTerrain,
        Dictionary<Vector2I, HexOverworldTile> tiles, HashSet<Vector2I> visited)
    {
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        visited.Add(start);
        int size = 0;

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            size++;

            for (int d = 0; d < 6; d++)
            {
                var nb = HexOverworldTile.GetNeighbor(cur.X, cur.Y, d);
                if (visited.Contains(nb)) continue;
                if (!tiles.TryGetValue(nb, out var nbTile)) continue;
                if (nbTile.Terrain != targetTerrain) continue;

                visited.Add(nb);
                queue.Enqueue(nb);
            }
        }

        return size;
    }
}
