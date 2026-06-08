// POIBattlePresetRegistry.cs
// 比例尺统一 — POI 类型/子类型 → 战斗 preset + Scale + Footprint 的映射
//
// 见 .kiro/specs/scale-unification/design.md
//
// 用法：
//   var preset = POIBattlePresetRegistry.Resolve(poi);
//   string templateName = preset.TemplateName;            // 战斗模板
//   POIScale scale       = preset.Scale;                   // 视觉/灯光/战斗规模档
//   string footprintTpl  = preset.FootprintTemplate;      // 大地图占用形状
//   var battleSize       = preset.OverrideBattleSize ?? POIScaleTable.Get(scale).BattleSize;

using Godot;
using System.Collections.Generic;
using static BladeHex.Strategic.OverworldPOI;

namespace BladeHex.Strategic;

/// <summary>POI 战斗预设记录</summary>
public readonly record struct POIBattlePreset(
    string TemplateName,                          // BattleMapGenerator 战斗模板 key
    POIScale Scale,                                // 决定 Marker / Light / BattleSize
    string FootprintTemplate,                     // 决定大地图 hex 占用形状
    BattleContext.BattleSize? OverrideBattleSize, // 通常 null，用 Scale 默认；Stronghold 等显式 override
    string DisplayName);

/// <summary>POIBattlePreset 注册表</summary>
public static class POIBattlePresetRegistry
{
    /// <summary>(POIType, subType int) → preset。subType 视情况是 SettlementRace 或 LairType 的 int 值；0 表示通配。</summary>
    static readonly Dictionary<(POIType, int), POIBattlePreset> _table = new()
    {
        // ========================================
        // Tiny — 单格小据点
        // ========================================
        [(POIType.Mine, 0)]     = new("plain_field", POIScale.Tiny,   "solo", null, "矿场冲突"),
        [(POIType.Farm, 0)]     = new("plain_field", POIScale.Tiny,   "solo", null, "农庄突袭"),

        // ========================================
        // Small — 多格小聚落
        // ========================================
        [(POIType.Village, 0)]                          = new("village_defense",    POIScale.Small,  "village_3",      null, "村庄防御"),
        [(POIType.Settlement, (int)SettlementRace.Bandit)]    = new("bandit_stronghold", POIScale.Small,  "forest_camp_3",  null, "山贼营地"),
        [(POIType.Settlement, (int)SettlementRace.Robber)]    = new("bandit_stronghold", POIScale.Small,  "forest_camp_3",  null, "劫匪窝点"),
        [(POIType.Settlement, (int)SettlementRace.Goblin)]    = new("goblin_camp",       POIScale.Small,  "swamp_camp_3",   null, "哥布林营地"),
        [(POIType.Settlement, (int)SettlementRace.Kobold)]    = new("gobold_mine",       POIScale.Small,  "mountain_dig_3", null, "狗头人矿坑"),
        [(POIType.Lair, (int)LairType.AncientTomb)]     = new("ancient_tomb",      POIScale.Small,  "ruins_3",        null, "远古墓穴"),
        [(POIType.Lair, (int)LairType.Ruins)]           = new("ruins_exploration", POIScale.Small,  "ruins_3",        null, "遗迹探索"),
        [(POIType.Lair, (int)LairType.BanditCamp)]      = new("bandit_stronghold", POIScale.Small,  "forest_camp_3",  null, "山贼据点"),
        [(POIType.Lair, (int)LairType.RobberHideout)]   = new("bandit_stronghold", POIScale.Small,  "forest_camp_3",  null, "劫匪藏身处"),
        [(POIType.Lair, (int)LairType.PirateCove)]      = new("pirate_cove",       POIScale.Small,  "coastal_3",      null, "海寇巢穴"),

        // ========================================
        // Medium — 中型据点（含变形 footprint）
        // ========================================
        [(POIType.Town, 0)]                             = new("town_defense",       POIScale.Medium, "town_5",          null, "城镇防御战"),
        [(POIType.Lair, (int)LairType.GolemForge)]      = new("golem_forge",        POIScale.Medium, "ruins_5",         null, "魔像工坊"),
        [(POIType.Lair, (int)LairType.RaiderOutpost)]   = new("raider_outpost",     POIScale.Medium, "plains_5",        null, "劫掠据点"),

        // ========================================
        // Large — 大型据点 / boss
        // ========================================
        [(POIType.Castle, 0)]                                 = new("castle_siege",        POIScale.Large, "mountain_castle_5", BattleContext.BattleSize.Stronghold, "城堡攻防"),
        [(POIType.Lair, (int)LairType.DragonLair)]            = new("dragon_lair",         POIScale.Large, "mountain_lair_5",   null, "巨龙巢穴"),
        [(POIType.Settlement, (int)SettlementRace.Minotaur)]  = new("minotaur_stronghold", POIScale.Large, "fortress_7",        BattleContext.BattleSize.Stronghold, "牛头人石堡"),
        [(POIType.Settlement, (int)SettlementRace.ShadowCult)]= new("shadow_cult_temple",  POIScale.Large, "swamp_temple_5",    null, "暗影教团祭坛"),
        [(POIType.Settlement, (int)SettlementRace.Pirate)]    = new("pirate_cove",         POIScale.Large, "port_city_7",       null, "海寇大寨"),
    };

    /// <summary>查询某 POI 的 preset</summary>
    public static POIBattlePreset Resolve(OverworldPOI poi)
    {
        if (poi.PoiTypeEnum == POIType.Town && poi.IsPortCity)
        {
            return new POIBattlePreset("pirate_cove", POIScale.Medium, "port_city_4", null, "港口袭扰");
        }

        int subType = poi.PoiTypeEnum switch
        {
            POIType.Settlement => (int)poi.SettlementRaceValue,
            POIType.Lair       => (int)poi.LairTypeValue,
            _ => 0,
        };

        // 1. 精确匹配 (type, subType)
        if (_table.TryGetValue((poi.PoiTypeEnum, subType), out var preset)) return preset;

        // 2. 通配匹配 (type, 0)
        if (_table.TryGetValue((poi.PoiTypeEnum, 0), out preset)) return preset;

        // 3. 兜底：plain_field + solo + Tiny
        GD.PushWarning($"[POIBattlePresetRegistry] 未找到 POI '{poi.PoiName}' (type={poi.PoiTypeEnum}, sub={subType}) 的 preset，回退默认");
        return new POIBattlePreset("plain_field", POIScale.Tiny, "solo", null, "未知遭遇");
    }

    /// <summary>仅按 type 查默认 preset（存档恢复用）</summary>
    public static POIBattlePreset Default(POIType type)
    {
        if (_table.TryGetValue((type, 0), out var preset)) return preset;
        return new POIBattlePreset("plain_field", POIScale.Tiny, "solo", null, "未知");
    }

    /// <summary>查询 POI 的 Scale（不需要完整 preset 时的便捷接口）</summary>
    public static POIScale ScaleOf(OverworldPOI poi) => Resolve(poi).Scale;

    /// <summary>查询 POI 的实际战斗规模（含 OverrideBattleSize 处理）</summary>
    public static BattleContext.BattleSize BattleSizeOf(OverworldPOI poi)
    {
        var preset = Resolve(poi);
        return preset.OverrideBattleSize ?? POIScaleTable.Get(preset.Scale).BattleSize;
    }

    public static IReadOnlyDictionary<(POIType, int), POIBattlePreset> All => _table;
}
