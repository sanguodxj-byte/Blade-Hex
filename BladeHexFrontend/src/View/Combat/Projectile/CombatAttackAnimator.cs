using Godot;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Scenes;

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
    private static readonly bool DebugLogging = false;
    private ProjectileSystem _projectileSystem = null!;

    /// <summary>当前攻击中双方是否都在视野内（PlayAttack 设置，子方法读取）</summary>
    private bool _bothVisible;

    /// <summary>可选：投射物飞行时用于相机跟随的相机控制器</summary>
    public CombatCameraController? CameraCtrl { get; set; }

    /// <summary>注入投射物逻辑系统（必须在使用前调用）</summary>
    public void Initialize(ProjectileSystem projectileSystem, CombatCameraController? cameraCtrl = null)
    {
        _projectileSystem = projectileSystem;
        CameraCtrl = cameraCtrl;
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 播放攻击动画并等待完成。
    /// <para>远程武器：发射投射物 → 目标播受击预备 → 等飞行时间</para>
    /// <para>近战武器：突刺动画 → 等固定时间</para>
    /// <para>镜头策略：先框定攻击者+目标双方，确保两者都在视野内。</para>
    /// </summary>
    public async Task PlayAttack(Unit attacker, Unit target, WeaponData? weapon)
    {
        if (DebugLogging) GD.Print($"[CombatAttackAnimator] PlayAttack: weapon={weapon?.ItemName}, IsRanged={weapon?.IsRanged}, _projectileSystem={_projectileSystem != null}");

        // 攻击前调整朝向，使攻击者始终面对防守者，骨骼动画正确播放
        int attackFacing = HexUtils.GetFacingDirection(attacker.GridPos, target.GridPos);
        attacker.Facing = attackFacing;

        // 镜头策略：仅当至少一方不在视野内时才框定，避免不必要的镜头移动
        _bothVisible = false;
        if (CameraCtrl != null)
        {
            _bothVisible = CameraCtrl.IsWorldPosVisible(attacker.Position)
                        && CameraCtrl.IsWorldPosVisible(target.Position);
            if (!_bothVisible)
            {
                await CameraCtrl.FrameTwoTargets(attacker.Position, target.Position, 0.3f);
            }
        }

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
        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.4f);

        CameraCtrl?.Unlock();
        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, _bothVisible ? 0.2f : 0.25f);
    }

    // ========================================
    // 远程
    // ========================================

    private async Task PlayRangedAttack(Unit attacker, Unit target, WeaponData weapon)
    {
        if (DebugLogging) GD.Print($"[CombatAttackAnimator] PlayRangedAttack: attacker={attacker.Data?.UnitName}, target={target.Data?.UnitName}, weapon={weapon.ItemName}, IsRanged={weapon.IsRanged}");

        // 所有参数从 WeaponData 获取
        var data = new ProjectileData
        {
            Origin = attacker.GridPos,
            Target = target.GridPos,
            ProjectileType = weapon.GetProjectileType(),
            Speed = weapon.GetProjectileSpeed(),
            ArcHeight = weapon.GetProjectileArcHeight(),
        };

        if (DebugLogging) GD.Print($"[CombatAttackAnimator] ProjectileData: type={data.ProjectileType}, speed={data.Speed}, arc={data.ArcHeight}");

        // 发射（EventBus → ProjectilePool → ProjectileView 飞行动画）
        _projectileSystem.Launch(data);

        // 计算飞行时间
        Vector3 logicalFromWorld = HexUtils.AxialToWorld3D(data.Origin.X, data.Origin.Y);
        Vector3 logicalToWorld = HexUtils.AxialToWorld3D(data.Target.X, data.Target.Y);
        float travelTime = ProjectileTrajectory.CalculateTravelTime(logicalFromWorld, logicalToWorld, data.Speed);

        // 飞行末段：目标播受击预备动画（飞行 70% 时触发）
        float braceDelay = travelTime * 0.7f;
        ScheduleBraceAnim(target, braceDelay);

        // 镜头策略：不跟随投射物，保持双方都在视野内（FrameTwoTargets 已在 PlayAttack 中调用）
        // 只需等待飞行时间完成
        await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, travelTime);

        CameraCtrl?.Unlock();
        if (!_bothVisible)
            await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.15f);
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
