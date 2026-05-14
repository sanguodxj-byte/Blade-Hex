// RegionRegistry.cs
// 区域定义 SSOT（Single Source of Truth）
// HexOverworldGenerator 和 WorldGenerator 都从同一份数据读取
using System.Collections.Generic;
using Godot;

namespace BladeHex.Map;

/// <summary>
/// 区域定义 — 地理区域的参数化描述
/// </summary>
public class RegionDef
{
    public string Name { get; set; } = "";
    public float CenterQ { get; set; } = 0.0f;
    public float CenterR { get; set; } = 0.0f;
    public float RadiusQ { get; set; } = 0.2f;
    public float RadiusR { get; set; } = 0.2f;
    public float DangerLevel { get; set; } = 0.0f;
    public HexOverworldTile.TerrainType[] PreferredTerrains { get; set; } = [];
    public float PoiDensity { get; set; } = 1.0f;
}

/// <summary>
/// 区域注册表 — 游戏世界中所有区域的唯一定义来源
/// 两份生成器（HexOverworldGenerator / WorldGenerator）共用同一份数据
/// </summary>
public static class RegionRegistry
{
    public static readonly RegionDef[] Regions =
    [
        new RegionDef
        {
            Name = "霜冠山脉", CenterQ = 0.5f, CenterR = 0.1f, RadiusQ = 0.4f, RadiusR = 0.12f,
            DangerLevel = 0.7f,
            PreferredTerrains =
            [
                HexOverworldTile.TerrainType.Mountain,
                HexOverworldTile.TerrainType.MountainSnow,
                HexOverworldTile.TerrainType.Snow,
                HexOverworldTile.TerrainType.Hills,
            ],
            PoiDensity = 0.6f,
        },
        new RegionDef
        {
            Name = "银叶森林", CenterQ = 0.15f, CenterR = 0.45f, RadiusQ = 0.12f, RadiusR = 0.25f,
            DangerLevel = 0.3f,
            PreferredTerrains =
            [
                HexOverworldTile.TerrainType.Forest,
                HexOverworldTile.TerrainType.DenseForest,
            ],
            PoiDensity = 0.9f,
        },
        new RegionDef
        {
            Name = "中央平原", CenterQ = 0.5f, CenterR = 0.5f, RadiusQ = 0.35f, RadiusR = 0.2f,
            DangerLevel = 0.1f,
            PreferredTerrains =
            [
                HexOverworldTile.TerrainType.Plains,
                HexOverworldTile.TerrainType.Grassland,
            ],
            PoiDensity = 1.2f,
        },
        new RegionDef
        {
            Name = "焦土荒原", CenterQ = 0.75f, CenterR = 0.85f, RadiusQ = 0.2f, RadiusR = 0.12f,
            DangerLevel = 0.8f,
            PreferredTerrains =
            [
                HexOverworldTile.TerrainType.Sand,
                HexOverworldTile.TerrainType.Savanna,
                HexOverworldTile.TerrainType.Wasteland,
            ],
            PoiDensity = 0.7f,
        },
        new RegionDef
        {
            Name = "蛮荒沼泽", CenterQ = 0.2f, CenterR = 0.85f, RadiusQ = 0.18f, RadiusR = 0.12f,
            DangerLevel = 0.5f,
            PreferredTerrains =
            [
                HexOverworldTile.TerrainType.Swamp,
                HexOverworldTile.TerrainType.Bog,
            ],
            PoiDensity = 0.8f,
        },
        new RegionDef
        {
            Name = "丘陵草原", CenterQ = 0.85f, CenterR = 0.5f, RadiusQ = 0.12f, RadiusR = 0.2f,
            DangerLevel = 0.4f,
            PreferredTerrains =
            [
                HexOverworldTile.TerrainType.Savanna,
                HexOverworldTile.TerrainType.Hills,
                HexOverworldTile.TerrainType.Plains,
            ],
            PoiDensity = 0.8f,
        },
    ];
}