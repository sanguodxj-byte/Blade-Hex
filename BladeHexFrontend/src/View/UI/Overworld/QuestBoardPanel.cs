// QuestBoardPanel.cs
// Quest board panel - View and accept available quests (dynamic data version)
// All data passed via ShowBoardDynamic, no hardcoded quest templates
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class QuestBoardPanel : POIPanelBase
{
    // ============================================================================
    // 面板规格
    // ============================================================================

    protected override int PanelWidth => 450;
    protected override int PanelHeight => 450;

    // ============================================================================
    // Signals
    // ============================================================================

    [Signal]
    public delegate void QuestAcceptedEventHandler(string questId);

    [Signal]
    public delegate void BoardClosedEventHandler();

    // ============================================================================
    // Fields
    // ============================================================================

    private VBoxContainer _questList = null!;
    private RichTextLabel _resultLabel = null!;

    private QuestGenerator? _questGenerator;
    private string _currentPoiId = "";
    private int _currentDay = 1;

    // ============================================================================
    // Content
    // ============================================================================

    protected override void BuildContent(VBoxContainer container)
    {
        // Title
        container.AddChild(CreateTitleLabel("Quest Board"));

        // Description
        container.AddChild(CreateBodyLabel("Bounty quests issued by the lord. Complete them for gold and experience."));

        // Separator
        container.AddChild(CreateSeparatorH());

        // Scroll list
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        container.AddChild(scroll);

        _questList = new VBoxContainer();
        _questList.AddThemeConstantOverride("separation", SpacingMd);
        scroll.AddChild(_questList);

        // Separator
        container.AddChild(CreateSeparatorH());

        // Result label
        _resultLabel = CreateRichText(new Vector2(410, 40));
        container.AddChild(_resultLabel);

        // Close button
        var closeBtn = CreateButton("Leave", new Vector2(410, 40));
        closeBtn.Pressed += () =>
        {
            EmitSignal(SignalName.BoardClosed);
            HidePanel();
        };
        container.AddChild(closeBtn);
    }

    // ============================================================================
    // Public API
    // ============================================================================

    /// <summary>
    /// Dynamic quest board version (gets quests from QuestGenerator)
    /// </summary>
    /// <param name="questGenerator">C# QuestGenerator reference</param>
    /// <param name="poiId">Current town POI name</param>
    /// <param name="currentDay">Current game day</param>
    public void ShowBoardDynamic(QuestGenerator questGenerator, string poiId, int currentDay)
    {
        _questGenerator = questGenerator;
        _currentPoiId = poiId;
        _currentDay = currentDay;
        _resultLabel.Text = "";
        PopulateDynamicQuests();
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.BoardClosed);
        HidePanel();
    }

    // ============================================================================
    // Dynamic quests
    // ============================================================================

    private void PopulateDynamicQuests()
    {
        // Clear list
        foreach (Node child in _questList.GetChildren())
            child.QueueFree();

        if (_questGenerator == null)
        {
            var emptyLabel = CreateMutedLabel("No quests available. Check back in a few days.");
            _questList.AddChild(emptyLabel);
            return;
        }

        var quests = _questGenerator.GetAvailableQuests(_currentPoiId, _currentDay);

        if (quests.Count == 0)
        {
            var emptyLabel = CreateMutedLabel("No quests available. Check back in a few days.");
            _questList.AddChild(emptyLabel);
            return;
        }

        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            var card = CreateQuestCard(q, i);
            _questList.AddChild(card);
        }
    }

    private Control CreateQuestCard(QuestData quest, int index)
    {
        // Card container
        var card = CreateCard(new Vector2(410, 0));

        // Padding
        var innerMargin = new MarginContainer();
        innerMargin.AddThemeConstantOverride("margin_left", 10);
        innerMargin.AddThemeConstantOverride("margin_top", 10);
        innerMargin.AddThemeConstantOverride("margin_right", 8);
        innerMargin.AddThemeConstantOverride("margin_bottom", 8);
        innerMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(innerMargin);

        // Vertical layout
        var qvbox = new VBoxContainer();
        qvbox.AddThemeConstantOverride("separation", SpacingXs);
        qvbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        innerMargin.AddChild(qvbox);

        // Title row
        var titleRow = new HBoxContainer();
        var titleLbl = CreateBodyLabel(quest.QuestName);
        titleLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(titleLbl);

        var diffLbl = CreateMutedLabel($"[{quest.GetDifficultyText()}]");
        titleRow.AddChild(diffLbl);
        qvbox.AddChild(titleRow);

        // Description
        var descLbl = CreateMutedLabel(quest.Description);
        qvbox.AddChild(descLbl);

        // Reward row
        var rewardRow = new HBoxContainer();
        string timeText = quest.HasTimeLimit ? $"Deadline: {quest.TimeLimitDays}d" : "No deadline";
        var rewardLabel = CreateMutedLabel(
            $"Reward: {quest.RewardGold}g | Reputation +{quest.RewardReputation} | {timeText}");
        rewardRow.AddChild(rewardLabel);

        // Separator
        rewardRow.AddChild(CreateSeparatorH());

        var acceptBtn = CreateButton("Accept", new Vector2(80, 28));
        int capturedIndex = index;
        acceptBtn.Pressed += () => AcceptDynamicQuest(capturedIndex);
        rewardRow.AddChild(acceptBtn);
        qvbox.AddChild(rewardRow);

        return card;
    }

    private void AcceptDynamicQuest(int index)
    {
        if (_questGenerator == null)
            return;

        var quest = _questGenerator.AcceptQuest(_currentPoiId, index, _currentDay);
        if (quest != null)
        {
            _resultLabel.Text = $"[color=green]Accepted: {quest.QuestName}[/color]";
            EmitSignal(SignalName.QuestAccepted, quest.QuestId);
            PopulateDynamicQuests();
        }
        else
        {
            _resultLabel.Text = "[color=red]Accept failed[/color]";
        }
    }
}
