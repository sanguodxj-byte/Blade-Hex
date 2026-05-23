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
using BladeHex.View.Environment;
using BladeHex.Combat;
using BladeHex.Combat.AI;
using BladeHex.Combat.Commands;
using BladeHex.UI.Combat;
using BladeHex.UI.Minimap;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景完整基类。子类只实现初始化和结束逻辑。
/// </summary>
[GlobalClass]
public abstract partial class CombatSceneBase : Node3D, ICombatSceneAdapter
{
	// ========== 行动模式枚举（替代字符串魔法值）==========
	protected enum ActionMode { None, Move, Attack, Spell, Item }

	[Signal] public delegate void CombatFinishedEventHandler(bool victory);

	// ========== 子系统 ==========
	protected HexGrid _hexGrid = null!;
	protected CombatManager _combatManager = null!;
	protected Camera3D _camera = null!;
	protected CombatUI _combatUi = null!;
	protected AIController _aiController = null!;
	protected SpellManager _spellManager = null!;
	protected SceneDecorationPlacer _decorationPlacer = null!;
	protected BladeHex.View.Combat.GrassOverlayBatcher _grassOverlay = null!;
	protected BladeHex.View.Combat.HexPathRenderer _waterRenderer = null!;
	protected BladeHex.View.Combat.WaterStripRenderer _waterStripRenderer = null!;
	protected CombatMinimapPanel _combatMinimap = null!;
	protected BladeHex.View.Combat.CombatSceneBackdrop? _backdrop;
	protected BladeHex.View.Combat.CombatSunLight? _combatSunLight;
	protected BladeHex.Audio.AudioManager? _audioManager;
	protected CombatAttackAnimator _attackAnimator = null!;

	// ========== 状态 ==========
	protected ActionMode _currentActionMode = ActionMode.None;
	protected Unit? _activePlayerUnit;
	protected readonly List<HexCell> _highlightedCells = new();
	protected bool _combatEnded;
	protected int _mapWidth = 12;
	protected int _mapHeight = 10;
	protected BattleMapGenerator.BattleMapData? _mapData;
	protected SpellData? _selectedSpell;
	protected string? _selectedSkillAction;
	private HexCell? _rightClickTarget;
	private BladeHex.UI.Combat.TerrainTooltip? _terrainTooltip;
	private HexCell? _hoverHighlightedCell;
	private bool _attackRangeShownForHover;
	private readonly List<HexCell> _attackRangeOverlayCells = new();

	// 长按检视状态
	private HexCell? _longPressCell;
	private double _longPressTimer;
	private const double LongPressDuration = 0.8; // 0.8秒触发检视
	private bool _longPressTriggered;

	// ========== 部署阶段状态 ==========
	private bool _deploymentPhaseActive;
	private readonly List<HexCell> _deploymentZoneCells = new();
	private readonly List<Unit> _unitsToPlace = new();
	private int _currentDeployIndex;
	private Unit? _selectedDeployUnit;
	private Button? _deployConfirmButton;

	// 战场世界边界（X/Z 范围），由 GenerateBattlefield 后计算。
	// 用途：决定"最大缩放=刚好框住整张地图"的语义。
	protected Aabb _battlefieldBounds;
	// 相机平移边界。等于 _battlefieldBounds 外扩 CameraPanPaddingHexes 个 hex，
	// 让玩家可以把视角拉到战场外围，看清边缘单位与远处地形。
	protected Aabb _cameraPanBounds;
	// 相机正交尺寸限制
	private const float MinOrthoSize = 200f;
	private float _maxOrthoSize = 2000f;
	// 相机可平移区域相对战场的额外外延（以 hex 为单位）。
	// 1.5 hex 是战场本身的渲染边距；这里再加 6 hex，使可视区域显著大于战场。
	private const float CameraPanPaddingHexes = 6.0f;

	// ========== 常量 ==========
	// 视野系统已移除：单位永久看见整张战场。
	// LOS 改为路径上累计命中惩罚（地形阻挡 + 中间单位），见 LosCore.GetPathPenalty。
	// 未来引入 50×50+ 大型战场时再考虑加回视野半径。

	// ========== 子类必须实现 ==========
	protected abstract void GenerateBattlefield();
	protected abstract void SpawnUnits();
	protected abstract void HandleCombatEnd(bool victory);

	// ========== 子类可选覆盖 ==========
	protected virtual void OnPreBattleSetup() { }
	protected virtual void PlayCombatMusic() { }
	/// <summary>是否启用玩家手动部署阶段。子类可覆盖返回 false 以禁用。</summary>
	protected virtual bool UsePlayerDeployment() => true;
	/// <summary>
	/// 部署阶段：生成玩家单位但不放置到地图上（仅注册到 CombatManager）。
	/// 子类覆盖此方法来创建待部署单位列表。默认实现调用 SpawnUnits() 保持兼容。
	/// </summary>
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
			ComputeBattlefieldBounds();
			// 预加载战斗纹理（地图格、角色、武器动画、抛射物、场景精灵）
			CombatTextureLoader.Instance.PreloadAll(_mapData);
			// 放置草地精灵叠加层（在装饰物下方）
			_grassOverlay.PlaceGrassOverlays(_hexGrid, _mapData);
			// 渲染水面（ImmediateMesh 三角带 + UV 流动动画）
			_waterStripRenderer.Render(_hexGrid);
			// 放置场景装饰精灵（树木、岩石等）
			_decorationPlacer.PlaceDecorations(_hexGrid, _mapData);
			// 初始化战斗小地图
			_combatMinimap.Initialize(_hexGrid, _combatManager, _mapWidth, _mapHeight);
			_combatUi.EmbedMinimap(_combatMinimap);
			OnPreBattleSetup();
			SpawnUnits();
			PlayCombatMusic();

			if (UsePlayerDeployment())
			{
				// 进入部署阶段：高亮部署区，等待玩家放置单位
				BeginDeploymentPhase();
			}
			else
			{
				_combatManager.StartCombat();
			}

