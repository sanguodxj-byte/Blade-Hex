// SmithyPanel.cs
// 铁匠铺面板 — 修理装备、磨砺武器、加固防具
// 使用统一布局基类，只填充数据；业务规则委托给 SmithyService。
using Godot;
using BladeHex.Data;
using BladeHex.Localization;
using BladeHex.Strategic.Facilities;
using BladeHex.Strategic.Economy;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class SmithyPanel : POIPanelBase
{
    [Signal] public delegate void SmithyFinishedEventHandler();

    private PoiPanelContext? _context;
    private EconomyManager? Economy => _context?.Economy;
    private PartyRoster? Roster => _context?.Roster;

    protected override Color GetIllustrationColor() => new(0.12f, 0.08f, 0.04f, 1.0f);
    protected override string GetIllustrationText() => L10n.Tr("FACILITY_SMITHY_BRACKET");
    protected override string? GetIllustrationPath()
        => POIIllustrationResolver.GetPanelIllustration("smithy");
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        string gold = Economy != null ? L10n.Tr("COMMON_GOLD_VALUE", Economy.Gold) : L10n.Tr("COMMON_GOLD_DASH");
        string memberCount = Roster != null ? L10n.Tr("COMMON_PARTY_MEMBERS", Roster.Count) : L10n.Tr("REASON_NO_PARTY");
        return L10n.Tr("SMITHY_INFO", gold, memberCount);
    }
    protected override string GetDescriptionText() => L10n.Tr("SMITHY_DESC");
    protected override string GetLeaveButtonText() => L10n.Tr("SMITHY_LEAVE");

    protected override void PopulateActions(VBoxContainer container)
    {
        int repairCost = SmithyService.CalculateRepairCost(Roster);
        bool hasDamaged = SmithyService.CountDamagedArmorPieces(Roster) > 0;
        bool canRepair = Economy != null && repairCost > 0 && Economy.Gold >= repairCost && hasDamaged;
        string repairReason = !hasDamaged ? L10n.Tr("SMITHY_REASON_NO_DAMAGED") : L10n.Tr("REASON_NOT_ENOUGH_GOLD");
        var btnRepair = CreateActionButton(L10n.Tr("SMITHY_REPAIR_ALL", repairCost), canRepair, repairReason);
        btnRepair.Pressed += () => ApplyResult(SmithyService.RepairAll(Roster, SpendGold));
        container.AddChild(btnRepair);

        bool hasWeapon = Roster?.Leader?.PrimaryMainHand != null;
        int sharpenCost = FacilityPricingService.GetSharpenCost(Roster?.Leader?.PrimaryMainHand);
        bool canSharpen = Economy != null && Economy.Gold >= sharpenCost && hasWeapon;
        var btnSharpen = CreateActionButton(L10n.Tr("SMITHY_SHARPEN", sharpenCost), canSharpen, hasWeapon ? L10n.Tr("REASON_NOT_ENOUGH_GOLD") : L10n.Tr("SMITHY_REASON_NO_MAIN_WEAPON"));
        btnSharpen.Pressed += () => ApplyResult(SmithyService.SharpenLeaderWeapon(Roster, SpendGold));
        container.AddChild(btnSharpen);

        bool hasArmor = Roster?.Leader?.Armor != null;
        int reinforceCost = FacilityPricingService.GetReinforceCost(Roster?.Leader?.Armor);
        bool canReinforce = Economy != null && Economy.Gold >= reinforceCost && hasArmor;
        var btnReinforce = CreateActionButton(L10n.Tr("SMITHY_REINFORCE", reinforceCost), canReinforce, hasArmor ? L10n.Tr("REASON_NOT_ENOUGH_GOLD") : L10n.Tr("SMITHY_REASON_NO_ARMOR"));
        btnReinforce.Pressed += () => ApplyResult(SmithyService.ReinforceLeaderArmor(Roster, SpendGold));
        container.AddChild(btnReinforce);
    }

    public void ShowSmithy(EconomyManager economy, bool instantOverlay = false)
    {
        _context = new PoiPanelContext { Economy = economy, PlayerParty = null, CurrentTown = null };
        ShowPanel(instantOverlay);
    }

    public void ShowSmithy(PoiPanelContext context, bool instantOverlay = false)
    {
        _context = context;
        ShowPanel(instantOverlay);
    }

    protected override void OnCloseRequested()
    {
    	HidePanel();
    	EmitSignal(SignalName.SmithyFinished);
    }

    private bool SpendGold(int amount) => Economy?.SpendGold(amount) == true;

    private void ApplyResult(FacilityServiceResult result)
    {
        string color = result.Success ? "green" : "red";
        SetResult($"[color={color}]{result.Message}[/color]");
        RefreshLayout();
    }
}
