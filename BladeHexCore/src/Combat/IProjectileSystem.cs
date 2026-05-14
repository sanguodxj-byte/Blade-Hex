// IProjectileSystem.cs
// 投射物系统接口 — 逻辑层契约
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
