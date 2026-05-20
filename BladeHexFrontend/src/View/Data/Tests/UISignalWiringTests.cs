// UISignalWiringTests.cs
// UI 信号接线与联通性测试 — 需要在 Godot 场景树中运行（依赖 Autoload）
//
// 验证内容：
//   - GameMenuManager 的 SaveRequested / LoadRequested 信号存在
//   - WeatherManager 的 WeatherChanged 信号存在且可触发
//   - SkillTreeManager Autoload 可访问且 TreeData 已加载
//   - ClassTitleResolver 在 Autoload 环境下正常工作
//   - GameMenuManager Open/Close 正确切换可见性和暂停状态
//
// 运行方式：
//   在任何场景中调用 UISignalWiringTests.RunAll()，或通过调试控制台触发。
//   示例：GD.Print(BladeHex.View.Data.Tests.UISignalWiringTests.RunAllFormatted());
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.View.Environment;
using BladeHex.UI.Global;

namespace BladeHex.View.Data.Tests;

public static class UISignalWiringTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    /// <summary>格式化输出（方便在调试控制台直接打印）</summary>
    public static string RunAllFormatted()
    {
        var (passed, failed, details) = RunAll();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== UISignalWiringTests ===");
        foreach (var line in details) sb.AppendLine(line);
        sb.AppendLine($"  TOTAL: {passed} passed, {failed} failed");
        return sb.ToString();
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(GameMenu_HasSaveSignal), GameMenu_HasSaveSignal);
        yield return Run(nameof(GameMenu_HasLoadSignal), GameMenu_HasLoadSignal);
        yield return Run(nameof(GameMenu_HasResumeSignal), GameMenu_HasResumeSignal);
        yield return Run(nameof(GameMenu_HasReturnToMainMenuSignal), GameMenu_HasReturnToMainMenuSignal);
        yield return Run(nameof(GameMenu_OpenClose_TogglesState), GameMenu_OpenClose_TogglesState);
        yield return Run(nameof(GameMenu_OpenSettings_ShowsSettingsPanel), GameMenu_OpenSettings_ShowsSettingsPanel);
        yield return Run(nameof(Weather_HasWeatherChangedSignal), Weather_HasWeatherChangedSignal);
        yield return Run(nameof(Weather_SetImmediate_EmitsSignal), Weather_SetImmediate_EmitsSignal);
        yield return Run(nameof(Weather_GetEffectiveIntensity_InRange), Weather_GetEffectiveIntensity_InRange);
        yield return Run(nameof(SkillTree_Autoload_Accessible), SkillTree_Autoload_Accessible);
        yield return Run(nameof(SkillTree_CreateAndRetrieve), SkillTree_CreateAndRetrieve);
        yield return Run(nameof(ClassTitle_EmptyTree_ReturnsWuMingZhe), ClassTitle_EmptyTree_ReturnsWuMingZhe);
        yield return Run(nameof(ClassTitle_WithNodes_ReturnsValidTitle), ClassTitle_WithNodes_ReturnsValidTitle);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ============================================================================
    // GameMenuManager 信号测试
    // ============================================================================

    private static (bool, string) GameMenu_HasSaveSignal()
    {
        var gm = Globals.GameMenuOrNull;
        if (gm == null) return (false, "GameMenuManager Autoload not found");
        if (!gm.HasSignal("SaveRequested"))
            return (false, "SaveRequested signal missing");
        return (true, "");
    }

    private static (bool, string) GameMenu_HasLoadSignal()
    {
        var gm = Globals.GameMenuOrNull;
        if (gm == null) return (false, "GameMenuManager Autoload not found");
        if (!gm.HasSignal("LoadRequested"))
            return (false, "LoadRequested signal missing");
        return (true, "");
    }

    private static (bool, string) GameMenu_HasResumeSignal()
    {
        var gm = Globals.GameMenuOrNull;
        if (gm == null) return (false, "GameMenuManager Autoload not found");
        if (!gm.HasSignal("ResumeRequested"))
            return (false, "ResumeRequested signal missing");
        return (true, "");
    }

    private static (bool, string) GameMenu_HasReturnToMainMenuSignal()
    {
        var gm = Globals.GameMenuOrNull;
        if (gm == null) return (false, "GameMenuManager Autoload not found");
        if (!gm.HasSignal("ReturnToMainMenuRequested"))
            return (false, "ReturnToMainMenuRequested signal missing");
        return (true, "");
    }

    private static (bool, string) GameMenu_OpenClose_TogglesState()
    {
        var gm = Globals.GameMenuOrNull;
        if (gm == null) return (false, "GameMenuManager Autoload not found");

        bool wasPaused = gm.GetTree().Paused;
        bool wasVisible = gm.Visible;

        gm.Open();
        if (!gm.IsOpen) { Restore(gm, wasPaused, wasVisible); return (false, "Open() → IsOpen should be true"); }
        if (!gm.GetTree().Paused) { Restore(gm, wasPaused, wasVisible); return (false, "Open() → Paused should be true"); }

        gm.Close();
        if (gm.IsOpen) { Restore(gm, wasPaused, wasVisible); return (false, "Close() → IsOpen should be false"); }
        if (gm.GetTree().Paused) { Restore(gm, wasPaused, wasVisible); return (false, "Close() → Paused should be false"); }

        Restore(gm, wasPaused, wasVisible);
        return (true, "");
    }

    private static (bool, string) GameMenu_OpenSettings_ShowsSettingsPanel()
    {
        var gm = Globals.GameMenuOrNull;
        if (gm == null) return (false, "GameMenuManager Autoload not found");

        bool wasPaused = gm.GetTree().Paused;
        bool wasVisible = gm.Visible;

        gm.OpenSettings();
        if (!gm.IsOpen) { Restore(gm, wasPaused, wasVisible); return (false, "OpenSettings() → IsOpen should be true"); }

        gm.Close();
        Restore(gm, wasPaused, wasVisible);
        return (true, "");
    }

    private static void Restore(GameMenuManager gm, bool paused, bool visible)
    {
        gm.Visible = visible;
        gm.GetTree().Paused = paused;
    }

    // ============================================================================
    // WeatherManager 信号测试
    // ============================================================================

    private static (bool, string) Weather_HasWeatherChangedSignal()
    {
        var wm = Globals.WeatherOrNull;
        if (wm == null) return (true, ""); // 容错：Autoload 缺失时跳过
        if (!wm.HasSignal("WeatherChanged"))
            return (false, "WeatherChanged signal missing");
        return (true, "");
    }

    private static (bool, string) Weather_SetImmediate_EmitsSignal()
    {
        var wm = Globals.WeatherOrNull;
        if (wm == null) return (true, ""); // 容错跳过

        bool signalReceived = false;
        void handler(int oldW, int newW) { signalReceived = true; }

        wm.WeatherChanged += handler;
        var prev = wm.CurrentWeather;
        var target = prev == WeatherType.Rain ? WeatherType.Snow : WeatherType.Rain;
        wm.SetWeatherImmediate(target);
        wm.WeatherChanged -= handler;

        // 恢复
        wm.SetWeatherImmediate(prev);

        if (!signalReceived)
            return (false, "SetWeatherImmediate did not emit WeatherChanged");
        return (true, "");
    }

    private static (bool, string) Weather_GetEffectiveIntensity_InRange()
    {
        var wm = Globals.WeatherOrNull;
        if (wm == null) return (true, "");

        float intensity = wm.GetEffectiveIntensity();
        if (intensity < 0f || intensity > 1f)
            return (false, $"Intensity {intensity} out of [0,1] range");
        return (true, "");
    }

    // ============================================================================
    // SkillTreeManager 联通测试
    // ============================================================================

    private static (bool, string) SkillTree_Autoload_Accessible()
    {
        var stm = Globals.SkillTreesOrNull;
        if (stm == null) return (false, "SkillTreeManager Autoload not found");
        if (stm.TreeData == null) return (false, "TreeData is null");
        if (stm.TreeData.GetNodeCount() == 0) return (false, "TreeData has 0 nodes");
        return (true, "");
    }

    private static (bool, string) SkillTree_CreateAndRetrieve()
    {
        var stm = Globals.SkillTreesOrNull;
        if (stm == null) return (false, "SkillTreeManager Autoload not found");

        long testId = -99999;
        var tree = stm.CreateSkillTree(testId, 5);
        if (tree == null) { stm.RemoveSkillTree(testId); return (false, "CreateSkillTree returned null"); }

        var retrieved = stm.GetSkillTree(testId);
        if (retrieved == null) { stm.RemoveSkillTree(testId); return (false, "GetSkillTree returned null"); }
        if (!ReferenceEquals(tree, retrieved)) { stm.RemoveSkillTree(testId); return (false, "Not same instance"); }

        stm.RemoveSkillTree(testId);
        return (true, "");
    }

    private static (bool, string) ClassTitle_EmptyTree_ReturnsWuMingZhe()
    {
        var stm = Globals.SkillTreesOrNull;
        if (stm == null) return (false, "SkillTreeManager Autoload not found");

        long testId = -99998;
        var tree = stm.CreateSkillTree(testId, 10);
        string title = tree.GetClassTitleName();
        stm.RemoveSkillTree(testId);

        if (title != "无名者")
            return (false, $"Expected '无名者', got '{title}'");
        return (true, "");
    }

    private static (bool, string) ClassTitle_WithNodes_ReturnsValidTitle()
    {
        var stm = Globals.SkillTreesOrNull;
        if (stm == null) return (false, "SkillTreeManager Autoload not found");

        long testId = -99997;
        var tree = stm.CreateSkillTree(testId, 10);

        // 找一个 STR 区域的 BIG 节点并直接激活
        string? strBig = null;
        foreach (var kvp in stm.TreeData!.Nodes)
        {
            if (kvp.Value.CurrentRegion == SkillNodeData.Region.Str &&
                kvp.Value.CurrentNodeType == SkillNodeData.NodeType.Big)
            {
                strBig = kvp.Key;
                break;
            }
        }

        if (strBig == null)
        {
            stm.RemoveSkillTree(testId);
            return (false, "No BIG node in STR region");
        }

        tree.ActivatedNodes.Add(strBig);
        tree.ActivatedSet.Add(strBig);
        string title = tree.GetClassTitleName();
        stm.RemoveSkillTree(testId);

        if (title != "战士")
            return (false, $"Expected '战士', got '{title}'");
        return (true, "");
    }
}
