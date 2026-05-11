using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 委托管理器 — 管理所有任务的状态、进度和奖励发放
/// </summary>
[GlobalClass]
public partial class QuestManager : Node
{
    // ========================================
    // 信号
    // ========================================

    [Signal] public delegate void QuestAcceptedEventHandler(QuestData quest);
    [Signal] public delegate void QuestCompletedEventHandler(QuestData quest);
    [Signal] public delegate void QuestFailedEventHandler(QuestData quest);
    [Signal] public delegate void QuestExpiredEventHandler(QuestData quest);
    [Signal] public delegate void QuestProgressUpdatedEventHandler(QuestData quest, int progress);

    [Signal] public delegate void QuestTargetSpawnedEventHandler(QuestTargetSite targetSite);
    [Signal] public delegate void QuestTargetClearedEventHandler(string questId);

    // ========================================
    // 数据字段
    // ========================================

    public Dictionary<string, QuestData> QuestTemplates = new();
    public List<QuestData> AvailableQuests = new();
    private static readonly Random _random = new();

    public static QuestManager Instance { get; private set; } = new();
    public List<QuestData> ActiveQuests = new();
    public List<string> CompletedQuestIds = new();
    public Dictionary<string, QuestTargetSite> ActiveTargetSites = new();

    public float GameTime = 0.0f;
    public int PlayerReputation = 0;
    public int PlayerGold = 0;

    private Timer? _expirationCheckTimer;

    // ========================================
    // 初始化
    // ========================================

    public override void _Ready()
    {
        LoadQuestTemplates();
        GenerateInitialQuests();

        _expirationCheckTimer = new Timer();
        _expirationCheckTimer.WaitTime = 5.0f;
        _expirationCheckTimer.Autostart = true;
        _expirationCheckTimer.Timeout += CheckQuestExpiration;
        AddChild(_expirationCheckTimer);
    }

    public override void _Process(double delta)
    {
        GameTime += (float)delta;
    }

    private void LoadQuestTemplates()
    {
        // TODO: 从资源文件夹加载
        CreateSampleQuests();
    }

    private void CreateSampleQuests()
    {
        // 哥布林讨伐
        var q1 = new QuestData
        {
            QuestId = "goblin_extermination_01",
            QuestName = "清除哥布林",
            Description = "村庄附近的哥布林营地威胁到了村民的安全，需要清除至少8只哥布林。",
            questType = QuestData.QuestType.Extermination,
            difficulty = QuestData.QuestDifficulty.Easy,
            IssuerName = "绿谷村",
            IssuerLocation = new Vector2I(2800, 2200),
            TargetDescription = "哥布林营地",
            TargetCount = 8,
            RewardGold = 150,
            RewardReputation = 5,
            RewardFaction = "绿谷村"
        };
        QuestTemplates[q1.QuestId] = q1;

        // 护送商队
        var q2 = new QuestData
        {
            QuestId = "escort_caravan_01",
            QuestName = "护送商队",
            Description = "护送商队从绿谷村前往银月城，路上可能遭遇强盗。",
            questType = QuestData.QuestType.Escort,
            difficulty = QuestData.QuestDifficulty.Medium,
            IssuerName = "商人吉尔伯特",
            IssuerLocation = new Vector2I(2600, 2000),
            TargetDescription = "银月城",
            TargetCount = 1,
            RewardGold = 300,
            RewardReputation = 10,
            HasTimeLimit = true,
            TimeLimitDays = 5
        };
        QuestTemplates[q2.QuestId] = q2;
    }

    private void GenerateInitialQuests()
    {
        foreach (var template in QuestTemplates.Values)
        {
            var q = (QuestData)template.Duplicate();
            AvailableQuests.Add(q);
        }
    }

    // ========================================
    // 任务操作
    // ========================================

    public bool AcceptQuest(QuestData quest)
    {
        // 这里需要 QuestData 的 can_accept 逻辑，如果 C# QuestData 没写，暂时返回 true
        // if (!quest.CanAccept(PlayerReputation, CompletedQuestIds)) return false;

        AvailableQuests.Remove(quest);
        // quest.Accept(GameTime); // 同理，如果 QuestData 没写这个方法，手动设置
        quest.Status = QuestData.QuestStatus.Active;
        quest.AcceptedTime = GameTime;

        ActiveQuests.Add(quest);
        SpawnQuestTarget(quest);

        EmitSignal(SignalName.QuestAccepted, quest);
        return true;
    }

