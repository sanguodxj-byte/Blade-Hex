// BattleOverworldFactory.cs
// 战斗用小型大地图工厂 — 统一架构的核心桥梁
//
// 设计目标：
//   - 消除 GenerateFromTemplate 路径，所有战斗地图统一走 GenerateFromOverworld pipeline
//   - 战斗模板的概念从"直接控制战斗地形分布"变为"控制小型大地图的生成参数"
//   - 通过 elevation/moisture/temperature 偏置 + 线性特征注入，让小型大地图产出期望的地形组合
//   - 小型大地图经过完整的 BiomeRules 决策，保证地形分布的自然合理性
//
// 用法：
//   var (grid, coord) = BattleOverworldFactory.Create("forest_ambush", BattleContext.BattleSize.Knight, seed);
//   var ctx = new BattleContext { OverworldGrid = grid, EncounterCoord = coord, ... };
//   var mapData = generator.Generate(ctx);
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Map.Generation;

/// <summary>
/// 战斗模板的大地图生成参数 — 控制小型大地图的噪声偏置和线性特征
/// </summary>
public sealed class BattleOverworldPreset
{
    /// <summary>模板名（与旧 BattleMapTemplate.TemplateName 一致，保持向后兼容）</summary>
    public string Name = "";

    /// <summary>高程偏置 [-0.5, 0.5]：正值 = 多山/丘陵，负值 = 多低地/水域</summary>
    public float ElevationBias = 0.0f;

    /// <summary>湿度偏置 [-0.5, 0.5]：正值 = 多森林/沼泽，负值 = 多沙漠/荒原</summary>
    public float MoistureBias = 0.0f;

    /// <summary>温度偏置 [-0.5, 0.5]：正值 = 炎热(丛林/沙漠)，负值 = 寒冷(针叶林/雪地)</summary>
    public float TemperatureBias = 0.0f;

    /// <summary>是否注入道路（横穿地图）</summary>
    public bool InjectRoad = false;

    /// <summary>是否注入河流</summary>
    public bool InjectRiver = false;

    /// <summary>环境事件（可为空）</summary>
    public string EnvironmentEvent = "";

    /// <summary>天气覆盖（可为空）</summary>
    public string? WeatherOverride = null;

    /// <summary>高程噪声频率倍率（默认1.0，越大地形越碎）</summary>
    public float ElevationFreqScale = 1.0f;

    /// <summary>湿度噪声频率倍率</summary>
    public float MoistureFreqScale = 1.0f;

    /// <summary>是否为据点类型（需要生成城墙结构）</summary>
    public bool IsStronghold = false;

    /// <summary>POI 类型（-1 = 非 POI）</summary>
    public int PoiType = -1;
}

/// <summary>
/// 战斗用小型大地图工厂 — 根据模板 preset 生成一个小型大地图供 GenerateFromOverworld 消费
/// </summary>
public static class BattleOverworldFactory
{
    // 小型大地图尺寸：根据战斗规模（采样圈数）决定
    // K=0 → 只采样 1 格，但小地图仍需 3×3 保证有邻居供 Voronoi 使用
    // K=1 → 采样 7 格，小地图 5×5 足够
    // K=2 → 采样 19 格，小地图 7×7
    // K=3 → 采样 37 格，小地图需要足够大让中心外扩 3 圈不越界
    private static readonly Dictionary<BattleContext.BattleSize, (int w, int h)> GridSizeMap = new()
    {
        { BattleContext.BattleSize.Mercenary, (4, 4) },    // K=0, 单格 + 最小邻居
        { BattleContext.BattleSize.Knight, (6, 5) },       // K=1, 7 格采样
        { BattleContext.BattleSize.Lord, (8, 7) },         // K=2, 19 格采样
        { BattleContext.BattleSize.Stronghold, (14, 12) }, // K=3, 37 格采样（需要足够大避免越界）
    };

    /// <summary>
    /// 从模板名创建小型大地图 + 遭遇坐标
    /// </summary>
    /// <returns>(grid, encounterCoord) 元组</returns>
    public static (HexOverworldGrid grid, Vector2I encounterCoord) Create(
        string templateName,
        BattleContext.BattleSize size,
        int seed)
    {
        var preset = GetPreset(templateName);
        return CreateFromPreset(preset, size, seed);
    }

