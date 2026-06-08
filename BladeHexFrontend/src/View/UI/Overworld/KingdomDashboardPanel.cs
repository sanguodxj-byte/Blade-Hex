// KingdomDashboardPanel.cs
// 玩家王国管理面板 — 概览 + 领土 + Lords + 法律
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic.Kingdom;
using BladeHex.Strategic.Hero;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 玩家王国管理面板
/// </summary>
[GlobalClass]
public partial class KingdomDashboardPanel : CanvasLayer
{
    [Signal]
    public delegate void PanelClosedEventHandler();

    private PlayerKingdom? _kingdom;
    private HeroRegistry? _heroRegistry;
    private VBoxContainer _mainContainer = null!;
    private TabContainer _tabContainer = null!;

    public override void _Ready()
    {
        Visible = false;
    }

    /// <summary>显示王国面板</summary>
    public void ShowPanel(PlayerKingdom kingdom, HeroRegistry heroRegistry)
    {
        _kingdom = kingdom;
        _heroRegistry = heroRegistry;

        // 清除旧内容
        foreach (var child in GetChildren())
            child.QueueFree();

        // 创建面板
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(600, 500);
        OverlayPanelLayout.Center(panel);

        // 样式
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.4f, 0.35f, 0.2f, 0.8f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 15,
            ContentMarginBottom = 15
        };
        panel.AddThemeStyleboxOverride("panel", style);
        AddChild(panel);

        _mainContainer = new VBoxContainer();
        panel.AddChild(_mainContainer);

        // 标题栏
        var titleHbox = new HBoxContainer();
        _mainContainer.AddChild(titleHbox);

        var titleLabel = new Label { Text = $"🏰 {kingdom.DisplayName}" };
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleHbox.AddChild(titleLabel);

        var closeBtn = new Button { Text = " X " };
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => ClosePanel();
        titleHbox.AddChild(closeBtn);

        _mainContainer.AddChild(new HSeparator());

        // Tab 容器
        _tabContainer = new TabContainer();
        _tabContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _mainContainer.AddChild(_tabContainer);

        // 概览 Tab
        _tabContainer.AddChild(CreateOverviewTab());

        // 领土 Tab
        _tabContainer.AddChild(CreateTerritoryTab());

        // Lords Tab
        _tabContainer.AddChild(CreateLordsTab());

        // 法律 Tab
        _tabContainer.AddChild(CreateLawsTab());

