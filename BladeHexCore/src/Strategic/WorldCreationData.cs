// WorldCreationData.cs
// 世界创建配置 + 世界数据容器
// 从 WorldCreator.cs 提取，降低单文件复杂度
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 世界创建配置
/// </summary>
public class WorldCreationConfig
{
    /// <summary>世界大小枚举</summary>
    public enum WorldSize
    {
        Small = 0,   // ~65k tiles, 单模板
        Medium = 1,  // ~221k tiles, 2x2 模板
        Large = 2,   // ~459k tiles, 3x3 模板
        Mega = 3,    // ~800k tiles, 4x4 模板
    }

    /// <summary>世界宽度（chunk 数）</summary>
    public int WorldChunksW { get; set; } = 64;

    /// <summary>世界高度（chunk 数）</summary>
    public int WorldChunksH { get; set; } = 48;

    /// <summary>国家配置列表</summary>
    public List<NationConfig> Nations { get; set; } = new();

    /// <summary>最小生态区面积（用于聚类）</summary>
    public int MinBiomeZoneSize { get; set; } = 200;

    /// <summary>世界 tile 总宽度</summary>
    public int WorldTileWidth => WorldChunksW * ChunkData.ChunkSize;

    /// <summary>世界 tile 总高度</summary>
    public int WorldTileHeight => WorldChunksH * ChunkData.ChunkSize;

    /// <summary>世界大小显示名称</summary>
    public static string[] GetSizeNames() => new[] { "小型", "中型", "大型", "超大" };

    /// <summary>世界大小描述</summary>
    public static string[] GetSizeDescriptions() => new[]
    {
        "约 6 万格 · 一个完整地区",
        "约 22 万格 · 一片广阔大陆",
        "约 46 万格 · 两个相邻地区",
        "约 80 万格 · 多区域世界",
    };

    /// <summary>根据大小枚举创建配置</summary>
    public static WorldCreationConfig Create(WorldSize size, int seed)
    {
        return size switch
        {
            WorldSize.Small => Small(seed),
            WorldSize.Medium => Medium(seed),
            WorldSize.Large => Large(seed),
            WorldSize.Mega => Mega(seed),
            _ => Medium(seed),
        };
    }

    /// <summary>每种大小的模板网格维度（每个网格用一个独立模板）</summary>
    public static (int gridW, int gridH) GetTemplateGrid(WorldSize size) => size switch
    {
        WorldSize.Small => (1, 1),  // 单一模板：聚焦一个区域
        WorldSize.Medium => (1, 1), // 单一模板：聚焦一个区域，规模放大
        WorldSize.Large => (2, 2),  // 2 个相邻区域
        WorldSize.Mega => (3, 3),   // 多个区域但不至于碎裂
        _ => (1, 1),
    };

    /// <summary>超大世界 — 80×56 chunks ≈ 800k tiles</summary>
    public static WorldCreationConfig Mega(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 80,
            WorldChunksH = 56,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 350,
        };
    }

    /// <summary>大型世界 — 56×32 chunks ≈ 459k tiles</summary>
    public static WorldCreationConfig Large(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 56,
            WorldChunksH = 32,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 200,
        };
    }

    /// <summary>中型世界 — 36×24 chunks ≈ 221k tiles</summary>
    public static WorldCreationConfig Medium(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 36,
            WorldChunksH = 24,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 120,
        };
    }

    /// <summary>小型世界 — 21×12 chunks ≈ 65k tiles</summary>
    public static WorldCreationConfig Small(int seed)
    {
        return new WorldCreationConfig
        {
            WorldChunksW = 21,
            WorldChunksH = 12,
            Nations = NationConfig.GetDefaultNations(),
            MinBiomeZoneSize = 50,
        };
    }

    /// <summary>旧接口兼容</summary>
    public static WorldCreationConfig Default(int seed) => Large(seed);
}

/// <summary>
/// 世界数据 — 生成结果的完整容器
/// </summary>
public class WorldData
{
    public int Seed { get; set; }
    public int WorldChunksW { get; set; }
    public int WorldChunksH { get; set; }
    public Dictionary<Vector2I, ChunkData> Chunks { get; set; } = new();
    public List<OverworldPOI> Pois { get; set; } = new();
    public RiverRoadSkeleton? Skeleton { get; set; }
    public List<BiomeZone> Zones { get; set; } = new();
    public Dictionary<string, NationTerritory> Territories { get; set; } = new();
    public List<NationConfig> Nations { get; set; } = new();
    /// <summary>特殊角色（领主 + 冒险者），生成后应收容到 DormantEntityPool</summary>
    public List<OverworldEntity> SpecialCharacters { get; set; } = new();
}
