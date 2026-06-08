// ProjectileSystem.cs
// 投射物逻辑系统 — 发射 + 延时命中结算。
using BladeHex.Map;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 投射物逻辑系统 — 纯调度。
/// 职责：接收发射请求 → 计算飞行时间 → 通知表现层 → 延时触发命中。
/// </summary>
public class ProjectileSystem : IProjectileSystem
{
    private readonly ITickScheduler _scheduler;
    public event System.Action<Godot.Collections.Dictionary>? ProjectileLaunched;
    public event System.Action<Godot.Collections.Dictionary>? ProjectileImpact;

    /// <summary>调试日志开关。默认关闭。</summary>
    public static bool DebugLogging { get; set; } = false;

    public ProjectileSystem(ITickScheduler scheduler) => _scheduler = scheduler;

    public void Launch(ProjectileData data)
    {
        if (data == null) { GD.PrintErr("[ProjectileSystem] Launch called with NULL data!"); return; }

        Vector3 from = HexUtils.AxialToWorld3D(data.Origin.X, data.Origin.Y);
        Vector3 to = HexUtils.AxialToWorld3D(data.Target.X, data.Target.Y);
        float travelTime = ProjectileTrajectory.CalculateTravelTime(from, to, data.Speed);

        if (DebugLogging)
            GD.Print($"[ProjectileSystem] Launch: type={data.ProjectileType}, from={data.Origin}, to={data.Target}, has_subscribers={ProjectileLaunched != null}");
        ProjectileLaunched?.Invoke(new Godot.Collections.Dictionary
        {
            { "data", data.Serialize() },
            { "travel_time", travelTime },
            { "from_world", from },
            { "to_world", to },
        });

        _scheduler.ScheduleOnce(travelTime, () =>
        {
            ProjectileImpact?.Invoke(new Godot.Collections.Dictionary
            {
                { "data", data.Serialize() },
            });
        });
    }
}
