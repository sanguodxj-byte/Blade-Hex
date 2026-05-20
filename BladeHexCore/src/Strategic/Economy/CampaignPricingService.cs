// CampaignPricingService.cs
// 战役模式经济定价服务：清理准备阶段雇佣、医官救治、通关奖励等硬编码金币公式。
using System;
using BladeHex.Data;

namespace BladeHex.Strategic.Economy;

/// <summary>
/// 战役模式关卡经济输入。
///
/// 该类型避免 Core 直接依赖 Frontend 的 CampaignLevelDef，使定价公式可单测、可被任意战役实现复用。
/// </summary>
public readonly record struct CampaignEconomyContext(
    int LevelIndex,
    int EnemyLevel,
    int EnemyCount,
    int Difficulty,
    int BattleSize,
    bool IsBoss = false)
{
    public int NormalizedLevelIndex => Math.Max(0, LevelIndex);
    public int NormalizedEnemyLevel => Math.Max(1, EnemyLevel);
    public int NormalizedEnemyCount => Math.Max(1, EnemyCount);
    public int NormalizedDifficulty => Math.Clamp(Difficulty, 0, 2);
    public int NormalizedBattleSize => Math.Clamp(BattleSize, 0, 3);
}

/// <summary>
/// 战役模式经济定价服务。
///
/// 与 TradePricingService 的关系：
/// - TradePricingService 负责“物品”买卖价格；
/// - CampaignPricingService 负责“战役进度”语境中的雇佣、救治、关卡奖励、战役商店繁荣度。
///
/// 公式锚点仍来自 EconomySimulation 的修正口粮模型，确保战役金币流和大地图装备价格处在同一数量级。
/// </summary>
public static class CampaignPricingService
{
    public static readonly EconomyPriceAnchor DefaultAnchor = EquipmentPriceAnchorService.CreateDefaultAnchor();

    /// <summary>战役商店虚拟繁荣度。随关卡推进逐步提高，影响 TradePricingService 买卖价和库存门槛。</summary>
    public static int GetShopProsperity(CampaignEconomyContext context)
    {
        int prosperity = 35 + context.NormalizedLevelIndex * 6 + context.NormalizedDifficulty * 8 + context.NormalizedBattleSize * 4;
        if (context.IsBoss) prosperity += 8;
        return Math.Clamp(prosperity, 30, 95);
    }

    /// <summary>战役佣兵雇佣费。</summary>
    public static int GetHireCost(UnitData unit, CampaignEconomyContext context, EconomyPriceAnchor? anchor = null)
    {
        int poiTier = Math.Clamp(context.NormalizedBattleSize + context.NormalizedDifficulty, 0, 3);
        int prosperity = GetShopProsperity(context);
        return RecruitPricingService.GetRecruitCost(unit, poiTier, prosperity, anchor);
    }

    /// <summary>战壕医官救治重伤单位费用。</summary>
    public static int GetMedicTreatmentCost(UnitData unit, CampaignEconomyContext context, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int level = Math.Max(1, unit.Level);
        double perQuest = Math.Max(1.0, anchor.DiscretionaryGoldPerQuest);
        double maxHpFactor = Math.Max(0, unit.BaseMaxHp - 10) * 1.5;

        // 重伤救治是强战役保险：约 2+ 个委托周期起，随等级/关卡压力上升。
        double cycles = 1.4 + level * 0.45 + context.NormalizedDifficulty * 0.30;
        return RoundCampaignGold(perQuest * cycles + maxHpFactor, minimum: 60);
    }

    /// <summary>战役通关金币奖励。</summary>
    public static int GetBattleGoldReward(CampaignEconomyContext context, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        double questReward = Math.Max(1, anchor.AverageQuestReward);
        double encounterScale = 0.65
            + context.NormalizedEnemyLevel * 0.10
            + context.NormalizedEnemyCount * 0.075
            + context.NormalizedDifficulty * 0.25
            + context.NormalizedBattleSize * 0.12;
        if (context.IsBoss) encounterScale += 1.35;

        return RoundCampaignGold(questReward * encounterScale, minimum: 60);
    }

    /// <summary>战役通关经验奖励。经验不是金币，但同样集中到此服务以避免战役结算硬编码散落。</summary>
    public static int GetBattleXpReward(CampaignEconomyContext context)
    {
        double baseXp = context.NormalizedEnemyLevel * 24 + context.NormalizedEnemyCount * 14;
        double multiplier = 1.0 + context.NormalizedDifficulty * 0.20 + context.NormalizedBattleSize * 0.08;
        if (context.IsBoss) multiplier += 0.35;
        return Math.Max(25, (int)Math.Round(baseXp * multiplier));
    }

    /// <summary>战役起始金币。约等于数个委托周期的可支配金币，足够应急但不足以无脑买满装备。</summary>
    public static int GetStartingGold(EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        return RoundCampaignGold(anchor.DiscretionaryGoldPerQuest * 8.0, minimum: 250);
    }

    private static double EstimateEquipmentBaseValue(UnitData unit, EconomyPriceAnchor anchor)
    {
        double total = 0;
        Add(unit.PrimaryMainHand);
        Add(unit.SecondaryMainHand);
        Add(unit.PrimaryOffHand);
        Add(unit.SecondaryOffHand);
        Add(unit.Armor);
        Add(unit.Shield);
        Add(unit.Helmet);
        Add(unit.Boots);
        Add(unit.Gauntlets);
        Add(unit.Accessory1);
        Add(unit.Accessory2);
        foreach (var weapon in unit.ExtraWeaponSlots) Add(weapon);

        return total;

        void Add(ItemData? item)
        {
            if (item == null) return;
            total += TradePricingService.GetBasePrice(item, anchor);
        }
    }

    private static int RoundCampaignGold(double value, int minimum)
    {
        int rounded;
        if (value < 100) rounded = (int)(Math.Round(value / 5.0) * 5);
        else if (value < 500) rounded = (int)(Math.Round(value / 10.0) * 10);
        else rounded = (int)(Math.Round(value / 25.0) * 25);
        return Math.Max(minimum, rounded);
    }
}
