// ICombatHighlightPort.cs
// 战斗场景高亮端口接口 — 提供地块高亮代理方法，解耦高亮控制器依赖。
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景高亮端口。代理 CombatHighlightController 的高亮操作。
/// </summary>
public interface ICombatHighlightPort
{
	void ClearHighlights();
	void HighlightRange(Unit unit, int range, Color color, bool emptyOnly = false);
	void HighlightRange(Unit unit, List<Vector2I> cells, Color color);
	bool HighlightedCellsContains(HexCell cell);
	void HighlightMoveRange(Unit unit);
	void HighlightAttackRange(Unit unit);
	void ShowSelectedUnitHighlights();
	void HighlightSkillRangeAction(string action);
}
