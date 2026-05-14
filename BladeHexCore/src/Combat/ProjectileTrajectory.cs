// ProjectileTrajectory.cs
// 投射物轨迹计算 — 纯数学，不依赖 Node
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 投射物轨迹 — 纯静态数学计算
/// 给定起点、终点、进度 t，返回世界坐标
/// 表现层每帧调用此方法更新投射物位置
/// </summary>
public static class ProjectileTrajectory
{
    /// <summary>
    /// 直线 + 自旋（飞刀/飞斧）
    /// </summary>
    public static Vector3 Knife(Vector3 from, Vector3 to, float t)
    {
        return from.Lerp(to, t);
    }

    /// <summary>
    /// 飞刀旋转角度 — 调用方设置 RotationDegrees
    /// </summary>
    public static float KnifeSpin(float t)
    {
        // 两圈 = 720°
        return t * 720.0f;
    }

    /// <summary>
    /// 抛物线（弓箭/弩箭）
    /// </summary>
    public static Vector3 Arrow(Vector3 from, Vector3 to, float t, float arcHeight = 1.5f)
    {
        Vector3 linear = from.Lerp(to, t);
        float arc = Mathf.Sin(t * Mathf.Pi) * arcHeight;
        return linear + Vector3.Up * arc;
    }

    /// <summary>
    /// 直线 + 微弱浮动（魔法弹/火球）
    /// </summary>
    public static Vector3 MagicBolt(Vector3 from, Vector3 to, float t)
    {
        Vector3 linear = from.Lerp(to, t);
        // 微弱的上下浮动
        float wobble = Mathf.Sin(t * Mathf.Pi * 4.0f) * 0.1f;
        return linear + Vector3.Up * wobble;
    }

    /// <summary>
    /// 根据投射物类型自动选择轨迹
    /// </summary>
    public static Vector3 Evaluate(string projectileType, Vector3 from, Vector3 to, float t, float arcHeight = 1.5f)
    {
        return projectileType switch
        {
            "throwing_knife" or "throwing_axe" => Knife(from, to, t),
            "arrow" or "crossbow_bolt" => Arrow(from, to, t, arcHeight),
            "fireball" or "magic_bolt" or "ice_shard" or "lightning" => MagicBolt(from, to, t),
            _ => Knife(from, to, t), // 默认直线
        };
    }

    /// <summary>
    /// 计算飞行时间（秒）
    /// </summary>
    public static float CalculateTravelTime(Vector3 from, Vector3 to, float speed)
    {
        if (speed <= 0) speed = 8.0f;
        float distance = from.DistanceTo(to);
        return distance / speed;
    }
}
