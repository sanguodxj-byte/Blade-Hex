// FiefData.cs
// 封地数据 — 玩家拥有的六边形领地，含建筑、驻军、经济
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

[GlobalClass]
public partial class FiefData : Resource
{
    // ============================================================================
    // 基础信息
    // ============================================================================
    [Export] public string FiefId { get; set; } = "";
    [Export] public string FiefName { get; set; } = "无名领地";
    [Export] public string OwningFaction { get; set; } = "";
    [Export] public Vector2I CenterHex { get; set; }
    [Export] public Vector2 WorldPosition { get; set; }

    // ============================================================================
    // 经济属性
    // ============================================================================
    [Export] public int Prosperity { get; set; } = 30;
    [Export] public int Population { get; set; } = 50;
    [Export] public int GarrisonCount { get; set; } = 0;
    [Export] public int GarrisonMax { get; set; } = 8;
    [Export] public int WallLevel { get; set; } = 0; // 0=无, 1=木栅, 2=石墙, 3=城墙

    // ============================================================================
    // 建筑列表
    // ============================================================================
    [Export] public Godot.Collections.Array<FiefBuilding> Buildings { get; set; } = new();

    // ============================================================================
    // 计算属性
    // ============================================================================
    public int DailyIncome => FiefEconomyPricingService.GetDailyIncome(this);
    public int DailyFood => FiefEconomyPricingService.GetDailyFood(this);
    public int DefenseRating => WallLevel * 10 + GetTowerCount() * 5 + GarrisonCount * 2;

    public int TaxRate => 20; // 上缴领主的比例（未来可根据声望调整）
    public int NetIncome => DailyIncome - DailyIncome * TaxRate / 100;

    // ============================================================================
    // 建筑查询
    // ============================================================================
    public int GetBuildingCount(FiefBuilding.BuildingType type)
    {
        int count = 0;
        foreach (var b in Buildings)
            if (b.Type == type) count++;
        return count;
    }

    public int GetBuildingBonus(FiefBuilding.BuildingType type, int bonusPerBuilding)
    {
        return GetBuildingCount(type) * bonusPerBuilding;
    }

    public int GetTowerCount()
    {
        return GetBuildingCount(FiefBuilding.BuildingType.ArrowTower)
             + GetBuildingCount(FiefBuilding.BuildingType.MagicTower);
    }

    // ============================================================================
    // 建筑操作
    // ============================================================================
    public bool CanBuild(FiefBuilding.BuildingType type)
    {
        if (FiefBuilding.IsEdgeType(type))
            return true; // 边缘建筑不占格位，数量不限（但同一边不能重复，由调用方检查）

        // 格内建筑：中心格已被领主宅邸占据，最多6个外围格
        int mainBuildingCount = 0;
        foreach (var b in Buildings)
            if (!b.IsEdgeBuilding) mainBuildingCount++;
        return mainBuildingCount < 7; // 1中心 + 6外围
    }

    /// <summary>检查指定格位是否已被占用</summary>
    public bool IsHexOccupied(int hexIndex)
    {
        foreach (var b in Buildings)
            if (!b.IsEdgeBuilding && b.HexIndex == hexIndex) return true;
        return false;
    }

    /// <summary>检查指定边缘是否已有建筑</summary>
    public bool IsEdgeOccupied(int hexIndex, int edgeDirection)
    {
        foreach (var b in Buildings)
            if (b.IsEdgeBuilding && b.HexIndex == hexIndex && b.EdgeDirection == edgeDirection) return true;
        return false;
    }

    public void AddBuilding(FiefBuilding building)
    {
        Buildings.Add(building);
        // 更新驻军上限
        if (building.Type == FiefBuilding.BuildingType.Barracks)
            GarrisonMax += 8;
        // 更新繁荣度
        if (building.Type == FiefBuilding.BuildingType.Market)
            Prosperity = System.Math.Min(100, Prosperity + 5);
    }

    // ============================================================================
    // 每日结算
    // ============================================================================
    public FiefDailyReport ProcessDay()
    {
        var report = new FiefDailyReport
        {
            GoldEarned = NetIncome,
            FoodProduced = DailyFood,
            FoodConsumed = GarrisonCount, // 每个驻军每天消耗1食物
        };

        // 繁荣度自然增长（有市集时更快）
        if (Prosperity < 100 && GetBuildingCount(FiefBuilding.BuildingType.Market) > 0)
            Prosperity = System.Math.Min(100, Prosperity + 1);

        // 人口增长（繁荣度高时）
        if (Prosperity > 50 && Population < 200)
            Population += 1;

        return report;
    }

    // ============================================================================
    // 被攻击
    // ============================================================================
    public void OnRaided(int severity)
    {
        var rng = new System.Random();
        Prosperity = System.Math.Max(0, Prosperity - severity);
        Population = System.Math.Max(10, Population - severity / 2);
        // 随机摧毁一个建筑（非领主宅邸）
        if (Buildings.Count > 1 && rng.Next(100) < severity)
        {
            // 找一个非宅邸的建筑摧毁
            for (int i = Buildings.Count - 1; i >= 0; i--)
            {
                if (Buildings[i].Type != FiefBuilding.BuildingType.LordManor)
                {
                    Buildings.RemoveAt(i);
                    break;
                }
            }
        }
    }
}

/// <summary>封地每日结算报告</summary>
public struct FiefDailyReport
{
    public int GoldEarned;
    public int FoodProduced;
    public int FoodConsumed;
}
