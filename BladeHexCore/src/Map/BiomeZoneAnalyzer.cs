// BiomeZoneAnalyzer.cs
// 生态区聚类器 — 对全部 chunk 执行 flood-fill，识别连通的同类生态区域
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 生态区聚类器 — 从已生成的 chunk 数据中识别出所有生态区
/// 算法：对每个未访问的陆地 tile 执行 BFS flood-fill，
/// 将相邻且同 BiomeType 的 tile 归入同一个 BiomeZone
/// </summary>
public class BiomeZoneAnalyzer
{
    /// <summary>最小生态区面积（tile 数），低于此值的区域不计为独立生态区</summary>
    public int MinZoneSize { get; set; } = 200;

    /// <summary>
    /// 对所有 chunk 执行 flood-fill 聚类，识别出所有生态区
    /// </summary>
    /// <param name="allChunks">全部已生成的 chunk（chunkCoord → ChunkData）</param>
    /// <returns>所有满足最小面积的生态区列表</returns>
    public List<BiomeZone> Analyze(Dictionary<Vector2I, ChunkData> allChunks)
    {
        var zones = new List<BiomeZone>();
        var visited = new HashSet<Vector2I>();
        int nextId = 0;

        // 构建全局 tile 查找表（coord → tile）用于 BFS 邻居查询
        var tileLookup = BuildTileLookup(allChunks);

        foreach (var (coord, tile) in tileLookup)
        {
            if (visited.Contains(coord)) continue;
            if (!TerrainToBiome.IsLandTerrain(tile.Terrain))
            {
                visited.Add(coord);
                continue;
            }

            var biome = TerrainToBiome.Map(tile.Terrain);
            var zone = FloodFill(coord, biome, tileLookup, visited);

            if (zone.TileCount >= MinZoneSize)
            {
                zone.Id = nextId++;
                zone.ComputeCentroid();
                zones.Add(zone);
            }
        }

        GD.Print($"[BiomeZoneAnalyzer] 识别出 {zones.Count} 个生态区 (最小面积={MinZoneSize})");
        return zones;
    }

    /// <summary>
    /// BFS flood-fill — 从起点扩展，收集所有相邻且同 BiomeType 的 tile
    /// </summary>
    private BiomeZone FloodFill(
        Vector2I start,
        BiomeType targetBiome,
        Dictionary<Vector2I, HexOverworldTile> tileLookup,
        HashSet<Vector2I> visited)
    {
        var zone = new BiomeZone { DominantBiome = targetBiome };
        var queue = new Queue<Vector2I>();

        queue.Enqueue(start);
        visited.Add(start);

        float sumElev = 0, sumTemp = 0, sumMoist = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            zone.TileCoords.Add(current);

            if (tileLookup.TryGetValue(current, out var tile))
            {
                sumElev += tile.Elevation;
                sumTemp += tile.Temperature;
                sumMoist += tile.Moisture;
            }

            // 遍历 6 个六边形邻居
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);

                if (visited.Contains(neighbor)) continue;
                if (!tileLookup.TryGetValue(neighbor, out var nTile)) continue;
                if (!TerrainToBiome.IsLandTerrain(nTile.Terrain)) { visited.Add(neighbor); continue; }

                var nBiome = TerrainToBiome.Map(nTile.Terrain);
                if (nBiome != targetBiome) continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        // 计算平均值
        int count = zone.TileCount;
        if (count > 0)
        {
            zone.AverageElevation = sumElev / count;
            zone.AverageTemperature = sumTemp / count;
            zone.AverageMoisture = sumMoist / count;
        }

        return zone;
    }

    /// <summary>
    /// 构建全局 tile 查找表（从所有 chunk 中提取）
    /// </summary>
    private static Dictionary<Vector2I, HexOverworldTile> BuildTileLookup(
        Dictionary<Vector2I, ChunkData> allChunks)
    {
        var lookup = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in allChunks.Values)
        {
            foreach (var (coord, tile) in chunk.Tiles)
            {
                lookup[coord] = tile;
            }
        }
        return lookup;
    }
}
