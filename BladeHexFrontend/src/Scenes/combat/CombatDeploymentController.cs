// CombatDeploymentController.cs
// 战斗场景部署阶段控制器 — 从 CombatSceneBase 提取。
// 负责：部署区高亮、单位放置、确认按钮、部署阶段状态机。
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.UI.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>战斗场景部署阶段控制器。</summary>
[GlobalClass]
public partial class CombatDeploymentController : Node
{
	// ===== 依赖注入 =====
	private HexGrid? _hexGrid;
	private CombatUI? _combatUi;
	private CombatManager? _combatManager;
	private CombatHighlightController? _highlightCtrl;
	private Node? _parentScene;

	public HexGrid HexGrid => _hexGrid ?? throw new InvalidOperationException("CombatDeploymentController not initialized.");
	public CombatUI CombatUI => _combatUi ?? throw new InvalidOperationException("CombatDeploymentController not initialized.");
	public CombatManager CombatManager => _combatManager ?? throw new InvalidOperationException("CombatDeploymentController not initialized.");
	public CombatHighlightController HighlightCtrl => _highlightCtrl ?? throw new InvalidOperationException("CombatDeploymentController not initialized.");
	public BattleMapGenerator.BattleMapData? MapData { get; set; }
	public Node ParentScene => _parentScene ?? throw new InvalidOperationException("CombatDeploymentController not initialized.");

	/// <summary>注入必要依赖。</summary>
	public void Initialize(HexGrid hexGrid, CombatUI combatUi, CombatManager combatManager, CombatHighlightController highlightCtrl, Node parentScene, BattleMapGenerator.BattleMapData? mapData = null)
	{
		_hexGrid = hexGrid ?? throw new ArgumentNullException(nameof(hexGrid));
		_combatUi = combatUi ?? throw new ArgumentNullException(nameof(combatUi));
		_combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
		_highlightCtrl = highlightCtrl ?? throw new ArgumentNullException(nameof(highlightCtrl));
		_parentScene = parentScene ?? throw new ArgumentNullException(nameof(parentScene));
		MapData = mapData;
	}

	// ===== 状态 =====
	private bool _deploymentPhaseActive;
	private readonly List<HexCell> _deploymentZoneCells = new();
	private readonly List<Unit> _unitsToPlace = new();
	private int _currentDeployIndex;
	private Unit? _selectedDeployUnit;
	private Button? _deployConfirmButton;

	public bool IsActive => _deploymentPhaseActive;

	// ===== 业务事件 =====
	/// <summary>当部署正式确认并结束时触发</summary>
	public event Action? DeploymentCompleted;

	// ===== 对外公开 API =====

