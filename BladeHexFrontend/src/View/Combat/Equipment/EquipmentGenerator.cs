using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 装备生成器 — 根据稀有度、物品等级和词缀动态生成装备
/// </summary>
public static class EquipmentGenerator
{
    private static readonly Dictionary<ItemData.Rarity, float> DefaultRarityWeights = new()
    {
        { ItemData.Rarity.Common, 60.0f },
        { ItemData.Rarity.Uncommon, 25.0f },
        { ItemData.Rarity.Rare, 10.0f },
        { ItemData.Rarity.Epic, 4.0f },
        { ItemData.Rarity.Legendary, 1.0f },
    };

    private static readonly Dictionary<string, Dictionary<ItemData.Rarity, float>> RarityWeightsByDifficulty = new()
    {
        { "easy", new Dictionary<ItemData.Rarity, float> { { ItemData.Rarity.Common, 70.0f }, { ItemData.Rarity.Uncommon, 20.0f }, { ItemData.Rarity.Rare, 8.0f }, { ItemData.Rarity.Epic, 2.0f }, { ItemData.Rarity.Legendary, 0.0f } } },
        { "normal", new Dictionary<ItemData.Rarity, float> { { ItemData.Rarity.Common, 55.0f }, { ItemData.Rarity.Uncommon, 28.0f }, { ItemData.Rarity.Rare, 12.0f }, { ItemData.Rarity.Epic, 4.0f }, { ItemData.Rarity.Legendary, 1.0f } } },
        { "hard", new Dictionary<ItemData.Rarity, float> { { ItemData.Rarity.Common, 40.0f }, { ItemData.Rarity.Uncommon, 30.0f }, { ItemData.Rarity.Rare, 18.0f }, { ItemData.Rarity.Epic, 9.0f }, { ItemData.Rarity.Legendary, 3.0f } } },
        { "nightmare", new Dictionary<ItemData.Rarity, float> { { ItemData.Rarity.Common, 25.0f }, { ItemData.Rarity.Uncommon, 30.0f }, { ItemData.Rarity.Rare, 25.0f }, { ItemData.Rarity.Epic, 14.0f }, { ItemData.Rarity.Legendary, 6.0f } } },
    };

    public static ItemData GenerateEquipment(ItemData baseItem, ItemData.Rarity targetRarity = (ItemData.Rarity)(-1), int itemLevel = 1, string difficulty = "normal")
    {
        var item = DeepCopyItem(baseItem);
        item.ItemLevel = itemLevel;

        if ((int)targetRarity == -1)
        {
            targetRarity = RollRarity(difficulty);
        }
        item.ItemRarity = targetRarity;

        ApplyRandomAffixes(item, itemLevel);

        return item;
    }

