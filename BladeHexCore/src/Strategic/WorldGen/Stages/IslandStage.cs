// IslandStage.cs
// 世界生成阶段 6：在深水区域生成可探索小岛。
//
// 抽取自 WorldCreator.GenerateIslands + IsValidIslandPosition + GenerateIslandShape + GetIslandTerrain。
// RNG：seed ^ 0x49534C44 ("ISLD")，与原实现一致。
// 跨阶段输出：写入 ctx.IslandCenters 供 IslandPOIStage 使用。
using System;
using System.Collections.Generic;
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 6：在深水区域随机生成 1-12 个 5~12 格大小的海岛，
/// 岛屿中心写入 <see cref="WorldBuildContext.IslandCenters"/>。
/// </summary>
public sealed class IslandStage : IWorldStage
{
    public string Name => "生成海岛";
    public float ProgressWeight => 5f;

    public void Execute(WorldBuildContext ctx)
    {
        var rng = ctx.NewRng(0x49534C44); // "ISLD"
        ctx.IslandCenters.Clear();

        // 统计深水格 + 稀疏采样候选中心
        int deepWaterCount = 0;
        var deepWaterTiles = new List<Vector2I>();

        foreach (var chunk in ctx.Chunks.Values)
        {
            foreach (var (coord, tile) in chunk.Tiles)
            {
                if (tile.Terrain == HexOverworldTile.TerrainType.DeepWater)
                {
                    deepWaterCount++;
                    if (deepWaterCount % 50 == 0)
                        deepWaterTiles.Add(coord);
                }
            }
        }

        // 每 10000 格深水生成 1 个海岛，最多 12 个
        int targetIslands = Math.Max(1, deepWaterCount / 10000);
        targetIslands = Math.Min(targetIslands, 12);

        if (deepWaterTiles.Count == 0)
        {
            GD.Print("[IslandStage] 0 个海岛（无深水）");
            return;
        }

        int islandsPlaced = 0;
        var usedPositions = new HashSet<Vector2I>();

        for (int attempt = 0; attempt < targetIslands * 5 && islandsPlaced < targetIslands; attempt++)
        {
            var center = deepWaterTiles[rng.Next(deepWaterTiles.Count)];

            if (!IsValidIslandPosition(center, ctx.Chunks, usedPositions)) continue;

            int islandSize = 5 + rng.Next(8);
            var islandTiles = GenerateIslandShape(center, islandSize, rng);

            var islandTerrain = GetIslandTerrain(rng);
            foreach (var coord in islandTiles)
            {
                var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
                if (!ctx.Chunks.TryGetValue(chunkCoord, out var chunk)) continue;
                var tile = chunk.GetTile(coord.X, coord.Y);
                if (tile == null) continue;

                tile.SetTerrain(islandTerrain);
                tile.IsPassable = true;
                tile.MoveCost = 1.0f;
                usedPositions.Add(coord);
            }

            ctx.IslandCenters.Add(center);
            islandsPlaced++;
        }

        GD.Print($"[IslandStage] {islandsPlaced} 个海岛生成");
    }

    private static bool IsValidIslandPosition(
        Vector2I center,
        Dictionary<Vector2I, ChunkData> chunks,
        HashSet<Vector2I> usedPositions)
    {
        // 距离已有岛屿至少 15 格
        foreach (var used in usedPositions)
        {
            if (HexOverworldTile.HexDistance(center.X, center.Y, used.X, used.Y) < 15)
                return false;
        }

        // 距离大陆至少 6 格
        var centerCube = HexOverworldTile.AxialToCube(center.X, center.Y);
        for (int ring = 1; ring <= 6; ring++)
        {
            var ringTiles = HexOverworldTile.CubeRing(centerCube, ring);
            foreach (var cube in ringTiles)
            {
                var axial = HexOverworldTile.CubeToAxial(cube);
                var chunkCoord = ChunkData.WorldToChunk(axial.X, axial.Y);
                if (!chunks.TryGetValue(chunkCoord, out var chunk)) continue;
                var tile = chunk.GetTile(axial.X, axial.Y);
                if (tile != null && tile.Terrain != HexOverworldTile.TerrainType.DeepWater &&
                    tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                    return false;
            }
        }

        return true;
    }

    private static List<Vector2I> GenerateIslandShape(Vector2I center, int size, Random rng)
    {
        var island = new List<Vector2I> { center };
        var frontier = new List<Vector2I>();

        for (int d = 0; d < 6; d++)
            frontier.Add(HexOverworldTile.GetNeighbor(center.X, center.Y, d));

        var used = new HashSet<Vector2I> { center };

        while (island.Count < size && frontier.Count > 0)
        {
            int idx = rng.Next(frontier.Count);
            var next = frontier[idx];
            frontier.RemoveAt(idx);

            if (used.Contains(next)) continue;
            used.Add(next);
            island.Add(next);

            for (int d = 0; d < 6; d++)
            {
                var nb = HexOverworldTile.GetNeighbor(next.X, next.Y, d);
                if (!used.Contains(nb) && rng.NextDouble() < 0.6)
                    frontier.Add(nb);
            }
        }

        return island;
    }

    private static HexOverworldTile.TerrainType GetIslandTerrain(Random rng)
    {
        return rng.Next(4) switch
        {
            0 => HexOverworldTile.TerrainType.Sand,
            1 => HexOverworldTile.TerrainType.Plains,
            2 => HexOverworldTile.TerrainType.Forest,
            3 => HexOverworldTile.TerrainType.Rocky,
            _ => HexOverworldTile.TerrainType.Sand,
        };
    }
}