    /// <summary>
    /// 从 preset 创建小型大地图 + 遭遇坐标
    /// </summary>
    public static (HexOverworldGrid grid, Vector2I encounterCoord) CreateFromPreset(
        BattleOverworldPreset preset,
        BattleContext.BattleSize size,
        int seed)
    {
        var (w, h) = GridSizeMap.GetValueOrDefault(size, (8, 6));

        // 委托给 HexOverworldGenerator 的战斗专用重载 — 复用核心管线
        var generator = new HexOverworldGenerator();
        var battleParams = new HexOverworldGenerator.BattleGridParams
        {
            ElevationBias = preset.ElevationBias,
            MoistureBias = preset.MoistureBias,
            TemperatureBias = preset.TemperatureBias,
            InjectRoad = preset.InjectRoad,
            InjectRiver = preset.InjectRiver,
            ElevationFreqScale = preset.ElevationFreqScale,
            MoistureFreqScale = preset.MoistureFreqScale,
        };

        var grid = generator.GenerateForBattle(w, h, seed, battleParams);

        // 遭遇坐标：取中心附近的可通行格
        var center = new Vector2I(w / 2 - h / 4, h / 2); // odd-r offset → axial 的中心近似
        var encounterCoord = FindPassableNearCenter(grid, center);

        // 据点类型：在中心放置城堡 POI 标记（让 OverworldSampler 能采样到 POI footprint）
        if (preset.IsStronghold)
        {
            var centerTile = grid.GetTileAtCoord(encounterCoord);
            if (centerTile != null)
            {
                centerTile.HasSettlement = true;
                centerTile.SettlementType = preset.PoiType;
                centerTile.PoiId = $"siege_poi_{preset.Name}";
                centerTile.IsPoiCenter = true;
                // 标记中心 + 1 圈邻居为 POI footprint（模拟 7 hex 城堡）
                foreach (var nb in grid.GetNeighbors(encounterCoord.X, encounterCoord.Y))
                {
                    nb.PoiId = $"siege_poi_{preset.Name}";
                }
            }
        }

        return (grid, encounterCoord);
    }

    /// <summary>
    /// 构建完整的 BattleContext（便捷方法）
    /// </summary>
    public static BattleContext CreateContext(
        string templateName,
        BattleContext.BattleSize size,
        int seed,
        BattleContext.EngagementType engagement = BattleContext.EngagementType.Normal)
    {
        var preset = GetPreset(templateName);
        var (grid, coord) = CreateFromPreset(preset, size, seed);

        var tile = grid.GetTileAtCoord(coord);
        var terrain = tile?.Terrain ?? HexOverworldTile.TerrainType.Plains;

        var ctx = new BattleContext
        {
            Terrain = terrain,
            Size = size,
            Engagement = engagement,
            Seed = seed,
            OverworldGrid = grid,
            EncounterCoord = coord,
            PoiType = preset.PoiType,
            EnvironmentOverride = preset.EnvironmentEvent,
            WeatherOverride = preset.WeatherOverride,
        };

        // 据点类型：设置默认攻击方向（从地图左下方进攻）
        if (preset.IsStronghold)
        {
            ctx.ApproachDirection = new Vector2I(-1, 1); // 攻方从西南来
        }

        return ctx;
    }

    // ========================================================================
    // Preset 注册表
    // ========================================================================

    private static readonly Dictionary<string, BattleOverworldPreset> _presets = BuildPresets();

    public static BattleOverworldPreset GetPreset(string name)
    {
        return _presets.GetValueOrDefault(name) ?? _presets["plain_field"];
    }

    public static string[] GetPresetNames()
    {
        var names = new string[_presets.Count];
        _presets.Keys.CopyTo(names, 0);
        return names;
    }

