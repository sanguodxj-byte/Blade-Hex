// POIPanelBase.cs
// POI 交互面板统一基类 — 固定布局脚手架
// 统一布局从上到下：插画区 → 信息行 → 描述文本 → 功能列表区 → 结果反馈 → 离开按钮
// 子类只需重写数据填充方法，不得修改布局结构
using Godot;
using BladeHex.UI.Common;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// POI 面板统一基类。
/// 固定布局：插画区 → 信息行 → 描述文本 → 功能列表（ScrollContainer） → 离开按钮。
/// 子类通过重写虚方法填充数据，不得修改布局结构。
/// </summary>
[GlobalClass]
public partial class POIPanelBase : CanvasLayer
{
    // ============================================================================
    // 统一主题常量
    // ============================================================================

    protected static readonly Color ThemeBgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    protected static readonly Color ThemeBgSecondary = new(0.12f, 0.12f, 0.14f, 0.80f);
    protected static readonly Color ThemeBgPanel = new(0.10f, 0.10f, 0.12f, 0.85f);
    protected static readonly Color ThemeBgCard = new(0.15f, 0.14f, 0.18f, 0.75f);
    protected static readonly Color ThemeBgCardHover = new(0.20f, 0.18f, 0.24f, 0.85f);
    protected static readonly Color ThemeOverlay = new(0.0f, 0.0f, 0.0f, 0.6f);

    protected static readonly Color ThemeBorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    protected static readonly Color ThemeBorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    protected static readonly Color ThemeBorderFriendly = new(0.3f, 0.5f, 0.4f, 0.7f);
    protected static readonly Color ThemeBorderEnemy = new(0.6f, 0.3f, 0.2f, 0.7f);

    protected static readonly Color ThemeTextPrimary = new(0.95f, 0.93f, 0.88f);
    protected static readonly Color ThemeTextSecondary = new(0.7f, 0.68f, 0.63f);
    protected static readonly Color ThemeTextMuted = new(0.5f, 0.48f, 0.45f);
    protected static readonly Color ThemeTextAccent = new(0.9f, 0.8f, 0.5f);
    protected static readonly Color ThemeTextPositive = new(0.3f, 0.85f, 0.3f);
    protected static readonly Color ThemeTextNegative = new(0.9f, 0.3f, 0.25f);

    protected static readonly Color ThemeBtnNormalBg = new(0.18f, 0.17f, 0.22f);
    protected static readonly Color ThemeBtnNormalBorder = new(0.35f, 0.32f, 0.28f, 0.7f);
    protected static readonly Color ThemeBtnHoverBg = new(0.28f, 0.26f, 0.34f);
    protected static readonly Color ThemeBtnHoverBorder = new(0.55f, 0.48f, 0.3f, 0.9f);
    protected static readonly Color ThemeBtnPressedBg = new(0.12f, 0.11f, 0.15f);
    protected static readonly Color ThemeBtnFontColor = new(0.92f, 0.90f, 0.85f);
    protected static readonly Color ThemeBtnFontHover = new(1.0f, 0.9f, 0.6f);
    protected static readonly Color ThemeBtnFontDisabled = new(0.4f, 0.4f, 0.4f);

    protected const int FontSizeXl = 28;
    protected const int FontSizeLg = 22;
    protected const int FontSizeMd = 18;
    protected const int FontSizeSm = 16;
    protected const int FontSizeXs = 14;

    protected const int SpacingXs = 2;
    protected const int SpacingSm = 4;
    protected const int SpacingMd = 8;
    protected const int SpacingLg = 12;
    protected const int SpacingXl = 16;

    protected const int RadiusSm = 4;
    protected const int RadiusMd = 8;

    protected const int IllustrationHeight = 260;

    // ============================================================================
    // 面板规格
    // ============================================================================

    protected virtual int PanelWidth => 960;
    protected virtual int PanelHeight => 760;
    protected virtual int PanelMargin => 0;
    protected virtual int PanelLayer => 25;
    protected virtual bool CloseOnOverlayClick => true;

    // ============================================================================
    // 子类数据填充接口
    // ============================================================================

    /// <summary>插画区背景色</summary>
    protected virtual Color GetIllustrationColor() => new(0.06f, 0.06f, 0.10f, 1.0f);

    /// <summary>插画区居中文字</summary>
    protected virtual string GetIllustrationText() => "[ 设施 ]";

