using Godot;
using System;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗上下文 — 封装从大地图到战斗场景的所有传递信息
/// </summary>
public partial class BattleContext : RefCounted
{
    /// <summary>交战类型</summary>
    public enum EngagementType
    {
        Normal,    // 正常遭遇：双方在地图两端部署
        Ambush,    // 玩家伏击敌人：玩家分散有利，敌人集中混乱
        Ambushed,  // 玩家被伏击：玩家集中混乱，敌人分散有利，首回合AC-2
    }

    /// <summary>大地图地形类型 (对应 OverworldTerrain.Type)</summary>
    public enum OverworldTerrainType
    {
        Plains,
        Forest,
        Mountain,
        Swamp,
        Water,
        Road,
        Desert
    }

    /// <summary>战斗规模 (对应 BattleMapGenerator.BattleSize)</summary>
    public enum BattleSize
    {
        Mercenary, // 雇佣兵
        Knight,    // 骑士
        Lord       // 领主
    }

    // ========================================
    // 数据字段
    // ========================================

    public OverworldTerrainType Terrain = OverworldTerrainType.Plains;
    public BattleSize Size = BattleSize.Mercenary;
    public EngagementType Engagement = EngagementType.Normal;
    public int Seed = 0;
    public string EnvironmentOverride = "";
    public Vector2I EncounterPosition = Vector2I.Zero;
    public HexOverworldGrid? OverworldGrid = null;
    public Vector2I EncounterCoord = Vector2I.Zero;
    public int PoiType = -1;

    // ========================================
    // 工厂方法
    // ========================================

    public static BattleContext Create(
        OverworldTerrainType terrain,
        BattleSize size,
        EngagementType engagement,
        int seedVal = 0
    )
    {
        var ctx = new BattleContext
        {
            Terrain = terrain,
            Size = size,
            Engagement = engagement,
            Seed = seedVal != 0 ? seedVal : (int)GD.Randi()
        };
        return ctx;
    }

    // 注意：create_from_noise 需要 OverworldTerrain.from_noise 逻辑
    // 这里暂时简化实现，后续如果 OverworldTerrain.cs 完善了再调用
    public static BattleContext CreateFromNoise(
        float noiseValue,
        BattleSize size = BattleSize.Mercenary,
        EngagementType engagement = EngagementType.Normal,
        int seedVal = 0
    )
    {
        OverworldTerrainType terrain = noiseValue switch
        {
            < -0.3f => OverworldTerrainType.Water,
            < -0.1f => OverworldTerrainType.Swamp,
            < 0.3f => OverworldTerrainType.Plains,
            < 0.5f => OverworldTerrainType.Forest,
            _ => OverworldTerrainType.Mountain
        };
        return Create(terrain, size, engagement, seedVal);
    }

    // ========================================
    // 辅助方法
    // ========================================

    public string GetDescription()
    {
        string terrainName = Terrain switch
        {
            OverworldTerrainType.Plains => "平原",
            OverworldTerrainType.Forest => "森林",
            OverworldTerrainType.Mountain => "山地",
            OverworldTerrainType.Swamp => "沼泽",
            OverworldTerrainType.Water => "水域",
            OverworldTerrainType.Road => "道路",
            OverworldTerrainType.Desert => "沙漠",
            _ => "未知"
        };

        string sizeName = Size switch
        {
            BattleSize.Mercenary => "雇佣兵",
            BattleSize.Knight => "骑士",
            BattleSize.Lord => "领主",
            _ => ""
        };

        string engagementName = Engagement switch
        {
            EngagementType.Normal => "正常遭遇",
            EngagementType.Ambush => "伏击",
            EngagementType.Ambushed => "被伏击",
            _ => ""
        };

        return $"{sizeName}规模·{terrainName}·{engagementName}";
    }
}
