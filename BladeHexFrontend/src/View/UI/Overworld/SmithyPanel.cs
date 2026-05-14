// SmithyPanel.cs
// Smithy panel - Repair and upgrade equipment
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class SmithyPanel : POIPanelBase
{
    [Signal]
    public delegate void SmithyFinishedEventHandler();

    private Label _goldLabel = null!;
    private RichTextLabel _resultLabel = null!;
    private EconomyManager _economy = null!;

    // ── Panel specs ────────────────────────────────────────────────────────
    protected override int PanelWidth => 420;
    protected override int PanelHeight => 420;

    // ── Public API ─────────────────────────────────────────────────────────

    public void ShowSmithy(EconomyManager economy)
    {
        _economy = economy;
        _resultLabel.Text = "";
        _goldLabel.Text = $"{(_economy?.Gold ?? 0)}";
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
        container.AddChild(CreateTitleLabel("铁匠铺"));

        // Description
        container.AddChild(CreateBodyLabel("经验丰富的铁匠可以帮你修理和强化装备。"));

        // Gold header
        var header = new HBoxContainer();
        var headerTitle = CreateBodyLabel("金币:");
        headerTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(headerTitle);

        _goldLabel = new Label();
        _goldLabel.Text = "0";
        _goldLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        header.AddChild(_goldLabel);
        container.AddChild(header);

        container.AddChild(CreateSeparatorH());

        // Service buttons
        var btnRepair = CreateButton("全副修理 (30金) — 恢复所有装备耐久", new Vector2(380, 40));
        btnRepair.Pressed += () => DoService("repair", 30);
        container.AddChild(btnRepair);

        var btnSharpen = CreateButton("磨砺武器 (50金) — 武器伤害+1", new Vector2(380, 40));
        btnSharpen.Pressed += () => DoService("sharpen", 50);
        container.AddChild(btnSharpen);

        var btnReinforce = CreateButton("加固防具 (80金) — AC+1", new Vector2(380, 40));
        btnReinforce.Pressed += () => DoService("reinforce", 80);
        container.AddChild(btnReinforce);

        container.AddChild(CreateSeparatorH());

        // Result
        _resultLabel = CreateResultLabel();
        _resultLabel.CustomMinimumSize = new Vector2(380, 40);
        container.AddChild(_resultLabel);

        // Close
        var closeBtn = CreateButton("离开铁匠铺", new Vector2(380, 40));
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        closeBtn.Pressed += () => { EmitSignal(SignalName.SmithyFinished); HidePanel(); };
        container.AddChild(closeBtn);
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.SmithyFinished);
        HidePanel();
    }

    // ── Internal Logic ─────────────────────────────────────────────────────

    private void DoService(string serviceType, int cost)
    {
        if (_economy != null && !_economy.SpendGold(cost))
        {
            _resultLabel.Text = "[color=red]金币不足！[/color]";
            return;
        }

        _resultLabel.Text = serviceType switch
        {
            "repair" => "[color=green]所有装备已修理完毕，耐久完全恢复。[/color]",
            "sharpen" => "[color=green]武器已磨砺，伤害+1！[/color]",
            "reinforce" => "[color=green]防具已加固，AC+1！[/color]",
            _ => "[color=green]服务完成。[/color]",
        };

        if (_economy != null)
            _goldLabel.Text = $"{_economy.Gold}";
    }
}
