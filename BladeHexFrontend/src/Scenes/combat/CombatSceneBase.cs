// CombatSceneBase.cs
// 战斗场景完整基类 — 包含所有战斗交互逻辑
// 子类只需实现：GenerateBattlefield, SpawnUnits, HandleCombatEnd
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Map;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.View.Environment;
using BladeHex.Combat;
using BladeHex.Combat.Skills;
using BladeHex.View.Combat;
using BladeHex.Combat.AI;
using BladeHex.Combat.Commands;
using BladeHex.UI.Combat;
using BladeHex.UI.Minimap;
using BladeHex.View.Effects;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景完整基类。子类只实现初始化和结束逻辑。
/// </summary>
[GlobalClass]
public abstract partial class CombatSceneBase : Node3D, ICombatSceneAdapter,
	ICombatSelectionContext, ICombatHighlightPort, ICombatActionServices, ICombatEndPort,
	ICombatFeedbackPort, ICombatGridQuery, ICombatTurnPort, ICombatActionPort, ICombatSkillPort, ICombatResultPort,
	ICombatSelectionPort
{
	[Signal] public delegate void CombatFinishedEventHandler(bool victory);

	// ========== 子系统 ==========
	protected HexGrid _hexGrid = null!;
	protected CombatManager _combatManager = null!;
	protected CombatUI _combatUi = null!;
	protected AIController _aiController = null!;
	protected SpellManager _spellManager = null!;
	protected SceneDecorationPlacer _decorationPlacer = null!;
	protected BladeHex.View.Combat.GrassOverlayBatcher _grassOverlay = null!;
	protected BladeHex.View.Combat.ElevationEdgeRenderer _elevationEdges = null!;
	protected BladeHex.View.Combat.HexPathRenderer _waterRenderer = null!;
	protected BladeHex.View.Combat.WaterStripRenderer _waterStripRenderer = null!;
	protected CombatMinimapPanel _combatMinimap = null!;
	protected BladeHex.View.Combat.CombatSceneBackdrop? _backdrop;
	protected BladeHex.View.Combat.CombatSunLight? _combatSunLight;
	protected BladeHex.View.Combat.BattleAnchorViewLayer _battleAnchorLayer = null!;
	protected BladeHex.Audio.AudioManager? _audioManager;
	protected CombatAttackAnimator _attackAnimator = null!;
	protected EffectOrchestrator _effectOrchestrator = null!;

	// ========== 运行时状态容器 ==========
	public CombatRuntimeState Runtime { get; } = new();

	// ========== 子类可访问的只读状态 ==========
	protected Unit? ActivePlayerUnit
	{
		get => Runtime.ActivePlayerUnit;
		set => Runtime.ActivePlayerUnit = value;
	}

	// ========== ICombatSelectionContext 实现 ==========
	Unit? ICombatSelectionContext.ActivePlayerUnit { get => Runtime.ActivePlayerUnit; set => Runtime.ActivePlayerUnit = value; }
	public bool IsExecutingAction { get => Runtime.IsExecutingAction; set => Runtime.IsExecutingAction = value; }
	public HexCell? RightClickTarget { get => Runtime.RightClickTarget; set => Runtime.RightClickTarget = value; }
	public ActionMode CurrentActionMode { get => Runtime.CurrentActionMode; set => Runtime.CurrentActionMode = value; }
	public string? SelectedSkillAction { get => Runtime.SelectedSkillAction; set => Runtime.SelectedSkillAction = value; }
	public SpellData? SelectedSpell { get => Runtime.SelectedSpell; set => Runtime.SelectedSpell = value; }
	bool ICombatSelectionContext.IsTargeting => Runtime.IsTargeting;
	bool ICombatSelectionContext.IsInteractionLocked => Runtime.IsInteractionLocked;
	void ICombatSelectionContext.EnterMoveMode() => Runtime.EnterMoveMode();
	void ICombatSelectionContext.EnterAttackMode() => Runtime.EnterAttackMode();
	void ICombatSelectionContext.EnterSkillTargeting(string skillAction)
	{
		Runtime.EnterSkillTargeting(skillAction);
		BladeHex.UI.CursorManager.SetState(BladeHex.UI.CursorState.CombatTargeting);
	}
	void ICombatSelectionContext.EnterSpellTargeting(SpellData spell)
	{
		Runtime.EnterSpellTargeting(spell);
		BladeHex.UI.CursorManager.SetState(BladeHex.UI.CursorState.CombatTargeting);
	}
	void ICombatSelectionContext.EnterItemMode() => Runtime.EnterItemMode();
	void ICombatSelectionContext.CancelAction()
	{
		Runtime.CancelAction();
		BladeHex.UI.CursorManager.SetState(BladeHex.UI.CursorState.Default);
	}
	void ICombatSelectionContext.ClearTargeting()
	{
		Runtime.ClearTargeting();
		BladeHex.UI.CursorManager.SetState(BladeHex.UI.CursorState.Default);
	}
	void ICombatSelectionContext.LockInteraction() => Runtime.LockInteraction();
	void ICombatSelectionContext.UnlockInteraction() => Runtime.UnlockInteraction();
	public CombatUI CombatUI => _combatUi;
	public CombatManager CombatManager => _combatManager;
	public SpellManager SpellManager => _spellManager;
	public HexGrid HexGrid => _hexGrid;
	public void TriggerCombatEnd(bool victory) => OnCombatEndedInternal(victory);

	protected int _mapWidth = 12;
	protected int _mapHeight = 10;
	protected BattleMapGenerator.BattleMapData? _mapData;
	private BladeHex.UI.Combat.TerrainTooltip? _terrainTooltip;

	// ========== 部署阶段状态 ==========
	private bool _deploymentPhaseActive => DeployCtrl != null && DeployCtrl.IsActive;

	// ========== 各控制器组件属性 (动态 new 创建) ==========
	public CombatCameraController CameraCtrl { get; set; } = null!;
	public CombatInputController InputCtrl { get; set; } = null!;
	public CombatHighlightController HighlightCtrl { get; set; } = null!;
	public CombatDeploymentController DeployCtrl { get; set; } = null!;
	public CombatActionDispatcher ActionDispatcher { get; set; } = null!;
	public CombatResultPresenter ResultPresenter { get; set; } = null!;
	public CombatMovementController MovementCtrl { get; set; } = null!;
	public CombatHoverPreviewController HoverPreviewCtrl { get; set; } = null!;
	public CombatSkillExecutor SkillExecutor { get; set; } = null!;
	public CombatActionPipeline ActionPipeline { get; set; } = null!;
	public CombatTargetingController TargetingController { get; set; } = null!;

	// ========== 子类必须实现 ==========
	protected abstract void GenerateBattlefield();
	protected abstract void SpawnUnits();
	protected abstract void HandleCombatEnd(bool victory);

	// ========== 子类可选覆盖 ==========
	protected virtual void OnPreBattleSetup() { }
	protected virtual void PlayCombatMusic() { }
	protected virtual bool UsePlayerDeployment() => true;
	protected virtual void SpawnUnitsForDeployment() => SpawnUnits();

	// ========== 生命周期 ==========

	public override void _Ready()
	{
		try
		{
			_audioManager = BladeHex.Data.Globals.AudioOrNull;
			InitEnvironment();
			InitSystems();
			GenerateBattlefield();
			InitCameraController();
			ComputeBattlefieldBounds();
			CombatTextureLoader.Instance.PreloadAll(_mapData);
			_grassOverlay.PlaceGrassOverlays(_hexGrid, _mapData);
			_elevationEdges.Render(_hexGrid);
			_waterStripRenderer.Render(_hexGrid);
			_decorationPlacer.PlaceDecorations(_hexGrid, _mapData);
			_combatMinimap.Initialize(_hexGrid, _combatManager, _mapWidth, _mapHeight);
			_combatUi.EmbedMinimap(_combatMinimap);
			OnPreBattleSetup();
			SpawnUnits();
			PlayCombatMusic();

			if (UsePlayerDeployment())
			{
				BeginDeploymentPhase();
			}
			else
			{
				_combatManager.StartCombat();
			}

			_combatUi.InitializePowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
			PlayEntranceTransition();
		}
		catch (Exception ex)
		{
			GD.PushError($"[{GetType().Name}] _Ready: {ex.Message}\n{ex.StackTrace}");
		}
	}

	private void ComputeBattlefieldBounds()
	{
		float margin = HexUtils.Size * 1.5f;
		float xMin, xMax, zMin, zMax;
		if (_mapData != null && _mapData.HexRadius > 0)
		{
			int n = _mapData.HexRadius;
			float halfX = n * 1.5f * HexUtils.Size;
			float halfZ = n * Mathf.Sqrt(3.0f) * HexUtils.Size;
			xMin = -halfX - margin; xMax = halfX + margin;
			zMin = -halfZ - margin; zMax = halfZ + margin;
		}
		else
		{
			float xSpacing = HexUtils.HorizontalSpacing;
			float zSpacing = HexUtils.VerticalSpacing;
			float battlefieldWidth = _mapWidth * xSpacing;
			float battlefieldDepth = _mapHeight * zSpacing;
			xMin = -margin; xMax = battlefieldWidth + margin;
			zMin = -margin; zMax = battlefieldDepth + margin;
		}

		var battlefieldBounds = new Aabb(
			new Vector3(xMin, 0, zMin),
			new Vector3(xMax - xMin, 1, zMax - zMin));
		_backdrop?.Configure(battlefieldBounds);

		// 按战场尺寸配置太阳光阴影覆盖距离(默认仅 100 单位,远端阴影会消失)
		float diagonal = new Vector2(xMax - xMin, zMax - zMin).Length();
		_combatSunLight?.ConfigureShadowDistance(diagonal);

		if (CameraCtrl != null)
		{
			CameraCtrl.ConfigureFromWorldBounds(xMin, xMax, zMin, zMax);
		}
	}



	protected virtual void InitEnvironment()
	{
		var sunLight = new DirectionalLight3D { Name = "SunLight", ShadowEnabled = true };
		AddChild(sunLight);

		_combatSunLight = new BladeHex.View.Combat.CombatSunLight { Name = "CombatSunLight" };
		AddChild(_combatSunLight);
		_combatSunLight.Initialize(sunLight, GetCurrentHour());

		_backdrop = new BladeHex.View.Combat.CombatSceneBackdrop { Name = "Backdrop" };
		AddChild(_backdrop);
	}

	/// <summary>初始化相机、输入与高亮控制器组件（在 GenerateBattlefield 之后调用）</summary>
	private void InitCameraController()
	{
		CameraCtrl = new CombatCameraController { Name = "CombatCameraController" };
		AddChild(CameraCtrl);
		CameraCtrl.CombatUI = _combatUi;
		CameraCtrl.MinimapPanel = _combatMinimap;

		// 动态装配移动控制器
		MovementCtrl = new CombatMovementController { Name = "CombatMovementController" };
		AddChild(MovementCtrl);
		MovementCtrl.Initialize(
			hexGrid: _hexGrid,
			combatManager: _combatManager,
			combatUi: _combatUi,
			parentScene: this,
			minimapPanel: _combatMinimap);
		MovementCtrl.CameraCtrl = CameraCtrl;

		// 动态装配高亮控制器
		HighlightCtrl = new CombatHighlightController { Name = "CombatHighlightController" };
		AddChild(HighlightCtrl);
		HighlightCtrl.HexGrid = _hexGrid;
		HighlightCtrl.CellHovered += (cell) => {
			if (InputCtrl != null) InputCtrl.CurrentHoverCell = cell;
			HoverPreviewCtrl?.OnCellHover(cell);
		};
		HighlightCtrl.CellHoverExited += (cell) => {
			if (InputCtrl != null && InputCtrl.CurrentHoverCell == cell) InputCtrl.CurrentHoverCell = null;
			HoverPreviewCtrl?.OnCellHoverExit(cell);
		};

		// 动态装配部署控制器
		if (DeployCtrl == null)
		{
			DeployCtrl = new CombatDeploymentController { Name = "CombatDeploymentController" };
			AddChild(DeployCtrl);
		}
		DeployCtrl.Initialize(
			hexGrid: _hexGrid,
			combatUi: _combatUi,
			combatManager: _combatManager,
			highlightCtrl: HighlightCtrl,
			parentScene: this,
			mapData: _mapData);

		// 动态装配行动分派器
		if (ActionDispatcher == null)
		{
			ActionDispatcher = new CombatActionDispatcher { Name = "CombatActionDispatcher" };
			AddChild(ActionDispatcher);
		}
		ActionDispatcher.Initialize(
			selection: this,      // ICombatSelectionContext
			highlight: this,      // ICombatHighlightPort
			feedback: this,       // ICombatFeedbackPort
			gridQuery: this,      // ICombatGridQuery
			turnPort: this,       // ICombatTurnPort
			actionPort: this,     // ICombatActionPort
			skillPort: this,      // ICombatSkillPort
			endPort: this,        // ICombatEndPort
			spellManager: _spellManager);

		// 动态装配悬停预览控制器
		if (HoverPreviewCtrl == null)
		{
			HoverPreviewCtrl = new CombatHoverPreviewController { Name = "CombatHoverPreviewController" };
			AddChild(HoverPreviewCtrl);
		}
		HoverPreviewCtrl.Initialize(
			selection: this,      // ICombatSelectionContext
			highlight: this,      // ICombatHighlightPort
			gridQuery: this,      // ICombatGridQuery
			turnPort: this,       // ICombatTurnPort
			skillPort: this,      // ICombatSkillPort
			highlightCtrl: HighlightCtrl,
			combatUi: _combatUi,
			combatManager: _combatManager,
			hexGrid: _hexGrid,
			parentScene: this);

		// 动态装配战斗结果呈现器
		if (ResultPresenter == null)
		{
			ResultPresenter = new CombatResultPresenter { Name = "CombatResultPresenter" };
			AddChild(ResultPresenter);
		}
		ResultPresenter.Initialize(
			highlight: this,      // ICombatHighlightPort
			feedback: this,       // ICombatFeedbackPort
			parentScene: this,
			audioManager: _audioManager);

		// 动态装配技能执行器
		if (SkillExecutor == null)
		{
			SkillExecutor = new CombatSkillExecutor { Name = "CombatSkillExecutor" };
			AddChild(SkillExecutor);
		}
		SkillExecutor.Initialize(
			selection: this,      // ICombatSelectionContext
			highlight: this,      // ICombatHighlightPort
			feedback: this,       // ICombatFeedbackPort
			gridQuery: this,      // ICombatGridQuery
			turnPort: this,       // ICombatTurnPort
			movementCtrl: MovementCtrl,
			attackAnimator: _attackAnimator,
			combatUi: _combatUi,
			combatManager: _combatManager,
			hexGrid: _hexGrid,
			parentScene: this,
			audioManager: _audioManager,
			effectOrchestrator: _effectOrchestrator);
		_attackAnimator.CameraCtrl = CameraCtrl;

		// 动态装配动作管线
		if (ActionPipeline == null)
		{
			ActionPipeline = new CombatActionPipeline { Name = "CombatActionPipeline" };
			AddChild(ActionPipeline);
		}
		ActionPipeline.Initialize(
			selection: this,      // ICombatSelectionContext
			dispatcher: ActionDispatcher,
			executor: SkillExecutor);

		// 动态装配瞄准控制器
		if (TargetingController == null)
		{
			TargetingController = new CombatTargetingController { Name = "CombatTargetingController" };
			AddChild(TargetingController);
		}
		TargetingController.HighlightCtrl = HighlightCtrl;
		TargetingController.HoverPreviewCtrl = HoverPreviewCtrl;
		HighlightCtrl.TargetingCtrl = TargetingController;

		// 注入 TargetingController 到 HoverPreviewCtrl
		HoverPreviewCtrl.TargetingController = TargetingController;

		// 动态装配输入控制器（在 ActionPipeline 和 DeployCtrl 之后）
		InputCtrl = new CombatInputController { Name = "CombatInputController" };
		AddChild(InputCtrl);
		InputCtrl.Initialize(
			selection: this,      // ICombatSelectionContext
			highlight: this,      // ICombatHighlightPort
			feedback: this,       // ICombatFeedbackPort
			gridQuery: this,      // ICombatGridQuery
			turnPort: this,       // ICombatTurnPort
			selectionPort: this,  // ICombatSelectionPort
			pipeline: ActionPipeline,
			deployCtrl: DeployCtrl,
			cameraCtrl: CameraCtrl);

		// 订阅输入控制器抛出的高层业务事件
		InputCtrl.EndTurnRequested += OnEndTurnRequested;
		InputCtrl.EscapePressed += OnEscapePressed;
		InputCtrl.TabPressed += CycleNextPlayerUnit;
		InputCtrl.QuickSlotRequested += (slot) => _combatUi.TriggerQuickSlot(slot);

		// 注入底部 HUD 遮挡检测所需的相机和单位列表
		if (CameraCtrl.Camera != null)
			_combatUi.SetOcclusionSources(CameraCtrl.Camera, _combatManager.AllUnits);
	}

	// ========== 输入与交互事件业务分流 ==========

	private void OnEndTurnRequested()
	{
		if (_combatManager.CurrentState == CombatManager.CombatState.PlayerTurn)
		{
			_combatUi.LogMessage("玩家结束回合。");
			Runtime.CancelAction();
			ClearHighlights();
			HighlightCtrl.HideSelectedUnitMarker();
			_combatManager.EndCurrentTurn();
		}
	}

	private void OnEscapePressed()
	{
		var gameMenu = BladeHex.Data.Globals.GameMenuOrNull;
		if (gameMenu != null && gameMenu.IsOpen)
			return;

		if (Runtime.IsInteractionLocked)
			return;

		if (Runtime.CurrentActionMode != ActionMode.None)
		{
			Runtime.CancelAction();
			ClearHighlights();
			_combatUi.SetApPreview(0f);
			if (Runtime.ActivePlayerUnit != null) ShowSelectedUnitHighlights();
			_combatUi.LogMessage("取消操作。");
		}
		else if (Runtime.ActivePlayerUnit != null)
		{
			Runtime.ActivePlayerUnit = null;
			ClearHighlights();
			HighlightCtrl.HideSelectedUnitMarker();
		}
		else
		{
			gameMenu?.Toggle();
		}
	}

	// ========== 系统 ==========

	protected virtual void InitSystems()
	{
		_hexGrid = new HexGrid { Name = "HexGrid" };
		AddChild(_hexGrid);

		var vfxManager = new VFXManager { Name = "VFXManager" };
		AddChild(vfxManager);

		_effectOrchestrator = new EffectOrchestrator { Name = "EffectOrchestrator" };
		AddChild(_effectOrchestrator);

		_combatManager = new CombatManager { Name = "CombatManager" };
		AddChild(_combatManager);
		_combatManager.CurrentGrid = _hexGrid;
		_combatManager.TurnStarted += OnTurnStarted;
		_combatManager.CombatEnded += OnCombatEndedInternal;

		_battleAnchorLayer = new BladeHex.View.Combat.BattleAnchorViewLayer { Name = "BattleAnchorLayer" };
		AddChild(_battleAnchorLayer);
		_battleAnchorLayer.Initialize(_hexGrid, _combatManager);

		_combatUi = new CombatUI { Name = "CombatUI" };
		AddChild(_combatUi);
		_combatUi.ActionSelected += OnActionSelected;
		_combatUi.SpellSelected += OnSpellSelected;
		_combatUi.ActionHovered += (action) => HoverPreviewCtrl?.OnActionHovered(action);

		_spellManager = new SpellManager { Name = "SpellManager" };
		AddChild(_spellManager);

		_aiController = new AIController { Name = "AIController", DifficultyConfig = _combatManager.DifficultyConfig };
		_aiController.Initialize();
		_aiController.SetCombatManager(_combatManager);
		_aiController.SetCombatScene(this);
		AddChild(_aiController);

		_decorationPlacer = new SceneDecorationPlacer { Name = "DecorationPlacer" };
		AddChild(_decorationPlacer);

		_grassOverlay = new BladeHex.View.Combat.GrassOverlayBatcher { Name = "GrassOverlay" };
		AddChild(_grassOverlay);

		_elevationEdges = new BladeHex.View.Combat.ElevationEdgeRenderer { Name = "ElevationEdges" };
		AddChild(_elevationEdges);

		_waterRenderer = new BladeHex.View.Combat.HexPathRenderer { Name = "WaterRenderer" };
		AddChild(_waterRenderer);

		_waterStripRenderer = new BladeHex.View.Combat.WaterStripRenderer { Name = "WaterStripRenderer" };
		AddChild(_waterStripRenderer);

		_combatMinimap = new CombatMinimapPanel();

		var scheduler = new SceneTreeScheduler(GetTree());
		var projectileSystem = new ProjectileSystem(scheduler);
		var projectilePool = new ProjectilePool { Name = "ProjectilePool" };
		AddChild(projectilePool);
		new ProjectileEventBridge().Bind(projectileSystem);
		_attackAnimator = new CombatAttackAnimator { Name = "AttackAnimator" };
		AddChild(_attackAnimator);
		_attackAnimator.Initialize(projectileSystem, CameraCtrl);
		_aiController.SetAttackAnimator(_attackAnimator);
	}

	private void PlayEntranceTransition()
	{
	}

	protected void RegisterAndInitUnit(Unit unit, bool isAlly)
	{
		_combatManager.RegisterUnit(unit, isAlly);
		if (isAlly) _combatUi.RegisterAlly(unit);
		else        _combatUi.RegisterEnemy(unit);
		unit.InitDr();

		var data = unit.Data;
		if (data == null) return;

		bool eligible = isAlly || data.enemyType == UnitData.EnemyType.Humanoid;
		if (!eligible) return;

		var stm = BladeHex.Data.Globals.SkillTreesOrNull;
		if (stm?.TreeData == null) return;

		CharacterSkillTree? tree = null;
		if (isAlly && data.CharacterId >= 0
			&& stm.GetSkillTree(data.CharacterId) is { } cached)
		{
			tree = cached;
		}
		tree ??= SkillTreeAllocator.AllocateForUnit(data, stm.TreeData);

		if (tree != null)
		{
			unit.SkillTree = tree;
			data.Runtime.SkillTree = tree;
			if (isAlly && data.CharacterId >= 0)
				stm.SetSkillTree(data.CharacterId, tree);
		}
	}

	// ========== 单位放置/移动 ==========

	protected void PlaceUnitAt(Unit unit, int q, int r)
	{
		var cell = _hexGrid.GetCell(q, r);
		if (cell == null)
		{
			cell = DeployCtrl?.FindClosestDeployableCell(q, r);
			if (cell == null)
			{
				GD.PushWarning($"[CombatSceneBase] PlaceUnitAt({q},{r}): 找不到任何可部署 cell,单位 {unit.Data?.UnitName ?? "?"} 未放置");
				return;
			}
			GD.Print($"[CombatSceneBase] 部署回退:({q},{r}) → ({cell.GridPos.X},{cell.GridPos.Y}) 单位 {unit.Data?.UnitName ?? "?"}");
		}
		else if (cell.Occupant != null)
		{
			var alt = DeployCtrl?.FindClosestDeployableCell(q, r);
			if (alt == null)
			{
				GD.PushWarning($"[CombatSceneBase] PlaceUnitAt({q},{r}): 目标已占据且无可用替代 cell");
				return;
			}
			cell = alt;
		}

		// 计算多格占用
		var (fw, fh) = BladeHex.Combat.UnitFootprint.GetSize(unit.Data!);
		unit.FootprintW = fw;
		unit.FootprintH = fh;
		var footprintCells = BladeHex.Combat.UnitFootprint.GetFootprintCells(cell.GridPos, fw, fh);

		// 验证所有足迹格可用（存在且未被占据）
		if (fw > 1 || fh > 1)
		{
			bool allFree = true;
			foreach (var fp in footprintCells)
			{
				var fpCell = _hexGrid.GetCell(fp.X, fp.Y);
				if (fpCell == null || (fpCell.Occupant != null && fpCell.Occupant != unit))
				{
					allFree = false;
					break;
				}
			}
			if (!allFree)
			{
				// 回退到单格放置
				GD.PushWarning($"[CombatSceneBase] 多格单位 {unit.Data?.UnitName} 足迹不完整，回退单格放置");
				fw = 1;
				fh = 1;
				unit.FootprintW = 1;
				unit.FootprintH = 1;
				footprintCells = [cell.GridPos];
			}
		}

		AddChild(unit);
		unit.Position = cell.Position + new Vector3(0, BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);
		unit.GridPos = cell.GridPos;
		unit.OccupiedCells = footprintCells;

		// 设置所有占用格的 Occupant
		foreach (var fp in footprintCells)
		{
			var fpCell = _hexGrid.GetCell(fp.X, fp.Y);
			if (fpCell != null) fpCell.Occupant = unit;
		}

		unit.Facing = unit.IsPlayerSide ? 0 : 3;
	}

	// ========== 战利品生成（子类共用）==========

	protected void GenerateLoot(List<LootEntry> lootEntries, List<ItemData> lootItems)
	{
		float totalCr = 0;
		foreach (var enemy in _combatManager.EnemyUnits)
		{
			if (enemy?.Data == null) continue;
			var data = enemy.Data;
			totalCr += data.ThreatLevel;

			if (data.Armor != null)
			{
				lootEntries.Add(new LootEntry(data.Armor.GetFullName(), LootEntry.LootType.Armor, 1, TradePricingService.GetSellPrice(data.Armor), data.Armor.GetArmorDescription()));
				lootItems.Add(data.Armor);
			}
			if (data.Shield != null)
			{
				lootEntries.Add(new LootEntry(data.Shield.GetFullName(), LootEntry.LootType.Shield, 1, TradePricingService.GetSellPrice(data.Shield), data.Shield.GetArmorDescription()));
				lootItems.Add(data.Shield);
			}
			if (data.Helmet != null)
			{
				lootEntries.Add(new LootEntry(data.Helmet.GetFullName(), LootEntry.LootType.Helmet, 1, TradePricingService.GetSellPrice(data.Helmet), data.Helmet.GetArmorDescription()));
				lootItems.Add(data.Helmet);
			}
			if (data.PrimaryMainHand != null && GD.Randf() < 0.5f)
			{
				lootEntries.Add(new LootEntry(data.PrimaryMainHand.GetFullName(), LootEntry.LootType.Weapon, 1, TradePricingService.GetSellPrice(data.PrimaryMainHand), data.PrimaryMainHand.GetWeaponDescription()));
				lootItems.Add(data.PrimaryMainHand);
			}
		}

		string difficulty = EquipmentGenerator.GetDifficultyFromCr(totalCr);
		int itemLevel = EquipmentGenerator.GetItemLevelFromCr(totalCr);
		// 随机掉落轮次：每个敌人 3 次 + 随机扰动 [-1, 0, 1]，最低 3 次
		int enemyCount = _combatManager.EnemyUnits.Count;
		int baseRolls = enemyCount * 3;
		int perturbation = Math.Clamp((int)(GD.Randf() * 3) - 1, -1, 1);
		int bonusDrops = Math.Max(3, baseRolls + perturbation);

		for (int i = 0; i < bonusDrops; i++)
		{
			if (GD.Randf() < 0.4f)
			{
				var generated = GD.Randf() < 0.5f
					? (ItemData)EquipmentGenerator.GenerateRandomWeapon(null, (ItemData.Rarity)(-1), itemLevel, difficulty)
					: (ItemData)EquipmentGenerator.GenerateRandomArmor(null, (ItemData.Rarity)(-1), itemLevel, difficulty);
				var lootType = generated is WeaponData ? LootEntry.LootType.Weapon : LootEntry.LootType.Armor;
				lootEntries.Add(new LootEntry(generated.GetFullName(), lootType, 1, TradePricingService.GetSellPrice(generated), $"{generated.GetRarityName()} | {generated.GetAffixDescriptions()}"));
				lootItems.Add(generated);
			}
		}

		var consumableList = new List<ConsumableData>(PrototypeData.GetConsumables().Values);
		// 消耗品掉落：每个敌人 1 次 + 随机扰动 [-1, 0, 1]，至少 1 次
		int consumPerturbation = Math.Clamp((int)(GD.Randf() * 3) - 1, -1, 1);
		int consumDrops = Math.Max(1, enemyCount + consumPerturbation);
		for (int i = 0; i < consumDrops; i++)
		{
			if (GD.Randf() < 0.35f && consumableList.Count > 0)
			{
				var c = consumableList[(int)(GD.Randf() * consumableList.Count)];
				lootEntries.Add(new LootEntry(c.ItemName, LootEntry.LootType.Consumable, 1, TradePricingService.GetSellPrice(c), c.Description));
				lootItems.Add(c);
			}
		}
	}

	public void MoveUnitTo(Unit unit, int q, int r, List<Vector2I>? path = null)
	{
		MovementCtrl?.MoveUnitTo(unit, q, r, path);
	}

	// ========== 地块点击与悬停转发 ==========

	protected void OnCellClicked(HexCell cell)
	{
		GD.Print($"[CombatSceneBase] OnCellClicked ({cell.GridPos.X},{cell.GridPos.Y}) InputCtrl={InputCtrl != null}");
		InputCtrl?.OnCellClicked(cell);
	}

	protected void OnCellRightClicked(HexCell cell)
	{
		InputCtrl?.OnCellRightClicked(cell);
	}

	protected void OnCellHover(HexCell cell)
	{
		if (InputCtrl != null) InputCtrl.CurrentHoverCell = cell;
		HoverPreviewCtrl?.OnCellHover(cell);
	}

	protected void OnCellHoverExit(HexCell cell)
	{
		if (InputCtrl != null && InputCtrl.CurrentHoverCell == cell) InputCtrl.CurrentHoverCell = null;
		HoverPreviewCtrl?.OnCellHoverExit(cell);
	}

	// ========== 高亮代理 (转发到 HighlightCtrl) ==========

	public void ClearHighlights()
	{
		HighlightCtrl.ClearHighlights();
		if (Runtime.ActivePlayerUnit == null)
			HighlightCtrl.HideSelectedUnitMarker();
	}

	public void HighlightRange(Unit unit, int range, Color color, bool emptyOnly = false)
		=> HighlightCtrl.HighlightRange(unit, range, color, emptyOnly);

	public void HighlightRange(Unit unit, List<Vector2I> cells, Color color)
		=> HighlightCtrl.HighlightRange(unit, cells, color);

	public bool HighlightedCellsContains(HexCell cell)
		=> HighlightCtrl.HighlightedCellsContains(cell);

	public void HighlightMoveRange(Unit unit)
		=> HighlightCtrl.HighlightMoveRange(unit);

	public void HighlightAttackRange(Unit unit)
		=> HighlightCtrl.HighlightAttackRange(unit);

	public void ShowSelectedUnitHighlights()
		=> HighlightCtrl.ShowSelectedUnitHighlights(Runtime.ActivePlayerUnit);

	// ========== 选中单位 ==========

	public void SelectUnit(Unit unit)
	{
		if (unit.Data != null && unit.Data.IsEnemy) return;
		if (!_combatManager.PlayerUnits.Contains(unit)) return;

		_combatUi.SetApPreview(0f);
		ClearHighlights(); Runtime.CancelAction();
		Runtime.ActivePlayerUnit = unit;
		_combatUi.UpdateUnitInfo(unit);
		_combatUi.LogMessage($"选中 {unit.Data?.UnitName}。");
		unit.PlaySelectBounce();
		ShowSelectedUnitHighlights();

		if (CameraCtrl != null)
			CameraCtrl.LockOnUnit(unit);
	}

	public void CycleNextPlayerUnit()
	{
		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;
		var alive = _combatManager.PlayerUnits.Where(u => IsInstanceValid(u) && u.CurrentHp > 0).ToList();
		if (alive.Count == 0) return;
		int idx = Runtime.ActivePlayerUnit != null ? alive.IndexOf(Runtime.ActivePlayerUnit) : -1;
		int next = (idx + 1) % alive.Count;
		SelectUnit(alive[next]);
		FocusCameraOn(alive[next].Position, 0.3f);
	}

	// ========== 部署阶段 ==========

	private void BeginDeploymentPhase()
	{
		DeployCtrl?.BeginDeploymentPhase();
	}

	// ========== 回合 ==========

	protected virtual void OnTurnStarted(int state)
	{
		Runtime.ResetActionMode();
		ClearHighlights();
		_combatMinimap.Refresh();
		var s = (CombatManager.CombatState)state;

		if (s == CombatManager.CombatState.Deployment) return;

		var orderedIds = _combatManager.Turns.GetOrderedUnitIds();
		var allUnits = new Godot.Collections.Array();
		foreach (long id in orderedIds)
		{
			foreach (var u in _combatManager.AllUnits)
			{
				if (IsInstanceValid(u) && (long)u.GetInstanceId() == id && u.CurrentHp > 0)
				{
					allUnits.Add(u);
					break;
				}
			}
		}
		_combatUi.UpdateTurnOrder(allUnits, _combatManager.CurrentInitiativeUnit, 0);

		if (s == CombatManager.CombatState.PlayerTurn)
		{
			var initUnit = _combatManager.CurrentInitiativeUnit;
			if (initUnit != null && IsInstanceValid(initUnit) && initUnit.CurrentHp > 0
				&& _combatManager.PlayerUnits.Contains(initUnit))
			{
				SelectUnit(initUnit);
			}
			else
			{
				var firstUnit = _combatManager.PlayerUnits.FirstOrDefault(u => IsInstanceValid(u) && u.CurrentHp > 0);
				if (firstUnit != null)
					SelectUnit(firstUnit);
			}

			string unitName = Runtime.ActivePlayerUnit?.Data?.UnitName ?? "玩家";
			_combatUi.SetTurnText($"▶ {unitName} 的回合", new Color(0.2f, 0.6f, 1));
			_combatUi.SetActionBarVisible(true);
			_combatUi.UpdateUnitInfo(Runtime.ActivePlayerUnit);
			_combatUi.LogMessage($"轮到 {unitName} 行动。");
			_audioManager?.PlaySfxName("combat_turn_start");

			if (Runtime.ActivePlayerUnit != null)
			{
				ShowSelectedUnitHighlights();
			}
		}
		else if (s == CombatManager.CombatState.EnemyTurn)
		{
			var initUnit = _combatManager.CurrentInitiativeUnit;
			string unitName = initUnit?.Data?.UnitName ?? "敌方";
			_combatUi.SetTurnText($"▶ {unitName} 的回合", new Color(1, 0.3f, 0.3f));
			_combatUi.SetActionBarVisible(false);
			_combatUi.UpdateUnitInfo(initUnit);
			_combatUi.LogMessage($"{unitName} 行动中...");
			_audioManager?.PlaySfxName("combat_enemy_turn");

			if (initUnit != null && IsInstanceValid(initUnit))
			{
				CameraCtrl?.LockOnUnit(initUnit);
			}

			ExecuteAiTurnForUnit(initUnit);
		}
	}

	protected async void ExecuteAiTurnForUnit(Unit? unit)
	{
		try
		{
			if (unit == null || !IsInstanceValid(unit) || unit.CurrentHp <= 0)
			{
				_combatManager.EndCurrentTurn();
				return;
			}

			await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.3);

			_aiController.AllActionsCompleted += OnAiSingleUnitDone;
			_ = _aiController.ExecuteEnemyTurn(
				new List<Unit> { unit },
				_combatManager.PlayerUnits.Where(p => IsInstanceValid(p) && p.CurrentHp > 0).ToList(),
				_hexGrid, _combatUi);
		}
		catch (Exception ex)
		{
			GD.PushError($"[CombatSceneBase] ExecuteAiTurnForUnit: {ex.Message}");
			_combatManager.EndCurrentTurn();
		}
	}

	private void OnAiSingleUnitDone()
	{
		_aiController.AllActionsCompleted -= OnAiSingleUnitDone;
		_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
		_combatManager.EndCurrentTurn();
	}

	protected void CenterCameraOnUnit(Unit unit)
	{
		CameraCtrl?.CenterCameraOnUnit(unit);
	}

	private void OnCombatEndedInternal(bool victory)
	{
		if (Runtime.CombatEnded) return;
		Runtime.CombatEnded = true;
		var outcome = BuildOutcomeForPresentation(victory);
		ResultPresenter?.OnCombatEnded(victory, outcome, (victory) =>
		{
			EmitSignal(SignalName.CombatFinished, victory);
			HandleCombatEnd(victory);
		});
	}

	/// <summary>构建战斗结算数据。子类可覆盖以提供自定义奖励计算。</summary>
	protected virtual BattleOutcome? BuildOutcomeForPresentation(bool victory) => null;

	// ========== 行动选择 ==========

	protected virtual void OnActionSelected(string action)
	{
		ActionDispatcher?.OnActionSelected(action);
	}

	protected void OnSpellSelected(SpellData spell)
	{
		ActionDispatcher?.OnSpellSelected(spell);
	}

	// ========== 悬停预览 ==========

	public void RefreshCurrentHover()
	{
	}

	// ========== 技能瞄准辅助方法 ==========

	public SkillTargetingInfo? ResolveSkillTargetingInfo(string action)
	{
		if (Runtime.ActivePlayerUnit == null || !IsInstanceValid(Runtime.ActivePlayerUnit))
			return null;
		return SkillTargetingInfo.FromAction(action, Runtime.ActivePlayerUnit);
	}

	public void HighlightSkillRangeAction(string action)
	{
		if (Runtime.ActivePlayerUnit == null) return;
		var info = ResolveSkillTargetingInfo(action);
		if (info == null) return;

		ClearHighlights();
		info.Value.ApplyHighlight(_hexGrid, HighlightCtrl.HighlightedCells);
		_combatUi.SetApPreview(info.Value.ActionCost);
	}

	public bool IsSkillTargetCellValid(string action, HexCell cell)
	{
		if (Runtime.ActivePlayerUnit == null || _combatManager == null) return false;
		var info = ResolveSkillTargetingInfo(action);
		if (info == null) return false;
		return info.Value.IsCellValid(cell, _combatManager);
	}

	public bool IsImmediateCastTargetType(string targetType)
	{
		return targetType switch
		{
			"Self" => true,
			"AllAllies" => true,
			"AllAdjacent" => true,
			_ => false,
		};
	}

	public void OnActionHovered(string action)
	{
		HoverPreviewCtrl?.OnActionHovered(action);
	}

	public async Task HandleSpell(HexCell cell)
	{
		if (SkillExecutor != null)
			await SkillExecutor.HandleSpell(cell);
	}

	public async Task HandleAttack(HexCell cell)
	{
		if (SkillExecutor != null)
			await SkillExecutor.HandleAttack(cell);
	}



	// ========== 镜头聚焦 ==========

	public async void FocusCameraOn(Vector3 targetWorldPos, float duration = 0.4f)
	{
		if (CameraCtrl != null)
		{
			await CameraCtrl.FocusCameraOn(targetWorldPos, duration);
		}
	}

	public void FocusOnUnit(Unit unit)
	{
		if (unit == null || !IsInstanceValid(unit)) return;
		FocusCameraOn(unit.Position);
	}

	// ========== 天气 ==========

	protected void SetupCombatWeather()
	{
		CombatWeatherSetup.Setup(this, _mapWidth, _mapHeight);
	}

	// ========== ICombatSceneAdapter ==========

	private static float GetCurrentHour()
	{
		try
		{
			var tree = Engine.GetMainLoop() as SceneTree;
			var econ = tree?.Root?.GetNodeOrNull<BladeHex.Data.EconomyManager>("/root/EconomyManager");
			if (econ != null) return econ.CurrentHour;
		}
		catch { /* ignore */ }
		// 仅 Quick Combat 等无世界时间(EconomyManager 由大地图挂到 /root)的测试场景才回退到此默认。
		return 12f;
	}

	void ICombatSceneAdapter.PlayUnitAnim(Unit unit, string animName) => unit.PlayAnim(animName);
	void ICombatSceneAdapter.LogMessage(string msg) { if (_combatUi != null && IsInstanceValid(_combatUi)) _combatUi.LogMessage(msg); }
	void ICombatSceneAdapter.UpdateUnitInfo(Unit unit) { if (_combatUi != null && IsInstanceValid(_combatUi)) _combatUi.UpdateUnitInfo(unit); }
	void ICombatSceneAdapter.PlayAttackHitSfx(int t, bool c) => _audioManager?.PlayAttackHitSfx(t, c);
	void ICombatSceneAdapter.PlayAttackMissSfx(int t) => _audioManager?.PlayAttackMissSfx(t);
	void ICombatSceneAdapter.PlaySfx(string n) => _audioManager?.PlaySfxName(n);
	void ICombatSceneAdapter.ShowDamageNumber(Unit target, int amount, bool isCritical, string? missLabel)
		=> BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(this, target, amount, isCritical, missLabel);

	void ICombatSceneAdapter.OnUnitKilled(Unit dead, Unit killer)
	{
		if (dead == null || !IsInstanceValid(dead)) return;

		_combatManager?.HandleUnitKilled(dead, killer);

		if (_combatUi != null && IsInstanceValid(_combatUi) && _combatManager != null)
		{
			_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
		}
	}

	// ========== ICombatFeedbackPort 实现 ==========

	void ICombatFeedbackPort.LogMessage(string message) => _combatUi?.LogMessage(message);
	void ICombatFeedbackPort.UpdateUnitInfo(Unit unit) { if (_combatUi != null && IsInstanceValid(_combatUi)) _combatUi.UpdateUnitInfo(unit); }
	void ICombatFeedbackPort.ShowDamageNumber(Unit target, int amount, bool isCritical, string? missLabel)
		=> BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(this, target, amount, isCritical, missLabel);
	void ICombatFeedbackPort.SetApPreview(float apCost) => _combatUi?.SetApPreview(apCost);
	void ICombatFeedbackPort.SetTurnText(string text, Color color) => _combatUi?.SetTurnText(text, color);
	void ICombatFeedbackPort.OpenSpellPanel(Unit unit, SpellManager spellManager) => _combatUi?.OpenSpellPanel(unit, spellManager);
	void ICombatFeedbackPort.CloseSpellPanel() => _combatUi?.CloseSpellPanel();
	void ICombatFeedbackPort.OpenRadialMenuCustom(Vector2 screenPos, Godot.Collections.Dictionary options) => _combatUi?.OpenRadialMenuCustom(screenPos, options);
	void ICombatFeedbackPort.HideUnitInspect() => _combatUi?.HideUnitInspect();
	void ICombatFeedbackPort.ShowUnitInspect(Unit unit, Vector2 screenPos) => _combatUi?.ShowUnitInspect(unit, screenPos);
	void ICombatFeedbackPort.AddChild(Node node) => AddChild(node);
	void ICombatFeedbackPort.SetActionBarVisible(bool visible) => _combatUi?.SetActionBarVisible(visible);
	void ICombatFeedbackPort.HideHitPreview() => _combatUi?.HideHitPreview();
	void ICombatFeedbackPort.HideSkillPreview() => _combatUi?.HideSkillPreview();
	void ICombatFeedbackPort.ShowHitPreview(Vector2 screenPos, Unit attacker, Unit defender, HexGrid grid, int cover, int elevDiff, bool flanking, bool isCritical)
		=> _combatUi?.ShowHitPreview(screenPos, attacker, defender, grid, cover, elevDiff, flanking, isCritical);
	void ICombatFeedbackPort.ShowOutOfRangePreview(Vector2 screenPos, Unit target, int distance, int range)
		=> _combatUi?.ShowOutOfRangePreview(screenPos, target, distance, range);
	void ICombatFeedbackPort.ShowApDeficientPreview(Vector2 screenPos, Unit target, float requiredAp, float currentAp)
		=> _combatUi?.ShowApDeficientPreview(screenPos, target, requiredAp, currentAp);
	void ICombatFeedbackPort.ShowSkillPreview(Vector2 screenPos, Unit caster, SkillTargetingInfo info, System.Collections.Generic.List<Unit> affectedUnits)
		=> _combatUi?.ShowSkillPreview(screenPos, caster, info, affectedUnits);

	// ========== ICombatGridQuery 实现 ==========

	HexCell? ICombatGridQuery.GetCell(int q, int r) => _hexGrid?.GetCell(q, r);
	HexCell? ICombatGridQuery.GetCell(Vector2I gridPos) => _hexGrid?.GetCell(gridPos.X, gridPos.Y);
	List<Vector2I>? ICombatGridQuery.FindPath(Vector2I from, Vector2I to) => _hexGrid?.FindPath(from, to);
	float ICombatGridQuery.GetPathCost(Vector2I from, List<Vector2I> path) => _hexGrid?.GetPathCost(from, path) ?? 0f;
	int ICombatGridQuery.GetAxialDistance(Vector2I a, Vector2I b) => HexUtils.AxialDistance(a, b);
	List<Vector2I> ICombatGridQuery.GetCellsInRange(int startQ, int startR, float movePoints) => _hexGrid?.GetCellsInRange(startQ, startR, movePoints) ?? new List<Vector2I>();
	bool ICombatGridQuery.IsCellPassable(HexCell cell) => cell?.Data?.isPassable ?? false;
	bool ICombatGridQuery.IsCellOccupied(HexCell cell) => cell?.Occupant != null;

	// ========== ICombatTurnPort 实现 ==========

	void ICombatTurnPort.EndCurrentTurn() => _combatManager?.EndCurrentTurn();
	CombatManager.CombatState ICombatTurnPort.CurrentState => _combatManager?.CurrentState ?? CombatManager.CombatState.PlayerTurn;
	bool ICombatTurnPort.IsPlayerTurn => _combatManager?.CurrentState == CombatManager.CombatState.PlayerTurn;
	bool ICombatTurnPort.IsPlayerUnit(Unit unit) => _combatManager?.PlayerUnits.Contains(unit) ?? false;
	IReadOnlyList<Unit> ICombatTurnPort.PlayerUnits => _combatManager?.PlayerUnits ?? new List<Unit>();
	IReadOnlyList<Unit> ICombatTurnPort.EnemyUnits => _combatManager?.EnemyUnits ?? new List<Unit>();

	// ========== ICombatActionPort 实现 ==========

	async Task ICombatActionPort.HandleAttack(HexCell targetCell)
	{
		if (SkillExecutor != null)
			await SkillExecutor.HandleAttack(targetCell);
	}

	async Task ICombatActionPort.HandleSpell(HexCell targetCell)
	{
		if (SkillExecutor != null)
			await SkillExecutor.HandleSpell(targetCell);
	}

	// ========== ICombatSkillPort 实现 ==========

	SkillTargetingInfo? ICombatSkillPort.ResolveSkillTargetingInfo(string action) => ResolveSkillTargetingInfo(action);
	bool ICombatSkillPort.IsImmediateCastTargetType(string targetType) => IsImmediateCastTargetType(targetType);
	void ICombatSkillPort.OnActionHovered(string action) => HoverPreviewCtrl?.OnActionHovered(action);
	void ICombatSkillPort.RefreshCurrentHover() { /* no-op */ }

	// ========== ICombatResultPort 实现 ==========

	void ICombatResultPort.TriggerCombatEnd(bool victory) => OnCombatEndedInternal(victory);
	void ICombatResultPort.ShowCombatResult(bool victory) { /* handled by ResultPresenter */ }

	// ========== ICombatSelectionPort 实现 ==========

	void ICombatSelectionPort.SelectUnit(Unit unit) => SelectUnit(unit);
	void ICombatSelectionPort.DeselectCurrentUnit() { Runtime.ActivePlayerUnit = null; ClearHighlights(); HighlightCtrl.HideSelectedUnitMarker(); }
	void ICombatSelectionPort.CycleNextPlayerUnit() => CycleNextPlayerUnit();
	Unit? ICombatSelectionPort.ActivePlayerUnit => Runtime.ActivePlayerUnit;

	// ========== 动态光影帧 Tick 驱动 ==========
	public override void _Process(double delta)
	{
		base._Process(delta);

		float currentHour = GetCurrentHour();

		// 驱动太阳方向光与色温 Tick（地表受光已交给 Godot 真实光照管线，无需再喂 shader uniform）
		if (_combatSunLight != null && GodotObject.IsInstanceValid(_combatSunLight))
		{
			_combatSunLight.Tick(currentHour);
		}
	}
}
