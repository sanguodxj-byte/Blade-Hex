// QuestAcceptanceServiceTests.cs
// 任务接取编排服务测试：验证任务板移除与登记失败回滚。
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Data;

namespace BladeHex.Strategic.Tests;

public static class QuestAcceptanceServiceTests
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
        yield return Run(nameof(AcceptFromBoard_WhenRegisterFails_DoesNotRemoveFromPool), AcceptFromBoard_WhenRegisterFails_DoesNotRemoveFromPool);
        yield return Run(nameof(AcceptFromBoard_WhenRegisterSucceeds_RemovesFromPool), AcceptFromBoard_WhenRegisterSucceeds_RemovesFromPool);
        yield return Run(nameof(AcceptFromBoard_InvalidIndex_DoesNotCallRegister), AcceptFromBoard_InvalidIndex_DoesNotCallRegister);
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
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    private static (bool, string) AcceptFromBoard_WhenRegisterFails_DoesNotRemoveFromPool()
    {
        var gen = MakeGenerator();
        var before = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (before.Count == 0) return (false, "pool initially empty");
        var quest = before[0];
        int countBefore = before.Count;
        bool registerCalled = false;

        var result = QuestAcceptanceService.AcceptFromBoard(
            gen,
            "Town_A",
            0,
            5,
            q =>
            {
                registerCalled = true;
                q.Status = QuestData.QuestStatus.Active;
                q.Progress = 3;
                q.AcceptedTime = 123;
                return false;
            });

        var after = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (result.Success) return (false, "result should fail");
        if (!registerCalled) return (false, "register delegate was not called");
        if (after.Count != countBefore) return (false, $"pool size changed: {countBefore} -> {after.Count}");
        if (!ReferenceEquals(after[0], quest)) return (false, "original quest is no longer at pool index 0");
        if (quest.Status != QuestData.QuestStatus.Available) return (false, $"status was not restored: {quest.Status}");
        if (quest.Progress != 0 || Math.Abs(quest.AcceptedTime) > 0.001f)
            return (false, $"runtime fields were not restored: progress={quest.Progress}, accepted={quest.AcceptedTime}");
        return (true, "");
    }

    private static (bool, string) AcceptFromBoard_WhenRegisterSucceeds_RemovesFromPool()
    {
        var gen = MakeGenerator();
        var before = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (before.Count == 0) return (false, "pool initially empty");
        var quest = before[0];
        int countBefore = before.Count;
        QuestData? registered = null;

        var result = QuestAcceptanceService.AcceptFromBoard(
            gen,
            "Town_A",
            0,
            5,
            q =>
            {
                registered = q;
                q.Accept(5);
                return true;
            });

        var after = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (!result.Success) return (false, result.Message);
        if (!ReferenceEquals(result.Quest, quest)) return (false, "result quest mismatch");
        if (!ReferenceEquals(registered, quest)) return (false, "registered quest mismatch");
        if (after.Count != countBefore - 1) return (false, $"pool size: expected {countBefore - 1}, got {after.Count}");
        if (after.Contains(quest)) return (false, "accepted quest still exists in pool");
        if (quest.Status != QuestData.QuestStatus.Active) return (false, $"expected active, got {quest.Status}");
        return (true, "");
    }

    private static (bool, string) AcceptFromBoard_InvalidIndex_DoesNotCallRegister()
    {
        var gen = MakeGenerator();
        var before = gen.GetAvailableQuests("Town_A", currentDay: 5);
        int countBefore = before.Count;
        bool registerCalled = false;

        var result = QuestAcceptanceService.AcceptFromBoard(gen, "Town_A", 999, 5, _ =>
        {
            registerCalled = true;
            return true;
        });

        var after = gen.GetAvailableQuests("Town_A", currentDay: 5);
        if (result.Success) return (false, "result should fail");
        if (registerCalled) return (false, "register should not be called for invalid index");
        if (after.Count != countBefore) return (false, $"pool size changed: {countBefore} -> {after.Count}");
        return (true, "");
    }

    private static QuestGenerator MakeGenerator()
    {
        var gen = new QuestGenerator();
        gen.Initialize(new List<OverworldPOI>
        {
            new()
            {
                PoiName = "Town_A",
                Position = new Vector2(100, 100),
                PoiTypeEnum = OverworldPOI.POIType.Town,
                OwningFaction = "neutral",
            },
            new()
            {
                PoiName = "Town_B",
                Position = new Vector2(500, 500),
                PoiTypeEnum = OverworldPOI.POIType.Town,
                OwningFaction = "neutral",
            },
        }, worldSeed: 42);
        return gen;
    }
}
