// POIPanelBase.cs
// POI 交互面板统一基类 — 提供共享主题、脚手架、UI 工厂方法
// 所有 POI 子面板（城镇/交易/休息/竞技场/铁匠/训练/神殿/委托/招募/港口）继承此类
// 物品相关面板使用 ArmyManagementUI（部队面板），不继承此类
using Godot;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// POI 面板统一基类。
/// 提供：主题色、标准脚手架（遮罩+居中面板+边距+VBox）、UI 工厂方法、Show/Hide 生命周期。
/// 子类只需重写 BuildContent(VBoxContainer) 填充内容区。
/// </summary>
[GlobalClass]
public abstract partial class POIPanelBase : CanvasLayer
{
    // ============================================================================
    // 统一主题常量 — 所有 POI 面板共享
    // ============================================================================

    // 背景
    protected static readonly Color ThemeBgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    protected static readonly Color ThemeBgSecondary = new(0.12f, 0.12f, 0.14f, 0.80f);
    protected static readonly Color ThemeBgPanel = new(0.10f, 0.10f, 0.12f, 0.85f);
    protected static readonly Color ThemeBgCard = new(0.15f, 0.14f, 0.18f, 0.75f);
    protected static readonly Color ThemeBgCardHover = new(0.20f, 0.18f, 0.24f, 0.85f);
    protected static readonly Color ThemeOverlay = new(0.0f, 0.0f, 0.0f, 0.6f);

    // 边框
    protected static readonly Color ThemeBorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    protected static readonly Color ThemeBorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    protected static readonly Color ThemeBorderFriendly = new(0.3f, 0.5f, 0.4f, 0.7f);
    protected static readonly Color ThemeBorderEnemy = new(0.6f, 0.3f, 0.2f, 0.7f);

    // 文字
    protected static readonly Color ThemeTextPrimary = new(0.95f, 0.93f, 0.88f);
    protected static readonly Color ThemeTextSecondary = new(0.7f, 0.68f, 0.63f);
    protected static readonly Color ThemeTextMuted = new(0.5f, 0.48f, 0.45f);
    protected static readonly Color ThemeTextAccent = new(0.9f, 0.8f, 0.5f);
    protected static readonly Color ThemeTextPositive = new(0.3f, 0.85f, 0.3f);
    protected static readonly Color ThemeTextNegative = new(0.9f, 0.3f, 0.25f);

    // 按钮
    protected static readonly Color ThemeBtnNormalBg = new(0.18f, 0.17f, 0.22f);
    protected static readonly Color ThemeBtnNormalBorder = new(0.35f, 0.32f, 0.28f, 0.7f);
    protected static readonly Color ThemeBtnHoverBg = new(0.28f, 0.26f, 0.34f);
    protected static readonly Color ThemeBtnHoverBorder = new(0.55f, 0.48f, 0.3f, 0.9f);
    protected static readonly Color ThemeBtnPressedBg = new(0.12f, 0.11f, 0.15f);
    protected static readonly Color ThemeBtnFontColor = new(0.92f, 0.90f, 0.85f);
    protected static readonly Color ThemeBtnFontHover = new(1.0f, 0.9f, 0.6f);
    protected static readonly Color ThemeBtnFontDisabled = new(0.4f, 0.4f, 0.4f);

    // 字号
    protected const int FontSizeXl = 20;
    protected const int FontSizeLg = 16;
    protected const int FontSizeMd = 14;
    protected const int FontSizeSm = 12;
    protected const int FontSizeXs = 10;

    // 间距
    protected const int SpacingXs = 2;
    protected const int SpacingSm = 4;
    protected const int SpacingMd = 8;
    protected const int SpacingLg = 12;
    protected const int SpacingXl = 16;

    // 圆角
    protected const int RadiusSm = 4;
    protected const int RadiusMd = 8;

    // ============================================================================
    // 面板规格 — 子类可重写
    // ============================================================================

    /// <summary>面板宽度（像素）</summary>
    protected virtual int PanelWidth => 450;

    /// <summary>面板高度（像素）</summary>
    protected virtual int PanelHeight => 420;

    /// <summary>内边距</summary>
    protected virtual int PanelMargin => 20;

    /// <summary>CanvasLayer 层级</summary>
    protected virtual int PanelLayer => 25;

    /// <summary>是否点击遮罩关闭面板</summary>
    protected virtual bool CloseOnOverlayClick => true;

    // ============================================================================
    // 脚手架节点引用
    // ============================================================================

    /// <summary>根控件（控制整体可见性）</summary>
    protected Control Root { get; private set; } = null!;

    /// <summary>内容区 VBox（子类在此添加内容）</summary>
    protected VBoxContainer ContentVBox { get; private set; } = null!;

    /// <summary>主面板容器（可用于设置自定义样式）</summary>
    protected PanelContainer MainPanel { get; private set; } = null!;

