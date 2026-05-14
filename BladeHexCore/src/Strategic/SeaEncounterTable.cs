// SeaEncounterTable.cs
// 海上遭遇表 — 定义海上随机事件类型和概率
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>海上遭遇类型</summary>
public enum SeaEncounterType
{
    PirateAttack,   // 海盗袭击 — 触发战斗
    Storm,          // 风暴 — 船只耐久损失 + 随机偏移
    SeaMonster,     // 海怪 — 强力战斗遭遇
    Flotsam,        // 漂流物 — 随机获得物资/装备
    MerchantShip,   // 商船 — 可交易或劫掠
}

/// <summary>海上遭遇结果</summary>
public class SeaEncounterResult
{
    public SeaEncounterType Type { get; set; }
    public string Description { get; set; } = "";
    public int DurabilityDamage { get; set; } = 0;
    public int GoldReward { get; set; } = 0;
    public bool TriggersCombat { get; set; } = false;
    public string CombatTemplate { get; set; } = "";
    public List<string> ItemRewards { get; set; } = new();
}

/// <summary>
/// 海上遭遇表 — 按概率随机生成海上事件
/// </summary>
public static class SeaEncounterTable
{
    /// <summary>每移动 500px 海上距离的遭遇检定基础概率</summary>
    public const float BaseEncounterChance = 0.08f;

    /// <summary>遭遇检定距离间隔（像素）</summary>
    public const float EncounterCheckInterval = 500.0f;

    private static readonly (SeaEncounterType type, float weight)[] _encounterWeights =
    [
        (SeaEncounterType.PirateAttack, 0.30f),
        (SeaEncounterType.Storm, 0.20f),
        (SeaEncounterType.SeaMonster, 0.10f),
        (SeaEncounterType.Flotsam, 0.25f),
        (SeaEncounterType.MerchantShip, 0.15f),
    ];

    /// <summary>根据概率随机选择一个遭遇类型</summary>
    public static SeaEncounterType RollEncounterType(Random rng)
    {
        float roll = (float)rng.NextDouble();
        float cumulative = 0f;

        foreach (var (type, weight) in _encounterWeights)
        {
            cumulative += weight;
            if (roll <= cumulative) return type;
        }

        return SeaEncounterType.Flotsam; // fallback
    }

    /// <summary>生成遭遇结果</summary>
    public static SeaEncounterResult GenerateEncounter(Random rng, int playerLevel)
    {
        var type = RollEncounterType(rng);

        return type switch
        {
            SeaEncounterType.PirateAttack => new SeaEncounterResult
            {
                Type = type,
                Description = "海盗船逼近！准备战斗！",
                TriggersCombat = true,
                CombatTemplate = "sea_battle_pirate",
            },
            SeaEncounterType.Storm => new SeaEncounterResult
            {
                Type = type,
                Description = "暴风雨来袭！船只受损。",
                DurabilityDamage = 15 + rng.Next(20),
            },
            SeaEncounterType.SeaMonster => new SeaEncounterResult
            {
                Type = type,
                Description = "海面下有巨大的阴影在移动...",
                TriggersCombat = true,
                CombatTemplate = "sea_battle_monster",
            },
            SeaEncounterType.Flotsam => new SeaEncounterResult
            {
                Type = type,
                Description = "发现漂浮的残骸，搜索后找到了一些物资。",
                GoldReward = 50 + rng.Next(100 + playerLevel * 10),
                ItemRewards = GenerateFlotsamLoot(rng),
            },
            SeaEncounterType.MerchantShip => new SeaEncounterResult
            {
                Type = type,
                Description = "一艘商船出现在视野中。",
                // 交易或劫掠由玩家选择，此处只标记类型
            },
            _ => new SeaEncounterResult { Type = type, Description = "平静的海面。" },
        };
    }

    private static List<string> GenerateFlotsamLoot(Random rng)
    {
        var loot = new List<string>();
        int count = 1 + rng.Next(3);
        string[] possibleItems = ["rope", "plank", "salt_fish", "rum", "compass", "pearl", "amber"];
        for (int i = 0; i < count; i++)
            loot.Add(possibleItems[rng.Next(possibleItems.Length)]);
        return loot;
    }

    /// <summary>快速旅行途中的遭遇概率（基于距离）</summary>
    public static float GetFastTravelEncounterChance(float distancePixels)
    {
        return Math.Min(0.5f, distancePixels / 10000.0f);
    }
}
