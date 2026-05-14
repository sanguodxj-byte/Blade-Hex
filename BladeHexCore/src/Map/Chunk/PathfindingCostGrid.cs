// PathfindingCostGrid.cs
// 预计算地形代价网格 — 按 Chunk 粒度缓存移动代价，加速 A* 寻路
// 两层数据: 基础地形代价 (float[]) + ZoC 附加乘数 (float[])
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 预计算地形代价网格 — 使用连续 float[] 数组按 Chunk 缓存移动代价。
/// ChunkAStar 在 Tile 级 A* 中通过数组索引直接读取代价，避免逐次查询 tile 对象。
/// </summary>
[GlobalClass]
public partial class PathfindingCostGrid : RefCounted
{
    // ========================================
    // 缓存数据
    // ========================================

    /// <summary>基础地形代价: ChunkCoord → float[ChunkSize*ChunkSize]</summary>
    private readonly Dictionary<Vector2I, float[]> _terrainCosts = new();

    /// <summary>ZoC 附加乘数: ChunkCoord → float[ChunkSize*ChunkSize]，默认 1.0</summary>
    private readonly Dictionary<Vector2I, float[]> _zocMultipliers = new();

    /// <summary>Chunk 尺寸（从 ChunkData.ChunkSize 获取）</summary>
    private static int ChunkSize => ChunkData.ChunkSize;

    /// <summary>单个 Chunk 的数组长度</summary>
    private static int ArrayLength => ChunkSize * ChunkSize;

    // ========================================
    // Chunk 生命周期
    // ========================================

    /// <summary>
    /// Chunk 加载时预计算代价数组。
    /// 遍历 chunk 内所有 tile，从 TerrainCostTable 获取代价并写入连续数组。
    /// </summary>
    public void OnChunkLoaded(ChunkData chunk)
    {
        var coord = chunk.ChunkCoord;
        var origin = ChunkData.ChunkToWorld(coord.X, coord.Y);

        var costs = new float[ArrayLength];

        for (int dq = 0; dq < ChunkSize; dq++)
        {
            for (int dr = 0; dr < ChunkSize; dr++)
            {
                int idx = dq * ChunkSize + dr;
                int worldQ = origin.X + dq;
                int worldR = origin.Y + dr;

                var tile = chunk.GetTile(worldQ, worldR);
                if (tile == null || !tile.IsPassable)
                {
                    costs[idx] = TerrainCostTable.ImpassableCost;
                }
                else
                {
                    costs[idx] = TerrainCostTable.GetMoveCost(tile);
                }
            }
        }

        _terrainCosts[coord] = costs;

        // ZoC 层: 如果已有（从 ZoC 更新中设置），保留；否则不创建（节省内存）
    }

    /// <summary>Chunk 卸载时释放缓存</summary>
    public void OnChunkUnloaded(Vector2I chunkCoord)
    {
        _terrainCosts.Remove(chunkCoord);
        _zocMultipliers.Remove(chunkCoord);
    }

    // ========================================
    // 代价查询
    // ========================================

    /// <summary>
    /// 获取综合移动代价: 基础地形代价 × ZoC 乘数。
    /// 如果 chunk 未缓存，返回 -1 表示需要回退到 tile 查询。
    /// </summary>
    public float GetCost(int worldQ, int worldR)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);

        if (!_terrainCosts.TryGetValue(chunkCoord, out var costs))
            return -1f; // 未缓存，调用方应回退

        var origin = ChunkData.ChunkToWorld(chunkCoord.X, chunkCoord.Y);
        int dq = worldQ - origin.X;
        int dr = worldR - origin.Y;

        if (dq < 0 || dq >= ChunkSize || dr < 0 || dr >= ChunkSize)
            return -1f;

        int idx = dq * ChunkSize + dr;
        float baseCost = costs[idx];

        // ZoC 乘数
        if (_zocMultipliers.TryGetValue(chunkCoord, out var zocArr))
            baseCost *= zocArr[idx];

        return baseCost;
    }

    /// <summary>获取纯地形代价（不含 ZoC）</summary>
    public float GetTerrainCost(int worldQ, int worldR)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);

        if (!_terrainCosts.TryGetValue(chunkCoord, out var costs))
            return -1f;

        var origin = ChunkData.ChunkToWorld(chunkCoord.X, chunkCoord.Y);
        int dq = worldQ - origin.X;
        int dr = worldR - origin.Y;

        if (dq < 0 || dq >= ChunkSize || dr < 0 || dr >= ChunkSize)
            return -1f;

        return costs[dq * ChunkSize + dr];
    }

    // ========================================
    // 单 tile 更新
    // ========================================

    /// <summary>更新单个 tile 的地形代价（道路状态变化时调用）</summary>
    public void UpdateTile(int worldQ, int worldR, float newCost)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);
        if (!_terrainCosts.TryGetValue(chunkCoord, out var costs)) return;

        var origin = ChunkData.ChunkToWorld(chunkCoord.X, chunkCoord.Y);
        int dq = worldQ - origin.X;
        int dr = worldR - origin.Y;

        if (dq >= 0 && dq < ChunkSize && dr >= 0 && dr < ChunkSize)
            costs[dq * ChunkSize + dr] = newCost;
    }

    // ========================================
    // ZoC 代价层
    // ========================================

    /// <summary>
    /// 更新一组 tile 的 ZoC 乘数。
    /// 用于 POI 控制区生效时增加代价。
    /// </summary>
    public void UpdateZocRegion(HashSet<Vector2I> tiles, float multiplier)
    {
        foreach (var tile in tiles)
        {
            var chunkCoord = ChunkData.WorldToChunk(tile.X, tile.Y);

            // 确保 ZoC 数组存在
            if (!_zocMultipliers.TryGetValue(chunkCoord, out var zocArr))
            {
                zocArr = new float[ArrayLength];
                System.Array.Fill(zocArr, 1.0f);
                _zocMultipliers[chunkCoord] = zocArr;
            }

            var origin = ChunkData.ChunkToWorld(chunkCoord.X, chunkCoord.Y);
            int dq = tile.X - origin.X;
            int dr = tile.Y - origin.Y;

            if (dq >= 0 && dq < ChunkSize && dr >= 0 && dr < ChunkSize)
            {
                int idx = dq * ChunkSize + dr;
                // 取最大乘数（多个 ZoC 重叠时不叠加，取最高惩罚）
                if (multiplier > zocArr[idx])
                    zocArr[idx] = multiplier;
            }
        }
    }

    /// <summary>
    /// 清除一组 tile 的 ZoC 乘数（POI 被攻占/摧毁时调用）。
    /// 重置为 1.0（无惩罚）。
    /// </summary>
    public void ClearZocRegion(HashSet<Vector2I> tiles)
    {
        foreach (var tile in tiles)
        {
            var chunkCoord = ChunkData.WorldToChunk(tile.X, tile.Y);
            if (!_zocMultipliers.TryGetValue(chunkCoord, out var zocArr)) continue;

            var origin = ChunkData.ChunkToWorld(chunkCoord.X, chunkCoord.Y);
            int dq = tile.X - origin.X;
            int dr = tile.Y - origin.Y;

            if (dq >= 0 && dq < ChunkSize && dr >= 0 && dr < ChunkSize)
                zocArr[dq * ChunkSize + dr] = 1.0f;
        }
    }

    /// <summary>检查是否有缓存数据</summary>
    public bool HasChunk(Vector2I chunkCoord) => _terrainCosts.ContainsKey(chunkCoord);
}
