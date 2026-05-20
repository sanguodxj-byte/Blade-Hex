// EconomySimulation.cs
// Core 经济模拟组件：用于无 UI、可重复的经济动态平衡测试。
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic.Economy;

/// <summary>
/// 战役经济模拟配置。
/// 默认配置用于雇佣兵阶段：小队规模、委托收入、工资压力、口粮压力、设施消费与可选扩编。
/// </summary>
public sealed class EconomySimProfile
{
    public string Name = "";
    public int Days = 30;
    public int StartingGold = 1000;
    public float StartingFood = 20f;
    public int InitialPartySize = 4;
    public int InitialMemberLevel = 2;
    public int Capacity = 6;
    public float TravelHoursPerDay = 8f;

    /// <summary>是否按大地图行军小时数连续扣粮。</summary>
    public bool IncludeHourlyTravelFood = true;

    /// <summary>
    /// 是否在跨日结算时再扣一整天口粮。
    /// 保留这个开关是为了显式模拟并暴露当前“双扣粮”行为。
    /// </summary>
    public bool IncludeDailyFoodSettlement = true;

    public int QuestEveryDays = 3;
    public int AverageQuestReward = 90;
    public int FacilityEveryDays = 4;
    public int FacilityGoldSpend = 15;
    public int RecruitEveryDays = 0;
    public int RecruitCost = 70;
    public int RecruitLevel = 2;

    public bool EnableFoodResupply = false;
    public int ResupplyEveryDays = 5;
    public float FoodResupplyThreshold = 8f;
    public float FoodResupplyTarget = 20f;
    public int FoodUnitPrice = 4;
}

/// <summary>单次经济模拟的汇总结果。</summary>
public sealed class EconomySimResult
{
    public string Name = "";
    public int Days;
    public int StartingGold;
    public int FinalGold;
    public float FinalFood;
    public int FinalPartySize;
    public int TotalQuestIncome;
    public int TotalWages;
    public int TotalFacilities;
    public int TotalRecruiting;
    public int TotalFoodPurchases;
    public float TotalFoodConsumed;
    public int QuestsCompleted;
    public int RecruitsHired;
    public int Desertions;
    public int StarvedDays;
    public int UnpaidDays;
    public int? FirstStarveDay;
    public int? FirstBrokeDay;
    public int MinGold;
    public float MinFood;

    public double NetGoldPerDay => Days <= 0 ? 0 : (double)(FinalGold - StartingGold) / Days;
}

/// <summary>
/// 战役经济模拟器。
///
/// 该组件刻意保持纯 Core：不依赖 Node、UI 或场景树，便于单元测试与 headless 批量平衡。
/// </summary>
public static class EconomySimulation
{
    public static IReadOnlyList<EconomySimProfile> CreateDefaultProfiles(int days, int averageQuestReward = 90, int foodUnitPrice = 4)
    {
        days = Math.Max(7, days);
        averageQuestReward = Math.Max(1, averageQuestReward);
        foodUnitPrice = Math.Max(1, foodUnitPrice);

        return new List<EconomySimProfile>
        {
            new()
            {
                Name = "当前实现/无食物补给",
                Days = days,
                AverageQuestReward = averageQuestReward,
                FoodUnitPrice = foodUnitPrice,
                EnableFoodResupply = false,
                IncludeHourlyTravelFood = true,
                IncludeDailyFoodSettlement = true,
            },
            new()
            {
                Name = "计划目标/可购买口粮",
                Days = days,
                AverageQuestReward = averageQuestReward,
                FoodUnitPrice = foodUnitPrice,
                EnableFoodResupply = true,
                IncludeHourlyTravelFood = true,
                IncludeDailyFoodSettlement = true,
            },
            new()
            {
                Name = "修正口粮/只按小时扣粮",
                Days = days,
                AverageQuestReward = averageQuestReward,
                FoodUnitPrice = foodUnitPrice,
                EnableFoodResupply = true,
                IncludeHourlyTravelFood = true,
                IncludeDailyFoodSettlement = false,
            },
            new()
            {
                Name = "成长压力/扩编到6人",
                Days = days,
                AverageQuestReward = averageQuestReward + 10,
                FoodUnitPrice = foodUnitPrice,
                EnableFoodResupply = true,
                IncludeHourlyTravelFood = true,
                IncludeDailyFoodSettlement = true,
                RecruitEveryDays = 10,
                RecruitCost = 70,
                FacilityEveryDays = 3,
                FacilityGoldSpend = 20,
            },
        };
    }

