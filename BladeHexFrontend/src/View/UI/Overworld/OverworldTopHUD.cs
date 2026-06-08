// OverworldTopHUD.cs
// 大地图顶部状态信息栏 — 重构为左上角精致卡片式 HUD
using Godot;
using System;
using BladeHex.Localization;
using BladeHex.UI;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class OverworldTopHUD : PanelContainer
{
    private Label _goldLabel = null!;
    private Label _foodLabel = null!;
    private Label _speedStatusLabel = null!;
    private Label _reputationLabel = null!;
    private Label _terrainLabel = null!;

    // 主题色彩与常数
    private static readonly Color BgPanel = new(0.08f, 0.08f, 0.10f, 0.92f);
    private static readonly Color BorderHighlight = new(0.65f, 0.55f, 0.35f, 0.75f);
    private static readonly Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.4f);
    private static readonly Color TextAccent = new(0.95f, 0.85f, 0.55f);
    private static readonly Color TextSecondary = new(0.85f, 0.82f, 0.78f);
    
    private const int FontSizeMd = 14;
    private const int SpacingLg = 8;

    public void Initialize()
    {
        // 限制尺寸以和左下角控制栏完全对称 (496x96)
        CustomMinimumSize = new Vector2(496, 96);
        Size = new Vector2(496, 96);

        // 使用与左下角完全一致的带古铜金丝细边框和哑光黑铁背景的 StyleBoxFlat
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = new Color(0.08f, 0.08f, 0.10f, 0.76f); // 哑光黑铁底色，半透明度 76%
        boxStyle.BorderWidthLeft = 1;
        boxStyle.BorderWidthTop = 1;
        boxStyle.BorderWidthRight = 1;
        boxStyle.BorderWidthBottom = 1;
        boxStyle.BorderColor = new Color(0.72f, 0.58f, 0.35f, 0.65f); // 极细掐丝暗金色线框
        boxStyle.CornerRadiusTopLeft = 6;
        boxStyle.CornerRadiusTopRight = 6;
        boxStyle.CornerRadiusBottomLeft = 6;
        boxStyle.CornerRadiusBottomRight = 6;
        boxStyle.ContentMarginLeft = 8f;
        boxStyle.ContentMarginRight = 8f;
        boxStyle.ContentMarginTop = 8f;
        boxStyle.ContentMarginBottom = 8f;
        AddThemeStyleboxOverride("panel", boxStyle);

        // 设为 BottomRight 锚定右下角，以与左下角完美对称
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical = GrowDirection.Begin;
        MouseFilter = MouseFilterEnum.Pass;

        // 创建 Columns=3 的网格布局容器，均分展示 6 个状态指标（日期/时钟移至上方轮盘 tooltip）
        var grid = new GridContainer();
        grid.Columns = 3;
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(grid);

        // 重新定义字体大小为 12 像素，确保在每一列 120px 宽度内长文本不会溢出
        const int fontSize = 12;

        // 第一行第一列：地形
        _terrainLabel = new Label {
            Text = L10n.Tr("HUD_TERRAIN_UNKNOWN"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _terrainLabel.AddThemeColorOverride("font_color", TextSecondary);
        _terrainLabel.AddThemeFontSizeOverride("font_size", fontSize);
        grid.AddChild(_terrainLabel);

        // 第一行第二列：速度
        _speedStatusLabel = new Label {
            Text = L10n.Tr("HUD_SPEED_STATUS", L10n.Tr("STATUS_NORMAL")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _speedStatusLabel.AddThemeColorOverride("font_color", TextSecondary);
        _speedStatusLabel.AddThemeFontSizeOverride("font_size", fontSize);
        grid.AddChild(_speedStatusLabel);

        // 第二行第一列：金币
        _goldLabel = new Label {
            Text = L10n.Tr("HUD_GOLD_AMOUNT", 1000),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _goldLabel.AddThemeColorOverride("font_color", TextAccent);
        _goldLabel.AddThemeFontSizeOverride("font_size", fontSize);
        grid.AddChild(_goldLabel);

        // 第二行第二列：口粮
        _foodLabel = new Label {
            Text = L10n.Tr("HUD_FOOD", 20, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _foodLabel.AddThemeColorOverride("font_color", TextSecondary);
        _foodLabel.AddThemeFontSizeOverride("font_size", fontSize);
        grid.AddChild(_foodLabel);

        // 第二行第三列：声望
        _reputationLabel = new Label {
            Text = L10n.Tr("HUD_REPUTATION", 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _reputationLabel.AddThemeColorOverride("font_color", TextSecondary);
        _reputationLabel.AddThemeFontSizeOverride("font_size", fontSize);
        grid.AddChild(_reputationLabel);

        // 重新应用锚点和位置计算以紧贴右下角
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);

    }

    public void UpdateTopInfo(int year, int month, int day, string season, string clock,
        int gold, int food, int foodMax, string speedStatus, int reputation)
    {
        _goldLabel.Text = L10n.Tr("HUD_GOLD_AMOUNT", gold);
        _foodLabel.Text = L10n.Tr("HUD_FOOD", food, foodMax);
        // _speedStatusLabel 由 UpdatePlayerSpeed 接管，此处保持占位
        _reputationLabel.Text = L10n.Tr("HUD_REPUTATION", reputation);
    }

    /// <summary>更新玩家移速数值（覆盖 speedStatus 文本）</summary>
    /// <param name="speedValue">最终移速</param>
    /// <param name="isCamping">是否扎营</param>
    /// <param name="speedTooltip">可选的速度分解 tooltip 文本</param>
    public void UpdatePlayerSpeed(float speedValue, bool isCamping, string? speedTooltip = null)
    {
        if (isCamping)
        {
            _speedStatusLabel.Text = L10n.Tr("HUD_SPEED_STATUS", L10n.Tr("STATUS_CAMPING"));
            _speedStatusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f));
            _speedStatusLabel.TooltipText = "";
        }
        else
        {
            _speedStatusLabel.Text = L10n.Tr("HUD_SPEED_VALUE", speedValue.ToString("F0"));
            Color c = speedValue >= 250f ? new Color(0.3f, 0.85f, 0.3f) :
                      speedValue >= 150f ? new Color(0.85f, 0.82f, 0.78f) :
                      new Color(0.9f, 0.4f, 0.3f);
            _speedStatusLabel.AddThemeColorOverride("font_color", c);
            _speedStatusLabel.TooltipText = speedTooltip ?? "";
        }
    }

    public void UpdateTopInfoStatus(string status)
    {
        if (!string.IsNullOrEmpty(status))
        {
            _speedStatusLabel.Text = L10n.Tr("HUD_SPEED_STATUS", status);
            _speedStatusLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        }
        else
        {
            _speedStatusLabel.RemoveThemeColorOverride("font_color");
        }
    }

    public void UpdateTerrainDisplay(string terrainName, Color terrainColor)
    {
        _terrainLabel.Text = L10n.Tr("HUD_TERRAIN", terrainName);
        _terrainLabel.AddThemeColorOverride("font_color", terrainColor);
    }

    // 气候不需要在此显示，已移至罗盘右侧，此处留空实现以维持接口兼容
    public void UpdateWeatherDisplay(string weatherText) {}

    private static HSeparator CreateSeparatorH()
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat();
        style.BgColor = BorderDefault;
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }
}
