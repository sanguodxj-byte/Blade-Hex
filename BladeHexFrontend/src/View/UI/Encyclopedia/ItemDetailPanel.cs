using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic.Economy;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Encyclopedia;

/// <summary>
/// 物品百科磨砂详情面板
/// </summary>
public partial class ItemDetailPanel : PanelContainer
{
    private static readonly Color BgPanel = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);

    private ItemData _item;

    public static void ShowDetail(ItemData item, Node parent)
    {
        var panel = new ItemDetailPanel(item);
        OverlayPanelLayout.AttachModal(parent, panel);
    }

    public ItemDetailPanel(ItemData item)
    {
        _item = item;
    }

    public override void _Ready()
    {
        // 1. Panel 样式 - 通透玻璃暗金外阴影
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 0.97f),
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.6f, 0.5f, 0.35f, 0.85f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ContentMarginLeft = 25,
            ContentMarginRight = 25,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            ShadowSize = 12,
            ShadowColor = new Color(0, 0, 0, 0.6f)
        };
        AddThemeStyleboxOverride("panel", style);

        CustomMinimumSize = new Vector2(520, 370);
        OverlayPanelLayout.Center(this);

        // 3. 布局组装
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddThemeConstantOverride("separation", 10);
        AddChild(mainVbox);

        // Header 行
        var header = new HBoxContainer();
        mainVbox.AddChild(header);

        var titleLabel = _MakeLabel($"✦  {_item.ItemName}  ✦", 24, _item.GetRarityColor());
        header.AddChild(titleLabel);

        var closeBtn = new Button();
        _StyleCloseButton(closeBtn);
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => OverlayPanelLayout.CloseModal(this);
        header.AddChild(closeBtn);

        var headerSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.25f) };
        mainVbox.AddChild(headerSep);

        // 属性网格
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 8);
        mainVbox.AddChild(grid);

        _AddLabelPair(grid, "🆔 物品标识:", _item.ItemId);
        _AddLabelPair(grid, "💎 稀有品质:", _item.GetRarityName(), _item.GetRarityColor());
        _AddLabelPair(grid, "🛡 装备部位:", _item.EquipSlotTarget.ToString());
        _AddLabelPair(grid, "⚖ 物品重量:", $"{_item.Weight} kg");
        _AddLabelPair(grid, "🪙 基础价格:", $"{_item.Price} 金币");
        _AddLabelPair(grid, "💰 预估售价:", $"{TradePricingService.GetSellPrice(_item)} 金币");

        var midSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        mainVbox.AddChild(midSep);

        // 描述文本
        var descTitle = _MakeLabel("📝 物品描述:", 16, TextSecondary);
        mainVbox.AddChild(descTitle);

        string desc = !string.IsNullOrEmpty(_item.Description) ? _item.Description : "暂无此物品的具体描述。";
        var descLabel = new Label { Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        descLabel.AddThemeFontSizeOverride("font_size", 14);
        descLabel.AddThemeColorOverride("font_color", TextPrimary);
        mainVbox.AddChild(descLabel);

        // 建议生产作坊
        var recipe = WorkshopRecipeRegistry.GetAll().FirstOrDefault(r => r.OutputItemId == _item.ItemId);
        if (recipe != null)
        {
            var botSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
            mainVbox.AddChild(botSep);
            string workshopName = recipe.Type switch
            {
                BladeHex.Strategic.FiefBuilding.BuildingType.BlacksmithWorkshop => "铁匠铺作坊",
                BladeHex.Strategic.FiefBuilding.BuildingType.BrewWorkshop => "酿酒厂作坊",
                BladeHex.Strategic.FiefBuilding.BuildingType.TextileWorkshop => "织造厂作坊",
                BladeHex.Strategic.FiefBuilding.BuildingType.TanneryWorkshop => "皮革厂作坊",
                _ => "未知作坊"
            };
            var recLabel = _MakeLabel($"⚙️ 生产作坊: 可由【{workshopName}】生产 (日产: {recipe.OutputQty}，成本: {recipe.RawCostGold}金/日)", 14, TextAccent);
            mainVbox.AddChild(recLabel);
        }
    }

    private void _AddLabelPair(GridContainer grid, string key, string val, Color? customValColor = null)
    {
        var k = _MakeLabel(key, 16, TextSecondary);
        grid.AddChild(k);

        var v = _MakeLabel(val, 16, customValColor ?? TextPrimary);
        grid.AddChild(v);
    }

    private static Label _MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static void _StyleCloseButton(Button closeBtn)
    {
        closeBtn.Text = "✕";
        closeBtn.FocusMode = Control.FocusModeEnum.None;
        var btnStyleNormal = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0f) };
        var btnStyleHover = new StyleBoxFlat {
            BgColor = new Color(0.9f, 0.3f, 0.25f, 0.4f),
            CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15
        };
        var btnStylePressed = new StyleBoxFlat {
            BgColor = new Color(0.9f, 0.3f, 0.25f, 0.6f),
            CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15
        };
        closeBtn.AddThemeStyleboxOverride("normal", btnStyleNormal);
        closeBtn.AddThemeStyleboxOverride("hover", btnStyleHover);
        closeBtn.AddThemeStyleboxOverride("pressed", btnStylePressed);
        closeBtn.AddThemeStyleboxOverride("focus", btnStyleNormal);
        closeBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.63f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
        closeBtn.AddThemeFontSizeOverride("font_size", 16);
        closeBtn.CustomMinimumSize = new Vector2(30, 30);
    }
}
