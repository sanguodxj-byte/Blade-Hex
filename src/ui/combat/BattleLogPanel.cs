using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗日志面板 — 滚动文本显示攻击结果、伤害数字、状态变化
/// 迁移自 GDScript BattleLogPanel.gd
/// </summary>
public partial class BattleLogPanel : PanelContainer
{
    private const int MaxEntries = 200;
    
    private RichTextLabel _logLabel = null!;
    private ScrollContainer _scroll = null!;
    private readonly List<string> _entries = new();
    private readonly UIFactory _factory = new();

    private Timer _fadeTimer = null!;
    private Tween? _fadeTween;
    private bool _isHovered = false;

    public override void _Ready()
    {
        Setup();

        _fadeTimer = new Timer
        {
            WaitTime = 4.0f,
            OneShot = true
        };
        _fadeTimer.Timeout += OnFadeTimerTimeout;
        AddChild(_fadeTimer);

        MouseEntered += () =>
        {
            _isHovered = true;
            WakeUp();
        };
        MouseExited += () =>
        {
            _isHovered = false;
            _fadeTimer.Start();
        };

        Modulate = new Color(1, 1, 1, 0);
    }

    private void Setup()
    {
        CustomMinimumSize = new Vector2(0, 100);
        AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(UITheme.Instance.BgTertiary, UITheme.Instance.BorderDefault, 1, UITheme.Instance.RadiusMd));

        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        AddChild(_scroll);

        _logLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            ScrollFollowing = true,
            FitContent = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 80)
        };
        _scroll.AddChild(_logLabel);
    }

    public void AddEntry(string text, string category = "info")
    {
        WakeUp();

        string bbcode = FormatEntry(text, category);
        _entries.Add(bbcode);

        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(0);
        }

        RefreshDisplay();
    }

    private void WakeUp()
    {
        _fadeTween?.Kill();
        Modulate = new Color(1, 1, 1, 1);
        if (!_isHovered) _fadeTimer.Start();
    }

    private void OnFadeTimerTimeout()
    {
        if (_isHovered) return;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 0.0f, 1.0f);
    }

    private void RefreshDisplay()
    {
        _logLabel.Text = string.Join("\n", _entries);
        
        // 自动滚动到底部 (需要等待一帧让布局更新)
        CallDeferred(nameof(ScrollToBottom));
    }

    private void ScrollToBottom()
    {
        var vScrollBar = _scroll.GetVScrollBar();
        _scroll.ScrollVertical = (int)vScrollBar.MaxValue;
    }

    private string FormatEntry(string text, string category)
    {
        Color color = category switch
        {
            "hit" => UITheme.Instance.TextPositive,
            "miss" => UITheme.Instance.TextNegative,
            "critical" => UITheme.Instance.TextAccent,
            "spell" => UITheme.Instance.TextMagic,
            "turn" => UITheme.Instance.TextAccent,
            "move" => UITheme.Instance.TextMuted,
            _ => UITheme.Instance.TextSecondary
        };
        return $"[color=#{color.ToHtml(false)}]{text}[/color]";
    }
}
