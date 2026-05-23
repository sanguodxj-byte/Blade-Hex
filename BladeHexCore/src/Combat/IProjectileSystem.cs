// IProjectileSystem.cs
// 投射物系统接口 — 逻辑层契约
using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 投射物系统 — 逻辑层接口
/// 只关心"发射 → 延时 → 命中"，不关心动画
/// </summary>
public interface IProjectileSystem
{
    /// <summary>
    /// 发射投射物
    /// 1. 计算飞行时间
    /// 2. 通知表现层播动画 (OnProjectileLaunched)
    /// 3. 延迟触发命中 (OnProjectileImpact)
    /// </summary>
    void Launch(ProjectileData data);
}

/// <summary>
/// 投射物视图 — 表现层接口
/// 只关心"怎么飞得好看"，不关心伤害
/// </summary>
public interface IProjectileView
{
    /// <summary>
    /// 播放飞行动画
    /// </summary>
    /// <param name="data">投射物数据</param>
    /// <param name="duration">飞行时长（秒）</param>
    void Play(ProjectileData data, float duration);

    /// <summary>
    /// 停止并回收
    /// </summary>
    void Stop();
}

/// <summary>延时调度接口 — 解耦 SceneTree 依赖。</summary>
public interface ITickScheduler
{
    /// <summary>在 delaySeconds 后执行 callback。</summary>
    void ScheduleOnce(float delaySeconds, Action callback);
}

/// <summary>手动调度器 — 测试使用，手动推进时间。</summary>
public sealed class ManualScheduler : ITickScheduler
{
    private readonly List<(float DueAt, Action Callback)> _pending = new();
    private float _now;

    public float CurrentTime => _now;

    public void ScheduleOnce(float delay, Action cb)
    {
        _pending.Add((_now + delay, cb));
    }

    /// <summary>推进时间，触发到期的回调。</summary>
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

    /// <summary>待执行回调数量。</summary>
    public int PendingCount => _pending.Count;
}
