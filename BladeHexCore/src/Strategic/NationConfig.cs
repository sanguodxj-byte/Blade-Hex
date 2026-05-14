// NationConfig.cs
// 国家/势力配置 — 数据驱动的国家定义
// 每个国家的生态偏好、POI 模板、兵种池、贸易品等全部配置化
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 国家/势力配置 — 定义世界中的一个政治实体
/// 可作为 Godot Resource (.tres) 序列化，也可在代码中构造
/// </summary>
[GlobalClass]
public partial class NationConfig : Resource
{
    // ========================================
    // 基础信息
    // ========================================

    /// <summary>唯一标识（如 "kingdom_central", "elf_silverleaf"）</summary>
    [Export] public string Id { get; set; } = "";

    /// <summary>显示名称（如 "中央王国", "银叶精灵国"）</summary>
    [Export] public string DisplayName { get; set; } = "";

    /// <summary>种族（"human", "elf", "dwarf", "orc", "goblin", "kobold", "minotaur", "shadow_cult"）</summary>
    [Export] public string Race { get; set; } = "human";

    /// <summary>是否为主要国家（主要国家优先分配大生态区）</summary>
    [Export] public bool IsMajorNation { get; set; } = true;

    // ========================================
    // 生态偏好
    // ========================================

    /// <summary>偏好生态类型（有序，第一个最偏好）</summary>
    public BiomeType[] PreferredBiomes = [];

    /// <summary>领土最小面积（tile 数，太小的生态区容纳不下）</summary>
    [Export] public int MinTerritoryTiles { get; set; } = 500;

    /// <summary>是否必须在大陆主体（避免被分到小岛上）</summary>
    [Export] public bool RequiresMainland { get; set; } = true;

    // ========================================
    // 人口与规模
    // ========================================

    /// <summary>人口规模因子（影响 POI 数量、军事实力）</summary>
    [Export] public float PopulationScale { get; set; } = 1.0f;

    /// <summary>每 1000 tile 的 POI 密度</summary>
    [Export] public float PoiDensityPer1000Tiles { get; set; } = 3.0f;

    // ========================================
    // POI 模板
    // ========================================

    /// <summary>可放置的 POI 类型模板（如 "human_town", "elf_outpost"）</summary>
    [Export] public string[] PoiTemplates = [];

    /// <summary>首都 POI 模板（每个国家有且仅有一个首都）</summary>
    [Export] public string CapitalTemplate { get; set; } = "";

    // ========================================
    // 军事与经济
    // ========================================

    /// <summary>可招募兵种池</summary>
    [Export] public string[] RecruitPool = [];

    /// <summary>特产贸易品</summary>
    [Export] public string[] TradeGoods = [];

    /// <summary>建筑风格标识（用于渲染）</summary>
    [Export] public string BuildingStyle { get; set; } = "stone";

    /// <summary>基础军事实力</summary>
    [Export] public float BaseMilitaryPower { get; set; } = 50.0f;

    // ========================================
    // 遭遇与生态
    // ========================================

    /// <summary>领土内常见遭遇池（敌人模板 ID）</summary>
    [Export] public string[] EncounterPool = [];

    /// <summary>领土内特有资源</summary>
    [Export] public string[] UniqueResources = [];

    /// <summary>领土基础危险等级（0~1）</summary>
    [Export] public float BaseDangerLevel { get; set; } = 0.1f;

    // ========================================
    // 外交
    // ========================================

    /// <summary>初始外交关系（nationId → 关系值 -100~100）</summary>
    public Dictionary<string, int> InitialRelations { get; set; } = new();

    // ========================================
    // 工厂方法 — 预设国家配置
    // ========================================

