// EncounterSpawner.cs
// 遭遇条件判定引擎 — 根据 chunk 条件生成遭遇槽位
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 遭遇类型枚举
/// </summary>
public enum EncounterType
{
    None,              // 无遭遇
    WildMonsters,      // 野生怪物
    HostilePatrol,     // 敌对巡逻队
    CaravanEvent,      // 商队事件
    Environmental,     // 环境事件（暴风雪/山崩等）
    ResourceNode,      // 资源点（草药/矿石/宝箱）
    Mystery,           // 悬念事件（特殊剧情）
}

/// <summary>
/// 遭遇数据 — 一个可触发的遭遇实例
/// </summary>
public class EncounterData
{
    /// <summary>遭遇所在的全局坐标</summary>
    public Vector2I WorldCoord = Vector2I.Zero;

    /// <summary>遭遇类型</summary>
    public EncounterType Type = EncounterType.None;

    /// <summary>遭遇等级（用于敌方队伍缩放）</summary>
    public int EncounterLevel = 1;

    /// <summary>敌方队伍人数</summary>
    public int PartySize = 1;

    /// <summary>敌人模板 ID 列表</summary>
    public List<string> EnemyTemplateIds = new();

    /// <summary>区域名称</summary>
    public string RegionName = "";

    /// <summary>地形类型</summary>
    public Map.HexOverworldTile.TerrainType Terrain = Map.HexOverworldTile.TerrainType.Plains;
}

/// <summary>
/// 遭遇生成器 — 根据 chunk 条件判定并生成遭遇
/// </summary>
[GlobalClass]
public partial class EncounterSpawner : RefCounted
{
    // ========================================
    // 遭遇概率配置
    // ========================================

    /// <summary>不同危险等级的基础遭遇概率</summary>
    private static readonly float[] DangerEncounterChance =
    [
        0.0f,   // 0.0 — 安全区（城镇附近）
        0.15f,  // 0.1
        0.25f,  // 0.2
        0.35f,  // 0.3
        0.45f,  // 0.4
        0.55f,  // 0.5
        0.65f,  // 0.6
        0.75f,  // 0.7
        0.85f,  // 0.8
    ];

    /// <summary>地形遭遇类型权重</summary>
    private static readonly Dictionary<Map.HexOverworldTile.TerrainType, (EncounterType type, float weight)[]> TerrainEncounters = new()
    {
        [Map.HexOverworldTile.TerrainType.Forest] = [
            (EncounterType.WildMonsters, 0.5f),
            (EncounterType.ResourceNode, 0.3f),
            (EncounterType.Mystery, 0.1f),
        ],
        [Map.HexOverworldTile.TerrainType.DenseForest] = [
            (EncounterType.WildMonsters, 0.6f),
            (EncounterType.ResourceNode, 0.25f),
            (EncounterType.Mystery, 0.15f),
        ],
        [Map.HexOverworldTile.TerrainType.Swamp] = [
            (EncounterType.WildMonsters, 0.55f),
            (EncounterType.Environmental, 0.25f),
            (EncounterType.Mystery, 0.2f),
        ],
        [Map.HexOverworldTile.TerrainType.Hills] = [
            (EncounterType.WildMonsters, 0.35f),
            (EncounterType.ResourceNode, 0.35f),
            (EncounterType.HostilePatrol, 0.15f),
        ],
        [Map.HexOverworldTile.TerrainType.Sand] = [
            (EncounterType.Environmental, 0.4f),
            (EncounterType.WildMonsters, 0.3f),
            (EncounterType.ResourceNode, 0.15f),
        ],
        [Map.HexOverworldTile.TerrainType.Savanna] = [
            (EncounterType.WildMonsters, 0.4f),
            (EncounterType.ResourceNode, 0.25f),
            (EncounterType.HostilePatrol, 0.2f),
        ],
        [Map.HexOverworldTile.TerrainType.Plains] = [
            (EncounterType.HostilePatrol, 0.3f),
            (EncounterType.CaravanEvent, 0.2f),
            (EncounterType.WildMonsters, 0.2f),
        ],
        [Map.HexOverworldTile.TerrainType.Grassland] = [
            (EncounterType.WildMonsters, 0.25f),
            (EncounterType.CaravanEvent, 0.25f),
            (EncounterType.ResourceNode, 0.2f),
        ],
        [Map.HexOverworldTile.TerrainType.Snow] = [
            (EncounterType.Environmental, 0.5f),
            (EncounterType.WildMonsters, 0.3f),
        ],
        [Map.HexOverworldTile.TerrainType.Taiga] = [
            (EncounterType.WildMonsters, 0.5f),
            (EncounterType.Environmental, 0.3f),
            (EncounterType.ResourceNode, 0.1f),
        ],
    };

