// QuestBoardPanel.cs
// 委托布告栏面板 — 查看和接取动态生成的任务
// 使用统一布局基类，只填充数据
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Localization;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class QuestBoardPanel : POIPanelBase
{
    // ============================================================================
    // 信号
    // ============================================================================

    [Signal]
    public delegate void QuestAcceptedEventHandler(string questId);

    [Signal]
    public delegate void BoardClosedEventHandler();

    // ============================================================================
    // 字段
    // ============================================================================

    private QuestGenerator? _questGenerator;
    private QuestManager? _questManager;
    private string _currentPoiId = "";
    private int _currentDay = 1;

    // ── 数据填充 ──

    protected override Color GetIllustrationColor() => new(0.10f, 0.08f, 0.06f, 1.0f);
    protected override string GetIllustrationText() => L10n.Tr("QUEST_BOARD_BRACKET");
    protected override string? GetIllustrationPath()
        => POIIllustrationResolver.GetPanelIllustration("quest_board");
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText() => L10n.Tr("QUEST_BOARD_INFO", _currentPoiId);
    protected override string GetDescriptionText() => L10n.Tr("QUEST_BOARD_DESC");
    protected override string GetLeaveButtonText() => L10n.Tr("JOIN_BATTLE_LEAVE");

    protected override void PopulateActions(VBoxContainer container)
    {
        bool hasAnyEntry = false;

        if (_questManager != null)
        {
            var ready = _questManager.GetRewardReadyQuestsForPoi(_currentPoiId);
            if (ready.Count > 0)
            {
                var readyTitle = CreateBodyLabel(L10n.Tr("QUEST_REWARD_READY_TITLE"), ThemeTextAccent);
                container.AddChild(readyTitle);

                foreach (var quest in ready)
                {
                    container.AddChild(CreateRewardReadyCard(quest));
                }

                hasAnyEntry = true;
            }
        }

        if (_questGenerator == null)
        {
            if (!hasAnyEntry)
                container.AddChild(CreateMutedLabel(L10n.Tr("QUEST_NONE_AVAILABLE")));
            return;
        }

        var quests = _questGenerator.GetAvailableQuests(_currentPoiId, _currentDay);

        if (quests.Count > 0)
        {
            var availableTitle = CreateBodyLabel(L10n.Tr("QUEST_AVAILABLE_TITLE"), ThemeTextAccent);
            container.AddChild(availableTitle);
        }

        if (quests.Count == 0)
        {
            if (!hasAnyEntry)
                container.AddChild(CreateMutedLabel(L10n.Tr("QUEST_NONE_AVAILABLE")));
            return;
        }

        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            var card = CreateQuestCard(q, i);
            container.AddChild(card);
        }
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>动态委托版本（从 QuestGenerator 获取任务）</summary>
    public void ShowBoardDynamic(QuestGenerator questGenerator, string poiId, int currentDay, bool instantOverlay = false)
    {
        _questGenerator = questGenerator;
        _questManager = null;
        _currentPoiId = poiId;
        _currentDay = currentDay;
        ShowPanel(instantOverlay);
    }

    /// <summary>上下文版本：从 PoiPanelContext 获取生成器、QuestManager 与当前 POI。</summary>
    public void ShowBoardDynamic(PoiPanelContext context, bool instantOverlay = false)
    {
        _questGenerator = context.QuestGenerator;
        _questManager = context.QuestManager;
        _currentPoiId = context.PoiId;
        _currentDay = context.CurrentDay;
        ShowPanel(instantOverlay);
    }

    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.BoardClosed);
    }

    // ============================================================================
    // 任务卡片
    // ============================================================================

    private Control CreateQuestCard(QuestData quest, int index)
    {
        var card = CreateCard(new Vector2(0, 0));
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var innerMargin = new MarginContainer();
        innerMargin.AddThemeConstantOverride("margin_left", 10);
        innerMargin.AddThemeConstantOverride("margin_top", 8);
        innerMargin.AddThemeConstantOverride("margin_right", 10);
        innerMargin.AddThemeConstantOverride("margin_bottom", 8);
        innerMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(innerMargin);

        var qvbox = new VBoxContainer();
        qvbox.AddThemeConstantOverride("separation", SpacingXs);
        qvbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        innerMargin.AddChild(qvbox);

        // 标题行
        var titleRow = new HBoxContainer();
        var titleLbl = CreateBodyLabel(quest.QuestName);
        titleLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(titleLbl);

        var diffLbl = CreateMutedLabel($"[{quest.GetDifficultyText()}]");
        titleRow.AddChild(diffLbl);
        qvbox.AddChild(titleRow);

        // 描述
        var descLbl = CreateMutedLabel(quest.Description);
        qvbox.AddChild(descLbl);

        // 奖励行
        var rewardRow = new HBoxContainer();
        string timeText = quest.HasTimeLimit ? L10n.Tr("QUEST_TIME_LIMIT", quest.TimeLimitDays) : L10n.Tr("QUEST_NO_TIME_LIMIT");
        var rewardLabel = CreateMutedLabel(
            L10n.Tr("QUEST_BOARD_REWARD_LINE", quest.RewardGold, quest.RewardReputation, timeText));
        rewardLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rewardRow.AddChild(rewardLabel);

        var acceptBtn = CreateButton(L10n.Tr("QUEST_ACCEPT"), new Vector2(80, 28));
        int capturedIndex = index;
        acceptBtn.Pressed += () => AcceptQuest(capturedIndex);
        rewardRow.AddChild(acceptBtn);
        qvbox.AddChild(rewardRow);

        return card;
    }

    private Control CreateRewardReadyCard(QuestData quest)
    {
        var card = CreateCard(new Vector2(0, 0));
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var innerMargin = new MarginContainer();
        innerMargin.AddThemeConstantOverride("margin_left", 10);
        innerMargin.AddThemeConstantOverride("margin_top", 8);
        innerMargin.AddThemeConstantOverride("margin_right", 10);
        innerMargin.AddThemeConstantOverride("margin_bottom", 8);
        innerMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(innerMargin);

        var qvbox = new VBoxContainer();
        qvbox.AddThemeConstantOverride("separation", SpacingXs);
        qvbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        innerMargin.AddChild(qvbox);

        var titleRow = new HBoxContainer();
        var titleLbl = CreateBodyLabel(quest.QuestName, ThemeTextPositive);
        titleLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(titleLbl);
        titleRow.AddChild(CreateMutedLabel(L10n.Tr("QUEST_COMPLETED_BRACKET")));
        qvbox.AddChild(titleRow);

        var descLbl = CreateMutedLabel(quest.Description);
        qvbox.AddChild(descLbl);

        var rewardRow = new HBoxContainer();
        var rewardLabel = CreateMutedLabel(L10n.Tr("QUEST_CLAIMABLE_REWARD_LINE", quest.RewardGold, quest.RewardReputation));
        rewardLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rewardRow.AddChild(rewardLabel);

        var claimBtn = CreateButton(L10n.Tr("QUEST_CLAIM"), new Vector2(80, 28));
        claimBtn.Pressed += () => ClaimQuestReward(quest.QuestId);
        rewardRow.AddChild(claimBtn);
        qvbox.AddChild(rewardRow);

        return card;
    }

    private void AcceptQuest(int index)
    {
        if (_questGenerator == null) return;

        var result = QuestAcceptanceService.AcceptFromBoard(
            _questGenerator,
            _currentPoiId,
            index,
            _currentDay,
            _questManager != null ? _questManager.AcceptQuest : null);

        if (result.Success && result.Quest != null)
        {
            SetResult($"[color=green]{result.Message}[/color]");
            EmitSignal(SignalName.QuestAccepted, result.Quest.QuestId);
            RefreshLayout();
        }
        else
        {
            SetResult($"[color=red]{result.Message}[/color]");
        }
    }

    private void ClaimQuestReward(string questId)
    {
        if (_questManager == null) return;

        if (_questManager.ClaimReward(questId))
        {
            SetResult($"[color=green]{L10n.Tr("QUEST_REWARD_CLAIMED")}[/color]");
            RefreshLayout();
        }
        else
        {
            SetResult($"[color=red]{L10n.Tr("QUEST_REWARD_CLAIM_FAILED")}[/color]");
        }
    }
}