    /// <summary>插画图片路径（res:// 路径）。返回 null 时回退到文字占位符。</summary>
    protected virtual string? GetIllustrationPath() => null;

    /// <summary>信息行左侧标题</summary>
    protected virtual string GetPanelTitle() => "设施";

    /// <summary>信息行右侧状态文字</summary>
    protected virtual string GetInfoText() => "";

    /// <summary>描述文本（支持 BBCode）</summary>
    protected virtual string GetDescriptionText() => "";

    /// <summary>离开按钮文字</summary>
    protected virtual string GetLeaveButtonText() => "离开";

    /// <summary>填充功能列表区</summary>
    protected virtual void PopulateActions(VBoxContainer actionsContainer) { }

    // ============================================================================
    // 脚手架节点引用
    // ============================================================================

    protected Control Root { get; private set; } = null!;
    protected PanelContainer MainPanel { get; private set; } = null!;
    private ColorRect _overlay = null!;
    protected BladeHex.UI.UIFactory Factory { get; private set; } = null!;

    private Label _illustLabel = null!;
    private TextureRect? _illustTexture;
    private PanelContainer _illustPanel = null!;
    private Label _titleLabel = null!;
    private Label _infoLabel = null!;
    private RichTextLabel _descLabel = null!;
    protected VBoxContainer ActionsVBox { get; private set; } = null!;
    private RichTextLabel _resultLabel = null!;
    private Button _leaveBtn = null!;

    // 兼容旧接口

