// OverworldScene3D.Interaction.cs
// 交互系统 — 修复所有 POI/实体交互问题
using Godot;
using BladeHex.Strategic;
using BladeHex.View.UI.Overworld;
using OverworldUI = BladeHex.View.UI.Overworld.OverworldUI;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 交互系统
    // ========================================

    private InteractionManager? _interactionMgr;
    private InteractionPanel? _interactionPanel;
    private TownPanel? _townPanel;
    private PoiSecondaryPanelRouter? _secondaryPanelRouter;

    /// <summary>当前交互的 town 节点（用于防止重复创建 + 清理）</summary>
    private OverworldTown? _currentTownNode;

    /// <summary>初始化交互系统</summary>
    private void SetupInteractionSystem()
    {
        _interactionMgr = new InteractionManager();
        _interactionMgr.PlayerParty = PlayerParty;
        _interactionMgr.HexGrid = _grid;
        AddChild(_interactionMgr);

        // 交互面板（用于非城镇遭遇：敌人/NPC）
        _interactionPanel = new InteractionPanel();
        AddChild(_interactionPanel);
        _interactionPanel.OptionSelected += OnInteractionOptionSelected;
        _interactionPanel.CloseRequested += OnInteractionClosed;

        // 城镇面板
        _townPanel = new TownPanel();
        AddChild(_townPanel);
        _townPanel.LeaveTown += OnLeaveTown;
        _townPanel.FacilitySelected += OnFacilitySelected;

        _secondaryPanelRouter = new PoiSecondaryPanelRouter(
            this,
            () => _townPanel,
            () => _currentTownNode,
            () => EconomyMgr,
            () => PlayerParty,
            () => _recruitService,
            () => _questGenerator,
            () => _questManager,
            () => _overworldUi,
            OnArenaCombatRequested,
            CleanupInteraction);

        // 信号连接 — 所有 InteractionManager 信号
        _interactionMgr.InteractionRequested += OnInteractionRequested;
        _interactionMgr.CombatRequested += OnCombatFromInteraction;
        _interactionMgr.InteractionCompleted += OnInteractionCompleted;
        _interactionMgr.TownEntered += OnTownEntered;
        _interactionMgr.TradeRequested += OnTradeRequested;
        _interactionMgr.RestRequested += OnRestRequested;
        _interactionMgr.TrainRequested += OnTrainRequested;
        _interactionMgr.HealRequested += OnHealRequested;
        _interactionMgr.ArenaRequested += OnArenaRequested;
        _interactionMgr.QuestRequested += OnQuestRequested;
        _interactionMgr.RepairRequested += OnRepairRequested;

        GD.Print("[OverworldScene3D] 交互系统已初始化");
    }

    /// <summary>触发 POI 交互 — 城镇类直接打开 TownPanel，跳过 InteractionPanel</summary>
    private void TriggerPOIInteraction(OverworldPOI poi)
    {
        if (_interactionMgr == null) return;

        IsTimePaused = true;
        _playerMoving = false;
        if (_camera != null) _camera.ExternalControl = true;

        // 清理上一个 town 节点
        CleanupCurrentTownNode();

        // 创建仅用于交互 UI 的临时 OverworldTown。
        var town = PoiTownAdapter.CreateTownNode(poi);

        // 加入场景树
        AddChild(town);
        _currentTownNode = town;

        // 城镇/村庄类 POI → 直接打开 TownPanel（不经过 InteractionPanel）
        if (PoiTownAdapter.OpensTownPanelDirectly(poi.PoiTypeEnum))
        {
            GD.Print($"[Interaction] 打开 TownPanel: {town.TownName}, 设施数={town.Facilities.Count}");
            _townPanel?.ShowTown(town);
        }
        else
        {
            // 其他类型 → 通过 InteractionManager 走标准流程
            GD.Print($"[Interaction] 走 InteractionManager 流程: {poi.PoiName}");
            _interactionMgr.TriggerInteraction(town);
        }
    }

    // ========================================
    // InteractionPanel 回调（非城镇遭遇）
    // ========================================

    private void OnInteractionRequested(Node2D entity, Godot.Collections.Array<InteractionOption> options)
    {
        var arr = new Godot.Collections.Array();
        foreach (var opt in options) arr.Add(opt);
        _interactionPanel?.ShowForEntity(entity, arr);
    }

    private void OnInteractionOptionSelected(InteractionOption option)
    {
        if (option.CurrentInteractionType == InteractionType.Type.Leave)
            OnInteractionClosed();
        else
            _interactionMgr?.ExecuteOption(option);
    }

    private void OnInteractionClosed()
    {
        _interactionPanel?.HidePanel();
        _interactionMgr?.EndInteraction();
        CleanupInteraction();
    }

    // ========================================
    // TownPanel 回调
    // ========================================

    private void OnTownEntered(OverworldTown town)
    {
        _townPanel?.ShowTown(town);
    }

    private void OnLeaveTown()
    {
        GD.Print("[Interaction] OnLeaveTown 信号收到");
        _townPanel?.HidePanel();
        _interactionMgr?.EndInteraction();
        CleanupInteraction();
    }

    private void OnFacilitySelected(int facilityType)
    {
        GD.Print($"[Interaction] OnFacilitySelected: type={facilityType} ({(TownFacility.FacilityType)facilityType})");
        _secondaryPanelRouter?.Open((TownFacility.FacilityType)facilityType);
    }

    // ========================================
    // 设施信号回调（InteractionManager 发出）
    // ========================================

    private void OnTradeRequested(string sourceName)
    {
        GD.Print($"[OverworldScene3D] 交易请求: {sourceName}");
        // TODO: 打开交易面板
        CleanupInteraction();
    }

    private void OnRestRequested(int facilityType)
    {
        GD.Print($"[OverworldScene3D] 休息请求: type={facilityType}");
        // TODO: 打开休息面板
        CleanupInteraction();
    }

    private void OnTrainRequested()
    {
        GD.Print("[OverworldScene3D] 训练请求");
        CleanupInteraction();
    }

    private void OnHealRequested()
    {
        GD.Print("[OverworldScene3D] 治疗请求");
        CleanupInteraction();
    }

    private void OnArenaRequested()
    {
        GD.Print("[OverworldScene3D] 竞技场请求");
        CleanupInteraction();
    }

    private void OnQuestRequested()
    {
        GD.Print("[OverworldScene3D] 委托请求");
        CleanupInteraction();
    }

    private void OnRepairRequested()
    {
        GD.Print("[OverworldScene3D] 修理请求");
        CleanupInteraction();
    }

    // ========================================
    // 战斗
    // ========================================

    private void OnCombatFromInteraction(BattleContext ctx)
    {
        _interactionPanel?.HidePanel();

        // 从 InteractionManager 的当前交互实体中提取 OverworldEntity
        var interactNode = _interactionMgr?.GetCurrentEntity();
        if (interactNode is BladeHex.Strategic.OverworldEnemy oe && oe.EntityRef != null)
            _lastEncounteredEntity = oe.EntityRef;

        _interactionMgr?.EndInteraction();
        CleanupInteraction();
        EnterCombatScene(ctx);
    }

    private int _pendingArenaPrize = 0;

    private void OnArenaCombatRequested(BattleContext ctx, int prize)
    {
        _pendingArenaPrize = prize;
        EnterCombatScene(ctx);
    }

    private void OnInteractionCompleted(string result)
    {
        CleanupInteraction();
    }

    // ========================================
    // 清理
    // ========================================

    /// <summary>统一清理交互状态</summary>
    private void CleanupInteraction()
    {
        IsTimePaused = false;
        _poiEntered = false;
        _encounterActive = false;
        if (_camera != null) _camera.ExternalControl = false;

        // 清理临时 town 节点
        CleanupCurrentTownNode();
    }

    private void CleanupCurrentTownNode()
    {
        if (_currentTownNode != null && GodotObject.IsInstanceValid(_currentTownNode))
            _currentTownNode.QueueFree();

        _currentTownNode = null;
    }
}
