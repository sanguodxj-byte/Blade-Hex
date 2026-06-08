// CombatTargetingController.cs
// 战斗瞄准控制器 — 集中管理 targeting/preview 状态，减少双 controller 重叠。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗瞄准控制器。组合 HighlightCtrl 和 HoverPreviewCtrl，统一管理 AOE 预览状态。
/// </summary>
[GlobalClass]
public partial class CombatTargetingController : Node
{
	// ===== 组合的子控制器 =====
	public CombatHighlightController HighlightCtrl { get; set; } = null!;
	public CombatHoverPreviewController HoverPreviewCtrl { get; set; } = null!;

	// ===== AOE 预览状态（从 HoverPreviewCtrl 迁入）=====
	private HexCell? _aoePreviewCenter;
	private readonly List<HexCell> _aoePreviewCells = new();

	/// <summary>清除 AOE 预览高亮</summary>
	public void ClearAoePreview(Predicate<HexCell> isPersistentHighlight)
	{
		foreach (var cell in _aoePreviewCells)
		{
			if (!isPersistentHighlight(cell))
				cell.SetHighlight(false);
		}
		_aoePreviewCells.Clear();
		_aoePreviewCenter = null;
	}

	/// <summary>设置 AOE 预览高亮</summary>
	public void SetAoePreview(Predicate<HexCell> isPersistentHighlight, HexCell center, List<HexCell> aoeCells)
	{
		ClearAoePreview(isPersistentHighlight);
		_aoePreviewCenter = center;
		foreach (var cell in aoeCells)
		{
			if (cell == center) continue;
			if (!isPersistentHighlight(cell))
			{
				cell.SetHighlight(true, new Color(1.0f, 0.5f, 0.1f, 0.3f));
				_aoePreviewCells.Add(cell);
			}
		}
	}

	/// <summary>AOE 预览中心</summary>
	public HexCell? AoePreviewCenter => _aoePreviewCenter;
}
