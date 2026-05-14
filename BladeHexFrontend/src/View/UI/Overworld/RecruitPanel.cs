// RecruitPanel.cs
// Recruit panel - Display available mercenaries for hire at town tavern
// Data source: C# RecruitService.GetAvailableGd(poi_id, current_day)
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class RecruitPanel : POIPanelBase
{
    // ============================================================================
    // 面板规格
    // ============================================================================

    protected override int PanelWidth => 500;
    protected override int PanelHeight => 500;

    // ============================================================================
    // Signals
    // ============================================================================

    [Signal]
    public delegate void RecruitFinishedEventHandler(bool hired);

    // ============================================================================
    // Fields
    // ============================================================================

    private Label _titleLabel = null!;
    private Label _goldLabel = null!;
    private Label _rosterCountLabel = null!;
    private VBoxContainer _listVbox = null!;
    private RichTextLabel _resultLabel = null!;

    private RecruitService? _recruitService;
    private EconomyManager? _economy;
    private OverworldParty? _playerParty;
    private string _currentPoiId = "";
    private int _currentDay = 1;

    // ============================================================================
    // Content
    // ============================================================================

    protected override void BuildContent(VBoxContainer container)
    {
        // Header row
        var header = new HBoxContainer();
        _titleLabel = CreateTitleLabel("Tavern - Recruit Mercenaries");
        _titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(_titleLabel);

        _goldLabel = CreateBodyLabel("");
        _goldLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        header.AddChild(_goldLabel);
        container.AddChild(header);

        // Party info
        _rosterCountLabel = CreateMutedLabel("");
        container.AddChild(_rosterCountLabel);

        // Separator
        container.AddChild(CreateSeparatorH());

        // Scroll list
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(460, 320);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        container.AddChild(scroll);

        _listVbox = new VBoxContainer();
        _listVbox.AddThemeConstantOverride("separation", SpacingSm);
        _listVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_listVbox);

        // Result label
        _resultLabel = CreateRichText(new Vector2(460, 30));
        container.AddChild(_resultLabel);

        // Close button
        var closeBtn = CreateButton("Leave Tavern", new Vector2(460, 36));
        closeBtn.Pressed += () =>
        {
            HidePanel();
            EmitSignal(SignalName.RecruitFinished, false);
        };
        container.AddChild(closeBtn);
    }

    // ============================================================================
    // Public API
    // ============================================================================

    /// <summary>
    /// Show recruit panel
    /// </summary>
    /// <param name="recruitService">RecruitService instance</param>
    /// <param name="poiId">Current town POI name</param>
    /// <param name="economy">EconomyManager</param>
    /// <param name="party">OverworldParty (has .Roster)</param>
    /// <param name="currentDay">Current game day</param>
    public void ShowRecruitList(RecruitService recruitService, string poiId,
        EconomyManager economy, OverworldParty party, int currentDay)
    {
        _recruitService = recruitService;
        _economy = economy;
        _playerParty = party;
        _currentPoiId = poiId;
        _currentDay = currentDay;
        _resultLabel.Text = "";
        RefreshList();
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        HidePanel();
        EmitSignal(SignalName.RecruitFinished, false);
    }

    // ============================================================================
    // List refresh
    // ============================================================================

    private void RefreshList()
    {
        // Clear list
        foreach (Node child in _listVbox.GetChildren())
            child.QueueFree();

        // Update gold and party info
        if (_economy != null)
            _goldLabel.Text = $"Gold: {_economy.Gold}";

        if (_playerParty?.Roster != null)
        {
            var roster = _playerParty.Roster;
            _rosterCountLabel.Text = $"Party: {roster.Count} / {roster.Capacity}";
        }

        // Get available recruits
        if (_recruitService == null)
            return;

        var available = _recruitService.GetAvailableGd(_currentPoiId, _currentDay);
        if (available.Count == 0)
        {
            var emptyLabel = CreateMutedLabel("No recruits available. Check back in a few days.");
            _listVbox.AddChild(emptyLabel);
            return;
        }

        for (int i = 0; i < available.Count; i++)
        {
            var recruitVar = available[i];
            if (recruitVar.AsGodotObject() is RecruitableUnit recruit)
            {
                var unit = recruit.Unit;
                if (unit == null)
                    continue;

                var row = CreateRecruitRow(i, recruit, unit);
                _listVbox.AddChild(row);
            }
        }
    }

    private Control CreateRecruitRow(int index, RecruitableUnit recruit, UnitData unit)
    {
        var row = new PanelContainer();
        var style = new StyleBoxFlat();
        style.BgColor = index % 2 == 0 ? ThemeBgSecondary : ThemeBgPrimary;
        style.SetCornerRadiusAll(RadiusSm);
        row.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", SpacingMd);
        row.AddChild(hbox);

        // Left: info
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Name
        var nameLabel = CreateBodyLabel(unit.UnitName);
        infoVbox.AddChild(nameLabel);

        // Stats
        string raceName = unit.Race?.RaceName ?? "Unknown";
        string statsText = $"Lv{unit.Level} {raceName} | STR{unit.Str} DEX{unit.Dex} CON{unit.Con}";
        var statsLabel = CreateMutedLabel(statsText);
        infoVbox.AddChild(statsLabel);

        // Cost
        string costText = $"Recruit: {recruit.Cost}g | Weekly wage: {recruit.WeeklyWage}g";
        var costLabel = CreateMutedLabel(costText);
        costLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        infoVbox.AddChild(costLabel);

        hbox.AddChild(infoVbox);

        // Right: recruit button
        var btn = CreateButton("Recruit", new Vector2(80, 50));
        int capturedIndex = index;
        btn.Pressed += () => DoRecruit(capturedIndex);

        // Check if recruitable
        if (_playerParty?.Roster?.IsFull == true)
        {
            btn.Disabled = true;
            btn.TooltipText = "Party is full";
        }
        else if (_economy != null && _economy.Gold < recruit.Cost)
        {
            btn.Disabled = true;
            btn.TooltipText = "Not enough gold";
        }

        hbox.AddChild(btn);
        return row;
    }

    private void DoRecruit(int index)
    {
        if (_recruitService == null || _playerParty == null || _economy == null)
            return;

        var result = _recruitService.Recruit(
            _currentPoiId, index, _playerParty.Roster, _currentDay, _economy);

        if (result != null)
        {
            _resultLabel.Text = $"[color=green]{result.UnitName} joined the party![/color]";
        }
        else
        {
            _resultLabel.Text = "[color=red]Recruit failed (not enough gold or party is full)[/color]";
        }

        RefreshList();
    }
}
