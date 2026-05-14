// ProjectilePool.cs
// 投射物对象池 — 高频生成/销毁的优化
// 同时作为 EventBus 路由者：收到 projectile_launched 事件时分配空闲 View
using Godot;
using System.Collections.Generic;
using BladeHex.Events;

namespace BladeHex.Combat;

/// <summary>
/// 投射物对象池 — 管理生命周期 + 事件路由
/// 职责：
///   1. 订阅 projectile_launched 事件
///   2. 为每次发射分配空闲的 ProjectileView
///   3. 飞行完成后回收 View
/// </summary>
[GlobalClass]
public partial class ProjectilePool : Node
{
    private readonly Dictionary<string, NodePool<ProjectileView>> _pools = new();
    private readonly List<ProjectileView> _activeViews = new();

    private readonly Dictionary<string, int> _prewarmCounts = new()
    {
        { "arrow", 8 },
        { "throwing_knife", 6 },
        { "fireball", 4 },
        { "magic_bolt", 4 },
    };

    private const int MAX_POOL_SIZE = 32;

    public int ActiveCount => _activeViews.Count;

    public override void _Ready()
    {
        // 为每种类型创建 NodePool
        foreach (var kv in _prewarmCounts)
        {
            var pool = CreatePoolForType(kv.Key, kv.Value);
            _pools[kv.Key] = pool;
        }

        // 订阅发射事件 — 由 Pool 统一路由给空闲 View
        if (EventBus.Instance != null)
            EventBus.Instance.Subscribe(EventBus.Signals.ProjectileLaunched, OnProjectileLaunched);
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.Unsubscribe(EventBus.Signals.ProjectileLaunched, OnProjectileLaunched);
    }

    // ========================================
    // 公开 API
    // ========================================

    /// <summary>获取一个投射物视图</summary>
    public ProjectileView Get(string type)
    {
        if (!_pools.TryGetValue(type, out var pool))
        {
            pool = CreatePoolForType(type, 2);
            _pools[type] = pool;
        }

        var view = pool.Retrieve();
        _activeViews.Add(view);
        return view;
    }

    /// <summary>回收投射物视图</summary>
    public void Return(ProjectileView view)
    {
        if (view == null || !GodotObject.IsInstanceValid(view)) return;

        _activeViews.Remove(view);

        // 根据类型回收到对应的池
        string type = view.ProjectileType;
        if (_pools.TryGetValue(type, out var pool))
        {
            pool.Return(view);
        }
        else
        {
            view.QueueFree();
        }
    }

    /// <summary>清空所有池 — 战斗结束时调用</summary>
    public void Clear()
    {
        _activeViews.Clear();
        foreach (var pool in _pools.Values)
            pool.Clear();
    }

    // ========================================
    // 事件路由 — 核心修复
    // ========================================

    /// <summary>
    /// EventBus 回调 — 收到发射事件时分配空闲 View
    /// 不再让每个 View 自己争抢事件
    /// </summary>
    private void OnProjectileLaunched(Godot.Collections.Dictionary eventData)
    {
        if (!eventData.ContainsKey("data")) return;

        var data = ProjectileData.Deserialize(eventData["data"].AsGodotDictionary());
        float travelTime = eventData.ContainsKey("travel_time") ? eventData["travel_time"].AsSingle() : 0.3f;

        // 从池中取出一个对应类型的 View
        var view = Get(data.ProjectileType);
        if (view != null)
            view.Play(data, travelTime);
    }

    // ========================================
    // 内部
    // ========================================

    private NodePool<ProjectileView> CreatePoolForType(string type, int prewarmCount)
    {
        var pool = new NodePool<ProjectileView>(
            factory: () =>
            {
                var view = new ProjectileView
                {
                    Name = $"Projectile_{type}",
                    ProjectileType = type,
                };
                AddChild(view);
                return view;
            },
            onRetrieve: v => v.Visible = true,
            onReturn: v => v.Stop(),
            maxSize: MAX_POOL_SIZE
        );
        pool.SetParent(this);
        pool.Prewarm(prewarmCount);
        return pool;
    }
}
