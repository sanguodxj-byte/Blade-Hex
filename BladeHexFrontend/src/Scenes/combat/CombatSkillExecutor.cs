// CombatSkillExecutor.cs
// 从 CombatSceneBase 提取的技能/行动执行器。
// 负责：移动行动、物理攻击行动、技能/法术释放行动、药水物品使用行动、死亡结算与技能反馈。
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Combat.Commands;
using BladeHex.Combat.Skills;
using BladeHex.View.Combat;
using BladeHex.View.Effects;
using BladeHex.UI.Combat;
using BladeHex.Data;

namespace BladeHex.Scenes;

[GlobalClass]
public partial class CombatSkillExecutor : Node
{
	// ===== 小 ports 依赖 =====
	private ICombatSelectionContext? _selection;
	private ICombatHighlightPort? _highlight;
	private ICombatFeedbackPort? _feedback;
	private ICombatGridQuery? _gridQuery;
	private ICombatTurnPort? _turnPort;
	private CombatMovementController? _movementCtrl;
	private CombatAttackAnimator? _attackAnimator;
	private CombatUI? _combatUi;
	private CombatManager? _combatManager;
	private HexGrid? _hexGrid;
	private Node3D? _parentScene;
	private EffectOrchestrator? _effectOrchestrator;
	public BladeHex.Audio.AudioManager? AudioManager { get; set; }

	// ===== 访问器（失败快速） =====
	private ICombatSelectionContext Selection => _selection ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private ICombatHighlightPort Highlight => _highlight ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private ICombatFeedbackPort Feedback => _feedback ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private ICombatGridQuery GridQuery => _gridQuery ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private ICombatTurnPort TurnPort => _turnPort ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	public CombatMovementController MovementCtrl => _movementCtrl ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	public CombatAttackAnimator AttackAnimator => _attackAnimator ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private CombatUI CombatUi => _combatUi ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private CombatManager CombatMgr => _combatManager ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	private HexGrid HexGrd => _hexGrid ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");
	public Node3D ParentScene => _parentScene ?? throw new InvalidOperationException("CombatSkillExecutor not initialized.");

	/// <summary>注入必要依赖。</summary>
	public void Initialize(
		ICombatSelectionContext selection,
		ICombatHighlightPort highlight,
		ICombatFeedbackPort feedback,
		ICombatGridQuery gridQuery,
		ICombatTurnPort turnPort,
		CombatMovementController movementCtrl,
		CombatAttackAnimator attackAnimator,
		CombatUI combatUi,
		CombatManager combatManager,
		HexGrid hexGrid,
		Node3D parentScene,
		BladeHex.Audio.AudioManager? audioManager = null,
		EffectOrchestrator? effectOrchestrator = null)
	{
		_selection = selection ?? throw new ArgumentNullException(nameof(selection));
		_highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
		_feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
		_gridQuery = gridQuery ?? throw new ArgumentNullException(nameof(gridQuery));
		_turnPort = turnPort ?? throw new ArgumentNullException(nameof(turnPort));
		_movementCtrl = movementCtrl ?? throw new ArgumentNullException(nameof(movementCtrl));
		_attackAnimator = attackAnimator ?? throw new ArgumentNullException(nameof(attackAnimator));
		_combatUi = combatUi ?? throw new ArgumentNullException(nameof(combatUi));
		_combatManager = combatManager ?? throw new ArgumentNullException(nameof(combatManager));
		_hexGrid = hexGrid ?? throw new ArgumentNullException(nameof(hexGrid));
		_parentScene = parentScene ?? throw new ArgumentNullException(nameof(parentScene));
		AudioManager = audioManager;
		_effectOrchestrator = effectOrchestrator;
	}

	// ===== 核心业务执行入口 =====

	private SpellManager? _spellManager;
	private SpellManager SpellMgr => _spellManager ??= (ParentScene as CombatSceneBase)?.SpellManager ?? throw new InvalidOperationException("SpellManager not available.");

