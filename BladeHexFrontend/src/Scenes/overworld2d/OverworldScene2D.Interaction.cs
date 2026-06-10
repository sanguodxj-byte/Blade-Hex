// OverworldScene2D.Interaction.cs
// 交互系统 — 从 OverworldScene3D.Interaction.cs 迁移
using Godot;
using System;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.Facilities;
using BladeHex.Strategic.Tournament;
using BladeHex.Scenes.Overworld;
using BladeHex.View.UI.Overworld;
using BladeHex.View.Strategic;
using BladeHex.Data;
using OverworldUI = BladeHex.View.UI.Overworld.OverworldUI;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 交互系统
    // ========================================

    /// <summary>是否已进入 POI</summary>
    private bool _poiEntered = false;

    /// <summary>是否处于面板切换中，用以防止状态清理发生闪烁跳变</summary>
    private bool _isPanelTransiting = false;

    private InteractionManager? _interactionMgr;
    private InteractionPanel? _interactionPanel;
    private DialoguePanel? _dialoguePanel;
    private TownPanel? _townPanel;
    private PoiSecondaryPanelRouter? _secondaryPanelRouter;
    private TournamentService? _tournamentService;

    /// <summary>当前交互的 town 节点（用于防止重复创建 + 清理）</summary>
    private OverworldTown? _currentTownNode;

    /// <summary>待触发的走私伏击战队实体</summary>
    public OverworldEntity? PendingSmuggleAmbushEntity { get; set; }

    /// <summary>当前交互的临时敌对/NPC节点（用于防止重复创建 + 清理）</summary>
    private OverworldEnemy? _currentEnemyNode;

    /// <summary>初始化交互系统</summary>
    private void InitInteraction()
    {
        SetupInteractionSystem();
    }

    /// <summary>初始化交互系统</summary>
    private void SetupInteractionSystem()
    {
        _interactionMgr = new InteractionManager();
        _interactionMgr.PlayerParty = PlayerParty;
        _interactionMgr.HexGrid = _grid;
        _interactionMgr.MapAccess = _mapAccess;
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

        // 锦标赛服务
        if (EntityMgr?.Heroes != null && ReputationTracker != null)
        {
            _tournamentService = new TournamentService(
                EntityMgr.Heroes,
                ReputationTracker,
                EntityMgr.WorldEngine?.Influence ?? new BladeHex.Strategic.InfluenceTracker(),
                EntityMgr.WorldEngine);
        }

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
            () => _tournamentService,
            () => EntityMgr,
            () => ReputationTracker,
            OnArenaCombatRequested,
            OnTournamentCombatRequested,
            CleanupInteraction);

        // 信号连接 — 所有 InteractionManager 信号
        _interactionMgr.InteractionRequested += OnInteractionRequested;
        _interactionMgr.CombatRequested += OnCombatFromInteraction;
        _interactionMgr.DialogueRequested += OnDialogueRequested;
        _interactionMgr.InteractionCompleted += OnInteractionCompleted;
        _interactionMgr.TownEntered += OnTownEntered;
        _interactionMgr.TradeRequested += OnTradeRequested;
        _interactionMgr.RestRequested += OnRestRequested;
        _interactionMgr.TrainRequested += OnTrainRequested;
        _interactionMgr.HealRequested += OnHealRequested;
        _interactionMgr.ArenaRequested += OnArenaRequested;
        _interactionMgr.QuestRequested += OnQuestRequested;
        _interactionMgr.RepairRequested += OnRepairRequested;

        GD.Print("[OverworldScene2D] 交互系统已初始化");
    }

    /// <summary>触发 POI 交互 — 城镇类直接打开 TownPanel，跳过 InteractionPanel</summary>
    private void TriggerPOIInteraction(OverworldPOI poi)
    {
        if (_interactionMgr == null) return;

        IsTimePaused = true;
        _playerMoving = false;
        if (_camera != null) _camera.PushInputBlock();

        // 激活 POI 发现日志
        if (EntityMgr != null && EntityMgr.Journal != null)
        {
            if (EntityMgr.Journal.DiscoverPoi(poi.PoiName))
            {
                GD.Print($"[Encyclopedia] 发现新 POI，已加入日志: {poi.PoiName}");
                _toast?.Show($"🗺 发现了新地点: {poi.PoiName}");
                BladeHex.UI.Tutorial.TutorialManager.Instance?.Trigger("discover_poi");
            }
        }

        // 如果 POI 被围攻，触发攻城战教程
        if (poi.IsUnderSiege)
        {
            BladeHex.UI.Tutorial.TutorialManager.Instance?.Trigger("siege_started");
        }

        // 清理上一个 town 节点
        CleanupCurrentTownNode();

        // 创建仅用于交互 UI 的临时 OverworldTown
        var town = PoiTownAdapter.CreateTownNode(poi);

        // 加入场景树
        AddChild(town);
        _currentTownNode = town;

        // 城镇/村庄类 POI → 直接打开 TownPanel
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
        // 普通主动离开：只关闭面板；被追击时的“尝试突围”使用独立结算。
        if (option.CurrentInteractionType == InteractionType.Type.Leave)
        {
            if (option.Id == "breakout")
                ResolveBreakoutAttempt();
            else
                OnInteractionClosed();
            return;
        }

        // 大地图实体战斗应保留 EntityRef，以便 BattleContext 使用实体部署/模板。
        if (option.CurrentInteractionType == InteractionType.Type.Attack
            && _interactionMgr?.GetCurrentEntity() is OverworldEnemy enemy
            && enemy.EntityRef != null)
        {
            _lastEncounteredEntity = enemy.EntityRef;
            _interactionPanel?.HidePanelImmediate();
            _interactionMgr.EndInteraction();
            CleanupCurrentEnemyNode();
            IsTimePaused = false;
            _encounterActive = true;
            if (_camera != null) _camera.PopInputBlock();
            TriggerCombatWithEntity(enemy.EntityRef);
            return;
        }

        _interactionMgr?.ExecuteOption(option);
    }

    private void ResolveBreakoutAttempt()
    {
        if (_interactionMgr?.GetCurrentEntity() is not OverworldEnemy enemy || enemy.EntityRef == null)
        {
            OnInteractionClosed();
            return;
        }

        var entity = enemy.EntityRef;
        float playerSpeed = GetPlayerEffectiveSpeed();
        float enemySpeed = GetEntityEffectiveSpeed(entity);
        float fleeChance = CalculateFleeChance(playerSpeed, enemySpeed);
        float roll = (float)GD.Randf();

        _interactionPanel?.HidePanelImmediate();
        _interactionMgr.EndInteraction();
        CleanupCurrentEnemyNode();

        if (roll < fleeChance)
        {
            _toast?.Show($"🏇 突围成功，甩开了 {entity.EntityName}！（成功率 {fleeChance:P0}）");
            MarkEntityLostPlayer(entity);
            _chasingEntity = null;
            _fleeAttempted = false;
            _encounterActive = false;
            IsTimePaused = false;
            if (_camera != null) _camera.PopInputBlock();
            return;
        }

        _toast?.Show($"⚔ 突围失败，{entity.EntityName} 迫使你迎战！（成功率 {fleeChance:P0}）");
        _lastEncounteredEntity = entity;
        _chasingEntity = null;
        _fleeAttempted = false;
        IsTimePaused = false;
        _encounterActive = true;
        if (_camera != null) _camera.PopInputBlock();
        TriggerCombatWithEntity(entity);
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
        GD.Print($"[OverworldScene2D] 交易请求: {sourceName}");
        BladeHex.UI.Tutorial.TutorialManager.Instance?.Trigger("first_trade");

        var interactNode = _interactionMgr?.GetCurrentEntity();
        if (_overworldUi == null || EconomyMgr == null || PlayerParty == null)
        {
            CleanupInteraction();
            return;
        }

        int prosperity = 45;
        string shopName = string.IsNullOrEmpty(sourceName) ? "旅商" : sourceName;

        if (interactNode is OverworldEnemy enemy)
        {
            shopName = enemy.GetDisplayName();
            if (enemy.EntityRef != null)
                prosperity = ResolveEntityTradeProsperity(enemy.EntityRef);
        }

        _isPanelTransiting = true;
        _interactionPanel?.HidePanelImmediate(); // 立即隐藏，防止遮罩重叠淡出闪烁
        _dialoguePanel?.HidePanel(); // 同步隐藏交谈面板
        _interactionMgr?.EndInteraction();
        
        // 关键：不改变 IsTimePaused 与相机输入阻断，进入商店时应维持大地图静止
        CleanupCurrentEnemyNode();

        _overworldUi.OpenPartyShop(
            shopName,
            EconomyMgr,
            MarketStockService.GenerateStock(prosperity),
            prosperity,
            poi: null,
            reputation: ReputationTracker,
            worldEngine: EntityMgr?.WorldEngine,
            overworldScene: this);
            
        _isPanelTransiting = false;
    }

    private static int ResolveEntityTradeProsperity(OverworldEntity entity)
    {
        int prosperity = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.Caravan => 60,
            OverworldEntity.EntityType.Adventurer => 45,
            OverworldEntity.EntityType.LordArmy => 70,
            _ => 35,
        };

        return Mathf.Clamp(prosperity + entity.TradeGoods / 10, 20, 90);
    }

    private void OnDialogueRequested(Resource npcProfile)
    {
        if (npcProfile is not NPCProfile profile)
        {
            CleanupInteraction();
            return;
        }

        _isPanelTransiting = true;
        _interactionPanel?.HidePanelImmediate(); // 立即隐藏，防止两个面板遮罩淡入淡出叠加闪烁
        _dialoguePanel ??= CreateDialoguePanel();
        _dialoguePanel.ShowDialogue(profile, this, instantOverlay: true);
        _isPanelTransiting = false;
    }

    private DialoguePanel CreateDialoguePanel()
    {
        var panel = new DialoguePanel();
        AddChild(panel);
        
        // 绑定信号
        panel.DialogueFinished += () =>
        {
            _interactionMgr?.EndInteraction();
            CleanupInteraction();
        };
        panel.TradeRequestedFromDialogue += (npcName) =>
        {
            OnTradeRequested(npcName);
        };
        panel.CombatRequestedFromDialogue += () =>
        {
            OnCombatFromDialogue();
        };
        panel.RecruitSuccessFromDialogue += () =>
        {
            OnRecruitSuccessFromDialogue();
        };
        panel.SurrenderFromDialogue += () =>
        {
            OnSurrenderFromDialogue();
        };
        
        return panel;
    }

    private void OnCombatFromDialogue()
    {
        _isPanelTransiting = true;
        var interactNode = _interactionMgr?.GetCurrentEntity();
        if (interactNode is OverworldEnemy enemy && _grid != null)
        {
            var ctx = enemy.EntityRef != null
                ? BattleContextFactory.CreatePlayerInitiatedEntityBattle(
                    defender: enemy.EntityRef,
                    grid: _grid,
                    playerPixelPosition: _playerPixelPos,
                    nearbyEntities: EntityMgr?.Entities ?? Enumerable.Empty<OverworldEntity>(),
                    engine: EntityMgr?.WorldEngine,
                    relationMatrix: EntityMgr?.Relations,
                    playerFaction: GetCurrentPlayerFaction(),
                    seed: (int)GD.Randi())
                : BattleContext.Create(
                    _mapAccess.GetActiveTileAtPixel(enemy.Position)?.Terrain ?? Map.HexOverworldTile.TerrainType.Plains,
                    BattleContext.BattleSize.Mercenary,
                    BattleContext.EngagementType.Normal,
                    (int)GD.Randi());
            ApplyBattleTerrainFromMapAccess(ctx, enemy.Position);
            ctx.EncounterPosition = new Vector2I((int)enemy.Position.X, (int)enemy.Position.Y);

            GD.Print($"[Dialogue] 触发对话战斗, 地形={ctx.Terrain}");

            _dialoguePanel?.HidePanel();
            
            if (enemy.EntityRef != null)
                _lastEncounteredEntity = enemy.EntityRef;

            _interactionMgr?.EndInteraction();
            CleanupCurrentEnemyNode();
            IsTimePaused = false;
            _encounterActive = true;
            if (_camera != null) _camera.PopInputBlock();
            
            EnterCombatScene(ctx);
            _isPanelTransiting = false;
        }
        else
        {
            GD.PrintErr("[Dialogue] HexGrid 为 null 或非 OverworldEnemy，无法创建战斗");
            _interactionMgr?.EndInteraction();
            _isPanelTransiting = false;
            CleanupInteraction();
        }
    }

    private void OnRecruitSuccessFromDialogue()
    {
        var interactNode = _interactionMgr?.GetCurrentEntity();
        if (interactNode is OverworldEnemy enemy && enemy.NpcProfile != null)
        {
            // 在玩家 Roster 中新增一个与该 NPC 对应的佣兵
            var race = RaceData.GetRaceById(RaceData.Race.Human);
            var newCompanion = CharacterGenerator.GenerateCharacter(race, level: 1, seedVal: -1);
            newCompanion.UnitName = enemy.NpcProfile.npcName;
            
            PartyRoster.SetCurrentHp(newCompanion, newCompanion.BaseMaxHp);
            PlayerParty?.Roster.Add(newCompanion);

            GD.Print($"[Dialogue] 招募成功: {enemy.NpcProfile.npcName} 加入队伍");
            _toast?.Show($"🤝 招募成功: {enemy.NpcProfile.npcName} 加入了您的队伍！");

            // 招募成功后销毁大地图上的对应实体，避免无限互动
            enemy.QueueFree();
        }

        _interactionMgr?.EndInteraction();
        CleanupInteraction();
    }

    private void OnSurrenderFromDialogue()
    {
        var interactNode = _interactionMgr?.GetCurrentEntity();

        // 1. 扣除 80% 的玩家持有金币，搜刮清空所有行军物资
        if (EconomyMgr != null)
        {
            int penaltyGold = (int)(EconomyMgr.Gold * 0.8f);
            EconomyMgr.SpendGold(penaltyGold);

            EconomyMgr.Food = 0f;
            EconomyMgr.Tools = 0f;
            EconomyMgr.Medicine = 0f;

            EconomyMgr.EmitSignal(BladeHex.Data.EconomyManager.SignalName.ResourcesChanged);
            GD.Print($"[Dialogue] 投降惩罚: 失去 {penaltyGold} 金币，且口粮、工具、药品被洗劫一空！");
        }

        _toast?.Show("💸 你投降了！强盗夺走了你 80% 金币及所有行军口粮与物资！", new Color(0.9f, 0.3f, 0.3f));

        // 3. 将玩家部队传送回初始大地图安全点并修正镜头
        Vector2 startPos = GetPlayerStartPosition();
        if (PlayerParty != null)
        {
            PlayerParty.Position = startPos;
            _playerPixelPos = startPos;
            PlayerParty.StopNavAgent(); // 停止当前自动移动，防止回折
            StopPlayerMovementForEncounter();
        }
        ForceCameraToPlayer();

        // 4. 销毁大地图的强盗实体，防止卡死原地遭遇循环
        if (interactNode != null && GodotObject.IsInstanceValid(interactNode))
        {
            interactNode.QueueFree();
        }

        _interactionMgr?.EndInteraction();
        CleanupInteraction();
    }

    private void OnRestRequested(int facilityType)
    {
        GD.Print($"[OverworldScene2D] 休息请求: type={facilityType}");
        CleanupInteraction();
    }

    private void OnTrainRequested()
    {
        GD.Print("[OverworldScene2D] 训练请求");
        CleanupInteraction();
    }

    private void OnHealRequested()
    {
        GD.Print("[OverworldScene2D] 治疗请求");
        CleanupInteraction();
    }

    private void OnArenaRequested()
    {
        GD.Print("[OverworldScene2D] 竞技场请求");
        CleanupInteraction();
    }

    private void OnQuestRequested()
    {
        GD.Print("[OverworldScene2D] 委托请求");
        CleanupInteraction();
    }

    private void OnRepairRequested()
    {
        GD.Print("[OverworldScene2D] 修理请求");
        CleanupInteraction();
    }

    // ========================================
    // 战斗
    // ========================================

    private void OnCombatFromInteraction(BattleContext ctx)
    {
        _isPanelTransiting = true;
        _interactionPanel?.HidePanelImmediate(); // 立即隐藏防止闪烁

        var interactNode = _interactionMgr?.GetCurrentEntity();
        if (interactNode is BladeHex.Strategic.OverworldEnemy oe && oe.EntityRef != null)
            _lastEncounteredEntity = oe.EntityRef;

        _interactionMgr?.EndInteraction();
        CleanupCurrentEnemyNode();
        IsTimePaused = false;
        _encounterActive = true;
        if (_camera != null) _camera.PopInputBlock();
        EnterCombatScene(ctx);
        _isPanelTransiting = false;
    }

    private int _pendingArenaPrize = 0;
    private Godot.Collections.Dictionary? _pendingTournamentState;

    private void OnArenaCombatRequested(BattleContext ctx, int prize)
    {
        _pendingArenaPrize = prize;
        _pendingTournamentState = null;
        EnterCombatScene(ctx);
    }

    private void OnTournamentCombatRequested(BattleContext ctx, Godot.Collections.Dictionary state)
    {
        _pendingArenaPrize = 0;
        _pendingTournamentState = state;
        EnterCombatScene(ctx);
    }

    private void OnInteractionCompleted(string result)
    {
        CleanupInteraction();
    }

    // ========================================
    // 清理
    // ========================================

    /// <summary>当部队面板/商店面板关闭时被触发的回调</summary>
    private void OnPanelDismissed()
    {
        if (_currentTownNode != null && GodotObject.IsInstanceValid(_currentTownNode))
        {
            // 如果我们当前正在城镇中交互，且 TownPanel 尚未显示，则无缝返回 TownPanel。
            if (_townPanel != null && !_townPanel.IsPanelVisible())
            {
                GD.Print("[Interaction] 城镇内关闭商店/队伍面板，无缝返回 TownPanel");
                _townPanel.ShowTown(_currentTownNode, instantOverlay: true); // 瞬间显示遮罩，无缝衔接
                return;
            }
        }

        CleanupInteraction();
    }

    /// <summary>统一清理交互状态</summary>
    private void CleanupInteraction()
    {
        if (_isPanelTransiting)
        {
            GD.Print("[Interaction] 处于面板无缝切换中，跳过清理以防止闪烁跳变");
            return;
        }

        IsTimePaused = false;
        _poiEntered = false;
        _encounterActive = false;
        StartPlayerEntityInteractionCooldown();
        // 面板关闭冷却（使用集中化冷却模块）
        _interactionCooldown.Trigger(CooldownSource.PanelClose, Time.GetTicksMsec() / 1000.0);
        if (_camera != null) _camera.PopInputBlock();

        CleanupCurrentTownNode();
        CleanupCurrentEnemyNode();

        ClearDirectedInteraction();

        if (PendingSmuggleAmbushEntity != null)
        {
            var ambush = PendingSmuggleAmbushEntity;
            PendingSmuggleAmbushEntity = null;
            CallDeferred(nameof(TriggerCombatWithEntity), ambush);
        }
    }

    private void CleanupCurrentTownNode()
    {
        if (_currentTownNode != null && GodotObject.IsInstanceValid(_currentTownNode))
            _currentTownNode.QueueFree();
        _currentTownNode = null;
    }

    private void CleanupCurrentEnemyNode()
    {
        if (_currentEnemyNode != null && GodotObject.IsInstanceValid(_currentEnemyNode))
            _currentEnemyNode.QueueFree();
        _currentEnemyNode = null;
    }
}