    /// <summary>获取默认的国家配置列表（用于新游戏）</summary>
    public static List<NationConfig> GetDefaultNations()
    {
        return
        [
            // === 人类王国 ===
            new NationConfig
            {
                Id = "kingdom_central", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("human"), Race = "human",
                IsMajorNation = true,
                PreferredBiomes = [BiomeType.Plains, BiomeType.Coastal],
                MinTerritoryTiles = 600, PopulationScale = 1.2f,
                PoiDensityPer1000Tiles = 8.0f,
                CapitalTemplate = "human_capital",
                PoiTemplates = ["human_town", "human_village", "human_castle"],
                RecruitPool = ["knight", "spearman", "archer", "militia"],
                TradeGoods = ["wheat", "iron", "horse", "cloth"],
                BuildingStyle = "stone_castle",
                EncounterPool = ["bandit", "wolf_pack", "deserter"],
                UniqueResources = ["wheat", "iron_ore"],
                BaseDangerLevel = 0.1f, BaseMilitaryPower = 60.0f,
            },
            new NationConfig
            {
                Id = "kingdom_east", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("human"), Race = "human",
                IsMajorNation = true,
                PreferredBiomes = [BiomeType.Plains, BiomeType.Forest],
                MinTerritoryTiles = 400, PopulationScale = 0.9f,
                PoiDensityPer1000Tiles = 7.0f,
                CapitalTemplate = "human_capital",
                PoiTemplates = ["human_town", "human_village", "human_castle"],
                RecruitPool = ["knight", "crossbowman", "swordsman"],
                TradeGoods = ["wine", "timber", "wool"],
                BuildingStyle = "stone_castle",
                EncounterPool = ["bandit", "wild_boar", "outlaw"],
                UniqueResources = ["timber", "wine"],
                BaseDangerLevel = 0.15f, BaseMilitaryPower = 50.0f,
            },

            // === 精灵王国 ===
            new NationConfig
            {
                Id = "elf_silverleaf", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("elf"), Race = "elf",
                IsMajorNation = true,
                PreferredBiomes = [BiomeType.Forest, BiomeType.Jungle],
                MinTerritoryTiles = 400, PopulationScale = 0.8f,
                PoiDensityPer1000Tiles = 5.0f,
                CapitalTemplate = "elf_capital",
                PoiTemplates = ["elf_city", "elf_outpost", "elf_shrine"],
                RecruitPool = ["ranger", "druid", "elf_archer", "blade_dancer"],
                TradeGoods = ["moonsilver", "herb", "enchanted_bow", "elixir"],
                BuildingStyle = "treehouse",
                EncounterPool = ["treant", "giant_spider", "forest_spirit"],
                UniqueResources = ["moonsilver", "rare_herb"],
                BaseDangerLevel = 0.2f, BaseMilitaryPower = 45.0f,
            },

            // === 矮人王国 ===
            new NationConfig
            {
                Id = "dwarf_frostcrown", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("dwarf"), Race = "dwarf",
                IsMajorNation = true,
                PreferredBiomes = [BiomeType.Mountain, BiomeType.Tundra],
                MinTerritoryTiles = 300, PopulationScale = 0.7f,
                PoiDensityPer1000Tiles = 4.0f,
                CapitalTemplate = "dwarf_capital",
                PoiTemplates = ["dwarf_fortress", "dwarf_mine", "dwarf_outpost"],
                RecruitPool = ["dwarf_warrior", "dwarf_crossbow", "dwarf_ironbreaker"],
                TradeGoods = ["heavy_armor", "weapon", "gem", "ale"],
                BuildingStyle = "underground_fortress",
                EncounterPool = ["goblin_raider", "cave_troll", "rock_elemental"],
                UniqueResources = ["mithril_ore", "gem", "deep_iron"],
                BaseDangerLevel = 0.3f, BaseMilitaryPower = 55.0f,
            },

            // === 兽人部落 ===
            new NationConfig
            {
                Id = "orc_bloodfang", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("orc"), Race = "orc",
                IsMajorNation = true,
                PreferredBiomes = [BiomeType.Wasteland, BiomeType.Plains],
                MinTerritoryTiles = 350, PopulationScale = 0.8f,
                PoiDensityPer1000Tiles = 4.0f,
                CapitalTemplate = "orc_stronghold",
                PoiTemplates = ["orc_camp", "orc_fortress", "orc_arena"],
                RecruitPool = ["orc_berserker", "orc_shaman", "boar_rider", "orc_archer"],
                TradeGoods = ["bone_armor", "war_banner", "beast_hide"],
                BuildingStyle = "bone_camp",
                EncounterPool = ["ogre", "hyena_pack", "sand_wurm"],
                UniqueResources = ["beast_hide", "war_trophy"],
                BaseDangerLevel = 0.5f, BaseMilitaryPower = 50.0f,
            },

            // === 小势力 ===
            new NationConfig
            {
                Id = "goblin_tribes", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("goblin"), Race = "goblin",
                IsMajorNation = false,
                PreferredBiomes = [BiomeType.Forest, BiomeType.Mountain, BiomeType.Swamp],
                MinTerritoryTiles = 100, PopulationScale = 0.3f,
                RequiresMainland = false,
                PoiDensityPer1000Tiles = 8.0f,
                CapitalTemplate = "goblin_warren",
                PoiTemplates = ["goblin_camp", "goblin_cave"],
                RecruitPool = ["goblin_warrior", "goblin_archer", "goblin_shaman"],
                TradeGoods = ["scrap_metal", "stolen_goods"],
                BuildingStyle = "wooden_palisade",
                EncounterPool = ["goblin_warrior", "goblin_archer", "goblin_wolf_rider"],
                BaseDangerLevel = 0.4f, BaseMilitaryPower = 15.0f,
            },
            new NationConfig
            {
                Id = "kobold_miners", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("kobold"), Race = "kobold",
                IsMajorNation = false,
                PreferredBiomes = [BiomeType.Mountain, BiomeType.Forest],
                MinTerritoryTiles = 80, PopulationScale = 0.2f,
                RequiresMainland = false,
                PoiDensityPer1000Tiles = 6.0f,
                CapitalTemplate = "kobold_mine",
                PoiTemplates = ["kobold_mine", "kobold_trap_nest"],
                RecruitPool = ["kobold_trapper", "kobold_sapper"],
                TradeGoods = ["raw_ore", "trap_parts"],
                BuildingStyle = "tunnel",
                EncounterPool = ["kobold_trapper", "kobold_sapper", "mine_spider"],
                BaseDangerLevel = 0.35f, BaseMilitaryPower = 10.0f,
            },
            new NationConfig
            {
                Id = "minotaur_clans", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("minotaur"), Race = "minotaur",
                IsMajorNation = false,
                PreferredBiomes = [BiomeType.Wasteland, BiomeType.Mountain],
                MinTerritoryTiles = 120, PopulationScale = 0.3f,
                RequiresMainland = false,
                PoiDensityPer1000Tiles = 4.0f,
                CapitalTemplate = "minotaur_fortress",
                PoiTemplates = ["minotaur_fortress", "minotaur_arena"],
                RecruitPool = ["minotaur_warrior", "minotaur_champion"],
                TradeGoods = ["stone_weapon", "war_horn"],
                BuildingStyle = "stone_fort",
                EncounterPool = ["minotaur_warrior", "minotaur_champion"],
                BaseDangerLevel = 0.6f, BaseMilitaryPower = 25.0f,
            },
            new NationConfig
            {
                Id = "shadow_cult", DisplayName = FactionNameGenerator.GenerateFactionNameByRace("shadow_cult"), Race = "shadow_cult",
                IsMajorNation = false,
                PreferredBiomes = [BiomeType.Swamp, BiomeType.Tundra],
                MinTerritoryTiles = 80, PopulationScale = 0.2f,
                RequiresMainland = false,
                PoiDensityPer1000Tiles = 5.0f,
                CapitalTemplate = "cult_temple",
                PoiTemplates = ["cult_altar", "cult_hideout"],
                RecruitPool = ["cultist", "shadow_mage", "undead_thrall"],
                TradeGoods = ["cursed_item", "dark_tome"],
                BuildingStyle = "black_stone",
                EncounterPool = ["cultist", "undead_skeleton", "shadow_beast"],
                BaseDangerLevel = 0.7f, BaseMilitaryPower = 20.0f,
            },
        ];
    }
}
