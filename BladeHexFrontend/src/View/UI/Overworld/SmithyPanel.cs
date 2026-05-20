// SmithyPanel.cs
// 铁匠铺面板 — 修理装备、磨砺武器、加固防具
// 使用统一布局基类，只填充数据；业务规则委托给 SmithyService。
using Godot;
using BladeHex.Data;
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
    protected override string GetIllustrationText() => "[ 铁匠铺 ]";
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        string gold = Economy != null ? $"金币: {Economy.Gold}" : "金币: —";
        string memberCount = Roster != null ? $"队伍: {Roster.Count}人" : "无队伍";
        return $"铁匠铺 | {gold} | {memberCount}";
    }
    protected override string GetDescriptionText() => "经验丰富的铁匠可以帮你修理和强化装备。炉火通明，铁锤声不绝于耳。";
    protected override string GetLeaveButtonText() => "离开铁匠铺";

    protected override void PopulateActions(VBoxContainer container)
    {
        int repairCost = SmithyService.CalculateRepairCost(Roster);
        bool hasDamaged = SmithyService.CountDamagedArmorPieces(Roster) > 0;
        bool canRepair = Economy != null && repairCost > 0 && Economy.Gold >= repairCost && hasDamaged;
        string repairReason = !hasDamaged ? "所有装备耐久完好" : "金币不足";
        var btnRepair = CreateActionButton($"全副修理 ({repairCost}金) -- 恢复所有装备耐久", canRepair, repairReason);
        btnRepair.Pressed += () => ApplyResult(SmithyService.RepairAll(Roster, SpendGold));
        container.AddChild(btnRepair);

        bool hasWeapon = Roster?.Leader?.PrimaryMainHand != null;
        int sharpenCost = FacilityPricingService.GetSharpenCost(Roster?.Leader?.PrimaryMainHand);
        bool canSharpen = Economy != null && Economy.Gold >= sharpenCost && hasWeapon;
        var btnSharpen = CreateActionButton($"磨砺武器 ({sharpenCost}金) -- 队长主手武器伤害永久+1", canSharpen, hasWeapon ? "金币不足" : "队长未装备主手武器");
        btnSharpen.Pressed += () => ApplyResult(SmithyService.SharpenLeaderWeapon(Roster, SpendGold));
        container.AddChild(btnSharpen);

        bool hasArmor = Roster?.Leader?.Armor != null;
        int reinforceCost = FacilityPricingService.GetReinforceCost(Roster?.Leader?.Armor);
        bool canReinforce = Economy != null && Economy.Gold >= reinforceCost && hasArmor;
        var btnReinforce = CreateActionButton($"加固防具 ({reinforceCost}金) -- 队长身甲装甲阈值永久+1", canReinforce, hasArmor ? "金币不足" : "队长未装备身甲");
        btnReinforce.Pressed += () => ApplyResult(SmithyService.ReinforceLeaderArmor(Roster, SpendGold));
        container.AddChild(btnReinforce);
    }

    public void ShowSmithy(EconomyManager economy)
    {
        _context = new PoiPanelContext { Economy = economy, PlayerParty = null, CurrentTown = null };
        ShowPanel();
    }

    public void ShowSmithy(PoiPanelContext context)
    {
        _context = context;
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.SmithyFinished);
        HidePanel();
    }

    private bool SpendGold(int amount) => Economy?.SpendGold(amount) == true;

    private void ApplyResult(FacilityServiceResult result)
    {
        string color = result.Success ? "green" : "red";
        SetResult($"[color={color}]{result.Message}[/color]");
        RefreshLayout();
    }
}
