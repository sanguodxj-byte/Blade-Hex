// CombatHoverPreviewController.cs
// 从 CombatSceneBase 提取的悬停预览控制器。
// 负责：空地路径折线、敌人攻击范围叠加层与 hit 命中率预览、技能/法术 AOE 格子预览及效果 Tooltip。
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.UI.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

[GlobalClass]
public partial class CombatHoverPreviewController : Node
{
	// ===== 小 ports 依赖 =====
	private ICombatSelectionContext? _selection;
	private ICombatHighlightPort? _highlight;
	private ICombatGridQuery? _gridQuery;
	private ICombatTurnPort? _turnPort;
	private ICombatSkillPort? _skillPort;
	private CombatHighlightController? _highlightCtrl;
	private CombatUI? _combatUi;
	private CombatManager? _combatManager;
	private HexGrid? _hexGrid;
	private Node? _parentScene;

	// ===== 访问器（失败快速） =====
	private ICombatSelectionContext Selection => _selection ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private ICombatHighlightPort Highlight => _highlight ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private ICombatGridQuery GridQuery => _gridQuery ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private ICombatTurnPort TurnPort => _turnPort ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private ICombatSkillPort SkillPort => _skillPort ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	public CombatHighlightController HighlightCtrl => _highlightCtrl ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private CombatUI CombatUi => _combatUi ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private CombatManager CombatMgr => _combatManager ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	private HexGrid HexGrd => _hexGrid ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");
	public Node ParentScene => _parentScene ?? throw new InvalidOperationException("CombatHoverPreviewController not initialized.");

	public CombatTargetingController? TargetingController { get; set; }

	/// <summary>注入必要依赖。</summary>
	public void Initialize(
		ICombatSelectionContext selection,
		ICombatHighlightPort highlight,
		ICombatGridQuery gridQuery,
		ICombatTurnPort turnPort,
		ICombatSkillPort skillPort,
		CombatHighlightController highlightCtrl,
		CombatUI combatUi,
		CombatManager combatManager,
		HexGrid hexGrid,
		Node parentScene)
	{
		_selection = selection ?? throw new ArgumentNullException(nameof(selection));
		_highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
		_gridQuery = gridQuery ?? throw new ArgumentNullException(nameof(gridQuery));
		_turnPort = turnPort ?? throw new ArgumentNullException(nameof(turnPort));
		_skillPort = skillPort ?? throw new ArgumentNullException(nameof(skillPort));
		_highlightCtrl = highlightCtrl ?? throw new ArgumentNullException(nameof(highlightCtrl));
		_combatUi = combatUi ?? throw new ArgumentNullException(nameof(combatUi));
		_combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
		_hexGrid = hexGrid ?? throw new ArgumentNullException(nameof(hexGrid));
		_parentScene = parentScene ?? throw new ArgumentNullException(nameof(parentScene));
	}

	// ===== 状态 =====
	private HexCell? _hoverHighlightedCell;
	private bool _attackRangeShownForHover;
	private string _lastActionHover = "none";
	private int _actionHoverVersion;

	// 对外暴露的业务入口
	public void OnCellHover(HexCell cell)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		// 悬浮轮廓: 任何 cell 都显示暗金色六边形边框
		HighlightCtrl.ShowHoverOutline(cell);

