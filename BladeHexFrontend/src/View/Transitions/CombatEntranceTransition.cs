// CombatEntranceTransition.cs
// 战斗场景入场 UI 动画
// 1. 屏幕从黑渐显
// 2. 底部面板从下方滑入就位
// 3. 回合顺序栏从下方滑入就位（略有延迟）
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
    private const float Stagger = 0.08f;           // 元素间延迟
    private const float SlideOffset = 120f;        // 滑入偏移量(像素)

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
    /// <param name="bottomPanel">底部角色面板（从下方滑入）</param>
    /// <param name="topBar">回合顺序栏（从下方滑入，略有延迟）</param>
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

        // 初始状态：所有面板透明 + 向下偏移
        if (bottomPanel != null)
        {
            bottomPanel.Modulate = new Color(1, 1, 1, 0);
            bottomPanel.Position = new Vector2(bottomPanel.Position.X, bottomPanel.Position.Y + SlideOffset);
        }
        if (topBar != null)
        {
            topBar.Modulate = new Color(1, 1, 1, 0);
            topBar.Position = new Vector2(topBar.Position.X, topBar.Position.Y + SlideOffset);
        }
        if (minimapControl != null)
        {
            minimapControl.Modulate = new Color(1, 1, 1, 0);
            minimapControl.Position = new Vector2(minimapControl.Position.X, minimapControl.Position.Y + SlideOffset);
        }

        // ── 1. 淡入 ──
        var fadeTween = CreateTween();
        fadeTween.TweenProperty(_fadeRect, "color", new Color(0, 0, 0, 0), FadeInDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(fadeTween, Tween.SignalName.Finished);

        // ── 2. UI 元素从下方滑入（并行，用相对偏移） ──
        var enterTween = CreateTween();
        enterTween.SetParallel(true);

        // 底部面板：从下方滑入 + 淡入
        if (bottomPanel != null)
        {
            float targetY = bottomPanel.Position.Y - SlideOffset;
            enterTween.TweenProperty(bottomPanel, "position:y", targetY, UiEnterDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            enterTween.TweenProperty(bottomPanel, "modulate:a", 1.0f, UiEnterDuration * 0.6f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
        }

        // 回合顺序栏：从下方滑入 + 淡入（延迟）
        if (topBar != null)
        {
            float targetY = topBar.Position.Y - SlideOffset;
            enterTween.TweenProperty(topBar, "position:y", targetY, UiEnterDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out)
                .SetDelay(Stagger);
            enterTween.TweenProperty(topBar, "modulate:a", 1.0f, UiEnterDuration * 0.6f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out)
                .SetDelay(Stagger);
        }

        // 小地图：从下方滑入 + 淡入（更多延迟）
        if (minimapControl != null)
        {
            float targetY = minimapControl.Position.Y - SlideOffset;
            enterTween.TweenProperty(minimapControl, "position:y", targetY, UiEnterDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out)
                .SetDelay(Stagger * 2);
            enterTween.TweenProperty(minimapControl, "modulate:a", 1.0f, UiEnterDuration * 0.6f)
                .SetTrans(Tween.TransitionType.Sine)
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
