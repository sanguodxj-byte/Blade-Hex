// DormantEntityPool.cs
// 不活跃实体池 — 存储离开活跃区域的实体，供后续复用
//
// 设计：
// - 活跃实体移动到 chunk 加载范围外 → 收容至不活跃池
// - 下次需要生成同类型实体时，高概率（80%）从池中取出复用
// - 复用的实体保留原有属性（等级/战力/装备），只更新位置和 AI 状态
// - 池有容量上限，超出时淘汰最旧的实体
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 不活跃实体池 — 管理离开玩家视野的实体的休眠与复用
/// </summary>
public class DormantEntityPool
{
    /// <summary>从池中复用实体的概率（0~1）</summary>
    public float ReuseChance { get; set; } = 0.8f;

    /// <summary>每种类型的最大池容量</summary>
    public int MaxPerType { get; set; } = 10;

    /// <summary>池中实体的最大存活天数（超过后淘汰）</summary>
    public int MaxDormantDays { get; set; } = 30;

    /// <summary>休眠实体交战结算回调 — 由 OverworldEntityManager 注入 BattleResolver.ResolveDormantEngagement</summary>
    public Action<OverworldEntity>? DormantEngagementResolver { get; set; }

    // 按实体类型分桶存储
    private readonly Dictionary<OverworldEntity.EntityType, List<DormantEntry>> _pool = new();
    private readonly Random _rng = new();

    /// <summary>不活跃实体条目</summary>
    private class DormantEntry
    {
        public OverworldEntity Entity;
        public int DormantSinceDay;

        public DormantEntry(OverworldEntity entity, int currentDay)
        {
            Entity = entity;
            DormantSinceDay = currentDay;
        }
    }

    // ========================================
    // 收容（活跃 → 不活跃）
    // ========================================

    /// <summary>
    /// 将实体收容到不活跃池。
    /// 重置移动状态，保留核心属性（等级/战力/种族/装备）。
    /// </summary>
    public void Store(OverworldEntity entity, int currentDay)
    {
        if (entity == null || !entity.IsAlive) return;

        var type = entity.EntityTypeEnum;
        if (!_pool.ContainsKey(type))
            _pool[type] = new List<DormantEntry>();

        var bucket = _pool[type];

        // 容量检查：淘汰最旧的
        while (bucket.Count >= MaxPerType)
        {
            bucket.RemoveAt(0);
        }

        // 重置运行时状态（保留核心属性和交战状态）
        entity.IsMoving = false;
        entity.Path.Clear();
        // 保留交战状态 — 交战数据(EngagedWith/EngagedSinceHour等)不清除，
        // 在 TryReuse 时由 BattleResolver.ResolveDormantEngagement 一次性结算
        if (entity.CurrentAIState != OverworldEntity.AIState.Engaged)
        {
            entity.CurrentAIState = OverworldEntity.AIState.Idle;
            entity.ChaseTarget = null;
        }
        entity.SiegeTarget = null;
        entity.ReinforceTarget = null;

        bucket.Add(new DormantEntry(entity, currentDay));
    }

    // ========================================
    // 复用（不活跃 → 活跃）
    // ========================================

    /// <summary>
    /// 尝试从池中取出一个同类型实体复用。
    /// 返回 null 表示池中无可用实体或概率未命中。
    /// 复用的实体会更新位置和 AI 状态，但保留原有战力/等级。
    /// </summary>
    public OverworldEntity? TryReuse(OverworldEntity.EntityType type, Godot.Vector2 newPosition, int currentDay)
    {
        // 概率检查
        if (_rng.NextDouble() > ReuseChance)
            return null;

        if (!_pool.TryGetValue(type, out var bucket) || bucket.Count == 0)
            return null;

        // 取最近入池的（LIFO — 更"新鲜"的实体优先）
        int lastIdx = bucket.Count - 1;
        var entry = bucket[lastIdx];
        bucket.RemoveAt(lastIdx);

        var entity = entry.Entity;

        // 休眠交战一次性结算
        if (entity.CurrentAIState == OverworldEntity.AIState.Engaged)
            DormantEngagementResolver?.Invoke(entity);

        // 更新位置和状态
        entity.Position = newPosition;
        entity.HomePosition = newPosition;
        entity.TerritoryCenter = newPosition;
        entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        entity.IsMoving = false;
        entity.IsAlive = true;
        entity.DaysAlive += (currentDay - entry.DormantSinceDay); // 累加休眠天数

        return entity;
    }

    // ========================================
    // 维护
    // ========================================

    /// <summary>
    /// 每日维护：淘汰过期实体
    /// </summary>
    public void OnDayPassed(int currentDay)
    {
        foreach (var bucket in _pool.Values)
        {
            bucket.RemoveAll(entry => (currentDay - entry.DormantSinceDay) > MaxDormantDays);
        }
    }

    /// <summary>池中实体总数</summary>
    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (var bucket in _pool.Values)
                total += bucket.Count;
            return total;
        }
    }

    /// <summary>获取指定类型的池中数量</summary>
    public int CountOfType(OverworldEntity.EntityType type)
    {
        return _pool.TryGetValue(type, out var bucket) ? bucket.Count : 0;
    }

    /// <summary>清空所有池</summary>
    public void Clear()
    {
        _pool.Clear();
    }
}
