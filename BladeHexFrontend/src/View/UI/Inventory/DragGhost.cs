// DragGhost.cs
// 拖拽幽灵 — 鼠标跟随的半透明物品预览
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 拖拽过程中跟随鼠标的幽灵面板。
/// TopLevel = true 确保使用全局坐标，不受父布局影响。
/// </summary>
[GlobalClass]
public partial class DragGhost : Panel
{
    /// <summary>创建一个拖拽幽灵</summary>
    public static DragGhost Create(ItemData item, Vector2 size)
    {
        var ghost = new DragGhost
        {
            ZIndex = 80,
            MouseFilter = MouseFilterEnum.Ignore,
            TopLevel = true,
            CustomMinimumSize = size,
            Size = size,
            Modulate = new Color(1, 1, 1, 0.7f),
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.12f, 0.2f, 0.85f)
        };
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.85f, 0.72f, 0.25f, 0.95f);
        style.SetCornerRadiusAll(3);
        ghost.AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = item.ItemName,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", item.GetRarityColor());
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ghost.AddChild(label);

        return ghost;
    }
}
