// ResourceRegistryTests.cs — T-102
// ResourceRegistry 单元测试：覆盖 Register / Get / Miss 三场景
//
// 使用方式：
//   从 Godot 场景或测试 harness 直接调用静态方法
//   不依赖任何测试框架（与 Wave 1 其他测试夹具一致）
//
// 测试用例：
//   1. Miss: 空注册表 / 未知 id → 返回 null，不崩溃
//   2. Register + Get (路径): 注册路径 → 懒加载 Get
//   3. Register + Get (对象): 注册 Resource 对象 → 直接缓存命中
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.View.Data;

namespace BladeHex.View.Data.Tests;

public static class ResourceRegistryTests
{
    /// <summary>
    /// 运行全部 Register / Get / Miss 测试
    /// </summary>
    public static (bool pass, List<string> failures) RunAll()
    {
        var failures = new List<string>();

        // Setup: 隔离的注册表
        ResourceRegistry.Clear();

        // Test 1: Miss
        TestMiss(failures);

        // Test 2: Register + Get (路径方式)
        TestRegisterPath(failures);

        // Test 3: Register + Get (对象方式)
        TestRegisterObject(failures);

        // 清理
        ResourceRegistry.Clear();

        return (failures.Count == 0, failures);
    }

    // ====================================================================
    // Test 1: Miss — 空注册表 / 未知 id
    // ====================================================================

    private static void TestMiss(List<string> failures)
    {
        try
        {
            ResourceRegistry.Clear();

            var icon = ResourceRegistry.GetIcon("nonexistent");
            if (icon != null)
                failures.Add("[Miss-Icon] GetIcon should return null for unknown id");

            var frames = ResourceRegistry.GetSpriteFrames("nonexistent");
            if (frames != null)
                failures.Add("[Miss-SpriteFrames] GetSpriteFrames should return null for unknown id");

            var mat = ResourceRegistry.GetMaterial("nonexistent");
            if (mat != null)
                failures.Add("[Miss-Material] GetMaterial should return null for unknown id");

            var found = ResourceRegistry.TryGet<Texture2D>("nonexistent", out var _);
            if (found)
                failures.Add("[Miss-TryGet] TryGet should return false for unknown id");

            // null/empty id 也应安全处理，不崩溃
            ResourceRegistry.GetIcon(null);
            ResourceRegistry.GetIcon("");
        }
        catch (Exception ex)
        {
            failures.Add($"[Miss-Crash] Miss test threw: {ex.Message}");
        }
    }

    // ====================================================================
    // Test 2: Register (路径) + Get (懒加载)
    // ====================================================================

    private static void TestRegisterPath(List<string> failures)
    {
        try
        {
            ResourceRegistry.Clear();

            // 注册不存在的路径 — 懒加载应返回 null 而不崩溃
            ResourceRegistry.RegisterIcon("missing_icon", "res://nonexistent_dir/nonexistent.png");
            ResourceRegistry.RegisterSpriteFrames("missing_frames", "res://nonexistent_dir/nonexistent.res");
            ResourceRegistry.RegisterMaterial("missing_mat", "res://nonexistent_dir/nonexistent.material");

            var icon = ResourceRegistry.GetIcon("missing_icon");
            var frames = ResourceRegistry.GetSpriteFrames("missing_frames");
            var mat = ResourceRegistry.GetMaterial("missing_mat");

            if (icon != null)
                failures.Add("[RegisterPath-Icon] GetIcon for nonexistent path should return null");
            if (frames != null)
                failures.Add("[RegisterPath-Frames] GetSpriteFrames for nonexistent path should return null");
            if (mat != null)
                failures.Add("[RegisterPath-Mat] GetMaterial for nonexistent path should return null");

            // 路径注册但 GD.Load 失败 → TryGet 返回 false
            bool found = ResourceRegistry.TryGet<Texture2D>("missing_icon", out var _);
            if (found)
                failures.Add("[RegisterPath-TryGet] TryGet for nonexistent path should return false");
        }
        catch (Exception ex)
        {
            failures.Add($"[RegisterPath-Crash] Path register+get threw: {ex.Message}");
        }
    }

    // ====================================================================
    // Test 3: Register (对象) + Get (缓存命中)
    // ====================================================================

    private static void TestRegisterObject(List<string> failures)
    {
        try
        {
            ResourceRegistry.Clear();

            // 直接注册 Resource 对象
            var frames = new SpriteFrames();
            var mat = new StandardMaterial3D();
            ResourceRegistry.RegisterSpriteFrames("obj_frames", frames);
            ResourceRegistry.RegisterMaterial("obj_mat", mat);

            // 对象注册后 Get 应直接命中缓存
            var gotFrames = ResourceRegistry.GetSpriteFrames("obj_frames");
            if (gotFrames == null)
                failures.Add("[RegisterObj-Frames] GetSpriteFrames returned null after object Register");
            else if (!ReferenceEquals(gotFrames, frames))
                failures.Add("[RegisterObj-Frames] GetSpriteFrames returned different instance");

            var gotMat = ResourceRegistry.GetMaterial("obj_mat");
            if (gotMat == null)
                failures.Add("[RegisterObj-Mat] GetMaterial returned null after object Register");
            else if (!ReferenceEquals(gotMat, mat))
                failures.Add("[RegisterObj-Mat] GetMaterial returned different instance");

            // TryGet 泛型
            bool foundFrames = ResourceRegistry.TryGet<SpriteFrames>("obj_frames", out var typedFrames);
            if (!foundFrames || typedFrames == null)
                failures.Add("[RegisterObj-TryGet] TryGet<SpriteFrames> should succeed");

            // 注册后 Clear 应移除
            ResourceRegistry.Clear();
            var afterClear = ResourceRegistry.GetSpriteFrames("obj_frames");
            if (afterClear != null)
                failures.Add("[RegisterObj-Clear] GetSpriteFrames should return null after Clear()");
        }
        catch (Exception ex)
        {
            failures.Add($"[RegisterObj-Crash] Object register+get threw: {ex.Message}");
        }
    }

    /// <summary>
    /// 快速 Miss 检测（无 Engine 依赖，适合 pre-commit hook）
    /// </summary>
    public static bool QuickMissCheck()
    {
        ResourceRegistry.Clear();
        return ResourceRegistry.GetIcon("any_id") == null
            && ResourceRegistry.GetSpriteFrames("any_id") == null
            && ResourceRegistry.GetMaterial("any_id") == null;
    }
}
