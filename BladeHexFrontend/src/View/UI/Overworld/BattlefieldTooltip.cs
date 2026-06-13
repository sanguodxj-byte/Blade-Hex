using Godot;
using BladeHex.Strategic;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>大地图野战悬浮详情，复用 FloatingPanel 的统一暗金悬浮窗样式。</summary>
[GlobalClass]
public partial class BattlefieldTooltip : FloatingPanel
{
    private Label _titleLabel = null!;
    private HBoxContainer _sides = null!;
    private PanelContainer _leftPanel = null!;
    private PanelContainer _rightPanel = null!;
    private Label _leftName = null!;
    private Label _rightName = null!;
    private Label _leftDetails = null!;
    private Label _rightDetails = null!;
    private Label _statusLabel = null!;

    protected override bool UseTopLevel => true;
    protected override bool FollowMouseContinuously => true;
    protected override float MinPanelWidth => 320f;
    protected override int PanelContentMargin => 12;
    protected override int PanelShadowSize => 8;
    protected override Color PanelShadowColor => new(0f, 0f, 0f, 0.35f);

    protected override void BuildContent()
    {
        Content.SizeFlagsVertical = SizeFlags.ShrinkBegin;

        _titleLabel = MakeTitleLabel("野战交锋", 17);
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        Content.AddChild(_titleLabel);
        Content.AddChild(MakeSeparator(0.22f));

        _sides = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        _sides.AddThemeConstantOverride("separation", 10);
        Content.AddChild(_sides);

        (_leftPanel, _leftName, _leftDetails) = CreateSidePanel();
        (_rightPanel, _rightName, _rightDetails) = CreateSidePanel();
        _sides.AddChild(_leftPanel);
        _sides.AddChild(_rightPanel);

        Content.AddChild(MakeSeparator(0.22f));
        _statusLabel = MakeMutedLabel("靠近战场点击可介入战斗。", 12);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        Content.AddChild(_statusLabel);
    }

    public void ShowForBattlefield(
        OverworldEntity left,
        OverworldEntity right,
        Vector2 screenPos,
        System.Enum leftRelation,
        System.Enum rightRelation,
        System.Collections.Generic.List<NationConfig>? nations = null,
        int attackerCount = 1,
        int defenderCount = 1,
        float attackerTotalPower = 0f,
        float defenderTotalPower = 0f)
    {
        _leftName.Text = left.EntityName +
            (attackerCount > 1 ? $" (+{attackerCount - 1})" : "");
        _rightName.Text = right.EntityName +
            (defenderCount > 1 ? $" (+{defenderCount - 1})" : "");
        _leftDetails.Text = BuildDetails(left, leftRelation, nations) +
            (attackerTotalPower > 0f ? $"\n战力: {attackerTotalPower:F0}" : "");
        _rightDetails.Text = BuildDetails(right, rightRelation, nations) +
            (defenderTotalPower > 0f ? $"\n战力: {defenderTotalPower:F0}" : "");
        _leftPanel.AddThemeStyleboxOverride("panel", CreateSideStyle(RelationColor(leftRelation)));
        _rightPanel.AddThemeStyleboxOverride("panel", CreateSideStyle(RelationColor(rightRelation)));
        _statusLabel.Text = $"{RelationText(leftRelation)} vs {RelationText(rightRelation)} · 点击选择介入阵营";
        ShowAt(screenPos);
    }

    private static (PanelContainer panel, Label name, Label details) CreateSidePanel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(144, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };

        var box = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        var name = MakeLabel("", 13, Colors.White);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(name);

        var details = MakeLabel("", 11, new Color(0.86f, 0.84f, 0.78f));
        details.HorizontalAlignment = HorizontalAlignment.Center;
        details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(details);

        return (panel, name, details);
    }

    private static string BuildDetails(
        OverworldEntity entity,
        System.Enum relation,
        System.Collections.Generic.List<NationConfig>? nations)
    {
        string faction = entity.Faction;
        if (nations != null && !string.IsNullOrEmpty(entity.Faction))
        {
            var nation = nations.Find(n => n.Id == entity.Faction);
            if (nation != null)
                faction = nation.DisplayName;
        }

        return $"{RelationText(relation)}\n{faction}\n战力 {entity.CombatPower:F0} / 规模 {entity.PartySize}";
    }

    private static string RelationText(System.Enum relation) => relation.ToString() switch
    {
        "Friendly" => "友方",
        "Hostile" => "敌对",
        _ => "中立",
    };

    private static Color RelationColor(System.Enum relation) => relation.ToString() switch
    {
        "Friendly" => new Color(0.08f, 0.34f, 0.16f, 0.88f),
        "Hostile" => new Color(0.42f, 0.06f, 0.055f, 0.90f),
        _ => new Color(0.015f, 0.015f, 0.018f, 0.92f),
    };

    private static StyleBoxFlat CreateSideStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            BorderColor = new Color(1f, 1f, 1f, 0.18f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 7,
            ContentMarginBottom = 7
        };
    }
}
