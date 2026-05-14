// ChunkFogOfWar.cs
// Chunk 级别战争迷雾 — 基于 ChunkCoord 的三级迷雾
// 未加载的 chunk = 自然不可见（等同于 Unexplored）
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// Chunk 级别迷雾状态
/// </summary>
public enum ChunkFogState : byte
{
    Unexplored = 0,  // 未探索（chunk 未生成或未进入视野）
    Revealed = 1,    // 已探索（永久可见但无实时信息）
    InVision = 2,    // 当前视野（玩家加载范围内）
}

/// <summary>
/// Chunk 级别战争迷雾 — 简化版
/// 只记录已探索的 chunk 坐标，活跃 chunk = InVision
/// </summary>
[GlobalClass]
public partial class ChunkFogOfWar : RefCounted
{
    // ========================================
    // 数据
    // ========================================

    /// <summary>已探索的 chunk 坐标集合（永久记录）</summary>
    public HashSet<Vector2I> RevealedChunks { get; private set; } = new();

    /// <summary>当前视野内的 chunk 坐标集合（每帧更新）</summary>
    public HashSet<Vector2I> InVisionChunks { get; private set; } = new();

    /// <summary>种族初始揭示（归一化区域列表）</summary>
    private List<Rect2> _initialRegions = new();

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 用种族 ID 初始化迷雾（揭示种族出生区域）
    /// </summary>
    public void Initialize(BladeHex.Data.RaceData.Race raceId)
    {
        RevealedChunks.Clear();
        InVisionChunks.Clear();
        _initialRegions = GetRaceInitialRegions(raceId);
    }

    // ========================================
    // 每帧更新
    // ========================================

    /// <summary>
    /// 根据活跃 chunk 列表更新迷雾
    /// </summary>
    /// <param name="activeChunkCoords">当前加载的 chunk 坐标集合</param>
    public void UpdateVision(HashSet<Vector2I> activeChunkCoords)
    {
        // 上一帧的 InVision 降级为 Revealed
        InVisionChunks.Clear();

        // 活跃 chunk 设为 InVision 并标记为已探索
        foreach (var coord in activeChunkCoords)
        {
            InVisionChunks.Add(coord);
            RevealedChunks.Add(coord);
        }
    }

    // ========================================
    // 查询
    // ========================================

    /// <summary>获取指定 chunk 的迷雾状态</summary>
    public ChunkFogState GetState(Vector2I chunkCoord)
    {
        if (InVisionChunks.Contains(chunkCoord)) return ChunkFogState.InVision;
        if (RevealedChunks.Contains(chunkCoord)) return ChunkFogState.Revealed;
        return ChunkFogState.Unexplored;
    }

    /// <summary>是否已探索</summary>
    public bool IsRevealed(Vector2I chunkCoord) => RevealedChunks.Contains(chunkCoord);

    /// <summary>是否在视野内</summary>
    public bool IsInVision(Vector2I chunkCoord) => InVisionChunks.Contains(chunkCoord);

    /// <summary>探索进度 (0~1)</summary>
    public float GetExplorationProgress(int totalWorldChunks)
    {
        if (totalWorldChunks <= 0) return 0f;
        return (float)RevealedChunks.Count / totalWorldChunks;
    }

    // ========================================
    // 手动揭示
    // ========================================

    /// <summary>揭示指定 chunk</summary>
    public void RevealChunk(Vector2I chunkCoord)
    {
        RevealedChunks.Add(chunkCoord);
    }

    /// <summary>揭示指定半径内的所有 chunk</summary>
    public void RevealArea(Vector2I centerChunk, int radius)
    {
        var chunks = ChunkManager.GetChunksInRadius(centerChunk, radius);
        foreach (var coord in chunks)
            RevealedChunks.Add(coord);
    }

    /// <summary>应用种族初始揭示</summary>
    public void ApplyRaceInitialReveal()
    {
        // 假设世界约 4×3 chunks（64/16 × 48/16）
        const int worldChunksQ = 4;
        const int worldChunksR = 3;

        foreach (var region in _initialRegions)
        {
            int qStart = (int)(region.Position.X * worldChunksQ);
            int rStart = (int)(region.Position.Y * worldChunksR);
            int qEnd = (int)((region.Position.X + region.Size.X) * worldChunksQ);
            int rEnd = (int)((region.Position.Y + region.Size.Y) * worldChunksR);

            for (int q = qStart; q < qEnd; q++)
                for (int r = rStart; r < rEnd; r++)
                    RevealedChunks.Add(new Vector2I(q, r));
        }
    }

    // ========================================
    // 种族初始区域（与 FogOfWar 一致）
    // ========================================

    private static List<Rect2> GetRaceInitialRegions(BladeHex.Data.RaceData.Race raceId)
    {
        return raceId switch
        {
            BladeHex.Data.RaceData.Race.Human => [
                new Rect2(0.05f, 0.2f, 0.85f, 0.55f),
                new Rect2(0.0f, 0.25f, 0.15f, 0.5f)
            ],
            BladeHex.Data.RaceData.Race.Elf => [
                new Rect2(0.0f, 0.2f, 0.25f, 0.6f),
                new Rect2(0.2f, 0.3f, 0.1f, 0.2f)
            ],
            BladeHex.Data.RaceData.Race.Dwarf => [
                new Rect2(0.1f, 0.0f, 0.8f, 0.25f),
                new Rect2(0.15f, 0.2f, 0.2f, 0.1f)
            ],
            BladeHex.Data.RaceData.Race.HalfOrc => [
                new Rect2(0.65f, 0.25f, 0.35f, 0.45f),
                new Rect2(0.55f, 0.35f, 0.15f, 0.15f)
            ],
            BladeHex.Data.RaceData.Race.HalfElf => [
                new Rect2(0.1f, 0.25f, 0.6f, 0.5f),
                new Rect2(0.0f, 0.25f, 0.15f, 0.4f)
            ],
            _ => [new Rect2(0.3f, 0.35f, 0.4f, 0.3f)]
        };
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var revealed = new Godot.Collections.Array();
        foreach (var coord in RevealedChunks)
            revealed.Add(new Vector2I(coord.X, coord.Y));

        return new Godot.Collections.Dictionary
        {
            ["revealed_chunks"] = revealed,
        };
    }

    public static ChunkFogOfWar Deserialize(Godot.Collections.Dictionary data)
    {
        var fog = new ChunkFogOfWar();

        if (data.ContainsKey("revealed_chunks") && data["revealed_chunks"].Obj is Godot.Collections.Array revealed)
        {
            foreach (var c in revealed)
            {
                var coord = (Vector2I)c;
                fog.RevealedChunks.Add(coord);
            }
        }

        return fog;
    }
}