    protected VBoxContainer ContentVBox => ActionsVBox;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        Layer = PanelLayer;
        Factory = new BladeHex.UI.UIFactory();
        BuildScaffold();
        // 兼容旧子类
        BuildContent(ActionsVBox);
    }

    /// <summary>旧接口兼容 - 新子类重写 PopulateActions 即可</summary>
    protected virtual void BuildContent(VBoxContainer container) { }

    // ============================================================================
    // 公共 API
    // ============================================================================

    private Tween? _showTween;

    public void ShowPanel(bool instantOverlay = false)
    {
        RefreshLayout();
        _showTween?.Kill();
        Root.Visible = true;

        if (instantOverlay)
        {
            _overlay.Color = ThemeOverlay;
            MainPanel.Scale = Vector2.One;
            MainPanel.Modulate = new Color(1, 1, 1, 1);
        }
        else
        {
            // 遮罩从透明渐入，避免黑屏闪烁
            _overlay.Color = new Color(ThemeOverlay.R, ThemeOverlay.G, ThemeOverlay.B, 0f);

            MainPanel.Scale = new Vector2(0.92f, 0.92f);
            MainPanel.Modulate = new Color(1, 1, 1, 0);
            MainPanel.PivotOffset = MainPanel.Size * 0.5f;

            _showTween = CreateTween();
            _showTween.SetParallel(true);
            _showTween.TweenProperty(_overlay, "color:a", ThemeOverlay.A, 0.12f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _showTween.TweenProperty(MainPanel, "modulate:a", 1.0f, 0.15f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _showTween.TweenProperty(MainPanel, "scale", Vector2.One, 0.18f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    public virtual void HidePanel()
    {
    	_showTween?.Kill();
    	// 立即隐藏遮罩，避免淡出期间遮罩仍然可见导致画面闪烁
    	_overlay.Color = new Color(ThemeOverlay.R, ThemeOverlay.G, ThemeOverlay.B, 0f);
    	_showTween = CreateTween();
    	_showTween.TweenProperty(MainPanel, "modulate:a", 0.0f, 0.1f)
    		.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
    	_showTween.Chain().TweenCallback(Callable.From(() => Root.Visible = false));
    }

    public bool IsPanelVisible() => Root.Visible;

    /// <summary>立即隐藏面板（无动画，用于面板切换避免遮罩叠加闪烁）</summary>
    public void HidePanelImmediate()
    {
        _showTween?.Kill();
        Root.Visible = false;
    }

    /// <summary>刷新布局数据</summary>
    public void RefreshLayout()
    {
        if (_illustPanel.GetThemeStylebox("panel") is StyleBoxFlat illustStyle)
            illustStyle.BgColor = GetIllustrationColor();

        // 插画区：优先加载真实插图，无则回退到文字占位符
        var illustPath = GetIllustrationPath();
        if (!string.IsNullOrEmpty(illustPath))
        {
            var tex = TextureAssetResolver.LoadPoiIllustration(illustPath);
            if (tex != null)
            {
                _illustTexture!.Texture = tex;
                _illustTexture.Visible = true;
                _illustLabel.Visible = false;
            }
            else
            {
                _illustTexture!.Visible = false;
                _illustLabel.Visible = true;
                _illustLabel.Text = GetIllustrationText();
            }
        }
        else
        {
            _illustTexture!.Visible = false;
            _illustLabel.Visible = true;
            _illustLabel.Text = GetIllustrationText();
        }

        _titleLabel.Text = GetPanelTitle();
        _infoLabel.Text = GetInfoText();
        _descLabel.Text = GetDescriptionText();
        _leaveBtn.Text = GetLeaveButtonText();
        _resultLabel.Text = "";

        foreach (Node c in ActionsVBox.GetChildren()) c.QueueFree();
        PopulateActions(ActionsVBox);
    }

    protected void SetResult(string bbcodeText) => _resultLabel.Text = bbcodeText;
    protected void UpdateInfo(string text) => _infoLabel.Text = text;

    // ============================================================================
    // 脚手架构建
    // ============================================================================

    private void BuildScaffold()
    {
        Root = new Control();
        Root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Root.Visible = false;
        AddChild(Root);

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
        _overlay = overlay;

        MainPanel = new PanelContainer();
        MainPanel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
        OverlayPanelLayout.Center(MainPanel);
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

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 0);
        mainVbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        MainPanel.AddChild(mainVbox);

        // 1. 插画区（支持真实插图 + 文字占位符回退）
        _illustPanel = new PanelContainer();
        _illustPanel.CustomMinimumSize = new Vector2(0, IllustrationHeight);
        var illustStyle = new StyleBoxFlat { BgColor = GetIllustrationColor() };
        illustStyle.SetContentMarginAll(0);
        illustStyle.SetCornerRadiusAll(0);
        illustStyle.SetBorderWidthAll(0);
        _illustPanel.AddThemeStyleboxOverride("panel", illustStyle);
        mainVbox.AddChild(_illustPanel);

        // 真实插图（初始隐藏，RefreshLayout 时按需加载）
        _illustTexture = new TextureRect();
        _illustTexture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _illustTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _illustTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _illustTexture.Visible = false;
        _illustPanel.AddChild(_illustTexture);

        // 占位符提示文字（居中，当无插图时显示）
        _illustLabel = new Label();
        _illustLabel.AddThemeFontSizeOverride("font_size", FontSizeLg);
        _illustLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.38f, 0.35f, 0.6f));
        _illustLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _illustLabel.VerticalAlignment = VerticalAlignment.Center;
        _illustLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _illustPanel.AddChild(_illustLabel);

        // 内容区
        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 20);
        contentMargin.AddThemeConstantOverride("margin_right", 20);
        contentMargin.AddThemeConstantOverride("margin_top", 12);
        contentMargin.AddThemeConstantOverride("margin_bottom", 16);
        contentMargin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        mainVbox.AddChild(contentMargin);

        var contentVbox = new VBoxContainer();
        contentVbox.AddThemeConstantOverride("separation", SpacingMd);
        contentVbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        contentMargin.AddChild(contentVbox);

        // 2. 信息行（仅状态文字，无标题）
        _infoLabel = new Label();
        _infoLabel.AddThemeFontSizeOverride("font_size", FontSizeSm);
        _infoLabel.AddThemeColorOverride("font_color", ThemeTextMuted);
        contentVbox.AddChild(_infoLabel);

        // 隐藏的标题引用（RefreshLayout 兼容）
        _titleLabel = new Label();
        _titleLabel.Visible = false;
        contentVbox.AddChild(_titleLabel);

        // 3. 描述文本
        _descLabel = new RichTextLabel();
        _descLabel.BbcodeEnabled = true;
        _descLabel.ScrollActive = false;
        _descLabel.FitContent = true;
        _descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _descLabel.CustomMinimumSize = new Vector2(0, 30);
        _descLabel.AddThemeFontSizeOverride("normal_font_size", FontSizeMd);
        _descLabel.AddThemeColorOverride("default_color", ThemeTextSecondary);
        contentVbox.AddChild(_descLabel);

        contentVbox.AddChild(CreateSeparatorH());

        // 4. 功能列表区
        var actionsScroll = new ScrollContainer();
        actionsScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        actionsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        contentVbox.AddChild(actionsScroll);

        ActionsVBox = new VBoxContainer();
        ActionsVBox.AddThemeConstantOverride("separation", SpacingMd);
        ActionsVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ActionsVBox.Alignment = BoxContainer.AlignmentMode.Center;
        actionsScroll.AddChild(ActionsVBox);

        contentVbox.AddChild(CreateSeparatorH());

        // 结果反馈
        _resultLabel = new RichTextLabel();
        _resultLabel.BbcodeEnabled = true;
        _resultLabel.ScrollActive = false;
        _resultLabel.FitContent = true;
        _resultLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resultLabel.CustomMinimumSize = new Vector2(0, 24);
        _resultLabel.AddThemeFontSizeOverride("normal_font_size", FontSizeSm);
        _resultLabel.AddThemeColorOverride("default_color", ThemeTextSecondary);
        contentVbox.AddChild(_resultLabel);

        // 5. 离开按钮
        _leaveBtn = CreateButton(GetLeaveButtonText(), new Vector2(0, 56));
        _leaveBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _leaveBtn.Pressed += OnCloseRequested;
        contentVbox.AddChild(_leaveBtn);
    }

    protected virtual void OnCloseRequested() => HidePanel();

    // ============================================================================
    // UI 工厂方法
    // ============================================================================

    protected Label CreateTitleLabel(string text) => Factory.CreateTitleLabel(text);
    protected Label CreateBodyLabel(string text, Color? color = null) => Factory.CreateBodyLabel(text, color);
    protected Label CreateMutedLabel(string text) => Factory.CreateMutedLabel(text);
    protected RichTextLabel CreateRichText(Vector2? minSize = null) => Factory.CreateRichText(minSize ?? default);
    protected Button CreateButton(string text, Vector2? minSize = null) => Factory.CreateButton(text, minSize ?? new Vector2(0, 56));
    protected PanelContainer CreateCard(Vector2? minSize = null, bool hoverable = false) => Factory.CreateCard(minSize ?? default, hoverable);
    protected HSeparator CreateSeparatorH() => Factory.CreateSeparatorH();
    protected VSeparator CreateSeparatorV() => Factory.CreateSeparatorV();

    protected Label CreateGoldLabel(int gold = 0)
    {
        var lbl = new Label();
        lbl.Text = $"金币: {gold}";
        lbl.AddThemeFontSizeOverride("font_size", FontSizeMd);
        lbl.AddThemeColorOverride("font_color", ThemeTextAccent);
        lbl.HorizontalAlignment = HorizontalAlignment.Right;
        return lbl;
    }

    protected RichTextLabel CreateResultLabel()
    {
        var rtl = new RichTextLabel();
        rtl.BbcodeEnabled = true;
        rtl.ScrollActive = false;
        rtl.FitContent = true;
        rtl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rtl.CustomMinimumSize = new Vector2(0, 30);
        rtl.AddThemeFontSizeOverride("normal_font_size", FontSizeSm);
        rtl.AddThemeColorOverride("default_color", ThemeTextSecondary);
        return rtl;
    }

    /// <summary>创建全宽功能按钮</summary>
    protected Button CreateActionButton(string text, string tooltip = "")
    {
        var btn = CreateButton(text, new Vector2(0, 48));
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (!string.IsNullOrEmpty(tooltip)) btn.TooltipText = tooltip;
        return btn;
    }

    /// <summary>创建带禁用状态的功能按钮</summary>
    protected Button CreateActionButton(string text, bool enabled, string disabledReason = "")
    {
        var btn = CreateActionButton(text);
        btn.Disabled = !enabled;
        if (!enabled && !string.IsNullOrEmpty(disabledReason)) btn.TooltipText = disabledReason;
        return btn;
    }

    // 兼容旧接口
    protected HBoxContainer CreateHeaderBar(string title, int gold = 0)
    {
        var hbox = new HBoxContainer();
        hbox.AddChild(CreateTitleLabel(title));
        hbox.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var goldLbl = CreateGoldLabel(gold);
        goldLbl.Name = "GoldLabel";
        hbox.AddChild(goldLbl);
        return hbox;
    }

    protected void UpdateHeaderGold(HBoxContainer header, int gold)
    {
        var goldLbl = header.GetNodeOrNull<Label>("GoldLabel");
        if (goldLbl != null) goldLbl.Text = $"金币: {gold}";
    }

    protected Button CreateCloseButton(string text = "离开")
    {
        var btn = CreateButton(text, new Vector2(0, 56));
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.Pressed += OnCloseRequested;
        return btn;
    }
}
