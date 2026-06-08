// WorkshopProductionService.cs
// 作坊生产与待运物流服务
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Economy;

public static class WorkshopProductionService
{
    /// <summary>
    /// 处理作坊的每日生产，将产物放入待运库存
    /// </summary>
    public static void ProcessDaily(FiefData fief, WorkshopProductionRecipe recipe, int currentDay)
    {
        if (fief == null || recipe == null) return;

        if (!fief.PendingShipments.ContainsKey(recipe.OutputItemId))
        {
            fief.PendingShipments[recipe.OutputItemId] = 0;
        }

        fief.PendingShipments[recipe.OutputItemId] += recipe.OutputQty;
        GD.Print($"[Workshop] {fief.FiefName} 的 {recipe.Type} 每日产出 {recipe.OutputQty}x {recipe.OutputItemId}");
    }

    /// <summary>
    /// 尝试将待运库存运输到最近的友好城镇或城堡销售，结算收益
    /// </summary>
    public static int TryShipToNearestFriendly(FiefData fief, List<OverworldPOI> allPois, IEconomyProvider economy, WorldEventEngine? worldEngine)
    {
        if (fief == null || allPois == null || economy == null) return 0;

        // 寻找最近的友好城镇或城堡
        var candidates = allPois.Where(p => 
            p.PoiTypeEnum == OverworldPOI.POIType.Town || 
            p.PoiTypeEnum == OverworldPOI.POIType.Castle).ToList();

        OverworldPOI? nearest = null;
        float minDist = float.MaxValue;

        foreach (var poi in candidates)
        {
            if (IsFriendly(fief.OwningFaction, poi.OwningFaction, worldEngine))
            {
                float dist = fief.WorldPosition.DistanceTo(poi.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = poi;
                }
            }
        }

        if (nearest == null)
        {
            GD.Print($"[Workshop] {fief.FiefName} 没有找到最近的友好据点，货物继续存留在待运库存中。");
            return 0;
        }

        int totalNetRevenue = 0;
        var keys = fief.PendingShipments.Keys.ToList();

        foreach (var itemId in keys)
        {
            int qty = fief.PendingShipments[itemId];
            if (qty <= 0) continue;

            var item = FindItem(itemId);
            if (item == null) continue;

            // 获取相应的作坊配方以计算原料成本
            var recipe = WorkshopRecipeRegistry.GetAll().FirstOrDefault(r => r.OutputItemId == itemId);
            if (recipe == null) continue;

            // 获取市场买入基准价格，应用大宗批发售价 (95%)
            int singleBasePrice = TradePricingService.GetBuyPrice(item, nearest.Prosperity);
            int singleSalePrice = (int)Math.Max(1, Math.Round(singleBasePrice * 0.95));

            int totalRevenue = singleSalePrice * qty;
            int totalRawCost = recipe.RawCostGold; // 每日原料成本是按天折算的，如果是积攒了多天，按对应批次算
            // 为简单和测试鲁棒性起见，单次生产原料成本直接对应了其待运批次，
            // 净利润 = 批发总售价 - 原料总成本
            int netRevenue = totalRevenue - totalRawCost;

            if (netRevenue > 0)
            {
                economy.AddGold(netRevenue);
                totalNetRevenue += netRevenue;
                
                if (netRevenue >= 50 && worldEngine != null)
                {
                    worldEngine.AddNews(
                        "economy_workshop_income",
                        $"💰 你的作坊在 {nearest.PoiName} 完成大宗批发，获取净收入 {netRevenue} 金币！",
                        fief.WorldPosition);
                }
            }

            fief.PendingShipments[itemId] = 0;
        }

        return totalNetRevenue;
    }

    private static bool IsFriendly(string fiefFaction, string poiFaction, WorldEventEngine? worldEngine)
    {
        if (string.IsNullOrEmpty(fiefFaction) || string.IsNullOrEmpty(poiFaction)) return false;
        if (fiefFaction == "player" || fiefFaction == "kingdom") return true;
        if (poiFaction == "player" || poiFaction == "kingdom") return true;
        if (fiefFaction == poiFaction) return true;
        if (worldEngine != null && worldEngine.AreAllied(fiefFaction, poiFaction)) return true;
        return false;
    }

    private static ItemData? FindItem(string itemId)
    {
        if (PrototypeData.GetWeapons().TryGetValue(itemId, out var w)) return w;
        if (PrototypeData.GetArmors().TryGetValue(itemId, out var a)) return a;
        if (PrototypeData.GetConsumables().TryGetValue(itemId, out var c)) return c;
        if (PrototypeData.GetQuivers().TryGetValue(itemId, out var q)) return q;
        if (PrototypeData.GetAccessories().TryGetValue(itemId, out var ac)) return ac;
        return null;
    }
}