    // ========================================
    // 主入口
    // ========================================

    /// <summary>
    /// 为一个新生成的 chunk 计算遭遇槽位
    /// </summary>
    /// <param name="chunk">目标 chunk</param>
    /// <param name="dangerLevel">区域危险等级 (0~1)</param>
    /// <param name="playerLevel">玩家平均等级</param>
    /// <param name="daysElapsed">已过天数</param>
    /// <param name="seed">确定性种子</param>
    public void PopulateEncounterSlots(Map.ChunkData chunk, float dangerLevel, int playerLevel, int daysElapsed, int seed)
    {
        var rng = new Random(seed ^ (chunk.ChunkCoord.X * 7919 + chunk.ChunkCoord.Y * 104729));

        foreach (var tile in chunk.Tiles.Values)
        {
            if (chunk.GetEncounterState(tile.Coord.X, tile.Coord.Y) != Map.EncounterSlotState.None)
                continue;

            // 不可通行的 tile 不生成遭遇
            if (!tile.IsPassable) continue;
            // 有定居点的 tile 不生成遭遇（安全区）
            if (tile.HasSettlement) continue;
            // 道路上的 tile 特殊处理（商队事件）
            if (tile.IsRoad) continue;

            // 计算遭遇概率
            float chance = GetEncounterChance(dangerLevel, tile.Terrain, daysElapsed);
            if (rng.NextDouble() > chance) continue;

            // 确定遭遇类型
            var encounterType = PickEncounterType(tile.Terrain, rng);
            if (encounterType == EncounterType.None) continue;

            // 设置遭遇槽位
            chunk.SetEncounterState(tile.Coord.X, tile.Coord.Y, Map.EncounterSlotState.Available);
        }
    }

    // ========================================
    // 遭遇概率计算
    // ========================================

    /// <summary>
    /// 计算单个 tile 的遭遇概率
    /// </summary>
    private float GetEncounterChance(float dangerLevel, Map.HexOverworldTile.TerrainType terrain, int daysElapsed)
    {
        // 危险等级基础概率
        int dangerIndex = (int)Math.Clamp(dangerLevel * 8, 0, 8);
        float baseChance = DangerEncounterChance[dangerIndex];

        // 地形修正
        float terrainMod = terrain switch
        {
            Map.HexOverworldTile.TerrainType.DeepWater => 0.0f,
            Map.HexOverworldTile.TerrainType.ShallowWater => 0.3f,
            Map.HexOverworldTile.TerrainType.River => 0.0f,
            Map.HexOverworldTile.TerrainType.Road => 0.5f, // 道路概率在 PopulateEncounterSlots 中跳过
            _ => 1.0f,
        };

        // 天数修正（越往后遭遇越多，但有上限）
        float dayMod = 1.0f + Math.Min(daysElapsed * 0.005f, 0.3f);

        return baseChance * terrainMod * dayMod;
    }

    // ========================================
    // 遭遇类型选择
    // ========================================

    private EncounterType PickEncounterType(Map.HexOverworldTile.TerrainType terrain, Random rng)
    {
        if (!TerrainEncounters.TryGetValue(terrain, out var weights))
        {
            // 默认：平原规则
            weights = [
                (EncounterType.WildMonsters, 0.3f),
                (EncounterType.HostilePatrol, 0.2f),
                (EncounterType.ResourceNode, 0.2f),
            ];
        }

        float totalWeight = 0f;
        foreach (var (_, w) in weights) totalWeight += w;

        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0f;

        foreach (var (type, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative) return type;
        }

        return EncounterType.WildMonsters; // fallback
    }

    // ========================================
    // 遭遇数据生成
    // ========================================

