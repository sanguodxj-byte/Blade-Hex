// TerrainVisualRegistry.cs
// 地形视觉画像注册表 — 全局 SSOT
//
// 查表：TerrainType -> TerrainVisualProfile
// 后续可替换为从 .tres 资源文件加载；目前内置一份默认表，保证代码层可独立编译运行。
using System.Collections.Generic;
using Godot;

namespace BladeHex.Map;

public static class TerrainVisualRegistry
{
    private static readonly Dictionary<HexOverworldTile.TerrainType, TerrainVisualProfile> _profiles = BuildDefault();

    /// <summary>查表；无配置时返回兜底的 Grassland profile，保证渲染器永不崩溃。</summary>
    public static TerrainVisualProfile Get(HexOverworldTile.TerrainType terrain)
    {
        return _profiles.TryGetValue(terrain, out var p) ? p : _profiles[HexOverworldTile.TerrainType.Grassland];
    }

    /// <summary>允许外部（例如从 .tres 加载）注入/覆盖 profile。</summary>
    public static void Register(TerrainVisualProfile profile)
    {
        _profiles[profile.Terrain] = profile;
    }

    /// <summary>遍历所有已注册地形（仅 overworld 渲染器预创建 bucket 用）。</summary>
    public static IEnumerable<TerrainVisualProfile> All() => _profiles.Values;

    // ============================================================================
    // 默认 profile 表
    //
    // OverworldKey    → 新资产命名（res://src/assets/tiles/overworld/），未交付前回退 Legacy
    // LegacyOverworldKey + LegacyOverworldVariantCount → 现有资产（res://src/assets/tiles/hex_terrain/）
    // BattleTopKey    → 战斗顶面贴图（res://src/assets/tiles/battle_ground/tops/）
    // BattleCliffKey  → 战斗侧面贴图
    //
    // 现有资产清单（hex_terrain/）：
    //   grassland_0
    //   forest_0/1/2
    //   rocky_land_0/1       → 丘陵/山地/岩石荒地 映射
    //   mountain_cave_0/1    → 山脉/雪地 映射
    //   pond_0               → 所有水域 映射
    //   swamp_0/1/2
    //   barren_land_0/1      → 稀树草原/平原 映射
    //   wasteland_0/1        → 沙漠/荒原 映射
    // ============================================================================

