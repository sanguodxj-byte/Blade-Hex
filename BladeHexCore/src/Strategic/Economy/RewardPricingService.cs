// RewardPricingService.cs
// 任务、遭遇、海上事件等金币/经验奖励生成服务。
using System;
using BladeHex.Data;

namespace BladeHex.Strategic.Economy;

public static class RewardPricingService
{
    public static readonly EconomyPriceAnchor DefaultAnchor = EquipmentPriceAnchorService.CreateDefaultAnchor();

    public static int GetQuestReward(QuestData.QuestType type, int difficulty, int targetCount = 1, float distanceFactor = 1.0f, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        difficulty = Math.Clamp(difficulty, 0, 3);
        targetCount = Math.Max(1, targetCount);
        double typeMultiplier = type switch
        {
            QuestData.QuestType.Extermination => 1.00,
            QuestData.QuestType.Escort => 1.15,
            QuestData.QuestType.Exploration => 0.95,
            QuestData.QuestType.Collection => 0.80,
            QuestData.QuestType.Bounty => 1.65,
            QuestData.QuestType.Defense => 1.25,
            QuestData.QuestType.Emergency => 1.45,
            _ => 1.00,
        };
        double countMultiplier = 1.0 + Math.Min(12, targetCount) * 0.045;
        double difficultyMultiplier = 0.75 + difficulty * 0.35;
        double distanceMultiplier = Math.Clamp(distanceFactor, 0.75f, 1.60f);
        return Round(anchor.AverageQuestReward * typeMultiplier * countMultiplier * difficultyMultiplier * distanceMultiplier, minimum: 30);
    }

    public static int GetEncounterGold(int level, int partySize, double typeMultiplier = 1.0, int carriedGold = 0, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        level = Math.Max(1, level);
        partySize = Math.Max(1, partySize);
        double reward = anchor.AverageQuestReward * (0.35 + level * 0.055 + partySize * 0.035) * typeMultiplier + carriedGold * 0.65;
        return Round(reward, minimum: 15);
    }

    public static int GetEncounterXp(int level, int partySize, double typeMultiplier = 1.0)
    {
        level = Math.Max(1, level);
        partySize = Math.Max(1, partySize);
        return Math.Max(10, (int)Math.Round((level * 25 + partySize * 12) * typeMultiplier));
    }

    public static int GetSeaFlotsamGold(int playerLevel, EconomyPriceAnchor? anchor = null)
    {
        anchor ??= DefaultAnchor;
        return Round(anchor.DiscretionaryGoldPerQuest * (0.8 + Math.Max(1, playerLevel) * 0.12), minimum: 25);
    }

    private static int Round(double value, int minimum)
    {
        int rounded;
        if (value < 100) rounded = (int)(Math.Round(value / 5.0) * 5);
        else if (value < 500) rounded = (int)(Math.Round(value / 10.0) * 10);
        else rounded = (int)(Math.Round(value / 25.0) * 25);
        return Math.Max(minimum, rounded);
    }
}
