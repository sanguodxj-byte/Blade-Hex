// PoiTownAdapter.cs
// POI → 临时 OverworldTown 适配器。
//
// 大地图上的 POI 是 Core/战略层数据，TownPanel 与二级面板当前消费的是
// OverworldTown 节点。这里集中维护两者的映射，避免 OverworldScene3D 继续堆积
// POI 类型 switch 与字段复制逻辑。
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

/// <summary>
/// 将 <see cref="OverworldPOI"/> 适配为交互面板可消费的临时 <see cref="OverworldTown"/>。
/// </summary>
internal static class PoiTownAdapter
{
    /// <summary>
    /// 创建仅用于交互 UI 的临时城镇节点。
    /// </summary>
    public static OverworldTown CreateTownNode(OverworldPOI poi)
    {
        var town = new OverworldTown
        {
            TownName = poi.PoiName,
            Prosperity = poi.Prosperity,
            Faction = poi.OwningFaction,
            Garrison = poi.GarrisonCurrent > 0 ? poi.GarrisonCurrent : poi.GarrisonMax,
            Visible = false,
        };

        ApplyFacilities(town, poi.PoiTypeEnum);
        return town;
    }

    /// <summary>
    /// 是否沿用当前的一层 TownPanel 入口。
    /// </summary>
    public static bool OpensTownPanelDirectly(OverworldPOI.POIType poiType) => poiType switch
    {
        OverworldPOI.POIType.Town => true,
        OverworldPOI.POIType.Village => true,
        OverworldPOI.POIType.Castle => true,
        OverworldPOI.POIType.Port => true,
        _ => false,
    };

    private static void ApplyFacilities(OverworldTown town, OverworldPOI.POIType poiType)
    {
        switch (poiType)
        {
            case OverworldPOI.POIType.Town:
                town.TownType = "town";
                town.SetupDefaultFacilities();
                break;
            case OverworldPOI.POIType.Village:
            case OverworldPOI.POIType.Farm:
                town.SetupVillageFacilities();
                break;
            case OverworldPOI.POIType.Castle:
                town.SetupCastleFacilities();
                break;
            case OverworldPOI.POIType.Tavern:
                town.SetupTavernFacilities();
                break;
            case OverworldPOI.POIType.Outpost:
                town.SetupOutpostFacilities();
                break;
            case OverworldPOI.POIType.Mine:
                town.SetupMineFacilities();
                break;
            case OverworldPOI.POIType.Shrine:
                town.SetupShrineFacilities();
                break;
            case OverworldPOI.POIType.Port:
                town.SetupPortFacilities();
                break;
            default:
                town.SetupVillageFacilities();
                break;
        }
    }
}