        Visible = true;
    }

    private Control CreateOverviewTab()
    {
        var container = new VBoxContainer();
        container.Name = "概览";

        // 国家信息
        AddLabel(container, $"国名: {_kingdom!.DisplayName}", 18);
        AddLabel(container, $"家族: {_kingdom.FamilyName}", 16);
        AddLabel(container, $"都城: {_kingdom.CapitalPoiName}", 16);
        AddLabel(container, $"创立: 第 {_kingdom.FoundedDay} 天", 16);

        container.AddChild(new HSeparator());

        // 统计
        AddLabel(container, $"领土: {_kingdom.PoiCount} 个 POI", 16);
        AddLabel(container, $"领主: {_kingdom.LordCount} 位", 16);

        container.AddChild(new HSeparator());

        // 法律摘要
        AddLabel(container, "当前法律:", 16);
        AddLabel(container, $"  征兵: {GetConscriptionName(_kingdom.Laws.Conscription)}", 14);
        AddLabel(container, $"  税率: {GetTaxName(_kingdom.Laws.TaxRate)}", 14);
        AddLabel(container, $"  宗教: {GetReligionName(_kingdom.Laws.Religion)}", 14);
        AddLabel(container, $"  贸易: {GetTradeName(_kingdom.Laws.Trade)}", 14);

        return container;
    }

    private Control CreateTerritoryTab()
    {
        var container = new VBoxContainer();
        container.Name = "领土";

        AddLabel(container, "控制的据点:", 16);
        container.AddChild(new HSeparator());

        foreach (var poiName in _kingdom!.ControlledPoiNames)
        {
            var hbox = new HBoxContainer();
            container.AddChild(hbox);

            var poiLabel = new Label { Text = $"  • {poiName}" };
            poiLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            poiLabel.AddThemeFontSizeOverride("font_size", 14);
            hbox.AddChild(poiLabel);

            // 查找领主
            string lordName = "玩家";
            // TODO: 从 OverworldEntity 查找 LordHeroId
            var lordLabel = new Label { Text = $"领主: {lordName}" };
            lordLabel.AddThemeFontSizeOverride("font_size", 12);
            lordLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            hbox.AddChild(lordLabel);
        }

        if (_kingdom.ControlledPoiNames.Count == 0)
        {
            AddLabel(container, "  （暂无领土）", 14);
        }

        return container;
    }

    private Control CreateLordsTab()
    {
        var container = new VBoxContainer();
        container.Name = "Lords";

        AddLabel(container, "家族成员:", 16);
        container.AddChild(new HSeparator());

        foreach (var heroId in _kingdom!.LordHeroIds)
        {
            var hero = _heroRegistry?.Get(heroId);
            string displayName = hero?.DisplayName ?? heroId;
            string role = heroId == "player" ? "（国王）" : "（领主）";

            var hbox = new HBoxContainer();
            container.AddChild(hbox);

            var nameLabel = new Label { Text = $"  • {displayName} {role}" };
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            hbox.AddChild(nameLabel);
        }

        return container;
    }

    private Control CreateLawsTab()
    {
        var container = new VBoxContainer();
        container.Name = "法律";

        AddLabel(container, "王国法律:", 16);
        container.AddChild(new HSeparator());

        // 征兵权
        var conscriptionHbox = new HBoxContainer();
        container.AddChild(conscriptionHbox);
        AddLabel(conscriptionHbox, "征兵权: ", 14);
        var conscriptionOption = new OptionButton();
        conscriptionOption.AddItem("常规", (int)ConscriptionLaw.Standard);
        conscriptionOption.AddItem("全民动员 (+50% 招募, -10 忠诚)", (int)ConscriptionLaw.Major);
        conscriptionOption.AddItem("贵族独占 (-30% 招募, +5 忠诚)", (int)ConscriptionLaw.Aristocracy);
        conscriptionOption.Selected = (int)_kingdom!.Laws.Conscription;
        conscriptionHbox.AddChild(conscriptionOption);

        // 税率
        var taxHbox = new HBoxContainer();
        container.AddChild(taxHbox);
        AddLabel(taxHbox, "税率:     ", 14);
        var taxOption = new OptionButton();
        taxOption.AddItem("低税 15% (-25% 收入, +10 忠诚)", (int)TaxLaw.Low);
        taxOption.AddItem("中税 20% (标准)", (int)TaxLaw.Medium);
        taxOption.AddItem("高税 25% (+25% 收入, -10 忠诚)", (int)TaxLaw.High);
        taxOption.Selected = (int)_kingdom.Laws.TaxRate;
        taxHbox.AddChild(taxOption);

        // 宗教
        var religionHbox = new HBoxContainer();
        container.AddChild(religionHbox);
        AddLabel(religionHbox, "宗教:     ", 14);
        var religionOption = new OptionButton();
        religionOption.AddItem("宽容 (多元共存)", (int)ReligionLaw.Tolerant);
        religionOption.AddItem("国教 (同教+20, 异教-20)", (int)ReligionLaw.StateReligion);
        religionOption.AddItem("迫害 (同教+10, 异教-30)", (int)ReligionLaw.Persecution);
        religionOption.Selected = (int)_kingdom.Laws.Religion;
        religionHbox.AddChild(religionOption);

        // 贸易
        var tradeHbox = new HBoxContainer();
        container.AddChild(tradeHbox);
        AddLabel(tradeHbox, "贸易:     ", 14);
        var tradeOption = new OptionButton();
        tradeOption.AddItem("自由贸易", (int)TradeLaw.Free);
        tradeOption.AddItem("保护主义 (+20% 本国收入)", (int)TradeLaw.Protected);
        tradeOption.AddItem("禁运 (-50% 收入, 敌国-20%)", (int)TradeLaw.Embargo);
        tradeOption.Selected = (int)_kingdom.Laws.Trade;
        tradeHbox.AddChild(tradeOption);

        container.AddChild(new HSeparator());

        // 应用按钮
        var applyBtn = new Button { Text = "应用法律变更" };
        applyBtn.Pressed += () =>
        {
            _kingdom.Laws.Conscription = (ConscriptionLaw)conscriptionOption.Selected;
            _kingdom.Laws.TaxRate = (TaxLaw)taxOption.Selected;
            _kingdom.Laws.Religion = (ReligionLaw)religionOption.Selected;
            _kingdom.Laws.Trade = (TradeLaw)tradeOption.Selected;
            GD.Print("[KingdomDashboard] 法律已更新");
        };
        container.AddChild(applyBtn);

        return container;
    }

    private void AddLabel(Container parent, string text, int fontSize)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        parent.AddChild(label);
    }

    private void ClosePanel()
    {
        Visible = false;
        EmitSignal(SignalName.PanelClosed);
    }

    private static string GetConscriptionName(ConscriptionLaw law) => law switch
    {
        ConscriptionLaw.Standard => "常规",
        ConscriptionLaw.Major => "全民动员",
        ConscriptionLaw.Aristocracy => "贵族独占",
        _ => "未知"
    };

    private static string GetTaxName(TaxLaw law) => law switch
    {
        TaxLaw.Low => "低税 (15%)",
        TaxLaw.Medium => "中税 (20%)",
        TaxLaw.High => "高税 (25%)",
        _ => "未知"
    };

    private static string GetReligionName(ReligionLaw law) => law switch
    {
        ReligionLaw.Tolerant => "宽容",
        ReligionLaw.StateReligion => "国教",
        ReligionLaw.Persecution => "迫害",
        _ => "未知"
    };

    private static string GetTradeName(TradeLaw law) => law switch
    {
        TradeLaw.Free => "自由贸易",
        TradeLaw.Protected => "保护主义",
        TradeLaw.Embargo => "禁运",
        _ => "未知"
    };
}
