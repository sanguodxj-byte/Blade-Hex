// BattleMapGenerator.cs — Part 1: Types + Templates + Public API
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Map;

[GlobalClass]
public partial class BattleMapGenerator : RefCounted
{
    // Use BattleContext.BattleSize directly — same enum values
    public enum BattleSize { Mercenary, Knight, Lord, Stronghold }
    public static BattleSize ToLocalBS(BattleContext.BattleSize bs) => (BattleSize)(int)bs;

    public class BattleMapTemplate
    {
        public string TemplateName = "";
        public BattleCellData.TerrainType PrimaryTerrain = BattleCellData.TerrainType.Plains;
        public float PrimaryWeight = 0.55f;
        public Dictionary<BattleCellData.TerrainType, float> SecondaryTerrains = new();
        public bool HasRiver, HasRoad;
        public float ElevationBias;
        public List<(BattleCellData.TerrainType Type, float Probability)> SpecialFeatures = new();
        public string EnvironmentEvent = "";
        public Vector2I TreeClusterCount = new(2, 5), TreeClusterRadius = new(2, 4);
        public float DenseTreeCoreChance = 0.3f;
        public Vector2I RuinStructureCount = new(1, 3);
        public Vector2I WallSegmentCount = new(0, 2), WallSegmentLength = new(2, 5);
        public int SmoothingPasses = 2;
    }

    public class BattleMapData
    {
        public int Width = 12, Height = 10;
        /// <summary>
        /// 六边形战斗地图的半径 N。当 &gt; 0 时，使用六边形布局；否则用 W×H 矩形布局。
        /// 总 cell 数 = 1 + 3·N·(N+1)。
        /// </summary>
        public int HexRadius = 0;
        public Godot.Collections.Dictionary Cells = new();
        public Godot.Collections.Array PlayerDeployment = new();
        public Godot.Collections.Array EnemyDeployment = new();
        public string EnvironmentEvent = "", TemplateName = "";
        // Backward compat alias for existing callers using snake_case
        public Godot.Collections.Dictionary cells { get => Cells; set => Cells = value; }
        public Godot.Collections.Array player_deployment { get => PlayerDeployment; set => PlayerDeployment = value; }
        public Godot.Collections.Array enemy_deployment { get => EnemyDeployment; set => EnemyDeployment = value; }

        /// <summary>枚举本 BattleMap 所有 axial 坐标（六边形或矩形）</summary>
        public IEnumerable<Vector2I> IterateCoords()
        {
            if (HexRadius > 0)
            {
                int n = HexRadius;
                for (int q = -n; q <= n; q++)
                {
                    int r1 = System.Math.Max(-n, -q - n);
                    int r2 = System.Math.Min(n, -q + n);
                    for (int r = r1; r <= r2; r++)
                        yield return new Vector2I(q, r);
                }
            }
            else
            {
                for (int q = 0; q < Width; q++)
                {
                    int qOff = Mathf.FloorToInt(q / 2.0f);
                    for (int r = -qOff; r < Height - qOff; r++)
                        yield return new Vector2I(q, r);
                }
            }
        }

        /// <summary>判断坐标是否在本 BattleMap 范围内</summary>
        public bool ContainsCoord(Vector2I coord)
        {
            if (HexRadius > 0)
            {
                int dist = (System.Math.Abs(coord.X) + System.Math.Abs(coord.Y) + System.Math.Abs(coord.X + coord.Y)) / 2;
                return dist <= HexRadius;
            }
            else
            {
                if (coord.X < 0 || coord.X >= Width) return false;
                int qOff = Mathf.FloorToInt(coord.X / 2.0f);
                return coord.Y >= -qOff && coord.Y < Height - qOff;
            }
        }
    }

    static readonly Dictionary<BattleSize, Vector2I> SMap = new()
    {
        { BattleSize.Mercenary, new(15, 10) },
        { BattleSize.Knight, new(18, 12) },
        { BattleSize.Lord, new(24, 16) },
        { BattleSize.Stronghold, new(30, 20) },
    };

    /// <summary>六边形战斗地图半径 N（cell 数 = 1+3·N·(N+1)）</summary>
    /// <remarks>
    /// 与 docs/31-比例尺与距离体系.md §五 对齐（SSOT）。
    /// 1 大地图 hex = 250m, 1 战斗 cell = 20m, 两者不是 1:1 物理映射。
    /// </remarks>
    static readonly Dictionary<BattleSize, int> RadiusMap = new()
    {
        { BattleSize.Mercenary, 7 },   // 169 cells, 300m — Tiny POI / 野外小遭遇
        { BattleSize.Knight,    11 },  // 397 cells, 460m — Medium POI / 城镇外围
        { BattleSize.Lord,      14 },  // 631 cells, 580m — Large POI / 大型会战
        { BattleSize.Stronghold, 14 }, // 631 cells, 580m — 据点攻城（与 Large 同尺寸，结构更密集）
    };

    /// <summary>
    /// 战斗规模 → 大地图采样圈数 K 的映射。
    /// 战斗地图大小完全由大地图采样范围决定：
    ///   Mercenary = 单格 hex (K=0, 1 sample)
    ///   Knight    = 7 格 (K=1, 中心+1圈邻居)
    ///   Lord      = 19 格 (K=2, 多一圈)
    ///   Stronghold= 37 格 (K=3, 多两圈)
    /// </summary>
    static readonly Dictionary<BattleSize, int> SamplingRingMap = new()
    {
        { BattleSize.Mercenary, 0 },
        { BattleSize.Knight,    1 },
        { BattleSize.Lord,      2 },
        { BattleSize.Stronghold, 3 },
    };

    /// <summary>
    /// Feature flag — 是否使用六边形战斗地图。
    /// 默认 true（design 钉死），需要回退到矩形可设为 false。
    /// </summary>
    public static bool UseHexagonalShape = true;

    readonly Dictionary<string, BattleMapTemplate> _templates = new();

    public BattleMapGenerator() { RegisterTemplates(); }
    public string[] GetTemplateNames() => Generation.BattleOverworldFactory.GetPresetNames();
    public BattleMapTemplate? GetTemplate(string n) => _templates.GetValueOrDefault(n);

    /// <summary>获取所有可用的战斗地图 preset 名称（静态便捷方法）</summary>
    public static string[] GetAvailablePresetNames() => Generation.BattleOverworldFactory.GetPresetNames();

    /// <summary>获取模板中文显示名</summary>
    public static string GetTemplateDisplayName(string templateName) => templateName switch
    {
        "plain_field" => "平原旷野",
        "forest_ambush" => "森林伏击",
        "mountain_pass" => "山间隘口",
        "swamp_battle" => "沼泽遭遇",
        "coastal_ambush" => "海岸伏击",
        "desert_skirmish" => "沙漠冲突",
        "dragon_lair" => "巨龙巢穴",
        "ancient_tomb" => "远古墓穴",
        "goblin_camp" => "哥布林营地",
        "kobold_mine" => "狗头人矿坑",
        "minotaur_fortress" => "牛头人要塞",
        "shadow_cult_hideout" => "暗影教团据点",
        "village_defense" => "村庄防御",
        "ruins_exploration" => "遗迹探索",
        "golem_forge" => "魔像工坊",
        "town_defense" => "城镇防御战",
        "castle_siege" => "攻城战",
        "castle_defense" => "守城战",
        "bandit_stronghold" => "山贼据点",
        "pirate_cove" => "海寇巢穴",
        "goblin_stronghold" => "哥布林大营",
        "kobold_stronghold" => "狗头人矿坑据点",
        "minotaur_stronghold" => "牛头人石堡",
        "shadow_cult_temple" => "暗影教团祭坛",
        "raider_outpost" => "劫掠队前哨",
        _ => templateName,
    };

    // shorthand
    static BattleCellData.TerrainType TT(int v) => (BattleCellData.TerrainType)v;
    static BattleCellData.TerrainType G => BattleCellData.TerrainType.Grassland;
    static BattleCellData.TerrainType P => BattleCellData.TerrainType.Plains;

    void AddTpl(string name, BattleCellData.TerrainType primary, float pw,
        Dictionary<BattleCellData.TerrainType, float> sec, float bias = 0, string env = "",
        bool road = false, bool river = false, Vector2I tcc = default, Vector2I tcr = default,
        float dtcc = 0.3f, Vector2I rsc = default, Vector2I wsc = default, Vector2I wsl = default,
        int sp = 2, List<(BattleCellData.TerrainType, float)>? sf = null)
    {
        if (tcc == default) tcc = new Vector2I(2, 5);
        if (tcr == default) tcr = new Vector2I(2, 4);
        if (rsc == default) rsc = new Vector2I(1, 3);
        if (wsc == default) wsc = new Vector2I(0, 2);
        if (wsl == default) wsl = new Vector2I(2, 5);
        _templates[name] = new BattleMapTemplate
        {
            TemplateName = name, PrimaryTerrain = primary, PrimaryWeight = pw,
            SecondaryTerrains = sec, ElevationBias = bias, EnvironmentEvent = env,
            HasRoad = road, HasRiver = river,
            TreeClusterCount = tcc, TreeClusterRadius = tcr, DenseTreeCoreChance = dtcc,
            RuinStructureCount = rsc, WallSegmentCount = wsc, WallSegmentLength = wsl,
            SmoothingPasses = sp, SpecialFeatures = sf ?? new(),
        };
    }

