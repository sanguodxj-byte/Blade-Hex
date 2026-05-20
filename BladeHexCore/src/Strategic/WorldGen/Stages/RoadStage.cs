// RoadStage.cs
// 世界生成阶段 10：在聚落（城镇/村庄/城堡/前哨/旅店/港口）之间用 A* 连接道路。
//
// 道路寻路使用与玩家相同的 tile.MoveCost 成本模型（HexOverworldAStar 兼容），
// 确保生成的道路路径与玩家实际行走路径一致。
// 唯一差异：道路允许穿越浅水/河流（建桥），玩家不允许。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic.WorldGen.Internal;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 10：用 Prim MST 选出聚落连接边，再对每条边做 tile 级 A* 寻路，将路径瓦片标记为道路。
/// 寻路成本与玩家 agent（HexOverworldAStar / ChunkAStar）一致，使用 tile.MoveCost。
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

        // 构建临时 Grid 供 HexOverworldAStar 使用
        var grid = new HexOverworldGrid();
        foreach (var chunk in ctx.Chunks.Values)
            foreach (var kvp in chunk.Tiles)
                grid.Tiles[kvp.Key] = kvp.Value;

        var aStar = new HexOverworldAStar(grid)
        {
            // 允许穿越不可通行地形（浅水/河流建桥），但惩罚高
            IgnorePassability = true,
            // 惩罚设为极高值：确保 A* 永远不会选择穿越深水/山脉的路径
            // （除非完全无路可走，此时后置过滤会丢弃该路径）
            ImpassablePenalty = 999.0f,
            // 允许穿越浅水（建桥）
            AllowShallowWater = true,
            // 道路偏好：复用已有道路
            RoadPreference = 0.3f,
        };

        int roadsStamped = 0;
        foreach (var (from, to) in edges)
        {
            var fromAxial = HexOverworldTile.PixelToAxial(from.Position.X, from.Position.Y);
            var toAxial = HexOverworldTile.PixelToAxial(to.Position.X, to.Position.Y);

            var path = FindRoadPath(aStar, fromAxial, toAxial, grid);
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

    /// <summary>
    /// 使用与玩家相同的 HexOverworldAStar 寻路，附加直线偏好 tie-breaking。
    /// 深水和山脉仍然完全阻断（不建道路穿越）。
    /// </summary>
    private static List<Vector2I> FindRoadPath(
        HexOverworldAStar aStar, Vector2I start, Vector2I end, HexOverworldGrid grid)
    {
        if (start == end) return new List<Vector2I> { start };
        if (!grid.HasTile(start.X, start.Y) || !grid.HasTile(end.X, end.Y))
            return new List<Vector2I>();

        // 使用 HexOverworldAStar.FindPath — 与玩家完全相同的成本模型
        // 但 IgnorePassability=true 允许穿越浅水/河流（高惩罚）
        var pathArray = aStar.FindPath(start, end);

        if (pathArray.Length < 2) return new List<Vector2I>();

        // 过滤：如果路径穿越了深水或山脉，放弃（这些地形不应建道路）
        var path = new List<Vector2I>(pathArray.Length);
        foreach (var coord in pathArray)
        {
            var tile = grid.GetTile(coord.X, coord.Y);
            if (tile == null) return new List<Vector2I>();
            if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater
                || tile.Terrain == HexOverworldTile.TerrainType.Mountain
                || tile.Terrain == HexOverworldTile.TerrainType.MountainSnow)
                return new List<Vector2I>(); // 路径不可行
            path.Add(coord);
        }

        return path;
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
            // 重新评估 IsPassable / MoveCost：道路覆盖在水/河流上变成桥
            tile.UpdateTerrainProperties();

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
