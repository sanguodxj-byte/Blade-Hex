// EconomySystemIntegrationTests.cs
// 佣兵团经济与生存系统集成测试
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树
//   - 覆盖 WageSystem 欠饷→阻断恢复链、FoodSystem 断粮→惩罚链、新存档字段往返序列化
//   - 每个 Test_xxx 方法返回 (bool ok, string description)

using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Tests.Strategic;

public static class EconomySystemIntegrationTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else    { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(WageSystem_Deducts_Gold_Each_Day),          WageSystem_Deducts_Gold_Each_Day);
        yield return Run(nameof(WageSystem_Tracks_Unpaid_Days),              WageSystem_Tracks_Unpaid_Days);
        yield return Run(nameof(WageSystem_Blocks_Restore_And_Keeps_Roster_When_Unpaid), WageSystem_Blocks_Restore_And_Keeps_Roster_When_Unpaid);
        yield return Run(nameof(WageSystem_Setter_Restores_UnpaidDays),      WageSystem_Setter_Restores_UnpaidDays);
        yield return Run(nameof(FoodSystem_Deducts_Food_Each_Day),           FoodSystem_Deducts_Food_Each_Day);
        yield return Run(nameof(FoodSystem_Sets_Starving_When_No_Food),      FoodSystem_Sets_Starving_When_No_Food);
        yield return Run(nameof(FoodSystem_Blocks_HpRestore_When_Starving),  FoodSystem_Blocks_HpRestore_When_Starving);
        yield return Run(nameof(FoodSystem_Setter_Restores_StarveDays),      FoodSystem_Setter_Restores_StarveDays);
        yield return Run(nameof(EconomySaveData_Roundtrip_New_Fields),       EconomySaveData_Roundtrip_New_Fields);
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

    // ============================================================================
    // WageSystem 测试
    // ============================================================================

    /// <summary>有足够金币时，每日应成功扣除军饷</summary>
    private static (bool, string) WageSystem_Deducts_Gold_Each_Day()
    {
        var wage = new WageSystem();
        var roster = BuildRoster(leaderLevel: 5, memberLevel: 2, memberCount: 2);

        int gold = 200;
        int expectedWage = 2 * wage.GetDailyWage(roster.Members[1]); // 2 名队员
        bool spent = false;

        var result = wage.ProcessDaily(roster, 1, trySpendGold: amt =>
        {
            if (gold >= amt) { gold -= amt; spent = true; return true; }
            return false;
        });

        if (!result.Paid)
            return (false, $"应当成功扣薪，但未扣除。TotalWageDue={result.TotalWageDue}");
        if (!spent)
            return (false, "trySpendGold 未被调用");
        if (gold != 200 - expectedWage)
            return (false, $"扣薪金额不正确：期望 {expectedWage}，实际扣 {200 - gold}");

        return (true, "");
    }

    /// <summary>金库不足时，应累计欠饷天数</summary>
    private static (bool, string) WageSystem_Tracks_Unpaid_Days()
    {
        var wage = new WageSystem();
        var roster = BuildRoster(leaderLevel: 5, memberLevel: 2, memberCount: 1);
        int gold = 0; // 空金库

        for (int i = 1; i <= 2; i++)
        {
            wage.ProcessDaily(roster, i, amt => gold >= amt);
        }

        if (wage.ConsecutiveUnpaidDays != 2)
            return (false, $"期望欠饷 2 天，实际 {wage.ConsecutiveUnpaidDays}");

        return (true, "");
    }

    /// <summary>欠饷时应阻断自然恢复，但不移除队员</summary>
    private static (bool, string) WageSystem_Blocks_Restore_And_Keeps_Roster_When_Unpaid()
    {
        var wage = new WageSystem();
        var roster = BuildRoster(leaderLevel: 5, memberLevel: 2, memberCount: 2);

        int gold = 0;
        int desertedCount = 0;

        for (int day = 1; day <= 3; day++)
        {
            var r = wage.ProcessDaily(roster, day, amt => gold >= amt);
            desertedCount += r.DesertedUnits.Count;
        }

        if (wage.CanRestore)
            return (false, "欠饷时应禁止自然恢复");
        if (desertedCount != 0)
            return (false, $"当前设计为欠饷不离队，但记录了 {desertedCount} 名离队");
        if (roster.Members.Count != 3)
            return (false, $"欠饷不应移除队员：期望 3，实际 {roster.Members.Count}");

        return (true, "");
    }

    /// <summary>SetConsecutiveUnpaidDays 应正确还原欠饷天数（读档逻辑）</summary>
    private static (bool, string) WageSystem_Setter_Restores_UnpaidDays()
    {
        var wage = new WageSystem();
        wage.SetConsecutiveUnpaidDays(5);

        if (wage.ConsecutiveUnpaidDays != 5)
            return (false, $"期望 5，实际 {wage.ConsecutiveUnpaidDays}");

        return (true, "");
    }

    // ============================================================================
    // FoodSystem 测试
    // ============================================================================

    /// <summary>食物充足时，每日应正确扣除人均口粮</summary>
    private static (bool, string) FoodSystem_Deducts_Food_Each_Day()
    {
        var food = new FoodSystem();
        var roster = BuildRoster(leaderLevel: 5, memberLevel: 2, memberCount: 1);
        float currentFood = 10.0f;
        float expectedDeduction = roster.Count * food.FoodPerMemberPerDay;

        var result = food.ProcessDaily(roster, ref currentFood);

        if (result.Starving)
            return (false, "食物充足时不应断粮");
        if (Math.Abs(currentFood - (10.0f - expectedDeduction)) > 0.001f)
            return (false, $"扣粮量不正确：期望 {10.0f - expectedDeduction:F2}，实际 {currentFood:F2}");

        return (true, "");
    }

    /// <summary>食物不足时，应设置断粮状态并累计天数</summary>
    private static (bool, string) FoodSystem_Sets_Starving_When_No_Food()
    {
        var food = new FoodSystem();
        var roster = BuildRoster(leaderLevel: 5, memberLevel: 2, memberCount: 2);
        float currentFood = 0.0f;

        var result1 = food.ProcessDaily(roster, ref currentFood);
        var result2 = food.ProcessDaily(roster, ref currentFood);

        if (!result2.Starving)
            return (false, "无食物时应断粮");
        if (food.ConsecutiveStarveDays != 2)
            return (false, $"断粮天数应为 2，实际 {food.ConsecutiveStarveDays}");

        return (true, "");
    }

    /// <summary>断粮时 CanRestoreHp 应返回 false</summary>
    private static (bool, string) FoodSystem_Blocks_HpRestore_When_Starving()
    {
        var food = new FoodSystem();
        var roster = BuildRoster(leaderLevel: 5, memberLevel: 2, memberCount: 1);
        float currentFood = 0.0f;

        food.ProcessDaily(roster, ref currentFood);

        if (food.CanRestoreHp)
            return (false, "断粮时 CanRestoreHp 应为 false");

        return (true, "");
    }

    /// <summary>SetConsecutiveStarveDays 应正确还原断粮天数（读档逻辑）</summary>
    private static (bool, string) FoodSystem_Setter_Restores_StarveDays()
    {
        var food = new FoodSystem();
        food.SetConsecutiveStarveDays(3);

        if (food.ConsecutiveStarveDays != 3)
            return (false, $"期望 3，实际 {food.ConsecutiveStarveDays}");
        if (food.CanRestoreHp)
            return (false, "断粮状态下 CanRestoreHp 应为 false");

        return (true, "");
    }

    // ============================================================================
    // EconomySaveData 序列化往返测试
    // ============================================================================

    /// <summary>EconomySaveData 的 4 个新字段应正确序列化和反序列化</summary>
    private static (bool, string) EconomySaveData_Roundtrip_New_Fields()
    {
        var original = new EconomySaveData
        {
            Gold = 500,
            Food = 18.5f,
            DaysPassed = 12,
            Month = 3,
            Year = 1252,
            CurrentHour = 14,
            ConsecutiveUnpaidDays = 2,
            ConsecutiveStarveDays = 1,
            Tools = 15.0f,
            Medicine = 8.5f,
        };

        // 模拟 JSON 序列化往返（这里用 System.Text.Json）
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        var restored = System.Text.Json.JsonSerializer.Deserialize<EconomySaveData>(json);

        if (restored == null)
            return (false, "反序列化结果为 null");

        if (restored.ConsecutiveUnpaidDays != 2)
            return (false, $"欠饷天数不匹配：期望 2，实际 {restored.ConsecutiveUnpaidDays}");
        if (restored.ConsecutiveStarveDays != 1)
            return (false, $"断粮天数不匹配：期望 1，实际 {restored.ConsecutiveStarveDays}");
        if (Math.Abs(restored.Tools - 15.0f) > 0.001f)
            return (false, $"工具存量不匹配：期望 15.0，实际 {restored.Tools}");
        if (Math.Abs(restored.Medicine - 8.5f) > 0.001f)
            return (false, $"药品存量不匹配：期望 8.5，实际 {restored.Medicine}");

        return (true, "");
    }

    // ============================================================================
    // 工具方法
    // ============================================================================

    /// <summary>构建一个带指定人数队员的测试名册</summary>
    private static PartyRoster BuildRoster(int leaderLevel, int memberLevel, int memberCount)
    {
        var roster = new PartyRoster();

        var leader = new UnitData { UnitName = "队长", Level = leaderLevel };
        PartyRoster.SetCurrentHp(leader, leader.BaseMaxHp);
        roster.SetLeader(leader);

        for (int i = 0; i < memberCount; i++)
        {
            var m = new UnitData { UnitName = $"佣兵{i + 1}", Level = memberLevel };
            PartyRoster.SetCurrentHp(m, m.BaseMaxHp);
            roster.Add(m);
        }

        return roster;
    }
}
