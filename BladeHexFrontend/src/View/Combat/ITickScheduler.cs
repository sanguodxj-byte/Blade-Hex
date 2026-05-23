// ITickScheduler.cs — 延时调度抽象（Phase 3.3）
// ProjectileSystem 通过此接口调度延时回调，避免直接依赖 SceneTree
using System;
using Godot;

namespace BladeHex.Combat;

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
