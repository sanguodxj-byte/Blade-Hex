// FacilityPricingService.cs
// 设施服务定价：治疗、修理、休息、港口、竞技场等非物品交易金币流。
using System;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Strategic.Facilities;

namespace BladeHex.Strategic.Economy;

/// <summary>
/// 设施服务定价服务。
///
/// TradePricingService 负责物品交易；本服务负责把设施消费也锚定到同一套可支配金币模型。
/// </summary>
public static class FacilityPricingService
{
    public static readonly EconomyPriceAnchor DefaultAnchor = EquipmentPriceAnchorService.CreateDefaultAnchor();

    public static int GetRepairCost(ArmorData armor, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int max = Math.Max(armor.MaxArmorPoints, armor.DrThreshold * 10);
        if (max <= 0) return 0;
        int missing = Math.Max(0, max - armor.CurrentArmorPoints);
        if (missing <= 0) return 0;

        double basePrice = TradePricingService.GetBasePrice(armor, anchor);
        double missingRatio = missing / (double)Math.Max(1, max);
        return Round(Math.Max(anchor.SustainableNetGoldPerDay * 0.6, basePrice * missingRatio * 0.35), minimum: 5);
    }

    public static int GetSharpenCost(WeaponData? weapon, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        double basePrice = weapon == null ? anchor.DiscretionaryGoldPerQuest : TradePricingService.GetBasePrice(weapon, anchor);
        return Round(basePrice * 0.25 + anchor.DiscretionaryGoldPerQuest * 0.45, minimum: 30);
    }

    public static int GetReinforceCost(ArmorData? armor, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        double basePrice = armor == null ? anchor.DiscretionaryGoldPerQuest : TradePricingService.GetBasePrice(armor, anchor);
        return Round(basePrice * 0.30 + anchor.DiscretionaryGoldPerQuest * 0.65, minimum: 45);
    }

    public static int GetHealCost(PartyRoster? roster, float targetRatio, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        if (roster == null || roster.Count == 0) return Round(anchor.SustainableNetGoldPerDay, minimum: 10);

        int missingToTarget = 0;
        int affected = 0;
        foreach (var unit in roster.Members)
        {
            int max = CombatStats.GetMaxHp(unit);
            int current = PartyRoster.GetCurrentHp(unit);
            int target = targetRatio >= 1.0f ? max : Math.Max(current, (int)Math.Ceiling(max * targetRatio));
            target = Math.Clamp(target, 0, max);
            int missing = Math.Max(0, target - current);
            if (missing > 0)
            {
                missingToTarget += missing;
                affected++;
            }
        }

        double cost = anchor.SustainableNetGoldPerDay * (targetRatio >= 1.0f ? 1.4 : 0.6)
            + missingToTarget * (targetRatio >= 1.0f ? 1.8 : 1.2)
            + affected * 2.0;
        return Round(cost, minimum: targetRatio >= 1.0f ? 35 : 12);
    }

    public static int GetPurifyCost(PartyRoster? roster, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int negatives = HealingService.CountNegativeEffects(roster);
        return Round(anchor.DiscretionaryGoldPerQuest * 1.25 + negatives * anchor.SustainableNetGoldPerDay * 0.45, minimum: 35);
    }

    public static int GetHolyWaterCost(int prosperity = 50, EconomyPriceAnchor? anchor = null)
    {
        var item = PrototypeData.GetConsumables().TryGetValue("holy_water", out var holyWater)
            ? holyWater
            : HealingService.CreateFallbackHolyWater();
        return TradePricingService.GetBuyPrice(item, prosperity, anchor: anchor);
    }

    public static int GetLongRestCost(PartyRoster? roster, int prosperity = 50, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        int count = Math.Max(1, roster?.Count ?? 1);
        int needingRest = RestService.CountMembersNeedingRest(roster);
        double innFactor = 1.0 + (100 - Math.Clamp(prosperity, 0, 100)) * 0.004;
        return Round((anchor.SustainableNetGoldPerDay * 0.45 + count * 2.0 + needingRest * 3.0) * innFactor, minimum: 8);
    }

    public static int GetShipRentCost(int prosperity, ShipType shipType = ShipType.Raft, int days = 1, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        double typeMultiplier = shipType switch
        {
            ShipType.Raft => 1.2,
            ShipType.Sloop => 2.4,
            ShipType.Galleon => 7.5,
            _ => 2.0,
        };
        double prosperityMultiplier = 0.9 + Math.Clamp(prosperity, 0, 100) / 100.0 * 0.35;
        return Round(anchor.DiscretionaryGoldPerQuest * typeMultiplier * prosperityMultiplier * Math.Max(1, days), minimum: 35);
    }

    public static int GetArenaEntryFee(int difficulty, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        difficulty = Math.Clamp(difficulty, 1, 5);
        return Round(anchor.DiscretionaryGoldPerQuest * (0.35 + difficulty * 0.30), minimum: 15);
    }

    public static int GetArenaPrize(int difficulty, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        difficulty = Math.Clamp(difficulty, 1, 5);
        return Round(anchor.AverageQuestReward * (0.55 + difficulty * 0.45), minimum: 40);
    }

    private static int Round(double value, int minimum)
    {
        int rounded;
        if (value < 20) rounded = Math.Max(1, (int)Math.Round(value));
        else if (value < 100) rounded = (int)(Math.Round(value / 5.0) * 5);
        else if (value < 500) rounded = (int)(Math.Round(value / 10.0) * 10);
        else rounded = (int)(Math.Round(value / 25.0) * 25);
        return Math.Max(minimum, rounded);
    }
}
