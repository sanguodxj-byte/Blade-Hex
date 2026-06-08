// SkillResultBuilder.cs
// 技能执行结果构建器 — 替代手写 Dictionary，提供类型安全的 API
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat.Skills;

/// <summary>
/// 技能结果构建器 — handler 通过此对象添加伤害/治疗/传送/状态效果等子结果，
/// 最终调用 Build() 生成不可变的 SkillExecutionResult。
/// </summary>
public sealed class SkillResultBuilder
{
    private bool _success = true;
    private string? _failureReason;
    private readonly List<SkillSubResult> _subResults = new();

    // ========================================================================
    // 伤害
    // ========================================================================

    /// <summary>添加一次伤害事件</summary>
    public void AddDamage(Unit target, int damage, bool isCritical = false, bool wasKillingBlow = false)
    {
        if (target?.Model == null) return;
        _subResults.Add(new DamageEvent(target.Model, damage, isCritical, wasKillingBlow));
    }

    /// <summary>从 CombatResolver.ResolveAttack 结果字典提取并添加伤害事件</summary>
    public void AddDamageFromResolver(Unit target, Godot.Collections.Dictionary resolverResult)
    {
        if (target?.Model == null) return;
        bool hit = resolverResult.ContainsKey("hit") && resolverResult["hit"].AsBool();
        if (!hit) return;
        int damage = resolverResult.ContainsKey("damage") ? resolverResult["damage"].AsInt32() : 0;
        bool critical = resolverResult.ContainsKey("critical") && resolverResult["critical"].AsBool();
        bool killingBlow = resolverResult.ContainsKey("killing_blow") && resolverResult["killing_blow"].AsBool();
        _subResults.Add(new DamageEvent(target.Model, damage, critical, killingBlow));
    }

    // ========================================================================
    // 治疗
    // ========================================================================

    /// <summary>添加一次治疗事件</summary>
    public void AddHeal(Unit target, int amount)
    {
        if (target?.Model == null) return;
        _subResults.Add(new HealEvent(target.Model, amount));
    }

    // ========================================================================
    // 传送
    // ========================================================================

    /// <summary>添加一次传送事件</summary>
    public void AddTeleport(Unit target, Vector2I destination, Vector2I? previousPosition = null)
    {
        if (target?.Model == null) return;
        _subResults.Add(new TeleportEvent(target.Model, destination, previousPosition));
    }

    // ========================================================================
    // 状态效果
    // ========================================================================

    /// <summary>添加一个状态效果应用</summary>
    public void AddStatusEffect(string effectId, Unit target, int duration = -1,
        StatusEffectSpecial special = StatusEffectSpecial.None)
    {
        if (target?.Model == null) return;
        _subResults.Add(new StatusEffectApplication(effectId, target.Model, duration, special));
    }

    /// <summary>添加一个需要移除的状态效果（RemoveEffects 特殊操作）</summary>
    public void AddRemoveEffect(Unit target, string effectId)
    {
        if (target?.Model == null) return;
        _subResults.Add(new StatusEffectApplication(effectId, target.Model, -1, StatusEffectSpecial.RemoveEffects));
    }

    /// <summary>添加移除所有负面状态效果</summary>
    public void AddRemoveAllNegative(Unit target)
    {
        if (target?.Model == null) return;
        _subResults.Add(new StatusEffectApplication("", target.Model, -1, StatusEffectSpecial.RemoveAllNegative));
    }

    // ========================================================================
    // Buff
    // ========================================================================

    /// <summary>添加一个 Buff 应用</summary>
    public void AddBuff(string buffId, Unit target)
    {
        if (target?.Model == null) return;
        _subResults.Add(new BuffApplication(buffId, target.Model));
    }

    // ========================================================================
    // 战场锚点
    // ========================================================================

    public void AddBattleAnchor(string anchorId, string source, Vector2I position, int duration,
        bool destructible = false, int hp = 1)
    {
        if (string.IsNullOrEmpty(anchorId) || string.IsNullOrEmpty(source)) return;
        _subResults.Add(new BattleAnchorEvent(anchorId, source, position, duration, destructible, hp));
    }

    // ========================================================================
    // 文本
    // ========================================================================

    /// <summary>添加一条文本结果（用于日志/显示）</summary>
    public void AddText(string text)
    {
        _subResults.Add(new ResultText(text));
    }

    // ========================================================================
    // 通用子结果
    // ========================================================================

    /// <summary>直接添加一个子结果（用于自定义 SkillSubResult 子类）</summary>
    public void AddSubResult(SkillSubResult subResult)
    {
        _subResults.Add(subResult);
    }

    // ========================================================================
    // 失败
    // ========================================================================

    /// <summary>标记技能执行失败</summary>
    public void Fail(string reason)
    {
        _success = false;
        _failureReason = reason;
    }

    /// <summary>当前是否已标记为失败</summary>
    public bool IsFailed => !_success;

    // ========================================================================
    // 构建
    // ========================================================================

    /// <summary>构建最终的不可变 SkillExecutionResult</summary>
    public SkillExecutionResult Build()
    {
        if (!_success)
            return SkillExecutionResult.Fail(_failureReason ?? "未知原因");

        return new SkillExecutionResult
        {
            Success = true,
            SubResults = _subResults.AsReadOnly(),
            StatusEffects = Array.Empty<StatusEffectApplication>(),
        };
    }
}
