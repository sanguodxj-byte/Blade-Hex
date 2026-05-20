// TemplePanel.cs
// 药师所面板 — 治疗伤痛、净化诅咒、购买净化药水
// 使用统一布局基类，只填充数据；治疗/净化规则委托给 HealingService。
using Godot;
using BladeHex.Data;
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
    protected override string GetIllustrationText() => "[ 药师所 ]";
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText()
    {
        string gold = Economy != null ? $"金币: {Economy.Gold}" : "金币: —";
        string wounded = Roster != null ? $"伤员: {HealingService.CountInjuredMembers(Roster)}" : "无队伍";
        return $"药师所 | {gold} | {wounded}";
    }
    protected override string GetDescriptionText() => "药师的力量可以治愈伤痛，净化邪恶。炉中草药的清香弥漫整个房间。";
    protected override string GetLeaveButtonText() => "离开药师所";

    protected override void PopulateActions(VBoxContainer container)
    {
        bool hasRoster = Roster != null && Roster.Count > 0;

        int minorCost = FacilityPricingService.GetHealCost(Roster, 0.5f);
        bool canMinor = Economy != null && Economy.Gold >= minorCost && hasRoster;
        var btnMinor = CreateActionButton($"轻度治疗 ({minorCost}金) -- 全队生命至少恢复至50%", canMinor, hasRoster ? "金币不足" : "无队伍");
        btnMinor.Pressed += () => ApplyResult(HealingService.HealToRatio(Roster, 0.5f, minorCost, SpendGold));
        container.AddChild(btnMinor);

        int majorCost = FacilityPricingService.GetHealCost(Roster, 1.0f);
        bool canMajor = Economy != null && Economy.Gold >= majorCost && hasRoster;
        var btnMajor = CreateActionButton($"深度治疗 ({majorCost}金) -- 恢复全队100%生命值", canMajor, hasRoster ? "金币不足" : "无队伍");
        btnMajor.Pressed += () => ApplyResult(HealingService.HealToRatio(Roster, 1.0f, majorCost, SpendGold));
        container.AddChild(btnMajor);

        bool hasNegativeEffects = HealingService.CountNegativeEffects(Roster) > 0;
        int purifyCost = FacilityPricingService.GetPurifyCost(Roster);
        bool canPurify = Economy != null && Economy.Gold >= purifyCost && hasRoster && hasNegativeEffects;
        string purifyReason = !hasRoster ? "无队伍" : !hasNegativeEffects ? "没有负面状态" : "金币不足";
        var btnPurify = CreateActionButton($"净化诅咒 ({purifyCost}金) -- 移除所有负面状态", canPurify, purifyReason);
        btnPurify.Pressed += () => ApplyResult(HealingService.PurifyAll(Roster, SpendGold));
        container.AddChild(btnPurify);

        int holyWaterCost = FacilityPricingService.GetHolyWaterCost(_context?.CurrentTown?.Prosperity ?? 50);
        bool canBuy = Economy != null && Economy.Gold >= holyWaterCost;
        var btnHoly = CreateActionButton($"购买净化药水 ({holyWaterCost}金) -- 加入背包", canBuy, "金币不足");
        btnHoly.Pressed += DoBuyHolyWater;
        container.AddChild(btnHoly);
    }

    public void ShowTemple(EconomyManager economy)
    {
        _context = new PoiPanelContext { Economy = economy, PlayerParty = null, CurrentTown = null };
        ShowPanel();
    }

    public void ShowTemple(PoiPanelContext context)
    {
        _context = context;
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.TempleFinished);
        HidePanel();
    }

    private bool SpendGold(int amount) => Economy?.SpendGold(amount) == true;

    private void DoBuyHolyWater()
    {
        int cost = FacilityPricingService.GetHolyWaterCost(_context?.CurrentTown?.Prosperity ?? 50);
        if (Economy == null || !Economy.SpendGold(cost))
        {
            ApplyResult(FacilityServiceResult.Fail("金币不足，无法购买净化药水。"));
            return;
        }

        var baseItem = PrototypeData.GetConsumables().TryGetValue("holy_water", out var item)
            ? item
            : HealingService.CreateFallbackHolyWater();
        Economy.AddItem((ItemData)baseItem.Duplicate());

        ApplyResult(FacilityServiceResult.Ok("获得净化药水 x1，已放入背包。", goldSpent: cost, affectedItems: 1));
    }

    private void ApplyResult(FacilityServiceResult result)
    {
        string color = result.Success ? "green" : "red";
        SetResult($"[color={color}]{result.Message}[/color]");
        RefreshLayout();
    }
}