	/// <summary>开始部署阶段</summary>
	public void BeginDeploymentPhase()
	{
		_deploymentPhaseActive = true;
		CombatManager.EnterDeployment();

		// 收集部署区格子
		_deploymentZoneCells.Clear();
		if (MapData?.PlayerDeployment != null)
		{
			foreach (var v in MapData.PlayerDeployment)
			{
				var coord = v.AsVector2I();
				var cell = HexGrid.GetCell(coord.X, coord.Y);
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
			DeploymentCompleted?.Invoke();
			return;
		}

		// 收集待部署的玩家单位
		_unitsToPlace.Clear();
		foreach (var unit in CombatManager.PlayerUnits)
		{
			if (GodotObject.IsInstanceValid(unit))
				_unitsToPlace.Add(unit);
		}
		_currentDeployIndex = 0;
		_selectedDeployUnit = _unitsToPlace.Count > 0 ? _unitsToPlace[0] : null;

		// 高亮部署区
		HighlightDeploymentZone();

		// 创建确认按钮
		CreateDeployConfirmButton();

		// UI 提示
		CombatUI.SetActionBarVisible(false);
		string unitName = _selectedDeployUnit?.Data?.UnitName ?? "单位";
		CombatUI.SetTurnText($"部署阶段 — 请放置 {unitName}", new Color(0.2f, 0.8f, 0.4f));
		CombatUI.LogMessage($"部署阶段开始。点击蓝色高亮区域放置单位。({_unitsToPlace.Count} 个单位待部署)");
		UpdateDeployConfirmButton();
	}

	/// <summary>暴露给外面 Input/Highlight 的交互入口</summary>
	public void HandleClick(HexCell cell)
	{
		if (!_deploymentPhaseActive) return;
		HandleDeploymentClick(cell);
	}

	/// <summary>高亮玩家部署区域（蓝色半透明）</summary>
	public void HighlightDeploymentZone()
	{
		HighlightCtrl.ClearHighlights();
		var deployColor = new Color(0.2f, 0.5f, 1.0f, 0.35f);
		foreach (var cell in _deploymentZoneCells)
		{
			cell.SetHighlight(true, deployColor);
			HighlightCtrl.HighlightedCells.Add(cell);
		}
	}

	// ===== 内部逻辑 =====

	/// <summary>部署阶段点击格子处理</summary>
	private void HandleDeploymentClick(HexCell cell)
	{
		// 点击已放置的友方单位 → 选中该单位（可重新放置）
		if (cell.Occupant != null && CombatManager.PlayerUnits.Contains(cell.Occupant))
		{
			_selectedDeployUnit = cell.Occupant;
			int idx = _unitsToPlace.IndexOf(cell.Occupant);
			if (idx >= 0) _currentDeployIndex = idx;
			string name = _selectedDeployUnit.Data?.UnitName ?? "单位";
			CombatUI.SetTurnText($"部署阶段 — 重新放置 {name}", new Color(0.2f, 0.8f, 0.4f));
			CombatUI.LogMessage($"选中 {name}，点击部署区空格重新放置。");
			return;
		}

		// 点击部署区空格 → 放置当前选中单位
		if (_selectedDeployUnit == null) return;
		if (!_deploymentZoneCells.Contains(cell)) return;
		if (cell.Occupant != null) return;

		// 如果该单位已经在地图上，先移除旧位置
		var oldCell = HexGrid.GetCell(_selectedDeployUnit.GridPos.X, _selectedDeployUnit.GridPos.Y);
		if (oldCell != null && oldCell.Occupant == _selectedDeployUnit)
		{
			oldCell.Occupant = null;
		}

		// 放置单位到新位置
		if (!_selectedDeployUnit.IsInsideTree())
			ParentScene.AddChild(_selectedDeployUnit);
		_selectedDeployUnit.Position = cell.Position + new Vector3(0,
			BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);
		_selectedDeployUnit.GridPos = cell.GridPos;
		cell.Occupant = _selectedDeployUnit;
		_selectedDeployUnit.Facing = 0; // 玩家朝右

		string unitName = _selectedDeployUnit.Data?.UnitName ?? "单位";
		CombatUI.LogMessage($"{unitName} 已部署到 ({cell.GridPos.X},{cell.GridPos.Y})。");

		// 推进到下一个未放置的单位
		AdvanceToNextUnplacedUnit();

		// 刷新高亮
		HighlightDeploymentZone();
		UpdateDeployConfirmButton();
	}

	/// <summary>推进到下一个尚未放置的单位</summary>
	private void AdvanceToNextUnplacedUnit()
	{
		for (int i = 0; i < _unitsToPlace.Count; i++)
		{
			int idx = (_currentDeployIndex + 1 + i) % _unitsToPlace.Count;
			var unit = _unitsToPlace[idx];
			var unitCell = HexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
			if (unitCell == null || unitCell.Occupant != unit)
			{
				_currentDeployIndex = idx;
				_selectedDeployUnit = unit;
				string name = unit.Data?.UnitName ?? "单位";
				CombatUI.SetTurnText($"部署阶段 — 请放置 {name}", new Color(0.2f, 0.8f, 0.4f));
				return;
			}
		}

		_selectedDeployUnit = null;
		CombatUI.SetTurnText("部署阶段 — 所有单位已就位", new Color(0.2f, 0.8f, 0.4f));
		CombatUI.LogMessage("所有单位已部署完毕，点击「开始战斗」进入正式战斗。");
	}

	/// <summary>检查是否所有单位都已部署</summary>
	private bool AllUnitsDeployed()
	{
		foreach (var unit in _unitsToPlace)
		{
			if (!GodotObject.IsInstanceValid(unit)) continue;
			var cell = HexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
			if (cell == null || cell.Occupant != unit) return false;
		}
		return true;
	}

	/// <summary>创建部署确认按钮</summary>
	private void CreateDeployConfirmButton()
	{
		if (_deployConfirmButton != null && GodotObject.IsInstanceValid(_deployConfirmButton))
		{
			_deployConfirmButton.QueueFree();
		}

		_deployConfirmButton = new Button
		{
			Text = "开始战斗",
			CustomMinimumSize = new Vector2(200, 54),
			Visible = false,
		};
		_deployConfirmButton.AddThemeColorOverride("font_color", new Color(0.95f, 0.90f, 0.78f));
		_deployConfirmButton.AddThemeFontSizeOverride("font_size", 18);

		// 将按钮移至屏幕中央
		_deployConfirmButton.AnchorLeft = 0.5f;
		_deployConfirmButton.AnchorRight = 0.5f;
		_deployConfirmButton.AnchorTop = 0.5f;
		_deployConfirmButton.AnchorBottom = 0.5f;
		_deployConfirmButton.OffsetLeft = -100;
		_deployConfirmButton.OffsetRight = 100;
		_deployConfirmButton.OffsetTop = -27;
		_deployConfirmButton.OffsetBottom = 27;
		_deployConfirmButton.GrowHorizontal = Control.GrowDirection.Both;

		// 注入华丽的深绿与暗金边框主题样式
		var normalStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.10f, 0.07f, 0.94f),
			BorderColor = new Color(0.78f, 0.62f, 0.34f, 0.95f),
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5
		};
		normalStyle.SetBorderWidthAll(2);
		_deployConfirmButton.AddThemeStyleboxOverride("normal", normalStyle);

