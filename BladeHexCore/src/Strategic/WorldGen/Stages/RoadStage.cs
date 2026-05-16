// RoadStage.cs
// 世界生成阶段 10：在聚落（城镇/村庄/城堡/前哨/旅店/港口）之间用 A* 连接道路。
//
// 抽取自 WorldCreator.ConnectSettlementRoads + BuildNearestNeighborRoads + RoadAStar +
//   GetRoadBuildCost + HeuristicDist + StampRoadPath。
// RNG：原 BuildNearestNeighborRoads 接收 seed 但内部未使用（纯 Prim MST），保留 seed 参数仅为兼容。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic.WorldGen.Internal;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 10：用 Prim MST 选出聚落连接边，再对每条边做 tile 级 A* 寻路，将路径瓦片标记为道路。
/// </summary>
public sealed class RoadStage : IWorldStage
{
    public string Name => "连接聚落道路";
    public float ProgressWeight => 4f;

    public void Execute(WorldBuildContext ctx)
    {
        var settlements = ctx.Pois
            .Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Town
                     || p.PoiTypeEnum == OverworldPOI.POIType.Village
                     || p.PoiTypeEnum == OverworldPOI.POIType.Castle
                     || p.PoiTypeEnum == OverworldPOI.POIType.Outpost
                     || p.PoiTypeEnum == OverworldPOI.POIType.Tavern
                     || p.PoiTypeEnum == OverworldPOI.POIType.Port)
            .ToList();

        if (settlements.Count < 2)
        {
            GD.Print($"[RoadStage] 0 条道路（聚落数量 {settlements.Count}）");
            return;
        }

