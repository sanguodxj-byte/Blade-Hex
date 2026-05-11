using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 城镇设施数据类 — 描述城镇中的可交互设施
/// </summary>
public partial class TownFacility : RefCounted
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

    public string FacilityName = "";
    public FacilityType CurrentFacilityType = FacilityType.Market;
    public bool IsAvailable = true;
    public string Description = "";
    public InteractionType.Type AssociatedInteractionType = InteractionType.Type.Leave;

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
        FacilityType.Temple => "神殿",
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
        new TownFacility("神殿", FacilityType.Temple)
    };

    public static List<TownFacility> CreateVillageFacilities() => new()
    {
        new TownFacility("布告栏", FacilityType.Castle),
        new TownFacility("杂货铺", FacilityType.Market),
        new TownFacility("旅店", FacilityType.Tavern)
    };

    private static string GetDefaultDescription(FacilityType type) => type switch
    {
        FacilityType.Castle => "领主的居所，可以领取委托和报告任务",
        FacilityType.Market => "各种商品琳琅满目，可以购买和出售物品",
        FacilityType.Tavern => "冒险者的聚集地，可以招募伙伴和打听消息",
        FacilityType.Arena => "展示实力的地方，赢得比赛获取奖品和声望",
        FacilityType.Smithy => "经验丰富的铁匠，可以修理和升级装备",
        FacilityType.Training => "训练场，花费金币提升经验",
        FacilityType.Temple => "神圣的殿堂，可以治疗伤病和购买圣水",
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
