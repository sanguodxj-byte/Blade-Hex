// CombatActionDispatcher.cs
// 从 CombatSceneBase 提取的行动分派器。
// 负责：OnActionSelected 巨型 switch、OnSpellSelected -> 动作/瞄准模式分派。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.UI.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

/// <summary>战斗场景行动分派器。</summary>
[GlobalClass]
public partial class CombatActionDispatcher : Node
{
	// ===== 小 ports 依赖 =====
	private ICombatSelectionContext? _selection;
	private ICombatHighlightPort? _highlight;
	private ICombatFeedbackPort? _feedback;
	private ICombatGridQuery? _gridQuery;
	private ICombatTurnPort? _turnPort;
	private ICombatActionPort? _actionPort;
	private ICombatSkillPort? _skillPort;
	private ICombatEndPort? _endPort;
	private SpellManager? _spellManager;

	private static readonly bool DebugLogging = false;

	// ===== 访问器（失败快速） =====
	private ICombatSelectionContext Selection => _selection ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatHighlightPort Highlight => _highlight ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatFeedbackPort Feedback => _feedback ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatGridQuery GridQuery => _gridQuery ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatTurnPort TurnPort => _turnPort ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatActionPort ActionPort => _actionPort ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatSkillPort SkillPort => _skillPort ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");
	private ICombatEndPort EndPort => _endPort ?? throw new InvalidOperationException("CombatActionDispatcher not initialized.");

	/// <summary>注入必要依赖。</summary>
	public void Initialize(
		ICombatSelectionContext selection,
		ICombatHighlightPort highlight,
		ICombatFeedbackPort feedback,
		ICombatGridQuery gridQuery,
		ICombatTurnPort turnPort,
		ICombatActionPort actionPort,
		ICombatSkillPort skillPort,
		ICombatEndPort endPort,
		SpellManager spellManager)
	{
		_selection = selection ?? throw new ArgumentNullException(nameof(selection));
		_highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
		_feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
		_gridQuery = gridQuery ?? throw new ArgumentNullException(nameof(gridQuery));
		_turnPort = turnPort ?? throw new ArgumentNullException(nameof(turnPort));
		_actionPort = actionPort ?? throw new ArgumentNullException(nameof(actionPort));
		_skillPort = skillPort ?? throw new ArgumentNullException(nameof(skillPort));
		_endPort = endPort ?? throw new ArgumentNullException(nameof(endPort));
		_spellManager = spellManager ?? throw new ArgumentNullException(nameof(spellManager));
	}

	// ===== 回调业务事件 =====
	public event Action<string>? ActionDispatched;

	/// <summary>处理动作选择</summary>
	public void OnActionSelected(string action)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: action={action} unit={activeUnit?.Name ?? "null"} ap={activeUnit?.CurrentAp ?? 0}");
		Feedback.SetApPreview(0f); // 默认重置 AP 消耗预览