	private Unit? FindRuntimeUnit(BattleUnitModel targetModel)
	{
		var targetData = targetModel.Data;
		var byReference = CombatMgr.AllUnits.FirstOrDefault(u =>
			GodotObject.IsInstanceValid(u)
			&& (ReferenceEquals(u.Model, targetModel) || ReferenceEquals(u.Model.Data, targetData) || ReferenceEquals(u.Data, targetData)));
		if (byReference != null) return byReference;

		int characterId = targetData?.CharacterId ?? -1;
		if (characterId >= 0)
		{
			return CombatMgr.AllUnits.FirstOrDefault(u =>
				GodotObject.IsInstanceValid(u)
				&& u.Data != null
				&& u.Data.CharacterId == characterId);
		}

		return null;
	}

	private void ProcessSkillExecutionFeedback(Unit caster, SkillExecutionResult result)
	{
		if (!result.Success) return;

		foreach (var sub in result.SubResults)
		{
			switch (sub)
			{
				case DamageEvent dmg:
					var targetNode = FindRuntimeUnit(dmg.Target);
					if (targetNode != null && GodotObject.IsInstanceValid(targetNode))
					{
						// 弹出伤害数字
						BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, targetNode, dmg.Damage, dmg.IsCritical);

						// 触发受击
						if (targetNode.RenderBus != null) targetNode.RenderBus.NotifyHit(targetNode);

						CombatUi.LogMessage($"[color=red]{caster.Data?.UnitName ?? "单位"} 释放技能对 {targetNode.Data?.UnitName ?? "单位"} 造成 {dmg.Damage} 伤害！[/color]");

						// 刷新 UI
						CombatUi.UpdateUnitInfo(targetNode);

						// 死亡处理
						if (targetNode.CurrentHp <= 0)
						{
							AudioManager?.PlaySfxName("combat_death");
							CombatUi.LogMessage($"[color=yellow]{targetNode.Data?.UnitName} 被击败！[/color]");
							CombatMgr.HandleUnitKilled(targetNode, caster);
						}
					}
					break;

				case HealEvent heal:
					var healTargetNode = FindRuntimeUnit(heal.Target);
					if (healTargetNode != null && GodotObject.IsInstanceValid(healTargetNode))
					{
						// 弹出治疗数字
						BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, healTargetNode, -heal.Amount, false);

						CombatUi.LogMessage($"[color=green]{caster.Data?.UnitName ?? "单位"} 释放技能对 {healTargetNode.Data?.UnitName ?? "单位"} 治疗 {heal.Amount} 生命。[/color]");

						// 刷新 UI
						CombatUi.UpdateUnitInfo(healTargetNode);
					}
					break;

				case StatusEffectApplication eff:
					var effTargetNode = FindRuntimeUnit(eff.Target);
					if (effTargetNode != null && GodotObject.IsInstanceValid(effTargetNode))
					{
						CombatUi.LogMessage($"对 {effTargetNode.Data?.UnitName} 施加状态：{eff.EffectId}");
					}
					break;

				case TeleportEvent tp:
					var tpUnitNode = FindRuntimeUnit(tp.Unit);
					if (tpUnitNode != null && GodotObject.IsInstanceValid(tpUnitNode))
					{
						CombatUi.LogMessage($"{tpUnitNode.Data?.UnitName ?? "单位"} 传送到 ({tp.Destination.X}, {tp.Destination.Y})");
					}
					break;

				case BuffApplication buff:
					var buffTargetNode = FindRuntimeUnit(buff.Target);
					if (buffTargetNode != null && GodotObject.IsInstanceValid(buffTargetNode))
					{
						CombatUi.LogMessage($"对 {buffTargetNode.Data?.UnitName ?? "单位"} 施加效果：{buff.BuffId}");
						CombatUi.UpdateUnitInfo(buffTargetNode);
					}
					break;

				case ResultText txt:
					CombatUi.LogMessage(txt.Text);
					break;
			}
		}

		// 刷新施法者 UI
		CombatUi.UpdateUnitInfo(caster);
	}

	public async Task<CombatActionResult> HandleMove(HexCell cell)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (Selection.IsExecutingAction)
		{
			GD.Print("[HandleMove] blocked: IsExecutingAction=true");
			return CombatActionResult.Fail("正在执行动作。", "move");
		}

		// null 守卫
		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit))
		{
			GD.PushWarning("[HandleMove] activeUnit null/invalid");
			return CombatActionResult.Fail("未选中动作单位。", "move");
		}

		// 计算路径
		var path = HexGrd.FindPath(activeUnit.GridPos, cell.GridPos);
		if (path == null || path.Count == 0)
		{
			GD.PushWarning("[HandleMove] path null/empty");
			return CombatActionResult.Fail("无可达路径。", "move");
		}

		float pathCost = HexGrd.GetPathCost(activeUnit.GridPos, path);
		if (pathCost > activeUnit.CurrentAp)
		{
			CombatUi.LogMessage("行动力不足。");
			return CombatActionResult.Fail("行动力不足。", "move");
		}

		// 执行移动
		Selection.IsExecutingAction = true;
		Selection.Runtime.LockInteraction();
		try
		{
			await MovementCtrl.MoveUnitToAsync(activeUnit, cell.GridPos.X, cell.GridPos.Y, path);
			activeUnit.CurrentAp -= pathCost;
			CombatUi.UpdateUnitInfo(activeUnit);
			return CombatActionResult.Ok("move", consumedAp: pathCost);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"HandleMove error: {ex.Message}");
			return CombatActionResult.Fail($"移动异常: {ex.Message}", "move");
		}
		finally
		{
			Selection.IsExecutingAction = false;
			Selection.Runtime.UnlockInteraction();
		}
	}

	public async Task<CombatActionResult> HandleAttack(HexCell cell)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (Selection.IsExecutingAction)
		{
			GD.Print("[HandleAttack] blocked: IsExecutingAction=true");
			return CombatActionResult.Fail("正在执行动作。", "attack");
		}

		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit))
		{
			GD.PushWarning("[HandleAttack] activeUnit null/invalid");
			return CombatActionResult.Fail("未选中动作单位。", "attack");
		}

		if (cell.Occupant == null
			&& CombatMgr.TryGetBattleAnchorAt(cell.GridPos, out var anchor)
			&& anchor.Destructible)
		{
			return await HandleBattleAnchorAttack(activeUnit, cell, anchor);
		}

		if (cell.Occupant == null || !GodotObject.IsInstanceValid(cell.Occupant))
		{
			GD.PushWarning("[HandleAttack] target null/invalid");
			return CombatActionResult.Fail("目标无效。", "attack");
		}

		var weapon = activeUnit.GetMainHand() as WeaponData;
		int apCost = weapon?.ApCost ?? 4;

		if (activeUnit.CurrentAp < apCost)
		{
			CombatUi.LogMessage("行动力不足。");
			return CombatActionResult.Fail("行动力不足。", "attack");
		}

		if (CombatAttackRules.IsMeleeElevationBlocked(activeUnit, cell.Occupant, HexGrd))
		{
			Feedback.LogMessage(CombatAttackRules.MeleeElevationBlockedReason);
			return CombatActionResult.Fail(CombatAttackRules.MeleeElevationBlockedReason, "attack");
		}

		// 创建 AttackCommand
		var command = new AttackCommand(
			(long)activeUnit.GetInstanceId(),
			(long)cell.Occupant.GetInstanceId());

		// 使用 CombatMgr.ExecuteCommand 入栈
		var cmdResult = CombatMgr.ExecuteCommand(command);
		if (!cmdResult.Success)
		{
			string failMsg = cmdResult.FailureReason ?? "执行攻击失败。";
			Feedback.LogMessage(failMsg);
			return CombatActionResult.Fail(failMsg, "attack");
		}

		// 执行攻击
		Selection.IsExecutingAction = true;
		Selection.Runtime.LockInteraction();
		try
		{
			activeUnit.CurrentAp -= apCost;
			await AttackAnimator.PlayAttack(activeUnit, cell.Occupant, weapon);

			// 真实结算攻击
			var allies = CombatMgr.AllUnits
				.Where(u => GodotObject.IsInstanceValid(u) && u != activeUnit && u.CurrentHp > 0
						 && u.IsPlayerSide == activeUnit.IsPlayerSide)
				.ToArray();
			var defenderAllies = CombatMgr.AllUnits
				.Where(u => GodotObject.IsInstanceValid(u) && u != cell.Occupant && u.CurrentHp > 0
						 && u.IsPlayerSide == cell.Occupant.IsPlayerSide)
				.ToArray();
			var result = CombatResolver.ResolveAttack(
				activeUnit, cell.Occupant, HexGrd, false, false, 0, 1.0f,
				attackerAllies: allies,
				defenderAllies: defenderAllies);

			if (result.ContainsKey("blocked_by_elevation") && result["blocked_by_elevation"].AsBool())
			{
				string reason = result.ContainsKey("reason") ? result["reason"].AsString() : CombatAttackRules.MeleeElevationBlockedReason;
				Feedback.LogMessage(reason);
				return CombatActionResult.Fail(reason, "attack");
			}

			if (result["hit"].AsBool())
			{
				int dmg = result["damage"].AsInt32();
				bool isCrit = result.ContainsKey("critical") && result["critical"].AsBool();

				// 播放命中音效
				AudioManager?.PlayAttackHitSfx((int)(weapon?.WeaponDamageType ?? WeaponData.DamageType.Slash), isCrit);

				// 弹出伤害数字
				BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, cell.Occupant, dmg, isCrit);

				// 记录日志
				var logParts = new List<string>
				{
					$"[color=red]{activeUnit.Data!.UnitName} 命中 {cell.Occupant.Data!.UnitName}，造成 {dmg} 伤害[/color]"
				};
				if (isCrit) logParts.Add("[color=yellow]★暴击!  [/color]");
				CombatUi.LogMessage(string.Join(" ", logParts));

				// 刷新 UI
				CombatUi.UpdateUnitInfo(cell.Occupant);

				// 击杀处理
				if (cell.Occupant.CurrentHp <= 0)
				{
					AudioManager?.PlaySfxName("combat_death");
					CombatUi.LogMessage($"[color=yellow]{cell.Occupant.Data!.UnitName} 被击败！[/color]");
					CombatMgr.HandleUnitKilled(cell.Occupant, activeUnit);
				}
			}
			else
			{
				// 播放未中音效
				AudioManager?.PlayAttackMissSfx((int)(weapon?.WeaponDamageType ?? WeaponData.DamageType.Slash));

				// 弹出 Miss
				BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, cell.Occupant, 0, false,
					result.ContainsKey("fumble") && result["fumble"].AsBool() ? "Fumble" : "Miss");

				CombatUi.LogMessage($"[color=gray]{activeUnit.Data!.UnitName} 的攻击未命中 {cell.Occupant.Data!.UnitName}。[/color]");
			}

			// 剑舞者额外攻击 VFX
			if (result.ContainsKey("blade_dancer_extra_hit") && result["blade_dancer_extra_hit"].AsBool()
			    && result.ContainsKey("blade_dancer_extra_target"))
			{
				var extraTarget = result["blade_dancer_extra_target"].AsGodotObject() as Unit;
				if (extraTarget != null && GodotObject.IsInstanceValid(extraTarget))
				{
					int extraDmg = result.ContainsKey("blade_dancer_extra_damage") ? result["blade_dancer_extra_damage"].AsInt32() : 0;
					bool extraCrit = result.ContainsKey("blade_dancer_extra_crit") && result["blade_dancer_extra_crit"].AsBool();
					bool extraHit = result["blade_dancer_extra_hit"].AsBool();

					if (extraHit)
					{
						BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, extraTarget, extraDmg, extraCrit);
						CombatUi.LogMessage($"[color=red]{activeUnit!.Data!.UnitName} 剑舞者额外命中 {extraTarget.Data!.UnitName}，造成 {extraDmg} 伤害[/color]");

						if (extraTarget.CurrentHp <= 0)
						{
							AudioManager?.PlaySfxName("combat_death");
							CombatUi.LogMessage($"[color=yellow]{extraTarget.Data!.UnitName} 被剑舞者额外攻击击败！[/color]");
							CombatMgr.HandleUnitKilled(extraTarget, activeUnit);
						}

						CombatUi.UpdateUnitInfo(extraTarget);
					}
				}
			}

			CombatUi.UpdateUnitInfo(activeUnit);
			Selection.Runtime.CancelAction();
			return CombatActionResult.Ok("attack", consumedAp: apCost);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"HandleAttack error: {ex.Message}");
			return CombatActionResult.Fail($"攻击执行异常: {ex.Message}", "attack");
		}
		finally
		{
			Selection.IsExecutingAction = false;
			Selection.Runtime.UnlockInteraction();

			// 清除高亮并刷新
			Highlight.ClearHighlights();
			Highlight.ShowSelectedUnitHighlights();
		}
	}

	private async Task<CombatActionResult> HandleBattleAnchorAttack(
		Unit activeUnit,
		HexCell cell,
		CombatManager.BattleAnchorState anchor)
	{
		var check = CombatAttackRules.CanAttackBattleAnchor(activeUnit, anchor);

		if (!check.CanAttack)
		{
			if (!string.IsNullOrEmpty(check.Reason))
				Feedback.LogMessage(check.Reason);
			return CombatActionResult.Fail(
				string.IsNullOrEmpty(check.Reason) ? "无法攻击战旗。" : check.Reason,
				"attack");
		}

		Selection.IsExecutingAction = true;
		Selection.Runtime.LockInteraction();
		try
		{
			activeUnit.CurrentAp -= check.ApCost;
			await ToSignal(GetTree().CreateTimer(CombatSpeed.ScaleSeconds(0.12)), SceneTreeTimer.SignalName.Timeout);

			var damageRoll = activeUnit.RollDamage();
			int damage = damageRoll.ContainsKey("total") ? damageRoll["total"].AsInt32() : 1;
			bool removed = CombatMgr.DamageBattleAnchor(anchor.Source, damage);

			var popupPos = cell.GlobalPosition
				+ new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer + 55f, 0);
			BladeHex.View.Combat.DamageNumberPopup.Spawn(ParentScene, popupPos, damage, false);
			CombatUi.LogMessage($"[color=red]{activeUnit.Data?.UnitName ?? "单位"} 攻击战旗，造成 {damage} 伤害。[/color]");
			if (removed && !CombatMgr.TryGetBattleAnchor(anchor.Source, out _))
			{
				CombatUi.LogMessage("[color=yellow]战旗被摧毁。[/color]");
			}

			CombatUi.UpdateUnitInfo(activeUnit);
			Selection.Runtime.CancelAction();
			return CombatActionResult.Ok("attack", consumedAp: check.ApCost);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"HandleBattleAnchorAttack error: {ex.Message}");
			return CombatActionResult.Fail($"攻击战旗异常: {ex.Message}", "attack");
		}
		finally
		{
			Selection.IsExecutingAction = false;
			Selection.Runtime.UnlockInteraction();
			Highlight.ClearHighlights();
			Highlight.ShowSelectedUnitHighlights();
		}
	}

	public async Task<CombatActionResult> HandleSpell(HexCell cell)
	{
		var activeUnit = Selection.ActivePlayerUnit;
		var selectedSpell = Selection.SelectedSpell;
		var selectedSkillAction = Selection.SelectedSkillAction;

		if (Selection.IsExecutingAction)
		{
			GD.Print("[HandleSpell] blocked: IsExecutingAction=true");
			return CombatActionResult.Fail("正在执行动作。", "spell");
		}

		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit))
		{
			GD.PushWarning("[HandleSpell] activeUnit null/invalid");
			return CombatActionResult.Fail("未选中动作单位。", "spell");
		}

		// 技能释放
		if (!string.IsNullOrEmpty(selectedSkillAction))
		{
			var info = _parentScene is ICombatSkillPort sp ? sp.ResolveSkillTargetingInfo(selectedSkillAction) : null;
			if (info == null || !info.Value.IsCellValid(cell, CombatMgr))
			{
				Feedback.LogMessage("选择的目标无效。");
				return CombatActionResult.Fail("选择的目标无效。", "skill");
			}

			return await HandleSkillAction(activeUnit, selectedSkillAction, cell);
		}

		// 法术释放
		if (selectedSpell != null)
		{
			int dist = HexUtils.AxialDistance(activeUnit.GridPos, cell.GridPos);
			if (dist > selectedSpell.RangeCells)
			{
				Feedback.LogMessage("超出法术射程。");
				return CombatActionResult.Fail("超出法术射程。", "spell");
			}

			return await HandleSpellCast(activeUnit, selectedSpell, cell);
		}

		GD.PushWarning("[HandleSpell] no spell or skill selected");
		return CombatActionResult.Fail("未选择任何法术或技能。", "spell");
	}

	private async Task<CombatActionResult> HandleSkillAction(Unit activeUnit, string skillAction, HexCell cell)
	{
		string? normalizedSkill = skillAction == "career_skill" ? null : NormalizeSkillEffect(skillAction);
		if (normalizedSkill != null && CombatAttackRules.IsMeleeSkillElevationBlocked(activeUnit, normalizedSkill, cell, HexGrd))
		{
			Feedback.LogMessage(CombatAttackRules.MeleeElevationBlockedReason);
			return CombatActionResult.Fail(CombatAttackRules.MeleeElevationBlockedReason, skillAction);
		}

		Selection.IsExecutingAction = true;
		Selection.Runtime.LockInteraction();
		try
		{
			SkillExecutionResult typedResult;
			int consumedAp = 0;
			string? visualSkillId = null;

			if (skillAction == "career_skill")
			{
				CombatUi.LogMessage("释放职业技能！");
				visualSkillId = activeUnit.GetCareerSkill()?.EffectId;
				typedResult = CombatMgr.UseCareerSkill(activeUnit, cell.GridPos, HexGrd);
				// 职业技能一般消耗 0 AP (UseCareerSkill 内部自行校验，HasActed 会打标)
			}
			else
			{
				string skillEffect = normalizedSkill ?? NormalizeSkillEffect(skillAction);
				visualSkillId = skillEffect;
				CombatUi.LogMessage($"释放技能：{skillEffect}");
				consumedAp = SkillRegistry.GetActionCost(skillEffect, activeUnit, cell.GridPos);
				typedResult = CombatMgr.UseSkill(activeUnit, skillEffect, cell.GridPos, HexGrd);
			}

			if (typedResult.Success)
			{
				PlaySkillPresentation(cell, visualSkillId);
				ProcessSkillExecutionFeedback(activeUnit, typedResult);

				Highlight.ClearHighlights();
				Selection.Runtime.CancelAction();
				Highlight.ShowSelectedUnitHighlights();
				await Task.CompletedTask;
				return CombatActionResult.Ok(skillAction, consumedAp: consumedAp);
			}
			else
			{
				string failReason = typedResult.FailureReason ?? "释放技能失败。";
				Feedback.LogMessage(failReason);
				return CombatActionResult.Fail(failReason, skillAction);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"HandleSkillAction error: {ex.Message}");
			return CombatActionResult.Fail($"技能执行异常: {ex.Message}", skillAction);
		}
		finally
		{
			Selection.IsExecutingAction = false;
			Selection.Runtime.UnlockInteraction();
		}
	}

	private static string NormalizeSkillEffect(string skillAction)
	{
		return skillAction.StartsWith("skill_", StringComparison.Ordinal)
			? skillAction["skill_".Length..]
			: skillAction;
	}

	private void PlaySkillPresentation(HexCell cell, string? skillId)
	{
		if (string.IsNullOrEmpty(skillId)) return;
		if (SkillEffectExecutor.GetSkillConfig(skillId).Count == 0) return;

		if (_effectOrchestrator != null && GodotObject.IsInstanceValid(_effectOrchestrator))
			_effectOrchestrator.PlaySkillCast(ParentScene, cell.GlobalPosition, skillId, AudioManager);
		else
			SkillVfxExecutor.Execute(ParentScene, cell.GlobalPosition, skillId, AudioManager);
	}

	private async Task<CombatActionResult> HandleSpellCast(Unit activeUnit, SpellData spell, HexCell cell)
	{
		int apCost = spell.castingTime == SpellData.CastingTime.MainAction ? 4 : 0;

		// v1 职业被动: 鏖战骑士/天启骑士 — 免费法术不消耗 AP
		bool freeAp = activeUnit.Data?.Runtime?.CareerNextSpellFreeAp == true;
		if (freeAp)
		{
			apCost = 0;
			// 标记在施法成功后消耗，避免失败吞掉免费机会
		}

		if (activeUnit.CurrentAp < apCost)
		{
			Feedback.LogMessage("行动力不足。");
			return CombatActionResult.Fail("行动力不足。", "spell");
		}

		Selection.IsExecutingAction = true;
		Selection.Runtime.LockInteraction();
		try
		{
			var castResult = SpellMgr.CastSpell(activeUnit, spell, cell.GridPos, HexGrd);

			if (castResult["success"].AsBool())
			{
				// v1 职业被动: 施法成功后消耗免费 AP 标记
				if (freeAp && activeUnit.Data?.Runtime != null)
					activeUnit.Data.Runtime.CareerNextSpellFreeAp = false;

				activeUnit.CurrentAp -= apCost;
				CombatUi.LogMessage($"施放法术：{spell.SpellName}");

				// 处理结果反馈
				var results = castResult.ContainsKey("results") ? castResult["results"].As<Godot.Collections.Array>() : null;
				if (results != null)
				{
					foreach (var itemObj in results)
					{
						var res = itemObj.As<Godot.Collections.Dictionary>();
						var targetNode = res["target"].As<Unit>();
						if (targetNode == null || !GodotObject.IsInstanceValid(targetNode)) continue;

						if (res.ContainsKey("damage") && res["damage"].AsInt32() > 0)
						{
							int dmg = res["damage"].AsInt32();
							bool isCrit = res.ContainsKey("critical") && res["critical"].AsBool();

							// 播放命中音效
							AudioManager?.PlayAttackHitSfx(spell.DamageType == "force" ? 0 : 1, isCrit);

							// 弹出伤害数字
							BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, targetNode, dmg, isCrit);

							// 触发受击
							if (targetNode.RenderBus != null) targetNode.RenderBus.NotifyHit(targetNode);

							CombatUi.LogMessage($"[color=red]{activeUnit.Data?.UnitName ?? "单位"} 的法术命中 {targetNode.Data?.UnitName ?? "单位"}，造成 {dmg} 伤害！[/color]");

							// 死亡处理
							if (targetNode.CurrentHp <= 0)
							{
								AudioManager?.PlaySfxName("combat_death");
								CombatUi.LogMessage($"[color=yellow]{targetNode.Data?.UnitName} 被击败！[/color]");
								CombatMgr.HandleUnitKilled(targetNode, activeUnit);
							}
						}
						else if (res.ContainsKey("amount") && res["amount"].AsInt32() > 0)
						{
							int heal = res["amount"].AsInt32();
							BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, targetNode, -heal, false);
							CombatUi.LogMessage($"[color=green]{activeUnit.Data?.UnitName ?? "单位"} 的法术治疗 {targetNode.Data?.UnitName ?? "单位"}，恢复 {heal} 生命！[/color]");
						}
						else if (res.ContainsKey("hit") && !res["hit"].AsBool())
						{
							AudioManager?.PlayAttackMissSfx(spell.DamageType == "force" ? 0 : 1);
							BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, targetNode, 0, false, "Miss");
							CombatUi.LogMessage($"[color=gray]{activeUnit.Data?.UnitName ?? "单位"} 的法术未命中 {targetNode.Data?.UnitName ?? "单位"}。[/color]");
						}

						CombatUi.UpdateUnitInfo(targetNode);
					}
				}

				Highlight.ClearHighlights();
				Selection.Runtime.CancelAction();
				Highlight.ShowSelectedUnitHighlights();

				CombatUi.UpdateUnitInfo(activeUnit);
				await Task.CompletedTask;
				return CombatActionResult.Ok("spell", consumedAp: apCost);
			}
			else
			{
				string reason = castResult.ContainsKey("reason") ? castResult["reason"].AsString() : "施放法术失败。";
				Feedback.LogMessage(reason);
				return CombatActionResult.Fail(reason, "spell");
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"HandleSpellCast error: {ex.Message}");
			return CombatActionResult.Fail($"法术执行异常: {ex.Message}", "spell");
		}
		finally
		{
			Selection.IsExecutingAction = false;
			Selection.Runtime.UnlockInteraction();
		}
	}

	public CombatActionResult HandleItem(HexCell cell)
	{
		var activeUnit = Selection.ActivePlayerUnit;

		if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit))
		{
			GD.PushWarning("[HandleItem] activeUnit null/invalid");
			return CombatActionResult.Fail("未选中动作单位。", "item");
		}

		if (cell.Occupant == null || !GodotObject.IsInstanceValid(cell.Occupant))
		{
			GD.PushWarning("[HandleItem] target null/invalid");
			return CombatActionResult.Fail("目标无效。", "item");
		}

		var item = activeUnit.Data?.Consumables.FirstOrDefault();
		if (item == null)
		{
			Feedback.LogMessage("背包中没有消耗品。");
			return CombatActionResult.Fail("背包中没有消耗品。", "item");
		}

		int apCost = 1;
		if (activeUnit.CurrentAp < apCost)
		{
			Feedback.LogMessage("行动力不足。");
			return CombatActionResult.Fail("行动力不足。", "item");
		}

		// 真实解算
		var res = ConsumableManager.UseConsumable(activeUnit, item, cell.GridPos, HexGrd);

		if (res["success"].AsBool())
		{
			activeUnit.CurrentAp -= apCost;
			string effect = res["effect"].AsString();
			int amount = res["amount"].AsInt32();

			CombatUi.LogMessage($"使用物品：{item.ItemName}");

			if (effect == "heal" && amount > 0)
			{
				BladeHex.View.Combat.DamageNumberPopup.SpawnAtUnit(ParentScene, cell.Occupant, -amount, false);
				CombatUi.LogMessage($"[color=green]{activeUnit.Data?.UnitName} 对 {cell.Occupant.Data?.UnitName} 使用 {item.ItemName}，恢复 {amount} 生命。[/color]");
			}
			else
			{
				CombatUi.LogMessage($"[color=yellow]{activeUnit.Data?.UnitName} 对 {cell.Occupant.Data?.UnitName} 使用了 {item.ItemName}。[/color]");
			}

			Highlight.ClearHighlights();
			Selection.Runtime.CancelAction();
			Highlight.ShowSelectedUnitHighlights();

			CombatUi.UpdateUnitInfo(activeUnit);
			CombatUi.UpdateUnitInfo(cell.Occupant);

			return CombatActionResult.Ok("item", consumedAp: apCost);
		}
		else
		{
			Feedback.LogMessage("使用物品失败。");
			return CombatActionResult.Fail("使用物品失败。", "item");
		}
	}
}
