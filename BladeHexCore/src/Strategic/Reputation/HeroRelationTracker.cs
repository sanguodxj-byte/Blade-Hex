// HeroRelationTracker.cs
// 英雄关系追踪器 — 个人关系层（−100..+100）
// T14: Faction reputation hero layer
using System.Collections.Generic;
using Godot;

namespace BladeHex.Strategic.Reputation;

/// <summary>
/// 英雄关系追踪器 — 管理玩家与具名英雄的个人关系
/// 最终态度 = clamp(faction + hero, -100, +100)
/// </summary>
public partial class HeroRelationTracker
{
    /// <summary>heroId → 个人关系值 (-100 ~ +100)</summary>
    private readonly Dictionary<string, int> _heroRelations = new();

    private const int MinRelation = -100;
    private const int MaxRelation = 100;

    /// <summary>获取英雄个人关系</summary>
    public int GetRelation(string heroId)
    {
        return _heroRelations.GetValueOrDefault(heroId, 0);
    }

    /// <summary>设置英雄个人关系</summary>
    public void SetRelation(string heroId, int value)
    {
        _heroRelations[heroId] = Mathf.Clamp(value, MinRelation, MaxRelation);
    }

    /// <summary>修改英雄个人关系</summary>
    public void AddRelation(string heroId, int delta)
    {
        int current = GetRelation(heroId);
        SetRelation(heroId, current + delta);
    }

    /// <summary>获取有效态度（派系 + 英雄个人）</summary>
    public int GetEffectiveAttitude(string factionId, string heroId, ReputationTracker factionTracker)
    {
        int factionRep = factionTracker.GetReputation(factionId);
        int heroRel = GetRelation(heroId);
        return Mathf.Clamp(factionRep + heroRel, MinRelation, MaxRelation);
    }

    /// <summary>获取所有英雄关系数据（用于保存）</summary>
    public Dictionary<string, int> GetAllData()
    {
        return new Dictionary<string, int>(_heroRelations);
    }

    /// <summary>加载英雄关系数据（用于读档）</summary>
    public void LoadData(Dictionary<string, int> data)
    {
        _heroRelations.Clear();
        foreach (var kv in data)
            _heroRelations[kv.Key] = Mathf.Clamp(kv.Value, MinRelation, MaxRelation);
    }

    // ========================================================================
    // 关系事件
    // ========================================================================

    /// <summary>完成具名英雄相关任务</summary>
    public void OnQuestCompletedForHero(string heroId, int reward = 5)
    {
        AddRelation(heroId, reward);
    }

    /// <summary>救援具名英雄</summary>
    public void OnRescuedHero(string heroId)
    {
        AddRelation(heroId, 20);
    }

    /// <summary>决斗胜利</summary>
    public void OnDuelVictory(string heroId)
    {
        AddRelation(heroId, 10);
    }

    /// <summary>决斗失败</summary>
    public void OnDuelDefeat(string heroId)
    {
        AddRelation(heroId, -5);
    }

    /// <summary>杀死具名英雄的同伴</summary>
    public void OnKilledHeroCompanion(string heroId)
    {
        AddRelation(heroId, -15);
    }
}
