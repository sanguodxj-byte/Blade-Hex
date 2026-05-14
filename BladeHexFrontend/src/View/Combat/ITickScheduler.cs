// ITickScheduler.cs — 延时调度抽象（Phase 3.3）
// ProjectileSystem 通过此接口调度延时回调，避免直接依赖 SceneTree
using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 延时调度接口 — 解耦 SceneTree 依赖
/// </summary>
public interface ITickScheduler
{
    /// <summary>在 delaySeconds 后执行 callback</summary>
    void ScheduleOnce(float delaySeconds, Action callback);
}

/// <summary>
/// SceneTree 调度器 — 生产环境使用
/// </summary>
public sealed class SceneTreeScheduler : ITickScheduler
{
    private readonly SceneTree _tree;

    public SceneTreeScheduler(SceneTree tree) => _tree = tree;

    public void ScheduleOnce(float delay, Action cb)
    {
        _tree.CreateTimer(delay).Timeout += () => cb();
    }
}

/// <summary>
/// 手动调度器 — 测试使用，手动推进时间
/// </summary>
public sealed class ManualScheduler : ITickScheduler
{
    private readonly List<(float DueAt, Action Callback)> _pending = new();
    private float _now;

    public float CurrentTime => _now;

    public void ScheduleOnce(float delay, Action cb)
    {
        _pending.Add((_now + delay, cb));
    }

    /// <summary>推进时间，触发到期的回调</summary>
    public void Advance(float deltaSeconds)
    {
        _now += deltaSeconds;
        var due = _pending.FindAll(p => p.DueAt <= _now);
        foreach (var item in due)
        {
            _pending.Remove(item);
            item.Callback();
        }
    }

    /// <summary>待执行回调数量</summary>
    public int PendingCount => _pending.Count;
}
