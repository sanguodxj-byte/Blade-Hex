// ChunkData.cs
// 单个 Chunk 数据模型 — 16×16 六边形瓦片 + 遭遇槽位
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// Chunk 遭遇槽位状态
/// </summary>
public enum EncounterSlotState : byte
{
    None = 0,       // 无遭遇
    Available = 1,  // 可触发
    Triggered = 2,  // 已触发
}

/// <summary>
/// 单个 Chunk — 16×16 六边形瓦片的存储单元
/// 坐标约定: ChunkCoord 为 chunk 级别的轴向坐标
/// 全局坐标 = ChunkCoord * ChunkSize + TileOffset
/// </summary>
[GlobalClass]
public partial class ChunkData : RefCounted
{
    // ========================================
    // 常量
    // ========================================

    /// <summary>每个 chunk 的边长（六边形数）</summary>
    public const int ChunkSize = 16;

    /// <summary>每个 chunk 的瓦片总数</summary>
    public const int TileCount = ChunkSize * ChunkSize;

    // ========================================
    // Chunk 坐标
    // ========================================

    /// <summary>Chunk 轴向坐标 (chunkQ, chunkR)</summary>
    public Vector2I ChunkCoord = Vector2I.Zero;

    // ========================================
    // 瓦片数据
    // ========================================

    /// <summary>所有瓦片: 全局轴向坐标 → HexOverworldTile</summary>
    public Dictionary<Vector2I, HexOverworldTile> Tiles { get; set; } = new();

    // ========================================
    // 遭遇槽位
    // ========================================

    /// <summary>遭遇槽位: 全局轴向坐标 → 状态</summary>
    public Dictionary<Vector2I, EncounterSlotState> EncounterSlots { get; set; } = new();

    // ========================================
    // Chunk 状态
    // ========================================

    /// <summary>是否已生成</summary>
    public bool IsGenerated { get; set; } = false;

    /// <summary>是否活跃（在玩家加载范围内）</summary>
    public bool IsActive { get; set; } = false;

    /// <summary>区域名称（来自 RegionDef）</summary>
    public string RegionName { get; set; } = "";

    // ========================================
    // 坐标转换
    // ========================================

    /// <summary>Chunk 坐标 → 该 chunk 左上角的全局轴向坐标</summary>
    public static Vector2I ChunkToWorld(int chunkQ, int chunkR)
    {
        return new Vector2I(chunkQ * ChunkSize, chunkR * ChunkSize);
    }

    /// <summary>全局轴向坐标 → 所属的 Chunk 坐标</summary>
    public static Vector2I WorldToChunk(int worldQ, int worldR)
    {
        // 处理负数坐标的向下取整
        int cq = worldQ >= 0 ? worldQ / ChunkSize : (worldQ - ChunkSize + 1) / ChunkSize;
        int cr = worldR >= 0 ? worldR / ChunkSize : (worldR - ChunkSize + 1) / ChunkSize;
        return new Vector2I(cq, cr);
    }

    /// <summary>全局坐标 → chunk 内偏移 (0 ~ ChunkSize-1)</summary>
    public static Vector2I WorldToOffset(int worldQ, int worldR)
    {
        int oq = ((worldQ % ChunkSize) + ChunkSize) % ChunkSize;
        int or_ = ((worldR % ChunkSize) + ChunkSize) % ChunkSize;
        return new Vector2I(oq, or_);
    }

    /// <summary>Chunk 坐标 + 偏移 → 全局坐标</summary>
    public static Vector2I ChunkOffsetToWorld(int chunkQ, int chunkR, int offsetQ, int offsetR)
    {
        return new Vector2I(chunkQ * ChunkSize + offsetQ, chunkR * ChunkSize + offsetR);
    }

    // ========================================
    // 瓦片访问
    // ========================================

    /// <summary>获取指定全局坐标的瓦片</summary>
    public HexOverworldTile? GetTile(int worldQ, int worldR)
    {
        return Tiles.GetValueOrDefault(new Vector2I(worldQ, worldR));
    }

    /// <summary>获取 chunk 内所有可通行瓦片</summary>
    public HexOverworldTile[] GetPassableTiles()
    {
        var result = new List<HexOverworldTile>();
        foreach (var tile in Tiles.Values)
            if (tile.IsPassable) result.Add(tile);
        return result.ToArray();
    }

    /// <summary>获取 chunk 中心的像素坐标</summary>
    public Vector2 GetCenterPixel()
    {
        var origin = ChunkToWorld(ChunkCoord.X, ChunkCoord.Y);
        int centerQ = origin.X + ChunkSize / 2;
        int centerR = origin.Y + ChunkSize / 2;
        return HexOverworldTile.AxialToPixel(centerQ, centerR);
    }

    // ========================================
    // 遭遇槽位
    // ========================================

    /// <summary>获取指定全局坐标的遭遇状态</summary>
    public EncounterSlotState GetEncounterState(int worldQ, int worldR)
    {
        return EncounterSlots.GetValueOrDefault(new Vector2I(worldQ, worldR));
    }

    /// <summary>设置遭遇状态</summary>
    public void SetEncounterState(int worldQ, int worldR, EncounterSlotState state)
    {
        EncounterSlots[new Vector2I(worldQ, worldR)] = state;
    }

    /// <summary>获取所有可触发的遭遇位置</summary>
    public Vector2I[] GetAvailableEncounters()
    {
        var result = new List<Vector2I>();
        foreach (var kv in EncounterSlots)
            if (kv.Value == EncounterSlotState.Available)
                result.Add(kv.Key);
        return result.ToArray();
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var tilesData = new Godot.Collections.Array();
        foreach (var tile in Tiles.Values)
            tilesData.Add(tile.Serialize());

        var encounterData = new Godot.Collections.Array();
        foreach (var kv in EncounterSlots)
        {
            if (kv.Value != EncounterSlotState.None)
            {
                encounterData.Add(new Godot.Collections.Dictionary
                {
                    ["q"] = kv.Key.X,
                    ["r"] = kv.Key.Y,
                    ["state"] = (byte)kv.Value,
                });
            }
        }

        return new Godot.Collections.Dictionary
        {
            ["chunk_q"] = ChunkCoord.X,
            ["chunk_r"] = ChunkCoord.Y,
            ["region"] = RegionName,
            ["tiles"] = tilesData,
            ["encounters"] = encounterData,
        };
    }

    public static ChunkData Deserialize(Godot.Collections.Dictionary data)
    {
        var chunk = new ChunkData();
        chunk.ChunkCoord = new Vector2I(
            data.ContainsKey("chunk_q") ? (int)data["chunk_q"] : 0,
            data.ContainsKey("chunk_r") ? (int)data["chunk_r"] : 0
        );
        chunk.RegionName = data.ContainsKey("region") ? (string)data["region"] : "";
        chunk.IsGenerated = true;

        if (data.ContainsKey("tiles") && data["tiles"].Obj is Godot.Collections.Array tilesData)
        {
            foreach (var tileData in tilesData)
            {
                var tile = HexOverworldTile.Deserialize((Godot.Collections.Dictionary)tileData);
                chunk.Tiles[tile.Coord] = tile;
            }
        }

        if (data.ContainsKey("encounters") && data["encounters"].Obj is Godot.Collections.Array encounterData)
        {
            foreach (var entry in encounterData)
            {
                var d = (Godot.Collections.Dictionary)entry;
                var coord = new Vector2I((int)d["q"], (int)d["r"]);
                chunk.EncounterSlots[coord] = (EncounterSlotState)(byte)d["state"];
            }
        }

        return chunk;
    }
}
