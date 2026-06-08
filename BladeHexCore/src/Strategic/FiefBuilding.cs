// FiefBuilding.cs
// 封地建筑数据 — 可放置在六边格上的城防/经济建筑
using Godot;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

[GlobalClass]
public partial class FiefBuilding : Resource
{
    // ============================================================================
    // 建筑类型
    // ============================================================================
    public enum BuildingType
    {
        // 防御建筑（格内）
        ArrowTower,     // 箭塔：每回合自动射击
        WatchTower,     // 瞭望塔：先攻+4，防突袭
        MagicTower,     // 魔法塔：AOE法术攻击
        TrapPit,        // 陷阱坑：首个进入敌人受伤+倒地

        // 防御建筑（边缘）
        WoodFence,      // 木栅栏：阻挡移动
        StoneWall,      // 石墙：阻挡+半掩体
        Fortification,  // 城墙：阻挡+全掩体
        Gate,           // 城门：可开关通道
        Barricade,      // 拒马：反骑兵

        // 经济建筑（格内）
        Farmland,       // 农田：+5食物/日
        Market,         // 市集：+10金/日，繁荣+5
        Barracks,       // 兵营：驻军上限+8
        Smithy,         // 铁匠坊：驻军装备+1
        LordManor,      // 领主宅邸：中心格，自动放置
        BlacksmithWorkshop,    // 武器坊:产 武器装备
        BrewWorkshop,          // 酒坊:产 啤酒
        TextileWorkshop,       // 织布坊:产 布料
        TanneryWorkshop,       // 制革坊:产 皮料
    }

    // ============================================================================
    // 数据字段
    // ============================================================================
    [Export] public BuildingType Type { get; set; } = BuildingType.WoodFence;
    [Export] public int HexIndex { get; set; } = 0;         // 放置在哪个格（0=中心，1-6=外围）
    [Export] public int EdgeDirection { get; set; } = -1;   // 边缘建筑朝向（0-5，-1=非边缘）
    [Export] public int Level { get; set; } = 1;            // 建筑等级
    [Export] public int CurrentHp { get; set; } = -1;       // 当前HP（-1=满血）
    [Export] public bool IsUnderConstruction { get; set; }   // 是否在建造中
    [Export] public int ConstructionDaysLeft { get; set; }   // 剩余建造天数

    // ============================================================================
    // 属性查询
    // ============================================================================
    public bool IsEdgeBuilding => EdgeDirection >= 0;

    public int MaxHp
    {
        get
        {
            var cfg = BuildingDataLoader.GetConfig(Type);
            if (cfg != null) return cfg.BaseHp + Level * cfg.HpPerLevel;
            return 50;
        }
    }

    public int BuildCost
    {
        get
        {
            var cfg = BuildingDataLoader.GetConfig(Type);
            return FiefEconomyPricingService.GetBuildCost(Type, cfg?.Cost ?? 100);
        }
    }

    public int BuildDays
    {
        get
        {
            var cfg = BuildingDataLoader.GetConfig(Type);
            return cfg?.Days ?? 3;
        }
    }

    /// <summary>城防战中每回合的自动攻击伤害（0=不攻击）</summary>
    public int AutoAttackDamage
    {
        get
        {
            var cfg = BuildingDataLoader.GetConfig(Type);
            if (cfg != null && cfg.Attack > 0) return cfg.Attack + Level * cfg.AttackPerLevel;
            return 0;
        }
    }

    /// <summary>城防战中的攻击范围</summary>
    public int AttackRange
    {
        get
        {
            var cfg = BuildingDataLoader.GetConfig(Type);
            if (cfg != null && cfg.Range > 0) return cfg.Range + Level * cfg.RangePerLevel;
            return 0;
        }
    }

    /// <summary>是否为边缘类型建筑</summary>
    public static bool IsEdgeType(BuildingType type)
    {
        var cfg = BuildingDataLoader.GetConfig(type);
        if (cfg != null) return cfg.IsEdge;
        return type switch
        {
            BuildingType.WoodFence or BuildingType.StoneWall or BuildingType.Fortification
                or BuildingType.Gate or BuildingType.Barricade => true,
            _ => false,
        };
    }

    /// <summary>获取建筑显示名</summary>
    public string GetDisplayName()
    {
        var cfg = BuildingDataLoader.GetConfig(Type);
        return cfg?.Name ?? "未知建筑";
    }

    /// <summary>获取建筑描述</summary>
    public string GetDescription()
    {
        var cfg = BuildingDataLoader.GetConfig(Type);
        if (cfg == null) return "";
        // 对于攻击型建筑，动态生成描述
        if (cfg.Attack > 0)
            return $"{cfg.Description.TrimEnd('。')}，伤害{AutoAttackDamage}，射程{AttackRange}格。";
        return cfg.Description;
    }

    // ============================================================================
    // 建造进度
    // ============================================================================
    public void StartConstruction()
    {
        IsUnderConstruction = true;
        ConstructionDaysLeft = BuildDays;
    }

    /// <summary>每日推进建造，返回true表示完工</summary>
    public bool AdvanceConstruction()
    {
        if (!IsUnderConstruction) return false;
        ConstructionDaysLeft--;
        if (ConstructionDaysLeft <= 0)
        {
            IsUnderConstruction = false;
            CurrentHp = MaxHp;
            return true;
        }
        return false;
    }
}
