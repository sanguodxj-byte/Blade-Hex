// PortPanel.cs
// 港口面板 — 进入港口时显示贸易/渡船/情报等功能
// 布局：居中 480×520 弹窗，上方港口插画 + 标题/信息 + 下方功能按钮列表
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class PortPanel : POIPanelBase
{
    // ============================================================================
    // 面板规格
    // ============================================================================
    protected override int PanelWidth => 480;
    protected override int PanelHeight => 520;

    // 面板特有常量
    private static readonly Color BgIllust = new(0.04f, 0.08f, 0.16f, 1.0f);
    private static readonly Color TextPortAccent = new(0.6f, 0.85f, 0.95f);

    private const int IllustHeight = 100;
    private const int BtnHeight = 48;
    private const int OptionSpacing = 8;

    // ============================================================================
    // 信号
    // ============================================================================

    /// <summary>玩家选择了港口功能</summary>
    [Signal] public delegate void PortActionSelectedEventHandler(string actionId);

    /// <summary>玩家选择了渡船目的地</summary>
    [Signal] public delegate void FerryDestinationSelectedEventHandler(string destinationName);

    /// <summary>关闭面板</summary>
    [Signal] public delegate void LeavePortEventHandler();

    // ============================================================================
    // 字段
    // ============================================================================
    private ColorRect _illustRect = null!;
    private Label _titleLabel = null!;
    private Label _infoLabel = null!;
    private RichTextLabel _descLabel = null!;
    private VBoxContainer _actionsVbox = null!;
    private VBoxContainer _ferryVbox = null!;
    private Control _ferrySection = null!;
    private OverworldPOI? _currentPort;

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>显示港口面板</summary>
    public void ShowPort(OverworldPOI port)
    {
        _currentPort = port;
        _titleLabel.Text = port.PoiName;
        _infoLabel.Text = $"港口 · 繁荣: {port.Prosperity} · 渡船费: {port.FerryCost}金";
        _descLabel.Text = GetPortDescription(port);
        PopulateActions(port);
        PopulateFerryDestinations(port);
        Root.Visible = true;
    }

    /// <summary>隐藏面板</summary>
    public override void HidePanel()
    {
        base.HidePanel();
        _currentPort = null;
        ClearActions();
        ClearFerryDestinations();
    }

    // ============================================================================
    // 关闭处理
    // ============================================================================
    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.LeavePort);
        HidePanel();
    }

    // ============================================================================
    // 内容构建
    // ============================================================================
    protected override void BuildContent(VBoxContainer container)
    {
        container.AddThemeConstantOverride("separation", 0);

        // 插画区
        BuildIllustration(container);

        // 文字内容区
        BuildTextContent(container);

        // 分隔线
        container.AddChild(CreateSeparatorH());

        // 功能按钮区
        BuildActionsArea(container);

        // 渡船目的地区
        BuildFerrySection(container);

        // 分隔线
        container.AddChild(CreateSeparatorH());

        // 离开按钮
        BuildLeaveButton(container);
    }

    private void BuildIllustration(VBoxContainer parent)
    {
        var illustPanel = new PanelContainer();
        illustPanel.CustomMinimumSize = new Vector2(0, IllustHeight);
        var style = new StyleBoxFlat { BgColor = BgIllust };
        style.SetContentMarginAll(0);
        illustPanel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(illustPanel);

        _illustRect = new ColorRect();
        _illustRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _illustRect.Color = BgIllust;
        illustPanel.AddChild(_illustRect);

        // 港口图标文字（占位）
        var iconLabel = new Label();
        iconLabel.Text = "⚓";
        iconLabel.AddThemeFontSizeOverride("font_size", 40);
        iconLabel.AddThemeColorOverride("font_color", TextPortAccent);
        iconLabel.HorizontalAlignment = HorizontalAlignment.Center;
        iconLabel.VerticalAlignment = VerticalAlignment.Center;
        iconLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        illustPanel.AddChild(iconLabel);
    }

    private void BuildTextContent(VBoxContainer parent)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", PanelMargin);
        margin.AddThemeConstantOverride("margin_right", PanelMargin);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        parent.AddChild(margin);

        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(textVbox);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", FontSizeXl);
        _titleLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        textVbox.AddChild(_titleLabel);

        _infoLabel = new Label();
        _infoLabel.AddThemeFontSizeOverride("font_size", FontSizeSm);
        _infoLabel.AddThemeColorOverride("font_color", ThemeTextMuted);
        textVbox.AddChild(_infoLabel);

        _descLabel = new RichTextLabel();
        _descLabel.BbcodeEnabled = true;
        _descLabel.ScrollActive = false;
        _descLabel.FitContent = true;
        _descLabel.CustomMinimumSize = new Vector2(0, 40);
        _descLabel.AddThemeFontSizeOverride("normal_font_size", FontSizeMd);
        _descLabel.AddThemeColorOverride("default_color", ThemeTextSecondary);
        textVbox.AddChild(_descLabel);
    }

    private void BuildActionsArea(VBoxContainer parent)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", PanelMargin);
        margin.AddThemeConstantOverride("margin_right", PanelMargin);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        parent.AddChild(margin);

        _actionsVbox = new VBoxContainer();
        _actionsVbox.AddThemeConstantOverride("separation", OptionSpacing);
        margin.AddChild(_actionsVbox);
    }

    private void BuildFerrySection(VBoxContainer parent)
    {
        _ferrySection = new Control();
        _ferrySection.Visible = false;
        parent.AddChild(_ferrySection);

        var ferryMargin = new MarginContainer();
        ferryMargin.AddThemeConstantOverride("margin_left", PanelMargin);
        ferryMargin.AddThemeConstantOverride("margin_right", PanelMargin);
        ferryMargin.AddThemeConstantOverride("margin_top", 4);
        ferryMargin.AddThemeConstantOverride("margin_bottom", 4);
        _ferrySection.AddChild(ferryMargin);

        var ferryContainer = new VBoxContainer();
        ferryContainer.AddThemeConstantOverride("separation", 4);
        ferryMargin.AddChild(ferryContainer);

        var ferryTitle = new Label();
        ferryTitle.Text = "渡船目的地:";
        ferryTitle.AddThemeFontSizeOverride("font_size", FontSizeSm);
        ferryTitle.AddThemeColorOverride("font_color", TextPortAccent);
        ferryContainer.AddChild(ferryTitle);

        _ferryVbox = new VBoxContainer();
        _ferryVbox.AddThemeConstantOverride("separation", 4);
        ferryContainer.AddChild(_ferryVbox);
    }

    private void BuildLeaveButton(VBoxContainer parent)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", PanelMargin);
        margin.AddThemeConstantOverride("margin_right", PanelMargin);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        parent.AddChild(margin);

        var leaveBtn = CreateButton("离开港口", new Vector2(0, BtnHeight));
        leaveBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leaveBtn.Pressed += () =>
        {
            EmitSignal(SignalName.LeavePort);
            HidePanel();
        };
        margin.AddChild(leaveBtn);
    }

    // ============================================================================
    // 功能填充
    // ============================================================================

    private void PopulateActions(OverworldPOI port)
    {
        ClearActions();

        // 贸易（港口总是有商店）
        var tradeBtn = CreateButton("海港贸易", new Vector2(0, BtnHeight));
        tradeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tradeBtn.TooltipText = "查看港口商品，购买航海物资和异域货物";
        tradeBtn.Pressed += () => EmitSignal(SignalName.PortActionSelected, "trade");
        _actionsVbox.AddChild(tradeBtn);

        // 酒馆（如果有）
        if (port.HasTavern)
        {
            var tavernBtn = CreateButton("海港酒馆", new Vector2(0, BtnHeight));
            tavernBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            tavernBtn.TooltipText = "招募水手和冒险者，打听海上传闻";
            tavernBtn.Pressed += () => EmitSignal(SignalName.PortActionSelected, "tavern");
            _actionsVbox.AddChild(tavernBtn);
        }

        // 情报
        var infoBtn = CreateButton("航海情报", new Vector2(0, BtnHeight));
        infoBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        infoBtn.TooltipText = "了解附近海域和其他港口的消息";
        infoBtn.Pressed += () => EmitSignal(SignalName.PortActionSelected, "information");
        _actionsVbox.AddChild(infoBtn);

        // 休息
        var restBtn = CreateButton("码头休息", new Vector2(0, BtnHeight));
        restBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        restBtn.TooltipText = "在港口休息，恢复队伍状态";
        restBtn.Pressed += () => EmitSignal(SignalName.PortActionSelected, "rest");
        _actionsVbox.AddChild(restBtn);
    }

    private void PopulateFerryDestinations(OverworldPOI port)
    {
        ClearFerryDestinations();

        if (port.FerryDestinations.Count == 0)
        {
            _ferrySection.Visible = false;
            return;
        }

        _ferrySection.Visible = true;

        foreach (var dest in port.FerryDestinations)
        {
            var btn = MakeFerryButton(dest, port.FerryCost);
            string capturedDest = dest;
            btn.Pressed += () => EmitSignal(SignalName.FerryDestinationSelected, capturedDest);
            _ferryVbox.AddChild(btn);
        }
    }

    private void ClearActions()
    {
        foreach (Node child in _actionsVbox.GetChildren())
            child.QueueFree();
    }

    private void ClearFerryDestinations()
    {
        foreach (Node child in _ferryVbox.GetChildren())
            child.QueueFree();
    }

    // ============================================================================
    // 按钮工厂
    // ============================================================================

    private Button MakeFerryButton(string destination, int cost)
    {
        var btn = new Button();
        btn.Text = $"⛵ {destination}  ({cost}金)";
        btn.CustomMinimumSize = new Vector2(0, 36);
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.AddThemeFontSizeOverride("font_size", FontSizeMd);

        var normalStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.10f, 0.15f) };
        normalStyle.SetBorderWidthAll(1);
        normalStyle.BorderColor = new Color(0.25f, 0.35f, 0.45f, 0.5f);
        normalStyle.SetCornerRadiusAll(3);
        normalStyle.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat { BgColor = new Color(0.12f, 0.18f, 0.28f) };
        hoverStyle.SetBorderWidthAll(1);
        hoverStyle.BorderColor = new Color(0.40f, 0.55f, 0.65f, 0.8f);
        hoverStyle.SetCornerRadiusAll(3);
        hoverStyle.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        btn.AddThemeColorOverride("font_color", ThemeTextSecondary);
        btn.AddThemeColorOverride("font_hover_color", TextPortAccent);

        btn.TooltipText = $"乘坐渡船前往 {destination}，费用 {cost} 金币";

        return btn;
    }

    // ============================================================================
    // 辅助
    // ============================================================================

    private static string GetPortDescription(OverworldPOI port)
    {
        string desc = "海风带来远方的气息，码头上停泊着大小船只。";
        if (port.FerryDestinations.Count > 0)
            desc += $"\n可乘渡船前往 {port.FerryDestinations.Count} 个目的地。";
        if (port.HasShipyard)
            desc += "\n港口设有造船厂。";
        return desc;
    }
}
