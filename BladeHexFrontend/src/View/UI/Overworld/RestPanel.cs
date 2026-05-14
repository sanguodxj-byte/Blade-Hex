// RestPanel.cs
// Rest panel - Rest at inn/tavern to recover party status
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class RestPanel : POIPanelBase
{
    [Signal]
    public delegate void RestCompletedEventHandler(int hours);

    private Label _statusLabel = null!;
    private EconomyManager _economyManager = null!;

    // ── Panel specs ────────────────────────────────────────────────────────
    protected override int PanelWidth => 350;
    protected override int PanelHeight => 300;

    // ── Public API ─────────────────────────────────────────────────────────

    public void ShowRest(EconomyManager economy)
    {
        _economyManager = economy;
        if (economy != null)
            _statusLabel.Text = $"Gold: {economy.Gold}   Food: {economy.Food:F1}";
        else
            _statusLabel.Text = "";
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
        container.AddChild(CreateTitleLabel("Rest"));

        // Description
        container.AddChild(CreateBodyLabel("Rest in a safe place to recover your party."));

        container.AddChild(CreateSeparatorH());

        // Status
        _statusLabel = new Label();
        _statusLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        _statusLabel.AddThemeColorOverride("font_color", ThemeTextPrimary);
        container.AddChild(_statusLabel);

        // Short rest
        var restShort = CreateButton("Short Rest (Free, recover 30% HP)", new Vector2(310, 40));
        restShort.Pressed += () => DoRest(0, 0.3f);
        container.AddChild(restShort);

        // Long rest
        var restLong = CreateButton("Long Rest (10g, recover 100% HP)", new Vector2(310, 40));
        restLong.Pressed += () => DoRest(10, 1.0f);
        container.AddChild(restLong);

        container.AddChild(CreateSeparatorH());

        // Close
        var closeBtn = CreateButton("Leave", new Vector2(310, 40));
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        closeBtn.Pressed += () => HidePanel();
        container.AddChild(closeBtn);
    }

    // ── Internal Logic ─────────────────────────────────────────────────────

    private void DoRest(int cost, float hpRatio)
    {
        if (_economyManager != null && cost > 0)
        {
            if (!_economyManager.SpendGold(cost))
            {
                _statusLabel.Text = "Not enough gold!";
                return;
            }
        }

        _economyManager?.AdvanceTime(8.0f);
        _statusLabel.Text = $"Party has rested, restored {hpRatio * 100:F0}% HP.";
        EmitSignal(SignalName.RestCompleted, 8);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_economyManager != null)
            _statusLabel.Text += $"\nGold: {_economyManager.Gold}";
    }
}
