// PoiSecondaryPanelRouter.cs
// POI 二级面板路由 module。
//
// 负责从城镇设施选择进入具体二级面板，并维护二级面板关闭后回到 TownPanel 的
// 生命周期。OverworldScene3D 只保留“交互会话状态”，不再直接持有每个二级面板。
using Godot;
using System;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Facilities;
using BladeHex.View.UI.Overworld;

namespace BladeHex.Scenes.Overworld;

/// <summary>
/// 城镇/POI 二级面板路由器。
/// </summary>
internal sealed class PoiSecondaryPanelRouter
{
    private readonly Node _panelParent;
    private readonly Func<TownPanel?> _townPanel;
    private readonly Func<OverworldTown?> _currentTown;
    private readonly Func<EconomyManager?> _economy;
    private readonly Func<OverworldParty?> _playerParty;
    private readonly Func<RecruitService?> _recruitService;
    private readonly Func<QuestGenerator?> _questGenerator;
    private readonly Func<QuestManager?> _questManager;
    private readonly Func<OverworldUI?> _overworldUi;
    private readonly Action<BattleContext, int> _arenaCombatRequested;
    private readonly Action _cleanupInteraction;

    private RestPanel? _restPanel;
    private RecruitPanel? _recruitPanel;
    private SmithyPanel? _smithyPanel;
    private TemplePanel? _templePanel;
    private ArenaPanel? _arenaPanel;
    private QuestBoardPanel? _questBoardPanel;

    public PoiSecondaryPanelRouter(
        Node panelParent,
        Func<TownPanel?> townPanel,
        Func<OverworldTown?> currentTown,
        Func<EconomyManager?> economy,
        Func<OverworldParty?> playerParty,
        Func<RecruitService?> recruitService,
        Func<QuestGenerator?> questGenerator,
        Func<QuestManager?> questManager,
        Func<OverworldUI?> overworldUi,
        Action<BattleContext, int> arenaCombatRequested,
        Action cleanupInteraction)
    {
        _panelParent = panelParent;
        _townPanel = townPanel;
        _currentTown = currentTown;
        _economy = economy;
        _playerParty = playerParty;
        _recruitService = recruitService;
        _questGenerator = questGenerator;
        _questManager = questManager;
        _overworldUi = overworldUi;
        _arenaCombatRequested = arenaCombatRequested;
        _cleanupInteraction = cleanupInteraction;
    }

    /// <summary>
    /// ESC 关闭当前二级面板。返回 true 表示已处理。
    /// </summary>
    public bool TryCloseActivePanel()
    {
        if (TryClosePanel(_smithyPanel)) return true;
        if (TryClosePanel(_templePanel)) return true;
        if (TryClosePanel(_arenaPanel)) return true;
        if (TryClosePanel(_restPanel)) return true;
        if (TryClosePanel(_recruitPanel)) return true;
        if (TryClosePanel(_questBoardPanel)) return true;

        return false;
    }

    private bool TryClosePanel(POIPanelBase? panel)
    {
        if (panel?.IsPanelVisible() != true) return false;

        panel.HidePanel();
        ReturnToTownPanel();
        return true;
    }

    /// <summary>打开设施对应的二级面板。</summary>
    public void Open(TownFacility.FacilityType facilityType)
    {
        _townPanel()?.HidePanelImmediate();

        switch (facilityType)
        {
            case TownFacility.FacilityType.Market:
                OpenTradePanel();
                break;
            case TownFacility.FacilityType.Tavern:
                OpenRecruitPanel();
                break;
            case TownFacility.FacilityType.Smithy:
                OpenSmithyPanel();
                break;
            case TownFacility.FacilityType.Temple:
                OpenTemplePanel();
                break;
            case TownFacility.FacilityType.Arena:
                OpenArenaPanel();
                break;
            case TownFacility.FacilityType.Castle:
            case TownFacility.FacilityType.QuestBoard:
                OpenQuestPanel();
                break;
            case TownFacility.FacilityType.Rest:
                OpenRestPanel();
                break;
            case TownFacility.FacilityType.Port:
                OpenPortPanel();
                break;
            default:
                ReturnToTownPanel();
                break;
        }
    }

