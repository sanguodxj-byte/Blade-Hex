// ProjectileSystem.cs
// 投射物逻辑系统 — 发射 + 延时命中结算
// 不处理动画，只通过 EventBus 触发事件
// Phase 3.3: 通过 ITickScheduler 注入延时调度，解耦 SceneTree
using Godot;
using BladeHex.Events;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 投射物逻辑系统 — 纯调度
/// 职责：接收发射请求 → 计算飞行时间 → 通知表现层 → 延时触发命中
/// 不关心：动画怎么播、伤害怎么算（由 DamageSystem 处理）
/// </summary>
public class ProjectileSystem : IProjectileSystem
{
    private readonly ITickScheduler _scheduler;

    public ProjectileSystem(ITickScheduler scheduler) => _scheduler = scheduler;

    /// <summary>
    /// 发射投射物 — 核心方法
    /// 同时触发两条独立时间线：
    ///   表现线：projectile_launched → ProjectilePool 路由给 View 飞行
    ///   逻辑线：delay 秒后 projectile_impact → DamageSystem 结算
    /// </summary>
    public void Launch(ProjectileData data)
    {
        if (data == null) return;

        // 计算世界坐标（从格坐标转换）
        Vector3 from = HexUtils.AxialToWorld3D(data.Origin.X, data.Origin.Y);
        Vector3 to = HexUtils.AxialToWorld3D(data.Target.X, data.Target.Y);

        // 飞行时间
        float travelTime = ProjectileTrajectory.CalculateTravelTime(from, to, data.Speed);

        // 事件 1：通知表现层播动画（立即）
        EventBus.Instance?.Publish(EventBus.Signals.ProjectileLaunched, new Godot.Collections.Dictionary
        {
            { "data", data.Serialize() },
            { "travel_time", travelTime },
            { "from_world", from },
            { "to_world", to },
        });

        // 事件 2：延迟触发命中（等动画飞完）
        ScheduleImpact(data, travelTime);
    }

    /// <summary>
    /// 延迟触发命中事件 — 通过 ITickScheduler 调度
    /// </summary>
    private void ScheduleImpact(ProjectileData data, float delay)
    {
        _scheduler.ScheduleOnce(delay, () =>
        {
            EventBus.Instance?.Publish(EventBus.Signals.ProjectileImpact, new Godot.Collections.Dictionary
            {
                { "data", data.Serialize() },
            });
        });
    }
}
