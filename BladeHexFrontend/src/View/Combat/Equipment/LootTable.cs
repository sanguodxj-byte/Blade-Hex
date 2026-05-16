using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 战利品表系统 — 根据敌人CR和类型生成掉落物品
/// </summary>
public static class LootTable
{
    private static readonly Dictionary<string, float> BaseDropChance = new()
    {
        { "weapon", 0.15f },
        { "armor", 0.12f },
        { "shield", 0.08f },
        { "accessory", 0.10f },
        { "consumable", 0.30f },
        { "gold", 0.80f },
    };

    private static readonly Dictionary<float, float> CrMultiplier = new()
    {
        { 0.125f, 0.5f }, { 0.25f, 0.7f }, { 0.5f, 0.9f }, { 1.0f, 1.0f },
        { 2.0f, 1.2f }, { 3.0f, 1.4f }, { 5.0f, 1.6f }, { 8.0f, 1.8f },
        { 10.0f, 2.0f }, { 13.0f, 2.5f }, { 15.0f, 3.0f }, { 20.0f, 4.0f },
    };

    private static readonly Dictionary<UnitData.EnemyType, Dictionary<string, float>> EnemyTypeLootBias = new()
    {
        { UnitData.EnemyType.Humanoid, new Dictionary<string, float> { { "weapon", 1.5f }, { "armor", 1.3f }, { "gold", 1.2f } } },
        { UnitData.EnemyType.Beast, new Dictionary<string, float> { { "weapon", 0.0f }, { "armor", 0.0f }, { "gold", 0.2f } } },
        { UnitData.EnemyType.Dragon, new Dictionary<string, float> { { "weapon", 1.5f }, { "accessory", 2.0f }, { "gold", 3.0f } } },
    };

    public static List<ItemData> GenerateLoot(UnitData enemyData)
    {
        List<ItemData> loot = new();
        if (enemyData == null || !enemyData.IsEnemy) return loot;

        float cr = enemyData.ThreatLevel;
        var enemyType = enemyData.enemyType;
        float crMult = GetCrMultiplier(cr);
        var bias = EnemyTypeLootBias.GetValueOrDefault(enemyType, new Dictionary<string, float>());

        var rand = new Random();

        // 金币
        if (rand.NextDouble() < BaseDropChance["gold"] * crMult * bias.GetValueOrDefault("gold", 1.0f))
        {
            int gold = RollGoldAmount(cr);
            loot.Add(new ItemData { ItemName = "金币", Description = $"{gold} 金币", Price = gold });
        }

        // 武器
        if (rand.NextDouble() < BaseDropChance["weapon"] * crMult * bias.GetValueOrDefault("weapon", 1.0f))
        {
            var w = EquipmentGenerator.GenerateRandomWeapon(null, (ItemData.Rarity)(-1), EquipmentGenerator.GetItemLevelFromCr(cr), EquipmentGenerator.GetDifficultyFromCr(cr));
            if (w != null) loot.Add(w);
        }

        // 护甲
        if (rand.NextDouble() < BaseDropChance["armor"] * crMult * bias.GetValueOrDefault("armor", 1.0f))
        {
            var a = EquipmentGenerator.GenerateRandomArmor(null, (ItemData.Rarity)(-1), EquipmentGenerator.GetItemLevelFromCr(cr), EquipmentGenerator.GetDifficultyFromCr(cr));
            if (a != null) loot.Add(a);
        }

        // 盾牌
        if (rand.NextDouble() < BaseDropChance["shield"] * crMult * bias.GetValueOrDefault("shield", 1.0f))
        {
            var s = EquipmentGenerator.GenerateRandomArmor(new[] { "light_wooden_shield", "infantry_round_shield", "infantry_heavy_shield", "knight_shield", "legion_tower_shield" }, (ItemData.Rarity)(-1), EquipmentGenerator.GetItemLevelFromCr(cr), EquipmentGenerator.GetDifficultyFromCr(cr));
            if (s != null) loot.Add(s);
        }

        // 饰品
        if (rand.NextDouble() < BaseDropChance["accessory"] * crMult * bias.GetValueOrDefault("accessory", 1.0f))
        {
            var acc = new AccessoryData
            {
                ItemId = $"acc_loot_{rand.Next(1000, 9999)}",
                ItemName = "神秘饰品",
                ItemLevel = EquipmentGenerator.GetItemLevelFromCr(cr),
                ItemRarity = (ItemData.Rarity)(-1)
            };
            loot.Add(acc);
        }

        // 箭筒（15% 掉率）
        if (rand.NextDouble() < 0.15f * crMult)
        {
            var quivers = PrototypeData.GetQuivers();
            if (quivers.Count > 0)
            {
                var quiverList = new System.Collections.Generic.List<ItemData>(quivers.Values);
                loot.Add(quiverList[rand.Next(quiverList.Count)]);
            }
        }

        // 消耗品
        if (rand.NextDouble() < BaseDropChance["consumable"] * crMult * bias.GetValueOrDefault("consumable", 1.0f))
        {
            var con = new ConsumableData
            {
                ItemId = $"potion_loot_{rand.Next(1000, 9999)}",
                ItemName = "治疗药水",
                consumableType = ConsumableData.ConsumableType.HealingPotion,
                HealDiceCount = 2,
                HealDiceSides = 4,
                HealBonus = 2
            };
            loot.Add(con);
        }

        // 唯一掉落
        if (!string.IsNullOrEmpty(enemyData.UniqueDropId))
        {
            var unique = GetUniqueDrop(enemyData.UniqueDropId);
            if (unique != null) loot.Add(unique);
        }

        return loot;
    }

    private static float GetCrMultiplier(float cr)
    {
        float closest = 1.0f;
        float minDiff = float.MaxValue;
        foreach (var key in CrMultiplier.Keys)
        {
            float diff = Math.Abs(cr - key);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = key;
            }
        }
        return CrMultiplier[closest];
    }

    private static int RollGoldAmount(float cr)
    {
        var rand = new Random();
        if (cr <= 0.25f) return rand.Next(1, 11);
        if (cr <= 1.0f) return rand.Next(10, 51);
        if (cr <= 5.0f) return rand.Next(50, 201);
        return rand.Next(100, 501);
    }

    private static ItemData? GetUniqueDrop(string id)
    {
        return id switch
        {
            "dragon_scale_armor" => new ArmorData { ItemId = "dragon_scale_armor", ItemName = "龙鳞甲", IsUnique = true, ItemRarity = ItemData.Rarity.Legendary },
            "demon_blade" => new WeaponData { ItemId = "demon_blade", ItemName = "恶魔之刃", IsUnique = true, ItemRarity = ItemData.Rarity.Legendary },
            "phoenix_feather" => new AccessoryData { ItemId = "phoenix_feather", ItemName = "凤凰羽", IsUnique = true, ItemRarity = ItemData.Rarity.Epic },
            "ancient_tome" => new ConsumableData { ItemId = "ancient_tome", ItemName = "古老法术书", IsUnique = true, ItemRarity = ItemData.Rarity.Rare },
            _ => null
        };
    }
}