    void RegisterTemplates()
    {
        var F = BattleCellData.TerrainType.Forest;
        var DF = BattleCellData.TerrainType.DenseForest;
        var H = BattleCellData.TerrainType.Hills;
        var M = BattleCellData.TerrainType.Mountain;
        var SW = BattleCellData.TerrainType.ShallowWater;
        var DW = BattleCellData.TerrainType.DeepWater;
        var S = BattleCellData.TerrainType.Sand;
        var Sn = BattleCellData.TerrainType.Snow;
        var Sv = BattleCellData.TerrainType.Savanna;
        var R = BattleCellData.TerrainType.Road;
        var Ru = BattleCellData.TerrainType.Ruins;
        var W = BattleCellData.TerrainType.Wall;
        var Sw = BattleCellData.TerrainType.Swamp;
        var PM = BattleCellData.TerrainType.PoisonMushroom;

        AddTpl("plain_field", G, 0.50f, new() { {P,.20f},{Sv,.15f},{H,.10f},{R,.05f} },
            road: true, tcc:new(2,4), tcr:new(2,3), dtcc:.15f, rsc:new(0,1), wsc:new(0,1), wsl:new(2,3),
            sf: new());

        AddTpl("forest_ambush", F, 0.45f, new() { {DF,.20f},{G,.15f},{H,.10f},{P,.10f} },
            bias:.1f, env:"fog", tcc:new(4,8), tcr:new(3,5), dtcc:.50f, rsc:new(0,1),
            sf: new() { (PM, .02f) });

        AddTpl("mountain_pass", H, 0.40f, new() { {M,.20f},{P,.15f},{Sn,.10f},{Ru,.05f},{G,.10f} },
            bias:.4f, env:"earthquake", road:true, tcc:new(1,3), tcr:new(1,3), dtcc:.10f,
            rsc:new(1,3), wsc:new(1,3), sp:1, sf: new() { (Ru, .05f) });

        AddTpl("swamp_battle", Sw, 0.40f, new() { {SW,.20f},{G,.15f},{P,.10f},{H,.05f},{DF,.10f} },
            bias:-.3f, env:"poison_fog", river:true, tcc:new(2,5), tcr:new(2,4), dtcc:.35f,
            rsc:new(0,1), sp:3, sf: new() { (PM, .06f) });

        AddTpl("coastal_ambush", S, 0.35f, new() { {SW,.15f},{G,.20f},{P,.15f},{DW,.05f},{Sv,.10f} },
            bias:-.2f, env:"storm", tcc:new(1,3), tcr:new(2,3), dtcc:.10f, rsc:new(0,2), sp:2,
            sf: new());

        AddTpl("desert_skirmish", S, 0.50f, new() { {P,.20f},{H,.15f},{Sv,.10f},{Ru,.05f} },
            bias:.1f, tcc:new(0,1), tcr:new(1,2), dtcc:0, rsc:new(1,3), wsc:new(1,2), wsl:new(2,4), sp:1,
            sf: new() { (Ru, .04f) });

        AddTpl("dragon_lair", M, 0.30f, new() { {Ru,.20f},{H,.15f},{P,.10f},{SW,.10f},{S,.10f},{W,.05f} },
            bias:.5f, env:"lava_surge", tcc:new(0,0), tcr:new(1,2), dtcc:0,
            rsc:new(1,2), wsc:new(1,3), wsl:new(3,6), sp:0,
            sf: new() { (DW, .06f) });

        AddTpl("ancient_tomb", Ru, 0.35f, new() { {W,.20f},{P,.15f},{SW,.10f},{H,.10f},{F,.05f},{PM,.05f} },
            bias:-.2f, env:"poison_fog", tcc:new(0,1), tcr:new(1,2), dtcc:.05f,
            rsc:new(2,4), wsc:new(2,4), wsl:new(3,6), sp:1,
            sf: new() { (PM, .05f), (W, .08f) });

        AddTpl("goblin_camp", G, 0.30f, new() { {F,.20f},{P,.15f},{Ru,.15f},{H,.10f},{PM,.05f},{Sv,.05f} },
            bias:-.1f, tcc:new(2,4), tcr:new(2,3), dtcc:.15f, rsc:new(1,3), wsc:new(0,2), wsl:new(2,4), sp:2,
            sf: new() { (PM, .06f), (Ru, .08f) });

        AddTpl("kobold_mine", Ru, 0.30f, new() { {W,.25f},{P,.15f},{H,.10f},{SW,.10f},{F,.05f},{PM,.05f} },
            bias:-.3f, env:"earthquake", river:true, tcc:new(0,0), tcr:new(1,2), dtcc:0,
            rsc:new(2,4), wsc:new(2,5), wsl:new(3,7), sp:0,
            sf: new() { (W, .10f), (PM, .04f) });

        AddTpl("minotaur_fortress", H, 0.30f, new() { {Ru,.20f},{P,.15f},{S,.15f},{W,.10f},{Sv,.10f} },
            bias:.3f, tcc:new(0,2), tcr:new(1,2), dtcc:.05f,
            rsc:new(2,4), wsc:new(1,3), wsl:new(3,6), sp:1,
            sf: new() { (Ru, .08f) });

        AddTpl("shadow_cult_hideout", Ru, 0.30f, new() { {W,.15f},{P,.15f},{Sw,.15f},{F,.10f},{PM,.10f},{DF,.05f} },
            bias:-.2f, env:"poison_fog", tcc:new(1,3), tcr:new(2,3), dtcc:.20f,
            rsc:new(2,3), wsc:new(2,4), wsl:new(2,5), sp:2,
            sf: new() { (PM, .08f) });

        AddTpl("village_defense", G, 0.35f, new() { {P,.20f},{R,.15f},{Ru,.10f},{Sv,.10f},{H,.05f},{F,.05f} },
            road:true, tcc:new(2,4), tcr:new(2,3), dtcc:.10f, rsc:new(1,3), wsc:new(1,2), wsl:new(2,4), sp:2,
            sf: new());

        AddTpl("ruins_exploration", Ru, 0.35f, new() { {W,.20f},{P,.15f},{H,.10f},{F,.10f},{SW,.05f} },
            tcc:new(0,2), tcr:new(1,2), dtcc:.10f, rsc:new(2,5), wsc:new(2,5), wsl:new(3,7), sp:1,
            sf: new() { (W, .06f) });

        AddTpl("golem_forge", Ru, 0.30f, new() { {W,.20f},{P,.15f},{M,.10f},{SW,.10f},{H,.10f},{S,.05f} },
            bias:.2f, env:"lava_surge", tcc:new(0,0), tcr:new(1,2), dtcc:0,
            rsc:new(2,4), wsc:new(2,5), wsl:new(3,6), sp:0,
            sf: new() { (W, .08f), (SW, .06f) });

        // ========================================
        // 据点模板 — 对应 POI 类型，使用 Stronghold 尺寸
        // ========================================

        // 城镇防御战（Town/Village 被攻击时）
        AddTpl("town_defense", G, 0.30f, new() { {P,.15f},{R,.20f},{Ru,.15f},{W,.10f},{F,.05f},{Sv,.05f} },
            road:true, tcc:new(2,4), tcr:new(2,3), dtcc:.10f,
            rsc:new(3,6), wsc:new(3,5), wsl:new(3,6), sp:2,
            sf: new());

        // 城堡攻防战（Castle）
        AddTpl("castle_siege", H, 0.25f, new() { {W,.25f},{Ru,.20f},{P,.15f},{R,.10f},{S,.05f} },
            bias:.2f, road:true, tcc:new(0,2), tcr:new(1,2), dtcc:.05f,
            rsc:new(3,5), wsc:new(4,7), wsl:new(4,8), sp:1,
            sf: new() { (Ru, .06f) });

        // 山贼/劫匪营地（Bandit/Robber Settlement + BanditCamp/RobberHideout Lair）
        AddTpl("bandit_stronghold", F, 0.30f, new() { {G,.20f},{P,.15f},{Ru,.10f},{H,.10f},{DF,.10f},{Sv,.05f} },
            bias:.1f, tcc:new(3,6), tcr:new(2,4), dtcc:.30f,
            rsc:new(2,4), wsc:new(1,3), wsl:new(2,5), sp:2,
            sf: new() { (PM, .03f), (Ru, .05f) });

        // 海寇据点（Pirate Settlement + PirateCove Lair）
        AddTpl("pirate_cove", S, 0.35f, new() { {SW,.10f},{G,.15f},{P,.15f},{Ru,.10f},{W,.10f},{Sv,.05f} },
            bias:-.1f, env:"storm", tcc:new(1,3), tcr:new(2,3), dtcc:.10f,
            rsc:new(2,4), wsc:new(1,3), wsl:new(2,5), sp:2,
            sf: new());

        // 哥布林大营（Goblin Settlement）
        AddTpl("goblin_stronghold", G, 0.25f, new() { {F,.20f},{P,.15f},{Ru,.15f},{Sw,.10f},{H,.05f},{PM,.05f},{DF,.05f} },
            bias:-.1f, tcc:new(3,5), tcr:new(2,4), dtcc:.20f,
            rsc:new(2,5), wsc:new(1,3), wsl:new(2,4), sp:2,
            sf: new() { (PM, .08f), (Ru, .10f) });

        // 狗头人矿坑据点（Kobold Settlement）
        AddTpl("kobold_stronghold", Ru, 0.30f, new() { {W,.25f},{H,.15f},{P,.10f},{M,.10f},{SW,.05f},{PM,.05f} },
            bias:-.2f, env:"earthquake", tcc:new(0,1), tcr:new(1,2), dtcc:0,
            rsc:new(3,5), wsc:new(3,6), wsl:new(3,7), sp:1,
            sf: new() { (W, .10f), (PM, .05f) });

        // 牛头人要塞（Minotaur Settlement）
        AddTpl("minotaur_stronghold", H, 0.30f, new() { {Ru,.20f},{W,.15f},{P,.10f},{S,.10f},{M,.10f},{Sv,.05f} },
            bias:.3f, tcc:new(0,2), tcr:new(1,2), dtcc:.05f,
            rsc:new(3,5), wsc:new(3,5), wsl:new(4,7), sp:1,
            sf: new() { (Ru, .08f) });

        // 暗影教团祭坛（ShadowCult Settlement）
        AddTpl("shadow_cult_temple", Ru, 0.30f, new() { {W,.20f},{Sw,.15f},{P,.10f},{DF,.10f},{PM,.10f},{F,.05f} },
            bias:-.2f, env:"poison_fog", tcc:new(1,3), tcr:new(2,3), dtcc:.20f,
            rsc:new(3,5), wsc:new(3,5), wsl:new(3,6), sp:2,
            sf: new() { (PM, .10f), (W, .06f) });

        // 劫掠队前哨（RaiderOutpost Lair）
        AddTpl("raider_outpost", P, 0.30f, new() { {G,.20f},{Sv,.15f},{H,.10f},{Ru,.10f},{W,.10f},{F,.05f} },
            bias:.1f, tcc:new(1,3), tcr:new(2,3), dtcc:.10f,
            rsc:new(2,4), wsc:new(2,4), wsl:new(3,5), sp:2,
            sf: new() { (Ru, .06f) });
    }

    // ========================================================================
    // Public API
    // ========================================================================

    public BattleMapData Generate(BattleContext context)
    {
        GD.Seed((ulong)context.Seed);
        if (context.OverworldGrid != null)
            return GenerateFromOverworld(context);
        return GenerateFromTemplateInternal(context);
    }

    /// <summary>
    /// 统一架构入口：通过 BattleOverworldFactory 生成小型大地图，再走 GenerateFromOverworld pipeline。
    /// 保留方法签名以兼容现有调用方（QuickCombatScene / SimulationHarness）。
    /// </summary>
    public BattleMapData GenerateFromTemplate(string templateName, BattleSize size, int seedVal = 0)
    {
        int seed = seedVal != 0 ? seedVal : (int)GD.Randi();
        var ctx = Generation.BattleOverworldFactory.CreateContext(
            templateName,
            (BattleContext.BattleSize)(int)size,
            seed);
        return Generate(ctx);
    }

    /// <summary>
    /// 内部模板路径：当 OverworldGrid 为 null 时，通过 BattleOverworldFactory 生成小型大地图再走统一 pipeline。
    /// </summary>
    BattleMapData GenerateFromTemplateInternal(BattleContext context)
    {
        // 从大地图地形类型推导模板名
        var tplName = OverworldTerrain.GetBattleTemplateName((OverworldTerrain.Type)context.Terrain);

        // 通过工厂生成小型大地图
        var preset = Generation.BattleOverworldFactory.GetPreset(tplName);
        var (grid, coord) = Generation.BattleOverworldFactory.CreateFromPreset(
            preset,
            context.Size,
            context.Seed);

        // 注入 grid 和坐标到 context，走统一的 GenerateFromOverworld
        context.OverworldGrid = grid;
        context.EncounterCoord = coord;

        // 保留 EnvironmentOverride（如果 preset 有环境事件且 context 没有覆盖）
        if (string.IsNullOrEmpty(context.EnvironmentOverride) && !string.IsNullOrEmpty(preset.EnvironmentEvent))
            context.EnvironmentOverride = preset.EnvironmentEvent;

        return GenerateFromOverworld(context);
    }