    private static Dictionary<string, BattleOverworldPreset> BuildPresets()
    {
        var d = new Dictionary<string, BattleOverworldPreset>();

        // ========== 野外遭遇模板 ==========

        // 平原旷野：温带偏干 → Plains/Grassland 为主
        d["plain_field"] = new BattleOverworldPreset
        {
            Name = "plain_field",
            ElevationBias = 0.0f,       // 中性高程
            MoistureBias = -0.15f,      // 偏干（BiomeRules: 温带 moisture<0.32 → Plains）
            TemperatureBias = 0.05f,    // 温带
            InjectRoad = true,
        };

        // 森林伏击：温带高湿度 → 森林/密林为主
        d["forest_ambush"] = new BattleOverworldPreset
        {
            Name = "forest_ambush",
            ElevationBias = 0.0f,       // 平地（避免丘陵）
            MoistureBias = 0.25f,       // 高湿度（BiomeRules: 温带 midMoisture>0.7 → Forest）
            TemperatureBias = 0.0f,     // 温带中间
            EnvironmentEvent = "fog",
        };

        // 山间隘口：高高程，中等湿度
        d["mountain_pass"] = new BattleOverworldPreset
        {
            Name = "mountain_pass",
            ElevationBias = 0.25f,      // 高海拔 → 丘陵/山地
            MoistureBias = -0.10f,
            TemperatureBias = -0.10f,   // 略冷
            InjectRoad = true,
            EnvironmentEvent = "earthquake",
            ElevationFreqScale = 1.5f,  // 地形更碎 → 更多高低起伏
        };

        // 沼泽遭遇：低高程，高湿度，温暖
        d["swamp_battle"] = new BattleOverworldPreset
        {
            Name = "swamp_battle",
            ElevationBias = -0.05f,     // 略低洼但不至于全水域
            MoistureBias = 0.30f,       // 高湿度 → 沼泽（BiomeRules: temp>0.55 + moist>0.62 = Swamp）
            TemperatureBias = 0.15f,    // 温暖 → Swamp 而非 Bog/DenseForest
            InjectRiver = true,
            EnvironmentEvent = "poison_fog",
        };

        // 海岸伏击：低高程 → 有浅水/沙滩
        d["coastal_ambush"] = new BattleOverworldPreset
        {
            Name = "coastal_ambush",
            ElevationBias = -0.20f,     // 低海拔 → 水域/沙滩（BiomeRules: elev<0.31 → Sand/Water）
            MoistureBias = -0.05f,      // 中性
            TemperatureBias = 0.20f,    // 温暖（Sand 需要 temp>ColdThreshold）
            EnvironmentEvent = "storm",
        };

        // 沙漠冲突：中等高程，极低湿度，炎热
        d["desert_skirmish"] = new BattleOverworldPreset
        {
            Name = "desert_skirmish",
            ElevationBias = 0.10f,
            MoistureBias = -0.30f,      // 极干 → 沙漠/荒原
            TemperatureBias = 0.25f,    // 炎热
        };

        // ========== 怪物巢穴模板 ==========

        // 巨龙巢穴：高山，干燥
        d["dragon_lair"] = new BattleOverworldPreset
        {
            Name = "dragon_lair",
            ElevationBias = 0.30f,
            MoistureBias = -0.20f,
            TemperatureBias = 0.0f,
            EnvironmentEvent = "lava_surge",
            ElevationFreqScale = 1.8f,
        };

        // 远古墓穴：低洼，湿润
        d["ancient_tomb"] = new BattleOverworldPreset
        {
            Name = "ancient_tomb",
            ElevationBias = -0.05f,
            MoistureBias = 0.10f,
            TemperatureBias = 0.0f,
            EnvironmentEvent = "poison_fog",
        };

        // 哥布林营地：温带森林边缘
        d["goblin_camp"] = new BattleOverworldPreset
        {
            Name = "goblin_camp",
            ElevationBias = 0.05f,
            MoistureBias = 0.10f,
            TemperatureBias = 0.0f,
        };

        // 狗头人矿坑：山地，有水
        d["kobold_mine"] = new BattleOverworldPreset
        {
            Name = "kobold_mine",
            ElevationBias = 0.20f,
            MoistureBias = 0.05f,
            TemperatureBias = -0.05f,
            InjectRiver = true,
            EnvironmentEvent = "earthquake",
        };

        // 牛头人要塞：丘陵，干燥
        d["minotaur_fortress"] = new BattleOverworldPreset
        {
            Name = "minotaur_fortress",
            ElevationBias = 0.20f,
            MoistureBias = -0.15f,
            TemperatureBias = 0.05f,
        };

        // 暗影教团据点：沼泽/密林
        d["shadow_cult_hideout"] = new BattleOverworldPreset
        {
            Name = "shadow_cult_hideout",
            ElevationBias = -0.05f,
            MoistureBias = 0.20f,
            TemperatureBias = 0.05f,
            EnvironmentEvent = "poison_fog",
        };

        // 村庄防御：平坦，有道路
        d["village_defense"] = new BattleOverworldPreset
        {
            Name = "village_defense",
            ElevationBias = 0.05f,
            MoistureBias = 0.0f,
            TemperatureBias = 0.0f,
            InjectRoad = true,
        };

        // 遗迹探索：中等高程
        d["ruins_exploration"] = new BattleOverworldPreset
        {
            Name = "ruins_exploration",
            ElevationBias = 0.10f,
            MoistureBias = 0.0f,
            TemperatureBias = 0.0f,
        };

        // 魔像工坊：山地，干燥
        d["golem_forge"] = new BattleOverworldPreset
        {
            Name = "golem_forge",
            ElevationBias = 0.25f,
            MoistureBias = -0.15f,
            TemperatureBias = 0.0f,
            EnvironmentEvent = "lava_surge",
        };

        // ========== 据点模板 ==========

        d["town_defense"] = new BattleOverworldPreset
        {
            Name = "town_defense",
            ElevationBias = 0.05f,
            MoistureBias = 0.0f,
            TemperatureBias = 0.0f,
            InjectRoad = true,
            IsStronghold = true,
            PoiType = 0,
        };

        d["castle_siege"] = new BattleOverworldPreset
        {
            Name = "castle_siege",
            ElevationBias = 0.05f,      // 略高于平地，但不至于生成山地
            MoistureBias = -0.05f,
            TemperatureBias = 0.0f,
            InjectRoad = true,
            IsStronghold = true,
            PoiType = 1,
        };

        // 守城战（玩家在城内防守）— 与攻城战相同地形，AttackingSide 由调用方设置
        d["castle_defense"] = new BattleOverworldPreset
        {
            Name = "castle_defense",
            ElevationBias = 0.05f,
            MoistureBias = -0.05f,
            TemperatureBias = 0.0f,
            InjectRoad = true,
            IsStronghold = true,
            PoiType = 1,
        };

        d["bandit_stronghold"] = new BattleOverworldPreset
        {
            Name = "bandit_stronghold",
            ElevationBias = 0.10f,
            MoistureBias = 0.15f,
            TemperatureBias = 0.0f,
            IsStronghold = true,
            PoiType = 2,
        };

        d["pirate_cove"] = new BattleOverworldPreset
        {
            Name = "pirate_cove",
            ElevationBias = -0.10f,
            MoistureBias = 0.05f,
            TemperatureBias = 0.15f,
            EnvironmentEvent = "storm",
            IsStronghold = true,
            PoiType = 3,
        };

        d["goblin_stronghold"] = new BattleOverworldPreset
        {
            Name = "goblin_stronghold",
            ElevationBias = 0.0f,
            MoistureBias = 0.15f,
            TemperatureBias = 0.0f,
            IsStronghold = true,
            PoiType = 4,
        };

        d["kobold_stronghold"] = new BattleOverworldPreset
        {
            Name = "kobold_stronghold",
            ElevationBias = 0.20f,
            MoistureBias = 0.0f,
            TemperatureBias = -0.05f,
            EnvironmentEvent = "earthquake",
            IsStronghold = true,
            PoiType = 5,
        };

        d["minotaur_stronghold"] = new BattleOverworldPreset
        {
            Name = "minotaur_stronghold",
            ElevationBias = 0.20f,
            MoistureBias = -0.10f,
            TemperatureBias = 0.05f,
            IsStronghold = true,
            PoiType = 6,
        };

        d["shadow_cult_temple"] = new BattleOverworldPreset
        {
            Name = "shadow_cult_temple",
            ElevationBias = -0.05f,
            MoistureBias = 0.20f,
            TemperatureBias = 0.05f,
            EnvironmentEvent = "poison_fog",
            IsStronghold = true,
            PoiType = 7,
        };

        d["raider_outpost"] = new BattleOverworldPreset
        {
            Name = "raider_outpost",
            ElevationBias = 0.10f,
            MoistureBias = -0.05f,
            TemperatureBias = 0.0f,
            IsStronghold = true,
            PoiType = 8,
        };

        return d;
    }

    // ========================================================================
    // 内部辅助
    // ========================================================================

    /// <summary>在中心附近找可通行格</summary>
    private static Vector2I FindPassableNearCenter(HexOverworldGrid grid, Vector2I center)
    {
        var tile = grid.GetTile(center.X, center.Y);
        if (tile != null && tile.IsPassable && !tile.IsRiver)
            return center;

        // BFS 找最近可通行格
        for (int radius = 1; radius <= 4; radius++)
        {
            var ring = HexOverworldTile.HexRing(center.X, center.Y, radius);
            foreach (var coord in ring)
            {
                var t = grid.GetTile(coord.X, coord.Y);
                if (t != null && t.IsPassable && !t.IsRiver)
                    return coord;
            }
        }

        // 兜底：返回中心
        return center;
    }
}
