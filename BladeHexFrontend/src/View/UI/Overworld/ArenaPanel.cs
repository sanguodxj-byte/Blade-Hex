// ArenaPanel.cs
// Arena panel - Participate in competitions for gold and reputation
using System;
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class ArenaPanel : POIPanelBase
{
    [Signal]
    public delegate void ArenaFinishedEventHandler();

    private static readonly Random _rng = new();

    private RichTextLabel _resultLabel = null!;
    private Label _goldLabel = null!;
    private EconomyManager _economy = null!;

    // ── Panel specs ────────────────────────────────────────────────────────
    protected override int PanelWidth => 400;
    protected override int PanelHeight => 380;

    // ── Public API ─────────────────────────────────────────────────────────

    public void ShowArena(EconomyManager economy)
    {
        _economy = economy;
        _resultLabel.Text = "";
        _goldLabel.Text = $"当前金币: {(_economy?.Gold ?? 0)}";
        ShowPanel();
    }

    public override void HidePanel()
    {
        base.HidePanel();
    }

    // ── Content ────────────────────────────────────────────────────────────

    protected override void BuildContent(VBoxContainer container)
    {
        // Title
        container.AddChild(CreateTitleLabel("竞技场"));

        // Description
        container.AddChild(CreateBodyLabel("在这里展示你的实力，赢取金币和声望。"));

        container.AddChild(CreateSeparatorH());

        // Difficulty label
        container.AddChild(CreateBodyLabel("选择对手:"));

        // Difficulty buttons
        var btnEasy = CreateButton("新手挑战 (报名费: 20金 | 奖金: 50金)", new Vector2(360, 40));
        btnEasy.Pressed += () => Fight(20, 50, 1);
        container.AddChild(btnEasy);

        var btnMed = CreateButton("精英挑战 (报名费: 50金 | 奖金: 150金)", new Vector2(360, 40));
        btnMed.Pressed += () => Fight(50, 150, 3);
        container.AddChild(btnMed);

        var btnHard = CreateButton("冠军挑战 (报名费: 100金 | 奖金: 400金)", new Vector2(360, 40));
        btnHard.Pressed += () => Fight(100, 400, 5);
        container.AddChild(btnHard);

        container.AddChild(CreateSeparatorH());

        // Gold
        _goldLabel = new Label();
        _goldLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        _goldLabel.AddThemeColorOverride("font_color", ThemeTextPrimary);
        container.AddChild(_goldLabel);

        // Result
        _resultLabel = CreateResultLabel();
        _resultLabel.CustomMinimumSize = new Vector2(360, 50);
        container.AddChild(_resultLabel);

        // Close
        var closeBtn = CreateButton("离开竞技场", new Vector2(360, 40));
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        closeBtn.Pressed += () => { EmitSignal(SignalName.ArenaFinished); HidePanel(); };
        container.AddChild(closeBtn);
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.ArenaFinished);
        HidePanel();
    }

    // ── Internal Logic ─────────────────────────────────────────────────────

    private void Fight(int entryFee, int prize, int difficulty)
    {
        if (_economy != null && !_economy.SpendGold(entryFee))
        {
            _resultLabel.Text = "[color=red]金币不足，无法报名！[/color]";
            return;
        }

        // Simplified combat: 50% + 5% per difficulty level win chance
        double winChance = 0.5 + difficulty * 0.05;
        bool won = _rng.NextDouble() < winChance;

        if (won)
        {
            _economy?.AddGold(prize);
            _resultLabel.Text = $"[color=green]胜利！你获得了 {prize} 金币！[/color]";
        }
        else
        {
            _resultLabel.Text = "[color=red]败北... 你被击败了，报名费已损失。[/color]";
        }

        if (_economy != null)
            _goldLabel.Text = $"当前金币: {_economy.Gold}";
    }
}
