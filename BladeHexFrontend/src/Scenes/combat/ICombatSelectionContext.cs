// ICombatSelectionContext.cs
// 战斗场景选择状态接口 — 提供当前选中单位、动作模式、技能/法术选择等运行时状态。
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景选择状态。暴露当前活跃单位、动作模式、技能/法术选择等可变状态。
/// </summary>
public interface ICombatSelectionContext
{
	CombatRuntimeState Runtime { get; }
	Unit? ActivePlayerUnit { get; set; }
	bool IsExecutingAction { get; set; }
	HexCell? RightClickTarget { get; set; }
	ActionMode CurrentActionMode { get; set; }
	string? SelectedSkillAction { get; set; }
	SpellData? SelectedSpell { get; set; }

	// ===== 只读查询 =====
	bool IsTargeting { get; }
	bool IsInteractionLocked { get; }

	// ===== 原子状态转换 =====
	void EnterMoveMode();
	void EnterAttackMode();
	void EnterSkillTargeting(string skillAction);
	void EnterSpellTargeting(SpellData spell);
	void EnterItemMode();
	void CancelAction();
	void ClearTargeting();
	void LockInteraction();
	void UnlockInteraction();
}

public enum ActionMode { None, Move, Attack, Spell, Item }
