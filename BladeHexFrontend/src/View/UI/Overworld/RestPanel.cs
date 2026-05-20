// RestPanel.cs
// 休息面板 — 短休息/长休息恢复队伍状态
// 使用统一布局基类，只填充数据；恢复规则委托给 RestService。
using Godot;
using BladeHex.Data;
using BladeHex.Strategic.Facilities;
using BladeHex.Strategic.Economy;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class RestPanel : POIPanelBase
{
    [Signal]
    public delegate void RestCompletedEventHandler(int hours);

    [Signal]
    public delegate void RestFinishedEventHandler();

    private PoiPanelContext? _context;
    private EconomyManager? Economy => _context?.Economy;
    private PartyRoster? Roster => _context?.Roster;

    protected override Color GetIllustrationColor() => new(0.06f, 0.06f, 0.12f, 1.0f);
    protected override string GetIllustrationText() => "[ 休息 ]";
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        string gold = Economy != null ? $"金币: {Economy.Gold}" : "金币: —";
        string state = Roster != null ? $"需恢复成员: {RestService.CountMembersNeedingRest(Roster)}" : "无队伍";
        return $"休息 | {gold} | {state}";
    }
    protected override string GetDescriptionText() => "在安全的地方休息，让疲惫的队伍恢复体力和精力。";
    protected override string GetLeaveButtonText() => "离开";

    protected override void PopulateActions(VBoxContainer container)
    {
        bool hasRoster = Roster != null && Roster.Count > 0;
        var btnShort = CreateActionButton("短休息 (免费) -- 恢复50%法力值，推进4小时", hasRoster, "无队伍");
        btnShort.Pressed += DoShortRest;
        container.AddChild(btnShort);

        bool needsLongRest = RestService.CountMembersNeedingRest(Roster) > 0;
        int longRestCost = FacilityPricingService.GetLongRestCost(Roster, _context?.CurrentTown?.Prosperity ?? 50);
        bool canLong = Economy != null && Economy.Gold >= longRestCost && hasRoster && needsLongRest;
        string reason = !hasRoster ? "无队伍" : !needsLongRest ? "队伍状态良好" : "金币不足";
        var btnLong = CreateActionButton($"长休息 ({longRestCost}金) -- 恢复100%生命和法力，推进8小时", canLong, reason);
        btnLong.Pressed += DoLongRest;
        container.AddChild(btnLong);
    }

    public void ShowRest(EconomyManager economy)
    {
        _context = new PoiPanelContext { Economy = economy, PlayerParty = null, CurrentTown = null };
        ShowPanel();
    }

    public void ShowRest(PoiPanelContext context)
    {
        _context = context;
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.RestFinished);
        HidePanel();
    }

    private bool SpendGold(int amount) => Economy?.SpendGold(amount) == true;

    private void DoShortRest()
    {
        var result = RestService.ShortRest(Roster);
        if (result.Success)
        {
            Economy?.AdvanceTime(4.0f);
            EmitSignal(SignalName.RestCompleted, 4);
        }
        ApplyResult(result);
    }

    private void DoLongRest()
    {
        var result = RestService.LongRest(Roster, SpendGold);
        if (result.Success)
        {
            Economy?.AdvanceTime(8.0f);
            EmitSignal(SignalName.RestCompleted, 8);
        }
        ApplyResult(result);
    }

    private void ApplyResult(FacilityServiceResult result)
    {
        string color = result.Success ? "green" : "red";
        SetResult($"[color={color}]{result.Message}[/color]");
        RefreshLayout();
    }
}
