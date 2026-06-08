// RecruitPanel.cs
// 酒馆招募面板 — 显示可招募佣兵列表
// 使用统一布局基类，只填充数据
using Godot;
using BladeHex.Data;
using BladeHex.Localization;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class RecruitPanel : POIPanelBase
{
    // ============================================================================
    // 信号
    // ============================================================================

    [Signal]
    public delegate void RecruitFinishedEventHandler(bool hired);

    // ============================================================================
    // 字段
    // ============================================================================

    private RecruitService? _recruitService;
    private EconomyManager? _economy;
    private PartyRoster? _roster;
    private string _currentPoiId = "";
    private int _currentDay = 1;

    // ── 数据填充 ──

    protected override Color GetIllustrationColor() => new(0.10f, 0.06f, 0.04f, 1.0f);
    protected override string GetIllustrationText() => L10n.Tr("FACILITY_TAVERN_BRACKET");
    protected override string? GetIllustrationPath()
        => POIIllustrationResolver.GetPanelIllustration("recruit");
    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        string gold = _economy != null ? L10n.Tr("COMMON_GOLD_VALUE", _economy.Gold) : "";
        string roster = _roster != null ? L10n.Tr("COMMON_PARTY_CAPACITY", _roster.Count, _roster.Capacity) : "";
        return L10n.Tr("RECRUIT_INFO", gold, roster);
    }

    protected override string GetDescriptionText() => L10n.Tr("RECRUIT_DESC");
    protected override string GetLeaveButtonText() => L10n.Tr("RECRUIT_LEAVE");

    protected override void PopulateActions(VBoxContainer container)
    {
        if (_recruitService == null)
        {
            container.AddChild(CreateMutedLabel(L10n.Tr("RECRUIT_NONE")));
            return;
        }

        var available = _recruitService.GetAvailableGd(_currentPoiId, _currentDay);
        if (available.Count == 0)
        {
            container.AddChild(CreateMutedLabel(L10n.Tr("RECRUIT_NONE")));
            return;
        }

        for (int i = 0; i < available.Count; i++)
        {
            var recruitVar = available[i];
            if (recruitVar.AsGodotObject() is RecruitableUnit recruit && recruit.Unit != null)
            {
                var row = CreateRecruitRow(i, recruit, recruit.Unit);
                container.AddChild(row);
            }
        }
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void ShowRecruitList(RecruitService recruitService, string poiId,
        EconomyManager economy, OverworldParty party, int currentDay, bool instantOverlay = false)
    {
        ShowRecruitList(new PoiPanelContext
        {
            RecruitService = recruitService,
            Economy = economy,
            PlayerParty = party,
            CurrentTown = null,
        }, poiId, currentDay, instantOverlay);
    }

    public void ShowRecruitList(PoiPanelContext context, bool instantOverlay = false)
    {
        ShowRecruitList(context, context.PoiId, context.CurrentDay, instantOverlay);
    }

    private void ShowRecruitList(PoiPanelContext context, string poiId, int currentDay, bool instantOverlay = false)
    {
        _recruitService = context.RecruitService;
        _economy = context.Economy;
        _roster = context.Roster;
        _currentPoiId = poiId;
        _currentDay = currentDay;
        ShowPanel(instantOverlay);
    }

    protected override void OnCloseRequested()
    {
        HidePanel();
        EmitSignal(SignalName.RecruitFinished, false);
    }

    // ============================================================================
    // 招募卡片
    // ============================================================================

    private Control CreateRecruitRow(int index, RecruitableUnit recruit, UnitData unit)
    {
        var card = CreateCard(new Vector2(0, 0));
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", SpacingMd);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        card.AddChild(margin);
        margin.AddChild(hbox);

        // 左侧信息
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var nameLabel = CreateBodyLabel(unit.UnitName);
        infoVbox.AddChild(nameLabel);

        string raceName = unit.Race?.RaceName ?? L10n.Tr("COMMON_UNKNOWN");
        string statsText = L10n.Tr("RECRUIT_STATS", unit.Level, raceName, unit.Str, unit.Dex, unit.Con);
        var statsLabel = CreateMutedLabel(statsText);
        infoVbox.AddChild(statsLabel);

        string costText = L10n.Tr("RECRUIT_COST", recruit.Cost, recruit.WeeklyWage);
        var costLabel = CreateMutedLabel(costText);
        costLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        infoVbox.AddChild(costLabel);

        hbox.AddChild(infoVbox);

        // 右侧按钮
        var btn = CreateButton(L10n.Tr("RECRUIT_BUTTON"), new Vector2(80, 50));
        int capturedIndex = index;
        btn.Pressed += () => DoRecruit(capturedIndex);

        if (_roster?.IsFull == true)
        {
            btn.Disabled = true;
            btn.TooltipText = L10n.Tr("REASON_PARTY_FULL");
        }
        else if (_economy != null && _economy.Gold < recruit.Cost)
        {
            btn.Disabled = true;
            btn.TooltipText = L10n.Tr("REASON_NOT_ENOUGH_GOLD");
        }

        hbox.AddChild(btn);
        return card;
    }

    private void DoRecruit(int index)
    {
    	if (_recruitService == null || _roster == null || _economy == null) return;
   
    	var result = _recruitService.Recruit(
    		_currentPoiId, index, _roster, _currentDay, _economy);
   
    	if (result != null)
    	{
    		SetResult($"[color=green]{L10n.Tr("RECRUIT_SUCCESS", result.UnitName)}[/color]");
    		// 先隐藏面板再发出信号，避免 RecruitPanel 与 TownPanel 叠放
    		HidePanel();
    		EmitSignal(SignalName.RecruitFinished, true);
    	}
    	else
    	{
    		SetResult($"[color=red]{L10n.Tr("RECRUIT_FAILED")}[/color]");
    		RefreshLayout();
    	}
    }
}
