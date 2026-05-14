using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 城镇设施数据类 — 描述城镇中的可交互设施
/// </summary>
[GlobalClass]
public partial class TownFacility : Resource
{
    public enum FacilityType
    {
        Castle,
        Market,
        Tavern,
        Arena,
        Smithy,
        Training,
        Temple
    }

    [Export] public string FacilityName { get; set; } = "";
    [Export] public FacilityType CurrentFacilityType = FacilityType.Market;
    [Export] public bool IsAvailable { get; set; } = true;
    [Export] public string Description { get; set; } = "";
    [Export] public InteractionType.Type AssociatedInteractionType = InteractionType.Type.Leave;

    /// <summary>兼容：facility_type 作为 int</summary>
    [Export] public int FacilityTypeInt
    {
        get => (int)CurrentFacilityType;
        set => CurrentFacilityType = (FacilityType)value;
    }

    public TownFacility() { }

    public TownFacility(string name, FacilityType type, bool available = true)
    {
        FacilityName = name;
        CurrentFacilityType = type;
        IsAvailable = available;
        Description = GetDefaultDescription(type);
        AssociatedInteractionType = GetDefaultInteractionType(type);
    }

    public static string GetTypeName(FacilityType type) => type switch
    {
        FacilityType.Castle => "城堡",
        FacilityType.Market => "市场",
        FacilityType.Tavern => "酒馆",
        FacilityType.Arena => "竞技场",
        FacilityType.Smithy => "铁匠铺",
        FacilityType.Training => "训练场",
        FacilityType.Temple => "药师所",
        _ => "未知"
    };

    public static string GetTypeIcon(FacilityType type) => type switch
    {
        FacilityType.Castle => "castle",
        FacilityType.Market => "store",
        FacilityType.Tavern => "beer",
        FacilityType.Arena => "trophy",
        FacilityType.Smithy => "anvil",
        FacilityType.Training => "dumbbell",
        FacilityType.Temple => "church",
        _ => "building"
    };

    public static List<TownFacility> CreateDefaultFacilities() => new()
    {
        new TownFacility("领主厅", FacilityType.Castle),
        new TownFacility("市场", FacilityType.Market),
        new TownFacility("酒馆", FacilityType.Tavern),
        new TownFacility("竞技场", FacilityType.Arena),
        new TownFacility("铁匠铺", FacilityType.Smithy),
        new TownFacility("训练场", FacilityType.Training),
        new TownFacility("药师所", FacilityType.Temple)
    };

    public static List<TownFacility> CreateVillageFacilities() => new()
    {
        new TownFacility("布告栏", FacilityType.Castle),
        new TownFacility("杂货铺", FacilityType.Market),
        new TownFacility("旅店", FacilityType.Tavern)
    };

    public static List<TownFacility> CreatePortFacilities() => new()
    {
        new TownFacility("港务厅", FacilityType.Castle),
        new TownFacility("海商市场", FacilityType.Market),
        new TownFacility("水手酒馆", FacilityType.Tavern),
        new TownFacility("船坞", FacilityType.Smithy),
        new TownFacility("药师所", FacilityType.Temple)
    };

    public static List<TownFacility> CreateCastleFacilities() => new()
    {
        new TownFacility("领主厅", FacilityType.Castle),
        new TownFacility("军械库", FacilityType.Market),
        new TownFacility("兵营酒馆", FacilityType.Tavern),
        new TownFacility("铁匠铺", FacilityType.Smithy),
        new TownFacility("训练场", FacilityType.Training)
    };

    public static List<TownFacility> CreateOutpostFacilities() => new()
    {
        new TownFacility("哨所布告栏", FacilityType.Castle),
        new TownFacility("补给站", FacilityType.Market),
        new TownFacility("营帐", FacilityType.Tavern)
    };

    public static List<TownFacility> CreateTavernFacilities() => new()
    {
        new TownFacility("酒馆", FacilityType.Tavern),
        new TownFacility("杂货铺", FacilityType.Market)
    };

    public static List<TownFacility> CreateMineFacilities() => new()
    {
        new TownFacility("矿务处", FacilityType.Castle),
        new TownFacility("矿工商店", FacilityType.Market),
        new TownFacility("矿工酒馆", FacilityType.Tavern)
    };

    public static List<TownFacility> CreateShrineFacilities() => new()
    {
        new TownFacility("药师所", FacilityType.Temple),
        new TownFacility("药材铺", FacilityType.Market)
    };

    private static string GetDefaultDescription(FacilityType type) => type switch
    {
        FacilityType.Castle => "领主的居所，可以觐见领主、查看声望和处理封地事务",
        FacilityType.Market => "各种商品琳琅满目，可以购买和出售物品",
        FacilityType.Tavern => "冒险者的聚集地，可以查看布告栏接取委托、招募伙伴和打听消息",
        FacilityType.Arena => "展示实力的地方，赢得比赛获取奖品和声望",
        FacilityType.Smithy => "经验丰富的铁匠，可以修理和升级装备",
        FacilityType.Training => "训练场，花费金币提升经验",
        FacilityType.Temple => "药师所，可以治疗伤病和购买净化药水",
        _ => ""
    };

    private static InteractionType.Type GetDefaultInteractionType(FacilityType type) => type switch
    {
        FacilityType.Castle => InteractionType.Type.Quest,
        FacilityType.Market => InteractionType.Type.Trade,
        FacilityType.Tavern => InteractionType.Type.Talk,
        FacilityType.Arena => InteractionType.Type.Arena,
        FacilityType.Smithy => InteractionType.Type.Repair,
        FacilityType.Training => InteractionType.Type.Train,
        FacilityType.Temple => InteractionType.Type.Heal,
        _ => InteractionType.Type.Leave
    };
}
