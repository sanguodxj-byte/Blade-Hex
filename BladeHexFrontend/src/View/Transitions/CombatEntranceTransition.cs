// CombatEntranceTransition.cs
// 战斗场景入场 UI 动画
// 1. 屏幕从黑渐显
// 2. 角色面板从左向右快速滑入
// 3. 顶部 UI（回合条/敌方信息）从右向左滑入
// 4. 小地图从下方滑入就位
using Godot;
using System;

namespace BladeHex.View.Transitions;

/// <summary>
/// 战斗场景入场过渡动画 — 在战斗场景 _Ready 后播放。
/// </summary>
public partial class CombatEntranceTransition : CanvasLayer
{
    // ========================================
    // 配置
    // ========================================
    private const float FadeInDuration = 0.4f;     // 淡入时长
    private const float UiEnterDuration = 0.5f;    // UI 进入时长
    private const float Stagger = 0.1f;            // 元素间延迟

    // ========================================
    // 引用
    // ========================================
    private ColorRect? _fadeRect;
    private Action? _onComplete;

    // ========================================
    // 公开接口
    // ========================================

    /// <summary>
    /// 播放战斗入场动画。
    /// </summary>
    /// <param name="bottomPanel">底部角色面板（从左滑入）</param>
    /// <param name="topBar">顶部回合条/敌方信息区（从右滑入）</param>
    /// <param name="minimapControl">小地图控件（从下方滑入）</param>
    /// <param name="onComplete">动画完成回调</param>
    public void Play(Control? bottomPanel, Control? topBar, Control? minimapControl, Action? onComplete = null)
    {
        _onComplete = onComplete;
        Layer = 100;

        // 全屏黑遮罩（初始不透明）
        _fadeRect = new ColorRect();
        _fadeRect.Color = new Color(0, 0, 0, 1);
        _fadeRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_fadeRect);

        RunEntrance(bottomPanel, topBar, minimapControl);
    }

    // ========================================
    // 动画序列
    // ========================================

    private async void RunEntrance(Control? bottomPanel, Control? topBar, Control? minimapControl)
    {
        var tree = GetTree();
        if (tree == null) { Finish(); return; }

        // 先把 UI 元素移到起始位置（屏幕外）
        var viewport = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

        float bottomOriginalX = 0;
        if (bottomPanel != null)
        {
            bottomOriginalX = bottomPanel.Position.X;
            bottomPanel.Position = new Vector2(-bottomPanel.Size.X - 50, bottomPanel.Position.Y);
        }

        float topOriginalX = 0;
        if (topBar != null)
        {
            topOriginalX = topBar.Position.X;
            topBar.Position = new Vector2(viewport.X + 50, topBar.Position.Y);
        }

        float minimapOriginalY = 0;
        if (minimapControl != null)
        {
            minimapOriginalY = minimapControl.Position.Y;
            minimapControl.Position = new Vector2(minimapControl.Position.X, viewport.Y + 50);
        }

        // ── 1. 淡入 ──
        var fadeTween = CreateTween();
        fadeTween.TweenProperty(_fadeRect, "color", new Color(0, 0, 0, 0), FadeInDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(fadeTween, Tween.SignalName.Finished);

        // ── 2. UI 元素滑入（并行） ──
        var enterTween = CreateTween();
        enterTween.SetParallel(true);

        // 底部面板从左到右
        if (bottomPanel != null)
        {
            enterTween.TweenProperty(bottomPanel, "position:x", bottomOriginalX, UiEnterDuration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        }

        // 顶部从右到左
        if (topBar != null)
        {
            enterTween.TweenProperty(topBar, "position:x", topOriginalX, UiEnterDuration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out)
                .SetDelay(Stagger);
        }

        // 小地图从下到上
        if (minimapControl != null)
        {
            enterTween.TweenProperty(minimapControl, "position:y", minimapOriginalY, UiEnterDuration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out)
                .SetDelay(Stagger * 2);
        }

        enterTween.SetParallel(false);
        await ToSignal(enterTween, Tween.SignalName.Finished);

        // 移除遮罩并销毁自身
        Finish();
    }

    private void Finish()
    {
        _onComplete?.Invoke();
        QueueFree();
    }
}
