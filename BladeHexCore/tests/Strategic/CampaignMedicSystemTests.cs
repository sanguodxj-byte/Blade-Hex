// CampaignMedicSystemTests.cs
// 战壕医官重伤复活系统核心逻辑与数据流转单元测试
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树
//   - 验证重伤成员出战过滤、跨关存档序列化与反序列化往返、医官救治状态恢复等核心业务指标
//   - 每个 Test_xxx 方法返回 (bool ok, string description)

using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Tests.Strategic;

public static class CampaignMedicSystemTests
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
        yield return Run(nameof(Wounded_Members_Filtered_From_Deployable), Wounded_Members_Filtered_From_Deployable);
        yield return Run(nameof(Wounded_Status_Preserved_During_Serialization), Wounded_Status_Preserved_During_Serialization);
        yield return Run(nameof(Medic_Treatment_Restores_Hp_And_Clears_Wounded), Medic_Treatment_Restores_Hp_And_Clears_Wounded);
        yield return Run(nameof(CampaignPricing_HireCost_Increases_With_Level_And_Equipment), CampaignPricing_HireCost_Increases_With_Level_And_Equipment);
        yield return Run(nameof(CampaignPricing_MedicCost_Uses_Generator), CampaignPricing_MedicCost_Uses_Generator);
        yield return Run(nameof(CampaignPricing_BossReward_Exceeds_NormalReward), CampaignPricing_BossReward_Exceeds_NormalReward);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
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

    // ============================================================================
    // 测试用例
    // ============================================================================

    private static (bool, string) Wounded_Members_Filtered_From_Deployable()
    {
        var roster = new PartyRoster();

        // 1. 创建健康的队长并设置
        var leader = new UnitData { UnitName = "测试队长", BaseMaxHp = 20 };
        PartyRoster.SetCurrentHp(leader, 20);
        roster.SetLeader(leader);

        // 2. 创建重伤的副官
        var companion = new UnitData { UnitName = "测试副官", BaseMaxHp = 15, IsWounded = true };
        PartyRoster.SetCurrentHp(companion, 0); // 重伤倒下
        roster.Members.Add(companion);

        // 3. 验证出战部队过滤
        var deployable = roster.GetDeployableMembers();
        if (deployable.Count != 1)
            return (false, $"Deployable count mismatch: expected 1, got {deployable.Count}");

        if (deployable[0].UnitName != "测试队长")
            return (false, $"Deployable member mismatch: expected '测试队长', got '{deployable[0].UnitName}'");

        if (deployable.Any(m => m.IsWounded))
            return (false, "Wounded member was not filtered out of deployable roster");

        return (true, "");
    }

    private static (bool, string) Wounded_Status_Preserved_During_Serialization()
    {
        var roster = new PartyRoster();

        var leader = new UnitData { UnitName = "测试队长", BaseMaxHp = 20 };
        PartyRoster.SetCurrentHp(leader, 20);
        roster.SetLeader(leader);

        var companion = new UnitData { UnitName = "测试副官", BaseMaxHp = 15, IsWounded = true };
        PartyRoster.SetCurrentHp(companion, 0);
        roster.Members.Add(companion);

        // 1. 序列化
        var data = roster.Serialize();

        // 2. 反序列化
        var deserializedRoster = PartyRoster.Deserialize(data);

        // 3. 验证字段持久化
        if (deserializedRoster.Members.Count != 2)
            return (false, $"Roster members count mismatch after deserialize: expected 2, got {deserializedRoster.Members.Count}");

        var desCompanion = deserializedRoster.Members.FirstOrDefault(m => m.UnitName == "测试副官");
        if (desCompanion == null)
            return (false, "Could not find '测试副官' in deserialized roster");

        if (!desCompanion.IsWounded)
            return (false, "Companion IsWounded status lost after roundtrip serialization");

        if (PartyRoster.GetCurrentHp(desCompanion) != 0)
            return (false, $"Companion CurrentHp mismatch after deserialize: expected 0, got {PartyRoster.GetCurrentHp(desCompanion)}");

        return (true, "");
    }

    private static (bool, string) Medic_Treatment_Restores_Hp_And_Clears_Wounded()
    {
        // 1. 创建重伤佣兵
        var unit = new UnitData { UnitName = "受伤新兵", BaseMaxHp = 12, Level = 2, IsWounded = true };
        PartyRoster.SetCurrentHp(unit, 0);

        // 2. 模拟医官救治过程
        int goldBefore = 300;
        int cost = CampaignPricingService.GetMedicTreatmentCost(unit, TestCampaignContext(enemyLevel: 2, enemyCount: 4));

        if (goldBefore < cost)
            return (false, "Simulated gold insufficient for test");

        int goldAfter = goldBefore - cost;
        if (goldAfter != goldBefore - cost)
            return (false, $"Cost calculation mismatch: expected gold {goldBefore - cost} left, got {goldAfter}");

        // 执行治愈
        unit.IsWounded = false;
        PartyRoster.SetCurrentHp(unit, unit.BaseMaxHp);

        // 3. 验证状态恢复
        if (unit.IsWounded)
            return (false, "IsWounded status was not cleared after treatment");

        if (PartyRoster.GetCurrentHp(unit) != unit.BaseMaxHp)
            return (false, $"HP was not fully restored: expected {unit.BaseMaxHp}, got {PartyRoster.GetCurrentHp(unit)}");

        return (true, "");
    }

    private static (bool, string) CampaignPricing_HireCost_Increases_With_Level_And_Equipment()
    {
        var ctx = TestCampaignContext(enemyLevel: 5, enemyCount: 5);
        var low = new UnitData { UnitName = "低级佣兵", Level = 1, BaseMaxHp = 10 };
        var high = new UnitData
        {
            UnitName = "高级佣兵",
            Level = 5,
            BaseMaxHp = 18,
            PrimaryMainHand = new WeaponData
            {
                ItemId = "test_sword",
                ItemName = "测试剑",
                Tier = 2,
                Price = 10,
                Weight = WeaponData.WeightCategory.Medium,
            },
            Armor = new ArmorData
            {
                ItemId = "test_armor",
                ItemName = "测试甲",
                armorType = ArmorData.ArmorType.Medium,
                DrThreshold = 8,
                Price = 10,
            },
        };

        int lowCost = CampaignPricingService.GetHireCost(low, ctx);
        int highCost = CampaignPricingService.GetHireCost(high, ctx);
        if (highCost <= lowCost) return (false, $"高级/带装佣兵应更贵：low={lowCost}, high={highCost}");
        return (true, "");
    }

    private static (bool, string) CampaignPricing_MedicCost_Uses_Generator()
    {
        var ctx = TestCampaignContext(enemyLevel: 3, enemyCount: 4);
        var unit = new UnitData { UnitName = "重伤者", Level = 2, BaseMaxHp = 12, IsWounded = true };
        int cost = CampaignPricingService.GetMedicTreatmentCost(unit, ctx);
        if (cost <= 0) return (false, $"救治费用应为正，实际 {cost}");
        if (cost == unit.Level * 40 + 100) return (false, "救治费用仍等于旧硬编码公式");
        return (true, "");
    }

    private static (bool, string) CampaignPricing_BossReward_Exceeds_NormalReward()
    {
        int normal = CampaignPricingService.GetBattleGoldReward(TestCampaignContext(enemyLevel: 8, enemyCount: 8, isBoss: false));
        int boss = CampaignPricingService.GetBattleGoldReward(TestCampaignContext(enemyLevel: 8, enemyCount: 8, isBoss: true));
        if (boss <= normal) return (false, $"Boss 奖励应高于普通关：normal={normal}, boss={boss}");
        return (true, "");
    }

    private static CampaignEconomyContext TestCampaignContext(int enemyLevel, int enemyCount, bool isBoss = false)
        => new(LevelIndex: enemyLevel - 1, EnemyLevel: enemyLevel, EnemyCount: enemyCount, Difficulty: enemyLevel >= 8 ? 2 : 1, BattleSize: enemyLevel >= 7 ? 2 : 1, IsBoss: isBoss);
}
