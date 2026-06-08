// SmugglingService.cs
// 走私业务逻辑服务
using System;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Economy;

public static class SmugglingService
{
    /// <summary>
    /// 计算走私被巡逻队抓到的概率
    /// </summary>
    public static double CalculateDetectionChance(OverworldPOI town, int playerLevel)
    {
        if (town == null) return 0.05;
        double chance = 0.1 + 0.005 * town.GarrisonCurrent - 0.002 * playerLevel;
        return Math.Max(0.05, chance);
    }

    /// <summary>
    /// 尝试走私购入商品
    /// </summary>
    public static SmuggleResult TryBuySmuggle(
        ItemData item, 
        int qty, 
        OverworldPOI town, 
        int playerLevel, 
        int currentDay, 
        IEconomyProvider economy,
        WorldEventEngine? worldEngine, 
        ReputationTracker? reputation)
    {
        if (item == null || town == null || economy == null)
            return new SmuggleResult(false, 0, 0, "无效参数", 0, 0);

        double detectionChance = CalculateDetectionChance(town, playerLevel);
        var rng = new Random();

        // 检定被抓
        if (rng.NextDouble() < detectionChance)
        {
            int goldCost = (int)Math.Round(TradePricingService.GetBuyPrice(item, town.Prosperity) * 0.8 * qty);
            
            // 被抓连锁反应：款项没收（金币扣除），不给商品，扣除各项声望
            economy.SpendGold(goldCost);
            
            if (reputation != null)
                reputation.AddReputation(town.OwningFaction, -10);

            if (worldEngine != null)
            {
                worldEngine.AdjustRelation("player", town.OwningFaction, -15);
                worldEngine.Influence.Add("player", -5, "走私被巡逻队抓获扣除影响力");
                worldEngine.AddNews(
                    "economy_smuggling_caught",
                    $"🚨 突发！兵团在 {town.PoiName} 走私购入大宗 {item.ItemName} 被守军当场人赃并获，罚没资金 {goldCost} 金币并面临追捕！",
                    town.Position);
            }

            return new SmuggleResult(false, -goldCost, 0, "被巡逻发现", -10, -5);
        }

        // 走私买入成功：打8折
        int finalPrice = (int)Math.Round(TradePricingService.GetBuyPrice(item, town.Prosperity) * 0.8 * qty);
        if (economy.Gold < finalPrice)
            return new SmuggleResult(false, 0, 0, "资金不足", 0, 0);

        economy.SpendGold(finalPrice);
        return new SmuggleResult(true, -finalPrice, qty, "", 0, 0);
    }

    /// <summary>
    /// 尝试走私出售商品
    /// </summary>
    public static SmuggleResult TrySellSmuggle(
        ItemData item, 
        int qty, 
        OverworldPOI town, 
        int playerLevel, 
        int currentDay, 
        IEconomyProvider economy,
        WorldEventEngine? worldEngine, 
        ReputationTracker? reputation)
    {
        if (item == null || town == null || economy == null)
            return new SmuggleResult(false, 0, 0, "无效参数", 0, 0);

        double detectionChance = CalculateDetectionChance(town, playerLevel);
        var rng = new Random();

        // 检定被抓
        if (rng.NextDouble() < detectionChance)
        {
            // 被抓连锁反应：没收全部待售商品（由前端背包负责移除），且不予结账
            if (reputation != null)
                reputation.AddReputation(town.OwningFaction, -10);

            if (worldEngine != null)
            {
                worldEngine.AdjustRelation("player", town.OwningFaction, -15);
                worldEngine.Influence.Add("player", -5, "走私出售被巡逻队抓获扣除影响力");
                worldEngine.AddNews(
                    "economy_smuggling_caught",
                    $"🚨 突发！兵团在 {town.PoiName} 走私销售大宗 {item.ItemName} 时遭遇巡逻卫队拦截，货物全数罚没并面临追缉！",
                    town.Position);
            }

            return new SmuggleResult(false, 0, -qty, "被巡逻发现", -10, -5);
        }

        // 走私卖出成功：1.1倍溢价
        int finalPrice = (int)Math.Round(TradePricingService.GetSellPrice(item, town.Prosperity) * 1.1 * qty);
        economy.AddGold(finalPrice);

        return new SmuggleResult(true, finalPrice, -qty, "", 0, 0);
    }
}
