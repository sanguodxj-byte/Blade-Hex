// QuestData.cs
// 委托/任务数据定义
// 根据策划案 01-世界观.md 的委托系统设计
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class QuestData : Resource
{
    // ========================================
    // 枚举
    // ========================================

    public enum QuestType
    {
        Extermination, // 讨伐型
        Escort,        // 护送型
        Exploration,   // 探索型
        Defense,       // 防御型
        Emergency,     // 紧急型
    }

    public enum QuestStatus
    {
        Available, // 可接取
        Active,    // 进行中
        Completed, // 已完成
        Failed,    // 失败
        Expired,   // 过期
    }

    public enum QuestDifficulty
    {
        Easy,   // 简单
        Medium, // 中等
        Hard,   // 困难
        Boss,   // BOSS级
    }

    // ========================================
    // 基础信息
    // ========================================

    [Export] public string QuestId { get; set; } = "";
    [Export] public string QuestName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public QuestType questType = QuestType.Extermination;
    [Export] public QuestDifficulty difficulty = QuestDifficulty.Easy;

    // 兼容别名（迁移期间使用）
    public string Title => QuestName;
    public QuestType Type => questType;
    public int RecommendedLevel => (int)difficulty + 1;
    public string Objectives => TargetDescription;

    // 发布信息
    [Export] public string IssuerName { get; set; } = "";
    [Export] public Vector2I IssuerLocation;

    // 目标信息
    [Export] public Vector2I TargetLocation;
    [Export] public Vector2 TargetWorldPosition { get; set; } = Vector2.Zero;
    [Export] public string TargetDescription { get; set; } = "";
    [Export] public int TargetCount { get; set; } = 1;

    // 奖励
    [Export] public int RewardGold;
    [Export] public string[] RewardItems = [];
    [Export] public int RewardReputation;
    [Export] public string RewardFaction { get; set; } = "";

    // 时间限制
    [Export] public bool HasTimeLimit;
    [Export] public int TimeLimitDays;

    // 前置条件
    [Export] public int RequiredReputation;
    [Export] public string[] RequiredQuests = [];

    // ========================================
    // 运行时状态（不序列化）
    // ========================================

    public QuestStatus Status = QuestStatus.Available;
    public int Progress;
    public float AcceptedTime;
    public float CompletionTime;

    // ========================================
    // 构造
    // ========================================

    public QuestData()
    {
        ResourceName = "QuestData";
    }

    /// <summary>检查是否可接取</summary>
    public bool CanAccept(int playerReputation, string[] completedQuests)
    {
        if (playerReputation < RequiredReputation) return false;
        foreach (var req in RequiredQuests)
        {
            bool found = false;
            foreach (var done in completedQuests)
                if (done == req) { found = true; break; }
            if (!found) return false;
        }
        return Status == QuestStatus.Available;
    }

    /// <summary>接取任务</summary>
    public void Accept(float currentGameTime)
    {
        Status = QuestStatus.Active;
        Progress = 0;
        AcceptedTime = currentGameTime;
    }

    /// <summary>更新进度</summary>
    public void UpdateProgress(int amount)
    {
        Progress = Mathf.Min(Progress + amount, TargetCount);
        if (Progress >= TargetCount)
            Status = QuestStatus.Completed;
    }

    /// <summary>检查是否过期</summary>
    public bool CheckExpiration(float currentGameTime)
    {
        if (!HasTimeLimit || Status != QuestStatus.Active) return false;
        float elapsedDays = (currentGameTime - AcceptedTime) / 86400.0f;
        if (elapsedDays > TimeLimitDays)
        {
            Status = QuestStatus.Expired;
            return true;
        }
        return false;
    }

    /// <summary>获取剩余时间（天数）</summary>
    public float GetRemainingDays(float currentGameTime)
    {
        if (!HasTimeLimit) return -1.0f;
        float elapsedDays = (currentGameTime - AcceptedTime) / 86400.0f;
        return Mathf.Max(0.0f, TimeLimitDays - elapsedDays);
    }

    public string GetDifficultyText() => difficulty switch
    {
        QuestDifficulty.Easy => "简单",
        QuestDifficulty.Medium => "中等",
        QuestDifficulty.Hard => "困难",
        QuestDifficulty.Boss => "BOSS级",
        _ => "未知",
    };

    public string GetTypeText() => questType switch
    {
        QuestType.Extermination => "讨伐",
        QuestType.Escort => "护送",
        QuestType.Exploration => "探索",
        QuestType.Defense => "防御",
        QuestType.Emergency => "紧急",
        _ => "未知",
    };

    public string GetProgressText() => $"{Progress} / {TargetCount}";

    /// <summary>创建副本（用于实例化）</summary>
    public QuestData DuplicateQuest()
    {
        var q = new QuestData();
        q.QuestId = QuestId;
        q.QuestName = QuestName;
        q.Description = Description;
        q.questType = questType;
        q.difficulty = difficulty;
        q.IssuerName = IssuerName;
        q.IssuerLocation = IssuerLocation;
        q.TargetLocation = TargetLocation;
        q.TargetWorldPosition = TargetWorldPosition;
        q.TargetDescription = TargetDescription;
        q.TargetCount = TargetCount;
        q.RewardGold = RewardGold;
        q.RewardItems = (string[])RewardItems.Clone();
        q.RewardReputation = RewardReputation;
        q.RewardFaction = RewardFaction;
        q.HasTimeLimit = HasTimeLimit;
        q.TimeLimitDays = TimeLimitDays;
        q.RequiredReputation = RequiredReputation;
        q.RequiredQuests = (string[])RequiredQuests.Clone();
        return q;
    }
}
