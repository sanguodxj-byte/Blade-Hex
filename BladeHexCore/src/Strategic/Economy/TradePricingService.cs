// TradePricingService.cs
// 市场交易定价服务：把经济锚点建议价转为买入/卖出价格。
using System;
using BladeHex.Data;

namespace BladeHex.Strategic.Economy;

/// <summary>
/// 交易定价服务。
///
/// JSON Price 仍保留为策划输入与兼容字段；实际市场可通过本服务使用经济锚点价格，
/// 再叠加繁荣度、声望、稀有度等倍率。这样装备价格可以随经济模型统一调整。
/// </summary>
public static class TradePricingService
{
    public static readonly EconomyPriceAnchor DefaultAnchor = EquipmentPriceAnchorService.CreateDefaultAnchor();

    /// <summary>获得不含商店加价的经济锚定基准价。</summary>
    public static int GetBasePrice(ItemData item, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int anchored = EquipmentPriceAnchorService.GetSuggestedPrice(item, anchor);
        double rarity = GetRarityMultiplier(item.ItemRarity);
        return Math.Max(1, RoundToTradeStep(anchored * rarity));
    }

    /// <summary>市场买入价：基准价 × 繁荣度加价 × 可选声望倍率。</summary>
    public static int GetBuyPrice(ItemData item, int prosperity = 50, float reputationMultiplier = 1.0f, EconomyPriceAnchor? anchor = null)
    {
        prosperity = Math.Clamp(prosperity, 0, 100);
        float markup = 1.0f + (100 - prosperity) * 0.005f;
        float mult = Math.Clamp(markup * reputationMultiplier, 0.8f, 1.8f);
        return Math.Max(1, RoundToTradeStep(GetBasePrice(item, anchor) * mult));
    }

    /// <summary>市场卖出价：基准价 × 繁荣度/声望回收倍率。</summary>
    public static int GetSellPrice(ItemData item, int prosperity = 50, float reputationMultiplier = 1.0f, EconomyPriceAnchor? anchor = null)
    {
        prosperity = Math.Clamp(prosperity, 0, 100);
        float mult = 0.35f + prosperity * 0.002f;
        mult = Math.Clamp(mult * reputationMultiplier, 0.25f, 0.65f);
        return Math.Max(1, RoundToTradeStep(GetBasePrice(item, anchor) * mult));
    }

    /// <summary>用于市场库存门槛。低繁荣只展示较便宜的经济锚定物品。</summary>
    public static int GetStockGatePrice(ItemData item, EconomyPriceAnchor? anchor = null)
        => GetBasePrice(item, anchor);

    private static double GetRarityMultiplier(ItemData.Rarity rarity) => rarity switch
    {
        ItemData.Rarity.Uncommon => 1.25,
        ItemData.Rarity.Rare => 1.75,
        ItemData.Rarity.Epic => 3.00,
        ItemData.Rarity.Legendary => 5.00,
        _ => 1.00,
    };

    private static int RoundToTradeStep(double value)
    {
        if (value < 20) return Math.Max(1, (int)Math.Round(value));
        if (value < 100) return (int)(Math.Round(value / 5.0) * 5);
        if (value < 500) return (int)(Math.Round(value / 10.0) * 10);
        return (int)(Math.Round(value / 25.0) * 25);
    }
}
