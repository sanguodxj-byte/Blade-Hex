// DiscoveryJournal.cs
// 发现日志 — 记录玩家已探索的 POI、遭遇的生物、领主、冒险者
// 仅文字记载，传奇生物在击败前只给故事性描述
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace BladeHex.Strategic.Encyclopedia;

/// <summary>
/// 发现日志 — 跟踪玩家在游戏中发现的所有实体。
/// 持久化到存档中，新游戏时根据出身国家自动填充领土内 POI。
/// </summary>
public class DiscoveryJournal
{
    // ============================================================================
    // 发现记录
    // ============================================================================

    /// <summary>已发现的 POI 名称集合</summary>
    public HashSet<string> DiscoveredPois { get; set; } = new();

    /// <summary>已遭遇的野怪类型 ID 集合（如 "goblin", "kobold", "bandit"）</summary>
    public HashSet<string> EncounteredCreatures { get; set; } = new();

    /// <summary>已遭遇的领主 HeroId 集合</summary>
    public HashSet<string> EncounteredLords { get; set; } = new();

    /// <summary>已遭遇的冒险者实体名集合</summary>
    public HashSet<string> EncounteredAdventurers { get; set; } = new();

    /// <summary>已遭遇的传奇生物 ID 集合（如 "dragon_ancient", "golem_titan"）</summary>
    public HashSet<string> EncounteredLegendary { get; set; } = new();

    /// <summary>已击败的传奇生物 ID 集合（击败后解锁完整信息）</summary>
    public HashSet<string> DefeatedLegendary { get; set; } = new();

    // ============================================================================
    // 发现 API
    // ============================================================================

    /// <summary>发现一个 POI</summary>
    public bool DiscoverPoi(string poiName)
    {
        if (string.IsNullOrEmpty(poiName)) return false;
        return DiscoveredPois.Add(poiName);
    }

    /// <summary>批量发现 POI（用于出身国家领土初始化）</summary>
    public void DiscoverPois(IEnumerable<string> poiNames)
    {
        foreach (var name in poiNames)
            DiscoveredPois.Add(name);
    }

    /// <summary>遭遇一种野怪</summary>
    public bool EncounterCreature(string creatureTypeId)
    {
        if (string.IsNullOrEmpty(creatureTypeId)) return false;
        return EncounteredCreatures.Add(creatureTypeId);
    }

    /// <summary>遭遇一位领主</summary>
    public bool EncounterLord(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return false;
        return EncounteredLords.Add(heroId);
    }

    /// <summary>遭遇一位冒险者</summary>
    public bool EncounterAdventurer(string entityName)
    {
        if (string.IsNullOrEmpty(entityName)) return false;
        return EncounteredAdventurers.Add(entityName);
    }

    /// <summary>遭遇一只传奇生物（仅解锁故事描述）</summary>
    public bool EncounterLegendary(string legendaryId)
    {
        if (string.IsNullOrEmpty(legendaryId)) return false;
        return EncounteredLegendary.Add(legendaryId);
    }

    /// <summary>击败一只传奇生物（解锁完整信息）</summary>
    public bool DefeatLegendary(string legendaryId)
    {
        if (string.IsNullOrEmpty(legendaryId)) return false;
        EncounteredLegendary.Add(legendaryId);
        return DefeatedLegendary.Add(legendaryId);
    }

    /// <summary>检查 POI 是否已发现</summary>
    public bool IsPoiDiscovered(string poiName) => DiscoveredPois.Contains(poiName);

    /// <summary>检查传奇生物是否已击败</summary>
    public bool IsLegendaryDefeated(string legendaryId) => DefeatedLegendary.Contains(legendaryId);

    // ============================================================================
    // 初始化 — 出身国家领土自动加入
    // ============================================================================

    /// <summary>
    /// 根据玩家出身国家，自动将该国领土内的所有 POI 加入已发现列表。
    /// </summary>
    public void InitializeFromOriginFaction(string factionId, List<OverworldPOI> allPois)
    {
        if (string.IsNullOrEmpty(factionId) || allPois == null) return;

        var factionPois = allPois.Where(p => p.OwningFaction == factionId);
        foreach (var poi in factionPois)
        {
            DiscoveredPois.Add(poi.PoiName);
        }
    }

    // ============================================================================
    // 序列化/反序列化
    // ============================================================================

    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            { "discovered_pois", new Godot.Collections.Array(DiscoveredPois.Select(s => (Variant)s).ToArray()) },
            { "encountered_creatures", new Godot.Collections.Array(EncounteredCreatures.Select(s => (Variant)s).ToArray()) },
            { "encountered_lords", new Godot.Collections.Array(EncounteredLords.Select(s => (Variant)s).ToArray()) },
            { "encountered_adventurers", new Godot.Collections.Array(EncounteredAdventurers.Select(s => (Variant)s).ToArray()) },
            { "encountered_legendary", new Godot.Collections.Array(EncounteredLegendary.Select(s => (Variant)s).ToArray()) },
            { "defeated_legendary", new Godot.Collections.Array(DefeatedLegendary.Select(s => (Variant)s).ToArray()) },
        };
    }

    public static DiscoveryJournal Deserialize(Godot.Collections.Dictionary data)
    {
        var journal = new DiscoveryJournal();
        if (data == null) return journal;

        if (data.ContainsKey("discovered_pois"))
        {
            var arr = data["discovered_pois"].AsGodotArray();
            foreach (var v in arr) journal.DiscoveredPois.Add(v.AsString());
        }
        if (data.ContainsKey("encountered_creatures"))
        {
            var arr = data["encountered_creatures"].AsGodotArray();
            foreach (var v in arr) journal.EncounteredCreatures.Add(v.AsString());
        }
        if (data.ContainsKey("encountered_lords"))
        {
            var arr = data["encountered_lords"].AsGodotArray();
            foreach (var v in arr) journal.EncounteredLords.Add(v.AsString());
        }
        if (data.ContainsKey("encountered_adventurers"))
        {
            var arr = data["encountered_adventurers"].AsGodotArray();
            foreach (var v in arr) journal.EncounteredAdventurers.Add(v.AsString());
        }
        if (data.ContainsKey("encountered_legendary"))
        {
            var arr = data["encountered_legendary"].AsGodotArray();
            foreach (var v in arr) journal.EncounteredLegendary.Add(v.AsString());
        }
        if (data.ContainsKey("defeated_legendary"))
        {
            var arr = data["defeated_legendary"].AsGodotArray();
            foreach (var v in arr) journal.DefeatedLegendary.Add(v.AsString());
        }

        return journal;
    }
}