		if (!TurnPort.IsPlayerTurn) return;
		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit)) return;

		// 如果未处于施法瞄准模式，默认清空 AP 预览闪烁
		if (Selection.CurrentActionMode != ActionMode.Spell)
		{
			CombatUi.SetApPreview(0f);
		}

		// ===== 施法瞄准模式悬浮预览 =====
		if (Selection.CurrentActionMode == ActionMode.Spell && !string.IsNullOrEmpty(Selection.SelectedSkillAction))
		{
			HandleSpellHover(cell);
			return;
		}

		// 敌人悬浮: 攻击预览
		if (cell.Occupant != null && CombatMgr.EnemyUnits.Contains(cell.Occupant) && cell.Occupant.Visible)
		{
			var targetUnit = cell.Occupant;
			HighlightCtrl.ClearPathPreview();
			if (Selection.CurrentActionMode == ActionMode.None || Selection.CurrentActionMode == ActionMode.Attack)
			{
				var weapon = activeUnit.GetMainHand() as WeaponData;
				int weaponRange = activeUnit.GetWeaponRange();
				int apCost = weapon?.ApCost ?? 4;
				int effectiveRange = weaponRange;
				int dist = HexUtils.AxialDistance(activeUnit.GridPos, cell.GridPos);
				bool blockedByElevation = CombatAttackRules.IsMeleeElevationBlocked(activeUnit, targetUnit, HexGrd);

				if (!_attackRangeShownForHover)
				{
					ShowAttackRangeOverlay(activeUnit);
					_attackRangeShownForHover = true;
				}

				if (dist <= effectiveRange && activeUnit.CurrentAp >= apCost && !blockedByElevation)
				{
					cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.2f), true);
					_hoverHighlightedCell = cell;
				}
				else
				{
					cell.SetHighlight(true, new Color(0.5f, 0.1f, 0.1f, 0.25f), true);
					_hoverHighlightedCell = cell;
				}

				// 指向敌人展示攻击消耗
				CombatUi.SetApPreview(apCost);

				var mousePos = ParentScene.GetViewport().GetMousePosition();
				if (dist <= effectiveRange)
				{
					if (blockedByElevation)
					{
						CombatUi.ShowAttackBlockedPreview(mousePos, targetUnit, CombatAttackRules.MeleeElevationBlockedReason);
					}
					else if (activeUnit.CurrentAp >= apCost)
					{
						int elevDiff = 0;
						var atkCell = HexGrd.GetCell(activeUnit.GridPos.X, activeUnit.GridPos.Y);
						var defCell = HexGrd.GetCell(cell.GridPos.X, cell.GridPos.Y);
						if (atkCell != null && defCell != null)
						{
							elevDiff = atkCell.Elevation - defCell.Elevation;
						}

						bool flanking = false;
						var flankBonus = FacingSystem.GetFlankingBonus(activeUnit.GridPos, targetUnit);
						flanking = flankBonus.DamageMultiplier > 1.0f;

						int cover = 0;
						if (weapon != null && weapon.IsRanged)
						{
							int losPenalty = LineOfSight.GetPathPenalty(
								activeUnit.GridPos, cell.GridPos, HexGrd, activeUnit, targetUnit);
							if (losPenalty < 0)
							{
								cover = 1;
							}
						}

						CombatUi.ShowHitPreview(mousePos, activeUnit, targetUnit, HexGrd, cover, elevDiff, flanking, false);
					}
					else
					{
						CombatUi.ShowApDeficientPreview(mousePos, targetUnit, apCost, activeUnit.CurrentAp);
					}
				}
				else
				{
					CombatUi.ShowOutOfRangePreview(mousePos, targetUnit, dist, effectiveRange);
				}
			}
			return;
		}

		if (cell.Occupant == null
			&& (Selection.CurrentActionMode == ActionMode.None || Selection.CurrentActionMode == ActionMode.Attack)
			&& CombatMgr.TryGetBattleAnchorAt(cell.GridPos, out var battleAnchor)
			&& battleAnchor.Destructible)
		{
			HighlightCtrl.ClearPathPreview();
			if (!_attackRangeShownForHover)
			{
				ShowAttackRangeOverlay(activeUnit);
				_attackRangeShownForHover = true;
			}

			var anchorAttackCheck = CombatAttackRules.CanAttackBattleAnchor(activeUnit, battleAnchor);
			cell.SetHighlight(
				true,
				anchorAttackCheck.CanAttack
					? new Color(1.0f, 0.2f, 0.2f, 0.2f)
					: new Color(0.5f, 0.1f, 0.1f, 0.25f),
				true);
			_hoverHighlightedCell = cell;
			if (anchorAttackCheck.ApCost > 0)
				CombatUi.SetApPreview(anchorAttackCheck.ApCost);
			return;
		}

		// 空地悬浮: 移动路径预览
		if (Selection.CurrentActionMode == ActionMode.None && cell.Occupant == null && activeUnit.CurrentAp >= 1
			&& (cell.Data == null || cell.Data.isPassable))
		{
			var path = HexGrd.FindPath(activeUnit.GridPos, cell.GridPos);
			if (path != null && path.Count >= 1)
			{
				float cost = HexGrd.GetPathCost(activeUnit.GridPos, path);
				if (cost <= activeUnit.CurrentAp)
				{
					DrawPathPreview(path);
					CombatUi.SetApPreview(cost);
				}
				else
				{
					HighlightCtrl.ClearPathPreview();
				}
			}
			else
			{
				HighlightCtrl.ClearPathPreview();
			}
		}
		else
		{
			HighlightCtrl.ClearPathPreview();
		}
	}

	public void OnCellHoverExit(HexCell cell)
	{
		HighlightCtrl.HideHoverOutline();
		CombatUi.HideHitPreview();
		CombatUi.HideSkillPreview();

		// 移出地块清空 AP 预览
		if (Selection.CurrentActionMode != ActionMode.Spell)
		{
			CombatUi.SetApPreview(0f);
		}

		ClearAoePreview();

		if (_hoverHighlightedCell != null)
		{
			if (!Highlight.HighlightedCellsContains(_hoverHighlightedCell))
				_hoverHighlightedCell.SetHighlight(false);
			_hoverHighlightedCell = null;
		}

		if (_attackRangeShownForHover)
		{
			ClearAttackRangeOverlay();
			_attackRangeShownForHover = false;
		}

		HighlightCtrl.ClearPathPreview();
	}

	// ===== 内部辅助逻辑 =====

	private void HandleSpellHover(HexCell cell)
	{
		var activeUnit = Selection.ActivePlayerUnit;
		var selectedSkillAction = Selection.SelectedSkillAction;
		var selectedSpell = Selection.SelectedSpell;

		if (activeUnit == null) return;

		if (selectedSpell != null)
		{
			ClearAoePreview();

			bool spellInRange = HexUtils.AxialDistance(activeUnit.GridPos, cell.GridPos) <= selectedSpell.RangeCells
				|| selectedSpell.RangeCells <= 0
				|| selectedSpell.shape == SpellData.SpellShape.Self;
			if (!spellInRange)
			{
				CombatUi.HideSkillPreview();
				return;
			}

			cell.SetHighlight(true, new Color(1.0f, 0.8f, 0.2f, 0.4f), true);
			_hoverHighlightedCell = cell;

			var aoeCells = new List<HexCell>();
			var cells = SpellShapeResolver.GetCellsInShape(
				(int)selectedSpell.shape,
				cell.GridPos,
				activeUnit.GridPos,
				selectedSpell.ShapeSize,
				pos => HexGrd.GetCell(pos.X, pos.Y) != null);
			foreach (var pos in cells)
			{
				var aoeCell = HexGrd.GetCell(pos.X, pos.Y);
				if (aoeCell != null) aoeCells.Add(aoeCell);
			}

			if (aoeCells.Count > 1)
			{
				if (TargetingController != null)
				{
					TargetingController.SetAoePreview(Highlight.HighlightedCellsContains, cell, aoeCells);
				}
				else
				{
					foreach (var aoeCell in aoeCells)
					{
						if (aoeCell == cell) continue;
						if (!Highlight.HighlightedCellsContains(aoeCell))
							aoeCell.SetHighlight(true, new Color(1.0f, 0.5f, 0.1f, 0.3f));
					}
				}
			}

			return;
		}

		if (string.IsNullOrEmpty(selectedSkillAction)) return;

		var info = SkillPort.ResolveSkillTargetingInfo(selectedSkillAction);
		if (info == null) return;

		ClearAoePreview();

		bool skillInRange = info.Value.IsCellValid(cell, CombatMgr);
		if (skillInRange)
		{
			cell.SetHighlight(true, new Color(1.0f, 0.8f, 0.2f, 0.4f), true);
			_hoverHighlightedCell = cell;

			if (info.Value.IsAoe)
			{
				var aoeCells = new List<HexCell>();
				info.Value.GetAoeCells(HexGrd, cell.GridPos, aoeCells);
				// 使用 TargetingController 设置 AOE 预览
				if (TargetingController != null)
				{
					TargetingController.SetAoePreview(Highlight.HighlightedCellsContains, cell, aoeCells);
				}
				else
				{
					// Fallback: 直接设置高亮（向后兼容）
					foreach (var aoeCell in aoeCells)
					{
						if (aoeCell == cell) continue;
						if (!Highlight.HighlightedCellsContains(aoeCell))
							aoeCell.SetHighlight(true, new Color(1.0f, 0.5f, 0.1f, 0.3f));
					}
				}
			}

			ShowSkillPreview(cell, info.Value);
		}
		else
		{
			CombatUi.HideSkillPreview();
		}
	}

	private void ShowSkillPreview(HexCell targetCell, SkillTargetingInfo info)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (activeUnit == null) return;

		var mousePos = ParentScene.GetViewport().GetMousePosition();
		var affectedUnits = new List<Unit>();

		if (info.IsAoe)
		{
			var aoeCells = new List<HexCell>();
			info.GetAoeCells(HexGrd, targetCell.GridPos, aoeCells);
			foreach (var cell in aoeCells)
			{
				if (cell.Occupant != null && cell.Occupant.CurrentHp > 0)
					affectedUnits.Add(cell.Occupant);
			}
		}
		else
		{
			if (targetCell.Occupant != null && targetCell.Occupant.CurrentHp > 0)
				affectedUnits.Add(targetCell.Occupant);
		}

		if (affectedUnits.Count == 0)
		{
			CombatUi.HideSkillPreview();
			return;
		}

		CombatUi.ShowSkillPreview(mousePos, activeUnit, info, affectedUnits);
	}

	public void ClearAoePreview()
	{
		if (TargetingController != null)
		{
			TargetingController.ClearAoePreview(Highlight.HighlightedCellsContains);
		}
		else
		{
			// Fallback: 清除本地状态（向后兼容）
		}

		if (_hoverHighlightedCell != null)
		{
			if (!Highlight.HighlightedCellsContains(_hoverHighlightedCell))
				_hoverHighlightedCell.SetHighlight(false);
			_hoverHighlightedCell = null;
		}
	}

	private void DrawPathPreview(List<Vector2I> path)
	{
		var activeUnit = Selection.ActivePlayerUnit;
		if (activeUnit != null)
		{
			var startCell = HexGrd.GetCell(activeUnit.GridPos.X, activeUnit.GridPos.Y);
			if (startCell != null)
			{
				HighlightCtrl.DrawPathPreview(path, startCell.Position);
			}
		}
	}

	// 攻击覆盖叠加层（委托给 HighlightCtrl）
	private void ShowAttackRangeOverlay(Unit unit)
	{
		HighlightCtrl.ShowAttackRangeOverlay(unit);
	}

	private void ClearAttackRangeOverlay()
	{
		HighlightCtrl.ClearAttackRangeOverlay();
	}

	public async void OnActionHovered(string action)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit))
		{
			return;
		}

		if (action == _lastActionHover)
		{
			return;
		}

		int hoverVersion = ++_actionHoverVersion;
		if (action == "none")
		{
			await ToSignal(GetTree().CreateTimer(0.04), SceneTreeTimer.SignalName.Timeout);
			if (hoverVersion != _actionHoverVersion) return;
		}

		// 已经锁定技能/法术瞄准时，HUD 鼠标离开不能清掉锁定范围
		if (action == "none"
			&& Selection.CurrentActionMode == ActionMode.Spell
			&& (!string.IsNullOrEmpty(Selection.SelectedSkillAction) || Selection.SelectedSpell != null))
		{
			return;
		}

		_lastActionHover = action;

		Highlight.ClearHighlights();
		CombatUi.SetApPreview(0f); // 默认重置 AP 消耗预览

		switch (action)
		{
			case "radial_attack":
			case "attack":
				Highlight.HighlightAttackRange(activeUnit);
				int atkCost = (activeUnit.GetMainHand() as WeaponData)?.ApCost ?? 4;
				CombatUi.SetApPreview(atkCost);
				break;
			case "switch_to_primary":
				var mainWeapon = activeUnit.Data?.PrimaryMainHand as WeaponData;
				CombatUi.SetApPreview(mainWeapon?.ApCost ?? 4);
				break;
			case "switch_to_secondary":
				var offWeapon = activeUnit.Data?.SecondaryMainHand as WeaponData;
				CombatUi.SetApPreview(offWeapon?.ApCost ?? 4);
				break;
			case "defend":
			case "none":
				if (action == "none" && Selection.CurrentActionMode == ActionMode.None)
				{
					HighlightCtrl.ShowSelectedUnitHighlights(activeUnit);
				}
				else
				{
					Highlight.HighlightRange(activeUnit, new List<Vector2I> { activeUnit.GridPos }, new Color(0.3f, 0.6f, 1.0f, 0.5f));
				}
				break;
			case "spell":
				int maxSpellRange = 1;
				if (activeUnit.Data?.KnownSpells != null)
					foreach (var spell in activeUnit.Data.KnownSpells)
						if (spell != null && spell.RangeCells > maxSpellRange) maxSpellRange = spell.RangeCells;
				
				var spellRangeCells = new List<Vector2I>();
				foreach (var coord in HexGrd.GetCellsInRange(activeUnit.GridPos.X, activeUnit.GridPos.Y, maxSpellRange))
				{
					spellRangeCells.Add(coord);
				}
				Highlight.HighlightRange(activeUnit, spellRangeCells, new Color(0.6f, 0.3f, 0.9f, 0.35f));
				break;
			case "career_skill":
			{
				var info = SkillPort.ResolveSkillTargetingInfo(action);
				if (info != null)
				{
					Highlight.HighlightSkillRangeAction(action);
					CombatUi.SetApPreview(info.Value.ActionCost);
				}
				break;
			}
			default:
				if (SpellStudyCatalog.IsEquippedSpellEntry(action))
				{
					var spell = activeUnit.Data != null
						? SpellStudyCatalog.GetKnownSpell(activeUnit.Data, SpellStudyCatalog.GetSpellIdFromEntry(action))
						: null;
					if (spell != null)
					{
						int apCost = spell.castingTime == SpellData.CastingTime.MainAction ? 4 : 0;
						if (spell.RangeCells <= 0 || spell.shape == SpellData.SpellShape.Self)
						{
							Highlight.HighlightRange(activeUnit, new List<Vector2I> { activeUnit.GridPos }, new Color(0.6f, 0.3f, 0.9f, 0.45f));
						}
						else
						{
							Highlight.HighlightRange(activeUnit, spell.RangeCells, new Color(0.6f, 0.3f, 0.9f, 0.35f));
						}
						CombatUi.SetApPreview(apCost);
					}
				}
				else if (action.StartsWith("skill_"))
				{
					var info = SkillPort.ResolveSkillTargetingInfo(action);
					if (info != null)
					{
						Highlight.HighlightSkillRangeAction(action);
						CombatUi.SetApPreview(info.Value.ActionCost);
					}
				}
				break;
		}
	}
}