    public static IReadOnlyList<EconomySimResult> RunDefaultProfiles(int days, int averageQuestReward = 90, int foodUnitPrice = 4)
    {
        return CreateDefaultProfiles(days, averageQuestReward, foodUnitPrice)
            .Select(Run)
            .ToList();
    }

    public static EconomySimResult Run(EconomySimProfile profile)
    {
        int gold = profile.StartingGold;
        float food = profile.StartingFood;
        var roster = BuildRoster(profile.InitialPartySize, profile.InitialMemberLevel, profile.Capacity);
        var wage = new WageSystem();
        var foodSystem = new FoodSystem();

        var result = new EconomySimResult
        {
            Name = profile.Name,
            Days = profile.Days,
            StartingGold = profile.StartingGold,
            MinGold = gold,
            MinFood = food,
        };

        for (int day = 1; day <= profile.Days; day++)
        {
            ProcessQuestIncome(profile, result, day, ref gold);
            ProcessFoodResupply(profile, result, day, ref gold, ref food);
            ProcessRecruiting(profile, result, roster, day, ref gold);
            ProcessFacilitySpend(profile, result, day, ref gold);
            ProcessWages(result, roster, wage, day, ref gold);
            ProcessFood(profile, result, roster, foodSystem, day, ref food);

            result.MinGold = Math.Min(result.MinGold, gold);
            result.MinFood = Math.Min(result.MinFood, food);
        }

        result.FinalGold = gold;
        result.FinalFood = food;
        result.FinalPartySize = roster.Count;
        return result;
    }

    public static List<string> FormatReport(IReadOnlyList<EconomySimResult> results, int averageQuestReward)
    {
        var lines = new List<string>
        {
            "=== 经济平衡模拟（雇佣兵阶段） ===",
            $"平均委托奖励假设：{averageQuestReward} 金/单",
            "Profile                 | days | gold | food | party | income | wage | food$ | fac$ | rec$ | quests | starve | broke | net/day",
        };

        foreach (var r in results)
        {
            string starve = r.FirstStarveDay.HasValue ? $"D{r.FirstStarveDay.Value}" : "-";
            string broke = r.FirstBrokeDay.HasValue ? $"D{r.FirstBrokeDay.Value}" : "-";
            lines.Add($"{Trim(r.Name, 22),-22} | {r.Days,4} | {r.FinalGold,4} | {r.FinalFood,4:F1} | {r.FinalPartySize,5} | {r.TotalQuestIncome,6} | {r.TotalWages,4} | {r.TotalFoodPurchases,5} | {r.TotalFacilities,4} | {r.TotalRecruiting,4} | {r.QuestsCompleted,6} | {starve,6} | {broke,5} | {r.NetGoldPerDay,7:F1}");
        }

        lines.Add("");
        lines.Add("读表说明：food$=为补口粮花费的金币；fac$=治疗/修理/休息等设施预算；rec$=招募花费。当前实现/无食物补给用于暴露资源闭环缺口。");
        lines.Add("关键平衡指标：早期 4 人队若每 3 天完成 1 个 80~100 金委托，应能覆盖工资+口粮+基础治疗，但不足以频繁买装备和扩编。");
        lines.Add("警告阈值：若 starve <= D10，说明口粮闭环或行军扣粮过重；若 net/day > 40，早期金币膨胀风险高；若 net/day < -20，主线委托压力过强。");
        return lines;
    }

    public static EconomyPriceAnchor CreatePriceAnchorFromModel(int averageQuestReward = 90, int foodUnitPrice = 4)
    {
        var corrected = new EconomySimProfile
        {
            Name = "装备定价锚点/修正口粮",
            Days = 60,
            AverageQuestReward = averageQuestReward,
            FoodUnitPrice = foodUnitPrice,
            EnableFoodResupply = true,
            IncludeHourlyTravelFood = true,
            IncludeDailyFoodSettlement = false,
        };
        var result = Run(corrected);
        return new EconomyPriceAnchor
        {
            AverageQuestReward = averageQuestReward,
            FoodUnitPrice = foodUnitPrice,
            QuestIntervalDays = corrected.QuestEveryDays,
            SustainableNetGoldPerDay = Math.Max(1.0, result.NetGoldPerDay),
        };
    }

    private static void ProcessQuestIncome(EconomySimProfile profile, EconomySimResult result, int day, ref int gold)
    {
        if (profile.QuestEveryDays <= 0 || day % profile.QuestEveryDays != 0) return;
        gold += profile.AverageQuestReward;
        result.TotalQuestIncome += profile.AverageQuestReward;
        result.QuestsCompleted++;
    }

