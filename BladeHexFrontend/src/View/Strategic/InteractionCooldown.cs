// InteractionCooldown.cs
// 玩家交互冷却集中化模块
//
// 设计目标:
//   - 把 0.5s 玩家-实体交互冷却变成独立模块
//   - 冷却适用于：实体重叠触发、战斗结束回到大地图、关闭对话面板、进入/离开 POI 交互
//   - 玩家和实体重叠时不会反复弹交互导致卡死
//   - 冷却不会阻止主动点击战场/围城入口
using System;

namespace BladeHex.View.Strategic;

/// <summary>冷却类型标识</summary>
public enum CooldownSource
{
    /// <summary>实体重叠触发</summary>
    EntityOverlap,
    /// <summary>战斗结束回到大地图</summary>
    PostCombat,
    /// <summary>关闭对话/交互面板</summary>
    PanelClose,
    /// <summary>进入/离开 POI 交互</summary>
    PoiInteraction,
}

/// <summary>
/// 玩家交互冷却集中管理器。
///
/// 管线位置:
///   任何可能触发交互的代码 → Cooldown.IsCoolingDown(source) → 跳过或继续
///
/// 用法:
///   var cooldown = new InteractionCooldown();
///   // 某事件发生时:
///   cooldown.Trigger(CooldownSource.EntityOverlap);
///   // 检测是否仍在冷却:
///   if (cooldown.IsCoolingDown(CooldownSource.EntityOverlap)) return;
///   // 主动点击（战场/围城入口）不受冷却限制:
///   // → 直接执行，无需检查冷却
/// </summary>
public sealed class InteractionCooldown
{
    /// <summary>默认冷却时间（秒）</summary>
    public double DefaultCooldownSeconds { get; set; } = 0.5;

    /// <summary>战斗结束后的冷却时间（秒）— 稍长，防止场景切换后立即触发</summary>
    public double PostCombatCooldownSeconds { get; set; } = 1.0;

    /// <summary>POI 交互冷却时间（秒）</summary>
    public double PoiCooldownSeconds { get; set; } = 0.3;

    private readonly System.Collections.Generic.Dictionary<CooldownSource, double> _cooldownUntil = new();

    /// <summary>触发指定类型的冷却</summary>
    public void Trigger(CooldownSource source, double currentTimeSec)
    {
        double duration = source switch
        {
            CooldownSource.PostCombat => PostCombatCooldownSeconds,
            CooldownSource.PoiInteraction => PoiCooldownSeconds,
            _ => DefaultCooldownSeconds,
        };
        _cooldownUntil[source] = currentTimeSec + duration;
    }

    /// <summary>检查指定类型是否仍在冷却中</summary>
    public bool IsCoolingDown(CooldownSource source, double currentTimeSec)
    {
        if (!_cooldownUntil.TryGetValue(source, out double until))
            return false;
        return currentTimeSec < until;
    }

    /// <summary>获取指定类型的剩余冷却时间（秒）</summary>
    public double GetRemainingSeconds(CooldownSource source, double currentTimeSec)
    {
        if (!_cooldownUntil.TryGetValue(source, out double until))
            return 0.0;
        return Math.Max(0.0, until - currentTimeSec);
    }

    /// <summary>强制清除所有冷却（场景切换、调试用）</summary>
    public void ClearAll()
    {
        _cooldownUntil.Clear();
    }

    /// <summary>强制清除指定类型的冷却</summary>
    public void Clear(CooldownSource source)
    {
        _cooldownUntil.Remove(source);
    }
}
