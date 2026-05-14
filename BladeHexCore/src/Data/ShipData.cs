// ShipData.cs
// 船只数据模型 — 定义船只类型、属性和费用
using Godot;

namespace BladeHex.Data;

/// <summary>船只类型</summary>
public enum ShipType
{
    Raft,       // 木筏 — 便宜但慢，容量小
    Sloop,      // 单桅帆船 — 均衡型
    Galleon,    // 大帆船 — 贵但容量大
}

/// <summary>
/// 船只数据 — 存储船只的属性和状态
/// </summary>
[GlobalClass]
public partial class ShipData : Resource
{
    [Export] public ShipType Type { get; set; } = ShipType.Raft;
    [Export] public string ShipName { get; set; } = "无名之舟";

    /// <summary>海上移速乘数（相对于基础海上速度）</summary>
    [Export] public float Speed { get; set; } = 0.8f;

    /// <summary>货物容量（额外背包格数）</summary>
    [Export] public int Capacity { get; set; } = 10;

    /// <summary>最大耐久</summary>
    [Export] public int MaxDurability { get; set; } = 100;

    /// <summary>当前耐久</summary>
    [Export] public int Durability { get; set; } = 100;

    /// <summary>购买价格</summary>
    [Export] public int BuyCost { get; set; } = 500;

    /// <summary>每次维修费用（恢复满耐久）</summary>
    [Export] public int RepairCost { get; set; } = 100;

    /// <summary>租赁费用（每日）</summary>
    [Export] public int RentCostPerDay { get; set; } = 50;

    /// <summary>是否为租赁（到期归还）</summary>
    [Export] public bool IsRented { get; set; } = false;

    /// <summary>租赁剩余天数</summary>
    [Export] public int RentDaysRemaining { get; set; } = 0;

    // ========================================
    // 工厂方法
    // ========================================

    public static ShipData CreateRaft()
    {
        return new ShipData
        {
            Type = ShipType.Raft,
            ShipName = "木筏",
            Speed = 0.8f,
            Capacity = 10,
            MaxDurability = 60,
            Durability = 60,
            BuyCost = 500,
            RepairCost = 80,
            RentCostPerDay = 30,
        };
    }

    public static ShipData CreateSloop()
    {
        return new ShipData
        {
            Type = ShipType.Sloop,
            ShipName = "单桅帆船",
            Speed = 1.2f,
            Capacity = 30,
            MaxDurability = 120,
            Durability = 120,
            BuyCost = 2000,
            RepairCost = 300,
            RentCostPerDay = 80,
        };
    }

    public static ShipData CreateGalleon()
    {
        return new ShipData
        {
            Type = ShipType.Galleon,
            ShipName = "大帆船",
            Speed = 1.0f,
            Capacity = 80,
            MaxDurability = 200,
            Durability = 200,
            BuyCost = 8000,
            RepairCost = 800,
            RentCostPerDay = 200,
        };
    }

    /// <summary>获取所有可购买船只模板</summary>
    public static ShipData[] GetShopInventory()
    {
        return [CreateRaft(), CreateSloop(), CreateGalleon()];
    }

    /// <summary>船只是否损坏（耐久为 0）</summary>
    public bool IsBroken => Durability <= 0;

    /// <summary>修复船只到满耐久</summary>
    public void Repair() => Durability = MaxDurability;

    /// <summary>受到损伤</summary>
    public void TakeDamage(int amount) => Durability = System.Math.Max(0, Durability - amount);
}
