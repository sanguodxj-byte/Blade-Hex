// BuffSystemTests.cs
// BuffSystem 核心 API 单元测试 — 无 Godot 场景依赖
//
// 覆盖路径:
//   - Apply / ApplyDirect（施加与叠层刷新）
//   - IncrementStacks / SetStacks（层数操作，含边界钳制）
//   - ConsumeModifierStack（修饰器消耗与自动移除）
//   - RemoveBuffInstance（按引用移除）
//   - HasBuff / HasTag / GetStacks（快捷查询）
using System.Collections.Generic;
using BladeHex.Combat.Buff;
using BladeHex.Data;

namespace BladeHex.Combat.Tests;

public static class BuffSystemTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        // 确保 BuffRegistry 已初始化（后续测试依赖 poison / bleed 等注册 buff）
        BuffRegistry.Get("poison");

        // 注册一个最大层数 5 的自定义测试 buff
        if (BuffRegistry.Get("_test_stackable") == null)
        {
            BuffRegistry.Register("_test_stackable", new BuffInstance
            {
                Id = "_test_stackable",
                Name = "Test Stackable",
                MaxStacks = 5,
                Duration = 3,
                Tags = new[] { "test" },
            });
        }

        // 注册一个有 modifier 的测试 buff
        if (BuffRegistry.Get("_test_modifier") == null)
        {
            BuffRegistry.Register("_test_modifier", new BuffInstance
            {
                Id = "_test_modifier",
                Name = "Test Modifier",
                Duration = 3,
                MaxStacks = 3,
                Modifiers = new()
                {
                    new StatModifier { Stat = "test_stat", Layer = ModifierLayer.Base, Value = 10 },
                },
            });
        }

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }

        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Apply_FromRegistry_CreatesBuff), Apply_FromRegistry_CreatesBuff);
        yield return Run(nameof(Apply_SameSourceId_IncrementsStacks), Apply_SameSourceId_IncrementsStacks);
        yield return Run(nameof(Apply_DifferentSource_SeparateBuffs), Apply_DifferentSource_SeparateBuffs);
        yield return Run(nameof(Apply_MaxStacks_NotExceeded), Apply_MaxStacks_NotExceeded);
        yield return Run(nameof(Apply_CancelTags_RemovesConflicting), Apply_CancelTags_RemovesConflicting);
        yield return Run(nameof(ApplyDirect_AddsBuff), ApplyDirect_AddsBuff);
        yield return Run(nameof(ApplyDirect_SameSource_Stacks), ApplyDirect_SameSource_Stacks);

        yield return Run(nameof(IncrementStacks_Normal), IncrementStacks_Normal);
        yield return Run(nameof(IncrementStacks_AtMax_NoChange), IncrementStacks_AtMax_NoChange);
        yield return Run(nameof(IncrementStacks_NoBuff_NoOp), IncrementStacks_NoBuff_NoOp);

        yield return Run(nameof(SetStacks_Normal), SetStacks_Normal);
        yield return Run(nameof(SetStacks_ClampLow), SetStacks_ClampLow);
        yield return Run(nameof(SetStacks_ClampHigh), SetStacks_ClampHigh);
        yield return Run(nameof(SetStacks_NoBuff_NoOp), SetStacks_NoBuff_NoOp);

        yield return Run(nameof(ConsumeModifierStack_Decrements), ConsumeModifierStack_Decrements);
        yield return Run(nameof(ConsumeModifierStack_RemovesOnZero), ConsumeModifierStack_RemovesOnZero);
        yield return Run(nameof(ConsumeModifierStack_NoModifier_Removes), ConsumeModifierStack_NoModifier_Removes);

        yield return Run(nameof(RemoveBuffInstance_Success), RemoveBuffInstance_Success);
        yield return Run(nameof(RemoveBuffInstance_NotFound), RemoveBuffInstance_NotFound);

        yield return Run(nameof(RemoveById_Removes), RemoveById_Removes);
        yield return Run(nameof(RemoveByTag_RemovesAll), RemoveByTag_RemovesAll);
        yield return Run(nameof(RemoveAll_ClearsAll), RemoveAll_ClearsAll);

        yield return Run(nameof(HasBuff_Has_True), HasBuff_Has_True);
        yield return Run(nameof(HasBuff_No_False), HasBuff_No_False);
        yield return Run(nameof(HasTag_Has_True), HasTag_Has_True);
        yield return Run(nameof(HasTag_No_False), HasTag_No_False);
        yield return Run(nameof(GetStacks_ReturnsValue), GetStacks_ReturnsValue);
        yield return Run(nameof(GetStacks_NoBuff_Zero), GetStacks_NoBuff_Zero);

        yield return Run(nameof(ResolveStatModifiers_Sum), ResolveStatModifiers_Sum);
    }

    // ============================================================
    // Apply
    // ============================================================

    private static (bool, string) Apply_FromRegistry_CreatesBuff()
    {
        var target = new UnitData();
        var buff = BuffSystem.Apply(target, "poison");

        if (buff == null) return (false, "Apply returned null");
        if (buff.Id != "poison") return (false, $"expected id='poison', got '{buff.Id}'");
        if (target.Runtime.ActiveBuffs.Count != 1) return (false, $"expected 1 buff, got {target.Runtime.ActiveBuffs.Count}");
        return Expect(true, "");
    }

    private static (bool, string) Apply_SameSourceId_IncrementsStacks()
    {
        var target = new UnitData();
        // MaxStacks=5 的 test_stackable, 同 source "" 时叠加
        var b1 = BuffSystem.Apply(target, "_test_stackable", source: "same");
        var b2 = BuffSystem.Apply(target, "_test_stackable", source: "same");

        if (b1 == null || b2 == null) return (false, "Apply returned null");
        // 第二次 apply 同源同 ID → 叠层
        if (target.Runtime.ActiveBuffs.Count != 1) return (false, $"expected 1 buff (stacked), got {target.Runtime.ActiveBuffs.Count}");
        if (b2.CurrentStacks != 2) return (false, $"expected stacks=2, got {b2.CurrentStacks}");
        // b1 和 b2 应是同一个实例
        if (!ReferenceEquals(b1, b2)) return (false, "expected same BuffInstance reference");
        return Expect(true, "");
    }

    private static (bool, string) Apply_DifferentSource_SeparateBuffs()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "poison", source: "src_a");
        BuffSystem.Apply(target, "poison", source: "src_b");

        if (target.Runtime.ActiveBuffs.Count != 2) return (false, $"expected 2 separate buffs, got {target.Runtime.ActiveBuffs.Count}");
        return Expect(true, "");
    }

    private static (bool, string) Apply_MaxStacks_NotExceeded()
    {
        var target = new UnitData();
        // 触发同源叠层到 MaxStacks=5
        for (int i = 0; i < 10; i++)
            BuffSystem.Apply(target, "_test_stackable", source: "max_test");

        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == "_test_stackable");
        if (buff == null) return (false, "buff not found");
        if (buff.CurrentStacks > buff.MaxStacks) return (false, $"stacks {buff.CurrentStacks} exceeds MaxStacks {buff.MaxStacks}");
        if (buff.CurrentStacks != 5) return (false, $"expected stacks=5, got {buff.CurrentStacks}");
        return Expect(true, "");
    }

    private static (bool, string) Apply_CancelTags_RemovesConflicting()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "burning");  // CancelTags=["ice"]
        BuffSystem.Apply(target, "frozen");   // CancelTags=["fire"] → 应移除 burning

        // frozen 的 CancelTags=["fire"] 会移除身上的 fire 标签 buff（burning 有 "fire" 标签）
        var burning = target.Runtime.ActiveBuffs.Find(b => b.Id == "burning");
        var frozen = target.Runtime.ActiveBuffs.Find(b => b.Id == "frozen");
        if (burning != null) return (false, "burning should have been cancelled by frozen's CancelTags");
        if (frozen == null) return (false, "frozen should exist");
        return Expect(true, "");
    }

    // ============================================================
    // ApplyDirect
    // ============================================================

    private static (bool, string) ApplyDirect_AddsBuff()
    {
        var target = new UnitData();
        var inst = new BuffInstance { Id = "_direct", Name = "Direct", Duration = 2, MaxStacks = 1 };
        BuffSystem.ApplyDirect(target, inst);

        if (target.Runtime.ActiveBuffs.Count != 1) return (false, $"expected 1 buff, got {target.Runtime.ActiveBuffs.Count}");
        if (target.Runtime.ActiveBuffs[0].Id != "_direct") return (false, $"expected '_direct', got '{target.Runtime.ActiveBuffs[0].Id}'");
        return Expect(true, "");
    }

    private static (bool, string) ApplyDirect_SameSource_Stacks()
    {
        var target = new UnitData();
        var a = new BuffInstance { Id = "_sd", Name = "S", MaxStacks = 5, Source = "s1" };
        var b = new BuffInstance { Id = "_sd", Name = "S", MaxStacks = 5, Source = "s1" };
        BuffSystem.ApplyDirect(target, a);
        BuffSystem.ApplyDirect(target, b);

        if (target.Runtime.ActiveBuffs.Count != 1) return (false, $"expected stacked (1), got {target.Runtime.ActiveBuffs.Count}");
        if (target.Runtime.ActiveBuffs[0].CurrentStacks != 2) return (false, $"expected stacks=2, got {target.Runtime.ActiveBuffs[0].CurrentStacks}");
        return Expect(true, "");
    }

    // ============================================================
    // IncrementStacks
    // ============================================================

    private static (bool, string) IncrementStacks_Normal()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "_test_stackable", source: "inc");
        BuffSystem.IncrementStacks(target, "_test_stackable");

        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == "_test_stackable");
        if (buff == null) return (false, "buff not found");
        if (buff.CurrentStacks != 2) return (false, $"expected stacks=2, got {buff.CurrentStacks}");
        return Expect(true, "");
    }

    private static (bool, string) IncrementStacks_AtMax_NoChange()
    {
        var target = new UnitData();
        // 叠满 5 层
        for (int i = 0; i < 5; i++)
            BuffSystem.Apply(target, "_test_stackable", source: "max_inc");

        BuffSystem.IncrementStacks(target, "_test_stackable");

        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == "_test_stackable");
        if (buff == null) return (false, "buff not found");
        if (buff.CurrentStacks != 5) return (false, $"expected stacks=5 (capped), got {buff.CurrentStacks}");
        return Expect(true, "");
    }

    private static (bool, string) IncrementStacks_NoBuff_NoOp()
    {
        var target = new UnitData();
        // 不应抛出异常
        BuffSystem.IncrementStacks(target, "_nonexistent");
        return Expect(true, "");
    }

    // ============================================================
    // SetStacks
    // ============================================================

    private static (bool, string) SetStacks_Normal()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "_test_stackable", source: "set");
        BuffSystem.SetStacks(target, "_test_stackable", 3);

        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == "_test_stackable");
        if (buff == null) return (false, "buff not found");
        if (buff.CurrentStacks != 3) return (false, $"expected stacks=3, got {buff.CurrentStacks}");
        return Expect(true, "");
    }

    private static (bool, string) SetStacks_ClampLow()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "_test_stackable", source: "clamp_low");
        BuffSystem.SetStacks(target, "_test_stackable", 0);

        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == "_test_stackable");
        if (buff == null) return (false, "buff not found");
        if (buff.CurrentStacks < 1) return (false, $"expected stacks>=1, got {buff.CurrentStacks}");
        return Expect(true, "");
    }

    private static (bool, string) SetStacks_ClampHigh()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "_test_stackable", source: "clamp_high");
        BuffSystem.SetStacks(target, "_test_stackable", 999);

        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == "_test_stackable");
        if (buff == null) return (false, "buff not found");
        if (buff.CurrentStacks > 5) return (false, $"expected stacks<=5, got {buff.CurrentStacks}");
        if (buff.CurrentStacks != 5) return (false, $"expected stacks=5 (max), got {buff.CurrentStacks}");
        return Expect(true, "");
    }

    private static (bool, string) SetStacks_NoBuff_NoOp()
    {
        var target = new UnitData();
        BuffSystem.SetStacks(target, "_nonexistent", 3);
        return Expect(true, "");
    }

    // ============================================================
    // ConsumeModifierStack
    // ============================================================

    private static (bool, string) ConsumeModifierStack_Decrements()
    {
        var target = new UnitData();
        var buff = new BuffInstance
        {
            Id = "_cm", Name = "CM", Duration = 3,
            Modifiers = { new StatModifier { Stat = "charges", Layer = ModifierLayer.Base, Value = 3 } },
        };
        BuffSystem.ApplyDirect(target, buff);

        bool removed = BuffSystem.ConsumeModifierStack(target, buff, "charges");

        if (removed) return (false, "buff should not be removed after 1 consume (3→2)");
        var found = target.Runtime.ActiveBuffs.Find(b => b.Id == "_cm");
        if (found == null) return (false, "buff unexpectedly removed");
        var mod = found.Modifiers.Find(m => m.Stat == "charges");
        if (mod == null) return (false, "modifier not found");
        if (mod.Value != 2f) return (false, $"expected modifier value=2, got {mod.Value}");
        return Expect(true, "");
    }

    private static (bool, string) ConsumeModifierStack_RemovesOnZero()
    {
        var target = new UnitData();
        var buff = new BuffInstance
        {
            Id = "_cmz", Name = "CMZ", Duration = 3,
            Modifiers = { new StatModifier { Stat = "charges", Layer = ModifierLayer.Base, Value = 1 } },
        };
        BuffSystem.ApplyDirect(target, buff);

        bool removed = BuffSystem.ConsumeModifierStack(target, buff, "charges");

        if (!removed) return (false, "buff should be removed when modifier reaches 0");
        var found = target.Runtime.ActiveBuffs.Find(b => b.Id == "_cmz");
        if (found != null) return (false, "buff should have been removed");
        return Expect(true, "");
    }

    private static (bool, string) ConsumeModifierStack_NoModifier_Removes()
    {
        var target = new UnitData();
        var buff = new BuffInstance { Id = "_cnm", Name = "CNM", Duration = 3 };
        BuffSystem.ApplyDirect(target, buff);

        // 没有指定 modifier → 直接移除
        bool removed = BuffSystem.ConsumeModifierStack(target, buff, "nonexistent_stat");

        if (!removed) return (false, "buff should be removed when no matching modifier");
        var found = target.Runtime.ActiveBuffs.Find(b => b.Id == "_cnm");
        if (found != null) return (false, "buff should have been removed");
        return Expect(true, "");
    }

    // ============================================================
    // RemoveBuffInstance
    // ============================================================

    private static (bool, string) RemoveBuffInstance_Success()
    {
        var target = new UnitData();
        var buff = new BuffInstance { Id = "_rbi", Name = "RBI", Duration = 2 };
        BuffSystem.ApplyDirect(target, buff);

        bool result = BuffSystem.RemoveBuffInstance(target, buff);
        if (!result) return (false, "RemoveBuffInstance returned false");
        if (target.Runtime.ActiveBuffs.Count != 0) return (false, $"expected 0 buffs, got {target.Runtime.ActiveBuffs.Count}");
        return Expect(true, "");
    }

    private static (bool, string) RemoveBuffInstance_NotFound()
    {
        var target = new UnitData();
        var buff = new BuffInstance { Id = "_notfound", Name = "NotFound" };
        // 从未添加过
        bool result = BuffSystem.RemoveBuffInstance(target, buff);
        if (result) return (false, "RemoveBuffInstance should return false for non-existent buff");
        return Expect(true, "");
    }

    // ============================================================
    // Remove / RemoveByTag / RemoveAll
    // ============================================================

    private static (bool, string) RemoveById_Removes()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "poison");
        BuffSystem.Apply(target, "bless");

        bool removed = BuffSystem.Remove(target, "poison");
        if (!removed) return (false, "Remove returned false");
        if (target.Runtime.ActiveBuffs.Count != 1) return (false, $"expected 1 buff remaining, got {target.Runtime.ActiveBuffs.Count}");
        if (target.Runtime.ActiveBuffs[0].Id != "bless") return (false, "expected 'bless' to remain");
        return Expect(true, "");
    }

    private static (bool, string) RemoveByTag_RemovesAll()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "poison");  // Tags=["dot","poison"]
        BuffSystem.Apply(target, "bleed");   // Tags=["dot","bleed"]
        BuffSystem.Apply(target, "bless");   // Tags=["buff","holy"]

        BuffSystem.RemoveByTag(target, "dot");
        if (target.Runtime.ActiveBuffs.Count != 1) return (false, $"expected 1 buff remaining, got {target.Runtime.ActiveBuffs.Count}");
        if (target.Runtime.ActiveBuffs[0].Id != "bless") return (false, "expected 'bless' to remain");
        return Expect(true, "");
    }

    private static (bool, string) RemoveAll_ClearsAll()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "poison");
        BuffSystem.Apply(target, "bless");
        BuffSystem.Apply(target, "haste");

        BuffSystem.RemoveAll(target);
        if (target.Runtime.ActiveBuffs.Count != 0) return (false, $"expected 0 buffs, got {target.Runtime.ActiveBuffs.Count}");
        return Expect(true, "");
    }

    // ============================================================
    // Query: HasBuff / HasTag / GetStacks
    // ============================================================

    private static (bool, string) HasBuff_Has_True()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "shield");
        if (!BuffSystem.HasBuff(target, "shield")) return (false, "HasBuff('shield') should be true");
        return Expect(true, "");
    }

    private static (bool, string) HasBuff_No_False()
    {
        var target = new UnitData();
        if (BuffSystem.HasBuff(target, "nonexistent")) return (false, "HasBuff('nonexistent') should be false");
        return Expect(true, "");
    }

    private static (bool, string) HasTag_Has_True()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "bless");  // Tags=["buff","holy"]
        if (!BuffSystem.HasTag(target, "holy")) return (false, "HasTag('holy') should be true");
        return Expect(true, "");
    }

    private static (bool, string) HasTag_No_False()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "bless");  // Tags=["buff","holy"]
        if (BuffSystem.HasTag(target, "ice")) return (false, "HasTag('ice') should be false");
        return Expect(true, "");
    }

    private static (bool, string) GetStacks_ReturnsValue()
    {
        var target = new UnitData();
        BuffSystem.Apply(target, "_test_stackable", source: "stacks_test");  // stacks=1 (default)

        int stacks = BuffSystem.GetStacks(target, "_test_stackable");
        if (stacks != 1) return (false, $"expected stacks=1, got {stacks}");
        return Expect(true, "");
    }

    private static (bool, string) GetStacks_NoBuff_Zero()
    {
        var target = new UnitData();
        int stacks = BuffSystem.GetStacks(target, "_nonexistent");
        if (stacks != 0) return (false, $"expected stacks=0, got {stacks}");
        return Expect(true, "");
    }

    // ============================================================
    // ResolveStatModifiers
    // ============================================================

    private static (bool, string) ResolveStatModifiers_Sum()
    {
        var target = new UnitData();
        // bless: attack_bonus=+2 (Base)  — 同层同源取最高
        // Apply 两次: 同源同 ID 会叠层，但 bless 的 MaxStacks=1，所以只叠层(CurrentStacks 保持 1)
        var blessing = BuffSystem.Apply(target, "bless");  // +2 atk, +1 ac
        if (blessing == null) return (false, "bless apply failed");

        var result = BuffSystem.ResolveStatModifiers(target, "attack_bonus");
        if (System.Math.Abs(result.FlatBonus - 2f) > 0.001f)
            return (false, $"expected FlatBonus~2, got {result.FlatBonus}");

        // 检查 AC 修正
        var acResult = BuffSystem.ResolveStatModifiers(target, "ac");
        if (System.Math.Abs(acResult.FlatBonus - 1f) > 0.001f)
            return (false, $"expected ac FlatBonus~1, got {acResult.FlatBonus}");

        return Expect(true, "");
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static (string name, bool ok, string msg) Run(string name, System.Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (System.Exception ex)
        {
            return (name, false, $"Exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static (bool, string) Expect(bool condition, string failureMsg)
        => condition ? (true, "") : (false, failureMsg);
}
