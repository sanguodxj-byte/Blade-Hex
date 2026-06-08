// EconomyBalanceTests.cs
// 经济动态平衡与装备价格锚定测试。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Tests.Strategic;

public static class EconomyBalanceTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(EconomySimulation_HourlyFoodModelAvoidsStarvation), EconomySimulation_HourlyFoodModelAvoidsStarvation);
        yield return Run(nameof(EconomySimulation_DoubleFoodModelShowsEarlyStarvation), EconomySimulation_DoubleFoodModelShowsEarlyStarvation);
        yield return Run(nameof(EquipmentPriceAnchor_CreatesPositiveDiscretionaryAnchor), EquipmentPriceAnchor_CreatesPositiveDiscretionaryAnchor);
        yield return Run(nameof(EquipmentPriceAnchor_Tier3WeaponCostsMoreThanTier1Weapon), EquipmentPriceAnchor_Tier3WeaponCostsMoreThanTier1Weapon);
        yield return Run(nameof(EquipmentPriceAnchor_EvaluateAllCoversLoadedItems), EquipmentPriceAnchor_EvaluateAllCoversLoadedItems);
        yield return Run(nameof(EquipmentPriceAnchor_ClassifiesSiegeWeaponSeparately), EquipmentPriceAnchor_ClassifiesSiegeWeaponSeparately);
        yield return Run(nameof(TradePricingService_UsesAnchoredPrice), TradePricingService_UsesAnchoredPrice);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"异常: {ex.Message}");
        }
    }

    private static (bool, string) EconomySimulation_HourlyFoodModelAvoidsStarvation()
    {
        var result = EconomySimulation.Run(new EconomySimProfile
        {
            Name = "修正口粮测试",
            Days = 60,
            EnableFoodResupply = true,
            IncludeHourlyTravelFood = true,
            IncludeDailyFoodSettlement = false,
        });

        if (result.StarvedDays != 0) return (false, $"修正口粮模型不应饥饿，实际 {result.StarvedDays} 天");
        if (result.NetGoldPerDay <= 0) return (false, $"净金币/天应为正，实际 {result.NetGoldPerDay:F1}");
        return (true, "");
    }

    private static (bool, string) EconomySimulation_DoubleFoodModelShowsEarlyStarvation()
    {
        // 历史回归测试：验证已修复的双扣粮行为确实会导致早期饥饿
        var result = EconomySimulation.Run(new EconomySimProfile
        {
            Name = "双扣粮测试",
            Days = 30,
            EnableFoodResupply = false,
            IncludeHourlyTravelFood = true,
            IncludeDailyFoodSettlement = true,
        });

        if (result.FirstStarveDay == null) return (false, "双扣粮模型应暴露饥饿风险，但没有饥饿");
        if (result.FirstStarveDay > 10) return (false, $"预期 D10 前后饥饿，实际 D{result.FirstStarveDay}");
        return (true, "");
    }

    private static (bool, string) EquipmentPriceAnchor_CreatesPositiveDiscretionaryAnchor()
    {
        var anchor = EconomySimulation.CreatePriceAnchorFromModel();
        if (anchor.SustainableNetGoldPerDay <= 0) return (false, "日可支配金币应为正");
        if (anchor.DiscretionaryGoldPerQuest <= 0) return (false, "单委托周期可支配金币应为正");
        return (true, "");
    }

    private static (bool, string) EquipmentPriceAnchor_Tier3WeaponCostsMoreThanTier1Weapon()
    {
        var anchor = EconomySimulation.CreatePriceAnchorFromModel();
        var t1 = new WeaponData
        {
            ItemId = "test_t1",
            ItemName = "测试剑",
            Tier = 1,
            Weight = WeaponData.WeightCategory.Medium,
            Price = 1,
        };
        var t3 = new WeaponData
        {
            ItemId = "test_t3",
            ItemName = "测试神剑",
            Tier = 3,
            Weight = WeaponData.WeightCategory.Medium,
            Price = 1,
        };

        var b1 = EquipmentPriceAnchorService.GetPriceBand(t1, anchor);
        var b3 = EquipmentPriceAnchorService.GetPriceBand(t3, anchor);
        if (b3.Target <= b1.Target) return (false, $"T3 目标价应高于 T1：T1={b1.Target}, T3={b3.Target}");
        return (true, "");
    }

    private static (bool, string) EquipmentPriceAnchor_EvaluateAllCoversLoadedItems()
    {
        var anchor = EconomySimulation.CreatePriceAnchorFromModel();
        var evaluations = EquipmentPriceAnchorService.EvaluateAll(anchor);
        int expectedAtLeast = PrototypeData.GetWeapons().Count + PrototypeData.GetArmors().Count;

        if (evaluations.Count < expectedAtLeast)
            return (false, $"评估数量不足：{evaluations.Count} < {expectedAtLeast}");
        if (evaluations.Any(e => e.Band.Target <= 0 || e.Band.Max < e.Band.Min))
            return (false, "存在非法价格带");
        return (true, "");
    }

    private static (bool, string) EquipmentPriceAnchor_ClassifiesSiegeWeaponSeparately()
    {
        var anchor = EconomySimulation.CreatePriceAnchorFromModel();
        var weapons = PrototypeData.GetWeapons();
        if (!weapons.TryGetValue("ballista", out var ballista)) return (false, "缺少 ballista 测试数据");
        if (!weapons.TryGetValue("light_crossbow", out var lightCrossbow)) return (false, "缺少 light_crossbow 测试数据");

        var siege = EquipmentPriceAnchorService.Evaluate(ballista, anchor);
        var common = EquipmentPriceAnchorService.Evaluate(lightCrossbow, anchor);
        if (siege.Category != ItemEconomyCategory.SiegeWeapon) return (false, $"ballista 应归为攻城器械，实际 {siege.Category}");
        if (siege.SuggestedPrice <= common.SuggestedPrice) return (false, $"攻城器械建议价应高于普通弩：{siege.SuggestedPrice} <= {common.SuggestedPrice}");
        return (true, "");
    }

    private static (bool, string) TradePricingService_UsesAnchoredPrice()
    {
        var weapons = PrototypeData.GetWeapons();
        if (!weapons.TryGetValue("club_t3", out var clubT3)) return (false, "缺少 club_t3 测试数据");
        int buyPrice = TradePricingService.GetBuyPrice(clubT3, prosperity: 50);
        if (buyPrice <= clubT3.Price) return (false, $"市场买价应使用锚定价而不是 JSON 低价：buy={buyPrice}, json={clubT3.Price}");
        return (true, "");
    }
}
