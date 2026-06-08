// ReputationTracker.cs
// 声望系统 — 追踪玩家与各国家/势力的关系
//
// 设计（对应 docs/18-声望系统.md）：
// - 每个国家独立声望值 -100 ~ +100
// - 声望影响：城镇准入、招募池、商店价格、任务解锁
// - 声望来源：完成委托、击败敌人、攻击友方、抢劫商队
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 声望等级
/// </summary>
public enum ReputationLevel
{
    Hated,      // -100 ~ -75: 通缉，城门拒绝，守军攻击
    Hostile,    // -74 ~ -50: 敌对，城门拒绝
    Unfriendly, // -49 ~ -25: 不友好，招募减半，涨价 20%
    Neutral,    // -24 ~ +24: 中立
    Friendly,   // +25 ~ +49: 友好，折扣 10%
    Honored,    // +50 ~ +74: 尊敬，解锁高级招募
    Exalted,    // +75 ~ +100: 崇拜，解锁领主任务，折扣 20%
}

/// <summary>
/// 声望追踪器 — 管理玩家与所有势力的关系
/// </summary>
[GlobalClass]
public partial class ReputationTracker : Resource
{
    /// <summary>nationId → reputation value (-100 ~ +100)</summary>
    private readonly Dictionary<string, int> _reputation = new();

    // ========================================
    // 查询
    // ========================================

    /// <summary>获取对某势力的声望值</summary>
    public int GetReputation(string nationId)
    {
        return _reputation.TryGetValue(nationId, out int val) ? val : 0;
    }

    /// <summary>获取声望等级</summary>
    public ReputationLevel GetLevel(string nationId)
    {
        int rep = GetReputation(nationId);
        return rep switch
        {
            <= -75 => ReputationLevel.Hated,
            <= -50 => ReputationLevel.Hostile,
            <= -25 => ReputationLevel.Unfriendly,
            <= 24 => ReputationLevel.Neutral,
            <= 49 => ReputationLevel.Friendly,
            <= 74 => ReputationLevel.Honored,
            _ => ReputationLevel.Exalted,
        };
    }

    /// <summary>声望等级中文名</summary>
    public static string GetLevelName(ReputationLevel level) => level switch
    {
        ReputationLevel.Hated => "通缉",
        ReputationLevel.Hostile => "敌对",
        ReputationLevel.Unfriendly => "不友好",
        ReputationLevel.Neutral => "中立",
        ReputationLevel.Friendly => "友好",
        ReputationLevel.Honored => "尊敬",
        ReputationLevel.Exalted => "崇拜",
        _ => "未知",
    };

    // ========================================
    // 修改
    // ========================================

    /// <summary>增减声望</summary>
    public void AddReputation(string nationId, int delta)
    {
        int current = GetReputation(nationId);
        _reputation[nationId] = Math.Clamp(current + delta, -100, 100);

        if (delta != 0)
            GD.Print($"[Reputation] {nationId}: {current} → {_reputation[nationId]} ({(delta > 0 ? "+" : "")}{delta})");
    }

    /// <summary>设置声望（覆盖）</summary>
    public void SetReputation(string nationId, int value)
    {
        _reputation[nationId] = Math.Clamp(value, -100, 100);
    }

    // ========================================
    // 影响计算
    // ========================================

    /// <summary>是否允许进入该势力的城镇</summary>
    public bool CanEnterTown(string nationId)
    {
        var level = GetLevel(nationId);
        return level != ReputationLevel.Hated && level != ReputationLevel.Hostile;
    }

    /// <summary>招募池倍率（声望低 → 减少可招募数量）</summary>
    public float GetRecruitMultiplier(string nationId)
    {
        var level = GetLevel(nationId);
        return level switch
        {
            ReputationLevel.Hated => 0.0f,
            ReputationLevel.Hostile => 0.0f,
            ReputationLevel.Unfriendly => 0.5f,
            ReputationLevel.Neutral => 1.0f,
            ReputationLevel.Friendly => 1.0f,
            ReputationLevel.Honored => 1.2f,
            ReputationLevel.Exalted => 1.5f,
            _ => 1.0f,
        };
    }