    public void UpdateQuestProgress(string questId, int amount = 1)
    {
        var quest = ActiveQuests.FirstOrDefault(q => q.QuestId == questId);
        if (quest != null)
        {
            quest.Progress += amount;
            EmitSignal(SignalName.QuestProgressUpdated, quest, quest.Progress);

            if (quest.Progress >= quest.TargetCount)
            {
                quest.Status = QuestData.QuestStatus.Completed;
                CompleteQuest(quest);
            }
        }
    }

    private void CompleteQuest(QuestData quest)
    {
        quest.CompletionTime = GameTime;
        GrantRewards(quest);
        ClearQuestTarget(quest.QuestId);
        ActiveQuests.Remove(quest);
        CompletedQuestIds.Add(quest.QuestId);
        EmitSignal(SignalName.QuestCompleted, quest);
    }

    public void FailQuest(QuestData quest)
    {
        quest.Status = QuestData.QuestStatus.Failed;
        ClearQuestTarget(quest.QuestId);
        ActiveQuests.Remove(quest);
        EmitSignal(SignalName.QuestFailed, quest);
    }

    private void CheckQuestExpiration()
    {
        var expired = ActiveQuests.Where(q => q.HasTimeLimit && (GameTime - q.AcceptedTime) > (q.TimeLimitDays * 86400.0f)).ToList();
        foreach (var q in expired)
        {
            ActiveQuests.Remove(q);
            ClearQuestTarget(q.QuestId);
            EmitSignal(SignalName.QuestExpired, q);
        }
    }

    private void GrantRewards(QuestData quest)
    {
        PlayerGold += quest.RewardGold;
        PlayerReputation += quest.RewardReputation;
        // TODO: Items
        GD.Print($"[Quest] {quest.QuestName} 完成: +{quest.RewardGold}金, +{quest.RewardReputation}名望");
    }

    // ========================================
    // 目标点管理
    // ========================================

    private void SpawnQuestTarget(QuestData quest)
    {
        if (quest.TargetWorldPosition == Vector2.Zero)
            quest.TargetWorldPosition = GenerateTargetPosition(quest);

        var site = QuestTargetSite.CreateFromQuest(quest);
        ActiveTargetSites[quest.QuestId] = site;
        EmitSignal(SignalName.QuestTargetSpawned, site);
    }

    private void ClearQuestTarget(string questId)
    {
        if (ActiveTargetSites.Remove(questId))
            EmitSignal(SignalName.QuestTargetCleared, questId);
    }

    private Vector2 GenerateTargetPosition(QuestData quest)
    {
        Vector2 issuerPx = new(quest.IssuerLocation.X, quest.IssuerLocation.Y);
        if (issuerPx == Vector2.Zero) issuerPx = new Vector2(3072.0f, 2048.0f);

        float minDist = 200.0f;
        float maxDist = 600.0f;
        // ... (Similar distance logic as GDScript)

        for (int i = 0; i < 30; i++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float d = (float)(_random.NextDouble() * (maxDist - minDist) + minDist);
            Vector2 cand = issuerPx + new Vector2(Mathf.Cos(angle) * d, Mathf.Sin(angle) * d);
            if (cand.X > 80 && cand.X < 6064 && cand.Y > 80 && cand.Y < 4016) return cand;
        }
        return issuerPx + new Vector2((float)(_random.NextDouble() * 200 - 100), (float)(_random.NextDouble() * 200 - 100));
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var activeArr = new Godot.Collections.Array();
        foreach (var q in ActiveQuests)
            activeArr.Add(new Godot.Collections.Dictionary { { "id", q.QuestId }, { "progress", q.Progress }, { "accepted", q.AcceptedTime } });

        var targetSitesData = new Godot.Collections.Dictionary();
        foreach (var kvp in ActiveTargetSites) targetSitesData[kvp.Key] = kvp.Value.Serialize();

        return new Godot.Collections.Dictionary
        {
            { "game_time", GameTime },
            { "reputation", PlayerReputation },
            { "gold", PlayerGold },
            { "completed", new Godot.Collections.Array<string>(CompletedQuestIds) },
            { "active", activeArr },
            { "available", new Godot.Collections.Array<string>(AvailableQuests.Select(q => q.QuestId)) },
            { "targets", targetSitesData }
        };
    }
}
