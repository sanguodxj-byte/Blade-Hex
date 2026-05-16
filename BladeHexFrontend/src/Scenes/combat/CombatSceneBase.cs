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
	protected CombatMinimapPanel _combatMinimap = null!;
	protected BladeHex.Audio.AudioManager? _audioManager;

	// ========== 状态 ==========
	protected ActionMode _currentActionMode = ActionMode.None;
	protected Unit? _activePlayerUnit;
	protected readonly List<HexCell> _highlightedCells = new();
	protected bool _combatEnded;
	protected int _mapWidth = 12;
	protected int _mapHeight = 10;
	protected BattleMapGenerator.BattleMapData? _mapData;
	protected SpellData? _selectedSpell;
	private HexCell? _rightClickTarget;
	private HexCell? _hoverHighlightedCell;
	private bool _attackRangeShownForHover;
	private readonly List<HexCell> _attackRangeOverlayCells = new();

	// 长按检视状态
	private HexCell? _longPressCell;
	private double _longPressTimer;
	private const double LongPressDuration = 0.8; // 0.8秒触发检视
	private bool _longPressTriggered;

	// 战场世界边界（X/Z 范围），由 GenerateBattlefield 后计算
	protected Aabb _battlefieldBounds;
	// 相机正交尺寸限制
	private const float MinOrthoSize = 200f;
	private float _maxOrthoSize = 2000f;

	// ========== 常量 ==========
	private const int BaseMaxVision = 12;
	private const int HighGroundVisionBonus = 2;

	// ========== 子类必须实现 ==========
	protected abstract void GenerateBattlefield();
	protected abstract void SpawnUnits();
	protected abstract void HandleCombatEnd(bool victory);

	// ========== 子类可选覆盖 ==========
	protected virtual void OnPreBattleSetup() { }
	protected virtual void PlayCombatMusic() { }

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
			// 放置场景装饰精灵（树木、岩石等）
			_decorationPlacer.PlaceDecorations(_hexGrid, _mapData);
			// 初始化战斗小地图
			_combatMinimap.Initialize(_hexGrid, _combatManager, _mapWidth, _mapHeight);
			_combatUi.EmbedMinimap(_combatMinimap);
			OnPreBattleSetup();
			SpawnUnits();
			PlayCombatMusic();
			_combatManager.StartCombat();

			// 播放入场 UI 动画
			PlayEntranceTransition();
		}
		catch (Exception ex)
		{
			GD.PushError($"[{GetType().Name}] _Ready: {ex.Message}\n{ex.StackTrace}");
		}
	}

	/// <summary>
	/// 根据 _mapWidth/_mapHeight 计算战场世界 AABB（含边距）。
	/// 同时计算最大正交尺寸（保证完全缩小时仍能看到全部战场）。
	/// </summary>
	private void ComputeBattlefieldBounds()
	{
		float xSpacing = HexUtils.HorizontalSpacing;
		float zSpacing = HexUtils.VerticalSpacing;

		float battlefieldWidth = _mapWidth * xSpacing;
		float battlefieldDepth = _mapHeight * zSpacing;

		// 加 1 格边距确保边缘格子完全可见
		float margin = HexUtils.Size * 1.5f;
		_battlefieldBounds = new Aabb(
			new Vector3(-margin, 0, -margin),
			new Vector3(battlefieldWidth + margin * 2, 1, battlefieldDepth + margin * 2));

		// 最大正交尺寸：能看见整个战场
		RecalcMaxOrthoSize();

		// 自动居中相机到战场中心
		var center = _battlefieldBounds.Position + _battlefieldBounds.Size * 0.5f;
		_camera.Position = new Vector3(center.X, _camera.Position.Y, center.Z + _camera.Position.Y);
		ClampCameraPosition();
	}

	/// <summary>重新计算最大正交尺寸（视口大小变化时也应调用）</summary>
	private void RecalcMaxOrthoSize()
	{
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		_maxOrthoSize = BladeHex.View.Camera.CameraBoundsClamp.MaxOrthoSizeToFit(
			_battlefieldBounds, -45f, aspect);
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
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 30, 0), ShadowEnabled = true });
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

		// 战斗小地图（嵌入底部面板最右侧）
		_combatMinimap = new CombatMinimapPanel();
		_combatMinimap.CustomMinimumSize = new Vector2(80, 80);
	}

	/// <summary>播放战斗入场 UI 过渡动画</summary>
	private void PlayEntranceTransition()
	{
		var transition = new BladeHex.View.Transitions.CombatEntranceTransition();
		AddChild(transition);
		transition.Play(
			_combatUi.BottomPanel,
			_combatUi.TurnOrderBarControl,
			_combatMinimap,
			null);
	}

	// ========== 单位放置/移动 ==========

	protected void PlaceUnitAt(Unit unit, int q, int r)
	{
		var cell = _hexGrid.GetCell(q, r);
		if (cell == null || cell.Occupant != null) return;
		AddChild(unit);
		unit.Position = cell.Position + new Vector3(0, HexUtils.Size * 0.25f, 0);
		unit.GridPos = new Vector2I(q, r);
		cell.Occupant = unit;
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
				var targetPos = newCell.Position + new Vector3(0, HexUtils.Size * 0.25f, 0);
				var tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				tween.TweenProperty(unit, "position", targetPos, 0.3);
				await ToSignal(tween, Tween.SignalName.Finished);
			}
			if (unit == _activePlayerUnit) UpdateFov();
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
		int moveRange = (int)unit.CurrentAp;
		if (moveRange <= 0) return;
		foreach (var coord in _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, moveRange))
		{
			var cell = _hexGrid.GetCell(coord.X, coord.Y);
			if (cell == null || cell.Occupant != null) continue;
			// 不可通行的格子不显示绿色高亮
			if (cell.Data != null && !cell.Data.isPassable) continue;
			cell.SetHighlight(true, new Color(0.2f, 0.8f, 0.3f, 0.4f));
			_highlightedCells.Add(cell);
		}
	}

	private void HighlightAttackRange(Unit unit)
	{
		ClearHighlights();
		var weapon = unit.GetMainHand() as WeaponData;
		int weaponRange = weapon?.RangeCells ?? 1;
		int maxVision = BaseMaxVision;
		var unitCell = _hexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
		if (unitCell != null && unitCell.Elevation >= 2) maxVision += HighGroundVisionBonus;
		int atkRange = Math.Min(weaponRange, maxVision);

		foreach (var coord in _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, atkRange))
		{
			// 只高亮有视线的格子
			if (!LineOfSight.HasLos(unit.GridPos, coord, _hexGrid)) continue;
			var cell = _hexGrid.GetCell(coord.X, coord.Y);
			if (cell != null) { cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.4f)); _highlightedCells.Add(cell); }
		}
	}

	/// <summary>显示攻击范围叠加层（不清除现有高亮，用于悬停预览）</summary>
	private void ShowAttackRangeOverlay(Unit unit)
	{
		ClearAttackRangeOverlay();
		var weapon = unit.GetMainHand() as WeaponData;
		int weaponRange = weapon?.RangeCells ?? 1;
		int maxVision = BaseMaxVision;
		var unitCell = _hexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
		if (unitCell != null && unitCell.Elevation >= 2) maxVision += HighGroundVisionBonus;
		int atkRange = Math.Min(weaponRange, maxVision);

		foreach (var coord in _hexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, atkRange))
		{
			if (!LineOfSight.HasLos(unit.GridPos, coord, _hexGrid)) continue;
			var cell = _hexGrid.GetCell(coord.X, coord.Y);
			if (cell == null || _highlightedCells.Contains(cell)) continue;
			cell.SetHighlight(true, new Color(1.0f, 0.3f, 0.2f, 0.2f)); // 淡红色半透明
			_attackRangeOverlayCells.Add(cell);
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
		ClearHighlights(); _currentActionMode = ActionMode.None;
		_activePlayerUnit = unit;
		_combatUi.UpdateUnitInfo(unit);
		_combatUi.LogMessage($"选中 {unit.Data?.UnitName}。");
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

	// ========== 回合 ==========

	protected virtual void OnTurnStarted(int state)
	{
		_currentActionMode = ActionMode.None;
		ClearHighlights();
		_combatMinimap.Refresh();
		var s = (CombatManager.CombatState)state;

		// 更新回合顺序栏
		var allUnits = new Godot.Collections.Array();
		foreach (var u in _combatManager.PlayerUnits)
			if (u.CurrentHp > 0) allUnits.Add(u);
		foreach (var u in _combatManager.EnemyUnits)
			if (u.CurrentHp > 0) allUnits.Add(u);
		_combatUi.UpdateTurnOrder(allUnits, _activePlayerUnit, 0);

		if (s == CombatManager.CombatState.PlayerTurn)
		{
			_combatUi.SetTurnText("=== 玩家回合 ===", new Color(0.2f, 0.6f, 1));
			_combatUi.SetActionBarVisible(true);

			// 自动选中第一个有行动力的玩家单位（确保不会因无选中而无法操作）
			if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit) || _activePlayerUnit.CurrentHp <= 0)
			{
				_activePlayerUnit = _combatManager.PlayerUnits.FirstOrDefault(u => IsInstanceValid(u) && u.CurrentHp > 0);
			}

			_combatUi.UpdateUnitInfo(_activePlayerUnit);
			_combatUi.LogMessage("轮到玩家行动。");
			_audioManager?.PlaySfxName("combat_turn_start");

			// 显示选中单位的移动范围并更新视野
			if (_activePlayerUnit != null)
			{
				ShowSelectedUnitHighlights();
				UpdateFov();
			}
		}
		else if (s == CombatManager.CombatState.EnemyTurn)
		{
			_combatUi.SetTurnText("=== 敌方回合 ===", new Color(1, 0.3f, 0.3f));
			_combatUi.SetActionBarVisible(false);
			_combatUi.LogMessage("敌方行动中...");
			_audioManager?.PlaySfxName("combat_enemy_turn");
			ExecuteAiTurn();
		}
	}

	protected async void ExecuteAiTurn()
	{
		try
		{
			await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
			if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit) || _activePlayerUnit.CurrentHp <= 0)
			{
				_activePlayerUnit = _combatManager.PlayerUnits.FirstOrDefault(p => IsInstanceValid(p) && p.CurrentHp > 0);
				if (_activePlayerUnit == null) { _combatManager.EndCurrentTurn(); return; }
			}
			_aiController.AllActionsCompleted += OnAiDone;
			_ = _aiController.ExecuteEnemyTurn(
				_combatManager.EnemyUnits.Where(e => IsInstanceValid(e) && e.CurrentHp > 0).ToList(),
				_combatManager.PlayerUnits.Where(p => IsInstanceValid(p) && p.CurrentHp > 0).ToList(),
				_hexGrid, _combatUi);
		}
		catch (Exception ex)
		{
			GD.PushError($"[CombatSceneBase] ExecuteAiTurn: {ex.Message}");
		}
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

			await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
			EmitSignal(SignalName.CombatFinished, victory);
			HandleCombatEnd(victory);
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
					// 切换后显示新武器的攻击范围
					HighlightAttackRange(_activePlayerUnit);
					_currentActionMode = ActionMode.Attack;
					return;
				}
				_currentActionMode = ActionMode.None;
				break;

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

			case "career_skill":
				if (_rightClickTarget != null && _activePlayerUnit != null)
				{
					var careerResult = _combatManager.UseCareerSkill(_activePlayerUnit, _rightClickTarget.GridPos, _hexGrid);
					if (careerResult["success"].AsBool())
						_combatUi.LogMessage($"[color=orange]释放职业技能！[/color]");
					else
						_combatUi.LogMessage($"[color=red]{careerResult.GetValueOrDefault("reason", "失败").AsString()}[/color]");
					_combatUi.UpdateUnitInfo(_activePlayerUnit);
					_rightClickTarget = null; ClearHighlights();
					if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
				}
				_currentActionMode = ActionMode.None;
				break;

			default:
				// skill_xxx 格式
				if (action.StartsWith("skill_") && _rightClickTarget != null && _activePlayerUnit != null)
				{
					string skillEffect = action["skill_".Length..];
					var skillResult = _combatManager.UseSkill(_activePlayerUnit, skillEffect, _rightClickTarget.GridPos, _hexGrid);
					if (skillResult["success"].AsBool())
						_combatUi.LogMessage($"[color=orange]释放技能！[/color]");
					else
						_combatUi.LogMessage($"[color=red]{skillResult.GetValueOrDefault("reason", "失败").AsString()}[/color]");
					_combatUi.UpdateUnitInfo(_activePlayerUnit);
					_rightClickTarget = null; ClearHighlights();
					if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
				}
				_currentActionMode = ActionMode.None;
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
		HighlightRange(_activePlayerUnit, spell.RangeCells, new Color(1, 0.5f, 0, 0.4f));
	}

	// ========== 格子点击 ==========

	protected void OnCellClicked(HexCell cell)
	{
		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;

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

		// 点击敌方（非攻击模式）：直接发起攻击（如果在射程内且有行动力）
		if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant))
		{
			var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
			int weaponRange = weapon?.RangeCells ?? 1;
			int maxVision = BaseMaxVision;
			var unitCell = _hexGrid.GetCell(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y);
			if (unitCell != null && unitCell.Elevation >= 2) maxVision += HighGroundVisionBonus;
			int effectiveRange = Math.Min(weaponRange, maxVision);
			int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);
			int apCost = weapon?.ApCost ?? 4;

			if (dist <= effectiveRange && _activePlayerUnit.CurrentAp >= apCost)
			{
				// 射程内且有行动力 → 检查视线后直接攻击
				if (!LineOfSight.HasLos(_activePlayerUnit.GridPos, cell.GridPos, _hexGrid))
				{
					_combatUi.LogMessage($"视线被阻挡，无法攻击 {cell.Occupant.Data?.UnitName ?? "未知"}。");
					return;
				}
				_currentActionMode = ActionMode.Attack;
				HighlightAttackRange(_activePlayerUnit);
				_ = HandleAttack(cell);
			}
			else
			{
				// 超出射程或行动力不足 → 仅显示日志提示，不切换选中目标
				if (dist > effectiveRange)
					_combatUi.LogMessage($"目标超出射程：{cell.Occupant.Data?.UnitName ?? "未知"} (距离 {dist}，射程 {effectiveRange})");
				else
					_combatUi.LogMessage($"行动力不足：{cell.Occupant.Data?.UnitName ?? "未知"} (需要 {apCost}，当前 {_activePlayerUnit.CurrentAp:F0})");
			}
			return;
		}

		// 点击可移动格：A* 寻路移动
		if (_highlightedCells.Contains(cell) && cell.Occupant == null && _activePlayerUnit.CurrentAp >= 1)
		{
			HandleMove(cell);
			return;
		}

		// 点击空地但不在高亮范围内：如果有行动力，尝试重新显示移动范围
		if (cell.Occupant == null && _currentActionMode == ActionMode.None && _activePlayerUnit.CurrentAp >= 1)
		{
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
			bool hasLos = LineOfSight.HasLos(_activePlayerUnit.GridPos, cell.GridPos, _hexGrid);
			if (dist <= range && hasLos) opts[$"⚔ 攻击({weapon?.ItemName ?? "徒手"})"] = "radial_attack";
			else if (!hasLos) opts["视线被阻挡"] = "none";
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

		// 右键空地：取消当前操作模式
		_currentActionMode = ActionMode.None;
		ClearHighlights();
		if (_activePlayerUnit != null && IsInstanceValid(_activePlayerUnit) && _activePlayerUnit.CurrentAp >= 1)
			ShowSelectedUnitHighlights();
	}

	// ========== 移动/攻击/法术/物品处理 ==========

	private void HandleMove(HexCell cell)
	{
		if (!_highlightedCells.Contains(cell) || cell.Occupant != null) return;

		var path = _hexGrid.FindPath(_activePlayerUnit!.GridPos, cell.GridPos);
		if (path == null || path.Count < 2) { _combatUi.LogMessage("无法到达该位置。"); return; }

		float pathCost = _hexGrid.GetPathCost(path);
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

		// 如果不在高亮范围内，检查是否在实际射程内且有视线（支持轮盘直接攻击）
		if (!_highlightedCells.Contains(cell))
		{
			var weapon = _activePlayerUnit!.GetMainHand() as WeaponData;
			int weaponRange = weapon?.RangeCells ?? 1;
			int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);
			if (dist > weaponRange)
			{ _combatUi.LogMessage("目标超出攻击射程。"); return; }
			if (!LineOfSight.HasLos(_activePlayerUnit.GridPos, cell.GridPos, _hexGrid))
			{ _combatUi.LogMessage("视线被阻挡，无法攻击。"); return; }
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
		_activePlayerUnit.PlayAnim("attack");
		_activePlayerUnit.PlayAttackLunge(target.GlobalPosition);
		await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);

		var result = CombatResolver.ResolveAttack(_activePlayerUnit, target, _hexGrid);

		if (result["hit"].AsBool())
		{
			int dmg = result["damage"].AsInt32();
			string critMsg = result["critical"].AsBool() ? " [color=yellow]暴击！[/color]" : "";
			string flankMsg = result.ContainsKey("is_flanking") && result["is_flanking"].AsBool() ? " [包夹]" : "";

			var weapon = _activePlayerUnit.GetMainHand();
			int dmgType = weapon is WeaponData wd ? (int)wd.WeaponDamageType : 0;
			_audioManager?.PlayAttackHitSfx(dmgType, result["critical"].AsBool());

			_combatUi.LogMessage($"[color=green]命中！[/color]{critMsg}{flankMsg} 使用 {weapon?.ItemName ?? "徒手"} 造成 {dmg} 伤害。");
			_combatUi.UpdateEnemyInfo(target);
			if (target.CurrentHp <= 0)
			{
				_audioManager?.PlaySfxName("combat_death");
				_combatUi.LogMessage($"[color=yellow]{target.Data?.UnitName} 被击败！[/color]");
				_combatUi.RemoveEnemy(target); cell.Occupant = null;
			}
		}
		else
		{
			if (result["fumble"].AsBool())
			{ _audioManager?.PlaySfxName("combat_fumble"); _combatUi.LogMessage("[color=red]严重失误！[/color]"); }
			else
			{
				var w2 = _activePlayerUnit.GetMainHand();
				int missDmgType = w2 is WeaponData wd2 ? (int)wd2.WeaponDamageType : 0;
				_audioManager?.PlayAttackMissSfx(missDmgType);
				_combatUi.LogMessage($"[color=red]未命中！[/color] (命中率 {result["hit_chance_percent"].AsInt32()}%)");
			}
		}

		_activePlayerUnit.PlayAnim("default");
		_currentActionMode = ActionMode.None; ClearHighlights();
		_combatUi.UpdateUnitInfo(_activePlayerUnit);
		if (_activePlayerUnit.CurrentAp >= 1) ShowSelectedUnitHighlights();
	}

	private async Task HandleSpell(HexCell cell)
	{
		if (_selectedSpell != null && _highlightedCells.Contains(cell))
		{
			_activePlayerUnit!.PlayAnim("attack");
			await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);

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

	protected void OnCellHover(HexCell cell)
	{
		if (_combatManager.CurrentState != CombatManager.CombatState.PlayerTurn) return;
		if (_activePlayerUnit == null || !IsInstanceValid(_activePlayerUnit)) return;

		if (cell.Occupant != null && _combatManager.EnemyUnits.Contains(cell.Occupant) && cell.Occupant.Visible)
		{
			var weapon = _activePlayerUnit.GetMainHand() as WeaponData;
			int weaponRange = weapon?.RangeCells ?? 1;
			int apCost = weapon?.ApCost ?? 4;
			int maxVision = BaseMaxVision;
			var unitCell = _hexGrid.GetCell(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y);
			if (unitCell != null && unitCell.Elevation >= 2) maxVision += HighGroundVisionBonus;
			int effectiveRange = Math.Min(weaponRange, maxVision);
			int dist = HexUtils.AxialDistance(_activePlayerUnit.GridPos, cell.GridPos);

			// 始终显示攻击范围高亮（让玩家看到武器射程）
			if (!_attackRangeShownForHover)
			{
				ShowAttackRangeOverlay(_activePlayerUnit);
				_attackRangeShownForHover = true;
			}

			// 在射程内且有行动力 → 红色高亮目标格
			if (dist <= effectiveRange && _activePlayerUnit.CurrentAp >= apCost)
			{
				cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.5f));
				_hoverHighlightedCell = cell;
			}

			// 显示命中率预览（射程内显示实际命中率，射程外显示 0%）
			var mousePos = GetViewport().GetMousePosition();
			if (dist <= effectiveRange)
			{
				_combatUi.ShowHitPreview(mousePos, _activePlayerUnit, cell.Occupant);
			}
			else
			{
				_combatUi.ShowOutOfRangePreview(mousePos, cell.Occupant, dist, effectiveRange);
			}
		}
	}

	protected void OnCellHoverExit(HexCell cell)
	{
		_combatUi.HideHitPreview();
		if (_hoverHighlightedCell != null)
		{
			if (!_highlightedCells.Contains(_hoverHighlightedCell))
				_hoverHighlightedCell.SetHighlight(false);
			_hoverHighlightedCell = null;
		}

		// 清除攻击范围叠加层
		if (_attackRangeShownForHover)
		{
			ClearAttackRangeOverlay();
			_attackRangeShownForHover = false;
		}
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
					foreach (var coord in _hexGrid.GetCellsInRange(_activePlayerUnit.GridPos.X, _activePlayerUnit.GridPos.Y, skillRange))
					{ var c = _hexGrid.GetCell(coord.X, coord.Y); if (c != null) { c.SetHighlight(true, new Color(0.9f, 0.7f, 0.2f, 0.4f)); _highlightedCells.Add(c); } }
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

	/// <summary>限制相机位置在战场边界内</summary>
	private void ClampCameraPosition()
	{
		if (_camera == null) return;
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		_camera.Position = BladeHex.View.Camera.CameraBoundsClamp.Clamp3DOrtho(
			_camera.Position, _camera.Size, -45f, _battlefieldBounds, aspect);
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

		// 限制目标位置在战场边界内
		var viewport = GetViewport().GetVisibleRect().Size;
		float aspect = viewport.X / Mathf.Max(1f, viewport.Y);
		targetCamPos = BladeHex.View.Camera.CameraBoundsClamp.Clamp3DOrtho(
			targetCamPos, _camera.Size, -45f, _battlefieldBounds, aspect);

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

	void ICombatSceneAdapter.PlayUnitAnim(Unit unit, string animName) => unit.PlayAnim(animName);
	void ICombatSceneAdapter.LogMessage(string msg) { if (_combatUi != null && IsInstanceValid(_combatUi)) _combatUi.LogMessage(msg); }
	void ICombatSceneAdapter.UpdateUnitInfo(Unit unit) { if (_combatUi != null && IsInstanceValid(_combatUi)) _combatUi.UpdateUnitInfo(unit); }
	void ICombatSceneAdapter.PlayAttackHitSfx(int t, bool c) => _audioManager?.PlayAttackHitSfx(t, c);
	void ICombatSceneAdapter.PlayAttackMissSfx(int t) => _audioManager?.PlayAttackMissSfx(t);
	void ICombatSceneAdapter.PlaySfx(string n) => _audioManager?.PlaySfxName(n);
}
