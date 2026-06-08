using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Encyclopedia;

/// <summary>
/// 据点百科磨砂详情面板
/// </summary>
public partial class PoiDetailPanel : PanelContainer
{
    private static readonly Color BgPanel = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);

    private OverworldPOI _poi;
    private OverworldEntityManager _entityMgr;

    public static void ShowDetail(OverworldPOI poi, OverworldEntityManager entityMgr, Node parent)
    {
        var panel = new PoiDetailPanel(poi, entityMgr);
        OverlayPanelLayout.AttachModal(parent, panel);
    }

    public PoiDetailPanel(OverworldPOI poi, OverworldEntityManager entityMgr)
    {
        _poi = poi;
        _entityMgr = entityMgr;
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

        CustomMinimumSize = new Vector2(500, 360);
        OverlayPanelLayout.Center(this);

        // 3. 布局组装
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddThemeConstantOverride("separation", 10);
        AddChild(mainVbox);

        // Header 行
        var header = new HBoxContainer();
        mainVbox.AddChild(header);

        var titleLabel = _MakeLabel($"✦  {_poi.PoiName}  ✦", 18, TextAccent);
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

        string typeStr = _poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Town => _poi.IsPortCity ? "港口城市 (水运枢纽)" : "城镇 (大型都市)",
            OverworldPOI.POIType.Castle => "城堡 (要塞封地)",
            OverworldPOI.POIType.Village => "村庄 (附属聚落)",
            OverworldPOI.POIType.Mine => "矿场 (附属资源点)",
            OverworldPOI.POIType.Farm => "农庄 (附属资源点)",
            _ => _poi.PoiTypeEnum.ToString()
        };
        _AddLabelPair(grid, "🏰 据点类型:", typeStr);

        // 所属势力
        string factionName = _poi.OwningFaction;
        var nation = _entityMgr.Nations.FirstOrDefault(n => n.Id == _poi.OwningFaction);
        if (nation != null) factionName = nation.DisplayName;
        else if (_poi.OwningFaction == "neutral") factionName = "中立";
        _AddLabelPair(grid, "🏳 归属势力:", factionName);

        // 繁荣度
        _AddLabelPair(grid, "📈 繁荣程度:", $"{_poi.Prosperity} / 100");

        // 守军规模
        _AddLabelPair(grid, "🛡 驻守军队:", $"{_poi.GarrisonCurrent} / {(_poi.GarrisonMax > 0 ? _poi.GarrisonMax.ToString() : "无上限")}");

        // 如果是城堡，显示防御等级
        if (_poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
        {
            string defStr = _poi.CastleDefenseLevel switch
            {
                1 => "木栅栏 (防御等级: 1)",
                2 => "石质城墙 (防御等级: 2)",
                3 => "铁壁坚城 (防御等级: 3)",
                _ => _poi.CastleDefenseLevel.ToString()
            };
            _AddLabelPair(grid, "🧱 城堡防务:", defStr);
        }

        var midSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        mainVbox.AddChild(midSep);

        // 据点设施
        mainVbox.AddChild(_MakeLabel("🏪 已设立据点设施:", 14, TextAccent));
        
        var facilities = new List<string>();
        if (_poi.HasTavern) facilities.Add("🍻 旅店 (可招集雇佣兵/听取传闻)");
        if (_poi.HasShop) facilities.Add("🛒 商店 (大宗物资与跑商贸易)");
        if (_poi.HasBlacksmith) facilities.Add("🔨 铁匠铺 (装备改造与补给)");
        if (_poi.HasQuestBoard) facilities.Add("📋 任务板 (承接领地悬赏)");
        if (_poi.HasBarracks) facilities.Add("⚔ 军营 (精锐驻防与扩招)");

        if (facilities.Count == 0)
        {
            facilities.Add("（该地点目前较为荒凉，未设立任何功能性设施）");
        }

        var facGrid = new GridContainer { Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        facGrid.AddThemeConstantOverride("v_separation", 4);
        mainVbox.AddChild(facGrid);

        foreach (var fac in facilities)
        {
            var facLabel = _MakeLabel($"  {fac}", 13, TextPrimary);
            facGrid.AddChild(facLabel);
        }
    }

    private void _AddLabelPair(GridContainer grid, string key, string val)
    {
        var k = _MakeLabel(key, 14, TextSecondary);
        grid.AddChild(k);

        var v = _MakeLabel(val, 14, TextPrimary);
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
