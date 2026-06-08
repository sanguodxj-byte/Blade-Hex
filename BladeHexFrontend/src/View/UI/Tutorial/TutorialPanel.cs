// TutorialPanel.cs
// 教程悬浮面板 — 右上角可翻页的教程提示
// 类似文明/骑砍/群星的教程弹窗风格：
// - 悬浮在右上角，不阻挡主要游戏操作
// - 支持手动翻页（上一页/下一页）
// - 可随时关闭，关闭后不再自动弹出同一章节
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Localization;

namespace BladeHex.UI.Tutorial;

/// <summary>
/// 教程悬浮面板 — 右上角半透明面板，显示教程内容并支持翻页。
/// </summary>
[GlobalClass]
public partial class TutorialPanel : CanvasLayer
{
    // ============================================================================
    // 事件
    // ============================================================================

    /// <summary>面板关闭时触发（传递章节 ID）</summary>
    public event Action<string>? Closed;

    // ============================================================================
    // 主题常量
    // ============================================================================

    private static readonly Color BgColor = new(0.06f, 0.06f, 0.09f, 0.92f);
    private static readonly Color BorderColor = new(0.5f, 0.42f, 0.25f, 0.85f);
    private static readonly Color TitleColor = new(0.95f, 0.85f, 0.5f);
    private static readonly Color ChapterTitleColor = new(0.7f, 0.62f, 0.4f);
    private static readonly Color TextColor = new(0.9f, 0.88f, 0.82f);
    private static readonly Color PageIndicatorColor = new(0.6f, 0.55f, 0.45f);
    private static readonly Color BtnNormalBg = new(0.14f, 0.13f, 0.18f);
    private static readonly Color BtnHoverBg = new(0.24f, 0.22f, 0.30f);
    private static readonly Color BtnBorder = new(0.4f, 0.35f, 0.25f, 0.7f);
    private static readonly Color BtnFont = new(0.92f, 0.88f, 0.78f);
    private static readonly Color BtnFontHover = new(1.0f, 0.9f, 0.6f);

    private const int PanelWidth = 380;
    private const int PanelMaxHeight = 520;
    private const int PanelMarginRight = 20;
    private const int PanelMarginTop = 20;
    private const int ContentMargin = 18;
    private const int FontTitle = 20;
    private const int FontChapter = 15;
    private const int FontBody = 15;
    private const int FontPageIndicator = 13;
    private const int BtnHeight = 32;

    // ============================================================================
    // 状态
    // ============================================================================

    private string _chapterId = "";
    private string _chapterTitle = "";
    private List<TutorialPage> _pages = new();
    private int _currentPage = 0;

    // UI 节点
    private Control _root = null!;
    private PanelContainer _panel = null!;
    private Label _chapterLabel = null!;
    private Label _titleLabel = null!;
    private RichTextLabel _contentLabel = null!;
    private Label _pageIndicator = null!;
    private Button _prevBtn = null!;
    private Button _nextBtn = null!;
    private Button _closeBtn = null!;

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>显示指定章节的教程</summary>
    public void ShowChapter(TutorialChapter chapter)
    {
        _chapterId = chapter.Id;
        _chapterTitle = chapter.Title;
        _pages = chapter.Pages;
        _currentPage = 0;
        RefreshContent();
        ShowPanel();
    }

