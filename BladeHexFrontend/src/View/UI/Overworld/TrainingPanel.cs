// TrainingPanel.cs
// Training ground panel - Spend gold to improve character stats and experience
using Godot;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TrainingPanel : POIPanelBase
{
    [Signal]
    public delegate void TrainingFinishedEventHandler();

    private Label _goldLabel = null!;
    private RichTextLabel _resultLabel = null!;
    private EconomyManager _economy = null!;

    // ── Panel specs ────────────────────────────────────────────────────────
    protected override int PanelWidth => 400;
    protected override int PanelHeight => 380;

    // ── Public API ─────────────────────────────────────────────────────────

    public void ShowTraining(EconomyManager economy)
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
        container.AddChild(CreateTitleLabel("Training Ground"));

        // Description
        container.AddChild(CreateBodyLabel("Spend gold for special training to improve character abilities."));

        // Gold header
        var header = new HBoxContainer();
        var headerTitle = CreateBodyLabel("Gold:");
        headerTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(headerTitle);

        _goldLabel = new Label();
        _goldLabel.Text = "0";
        _goldLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        header.AddChild(_goldLabel);
        container.AddChild(header);

        container.AddChild(CreateSeparatorH());

        // Training items
        var trainings = new (string name, int cost, string stat, string desc)[]
        {
            ("Strength Training (40g)", 40, "str", "Strength +1"),
            ("Dexterity Training (40g)", 40, "dex", "Dexterity +1"),
            ("Constitution Training (40g)", 40, "con", "Constitution +1"),
            ("Intelligence Training (40g)", 40, "int", "Intelligence +1"),
            ("Comprehensive Training (100g)", 100, "all", "All stats +1"),
        };

        foreach (var t in trainings)
        {
            var btn = CreateButton(t.name, new Vector2(360, 36));
            btn.TooltipText = t.desc;

            int cost = t.cost;
            string stat = t.stat;
            string descText = t.desc;
            btn.Pressed += () => Train(cost, stat, descText);
            container.AddChild(btn);
        }

        container.AddChild(CreateSeparatorH());

        // Result
        _resultLabel = CreateResultLabel();
        _resultLabel.CustomMinimumSize = new Vector2(360, 40);
        container.AddChild(_resultLabel);

        // Close
        var closeBtn = CreateButton("Leave Training Ground", new Vector2(360, 40));
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        closeBtn.Pressed += () => { EmitSignal(SignalName.TrainingFinished); HidePanel(); };
        container.AddChild(closeBtn);
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.TrainingFinished);
        HidePanel();
    }

    // ── Internal Logic ─────────────────────────────────────────────────────

    private void Train(int cost, string stat, string desc)
    {
        if (_economy != null && !_economy.SpendGold(cost))
        {
            _resultLabel.Text = "[color=red]Not enough gold![/color]";
            return;
        }

        _resultLabel.Text = $"[color=green]Training complete: {desc}[/color]";

        if (_economy != null)
            _goldLabel.Text = $"{_economy.Gold}";
    }
}
