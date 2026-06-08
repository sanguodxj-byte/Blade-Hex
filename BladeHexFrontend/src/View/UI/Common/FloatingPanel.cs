// FloatingPanel.cs
// 悬浮面板基类 — 所有鼠标跟随/右键弹出的详情面板的公共基础。
//
// 提供：
//   - 统一的深色半透明面板样式（颜色/边框/圆角接入 UITheme）
//   - 三种交互策略（外部控制 / 鼠标移出关闭）
//   - 高 ZIndex（始终在最上层）
//   - 鼠标位置定位 + 视口边界修正
//   - 标准 Label 工厂方法（统一比例与配色）
//
// 子类只需：
//   1. override BuildContent() 构建内部 UI
//   2. 调用 ShowAt(screenPos) 或 ShowAtMouse() 显示
//   3. 调用 HidePanel() 隐藏
using Godot;
using BladeHex.UI;

namespace BladeHex.UI.Common;

/// <summary>悬浮面板交互策略</summary>
public enum FloatingPanelDismissMode
{
    /// <summary>外部代码控制显隐（默认）</summary>
    None,

    /// <summary>鼠标移出面板时自动关闭</summary>
    OnMouseExit,
}

/// <summary>
/// 悬浮面板基类 — 鼠标跟随/弹出式详情面板的公共基础。
/// </summary>
[GlobalClass]
public abstract partial class FloatingPanel : PanelContainer
{
    // ============================================================================
    // 可配置参数（子类可在构造函数或 _Ready 前覆盖）
    // ============================================================================

    /// <summary>面板背景色（默认从 UITheme 读取）</summary>
    protected virtual Color PanelBgColor => UITheme.Instance?.BgTooltip ?? new Color(0.06f, 0.06f, 0.08f, 0.95f);

    /// <summary>面板边框色（默认从 UITheme 读取）</summary>
    protected virtual Color PanelBorderColor => UITheme.Instance?.BorderHighlight ?? new Color(0.45f, 0.38f, 0.28f, 0.8f);

    /// <summary>边框宽度</summary>
    protected virtual int PanelBorderWidth => 1;

    /// <summary>圆角半径</summary>
    protected virtual int PanelCornerRadius => 6;

    /// <summary>内容边距</summary>
    protected virtual int PanelContentMargin => 10;

    /// <summary>交互策略</summary>
    protected virtual FloatingPanelDismissMode PanelDismiss => FloatingPanelDismissMode.None;

    /// <summary>阴影大小（0=无阴影）</summary>
    protected virtual int PanelShadowSize => 0;

    /// <summary>阴影颜色</summary>
    protected virtual Color PanelShadowColor => new(0, 0, 0, 0.5f);

    /// <summary>ZIndex 层级（越高越在前）</summary>
    protected virtual int PanelZIndex => 100;

    /// <summary>鼠标偏移量（面板相对鼠标位置的偏移）</summary>
    protected virtual Vector2 MouseOffset => new(16, 16);

    /// <summary>视口边缘安全距离</summary>
    protected virtual float ViewportPadding => 8f;

    /// <summary>最小面板宽度</summary>
    protected virtual float MinPanelWidth => 200f;

    /// <summary>是否使用 TopLevel（脱离父节点变换，使用全局坐标）</summary>
    protected virtual bool UseTopLevel => false;

    /// <summary>是否在 _Process 中持续跟随鼠标</summary>
    protected virtual bool FollowMouseContinuously => false;

    // ============================================================================
    // 内部状态
    // ============================================================================

    /// <summary>内容容器（子类在 BuildContent 中向此添加控件）</summary>
    protected VBoxContainer Content { get; private set; } = null!;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        Visible = false;
        ZIndex = PanelZIndex;
        TopLevel = UseTopLevel;
        CustomMinimumSize = new Vector2(MinPanelWidth, 0);

        ApplyPanelStyle();

        // 根据 DismissMode 配置交互策略
        if (PanelDismiss == FloatingPanelDismissMode.OnMouseExit)
        {
            MouseFilter = MouseFilterEnum.Stop;
            MouseExited += HidePanel;
        }
        else
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        Content = new VBoxContainer();
        Content.AddThemeConstantOverride("separation", 4);
        AddChild(Content);