    /// <summary>当前是否可见</summary>
    public new bool IsVisible => _root?.Visible ?? false;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        Layer = 150; // 高于大部分 UI，低于 GameMenuManager(200)
        BuildUI();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_root.Visible) return;

        // 支持键盘翻页
        if (@event is InputEventKey key && key.Pressed)
        {
            switch (key.Keycode)
            {
                case Key.Left:
                case Key.A:
                    PrevPage();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Right:
                case Key.D:
                case Key.Space:
                    NextPage();
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.Escape:
                    ClosePanel();
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }
    }

    // ============================================================================
    // UI 构建
    // ============================================================================

    private void BuildUI()
    {
        _root = new Control();
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.Visible = false;
        AddChild(_root);

        // 面板容器 — 右上角定位
        _panel = new PanelContainer();
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.GrowVertical = Control.GrowDirection.End;
        _panel.OffsetLeft = -PanelWidth - PanelMarginRight;
        _panel.OffsetRight = -PanelMarginRight;
        _panel.OffsetTop = PanelMarginTop;
        _panel.CustomMinimumSize = new Vector2(PanelWidth, 0);

        var panelStyle = new StyleBoxFlat { BgColor = BgColor };
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = BorderColor;
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetContentMarginAll(ContentMargin);
        panelStyle.ShadowColor = new Color(0, 0, 0, 0.4f);
        panelStyle.ShadowSize = 6;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _root.AddChild(_panel);

        // 内容布局
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(vbox);

        // 章节标题行（章节名 + 关闭按钮）
        var headerRow = new HBoxContainer();
        vbox.AddChild(headerRow);

        _chapterLabel = new Label();
        _chapterLabel.AddThemeFontSizeOverride("font_size", FontChapter);
        _chapterLabel.AddThemeColorOverride("font_color", ChapterTitleColor);
        _chapterLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerRow.AddChild(_chapterLabel);

        _closeBtn = new Button();
        _closeBtn.Text = "✕";
        _closeBtn.CustomMinimumSize = new Vector2(28, 28);
        _closeBtn.AddThemeFontSizeOverride("font_size", 16);
        _closeBtn.AddThemeColorOverride("font_color", PageIndicatorColor);
        _closeBtn.AddThemeColorOverride("font_hover_color", BtnFontHover);
        var closeBtnStyle = new StyleBoxFlat { BgColor = Colors.Transparent };
        closeBtnStyle.SetContentMarginAll(2);
        _closeBtn.AddThemeStyleboxOverride("normal", closeBtnStyle);
        _closeBtn.AddThemeStyleboxOverride("hover", closeBtnStyle);
        _closeBtn.AddThemeStyleboxOverride("pressed", closeBtnStyle);
        _closeBtn.Pressed += ClosePanel;
        headerRow.AddChild(_closeBtn);

        // 分隔线
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        sep.AddThemeStyleboxOverride("separator", new StyleBoxLine
        {
            Color = new Color(0.35f, 0.3f, 0.2f, 0.5f),
            Thickness = 1
        });
        vbox.AddChild(sep);

        // 页面标题
        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", FontTitle);
        _titleLabel.AddThemeColorOverride("font_color", TitleColor);
        vbox.AddChild(_titleLabel);

        // 内容区域（RichTextLabel 支持换行）
        _contentLabel = new RichTextLabel();
        _contentLabel.BbcodeEnabled = true;
        _contentLabel.FitContent = true;
        _contentLabel.ScrollActive = false;
        _contentLabel.CustomMinimumSize = new Vector2(0, 120);
        _contentLabel.AddThemeFontSizeOverride("normal_font_size", FontBody);
        _contentLabel.AddThemeColorOverride("default_color", TextColor);
        vbox.AddChild(_contentLabel);

        // 底部：页码指示 + 翻页按钮
        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(bottomRow);

        _prevBtn = MakeNavButton(L10n.Tr("TUTORIAL_PREV_PAGE"));
        _prevBtn.Pressed += PrevPage;
        bottomRow.AddChild(_prevBtn);

        _pageIndicator = new Label();
        _pageIndicator.AddThemeFontSizeOverride("font_size", FontPageIndicator);
        _pageIndicator.AddThemeColorOverride("font_color", PageIndicatorColor);
        _pageIndicator.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _pageIndicator.HorizontalAlignment = HorizontalAlignment.Center;
        _pageIndicator.VerticalAlignment = VerticalAlignment.Center;
        bottomRow.AddChild(_pageIndicator);

        _nextBtn = MakeNavButton(L10n.Tr("TUTORIAL_NEXT_PAGE"));
        _nextBtn.Pressed += NextPage;
        bottomRow.AddChild(_nextBtn);
    }

    private Button MakeNavButton(string text)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(90, BtnHeight);
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.AddThemeColorOverride("font_color", BtnFont);
        btn.AddThemeColorOverride("font_hover_color", BtnFontHover);

        var normal = new StyleBoxFlat { BgColor = BtnNormalBg };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = BtnBorder;
        normal.SetCornerRadiusAll(4);
        normal.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = BtnHoverBg };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = new Color(0.55f, 0.48f, 0.3f, 0.9f);
        hover.SetCornerRadiusAll(4);
        hover.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);

        return btn;
    }

    // ============================================================================
    // 翻页逻辑
    // ============================================================================

    private void PrevPage()
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            RefreshContent();
        }
    }

    private void NextPage()
    {
        if (_currentPage < _pages.Count - 1)
        {
            _currentPage++;
            RefreshContent();
        }
        else
        {
            // 最后一页再点下一页 = 关闭
            ClosePanel();
        }
    }

    private void RefreshContent()
    {
        if (_pages.Count == 0) return;

        var page = _pages[_currentPage];
        _chapterLabel.Text = $"📖 {_chapterTitle}";
        _titleLabel.Text = page.Title;
        _contentLabel.Text = page.Content;
        _pageIndicator.Text = $"{_currentPage + 1} / {_pages.Count}";

        _prevBtn.Disabled = _currentPage <= 0;
        _nextBtn.Text = _currentPage >= _pages.Count - 1 ? L10n.Tr("TUTORIAL_FINISH") : L10n.Tr("TUTORIAL_NEXT_PAGE");
    }

    // ============================================================================
    // 显示/隐藏动画
    // ============================================================================

    private Tween? _tween;

    private void ShowPanel()
    {
        _tween?.Kill();
        _root.Visible = true;

        _panel.Modulate = new Color(1, 1, 1, 0);
        _panel.Position = new Vector2(_panel.Position.X + 30, _panel.Position.Y);
        var targetX = _panel.Position.X - 30;

        _tween = CreateTween();
        _tween.SetParallel(true);
        _tween.TweenProperty(_panel, "modulate:a", 1.0f, 0.25f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(_panel, "position:x", targetX, 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void ClosePanel()
    {
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(_panel, "modulate:a", 0.0f, 0.15f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _tween.Chain().TweenCallback(Callable.From(() =>
        {
            _root.Visible = false;
            Closed?.Invoke(_chapterId);
        }));
    }
}
