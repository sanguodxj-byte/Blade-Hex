// CombatRuntimeState.cs
// 战斗场景运行时状态容器 — 从 CombatSceneBase 提取的可变状态。
// 纯 C# 类，不继承 Node，便于测试和状态管理。
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景运行时状态。集中管理战斗中的可变状态字段。
/// </summary>
public class CombatRuntimeState
{
	// ===== 核心状态 =====

	/// <summary>当前活跃的玩家单位</summary>
	public Unit? ActivePlayerUnit { get; set; }

	/// <summary>是否正在执行动作（并发锁，防止狂点地块导致多次消耗AP）</summary>
	public bool IsExecutingAction { get; set; }

	/// <summary>右键点击的目标地块</summary>
	public HexCell? RightClickTarget { get; set; }

	/// <summary>当前行动模式</summary>
	public ActionMode CurrentActionMode { get; set; } = ActionMode.None;

	/// <summary>选中的技能动作标识</summary>
	public string? SelectedSkillAction { get; set; }

	/// <summary>选中的法术数据</summary>
	public SpellData? SelectedSpell { get; set; }

	/// <summary>战斗是否已结束</summary>
	public bool CombatEnded { get; set; }

	// ===== 状态操作方法 =====

	/// <summary>是否正在瞄准（有选中的技能或法术）</summary>
	public bool IsTargeting => SelectedSkillAction != null || SelectedSpell != null;

	/// <summary>交互是否被锁定（UI 级别锁，防止过渡期输入）</summary>
	public bool IsInteractionLocked { get; private set; }

	// ===== 状态转换方法（原子操作） =====

	/// <summary>进入移动模式。清除技能/法术选择。</summary>
	public void EnterMoveMode()
	{
		SelectedSkillAction = null;
		SelectedSpell = null;
		RightClickTarget = null;
		CurrentActionMode = ActionMode.Move;
	}

	/// <summary>进入攻击模式。清除技能/法术选择。</summary>
	public void EnterAttackMode()
	{
		SelectedSkillAction = null;
		SelectedSpell = null;
		RightClickTarget = null;
		CurrentActionMode = ActionMode.Attack;
	}

	/// <summary>进入技能瞄准模式。</summary>
	public void EnterSkillTargeting(string skillAction)
	{
		SelectedSpell = null;
		RightClickTarget = null;
		SelectedSkillAction = skillAction;
		CurrentActionMode = ActionMode.Spell;
	}

	/// <summary>进入法术瞄准模式。</summary>
	public void EnterSpellTargeting(SpellData spell)
	{
		SelectedSkillAction = null;
		RightClickTarget = null;
		SelectedSpell = spell;
		CurrentActionMode = ActionMode.Spell;
	}

	/// <summary>进入物品使用模式。清除技能/法术选择。</summary>
	public void EnterItemMode()
	{
		SelectedSkillAction = null;
		SelectedSpell = null;
		RightClickTarget = null;
		CurrentActionMode = ActionMode.Item;
	}

	/// <summary>取消当前操作。清除所有瞄准/选择状态，恢复为无模式。</summary>
	public void CancelAction()
	{
		SelectedSkillAction = null;
		SelectedSpell = null;
		RightClickTarget = null;
		CurrentActionMode = ActionMode.None;
	}

	/// <summary>清除瞄准状态（保留行动模式和单位选中）</summary>
	public void ClearTargeting()
	{
		SelectedSkillAction = null;
		SelectedSpell = null;
		RightClickTarget = null;
	}

	/// <summary>清除瞄准/选择状态（保留单位选中和行动模式）—— 向后兼容别名</summary>
	public void ClearTargetingSelection() => ClearTargeting();

	/// <summary>重置行动模式为 None</summary>
	public void ResetActionMode()
	{
		CurrentActionMode = ActionMode.None;
	}

	/// <summary>锁定交互（防止并发输入）</summary>
	public void LockInteraction() => IsInteractionLocked = true;

	/// <summary>解锁交互</summary>
	public void UnlockInteraction() => IsInteractionLocked = false;

	/// <summary>尝试开始执行动作。如果已在执行中返回 false。</summary>
	public bool BeginExecution()
	{
		if (IsExecutingAction)
			return false;
		IsExecutingAction = true;
		return true;
	}

	/// <summary>结束动作执行</summary>
	public void EndExecution()
	{
		IsExecutingAction = false;
	}
}
