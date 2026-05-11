using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战斗结果面板 — 显示胜利/失败信息、战利品、经验获得
/// 迁移自 GDScript BattleResultPanel.gd
/// </summary>
public partial class BattleResultPanel : PanelContainer
{
    [Signal] public delegate void ConfirmedEventHandler();

    private Label _titleLabel = null!;
    private Label _resultLabel = null!;
    private Label _lootLabel = null!;
    private Label _xpLabel = null!;
    private Label _goldLabel = null!;
    private RichTextLabel _detailRich = null!;
    private Button _confirmBtn = null!;
    
    private readonly UIFactory _factory = new();

    public override void _Ready()
    {
        Setup();
        Visible = false;
    }

    private void Setup()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 100;

        // 半透明遮罩背景
        var overlayBg = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.7f) };
        AddThemeStyleboxOverride("panel", overlayBg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var inner = new PanelContainer();
        inner.AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(UITheme.Instance.BgPrimary, UITheme.Instance.BorderHighlight, 2, UITheme.Instance.RadiusLg, 30));
        inner.CustomMinimumSize = new Vector2(420, 0);
        center.AddChild(inner);

        var vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        vbox.AddThemeConstantOverride("separation", UITheme.Instance.SpacingMd);
        inner.AddChild(vbox);

        _titleLabel = _factory.CreateTitleLabel("战斗结束", UITheme.Instance.FontSizeXxl);
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_titleLabel);

        vbox.AddChild(_factory.CreateSeparatorH(UITheme.Instance.BorderHighlight));

        _resultLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _resultLabel.AddThemeFontSizeOverride("font_size", UITheme.Instance.FontSizeXl);
        vbox.AddChild(_resultLabel);

        vbox.AddChild(_factory.CreateSeparatorH());

        var rewardTitle = _factory.CreateTitleLabel("— 战 利 品 —", UITheme.Instance.FontSizeLg);
        rewardTitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(rewardTitle);

        _xpLabel = _factory.CreateBodyLabel("获得经验: 0", UITheme.Instance.TextPositive);
        vbox.AddChild(_xpLabel);

        _goldLabel = _factory.CreateBodyLabel("获得金币: 0", UITheme.Instance.TextAccent);
        vbox.AddChild(_goldLabel);

        _lootLabel = _factory.CreateBodyLabel("", UITheme.Instance.TextMuted);
        _lootLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_lootLabel);

        vbox.AddChild(_factory.CreateSeparatorH());

        _detailRich = _factory.CreateRichText(new Vector2(360, 80));
        vbox.AddChild(_detailRich);

        _confirmBtn = _factory.CreateButton("返回大地图", new Vector2(200, UITheme.Instance.ButtonHeightLg));
        _confirmBtn.Pressed += () => { EmitSignal(SignalName.Confirmed); Visible = false; };
        _confirmBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        vbox.AddChild(_confirmBtn);
    }

    public void ShowVictory(int xp, int gold, List<string> loot, string details = "")
    {
        _titleLabel.AddThemeColorOverride("font_color", UITheme.Instance.TextPositive);
        _resultLabel.Text = "🎉 胜 利！";
        _resultLabel.AddThemeColorOverride("font_color", UITheme.Instance.TextAccent);
        _xpLabel.Text = $"获得经验: +{xp}";
        _goldLabel.Text = $"获得金币: +{gold}";
        _lootLabel.Text = loot.Count > 0 ? $"战利品: {string.Join(", ", loot)}" : "";
        _detailRich.Text = $"[color=gray]{details}[/color]";
        Visible = true;
    }

    public void ShowDefeat(int survivors, string details = "")
    {
        _titleLabel.AddThemeColorOverride("font_color", UITheme.Instance.TextNegative);
        _resultLabel.Text = "💀 全军覆没";
        _resultLabel.AddThemeColorOverride("font_color", UITheme.Instance.TextNegative);
        _xpLabel.Text = "";
        _goldLabel.Text = "";
        _lootLabel.Text = survivors > 0 ? $"幸存者: {survivors}人" : "无人生还";
        _detailRich.Text = $"[color=gray]{details}[/color]";
        _confirmBtn.Text = "回到主菜单";
        Visible = true;
    }
}
