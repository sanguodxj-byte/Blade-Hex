// PortService.cs
// 港口设施规则：租船出海。
using System;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 港口规则服务。租船入口不依赖 Godot UI 节点，可由大地图或其他入口复用。
/// </summary>
public static class PortService
{
    /// <summary>
    /// 根据港口繁荣度计算一次性租船费用。
    /// 保留当前玩法约定：繁荣港口费用更高但默认提供更好的船。
    /// </summary>
    public static int CalculateRentCost(int prosperity)
    {
        var shipType = prosperity >= 50 ? ShipType.Sloop : ShipType.Raft;
        return FacilityPricingService.GetShipRentCost(prosperity, shipType);
    }

    /// <summary>
    /// 根据港口繁荣度创建租赁船只。
    /// </summary>
    public static ShipData CreateRentalShip(int prosperity)
    {
        var ship = prosperity >= 50 ? ShipData.CreateSloop() : ShipData.CreateRaft();
        ship.IsRented = true;
        ship.RentDaysRemaining = 1;
        return ship;
    }

    /// <summary>
    /// 执行租船出海：扣费、设置船只、切换海上状态。
    /// </summary>
    public static FacilityServiceResult RentShip(
        int prosperity,
        Func<int, bool> spendGold,
        Action<ShipData> assignShip,
        Action<bool> setAtSea)
    {
        int cost = CalculateRentCost(prosperity);
        if (!spendGold(cost))
            return FacilityServiceResult.Fail("金币不足，无法租船出海。");

        var ship = CreateRentalShip(prosperity);
        assignShip(ship);
        setAtSea(true);

        return FacilityServiceResult.Ok(
            $"已租用{ship.ShipName}出海，扣除 {cost} 金币。",
            goldSpent: cost,
            affectedItems: 1);
    }
}
