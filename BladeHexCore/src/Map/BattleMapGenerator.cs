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
        public Godot.Collections.Dictionary Cells = new();
        public Godot.Collections.Array PlayerDeployment = new();
        public Godot.Collections.Array EnemyDeployment = new();
        public string EnvironmentEvent = "", TemplateName = "";
        // Backward compat alias for existing callers using snake_case
        public Godot.Collections.Dictionary cells { get => Cells; set => Cells = value; }
        public Godot.Collections.Array player_deployment { get => PlayerDeployment; set => PlayerDeployment = value; }
        public Godot.Collections.Array enemy_deployment { get => EnemyDeployment; set => EnemyDeployment = value; }
    }

    static readonly Dictionary<BattleSize, Vector2I> SMap = new()
    {
        { BattleSize.Mercenary, new(15, 10) },
        { BattleSize.Knight, new(18, 12) },
        { BattleSize.Lord, new(24, 16) },
        { BattleSize.Stronghold, new(30, 20) },
    };
    readonly Dictionary<string, BattleMapTemplate> _templates = new();

    public BattleMapGenerator() { RegisterTemplates(); }
    public string[] GetTemplateNames() => _templates.Keys.ToArray();
    public BattleMapTemplate? GetTemplate(string n) => _templates.GetValueOrDefault(n);

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
        "castle_siege" => "城堡攻防战",
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
        var LG = BattleCellData.TerrainType.LuckyGrass;

        AddTpl("plain_field", G, 0.50f, new() { {P,.20f},{Sv,.15f},{H,.10f},{R,.05f} },
            road: true, tcc:new(2,4), tcr:new(2,3), dtcc:.15f, rsc:new(0,1), wsc:new(0,1), wsl:new(2,3),
            sf: new() { (LG, .03f) });

        AddTpl("forest_ambush", F, 0.45f, new() { {DF,.20f},{G,.15f},{H,.10f},{P,.10f} },
            bias:.1f, env:"fog", tcc:new(4,8), tcr:new(3,5), dtcc:.50f, rsc:new(0,1),
            sf: new() { (LG, .02f), (PM, .02f) });

        AddTpl("mountain_pass", H, 0.40f, new() { {M,.20f},{P,.15f},{Sn,.10f},{Ru,.05f},{G,.10f} },
            bias:.4f, env:"earthquake", road:true, tcc:new(1,3), tcr:new(1,3), dtcc:.10f,
            rsc:new(1,3), wsc:new(1,3), sp:1, sf: new() { (Ru, .05f) });

        AddTpl("swamp_battle", Sw, 0.40f, new() { {SW,.20f},{G,.15f},{P,.10f},{H,.05f},{DF,.10f} },
            bias:-.3f, env:"poison_fog", river:true, tcc:new(2,5), tcr:new(2,4), dtcc:.35f,
            rsc:new(0,1), sp:3, sf: new() { (PM, .06f) });

        AddTpl("coastal_ambush", S, 0.35f, new() { {SW,.15f},{G,.20f},{P,.15f},{DW,.05f},{Sv,.10f} },
            bias:-.2f, env:"storm", tcc:new(1,3), tcr:new(2,3), dtcc:.10f, rsc:new(0,2), sp:2,
            sf: new() { (LG, .02f) });

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
            sf: new() { (LG, .03f) });

        AddTpl("ruins_exploration", Ru, 0.35f, new() { {W,.20f},{P,.15f},{H,.10f},{F,.10f},{SW,.05f},{LG,.05f} },
            tcc:new(0,2), tcr:new(1,2), dtcc:.10f, rsc:new(2,5), wsc:new(2,5), wsl:new(3,7), sp:1,
            sf: new() { (LG, .04f), (W, .06f) });

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
            sf: new() { (LG, .02f) });

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
            sf: new() { (LG, .02f) });

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

    public BattleMapData GenerateFromTemplate(string templateName, BattleSize size, int seedVal = 0)
    {
        int seed = seedVal != 0 ? seedVal : (int)GD.Randi();
        GD.Seed((ulong)seed);
        var template = _templates.GetValueOrDefault(templateName) ?? _templates["plain_field"];
        var sizeInfo = SMap.GetValueOrDefault(size, SMap[BattleSize.Mercenary]);

        var mapData = CreateMapData(sizeInfo, template);

        var elevationMap = GenElevationMap(mapData.Width, mapData.Height, template.ElevationBias);
        var terrainMap = GenTerrainMap(mapData.Width, mapData.Height, template);
        ApplyLinearFeatures(terrainMap, elevationMap, mapData.Width, mapData.Height, template);
        GenTreeClusters(terrainMap, mapData.Width, mapData.Height, template);
        GenRuinStructures(terrainMap, mapData.Width, mapData.Height, template);
        GenWallSegments(terrainMap, mapData.Width, mapData.Height, template);
        if (IsStrongholdTemplate(template.TemplateName))
            GenStrongholdStructure(terrainMap, elevationMap, mapData.Width, mapData.Height);
        ApplySpecialFeatures(terrainMap, template);
        SmoothTerrainMap(terrainMap, mapData.Width, mapData.Height, template.SmoothingPasses);
        FinalizeCells(mapData, terrainMap, elevationMap, template.PrimaryTerrain);

        var zones = DeploymentZone.GenerateZones(mapData.Width, mapData.Height,
            BattleContext.EngagementType.Normal, mapData.Cells);
        mapData.PlayerDeployment = zones["player"].AsGodotArray();
        mapData.EnemyDeployment = zones["enemy"].AsGodotArray();
        mapData.EnvironmentEvent = template.EnvironmentEvent;
        EnsureConnectivity(mapData, (int)BattleContext.EngagementType.Normal);
        return mapData;
    }

    BattleMapData GenerateFromTemplateInternal(BattleContext context)
    {
        var tplName = OverworldTerrain.GetBattleTemplateName((OverworldTerrain.Type)context.Terrain);
        var template = _templates.GetValueOrDefault(tplName) ?? _templates["plain_field"];
        var sizeInfo = SMap.GetValueOrDefault(ToLocalBS(context.Size), SMap[BattleSize.Mercenary]);
        var mapData = CreateMapData(sizeInfo, template);

        var elevationMap = GenElevationMap(mapData.Width, mapData.Height, template.ElevationBias);
        var terrainMap = GenTerrainMap(mapData.Width, mapData.Height, template);
        ApplyLinearFeatures(terrainMap, elevationMap, mapData.Width, mapData.Height, template);
        GenTreeClusters(terrainMap, mapData.Width, mapData.Height, template);
        GenRuinStructures(terrainMap, mapData.Width, mapData.Height, template);
        GenWallSegments(terrainMap, mapData.Width, mapData.Height, template);
        if (IsStrongholdTemplate(template.TemplateName))
            GenStrongholdStructure(terrainMap, elevationMap, mapData.Width, mapData.Height);
        ApplySpecialFeatures(terrainMap, template);
        SmoothTerrainMap(terrainMap, mapData.Width, mapData.Height, template.SmoothingPasses);
        FinalizeCells(mapData, terrainMap, elevationMap, template.PrimaryTerrain);

        var zones = DeploymentZone.GenerateZones(mapData.Width, mapData.Height,
            context.Engagement, mapData.Cells);
        mapData.PlayerDeployment = zones["player"].AsGodotArray();
        mapData.EnemyDeployment = zones["enemy"].AsGodotArray();
        mapData.EnvironmentEvent = !string.IsNullOrEmpty(context.EnvironmentOverride)
            ? context.EnvironmentOverride : template.EnvironmentEvent;
        EnsureConnectivity(mapData, (int)context.Engagement);
        return mapData;
    }

    BattleMapData GenerateFromOverworld(BattleContext context)
    {
        var sizeInfo = SMap.GetValueOrDefault(ToLocalBS(context.Size), SMap[BattleSize.Mercenary]);
        var mapData = new BattleMapData { Width = sizeInfo.X, Height = sizeInfo.Y };

        int sampleRadius = context.Size switch { BattleContext.BattleSize.Knight => 4, BattleContext.BattleSize.Lord => 5, _ => 3 };
        var grid = context.OverworldGrid!;
        var center = context.EncounterCoord;

        var sampledTiles = new List<HexOverworldTile>();
        var ct = grid.GetTileAtCoord(center);
        if (ct != null) sampledTiles.Add(ct);
        sampledTiles.AddRange(grid.GetTilesInRange(center.X, center.Y, sampleRadius));

        var terrainMap = new Dictionary<Vector2I, BattleCellData.TerrainType>();
        var elevationMap = new Dictionary<Vector2I, int>();
        var detailNoise = new FastNoiseLite();
        detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        detailNoise.Seed = (int)GD.Randi();
        detailNoise.Frequency = 0.15f;

        for (int q = 0; q < mapData.Width; q++)
        {
            int qOff = Mathf.FloorToInt(q / 2.0f);
            for (int r = -qOff; r < mapData.Height - qOff; r++)
            {
                terrainMap[new Vector2I(q, r)] = BattleCellData.TerrainType.Grassland;
                elevationMap[new Vector2I(q, r)] = 1;
            }
        }

        // Axial 1-ring offsets: center + 6 neighbors = 7 cells per overworld tile.
        // This produces a natural hexagonal footprint instead of a rectangular 2×2 block.
        var ring1Offsets = new Vector2I[]
        {
            new(0, 0),   // center
            new(1, 0), new(1, -1), new(0, -1),
            new(-1, 0), new(-1, 1), new(0, 1),
        };
        foreach (var tile in sampledTiles)
        {
            var bt = MapOverworldToBattle(tile.Terrain);
            int be = MapOverworldElevation(tile.Elevation);
            // Map overworld tile to battle map center
            int baseQ = (tile.Coord.X - center.X) * 2 + mapData.Width / 2;
            int baseR = (tile.Coord.Y - center.Y) * 2 + mapData.Height / 2;
            foreach (var off in ring1Offsets)
            {
                var key = new Vector2I(baseQ + off.X, baseR + off.Y);
                if (!terrainMap.ContainsKey(key)) continue;
                float nv = detailNoise.GetNoise2D(key.X * 2.0f, key.Y * 2.0f);
                var ft = TerrainMicroVariation(bt, tile.Moisture, nv);
                int fe = be;
                if (nv < -0.5f && fe > 0) fe--;
                else if (nv > 0.5f && fe < 2) fe++;
                if (tile.IsRoad) { ft = BattleCellData.TerrainType.Road; fe = 1; }
                else if (tile.IsRiver || tile.Terrain == HexOverworldTile.TerrainType.River)
                    { ft = BattleCellData.TerrainType.ShallowWater; fe = 0; }
                terrainMap[key] = ft;
                elevationMap[key] = fe;
            }
        }

        // River spread — limited: only 1 neighbor per water tile, max 5% chance
        var sw = BattleCellData.TerrainType.ShallowWater;
        int waterCount = 0;
        int totalCells = terrainMap.Count;
        int maxWater = totalCells / 6; // 水域上限：总格子的 ~16%
        foreach (var v in terrainMap.Values)
            if (v == sw || v == BattleCellData.TerrainType.DeepWater) waterCount++;

        if (waterCount < maxWater)
        {
            var waterKeys = new List<Vector2I>();
            foreach (var key in terrainMap.Keys)
                if (terrainMap[key] == sw) waterKeys.Add(key);

            foreach (var key in waterKeys)
            {
                if (waterCount >= maxWater) break;
                // 只向一个随机方向扩展
                int startDir = (int)(GD.Randi() % 6);
                for (int di = 0; di < 6 && waterCount < maxWater; di++)
                {
                    int d = (startDir + di) % 6;
                    var nb = HexUtils.GetNeighbor(key.X, key.Y, d);
                    if (terrainMap.TryGetValue(nb, out var nt) && nt != sw
                        && nt != BattleCellData.TerrainType.DeepWater
                        && nt != BattleCellData.TerrainType.Road
                        && nt != BattleCellData.TerrainType.Wall && GD.Randf() < 0.05f)
                    {
                        terrainMap[nb] = sw; elevationMap[nb] = 0;
                        waterCount++;
                        break; // 每个水格最多扩展 1 个邻居
                    }
                }
            }
        }

        // POI structures
        int poiType = context.PoiType;
        var rsc = poiType <= 2 ? new Vector2I(1, 3) : poiType == 3 ? new Vector2I(1, 3) : new Vector2I(2, 4);
        var wsc = poiType <= 2 ? new Vector2I(1, 2) : poiType == 3 ? new Vector2I(0, 2) : new Vector2I(1, 3);
        var fakeTpl = new BattleMapTemplate { RuinStructureCount = rsc, WallSegmentCount = wsc,
            WallSegmentLength = new Vector2I(2, 4), SmoothingPasses = 2 };
        GenRuinStructures(terrainMap, mapData.Width, mapData.Height, fakeTpl);
        GenWallSegments(terrainMap, mapData.Width, mapData.Height, fakeTpl);
        ScatterSpecialFeatures(terrainMap);
        SmoothTerrainMap(terrainMap, mapData.Width, mapData.Height, 2);
        FinalizeCells(mapData, terrainMap, elevationMap, BattleCellData.TerrainType.Grassland);
        mapData.TemplateName = "overworld_" + OverworldTerrain.GetName((OverworldTerrain.Type)context.Terrain);
        mapData.EnvironmentEvent = DeriveEnvironmentEvent(terrainMap);
        var zones2 = DeploymentZone.GenerateZones(mapData.Width, mapData.Height,
            context.Engagement, mapData.Cells);
        mapData.PlayerDeployment = zones2["player"].AsGodotArray();
        mapData.EnemyDeployment = zones2["enemy"].AsGodotArray();
        EnsureConnectivity(mapData, (int)context.Engagement);
        if (!string.IsNullOrEmpty(context.EnvironmentOverride))
            mapData.EnvironmentEvent = context.EnvironmentOverride;
        return mapData;
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    BattleMapData CreateMapData(Vector2I size, BattleMapTemplate tpl) => new()
    {
        Width = size.X, Height = size.Y, TemplateName = tpl.TemplateName,
    };

    /// <summary>判断地形是否为水域（不可通行或减速）</summary>
    static bool IsWaterTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.ShallowWater || t == BattleCellData.TerrainType.DeepWater;

    static BattleCellData.TerrainType MapOverworldToBattle(HexOverworldTile.TerrainType t) => t switch
    {
        HexOverworldTile.TerrainType.DeepWater => BattleCellData.TerrainType.DeepWater,
        HexOverworldTile.TerrainType.ShallowWater => BattleCellData.TerrainType.ShallowWater,
        HexOverworldTile.TerrainType.Sand => BattleCellData.TerrainType.Sand,
        HexOverworldTile.TerrainType.Plains => BattleCellData.TerrainType.Plains,
        HexOverworldTile.TerrainType.Grassland => BattleCellData.TerrainType.Grassland,
        HexOverworldTile.TerrainType.Forest => BattleCellData.TerrainType.Forest,
        HexOverworldTile.TerrainType.DenseForest => BattleCellData.TerrainType.DenseForest,
        HexOverworldTile.TerrainType.Hills => BattleCellData.TerrainType.Hills,
        HexOverworldTile.TerrainType.Mountain => BattleCellData.TerrainType.Mountain,
        HexOverworldTile.TerrainType.Snow => BattleCellData.TerrainType.Snow,
        HexOverworldTile.TerrainType.Swamp => BattleCellData.TerrainType.Swamp,
        HexOverworldTile.TerrainType.Savanna => BattleCellData.TerrainType.Savanna,
        HexOverworldTile.TerrainType.Road => BattleCellData.TerrainType.Road,
        HexOverworldTile.TerrainType.River => BattleCellData.TerrainType.ShallowWater,
        _ => BattleCellData.TerrainType.Plains,
    };

    static int MapOverworldElevation(float e) => e < 0.30f ? 0 : e > 0.65f ? 2 : 1;

    static BattleCellData.TerrainType TerrainMicroVariation(
        BattleCellData.TerrainType b, float moisture, float nv)
    {
        if (nv < 0.65f) return b;
        return b switch
        {
            BattleCellData.TerrainType.Grassland => moisture > 0.6f
                ? BattleCellData.TerrainType.Forest
                : nv > 0.85f ? BattleCellData.TerrainType.LuckyGrass : b,
            BattleCellData.TerrainType.Plains => moisture < 0.35f
                ? BattleCellData.TerrainType.Savanna : b,
            BattleCellData.TerrainType.Forest => nv > 0.85f
                ? BattleCellData.TerrainType.DenseForest : b,
            BattleCellData.TerrainType.Savanna => moisture > 0.55f
                ? BattleCellData.TerrainType.Grassland : b,
            BattleCellData.TerrainType.Swamp => nv > 0.9f
                ? BattleCellData.TerrainType.PoisonMushroom : b,
            _ => b,
        };
    }

    void ScatterSpecialFeatures(Dictionary<Vector2I, BattleCellData.TerrainType> tm)
    {
        var chances = new List<(BattleCellData.TerrainType, float)>
            { (BattleCellData.TerrainType.LuckyGrass, 0.02f), (BattleCellData.TerrainType.PoisonMushroom, 0.01f) };
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

    Dictionary<Vector2I, int> GenElevationMap(int w, int h, float bias)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = (int)GD.Randi(), Frequency = 0.08f,
        };
        var result = new Dictionary<Vector2I, int>();
        for (int q = 0; q < w; q++)
        {
            int qOff = Mathf.FloorToInt(q / 2.0f);
            for (int r = -qOff; r < h - qOff; r++)
            {
                float n = noise.GetNoise2D(q, r) + bias;
                result[new Vector2I(q, r)] = n > 0.35f ? 2 : n < -0.35f ? 0 : 1;
            }
        }
        return result;
    }

    Dictionary<Vector2I, BattleCellData.TerrainType> GenTerrainMap(
        int w, int h, BattleMapTemplate tpl)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = (int)GD.Randi(), Frequency = 0.12f,
        };
        float totalSec = 0f;
        foreach (var wt in tpl.SecondaryTerrains.Values) totalSec += wt;
        var result = new Dictionary<Vector2I, BattleCellData.TerrainType>();

        // 计算地图中心用于水域边缘限制
        float centerQ = w * 0.5f;
        float centerR = h * 0.5f;
        float maxDist = Mathf.Sqrt(centerQ * centerQ + centerR * centerR);

        for (int q = 0; q < w; q++)
        {
            int qOff = Mathf.FloorToInt(q / 2.0f);
            for (int r = -qOff; r < h - qOff; r++)
            {
                var key = new Vector2I(q, r);
                float raw = noise.GetNoise2D(q * 3.7f, r * 3.7f);
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
                    float distFromCenter = Mathf.Sqrt(
                        (q - centerQ) * (q - centerQ) + (r + qOff - centerR) * (r + qOff - centerR));
                    float edgeRatio = distFromCenter / maxDist;
                    if (edgeRatio < 0.65f)
                    {
                        // 非边缘区域：将水域替换为主地形或草地
                        chosen = tpl.PrimaryTerrain == BattleCellData.TerrainType.ShallowWater
                            ? BattleCellData.TerrainType.Grassland
                            : tpl.PrimaryTerrain;
                    }
                }

                result[key] = chosen;
            }
        }
        return result;
    }

    void ApplyLinearFeatures(Dictionary<Vector2I, BattleCellData.TerrainType> tm,
        Dictionary<Vector2I, int> em, int w, int h, BattleMapTemplate tpl)
    {
        if (tpl.HasRoad)
        {
            int roadR = h / 2;
            for (int q = 0; q < w; q++)
            {
                int qOff = Mathf.FloorToInt(q / 2.0f);
                var key = new Vector2I(q, roadR - qOff);
                if (tm.ContainsKey(key)) { tm[key] = BattleCellData.TerrainType.Road; em[key] = 1; }
            }
        }
        if (tpl.HasRiver)
        {
            // 河流沿地图边缘（左侧 1/4 处）纵向流过，宽度 1 格，不扩散
            int riverQ = Mathf.Max(1, w / 4);
            for (int rOff = 0; rOff < h + 2; rOff++)
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

    void GenTreeClusters(Dictionary<Vector2I, BattleCellData.TerrainType> tm, int w, int h, BattleMapTemplate tpl)
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

    void GenRuinStructures(Dictionary<Vector2I, BattleCellData.TerrainType> tm, int w, int h, BattleMapTemplate tpl)
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

    void GenWallSegments(Dictionary<Vector2I, BattleCellData.TerrainType> tm, int w, int h, BattleMapTemplate tpl)
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
    /// 生成据点结构：以地图中心为核心，生成六边形城墙环。
    /// 规则：
    /// - 城墙环上每隔一定间距放置塔楼（角落位置）
    /// - 每面墙中间放置城门（2-4个）
    /// - 城墙内侧紧邻处生成楼梯（朝向指向城墙外侧）
    /// - 连续城墙段必定在内侧生成至少一个楼梯
    /// </summary>
    void GenStrongholdStructure(Dictionary<Vector2I, BattleCellData.TerrainType> tm,
        Dictionary<Vector2I, int> em, int w, int h)
    {
        _staircaseFacings.Clear();

        int centerQ = w / 2;
        int centerR = h / 2 - Mathf.FloorToInt(centerQ / 2.0f);
        var center = new Vector2I(centerQ, centerR);

        int shortSide = Mathf.Min(w, h);
        int wallRadius = Mathf.Max(3, (int)(shortSide * 0.33f));

        var wallRing = HexUtils.GetRing(center.X, center.Y, wallRadius);
        var wallSet = new HashSet<Vector2I>();
        var validWallCells = new List<Vector2I>();

        foreach (var pos in wallRing)
        {
            if (!tm.ContainsKey(pos)) continue;
            if (IsWaterTerrain(tm[pos])) continue;
            validWallCells.Add(pos);
            wallSet.Add(pos);
        }

        if (validWallCells.Count < 6) return;

        // 塔楼：环上每隔 ~1/6 周长放一个
        int towerInterval = Mathf.Max(3, validWallCells.Count / 6);
        var towerPositions = new HashSet<Vector2I>();
        for (int i = 0; i < validWallCells.Count; i += towerInterval)
            towerPositions.Add(validWallCells[i]);

        // 城门：塔楼之间中点，共 2-4 个
        var gatePositions = new HashSet<Vector2I>();
        int gateInterval = Mathf.Max(4, validWallCells.Count / 4);
        int gateOffset = towerInterval / 2;
        for (int i = gateOffset; i < validWallCells.Count; i += gateInterval)
        {
            if (!towerPositions.Contains(validWallCells[i]))
                gatePositions.Add(validWallCells[i]);
        }

        // 放置城墙、塔楼、城门
        foreach (var pos in validWallCells)
        {
            if (towerPositions.Contains(pos))
            { tm[pos] = BattleCellData.TerrainType.Tower; em[pos] = 3; }
            else if (gatePositions.Contains(pos))
            { tm[pos] = BattleCellData.TerrainType.Gate; em[pos] = 1; }
            else
            { tm[pos] = BattleCellData.TerrainType.Rampart; em[pos] = 2; }
        }

        // 楼梯：城墙内侧，每段连续城墙至少 1 个
        var staircasePlaced = new HashSet<Vector2I>();
        int sinceLastStaircase = 0;
        int maxGap = Mathf.Max(2, towerInterval / 2);

        for (int i = 0; i < validWallCells.Count; i++)
        {
            var wallPos = validWallCells[i];
            sinceLastStaircase++;

            if (towerPositions.Contains(wallPos) || gatePositions.Contains(wallPos))
            { sinceLastStaircase = 0; continue; }

            bool force = sinceLastStaircase >= maxGap;
            bool random = GD.Randf() < 0.25f && sinceLastStaircase >= 2;

            if (force || random)
            {
                // 找内侧邻居（最靠近中心的方向）
                int bestDir = -1; float bestDist = float.MaxValue;
                for (int d = 0; d < 6; d++)
                {
                    var nb = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, d);
                    if (!tm.ContainsKey(nb) || wallSet.Contains(nb)) continue;
                    float dist = HexUtils.AxialDistance(nb, center);
                    if (dist < bestDist) { bestDist = dist; bestDir = d; }
                }

                if (bestDir >= 0)
                {
                    var stairPos = HexUtils.GetNeighbor(wallPos.X, wallPos.Y, bestDir);
                    if (tm.ContainsKey(stairPos) && !wallSet.Contains(stairPos)
                        && !staircasePlaced.Contains(stairPos) && !IsWaterTerrain(tm[stairPos]))
                    {
                        tm[stairPos] = BattleCellData.TerrainType.Staircase;
                        em[stairPos] = 1;
                        _staircaseFacings[stairPos] = (bestDir + 3) % 6; // 朝向城墙外侧
                        staircasePlaced.Add(stairPos);
                        sinceLastStaircase = 0;
                    }
                }
            }
        }

        // 城墙内部清理不合理地形
        var innerCells = new HashSet<Vector2I>();
        var bfsQueue = new List<Vector2I> { center };
        innerCells.Add(center);
        while (bfsQueue.Count > 0)
        {
            var cur = bfsQueue[0]; bfsQueue.RemoveAt(0);
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(cur.X, cur.Y, d);
                if (innerCells.Contains(nb) || wallSet.Contains(nb) || !tm.ContainsKey(nb)) continue;
                innerCells.Add(nb); bfsQueue.Add(nb);
            }
        }
        foreach (var pos in innerCells)
        {
            if (wallSet.Contains(pos) || staircasePlaced.Contains(pos)) continue;
            var t = tm[pos];
            if (t == BattleCellData.TerrainType.Mountain || t == BattleCellData.TerrainType.DeepWater
                || t == BattleCellData.TerrainType.DenseForest)
            {
                tm[pos] = GD.Randf() < 0.3f ? BattleCellData.TerrainType.Road : BattleCellData.TerrainType.Plains;
                em[pos] = 1;
            }
        }
    }

    /// <summary>判断模板是否为据点类型（需要生成城墙结构）</summary>
    static bool IsStrongholdTemplate(string name) =>
        name.Contains("stronghold") || name.Contains("siege") || name.Contains("fortress")
        || name.Contains("temple") || name.Contains("outpost") || name == "town_defense"
        || name == "castle_siege" || name == "pirate_cove";

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

    void SmoothTerrainMap(Dictionary<Vector2I, BattleCellData.TerrainType> tm, int w, int h, int passes)
    {
        var immune = new HashSet<BattleCellData.TerrainType>
        {
            BattleCellData.TerrainType.Wall, BattleCellData.TerrainType.Ruins,
            BattleCellData.TerrainType.DeepWater, BattleCellData.TerrainType.ShallowWater,
            BattleCellData.TerrainType.PoisonMushroom, BattleCellData.TerrainType.LuckyGrass,
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
        for (int q = 0; q < md.Width; q++)
        {
            int qOff = Mathf.FloorToInt(q / 2.0f);
            for (int r = -qOff; r < md.Height - qOff; r++)
            {
                var key = new Vector2I(q, r);
                var tt = tm.GetValueOrDefault(key, defaultTerrain);
                int elev = em.GetValueOrDefault(key, 1);
                var cell = BattleCellData.CreateFromType(tt, elev);
                if (tt == BattleCellData.TerrainType.DeepWater) cell.elevation = 0;
                else if (tt == BattleCellData.TerrainType.ShallowWater) cell.elevation = Mathf.Min(elev, 1);
                else if (tt == BattleCellData.TerrainType.Swamp) cell.elevation = Mathf.Min(elev, 1);
                else if (tt == BattleCellData.TerrainType.Mountain) cell.elevation = Mathf.Max(elev, 2);
                else if (tt == BattleCellData.TerrainType.Hills)
                    cell.elevation = GD.Randf() < 0.6f ? Mathf.Max(elev, 2) : Mathf.Max(elev, 1);
                else if (tt == BattleCellData.TerrainType.Rampart) cell.elevation = 2;
                else if (tt == BattleCellData.TerrainType.Tower) cell.elevation = 3;
                else if (tt == BattleCellData.TerrainType.Gate) cell.elevation = 1;
                else if (tt == BattleCellData.TerrainType.Staircase)
                {
                    cell.elevation = 1;
                    if (_staircaseFacings.TryGetValue(key, out int facing))
                        cell.facingDirection = facing;
                }
                md.Cells[Variant.From(key)] = cell;
            }
        }
    }
}