        BuildContent();
    }

    public override void _Process(double delta)
    {
        if (!Visible || !FollowMouseContinuously) return;
        PositionAtMouse();
    }

    // ============================================================================
    // 子类必须实现
    // ============================================================================

    /// <summary>构建面板内部 UI 内容。在 _Ready 中调用一次。</summary>
    protected abstract void BuildContent();

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>在指定屏幕位置显示面板</summary>
    public void ShowAt(Vector2 screenPos)
    {
        Visible = true;

        if (UseTopLevel)
            GlobalPosition = screenPos + MouseOffset;
        else
            Position = screenPos + MouseOffset;

        // 延迟一帧修正边界（等待布局计算完成）
        CallDeferred(nameof(ClampToViewport));
    }

    /// <summary>在当前鼠标位置显示面板</summary>
    public void ShowAtMouse()
    {
        ShowAt(GetViewport().GetMousePosition());
    }

    /// <summary>隐藏面板</summary>
    public virtual void HidePanel()
    {
        Visible = false;
    }

    /// <summary>面板是否正在显示</summary>
    public bool IsShowing => Visible;

    // ============================================================================
    // 定位逻辑
    // ============================================================================

    /// <summary>将面板定位到当前鼠标位置（用于持续跟随模式）</summary>
    protected void PositionAtMouse()
    {
        var mousePos = GetViewport().GetMousePosition();
        var targetPos = mousePos + MouseOffset;

        // 即时边界修正
        var vpSize = GetViewport().GetVisibleRect().Size;
        if (targetPos.X + Size.X > vpSize.X - ViewportPadding)
            targetPos.X = mousePos.X - Size.X - MouseOffset.X;
        if (targetPos.Y + Size.Y > vpSize.Y - ViewportPadding)
            targetPos.Y = mousePos.Y - Size.Y - MouseOffset.Y;
        if (targetPos.X < ViewportPadding)
            targetPos.X = ViewportPadding;
        if (targetPos.Y < ViewportPadding)
            targetPos.Y = ViewportPadding;

        if (UseTopLevel)
            GlobalPosition = targetPos;
        else
            Position = targetPos;
    }

    /// <summary>修正面板位置使其不超出视口边界（延迟调用）</summary>
    protected void ClampToViewport()
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        var pos = UseTopLevel ? GlobalPosition : Position;

        if (pos.X + Size.X > vpSize.X - ViewportPadding)
            pos.X = vpSize.X - Size.X - ViewportPadding;
        if (pos.Y + Size.Y > vpSize.Y - ViewportPadding)
            pos.Y = vpSize.Y - Size.Y - ViewportPadding;
        if (pos.X < ViewportPadding)
            pos.X = ViewportPadding;
        if (pos.Y < ViewportPadding)
            pos.Y = ViewportPadding;

        if (UseTopLevel)
            GlobalPosition = pos;
        else
            Position = pos;
    }

    // ============================================================================
    // 样式
    // ============================================================================

    /// <summary>应用面板样式</summary>
    private void ApplyPanelStyle()
    {
        var style = new StyleBoxFlat();
        style.BgColor = PanelBgColor;
        style.SetBorderWidthAll(PanelBorderWidth);
        style.BorderColor = PanelBorderColor;
        style.SetCornerRadiusAll(PanelCornerRadius);
        style.SetContentMarginAll(PanelContentMargin);

        if (PanelShadowSize > 0)
        {
            style.ShadowColor = PanelShadowColor;
            style.ShadowSize = PanelShadowSize;
        }

        AddThemeStyleboxOverride("panel", style);
    }

    // ============================================================================
    // UI 工具方法（子类可直接使用）
    // ============================================================================

    /// <summary>创建标签</summary>
    protected static Label MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    /// <summary>创建标题标签（大字号，强调色，从 UITheme 读取）</summary>
    protected static Label MakeTitleLabel(string text, int fontSize = 16)
    {
        var c = UITheme.Instance?.TextAccent ?? new Color(0.95f, 0.85f, 0.5f);
        return MakeLabel(text, fontSize, c);
    }

    /// <summary>创建正文标签（从 UITheme 读取）</summary>
    protected static Label MakeBodyLabel(string text, int fontSize = 13)
    {
        var c = UITheme.Instance?.TextPrimary ?? new Color(0.9f, 0.88f, 0.82f);
        return MakeLabel(text, fontSize, c);
    }

    /// <summary>创建次要文本标签（从 UITheme 读取）</summary>
    protected static Label MakeMutedLabel(string text, int fontSize = 11)
    {
        var c = UITheme.Instance?.TextMuted ?? new Color(0.55f, 0.53f, 0.5f);
        return MakeLabel(text, fontSize, c);
    }

    /// <summary>创建数据行标签（字号12，次要色）</summary>
    protected static Label MakeStatLabel(string text)
    {
        var c = UITheme.Instance?.TextSecondary ?? new Color(0.7f, 0.68f, 0.63f);
        return MakeLabel(text, 12, c);
    }

    /// <summary>创建富文本标签</summary>
    protected static RichTextLabel MakeRichText(float minWidth = 200f)
    {
        var rt = new RichTextLabel();
        rt.BbcodeEnabled = true;
        rt.ScrollActive = false;
        rt.FitContent = true;
        rt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rt.CustomMinimumSize = new Vector2(minWidth, 0);
        rt.MouseFilter = MouseFilterEnum.Ignore;
        return rt;
    }

    /// <summary>创建分割线</summary>
    protected static HSeparator MakeSeparator(float alpha = 0.3f)
    {
        var sep = new HSeparator();
        sep.Modulate = new Color(1, 1, 1, alpha);
        return sep;
    }
}