		Highlight.ClearHighlights();

		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit)) return;

		if (SpellStudyCatalog.IsEquippedSpellEntry(action))
		{
			OnEquippedSpellSelected(action, activeUnit);
			return;
		}

		switch (action)
		{
			case "move":
				if (activeUnit.CurrentAp < 1)
				{
					Feedback.LogMessage("行动力不足。");
					Selection.Runtime.CancelAction();
				}
				else
				{
					Feedback.LogMessage("选择移动：请点击高亮空地。");
					Selection.Runtime.EnterMoveMode();
					Highlight.HighlightMoveRange(activeUnit);
				}
				break;

			case "attack":
				var atkWeapon = activeUnit.GetMainHand() as WeaponData;
				int atkApCost = atkWeapon?.ApCost ?? 4;
				if (activeUnit.CurrentAp < atkApCost)
				{
					Feedback.LogMessage("行动力不足。");
					Selection.Runtime.CancelAction();
				}
				else
				{
					string atkName = atkWeapon?.ItemName ?? "徒手";
					int range = atkWeapon?.RangeCells ?? 1;
					Feedback.LogMessage($"选择攻击：当前武器【{atkName}】(射程 {range})。请点击红色高亮敌人。");
					Selection.Runtime.EnterAttackMode();
					Highlight.HighlightAttackRange(activeUnit);
				}
				break;

			case "spell":
				if (activeUnit.Data?.KnownSpells == null || activeUnit.Data.KnownSpells.Count == 0)
				{
					Feedback.LogMessage("未学习任何法术。");
					Selection.Runtime.CancelAction();
				}
				else
				{
					Feedback.LogMessage("打开法术选择面板...");
					Selection.Runtime.ResetActionMode();
					Feedback.OpenSpellPanel(activeUnit, _spellManager!);
				}
				break;

			case "item":
				if (activeUnit.Data?.Consumables == null || activeUnit.Data.Consumables.Count == 0)
				{
					Feedback.LogMessage("背包中没有消耗品。");
					Selection.Runtime.CancelAction();
				}
				else
				{
					Feedback.LogMessage("选择物品：请点击相邻的友方单位或自身使用药水。");
					Selection.Runtime.EnterItemMode();
					Highlight.HighlightRange(activeUnit, 1, new Color(0.2f, 0.9f, 0.4f, 0.4f));
				}
				break;

			case "defend":
				if (activeUnit.Data != null)
				{
					activeUnit.Model.IsDefending = true;
					activeUnit.HasActed = true;
					Feedback.LogMessage("[color=cyan]进入防御模式！[/color] AC+2，免疫包夹。");
				}
				Selection.Runtime.CancelAction();
				break;

			case "swap_weapon":
			case "switch_to_primary":
			case "switch_to_secondary":
				if (action == "switch_to_primary" && activeUnit.UsingPrimaryWeapon)
				{
					Feedback.LogMessage("已经在使用主手武器。");
				}
				else if (action == "switch_to_secondary" && !activeUnit.UsingPrimaryWeapon)
				{
					Feedback.LogMessage("已经在使用副手武器。");
				}
				else
				{
					if (action == "switch_to_primary") activeUnit.UsingPrimaryWeapon = false;
					else if (action == "switch_to_secondary") activeUnit.UsingPrimaryWeapon = true;
					activeUnit.SwitchWeaponSet();
					Feedback.UpdateUnitInfo(activeUnit);
					var swWeapon = activeUnit.GetMainHand();
					Feedback.LogMessage($"切换武器！当前武器为：【{swWeapon?.ItemName ?? "徒手"}】。");
				}
				Highlight.HighlightAttackRange(activeUnit);
				Selection.Runtime.EnterAttackMode();
				SkillPort.RefreshCurrentHover();
				return;

			case "end_turn":
				Feedback.LogMessage("玩家结束回合。");
				TurnPort.EndCurrentTurn();
				Selection.Runtime.CancelAction();
				break;

			case "retreat":
				Feedback.LogMessage("队伍选择了撤退...");
				EndPort.TriggerCombatEnd(false);
				break;

			case "radial_attack":
				if (Selection.RightClickTarget != null && activeUnit != null)
				{
					Selection.Runtime.EnterAttackMode();
					_ = ActionPort.HandleAttack(Selection.RightClickTarget);
					Selection.RightClickTarget = null;
				}
				break;

			case "build_ladder":
				if (Selection.RightClickTarget != null && activeUnit != null && Selection.RightClickTarget.Data != null)
				{
					var (canLadder, _) = BladeHex.Combat.SiegeActions.CanBuildLadder(
						activeUnit.GridPos, Selection.RightClickTarget.Data, Selection.RightClickTarget.GridPos, activeUnit.CurrentAp);
					if (canLadder)
					{
						activeUnit.CurrentAp -= BladeHex.Combat.SiegeActions.LadderApCost;
						bool completed = BladeHex.Combat.SiegeActions.BuildLadder(Selection.RightClickTarget.Data);
						if (completed)
						{
							Selection.RightClickTarget.Elevation = 1;
							Feedback.LogMessage("[color=green]云梯架设完成！城墙可攀登。[/color]");
						}
						else
						{
							int progress = Selection.RightClickTarget.Data.ladderProgress;
							Feedback.LogMessage($"[color=yellow]云梯建设中 ({progress}/{BladeHex.Combat.SiegeActions.LadderRequiredSteps})...[/color]");
						}
						Feedback.UpdateUnitInfo(activeUnit);
						Highlight.ClearHighlights();
						if (activeUnit.CurrentAp >= 1) Highlight.ShowSelectedUnitHighlights();
					}
					Selection.RightClickTarget = null;
				}
				Selection.Runtime.CancelAction();
				break;

			case "attack_gate":
				if (Selection.RightClickTarget != null && activeUnit != null && Selection.RightClickTarget.Data != null)
				{
					var weapon = activeUnit.GetMainHand() as WeaponData;
					activeUnit.CurrentAp -= weapon?.ApCost ?? 4;
					bool destroyed = BladeHex.Combat.SiegeActions.DamageDestructible(Selection.RightClickTarget.Data);
					if (destroyed)
					{
						Selection.RightClickTarget.Elevation = 1;
						Feedback.LogMessage($"[color=red]城门被破坏！[/color]");
					}
					else
					{
						Feedback.LogMessage($"攻击城门（剩余 {Selection.RightClickTarget.Data.durability}/{Selection.RightClickTarget.Data.maxDurability} 次）");
					}
					Feedback.UpdateUnitInfo(activeUnit);
					Highlight.ClearHighlights();
					if (activeUnit.CurrentAp >= 1) Highlight.ShowSelectedUnitHighlights();
					Selection.RightClickTarget = null;
				}
				Selection.Runtime.CancelAction();
				break;

			case "career_skill":
				if (activeUnit != null)
				{
					if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: career_skill - SkillTree={activeUnit.SkillTree != null}, HasCareerSkill={activeUnit.SkillTree?.GetCareerSkill() != null}");
					var careerInfo = SkillPort.ResolveSkillTargetingInfo("career_skill");
					if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: career_skill - ResolveSkillTargetingInfo returned {(careerInfo != null ? $"range={careerInfo.Value.Range} target={careerInfo.Value.TargetType}" : "NULL")}");
					if (careerInfo != null && SkillPort.IsImmediateCastTargetType(careerInfo.Value.TargetType))
					{
						if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: career_skill immediate cast (target={careerInfo.Value.TargetType})");
						var selfCell = GridQuery.GetCell(activeUnit.GridPos.X, activeUnit.GridPos.Y);
						if (selfCell != null)
						{
							Selection.Runtime.EnterSkillTargeting("career_skill");
							_ = ActionPort.HandleSpell(selfCell);
						}
						return;
					}
					if (DebugLogging) GD.Print("[SkillTarget] OnActionSelected: career_skill entering aim mode");
					Selection.RightClickTarget = null;
					Selection.Runtime.EnterSkillTargeting("career_skill");
					SkillPort.OnActionHovered(action);
					int apCost2 = activeUnit.GetCareerSkill()?.ApCost ?? 0;
					if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: career_skill apCost={apCost2}");
					Feedback.SetApPreview(apCost2);
					SkillPort.RefreshCurrentHover();
					return;
				}
				else
				{
					if (DebugLogging) GD.Print("[SkillTarget] OnActionSelected: career_skill - _activePlayerUnit is null");
				}
				break;

			default:
				if (action.StartsWith("skill_") && activeUnit != null)
				{
					string skillEffect = action["skill_".Length..];
					if (!SkillRegistry.CanUseWithEquipment(skillEffect, activeUnit, out var equipmentReason))
					{
						Selection.Runtime.CancelAction();
						Feedback.LogMessage(equipmentReason);
						return;
					}

					var skillInfo = SkillPort.ResolveSkillTargetingInfo(action);
					if (skillInfo != null && SkillPort.IsImmediateCastTargetType(skillInfo.Value.TargetType))
					{
						if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: skill_{skillEffect} immediate cast (target={skillInfo.Value.TargetType})");
						var selfCell = GridQuery.GetCell(activeUnit.GridPos.X, activeUnit.GridPos.Y);
						if (selfCell != null)
						{
							Selection.Runtime.EnterSkillTargeting(action);
							_ = ActionPort.HandleSpell(selfCell);
						}
						return;
					}
					if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: skill_{skillEffect} case entered");
					Selection.RightClickTarget = null;
					Selection.Runtime.EnterSkillTargeting(action);
					SkillPort.OnActionHovered(action);
					int skillCost = SkillRegistry.GetActionCost(skillEffect, activeUnit);
					if (DebugLogging) GD.Print($"[SkillTarget] OnActionSelected: skill_{skillEffect} cost={skillCost}");
					Feedback.SetApPreview(skillCost);
					SkillPort.RefreshCurrentHover();
					return;
				}
				else
				{
					Selection.Runtime.CancelAction();
				}
				break;
		}
		SkillPort.RefreshCurrentHover();
		ActionDispatched?.Invoke(action);
	}

	/// <summary>处理法术选择</summary>
	public void OnSpellSelected(SpellData spell)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (activeUnit == null) return;
		Feedback.CloseSpellPanel();
		EnterSpellTargetingOrCastSelf(activeUnit, spell);
	}

	private void OnEquippedSpellSelected(string action, Unit activeUnit)
	{
		if (activeUnit.Data == null)
		{
			Selection.Runtime.CancelAction();
			return;
		}

		string spellId = SpellStudyCatalog.GetSpellIdFromEntry(action);
		var spell = SpellStudyCatalog.GetKnownSpell(activeUnit.Data, spellId);
		if (spell == null)
		{
			Feedback.LogMessage("该法术尚未学习或已失效。");
			Selection.Runtime.CancelAction();
			return;
		}

		EnterSpellTargetingOrCastSelf(activeUnit, spell);
	}

	private void EnterSpellTargetingOrCastSelf(Unit activeUnit, SpellData spell)
	{
		int apCost = spell.castingTime == SpellData.CastingTime.MainAction ? 4 : 0;
		Feedback.SetApPreview(apCost);
		Selection.Runtime.EnterSpellTargeting(spell);

		if (spell.RangeCells <= 0 || spell.shape == SpellData.SpellShape.Self)
		{
			Feedback.LogMessage($"[color=orange]施放法术：{spell.SpellName}[/color]");
			var selfCell = GridQuery.GetCell(activeUnit.GridPos.X, activeUnit.GridPos.Y);
			if (selfCell != null)
				_ = ActionPort.HandleSpell(selfCell);
			return;
		}

		Feedback.LogMessage($"[color=orange]选择法术：{spell.SpellName}[/color] — 请点击射程内的目标。");
		Highlight.HighlightRange(activeUnit, spell.RangeCells, new Color(1, 0.5f, 0, 0.4f));
		SkillPort.RefreshCurrentHover();
	}
}
