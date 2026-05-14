// ToastNotification.cs
// 大地图消息提示 — 透明面板，淡入淡出，不拦截输入
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 轻量消息提示系统 — 在屏幕下方居中显示短暂消息。
/// 支持队列（多条消息依次显示），淡入淡出动画，完全不拦截鼠标/键盘输入。
/// </summary>
[GlobalClass]
public partial class ToastNotification : CanvasLayer
{
    // ========================================
    // 配置
    // ========================================

    [Export] public float FadeInDuration { get; set; } = 0.3f;
    [Export] public float DisplayDuration { get; set; } = 2.5f;
    [Export] public float FadeOutDuration { get; set; } = 0.8f;
    [Export] public int MaxVisibleToasts { get; set; } = 3;

    // ========================================
    // 内部
    // ========================================

    private VBoxContainer _container = null!;
    private readonly Queue<string> _pendingMessages = new();
    private int _activeCount;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        Layer = 15; // 在 UI 之上但不影响交互

        // 容器：屏幕底部居中，向上堆叠
        _container = new VBoxContainer();
        _container.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _container.GrowVertical = Control.GrowDirection.Begin;
        _container.GrowHorizontal = Control.GrowDirection.Both;
        _container.OffsetBottom = -120; // 距底部 120px
        _container.OffsetTop = -300;
        _container.OffsetLeft = -300;
        _container.OffsetRight = 300;
        _container.AddThemeConstantOverride("separation", 8);
        _container.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_container);
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>显示一条消息</summary>
    public void Show(string message)
    {
        if (_activeCount >= MaxVisibleToasts)
        {
            _pendingMessages.Enqueue(message);
            return;
        }

        SpawnToast(message);
    }

    /// <summary>显示带颜色的消息</summary>
    public void Show(string message, Color color)
    {
        if (_activeCount >= MaxVisibleToasts)
        {
            _pendingMessages.Enqueue(message);
            return;
        }

        SpawnToast(message, color);
    }

    /// <summary>显示带图标的消息</summary>
    public void ShowWithIcon(string icon, string message, Color? color = null)
    {
        Show($"{icon} {message}", color ?? Colors.White);
    }

    // ========================================
    // 内部
    // ========================================

    private void SpawnToast(string message, Color? textColor = null)
    {
        _activeCount++;

        var label = new Label();
        label.Text = message;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", textColor ?? new Color(0.95f, 0.93f, 0.85f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.Modulate = new Color(1, 1, 1, 0); // 初始透明

        _container.AddChild(label);

        // 动画：淡入 → 停留 → 淡出 → 移除
        var tween = CreateTween();
        tween.TweenProperty(label, "modulate:a", 1.0f, FadeInDuration)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(DisplayDuration);
        tween.TweenProperty(label, "modulate:a", 0.0f, FadeOutDuration)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            label.QueueFree();
            _activeCount--;
            TryShowNext();
        }));
    }

    private void TryShowNext()
    {
        if (_pendingMessages.Count > 0 && _activeCount < MaxVisibleToasts)
        {
            SpawnToast(_pendingMessages.Dequeue());
        }
    }
}
