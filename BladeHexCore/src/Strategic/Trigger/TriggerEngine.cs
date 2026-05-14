// TriggerEngine.cs
// 条件触发引擎 — 注册条件、评估触发、驱动五种 Handler
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

// ========================================
// ITriggerHandler — 触发处理器接口
// ========================================

/// <summary>
/// 触发处理器接口 — 每种触发类型实现此接口
/// </summary>
public interface ITriggerHandler
{
    /// <summary>处理的触发类型</summary>
    TriggerType Type { get; }

    /// <summary>
    /// 评估触发条件
    /// </summary>
    /// <param name="condition">触发条件</param>
    /// <param name="ctx">触发上下文</param>
    /// <returns>触发结果（null 表示不适用）</returns>
    TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx);
}

// ========================================
// TriggerEngine — 触发引擎
// ========================================

/// <summary>
/// 条件触发引擎 — 统一管理所有触发条件的注册和判定
/// </summary>
[GlobalClass]
public partial class TriggerEngine : RefCounted
{
    // ========================================
    // 注册数据
    // ========================================

    /// <summary>所有已注册的触发条件</summary>
    private readonly Dictionary<string, TriggerCondition> _conditions = new();

    /// <summary>按类型分组的触发条件（用于批量判定）</summary>
    private readonly Dictionary<TriggerType, List<TriggerCondition>> _byType = new()
    {
        [TriggerType.Spatial] = new(),
        [TriggerType.Interaction] = new(),
        [TriggerType.Time] = new(),
        [TriggerType.Chain] = new(),
        [TriggerType.Environment] = new(),
    };

    /// <summary>触发处理器</summary>
    private readonly Dictionary<TriggerType, ITriggerHandler> _handlers = new();

    // ========================================
    // 注册
    // ========================================

    /// <summary>注册一个触发条件</summary>
    public void RegisterCondition(TriggerCondition condition)
    {
        _conditions[condition.Id] = condition;
        if (!_byType.ContainsKey(condition.Type))
            _byType[condition.Type] = new List<TriggerCondition>();
        _byType[condition.Type].Add(condition);
    }

    /// <summary>批量注册触发条件</summary>
    public void RegisterConditions(IEnumerable<TriggerCondition> conditions)
    {
        foreach (var c in conditions) RegisterCondition(c);
    }

    /// <summary>注册触发处理器</summary>
    public void RegisterHandler(ITriggerHandler handler)
    {
        _handlers[handler.Type] = handler;
    }

    // ========================================
    // 评估 — 单条条件
    // ========================================

    /// <summary>
    /// 评估单个触发条件
    /// </summary>
    public TriggerResult? Evaluate(string conditionId, TriggerContext ctx)
    {
        if (!_conditions.TryGetValue(conditionId, out var condition)) return null;
        return EvaluateCondition(condition, ctx);
    }

    /// <summary>
    /// 评估单个触发条件
    /// </summary>
    public TriggerResult? EvaluateCondition(TriggerCondition condition, TriggerContext ctx)
    {
        // 检查是否有对应处理器
        if (!_handlers.TryGetValue(condition.Type, out var handler)) return null;

        // 前置条件检查（所有类型通用）
        if (!CheckPrerequisites(condition, ctx)) return null;

        // 委托给处理器
        var result = handler.Evaluate(condition, ctx);
        if (result != null && result.Triggered)
        {
            // 记录到历史
            ctx.History.Record(condition.Id, ctx.CurrentDay);

            // 解锁后续触发
            if (condition.UnlockedIds.Length > 0)
                result.UnlockedIds = condition.UnlockedIds;
        }

        return result;
    }

    // ========================================
    // 评估 — 按类型批量判定
    // ========================================

    /// <summary>
    /// 评估所有空间触发条件（玩家进入新 Chunk 时调用）
    /// </summary>
    public List<TriggerResult> EvaluateSpatial(TriggerContext ctx)
    {
        return EvaluateByType(TriggerType.Spatial, ctx);
    }

    /// <summary>
    /// 评估所有时间触发条件（天数推进时调用）
    /// </summary>
    public List<TriggerResult> EvaluateTime(TriggerContext ctx)
    {
        return EvaluateByType(TriggerType.Time, ctx);
    }

    /// <summary>
    /// 评估所有连锁触发条件（某个事件完成后调用）
    /// </summary>
    public List<TriggerResult> EvaluateChain(TriggerContext ctx)
    {
        return EvaluateByType(TriggerType.Chain, ctx);
    }

    /// <summary>
    /// 评估所有环境触发条件
    /// </summary>
    public List<TriggerResult> EvaluateEnvironment(TriggerContext ctx)
    {
        return EvaluateByType(TriggerType.Environment, ctx);
    }

    // ========================================
    // 内部方法
    // ========================================

    /// <summary>按类型批量评估</summary>
    private List<TriggerResult> EvaluateByType(TriggerType type, TriggerContext ctx)
    {
        var results = new List<TriggerResult>();

        if (!_byType.TryGetValue(type, out var conditions)) return results;

        // 按优先级降序排序
        conditions.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (var condition in conditions)
        {
            var result = EvaluateCondition(condition, ctx);
            if (result != null) results.Add(result);
        }

        return results;
    }

    /// <summary>检查前置条件（所有类型通用）</summary>
    private bool CheckPrerequisites(TriggerCondition condition, TriggerContext ctx)
    {
        // 玩家等级检查
        if (condition.MinPlayerLevel > 0 && ctx.PlayerLevel < condition.MinPlayerLevel)
            return false;

        // 天数范围检查
        if (condition.MinDay > 0 && ctx.CurrentDay < condition.MinDay)
            return false;
        if (condition.MaxDay > 0 && ctx.CurrentDay > condition.MaxDay)
            return false;

        // 冷却检查
        if (condition.CooldownDays > 0 && ctx.History.IsOnCooldown(condition.Id, ctx.CurrentDay, condition.CooldownDays))
            return false;

        // 一次性触发检查（CooldownDays = 0 且已触发 → 不再触发）
        if (condition.CooldownDays == 0 && ctx.History.IsTriggered(condition.Id))
            return false;

        // 前置触发检查
        foreach (var prereq in condition.PrerequisiteIds)
        {
            if (!ctx.History.IsTriggered(prereq)) return false;
        }

        // 互斥检查
        foreach (var exclusive in condition.MutuallyExclusive)
        {
            if (ctx.History.IsTriggered(exclusive)) return false;
        }

        return true;
    }

    // ========================================
    // 查询
    // ========================================

    /// <summary>获取指定类型的所有条件</summary>
    public TriggerCondition[] GetConditionsByType(TriggerType type)
    {
        return _byType.TryGetValue(type, out var list) ? list.ToArray() : [];
    }

    /// <summary>获取所有已注册的条件数量</summary>
    public int ConditionCount => _conditions.Count;
}
