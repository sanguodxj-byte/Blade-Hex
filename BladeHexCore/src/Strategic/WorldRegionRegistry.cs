// WorldRegionRegistry.cs
// SSOT 区域定义 — 全局唯一的区域配置注册表（战略层世界生成用）
// 从 WorldGenerator 拆出：Region 数据模型、区域查询、位置验证
// 注意：与 BladeHex.Map.RegionRegistry（地图生成层静态区域定义）是不同系统
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 区域定义数据模型
/// </summary>
public class Region
{
    public string Name = "";
    public Vector2 NoiseRange = new(-1, 1);
    public Vector2 XRange = new(0, 1);
    public Vector2 YRange = new(0, 1);
    public Map.HexOverworldTile.TerrainType TerrainPreference = Map.HexOverworldTile.TerrainType.Plains;
    public float DangerLevel = 0.0f;
    public float PoiDensity = 1.0f;
}

/// <summary>
/// 区域注册表 — SSOT 区域定义（战略层）
/// 全局唯一的区域配置，由 WorldGenerator 初始化时创建
/// 提供区域查询、POI 位置验证等功能
/// 注意：与 BladeHex.Map.RegionRegistry（静态地图区域定义）是不同系统
/// </summary>
[GlobalClass]
public partial class WorldRegionRegistry : RefCounted
{
    // ========================================
    // 数据
    // ========================================

    /// <summary>所有已注册的区域</summary>
    public List<Region> Regions { get; private set; } = new();

    /// <summary>世界尺寸（像素）</summary>
    public int MapWidth { get; set; } = 6144;
    public int MapHeight { get; set; } = 4096;

    /// <summary>全局噪声（用于 POI 位置验证）</summary>
    public FastNoiseLite? Noise { get; set; }

    // ========================================
    // 初始化
    // ========================================

    public WorldRegionRegistry()
    {
        SetupDefaultRegions();
    }

    // ========================================
    // 默认区域配置
    // ========================================

    private void SetupDefaultRegions()
    {
        // 中央平原
        Regions.Add(new Region
        {
            Name = "中央平原",
            NoiseRange = new(-0.15f, 0.25f),
            XRange = new(0.1f, 0.9f),
            YRange = new(0.25f, 0.75f),
            TerrainPreference = Map.HexOverworldTile.TerrainType.Plains,
            DangerLevel = 0.1f,
            PoiDensity = 1.2f
        });

        // 霜冠山脉
        Regions.Add(new Region
        {
            Name = "霜冠山脉",
            NoiseRange = new(0.25f, 1.0f),
            XRange = new(0.1f, 0.9f),
            YRange = new(0.0f, 0.2f),
            TerrainPreference = Map.HexOverworldTile.TerrainType.Mountain,
            DangerLevel = 0.7f,
            PoiDensity = 0.6f
        });

        // 银叶森林
        Regions.Add(new Region
        {
            Name = "银叶森林",
            NoiseRange = new(0.15f, 0.5f),
            XRange = new(0.0f, 0.25f),
            YRange = new(0.2f, 0.8f),
            TerrainPreference = Map.HexOverworldTile.TerrainType.Forest,
            DangerLevel = 0.3f,
            PoiDensity = 0.9f
        });

        // 焦土荒原
        Regions.Add(new Region
        {
            Name = "焦土荒原",
            NoiseRange = new(-0.15f, 0.3f),
            XRange = new(0.5f, 1.0f),
            YRange = new(0.75f, 1.0f),
            TerrainPreference = Map.HexOverworldTile.TerrainType.Sand,
            DangerLevel = 0.8f,
            PoiDensity = 0.7f
        });

        // 蛮荒沼泽
        Regions.Add(new Region
        {
            Name = "蛮荒沼泽",
            NoiseRange = new(-0.3f, -0.05f),
            XRange = new(0.0f, 0.4f),
            YRange = new(0.75f, 1.0f),
            TerrainPreference = Map.HexOverworldTile.TerrainType.Swamp,
            DangerLevel = 0.5f,
            PoiDensity = 0.8f
        });

        // 丘陵草原
        Regions.Add(new Region
        {
            Name = "丘陵草原",
            NoiseRange = new(-0.1f, 0.3f),
            XRange = new(0.7f, 1.0f),
            YRange = new(0.25f, 0.7f),
            TerrainPreference = Map.HexOverworldTile.TerrainType.Plains,
            DangerLevel = 0.4f,
            PoiDensity = 0.8f
        });
    }

    // ========================================
    // 区域查询
    // ========================================

    /// <summary>
    /// 根据像素坐标和噪声值获取最佳匹配区域
    /// 使用距离中心的接近度作为评分标准
    /// </summary>
    public Region GetRegionAt(float px, float py, float noiseVal)
    {
        float nx = px / MapWidth;
        float ny = py / MapHeight;

        Region bestRegion = Regions[0];
        float bestScore = -1.0f;

        foreach (var region in Regions)
        {
            if (nx >= region.XRange.X && nx <= region.XRange.Y &&
                ny >= region.YRange.X && ny <= region.YRange.Y &&
                noiseVal >= region.NoiseRange.X && noiseVal <= region.NoiseRange.Y)
            {
                float cx = (region.XRange.X + region.XRange.Y) / 2.0f;
                float cy = (region.YRange.X + region.YRange.Y) / 2.0f;
                float dist = new Vector2(nx - cx, ny - cy).Length();
                float score = 1.0f - dist;
                if (score > bestScore) { bestScore = score; bestRegion = region; }
            }
        }
        return bestRegion;
    }

    /// <summary>
    /// 按名称查找区域
    /// </summary>
    public Region GetRegionByName(string name)
    {
        foreach (var r in Regions)
            if (r.Name == name) return r;
        return Regions[0];
    }

    // ========================================
    // 位置验证
    // ========================================

    /// <summary>
    /// 验证给定像素位置是否适合放置 POI
    /// 排除噪声低谷、地图边缘、已有 POI 附近的位置
    /// </summary>
    public bool IsValidPoiPosition(float px, float py, List<OverworldPOI> existingPois, float minDistance = 120.0f)
    {
        if (Noise != null && Noise.GetNoise2D(px, py) < -0.25f) return false;
        if (px < 80 || py < 80 || px > MapWidth - 80 || py > MapHeight - 80) return false;
        foreach (var poi in existingPois)
            if (poi.Position.DistanceTo(new Vector2(px, py)) < minDistance) return false;
        return true;
    }

    /// <summary>
    /// 在指定区域内随机寻找一个有效的 POI 位置
    /// 最多尝试 50 次
    /// </summary>
    public Vector2 FindPositionInRegion(Region region, List<OverworldPOI> existingPois, float minDistance = 120.0f)
    {
        var random = new Random();
        for (int i = 0; i < 50; i++)
        {
            float px = region.XRange.X * MapWidth + (float)random.NextDouble() * (region.XRange.Y - region.XRange.X) * MapWidth;
            float py = region.YRange.X * MapHeight + (float)random.NextDouble() * (region.YRange.Y - region.YRange.X) * MapHeight;
            if (IsValidPoiPosition(px, py, existingPois, minDistance)) return new Vector2(px, py);
        }
        return new Vector2(MapWidth / 2.0f, MapHeight / 2.0f);
    }
}
