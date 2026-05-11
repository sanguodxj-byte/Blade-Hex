using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.UI;

/// <summary>
/// UI主题系统 —— 集中管理所有设计令牌（颜色、字号、间距、圆角等）
/// 迁移自 GDScript UITheme.gd
/// </summary>
public partial class UITheme : Node
{
    private static UITheme? _instance;
    public static UITheme Instance
    {
        get
        {
            if (_instance == null || !GodotObject.IsInstanceValid(_instance))
            {
                _instance = new UITheme();
                // 在 C# 中通常不建议直接在 getter 中 AddChild 到根节点，
                // 但为了保持与 GDScript 逻辑一致：
                var root = ((SceneTree)Engine.GetMainLoop()).Root;
                root.CallDeferred(Node.MethodName.AddChild, _instance);
            }
            return _instance;
        }
    }

    public enum ThemeMode { ProceduralDark, ImageBased }
    public ThemeMode CurrentMode = ThemeMode.ProceduralDark;

    // --- 调色板 ---
    public Color BgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    public Color BgSecondary = new(0.12f, 0.12f, 0.14f, 0.80f);
    public Color BgTertiary = new(0.06f, 0.06f, 0.08f, 0.75f);
    public Color BgPanel = new(0.10f, 0.10f, 0.12f, 0.85f);
    public Color BgCard = new(0.15f, 0.14f, 0.18f, 0.75f);
    public Color BgCardHover = new(0.25f, 0.22f, 0.30f, 0.90f);
    public Color BgOverlay = new(0, 0, 0, 0.4f);
    public Color BgTooltip = new(0.06f, 0.05f, 0.09f, 0.95f);

    public Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    public Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    public Color BorderFriendly = new(0.2f, 0.5f, 0.8f, 0.8f);
    public Color BorderEnemy = new(0.6f, 0.2f, 0.2f, 0.8f);
    public Color BorderMagic = new(0.4f, 0.35f, 0.6f, 0.8f);

    public Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    public Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    public Color TextMuted = new(0.5f, 0.48f, 0.45f);
    public Color TextAccent = new(0.9f, 0.8f, 0.5f);
    public Color TextPositive = new(0.3f, 0.85f, 0.3f);
    public Color TextNegative = new(0.9f, 0.3f, 0.25f);
    public Color TextMagic = new(0.7f, 0.6f, 1.0f);
    public Color TextWarning = new(0.9f, 0.7f, 0.2f);

    public Color HpHigh = new(0.2f, 0.75f, 0.2f);
    public Color HpMid = new(0.85f, 0.75f, 0.1f);
    public Color HpLow = new(0.9f, 0.15f, 0.1f);
    public Color HpBarBg = new(0.15f, 0.08f, 0.08f, 0.7f);

    public Color ManaFill = new(0.3f, 0.5f, 1.0f);
    public Color ManaBg = new(0.1f, 0.1f, 0.2f, 0.7f);

    public Color XpFill = new(0.6f, 0.5f, 0.9f);
    public Color XpBg = new(0.1f, 0.08f, 0.15f, 0.7f);

    // --- 字号 ---
    public int FontSizeXs = 10;
    public int FontSizeSm = 12;
    public int FontSizeMd = 14;
    public int FontSizeLg = 16;
    public int FontSizeXl = 20;
    public int FontSizeXxl = 24;
    public int FontSizeTitle = 28;

    // --- 间距 ---
    public int SpacingXs = 2;
    public int SpacingSm = 4;
    public int SpacingMd = 8;
    public int SpacingLg = 12;
    public int SpacingXl = 16;

    // --- 圆角 ---
    public int RadiusSm = 4;
    public int RadiusMd = 8;
    public int RadiusLg = 12;
    public int RadiusRound = 24;

    // --- 尺寸 ---
    public int ButtonHeight = 36;
    public int ButtonHeightLg = 48;
    public int BarHeightMd = 12;
    public int PortraitSize = 80;
    public int IconSizeLg = 48;

    // --- 辅助方法 ---

    public StyleBoxFlat MakePanelStyle(Color bg = default, Color border = default, int borderWidth = 1, int cornerRadius = -1, int contentMargin = -1)
    {
        bg = bg == default ? BgPanel : bg;
        border = border == default ? BorderDefault : border;
        cornerRadius = cornerRadius == -1 ? RadiusMd : cornerRadius;
        contentMargin = contentMargin == -1 ? SpacingMd : contentMargin;

        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            ShadowColor = new Color(0, 0, 0, 0.35f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(2, 2)
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(cornerRadius);
        style.SetContentMarginAll(contentMargin);
        return style;
    }

    public void ApplyButtonTheme(Button btn)
    {
        var normal = MakeBtnStyle(new Color(0.18f, 0.17f, 0.22f), BorderDefault, RadiusMd);
        var hover = MakeBtnStyle(new Color(0.28f, 0.26f, 0.34f), BorderHighlight, RadiusMd);
        var pressed = MakeBtnStyle(new Color(0.12f, 0.11f, 0.15f), BorderHighlight, RadiusMd);
        var disabled = MakeBtnStyle(new Color(0.12f, 0.12f, 0.12f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 0.3f), RadiusMd);

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("disabled", disabled);
        
        btn.AddThemeColorOverride("font_color", TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", TextAccent);
        btn.AddThemeColorOverride("font_pressed_color", TextSecondary);
        btn.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.4f, 0.4f));
    }

    private StyleBoxFlat MakeBtnStyle(Color bg, Color border, int cr)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border
        };
        s.SetBorderWidthAll(1);
        s.SetCornerRadiusAll(cr);
        s.SetContentMarginAll(SpacingSm);
        return s;
    }

    public void ApplyBarTheme(ProgressBar bar, Color fillColor, Color bgColor = default)
    {
        bgColor = bgColor == default ? new Color(0.1f, 0.1f, 0.12f) : bgColor;
        
        var fill = new StyleBoxFlat { BgColor = fillColor };
        fill.SetCornerRadiusAll(RadiusSm);
        
        var bg = new StyleBoxFlat { BgColor = bgColor };
        bg.SetCornerRadiusAll(RadiusSm);

        bar.AddThemeStyleboxOverride("fill", fill);
        bar.AddThemeStyleboxOverride("background", bg);
    }
}
