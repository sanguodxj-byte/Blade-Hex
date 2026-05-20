// QuestBoardPanel.cs
// 委托布告栏面板 — 查看和接取动态生成的任务
// 使用统一布局基类，只填充数据
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;

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
    protected override string GetIllustrationText() => "[ 布告栏 ]";
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText() => $"布告栏 | {_currentPoiId}";
    protected override string GetDescriptionText() => "领主发布的悬赏委托。完成任务可获得金币和声望奖励。";
    protected override string GetLeaveButtonText() => "离开";

    protected override void PopulateActions(VBoxContainer container)
    {
        if (_questGenerator == null)
        {
            container.AddChild(CreateMutedLabel("暂无可接取的委托。过几天再来看看。"));
            return;
        }

        var quests = _questGenerator.GetAvailableQuests(_currentPoiId, _currentDay);

        if (quests.Count == 0)
        {
            container.AddChild(CreateMutedLabel("暂无可接取的委托。过几天再来看看。"));
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
    public void ShowBoardDynamic(QuestGenerator questGenerator, string poiId, int currentDay)
    {
        _questGenerator = questGenerator;
        _questManager = null;
        _currentPoiId = poiId;
        _currentDay = currentDay;
        ShowPanel();
    }

    /// <summary>上下文版本：从 PoiPanelContext 获取生成器、QuestManager 与当前 POI。</summary>
    public void ShowBoardDynamic(PoiPanelContext context)
    {
        _questGenerator = context.QuestGenerator;
        _questManager = context.QuestManager;
        _currentPoiId = context.PoiId;
        _currentDay = context.CurrentDay;
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.BoardClosed);
        HidePanel();
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
        string timeText = quest.HasTimeLimit ? $"期限: {quest.TimeLimitDays}天" : "无期限";
        var rewardLabel = CreateMutedLabel(
            $"奖励: {quest.RewardGold}金 | 声望+{quest.RewardReputation} | {timeText}");
        rewardLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rewardRow.AddChild(rewardLabel);

        var acceptBtn = CreateButton("接取", new Vector2(80, 28));
        int capturedIndex = index;
        acceptBtn.Pressed += () => AcceptQuest(capturedIndex);
        rewardRow.AddChild(acceptBtn);
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
}
