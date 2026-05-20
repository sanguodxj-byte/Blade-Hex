// QuestAcceptanceService.cs
// 委托接取编排服务：保证任务板池移除与任务管理器登记尽量保持原子性。
using System;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 任务接取结果。
/// </summary>
public sealed class QuestAcceptanceResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public QuestData? Quest { get; init; }

    public static QuestAcceptanceResult Ok(QuestData quest, string message = "任务接取成功。") => new()
    {
        Success = true,
        Message = message,
        Quest = quest,
    };

    public static QuestAcceptanceResult Fail(string message, QuestData? quest = null) => new()
    {
        Success = false,
        Message = message,
        Quest = quest,
    };
}

/// <summary>
/// 任务板接取服务。
///
/// <para>
/// 旧流程是 <c>QuestGenerator.AcceptQuest()</c> 先从 POI 任务池移除，再调用
/// <c>QuestManager.AcceptQuest()</c> 登记到 ActiveQuests；如果后者失败，任务会从任务板消失。
/// 本服务反转顺序：先让登记方确认可接取，成功后才从当前 POI 池移除。
/// </para>
///
/// <para>
/// 为保持 Core/Frontend 解耦，登记方通过委托传入。Frontend 可传入
/// <c>questManager.AcceptQuest</c>，测试或兼容路径可传入 null。
/// </para>
/// </summary>
public static class QuestAcceptanceService
{
    public static QuestAcceptanceResult AcceptFromBoard(
        QuestGenerator? questGenerator,
        string poiId,
        int questIndex,
        int currentDay,
        Func<QuestData, bool>? registerQuest)
    {
        if (questGenerator == null)
            return QuestAcceptanceResult.Fail("任务生成器不可用。");

        var quests = questGenerator.GetAvailableQuests(poiId, currentDay);
        if (questIndex < 0 || questIndex >= quests.Count)
            return QuestAcceptanceResult.Fail("接取失败：委托已刷新或不存在。");

        var quest = quests[questIndex];
        var previousStatus = quest.Status;
        var previousProgress = quest.Progress;
        var previousAcceptedTime = quest.AcceptedTime;

        bool registered;
        if (registerQuest != null)
        {
            registered = registerQuest(quest);
        }
        else
        {
            quest.Accept(currentDay);
            registered = true;
        }

        if (!registered)
        {
            quest.Status = previousStatus;
            quest.Progress = previousProgress;
            quest.AcceptedTime = previousAcceptedTime;
            return QuestAcceptanceResult.Fail("接取失败：任务管理器拒绝了该委托。", quest);
        }

        bool removed = questIndex < quests.Count && ReferenceEquals(quests[questIndex], quest)
            ? RemoveAt(quests, questIndex)
            : quests.Remove(quest);

        if (!removed)
        {
            // 正常 UI 单线程流程不应发生；若发生，至少让调用方看到明确错误，避免静默重复接取。
            return QuestAcceptanceResult.Fail("接取失败：无法从任务板移除该委托。", quest);
        }

        return QuestAcceptanceResult.Ok(quest, $"已接取: {quest.QuestName}");
    }

    private static bool RemoveAt(System.Collections.Generic.List<QuestData> quests, int index)
    {
        quests.RemoveAt(index);
        return true;
    }
}