    private static void ProcessFoodResupply(EconomySimProfile profile, EconomySimResult result, int day, ref int gold, ref float food)
    {
        if (!profile.EnableFoodResupply || profile.ResupplyEveryDays <= 0) return;
        if (day % profile.ResupplyEveryDays != 0 || food >= profile.FoodResupplyThreshold) return;

        float amount = Math.Max(0f, profile.FoodResupplyTarget - food);
        int cost = (int)Math.Ceiling(amount * profile.FoodUnitPrice);
        if (TrySpend(ref gold, cost))
        {
            food += amount;
            result.TotalFoodPurchases += cost;
        }
        else
        {
            result.FirstBrokeDay ??= day;
        }
    }

    private static void ProcessRecruiting(EconomySimProfile profile, EconomySimResult result, PartyRoster roster, int day, ref int gold)
    {
        if (profile.RecruitEveryDays <= 0 || day % profile.RecruitEveryDays != 0 || roster.Count >= roster.Capacity) return;

        if (TrySpend(ref gold, profile.RecruitCost))
        {
            var unit = MakeUnit($"新佣兵{result.RecruitsHired + 1}", profile.RecruitLevel);
            roster.Add(unit);
            result.RecruitsHired++;
            result.TotalRecruiting += profile.RecruitCost;
        }
        else
        {
            result.FirstBrokeDay ??= day;
        }
    }

    private static void ProcessFacilitySpend(EconomySimProfile profile, EconomySimResult result, int day, ref int gold)
    {
        if (profile.FacilityEveryDays <= 0 || day % profile.FacilityEveryDays != 0) return;

        if (TrySpend(ref gold, profile.FacilityGoldSpend))
            result.TotalFacilities += profile.FacilityGoldSpend;
        else
            result.FirstBrokeDay ??= day;
    }

    private static void ProcessWages(EconomySimResult result, PartyRoster roster, WageSystem wage, int day, ref int gold)
    {
        int goldBeforeWage = gold;
        int countBeforeWage = roster.Count;
        int wageGold = gold;
        var wageResult = wage.ProcessDaily(roster, day, amount => TrySpend(ref wageGold, amount));
        gold = wageGold;

        // WageSystem 只通过 trySpendGold 扣款，因此这里用前后金币差统计真实工资支出。
        result.TotalWages += Math.Max(0, goldBeforeWage - wageGold);
        if (!wageResult.Paid && wageResult.TotalWageDue > 0)
        {
            result.UnpaidDays++;
            result.FirstBrokeDay ??= day;
        }
        if (roster.Count < countBeforeWage)
            result.Desertions += countBeforeWage - roster.Count;
    }

    private static void ProcessFood(EconomySimProfile profile, EconomySimResult result, PartyRoster roster, FoodSystem foodSystem, int day, ref float food)
    {
        if (profile.IncludeHourlyTravelFood && profile.TravelHoursPerDay > 0)
        {
            float consumed = roster.Count * foodSystem.FoodPerMemberPerDay * (profile.TravelHoursPerDay / 24f);
            float before = food;
            food = Math.Max(0f, food - consumed);
            result.TotalFoodConsumed += before - food;
        }

        if (profile.IncludeDailyFoodSettlement)
        {
            float before = food;
            var foodResult = foodSystem.ProcessDaily(roster, ref food);
            result.TotalFoodConsumed += before - food;
            if (foodResult.Starving)
            {
                result.StarvedDays++;
                result.FirstStarveDay ??= day;
            }
        }
    }

    private static PartyRoster BuildRoster(int partySize, int memberLevel, int capacity)
    {
        var roster = new PartyRoster { Capacity = Math.Max(capacity, partySize) };
        roster.SetLeader(MakeUnit("队长", memberLevel));
        for (int i = 1; i < partySize; i++)
            roster.Add(MakeUnit($"佣兵{i}", memberLevel));
        return roster;
    }

    private static UnitData MakeUnit(string name, int level)
    {
        var unit = new UnitData
        {
            UnitName = name,
            Level = level,
            BaseMaxHp = 10,
            Morale = 50,
        };
        PartyRoster.SetCurrentHp(unit, unit.BaseMaxHp);
        return unit;
    }

    private static bool TrySpend(ref int gold, int amount)
    {
        if (amount <= 0) return true;
        if (gold < amount) return false;
        gold -= amount;
        return true;
    }

    private static string Trim(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..Math.Max(0, maxChars - 1)] + "…";
    }
}
