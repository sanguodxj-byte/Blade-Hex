using Godot;
using System;
using BladeHex.UI;

namespace BladeHex.UI;

/// <summary>
/// UI组件工厂 —— 统一创建接口，所有UI组件通过此工厂生成
/// 迁移自 GDScript UIFactory.gd
/// </summary>
public partial class UIFactory
{
    private UITheme _theme => UITheme.Instance;

    // ========================================
    // 面板
    // ========================================

    public PanelContainer CreatePanel(Vector2 minSize = default, Color bg = default, Color border = default, int contentMargin = -1)
    {
        var panel = new PanelContainer();
        if (minSize != Vector2.Zero) panel.CustomMinimumSize = minSize;
        
        var bgColor = bg == default ? _theme.BgPanel : bg;
        var borderColor = border == default ? _theme.BorderDefault : border;
        
        panel.AddThemeStyleboxOverride("panel", _theme.MakePanelStyle(bgColor, borderColor, 1, _theme.RadiusMd, contentMargin));
        return panel;
    }

    public PanelContainer CreateCard(Vector2 minSize = default, bool hoverable = true)
    {
        var card = new PanelContainer();
        if (minSize != Vector2.Zero) card.CustomMinimumSize = minSize;
        
        card.AddThemeStyleboxOverride("panel", _theme.MakePanelStyle(_theme.BgCard, _theme.BorderDefault, 1, _theme.RadiusMd, _theme.SpacingSm));
        
        if (hoverable)
        {
            card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        }
        return card;
    }

    // ========================================
    // 按钮
    // ========================================

    public Button CreateButton(string text, Vector2 minSize = default)
    {
        var btn = new Button { Text = text };
        if (minSize != Vector2.Zero)
        {
            btn.CustomMinimumSize = minSize;
        }
        else
        {
            btn.CustomMinimumSize = new Vector2(0, _theme.ButtonHeight);
        }
        _theme.ApplyButtonTheme(btn);
        return btn;
    }

    public Button CreateActionButton(string label, string shortcut, string icon = "", Color color = default)
    {
        var btn = new Button();
        var display = $"{label}\n({shortcut})";
        if (!string.IsNullOrEmpty(icon)) display = $"{icon} {display}";
        btn.Text = display;
        btn.CustomMinimumSize = new Vector2(90, 64);
        _theme.ApplyButtonTheme(btn);
        
        if (color != default)
        {
            btn.AddThemeColorOverride("font_color", color);
            btn.AddThemeColorOverride("font_hover_color", new Color(color.R + 0.2f, color.G + 0.2f, color.B + 0.2f));
        }
        return btn;
    }

    // ========================================
    // 标签
    // ========================================

    public Label CreateTitleLabel(string text, int size = -1)
    {
        var lbl = new Label { Text = text };
        int fs = size > 0 ? size : _theme.FontSizeXl;
        lbl.AddThemeFontSizeOverride("font_size", fs);
        lbl.AddThemeColorOverride("font_color", _theme.TextAccent);
        return lbl;
    }

    public Label CreateBodyLabel(string text, Color color = default)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", _theme.FontSizeMd);
        var c = color == default ? _theme.TextPrimary : color;
        lbl.AddThemeColorOverride("font_color", c);
        return lbl;
    }

    public Label CreateMutedLabel(string text, int size = -1)
    {
        var lbl = new Label { Text = text };
        int fs = size > 0 ? size : _theme.FontSizeSm;
        lbl.AddThemeFontSizeOverride("font_size", fs);
        lbl.AddThemeColorOverride("font_color", _theme.TextMuted);
        return lbl;
    }

    // ========================================
    // 进度条
    // ========================================

    public ProgressBar CreateHpBar(float width = 120, int height = -1)
    {
        var bar = new ProgressBar { ShowPercentage = false };
        int h = height > 0 ? height : _theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        _theme.ApplyBarTheme(bar, _theme.HpHigh, _theme.HpBarBg);
        return bar;
    }

    public ProgressBar CreateManaBar(float width = 120, int height = -1)
    {
        var bar = new ProgressBar { ShowPercentage = false };
        int h = height > 0 ? height : _theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        _theme.ApplyBarTheme(bar, _theme.ManaFill, _theme.ManaBg);
        return bar;
    }

    // ========================================
    // 容器与辅助
    // ========================================

    public MarginContainer CreateMargin(int left = -1, int right = -1, int top = -1, int bottom = -1)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left >= 0 ? left : _theme.SpacingLg);
        m.AddThemeConstantOverride("margin_right", right >= 0 ? right : _theme.SpacingLg);
        m.AddThemeConstantOverride("margin_top", top >= 0 ? top : _theme.SpacingMd);
        m.AddThemeConstantOverride("margin_bottom", bottom >= 0 ? bottom : _theme.SpacingMd);
        return m;
    }

    public VSeparator CreateSeparatorV(Color color = default)
    {
        var sep = new VSeparator();
        var c = color == default ? _theme.BorderDefault : color;
        var style = new StyleBoxFlat { BgColor = c };
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    public HSeparator CreateSeparatorH(Color color = default)
    {
        var sep = new HSeparator();
        var c = color == default ? _theme.BorderDefault : color;
        var style = new StyleBoxFlat { BgColor = c };
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    public RichTextLabel CreateRichText(Vector2 minSize = default)
    {
        var rtl = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
        if (minSize != Vector2.Zero) rtl.CustomMinimumSize = minSize;
        return rtl;
    }

    public ScrollContainer CreateScrollContainer(bool horizontal = false)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = horizontal ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = !horizontal ? ScrollContainer.ScrollMode.Auto : ScrollContainer.ScrollMode.Disabled
        };
        return scroll;
    }

    public Control CreatePortrait(int size = -1)
    {
        int s = size > 0 ? size : _theme.PortraitSize;
        var container = new PanelContainer();
        container.CustomMinimumSize = new Vector2(s, s);
        container.AddThemeStyleboxOverride("panel", _theme.MakePanelStyle(_theme.BgCard, _theme.BorderHighlight, 2, _theme.RadiusMd, 2));
        
        var rect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        container.AddChild(rect);
        container.SetMeta("portrait_rect", rect);
        return container;
    }
}
