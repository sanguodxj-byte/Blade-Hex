using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// [Scene Service] 委托管理器 — 管理所有任务的状态、进度和奖励发放。
///
/// <para>所属场景：<see cref="BladeHex.View.OverworldScene3D"/>（在 OverworldScene3D.Entities.cs 中由父场景创建并 AddChild）。</para>
/// <para>生命周期：随 Overworld 场景创建与销毁。</para>
/// <para>访问方式：父场景持有引用；CombatScene 通过 <c>GetParent().GetNodeOrNull("QuestManager")</c> 跨场景查找。</para>
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

    public List<QuestData> ActiveQuests = new();
    public List<QuestData> RewardReadyQuests = new();
    public List<string> CompletedQuestIds = new();
    public Dictionary<string, QuestTargetSite> ActiveTargetSites = new();
    private readonly Dictionary<string, (QuestData Quest, int FailedDay)> _pendingFailedQuests = new();

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

        // 订阅 POI 易手事件，处理任务的敌方夺取中断与夺回恢复
        PoiTransferService.PoiTransferred += OnPoiTransferred;
    }

    public override void _Process(double delta)
    {
        GameTime += (float)delta;
    }

    private void LoadQuestTemplates()
    {
        // 当前使用硬编码样例任务（适合原型阶段）。
        // 后续可改为从 res://data/quests/ 文件夹扫描 .tres 资源加载。
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
        if (ActiveQuests.Any(q => q.QuestId == quest.QuestId) ||
            RewardReadyQuests.Any(q => q.QuestId == quest.QuestId) ||
            CompletedQuestIds.Contains(quest.QuestId))
        {
            return false;
        }

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
                MarkQuestReadyForReward(quest);
            }
        }
    }

    private void MarkQuestReadyForReward(QuestData quest)
    {
        quest.CompletionTime = GameTime;
        ClearQuestTarget(quest.QuestId);
        ActiveQuests.Remove(quest);
        if (!RewardReadyQuests.Any(q => q.QuestId == quest.QuestId))
            RewardReadyQuests.Add(quest);
        EmitSignal(SignalName.QuestCompleted, quest);
    }

    public List<QuestData> GetRewardReadyQuestsForPoi(string poiId)
    {
        return RewardReadyQuests
            .Where(q => q.IssuerName == poiId)
            .ToList();
    }

    public QuestData? GetActiveQuest(string questId)
    {
        return ActiveQuests.FirstOrDefault(q => q.QuestId == questId);
    }

    public QuestTargetSite? GetActiveTargetSite(string questId)
    {
        return ActiveTargetSites.TryGetValue(questId, out var site) ? site : null;
    }

    public bool ClaimReward(string questId)
    {
        var quest = RewardReadyQuests.FirstOrDefault(q => q.QuestId == questId);
        if (quest == null) return false;

        GrantRewards(quest);
        RewardReadyQuests.Remove(quest);
        if (!CompletedQuestIds.Contains(quest.QuestId))
            CompletedQuestIds.Add(quest.QuestId);
        return true;
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

        CheckPendingFailedQuests();
    }

    private void CheckPendingFailedQuests()
    {
        if (GetParent() is Scenes.Overworld.IOverworldContext ctx)
        {
            int currentDay = ctx.CurrentDay;
            var expiredIds = new List<string>();

            foreach (var kvp in _pendingFailedQuests)
            {
                if (currentDay - kvp.Value.FailedDay >= 3)
                {
                    expiredIds.Add(kvp.Key);
                }
            }

            foreach (string qid in expiredIds)
            {
                var val = _pendingFailedQuests[qid];
                _pendingFailedQuests.Remove(qid);
                FailQuest(val.Quest);
                GD.Print($"[Quest] 超过3天未收复聚落，任务永久失败: {val.Quest.QuestName}");
            }
        }
    }

    private void OnPoiTransferred(PoiTransferEvent evt)
    {
        if (evt?.Poi == null) return;

        string? playerFaction = GetPlayerFaction();
        if (string.IsNullOrEmpty(playerFaction)) return;

        // 如果该聚落原属于玩家阵营，但被非玩家阵营夺取
        if (evt.OldFaction == playerFaction && evt.NewFaction != playerFaction)
        {
            var toHang = ActiveQuests.Where(q => q.IssuerName == evt.Poi.PoiName).ToList();
            foreach (var q in toHang)
            {
                ActiveQuests.Remove(q);
                q.Status = QuestData.QuestStatus.Failed; // 设为临时挂起
                _pendingFailedQuests[q.QuestId] = (q, evt.Day);
                GD.Print($"[Quest] 聚落被夺，任务挂起失败（宽限期3天）: {q.QuestName}");
            }
        }
        // 如果该聚落重新被玩家阵营收复
        else if (evt.NewFaction == playerFaction)
        {
            var toRestore = _pendingFailedQuests.Where(kvp => kvp.Value.Quest.IssuerName == evt.Poi.PoiName).ToList();
            foreach (var kvp in toRestore)
            {
                _pendingFailedQuests.Remove(kvp.Key);
                kvp.Value.Quest.Status = QuestData.QuestStatus.Active;
                ActiveQuests.Add(kvp.Value.Quest);
                GD.Print($"[Quest] 收复聚落，任务重新恢复: {kvp.Value.Quest.QuestName}");
            }
        }
    }

    private string? GetPlayerFaction()
    {
        if (GetParent() is Scenes.Overworld.IOverworldContext ctx && ctx.ReputationTracker != null)
        {
            return new PlayerNationResolver().GetCurrent(ctx.ReputationTracker, ctx.CurrentDay);
        }
        return null;
    }

    private void GrantRewards(QuestData quest)
    {
        PlayerGold += quest.RewardGold;
        PlayerReputation += quest.RewardReputation;

        if (GetParent() is Scenes.Overworld.IOverworldContext ctx)
        {
            if (quest.RewardGold != 0)
                ctx.AddGold(quest.RewardGold);

            string faction = !string.IsNullOrEmpty(quest.RewardFaction)
                ? quest.RewardFaction
                : GetPlayerFaction() ?? "";
            if (!string.IsNullOrEmpty(faction) && ctx.ReputationTracker != null)
                ctx.ReputationTracker.OnQuestCompleted(faction, quest.RewardReputation);
        }

        // 发放物品奖励到队伍背包
        if (quest.RewardItems != null && quest.RewardItems.Length > 0)
        {
            var party = (GetParent() as Scenes.Overworld.IOverworldContext)?.PlayerParty;
            if (party != null)
            {
                foreach (var itemName in quest.RewardItems)
                {
                    if (string.IsNullOrEmpty(itemName)) continue;
                    var entry = new LootEntry(itemName, LootEntry.LootType.Material, 1, 0, "委托奖励");
                    party.Inventory.Add(entry);
                    GD.Print($"[Quest] 获得物品: {itemName}");
                }
            }
        }

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
        // ...

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
            { "reward_ready", new Godot.Collections.Array<string>(RewardReadyQuests.Select(q => q.QuestId)) },
            { "active", activeArr },
            { "available", new Godot.Collections.Array<string>(AvailableQuests.Select(q => q.QuestId)) },
            { "targets", targetSitesData }
        };
    }
}
