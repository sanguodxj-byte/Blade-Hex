// SceneTransition.cs
// 切换场景前的统一清理工具：把所有"游离在 /root 下、并非自动加载、并非当前场景"的节点全部释放。
// 解决场景手动 AddChild 到 Root 后再调用 ChangeSceneToFile 导致旧场景/UI 残留的问题。
using Godot;
using System.Collections.Generic;

namespace BladeHex.View;

public static class SceneTransition
{
    /// <summary>
    /// /root 下需要保留的节点名称（自动加载 + 加载屏 + 主视口必备）。
    /// 这些节点不会被清理。
    /// </summary>
    private static readonly HashSet<string> Persistent = new()
    {
        // Autoloads（必须与 project.godot [autoload] 同步）
        "GlobalState",
        "SkillTreeManager",
        "EventBus",
        "AudioManager",
        "AudioEventReactor",
        "UITheme",
        "DebugConsole",
        "GameMenuManager",
        // 加载屏（singleton，跨场景复用）
        "LoadingScreen",
    };

    /// <summary>
    /// 切换到指定场景文件。先释放 /root 下所有非保留、非当前场景的节点，再切换场景。
    /// 适用于战斗场景被手动 AddChild 到 Root 后的清理。
    /// </summary>
    public static void ChangeSceneTo(SceneTree tree, string scenePath)
    {
        CleanupOrphanNodes(tree);
        // 切换前一定要解除暂停，避免新场景被冻住
        tree.Paused = false;
        tree.ChangeSceneToFile(scenePath);
    }

    /// <summary>
    /// 释放 /root 下所有非保留、非当前场景的节点。
    /// 这些通常是手动 AddChild 上去的额外场景（战斗场景、临时 UI）。
    /// </summary>
    public static void CleanupOrphanNodes(SceneTree tree)
    {
        var root = tree.Root;
        var current = tree.CurrentScene;

        // 收集要清理的节点（不能在迭代时直接 QueueFree）
        var toFree = new List<Node>();
        foreach (var child in root.GetChildren())
        {
            if (child == current) continue;
            if (Persistent.Contains(child.Name)) continue;
            toFree.Add(child);
        }

        foreach (var n in toFree)
        {
            // 切场景时立刻断开正在播放的音乐，避免残音
            if (n is AudioStreamPlayer asp) asp.Stop();
            n.QueueFree();
        }
    }
}
