// OverworldScene.Interaction.cs
// [T-602] 交互系统 partial 类 — 面板创建、信号连接、交互回调
using Godot;
using System;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.View.UI.Overworld;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene
{
    // ========================================
    // 设施类型常量 (TownFacility.FacilityType)
    // ========================================
    public const int TF_CASTLE = 0;
    public const int TF_MARKET = 1;
    public const int TF_TAVERN = 2;
    public const int TF_ARENA = 3;
    public const int TF_SMITHY = 4;
    public const int TF_TRAINING = 5;
    public const int TF_TEMPLE = 6;

    // 当前交互实体跟踪（因 InteractionManager._currentEntity 为 private）
    private Node2D? _currentInteractionEntity;

    // ========================================
    // SetupInteractionSystem — GD lines 656-730
    // ========================================

    private void SetupInteractionSystem()
    {
        // InteractionManager
        InteractionMgr = new InteractionManager();
        InteractionMgr.PlayerParty = PlayerParty;
        InteractionMgr.HexGrid = HexGrid;
        AddChild(InteractionMgr);

        // 交互面板 — InteractionPanel C#
        _interactionPanel = new InteractionPanel();
        AddChild(_interactionPanel);
        _interactionPanel.OptionSelected += OnInteractionOptionSelected;
        _interactionPanel.CloseRequested += OnInteractionClosed;

        // 对话面板 — DialoguePanel C#
        _dialoguePanel = new DialoguePanel();
        AddChild(_dialoguePanel);
        _dialoguePanel.DialogueFinished += OnDialogueFinished;

        // 交易面板 — TradePanel C#
        _tradePanel = new TradePanel();
        AddChild(_tradePanel);
        _tradePanel.TradeFinished += OnSubPanelClosed;

        // 休息面板 — RestPanel C#
        _restPanel = new RestPanel();
        AddChild(_restPanel);
        _restPanel.RestCompleted += (int hours) => OnSubPanelClosed();

        // 城镇面板 — TownPanel C#
        _townPanel = new TownPanel();
        AddChild(_townPanel);
        _townPanel.FacilitySelected += OnFacilitySelected;
        _townPanel.LeaveTown += OnSubPanelClosed;

        // 竞技场面板 — ArenaPanel C#
        _arenaPanel = new ArenaPanel();
        AddChild(_arenaPanel);
        _arenaPanel.ArenaFinished += OnSubPanelClosed;

        // 铁匠铺面板 — SmithyPanel C#
        _smithyPanel = new SmithyPanel();
        AddChild(_smithyPanel);
        _smithyPanel.SmithyFinished += OnSubPanelClosed;

        // 训练场面板 — TrainingPanel C#
        _trainingPanel = new TrainingPanel();
        AddChild(_trainingPanel);
        _trainingPanel.TrainingFinished += OnSubPanelClosed;

        // 药师所面板 — TemplePanel C#
        _templePanel = new TemplePanel();
        AddChild(_templePanel);
        _templePanel.TempleFinished += OnSubPanelClosed;

        // 委托面板 — QuestBoardPanel C#
        _questBoardPanel = new QuestBoardPanel();
        AddChild(_questBoardPanel);
        _questBoardPanel.BoardClosed += OnSubPanelClosed;
        _questBoardPanel.QuestAccepted += OnQuestBoardAccepted;

        // 招募面板 — RecruitPanel C#
        _recruitPanel = new RecruitPanel();
        AddChild(_recruitPanel);
        _recruitPanel.RecruitFinished += OnRecruitFinished;

        // ========================================
        // InteractionManager 信号连接
        // ========================================
        InteractionMgr.InteractionRequested += OnInteractionRequested;
        InteractionMgr.CombatRequested += OnCombatFromInteraction;
        InteractionMgr.DialogueRequested += OnDialogueRequested;
        InteractionMgr.TradeRequested += OnTradeRequested;
        InteractionMgr.RestRequested += OnRestRequested;
        InteractionMgr.TrainRequested += OnTrainRequested;
        InteractionMgr.HealRequested += OnHealRequested;
        InteractionMgr.ArenaRequested += OnArenaRequested;
        InteractionMgr.QuestRequested += OnQuestRequested;
        InteractionMgr.RepairRequested += OnRepairRequested;
        InteractionMgr.InteractionCompleted += OnInteractionCompleted;
    }

    // ========================================
    // 交互回调
    // ========================================

    /// <summary>交互请求 — 暂停时间并显示交互面板</summary>
    private void OnInteractionRequested(Node2D entity, Godot.Collections.Array<InteractionOption> options)
    {
        _currentInteractionEntity = entity;
        IsTimePaused = true;
        _interactionPanel.ShowForEntity(entity, (Godot.Collections.Array)options);
    }

    /// <summary>当前遭遇事件（供战斗系统读取先手/士气/地形修正，仅任务触发时有值）</summary>
    protected EncounterEvent? _currentEncounterEvent;

    /// <summary>获取当前遭遇事件（供外部系统查询机制效果）</summary>
    public EncounterEvent? GetCurrentEncounter() => _currentEncounterEvent;

    /// <summary>清除当前遭遇事件（战斗结束后调用）</summary>
    public void ClearCurrentEncounter() => _currentEncounterEvent = null;

    /// <summary>交互选项被选择 — 隐藏面板并执行选项</summary>
    private void OnInteractionOptionSelected(InteractionOption option)
    {
        _interactionPanel.HidePanel();
        // 如果选择了离开/逃跑，重置遭遇锁
        if (option.CurrentInteractionType == InteractionType.Type.Leave)
            OnEncounterInteractionClosed();
        InteractionMgr.ExecuteOption(option);
    }

    /// <summary>交互关闭 — 隐藏面板、结束交互、取消暂停</summary>
    private void OnInteractionClosed()
    {
        _interactionPanel.HidePanel();
        InteractionMgr.EndInteraction();
        IsTimePaused = false;
        // 重置遭遇锁，允许下次触发
        OnEncounterInteractionClosed();
        // 重置 POI 进入标志并设置冷却
        _poiEntered = false;
        _poiLeaveCooldown = 60;
    }

    /// <summary>交互完成 — 取消暂停</summary>
    private void OnInteractionCompleted(string result)
    {
        IsTimePaused = false;
    }

    /// <summary>从交互进入战斗 — 结束交互、取消暂停、进入战斗场景</summary>
    private void OnCombatFromInteraction(BattleContext battleContext)
    {
        InteractionMgr.EndInteraction();
        IsTimePaused = false;

        // 声望惩罚：如果攻击的是非敌对实体（友方/中立），扣声望
        if (_pendingEncounterEntity != null && !_pendingEncounterEntity.IsHostileToPlayer)
        {
            string faction = _pendingEncounterEntity.Faction;
            if (!string.IsNullOrEmpty(faction) && faction != "neutral" && faction != "hostile")
            {
                _reputationTracker.OnAttackedFaction(faction);
                GD.Print($"[Reputation] 攻击了 {faction} 的实体！声望 -10");
            }
        }

        _EnterCombatWithContext(battleContext);
    }

    /// <summary>对话请求 — 显示对话面板</summary>
    private void OnDialogueRequested(Resource npcProfile)
    {
        if (npcProfile is NPCProfile profile)
            _dialoguePanel.ShowDialogue(profile);
    }

    /// <summary>对话结束 — 结束交互、取消暂停</summary>
    private void OnDialogueFinished()
    {
        InteractionMgr.EndInteraction();
        IsTimePaused = false;
    }

    /// <summary>交易请求 — 显示交易面板</summary>
    private void OnTradeRequested(string sourceName)
    {
        _tradePanel.ShowTrade(sourceName, EconomyMgr);
    }

    /// <summary>休息请求 — 显示休息面板</summary>
    private void OnRestRequested(int facilityType)
    {
        _restPanel.ShowRest(EconomyMgr);
    }

    /// <summary>训练请求 — 显示训练面板</summary>
    private void OnTrainRequested()
    {
        _trainingPanel.ShowTraining(EconomyMgr);
    }

    /// <summary>治疗请求 — 显示药师所面板</summary>
    private void OnHealRequested()
    {
        _templePanel.ShowTemple(EconomyMgr);
    }

    /// <summary>竞技场请求 — 显示竞技场面板</summary>
    private void OnArenaRequested()
    {
        _arenaPanel.ShowArena(EconomyMgr);
    }

    /// <summary>委托请求 — 显示委托面板</summary>
    private void OnQuestRequested()
    {
        ShowQuestBoard();
    }

    /// <summary>修理请求 — 显示铁匠铺面板</summary>
    private void OnRepairRequested()
    {
        _smithyPanel.ShowSmithy(EconomyMgr);
    }

    /// <summary>招募完成 — 关闭子面板</summary>
    private void OnRecruitFinished(bool hired)
    {
        OnSubPanelClosed();
    }

    /// <summary>设施选择 — 路由到对应子面板 (GD lines 791-808)</summary>
    private void OnFacilitySelected(int facilityType)
    {
        _townPanel.HidePanel();

        switch (facilityType)
        {
            case TF_MARKET:
                _tradePanel.ShowTrade(CurrentTownName(), EconomyMgr);
                break;
            case TF_TAVERN:
                // 酒馆：招募 + 委托板（布告栏在酒馆里）
                ShowTavernWithQuestBoard();
                break;
            case TF_TEMPLE:
                _templePanel.ShowTemple(EconomyMgr);
                break;
            case TF_ARENA:
                _arenaPanel.ShowArena(EconomyMgr);
                break;
            case TF_SMITHY:
                _smithyPanel.ShowSmithy(EconomyMgr);
                break;
            case TF_TRAINING:
                _trainingPanel.ShowTraining(EconomyMgr);
                break;
            case TF_CASTLE:
                // 领主厅：声望/封地/政治（暂用委托板占位）
                ShowLordHall();
                break;
        }
    }

    // ========================================
    // 酒馆（招募 + 委托板）
    // ========================================

    private RecruitService? _recruitService;

    /// <summary>打开酒馆：先显示委托板，招募面板通过委托板内按钮访问</summary>
    private void ShowTavernWithQuestBoard()
    {
        // 酒馆的布告栏 = 委托板
        ShowQuestBoard();
    }

    /// <summary>打开酒馆招募面板</summary>
    private void ShowTavernRecruit()
    {
        if (_recruitService == null)
        {
            _recruitService = new RecruitService();
            _recruitService.Initialize(WorldPois, _worldNations, GetWorldSeed());
        }

        // 声望影响：获取当前城镇所属势力的招募倍率
        string poiId = CurrentTownName();
        string? faction = GetCurrentPoiFaction();
        float recruitMult = 1.0f;
        if (!string.IsNullOrEmpty(faction))
            recruitMult = _reputationTracker.GetRecruitMultiplier(faction);

        int currentDay = EconomyMgr != null ? EconomyMgr.DaysPassed : 1;

        // 如果声望太低，招募池为空
        if (recruitMult <= 0f)
        {
            GD.Print($"[Reputation] 声望过低，{poiId} 无人愿意为你效力");
        }

        if (EconomyMgr == null) return;
        _recruitPanel.ShowRecruitList(_recruitService, poiId, EconomyMgr, PlayerParty, currentDay);
    }

    /// <summary>领主厅：声望/封地/政治（暂时显示招募面板作为占位）</summary>
    private void ShowLordHall()
    {
        // 领主厅面板（声望查看、封地管理、政治任务）— 需要独立 UI 设计后实现
        // 暂时复用招募面板
        ShowTavernRecruit();
    }

    /// <summary>获取当前交互 POI 的所属势力</summary>
    private string? GetCurrentPoiFaction()
    {
        if (_currentInteractionEntity is OverworldTown town)
            return string.IsNullOrEmpty(town.Faction) ? null : town.Faction;
        return null;
    }

    // ========================================
    // 任务板
    // ========================================

    /// <summary>打开动态任务板</summary>
    private void ShowQuestBoard()
    {
        if (_questGenerator == null)
        {
            _questGenerator = new QuestGenerator();
            _questGenerator.Initialize(WorldPois, GetWorldSeed());
        }

        string poiId = CurrentTownName();
        int currentDay = EconomyMgr != null ? EconomyMgr.DaysPassed : 1;

        // 直接传递 quest generator 给面板（不再设置 _dynamic_quests 等）
        _questBoardPanel.ShowBoardDynamic(_questGenerator, poiId, currentDay);
    }

    /// <summary>任务板接取委托回调</summary>
    private void OnQuestBoardAccepted(string questId)
    {
        if (_questGenerator == null || QuestMgr == null) return;

        // 从 generator 的当前池中找到该 quest 并接取
        string poiId = CurrentTownName();
        int currentDay = EconomyMgr != null ? EconomyMgr.DaysPassed : 1;
        var quests = _questGenerator.GetAvailableQuests(poiId, currentDay);

        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i].QuestId == questId)
            {
                var quest = _questGenerator.AcceptQuest(poiId, i, currentDay);
                if (quest != null)
                    AcceptQuestDirect(quest);
                break;
            }
        }

        OnSubPanelClosed();
    }

    private int GetWorldSeed()
    {
        var gs = GetNodeOrNull<BladeHex.Data.GlobalState>("/root/GlobalState");
        return gs?.WorldSeed ?? 42;
    }

    // ========================================
    // 辅助方法
    // ========================================

    /// <summary>获取当前交互城镇名称 (GD lines 810-813)</summary>
    private string CurrentTownName()
    {
        if (_currentInteractionEntity is OverworldTown town)
        {
            return town.TownName;
        }
        return "商店";
    }

    /// <summary>子面板关闭 (GD lines 815-820)</summary>
    private void OnSubPanelClosed()
    {
        // 如果城镇面板还在显示，不恢复
        if (_townPanel.IsPanelVisible())
            return;

        InteractionMgr.EndInteraction();
        IsTimePaused = false;

        // 重置 POI 进入标志并设置冷却（防止立即重新触发）
        _poiEntered = false;
        _poiLeaveCooldown = 60; // 约 1 秒冷却（60 帧）
    }

    /// <summary>进入战斗场景（由 OnCombatFromInteraction 调用）</summary>
    private void _EnterCombatWithContext(BattleContext ctx)
    {
        GD.Print($"[OverworldScene] 战斗上下文: {ctx.GetDescription()}");
        StopPlayer();

        // 从当前交互实体生成敌方单位
        if (_pendingEncounterEntity != null)
        {
            _pendingEncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(_pendingEncounterEntity);
        }
        else if (_currentInteractionEntity is OverworldEnemy enemy && enemy.EntityRef != null)
        {
            _pendingEncounterEntity = enemy.EntityRef;
            _pendingEncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(enemy.EntityRef);
        }

        EnterCombatSceneFromCs(ctx);
    }
}
