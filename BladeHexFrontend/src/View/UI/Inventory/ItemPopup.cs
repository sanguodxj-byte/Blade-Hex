// ItemPopup.cs
// 物品详情悬浮窗 — 右键点击物品时显示
// 完全独立组件，由 DragController 或容器调用
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 浮动物品详情面板。
/// 使用 TopLevel + ZIndex 100 确保始终在最顶层。
/// </summary>
[GlobalClass]
public partial class ItemPopup : PanelContainer
{
    private RichTextLabel _text = null!;

    public override void _Ready()
    {
        Visible = false;
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = true;
        CustomMinimumSize = new Vector2(220, 0);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.96f);
        style.SetBorderWidthAll(1);
        style.BorderColor = new Color(0.45f, 0.4f, 0.3f, 0.9f);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(10);
        style.ShadowColor = new Color(0, 0, 0, 0.5f);
        style.ShadowSize = 4;
        AddThemeStyleboxOverride("panel", style);

        _text = new RichTextLabel();
        _text.BbcodeEnabled = true;
        _text.ScrollActive = false;
        _text.FitContent = true;
        _text.CustomMinimumSize = new Vector2(200, 0);
        _text.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_text);
    }

    /// <summary>在指定屏幕位置显示物品详情</summary>
    public void ShowFor(ItemData item, Vector2 mousePos)
    {
        _text.Text = BuildText(item);
        Visible = true;

        // 智能定位：默认在鼠标右侧，超出屏幕则放左侧
        var vpSize = GetViewport().GetVisibleRect().Size;
        float popupX = mousePos.X + 12;
        float popupY = mousePos.Y - 20;
        if (popupX + 240 > vpSize.X) popupX = mousePos.X - 252;
        if (popupY + 200 > vpSize.Y) popupY = vpSize.Y - 200;
        if (popupY < 0) popupY = 0;
        GlobalPosition = new Vector2(popupX, popupY);
    }

    public new void Hide() => Visible = false;

    private static string BuildText(ItemData item)
    {
        var rc = item.GetRarityColor().ToHtml(false);
        string text = $"[color=#{rc}][b]{item.GetFullName()}[/b][/color]\n";
        text += $"[color=#999]{item.GetRarityName()}[/color]";

        if (item is WeaponData wpn)
            text += $"\n\n[color=#ddd]{wpn.GetWeaponDescription()}[/color]";
        else if (item is ArmorData arm)
        {
            text += $"\n\n[color=#ddd]装甲: {arm.MaxArmorPoints} | 穿透阈值: {arm.DrThreshold}[/color]";
            if (arm.MaxDexBonus < 99)
                text += $"\n[color=#ddd]DEX上限: {arm.MaxDexBonus}[/color]";
        }

        if (!string.IsNullOrEmpty(item.Description))
            text += $"\n\n[color=#aaa]{item.Description}[/color]";

        text += $"\n\n[color=#666]占用: {item.InvWidth}×{item.InvHeight} 格[/color]";
        text += $"\n[color=#666]价值: {item.GetSellPrice()} 金[/color]";

        string affixes = item.GetAffixDescriptions();
        if (!string.IsNullOrEmpty(affixes))
            text += $"\n\n[color=#b07de8]{affixes}[/color]";

        return text;
    }
}
