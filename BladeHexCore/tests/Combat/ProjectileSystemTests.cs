// ProjectileSystemTests.cs
// 投射物系统无头测试 — 使用 ManualScheduler 验证发射→延时→命中事件流
using System.Collections.Generic;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Combat.Tests;

public static class ProjectileSystemTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        int passed = 0, failed = 0;
        var details = new List<string>();

        Run("WeaponData_GetProjectileType_Bow", WeaponData_GetProjectileType_Bow, ref passed, ref failed, details);
        Run("WeaponData_GetProjectileType_Crossbow", WeaponData_GetProjectileType_Crossbow, ref passed, ref failed, details);
        Run("WeaponData_GetProjectileType_Throwing", WeaponData_GetProjectileType_Throwing, ref passed, ref failed, details);
        Run("WeaponData_GetProjectileType_Catalyst_Fire", WeaponData_GetProjectileType_Catalyst_Fire, ref passed, ref failed, details);
        Run("WeaponData_GetProjectileType_Melee_Empty", WeaponData_GetProjectileType_Melee_Empty, ref passed, ref failed, details);
        Run("WeaponData_GetProjectileSpeed_ByType", WeaponData_GetProjectileSpeed_ByType, ref passed, ref failed, details);
        Run("WeaponData_GetProjectileArcHeight_ByType", WeaponData_GetProjectileArcHeight_ByType, ref passed, ref failed, details);
        Run("ProjectileTrajectory_Arrow_Parabolic", ProjectileTrajectory_Arrow_Parabolic, ref passed, ref failed, details);
        Run("ProjectileTrajectory_Knife_Linear", ProjectileTrajectory_Knife_Linear, ref passed, ref failed, details);
        Run("ProjectileTrajectory_TravelTime", ProjectileTrajectory_TravelTime, ref passed, ref failed, details);
        Run("ProjectileSystem_Launch_FiresEvents", ProjectileSystem_Launch_FiresEvents, ref passed, ref failed, details);
        Run("ProjectileSystem_Impact_AfterDelay", ProjectileSystem_Impact_AfterDelay, ref passed, ref failed, details);
        Run("ProjectileData_SerializeRoundtrip", ProjectileData_SerializeRoundtrip, ref passed, ref failed, details);

        return (passed, failed, details);
    }

    // ========================================
    // WeaponData.GetProjectileType 测试
    // ========================================

    private static bool WeaponData_GetProjectileType_Bow()
    {
        var w = new WeaponData { IsRanged = true, Subtype = WeaponData.WeaponSubtype.Shortbow };
        return w.GetProjectileType() == "arrow";
    }

    private static bool WeaponData_GetProjectileType_Crossbow()
    {
        var w = new WeaponData { IsRanged = true, IsCrossbow = true, Subtype = WeaponData.WeaponSubtype.HeavyCrossbow };
        return w.GetProjectileType() == "crossbow_bolt";
    }

    private static bool WeaponData_GetProjectileType_Throwing()
    {
        var w = new WeaponData { IsThrowing = true, Subtype = WeaponData.WeaponSubtype.Francisca };
        return w.GetProjectileType() == "throwing_axe";
    }

    private static bool WeaponData_GetProjectileType_Catalyst_Fire()
    {
        var w = new WeaponData { IsRanged = true, IsCatalyst = true, WeaponDamageType = WeaponData.DamageType.Fire };
        return w.GetProjectileType() == "fireball";
    }

    private static bool WeaponData_GetProjectileType_Melee_Empty()
    {
        var w = new WeaponData { Subtype = WeaponData.WeaponSubtype.ArmingSword };
        return w.GetProjectileType() == "";
    }

    // ========================================
    // WeaponData 飞行参数测试
    // ========================================

    private static bool WeaponData_GetProjectileSpeed_ByType()
    {
        var bow = new WeaponData { IsRanged = true };
        var xbow = new WeaponData { IsRanged = true, IsCrossbow = true };
        var thrown = new WeaponData { IsThrowing = true };
        var catalyst = new WeaponData { IsRanged = true, IsCatalyst = true };

        return bow.GetProjectileSpeed() == 10.0f
            && xbow.GetProjectileSpeed() == 14.0f
            && thrown.GetProjectileSpeed() == 8.0f
            && catalyst.GetProjectileSpeed() == 9.0f;
    }

    private static bool WeaponData_GetProjectileArcHeight_ByType()
    {
        var bow = new WeaponData { IsRanged = true };
        var xbow = new WeaponData { IsRanged = true, IsCrossbow = true };
        var thrown = new WeaponData { IsThrowing = true };
        var catalyst = new WeaponData { IsRanged = true, IsCatalyst = true };

        return bow.GetProjectileArcHeight() == 1.5f
            && xbow.GetProjectileArcHeight() == 0.5f
            && thrown.GetProjectileArcHeight() == 1.2f
            && catalyst.GetProjectileArcHeight() == 0.3f;
    }

    // ========================================
    // ProjectileTrajectory 纯数学测试
    // ========================================

    private static bool ProjectileTrajectory_Arrow_Parabolic()
    {
        var from = new Vector3(0, 0, 0);
        var to = new Vector3(100, 0, 0);

        // t=0 应在起点
        var p0 = ProjectileTrajectory.Arrow(from, to, 0f);
        if (p0.DistanceTo(from) > 0.01f) return false;

        // t=1 应在终点
        var p1 = ProjectileTrajectory.Arrow(from, to, 1f);
        if (p1.DistanceTo(to) > 0.01f) return false;

        // t=0.5 应在中点且 Y > 0（抛物线顶点）
        var pMid = ProjectileTrajectory.Arrow(from, to, 0.5f);
        if (pMid.Y <= 0) return false;
        if (Mathf.Abs(pMid.X - 50f) > 0.01f) return false;

        return true;
    }

    private static bool ProjectileTrajectory_Knife_Linear()
    {
        var from = new Vector3(0, 5, 0);
        var to = new Vector3(80, 5, 0);

        // 直线轨迹：任意 t 的 Y 应等于起点 Y
        var p = ProjectileTrajectory.Knife(from, to, 0.5f);
        return Mathf.Abs(p.Y - 5f) < 0.01f && Mathf.Abs(p.X - 40f) < 0.01f;
    }

    private static bool ProjectileTrajectory_TravelTime()
    {
        var from = new Vector3(0, 0, 0);
        var to = new Vector3(100, 0, 0);
        float speed = 10.0f;

        float time = ProjectileTrajectory.CalculateTravelTime(from, to, speed);
        float expectedTime = 100.0f / (speed * HexUtils.Size * Mathf.Sqrt(3.0f));
        return Mathf.Abs(time - expectedTime) < 0.01f;
    }

    // ========================================
    // ProjectileSystem 事件流测试（ManualScheduler）
    // ========================================

    private static bool ProjectileSystem_Launch_FiresEvents()
    {
        var scheduler = new ManualScheduler();
        var system = new ProjectileSystem(scheduler);

        var data = new ProjectileData
        {
            Origin = new Vector2I(0, 0),
            Target = new Vector2I(3, 0),
            ProjectileType = "arrow",
            Speed = 10.0f,
        };

        // Launch 应该调度一个延时回调（impact 事件）
        system.Launch(data);
        return scheduler.PendingCount == 1;
    }

    private static bool ProjectileSystem_Impact_AfterDelay()
    {
        var scheduler = new ManualScheduler();
        var system = new ProjectileSystem(scheduler);

        var data = new ProjectileData
        {
            Origin = new Vector2I(0, 0),
            Target = new Vector2I(3, 0),
            ProjectileType = "arrow",
            Speed = 10.0f,
        };

        system.Launch(data);

        // 计算预期飞行时间
        Vector3 from = HexUtils.AxialToWorld3D(0, 0);
        Vector3 to = HexUtils.AxialToWorld3D(3, 0);
        float expectedTime = ProjectileTrajectory.CalculateTravelTime(from, to, 10.0f);

        // 推进不够的时间 — 不应触发
        scheduler.Advance(expectedTime * 0.5f);
        if (scheduler.PendingCount != 1) return false;

        // 推进到超过飞行时间 — 应触发
        scheduler.Advance(expectedTime * 0.6f);
        return scheduler.PendingCount == 0;
    }

    // ========================================
    // ProjectileData 序列化测试
    // ========================================

    private static bool ProjectileData_SerializeRoundtrip()
    {
        var original = new ProjectileData
        {
            Origin = new Vector2I(2, 3),
            Target = new Vector2I(5, 7),
            ProjectileType = "fireball",
            Speed = 9.0f,
            ArcHeight = 0.3f,
            Damage = 15,
            AttackerUnitId = 42,
            TargetUnitId = 99,
            TexturePath = "res://test.png",
        };

        var dict = original.Serialize();
        var restored = ProjectileData.Deserialize(dict);

        return restored.Origin == original.Origin
            && restored.Target == original.Target
            && restored.ProjectileType == original.ProjectileType
            && Mathf.Abs(restored.Speed - original.Speed) < 0.01f
            && Mathf.Abs(restored.ArcHeight - original.ArcHeight) < 0.01f
            && restored.Damage == original.Damage
            && restored.AttackerUnitId == original.AttackerUnitId
            && restored.TargetUnitId == original.TargetUnitId
            && restored.TexturePath == original.TexturePath;
    }

    // ========================================
    // 辅助
    // ========================================

    private static void Run(string name, System.Func<bool> test, ref int passed, ref int failed, List<string> details)
    {
        try
        {
            if (test())
            {
                passed++;
                details.Add($"  [PASS] {name}");
            }
            else
            {
                failed++;
                details.Add($"  [FAIL] {name}");
            }
        }
        catch (System.Exception ex)
        {
            failed++;
            details.Add($"  [FAIL] {name} — {ex.GetType().Name}: {ex.Message}");
        }
    }
}
