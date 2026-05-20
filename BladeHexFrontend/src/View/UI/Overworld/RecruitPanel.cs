// RecruitPanel.cs
// 酒馆招募面板 — 显示可招募佣兵列表
// 使用统一布局基类，只填充数据
using Godot;
using BladeHex.Data;
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
    protected override string GetIllustrationText() => "[ 酒馆 ]";
    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        string gold = _economy != null ? $"金币: {_economy.Gold}" : "";
        string roster = _roster != null ? $"队伍: {_roster.Count}/{_roster.Capacity}" : "";
        return $"酒馆招募 | {gold} | {roster}";
    }

    protected override string GetDescriptionText() => "冒险者的聚集地。形形色色的佣兵在此等待雇主，支付招募费即可加入队伍。";
    protected override string GetLeaveButtonText() => "离开酒馆";

    protected override void PopulateActions(VBoxContainer container)
    {
        if (_recruitService == null)
        {
            container.AddChild(CreateMutedLabel("暂无可招募的佣兵。过几天再来看看。"));
            return;
        }

        var available = _recruitService.GetAvailableGd(_currentPoiId, _currentDay);
        if (available.Count == 0)
        {
            container.AddChild(CreateMutedLabel("暂无可招募的佣兵。过几天再来看看。"));
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
        EconomyManager economy, OverworldParty party, int currentDay)
    {
        ShowRecruitList(new PoiPanelContext
        {
            RecruitService = recruitService,
            Economy = economy,
            PlayerParty = party,
            CurrentTown = null,
        }, poiId, currentDay);
    }

    public void ShowRecruitList(PoiPanelContext context)
    {
        ShowRecruitList(context, context.PoiId, context.CurrentDay);
    }

    private void ShowRecruitList(PoiPanelContext context, string poiId, int currentDay)
    {
        _recruitService = context.RecruitService;
        _economy = context.Economy;
        _roster = context.Roster;
        _currentPoiId = poiId;
        _currentDay = currentDay;
        ShowPanel();
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

        string raceName = unit.Race?.RaceName ?? "未知";
        string statsText = $"等级{unit.Level} {raceName} | 力{unit.Str} 敏{unit.Dex} 体{unit.Con}";
        var statsLabel = CreateMutedLabel(statsText);
        infoVbox.AddChild(statsLabel);

        string costText = $"招募费: {recruit.Cost}金 | 周薪: {recruit.WeeklyWage}金";
        var costLabel = CreateMutedLabel(costText);
        costLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        infoVbox.AddChild(costLabel);

        hbox.AddChild(infoVbox);

        // 右侧按钮
        var btn = CreateButton("招募", new Vector2(80, 50));
        int capturedIndex = index;
        btn.Pressed += () => DoRecruit(capturedIndex);

        if (_roster?.IsFull == true)
        {
            btn.Disabled = true;
            btn.TooltipText = "队伍已满";
        }
        else if (_economy != null && _economy.Gold < recruit.Cost)
        {
            btn.Disabled = true;
            btn.TooltipText = "金币不足";
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
            SetResult($"[color=green]{result.UnitName} 已加入队伍![/color]");
            EmitSignal(SignalName.RecruitFinished, true);
        }
        else
        {
            SetResult("[color=red]招募失败（金币不足或队伍已满）[/color]");
        }

        RefreshLayout();
    }
}
