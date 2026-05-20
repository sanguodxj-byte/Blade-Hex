// PoiPanelContext.cs
// POI 二级面板运行上下文。
//
// 将 EconomyManager / OverworldParty / 当前城镇等依赖收束为一个参数，避免
// 每个面板各自扩展 ShowXxx(...) 签名。
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 打开 POI 二级面板时注入的运行上下文。
/// </summary>
public sealed class PoiPanelContext
{
    public EconomyManager? Economy { get; init; }
    public OverworldParty? PlayerParty { get; init; }
    public OverworldTown? CurrentTown { get; init; }
    public RecruitService? RecruitService { get; init; }
    public QuestGenerator? QuestGenerator { get; init; }
    public QuestManager? QuestManager { get; init; }

    public PartyRoster? Roster => PlayerParty?.Roster ?? Economy?.ActiveRoster;
    public PartyInventory? Inventory => PlayerParty?.Inventory;
    public string PoiId => CurrentTown?.TownName ?? "";
    public int CurrentDay => Economy?.DaysPassed ?? 1;
    public int Prosperity => CurrentTown?.Prosperity ?? 50;
}
