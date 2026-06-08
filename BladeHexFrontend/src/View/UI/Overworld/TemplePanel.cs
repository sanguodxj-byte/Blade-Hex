// TemplePanel.cs
// 药师所面板 — 治疗伤痛、净化诅咒、购买净化药水
// 使用统一布局基类，只填充数据；治疗/净化规则委托给 HealingService。
using Godot;
using BladeHex.Data;
using BladeHex.Localization;
using BladeHex.Strategic.Facilities;
using BladeHex.Strategic.Economy;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TemplePanel : POIPanelBase
{
    [Signal]
    public delegate void TempleFinishedEventHandler();

    private PoiPanelContext? _context;
    private EconomyManager? Economy => _context?.Economy;
    private PartyRoster? Roster => _context?.Roster;

    protected override Color GetIllustrationColor() => new(0.08f, 0.12f, 0.08f, 1.0f);
    protected override string GetIllustrationText() => L10n.Tr("FACILITY_TEMPLE_BRACKET");
    protected override string? GetIllustrationPath()
        => POIIllustrationResolver.GetPanelIllustration("temple");
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        string gold = Economy != null ? L10n.Tr("COMMON_GOLD_VALUE", Economy.Gold) : L10n.Tr("COMMON_GOLD_DASH");
        string wounded = Roster != null ? L10n.Tr("TEMPLE_INJURED", HealingService.CountInjuredMembers(Roster)) : L10n.Tr("REASON_NO_PARTY");
        return L10n.Tr("TEMPLE_INFO", gold, wounded);
    }
    protected override string GetDescriptionText() => L10n.Tr("TEMPLE_DESC");
    protected override string GetLeaveButtonText() => L10n.Tr("TEMPLE_LEAVE");

    protected override void PopulateActions(VBoxContainer container)
    {
        bool hasRoster = Roster != null && Roster.Count > 0;

        int minorCost = FacilityPricingService.GetHealCost(Roster, 0.5f);
        bool canMinor = Economy != null && Economy.Gold >= minorCost && hasRoster;
        var btnMinor = CreateActionButton(L10n.Tr("TEMPLE_MINOR_HEAL", minorCost), canMinor, hasRoster ? L10n.Tr("REASON_NOT_ENOUGH_GOLD") : L10n.Tr("REASON_NO_PARTY"));
        btnMinor.Pressed += () => ApplyResult(HealingService.HealToRatio(Roster, 0.5f, minorCost, SpendGold));
        container.AddChild(btnMinor);

        int majorCost = FacilityPricingService.GetHealCost(Roster, 1.0f);
        bool canMajor = Economy != null && Economy.Gold >= majorCost && hasRoster;
        var btnMajor = CreateActionButton(L10n.Tr("TEMPLE_MAJOR_HEAL", majorCost), canMajor, hasRoster ? L10n.Tr("REASON_NOT_ENOUGH_GOLD") : L10n.Tr("REASON_NO_PARTY"));
        btnMajor.Pressed += () => ApplyResult(HealingService.HealToRatio(Roster, 1.0f, majorCost, SpendGold));
        container.AddChild(btnMajor);

        bool hasNegativeEffects = HealingService.CountNegativeEffects(Roster) > 0;
        int purifyCost = FacilityPricingService.GetPurifyCost(Roster);
        bool canPurify = Economy != null && Economy.Gold >= purifyCost && hasRoster && hasNegativeEffects;
        string purifyReason = !hasRoster ? L10n.Tr("REASON_NO_PARTY") : !hasNegativeEffects ? L10n.Tr("TEMPLE_REASON_NO_NEGATIVE") : L10n.Tr("REASON_NOT_ENOUGH_GOLD");
        var btnPurify = CreateActionButton(L10n.Tr("TEMPLE_PURIFY", purifyCost), canPurify, purifyReason);
        btnPurify.Pressed += () => ApplyResult(HealingService.PurifyAll(Roster, SpendGold));
        container.AddChild(btnPurify);

        int holyWaterCost = FacilityPricingService.GetHolyWaterCost(_context?.CurrentTown?.Prosperity ?? 50);
        bool canBuy = Economy != null && Economy.Gold >= holyWaterCost;
        var btnHoly = CreateActionButton(L10n.Tr("TEMPLE_BUY_HOLY_WATER", holyWaterCost), canBuy, L10n.Tr("REASON_NOT_ENOUGH_GOLD"));
        btnHoly.Pressed += DoBuyHolyWater;
        container.AddChild(btnHoly);
    }

    public void ShowTemple(EconomyManager economy, bool instantOverlay = false)
    {
        _context = new PoiPanelContext { Economy = economy, PlayerParty = null, CurrentTown = null };
        ShowPanel(instantOverlay);
    }

    public void ShowTemple(PoiPanelContext context, bool instantOverlay = false)
    {
        _context = context;
        ShowPanel(instantOverlay);
    }

    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.TempleFinished);
    }

    private bool SpendGold(int amount) => Economy?.SpendGold(amount) == true;

    private void DoBuyHolyWater()
    {
        int cost = FacilityPricingService.GetHolyWaterCost(_context?.CurrentTown?.Prosperity ?? 50);
        if (Economy == null || !Economy.SpendGold(cost))
        {
            ApplyResult(FacilityServiceResult.Fail(L10n.Tr("TEMPLE_BUY_FAILED")));
            return;
        }

        var baseItem = PrototypeData.GetConsumables().TryGetValue("holy_water", out var item)
            ? item
            : HealingService.CreateFallbackHolyWater();
        Economy.AddItem((ItemData)baseItem.Duplicate());

        ApplyResult(FacilityServiceResult.Ok(L10n.Tr("TEMPLE_BUY_SUCCESS"), goldSpent: cost, affectedItems: 1));
    }

    private void ApplyResult(FacilityServiceResult result)
    {
        string color = result.Success ? "green" : "red";
        SetResult($"[color={color}]{result.Message}[/color]");
        RefreshLayout();
    }
}
