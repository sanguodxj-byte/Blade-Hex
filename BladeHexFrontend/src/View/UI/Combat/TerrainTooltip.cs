// TerrainTooltip.cs
// 战斗场景地形信息悬浮窗 — 右键空地时显示地形属性。
using Godot;
using BladeHex.Data;
using BladeHex.UI.Common;

namespace BladeHex.UI.Combat;

[GlobalClass]
public partial class TerrainTooltip : FloatingPanel
{
    private Label _titleLabel = null!;
    private Label _moveCostLabel = null!;
    private Label _acBonusLabel = null!;
    private Label _coverLabel = null!;
    private Label _elevationLabel = null!;
    private Label _effectLabel = null!;
    private Label _passableLabel = null!;

    protected override float MinPanelWidth => 180f;
    protected override bool UseTopLevel => true;

    protected override void BuildContent()
    {
        _titleLabel = MakeTitleLabel("", 15);
        Content.AddChild(_titleLabel);

        Content.AddChild(MakeSeparator());

        _moveCostLabel = MakeBodyLabel("", 12);
        Content.AddChild(_moveCostLabel);

        _acBonusLabel = MakeBodyLabel("", 12);
        Content.AddChild(_acBonusLabel);

        _coverLabel = MakeBodyLabel("", 12);
        Content.AddChild(_coverLabel);

        _elevationLabel = MakeBodyLabel("", 12);
        Content.AddChild(_elevationLabel);

        _passableLabel = MakeBodyLabel("", 12);
        Content.AddChild(_passableLabel);

        _effectLabel = MakeMutedLabel("", 11);
        Content.AddChild(_effectLabel);
    }

    /// <summary>显示指定 cell 的地形信息</summary>
    public void ShowTerrain(BattleCellData data, Vector2 screenPos)
    {
        if (data == null) { HidePanel(); return; }

        _titleLabel.Text = data.terrainName;

        _moveCostLabel.Text = $"移动消耗: {data.moveCost} AP";
        _moveCostLabel.AddThemeColorOverride("font_color",
            data.moveCost <= 1 ? new Color(0.7f, 0.9f, 0.7f) :
            data.moveCost == 2 ? new Color(0.9f, 0.85f, 0.5f) :
            new Color(0.9f, 0.5f, 0.5f));

        _acBonusLabel.Text = data.acBonus != 0 ? $"防御加值: {(data.acBonus > 0 ? "+" : "")}{data.acBonus} AC" : "防御加值: 无";
        _acBonusLabel.Visible = true;

        string coverText = data.coverLevel switch
        {
            0 => "掩体: 无",
            1 => "掩体: 半掩体",
            2 => "掩体: 全掩体",
            _ => $"掩体: {data.coverLevel}",
        };
        _coverLabel.Text = coverText;

        string elevText = data.elevation switch
        {
            0 => "地势: 低地",
            1 => "地势: 平地",
            2 => "地势: 高地",
            _ => $"地势: {data.elevation}",
        };
        _elevationLabel.Text = elevText;

        _passableLabel.Text = data.isPassable ? "可通行" : "不可通行";
        _passableLabel.AddThemeColorOverride("font_color",
            data.isPassable ? new Color(0.6f, 0.8f, 0.6f) : new Color(0.9f, 0.4f, 0.4f));

        if (!string.IsNullOrEmpty(data.specialEffect))
        {
            _effectLabel.Text = $"特殊: {data.specialEffect}";
            _effectLabel.Visible = true;
        }
        else
        {
            _effectLabel.Visible = false;
        }

        ShowAt(screenPos);
    }

    /// <summary>兼容旧 API：用富文本显示</summary>
    public void ShowRichText(string text)
    {
        _titleLabel.Text = "";
        _moveCostLabel.Text = text;
        _acBonusLabel.Visible = false;
        _coverLabel.Text = "";
        _elevationLabel.Text = "";
        _passableLabel.Text = "";
        _effectLabel.Visible = false;
        ShowAtMouse();
    }

    /// <summary>兼容旧 API：隐藏</summary>
    public void HideTooltip() => HidePanel();
}