    /// <summary>商店价格倍率</summary>
    public float GetPriceMultiplier(string nationId)
    {
        var level = GetLevel(nationId);
        return level switch
        {
            ReputationLevel.Unfriendly => 1.2f,
            ReputationLevel.Friendly => 0.9f,
            ReputationLevel.Honored => 0.85f,
            ReputationLevel.Exalted => 0.8f,
            _ => 1.0f,
        };
    }

    /// <summary>是否解锁高级任务</summary>
    public bool HasEliteQuestAccess(string nationId)
    {
        return GetLevel(nationId) >= ReputationLevel.Honored;
    }

    /// <summary>是否可以请求封地（声望≥60）</summary>
    public bool CanRequestFief(string nationId)
    {
        return GetReputation(nationId) >= 60;
    }

    /// <summary>封地税率（声望越高税率越低）</summary>
    public int GetFiefTaxRate(string nationId)
    {
        var level = GetLevel(nationId);
        return level switch
        {
            ReputationLevel.Exalted => 10,
            ReputationLevel.Honored => 15,
            _ => 20,
        };
    }

    // ========================================
    // 头衔系统
    // ========================================

    /// <summary>nationId → 获得的头衔列表</summary>
    private readonly Dictionary<string, List<string>> _titles = new();

    /// <summary>添加头衔</summary>
    public void AddTitle(string nationId, string title)
    {
        if (!_titles.ContainsKey(nationId))
            _titles[nationId] = new List<string>();

        if (!_titles[nationId].Contains(title))
        {
            _titles[nationId].Add(title);
            GD.Print($"[Reputation] 获得头衔: {title} (势力: {nationId})");
        }
    }

    /// <summary>检查是否拥有某头衔</summary>
    public bool HasTitle(string nationId, string title)
    {
        return _titles.TryGetValue(nationId, out var titles) && titles.Contains(title);
    }

    /// <summary>获取某势力的所有头衔</summary>
    public List<string> GetTitles(string nationId)
    {
        return _titles.TryGetValue(nationId, out var titles) ? new List<string>(titles) : new List<string>();
    }

    // ========================================
    // 声望事件（预定义增减量）
    // ========================================

    /// <summary>完成该势力的委托</summary>
    public void OnQuestCompleted(string nationId, int reputationReward)
    {
        AddReputation(nationId, reputationReward > 0 ? reputationReward : 5);
    }

    /// <summary>击败该势力的敌人（帮助该势力）</summary>
    public void OnEnemyDefeated(string nationId)
    {
        AddReputation(nationId, 1);
    }

    /// <summary>攻击该势力的实体</summary>
    public void OnAttackedFaction(string nationId)
    {
        AddReputation(nationId, -10);
    }

    /// <summary>抢劫该势力的商队</summary>
    public void OnRobbedCaravan(string nationId)
    {
        AddReputation(nationId, -25);
    }

    /// <summary>杀死该势力的 NPC</summary>
    public void OnKilledNpc(string nationId)
    {
        AddReputation(nationId, -50);
    }

    /// <summary>释放战俘</summary>
    public void OnReleasedPrisoner(string nationId)
    {
        AddReputation(nationId, 5);
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var dict = new Godot.Collections.Dictionary();
        foreach (var (k, v) in _reputation)
            dict[k] = v;

        // 序列化头衔
        var titlesDict = new Godot.Collections.Dictionary();
        foreach (var (k, v) in _titles)
        {
            var arr = new Godot.Collections.Array();
            foreach (var title in v)
                arr.Add(title);
            titlesDict[k] = arr;
        }
        dict["_titles"] = titlesDict;

        return dict;
    }

    public static ReputationTracker Deserialize(Godot.Collections.Dictionary data)
    {
        var tracker = new ReputationTracker();
        foreach (var key in data.Keys)
        {
            if (key.AsString() == "_titles")
            {
                var titlesDict = (Godot.Collections.Dictionary)data[key];
                foreach (var nationKey in titlesDict.Keys)
                {
                    var arr = (Godot.Collections.Array)titlesDict[nationKey];
                    tracker._titles[nationKey.AsString()] = new List<string>();
                    foreach (var title in arr)
                        tracker._titles[nationKey.AsString()].Add(title.AsString());
                }
            }
            else
            {
                tracker._reputation[key.AsString()] = data[key].AsInt32();
            }
        }
        return tracker;
    }

    /// <summary>获取所有势力声望（兼容）</summary>
    public Godot.Collections.Dictionary GetAllReputationsGd()
    {
        return Serialize();
    }
}
