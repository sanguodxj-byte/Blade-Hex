// DialoguePanel.cs
// Dialogue panel - Display dialogue content when talking to NPC
using Godot;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class DialoguePanel : CanvasLayer
{
    [Signal]
    public delegate void DialogueFinishedEventHandler();

    private Control _root = null!;
    private Label _npcNameLabel = null!;
    private Label _npcInfoLabel = null!;
    private RichTextLabel _dialogueText = null!;
    private VBoxContainer _responsesVbox = null!;
    private NPCProfile? _currentProfile;
    private Godot.Collections.Dictionary _dialogueData = null!;

    // ── Theme colours ──────────────────────────────────────────────────────
    private static readonly Color BgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);

    private const int SpacingMd = 8;
    private const int SpacingSm = 4;
    private const int FontSizeXl = 20;
    private const int FontSizeSm = 12;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer = 25;
        SetupUI();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void ShowDialogue(NPCProfile profile)
    {
        _currentProfile = profile;

        _dialogueData = profile.dialogueLines.Count > 0
            ? profile.dialogueLines
            : profile.GetDefaultDialogue();

        _npcNameLabel.Text = profile.npcName;

        string typeName = profile.GetNpcTypeNameForType((int)profile.npcType);
        string attitudeText = profile.GetAttitudeText();
        _npcInfoLabel.Text = $"{typeName} - {attitudeText}";

        // Greeting
        string greeting = _dialogueData.ContainsKey("greeting")
            ? _dialogueData["greeting"].AsString()
            : "...";
        _dialogueText.Text = greeting;

        // Response options
        PopulateResponses();
        _root.Visible = true;
    }

    public void HidePanel()
    {
        _root.Visible = false;
        _currentProfile = null;
        ClearResponses();
    }

    public bool IsPanelVisible()
    {
        return _root.Visible;
    }

    // ── UI Construction ────────────────────────────────────────────────────

    private void SetupUI()
    {
        _root = new Control();
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.Visible = false;
        AddChild(_root);

        // Overlay
        var overlay = new ColorRect();
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.Color = new Color(0, 0, 0, 0.6f);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(overlay);

        // Dialogue panel (bottom)
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(600, 250);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        panel.OffsetLeft = (1280f - 600f) / 2f;
        panel.OffsetTop = -280;
        panel.OffsetRight = -(1280f - 600f) / 2f;
        panel.OffsetBottom = -15;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle(BgPrimary, BorderHighlight));
        _root.AddChild(panel);

        // Margin
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 15);
        margin.AddThemeConstantOverride("margin_bottom", 15);
        panel.AddChild(margin);

        // VBox
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", SpacingMd);
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddChild(vbox);

        // NPC name
        _npcNameLabel = new Label();
        _npcNameLabel.AddThemeFontSizeOverride("font_size", FontSizeXl);
        _npcNameLabel.AddThemeColorOverride("font_color", TextAccent);
        vbox.AddChild(_npcNameLabel);

        // NPC info
        _npcInfoLabel = new Label();
        _npcInfoLabel.AddThemeFontSizeOverride("font_size", FontSizeSm);
        _npcInfoLabel.AddThemeColorOverride("font_color", TextMuted);
        vbox.AddChild(_npcInfoLabel);

        // Dialogue text
        _dialogueText = new RichTextLabel();
        _dialogueText.CustomMinimumSize = new Vector2(560, 80);
        _dialogueText.BbcodeEnabled = true;
        _dialogueText.ScrollActive = false;
        _dialogueText.FitContent = true;
        vbox.AddChild(_dialogueText);

        // Separator
        vbox.AddChild(CreateSeparator());

        // Response options
        _responsesVbox = new VBoxContainer();
        _responsesVbox.AddThemeConstantOverride("separation", SpacingSm);
        vbox.AddChild(_responsesVbox);
    }

    // ── Internal Logic ─────────────────────────────────────────────────────

    private void PopulateResponses()
    {
        ClearResponses();

        Godot.Collections.Array options = _dialogueData.ContainsKey("options")
            ? _dialogueData["options"].AsGodotArray()
            : new Godot.Collections.Array();

        for (int i = 0; i < options.Count; i++)
        {
            var btn = new Button();
            btn.Text = options[i].AsString();
            btn.CustomMinimumSize = new Vector2(560, 36);
            btn.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
            StyleButton(btn);

            int capturedIndex = i;
            btn.Pressed += () => OnResponseSelected(capturedIndex);
            _responsesVbox.AddChild(btn);
        }
    }

    private void ClearResponses()
    {
        foreach (Node child in _responsesVbox.GetChildren())
            child.QueueFree();
    }

    private void OnResponseSelected(int index)
    {
        Godot.Collections.Array responses = _dialogueData.ContainsKey("responses")
            ? _dialogueData["responses"].AsGodotArray()
            : new Godot.Collections.Array();

        if (index < responses.Count)
            _dialogueText.Text = responses[index].AsString();
        else
            _dialogueText.Text = "...";

        ClearResponses();

        // Show close button
        var closeBtn = new Button();
        closeBtn.Text = "End Dialogue";
        closeBtn.CustomMinimumSize = new Vector2(560, 36);
        closeBtn.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        StyleButton(closeBtn);
        closeBtn.Pressed += () =>
        {
            EmitSignal(SignalName.DialogueFinished);
            HidePanel();
        };
        _responsesVbox.AddChild(closeBtn);
    }

    // ── Theme Helpers ──────────────────────────────────────────────────────

    private static StyleBoxFlat MakePanelStyle(Color bg, Color border, int borderWidth = 1, int radius = 8, int margin = 8)
    {
        var style = new StyleBoxFlat { BgColor = bg };
        style.SetBorderWidthAll(borderWidth);
        style.BorderColor = border;
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(margin);
        return style;
    }

    private static void StyleButton(Button btn)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.18f, 0.17f, 0.22f) };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
        normal.SetCornerRadiusAll(8);
        normal.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.28f, 0.26f, 0.34f) };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f);
        hover.SetCornerRadiusAll(8);
        hover.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("hover", hover);

        btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.9f, 0.8f, 0.5f));
    }

    private static HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }
}