    /// <summary>
    /// 从遭遇数据创建战斗上下文 — 包含完整的部署数据
    /// </summary>
    public BattleContext CreateBattleContext(
        EncounterData encounter,
        OverworldEntity playerParty,
        OverworldEntity? defender = null,
        OverworldPOI? poi = null)
    {
        if (encounter == null || playerParty == null) return new BattleContext();

        var context = BattleContext.CreateFromEncounter(
            attacker: playerParty,
            defender: defender,
            poi: poi,
            grid: null,
            coord: new Vector2I(encounter.WorldCoord.X, encounter.WorldCoord.Y)
        );

        // Set encounter-specific data
        context.EncounterCoord = new Vector2I(encounter.WorldCoord.X, encounter.WorldCoord.Y);

        return context;
    }

    /// <summary>
    /// 为一个可触发的遭遇生成详细数据
    /// </summary>
    public EncounterData BuildEncounter(Vector2I worldCoord, Map.HexOverworldTile tile, int playerLevel, float dangerLevel)
    {
        var data = new EncounterData
        {
            WorldCoord = worldCoord,
            RegionName = tile.RegionName,
            Terrain = tile.Terrain,
        };

        // 确定遭遇类型（从槽位反推或重新生成）
        var rng = new Random(worldCoord.X * 31 + worldCoord.Y * 37);

        if (!TerrainEncounters.TryGetValue(tile.Terrain, out var weights))
        {
            weights = [(EncounterType.WildMonsters, 1.0f)];
        }

        data.Type = PickEncounterType(tile.Terrain, rng);

        // 遭遇等级 = 玩家等级 ± 1 + 区域危险修正
        data.EncounterLevel = Math.Clamp(
            playerLevel + (rng.Next(-1, 2)) + (int)(dangerLevel * 3),
            1, 120
        );

        // 队伍人数
        data.PartySize = data.Type switch
        {
            EncounterType.WildMonsters => 2 + data.EncounterLevel / 5,
            EncounterType.HostilePatrol => 3 + data.EncounterLevel / 4,
            EncounterType.CaravanEvent => 2 + rng.Next(3),
            EncounterType.Environmental => 0,
            EncounterType.ResourceNode => 0,
            EncounterType.Mystery => 1 + rng.Next(3),
            _ => 1,
        };

        // 敌人模板
        data.EnemyTemplateIds = GenerateEnemyTemplates(data.Type, data.EncounterLevel, tile);

        return data;
    }

    /// <summary>
    /// 为一个可触发的遭遇生成详细数据，并同时创建战斗上下文（含完整部署数据）
    /// </summary>
    public EncounterData BuildEncounter(
        Vector2I worldCoord,
        Map.HexOverworldTile tile,
        int playerLevel,
        float dangerLevel,
        OverworldEntity playerParty,
        OverworldEntity? defender,
        OverworldPOI? poi,
        out BattleContext battleContext)
    {
        var data = BuildEncounter(worldCoord, tile, playerLevel, dangerLevel);

        // 创建战斗上下文
        battleContext = CreateBattleContext(data, playerParty, defender, poi);

        // 从 encounter 覆盖部分上下文字段
        battleContext.EncounterCoord = new Vector2I(worldCoord.X, worldCoord.Y);
        battleContext.Terrain = tile.Terrain;

        return data;
    }

    /// <summary>
    /// 根据遭遇类型和等级生成敌人模板列表
    /// </summary>
    private List<string> GenerateEnemyTemplates(EncounterType type, int level, Map.HexOverworldTile tile)
    {
        var templates = new List<string>();

        switch (type)
        {
            case EncounterType.WildMonsters:
                templates.AddRange(tile.Terrain switch
                {
                    Map.HexOverworldTile.TerrainType.Forest or Map.HexOverworldTile.TerrainType.DenseForest
                        => ["wolf", "bandit", "treant"],
                    Map.HexOverworldTile.TerrainType.Swamp => ["lizardman", "swamp_beast", "will_o_wisp"],
                    Map.HexOverworldTile.TerrainType.Hills => ["harpy", "ogre", "rock_elemental"],
                    Map.HexOverworldTile.TerrainType.Snow or Map.HexOverworldTile.TerrainType.Taiga
                        => ["ice_wolf", "frost_sprite", "yeti"],
                    _ => ["goblin_warrior", "bandit", "wild_boar"],
                });
                break;

            case EncounterType.HostilePatrol:
                templates.AddRange(["goblin_warrior", "goblin_archer", "kobold_trapper"]);
                break;

            case EncounterType.CaravanEvent:
                templates.Add("caravan_guard");
                break;

            case EncounterType.Mystery:
                templates.Add("unknown_entity");
                break;
        }

        return templates;
    }
}
