using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗上下文 — 封装从大地图到战斗场景的所有传递信息
/// </summary>
[GlobalClass]
public partial class BattleContext : Resource
{
    /// <summary>交战类型</summary>
    public enum EngagementType
    {
        Normal,    // 正常遭遇：双方在地图两端部署
        Ambush,    // 玩家伏击敌人：玩家分散有利，敌人集中混乱
        Ambushed,  // 玩家被伏击：玩家集中混乱，敌人分散有利，首回合AC-2
    }

    /// <summary>战斗规模 (对应 BattleMapGenerator.BattleSize)</summary>
    public enum BattleSize
    {
        Mercenary,  // 雇佣兵（小型遭遇）
        Knight,     // 骑士（中型遭遇）
        Lord,       // 领主（大型遭遇）
        Stronghold, // 据点（攻城/据点战，显著大于遭遇战）
    }

    // ========================================
    // 数据字段
    // ========================================

    /// <summary>大地图地形 — 使用统一的 HexOverworldTile.TerrainType</summary>
    public Map.HexOverworldTile.TerrainType Terrain = Map.HexOverworldTile.TerrainType.Plains;
    public BattleSize Size = BattleSize.Mercenary;
    public EngagementType Engagement = EngagementType.Normal;
    public int Seed = 0;
    public string EnvironmentOverride = "";
    public Vector2I EncounterPosition = Vector2I.Zero;
    public HexOverworldGrid? OverworldGrid = null;
    public Vector2I EncounterCoord = Vector2I.Zero;
    public int PoiType = -1;

    // ========================================
    // 战略层 ↔ 战斗层桥梁数据
    // ========================================

    /// <summary>攻击方战略实体（可为 null 表示玩家）</summary>
    public OverworldEntity? AttackerEntity;

    /// <summary>防御方战略实体（可为 null）</summary>
    public OverworldEntity? DefenderEntity;

    /// <summary>被围攻的 POI（围攻/劫掠场景）</summary>
    public OverworldPOI? DefendingPOI;

    /// <summary>攻击方部署数据</summary>
    public BattleUnitDeployment[]? AttackerDeployment;

    /// <summary>防御方部署数据</summary>
    public BattleUnitDeployment[]? DefenderDeployment;

    /// <summary>是否围攻战</summary>
    public bool IsSiege = false;

    /// <summary>是否劫掠</summary>
    public bool IsRaid = false;

    /// <summary>是否伏击</summary>
    public bool IsAmbush = false;

    // ========================================
    // 工厂方法
    // ========================================

    /// <summary>
    /// 从战略遭遇创建战斗上下文
    /// </summary>
    public static BattleContext CreateFromEncounter(
        OverworldEntity? attacker,
        OverworldEntity? defender,
        OverworldPOI? poi,
        Map.HexOverworldGrid? grid,
        Vector2I coord)
    {
        var context = new BattleContext();

        context.AttackerEntity = attacker;
        context.DefenderEntity = defender;
        context.DefendingPOI = poi;

        // 生成部署
        if (attacker != null)
            context.AttackerDeployment = attacker.GetDeployment(true);

        if (defender != null)
            context.DefenderDeployment = defender.GetDeployment(false);
        else if (poi != null)
            context.DefenderDeployment = poi.GenerateDefenseDeployment();

        // 判断战斗类型
        context.IsSiege = poi != null && attacker != null;
        context.IsRaid = poi != null && attacker?.EntityTypeEnum == OverworldEntity.EntityType.RaidingParty;
        context.IsAmbush = defender != null && defender.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster;

        // 设置战斗坐标
        context.EncounterCoord = coord;

        return context;
    }

    // ========================================
    // 工厂方法
    // ========================================

    public static BattleContext Create(
        Map.HexOverworldTile.TerrainType terrain,
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

    public static BattleContext CreateFromNoise(
        float noiseValue,
        BattleSize size = BattleSize.Mercenary,
        EngagementType engagement = EngagementType.Normal,
        int seedVal = 0
    )
    {
        var terrain = noiseValue switch
        {
            < -0.3f => Map.HexOverworldTile.TerrainType.DeepWater,
            < -0.1f => Map.HexOverworldTile.TerrainType.Swamp,
            < 0.3f => Map.HexOverworldTile.TerrainType.Plains,
            < 0.5f => Map.HexOverworldTile.TerrainType.Forest,
            _ => Map.HexOverworldTile.TerrainType.Mountain
        };
        return Create(terrain, size, engagement, seedVal);
    }

    // ========================================
    // 辅助方法
    // ========================================

    public string GetDescription()
    {
        string terrainName = Map.HexOverworldTile.GetBattleCategory(Terrain) switch
        {
            Map.HexOverworldTile.BattleTerrainCategory.Plains => "平原",
            Map.HexOverworldTile.BattleTerrainCategory.Forest => "森林",
            Map.HexOverworldTile.BattleTerrainCategory.Mountain => "山地",
            Map.HexOverworldTile.BattleTerrainCategory.Swamp => "沼泽",
            Map.HexOverworldTile.BattleTerrainCategory.Water => "水域",
            Map.HexOverworldTile.BattleTerrainCategory.Road => "道路",
            Map.HexOverworldTile.BattleTerrainCategory.Desert => "沙漠",
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