    private static Dictionary<HexOverworldTile.TerrainType, TerrainVisualProfile> BuildDefault()
    {
        var d = new Dictionary<HexOverworldTile.TerrainType, TerrainVisualProfile>();

        void Add(HexOverworldTile.TerrainType t, string display,
            string owKey, int owVariants,
            string legacyKey, int legacyVariants,
            string btKey, int btVariants,
            string cliffKey,
            Color dominant,
            float propDensity = 0f,
            params string[] propPack)
        {
            d[t] = new TerrainVisualProfile
            {
                Terrain = t,
                DisplayName = display,
                OverworldKey = owKey,
                OverworldVariantCount = owVariants,
                LegacyOverworldKey = legacyKey,
                LegacyOverworldVariantCount = legacyVariants,
                BattleTopKey = btKey,
                BattleTopVariantCount = btVariants,
                BattleCliffKey = cliffKey,
                DominantColor = dominant,
                PaletteDark = _DarkenColor(dominant, 0.55f),
                PaletteLight = _LightenColor(dominant, 0.30f),
                PropDensity = propDensity,
                BattlePropPack = new List<string>(propPack),
            };
        }

        // 自动从 dominant 派生调色板：暗影 = dominant × 0.55, 高光 = dominant + 30%
        // 后续可针对特殊地形手动覆盖（见末尾的 _OverridePalettes）

        // 水域 → 旧资产 pond
        Add(HexOverworldTile.TerrainType.DeepWater, "深水",
            owKey: "deep_water", owVariants: 1,
            legacyKey: "pond", legacyVariants: 1,
            btKey: "water_deep", btVariants: 1, cliffKey: "cliff_rock",
            dominant: new Color(0.18f, 0.30f, 0.55f));
        Add(HexOverworldTile.TerrainType.ShallowWater, "浅水",
            owKey: "shallow_water", owVariants: 1,
            legacyKey: "pond", legacyVariants: 1,
            btKey: "water_shallow", btVariants: 1, cliffKey: "cliff_dirt",
            dominant: new Color(0.30f, 0.45f, 0.70f));
        Add(HexOverworldTile.TerrainType.River, "河流",
            owKey: "river", owVariants: 1,
            legacyKey: "pond", legacyVariants: 1,
            btKey: "water_shallow", btVariants: 1, cliffKey: "cliff_dirt",
            dominant: new Color(0.25f, 0.42f, 0.68f));

        // 平坦 → 旧资产 grassland / barren_land
        Add(HexOverworldTile.TerrainType.Plains, "平原",
            owKey: "plains", owVariants: 2,
            legacyKey: "grassland", legacyVariants: 1,
            btKey: "grassland_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.72f, 0.68f, 0.48f),
            propDensity: 0.05f, propPack: new[] { "wildflower", "small_rock" });
        Add(HexOverworldTile.TerrainType.Grassland, "草地",
            owKey: "grassland", owVariants: 2,
            legacyKey: "grassland", legacyVariants: 1,
            btKey: "grassland_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.55f, 0.70f, 0.35f),
            propDensity: 0.10f, propPack: new[] { "wildflower", "bush_small" });
        Add(HexOverworldTile.TerrainType.Savanna, "稀树草原",
            owKey: "savanna", owVariants: 2,
            legacyKey: "barren_land", legacyVariants: 2,
            btKey: "savanna_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.70f, 0.65f, 0.30f),
            propDensity: 0.15f, propPack: new[] { "acacia_tree", "dry_bush" });

        // 森林 → 旧资产 forest
        Add(HexOverworldTile.TerrainType.Forest, "森林",
            owKey: "forest", owVariants: 3,
            legacyKey: "forest", legacyVariants: 3,
            btKey: "grassland_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.22f, 0.45f, 0.18f),
            propDensity: 0.45f, propPack: new[] { "oak_tree", "pine_tree", "bush_small", "mossy_rock" });
        Add(HexOverworldTile.TerrainType.DenseForest, "密林",
            owKey: "dense_forest", owVariants: 3,
            legacyKey: "forest", legacyVariants: 3,
            btKey: "grassland_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.12f, 0.30f, 0.08f),
            propDensity: 0.70f, propPack: new[] { "oak_tree", "pine_tree", "fallen_log", "mossy_rock" });
        Add(HexOverworldTile.TerrainType.Jungle, "丛林",
            owKey: "jungle", owVariants: 2,
            legacyKey: "forest", legacyVariants: 3,
            btKey: "jungle_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.15f, 0.35f, 0.10f),
            propDensity: 0.60f, propPack: new[] { "jungle_tree", "large_fern", "vine_cluster" });
        Add(HexOverworldTile.TerrainType.Taiga, "针叶林",
            owKey: "taiga", owVariants: 2,
            legacyKey: "forest", legacyVariants: 3,
            btKey: "taiga_top", btVariants: 2, cliffKey: "cliff_rock",
            dominant: new Color(0.25f, 0.35f, 0.30f),
            propDensity: 0.40f, propPack: new[] { "pine_tree", "fir_tree", "snow_rock" });

        // 沼泽 → 旧资产 swamp
        Add(HexOverworldTile.TerrainType.Swamp, "沼泽",
            owKey: "swamp", owVariants: 3,
            legacyKey: "swamp", legacyVariants: 3,
            btKey: "swamp_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.38f, 0.48f, 0.28f),
            propDensity: 0.30f, propPack: new[] { "swamp_tree", "reed_cluster", "moss_patch" });
        Add(HexOverworldTile.TerrainType.Bog, "冻土沼泽",
            owKey: "bog", owVariants: 2,
            legacyKey: "swamp", legacyVariants: 3,
            btKey: "swamp_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.35f, 0.40f, 0.38f),
            propDensity: 0.20f, propPack: new[] { "dead_tree", "frost_reed" });

        // 荒地 → 旧资产 wasteland / rocky_land
        Add(HexOverworldTile.TerrainType.Wasteland, "荒原",
            owKey: "wasteland", owVariants: 2,
            legacyKey: "wasteland", legacyVariants: 2,
            btKey: "wasteland_top", btVariants: 2, cliffKey: "cliff_dirt",
            dominant: new Color(0.65f, 0.55f, 0.45f),
            propDensity: 0.05f, propPack: new[] { "dry_bush", "cracked_rock" });
        Add(HexOverworldTile.TerrainType.Rocky, "岩石荒地",
            owKey: "rocky", owVariants: 2,
            legacyKey: "rocky_land", legacyVariants: 2,
            btKey: "rocky_top", btVariants: 2, cliffKey: "cliff_rock",
            dominant: new Color(0.45f, 0.45f, 0.50f),
            propDensity: 0.15f, propPack: new[] { "boulder", "small_rock" });

        // 沙漠 → 旧资产 wasteland
        Add(HexOverworldTile.TerrainType.Sand, "沙漠",
            owKey: "sand", owVariants: 2,
            legacyKey: "wasteland", legacyVariants: 2,
            btKey: "sand_top", btVariants: 2, cliffKey: "cliff_sand",
            dominant: new Color(0.85f, 0.75f, 0.50f),
            propDensity: 0.05f, propPack: new[] { "cactus", "desert_rock" });

        // 山地 → 旧资产 mountain_cave / rocky_land
        Add(HexOverworldTile.TerrainType.Hills, "丘陵",
            owKey: "hills", owVariants: 2,
            legacyKey: "rocky_land", legacyVariants: 2,
            btKey: "grassland_top", btVariants: 2, cliffKey: "cliff_rock",
            dominant: new Color(0.58f, 0.52f, 0.38f),
            propDensity: 0.20f, propPack: new[] { "boulder", "pine_tree" });
        Add(HexOverworldTile.TerrainType.Mountain, "山地",
            owKey: "mountain", owVariants: 2,
            legacyKey: "mountain_cave", legacyVariants: 2,
            btKey: "rocky_top", btVariants: 2, cliffKey: "cliff_rock",
            dominant: new Color(0.40f, 0.38f, 0.42f),
            propDensity: 0.30f, propPack: new[] { "boulder", "cliff_chunk" });
        Add(HexOverworldTile.TerrainType.MountainSnow, "雪山",
            owKey: "mountain_snow", owVariants: 1,
            legacyKey: "mountain_cave", legacyVariants: 2,
            btKey: "snow_top", btVariants: 1, cliffKey: "cliff_snow",
            dominant: new Color(0.85f, 0.88f, 0.92f),
            propDensity: 0.20f, propPack: new[] { "snow_rock", "frozen_pine" });

        // 寒带 → 旧资产 mountain_cave（现有白/灰色最接近）
        Add(HexOverworldTile.TerrainType.Snow, "雪地",
            owKey: "snow", owVariants: 2,
            legacyKey: "mountain_cave", legacyVariants: 2,
            btKey: "snow_top", btVariants: 2, cliffKey: "cliff_snow",
            dominant: new Color(0.92f, 0.95f, 0.98f),
            propDensity: 0.10f, propPack: new[] { "snow_rock", "frozen_pine" });
        Add(HexOverworldTile.TerrainType.Ice, "冰原",
            owKey: "ice", owVariants: 1,
            legacyKey: "pond", legacyVariants: 1,
            btKey: "ice_top", btVariants: 1, cliffKey: "cliff_snow",
            dominant: new Color(0.75f, 0.85f, 0.95f),
            propDensity: 0.02f, propPack: new[] { "ice_shard" });

        // 道路 → 旧资产 crossroads
        Add(HexOverworldTile.TerrainType.Road, "道路",
            owKey: "road", owVariants: 1,
            legacyKey: "crossroads", legacyVariants: 1,
            btKey: "road_top", btVariants: 1, cliffKey: "cliff_dirt",
            dominant: new Color(0.65f, 0.55f, 0.38f));

        return d;
    }

    // ─────────────────────────────────────────────
    // 调色板辅助方法
    // ─────────────────────────────────────────────

    /// <summary>颜色变暗：朝墨褐色（#1A1208）方向插值</summary>
    private static Color _DarkenColor(Color c, float amount)
    {
        var ink = new Color(0.10f, 0.07f, 0.03f); // 墨褐
        return new Color(
            Mathf.Lerp(c.R, ink.R, amount),
            Mathf.Lerp(c.G, ink.G, amount),
            Mathf.Lerp(c.B, ink.B, amount),
            1.0f
        );
    }

    /// <summary>颜色变亮：朝暖奶黄色（#EDE8D8）方向插值</summary>
    private static Color _LightenColor(Color c, float amount)
    {
        var cream = new Color(0.93f, 0.91f, 0.85f); // 暖奶黄
        return new Color(
            Mathf.Lerp(c.R, cream.R, amount),
            Mathf.Lerp(c.G, cream.G, amount),
            Mathf.Lerp(c.B, cream.B, amount),
            1.0f
        );
    }
}