			// 初始化战力对比面板
			_combatUi.InitializePowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);

			// 播放入场 UI 动画
			PlayEntranceTransition();
		}
		catch (Exception ex)
		{
			GD.PushError($"[{GetType().Name}] _Ready: {ex.Message}\n{ex.StackTrace}");
		}
	}

	/// <summary>
	/// 根据 _mapData 计算战场世界 AABB（含边距）。
	/// 适配六边形 / 矩形两种 shape。
	/// </summary>
	private void ComputeBattlefieldBounds()
	{
		float margin = HexUtils.Size * 1.5f;

		float xMin, xMax, zMin, zMax;
		if (_mapData != null && _mapData.HexRadius > 0)
		{
			// 六边形：以 (0,0) 为中心，radius N
			int n = _mapData.HexRadius;
			// 六边形 axial 顶点：world.X 范围 [-N·1.5·Size, N·1.5·Size]
			// world.Z 范围（pointy-top axial）：约 [-N·sqrt(3)·Size, N·sqrt(3)·Size]
			float halfX = n * 1.5f * HexUtils.Size;
			float halfZ = n * Mathf.Sqrt(3.0f) * HexUtils.Size;
			xMin = -halfX - margin; xMax = halfX + margin;
			zMin = -halfZ - margin; zMax = halfZ + margin;
		}
		else
		{
			// 矩形：原 W×H bbox
			float xSpacing = HexUtils.HorizontalSpacing;
			float zSpacing = HexUtils.VerticalSpacing;
			float battlefieldWidth = _mapWidth * xSpacing;
			float battlefieldDepth = _mapHeight * zSpacing;
			xMin = -margin; xMax = battlefieldWidth + margin;
			zMin = -margin; zMax = battlefieldDepth + margin;
		}

		_battlefieldBounds = new Aabb(
			new Vector3(xMin, 0, zMin),
			new Vector3(xMax - xMin, 1, zMax - zMin));

		// 相机平移边界：战场外扩 CameraPanPaddingHexes 个 hex
		float panPad = CameraPanPaddingHexes * HexUtils.Size;
		_cameraPanBounds = new Aabb(
			new Vector3(xMin - panPad, 0, zMin - panPad),
			new Vector3((xMax - xMin) + panPad * 2f, 1, (zMax - zMin) + panPad * 2f));

		// 配置占位背景到战场外延
		_backdrop?.Configure(_battlefieldBounds);

		// 最大正交尺寸：能看见整个战场
		RecalcMaxOrthoSize();

		// 自动居中相机到战场中心
		var center = _battlefieldBounds.Position + _battlefieldBounds.Size * 0.5f;
		_camera.Position = new Vector3(center.X, _camera.Position.Y, center.Z + _camera.Position.Y);
		ClampCameraPosition();

		// UI 布局完成后再次居中（首帧 UI 大小可能仍为 0）
		CallDeferred(nameof(ApplyUiAwareCamera));
	}

	/// <summary>等 UI 布局确定后再次根据 UI insets 校正相机和缩放上限</summary>
	private void ApplyUiAwareCamera()
	{
		if (_camera == null) return;
		RecalcMaxOrthoSize();
		_camera.Size = Mathf.Min(_camera.Size, _maxOrthoSize);
		var center = _battlefieldBounds.Position + _battlefieldBounds.Size * 0.5f;
		_camera.Position = new Vector3(center.X, _camera.Position.Y, center.Z + _camera.Position.Y);
		ClampCameraPosition();
	}

	/// <summary>重新计算最大正交尺寸（视口大小变化时也应调用）</summary>
	private void RecalcMaxOrthoSize()
	{
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		_maxOrthoSize = BladeHex.View.Camera.CameraBoundsClamp.MaxOrthoSizeToFit(
			_battlefieldBounds, -45f, aspect, topRatio, bottomRatio);
	}

	/// <summary>
	/// 估算顶部 / 底部 UI 占视口高度的比例，用于把"有效视口"视为 UI 之间的部分。
	/// 顶部 = 无（UI 已移至底部）；底部 = 回合顺序栏 + 角色信息面板。
	/// </summary>
	private (float top, float bottom) GetUiInsetRatios()
	{
		var viewport = GetViewport().GetVisibleRect().Size;
		float vh = Mathf.Max(1f, viewport.Y);
		float topPx = 0f, bottomPx = 0f;
		if (_combatUi != null)
		{
			// 顶部无 UI
			// 底部 = 回合顺序栏 + 底部面板
			if (_combatUi.TurnOrderBarControl is { } turnBar && turnBar.Size.Y > 0)
				bottomPx += turnBar.Size.Y;
			if (_combatUi.BottomPanel is { } bot && bot.Size.Y > 0)
				bottomPx += bot.Size.Y + 16f;
		}
		// 兜底：万一 UI 还没布局完（_Ready 早期），用经验值
		if (bottomPx <= 0f) bottomPx = 250f;
		return (Mathf.Clamp(topPx / vh, 0f, 0.45f), Mathf.Clamp(bottomPx / vh, 0f, 0.45f));
	}

	// ========== 环境 ==========

	protected virtual void InitEnvironment()
	{
		_camera = new Camera3D
		{
			Projection = Camera3D.ProjectionType.Orthogonal,
			Size = 700.0f,
			RotationDegrees = new Vector3(-45, 0, 0),
			Position = new Vector3(600, 800, 1000),
			Current = true,
		};
		AddChild(_camera);
		// 太阳光:由 CombatSunLight 组件根据时刻控制方向/色温/能量
		var sunLight = new DirectionalLight3D { Name = "SunLight", ShadowEnabled = true };
		AddChild(sunLight);

		_combatSunLight = new BladeHex.View.Combat.CombatSunLight { Name = "CombatSunLight" };
		AddChild(_combatSunLight);
		_combatSunLight.Initialize(sunLight, GetCurrentHour());

		// 占位背景：上方天空 + 周围地面（颜色对比）
		_backdrop = new BladeHex.View.Combat.CombatSceneBackdrop { Name = "Backdrop" };
		AddChild(_backdrop);
	}

	// ========== 系统 ==========

	protected virtual void InitSystems()
	{
		_hexGrid = new HexGrid { Name = "HexGrid" };
		AddChild(_hexGrid);

		// VFXManager — Scene Service。挂载后 _Ready 初始化粒子池，
		// VFXManager.PlayXxx 静态 API 通过共享池工作。
		var vfxManager = new VFXManager { Name = "VFXManager" };
		AddChild(vfxManager);

		_combatManager = new CombatManager { Name = "CombatManager" };
		AddChild(_combatManager);
		_combatManager.TurnStarted += OnTurnStarted;
		_combatManager.CombatEnded += OnCombatEndedInternal;

		_combatUi = new CombatUI { Name = "CombatUI" };
		AddChild(_combatUi);
		_combatUi.ActionSelected += OnActionSelected;
		_combatUi.SpellSelected += OnSpellSelected;
		_combatUi.ActionHovered += OnActionHovered;

		_spellManager = new SpellManager { Name = "SpellManager" };
		AddChild(_spellManager);

		_aiController = new AIController { Name = "AIController", DifficultyConfig = _combatManager.DifficultyConfig };
		_aiController.Initialize();
		_aiController.SetCombatScene(this);
		AddChild(_aiController);

		_decorationPlacer = new SceneDecorationPlacer { Name = "DecorationPlacer" };
		AddChild(_decorationPlacer);

		_grassOverlay = new BladeHex.View.Combat.GrassOverlayBatcher { Name = "GrassOverlay" };
		AddChild(_grassOverlay);

		_waterRenderer = new BladeHex.View.Combat.HexPathRenderer { Name = "WaterRenderer" };
		AddChild(_waterRenderer);

		_waterStripRenderer = new BladeHex.View.Combat.WaterStripRenderer { Name = "WaterStripRenderer" };
		AddChild(_waterStripRenderer);

		// 战斗小地图（嵌入底部面板最右侧）
		_combatMinimap = new CombatMinimapPanel();

		// 攻击动画编排器（含投射物系统）
		var scheduler = new SceneTreeScheduler(GetTree());
		var projectileSystem = new ProjectileSystem(scheduler);
		var projectilePool = new ProjectilePool { Name = "ProjectilePool" };
		AddChild(projectilePool);
		new ProjectileEventBridge().Bind(projectileSystem);
		_attackAnimator = new CombatAttackAnimator { Name = "AttackAnimator" };
		AddChild(_attackAnimator);
		_attackAnimator.Initialize(projectileSystem);
		_aiController.SetAttackAnimator(_attackAnimator);
	}

	/// <summary>播放战斗入场 UI 过渡动画（暂时禁用，等布局稳定后再启用）</summary>
	private void PlayEntranceTransition()
	{
		// 入场动画暂时禁用
	}

	// ========== 单位放置/移动 ==========

	protected void PlaceUnitAt(Unit unit, int q, int r)
	{
		var cell = _hexGrid.GetCell(q, r);
		// 六边形地图在 v0.6 已默认启用,旧的"矩形 q/r"硬编码 fallback 会落到地图外。
		// 此处自动找最近的合法 cell 兜底,避免单位被静默丢弃。
		if (cell == null)
		{
			cell = FindClosestDeployableCell(q, r);
			if (cell == null)
			{
				GD.PushWarning($"[CombatSceneBase] PlaceUnitAt({q},{r}): 找不到任何可部署 cell,单位 {unit.Data?.UnitName ?? "?"} 未放置");
				return;
			}
			GD.Print($"[CombatSceneBase] 部署回退:({q},{r}) → ({cell.GridPos.X},{cell.GridPos.Y}) 单位 {unit.Data?.UnitName ?? "?"}");
		}
		else if (cell.Occupant != null)
		{
			// 目标已被占据 → 找最近空位
			var alt = FindClosestDeployableCell(q, r);
			if (alt == null)
			{
				GD.PushWarning($"[CombatSceneBase] PlaceUnitAt({q},{r}): 目标已占据且无可用替代 cell");
				return;
			}
			cell = alt;
		}
		AddChild(unit);
		unit.Position = cell.Position + new Vector3(0, BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);
		unit.GridPos = cell.GridPos;
		cell.Occupant = unit;
		// 初始朝向:玩家朝右(方向 0),敌人朝左(方向 3)— 两方面对面对峙
		unit.Facing = unit.IsPlayerSide ? 0 : 3;
	}

	/// <summary>找到距 (q,r) 最近的、未占据的、可通行的 cell。BFS 螺旋扫描。</summary>
	private HexCell? FindClosestDeployableCell(int q, int r)
	{
		// 简单按 axial 距离对所有现存 cell 排序,取第一个空格
		HexCell? best = null;
		int bestDist = int.MaxValue;
		foreach (var kv in _hexGrid.Cells)
		{
			var c = kv.Value;
			if (c == null || c.Occupant != null) continue;
			if (c.Data != null && !c.Data.isPassable) continue;
			int d = HexUtils.AxialDistance(kv.Key, new Vector2I(q, r));
			if (d < bestDist) { bestDist = d; best = c; }
		}
		return best;
	}

	public async void MoveUnitTo(Unit unit, int q, int r)
	{
		try
		{
			var oldCell = _hexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
			if (oldCell != null) oldCell.Occupant = null;
			var newCell = _hexGrid.GetCell(q, r);
			if (newCell != null)
			{
				newCell.Occupant = unit;
				unit.GridPos = new Vector2I(q, r);
				var startPos = unit.Position;
				var targetPos = newCell.Position + new Vector3(0, BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);

				// 平滑移动:0.5 秒基础(被快进缩放) + 小抛物线弧度
				double duration = BladeHex.View.Combat.CombatSpeed.ScaleSeconds(0.5);
				float arcHeight = 15f; // 抛物线最高点偏移(世界单位)

				double elapsed = 0;
				while (elapsed < duration)
				{
					elapsed += GetProcessDeltaTime();
					float t = Mathf.Clamp((float)(elapsed / duration), 0f, 1f);
					// Smoothstep 让起步和落地更柔和
					float smooth = t * t * (3f - 2f * t);

					// XZ 线性插值,Y 加抛物线弧
					var pos = startPos.Lerp(targetPos, smooth);
					// 抛物线:4 * h * t * (1-t),在 t=0.5 时达到最高
					pos.Y += arcHeight * 4f * t * (1f - t);

					unit.Position = pos;
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				}
				unit.Position = targetPos;
			}
			if (unit == _activePlayerUnit) UpdateFov();
			_combatMinimap?.Refresh();
		}
		catch (Exception ex)
		{
			GD.PushError($"[CombatSceneBase] MoveUnitTo: {ex.Message}");
		}
	}

	// ========== 视野 ==========

	protected void UpdateFov()
	{
		// 无视野机制：所有格子和单位永久可见
		foreach (var kvp in _hexGrid.Cells)
			kvp.Value.SetShrouded(false);
		foreach (var e in _combatManager.EnemyUnits)
			if (IsInstanceValid(e)) e.Visible = true;
	}

	// ========== 高亮 ==========

	protected void ClearHighlights() { foreach (var c in _highlightedCells) c.SetHighlight(false); _highlightedCells.Clear(); }

	protected void HighlightRange(Unit unit, int range, Color color, bool emptyOnly = false)
	{
		ClearHighlights();
		foreach (var coord in _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, range))
		{
			var cell = _hexGrid.GetCell(coord.X, coord.Y);
			if (cell != null && (!emptyOnly || cell.Occupant == null))
			{ cell.SetHighlight(true, color); _highlightedCells.Add(cell); }
		}
	}

	private void HighlightMoveRange(Unit unit)
	{
		ClearHighlights();
		float moveRange = unit.CurrentAp;
		if (moveRange <= 0) return;
		foreach (var coord in _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, moveRange))
		{
			var cell = _hexGrid.GetCell(coord.X, coord.Y);
			if (cell == null || cell.Occupant != null) continue;
			// 不可通行的格子不显示绿色高亮
			if (cell.Data != null && !cell.Data.isPassable) continue;
			cell.SetHighlight(true, new Color(0.2f, 0.8f, 0.3f, 0.2f));
			_highlightedCells.Add(cell);
		}
	}

	private void HighlightAttackRange(Unit unit)
	{
		ClearHighlights();
		var weapon = unit.GetMainHand() as WeaponData;
		int atkRange = weapon?.RangeCells ?? 1;

		// 攻击范围用 axial distance（直线距离），不受地形通行性影响
		foreach (var kvp in _hexGrid.Cells)
		{
			int dist = HexUtils.AxialDistance(unit.GridPos, kvp.Key);
			if (dist > 0 && dist <= atkRange)
			{
				var cell = kvp.Value;
				if (cell != null) { cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.2f)); _highlightedCells.Add(cell); }
			}
		}
	}

	/// <summary>显示攻击范围叠加层（不清除现有高亮，用于悬停预览）</summary>
	private void ShowAttackRangeOverlay(Unit unit)
	{
		ClearAttackRangeOverlay();
		var weapon = unit.GetMainHand() as WeaponData;
		int atkRange = weapon?.RangeCells ?? 1;

		foreach (var kvp in _hexGrid.Cells)
		{
			int dist = HexUtils.AxialDistance(unit.GridPos, kvp.Key);
			if (dist > 0 && dist <= atkRange)
			{
				var cell = kvp.Value;
				if (cell == null || _highlightedCells.Contains(cell)) continue;
				cell.SetHighlight(true, new Color(1.0f, 0.3f, 0.2f, 0.2f));
				_attackRangeOverlayCells.Add(cell);
			}
		}
	}

	/// <summary>清除攻击范围叠加层</summary>
	private void ClearAttackRangeOverlay()
	{
		foreach (var cell in _attackRangeOverlayCells)
		{
			if (!_highlightedCells.Contains(cell))
				cell.SetHighlight(false);
		}
		_attackRangeOverlayCells.Clear();
	}

	private void ShowSelectedUnitHighlights()
	{
		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;
		ClearHighlights();
		if (_activePlayerUnit.CurrentAp >= 1)
			HighlightMoveRange(_activePlayerUnit);
	}

	// ========== 选中单位 ==========

	private void SelectUnit(Unit unit)
	{
		// 安全检查：只允许选中玩家单位
		if (unit.Data != null && unit.Data.IsEnemy) return;
		if (!_combatManager.PlayerUnits.Contains(unit)) return;

		ClearHighlights(); _currentActionMode = ActionMode.None;
		_activePlayerUnit = unit;
		_combatUi.UpdateUnitInfo(unit);
		_combatUi.LogMessage($"选中 {unit.Data?.UnitName}。");
		unit.PlaySelectBounce();
		ShowSelectedUnitHighlights();
		UpdateFov();
	}

	private void CycleNextPlayerUnit()
	{
		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;
		var alive = _combatManager.PlayerUnits.Where(u => IsInstanceValid(u) && u.CurrentHp > 0).ToList();
		if (alive.Count == 0) return;
		int idx = _activePlayerUnit != null ? alive.IndexOf(_activePlayerUnit) : -1;
		int next = (idx + 1) % alive.Count;
		SelectUnit(alive[next]);
		FocusCameraOn(alive[next].Position, 0.3f);
	}

	// ========== 部署阶段 ==========

	/// <summary>开始部署阶段：高亮部署区，显示确认按钮，等待玩家放置单位。</summary>
	private void BeginDeploymentPhase()
	{
		_deploymentPhaseActive = true;
		_combatManager.EnterDeployment();

		// 收集部署区格子
		_deploymentZoneCells.Clear();
		if (_mapData?.PlayerDeployment != null)
		{
			foreach (var v in _mapData.PlayerDeployment)
			{
				var coord = v.AsVector2I();
				var cell = _hexGrid.GetCell(coord.X, coord.Y);
				if (cell != null && (cell.Data == null || cell.Data.isPassable))
					_deploymentZoneCells.Add(cell);
			}
		}

		// 边界情况：部署区为空 → 回退到自动部署，直接开始战斗
		if (_deploymentZoneCells.Count == 0)
		{
			GD.PushWarning("[Deployment] 部署区为空，回退到自动放置并开始战斗");
			_deploymentPhaseActive = false;
			AutoPlaceUnitsAndStart();
			return;
		}

		// 收集待部署的玩家单位（已注册但尚未放置到地图上的）
		_unitsToPlace.Clear();
		foreach (var unit in _combatManager.PlayerUnits)
		{
			if (IsInstanceValid(unit))
				_unitsToPlace.Add(unit);
		}
		_currentDeployIndex = 0;
		_selectedDeployUnit = _unitsToPlace.Count > 0 ? _unitsToPlace[0] : null;

		// 高亮部署区
		HighlightDeploymentZone();

		// 创建确认按钮
		CreateDeployConfirmButton();

		// UI 提示
		_combatUi.SetActionBarVisible(false);
		string unitName = _selectedDeployUnit?.Data?.UnitName ?? "单位";
		_combatUi.SetTurnText($"部署阶段 — 请放置 {unitName}", new Color(0.2f, 0.8f, 0.4f));
		_combatUi.LogMessage($"部署阶段开始。点击蓝色高亮区域放置单位。({_unitsToPlace.Count} 个单位待部署)");
		UpdateDeployConfirmButton();
	}

	/// <summary>部署区为空时的回退：自动将玩家单位放到最近可用格子并开始战斗</summary>
	private void AutoPlaceUnitsAndStart()
	{
		int placed = 0;
		foreach (var unit in _combatManager.PlayerUnits)
		{
			if (!IsInstanceValid(unit)) continue;
			// 找一个空的可通行格子
			var cell = FindClosestDeployableCell(placed, 0);
			if (cell == null) cell = FindClosestDeployableCell(0, placed);
			if (cell != null)
			{
				if (!unit.IsInsideTree()) AddChild(unit);
				unit.Position = cell.Position + new Vector3(0,
					BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);
				unit.GridPos = cell.GridPos;
				cell.Occupant = unit;
				unit.Facing = 0;
			}
			placed++;
		}
		_combatManager.ConfirmDeployment();
	}

	/// <summary>高亮玩家部署区域（蓝色半透明）</summary>
	private void HighlightDeploymentZone()
	{
		ClearHighlights();
		var deployColor = new Color(0.2f, 0.5f, 1.0f, 0.35f);
		foreach (var cell in _deploymentZoneCells)
		{
			if (cell.Occupant == null)
			{
				cell.SetHighlight(true, deployColor);
				_highlightedCells.Add(cell);
			}
		}
	}

	/// <summary>部署阶段点击格子处理</summary>
	private void HandleDeploymentClick(HexCell cell)
	{
		// 点击已放置的友方单位 → 选中该单位（可重新放置）
		if (cell.Occupant != null && _combatManager.PlayerUnits.Contains(cell.Occupant))
		{
			_selectedDeployUnit = cell.Occupant;
			int idx = _unitsToPlace.IndexOf(cell.Occupant);
			if (idx >= 0) _currentDeployIndex = idx;
			string name = _selectedDeployUnit.Data?.UnitName ?? "单位";
			_combatUi.SetTurnText($"部署阶段 — 重新放置 {name}", new Color(0.2f, 0.8f, 0.4f));
			_combatUi.LogMessage($"选中 {name}，点击部署区空格重新放置。");
			return;
		}

		// 点击部署区空格 → 放置当前选中单位
		if (_selectedDeployUnit == null) return;
		if (!_deploymentZoneCells.Contains(cell)) return;
		if (cell.Occupant != null) return;

		// 如果该单位已经在地图上，先移除旧位置
		var oldCell = _hexGrid.GetCell(_selectedDeployUnit.GridPos.X, _selectedDeployUnit.GridPos.Y);
		if (oldCell != null && oldCell.Occupant == _selectedDeployUnit)
		{
			oldCell.Occupant = null;
		}

		// 放置单位到新位置
		if (!_selectedDeployUnit.IsInsideTree())
			AddChild(_selectedDeployUnit);
		_selectedDeployUnit.Position = cell.Position + new Vector3(0,
			BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);
		_selectedDeployUnit.GridPos = cell.GridPos;
		cell.Occupant = _selectedDeployUnit;
		_selectedDeployUnit.Facing = 0; // 玩家朝右

		string unitName = _selectedDeployUnit.Data?.UnitName ?? "单位";
		_combatUi.LogMessage($"{unitName} 已部署到 ({cell.GridPos.X},{cell.GridPos.Y})。");

		// 推进到下一个未放置的单位
		AdvanceToNextUnplacedUnit();

		// 刷新高亮
		HighlightDeploymentZone();
		UpdateDeployConfirmButton();
	}

	/// <summary>推进到下一个尚未放置的单位</summary>
	private void AdvanceToNextUnplacedUnit()
	{
		// 找下一个未放置的单位
		for (int i = 0; i < _unitsToPlace.Count; i++)
		{
			int idx = (_currentDeployIndex + 1 + i) % _unitsToPlace.Count;
			var unit = _unitsToPlace[idx];
			var unitCell = _hexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
			if (unitCell == null || unitCell.Occupant != unit)
			{
				// 该单位尚未放置
				_currentDeployIndex = idx;
				_selectedDeployUnit = unit;
				string name = unit.Data?.UnitName ?? "单位";
				_combatUi.SetTurnText($"部署阶段 — 请放置 {name}", new Color(0.2f, 0.8f, 0.4f));
				return;
			}
		}

		// 所有单位都已放置
		_selectedDeployUnit = null;
		_combatUi.SetTurnText("部署阶段 — 所有单位已就位", new Color(0.2f, 0.8f, 0.4f));
		_combatUi.LogMessage("所有单位已部署完毕，点击「开始战斗」进入正式战斗。");
	}

	/// <summary>检查是否所有单位都已部署</summary>
	private bool AllUnitsDeployed()
	{
		foreach (var unit in _unitsToPlace)
		{
			if (!IsInstanceValid(unit)) continue;
			var cell = _hexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
			if (cell == null || cell.Occupant != unit) return false;
		}
		return true;
	}

	/// <summary>创建部署确认按钮</summary>
	private void CreateDeployConfirmButton()
	{
		_deployConfirmButton = new Button
		{
			Text = "开始战斗",
			CustomMinimumSize = new Vector2(160, 48),
			Visible = false,
		};
		// 样式
		_deployConfirmButton.AddThemeColorOverride("font_color", Colors.White);
		_deployConfirmButton.AddThemeFontSizeOverride("font_size", 18);

		// 放置在屏幕底部中央
		_deployConfirmButton.AnchorLeft = 0.5f;
		_deployConfirmButton.AnchorRight = 0.5f;
		_deployConfirmButton.AnchorTop = 1.0f;
		_deployConfirmButton.AnchorBottom = 1.0f;
		_deployConfirmButton.OffsetLeft = -80;
		_deployConfirmButton.OffsetRight = 80;
		_deployConfirmButton.OffsetTop = -120;
		_deployConfirmButton.OffsetBottom = -72;
		_deployConfirmButton.GrowHorizontal = Control.GrowDirection.Both;

		_deployConfirmButton.Pressed += OnDeployConfirmPressed;
		_combatUi.AddChild(_deployConfirmButton);
	}

	/// <summary>更新确认按钮可见性</summary>
	private void UpdateDeployConfirmButton()
	{
		if (_deployConfirmButton == null) return;
		_deployConfirmButton.Visible = AllUnitsDeployed();
	}

	/// <summary>确认部署按钮点击</summary>
	private void OnDeployConfirmPressed()
	{
		if (!AllUnitsDeployed()) return;

		_deploymentPhaseActive = false;

		// 清理部署 UI
		ClearHighlights();
		if (_deployConfirmButton != null)
		{
			_deployConfirmButton.QueueFree();
			_deployConfirmButton = null;
		}
		_deploymentZoneCells.Clear();
		_unitsToPlace.Clear();

		// 正式开始战斗
		_combatManager.ConfirmDeployment();
		_combatUi.LogMessage("部署完成，战斗开始！");
	}

	// ========== 回合 ==========

	protected virtual void OnTurnStarted(int state)
	{
		_currentActionMode = ActionMode.None;
		ClearHighlights();
		_combatMinimap.Refresh();
		var s = (CombatManager.CombatState)state;

		// 部署阶段信号 — 不做回合处理
		if (s == CombatManager.CombatState.Deployment) return;

		// 更新回合顺序栏（按先攻排序）
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
			// 先攻制：选中当前先攻单位（必须验证是玩家单位）
			var initUnit = _combatManager.CurrentInitiativeUnit;
			if (initUnit != null && IsInstanceValid(initUnit) && initUnit.CurrentHp > 0
				&& _combatManager.PlayerUnits.Contains(initUnit))
			{
				_activePlayerUnit = initUnit;
			}
			else
			{
				_activePlayerUnit = _combatManager.PlayerUnits.FirstOrDefault(u => IsInstanceValid(u) && u.CurrentHp > 0);
			}

			string unitName = _activePlayerUnit?.Data?.UnitName ?? "玩家";
			_combatUi.SetTurnText($"▶ {unitName} 的回合", new Color(0.2f, 0.6f, 1));
			_combatUi.SetActionBarVisible(true);
			_combatUi.UpdateUnitInfo(_activePlayerUnit);
			_combatUi.LogMessage($"轮到 {unitName} 行动。");
			_audioManager?.PlaySfxName("combat_turn_start");

			if (_activePlayerUnit != null)
			{
				ShowSelectedUnitHighlights();
				UpdateFov();
				// 相机聚焦到当前行动单位
				CenterCameraOnUnit(_activePlayerUnit);
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

			if (initUnit != null)
				CenterCameraOnUnit(initUnit);

			ExecuteAiTurnForUnit(initUnit);
		}
	}

	/// <summary>为单个敌方单位执行 AI 行动（先攻制）</summary>
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


			// 使用现有 AI 系统处理单个单位
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
		// 刷新战力条（AI 可能击杀了玩家单位）
		_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
		// 单位行动完毕，结束其回合
		_combatManager.EndCurrentTurn();
	}

	/// <summary>相机平滑聚焦到指定单位</summary>
	protected void CenterCameraOnUnit(Unit unit)
	{
		if (_camera == null || unit == null) return;
		var targetPos = unit.Position;
		_camera.Position = new Vector3(targetPos.X, _camera.Position.Y, targetPos.Z + _camera.Position.Y);
		ClampCameraPosition();
	}
	private void OnAiDone() { _aiController.AllActionsCompleted -= OnAiDone; _combatManager.EndCurrentTurn(); }

	private async void OnCombatEndedInternal(bool victory)
	{
		if (_combatEnded) return;
		_combatEnded = true;
		try
		{
			ClearHighlights();
			_combatUi.SetTurnText(victory ? "战斗胜利！" : "战斗失败！", victory ? Colors.Green : Colors.Red);
			_combatUi.SetActionBarVisible(false);

			// 播放胜利/失败音效与音乐
			if (_audioManager != null)
			{
				_audioManager.PlaySfxName(victory ? "combat_victory" : "combat_defeat");
				_audioManager.PlayScenarioBgm(
					victory ? BladeHex.Audio.AudioManager.Scenario.Victory : BladeHex.Audio.AudioManager.Scenario.Defeat,
					"default", 2.0f);
			}

			await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

			// 弹出结算面板
			int xp = 0, gold = 0;
			var lootNames = new System.Collections.Generic.List<string>();
			if (victory)
			{
				int enemyCount = _combatManager.EnemyUnits.Count;
				int avgLevel = enemyCount > 0
					? _combatManager.EnemyUnits.Sum(e => e.Data?.Level ?? 1) / System.Math.Max(1, enemyCount) : 1;
				xp = avgLevel * 25 + enemyCount * 15;
				gold = avgLevel * 10 + enemyCount * 8;
			}

			var resultPanel = new BladeHex.UI.Combat.BattleResultPanel();
			AddChild(resultPanel);
			resultPanel.Show(victory, xp, gold, lootNames.ToArray());
			resultPanel.ContinueClicked += () =>
			{
				EmitSignal(SignalName.CombatFinished, victory);
				HandleCombatEnd(victory);
			};
		}
		catch (Exception ex)
		{
			GD.PushError($"[CombatSceneBase] OnCombatEndedInternal: {ex.Message}");
		}
	}

	// ========== 行动选择 ==========

	protected virtual void OnActionSelected(string action)
	{
		_currentActionMode = action switch
		{
			"move" => ActionMode.Move,
			"attack" or "radial_attack" => ActionMode.Attack,
			"spell" => ActionMode.Spell,
			"item" => ActionMode.Item,
			_ => ActionMode.None,
		};
		ClearHighlights();

		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;

		switch (action)
		{
			case "move":
				if (_activePlayerUnit.CurrentAp < 1) { _combatUi.LogMessage("行动力不足。"); _currentActionMode = ActionMode.None; }
				else { _combatUi.LogMessage("选择移动：请点击高亮空地。"); HighlightMoveRange(_activePlayerUnit); }
				break;

			case "attack":
				var atkWeapon = _activePlayerUnit.GetMainHand() as WeaponData;
				int atkApCost = atkWeapon?.ApCost ?? 4;
				if (_activePlayerUnit.CurrentAp < atkApCost) { _combatUi.LogMessage("行动力不足。"); _currentActionMode = ActionMode.None; }
				else
				{
					string atkName = atkWeapon?.ItemName ?? "徒手";
					int range = atkWeapon?.RangeCells ?? 1;
					_combatUi.LogMessage($"选择攻击：当前武器【{atkName}】(射程 {range})。请点击红色高亮敌人。");
					HighlightAttackRange(_activePlayerUnit);
				}
				break;

			case "spell":
				if (_activePlayerUnit.Data?.KnownSpells == null || _activePlayerUnit.Data.KnownSpells.Count == 0)
				{ _combatUi.LogMessage("未学习任何法术。"); _currentActionMode = ActionMode.None; }
				else { _combatUi.LogMessage("打开法术选择面板..."); _combatUi.OpenSpellPanel(_activePlayerUnit, _spellManager); }
				break;

			case "item":
				if (_activePlayerUnit.Data?.Consumables == null || _activePlayerUnit.Data.Consumables.Count == 0)
				{ _combatUi.LogMessage("背包中没有消耗品。"); _currentActionMode = ActionMode.None; }
				else { _combatUi.LogMessage("选择物品：请点击相邻的友方单位或自身使用药水。"); HighlightRange(_activePlayerUnit, 1, new Color(0.2f, 0.9f, 0.4f, 0.4f)); }
				break;

			case "defend":
				if (_activePlayerUnit.Data != null)
				{
					_activePlayerUnit.Data.Runtime.IsDefending = true;
					_activePlayerUnit.HasActed = true;
					_combatUi.LogMessage("[color=cyan]进入防御模式！[/color] AC+2，免疫包夹。");
				}
				_currentActionMode = ActionMode.None;
				break;

			case "swap_weapon":
			case "switch_to_primary":
			case "switch_to_secondary":
				if (action == "switch_to_primary" && _activePlayerUnit.UsingPrimaryWeapon)
				{ _combatUi.LogMessage("已经在使用主手武器。"); }
				else if (action == "switch_to_secondary" && !_activePlayerUnit.UsingPrimaryWeapon)
				{ _combatUi.LogMessage("已经在使用副手武器。"); }
				else
				{
					if (action == "switch_to_primary") _activePlayerUnit.UsingPrimaryWeapon = false;
					else if (action == "switch_to_secondary") _activePlayerUnit.UsingPrimaryWeapon = true;
					_activePlayerUnit.SwitchWeaponSet();
					_combatUi.UpdateUnitInfo(_activePlayerUnit);
					var swWeapon = _activePlayerUnit.GetMainHand();
					_combatUi.LogMessage($"切换武器！当前武器为：【{swWeapon?.ItemName ?? "徒手"}】。");
				}
				// 无论是否切换，都显示当前武器的攻击范围
				HighlightAttackRange(_activePlayerUnit);
				_currentActionMode = ActionMode.Attack;
				return;

			case "end_turn":
				_combatUi.LogMessage("玩家结束回合。");
				_combatManager.EndCurrentTurn(); _currentActionMode = ActionMode.None;
				break;

			case "retreat":
				_combatUi.LogMessage("队伍选择了撤退...");
				OnCombatEndedInternal(false);
				break;

			case "radial_attack":
				if (_rightClickTarget != null && _activePlayerUnit != null)
				{ _currentActionMode = ActionMode.Attack; _ = HandleAttack(_rightClickTarget); _rightClickTarget = null; }
				break;

			case "build_ladder":
				if (_rightClickTarget != null && _activePlayerUnit != null && _rightClickTarget.Data != null)
				{
					var (canLadder, _) = BladeHex.Combat.SiegeActions.CanBuildLadder(
						_activePlayerUnit.GridPos, _rightClickTarget.Data, _rightClickTarget.GridPos, _activePlayerUnit.CurrentAp);
					if (canLadder)
					{
						_activePlayerUnit.CurrentAp -= BladeHex.Combat.SiegeActions.LadderApCost;
						bool completed = BladeHex.Combat.SiegeActions.BuildLadder(_rightClickTarget.Data);
						if (completed)
						{
							_rightClickTarget.Elevation = 1;
							_combatUi.LogMessage("[color=green]云梯架设完成！城墙可攀登。[/color]");
						}
						else
						{
							int progress = _rightClickTarget.Data.ladderProgress;
							_combatUi.LogMessage($"[color=yellow]云梯建设中 ({progress}/{BladeHex.Combat.SiegeActions.LadderRequiredSteps})...[/color]");
						}
						_combatUi.UpdateUnitInfo(_activePlayerUnit);
						ClearHighlights();
						if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
					}
					_rightClickTarget = null;
				}
				_currentActionMode = ActionMode.None;
				break;

			case "attack_gate":
				if (_rightClickTarget != null && _activePlayerUnit != null && _rightClickTarget.Data != null)
				{
					var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
					_activePlayerUnit.CurrentAp -= weapon?.ApCost ?? 4;
					bool destroyed = BladeHex.Combat.SiegeActions.DamageDestructible(_rightClickTarget.Data);
					if (destroyed)
					{
						_rightClickTarget.Elevation = 1;
						_combatUi.LogMessage($"[color=red]城门被破坏！[/color]");
					}
					else
					{
						_combatUi.LogMessage($"攻击城门（剩余 {_rightClickTarget.Data.durability}/{_rightClickTarget.Data.maxDurability} 次）");
					}
					_combatUi.UpdateUnitInfo(_activePlayerUnit);
					ClearHighlights();
					if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
					_rightClickTarget = null;
				}
				_currentActionMode = ActionMode.None;
				break;

			case "career_skill":
				if (_activePlayerUnit != null)
				{
					if (_rightClickTarget != null)
					{
						var careerResult = _combatManager.UseCareerSkill(_activePlayerUnit, _rightClickTarget.GridPos, _hexGrid);
						if (careerResult["success"].AsBool())
						{
							_combatUi.LogMessage($"[color=orange]释放职业技能！[/color]");
							CheckAndResolveUnitDeaths(careerResult);
						}
						else
						{
							_combatUi.LogMessage($"[color=red]{careerResult.GetValueOrDefault("reason", "失败").AsString()}[/color]");
						}
						_combatUi.UpdateUnitInfo(_activePlayerUnit);
						_rightClickTarget = null; ClearHighlights();
						if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
						_currentActionMode = ActionMode.None;
					}
					else
					{
						// 快捷栏点击职业技能：显示范围并保存选中状态
						_selectedSkillAction = "career_skill";
						_selectedSpell = null;
						OnActionHovered(action);
						_currentActionMode = ActionMode.Spell;
						return;
					}
				}
				break;

			default:
				// skill_xxx 格式
				if (action.StartsWith("skill_") && _activePlayerUnit != null)
				{
					if (_rightClickTarget != null)
					{
						// 有目标：执行技能
						string skillEffect = action["skill_".Length..];
						var skillResult = _combatManager.UseSkill(_activePlayerUnit, skillEffect, _rightClickTarget.GridPos, _hexGrid);
						if (skillResult["success"].AsBool())
						{
							_combatUi.LogMessage($"[color=orange]释放技能！[/color]");
							CheckAndResolveUnitDeaths(skillResult);
						}
						else
						{
							_combatUi.LogMessage($"[color=red]{skillResult.GetValueOrDefault("reason", "失败").AsString()}[/color]");
						}
						_combatUi.UpdateUnitInfo(_activePlayerUnit);
						_rightClickTarget = null; ClearHighlights();
						if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
						_currentActionMode = ActionMode.None;
					}
					else
					{
						// 无目标（快捷栏点击）：显示技能范围并保存选中状态
						_selectedSkillAction = action;
						_selectedSpell = null;
						OnActionHovered(action);
						_currentActionMode = ActionMode.Spell;
						return;
					}
				}
				else
				{
					_currentActionMode = ActionMode.None;
				}
				break;
		}
	}

	protected void OnSpellSelected(SpellData spell)
	{
		if (_activePlayerUnit == null) return;
		_combatUi.CloseSpellPanel();
		_combatUi.LogMessage($"[color=orange]选择法术：{spell.SpellName}[/color] — 请点击射程内的目标。");
		_currentActionMode = ActionMode.Spell;
		_selectedSpell = spell;
		_selectedSkillAction = null;
		HighlightRange(_activePlayerUnit, spell.RangeCells, new Color(1, 0.5f, 0, 0.4f));
	}

	// ========== 格子点击 ==========

	protected void OnCellClicked(HexCell cell)
	{
		// 部署阶段：交由部署逻辑处理
		if (_deploymentPhaseActive)
		{
			HandleDeploymentClick(cell);
			return;
		}

		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;

		// 隐藏地形信息浮窗
		_terrainTooltip?.HidePanel();

		// 点击友方：选中（即使当前没有活跃单位也可以选择）
		if (cell.Occupant != null && _combatManager.PlayerUnits.Contains(cell.Occupant) && cell.Occupant.CurrentHp > 0)
		{ SelectUnit(cell.Occupant); return; }

		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;

		// 法术/物品瞄准模式
		if (_currentActionMode == ActionMode.Spell) { _ = HandleSpell(cell); return; }
		if (_currentActionMode == ActionMode.Item) { HandleItem(cell); return; }

		// 攻击模式：点击高亮范围内的敌人 → 执行攻击
		if (_currentActionMode == ActionMode.Attack)
		{
			if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant))
			{
				_ = HandleAttack(cell);
				return;
			}
			// 点击非敌人格 → 取消攻击模式
			_currentActionMode = ActionMode.None;
			ClearHighlights();
			ShowSelectedUnitHighlights();
			return;
		}

		// 点击敌方(非攻击模式):直接发起攻击(如果在射程内且有行动力)
		if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant))
		{
			var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
			int effectiveRange = weapon?.RangeCells ?? 1;
			int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);
			int apCost = weapon?.ApCost ?? 4;

			if (dist <= effectiveRange && _activePlayerUnit.CurrentAp >= apCost)
			{
				// 射程内且有行动力 → 直接攻击;视线惩罚由 CombatResolver 自动累计
				_currentActionMode = ActionMode.Attack;
				HighlightAttackRange(_activePlayerUnit);
				_ = HandleAttack(cell);
			}
			else
			{
				// 超出射程或行动力不足 → 仅显示日志提示,不切换选中目标
				if (dist > effectiveRange)
					_combatUi.LogMessage($"目标超出射程:{cell.Occupant.Data?.UnitName ?? "未知"} (距离 {dist},射程 {effectiveRange})");
				else
					_combatUi.LogMessage($"行动力不足:{cell.Occupant.Data?.UnitName ?? "未知"} (需要 {apCost},当前 {_activePlayerUnit.CurrentAp:F0})");
			}
			return;
		}

		// 点击空地 → 移动判定。
		// 优先级 1: 在移动高亮范围内 → A* 寻路移动
		// 优先级 2(兜底): 在 AP 范围内但高亮被攻击模式 / 其它路径覆盖了 → 走一次主动寻路,只要 path cost ≤ AP 就移动
		// 这避免了"刚切到攻击模式后想移动需要先取消再点击两次"的体验问题
		if (cell.Occupant == null && _activePlayerUnit.CurrentAp >= 1)
		{
			// 优先级 1
			if (_highlightedCells.Contains(cell))
			{
				HandleMove(cell);
				return;
			}

			// 优先级 2:主动寻路兜底(攻击模式遗留高亮 / 高亮没初始化等场景)
			if (cell.Data == null || cell.Data.isPassable)
			{
				var path = _hexGrid.FindPath(_activePlayerUnit.GridPos, cell.GridPos);
				if (path != null && path.Count >= 2)
				{
					float pathCost = _hexGrid.GetPathCost(_activePlayerUnit.GridPos, path);
					if (pathCost <= _activePlayerUnit.CurrentAp)
					{
						// 切到普通模式后正式移动
						_currentActionMode = ActionMode.None;
						ClearHighlights();
						HandleMove(cell);
						return;
					}
				}
			}

			// AP 不够 / 路径不可达 → 重新显示移动范围作为提示
			if (_currentActionMode == ActionMode.None)
				ShowSelectedUnitHighlights();
		}
	}

	protected void OnCellRightClicked(HexCell cell)
	{
		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;

		// 长按检视触发后不执行轮盘
		if (_longPressTriggered) return;

		// 记录长按目标格（用于 _Process 中的计时检测）
		if (cell.Occupant != null)
		{
			_longPressCell = cell;
			_longPressTimer = 0;
			_longPressTriggered = false;
		}

		// 关闭检视面板
		_combatUi.HideUnitInspect();

		// 瞄准模式右键取消
		if (_currentActionMode == ActionMode.Spell || _currentActionMode == ActionMode.Item)
		{
			_currentActionMode = ActionMode.None; ClearHighlights();
			if (_activePlayerUnit != null) ShowSelectedUnitHighlights();
			_combatUi.LogMessage("取消操作。");
			return;
		}

		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;

		// 右键敌人：轮盘菜单
		if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant))
		{
			var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
			int apCost = weapon?.ApCost ?? 4;
			if (_activePlayerUnit.CurrentAp < apCost) { _combatUi.LogMessage($"行动力不足（需要 {apCost}，当前 {_activePlayerUnit.CurrentAp:F0}）。"); return; }
			_rightClickTarget = cell;

			var opts = new Godot.Collections.Dictionary();
			int range = weapon?.RangeCells ?? 1;
			int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);
			// 路径上的命中惩罚（地形 + 中间单位），用于轮盘菜单显示
			int losPenalty = LineOfSight.GetPathPenalty(
				_activePlayerUnit.GridPos, cell.GridPos, _hexGrid, _activePlayerUnit, cell.Occupant);
			if (dist <= range)
			{
				string label = $"⚔ 攻击({weapon?.ItemName ?? "徒手"})";
				if (losPenalty < 0) label += $" [{losPenalty} 命中]";
				opts[label] = "radial_attack";
			}
			else opts[$"超出射程({dist}/{range})"] = "none";

			if (_activePlayerUnit.Data?.KnownSpells != null && _activePlayerUnit.Data.KnownSpells.Count > 0)
				opts["✨ 法术"] = "spell";

			if (_activePlayerUnit.SkillTree != null)
			{
				var activeSkills = _activePlayerUnit.SkillTree.GetActiveSkills();
				foreach (var skillNode in activeSkills)
				{
					if (string.IsNullOrEmpty(skillNode.SkillEffect)) continue;
					opts[$"⚡ {skillNode.NodeName}"] = $"skill_{skillNode.SkillEffect}";
				}
				var careerSkill = _activePlayerUnit.GetCareerSkill();
				if (careerSkill != null && careerSkill.ApCost > 0)
					opts[$"🎯 {careerSkill.DisplayName}"] = "career_skill";
			}

			opts["🛡 防御"] = "defend";
			opts["✕ 取消"] = "none";

			var screenPos = GetViewport().GetCamera3D()?.UnprojectPosition(cell.Occupant.Position + Vector3.Up * 80f) ?? Vector2.Zero;
			_combatUi.OpenRadialMenuCustom(screenPos, opts);
			return;
		}

		// 右键空地：显示地形信息
		if (cell.Data != null)
		{
			// 攻城交互：城墙 → 架设云梯选项
			if (cell.Data.terrainType == BattleCellData.TerrainType.Rampart && !cell.Data.HasLadder)
			{
				var (canLadder, ladderReason) = BladeHex.Combat.SiegeActions.CanBuildLadder(
					_activePlayerUnit.GridPos, cell.Data, cell.GridPos, _activePlayerUnit.CurrentAp);
				if (canLadder)
				{
					var opts = new Godot.Collections.Dictionary();
					int progress = cell.Data.ladderProgress;
					opts[$"🪜 架设云梯 ({progress}/3) [-8AP]"] = "build_ladder";
					opts["✕ 取消"] = "none";
					_rightClickTarget = cell;
					var screenPos = GetViewport().GetMousePosition();
					_combatUi.OpenRadialMenuCustom(screenPos, opts);
					return;
				}
				else if (!string.IsNullOrEmpty(ladderReason))
				{
					_combatUi.LogMessage(ladderReason);
				}
			}

			// 攻城交互：城门 → 攻击破坏选项
			if (cell.Data.isDestructible && cell.Data.durability > 0)
			{
				var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
				int apCost = weapon?.ApCost ?? 4;
				var (canAttack, attackReason) = BladeHex.Combat.SiegeActions.CanAttackDestructible(
					_activePlayerUnit.GridPos, cell.Data, cell.GridPos, _activePlayerUnit.CurrentAp, apCost);
				if (canAttack)
				{
					var opts = new Godot.Collections.Dictionary();
					opts[$"⚔ 攻击城门 ({cell.Data.durability}/{cell.Data.maxDurability}HP)"] = "attack_gate";
					opts["✕ 取消"] = "none";
					_rightClickTarget = cell;
					var screenPos = GetViewport().GetMousePosition();
					_combatUi.OpenRadialMenuCustom(screenPos, opts);
					return;
				}
				else if (!string.IsNullOrEmpty(attackReason))
				{
					_combatUi.LogMessage(attackReason);
				}
			}

			var terrainScreenPos = GetViewport().GetMousePosition();
			_terrainTooltip ??= new BladeHex.UI.Combat.TerrainTooltip();
			if (!_terrainTooltip.IsInsideTree())
				_combatUi.AddChild(_terrainTooltip);
			_terrainTooltip.ShowTerrain(cell.Data, terrainScreenPos);
		}
		_currentActionMode = ActionMode.None;
		ClearHighlights();
		if (_activePlayerUnit != null && IsInstanceValid(_activePlayerUnit) && _activePlayerUnit.CurrentAp >= 1)
			ShowSelectedUnitHighlights();
	}

	// ========== 移动/攻击/法术/物品处理 ==========

	private void HandleMove(HexCell cell)
	{
		// 注:不再要求 cell 必须在 _highlightedCells 内(攻击模式遗留高亮 / 兜底寻路场景下,
		// 上层 OnCellClicked 已经做过路径与 AP 检查)。这里只做最终安全检查。
		if (cell.Occupant != null) return;
		if (cell.Data != null && !cell.Data.isPassable) { _combatUi.LogMessage("目标不可通行。"); return; }

		var path = _hexGrid.FindPath(_activePlayerUnit!.GridPos, cell.GridPos);
		if (path == null || path.Count < 1) { _combatUi.LogMessage("无法到达该位置。"); return; }

		float pathCost = _hexGrid.GetPathCost(_activePlayerUnit.GridPos, path);
		if (pathCost > _activePlayerUnit.CurrentAp) { _combatUi.LogMessage("行动力不足。"); return; }

		var moveCmd = new BladeHex.Combat.Commands.MoveCommand(
			(long)_activePlayerUnit.GetInstanceId(), _activePlayerUnit.GridPos, path);
		var cmdResult = _combatManager.ExecuteCommand(moveCmd);

		if (cmdResult.Success)
		{
			MoveUnitTo(_activePlayerUnit, cell.GridPos.X, cell.GridPos.Y);
			_combatUi.LogMessage($"移动到 {cell.GridPos} (消耗 {(int)pathCost} 行动力)");
			_combatUi.UpdateUnitInfo(_activePlayerUnit);
		}

		_currentActionMode = ActionMode.None; ClearHighlights();
		if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
	}

	private async Task HandleAttack(HexCell cell)
	{
		// 验证攻击目标有效性
		if (cell.Occupant == null || cell.Occupant == _activePlayerUnit)
		{ _combatUi.LogMessage("无效的攻击目标。"); return; }

		// 如果不在高亮范围内，检查是否在实际射程内（支持轮盘直接攻击）
		if (!_highlightedCells.Contains(cell))
		{
			var weapon = _activePlayerUnit!.GetMainHand() as WeaponData;
			int weaponRange = weapon?.RangeCells ?? 1;
			int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);
			if (dist > weaponRange)
			{ _combatUi.LogMessage("目标超出攻击射程。"); return; }
			// 视线惩罚由 CombatResolver 自动累计；这里不再硬性阻挡
		}

		var target = cell.Occupant;
		var attackWeapon = _activePlayerUnit!.GetMainHand() as WeaponData;
		int apCost = attackWeapon?.ApCost ?? 4;
		if (_activePlayerUnit.CurrentAp < apCost)
		{ _combatUi.LogMessage($"行动力不足（需要 {apCost}）。"); _currentActionMode = ActionMode.None; ClearHighlights(); return; }

		// 记录攻击命令到历史（用于录像/回放）
		var atkCmd = new AttackCommand(
			(long)_activePlayerUnit.GetInstanceId(),
			(long)target.GetInstanceId());
		_combatManager.ExecuteCommand(atkCmd);

		_activePlayerUnit.ConsumeAp(apCost);
		// 攻击动画编排（远程=投射物飞行，近战=突刺）
		await _attackAnimator.PlayAttack(_activePlayerUnit, target, attackWeapon);

		// 包围加成需要传入攻击者同阵营单位
		var allies = _combatManager.PlayerUnits
			.Where(u => IsInstanceValid(u) && u != _activePlayerUnit && u.CurrentHp > 0)
			.ToArray();
		var result = CombatResolver.ResolveAttack(_activePlayerUnit, target, _hexGrid,
			attackerAllies: allies);

		if (result["hit"].AsBool())
		{
			int dmg = result["damage"].AsInt32();
			bool isCrit = result["critical"].AsBool();
			string critMsg = isCrit ? " [color=yellow]暴击！[/color]" : "";
			string flankMsg = result.ContainsKey("is_flanking") && result["is_flanking"].AsBool() ? " [包夹]" : "";

			var weapon = _activePlayerUnit.GetMainHand();
			int dmgType = weapon is WeaponData wd ? (int)wd.WeaponDamageType : 0;
			_audioManager?.PlayAttackHitSfx(dmgType, isCrit);

			// 伤害数字
			BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(this, target, dmg, isCrit);

			_combatUi.LogMessage($"[color=green]命中！[/color]{critMsg}{flankMsg} 使用 {weapon?.ItemName ?? "徒手"} 造成 {dmg} 伤害。");
			_combatUi.UpdateEnemyInfo(target);
			if (target.CurrentHp <= 0)
			{
				_audioManager?.PlaySfxName("combat_death");
				_combatUi.LogMessage($"[color=yellow]{target.Data?.UnitName} 被击败！[/color]");
				_combatUi.RemoveEnemy(target); cell.Occupant = null;
				_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
			}
		}
		else
		{
			if (result["fumble"].AsBool())
			{
				_audioManager?.PlaySfxName("combat_fumble");
				_combatUi.LogMessage("[color=red]严重失误！[/color]");
				BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(this, target, 0, missLabel: "Fumble");
			}
			else
			{
				var w2 = _activePlayerUnit.GetMainHand();
				int missDmgType = w2 is WeaponData wd2 ? (int)wd2.WeaponDamageType : 0;
				_audioManager?.PlayAttackMissSfx(missDmgType);
				_combatUi.LogMessage($"[color=red]未命中！[/color] (命中率 {result["hit_chance_percent"].AsInt32()}%)");
				BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(this, target, 0, missLabel: "Miss");
				// 闪避微动画
				target.PlayDodgeBack(_activePlayerUnit.GlobalPosition);
			}
		}

		_activePlayerUnit.PlayAnim("default");
		_currentActionMode = ActionMode.None; ClearHighlights();
		_combatUi.UpdateUnitInfo(_activePlayerUnit);
		_combatMinimap?.Refresh();
		if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
	}

	private void CheckAndResolveUnitDeaths(Godot.Collections.Dictionary skillResult)
	{
		if (skillResult == null || !skillResult.ContainsKey("results")) return;
		var results = skillResult["results"].AsGodotArray();
		if (results == null) return;

		foreach (var rVar in results)
		{
			if (rVar.VariantType != Variant.Type.Dictionary) continue;
			var r = rVar.AsGodotDictionary();
			if (r == null) continue;

			var target = r.ContainsKey("target") ? r["target"].As<Unit>() : null;
			if (target != null && GodotObject.IsInstanceValid(target) && target.CurrentHp <= 0)
			{
				_audioManager?.PlaySfxName("combat_death");
				_combatUi.LogMessage($"[color=yellow]{target.Data?.UnitName} 被击败！[/color]");
				_combatUi.RemoveEnemy(target);
				var tcell = _hexGrid.GetCell(target.GridPos.X, target.GridPos.Y);
				if (tcell != null) tcell.Occupant = null;
				_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
			}
		}
	}

	private async Task HandleSpell(HexCell cell)
	{
		if (_activePlayerUnit == null) return;

		// 1. 处理快捷栏技能与职业技能
		if (!string.IsNullOrEmpty(_selectedSkillAction))
		{
			if (_highlightedCells.Contains(cell))
			{
				string action = _selectedSkillAction;
				if (action.StartsWith("skill_"))
				{
					string skillEffect = action["skill_".Length..];
					
					_activePlayerUnit.PlayAnim("attack");
					await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.6);

					// 播放对应的音效与粒子特效
					string sfxName = "skill_slash";
					var cfg = SkillEffectExecutor.GetSkillConfig(skillEffect);
					if (cfg.ContainsKey("vfx") && cfg["vfx"].AsString().Contains("fire"))
						sfxName = "skill_fire";
					else if (skillEffect.Contains("heal") || skillEffect.Contains("cure"))
						sfxName = "skill_heal";
					
					_audioManager?.PlaySfxName(sfxName);
					VFXManager.PlayExplosionEffect(this, cell.GlobalPosition);

					var skillResult = _combatManager.UseSkill(_activePlayerUnit, skillEffect, cell.GridPos, _hexGrid);
					if (skillResult["success"].AsBool())
					{
						_combatUi.LogMessage($"[color=orange]释放技能！[/color]");
						if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant))
							_combatUi.UpdateEnemyInfo(cell.Occupant);
						CheckAndResolveUnitDeaths(skillResult);
					}
					else
					{
						string reason = skillResult.GetValueOrDefault("reason", "失败").AsString();
						_combatUi.LogMessage($"[color=red]技能失败：{reason}[/color]");
					}
				}
				else if (action == "career_skill")
				{
					_activePlayerUnit.PlayAnim("attack");
					await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.6);

					_audioManager?.PlaySfxName("skill_slash");
					VFXManager.PlayExplosionEffect(this, cell.GlobalPosition);

					var careerResult = _combatManager.UseCareerSkill(_activePlayerUnit, cell.GridPos, _hexGrid);
					if (careerResult["success"].AsBool())
					{
						_combatUi.LogMessage($"[color=orange]释放职业技能！[/color]");
						if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant))
							_combatUi.UpdateEnemyInfo(cell.Occupant);
						CheckAndResolveUnitDeaths(careerResult);
					}
					else
					{
						string reason = careerResult.GetValueOrDefault("reason", "失败").AsString();
						_combatUi.LogMessage($"[color=red]释放失败：{reason}[/color]");
					}
				}

				_activePlayerUnit.PlayAnim("default");
				_selectedSkillAction = null;
				_currentActionMode = ActionMode.None;
				ClearHighlights();
				_combatUi.UpdateUnitInfo(_activePlayerUnit);
				_combatMinimap?.Refresh();
				if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
			}
			else
			{
				_combatUi.LogMessage("目标点不在范围目标内。");
				_selectedSkillAction = null;
				_currentActionMode = ActionMode.None;
				ClearHighlights();
			}
			return;
		}

		// 2. 处理原有的法术面板里的法术
		if (_selectedSpell != null && _highlightedCells.Contains(cell))
		{
			_activePlayerUnit!.PlayAnim("attack");
			await BladeHex.View.Combat.CombatSpeed.ScaledWait(this, 0.6);

			string spellSchool = _selectedSpell.DamageType ?? "arcane";
			_audioManager?.PlaySpellCastSfx(spellSchool);
			VFXManager.PlayExplosionEffect(this, cell.GlobalPosition);

			var result = _spellManager.CastSpell(_activePlayerUnit, _selectedSpell, cell.GridPos, _hexGrid);
			if (result["success"].AsBool())
			{
				var results = result["results"].AsGodotArray();
				if (results != null)
				{
					foreach (var rVar in results)
					{
						var r = rVar.AsGodotDictionary();
						if (r == null) continue;
						if (r.ContainsKey("hit") && r["hit"].AsBool())
						{
							if (r.ContainsKey("healed") && r["healed"].AsBool())
							{
								var healTarget = r["target"].As<Unit>();
								_audioManager?.PlaySfxName("skill_heal");
								_combatUi.LogMessage($"[color=cyan]{healTarget?.Data?.UnitName} 被治疗了 {r["amount"].AsInt32()} HP。[/color]");
							}
							else
							{
								var spellTarget = r["target"].As<Unit>();
								_audioManager?.PlaySpellImpactSfx(spellSchool);
								_combatUi.LogMessage($"[color=orange]法术命中 {spellTarget?.Data?.UnitName}！造成 {r.GetValueOrDefault("damage", 0).AsInt32()} 伤害。[/color]");
								if (spellTarget != null) _combatUi.UpdateEnemyInfo(spellTarget);
								if (spellTarget?.CurrentHp <= 0)
								{
									_audioManager?.PlaySfxName("combat_death");
									_combatUi.LogMessage($"[color=yellow]{spellTarget.Data?.UnitName} 被击败！[/color]");
									_combatUi.RemoveEnemy(spellTarget);
									var tcell = _hexGrid.GetCell(spellTarget.GridPos.X, spellTarget.GridPos.Y);
									if (tcell != null) tcell.Occupant = null;
									_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
								}
							}
						}
						else
						{
							var missTarget = r.ContainsKey("target") ? r["target"].As<Unit>() : null;
							_combatUi.LogMessage($"[color=red]法术未命中 {missTarget?.Data?.UnitName ?? "目标"}。[/color]");
						}
					}
				}
				_combatUi.LogMessage($"[color=orange]释放【{_selectedSpell.SpellName}】。[/color]");
			}
			else
			{
				string reason = result.ContainsKey("reason") ? result["reason"].AsString() : "未知原因";
				_combatUi.LogMessage($"[color=red]施法失败：{reason}[/color]");
			}

			_activePlayerUnit.PlayAnim("default");
			_selectedSpell = null; _activePlayerUnit.HasActed = true;
			_currentActionMode = ActionMode.None; ClearHighlights();
		}
		else
		{
			_combatUi.LogMessage("目标点不在射程内。");
			_selectedSpell = null; _currentActionMode = ActionMode.None; ClearHighlights();
		}
	}

	private void HandleItem(HexCell cell)
	{
		if (!_highlightedCells.Contains(cell)) return;
		if (cell.Occupant == null || cell.Occupant.Data?.IsEnemy != false)
		{ _combatUi.LogMessage("无效的目标。"); return; }

		var target = cell.Occupant;
		var consumables = _activePlayerUnit!.Data?.Consumables;
		ConsumableData? potion = null;
		if (consumables != null)
			foreach (var c in consumables)
				if (c.consumableType == ConsumableData.ConsumableType.HealingPotion) { potion = c; break; }

		if (potion == null) { _combatUi.LogMessage("没有可用的治疗药水。"); _currentActionMode = ActionMode.None; ClearHighlights(); return; }

		var result = ConsumableManager.UseConsumable(target, potion, cell.GridPos, _hexGrid);
		if (result["success"].AsBool())
		{
			_combatUi.LogMessage($"[color=green]{target.Data?.UnitName} 使用了{potion.ItemName}，恢复 {result["amount"].AsInt32()} HP。[/color]");
			_combatUi.UpdateUnitInfo(target);
			_activePlayerUnit.HasActed = true;
		}
		else _combatUi.LogMessage("使用失败。");

		_currentActionMode = ActionMode.None; ClearHighlights();
	}

	// ========== 悬停预览 ==========

	// 路径预览线
	private MeshInstance3D? _pathPreviewLine;
	private List<Vector2I>? _previewPath;

	// 悬浮高亮
	private HexCell? _currentHoverCell;
	private Decal? _hoverDecal;

	protected void OnCellHover(HexCell cell)
	{
		// 悬浮轮廓:任何 cell 都显示暗金色六边形边框
		ShowHoverOutline(cell);

		if (_combatManager == null) return;
		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;
		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;

		// 敌人悬浮:攻击预览
		if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant) && cell.Occupant.Visible)
		{
			ClearPathPreview();
			// 仅在未处于施法或物品模式时，悬浮敌人才显示常规物理武器攻击范围
			if (_currentActionMode == ActionMode.None || _currentActionMode == ActionMode.Attack)
			{
				var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
				int weaponRange = weapon?.RangeCells ?? 1;
				int apCost = weapon?.ApCost ?? 4;
				int effectiveRange = weaponRange;
				int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);

				if (!_attackRangeShownForHover)
				{
					ShowAttackRangeOverlay(_activePlayerUnit);
					_attackRangeShownForHover = true;
				}

				if (dist <= effectiveRange && _activePlayerUnit.CurrentAp >= apCost)
				{
					cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.5f));
					_hoverHighlightedCell = cell;
				}

				var mousePos = GetViewport().GetMousePosition();
				if (dist <= effectiveRange)
					_combatUi.ShowHitPreview(mousePos, _activePlayerUnit, cell.Occupant);
				else
					_combatUi.ShowOutOfRangePreview(mousePos, cell.Occupant, dist, effectiveRange);
			}
			return;
		}

		// 空地悬浮:移动路径预览（仅在普通/非瞄准模式下绘制移动折线线框）
		if (_currentActionMode == ActionMode.None && cell.Occupant == null && _activePlayerUnit.CurrentAp >= 1
			&& (cell.Data == null || cell.Data.isPassable))
		{
			var path = _hexGrid.FindPath(_activePlayerUnit.GridPos, cell.GridPos);
			if (path != null && path.Count >= 1)
			{
				float cost = _hexGrid.GetPathCost(_activePlayerUnit.GridPos, path);
				if (cost <= _activePlayerUnit.CurrentAp)
				{
					DrawPathPreview(path);
					_previewPath = path;
				}
				else
				{
					ClearPathPreview();
				}
			}
			else
			{
				ClearPathPreview();
			}
		}
		else
		{
			ClearPathPreview();
		}
	}

	protected void OnCellHoverExit(HexCell cell)
	{
		HideHoverOutline();
		_combatUi.HideHitPreview();
		if (_hoverHighlightedCell != null)
		{
			if (!_highlightedCells.Contains(_hoverHighlightedCell))
				_hoverHighlightedCell.SetHighlight(false);
			_hoverHighlightedCell = null;
		}

		if (_attackRangeShownForHover)
		{
			ClearAttackRangeOverlay();
			_attackRangeShownForHover = false;
		}

		ClearPathPreview();
	}

	private void DrawPathPreview(List<Vector2I> path)
	{
		ClearPathPreview();

		var mesh = new ImmediateMesh();
		mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

		float uiY = BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.UIHintLayer;

		// 起点(当前单位位置)
		var startCell = _hexGrid.GetCell(_activePlayerUnit!.GridPos.X, _activePlayerUnit.GridPos.Y);
		if (startCell != null)
			mesh.SurfaceAddVertex(startCell.Position + Vector3.Up * uiY);

		// 路径各点
		foreach (var coord in path)
		{
			var c = _hexGrid.GetCell(coord.X, coord.Y);
			if (c != null)
				mesh.SurfaceAddVertex(c.Position + Vector3.Up * uiY);
		}

		mesh.SurfaceEnd();

		if (_pathPreviewLine == null)
		{
			_pathPreviewLine = new MeshInstance3D();
			_pathPreviewLine.Name = "PathPreviewLine";
			var mat = new StandardMaterial3D();
			mat.AlbedoColor = new Color(0.3f, 0.9f, 0.4f, 0.8f);
			mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			mat.NoDepthTest = true;
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			_pathPreviewLine.MaterialOverride = mat;
			AddChild(_pathPreviewLine);
		}
		_pathPreviewLine.Mesh = mesh;
		_pathPreviewLine.Visible = true;
	}

	private void ClearPathPreview()
	{
		_previewPath = null;
		if (_pathPreviewLine != null)
			_pathPreviewLine.Visible = false;
	}

	// ========== 悬浮六边形轮廓 ==========

	private void ShowHoverOutline(HexCell cell)
	{
		if (_currentHoverCell == cell) return;
		HideHoverOutline();
		_currentHoverCell = cell;

		if (_hoverDecal == null)
		{
			_hoverDecal = new Decal();
			_hoverDecal.Name = "HoverDecal";

			var tex = BladeHex.View.Combat.HexHoverTextureGenerator.Get();
			_hoverDecal.TextureAlbedo = tex;
			_hoverDecal.TextureEmission = tex;

			float hexDiameter = HexUtils.Size * 2.0f;
			_hoverDecal.Size = new Vector3(hexDiameter, 80f, hexDiameter);

			_hoverDecal.Modulate = new Color(1.0f, 0.8f, 0.35f, 0.45f);
			_hoverDecal.EmissionEnergy = 0.8f;
			_hoverDecal.AlbedoMix = 0.3f;
			_hoverDecal.UpperFade = 0.0f;
			_hoverDecal.LowerFade = 0.0f;
			_hoverDecal.TextureNormal = null;
			_hoverDecal.RotationDegrees = new Vector3(0, 30, 0);

			AddChild(_hoverDecal);
		}

		// Decal 位置：在 hex 正上方，向下投射覆盖顶面和纹理层
		float decalY = cell.Position.Y + BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + 30f;
		_hoverDecal.Position = new Vector3(cell.Position.X, decalY, cell.Position.Z);
		_hoverDecal.Visible = true;
	}

	private void HideHoverOutline()
	{
		_currentHoverCell = null;
		if (_hoverDecal != null)
			_hoverDecal.Visible = false;
	}

	private void UpdateHoverPulse()
	{
		if (_hoverDecal == null || !_hoverDecal.Visible) return;
		float t = (float)Time.GetTicksMsec() / 1000f;
		float pulse = 0.7f + 0.3f * Mathf.Sin(t * 2.5f);
		_hoverDecal.Modulate = new Color(1.0f, 0.8f, 0.35f, pulse * 0.45f);
		_hoverDecal.EmissionEnergy = 0.5f + 0.3f * Mathf.Sin(t * 2.5f);
	}

	// ========== 轮盘悬浮预览 ==========

	private void OnActionHovered(string action)
	{
		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;
		ClearHighlights();

		switch (action)
		{
			case "radial_attack":
				HighlightAttackRange(_activePlayerUnit);
				break;
			case "defend":
			case "none":
				var selfCell = _hexGrid.GetCell(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y);
				if (selfCell != null) { selfCell.SetHighlight(true, new Color(0.3f, 0.6f, 1.0f, 0.5f)); _highlightedCells.Add(selfCell); }
				break;
			case "spell":
				int maxSpellRange = 1;
				if (_activePlayerUnit.Data?.KnownSpells != null)
					foreach (var spell in _activePlayerUnit.Data.KnownSpells)
						if (spell != null && spell.RangeCells > maxSpellRange) maxSpellRange = spell.RangeCells;
				foreach (var coord in _hexGrid.GetCellsInRange(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y, maxSpellRange))
				{ var c = _hexGrid.GetCell(coord.X, coord.Y); if (c != null) { c.SetHighlight(true, new Color(0.6f, 0.3f, 0.9f, 0.35f)); _highlightedCells.Add(c); } }
				break;
			case "career_skill":
				var career = _activePlayerUnit.GetCareerSkill();
				int careerRange = career?.EffectParams.ContainsKey("range") == true ? career.EffectParams["range"].AsInt32() : 1;
				foreach (var coord in _hexGrid.GetCellsInRange(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y, careerRange))
				{ var c = _hexGrid.GetCell(coord.X, coord.Y); if (c != null) { c.SetHighlight(true, new Color(1.0f, 0.5f, 0.2f, 0.4f)); _highlightedCells.Add(c); } }
				break;
			default:
				if (action.StartsWith("skill_"))
				{
					string skillEffect = action["skill_".Length..];
					var cfg = SkillEffectExecutor.GetSkillConfig(skillEffect);
					int skillRange = cfg.ContainsKey("range") ? cfg["range"].AsInt32() : 1;
					int aoeRadius = cfg.ContainsKey("aoe_radius") ? cfg["aoe_radius"].AsInt32() : 0;
					string targetType = cfg.ContainsKey("target") ? cfg["target"].AsString() : "";

					// 根据目标类型选择高亮颜色
					Color hlColor = targetType switch
					{
						"SingleEnemy" or "RangedSingle" => new Color(0.9f, 0.3f, 0.2f, 0.35f),  // 红：单体敌人
						"RangedAoe" or "AoeSmall" or "AoeCone" => new Color(0.9f, 0.5f, 0.1f, 0.35f), // 橙：AOE
						"AllAdjacent" => new Color(0.9f, 0.7f, 0.2f, 0.4f), // 黄：周围
						"SingleAlly" or "AllAllies" => new Color(0.2f, 0.8f, 0.4f, 0.35f), // 绿：友军
						_ => new Color(0.9f, 0.7f, 0.2f, 0.4f), // 默认黄
					};

					// Self 类技能：只高亮自身
					if (targetType == "Self" || skillRange == 0)
					{
						var selfC = _hexGrid.GetCell(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y);
						if (selfC != null) { selfC.SetHighlight(true, new Color(0.3f, 0.6f, 1.0f, 0.5f)); _highlightedCells.Add(selfC); }
					}
					else
					{
						// 高亮施法范围（可选中的目标区域）
						foreach (var coord in _hexGrid.GetCellsInRange(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y, skillRange))
						{
							var c = _hexGrid.GetCell(coord.X, coord.Y);
							if (c != null) { c.SetHighlight(true, hlColor); _highlightedCells.Add(c); }
						}

						// 如果有 AOE 半径，用更亮的颜色标注中心点周围的溅射范围提示
						// （实际溅射范围在选中目标后再显示，这里只显示施法范围）
					}
				}
				break;
		}
	}

	// ========== 视角控制 ==========

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			switch (keyEvent.Keycode)
			{
				case Key.Space:
					if (_combatManager.CurrentState == CombatManager.CombatState.PlayerTurn)
					{ _combatUi.LogMessage("玩家结束回合。"); _currentActionMode = ActionMode.None; ClearHighlights(); _combatManager.EndCurrentTurn(); }
					return;
				case Key.Escape:
					// 1. 若全局菜单已开 → 让它自己处理
					var gameMenu = BladeHex.Data.Globals.GameMenuOrNull;
					if (gameMenu != null && gameMenu.IsOpen)
						return;
					// 2. 取消当前行动模式
					if (_currentActionMode != ActionMode.None)
					{ _currentActionMode = ActionMode.None; ClearHighlights(); if (_activePlayerUnit != null) ShowSelectedUnitHighlights(); _combatUi.LogMessage("取消操作。"); }
					// 3. 取消选中单位
					else if (_activePlayerUnit != null) { _activePlayerUnit = null; ClearHighlights(); }
					// 4. 都没有 → 打开全局系统菜单
					else { gameMenu?.Toggle(); GetViewport().SetInputAsHandled(); }
					return;
				case Key.Tab:
					CycleNextPlayerUnit();
					return;
				// 快捷技能槽 1-0(共 10 个)
				case Key.Key1: _combatUi.TriggerQuickSlot(0); return;
				case Key.Key2: _combatUi.TriggerQuickSlot(1); return;
				case Key.Key3: _combatUi.TriggerQuickSlot(2); return;
				case Key.Key4: _combatUi.TriggerQuickSlot(3); return;
				case Key.Key5: _combatUi.TriggerQuickSlot(4); return;
				case Key.Key6: _combatUi.TriggerQuickSlot(5); return;
				case Key.Key7: _combatUi.TriggerQuickSlot(6); return;
				case Key.Key8: _combatUi.TriggerQuickSlot(7); return;
				case Key.Key9: _combatUi.TriggerQuickSlot(8); return;
				case Key.Key0: _combatUi.TriggerQuickSlot(9); return;
			}
		}

		if (@event is InputEventMouseButton mb && mb.Pressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
			{
				_camera.Size = Mathf.Clamp(_camera.Size * 0.9f, MinOrthoSize, _maxOrthoSize);
			}
			else if (mb.ButtonIndex == MouseButton.WheelDown)
			{
				_camera.Size = Mathf.Clamp(_camera.Size * 1.1f, MinOrthoSize, _maxOrthoSize);
				// 缩小到最大时，自动居中相机到战场中心
				if (_camera.Size >= _maxOrthoSize)
				{
					var center = _battlefieldBounds.Position + _battlefieldBounds.Size * 0.5f;
					_camera.Position = new Vector3(center.X, _camera.Position.Y, center.Z + _camera.Position.Y);
				}
			}
			ClampCameraPosition();
		}

		// 右键长按检视（0.8s 触发详情面板）
		if (@event is InputEventMouseButton rmb2 && rmb2.ButtonIndex == MouseButton.Right)
		{
			if (rmb2.Pressed)
			{
				_longPressCell = _hoverHighlightedCell;
				_longPressTimer = 0;
				_longPressTriggered = false;
			}
			else
			{
				if (_longPressTriggered)
				{
					_longPressTriggered = false;
					_longPressCell = null;
					GetViewport().SetInputAsHandled();
				}
				else
				{
					_longPressCell = null;
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_camera == null) return;

		// 悬浮魔法阵呼吸脉动
		UpdateHoverPulse();

		// 长按检视检测
		if (_longPressCell != null && !_longPressTriggered)
		{
			if (Input.IsMouseButtonPressed(MouseButton.Right))
			{
				_longPressTimer += delta;
				if (_longPressTimer >= LongPressDuration)
				{
					_longPressTriggered = true;
					if (_longPressCell.Occupant != null && IsInstanceValid(_longPressCell.Occupant))
					{
						var screenPos = GetViewport().GetMousePosition();
						_combatUi.ShowUnitInspect(_longPressCell.Occupant, screenPos);
					}
				}
			}
			else
			{
				// 松开鼠标 — 重置长按状态
				_longPressCell = null;
				_longPressTimer = 0;
			}
		}

		// 当缩放到最大（整个地图可见）时，锁定 WASD 移动
		if (_camera.Size >= _maxOrthoSize) return;

		float spd = 800 * (float)delta * (_camera.Size / 1000);
		var v = Vector3.Zero;
		if (Input.IsKeyPressed(Key.W)) v.Z -= 1;
		if (Input.IsKeyPressed(Key.S)) v.Z += 1;
		if (Input.IsKeyPressed(Key.A)) v.X -= 1;
		if (Input.IsKeyPressed(Key.D)) v.X += 1;
		if (v.Length() > 0)
		{
			_camera.Position += v.Normalized() * spd;
			ClampCameraPosition();
		}
	}

	/// <summary>限制相机位置在可平移边界内（外扩后的范围，比战场边界更大）</summary>
	private void ClampCameraPosition()
	{
		if (_camera == null) return;
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		_camera.Position = BladeHex.View.Camera.CameraBoundsClamp.Clamp3DOrtho(
			_camera.Position, _camera.Size, -45f, _cameraPanBounds, aspect,
			topRatio, bottomRatio);
		UpdateMinimapViewport();
	}

	private void UpdateMinimapViewport()
	{
		if (_combatMinimap == null || _camera == null) return;
		float xSpacing = HexUtils.HorizontalSpacing;
		float zSpacing = HexUtils.VerticalSpacing;
		if (xSpacing <= 0 || zSpacing <= 0) return;

		float camX = _camera.Position.X / xSpacing;
		float camZ = (_camera.Position.Z - _camera.Position.Y) / zSpacing;
		float halfViewW = _camera.Size * 0.5f / xSpacing;
		float halfViewH = _camera.Size * 0.35f / zSpacing;
		_combatMinimap.UpdateViewport(new Vector2(camX, camZ), new Vector2(halfViewW, halfViewH));
	}

	// ========== 镜头聚焦 ==========

	public async void FocusCameraOn(Vector3 targetWorldPos, float duration = 0.4f)
	{
		if (_camera == null) return;
		var currentPos = _camera.Position;
		var targetCamPos = new Vector3(targetWorldPos.X, currentPos.Y, targetWorldPos.Z + currentPos.Y * 0.7f);

		// 限制目标位置在可平移边界内（与 ClampCameraPosition 保持一致）
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		var (topRatio, bottomRatio) = GetUiInsetRatios();
		targetCamPos = BladeHex.View.Camera.CameraBoundsClamp.Clamp3DOrtho(
			targetCamPos, _camera.Size, -45f, _cameraPanBounds, aspect,
			topRatio, bottomRatio);

		if ((targetCamPos - currentPos).Length() < 100f) return;

		var tween = CreateTween();
		tween.TweenProperty(_camera, "position", targetCamPos, duration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		await ToSignal(tween, Tween.SignalName.Finished);
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

	/// <summary>从 EconomyManager 读当前小时(0~24),fallback 12(正午)</summary>
	private static float GetCurrentHour()
	{
		try
		{
			var tree = Engine.GetMainLoop() as SceneTree;
			var econ = tree?.Root?.GetNodeOrNull<BladeHex.Data.EconomyManager>("/root/EconomyManager");
			if (econ != null) return econ.CurrentHour;
		}
		catch { /* ignore */ }
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

		var cell = _hexGrid?.GetCell(dead.GridPos.X, dead.GridPos.Y);
		if (cell != null && cell.Occupant == dead)
			cell.Occupant = null;

		if (_combatUi != null && IsInstanceValid(_combatUi))
		{
			if (!dead.IsPlayerSide)
			{
				_combatUi.RemoveEnemy(dead);
			}
			else
			{
				_combatUi.UpdateUnitInfo(dead);
			}

			_combatUi.RefreshPowerBar(_combatManager.PlayerUnits, _combatManager.EnemyUnits);
		}

		_combatManager?.HandleUnitKilled(dead, killer);
	}
}