    /// <summary>UI 组件工厂（统一创建接口）</summary>
    protected BladeHex.UI.UIFactory Factory { get; private set; } = null!;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        Layer = PanelLayer;
        Factory = new BladeHex.UI.UIFactory();
        BuildScaffold();
        BuildContent(ContentVBox);
    }

    /// <summary>子类重写此方法填充面板内容</summary>
    protected abstract void BuildContent(VBoxContainer container);

    // ============================================================================
    // 公共 API — 统一 Show/Hide
    // ============================================================================

    /// <summary>显示面板</summary>
    public void ShowPanel() => Root.Visible = true;

    /// <summary>隐藏面板</summary>
    public virtual void HidePanel() => Root.Visible = false;

    /// <summary>面板是否可见</summary>
    public bool IsPanelVisible() => Root.Visible;

    // ============================================================================
    // 脚手架构建
    // ============================================================================

    private void BuildScaffold()
    {
        // 根控件
        Root = new Control();
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Root.Visible = false;
        AddChild(Root);

        // 半透明遮罩
        var overlay = new ColorRect();
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.Color = ThemeOverlay;
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        if (CloseOnOverlayClick)
        {
            overlay.GuiInput += (InputEvent ev) =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    OnCloseRequested();
            };
        }
        Root.AddChild(overlay);

        // 居中主面板
        MainPanel = new PanelContainer();
        MainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
        MainPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        MainPanel.OffsetLeft = -PanelWidth / 2;
        MainPanel.OffsetTop = -PanelHeight / 2;
        MainPanel.OffsetRight = PanelWidth / 2;
        MainPanel.OffsetBottom = PanelHeight / 2;
        MainPanel.MouseFilter = Control.MouseFilterEnum.Stop;

        var panelStyle = new StyleBoxFlat { BgColor = ThemeBgPrimary };
        panelStyle.SetBorderWidthAll(1);
        panelStyle.BorderColor = ThemeBorderHighlight;
        panelStyle.SetCornerRadiusAll(RadiusMd);
        panelStyle.SetContentMarginAll(0);
        panelStyle.ShadowColor = new Color(0, 0, 0, 0.6f);
        panelStyle.ShadowSize = 8;
        MainPanel.AddThemeStyleboxOverride("panel", panelStyle);
        Root.AddChild(MainPanel);

        // 内边距
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", PanelMargin);
        margin.AddThemeConstantOverride("margin_right", PanelMargin);
        margin.AddThemeConstantOverride("margin_top", PanelMargin);
        margin.AddThemeConstantOverride("margin_bottom", PanelMargin);
        MainPanel.AddChild(margin);

        // 内容 VBox
        ContentVBox = new VBoxContainer();
        ContentVBox.AddThemeConstantOverride("separation", SpacingMd);
        margin.AddChild(ContentVBox);
    }

    /// <summary>关闭请求（遮罩点击或关闭按钮）— 子类可重写以发射信号</summary>
    protected virtual void OnCloseRequested()
    {
        HidePanel();
    }

    // ============================================================================
    // UI 工厂方法 — 委托给 UIFactory 统一实现
    // ============================================================================

    /// <summary>创建标题标签（大号金色）</summary>
    protected Label CreateTitleLabel(string text)
        => Factory.CreateTitleLabel(text);

    /// <summary>创建正文标签</summary>
    protected Label CreateBodyLabel(string text, Color? color = null)
        => Factory.CreateBodyLabel(text, color);

    /// <summary>创建次要标签（小号灰色）</summary>
    protected Label CreateMutedLabel(string text)
        => Factory.CreateMutedLabel(text);

    /// <summary>创建富文本标签（支持 BBCode）</summary>
    protected RichTextLabel CreateRichText(Vector2? minSize = null)
        => Factory.CreateRichText(minSize ?? default);

    /// <summary>创建标准按钮（带 hover/pressed 样式）</summary>
    protected Button CreateButton(string text, Vector2? minSize = null)
        => Factory.CreateButton(text, minSize ?? new Vector2(0, 40));

    /// <summary>创建卡片容器（用于列表项）</summary>
    protected PanelContainer CreateCard(Vector2? minSize = null, bool hoverable = false)
        => Factory.CreateCard(minSize ?? default, hoverable);

    /// <summary>创建水平分隔线</summary>
    protected HSeparator CreateSeparatorH()
        => Factory.CreateSeparatorH();

    /// <summary>创建垂直分隔线</summary>
    protected VSeparator CreateSeparatorV()
        => Factory.CreateSeparatorV();

    /// <summary>创建金币显示标签（右对齐）</summary>
    protected Label CreateGoldLabel(int gold = 0)
    {
        var lbl = new Label();
        lbl.Text = $"💰 {gold}";
        lbl.AddThemeFontSizeOverride("font_size", FontSizeMd);
        lbl.AddThemeColorOverride("font_color", ThemeTextAccent);
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        return lbl;
    }

    /// <summary>创建标题栏（标题 + 金币，水平排列）</summary>
    protected HBoxContainer CreateHeaderBar(string title, int gold = 0)
    {
        var hbox = new HBoxContainer();

        var titleLbl = CreateTitleLabel(title);
        hbox.AddChild(titleLbl);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(spacer);

        var goldLbl = CreateGoldLabel(gold);
        goldLbl.Name = "GoldLabel";
        hbox.AddChild(goldLbl);

        return hbox;
    }

    /// <summary>更新标题栏中的金币显示</summary>
    protected void UpdateHeaderGold(HBoxContainer header, int gold)
    {
        var goldLbl = header.GetNodeOrNull<Label>("GoldLabel");
        if (goldLbl != null)
            goldLbl.Text = $"💰 {gold}";
    }

    /// <summary>创建结果/反馈文本区域</summary>
    protected RichTextLabel CreateResultLabel()
    {
        var rtl = new RichTextLabel();
        rtl.BbcodeEnabled = true;
        rtl.ScrollActive = false;
        rtl.FitContent = true;
        rtl.CustomMinimumSize = new Vector2(0, 30);
        rtl.AddThemeFontSizeOverride("normal_font_size", FontSizeSm);
        rtl.AddThemeColorOverride("default_color", ThemeTextSecondary);
        return rtl;
    }

    /// <summary>创建关闭/离开按钮（底部居中）</summary>
    protected Button CreateCloseButton(string text = "离开")
    {
        var btn = CreateButton(text, new Vector2(0, 40));
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.Pressed += OnCloseRequested;
        return btn;
    }
}
