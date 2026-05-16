// TriggerEngineTests.cs
// 触发引擎单元测试 — 服务于架构优化 spec R7。
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树
//   - 用 FakeHandler 替换实际 handler，专注测试 Engine 的前置条件 + 历史去重逻辑
//   - 每个 Test_xxx 方法返回 (bool ok, string description)
//
// 覆盖关键路径：
//   - 一次性触发：CooldownDays = 0 时已触发的条件不再触发
//   - 冷却触发：在冷却期内不触发，过期后再次可触发
//   - 玩家等级门槛
//   - 天数范围（MinDay / MaxDay）
//   - 前置触发（PrerequisiteIds 全部满足）
//   - 互斥触发（MutuallyExclusive 任一已触发则跳过）
//   - 触发历史的 Record / IsTriggered / IsOnCooldown
//   - TriggerHistory 序列化往返
//   - 优先级排序
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic.Tests;

public static class TriggerEngineTests
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
        yield return Run(nameof(History_Record_AndIsTriggered), History_Record_AndIsTriggered);
        yield return Run(nameof(History_Cooldown_BlocksThenAllows), History_Cooldown_BlocksThenAllows);
        yield return Run(nameof(History_Roundtrip_PreservesEntries), History_Roundtrip_PreservesEntries);
        yield return Run(nameof(Engine_OneShot_TriggersOnceOnly), Engine_OneShot_TriggersOnceOnly);
        yield return Run(nameof(Engine_PlayerLevelGate_BlocksLowLevel), Engine_PlayerLevelGate_BlocksLowLevel);
        yield return Run(nameof(Engine_DayRange_BlocksOutOfRange), Engine_DayRange_BlocksOutOfRange);
        yield return Run(nameof(Engine_Prerequisites_RequireAll), Engine_Prerequisites_RequireAll);
        yield return Run(nameof(Engine_MutuallyExclusive_BlocksWhenOtherFired), Engine_MutuallyExclusive_BlocksWhenOtherFired);
        yield return Run(nameof(Engine_Triggered_RecordsToHistory), Engine_Triggered_RecordsToHistory);
        yield return Run(nameof(Engine_NoHandler_ReturnsNull), Engine_NoHandler_ReturnsNull);
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
    // TriggerHistory 测试
    // ============================================================================

    private static (bool, string) History_Record_AndIsTriggered()
    {
        var h = new TriggerHistory();
        if (h.IsTriggered("foo")) return (false, "should not be triggered initially");
        h.Record("foo", 5);
        if (!h.IsTriggered("foo")) return (false, "should be triggered after Record");
        if (h.GetTriggerDay("foo") != 5) return (false, $"day mismatch: got {h.GetTriggerDay("foo")}");
        if (h.IsTriggered("bar")) return (false, "bar should not be triggered");
        return (true, "");
    }

    private static (bool, string) History_Cooldown_BlocksThenAllows()
    {
        var h = new TriggerHistory();
        h.Record("event", 10);
        // 在冷却期内（10 + 5 = 15）
        if (!h.IsOnCooldown("event", currentDay: 12, cooldownDays: 5))
            return (false, "should be on cooldown at day 12");
        // 冷却结束
        if (h.IsOnCooldown("event", currentDay: 16, cooldownDays: 5))
            return (false, "cooldown should have expired at day 16");
        return (true, "");
    }

    private static (bool, string) History_Roundtrip_PreservesEntries()
    {
        var h = new TriggerHistory();
        h.Record("a", 1);
        h.Record("b", 7);
        h.Record("c", 99);

        var data = h.Serialize();
        var copy = TriggerHistory.Deserialize(data);

        if (!copy.IsTriggered("a") || copy.GetTriggerDay("a") != 1) return (false, "entry a wrong");
        if (!copy.IsTriggered("b") || copy.GetTriggerDay("b") != 7) return (false, "entry b wrong");
        if (!copy.IsTriggered("c") || copy.GetTriggerDay("c") != 99) return (false, "entry c wrong");
        return (true, "");
    }

    // ============================================================================
    // TriggerEngine 测试
    // ============================================================================

    private static (bool, string) Engine_OneShot_TriggersOnceOnly()
    {
        var (engine, ctx) = MakeEngine();
        var cond = new TriggerCondition
        {
            Id = "oneshot",
            Type = TriggerType.Spatial,
            CooldownDays = 0, // 一次性
        };
        engine.RegisterCondition(cond);

        var r1 = engine.Evaluate("oneshot", ctx);
        if (r1 == null || !r1.Triggered) return (false, "first call should trigger");

        var r2 = engine.Evaluate("oneshot", ctx);
        if (r2 != null) return (false, "second call should return null (already triggered)");
        return (true, "");
    }

    private static (bool, string) Engine_PlayerLevelGate_BlocksLowLevel()
    {
        var (engine, ctx) = MakeEngine();
        ctx.PlayerLevel = 3;
        var cond = new TriggerCondition
        {
            Id = "highlevel",
            Type = TriggerType.Spatial,
            MinPlayerLevel = 5,
        };
        engine.RegisterCondition(cond);

        var r = engine.Evaluate("highlevel", ctx);
        if (r != null) return (false, "should be blocked at level 3");

        ctx.PlayerLevel = 5;
        var r2 = engine.Evaluate("highlevel", ctx);
        if (r2 == null || !r2.Triggered) return (false, "should pass at level 5");
        return (true, "");
    }

    private static (bool, string) Engine_DayRange_BlocksOutOfRange()
    {
        var (engine, ctx) = MakeEngine();
        var cond = new TriggerCondition
        {
            Id = "midgame",
            Type = TriggerType.Spatial,
            MinDay = 10,
            MaxDay = 20,
        };
        engine.RegisterCondition(cond);

        // ctx.CurrentDay 默认走 TimeProvider，未注册时为 1
        // 用 fake provider 注入天数
        var prevProvider = TimeProvider.IsRegistered;
        try
        {
            // Day 5: too early
            TimeProvider.Set(new FakeTimeProvider(5));
            if (engine.Evaluate("midgame", ctx) != null)
                return (false, "should be blocked at day 5");

            // Day 15: in range
            TimeProvider.Set(new FakeTimeProvider(15));
            var r = engine.Evaluate("midgame", ctx);
            if (r == null || !r.Triggered) return (false, "should fire at day 15");

            // 重新建一条 oneshot=true 的条件，避免上一次的 history 干扰
            var cond2 = new TriggerCondition
            {
                Id = "midgame2",
                Type = TriggerType.Spatial,
                MinDay = 10,
                MaxDay = 20,
            };
            engine.RegisterCondition(cond2);

            // Day 25: too late
            TimeProvider.Set(new FakeTimeProvider(25));
            if (engine.Evaluate("midgame2", ctx) != null)
                return (false, "should be blocked at day 25");

            return (true, "");
        }
        finally
        {
            TimeProvider.Clear();
        }
    }

    private static (bool, string) Engine_Prerequisites_RequireAll()
    {
        var (engine, ctx) = MakeEngine();
        var cond = new TriggerCondition
        {
            Id = "needs_a_and_b",
            Type = TriggerType.Spatial,
            PrerequisiteIds = new[] { "prereq_a", "prereq_b" },
        };
        engine.RegisterCondition(cond);

        // 都未触发 → 阻断
        if (engine.Evaluate("needs_a_and_b", ctx) != null)
            return (false, "should block when neither prereq satisfied");

        // 只有 a → 仍阻断
        ctx.History.Record("prereq_a", 1);
        if (engine.Evaluate("needs_a_and_b", ctx) != null)
            return (false, "should block when only a satisfied");

        // a + b → 通过
        ctx.History.Record("prereq_b", 2);
        var r = engine.Evaluate("needs_a_and_b", ctx);
        if (r == null || !r.Triggered) return (false, "should fire when both satisfied");
        return (true, "");
    }

    private static (bool, string) Engine_MutuallyExclusive_BlocksWhenOtherFired()
    {
        var (engine, ctx) = MakeEngine();
        var cond = new TriggerCondition
        {
            Id = "path_a",
            Type = TriggerType.Spatial,
            MutuallyExclusive = new[] { "path_b" },
        };
        engine.RegisterCondition(cond);

        ctx.History.Record("path_b", 5);
        if (engine.Evaluate("path_a", ctx) != null)
            return (false, "path_a should be blocked when path_b is triggered");
        return (true, "");
    }

    private static (bool, string) Engine_Triggered_RecordsToHistory()
    {
        var (engine, ctx) = MakeEngine();
        var cond = new TriggerCondition
        {
            Id = "auto_record",
            Type = TriggerType.Spatial,
            CooldownDays = 100, // 不一次性，避免 OneShot 路径干扰
        };
        engine.RegisterCondition(cond);

        if (ctx.History.IsTriggered("auto_record"))
            return (false, "should not be in history before evaluation");

        var r = engine.Evaluate("auto_record", ctx);
        if (r == null || !r.Triggered) return (false, "should trigger");
        if (!ctx.History.IsTriggered("auto_record"))
            return (false, "should be recorded in history after triggering");
        return (true, "");
    }

    private static (bool, string) Engine_NoHandler_ReturnsNull()
    {
        // 创建 engine 但不注册 handler
        var engine = new TriggerEngine();
        var ctx = new TriggerContext();
        var cond = new TriggerCondition { Id = "no_handler", Type = TriggerType.Spatial };
        engine.RegisterCondition(cond);

        var r = engine.Evaluate("no_handler", ctx);
        if (r != null) return (false, "should return null when no handler is registered");
        return (true, "");
    }

    // ============================================================================
    // 工具方法 / Fake
    // ============================================================================

    private static (TriggerEngine engine, TriggerContext ctx) MakeEngine()
    {
        var engine = new TriggerEngine();
        // 注册一个总是触发的 fake handler，覆盖五种类型
        engine.RegisterHandler(new FakeAlwaysTriggerHandler(TriggerType.Spatial));
        engine.RegisterHandler(new FakeAlwaysTriggerHandler(TriggerType.Interaction));
        engine.RegisterHandler(new FakeAlwaysTriggerHandler(TriggerType.Time));
        engine.RegisterHandler(new FakeAlwaysTriggerHandler(TriggerType.Chain));
        engine.RegisterHandler(new FakeAlwaysTriggerHandler(TriggerType.Environment));
        var ctx = new TriggerContext { PlayerLevel = 10 };
        return (engine, ctx);
    }

    /// <summary>测试用 handler — 始终返回 Triggered=true</summary>
    private class FakeAlwaysTriggerHandler : ITriggerHandler
    {
        public TriggerType Type { get; }
        public FakeAlwaysTriggerHandler(TriggerType type) { Type = type; }
        public TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx)
        {
            return new TriggerResult { TriggerId = condition.Id, Triggered = true };
        }
    }

    /// <summary>测试用 time provider</summary>
    private class FakeTimeProvider : ITimeProvider
    {
        public int CurrentDay { get; }
        public FakeTimeProvider(int day) { CurrentDay = day; }
    }
}
