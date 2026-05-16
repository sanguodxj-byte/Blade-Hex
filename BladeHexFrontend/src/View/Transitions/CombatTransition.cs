// CombatTransition.cs
// 大地图 → 战斗的过渡动画
// 1. 相机快速拉近玩家队伍
// 2. 大地图 UI 退出（底部下滑、顶部上滑、小地图下移）
// 3. 屏幕淡出（白闪/黑渐隐）
// 4. 回调执行场景切换
using Godot;
using System;

namespace BladeHex.View.Transitions;

/// <summary>
/// 战斗过渡动画控制器 — 挂载到大地图场景，播放完毕后回调执行实际切换。
/// </summary>
public partial class CombatTransition : CanvasLayer
{
    // ========================================
    // 配置
    // ========================================
    private const float ZoomDuration = 0.6f;       // 相机拉近时长
    private const float UiExitDuration = 0.4f;     // UI 退出时长
    private const float FadeOutDuration = 0.5f;    // 淡出时长
    private const float ZoomScale = 0.35f;         // 相机缩放到原始的比例

    // ========================================
    // 引用
    // ========================================
    private Camera3D? _camera;
    private Control? _topPanel;       // 顶部信息栏
    private Control? _bottomPanel;    // 底部功能栏
    private Control? _minimapPanel;   // 右上角小地图
    private ColorRect? _fadeRect;     // 全屏淡出遮罩
    private Action? _onComplete;

    // ========================================
    // 公开接口
    // ========================================

    /// <summary>
    /// 播放大地图→战斗过渡动画。
    /// </summary>
    /// <param name="camera">大地图 3D 正交相机</param>
    /// <param name="topPanel">顶部 UI 面板</param>
    /// <param name="bottomPanel">底部 UI 面板</param>
    /// <param name="minimapPanel">右上角小地图面板</param>
    /// <param name="onComplete">动画结束后的回调（执行实际场景切换）</param>
    public void Play(Camera3D camera, Control? topPanel, Control? bottomPanel,
        Control? minimapPanel, Action onComplete)
    {
        _camera = camera;
        _topPanel = topPanel;
        _bottomPanel = bottomPanel;
        _minimapPanel = minimapPanel;
        _onComplete = onComplete;

        Layer = 100; // 确保遮罩在最顶层

        // 创建全屏遮罩（初始透明）
        _fadeRect = new ColorRect();
        _fadeRect.Color = new Color(0, 0, 0, 0);
        _fadeRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_fadeRect);

        RunTransition();
    }

    // ========================================
    // 动画序列
    // ========================================

    private async void RunTransition()
    {
        var tree = GetTree();
        if (tree == null) { _onComplete?.Invoke(); return; }

        var tween = CreateTween();
        tween.SetParallel(true);

        // ── 1. 相机拉近（正交 Size 缩小） ──
        if (_camera != null)
        {
            float targetSize = _camera.Size * ZoomScale;
            tween.TweenProperty(_camera, "size", targetSize, ZoomDuration)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
        }

        // ── 2. 顶部 UI 上滑退出 ──
        if (_topPanel != null)
        {
            float exitY = -_topPanel.Size.Y - 20;
            tween.TweenProperty(_topPanel, "position:y", exitY, UiExitDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In)
                .SetDelay(0.1f);
        }

        // ── 3. 底部 UI 下滑退出 ──
        if (_bottomPanel != null)
        {
            var viewport = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            float exitY = viewport.Y + 20;
            tween.TweenProperty(_bottomPanel, "position:y", exitY, UiExitDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In)
                .SetDelay(0.1f);
        }

        // ── 4. 小地图下移（从右上 → 右下方向移动） ──
        if (_minimapPanel != null)
        {
            var viewport = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            float targetY = viewport.Y - _minimapPanel.Size.Y - 80;
            tween.TweenProperty(_minimapPanel, "offset_top", targetY, UiExitDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.InOut)
                .SetDelay(0.15f);
        }

        // 等待相机拉近完成
        tween.SetParallel(false);
        await ToSignal(tween, Tween.SignalName.Finished);

        // ── 5. 屏幕淡出（黑色） ──
        if (_fadeRect != null)
        {
            var fadeTween = CreateTween();
            fadeTween.TweenProperty(_fadeRect, "color", new Color(0, 0, 0, 1), FadeOutDuration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.In);
            await ToSignal(fadeTween, Tween.SignalName.Finished);
        }

        // ── 6. 动画结束 → 执行切换 ──
        _onComplete?.Invoke();
    }
}
