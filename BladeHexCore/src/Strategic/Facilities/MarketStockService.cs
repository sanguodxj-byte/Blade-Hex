// MarketStockService.cs
// 市场库存生成规则。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic.Facilities;

/// <summary>
/// 市场库存生成服务。根据城镇繁荣度生成可售商品列表。
/// </summary>
public static class MarketStockService
{
    public static List<ItemData> GenerateStock(int prosperity)
    {
        prosperity = Math.Clamp(prosperity, 0, 100);

        var stock = new List<ItemData>();
        int weaponCount = 3 + prosperity / 20;
        int armorCount = 2 + prosperity / 25;
        int consumableCount = 3 + prosperity / 30;
        string difficulty = prosperity >= 70 ? "hard" : prosperity >= 40 ? "normal" : "easy";
        int itemLevel = 1 + prosperity / 10;

        AddSupplies(stock, prosperity);
        AddWeapons(stock, weaponCount, prosperity, difficulty, itemLevel);
        AddArmors(stock, armorCount, prosperity, difficulty, itemLevel);
        AddConsumables(stock, consumableCount);
        AddQuivers(stock, 1 + (prosperity >= 50 ? 1 : 0));

        stock.Sort((a, b) => TradePricingService.GetBuyPrice(a, prosperity).CompareTo(TradePricingService.GetBuyPrice(b, prosperity)));
        return stock;
    }

    private static void AddWeapons(List<ItemData> stock, int count, int prosperity, string difficulty, int itemLevel)
    {
        int maxWeaponPrice = 60 + prosperity * 14;
        var affordable = PrototypeData.GetWeapons().Values
            .Where(w => TradePricingService.GetStockGatePrice(w) <= maxWeaponPrice)
            .ToList();

        for (int i = 0; i < Math.Min(count, affordable.Count); i++)
        {
            var baseItem = affordable[CombatRandom.RandRange(0, affordable.Count - 1)];
            if (stock.Any(s => s.ItemId == baseItem.ItemId)) continue;
            stock.Add(GenerateShopEquipment(baseItem, difficulty, itemLevel));
        }
    }

    private static void AddArmors(List<ItemData> stock, int count, int prosperity, string difficulty, int itemLevel)
    {
        int maxArmorPrice = 50 + prosperity * 12;
        var affordable = PrototypeData.GetArmors().Values
            .Where(a => TradePricingService.GetStockGatePrice(a) <= maxArmorPrice)
            .ToList();

        for (int i = 0; i < Math.Min(count, affordable.Count); i++)
        {
            var baseItem = affordable[CombatRandom.RandRange(0, affordable.Count - 1)];
            if (stock.Any(s => s.ItemId == baseItem.ItemId)) continue;
            stock.Add(GenerateShopEquipment(baseItem, difficulty, itemLevel));
        }
    }

    private static ItemData GenerateShopEquipment(ItemData baseItem, string difficulty, int itemLevel)
    {
        var rarity = EquipmentGenerator.RollRarity(difficulty);
        if (rarity > ItemData.Rarity.Rare) rarity = ItemData.Rarity.Rare;
        return EquipmentGenerator.GenerateEquipment(baseItem, rarity, itemLevel, difficulty);
    }

    private static void AddSupplies(List<ItemData> stock, int prosperity)
    {
        AddSupplyStack(stock, "rations", 6 + prosperity / 10);
        AddSupplyStack(stock, "bandage", 3 + prosperity / 20);
        AddSupplyStack(stock, "camp_kit", 1 + prosperity / 35);
        if (prosperity >= 35) AddSupplyStack(stock, "antidote", 2 + prosperity / 35);
        if (prosperity >= 50) AddSupplyStack(stock, "whetstone", 1 + prosperity / 40);
    }

    private static void AddSupplyStack(List<ItemData> stock, string itemId, int quantity)
    {
        if (!PrototypeData.GetConsumables().TryGetValue(itemId, out var item)) return;

        for (int i = 0; i < Math.Max(1, quantity); i++)
            stock.Add(item);
    }

    private static void AddConsumables(List<ItemData> stock, int count)
    {
        var consumables = PrototypeData.GetConsumables().Values.ToList();
        for (int i = 0; i < Math.Min(count, consumables.Count); i++)
        {
            var item = consumables[CombatRandom.RandRange(0, consumables.Count - 1)];
            if (!stock.Any(s => s.ItemId == item.ItemId)) stock.Add(item);
        }
    }

    private static void AddQuivers(List<ItemData> stock, int count)
    {
        var quivers = PrototypeData.GetQuivers().Values.ToList();
        for (int i = 0; i < Math.Min(count, quivers.Count); i++)
        {
            var item = quivers[CombatRandom.RandRange(0, quivers.Count - 1)];
            if (!stock.Any(s => s.ItemId == item.ItemId)) stock.Add(item);
        }
    }
}
