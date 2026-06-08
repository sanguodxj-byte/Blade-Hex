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
using System.IO;
using System.Text.RegularExpressions;
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
        yield return Run(nameof(OverlayPanels_DoNotUseBareCenterAnchors), OverlayPanels_DoNotUseBareCenterAnchors);
        yield return Run(nameof(InteractionPanel_DoesNotUseLegacyBuildContent), InteractionPanel_DoesNotUseLegacyBuildContent);
        yield return Run(nameof(InteractionPanel_UsesTownPanelBaseLayout), InteractionPanel_UsesTownPanelBaseLayout);
        yield return Run(nameof(QuestManager_CompletionWaitsForRewardClaim), QuestManager_CompletionWaitsForRewardClaim);
        yield return Run(nameof(OverworldQuestLoop_HasTargetAndCombatWiring), OverworldQuestLoop_HasTargetAndCombatWiring);
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

    private static (bool, string) OverlayPanels_DoNotUseBareCenterAnchors()
    {
        string? uiRoot = FindFrontendUiRoot();
        if (uiRoot == null) return (true, "");

        var pattern = new Regex(
            @"SetAnchorsPreset\s*\(\s*(?:Control\.)?LayoutPreset\.Center\s*\)",
            RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories))
        {
            int lineNo = 0;
            foreach (string line in File.ReadLines(file))
            {
                lineNo++;
                if (!pattern.IsMatch(line)) continue;

                string relative = Path.GetRelativePath(uiRoot, file);
                offenders.Add($"{relative}:{lineNo}: {line.Trim()}");
                if (offenders.Count >= 12) break;
            }

            if (offenders.Count >= 12) break;
        }

        if (offenders.Count > 0)
        {
            return (false,
                "Use OverlayPanelLayout.Center/AttachCentered instead of anchors-only Center:\n" +
                string.Join("\n", offenders));
        }

        return (true, "");
    }

    private static (bool, string) InteractionPanel_DoesNotUseLegacyBuildContent()
    {
        string? uiRoot = FindFrontendUiRoot();
        if (uiRoot == null) return (true, "");

        string file = Path.Combine(uiRoot, "Overworld", "InteractionPanel.cs");
        if (!File.Exists(file)) return (true, "");

        var pattern = new Regex(
            @"override\s+void\s+BuildContent\s*\(\s*VBoxContainer\s+\w+\s*\)",
            RegexOptions.Compiled);

        int lineNo = 0;
        foreach (string line in File.ReadLines(file))
        {
            lineNo++;
            if (!pattern.IsMatch(line)) continue;

            return (false,
                "InteractionPanel must fill POIPanelBase through data hooks and PopulateActions, not BuildContent(): " +
                $"Overworld/InteractionPanel.cs:{lineNo}: {line.Trim()}");
        }

        return (true, "");
    }

    private static (bool, string) InteractionPanel_UsesTownPanelBaseLayout()
    {
        string? uiRoot = FindFrontendUiRoot();
        if (uiRoot == null) return (true, "");

        string file = Path.Combine(uiRoot, "Overworld", "InteractionPanel.cs");
        if (!File.Exists(file)) return (true, "");

        var pattern = new Regex(
            @"override\s+int\s+(PanelWidth|PanelHeight|PanelMargin)\s*=>",
            RegexOptions.Compiled);

        int lineNo = 0;
        foreach (string line in File.ReadLines(file))
        {
            lineNo++;
            if (!pattern.IsMatch(line)) continue;

            return (false,
                "InteractionPanel should share TownPanel/POIPanelBase layout sizing instead of defining custom panel geometry: " +
                $"Overworld/InteractionPanel.cs:{lineNo}: {line.Trim()}");
        }

        return (true, "");
    }

    private static (bool, string) QuestManager_CompletionWaitsForRewardClaim()
    {
        var manager = new QuestManager();
        var quest = new QuestData
        {
            QuestId = "test_quest_claim",
            QuestName = "测试委托",
            TargetDescription = "测试目标",
            TargetWorldPosition = new Vector2(100, 100),
            TargetCount = 1,
            RewardGold = 123,
            RewardReputation = 7,
            RewardFaction = "test_faction",
        };

        if (!manager.AcceptQuest(quest)) return (false, "AcceptQuest returned false");
        manager.UpdateQuestProgress(quest.QuestId, 1);

        if (manager.ActiveQuests.Contains(quest)) return (false, "completed quest still active");
        if (!manager.RewardReadyQuests.Contains(quest)) return (false, "completed quest not waiting for reward claim");
        if (manager.CompletedQuestIds.Contains(quest.QuestId)) return (false, "quest marked completed before reward claim");

        if (!manager.ClaimReward(quest.QuestId)) return (false, "ClaimReward returned false");
        if (manager.RewardReadyQuests.Contains(quest)) return (false, "claimed quest still in reward-ready list");
        if (!manager.CompletedQuestIds.Contains(quest.QuestId)) return (false, "claimed quest not recorded as completed");
        if (manager.PlayerGold != quest.RewardGold) return (false, $"PlayerGold expected {quest.RewardGold}, got {manager.PlayerGold}");

        return (true, "");
    }

    private static (bool, string) OverworldQuestLoop_HasTargetAndCombatWiring()
    {
        string? root = FindRepoRoot();
        if (root == null) return (true, "");

        string entities = Path.Combine(root, "BladeHexFrontend", "src", "Scenes", "overworld2d", "OverworldScene2D.Entities.cs");
        string battleContext = Path.Combine(root, "BladeHexCore", "src", "Strategic", "BattleContext.cs");
        string questBoard = Path.Combine(root, "BladeHexFrontend", "src", "View", "UI", "Overworld", "QuestBoardPanel.cs");
        if (!File.Exists(entities) || !File.Exists(battleContext) || !File.Exists(questBoard)) return (true, "");

        string e = File.ReadAllText(entities);
        string b = File.ReadAllText(battleContext);
        string q = File.ReadAllText(questBoard);

        var missing = new List<string>();
        if (!e.Contains("QuestTargetSpawned += OnQuestTargetSpawned")) missing.Add("QuestTargetSpawned subscription");
        if (!e.Contains("QuestTargetCleared += OnQuestTargetCleared")) missing.Add("QuestTargetCleared subscription");
        if (!e.Contains("CheckQuestTargets()")) missing.Add("quest target proximity check");
        if (!e.Contains("BuildEnemyUnitsFromQuestTarget")) missing.Add("quest target enemy generation");
        if (!e.Contains("CompleteQuestBattleTarget")) missing.Add("quest combat victory progress writeback");
        if (!b.Contains("public string QuestId")) missing.Add("BattleContext.QuestId");
        if (!q.Contains("ClaimQuestReward")) missing.Add("quest reward claim UI");

        if (missing.Count > 0)
            return (false, "Missing quest loop wiring: " + string.Join(", ", missing));

        return (true, "");
    }

    private static string? FindFrontendUiRoot()
    {
        var candidates = new List<string>();

        AddFrontendUiRootCandidates(candidates, ProjectSettings.GlobalizePath("res://"));
        AddFrontendUiRootCandidates(candidates, Directory.GetCurrentDirectory());

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            AddFrontendUiRootCandidates(candidates, dir.FullName);

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string? FindRepoRoot()
    {
        var candidates = new List<string>
        {
            ProjectSettings.GlobalizePath("res://"),
            Directory.GetCurrentDirectory(),
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            candidates.Add(dir.FullName);

        foreach (string candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "BladeHexCore", "src", "Data", "QuestData.cs")) &&
                Directory.Exists(Path.Combine(candidate, "BladeHexFrontend")))
                return candidate;
        }

        return null;
    }

    private static void AddFrontendUiRootCandidates(List<string> candidates, string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return;

        candidates.Add(Path.Combine(root, "BladeHexFrontend", "src", "View", "UI"));
        candidates.Add(Path.Combine(root, "src", "View", "UI"));
    }
}