    public static WeaponData GenerateRandomWeapon(string[]? weaponPool = null, ItemData.Rarity targetRarity = (ItemData.Rarity)(-1), int itemLevel = 1, string difficulty = "normal")
    {
        var allWeapons = PrototypeData.GetWeapons();
        List<WeaponData> candidates = new();

        if (weaponPool == null || weaponPool.Length == 0)
        {
            candidates.AddRange(allWeapons.Values);
        }
        else
        {
            foreach (var key in weaponPool)
            {
                if (allWeapons.TryGetValue(key, out var w)) candidates.Add(w);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.Add(allWeapons.ContainsKey("arming_sword") ? allWeapons["arming_sword"] : new WeaponData { ItemName = "练习剑" });
        }

        var rand = new Random();
        var baseItem = candidates[rand.Next(candidates.Count)];
        return (GenerateEquipment(baseItem, targetRarity, itemLevel, difficulty) as WeaponData)!;
    }

    public static ArmorData GenerateRandomArmor(string[]? armorPool = null, ItemData.Rarity targetRarity = (ItemData.Rarity)(-1), int itemLevel = 1, string difficulty = "normal")
    {
        var allArmors = PrototypeData.GetArmors();
        List<ArmorData> candidates = new();

        if (armorPool == null || armorPool.Length == 0)
        {
            candidates.AddRange(allArmors.Values.Where(a => a.armorType != ArmorData.ArmorType.Shield));
        }
        else
        {
            foreach (var key in armorPool)
            {
                if (allArmors.TryGetValue(key, out var a)) candidates.Add(a);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.Add(allArmors.ContainsKey("leather") ? allArmors["leather"] : new ArmorData { ItemName = "布衣" });
        }

        var rand = new Random();
        var baseItem = candidates[rand.Next(candidates.Count)];
        return (GenerateEquipment(baseItem, targetRarity, itemLevel, difficulty) as ArmorData)!;
    }

    public static ItemData.Rarity RollRarity(string difficulty = "normal")
    {
        var weights = RarityWeightsByDifficulty.GetValueOrDefault(difficulty, DefaultRarityWeights);
        float total = weights.Values.Sum();

        var rand = new Random();
        float roll = (float)rand.NextDouble() * total;
        float cumulative = 0;

        foreach (var pair in weights)
        {
            cumulative += pair.Value;
            if (roll <= cumulative) return pair.Key;
        }

        return ItemData.Rarity.Common;
    }

    public static int GetItemLevelFromCr(float cr)
    {
        var rand = new Random();
        if (cr <= 0.25f) return rand.Next(1, 3);
        if (cr <= 0.5f) return rand.Next(1, 4);
        if (cr <= 1.0f) return rand.Next(2, 5);
        if (cr <= 2.0f) return rand.Next(3, 7);
        if (cr <= 5.0f) return rand.Next(5, 11);
        if (cr <= 10.0f) return rand.Next(8, 16);
        return rand.Next(12, 21);
    }

    public static string GetDifficultyFromCr(float cr)
    {
        if (cr <= 0.5f) return "easy";
        if (cr <= 2.0f) return "normal";
        if (cr <= 5.0f) return "hard";
        return "nightmare";
    }

    private static void ApplyRandomAffixes(ItemData item, int itemLevel)
    {
        int maxAffixes = item.GetMaxAffixCount();
        if (maxAffixes == 0) return;

        EquipmentAffix.AffixTarget target = EquipmentAffix.AffixTarget.Any;
        if (item is WeaponData) target = EquipmentAffix.AffixTarget.Weapon;
        else if (item is ArmorData armor)
        {
            if (armor.armorType == ArmorData.ArmorType.Shield) target = EquipmentAffix.AffixTarget.Shield;
            else target = EquipmentAffix.AffixTarget.Armor;
        }
        else if (item is AccessoryData) target = EquipmentAffix.AffixTarget.Accessory;

        var available = EquipmentAffix.GetAffixesForTarget(target, itemLevel, (int)item.ItemRarity);
        if (available.Length == 0) return;

        var prefixes = available.Where(a => a.IsPrefix).ToList();
        var suffixes = available.Where(a => !a.IsPrefix).ToList();

        var rand = new Random();
        if (prefixes.Count > 0 && maxAffixes > 0)
        {
            var chosen = WeightedRandomAffix(prefixes);
            if (chosen != null)
            {
                item.AddAffix(chosen);
                maxAffixes--;
            }
        }

        while (maxAffixes > 0 && suffixes.Count > 0)
        {
            var chosen = WeightedRandomAffix(suffixes);
            if (chosen != null)
            {
                item.AddAffix(chosen);
                suffixes.Remove(chosen);
                maxAffixes--;
            }
            else break;
        }
    }

    private static EquipmentAffix? WeightedRandomAffix(List<EquipmentAffix> pool)
    {
        if (pool.Count == 0) return null;
        float total = pool.Sum(a => a.Weight);
        var rand = new Random();
        float roll = (float)rand.NextDouble() * total;
        float cumulative = 0;
        foreach (var a in pool)
        {
            cumulative += a.Weight;
            if (roll <= cumulative) return a;
        }
        return pool.Last();
    }

    private static ItemData DeepCopyItem(ItemData src)
    {
        // 简化处理：使用 Duplicate() 或手动拷贝。
        // 由于 ItemData 是 Resource，Duplicate() 是 Godot 提供的。
        return (ItemData)src.Duplicate();
    }
}
