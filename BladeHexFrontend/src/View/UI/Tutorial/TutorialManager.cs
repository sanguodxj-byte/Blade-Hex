// TutorialManager.cs
// 教程管理器 — 控制教程的触发、显示和已读状态
// 作为 Autoload 单例运行，跨场景持久
using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace BladeHex.UI.Tutorial;

/// <summary>
/// [Autoload Singleton] 教程管理器。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>TutorialManager</c>。</para>
/// <para>职责：加载教程数据、管理已读状态、响应触发事件显示教程面板。</para>
/// </summary>
[GlobalClass]
public partial class TutorialManager : Node
{
    // ============================================================================
    // 单例
    // ============================================================================

    public static TutorialManager? Instance { get; private set; }

    private const string DataPath = "res://BladeHexFrontend/src/View/UI/Tutorial/tutorial_pages.json";
    private const string SavePath = "user://tutorial_progress.json";

    // ============================================================================
    // 状态
    // ============================================================================

    private TutorialDataRoot _data = new();
    private HashSet<string> _completedChapters = new();
    private bool _tutorialEnabled = true;
    private TutorialPanel? _panel;
    private readonly Queue<TutorialChapter> _queue = new();

    /// <summary>教程是否已启用</summary>
    public bool IsEnabled => _tutorialEnabled;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
        LoadData();
        LoadProgress();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>启用/禁用教程系统</summary>
    public void SetEnabled(bool enabled)
    {
        _tutorialEnabled = enabled;
        SaveProgress();

        if (!enabled && _panel != null && _panel.IsVisible)
        {
            // 禁用时关闭当前面板
            _panel.QueueFree();
            _panel = null;
        }
    }

    /// <summary>
    /// 触发指定类型的教程。如果该章节未完成且教程已启用，则显示。
    /// </summary>
    /// <param name="trigger">触发标识（如 "new_game", "first_combat" 等）</param>
    public void Trigger(string trigger)
    {
        if (!_tutorialEnabled) return;

        foreach (var chapter in _data.Chapters)
        {
            if (chapter.Trigger == trigger && !_completedChapters.Contains(chapter.Id))
            {
                _queue.Enqueue(chapter);
            }
        }

        TryShowNext();
    }

    /// <summary>显示新游戏教程确认对话框</summary>
    public void ShowNewGamePrompt()
    {
        var dialog = new TutorialPromptDialog();
        dialog.Confirmed += (enabled) =>
        {
            SetEnabled(enabled);
            if (enabled)
            {
                // 延迟一帧再触发，让对话框先消失
                CallDeferred(nameof(TriggerNewGame));
            }
        };
        GetTree().Root.AddChild(dialog);
    }

    private void TriggerNewGame()
    {
        Trigger("new_game");
    }

    /// <summary>重置所有教程进度（用于调试或新游戏）</summary>
    public void ResetProgress()
    {
        _completedChapters.Clear();
        SaveProgress();
    }

    /// <summary>标记章节为已完成</summary>
    public void MarkCompleted(string chapterId)
    {
        _completedChapters.Add(chapterId);
        SaveProgress();
    }

    /// <summary>检查章节是否已完成</summary>
    public bool IsCompleted(string chapterId) => _completedChapters.Contains(chapterId);

    // ============================================================================
    // 内部逻辑
    // ============================================================================

    private void TryShowNext()
    {
        if (_panel != null && _panel.IsVisible) return; // 当前有面板显示中
        if (_queue.Count == 0) return;

        var chapter = _queue.Dequeue();
        ShowChapter(chapter);
    }

    private void ShowChapter(TutorialChapter chapter)
    {
        if (_panel != null)
        {
            _panel.QueueFree();
        }

        _panel = new TutorialPanel();
        _panel.Closed += OnPanelClosed;
        GetTree().Root.AddChild(_panel);

        // 延迟一帧让面板 _Ready 执行完
        CallDeferred(nameof(DeferredShowChapter), chapter.Id);
    }

    private void DeferredShowChapter(string chapterId)
    {
        if (_panel == null) return;

        foreach (var ch in _data.Chapters)
        {
            if (ch.Id == chapterId)
            {
                _panel.ShowChapter(ch);
                break;
            }
        }
    }

    private void OnPanelClosed(string chapterId)
    {
        MarkCompleted(chapterId);

        // 尝试显示队列中的下一个
        CallDeferred(nameof(TryShowNext));
    }

    // ============================================================================
    // 数据加载/保存
    // ============================================================================

    private void LoadData()
    {
        if (!FileAccess.FileExists(DataPath))
        {
            GD.PushWarning($"[TutorialManager] 教程数据文件不存在: {DataPath}");
            return;
        }

        using var file = FileAccess.Open(DataPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushWarning($"[TutorialManager] 无法打开教程数据: {DataPath}");
            return;
        }

        var json = file.GetAsText();
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var root = JsonSerializer.Deserialize<TutorialDataRoot>(json, options);
            if (root != null)
                _data = root;
            GD.Print($"[TutorialManager] 加载教程数据: {_data.Chapters.Count} 章节");
        }
        catch (System.Exception e)
        {
            GD.PushError($"[TutorialManager] 解析教程数据失败: {e.Message}");
        }
    }

    private void LoadProgress()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            _tutorialEnabled = true;
            return;
        }

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = file.GetAsText();
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var progress = JsonSerializer.Deserialize<TutorialProgress>(json, options);
            if (progress != null)
            {
                _tutorialEnabled = progress.Enabled;
                _completedChapters = new HashSet<string>(progress.Completed);
            }
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[TutorialManager] 加载教程进度失败: {e.Message}");
        }
    }

    private void SaveProgress()
    {
        var progress = new TutorialProgress
        {
            Enabled = _tutorialEnabled,
            Completed = new List<string>(_completedChapters)
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(progress, options);

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }

    // ============================================================================
    // 进度数据模型
    // ============================================================================

    private class TutorialProgress
    {
        public bool Enabled { get; set; } = true;
        public List<string> Completed { get; set; } = new();
    }
}
