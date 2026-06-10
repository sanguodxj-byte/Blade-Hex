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

    /// <summary>战斗规模 — 决定大地图采样范围和战斗地图大小</summary>
    public enum BattleSize
    {
        Mercenary,  // 小型：K=0, 单格采样, radius=7 (169 cells)
        Knight,     // 中型：K=1, 7格采样, radius=11 (397 cells)
        Lord,       // 大型：K=2, 19格采样, radius=14 (631 cells)
        Stronghold, // 巨大：K=3, 37格采样, radius=18 (973 cells)
    }

    /// <summary>
    /// R13 (2026-05-17) 主动发起战斗的一方。决定 R13#11a Castle 战中谁部署在城堡内。
    /// 默认 Player（玩家点 POI 攻击）；POI 主动派兵攻击玩家时设为 Enemy。
    /// </summary>
    public enum BattleSide { Player, Enemy }

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

    /// <summary>
    /// 大地图委托目标战斗关联的任务 ID。非委托战斗为空。
    /// 战斗场景不直接修改任务状态，由大地图恢复后根据该字段回写 QuestManager。
    /// </summary>
    public string QuestId = "";

    /// <summary>
    /// 敌方相对玩家的接近方向(以战场中心为原点的六边形 axial 方向向量)。
    /// null = 未知/不适用,DeploymentZone 退化到 q 轴左右切。
    /// 由 EncounterSpawner 在创建战斗时计算:POI 战取 `POI.CenterHex - PlayerCoord`,野外暂未实现保持 null。
    /// </summary>
    public Vector2I? ApproachDirection = null;

    /// <summary>
    /// R7 (2026-05-17) 大地图天气进入战斗的覆盖。
    /// 取值: "clear" / "rain" / "snow" / "sandstorm" / null。
    /// 不直接引用 Frontend 的 WeatherType 枚举（避免 Core ↔ Frontend 跨层依赖），由调用方负责 ToString().ToLower()。
    /// 未列出值或 "clear"/null 时不改写地形（仅记 info 日志）。
    /// </summary>
    public string? WeatherOverride = null;

    /// <summary>
    /// R13 (2026-05-17) 主动发起战斗的一方，决定 Castle 战墙内/墙外部署。
    /// 默认 Player（玩家攻击 POI）；POI 派兵反击玩家时设为 Enemy。
    /// </summary>
    public BattleSide AttackingSide = BattleSide.Player;

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
    // 战争闭环 MVP 战事中途加入扩展
    // ========================================

    public JoinOpportunity? WarJoinOppRef { get; set; }
    public bool PlayerJoinedAsAttacker { get; set; }

    // ── NvN 多方战场扩展 ──

    /// <summary>来源战场 ID（用于战后回写清理）</summary>
    public string SourceBattlefieldId { get; set; } = "";

    /// <summary>玩家加入后，参与战斗的攻击方实体列表</summary>
    public List<OverworldEntity> JoinedAttackers { get; set; } = new();

    /// <summary>玩家加入后，参与战斗的防御方实体列表</summary>
    public List<OverworldEntity> JoinedDefenders { get; set; } = new();

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
            context.AttackerDeployment = EntityCombatBridge.GetDeployment(attacker, true);

        if (defender != null)
            context.DefenderDeployment = EntityCombatBridge.GetDeployment(defender, false);
        else if (poi != null)
            context.DefenderDeployment = poi.GenerateDefenseDeployment();

        // 判断战斗类型
        context.IsSiege = poi != null && attacker != null;
        context.IsRaid = poi != null && attacker?.EntityTypeEnum == OverworldEntity.EntityType.RaidingParty;
        context.IsAmbush = defender != null && defender.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster;

        // 设置战斗坐标
        context.EncounterCoord = coord;
        context.OverworldGrid = grid;

        // 推导接近方向:POI 战 = POI 中心 - 玩家(玩家 coord)方向
        if (poi != null)
        {
            var dq = poi.CenterHex.X - coord.X;
            var dr = poi.CenterHex.Y - coord.Y;
            if (dq != 0 || dr != 0)
                context.ApproachDirection = new Vector2I(dq, dr);
        }
        else if (attacker != null && defender != null)
        {
            // R13#4 (2026-05-17) 野外遭遇:用 attacker - defender 像素坐标差转 axial 后归一化方向。
            // 站在 defender(玩家视角)看,敌人朝哪个 axial 方向来。
            var atkAxial = Map.HexOverworldTile.PixelToAxial(attacker.Position.X, attacker.Position.Y);
            var defAxial = Map.HexOverworldTile.PixelToAxial(defender.Position.X, defender.Position.Y);
            var dq = atkAxial.X - defAxial.X;
            var dr = atkAxial.Y - defAxial.Y;
            if (dq != 0 || dr != 0)
                context.ApproachDirection = new Vector2I(dq, dr);
        }

        // === 比例尺统一：从 POI preset 派生战斗规模 ===
        if (poi != null)
        {
            var preset = POIBattlePresetRegistry.Resolve(poi);
            context.Size = preset.OverrideBattleSize ?? POIScaleTable.Get(preset.Scale).BattleSize;
            context.PoiType = (int)poi.PoiTypeEnum;
        }

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