        var edges = BuildNearestNeighborRoads(settlements);

        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in ctx.Chunks.Values)
            foreach (var kvp in chunk.Tiles)
                allTiles[kvp.Key] = kvp.Value;

        int roadsStamped = 0;
        foreach (var (from, to) in edges)
        {
            var fromAxial = HexOverworldTile.PixelToAxial(from.Position.X, from.Position.Y);
            var toAxial = HexOverworldTile.PixelToAxial(to.Position.X, to.Position.Y);

            var path = RoadAStar(fromAxial, toAxial, allTiles);
            if (path.Count >= 2)
            {
                StampRoadPath(path, ctx.Chunks);
                roadsStamped++;
            }
        }

        GD.Print($"[RoadStage] {roadsStamped}/{edges.Count} 条连接 {settlements.Count} 个聚落");
    }

    /// <summary>Prim MST — 保证连通且不产生三角形。</summary>
    private static List<(OverworldPOI, OverworldPOI)> BuildNearestNeighborRoads(List<OverworldPOI> settlements)
    {
        var edges = new List<(OverworldPOI, OverworldPOI)>();
        if (settlements.Count < 2) return edges;

        var inTree = new HashSet<int> { 0 };
        var candidates = new HashSet<int>();
        for (int i = 1; i < settlements.Count; i++) candidates.Add(i);

        while (candidates.Count > 0)
        {
            float bestDist = float.MaxValue;
            int bestFrom = -1, bestTo = -1;

            foreach (int from in inTree)
            {
                foreach (int to in candidates)
                {
                    float d = settlements[from].Position.DistanceTo(settlements[to].Position);
                    if (d < bestDist) { bestDist = d; bestFrom = from; bestTo = to; }
                }
            }

            if (bestTo < 0) break;
            edges.Add((settlements[bestFrom], settlements[bestTo]));
            inTree.Add(bestTo);
            candidates.Remove(bestTo);
        }

        return edges;
    }

    private static List<Vector2I> RoadAStar(
        Vector2I start, Vector2I end,
        Dictionary<Vector2I, HexOverworldTile> allTiles)
    {
        if (start == end) return new List<Vector2I> { start };

        if (!allTiles.ContainsKey(start) || !allTiles.ContainsKey(end))
            return new List<Vector2I>();

        var openQueue = new PriorityQueue<Vector2I, float>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0 };
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var closed = new HashSet<Vector2I>();

        openQueue.Enqueue(start, HeuristicDist(start, end));

        int maxIter = 50000;
        int iter = 0;

        while (openQueue.Count > 0 && iter < maxIter)
        {
            iter++;
            var current = openQueue.Dequeue();
            if (closed.Contains(current)) continue;
            closed.Add(current);

            if (current == end)
            {
                var path = new List<Vector2I>();
                var node = end;
                while (node != start)
                {
                    path.Add(node);
                    node = cameFrom[node];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            float currentG = gScore[current];

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (closed.Contains(neighbor)) continue;
                if (!allTiles.TryGetValue(neighbor, out var nTile)) continue;
                if (!nTile.IsPassable) continue;
                if (nTile.Terrain == HexOverworldTile.TerrainType.ShallowWater ||
                    nTile.Terrain == HexOverworldTile.TerrainType.DeepWater) continue;

                float moveCost = GetRoadBuildCost(nTile);
                float tentativeG = currentG + moveCost;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    float f = tentativeG + HeuristicDist(neighbor, end);
                    openQueue.Enqueue(neighbor, f);
                }
            }
        }

        return new List<Vector2I>();
    }

    private static float GetRoadBuildCost(HexOverworldTile tile)
    {
        if (tile.IsRoad) return 1.0f;
        return tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Road => 1.0f,
            HexOverworldTile.TerrainType.Plains => 1.0f,
            HexOverworldTile.TerrainType.Grassland => 1.0f,
            HexOverworldTile.TerrainType.Savanna => 1.2f,
            HexOverworldTile.TerrainType.Wasteland => 1.5f,
            HexOverworldTile.TerrainType.Sand => 2.0f,
            HexOverworldTile.TerrainType.Taiga => 2.5f,
            HexOverworldTile.TerrainType.Snow => 3.0f,
            HexOverworldTile.TerrainType.Forest => 4.0f,
            HexOverworldTile.TerrainType.Hills => 4.0f,
            HexOverworldTile.TerrainType.Rocky => 5.0f,
            HexOverworldTile.TerrainType.DenseForest => 6.0f,
            HexOverworldTile.TerrainType.Jungle => 7.0f,
            HexOverworldTile.TerrainType.Swamp or HexOverworldTile.TerrainType.Bog => 8.0f,
            HexOverworldTile.TerrainType.ShallowWater => 15.0f,
            HexOverworldTile.TerrainType.Ice => 10.0f,
            _ => 3.0f,
        };
    }

    private static float HeuristicDist(Vector2I a, Vector2I b)
    {
        int dq = Math.Abs(a.X - b.X);
        int dr = Math.Abs(a.Y - b.Y);
        int ds = Math.Abs((-a.X - a.Y) - (-b.X - b.Y));
        return (dq + dr + ds) / 2.0f;
    }

    private static void StampRoadPath(List<Vector2I> path, Dictionary<Vector2I, ChunkData> chunks)
    {
        for (int i = 0; i < path.Count; i++)
        {
            var coord = path[i];
            var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
            if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;

            var tile = chunk.GetTile(coord.X, coord.Y);
            if (tile == null) continue;

            tile.IsRoad = true;
            tile.MoveCost = 0.2f;

            if (i > 0)
            {
                int dirFrom = HexDirectionHelpers.GetRoadDirection(path[i - 1], coord);
                if (dirFrom >= 0) tile.RoadDirections = HexDirectionHelpers.SetBit(tile.RoadDirections, dirFrom);
            }
            if (i < path.Count - 1)
            {
                int dirTo = HexDirectionHelpers.GetRoadDirection(coord, path[i + 1]);
                if (dirTo >= 0) tile.RoadDirections = HexDirectionHelpers.SetBit(tile.RoadDirections, dirTo);
            }
        }
    }
}