		var hoverStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.14f, 0.085f, 0.98f),
			BorderColor = new Color(0.92f, 0.74f, 0.42f, 1.0f),
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5
		};
		hoverStyle.SetBorderWidthAll(2);
		_deployConfirmButton.AddThemeStyleboxOverride("hover", hoverStyle);

		var pressedStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.060f, 0.052f, 0.044f, 0.98f),
			BorderColor = new Color(0.58f, 0.46f, 0.26f, 0.95f),
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5
		};
		pressedStyle.SetBorderWidthAll(2);
		_deployConfirmButton.AddThemeStyleboxOverride("pressed", pressedStyle);

		_deployConfirmButton.Pressed += OnDeployConfirmPressed;
		CombatUI.AddChild(_deployConfirmButton);
	}

	private void UpdateDeployConfirmButton()
	{
		if (_deployConfirmButton == null) return;
		_deployConfirmButton.Visible = AllUnitsDeployed();
	}

	private void OnDeployConfirmPressed()
	{
		if (!AllUnitsDeployed()) return;

		_deploymentPhaseActive = false;

		HighlightCtrl.ClearHighlights();
		if (_deployConfirmButton != null)
		{
			_deployConfirmButton.QueueFree();
			_deployConfirmButton = null;
		}
		_deploymentZoneCells.Clear();
		_unitsToPlace.Clear();

		CombatManager.ConfirmDeployment();
		CombatUI.LogMessage("部署完成，战斗开始！");

		DeploymentCompleted?.Invoke();
	}

	/// <summary>部署区为空时的回退：自动将玩家单位放到最近可用格子并开始战斗</summary>
	private void AutoPlaceUnitsAndStart()
	{
		int placed = 0;
		foreach (var unit in CombatManager.PlayerUnits)
		{
			if (!GodotObject.IsInstanceValid(unit)) continue;
			var cell = FindClosestDeployableCell(placed, 0);
			if (cell == null) cell = FindClosestDeployableCell(0, placed);
			if (cell != null)
			{
				if (!unit.IsInsideTree()) ParentScene.AddChild(unit);
				unit.Position = cell.Position + new Vector3(0,
					BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.CharacterLayer, 0);
				unit.GridPos = cell.GridPos;
				cell.Occupant = unit;
				unit.Facing = 0;
			}
			placed++;
		}
		CombatManager.ConfirmDeployment();
	}

	public HexCell? FindClosestDeployableCell(int q, int r)
	{
		HexCell? best = null;
		int bestDist = int.MaxValue;
		foreach (var kv in HexGrid.Cells)
		{
			var c = kv.Value;
			if (c == null || c.Occupant != null) continue;
			if (c.Data != null && !c.Data.isPassable) continue;
			int d = HexUtils.AxialDistance(kv.Key, new Vector2I(q, r));
			if (d < bestDist) { bestDist = d; best = c; }
		}
		return best;
	}

	public void EndDeployment()
	{
		_deploymentPhaseActive = false;
		_deploymentZoneCells.Clear();
		_unitsToPlace.Clear();
		if (_deployConfirmButton != null && GodotObject.IsInstanceValid(_deployConfirmButton))
		{
			_deployConfirmButton.QueueFree();
		}
		_deployConfirmButton = null;
	}
}
