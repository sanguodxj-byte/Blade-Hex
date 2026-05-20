// ItemGridWidget.cs
// 物品网格控件工厂 — 共享物品块的视觉渲染（背包、商店、战利品都用同一套）
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.View.Data;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 物品网格控件构建器。
/// 负责创建一个表示物品的 Panel：图标 + 名称 + 数量/价格角标。
/// 不包含事件绑定 — 由调用者负责 GuiInput 和拖拽。
/// </summary>
public static class ItemGridWidget
{
    /// <summary>
    /// 创建一个物品控件，基于格子尺寸自动布局。
    /// </summary>
    /// <param name="item">物品数据</param>
    /// <param name="cellSize">单格像素大小</param>
    /// <param name="cellGap">格间距</param>
    /// <param name="quantity">堆叠数量（>1 才显示）</param>
    /// <param name="overlay">右上角覆盖文字（如价格 "20金"）</param>
    /// <param name="overlayColor">覆盖文字颜色</param>
    public static Panel Create(
        ItemData item,
        int cellSize,
        int cellGap,
        int quantity = 1,
        string? overlay = null,
        Color? overlayColor = null)
    {
        ItemSizeConfig.ApplyRecommendedSize(item);

        int pw = item.InvWidth * (cellSize + cellGap) - cellGap;
        int ph = item.InvHeight * (cellSize + cellGap) - cellGap;

        var w = new Panel();
        w.Size = new Vector2(pw, ph);
        w.CustomMinimumSize = new Vector2(pw, ph);
        w.MouseFilter = Control.MouseFilterEnum.Stop;
        w.ZIndex = 1;
        w.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        w.ClipContents = true;

        // 背景样式（按稀有度着色）
        var rc = item.GetRarityColor();
        var bg = new Color(0.13f + rc.R * 0.04f, 0.12f + rc.G * 0.04f, 0.16f + rc.B * 0.04f, 0.95f);
        var ws = new StyleBoxFlat { BgColor = bg };
        ws.SetBorderWidthAll(1);
        ws.BorderColor = new Color(rc.R * 0.7f, rc.G * 0.7f, rc.B * 0.7f, 0.85f);
        ws.SetCornerRadiusAll(2);
        w.AddThemeStyleboxOverride("panel", ws);

        // 图标 — 优先 ResourceRegistry 加载真实纹理，缺失则使用程序化占位符
        Texture2D? tex = !string.IsNullOrEmpty(item.IconId)
            ? (ResourceRegistry.GetIcon(item.IconId) ?? ResourceRegistry.GetIcon(item.IconFallbackId))
            : null;

        if (tex != null)
        {
            var icon = new TextureRect();
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = 6; icon.OffsetTop = 6; icon.OffsetRight = -6; icon.OffsetBottom = -24;
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            float aspect = (float)item.InvWidth / item.InvHeight;
            icon.StretchMode = (aspect < 0.6f || aspect > 1.7f)
                ? TextureRect.StretchModeEnum.KeepAspectCovered
                : TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.MouseFilter = Control.MouseFilterEnum.Ignore;
            icon.Texture = tex;
            w.AddChild(icon);
        }
        else
        {
            // 程序化占位符
            var placeholder = new ItemPlaceholderRenderer
            {
                Shape = ItemPlaceholderRenderer.GetShapeForItem(item),
                MainColor = item.GetRarityColor(),
                BgColor = new Color(0.08f, 0.08f, 0.10f, 0.5f),
            };
            placeholder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            placeholder.OffsetLeft = 6; placeholder.OffsetTop = 6;
            placeholder.OffsetRight = -6; placeholder.OffsetBottom = -24;
            w.AddChild(placeholder);
        }

        // 名称（底部）
        int maxC = item.InvWidth * 3;
        string displayName = item.ItemName.Length > maxC ? item.ItemName[..maxC] : item.ItemName;
        var nl = new Label { Text = displayName };
        nl.AddThemeFontSizeOverride("font_size", 18);
        nl.AddThemeColorOverride("font_color", rc);
        nl.HorizontalAlignment = HorizontalAlignment.Center;
        nl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        nl.OffsetTop = -22; nl.OffsetBottom = 0;
        nl.MouseFilter = Control.MouseFilterEnum.Ignore;
        w.AddChild(nl);

        // 堆叠数量（右上）
        if (quantity > 1)
        {
            var ql = new Label { Text = $"×{quantity}" };
            ql.AddThemeFontSizeOverride("font_size", 20);
            ql.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f));
            ql.HorizontalAlignment = HorizontalAlignment.Right;
            ql.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
            ql.OffsetRight = -4; ql.OffsetTop = 2;
            ql.MouseFilter = Control.MouseFilterEnum.Ignore;
            w.AddChild(ql);
        }
        if (!string.IsNullOrEmpty(overlay))
        {
            var ol = new Label { Text = overlay };
            ol.AddThemeFontSizeOverride("font_size", 18);
            ol.AddThemeColorOverride("font_color", overlayColor ?? new Color(0.3f, 0.85f, 0.3f));
            ol.HorizontalAlignment = HorizontalAlignment.Right;
            ol.SetAnchorsAndOffsetsPreset(quantity > 1 ? Control.LayoutPreset.BottomRight : Control.LayoutPreset.TopRight);
            ol.OffsetRight = -4; ol.OffsetTop = quantity > 1 ? -22 : 2; ol.OffsetBottom = quantity > 1 ? -2 : 0;
            ol.MouseFilter = Control.MouseFilterEnum.Ignore;
            w.AddChild(ol);
        }

        return w;
    }
}
