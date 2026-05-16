// QuestGeneratorTests.cs
// 委托生成器单元测试 — 服务于架构优化 spec R7。
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树（但 QuestTemplateLoader 用 Godot FileAccess 加载模板，
//     未加载到时会走 CreateFallbackTemplates 兜底，行为依然可测）
//   - 每个 Test_xxx 方法返回 (bool ok, string description)
//
// 覆盖关键路径：
//   - 池容量约束（≤ MaxQuestsPerPoi）
//   - 刷新间隔：未到 RefreshIntervalDays 时返回同一池
//   - 刷新间隔：到期后池被替换
//   - AcceptQuest 从池中移除并设置 Active 状态
//   - 不同 POI 拥有独立池
//   - 空 POI 列表 → 空池
//   - 任务字段完整性（QuestId、QuestName、IssuerName 非空）
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Data;

namespace BladeHex.Strategic.Tests;

public static class QuestGeneratorTests
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
        yield return Run(nameof(EmptyPoiList_ReturnsEmptyQuests), EmptyPoiList_ReturnsEmptyQuests);
        yield return Run(nameof(GetAvailableQuests_RespectsMaxPerPoi), GetAvailableQuests_RespectsMaxPerPoi);
        yield return Run(nameof(GetAvailableQuests_ConsistentWithinInterval), GetAvailableQuests_ConsistentWithinInterval);
        yield return Run(nameof(GetAvailableQuests_RefreshAfterInterval), GetAvailableQuests_RefreshAfterInterval);
        yield return Run(nameof(AcceptQuest_RemovesFromPool), AcceptQuest_RemovesFromPool);
        yield return Run(nameof(AcceptQuest_OutOfBounds_ReturnsNull), AcceptQuest_OutOfBounds_ReturnsNull);
        yield return Run(nameof(DifferentPois_HaveSeparatePools), DifferentPois_HaveSeparatePools);
        yield return Run(nameof(GeneratedQuests_HaveRequiredFields), GeneratedQuests_HaveRequiredFields);
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

    private static (bool, string) EmptyPoiList_ReturnsEmptyQuests()
    {
        var gen = new QuestGenerator();
        gen.Initialize(new List<OverworldPOI>(), worldSeed: 42);
        var quests = gen.GetAvailableQuests("noexist", currentDay: 1);
        if (quests.Count != 0) return (false, $"expected 0, got {quests.Count}");
        return (true, "");
    }

    private static (bool, string) GetAvailableQuests_RespectsMaxPerPoi()
    {
        var gen = new QuestGenerator { MaxQuestsPerPoi = 5 };
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        // currentDay 必须 ≥ RefreshIntervalDays（默认 3）才会触发首次刷新
        var quests = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (quests.Count > 5) return (false, $"exceeded max: got {quests.Count}");
        // 期望 3-5 个
        if (quests.Count < 3) return (false, $"expected at least 3 quests, got {quests.Count}");
        return (true, "");
    }

    private static (bool, string) GetAvailableQuests_ConsistentWithinInterval()
    {
        var gen = new QuestGenerator { RefreshIntervalDays = 3 };
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        var first = gen.GetAvailableQuests("Town_A", currentDay: 5);
        var firstCount = first.Count;
        var firstFirstId = firstCount > 0 ? first[0].QuestId : "";

        // 在同一刷新窗口内调用应返回相同的池
        var second = gen.GetAvailableQuests("Town_A", currentDay: 6);

        if (second.Count != firstCount)
            return (false, $"count changed within interval: {firstCount} → {second.Count}");
        if (firstCount > 0 && second[0].QuestId != firstFirstId)
            return (false, $"first quest changed within interval");
        return (true, "");
    }

    private static (bool, string) GetAvailableQuests_RefreshAfterInterval()
    {
        var gen = new QuestGenerator { RefreshIntervalDays = 3 };
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        var first = gen.GetAvailableQuests("Town_A", currentDay: 5);
        var firstFirstId = first.Count > 0 ? first[0].QuestId : "";

        // Day 10: 已超过 5 + 3，应触发刷新
        var second = gen.GetAvailableQuests("Town_A", currentDay: 10);
        if (second.Count == 0) return (false, "second pool is empty");

        // ID 应该不同（含 currentDay 在 ID 中）
        if (second.Count > 0 && firstFirstId == second[0].QuestId)
            return (false, "expected different quest IDs after refresh");
        return (true, "");
    }

    private static (bool, string) AcceptQuest_RemovesFromPool()
    {
        var gen = new QuestGenerator();
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        var poolBefore = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (poolBefore.Count == 0) return (false, "pool initially empty");
        int countBefore = poolBefore.Count;

        var accepted = gen.AcceptQuest("Town_A", questIndex: 0, currentDay: 5);
        if (accepted == null) return (false, "AcceptQuest returned null");

        var poolAfter = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (poolAfter.Count != countBefore - 1)
            return (false, $"pool size: expected {countBefore - 1}, got {poolAfter.Count}");

        if (accepted.Status != QuestData.QuestStatus.Active)
            return (false, $"expected Status=Active, got {accepted.Status}");
        return (true, "");
    }

    private static (bool, string) AcceptQuest_OutOfBounds_ReturnsNull()
    {
        var gen = new QuestGenerator();
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        // 提前刷新池
        _ = gen.GetAvailableQuests("Town_A", currentDay: 5);

        var negative = gen.AcceptQuest("Town_A", questIndex: -1, currentDay: 5);
        if (negative != null) return (false, "negative index should return null");

        var tooLarge = gen.AcceptQuest("Town_A", questIndex: 999, currentDay: 5);
        if (tooLarge != null) return (false, "out-of-range index should return null");
        return (true, "");
    }

    private static (bool, string) DifferentPois_HaveSeparatePools()
    {
        var gen = new QuestGenerator();
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        var poolA = gen.GetAvailableQuests("Town_A", currentDay: 5);
        var poolB = gen.GetAvailableQuests("Town_B", currentDay: 5);

        if (poolA.Count == 0 || poolB.Count == 0)
            return (false, "expected both pools to have quests");

        // 不同 POI 的种子异或不同，第一个任务的 ID 应不同（含 issuer 名字）
        if (poolA[0].QuestId == poolB[0].QuestId)
            return (false, "different POIs should produce different quest IDs");
        if (poolA[0].IssuerName == poolB[0].IssuerName)
            return (false, "issuer should differ between POIs");
        return (true, "");
    }

    private static (bool, string) GeneratedQuests_HaveRequiredFields()
    {
        var gen = new QuestGenerator();
        var pois = MakePoiPair();
        gen.Initialize(pois, worldSeed: 42);

        var pool = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (pool.Count == 0) return (false, "pool is empty");

        foreach (var q in pool)
        {
            if (string.IsNullOrEmpty(q.QuestId)) return (false, "QuestId empty");
            if (string.IsNullOrEmpty(q.QuestName)) return (false, "QuestName empty");
            if (string.IsNullOrEmpty(q.IssuerName)) return (false, "IssuerName empty");
            if (q.RewardGold <= 0) return (false, $"RewardGold should be > 0, got {q.RewardGold}");
        }
        return (true, "");
    }

    // ============================================================================
    // 工具方法
    // ============================================================================

    private static List<OverworldPOI> MakePoiPair()
    {
        var a = new OverworldPOI
        {
            PoiName = "Town_A",
            Position = new Vector2(100, 100),
            PoiTypeEnum = OverworldPOI.POIType.Town,
            OwningFaction = "neutral",
        };
        var b = new OverworldPOI
        {
            PoiName = "Town_B",
            Position = new Vector2(500, 500),
            PoiTypeEnum = OverworldPOI.POIType.Town,
            OwningFaction = "neutral",
        };
        return new List<OverworldPOI> { a, b };
    }
}