    /// <summary>
    /// T8(spec combat-hex-from-overworld-state Phase 3) — 6 stage 流水线：
    ///   1. Sample      OverworldSampler.Sample(context, grid, K)
    ///   2. Project     BattleProjection.Project(samples, N)
    ///   3. Voronoi+水  AssignLandVoronoi + PlaceWater (内部子函数)
    ///   4. Bridge      BridgePlacer.Place(projections, ...)
    ///   5. Structures  GenRuinStructures / GenWallSegments + Scatter + Smooth (T14 后换 StructurePlacer)
    ///   6. Weather     WeatherOverlay.Apply(context.WeatherOverride, ...)
    /// 任意阶段空集 / 异常 → 走 GenerateFromTemplateInternal fallback。
    /// </summary>
    BattleMapData GenerateFromOverworld(BattleContext context)
    {
        // ============================================================
        // Stage 0:派生 mapData（hex 半径/W/H）
        // ============================================================
        var fakeTplForCreate = new BattleMapTemplate { TemplateName = "overworld_dynamic" };
        var mapData = CreateMapData(ToLocalBS(context.Size), fakeTplForCreate);

        var grid = context.OverworldGrid!;
        var rng = new Generation.SeededRng(context.Seed);

        // ============================================================
        // Stage 1:Sample — 从大地图抽 footprint + K 圈邻居
        // ============================================================
        int samplingRingCount = ResolveSamplingRingCount(context);
        var samples = Generation.OverworldSampler.Sample(context, grid, samplingRingCount);
        if (samples.IsEmpty)
        {
            // R6:无 sample — 直接用默认地形填充，不递归调用 GenerateFromTemplateInternal（避免无限递归）
            GD.PushWarning("[BattleMapGenerator] Sample 为空，使用默认草地填充");
            var defaultTerrain = new Dictionary<Vector2I, BattleCellData.TerrainType>();
            var defaultElev = new Dictionary<Vector2I, int>();
            foreach (var coord in mapData.IterateCoords())
            {
                defaultTerrain[coord] = BattleCellData.TerrainType.Grassland;
                defaultElev[coord] = 1;
            }
            FinalizeCells(mapData, defaultTerrain, defaultElev, BattleCellData.TerrainType.Grassland);
            var fallbackZones = DeploymentZone.GenerateZones(mapData, context.Engagement, context.ApproachDirection);
            mapData.PlayerDeployment = fallbackZones["player"].AsGodotArray();
            mapData.EnemyDeployment = fallbackZones["enemy"].AsGodotArray();
            mapData.TemplateName = "fallback_empty_sample";
            return mapData;
        }

        // ============================================================
        // Stage 2:Project — 大地图 axial → 战斗 axial(SampleProjection 列表)
        // ============================================================
        int battleRadius = mapData.HexRadius > 0 ? mapData.HexRadius
            : Mathf.Max(mapData.Width, mapData.Height) / 2;
        var projections = Generation.BattleProjection.Project(samples, battleRadius);

        // ============================================================
        // Stage 3:Voronoi 陆地 + 水域 splash + 河流连线
        // ============================================================
        var terrainMap = new Dictionary<Vector2I, BattleCellData.TerrainType>();
        var elevationMap = new Dictionary<Vector2I, int>();
        var detailNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = context.Seed,    // R8 确定性
            Frequency = 0.15f,
        };

        // 初始化所有 cell 为草地，基础海拔=2（平地）
        foreach (var coord in mapData.IterateCoords())
        {
            terrainMap[coord] = BattleCellData.TerrainType.Grassland;
            elevationMap[coord] = 2;
        }

        AssignLandVoronoi(mapData, projections, terrainMap, elevationMap, detailNoise);
        PlaceWater(mapData, projections, terrainMap, elevationMap, rng);

        // ============================================================
        // Stage 3b:Roads
        // ============================================================
        PlaceRoads(mapData, projections, terrainMap, elevationMap);

        // ============================================================
        // Stage 4:Bridge
        // ============================================================
        Generation.BridgePlacer.Place(projections, terrainMap, elevationMap, mapData);

        // ============================================================
        // Stage 5:Structures
        // ============================================================
        int poiType = context.PoiType;
        var rsc = poiType <= 2 ? new Vector2I(1, 3) : poiType == 3 ? new Vector2I(1, 3) : new Vector2I(2, 4);
        var wsc = poiType <= 2 ? new Vector2I(1, 2) : poiType == 3 ? new Vector2I(0, 2) : new Vector2I(1, 3);
        var fakeTpl = new BattleMapTemplate { RuinStructureCount = rsc, WallSegmentCount = wsc,
            WallSegmentLength = new Vector2I(2, 4), SmoothingPasses = 2 };
        GenRuinStructures(terrainMap, fakeTpl);
        GenWallSegments(terrainMap, fakeTpl);

        // 据点结构：城墙 + 城门 + 塔楼 + 楼梯（使用独立组件）
        if (ShouldGenerateStronghold(context))
            Generation.StrongholdPlacer.Place(terrainMap, elevationMap, mapData, context.ApproachDirection, _staircaseFacings);

        ScatterSpecialFeatures(terrainMap);
        SmoothTerrainMap(terrainMap, 2);

        // ============================================================
        // Stage 6:Weather — R7 天气覆盖（Plains/Grassland/Savanna 25% 改写）
        // ============================================================
        Generation.WeatherOverlay.Apply(context.WeatherOverride, terrainMap, mapData, rng);

        // ============================================================
        // Stage 7:Finalize cells + deployment + connectivity
        // ============================================================
        FinalizeCells(mapData, terrainMap, elevationMap, BattleCellData.TerrainType.Grassland);

        // 描述性 templateName(便于调试 / 校验):
        //   POI 战:"poi_{type}_with_{neighbors}" 例如 "poi_3_with_river_road"
        //   野外:  "wild_{terrain}"           例如 "wild_forest"
        mapData.TemplateName = ResolveTemplateName(context, samples);

        // EnvironmentEvent 优先级:
        //   1) WeatherOverlay 已经在 stage 6 设置(rain → "rain")
        //   2) 若 stage 6 未设,用 DeriveEnvironmentEvent(地形多数派 → fog/storm/...)
        //   3) EnvironmentOverride 永远最高(R7#3)
        if (string.IsNullOrEmpty(mapData.EnvironmentEvent))
            mapData.EnvironmentEvent = DeriveEnvironmentEvent(terrainMap);

