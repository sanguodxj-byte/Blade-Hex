// UIFactory.cs
// UI组件工厂 — 统一创建接口，所有UI组件通过此工厂生成
// 预留图像UI切换能力：当UITheme中配置了图像资源时自动切换渲染方式
// 对应策划案 09-UI设计.md 的设计原则
using Godot;
using BladeHex.Audio;
using System;
using BladeHex.UI.Loading;

namespace BladeHex.UI;

/// <summary>
/// UI组件工厂 — 统一的 UI 组件创建接口
/// </summary>
public partial class UIFactory : RefCounted
{
    // ============================================================================
    // 主题引用
    // ============================================================================

    private UITheme Theme => UITheme.Instance!;

    // ============================================================================
    // 加载界面组件创建
    // ============================================================================

    public LoadingPhaseData CreatePhaseData()
    {
        return new LoadingPhaseData();
    }

    public TipsDisplay CreateTipsDisplay()
    {
        return new TipsDisplay();
    }

    // ============================================================================
    // 面板
    // ============================================================================

    /// <summary>创建标准面板</summary>
    public PanelContainer CreatePanel(Vector2 minSize = default, Color? bg = null,
        Color? border = null, int contentMargin = -1)
    {
        var panel = new PanelContainer();
        if (minSize != Vector2.Zero)
            panel.CustomMinimumSize = minSize;

        if (Theme.OverworldPanelStyle != null)
        {
            panel.AddThemeStyleboxOverride("panel", Theme.OverworldPanelStyle);
        }
        else
        {
            var bgColor = bg ?? Theme.BgPanel;
            var borderColor = border ?? Theme.BorderDefault;
            var margin = contentMargin >= 0 ? contentMargin : Theme.SpacingMd;

            panel.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
                bgColor, borderColor, 1, Theme.RadiusMd, margin));
        }

        AttachPanelSfx(panel);
        return panel;
    }

    /// <summary>创建卡片（可悬停高亮）</summary>
    public PanelContainer CreateCard(Vector2 minSize = default, bool hoverable = true)
    {
        var card = new PanelContainer();
        if (minSize != Vector2.Zero)
            card.CustomMinimumSize = minSize;

        card.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgCard, Theme.BorderDefault, 1, Theme.RadiusMd, Theme.SpacingSm));

        if (hoverable)
            card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        return card;
    }

    // ============================================================================
    // 按钮
    // ============================================================================

    /// <summary>创建标准按钮</summary>
    public Button CreateButton(string text, Vector2 minSize = default, string actionName = "")
    {
        var btn = new Button();
        btn.Text = text;
        btn.FocusMode = Control.FocusModeEnum.None;

        if (minSize != Vector2.Zero)
            btn.CustomMinimumSize = minSize;
        else
            btn.CustomMinimumSize = new Vector2(0, Theme.ButtonHeight);

        Theme.ApplyButtonTheme(btn);

        if (!string.IsNullOrEmpty(actionName))
            btn.SetMeta("action_name", actionName);

        AttachButtonSfx(btn);
        return btn;
    }

    /// <summary>创建图标按钮</summary>
    public Button CreateIconButton(string iconText, string tooltip = "", int size = 36)
    {
        var btn = new Button();
        btn.Text = iconText;
        btn.FocusMode = Control.FocusModeEnum.None;
        btn.CustomMinimumSize = new Vector2(size, size);
        Theme.ApplyButtonTheme(btn);

        if (!string.IsNullOrEmpty(tooltip))
            btn.TooltipText = tooltip;

        AttachButtonSfx(btn);
        return btn;
    }

    /// <summary>创建操作栏按钮（战斗底部操作面板用）</summary>
    public Button CreateActionButton(string label, string shortcut,
        string icon = "", Color? color = null)
    {
        var btn = new Button();
        btn.FocusMode = Control.FocusModeEnum.None;
        var display = $"{label}\n({shortcut})";
        if (!string.IsNullOrEmpty(icon))
            display = $"{icon} {display}";
        btn.Text = display;
        btn.CustomMinimumSize = new Vector2(90, 64);
        Theme.ApplyButtonTheme(btn);

        if (color.HasValue)
        {
            btn.AddThemeColorOverride("font_color", color.Value);
            btn.AddThemeColorOverride("font_hover_color", new Color(
                Mathf.Min(color.Value.R + 0.2f, 1.0f),
                Mathf.Min(color.Value.G + 0.2f, 1.0f),
                Mathf.Min(color.Value.B + 0.2f, 1.0f)));
        }

        AttachButtonSfx(btn);
        return btn;
    }

    // ============================================================================
    // 按钮音效辅助
    // ============================================================================

    /// <summary>为按钮自动挂载点击和悬停音效</summary>
    public void AttachButtonSfx(Button btn)
    {
        btn.Pressed += OnBtnPressedSfx;
        btn.MouseEntered += OnBtnHoverSfx;
    }

    private void OnBtnPressedSfx()
    {
        var audio = GetAudioManager();
        if (audio != null)
            audio.PlaySfxName("ui_click");
    }

    private void OnBtnHoverSfx()
    {
        var audio = GetAudioManager();
        if (audio != null)
            audio.PlaySfxName("ui_hover", -6.0f);
    }

    /// <summary>为面板自动挂载显示/隐藏音效</summary>
    public void AttachPanelSfx(Control panel)
    {
        panel.VisibilityChanged += () => OnPanelVisibilityChanged(panel);
    }

    private void OnPanelVisibilityChanged(Control panel)
    {
        var audio = GetAudioManager();
        if (audio == null) return;

        if (panel.Visible)
            audio.PlaySfxName("ui_panel_open");
        else
            audio.PlaySfxName("ui_panel_close");
    }

    private static BladeHex.Audio.AudioManager? GetAudioManager()
    {
        var root = Engine.GetMainLoop() as SceneTree;
        return root?.Root.GetNodeOrNull<BladeHex.Audio.AudioManager>("AudioManager");
    }

    // ============================================================================
    // 标签
    // ============================================================================

    /// <summary>创建标题标签</summary>
    public Label CreateTitleLabel(string text, int size = -1)
    {
        var lbl = new Label();
        lbl.Text = text;
        var fs = size > 0 ? size : Theme.FontSizeXl;
        lbl.AddThemeFontSizeOverride("font_size", fs);
        lbl.AddThemeColorOverride("font_color", Theme.TextAccent);
        return lbl;
    }

    /// <summary>创建正文标签</summary>
    public Label CreateBodyLabel(string text, Color? color = null)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
        var c = color ?? Theme.TextPrimary;
        lbl.AddThemeColorOverride("font_color", c);
        return lbl;
    }

    /// <summary>创建次要标签</summary>
    public Label CreateMutedLabel(string text)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
        lbl.AddThemeColorOverride("font_color", Theme.TextMuted);
        return lbl;
    }

    /// <summary>创建属性名-值对</summary>
    public HBoxContainer CreateStatPair(string statName, string value,
        Color? nameColor = null, Color? valueColor = null)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", Theme.SpacingSm);

        var nameLbl = new Label();
        nameLbl.Text = $"{statName}:";
        nameLbl.AddThemeColorOverride("font_color",
            nameColor ?? Theme.TextSecondary);
        nameLbl.CustomMinimumSize = new Vector2(100, 0);
        hbox.AddChild(nameLbl);

        var valLbl = new Label();
        valLbl.Text = value;
        valLbl.AddThemeColorOverride("font_color",
            valueColor ?? Theme.TextPrimary);
        valLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(valLbl);

        return hbox;
    }

    // ============================================================================
    // 进度条
    // ============================================================================

    /// <summary>创建HP条</summary>
    public ProgressBar CreateHpBar(float width = 120, int height = -1)
    {
        var bar = new ProgressBar();
        var h = height > 0 ? height : Theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.ShowPercentage = false;
        Theme.ApplyBarTheme(bar, Theme.HpHigh, Theme.HpBarBg);
        return bar;
    }

    /// <summary>创建魔力条</summary>
    public ProgressBar CreateManaBar(float width = 120, int height = -1)
    {
        var bar = new ProgressBar();
        var h = height > 0 ? height : Theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.ShowPercentage = false;
        Theme.ApplyBarTheme(bar, Theme.ManaFill, Theme.ManaBg);
        return bar;
    }

    /// <summary>创建经验条</summary>
    public ProgressBar CreateXpBar(float width = 120, int height = -1)
    {
        var bar = new ProgressBar();
        var h = height > 0 ? height : Theme.BarHeightSm;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.ShowPercentage = false;
        Theme.ApplyBarTheme(bar, Theme.XpFill, Theme.XpBg);
        return bar;
    }

    /// <summary>创建自定义颜色进度条</summary>
    public ProgressBar CreateBar(Color fillColor, Color? bgColor = null,
        float width = 120, int height = -1)
    {
        var bar = new ProgressBar();
        var h = height > 0 ? height : Theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.ShowPercentage = false;
        var bg = bgColor ?? new Color(0.1f, 0.1f, 0.12f);
        Theme.ApplyBarTheme(bar, fillColor, bg);
        return bar;
    }

    // ============================================================================
    // 容器
    // ============================================================================

    /// <summary>创建带内边距的容器</summary>
    public MarginContainer CreateMargin(int left = -1, int right = -1,
        int top = -1, int bottom = -1)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left >= 0 ? left : Theme.SpacingLg);
        m.AddThemeConstantOverride("margin_right", right >= 0 ? right : Theme.SpacingLg);
        m.AddThemeConstantOverride("margin_top", top >= 0 ? top : Theme.SpacingMd);
        m.AddThemeConstantOverride("margin_bottom", bottom >= 0 ? bottom : Theme.SpacingMd);
        return m;
    }

    /// <summary>创建滚动容器</summary>
    public ScrollContainer CreateScrollContainer(bool horizontal = false)
    {
        var sc = new ScrollContainer();
        sc.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        sc.HorizontalScrollMode = horizontal
            ? ScrollContainer.ScrollMode.Auto
            : ScrollContainer.ScrollMode.ShowNever;
        sc.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        return sc;
    }

    // ============================================================================
    // 分割线
    // ============================================================================

    /// <summary>创建水平分割线</summary>
    public HSeparator CreateSeparatorH(Color? color = null)
    {
        var sep = new HSeparator();
        var c = color ?? Theme.BorderDefault;
        var style = new StyleBoxFlat();
        style.BgColor = c;
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    /// <summary>创建垂直分割线</summary>
    public VSeparator CreateSeparatorV(Color? color = null)
    {
        var sep = new VSeparator();
        var c = color ?? Theme.BorderDefault;
        var style = new StyleBoxFlat();
        style.BgColor = c;
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    // ============================================================================
    // 头像
    // ============================================================================

    /// <summary>创建头像区域</summary>
    public PanelContainer CreatePortrait(int size = -1)
    {
        var s = size > 0 ? size : Theme.PortraitSize;
        // 外框
        var container = new PanelContainer();
        container.CustomMinimumSize = new Vector2(s, s);
        container.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgCard, Theme.BorderHighlight, 2, Theme.RadiusMd, 2));

        // 内部图像区域
        var rect = new TextureRect();
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        // 预留：如果主题有头像框图像，在此应用
        if (Theme.PortraitFrame != null)
            rect.Texture = Theme.PortraitFrame;
        container.AddChild(rect);

        // 存储引用以便后续设置头像
        container.SetMeta("portrait_rect", rect);
        return container;
    }

    // ============================================================================
    // 装备槽
    // ============================================================================

    /// <summary>创建装备槽位</summary>
    public PanelContainer CreateEquipmentSlot(string slotName, int size = -1)
    {
        var s = size > 0 ? size : Theme.IconSizeLg;
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(s, s);
        slot.TooltipText = slotName;
        slot.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgCard, Theme.BorderDefault, 1, Theme.RadiusMd, 2));

        // 图标区
        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        slot.AddChild(iconRect);

        // 槽位名标签
        var nameLbl = new Label();
        nameLbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
        nameLbl.Text = slotName;
        nameLbl.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        nameLbl.AddThemeColorOverride("font_color", Theme.TextMuted);
        slot.AddChild(nameLbl);

        slot.SetMeta("icon_rect", iconRect);
        slot.SetMeta("name_label", nameLbl);
        return slot;
    }

    // ============================================================================
    // 物品格子
    // ============================================================================

    /// <summary>创建物品格子</summary>
    public Panel CreateItemSlot(int size = -1)
    {
        var s = size > 0 ? size : Theme.IconSizeLg;
        var slot = new Panel();
        slot.CustomMinimumSize = new Vector2(s, s);
        slot.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgCard, Theme.BorderDefault, 1, Theme.RadiusSm, 2));
        return slot;
    }

    // ============================================================================
    // 富文本
    // ============================================================================

    /// <summary>创建BBCode富文本</summary>
    /// <remarks>
    /// 默认开启 <c>AutowrapMode = WordSmart</c> 防止长文本横向溢出。
    /// 默认 <c>FitContent = true</c>（按内容撑高，常用于 tooltip / 描述）。
    /// 若文本受固定宽度容器约束且需滚动，调用方应把 <c>FitContent</c> 改为 <c>false</c>
    /// 并把 <c>ScrollActive</c> 改为 <c>true</c>。
    /// </remarks>
    public RichTextLabel CreateRichText(Vector2 minSize = default)
    {
        var rt = new RichTextLabel();
        if (minSize != Vector2.Zero)
            rt.CustomMinimumSize = minSize;
        rt.BbcodeEnabled = true;
        rt.ScrollActive = false;
        rt.FitContent = true;
        rt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return rt;
    }

    // ============================================================================
    // Tab按钮组
    // ============================================================================

    /// <summary>创建标签页按钮组</summary>
    public HBoxContainer CreateTabBar(string[] tabs)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 2);
        for (int i = 0; i < tabs.Length; i++)
        {
            var btn = CreateButton(tabs[i], new Vector2(0, 30));
            btn.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
            btn.SetMeta("tab_index", i);
            // Note: CreateButton already attaches sfx
            hbox.AddChild(btn);
        }
        return hbox;
    }

    // ============================================================================
    // 加载界面
    // ============================================================================

    /// <summary>创建加载进度条（独立组件，可嵌入任意容器）</summary>
    public ProgressBar CreateLoadingBar(float width = 400.0f, int height = -1)
    {
        var bar = new ProgressBar();
        var h = height > 0 ? height : Theme.BarHeightLg;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.MinValue = 0.0;
        bar.MaxValue = 100.0;
        bar.Value = 0.0;
        bar.ShowPercentage = false;

        // 金色填充 + 发光阴影
        var fillStyle = new StyleBoxFlat();
        fillStyle.BgColor = Theme.TextAccent;
        fillStyle.SetCornerRadiusAll(Theme.RadiusSm);
        fillStyle.ShadowColor = new Color(Theme.TextAccent.R, Theme.TextAccent.G,
            Theme.TextAccent.B, 0.3f);
        fillStyle.ShadowSize = 4;
        bar.AddThemeStyleboxOverride("fill", fillStyle);

        // 深色背景 + 边框
        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.08f, 0.08f, 0.10f, 0.9f);
        bgStyle.SetBorderWidthAll(1);
        bgStyle.BorderColor = Theme.BorderDefault;
        bgStyle.SetCornerRadiusAll(Theme.RadiusSm);
        bar.AddThemeStyleboxOverride("background", bgStyle);

        return bar;
    }

    /// <summary>创建加载阶段描述区域（标题 + 描述文本）</summary>
    public VBoxContainer CreateLoadingPhaseDisplay()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", Theme.SpacingSm);

        // 阶段标题
        var title = new Label();
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", Theme.FontSizeXxl);
        title.AddThemeColorOverride("font_color", Theme.TextAccent);
        vbox.AddChild(title);
        vbox.SetMeta("title_label", title);

        // 阶段描述
        var desc = new RichTextLabel();
        desc.BbcodeEnabled = true;
        desc.ScrollActive = false;
        desc.FitContent = true;
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.HorizontalAlignment = HorizontalAlignment.Center;
        desc.AddThemeFontSizeOverride("normal_font_size", Theme.FontSizeMd);
        desc.AddThemeColorOverride("default_color", Theme.TextSecondary);
        var emptyStyle = new StyleBoxEmpty();
        emptyStyle.SetContentMarginAll(0);
        desc.AddThemeStyleboxOverride("normal", emptyStyle);
        vbox.AddChild(desc);
        vbox.SetMeta("desc_label", desc);

        return vbox;
    }

    /// <summary>创建完整加载界面（嵌入式，不含CanvasLayer）</summary>
    public VBoxContainer CreateLoadingScreenEmbedded(int phaseType = 0)
    {
        var pd = CreatePhaseData();

        var phases = phaseType switch
        {
            0 => pd.GetNewWorldPhases(),
            1 => pd.GetLoadSavePhases(),
            2 => pd.GetCombatPhases(),
            3 => pd.GetQuickGamePhases(),
            _ => pd.GetNewWorldPhases(),
        };

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        root.Alignment = BoxContainer.AlignmentMode.Center;

        // 顶部间距
        var topSpacer = new Control();
        topSpacer.CustomMinimumSize = new Vector2(0, 60);
        root.AddChild(topSpacer);

        // 阶段描述区域
        var phaseDisplay = CreateLoadingPhaseDisplay();
        phaseDisplay.CustomMinimumSize = new Vector2(500, 0);
        root.AddChild(phaseDisplay);

        // 间距
        var gap = new Control();
        gap.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(gap);

        // 装饰线
        root.AddChild(CreateSeparatorH(new Color(
            Theme.BorderHighlight.R, Theme.BorderHighlight.G,
            Theme.BorderHighlight.B, 0.3f)));

        // 间距
        var gap2 = new Control();
        gap2.CustomMinimumSize = new Vector2(0, Theme.SpacingLg);
        root.AddChild(gap2);

        // 进度条
        var bar = CreateLoadingBar(500);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        root.AddChild(bar);

        // 百分比
        var pct = new Label();
        pct.HorizontalAlignment = HorizontalAlignment.Center;
        pct.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
        pct.AddThemeColorOverride("font_color", Theme.TextMuted);
        pct.Text = "0%";
        root.AddChild(pct);

        // 间距
        var gap3 = new Control();
        gap3.CustomMinimumSize = new Vector2(0, Theme.SpacingLg);
        root.AddChild(gap3);

        // 装饰线
        root.AddChild(CreateSeparatorH(new Color(
            Theme.BorderHighlight.R, Theme.BorderHighlight.G,
            Theme.BorderHighlight.B, 0.3f)));

        // 弹性间距推到底部
        var pusher = new Control();
        pusher.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(pusher);

        // Tips组件
        var tipsControl = CreateTipsDisplay();
        tipsControl.CustomMinimumSize = new Vector2(500, 30);
        tipsControl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        root.AddChild(tipsControl);
        root.SetMeta("tips_display", tipsControl);

        // 存储引用
        root.SetMeta("progress_bar", bar);
        root.SetMeta("percent_label", pct);
        root.SetMeta("phase_display", phaseDisplay);
        // phases stored in local scope only (List<LoadingPhase> not Variant-compatible)

        return root;
    }
}
