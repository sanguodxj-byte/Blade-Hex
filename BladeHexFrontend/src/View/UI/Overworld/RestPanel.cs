// RestPanel.cs
// 休息面板 — 城内等待：推进时间，恢复倍率 300%（4x）
// 城内恢复免费（人在城内即享有 4x 倍率），旅店只是入口设施。
// 所有恢复走 RestService.TimeBasedRecovery 统一路径（AdvanceTime 触发）。
// 欠饷 + 断粮阻断所有恢复。
using System;
using Godot;
using BladeHex.Data;
using BladeHex.Localization;
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

    /// <summary>城内恢复倍率（300% = 4x）</summary>
    public const float TownRecoveryRate = 4.0f;

    /// <summary>每次等待推进小时数</summary>
    public const float WaitHours = 8.0f;

    private PoiPanelContext? _context;
    private EconomyManager? Economy => _context?.Economy;
    private PartyRoster? Roster => _context?.Roster;

    /// <summary>综合恢复条件：不欠饷且不断粮</summary>
    private bool CanRestore
    {
        get
        {
            if (Economy == null) return false;
            bool paid = Economy.WageSys.CanRestore;
            bool fed = Economy.FoodSys.ConsecutiveStarveDays == 0;
            return paid && fed;
        }
    }

    protected override Color GetIllustrationColor() => new(0.06f, 0.06f, 0.12f, 1.0f);
    protected override string GetIllustrationText() => L10n.Tr("FACILITY_REST_BRACKET");
    protected override string? GetIllustrationPath()
        => POIIllustrationResolver.GetPanelIllustration("rest");
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        string state = Roster != null ? L10n.Tr("REST_NEED_RECOVERY", RestService.CountMembersNeedingRest(Roster)) : L10n.Tr("REASON_NO_PARTY");
        return L10n.Tr("REST_INFO", state);
    }
    protected override string GetDescriptionText()
    {
        string blockReason = "";
        if (Economy != null)
        {
            if (!Economy.WageSys.CanRestore)
                blockReason = $"\n[color=red]{L10n.Tr("REST_BLOCK_UNPAID")}[/color]";
            else if (Economy.FoodSys.ConsecutiveStarveDays > 0)
                blockReason = $"\n[color=red]{L10n.Tr("REST_BLOCK_STARVING")}[/color]";
        }
        return L10n.Tr("REST_DESC", WaitHours, TownRecoveryRate) + blockReason;
    }
    protected override string GetLeaveButtonText() => L10n.Tr("EVENT_CANCEL");

    protected override void PopulateActions(VBoxContainer container)
    {
        bool hasRoster = Roster != null && Roster.Count > 0;
        bool needsRest = RestService.CountMembersNeedingRest(Roster) > 0;
        bool canRestore = CanRestore;

        // 在城内等待（免费）
        bool canWait = hasRoster && needsRest && canRestore;
        string waitReason = !hasRoster ? L10n.Tr("REASON_NO_PARTY") : !needsRest ? L10n.Tr("REST_REASON_HEALTHY") : L10n.Tr("REST_REASON_BLOCKED");

        var btnWait = CreateActionButton(
            L10n.Tr("REST_WAIT_ACTION", WaitHours, TownRecoveryRate),
            canWait, waitReason);
        btnWait.Pressed += DoTownWait;
        container.AddChild(btnWait);
    }

    public void ShowRest(EconomyManager economy, bool instantOverlay = false)
    {
        _context = new PoiPanelContext { Economy = economy, PlayerParty = null, CurrentTown = null };
        ShowPanel(instantOverlay);
    }

    public void ShowRest(PoiPanelContext context, bool instantOverlay = false)
    {
        _context = context;
        ShowPanel(instantOverlay);
    }

    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.RestFinished);
    }

    private void DoTownWait()
    {
        // 城内恢复 4x（AdvanceTime 内触发 TimeBasedRecovery）
        Economy?.AdvanceTime(WaitHours, TownRecoveryRate);
        EmitSignal(SignalName.RestCompleted, (int)WaitHours);
        SetResult($"[color=green]{L10n.Tr("REST_WAIT_DONE", WaitHours, TownRecoveryRate)}[/color]");
        RefreshLayout();
    }
}