        // 部署区：攻城战用专门的部署逻辑，普通战斗用通用逻辑
        Godot.Collections.Dictionary zones2;
        if (ShouldGenerateStronghold(context))
        {
            zones2 = Generation.SiegeDeployment.GenerateZones(mapData, context.AttackingSide);
        }
        else
        {
            zones2 = DeploymentZone.GenerateZones(mapData, context.Engagement, context.ApproachDirection);
        }
        mapData.PlayerDeployment = zones2["player"].AsGodotArray();
        mapData.EnemyDeployment = zones2["enemy"].AsGodotArray();
        EnsureConnectivity(mapData, (int)context.Engagement);
        if (!string.IsNullOrEmpty(context.EnvironmentOverride))
            mapData.EnvironmentEvent = context.EnvironmentOverride;
        return mapData;
    }

    // ========================================================================
    // T8 内部子函数:T12 后会拆到独立 WaterPlacer.cs
    // ========================================================================

    /// <summary>
    /// 战斗规模 → 采样圈数 K。
    /// 战斗地图大小完全由大地图采样范围决定：
    ///   Mercenary = 单格 (K=0)
    ///   Knight    = 7 格 (K=1)
    ///   Lord      = 19 格 (K=2)
    ///   Stronghold= 37 格 (K=3)
    /// POI 战斗：取 max(规模派生的 K, POI footprint 自然覆盖的范围)
    /// </summary>
    static int ResolveSamplingRingCount(BattleContext context)
    {
        int sizeK = SamplingRingMap.GetValueOrDefault(ToLocalBS(context.Size), 1);

        // POI 战斗：POI footprint 本身可能跨多格，取 max 保证不缩小
        if (context.DefendingPOI != null && context.DefendingPOI.OccupiedHexes.Length > 0)
        {
            var profile = Strategic.POIScaleTable.Get(context.DefendingPOI.Scale);
            return System.Math.Max(sizeK, profile.SamplingRingCount);
        }

        return sizeK;
    }

    /// <summary>
    /// 用陆地 sample(IsLand=true) 投影点对每个 battle cell 做 Voronoi 分配。
    /// 改进：在 Voronoi 边界处生成自然过渡带，高程与地形协调。
    /// 道路 sample 强制覆盖为 Road 地形(elevation=1)。
    /// 桥 sample(IsBridge)在此阶段不参与 Voronoi(BridgePlacer 单独处理)。
    /// </summary>
    void AssignLandVoronoi(
        BattleMapData mapData,
        IReadOnlyList<Generation.SampleProjection> projections,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        Dictionary<Vector2I, int> elevationMap,
        FastNoiseLite detailNoise)
    {
        // 桥 sample 不在 Voronoi 池里 —— BridgePlacer 之后单独覆盖
        var landProjections = new List<Generation.SampleProjection>();
        foreach (var p in projections)
            if (p.IsLand && !p.IsBridge) landProjections.Add(p);

        // 边界保护:全水 sample(港口 footprint 全在海上) → 全图 Grassland 兜底
        if (landProjections.Count == 0) return;

        // 第二层噪声用于过渡带扰动
        var transitionNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = detailNoise.Seed + 777,
            Frequency = 0.10f,
        };

        foreach (var coord in mapData.IterateCoords())
        {
            // 找最近和次近的 sample（用于过渡带判定）
            Generation.SampleProjection? best = null, second = null;
            int bestDist = int.MaxValue, secondDist = int.MaxValue;
            foreach (var p in landProjections)
            {
                int d = HexUtils.AxialDistance(coord, p.BattleAxial);
                if (d < bestDist)
                {
                    second = best; secondDist = bestDist;
                    best = p; bestDist = d;
                }
                else if (d < secondDist)
                {
                    second = p; secondDist = d;
                }
            }

            if (best == null) continue;
            var chosen = best.Value;

            float nv = detailNoise.GetNoise2D(coord.X * 2.0f, coord.Y * 2.0f);
            float tv = transitionNoise.GetNoise2D(coord.X * 1.5f, coord.Y * 1.5f);

            // 基础地形和高程
            var bt = MapOverworldToBattle(chosen.Tile.Terrain);
            int be = MapOverworldElevation(chosen.Tile.Elevation);

            // === 过渡带逻辑 ===
            // 如果最近和次近 sample 地形不同，且距离差 ≤ 2，有概率使用过渡地形
            if (second != null && secondDist - bestDist <= 2)
            {
                var secondTerrain = MapOverworldToBattle(second.Value.Tile.Terrain);
                if (bt != secondTerrain && !chosen.Tile.IsRoad && !second.Value.Tile.IsRoad)
                {
                    // 过渡概率：距离差越小越可能过渡，噪声扰动边界
                    float transitionChance = secondDist == bestDist ? 0.5f : 0.25f;
                    transitionChance += tv * 0.2f; // 噪声扰动 ±20%

                    if (nv > 1.0f - transitionChance * 2.0f)
                    {
                        // 选择过渡地形：取两种地形之间的中间态
                        bt = ResolveTransitionTerrain(bt, secondTerrain);
                        // 高程也取中间值
                        int secondElev = MapOverworldElevation(second.Value.Tile.Elevation);
                        if (be != secondElev) be = (be + secondElev + 1) / 2;
                    }
                }
            }

            // === 微变化（放宽触发条件）===
            var ft = TerrainMicroVariation(bt, chosen.Tile.Moisture, nv);

            // === 高程扰动（与地形协调）===
            int fe = be;
            if (nv < -0.4f && fe > 0 && !IsHighTerrain(ft)) fe--;
            else if (nv > 0.4f && fe < 4 && !IsLowTerrain(ft)) fe++;

            // 强制高程-地形一致性
            fe = EnforceTerrainElevation(ft, fe);

            // 道路不在 Voronoi 阶段处理，由独立的 PlaceRoads 阶段用寻路算法生成
            // if (chosen.Tile.IsRoad) — 跳过，保留自然地形

            terrainMap[coord] = ft;
            elevationMap[coord] = fe;
        }
    }

    /// <summary>两种地形之间的过渡地形</summary>
    static BattleCellData.TerrainType ResolveTransitionTerrain(
        BattleCellData.TerrainType a, BattleCellData.TerrainType b)
    {
        // 森林 ↔ 草地/平原 → 稀树草原
        if ((IsForestTerrain(a) && IsOpenTerrain(b)) || (IsOpenTerrain(a) && IsForestTerrain(b)))
            return BattleCellData.TerrainType.Savanna;

        // 森林 ↔ 密林 → 森林
        if ((a == BattleCellData.TerrainType.DenseForest && b == BattleCellData.TerrainType.Forest)
            || (a == BattleCellData.TerrainType.Forest && b == BattleCellData.TerrainType.DenseForest))
            return BattleCellData.TerrainType.Forest;

        // 丘陵 ↔ 平原/草地 → 丘陵（低处）
        if ((a == BattleCellData.TerrainType.Hills && IsOpenTerrain(b))
            || (IsOpenTerrain(a) && b == BattleCellData.TerrainType.Hills))
            return BattleCellData.TerrainType.Hills;

        // 沼泽 ↔ 草地 → 沼泽边缘（仍是草地但湿润）
        if ((a == BattleCellData.TerrainType.Swamp && IsOpenTerrain(b))
            || (IsOpenTerrain(a) && b == BattleCellData.TerrainType.Swamp))
            return BattleCellData.TerrainType.Grassland;

        // 沙地 ↔ 草地/平原 → 稀树草原
        if ((a == BattleCellData.TerrainType.Sand && IsOpenTerrain(b))
            || (IsOpenTerrain(a) && b == BattleCellData.TerrainType.Sand))
            return BattleCellData.TerrainType.Savanna;

        // 默认：保持 a
        return a;
    }

    static bool IsForestTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.Forest || t == BattleCellData.TerrainType.DenseForest
        || t == BattleCellData.TerrainType.Taiga || t == BattleCellData.TerrainType.Jungle;

    static bool IsOpenTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.Plains || t == BattleCellData.TerrainType.Grassland
        || t == BattleCellData.TerrainType.Savanna;

    static bool IsHighTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.Mountain || t == BattleCellData.TerrainType.MountainSnow
        || t == BattleCellData.TerrainType.Hills;

    static bool IsLowTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.ShallowWater || t == BattleCellData.TerrainType.DeepWater
        || t == BattleCellData.TerrainType.Swamp || t == BattleCellData.TerrainType.Bog;

    /// <summary>强制高程与地形类型一致（叠加模式，0-5 级）</summary>
    static int EnforceTerrainElevation(BattleCellData.TerrainType t, int baseElev)
    {
        // 地形加成叠加到基础海拔上
        int bonus = t switch
        {
            BattleCellData.TerrainType.DeepWater => -2,
            BattleCellData.TerrainType.ShallowWater or BattleCellData.TerrainType.River => -1,
            BattleCellData.TerrainType.Swamp or BattleCellData.TerrainType.Bog => -1,
            BattleCellData.TerrainType.Hills or BattleCellData.TerrainType.Rocky => 1,
            BattleCellData.TerrainType.Mountain or BattleCellData.TerrainType.MountainSnow => 2,
            _ => 0,
        };
        return Mathf.Clamp(baseElev + bonus, 0, 5);
    }

    /// <summary>
    /// 水 sample 在投影点周围打 splash 水洼,用噪声扰动水岸线使形态自然。
    /// 硬封顶 12%(全水 sample 时 30%)。
    /// IsRiver sample 之间用 hex 直线连接成"河带"，河带两侧有浅水过渡。
    /// </summary>
    void PlaceWater(
        BattleMapData mapData,
        IReadOnlyList<Generation.SampleProjection> projections,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        Dictionary<Vector2I, int> elevationMap,
        Generation.SeededRng rng)
    {
        var waterProjections = new List<Generation.SampleProjection>();
        bool anyLand = false;
        foreach (var p in projections)
        {
            // 桥 sample 既不参与水域 splash,也不参与陆地 Voronoi
            if (p.IsBridge) continue;
            if (p.IsWater) waterProjections.Add(p);
            else if (p.IsLand) anyLand = true;
        }
        if (waterProjections.Count == 0) return;

        int totalCells = terrainMap.Count;
        // 水域硬封顶:有陆地 sample 时 12%,全水 sample 时放宽到 30%
        int maxWater = (int)(totalCells * (anyLand ? 0.12f : 0.30f));
        int waterPlaced = 0;

        // 水岸噪声：扰动水域边界，让湖岸/河岸不规则
        var shoreNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = rng.NextRange(0, int.MaxValue),
            Frequency = 0.20f,
        };

        // splashRadius:water sample 越多每个水洼越小(避免堆水)
        int splashRadius = waterProjections.Count switch
        {
            1 => 3,
            2 => 2,
            _ => 2,
        };

        foreach (var p in waterProjections)
        {
            if (waterPlaced >= maxWater) break;
            for (int dq = -splashRadius; dq <= splashRadius; dq++)
            {
                int r1 = Mathf.Max(-splashRadius, -dq - splashRadius);
                int r2 = Mathf.Min(splashRadius, -dq + splashRadius);
                for (int dr = r1; dr <= r2; dr++)
                {
                    if (waterPlaced >= maxWater) break;
                    var c = new Vector2I(p.BattleAxial.X + dq, p.BattleAxial.Y + dr);
                    if (!terrainMap.ContainsKey(c)) continue;
                    var existing = terrainMap[c];
                    if (existing == BattleCellData.TerrainType.Road
                        || existing == BattleCellData.TerrainType.Wall) continue;
                    if (existing == BattleCellData.TerrainType.ShallowWater
                        || existing == BattleCellData.TerrainType.DeepWater) continue;

                    int dist = Mathf.Max(Mathf.Abs(dq), Mathf.Max(Mathf.Abs(dr), Mathf.Abs(dq + dr)));

                    // 噪声扰动水岸线：在边缘处用噪声决定是否放水
                    float shoreVal = shoreNoise.GetNoise2D(c.X * 3.0f, c.Y * 3.0f);
                    float baseChance = 1.0f - 0.35f * dist;  // dist 0→1.0, 1→0.65, 2→0.30, 3→-0.05
                    float finalChance = baseChance + shoreVal * 0.25f; // 噪声扰动 ±25%

                    if (finalChance <= 0f || !rng.NextBool(Mathf.Clamp(finalChance, 0f, 1f))) continue;

                    terrainMap[c] = BattleCellData.TerrainType.ShallowWater;
                    elevationMap[c] = 0;
                    waterPlaced++;
                }
            }
        }

        // 河流连线:相邻 IsRiver sample 之间用 hex 直线连成水带
        var riverPoints = new List<Vector2I>();
        foreach (var p in waterProjections)
            if (p.Tile.IsRiver) riverPoints.Add(p.BattleAxial);
        if (riverPoints.Count >= 2 && waterPlaced < maxWater)
        {
            for (int i = 0; i < riverPoints.Count - 1; i++)
            {
                var line = HexLineDraw(riverPoints[i], riverPoints[i + 1]);
                foreach (var c in line)
                {
                    if (waterPlaced >= maxWater) break;
                    if (!terrainMap.ContainsKey(c)) continue;
                    var existing = terrainMap[c];
                    if (existing == BattleCellData.TerrainType.Road
                        || existing == BattleCellData.TerrainType.Wall
                        || existing == BattleCellData.TerrainType.ShallowWater
                        || existing == BattleCellData.TerrainType.DeepWater) continue;
                    terrainMap[c] = BattleCellData.TerrainType.ShallowWater;
                    elevationMap[c] = 0;
                    waterPlaced++;
                }
            }
        }
    }

    /// <summary>
    /// 描述性 template name —— 便于调试与 audit。
    ///   POI 战:"poi_{type}_with_{neighbors}" — neighbors 取 sample 中除主地形外占比最高的 1-2 个
    ///   野外:  "wild_{terrain}"              — terrain 取 sample 中占比最高的
    /// </summary>
    static string ResolveTemplateName(BattleContext context, Generation.SampleSet samples)
    {
        if (samples.IsEmpty)
            return "overworld_" + OverworldTerrain.GetName((OverworldTerrain.Type)context.Terrain);

        // 统计 sample 中地形频次
        var counts = new Dictionary<HexOverworldTile.TerrainType, int>();
        bool hasRoad = false, hasRiver = false, hasBridge = false;
        foreach (var t in samples.Tiles)
        {
            counts[t.Terrain] = counts.GetValueOrDefault(t.Terrain) + 1;
            if (t.IsRoad) hasRoad = true;
            if (t.IsRiver) hasRiver = true;
            if (t.IsBridge) hasBridge = true;
        }
        var dom = HexOverworldTile.TerrainType.Plains;
        int max = 0;
        foreach (var kv in counts)
            if (kv.Value > max) { max = kv.Value; dom = kv.Key; }

        var modifiers = new List<string>();
        if (hasRoad) modifiers.Add("road");
        if (hasRiver) modifiers.Add("river");
        if (hasBridge) modifiers.Add("bridge");

        string body = dom.ToString().ToLowerInvariant();
        if (modifiers.Count > 0) body += "_" + string.Join("_", modifiers);

        if (context.DefendingPOI != null && context.DefendingPOI.OccupiedHexes.Length > 0)
            return $"poi_{context.PoiType}_with_{body}";
        return $"wild_{body}";
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    BattleMapData CreateMapData(BattleSize size, BattleMapTemplate tpl)
    {
        var sz = SMap.GetValueOrDefault(size, SMap[BattleSize.Mercenary]);
        var md = new BattleMapData
        {
            Width = sz.X,
            Height = sz.Y,
            HexRadius = UseHexagonalShape ? RadiusMap.GetValueOrDefault(size, RadiusMap[BattleSize.Mercenary]) : 0,
            TemplateName = tpl.TemplateName,
        };
        return md;
    }

    /// <summary>判断地形是否为水域（不可通行或减速）</summary>
    static bool IsWaterTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.ShallowWater || t == BattleCellData.TerrainType.DeepWater;

    /// <summary>六边形格子直线（用于河流连线）</summary>
    static List<Vector2I> HexLineDraw(Vector2I a, Vector2I b)
    {
        int n = HexUtils.AxialDistance(a, b);
        var result = new List<Vector2I>();
        if (n == 0) { result.Add(a); return result; }
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float fq = a.X + (b.X - a.X) * t;
            float fr = a.Y + (b.Y - a.Y) * t;
            // Cube round
            float fs = -fq - fr;
            int rq = Mathf.RoundToInt(fq);
            int rr = Mathf.RoundToInt(fr);
            int rs = Mathf.RoundToInt(fs);
            float dq = Mathf.Abs(rq - fq);
            float dr = Mathf.Abs(rr - fr);
            float ds = Mathf.Abs(rs - fs);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            result.Add(new Vector2I(rq, rr));
        }
        return result;
    }

    static BattleCellData.TerrainType MapOverworldToBattle(HexOverworldTile.TerrainType t) => t switch
    {
        // R5 (2026-05-17) 大地图 21 项 → 战斗 21 项 1:1 显式映射；不再用 Plains 兜底
        HexOverworldTile.TerrainType.DeepWater     => BattleCellData.TerrainType.DeepWater,
        HexOverworldTile.TerrainType.ShallowWater  => BattleCellData.TerrainType.ShallowWater,
        HexOverworldTile.TerrainType.River         => BattleCellData.TerrainType.River,         // R10 新枚举（不再 → ShallowWater）
        HexOverworldTile.TerrainType.Sand          => BattleCellData.TerrainType.Sand,
        HexOverworldTile.TerrainType.Plains        => BattleCellData.TerrainType.Plains,
        HexOverworldTile.TerrainType.Grassland     => BattleCellData.TerrainType.Grassland,
        HexOverworldTile.TerrainType.Forest        => BattleCellData.TerrainType.Forest,
        HexOverworldTile.TerrainType.DenseForest   => BattleCellData.TerrainType.DenseForest,
        HexOverworldTile.TerrainType.Jungle        => BattleCellData.TerrainType.Jungle,        // R10 新枚举
        HexOverworldTile.TerrainType.Taiga         => BattleCellData.TerrainType.Taiga,         // R10 新枚举
        HexOverworldTile.TerrainType.Bog           => BattleCellData.TerrainType.Bog,           // R10 新枚举
        HexOverworldTile.TerrainType.Hills         => BattleCellData.TerrainType.Hills,
        HexOverworldTile.TerrainType.Mountain      => BattleCellData.TerrainType.Mountain,
        HexOverworldTile.TerrainType.MountainSnow  => BattleCellData.TerrainType.MountainSnow,  // R10 新枚举
        HexOverworldTile.TerrainType.Snow          => BattleCellData.TerrainType.Snow,
        HexOverworldTile.TerrainType.Ice           => BattleCellData.TerrainType.Ice,           // R10 新枚举
        HexOverworldTile.TerrainType.Swamp         => BattleCellData.TerrainType.Swamp,
        HexOverworldTile.TerrainType.Savanna       => BattleCellData.TerrainType.Savanna,
        HexOverworldTile.TerrainType.Wasteland     => BattleCellData.TerrainType.Wasteland,     // R10 新枚举
        HexOverworldTile.TerrainType.Rocky         => BattleCellData.TerrainType.Rocky,         // R10 新枚举
        HexOverworldTile.TerrainType.Road          => BattleCellData.TerrainType.Road,
        _ => MapOverworldFallback(t),
    };

    /// <summary>
    /// R5#3 兜底：大地图未来若新增 TerrainType 而漏改本表，PushError 让漏 case 立刻暴露。
    /// </summary>
    static BattleCellData.TerrainType MapOverworldFallback(HexOverworldTile.TerrainType t)
    {
        GD.PushError($"[BattleMapGenerator] 未识别的大地图地形: {t}");
        return BattleCellData.TerrainType.Plains;
    }

    static int MapOverworldElevation(float e) => e < 0.35f ? 1 : e > 0.65f ? 3 : 2;

    static BattleCellData.TerrainType TerrainMicroVariation(
        BattleCellData.TerrainType b, float moisture, float nv)
    {
        // 放宽触发条件：nv > 0.3 就有机会微变化（旧值 0.65 太苛刻）
        if (nv < 0.3f) return b;
        // 变化强度随 nv 递增
        float intensity = (nv - 0.3f) / 0.7f; // 0→1 映射

        return b switch
        {
            BattleCellData.TerrainType.Grassland => moisture > 0.6f && intensity > 0.4f
                ? BattleCellData.TerrainType.Forest : b,
            BattleCellData.TerrainType.Plains => moisture < 0.35f && intensity > 0.3f
                ? BattleCellData.TerrainType.Savanna
                : moisture > 0.55f && intensity > 0.5f ? BattleCellData.TerrainType.Grassland : b,
            BattleCellData.TerrainType.Forest => intensity > 0.6f
                ? BattleCellData.TerrainType.DenseForest : b,
            BattleCellData.TerrainType.Savanna => moisture > 0.55f && intensity > 0.4f
                ? BattleCellData.TerrainType.Grassland
                : moisture < 0.2f && intensity > 0.6f ? BattleCellData.TerrainType.Sand : b,
            BattleCellData.TerrainType.Swamp => intensity > 0.7f
                ? BattleCellData.TerrainType.PoisonMushroom : b,
            BattleCellData.TerrainType.Hills => intensity > 0.8f
                ? BattleCellData.TerrainType.Rocky : b,
            BattleCellData.TerrainType.Snow => intensity > 0.7f
                ? BattleCellData.TerrainType.Ice : b,
            _ => b,
        };
    }

    /// <summary>
    /// 道路生成：用 A* 寻路连接所有 IsRoad sample 的投影点，宽度 3 格。
    /// 道路方向由大地图采样的 RoadDirections 决定，保证贯通。
    /// </summary>
    void PlaceRoads(
        BattleMapData mapData,
        IReadOnlyList<Generation.SampleProjection> projections,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        Dictionary<Vector2I, int> elevationMap)
    {
        // 收集所有 IsRoad sample 的投影点
        var roadPoints = new List<Vector2I>();
        foreach (var p in projections)
        {
            if (p.Tile.IsRoad && !p.IsBridge)
                roadPoints.Add(p.BattleAxial);
        }
        if (roadPoints.Count == 0) return;

        // 如果只有 1 个道路点，向地图边缘延伸（保证道路贯通）
        if (roadPoints.Count == 1)
        {
            var center = roadPoints[0];
            // 找到该 sample 的道路方向，延伸到地图边缘
            var roadSample = projections[0];
            foreach (var p in projections)
                if (p.Tile.IsRoad && !p.IsBridge) { roadSample = p; break; }

            // 用 RoadDirections 推导方向，如果没有则默认东西贯通
            var dirs = GetRoadExitDirections(roadSample.Tile);
            if (dirs.Count == 0) dirs = new List<int> { 0, 3 }; // 东、西

            foreach (int dir in dirs)
            {
                var edgePoint = ExtendToEdge(center, dir, mapData);
                if (edgePoint != center) roadPoints.Add(edgePoint);
            }
        }

        // 对所有道路点排序（按到地图中心的距离），然后依次用 A* 连接
        var mapCenter = mapData.HexRadius > 0 ? Vector2I.Zero : new Vector2I(mapData.Width / 2, mapData.Height / 2);
        roadPoints.Sort((a, b) =>
        {
            int da = HexUtils.AxialDistance(a, mapCenter);
            int db = HexUtils.AxialDistance(b, mapCenter);
            return da.CompareTo(db);
        });

        // 用 A* 连接相邻的道路点
        var roadCells = new HashSet<Vector2I>();
        for (int i = 0; i < roadPoints.Count - 1; i++)
        {
            var path = FindRoadPath(roadPoints[i], roadPoints[i + 1], terrainMap, mapData);
            foreach (var cell in path)
                roadCells.Add(cell);
        }

        // 如果只有 2 个点且它们是同一个点（单 sample），画一条贯通线
        if (roadCells.Count == 0 && roadPoints.Count >= 2)
        {
            var line = HexLineDraw(roadPoints[0], roadPoints[roadPoints.Count - 1]);
            foreach (var cell in line)
                if (terrainMap.ContainsKey(cell)) roadCells.Add(cell);
        }

        // 扩展道路宽度到 3（中心线 + 两侧各 1 格）
        // 只对中心线格子扩展，不对已扩展的格子再扩展（避免 5 格宽）
        var wideRoad = new HashSet<Vector2I>();
        foreach (var cell in roadCells)
        {
            wideRoad.Add(cell); // 中心线本身
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(cell.X, cell.Y, d);
                if (terrainMap.ContainsKey(nb) && !roadCells.Contains(nb))
                    wideRoad.Add(nb);
            }
        }

        // 写入道路地形（不覆盖水域和城墙）
        foreach (var cell in wideRoad)
        {
            if (!terrainMap.ContainsKey(cell)) continue;
            var existing = terrainMap[cell];
            if (existing == BattleCellData.TerrainType.DeepWater
                || existing == BattleCellData.TerrainType.ShallowWater
                || existing == BattleCellData.TerrainType.Wall
                || existing == BattleCellData.TerrainType.Rampart
                || existing == BattleCellData.TerrainType.Tower
                || existing == BattleCellData.TerrainType.Gate) continue;

            terrainMap[cell] = BattleCellData.TerrainType.Road;
            // 道路高度取周围地形最高值（保证不低于邻居）
            int maxElev = elevationMap.GetValueOrDefault(cell, 2);
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(cell.X, cell.Y, d);
                if (elevationMap.TryGetValue(nb, out int nbElev) && nbElev > maxElev)
                    maxElev = nbElev;
            }
            elevationMap[cell] = maxElev;
        }
    }

    /// <summary>从 tile 的 RoadDirections 位掩码提取道路出口方向列表</summary>
    static List<int> GetRoadExitDirections(HexOverworldTile tile)
    {
        var dirs = new List<int>();
        for (int d = 0; d < 6; d++)
        {
            if ((tile.RoadDirections & (1 << d)) != 0)
                dirs.Add(d);
        }
        return dirs;
    }

    /// <summary>从起点沿指定方向延伸到地图边缘</summary>
    static Vector2I ExtendToEdge(Vector2I start, int direction, BattleMapData mapData)
    {
        var cur = start;
        for (int step = 0; step < 30; step++)
        {
            var next = HexUtils.GetNeighbor(cur.X, cur.Y, direction);
            if (!mapData.ContainsCoord(next)) return cur;
            cur = next;
        }
        return cur;
    }

    /// <summary>
    /// A* 寻路：找到从 start 到 end 的最低成本路径。
    /// 使用 priority queue 模拟（List + 排序），有迭代上限防止死循环。
    /// </summary>
    List<Vector2I> FindRoadPath(
        Vector2I start, Vector2I end,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        BattleMapData mapData)
    {
        if (start == end) return new List<Vector2I> { start };

        var gScores = new Dictionary<Vector2I, int>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var openList = new List<(int fScore, Vector2I pos)>();
        var closedSet = new HashSet<Vector2I>();

        gScores[start] = 0;
        int h = HexUtils.AxialDistance(start, end);
        openList.Add((h, start));

        int maxIterations = 800; // 防止死循环（631 cells 地图足够）
        int iterations = 0;

        while (openList.Count > 0 && iterations++ < maxIterations)
        {
            // 取 fScore 最小的
            int bestIdx = 0;
            for (int i = 1; i < openList.Count; i++)
                if (openList[i].fScore < openList[bestIdx].fScore) bestIdx = i;

            var (_, current) = openList[bestIdx];
            openList.RemoveAt(bestIdx);

            if (current == end)
            {
                // 重建路径
                var path = new List<Vector2I>();
                var node = end;
                while (node != start)
                {
                    path.Add(node);
                    if (!cameFrom.ContainsKey(node)) break;
                    node = cameFrom[node];
                }
                path.Add(start);
                path.Reverse();
                return path;
            }

            if (closedSet.Contains(current)) continue;
            closedSet.Add(current);

            int g = gScores[current];
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(current.X, current.Y, d);
                if (!mapData.ContainsCoord(nb) || closedSet.Contains(nb)) continue;

                int moveCost = GetRoadMoveCost(terrainMap.GetValueOrDefault(nb, BattleCellData.TerrainType.Plains));
                int tentativeG = g + moveCost;

                if (!gScores.ContainsKey(nb) || tentativeG < gScores[nb])
                {
                    gScores[nb] = tentativeG;
                    cameFrom[nb] = current;
                    int f = tentativeG + HexUtils.AxialDistance(nb, end);
                    openList.Add((f, nb));
                }
            }
        }

        // 寻路失败或超时，用直线兜底
        return HexLineDraw(start, end);
    }

    /// <summary>道路寻路的地形移动成本</summary>
    static int GetRoadMoveCost(BattleCellData.TerrainType t) => t switch
    {
        BattleCellData.TerrainType.Plains or BattleCellData.TerrainType.Grassland
            or BattleCellData.TerrainType.Savanna or BattleCellData.TerrainType.Road => 1,
        BattleCellData.TerrainType.Forest or BattleCellData.TerrainType.Taiga => 2,
        BattleCellData.TerrainType.Hills or BattleCellData.TerrainType.Sand => 3,
        BattleCellData.TerrainType.DenseForest or BattleCellData.TerrainType.Jungle => 4,
        BattleCellData.TerrainType.Mountain or BattleCellData.TerrainType.MountainSnow => 8,
        BattleCellData.TerrainType.DeepWater => 10,
        BattleCellData.TerrainType.ShallowWater or BattleCellData.TerrainType.Swamp => 5,
        _ => 2,
    };

    void ScatterSpecialFeatures(Dictionary<Vector2I, BattleCellData.TerrainType> tm)
    {
        var chances = new List<(BattleCellData.TerrainType, float)>
            { (BattleCellData.TerrainType.PoisonMushroom, 0.01f) };
        foreach (var kvp in tm)
        {
            if (kvp.Value != BattleCellData.TerrainType.Grassland
                && kvp.Value != BattleCellData.TerrainType.Plains
                && kvp.Value != BattleCellData.TerrainType.Savanna) continue;
            foreach (var (ft, prob) in chances)
                if (GD.Randf() < prob) { tm[kvp.Key] = ft; break; }
        }
    }

    static string DeriveEnvironmentEvent(Dictionary<Vector2I, BattleCellData.TerrainType> tm)
    {
        var counts = new Dictionary<BattleCellData.TerrainType, int>();
        foreach (var t in tm.Values)
            counts[t] = counts.GetValueOrDefault(t, 0) + 1;
        var dom = BattleCellData.TerrainType.Plains; int mx = 0;
        foreach (var kvp in counts) if (kvp.Value > mx) { mx = kvp.Value; dom = kvp.Key; }
        return dom switch
        {
            BattleCellData.TerrainType.Swamp => "poison_fog",
            BattleCellData.TerrainType.Forest or BattleCellData.TerrainType.DenseForest => "fog",
            BattleCellData.TerrainType.Hills or BattleCellData.TerrainType.Mountain => "earthquake",
            BattleCellData.TerrainType.ShallowWater or BattleCellData.TerrainType.Sand => "storm",
            _ => "",
        };
    }

    void EnsureConnectivity(BattleMapData md, int engagementType = 0)
    {
        // 确保玩家部署区到敌方部署区可通行
        // 策略：不强行开路，而是将阻断路径的不可通行格子（深水/山）转为可通行地形（浅水/丘陵）
        var pd = md.PlayerDeployment; var ed = md.EnemyDeployment;
        if (pd.Count == 0 || ed.Count == 0) return;

        for (int retry = 0; retry < 3; retry++)
        {
            var start = pd[0].AsVector2I();
            var reachable = FloodFillPassable(md, start);

            // 检查所有敌方部署点是否可达
            Vector2I? blocked = null;
            foreach (var ev in ed)
            {
                var ep = ev.AsVector2I();
                if (!reachable.Contains(ep)) { blocked = ep; break; }
            }
            if (blocked == null) return; // 全部可达

            // 找到阻断路径，将不可通行格子转为可通行
            var path = FindPathIgnoring(md, start, blocked.Value);
            foreach (var pos in path)
            {
                var posV = Variant.From(pos);
                if (!md.Cells.ContainsKey(posV)) continue;
                var c = md.Cells[posV].As<BattleCellData>();
                if (c == null || c.isPassable) continue;

                // 根据原地形选择合理的替代
                BattleCellData.TerrainType replacement = c.terrainType switch
                {
                    BattleCellData.TerrainType.DeepWater => BattleCellData.TerrainType.ShallowWater,
                    BattleCellData.TerrainType.Mountain => BattleCellData.TerrainType.Hills,
                    BattleCellData.TerrainType.Wall => BattleCellData.TerrainType.Ruins,
                    _ => BattleCellData.TerrainType.Plains,
                };
                int elev = replacement == BattleCellData.TerrainType.ShallowWater ? 0
                    : replacement == BattleCellData.TerrainType.Hills ? 2 : 1;
                md.Cells[posV] = BattleCellData.CreateFromType(replacement, elev);
            }
        }
    }

    /// <summary>从起点 BFS 找到所有可通行格子</summary>
    HashSet<Vector2I> FloodFillPassable(BattleMapData md, Vector2I start)
    {
        var reachable = new HashSet<Vector2I> { start };
        var queue = new List<Vector2I> { start };
        while (queue.Count > 0)
        {
            var cur = queue[0]; queue.RemoveAt(0);
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(cur.X, cur.Y, d);
                if (reachable.Contains(nb)) continue;
                var nbV = Variant.From(nb);
                if (!md.Cells.ContainsKey(nbV)) continue;
                var cell = md.Cells[nbV].As<BattleCellData>();
                if (cell != null && cell.isPassable) { reachable.Add(nb); queue.Add(nb); }
            }
        }
        return reachable;
    }

    List<Vector2I> FindPathIgnoring(BattleMapData md, Vector2I start, Vector2I end)
    {
        var visited = new Dictionary<Vector2I, bool>();
        var parent = new Dictionary<Vector2I, Vector2I>();
        var queue = new List<Vector2I> { start };
        visited[start] = true;
        while (queue.Count > 0)
        {
            var cur = queue[0]; queue.RemoveAt(0);
            if (cur == end)
            {
                var path = new List<Vector2I>();
                var node = end;
                while (node != start) { path.Add(node); node = parent[node]; }
                path.Reverse(); return path;
            }
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(cur.X, cur.Y, d);
                if (visited.ContainsKey(nb)) continue;
                if (!md.Cells.ContainsKey(Variant.From(nb))) continue;
                visited[nb] = true; parent[nb] = cur; queue.Add(nb);
            }
        }
        return new();
    }

    // ========================================================================
    // Generation Steps
    // ========================================================================

    Dictionary<Vector2I, int> GenElevationMap(BattleMapData md, float bias)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = (int)GD.Randi(), Frequency = 0.08f,
        };
        var result = new Dictionary<Vector2I, int>();
        foreach (var coord in md.IterateCoords())
        {
            float n = noise.GetNoise2D(coord.X, coord.Y) + bias;
            result[coord] = n > 0.35f ? 2 : n < -0.35f ? 0 : 1;
        }
        return result;
    }

    Dictionary<Vector2I, BattleCellData.TerrainType> GenTerrainMap(BattleMapData md, BattleMapTemplate tpl)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = (int)GD.Randi(), Frequency = 0.12f,
        };
        float totalSec = 0f;
        foreach (var wt in tpl.SecondaryTerrains.Values) totalSec += wt;
        var result = new Dictionary<Vector2I, BattleCellData.TerrainType>();

        // 中心点（六边形=(0,0)，矩形=W/2,H/2 等价像素中心）
        Vector2I centerCoord = md.HexRadius > 0 ? Vector2I.Zero : new Vector2I(md.Width / 2, md.Height / 2 - md.Width / 4);
        // 最大距离（六边形 = HexRadius，矩形 = 对角线）
        float maxDist = md.HexRadius > 0
            ? md.HexRadius
            : Mathf.Sqrt((md.Width * 0.5f) * (md.Width * 0.5f) + (md.Height * 0.5f) * (md.Height * 0.5f));

        foreach (var coord in md.IterateCoords())
        {
            float raw = noise.GetNoise2D(coord.X * 3.7f, coord.Y * 3.7f);
            float noiseNorm = (raw + 1.0f) * 0.5f;
            float perturb = (noiseNorm - 0.5f) * 0.3f;
            float threshold = tpl.PrimaryWeight + perturb;
            BattleCellData.TerrainType chosen;
            if (GD.Randf() < threshold)
                chosen = tpl.PrimaryTerrain;
            else
            {
                float roll = GD.Randf() * totalSec; float cum = 0f;
                chosen = tpl.PrimaryTerrain;
                foreach (var kvp in tpl.SecondaryTerrains)
                {
                    cum += kvp.Value;
                    if (roll <= cum) { chosen = kvp.Key; break; }
                }
            }

            // 水域限制：只允许在地图边缘生成（距中心 > 65% 半径）
            if (IsWaterTerrain(chosen))
            {
                float distFromCenter = md.HexRadius > 0
                    ? HexUtils.AxialDistance(coord, centerCoord)
                    : Mathf.Sqrt((coord.X - centerCoord.X) * (coord.X - centerCoord.X)
                        + (coord.Y - centerCoord.Y) * (coord.Y - centerCoord.Y));
                float edgeRatio = distFromCenter / maxDist;
                if (edgeRatio < 0.65f)
                {
                    // 非边缘区域：将水域替换为主地形或草地
                    chosen = tpl.PrimaryTerrain == BattleCellData.TerrainType.ShallowWater
                        ? BattleCellData.TerrainType.Grassland
                        : tpl.PrimaryTerrain;
                }
            }

            result[coord] = chosen;
        }
        return result;
    }

    void ApplyLinearFeatures(Dictionary<Vector2I, BattleCellData.TerrainType> tm,
        Dictionary<Vector2I, int> em, BattleMapData md, BattleMapTemplate tpl)
    {
        if (tpl.HasRoad)
        {
            // 横穿地图的道路：六边形从 q=-N 到 q=N，r=0；矩形 q∈[0,W) r=H/2
            if (md.HexRadius > 0)
            {
                int n = md.HexRadius;
                for (int q = -n; q <= n; q++)
                {
                    var key = new Vector2I(q, 0);
                    if (tm.ContainsKey(key)) { tm[key] = BattleCellData.TerrainType.Road; em[key] = 1; }
                }
            }
            else
            {
                int roadR = md.Height / 2;
                for (int q = 0; q < md.Width; q++)
                {
                    int qOff = Mathf.FloorToInt(q / 2.0f);
                    var key = new Vector2I(q, roadR - qOff);
                    if (tm.ContainsKey(key)) { tm[key] = BattleCellData.TerrainType.Road; em[key] = 1; }
                }
            }
        }
        if (tpl.HasRiver)
        {
            // 河流沿一条 axial 列纵穿地图
            if (md.HexRadius > 0)
            {
                int n = md.HexRadius;
                int riverQ = -n / 2; // 左 1/4 区域
                for (int r = -n; r <= n; r++)
                {
                    var key = new Vector2I(riverQ, r);
                    if (tm.ContainsKey(key)) { tm[key] = BattleCellData.TerrainType.ShallowWater; em[key] = 0; }
                }
            }
            else
            {
                int riverQ = Mathf.Max(1, md.Width / 4);
                for (int rOff = 0; rOff < md.Height + 2; rOff++)
                {
                    int qOff = Mathf.FloorToInt(riverQ / 2.0f);
                    int r = rOff - qOff - 1;
                    var key = new Vector2I(riverQ, r);
                    if (tm.ContainsKey(key))
                    {
                        tm[key] = BattleCellData.TerrainType.ShallowWater; em[key] = 0;
                    }
                }
            }
        }
    }

    void GenTreeClusters(Dictionary<Vector2I, BattleCellData.TerrainType> tm, BattleMapTemplate tpl)
    {
        var plantable = new List<Vector2I>();
        foreach (var kvp in tm)
            if (kvp.Value == BattleCellData.TerrainType.Plains
                || kvp.Value == BattleCellData.TerrainType.Grassland
                || kvp.Value == BattleCellData.TerrainType.Savanna)
                plantable.Add(kvp.Key);
        if (plantable.Count == 0) return;
        int clusterCount = GD.RandRange(tpl.TreeClusterCount.X, tpl.TreeClusterCount.Y);
        var used = new Dictionary<Vector2I, bool>();
        for (int i = 0; i < clusterCount; i++)
        {
            var seedCell = plantable[GD.RandRange(0, plantable.Count - 1)];
            if (used.ContainsKey(seedCell)) continue;
            int clusterRadius = GD.RandRange(tpl.TreeClusterRadius.X, tpl.TreeClusterRadius.Y);
            var q2 = new List<Vector2I> { seedCell };
            var visited = new Dictionary<Vector2I, bool> { [seedCell] = true };
            int placed = 0, maxCells = (int)(clusterRadius * clusterRadius * 2.0);
            while (q2.Count > 0 && placed < maxCells)
            {
                var cur = q2[0]; q2.RemoveAt(0);
                for (int d = 0; d < 6; d++)
                {
                    var nb = HexUtils.GetNeighbor(cur.X, cur.Y, d);
                    if (!visited.ContainsKey(nb) && tm.ContainsKey(nb))
                        { visited[nb] = true; q2.Add(nb); }
                }
                if (used.ContainsKey(cur) || !tm.ContainsKey(cur)) continue;
                var ct = tm[cur];
                if (ct != BattleCellData.TerrainType.Plains
                    && ct != BattleCellData.TerrainType.Grassland
                    && ct != BattleCellData.TerrainType.Savanna) continue;
                int dist = HexUtils.Distance(seedCell.X, seedCell.Y, cur.X, cur.Y);
                if (dist > clusterRadius) continue;
                float placeChance = 1.0f - ((float)dist / (clusterRadius + 1)) * 0.6f;
                if (GD.Randf() < placeChance)
                {
                    tm[cur] = dist <= 1 && GD.Randf() < tpl.DenseTreeCoreChance
                        ? BattleCellData.TerrainType.DenseForest : BattleCellData.TerrainType.Forest;
                    used[cur] = true; placed++;
                }
            }
        }
    }

    void GenRuinStructures(Dictionary<Vector2I, BattleCellData.TerrainType> tm, BattleMapTemplate tpl)
    {
        var buildable = new List<Vector2I>();
        foreach (var kvp in tm)
            if (kvp.Value == BattleCellData.TerrainType.Plains
                || kvp.Value == BattleCellData.TerrainType.Grassland
                || kvp.Value == BattleCellData.TerrainType.Hills
                || kvp.Value == BattleCellData.TerrainType.Sand)
                buildable.Add(kvp.Key);
        if (buildable.Count == 0) return;
        int count = GD.RandRange(tpl.RuinStructureCount.X, tpl.RuinStructureCount.Y);
        var used = new Dictionary<Vector2I, bool>();
        var patterns = new List<Vector2I[]>
        {
            new Vector2I[] { new(0,0), new(1,0), new(2,0) },
            new Vector2I[] { new(0,0), new(1,0), new(1,1) },
            new Vector2I[] { new(0,0), new(1,0), new(0,1), new(1,-1) },
            new Vector2I[] { new(0,0), new(1,0), new(-1,0), new(0,1) },
            new Vector2I[] { new(0,0), new(1,0), new(-1,0), new(0,-1), new(0,1) },
        };
        for (int i = 0; i < count; i++)
        {
            var anchor = buildable[GD.RandRange(0, buildable.Count - 1)];
            if (used.ContainsKey(anchor)) continue;
            var pat = patterns[GD.RandRange(0, patterns.Count - 1)];
            var toPlace = new List<Vector2I>(); bool valid = true;
            foreach (var off in pat)
            {
                var cell = new Vector2I(anchor.X + off.X, anchor.Y + off.Y);
                if (!tm.ContainsKey(cell)) { valid = false; break; }
                var t = tm[cell];
                if (t == BattleCellData.TerrainType.DeepWater || t == BattleCellData.TerrainType.ShallowWater
                    || t == BattleCellData.TerrainType.Mountain || t == BattleCellData.TerrainType.Wall
                    || used.ContainsKey(cell)) { valid = false; break; }
                toPlace.Add(cell);
            }
            if (!valid) continue;
            for (int idx = 0; idx < toPlace.Count; idx++)
            {
                tm[toPlace[idx]] = idx == 0 || GD.Randf() >= 0.3f
                    ? BattleCellData.TerrainType.Ruins : BattleCellData.TerrainType.Wall;
                used[toPlace[idx]] = true;
            }
        }
    }

    void GenWallSegments(Dictionary<Vector2I, BattleCellData.TerrainType> tm, BattleMapTemplate tpl)
    {
        var placeable = new List<Vector2I>();
        foreach (var kvp in tm)
            if (kvp.Value == BattleCellData.TerrainType.Plains
                || kvp.Value == BattleCellData.TerrainType.Grassland
                || kvp.Value == BattleCellData.TerrainType.Hills)
                placeable.Add(kvp.Key);
        if (placeable.Count == 0) return;
        int segCount = GD.RandRange(tpl.WallSegmentCount.X, tpl.WallSegmentCount.Y);
        var used = new Dictionary<Vector2I, bool>();
        for (int i = 0; i < segCount; i++)
        {
            var startCell = placeable[GD.RandRange(0, placeable.Count - 1)];
            if (used.ContainsKey(startCell)) continue;
            int segLen = GD.RandRange(tpl.WallSegmentLength.X, tpl.WallSegmentLength.Y);
            int dir = GD.RandRange(0, 5);
            var cur = startCell;
            for (int step = 0; step < segLen; step++)
            {
                if (!tm.ContainsKey(cur)) break;
                var t = tm[cur];
                if (t == BattleCellData.TerrainType.Wall || t == BattleCellData.TerrainType.Ruins
                    || t == BattleCellData.TerrainType.DeepWater || t == BattleCellData.TerrainType.ShallowWater
                    || t == BattleCellData.TerrainType.Mountain
                    || t == BattleCellData.TerrainType.Forest || t == BattleCellData.TerrainType.DenseForest
                    || used.ContainsKey(cur)) break;
                float collapseChance = (float)step / segLen * 0.5f;
                tm[cur] = GD.Randf() < collapseChance
                    ? BattleCellData.TerrainType.Ruins : BattleCellData.TerrainType.Wall;
                used[cur] = true;
                cur = HexUtils.GetNeighbor(cur.X, cur.Y, dir);
                if (GD.Randf() < 0.20f)
                {
                    dir = (dir + (GD.Randf() < 0.5f ? 1 : -1)) % 6;
                    if (dir < 0) dir += 6;
                }
            }
        }
    }

    // ========================================================================
    // 据点结构生成 — 城墙环 + 城门 + 塔楼 + 楼梯
    // ========================================================================

    /// <summary>据点结构数据（楼梯朝向等需要传递到 FinalizeCells）</summary>
    private Dictionary<Vector2I, int> _staircaseFacings = new();

    /// <summary>
    /// 生成据点结构：城墙只出现在被攻打的那一面（半弧形）。
    /// 规则：
    /// - 城墙是半环（约 180°弧），朝向由 ApproachDirection 决定
    /// - 城墙弧上每隔一定间距放置塔楼（端点 + 中间）
    /// - 弧中间放置城门（1-2个）
    /// - 城墙内侧紧邻处生成楼梯
    /// - 城墙后方（防守方一侧）放置建筑废墟（模拟城镇内部）
    /// </summary>
    void GenStrongholdStructure(Dictionary<Vector2I, BattleCellData.TerrainType> tm,
        Dictionary<Vector2I, int> em, BattleMapData md, Vector2I? approachDir)
    {
        _staircaseFacings.Clear();

        int N = md.HexRadius > 0 ? md.HexRadius : Mathf.Min(md.Width, md.Height) / 2;

        // 城墙方向由 ApproachDirection 决定：
        // 攻方从 approachDir 方向来 → 城墙在 approachDir 的反方向（防守方一侧边缘）
        // 归一化到 6 个 axial 单位方向之一
        Vector2I defenseDir; // 防守方方向（城墙所在侧）
        if (approachDir.HasValue && (approachDir.Value.X != 0 || approachDir.Value.Y != 0))
        {
            // 取 approachDir 的反方向，归一化到最近的 axial 单位方向
            defenseDir = NormalizeToHexDirection(-approachDir.Value.X, -approachDir.Value.Y);
        }
        else
        {
            // 默认：防守方在右上角 (1, -1)
            defenseDir = new Vector2I(1, -1);
        }

        // 城墙角落位置：沿防守方向推到地图边缘附近
        int cornerOffset = (int)(N * 0.65f);
        Vector2I cornerCenter = new Vector2I(defenseDir.X * cornerOffset, defenseDir.Y * cornerOffset);

        // 确保角落在地图内
        if (!md.ContainsCoord(cornerCenter))
        {
            cornerOffset = (int)(N * 0.5f);
            cornerCenter = new Vector2I(defenseDir.X * cornerOffset, defenseDir.Y * cornerOffset);
        }

        // 两段墙的延伸方向：垂直于防守方向的两个相邻 axial 方向
        // 找到与 defenseDir 垂直的两个方向
        var (dir1, dir2) = GetPerpendicularDirections(defenseDir);

        // 段长 = 地图半径的 45%
        int segLength = Mathf.Max(4, (int)(N * 0.45f));

        Vector2I wallCornerDir = defenseDir; // 用于楼梯内侧判断

        var wallSet = new HashSet<Vector2I>();
        var validWallCells = new List<Vector2I>();

        // 第一段墙：从角落向 dir1 方向延伸
        var cur = cornerCenter;
        for (int i = 0; i < segLength; i++)
        {
            if (tm.ContainsKey(cur) && !IsWaterTerrain(tm[cur]))
            {
                validWallCells.Add(cur);
                wallSet.Add(cur);
            }
            cur = HexUtils.GetNeighbor(cur.X, cur.Y, dir1);
        }

        // 第二段墙：从角落向 dir2 方向延伸
        cur = HexUtils.GetNeighbor(cornerCenter.X, cornerCenter.Y, dir2); // 跳过角落本身（已在第一段）
        for (int i = 0; i < segLength; i++)
        {
            if (tm.ContainsKey(cur) && !IsWaterTerrain(tm[cur]) && !wallSet.Contains(cur))
            {
                validWallCells.Add(cur);
                wallSet.Add(cur);
            }
            cur = HexUtils.GetNeighbor(cur.X, cur.Y, dir2);
        }

        if (validWallCells.Count < 4) return;

        // 塔楼：角落点 + 两段墙的末端
        var towerPositions = new HashSet<Vector2I>();
        if (wallSet.Contains(cornerCenter)) towerPositions.Add(cornerCenter);
        // 每段墙末端
        if (validWallCells.Count > 0) towerPositions.Add(validWallCells[0]);
        if (validWallCells.Count > segLength) towerPositions.Add(validWallCells[segLength]);
        if (validWallCells.Count > 1) towerPositions.Add(validWallCells[validWallCells.Count - 1]);
        // 中间塔楼
        int mid1 = segLength / 2;
        if (mid1 < validWallCells.Count && !towerPositions.Contains(validWallCells[mid1]))
            towerPositions.Add(validWallCells[mid1]);
        int mid2 = segLength + segLength / 2;
        if (mid2 < validWallCells.Count && !towerPositions.Contains(validWallCells[mid2]))
            towerPositions.Add(validWallCells[mid2]);

        // 城门：每段墙的 1/3 处
        var gatePositions = new HashSet<Vector2I>();
        int gate1 = segLength / 3;
        if (gate1 < validWallCells.Count && !towerPositions.Contains(validWallCells[gate1]))
            gatePositions.Add(validWallCells[gate1]);
        int gate2 = segLength + segLength / 3;
        if (gate2 < validWallCells.Count && !towerPositions.Contains(validWallCells[gate2]))
            gatePositions.Add(validWallCells[gate2]);

        // 放置城墙、塔楼、城门
        foreach (var pos in validWallCells)
        {
            if (towerPositions.Contains(pos))
            { tm[pos] = BattleCellData.TerrainType.Tower; em[pos] = 3; }
            else if (gatePositions.Contains(pos))
            { tm[pos] = BattleCellData.TerrainType.Gate; em[pos] = 2; }
            else
            { tm[pos] = BattleCellData.TerrainType.Rampart; em[pos] = 2; }
        }

        // 楼梯：城墙内侧必定连接楼梯（高度1），每个城墙格都尝试放置
        var staircasePlaced = new HashSet<Vector2I>();

        // 强制：每段城墙（两段各自）至少放 1 个楼梯
        // 策略：每隔 3 格放一个，保证覆盖
        for (int i = 0; i < validWallCells.Count; i++)
        {
            var wallPos = validWallCells[i];
            if (towerPositions.Contains(wallPos) || gatePositions.Contains(wallPos)) continue;

            // 每隔 3 格强制放一个楼梯
            bool shouldPlace = (i % 3 == 1);
            if (!shouldPlace) continue;

            // 找内侧邻居（靠近角落方向 = 城墙内部，禁止刷在围墙外）
            int bestDir = -1; float bestScore = float.MinValue;
            float wallScore = wallPos.X * wallCornerDir.X + wallPos.Y * wallCornerDir.Y;
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, d);
                if (!tm.ContainsKey(nb) || wallSet.Contains(nb) || staircasePlaced.Contains(nb)) continue;
                if (IsWaterTerrain(tm[nb])) continue;
                // 内侧 = 投影值比城墙本身更大（更靠近角落）
                float score = nb.X * wallCornerDir.X + nb.Y * wallCornerDir.Y;
                if (score <= wallScore) continue; // 外侧，跳过
                if (score > bestScore) { bestScore = score; bestDir = d; }
            }

            if (bestDir >= 0)
            {
                var stairPos = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, bestDir);
                tm[stairPos] = BattleCellData.TerrainType.Staircase;
                em[stairPos] = 1;
                _staircaseFacings[stairPos] = (bestDir + 3) % 6;
                staircasePlaced.Add(stairPos);
            }
        }

        // 兜底：如果一个楼梯都没放成功，强制在第一个城墙格旁放一个
        if (staircasePlaced.Count == 0 && validWallCells.Count > 0)
        {
            var wallPos = validWallCells[0];
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, d);
                if (!tm.ContainsKey(nb) || wallSet.Contains(nb)) continue;
                tm[nb] = BattleCellData.TerrainType.Staircase;
                em[nb] = 1;
                _staircaseFacings[nb] = (d + 3) % 6;
                staircasePlaced.Add(nb);
                break;
            }
        }

        // 城墙内部（角落区域）：放置建筑，模拟城镇
        // 内部 = 城墙与地图角落之间的区域
        var innerCells = new HashSet<Vector2I>();
        foreach (var coord in tm.Keys)
        {
            if (wallSet.Contains(coord) || staircasePlaced.Contains(coord)) continue;
            // 在角落方向上比城墙更远的格子 = 内部
            float coordScore = coord.X * wallCornerDir.X + coord.Y * wallCornerDir.Y;
            float wallMinScore = float.MaxValue;
            foreach (var w in wallSet)
            {
                float ws = w.X * wallCornerDir.X + w.Y * wallCornerDir.Y;
                if (ws < wallMinScore) wallMinScore = ws;
            }
            if (coordScore > wallMinScore + 1) innerCells.Add(coord);
        }

        // 在内部区域放置建筑
        int buildingsPlaced = 0;
        int maxBuildings = Mathf.Max(3, innerCells.Count / 4);
        foreach (var pos in innerCells)
        {
            if (buildingsPlaced >= maxBuildings) break;
            var t = tm[pos];
            if (t == BattleCellData.TerrainType.Mountain || t == BattleCellData.TerrainType.DeepWater
                || t == BattleCellData.TerrainType.ShallowWater) continue;

            float distFromCorner = Mathf.Abs(pos.X - cornerCenter.X) + Mathf.Abs(pos.Y - cornerCenter.Y);
            float buildChance = distFromCorner <= 3 ? 0.5f : 0.20f;

            if (GD.Randf() < buildChance)
            {
                tm[pos] = buildingsPlaced % 3 == 0
                    ? BattleCellData.TerrainType.Road
                    : BattleCellData.TerrainType.Ruins;
                em[pos] = 1;
                buildingsPlaced++;
            }
        }
    }

    /// <summary>归一化 axial 偏移到 6 个单位方向之一</summary>
    static Vector2I NormalizeToHexDirection(int dq, int dr)
    {
        if (dq == 0 && dr == 0) return new Vector2I(1, 0);
        var dirs = new Vector2I[]
        {
            new(1, 0), new(0, -1), new(-1, 0),
            new(-1, 1), new(0, 1), new(1, -1),
        };
        float bestScore = float.MinValue;
        Vector2I best = dirs[0];
        foreach (var d in dirs)
        {
            float score = dq * d.X + dr * d.Y;
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    /// <summary>获取与给定方向垂直的两个 hex 方向索引（用于城墙延伸）</summary>
    static (int dir1, int dir2) GetPerpendicularDirections(Vector2I defenseDir)
    {
        // 6 个 axial 方向及其索引
        var dirs = new Vector2I[]
        {
            new(1, 0),   // 0: E
            new(0, -1),  // 1: NE  (注意：axial 中 r 减小 = 北)
            new(-1, 0),  // 2: W... 不对，用标准 hex 方向
        };
        // 标准 6 方向（与 HexUtils.GetNeighbor 一致）：
        // 0=(1,0), 1=(1,-1), 2=(0,-1), 3=(-1,0), 4=(-1,1), 5=(0,1)
        var hexDirs = new Vector2I[]
        {
            new(1, 0), new(1, -1), new(0, -1),
            new(-1, 0), new(-1, 1), new(0, 1),
        };

        // 找到 defenseDir 最接近的方向索引
        int bestIdx = 0;
        float bestScore = float.MinValue;
        for (int i = 0; i < 6; i++)
        {
            float score = defenseDir.X * hexDirs[i].X + defenseDir.Y * hexDirs[i].Y;
            if (score > bestScore) { bestScore = score; bestIdx = i; }
        }

        // 垂直方向 = 顺时针和逆时针各偏 2 个方向（约 120° 和 -120°，形成 L 形）
        int perpDir1 = (bestIdx + 2) % 6;
        int perpDir2 = (bestIdx + 4) % 6;
        return (perpDir1, perpDir2);
    }

    /// <summary>判断模板是否为据点类型（需要生成城墙结构）</summary>
    static bool IsStrongholdTemplate(string name) =>
        name.Contains("stronghold") || name.Contains("siege") || name.Contains("fortress")
        || name.Contains("temple") || name.Contains("outpost") || name == "town_defense"
        || name == "castle_siege" || name == "pirate_cove";

    /// <summary>判断当前 context 是否需要生成据点结构（统一架构用）</summary>
    static bool ShouldGenerateStronghold(BattleContext context)
    {
        // 1. 有 DefendingPOI 且规模为 Stronghold
        if (context.DefendingPOI != null && context.Size == BattleContext.BattleSize.Stronghold)
            return true;
        // 2. 通过 BattleOverworldFactory 的 IsStronghold preset 生成
        if (context.PoiType >= 0 && context.Size == BattleContext.BattleSize.Stronghold)
            return true;
        // 3. 模板名匹配据点关键词（兼容旧路径）
        if (context.OverworldGrid != null)
        {
            var preset = Generation.BattleOverworldFactory.GetPreset(
                OverworldTerrain.GetBattleTemplateName((OverworldTerrain.Type)context.Terrain));
            if (preset.IsStronghold) return true;
        }
        return false;
    }

    void ApplySpecialFeatures(Dictionary<Vector2I, BattleCellData.TerrainType> tm, BattleMapTemplate tpl)
    {
        foreach (var (ft, prob) in tpl.SpecialFeatures)
            foreach (var key in new List<Vector2I>(tm.Keys))
            {
                var cur = tm[key];
                if (cur == BattleCellData.TerrainType.Grassland
                    || cur == BattleCellData.TerrainType.Plains
                    || cur == BattleCellData.TerrainType.Savanna)
                    if (GD.Randf() < prob) tm[key] = ft;
            }
    }

    void SmoothTerrainMap(Dictionary<Vector2I, BattleCellData.TerrainType> tm, int passes)
    {
        var immune = new HashSet<BattleCellData.TerrainType>
        {
            BattleCellData.TerrainType.Wall, BattleCellData.TerrainType.Ruins,
            BattleCellData.TerrainType.DeepWater, BattleCellData.TerrainType.ShallowWater,
            BattleCellData.TerrainType.PoisonMushroom,
            BattleCellData.TerrainType.Rampart, BattleCellData.TerrainType.Tower,
            BattleCellData.TerrainType.Gate, BattleCellData.TerrainType.Staircase,
        };
        for (int p = 0; p < passes; p++)
        {
            var changes = new Dictionary<Vector2I, BattleCellData.TerrainType>();
            foreach (var kvp in tm)
            {
                if (immune.Contains(kvp.Value)) continue;
                var nc = new Dictionary<BattleCellData.TerrainType, int>();
                int total = 0;
                for (int d = 0; d < 6; d++)
                {
                    var nb = HexUtils.GetNeighbor(kvp.Key.X, kvp.Key.Y, d);
                    if (tm.TryGetValue(nb, out var nt))
                        { nc[nt] = nc.GetValueOrDefault(nt, 0) + 1; total++; }
                }
                if (total == 0) continue;
                foreach (var nkvp in nc)
                    if (nkvp.Value >= 4 && nkvp.Key != kvp.Value && GD.Randf() < 0.5f)
                        { changes[kvp.Key] = nkvp.Key; break; }
            }
            foreach (var ckvp in changes) tm[ckvp.Key] = ckvp.Value;
        }
    }

    void FinalizeCells(BattleMapData md, Dictionary<Vector2I, BattleCellData.TerrainType> tm,
        Dictionary<Vector2I, int> em, BattleCellData.TerrainType defaultTerrain)
    {
        foreach (var key in md.IterateCoords())
        {
            var tt = tm.GetValueOrDefault(key, defaultTerrain);
            int elev = em.GetValueOrDefault(key, 2); // 默认基础海拔=2（平地）
            var cell = BattleCellData.CreateFromType(tt, elev);

            // 据点建筑：使用 StrongholdPlacer 设置的动态高度（已采样周围地形）
            if (tt == BattleCellData.TerrainType.Rampart
                || tt == BattleCellData.TerrainType.Tower
                || tt == BattleCellData.TerrainType.Gate)
            {
                cell.elevation = elev; // 直接用 elevationMap 中的值
            }
            else if (tt == BattleCellData.TerrainType.Staircase)
            {
                cell.elevation = elev;
                if (_staircaseFacings.TryGetValue(key, out int facing))
                    cell.facingDirection = facing;
            }
            else
            {
                // 自然地形：基础海拔 + 地形加成，clamp [0,4]
                cell.elevation = EnforceTerrainElevation(tt, elev);
            }

            md.Cells[Variant.From(key)] = cell;
        }
    }
}
