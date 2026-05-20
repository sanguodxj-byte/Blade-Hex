// PoiFactory.cs
// POI 工厂 — 从世界数据创建和配置 OverworldPOI 实例
// 从 WorldGenerator 拆出：BuildPoisFromData 及类型转换工具
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// POI 工厂 — 负责从数据源创建 OverworldPOI 实例
/// 解析 Godot Collections.Dictionary 数据并生成完整的 POI 配置
/// </summary>
[GlobalClass]
public partial class PoiFactory : RefCounted
{
    // ========================================
    // POI 创建
    // ========================================

    /// <summary>
    /// 从 Godot Array 数据批量构建 POI 列表
    /// 每个元素为包含 POI 配置的 Godot Dictionary
    /// </summary>
    public List<OverworldPOI> BuildPoisFromData(Godot.Collections.Array dataArray)
    {
        var result = new List<OverworldPOI>();
        foreach (Godot.Collections.Dictionary entry in dataArray)
        {
            var poi = CreateFromDictionary(entry);
            if (poi != null) result.Add(poi);
        }
        return result;
    }

    /// <summary>
    /// 从单个 Godot Dictionary 创建 POI
    /// </summary>
    public OverworldPOI? CreateFromDictionary(Godot.Collections.Dictionary entry)
    {
        var poi = new OverworldPOI();
        poi.PoiName = entry.ContainsKey("poi_name") ? (string)entry["poi_name"] : "未命名";

        string typeStr = entry.ContainsKey("poi_type") ? (string)entry["poi_type"] : "VILLAGE";
        poi.PoiTypeEnum = PoiTypeFromString(typeStr);

        var posArr = (Godot.Collections.Array)entry["position"];
        poi.Position = new Vector2((float)posArr[0], (float)posArr[1]);

        poi.OwningFaction = entry.ContainsKey("owning_faction") ? (string)entry["owning_faction"] : "neutral";
        poi.Prosperity = entry.ContainsKey("prosperity") ? (int)entry["prosperity"] : 50;

        if (poi.PoiTypeEnum == OverworldPOI.POIType.Settlement)
        {
            poi.SettlementRaceValue = SettlementRaceFromString(entry.ContainsKey("settlement_race") ? (string)entry["settlement_race"] : "GOBLIN");
            poi.ThreatLevel = entry.ContainsKey("threat_level") ? (float)entry["threat_level"] : 0.5f;
            poi.RaidIntervalDays = entry.ContainsKey("raid_interval_days") ? (int)entry["raid_interval_days"] : 7;
            poi.MaxRaidingParties = entry.ContainsKey("max_raiding_parties") ? (int)entry["max_raiding_parties"] : 2;
        }

        if (poi.PoiTypeEnum == OverworldPOI.POIType.Lair)
        {
            poi.LairTypeValue = LairTypeFromString(entry.ContainsKey("lair_type") ? (string)entry["lair_type"] : "ANCIENT_TOMB");
            poi.LairLevel = entry.ContainsKey("lair_level") ? (int)entry["lair_level"] : 1;
        }

        if (poi.PoiTypeEnum == OverworldPOI.POIType.Town || poi.PoiTypeEnum == OverworldPOI.POIType.Village)
        {
            poi.HasTavern = entry.ContainsKey("has_tavern") ? (bool)entry["has_tavern"] : (poi.PoiTypeEnum == OverworldPOI.POIType.Town);
            poi.HasShop = entry.ContainsKey("has_shop") && (bool)entry["has_shop"];
            poi.HasBlacksmith = entry.ContainsKey("has_blacksmith") && (bool)entry["has_blacksmith"];
            poi.HasQuestBoard = !entry.ContainsKey("has_quest_board") || (bool)entry["has_quest_board"];
            poi.HasBarracks = entry.ContainsKey("has_barracks") && (bool)entry["has_barracks"];
        }

        if (poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
        {
            poi.CastleDefenseLevel = entry.ContainsKey("castle_defense_level") ? (int)entry["castle_defense_level"] : 2;
            poi.GarrisonMax = entry.ContainsKey("garrison_max") ? (int)entry["garrison_max"] : 50;
            poi.GarrisonCurrent = entry.ContainsKey("garrison_current") ? (int)entry["garrison_current"] : 30;
        }
        else
        {
            // 非城堡类型：根据类型设置合理的默认驻军
            int defaultMax = poi.PoiTypeEnum switch
            {
                OverworldPOI.POIType.Town => 80,
                OverworldPOI.POIType.Port => 50,
                OverworldPOI.POIType.Outpost => 40,
                OverworldPOI.POIType.Village => 25,
                OverworldPOI.POIType.Mine => 20,
                OverworldPOI.POIType.Farm => 15,
                OverworldPOI.POIType.Tavern => 10,
                OverworldPOI.POIType.Shrine => 8,
                _ => 0,
            };
            poi.GarrisonMax = entry.ContainsKey("garrison_max") ? (int)entry["garrison_max"] : defaultMax;
            poi.GarrisonCurrent = entry.ContainsKey("garrison_current") ? (int)entry["garrison_current"] : defaultMax;
        }

        return poi;
    }

    // ========================================
    // 类型转换工具
    // ========================================

    public static OverworldPOI.POIType PoiTypeFromString(string s) => s switch
    {
        "TOWN" => OverworldPOI.POIType.Town,
        "VILLAGE" => OverworldPOI.POIType.Village,
        "CASTLE" => OverworldPOI.POIType.Castle,
        "SETTLEMENT" => OverworldPOI.POIType.Settlement,
        "LAIR" => OverworldPOI.POIType.Lair,
        _ => OverworldPOI.POIType.Village
    };

    public static OverworldPOI.SettlementRace SettlementRaceFromString(string s) => s switch
    {
        "GOBLIN" => OverworldPOI.SettlementRace.Goblin,
        "KOBOLD" => OverworldPOI.SettlementRace.Kobold,
        "MINOTAUR" => OverworldPOI.SettlementRace.Minotaur,
        "SHADOW_CULT" => OverworldPOI.SettlementRace.ShadowCult,
        "BANDIT" => OverworldPOI.SettlementRace.Bandit,
        "ROBBER" => OverworldPOI.SettlementRace.Robber,
        "PIRATE" => OverworldPOI.SettlementRace.Pirate,
        _ => OverworldPOI.SettlementRace.Goblin
    };

    public static OverworldPOI.LairType LairTypeFromString(string s) => s switch
    {
        "DRAGON_LAIR" => OverworldPOI.LairType.DragonLair,
        "ANCIENT_TOMB" => OverworldPOI.LairType.AncientTomb,
        "RUINS" => OverworldPOI.LairType.Ruins,
        "GOLEM_FORGE" => OverworldPOI.LairType.GolemForge,
        "BANDIT_CAMP" => OverworldPOI.LairType.BanditCamp,
        "ROBBER_HIDEOUT" => OverworldPOI.LairType.RobberHideout,
        "PIRATE_COVE" => OverworldPOI.LairType.PirateCove,
        "RAIDER_OUTPOST" => OverworldPOI.LairType.RaiderOutpost,
        _ => OverworldPOI.LairType.AncientTomb
    };
}
