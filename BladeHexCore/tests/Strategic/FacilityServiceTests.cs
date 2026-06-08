// FacilityServiceTests.cs
// Strategic/Facilities 核心服务回归测试。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Combat;
using BladeHex.Combat.Buff;
using BladeHex.Data;
using BladeHex.Strategic.Facilities;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic.Tests;

public static class FacilityServiceTests
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
        yield return Run(nameof(SmithyRepairAll_RestoresDamagedArmorAndChargesGold), SmithyRepairAll_RestoresDamagedArmorAndChargesGold);
        yield return Run(nameof(SmithySharpenAndReinforce_ModifyLeaderEquipment), SmithySharpenAndReinforce_ModifyLeaderEquipment);
        yield return Run(nameof(HealingHealToRatio_OnlyRaisesBelowTarget), HealingHealToRatio_OnlyRaisesBelowTarget);
        yield return Run(nameof(HealingPurifyAll_RemovesOnlyNegativeEffects), HealingPurifyAll_RemovesOnlyNegativeEffects);
        yield return Run(nameof(RestTimeBasedRecovery_RateMultiplier), RestTimeBasedRecovery_RateMultiplier);
        yield return Run(nameof(MarketStockService_ClampsProsperityAndSortsStock), MarketStockService_ClampsProsperityAndSortsStock);
        yield return Run(nameof(PortService_RentShipAssignsShipAndAtSea), PortService_RentShipAssignsShipAndAtSea);
        yield return Run(nameof(PortService_RentShipFailsWithoutGold), PortService_RentShipFailsWithoutGold);
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
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    private static (bool, string) SmithyRepairAll_RestoresDamagedArmorAndChargesGold()
    {
        var roster = MakeRoster();
        var armor = MakeArmor("armor", dr: 10, current: 40);
        var shield = MakeArmor("shield", dr: 6, current: 30, ArmorData.ArmorType.Shield);
        roster.Leader!.Armor = armor;
        roster.Leader.Shield = shield;

        // 修理费用依赖 FacilityPricingService 动态定价（经济锚定模型），给足金币
        int gold = 200;
        var result = SmithyService.RepairAll(roster, cost => Spend(ref gold, cost));

        if (!result.Success) return (false, result.Message);
        int expectedGold = 200 - result.GoldSpent;
        if (gold != expectedGold) return (false, $"expected gold {expectedGold}, got {gold}");
        if (armor.CurrentArmorPoints != 100 || shield.CurrentArmorPoints != 60)
            return (false, $"armor not fully repaired: armor={armor.CurrentArmorPoints}, shield={shield.CurrentArmorPoints}");
        if (result.AffectedItems != 2) return (false, $"expected 2 affected items, got {result.AffectedItems}");
        if (result.AmountChanged != 90) return (false, $"expected 90 restored points, got {result.AmountChanged}");
        return (true, "");
    }

    private static (bool, string) SmithySharpenAndReinforce_ModifyLeaderEquipment()
    {
        var roster = MakeRoster();
        var weapon = new WeaponData { ItemId = "sword", ItemName = "剑", BonusDamage = 0 };
        var armor = MakeArmor("armor", dr: 8, current: 80);
        roster.Leader!.PrimaryMainHand = weapon;
        roster.Leader.Armor = armor;

        int gold = 200;
        var sharpen = SmithyService.SharpenLeaderWeapon(roster, cost => Spend(ref gold, cost));
        var reinforce = SmithyService.ReinforceLeaderArmor(roster, cost => Spend(ref gold, cost));

        if (!sharpen.Success || !reinforce.Success) return (false, $"sharpen={sharpen.Message}, reinforce={reinforce.Message}");
        int expectedGold = 200 - sharpen.GoldSpent - reinforce.GoldSpent;
        if (gold != expectedGold) return (false, $"expected gold {expectedGold}, got {gold}");
        if (weapon.BonusDamage != 1) return (false, $"expected weapon bonus damage 1, got {weapon.BonusDamage}");
        if (armor.DrThreshold != 9) return (false, $"expected armor DR 9, got {armor.DrThreshold}");
        if (armor.CurrentArmorPoints != armor.MaxArmorPoints || armor.MaxArmorPoints != 90)
            return (false, $"armor AP mismatch current={armor.CurrentArmorPoints}, max={armor.MaxArmorPoints}");
        return (true, "");
    }

    private static (bool, string) HealingHealToRatio_OnlyRaisesBelowTarget()
    {
        var roster = MakeRoster();
        var leader = roster.Leader!;
        leader.BaseMaxHp = 20;
        leader.Con = 10;
        leader.Level = 1;
        PartyRoster.SetCurrentHp(leader, 3);

        int gold = 20;
        int cost = FacilityPricingService.GetHealCost(roster, 0.5f);
        var result = HealingService.HealToRatio(roster, 0.5f, cost, cost => Spend(ref gold, cost));

        int maxHp = BladeHex.Combat.CombatStats.GetMaxHp(leader);
        int expected = (int)Math.Ceiling(maxHp * 0.5f);
        if (!result.Success) return (false, result.Message);
        if (gold != 20 - result.GoldSpent) return (false, $"expected gold {20 - result.GoldSpent}, got {gold}");
        if (PartyRoster.GetCurrentHp(leader) != expected)
            return (false, $"expected hp {expected}, got {PartyRoster.GetCurrentHp(leader)}");
        return (true, "");
    }

    private static (bool, string) HealingPurifyAll_RemovesOnlyNegativeEffects()
    {
        var roster = MakeRoster();
        var leader = roster.Leader!;
        leader.Runtime.ActiveStatusEffects.Add(new StatusEffectInstance { Id = "poison", IsNegative = true });
        leader.Runtime.ActiveStatusEffects.Add(new StatusEffectInstance { Id = "bless", IsNegative = false });
        leader.Runtime.ActiveBuffs.Add(new BuffInstance { Id = "burn", IsNegative = true });
        leader.Runtime.ActiveBuffs.Add(new BuffInstance { Id = "shield", IsNegative = false });

        int gold = 100;
        var result = HealingService.PurifyAll(roster, cost => Spend(ref gold, cost));

        if (!result.Success) return (false, result.Message);
        if (gold != 100 - result.GoldSpent) return (false, $"expected gold {100 - result.GoldSpent}, got {gold}");
        if (result.AmountChanged != 2) return (false, $"expected 2 removed effects, got {result.AmountChanged}");
        if (leader.Runtime.ActiveStatusEffects.Count != 1 || leader.Runtime.ActiveStatusEffects[0].Id != "bless")
            return (false, "positive status effect was removed or negative remained");
        if (leader.Runtime.ActiveBuffs.Count != 1 || leader.Runtime.ActiveBuffs[0].Id != "shield")
            return (false, "positive buff was removed or negative remained");
        return (true, "");
    }

    private static (bool, string) RestTimeBasedRecovery_RateMultiplier()
    {
        var roster = MakeRoster();
        var leader = roster.Leader!;
        leader.BaseMaxHp = 20;
        leader.Con = 10;
        leader.Level = 1;
        leader.Intel = 10;
        leader.Wis = 8;
        leader.CurrentMana = 0;
        PartyRoster.SetCurrentHp(leader, 1);

        // 8h at 1x
        var (hp1x, mana1x) = RestService.TimeBasedRecovery(roster, 8.0f, canRestore: true, rateMultiplier: 1.0f);
        if (hp1x <= 0 || mana1x <= 0)
            return (false, $"expected positive 8h 1x recovery, got hp={hp1x}, mana={mana1x}");

        // 8h at 2x (camp): should recover more than 1x
        PartyRoster.SetCurrentHp(leader, 1);
        leader.CurrentMana = 0;
        var (hp2x, mana2x) = RestService.TimeBasedRecovery(roster, 8.0f, canRestore: true, rateMultiplier: 2.0f);
        if (hp2x <= hp1x || mana2x <= mana1x)
            return (false, $"expected 2x > 1x recovery, got 1x=({hp1x},{mana1x}) vs 2x=({hp2x},{mana2x})");

        // 8h at 4x (town): should recover more than 2x
        PartyRoster.SetCurrentHp(leader, 1);
        leader.CurrentMana = 0;
        var (hp4x, mana4x) = RestService.TimeBasedRecovery(roster, 8.0f, canRestore: true, rateMultiplier: 4.0f);
        if (hp4x <= hp2x || mana4x <= mana2x)
            return (false, $"expected 4x > 2x recovery, got 2x=({hp2x},{mana2x}) vs 4x=({hp4x},{mana4x})");

        // canRestore = false with 4x: still no recovery
        PartyRoster.SetCurrentHp(leader, 1);
        leader.CurrentMana = 0;
        (hp4x, mana4x) = RestService.TimeBasedRecovery(roster, 8.0f, canRestore: false, rateMultiplier: 4.0f);
        if (hp4x != 0 || mana4x != 0)
            return (false, $"expected zero recovery when blocked with 4x, got hp={hp4x}, mana={mana4x}");
        if (PartyRoster.GetCurrentHp(leader) != 1)
            return (false, "HP changed when recovery was blocked");

        return (true, "");
    }

    private static (bool, string) MarketStockService_ClampsProsperityAndSortsStock()
    {
        var low = MarketStockService.GenerateStock(-999);
        var high = MarketStockService.GenerateStock(999);

        if (low.Count == 0) return (false, "low prosperity stock is empty");
        if (high.Count < low.Count) return (false, $"expected high prosperity stock count >= low, got {high.Count} < {low.Count}");
        if (!IsSortedByPrice(low) || !IsSortedByPrice(high)) return (false, "stock is not sorted by price");
        if (high.Any(i => i.ItemRarity > ItemData.Rarity.Rare)) return (false, "shop generated item above Rare rarity");
        return (true, "");
    }

    private static (bool, string) PortService_RentShipAssignsShipAndAtSea()
    {
        int expectedCost = PortService.CalculateRentCost(70);
        int gold = expectedCost + 50;
        ShipData? assigned = null;
        bool atSea = false;

        var result = PortService.RentShip(70, cost => Spend(ref gold, cost), ship => assigned = ship, value => atSea = value);

        if (!result.Success) return (false, result.Message);
        if (gold != 50) return (false, $"expected gold 50, got {gold}");
        if (assigned == null) return (false, "ship was not assigned");
        if (assigned.Type != ShipType.Sloop) return (false, $"expected Sloop, got {assigned.Type}");
        if (!assigned.IsRented || assigned.RentDaysRemaining != 1) return (false, "rental metadata missing");
        if (!atSea) return (false, "party was not set at sea");
        return (true, "");
    }

    private static (bool, string) PortService_RentShipFailsWithoutGold()
    {
        int expectedCost = PortService.CalculateRentCost(70);
        int gold = Math.Max(0, expectedCost - 1);
        ShipData? assigned = null;
        bool atSea = false;

        var result = PortService.RentShip(70, cost => Spend(ref gold, cost), ship => assigned = ship, value => atSea = value);

        if (result.Success) return (false, "rent should fail when gold is insufficient");
        if (gold != Math.Max(0, expectedCost - 1)) return (false, $"gold changed on failure: {gold}");
        if (assigned != null) return (false, "ship assigned on failure");
        if (atSea) return (false, "atSea set on failure");
        return (true, "");
    }

    private static PartyRoster MakeRoster()
    {
        var roster = new PartyRoster();
        roster.SetLeader(new UnitData
        {
            UnitName = "Leader",
            BaseMaxHp = 10,
            Level = 1,
            Con = 10,
            Intel = 10,
            Wis = 10,
        });
        return roster;
    }

    private static ArmorData MakeArmor(string id, int dr, int current, ArmorData.ArmorType type = ArmorData.ArmorType.Medium)
    {
        return new ArmorData
        {
            ItemId = id,
            ItemName = id,
            armorType = type,
            DrThreshold = dr,
            MaxArmorPoints = dr * 10,
            CurrentArmorPoints = current,
        };
    }

    private static bool Spend(ref int gold, int cost)
    {
        if (gold < cost) return false;
        gold -= cost;
        return true;
    }

    private static bool IsSortedByPrice(IReadOnlyList<ItemData> items)
    {
        for (int i = 1; i < items.Count; i++)
        {
            if (TradePricingService.GetBuyPrice(items[i - 1]) > TradePricingService.GetBuyPrice(items[i])) return false;
        }
        return true;
    }
}
