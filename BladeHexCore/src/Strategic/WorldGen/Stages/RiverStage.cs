// RiverStage.cs
// 世界生成阶段 5：从高地源头沿下坡寻路生成河流。
//
// 抽取自 WorldCreator.GenerateRiversDirect + RiverDownhillAStar + StampRiverDirect + StampRiverTile。
// RNG：seed ^ 0x52495645 ("RIVE")，与原实现完全一致。
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic.WorldGen.Internal;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 5：在地形上印章 3-6 条河流，沿下坡 A* 路径流入海岸。
/// </summary>
public sealed class RiverStage : IWorldStage
{
    public string Name => "生成河流";
    public float ProgressWeight => 8f;

    public void Execute(WorldBuildContext ctx)
    {
        int placed = GenerateRiversDirect(ctx);
        GD.Print($"[RiverStage] {placed} 条河流生成");
    }

    private static int GenerateRiversDirect(WorldBuildContext ctx)
    {
        var rng = ctx.NewRng(0x52495645); // "RIVE"
        int riverCount = 3 + rng.Next(4); // 3-6 条河流

        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in ctx.Chunks.Values)
            foreach (var kvp in chunk.Tiles)
                allTiles[kvp.Key] = kvp.Value;

        var highTiles = new List<Vector2I>();
        var coastTiles = new List<Vector2I>();

        foreach (var (coord, tile) in allTiles)
        {
            if (tile.Elevation > 0.55f && tile.Elevation < 0.75f && tile.IsPassable &&
                tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                highTiles.Add(coord);

            if (tile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                coastTiles.Add(coord);
        }

        if (highTiles.Count == 0 || coastTiles.Count == 0) return 0;

        // 洗牌源头（与原 WorldCreator 顺序一致）
        for (int i = highTiles.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (highTiles[i], highTiles[j]) = (highTiles[j], highTiles[i]);
        }

        int riversPlaced = 0;
        var allRiverTiles = new HashSet<Vector2I>();

        for (int attempt = 0; attempt < riverCount * 5 && riversPlaced < riverCount; attempt++)
        {
            if (attempt >= highTiles.Count) break;
            var source = highTiles[attempt];

            // 找最近海岸
            Vector2I bestCoast = coastTiles[0];
            int bestDist = int.MaxValue;
            foreach (var coast in coastTiles)
            {
                int d = HexOverworldTile.HexDistance(source.X, source.Y, coast.X, coast.Y);
                if (d < bestDist) { bestDist = d; bestCoast = coast; }
            }

            if (bestDist < 15) continue;

            var path = RiverDownhillAStar(source, bestCoast, allTiles, allRiverTiles);
            if (path.Count < 15) continue;

            StampRiverDirect(path, ctx.Chunks, allRiverTiles);
            riversPlaced++;
        }

        return riversPlaced;
    }

    private static List<Vector2I> RiverDownhillAStar(
        Vector2I start, Vector2I target,
        Dictionary<Vector2I, HexOverworldTile> allTiles,
        HashSet<Vector2I> existingRivers)
    {
        if (!allTiles.ContainsKey(start) || !allTiles.ContainsKey(target))
            return new List<Vector2I>();

        var openQueue = new PriorityQueue<Vector2I, float>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var closed = new HashSet<Vector2I>();

        float heuristic = HexOverworldTile.HexDistance(start.X, start.Y, target.X, target.Y);
        openQueue.Enqueue(start, heuristic);

        int maxIter = 100000;
        int iter = 0;

        while (openQueue.Count > 0 && iter < maxIter)
        {
            iter++;
            var current = openQueue.Dequeue();
            if (closed.Contains(current)) continue;
            closed.Add(current);

            if (current == target)
            {
                var path = new List<Vector2I>();
                var node = target;
                while (node != start)
                {
                    path.Add(node);
                    node = cameFrom[node];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            float currentElev = allTiles[current].Elevation;
            float currentG = gScore[current];

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (closed.Contains(neighbor)) continue;
                if (!allTiles.TryGetValue(neighbor, out var nTile)) continue;

                if (nTile.Terrain == HexOverworldTile.TerrainType.DeepWater) continue;
                if (nTile.Terrain == HexOverworldTile.TerrainType.Mountain ||
                    nTile.Terrain == HexOverworldTile.TerrainType.MountainSnow) continue;
                if (existingRivers.Contains(neighbor)) continue;

                float neighborElev = nTile.Elevation;
                float elevDiff = neighborElev - currentElev;

                float cost;
                if (elevDiff <= 0)
                    cost = 1.0f + neighborElev * 2.0f;
                else
                    cost = 1.0f + elevDiff * 50.0f;

                if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater)
                    cost = 0.5f;

                float tentativeG = currentG + cost;
                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    float h = HexOverworldTile.HexDistance(neighbor.X, neighbor.Y, target.X, target.Y) * 0.5f;
                    openQueue.Enqueue(neighbor, tentativeG + h);
                }
            }
        }

        return new List<Vector2I>();
    }

    private static void StampRiverDirect(
        List<Vector2I> path,
        Dictionary<Vector2I, ChunkData> chunks,
        HashSet<Vector2I> allRiverTiles)
    {
        int totalLen = path.Count;

        for (int i = 0; i < totalLen; i++)
        {
            var coord = path[i];
            var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(coord.X, coord.Y);
            if (tile == null) continue;

            if (tile.IsRoad) continue;

            tile.IsRiver = true;
            tile.SetTerrain(HexOverworldTile.TerrainType.River);
            allRiverTiles.Add(coord);

            if (i > 0)
            {
                int dirFrom = HexDirectionHelpers.GetRoadDirection(path[i - 1], coord);
                if (dirFrom >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirFrom);
            }
            if (i < totalLen - 1)
            {
                int dirTo = HexDirectionHelpers.GetRoadDirection(coord, path[i + 1]);
                if (dirTo >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirTo);
            }

            float progress = (float)i / totalLen;
            int width = progress < 0.4f ? 1 : progress < 0.75f ? 2 : 3;

            if (width >= 2 && i < totalLen - 1)
            {
                int flowDir = HexDirectionHelpers.GetRoadDirection(coord, path[i + 1]);
                if (flowDir < 0) flowDir = 0;
                int perpDir1 = (flowDir + 2) % 6;
                int perpDir2 = (flowDir + 4) % 6;

                var side1 = HexOverworldTile.GetNeighbor(coord.X, coord.Y, perpDir1);
                StampRiverTile(side1, chunks, allRiverTiles);

                if (width >= 3)
                {
                    var side2 = HexOverworldTile.GetNeighbor(coord.X, coord.Y, perpDir2);
                    StampRiverTile(side2, chunks, allRiverTiles);
                }
            }
        }
    }

    private static void StampRiverTile(Vector2I coord, Dictionary<Vector2I, ChunkData> chunks, HashSet<Vector2I> allRiverTiles)
    {
        var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
        if (!chunks.TryGetValue(chunkCoord, out var chunk)) return;
        var tile = chunk.GetTile(coord.X, coord.Y);
        if (tile == null || tile.IsRoad || tile.IsRiver) return;
        if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater) return;

        // 河流加宽：只改地形为 ShallowWater，不设置 IsRiver。
        // 这样 RiverRenderer 只追踪主流，不会把 expansion 当成分叉，
        // 避免一条河被切成几十个 1-2 格的渲染段。
        tile.SetTerrain(HexOverworldTile.TerrainType.ShallowWater);
        // 不再 allRiverTiles.Add(coord);
    }
}
