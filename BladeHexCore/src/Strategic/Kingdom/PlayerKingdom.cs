// PlayerKingdom.cs
// 玩家王国数据模型
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic.Kingdom;

/// <summary>
/// 玩家王国 — 玩家创建的势力
/// </summary>
public class PlayerKingdom
{
    /// <summary>王国 ID（复用 "player" 降低改动面）</summary>
    public string KingdomId { get; set; } = "player";

    /// <summary>王国显示名</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>家族姓</summary>
    public string FamilyName { get; set; } = "";

    /// <summary>旗帜颜色</summary>
    public Color BannerColor { get; set; } = new Color(0.2f, 0.4f, 0.8f);

    /// <summary>都城 POI 名</summary>
    public string CapitalPoiName { get; set; } = "";

    /// <summary>控制的 POI 名列表</summary>
    public List<string> ControlledPoiNames { get; set; } = new();

    /// <summary>领主 HeroId 列表（玩家 + 已分封 Companion）</summary>
    public List<string> LordHeroIds { get; set; } = new();

    /// <summary>王国法律</summary>
    public KingdomLaws Laws { get; set; } = new();

    /// <summary>创立天数</summary>
    public int FoundedDay { get; set; } = 1;

    /// <summary>检查是否控制某 POI</summary>
    public bool ControlsPoi(string poiName) => ControlledPoiNames.Contains(poiName);

    /// <summary>获取控制的 POI 数量</summary>
    public int PoiCount => ControlledPoiNames.Count;

    /// <summary>获取领主数量</summary>
    public int LordCount => LordHeroIds.Count;

    /// <summary>序列化</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        var controlledArr = new Godot.Collections.Array();
        foreach (var name in ControlledPoiNames)
            controlledArr.Add(name);

        var lordsArr = new Godot.Collections.Array();
        foreach (var id in LordHeroIds)
            lordsArr.Add(id);

        return new Godot.Collections.Dictionary
        {
            { "kingdom_id", KingdomId },
            { "display_name", DisplayName },
            { "family_name", FamilyName },
            { "banner_color", BannerColor },
            { "capital_poi_name", CapitalPoiName },
            { "controlled_poi_names", controlledArr },
            { "lord_hero_ids", lordsArr },
            { "laws", Laws.Serialize() },
            { "founded_day", FoundedDay }
        };
    }

    /// <summary>反序列化</summary>
    public static PlayerKingdom Deserialize(Godot.Collections.Dictionary data)
    {
        var kingdom = new PlayerKingdom
        {
            KingdomId = data.ContainsKey("kingdom_id") ? data["kingdom_id"].AsString() : "player",
            DisplayName = data.ContainsKey("display_name") ? data["display_name"].AsString() : "",
            FamilyName = data.ContainsKey("family_name") ? data["family_name"].AsString() : "",
            BannerColor = data.ContainsKey("banner_color") ? data["banner_color"].AsColor() : new Color(0.2f, 0.4f, 0.8f),
            CapitalPoiName = data.ContainsKey("capital_poi_name") ? data["capital_poi_name"].AsString() : "",
            FoundedDay = data.ContainsKey("founded_day") ? data["founded_day"].AsInt32() : 1
        };

        if (data.ContainsKey("controlled_poi_names"))
        {
            var arr = (Godot.Collections.Array)data["controlled_poi_names"];
            foreach (var item in arr)
                kingdom.ControlledPoiNames.Add(item.AsString());
        }

        if (data.ContainsKey("lord_hero_ids"))
        {
            var arr = (Godot.Collections.Array)data["lord_hero_ids"];
            foreach (var item in arr)
                kingdom.LordHeroIds.Add(item.AsString());
        }

        if (data.ContainsKey("laws"))
            kingdom.Laws = KingdomLaws.Deserialize(data["laws"].AsGodotDictionary());

        return kingdom;
    }
}
