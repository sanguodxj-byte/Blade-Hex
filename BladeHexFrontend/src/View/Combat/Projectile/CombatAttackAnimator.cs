// CombatAttackAnimator.cs
// 攻击动画编排器 — 独立组件
// 职责：判断武器类型 → 编排动画序列（近战突刺 / 远程投射物飞行）→ 等待完成
// 所有飞行参数从 WeaponData 获取，本组件不含硬编码数值
using Godot;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 攻击动画编排器 — 场景子节点。
/// <para>由 CombatSceneBase.InitSystems() 创建并 AddChild。</para>
/// <para>CombatSceneBase 和 AIController 通过此组件统一播放攻击动画。</para>
/// <para>所有投射物参数（速度、弧高、类型）均从 WeaponData 获取。</para>
/// </summary>
[GlobalClass]
public partial class CombatAttackAnimator : Node
{
    private ProjectileSystem _projectileSystem = null!;

    /// <summary>注入投射物逻辑系统（必须在使用前调用）</summary>
    public void Initialize(ProjectileSystem projectileSystem)
    {
        _projectileSystem = projectileSystem;
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 播放攻击动画并等待完成。
    /// <para>远程武器：发射投射物 → 目标播受击预备 → 等飞行时间</para>
    /// <para>近战武器：突刺动画 → 等固定时间</para>
    /// </summary>
    public async Task PlayAttack(Unit attacker, Unit target, WeaponData? weapon)
    {
        attacker.PlayAnim("attack");

        if (weapon != null && weapon.IsRanged)
            await PlayRangedAttack(attacker, target, weapon);
        else
            await PlayMeleeAttack(attacker, target);
    }

    // ========================================
    // 近战
    // ========================================

    private async Task PlayMeleeAttack(Unit attacker, Unit target)
    {
        attacker.PlayAttackLunge(target.GlobalPosition);
        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.6f);
    }

    // ========================================
    // 远程
    // ========================================

    private async Task PlayRangedAttack(Unit attacker, Unit target, WeaponData weapon)
    {
        // 所有参数从 WeaponData 获取
        var data = new ProjectileData
        {
            Origin = attacker.GridPos,
            Target = target.GridPos,
            ProjectileType = weapon.GetProjectileType(),
            Speed = weapon.GetProjectileSpeed(),
            ArcHeight = weapon.GetProjectileArcHeight(),
        };

        // 发射（EventBus → ProjectilePool → ProjectileView 飞行动画）
        _projectileSystem.Launch(data);

        // 计算飞行时间
        Vector3 fromWorld = HexUtils.AxialToWorld3D(data.Origin.X, data.Origin.Y);
        Vector3 toWorld = HexUtils.AxialToWorld3D(data.Target.X, data.Target.Y);
        float travelTime = ProjectileTrajectory.CalculateTravelTime(fromWorld, toWorld, data.Speed);

        // 飞行末段：目标播受击预备动画（飞行 70% 时触发）
        float braceDelay = travelTime * 0.7f;
        ScheduleBraceAnim(target, braceDelay);

        // 等待飞行完成
        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, travelTime);
    }

    // ========================================
    // 受击预备
    // ========================================

    /// <summary>延迟触发目标的受击预备动画（微后仰）</summary>
    private void ScheduleBraceAnim(Unit target, float delay)
    {
        // 飞行末段让目标做一个微小的预备反应
        float scaledDelay = delay / Mathf.Max(0.01f, BladeHex.View.Combat.CombatSpeed.Multiplier);
        if (scaledDelay <= 0.01f) return;

        GetTree().CreateTimer(scaledDelay).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(target) && target.CurrentHp > 0)
                target.PlayAnim("hit"); // 复用受击帧作为预备反应
        };
    }
}
