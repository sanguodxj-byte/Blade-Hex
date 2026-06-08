// EncyclopediaService.cs
// 百科全书纯数据静态查询层
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;

namespace BladeHex.Strategic.Encyclopedia;

public static class EncyclopediaService
{
    public static List<HeroData> GetAllHeroes(HeroRegistry r)
    {
        if (r == null) return new List<HeroData>();
        return r.AllHeroes.ToList();
    }

    public static Dictionary<string, List<HeroData>> GetAllFamilies(HeroRegistry r)
    {
        if (r == null) return new Dictionary<string, List<HeroData>>();
        return r.AllHeroes
            .Where(h => !string.IsNullOrEmpty(h.FamilyName))
            .GroupBy(h => h.FamilyName)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public static List<NationConfig> GetAllFactions(List<NationConfig> nations)
    {
        if (nations == null) return new List<NationConfig>();
        return nations.ToList();
    }

    public static List<ItemData> GetAllItems()
    {
        var items = new List<ItemData>();
        items.AddRange(PrototypeData.GetWeapons().Values);
        items.AddRange(PrototypeData.GetArmors().Values);
        items.AddRange(PrototypeData.GetConsumables().Values);
        items.AddRange(PrototypeData.GetQuivers().Values);
        items.AddRange(PrototypeData.GetAccessories().Values);
        return items;
    }

    public static List<OverworldPOI> GetAllKnownPois(List<OverworldPOI> pois)
    {
        if (pois == null) return new List<OverworldPOI>();
        // 过滤出城镇、村庄、城堡、港口等供玩家查阅
        return pois.Where(p =>
            p.PoiTypeEnum == OverworldPOI.POIType.Town ||
            p.PoiTypeEnum == OverworldPOI.POIType.Castle ||
            p.PoiTypeEnum == OverworldPOI.POIType.Village
        ).ToList();
    }

    // T03: Get all races for encyclopedia
    public static List<RaceData> GetAllRaces()
    {
        return RaceData.GetAllRaces().ToList();
    }
}
