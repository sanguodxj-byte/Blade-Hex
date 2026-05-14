// RiverRoadStamper.cs
// 河流印章器 — 将全局河流骨架标记到 ChunkData 瓦片上
// 道路已移除：由 WorldCreator.ConnectSettlementRoads（纯 MST）直接生成
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 河流印章器 — 负责将全局河流骨架路径标记到 chunk 瓦片
/// 处理地形覆盖、方向位掩码
/// 道路不再由骨架生成，改为 WorldCreator.ConnectSettlementRoads 纯 MST 直接在 tiles 上生成
/// </summary>
[GlobalClass]
public partial class RiverRoadStamper : RefCounted
{
    // ========================================
    // 依赖
    // ========================================

    /// <summary>世界种子（用于确定性随机扩散）</summary>
    public int WorldSeed { get; set; } = 0;

    /// <summary>全局河流骨架数据</summary>
    public Map.RiverRoadSkeleton? Skeleton { get; set; }

    // ========================================
    // Chunk 查询
    // ========================================

    /// <summary>
    /// 获取指定 chunk 内的河流路径段
    /// 返回: 该 chunk 范围内的河流坐标集合（按路径分组）
    /// </summary>
    public List<Vector2I[]> GetRiverPathsForChunk(int chunkQ, int chunkR)
    {
        if (Skeleton == null) return new List<Vector2I[]>();

        var origin = Map.ChunkData.ChunkToWorld(chunkQ, chunkR);
        int endQ = origin.X + Map.ChunkData.ChunkSize;
        int endR = origin.Y + Map.ChunkData.ChunkSize;
        var result = new List<Vector2I[]>();

        foreach (var river in Skeleton.RiverPaths)
        {
            var segment = new List<Vector2I>();
            foreach (var coord in river)
            {
                if (coord.X >= origin.X && coord.X < endQ && coord.Y >= origin.Y && coord.Y < endR)
                    segment.Add(coord);
            }
            if (segment.Count > 0)
                result.Add(segment.ToArray());
        }

        return result;
    }

    /// <summary>
    /// [已废弃] 获取指定 chunk 内的道路路径段 — 始终返回空列表。
    /// 道路由 ConnectSettlementRoads 直接生成在 tiles 上，不再存储在骨架中。
    /// </summary>
    [Obsolete("道路由 ConnectSettlementRoads 纯 MST 生成，不再存储在骨架中")]
    public List<Vector2I[]> GetRoadPathsForChunk(int chunkQ, int chunkR)
    {
        return new List<Vector2I[]>();
    }

    // ========================================
    // 印章（Stamp）
    // ========================================

    /// <summary>
    /// 将河流路径标记到 chunk 的瓦片上（仅河流，道路已移除）
    /// 处理地形覆盖、方向位掩码
    /// </summary>
    public void StampOnChunk(Map.ChunkData chunk)
    {
        if (Skeleton == null) return;

        int chunkQ = chunk.ChunkCoord.X;
        int chunkR = chunk.ChunkCoord.Y;
        var origin = Map.ChunkData.ChunkToWorld(chunkQ, chunkR);
        int endQ = origin.X + Map.ChunkData.ChunkSize;
        int endR = origin.Y + Map.ChunkData.ChunkSize;

        bool IsInChunk(int q, int r) => q >= origin.X && q < endQ && r >= origin.Y && r < endR;

        // 标记河流 — 遍历全局路径，保留完整方向上下文
        foreach (var river in Skeleton.RiverPaths)
        {
            StampGlobalPathAsRiver(chunk, river, IsInChunk);
        }
    }

    /// <summary>
    /// 标记全局河流路径到 chunk 瓦片。
    /// 河道宽度随流程渐进加宽：源头 1 格，中游 2 格，下游（最后 30%）3 格。
    /// 加宽方向基于河流流向的垂直方向，确保河道形状自然。
    /// </summary>
    private void StampGlobalPathAsRiver(Map.ChunkData chunk, Vector2I[] path, Func<int, int, bool> isInChunk)
    {
        int totalLen = path.Length;

        for (int i = 0; i < totalLen; i++)
        {
            var coord = path[i];
            if (!isInChunk(coord.X, coord.Y)) continue;

            var tile = chunk.GetTile(coord.X, coord.Y);
            if (tile == null) continue;

            tile.IsRiver = true;
            tile.SetTerrain(Map.HexOverworldTile.TerrainType.River);

            // 方向位掩码
            if (i > 0)
            {
                int dirFrom = GetDirection(path[i - 1], coord);
                if (dirFrom >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirFrom);
            }
            if (i < totalLen - 1)
            {
                int dirTo = GetDirection(coord, path[i + 1]);
                if (dirTo >= 0) tile.RiverDirections = tile.SetDirectionBit(tile.RiverDirections, dirTo);
            }

            // 渐进加宽：根据流程比例决定宽度
            float progress = (float)i / totalLen; // 0=源头, 1=入海口
            int width = GetRiverWidth(progress);

            if (width >= 2)
            {
                // 计算河流流向的垂直方向（用于加宽）
                int flowDir = (i < totalLen - 1) ? GetDirection(coord, path[i + 1]) : 
                              (i > 0) ? GetDirection(path[i - 1], coord) : 0;
                
                // 垂直方向 = 流向 +2 和 +4（六边形的两个垂直邻居）
                int perpDir1 = (flowDir + 2) % 6;
                int perpDir2 = (flowDir + 4) % 6;

                // 加宽到一侧（确定性选择）
                var side1 = Map.HexOverworldTile.GetNeighbor(coord.X, coord.Y, perpDir1);
                StampRiverExpansion(chunk, side1, isInChunk);

                // 宽度 3 时加宽到另一侧
                if (width >= 3)
                {
                    var side2 = Map.HexOverworldTile.GetNeighbor(coord.X, coord.Y, perpDir2);
                    StampRiverExpansion(chunk, side2, isInChunk);
                }
            }
        }
    }

    /// <summary>根据流程比例确定河道宽度</summary>
    private static int GetRiverWidth(float progress)
    {
        if (progress < 0.4f) return 1;       // 源头到中上游：1 格宽
        if (progress < 0.75f) return 2;      // 中游：2 格宽
        return 3;                             // 下游（最后 25%）：3 格宽
    }

    /// <summary>将河流扩展到邻居格（不覆盖道路和已有河流）</summary>
    private static void StampRiverExpansion(Map.ChunkData chunk, Vector2I coord, Func<int, int, bool> isInChunk)
    {
        if (!isInChunk(coord.X, coord.Y)) return;
        var tile = chunk.GetTile(coord.X, coord.Y);
        if (tile == null || tile.IsRoad || tile.IsRiver) return;
        // 不覆盖已有的深水（海洋）
        if (tile.Terrain == Map.HexOverworldTile.TerrainType.DeepWater) return;

        tile.IsRiver = true;
        tile.SetTerrain(Map.HexOverworldTile.TerrainType.River);
    }

    /// <summary>计算两个相邻坐标之间的六角方向 (0-5)</summary>
    private static int GetDirection(Vector2I from, Vector2I to)
    {
        var diffCube = Map.HexOverworldTile.AxialToCube(to.X, to.Y) - Map.HexOverworldTile.AxialToCube(from.X, from.Y);
        for (int i = 0; i < 6; i++)
            if (diffCube == Map.HexOverworldTile.CubeDirections[i]) return i;
        return -1;
    }
}