    private void OpenTradePanel()
    {
        var party = _playerParty();
        var economy = _economy();
        var ui = _overworldUi();
        if (party?.Roster == null || economy == null || ui == null)
        {
            ReturnToTownPanel();
            return;
        }

        string shopName = _currentTown()?.TownName ?? "商店";
        int prosperity = _currentTown()?.Prosperity ?? 50;
        ui.OpenPartyShop(shopName, economy, MarketStockService.GenerateStock(prosperity), prosperity);
    }

    private PoiPanelContext CreatePanelContext() => new()
    {
        Economy = _economy(),
        PlayerParty = _playerParty(),
        CurrentTown = _currentTown(),
        RecruitService = _recruitService(),
        QuestGenerator = _questGenerator(),
        QuestManager = _questManager(),
    };

    private void OpenRecruitPanel()
    {
        _recruitPanel ??= AddPanel(new RecruitPanel(), panel =>
        {
            panel.RecruitFinished += _ => ReturnToTownPanel();
        });

        _recruitPanel.ShowRecruitList(CreatePanelContext());
    }

    private void OpenSmithyPanel()
    {
        _smithyPanel ??= AddPanel(new SmithyPanel(), panel =>
        {
            panel.SmithyFinished += ReturnToTownPanel;
        });

        _smithyPanel.ShowSmithy(CreatePanelContext());
    }

    private void OpenTemplePanel()
    {
        _templePanel ??= AddPanel(new TemplePanel(), panel =>
        {
            panel.TempleFinished += ReturnToTownPanel;
        });

        _templePanel.ShowTemple(CreatePanelContext());
    }

    private void OpenArenaPanel()
    {
        _arenaPanel ??= AddPanel(new ArenaPanel(), panel =>
        {
            panel.ArenaFinished += ReturnToTownPanel;
            panel.ArenaCombatRequested += OnArenaCombatRequested;
        });

        var economy = _economy();
        if (economy != null) _arenaPanel.ShowArena(economy);
        else _arenaPanel.ShowPanel();
    }

    private void OnArenaCombatRequested(BattleContext ctx, int prize)
    {
        _arenaPanel?.HidePanel();
        _arenaCombatRequested(ctx, prize);
    }

    private void OpenQuestPanel()
    {
        _questBoardPanel ??= AddPanel(new QuestBoardPanel(), panel =>
        {
            panel.BoardClosed += ReturnToTownPanel;
        });

        _questBoardPanel.ShowBoardDynamic(CreatePanelContext());
    }

    private void OpenRestPanel()
    {
        _restPanel ??= AddPanel(new RestPanel(), panel =>
        {
            panel.RestCompleted += _ => ReturnToTownPanel();
            panel.RestFinished += ReturnToTownPanel;
        });

        _restPanel.ShowRest(CreatePanelContext());
    }

    private void OpenPortPanel()
    {
        var economy = _economy();
        var party = _playerParty();
        if (economy == null || party == null)
        {
            GD.Print("[Port] 无法租船：缺少经济系统或玩家队伍");
            ReturnToTownPanel();
            return;
        }

        var result = PortService.RentShip(
            _currentTown()?.Prosperity ?? 50,
            economy.SpendGold,
            ship => party.CurrentShip = ship,
            atSea => party.IsAtSea = atSea);

        GD.Print($"[Port] {result.Message}");
        ReturnToTownPanel();
    }

    private T AddPanel<T>(T panel, Action<T>? configure = null) where T : CanvasLayer
    {
        _panelParent.AddChild(panel);
        configure?.Invoke(panel);
        return panel;
    }

    private void ReturnToTownPanel()
    {
        var town = _currentTown();
        var panel = _townPanel();
        if (town != null && GodotObject.IsInstanceValid(town) && panel != null)
            panel.ShowTown(town);
        else
            _cleanupInteraction();
    }
}
