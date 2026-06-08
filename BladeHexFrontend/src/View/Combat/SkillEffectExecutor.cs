using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat.Skills;

namespace BladeHex.Combat;

/// <summary>
/// 技能特效执行引擎 — Lua 脚本分发
/// 所有主动技能通过 LuaSkillBridge 执行，法术槽为 UI 层特殊拦截。
/// </summary>
public static class SkillEffectExecutor
{
    // ============================================================================
    // 法术研习槽 stub（UI 层特殊处理，不走 Lua）
    // ============================================================================

    private static readonly HashSet<string> SpellSlotIds = new()
    {
        "spell_slot_1", "spell_slot_2", "spell_slot_3", "spell_slot_4", "spell_slot_5",
    };

    /// <summary>
    /// 法术研习槽节点的 stub。spell_slot_X 不通过 UseSkill 调用 — 节点激活时
    /// UI 层调出"选系面板"，从该环位的 5 系（毁灭/幻术/附魔/防护/生命）中选 1 个，
    /// 把对应法术写入 LearnedSpells。详见 docs/法表系统.md §5.1
    /// </summary>
    private static void StubSpellSlot(in SkillHandlerContext ctx)
    {
        SkillUtils.Fail(ctx.Builder, "法术槽节点 — 请通过节点激活面板选择法术学派");
    }


    // ============================================================================
    // 基础接口（保持向后兼容）
    // ============================================================================

    public static Godot.Collections.Dictionary GetSkillConfig(string skillEffect) => SkillRegistry.GetSkillConfig(skillEffect);
    public static bool IsActiveSkill(string skillEffect) => SkillRegistry.IsActiveSkill(skillEffect);
    public static bool IsPassiveSkill(string skillEffect) => SkillRegistry.IsPassiveSkill(skillEffect);

	// ============================================================================
	// 主动技能执行 — 主入口（Builder 驱动，返回强类型结果）
	// ============================================================================

	/// <summary>
	/// 执行主动技能，返回强类型 SkillExecutionResult。
	/// 使用 SkillResultBuilder 收集子结果，handler 通过 ctx.Builder 写入。
	/// </summary>
	public static SkillExecutionResult ExecuteActiveSkill(
		Unit attacker,
		string skillEffect,
		Vector2I targetCell,
		HexGrid? grid,
		IEnumerable<Unit> allUnits,
		IEnumerable<Unit> playerUnits,
		IEnumerable<Unit> enemyUnits
	)
	{
		var cfg = GetSkillConfig(skillEffect);
		if (cfg.Count == 0 || !IsActiveSkill(skillEffect))
		{
			return SkillExecutionResult.Fail("未知或非主动技能");
		}

		var allies = !attacker.Data!.IsEnemy ? playerUnits : enemyUnits;
		var enemies = !attacker.Data!.IsEnemy ? enemyUnits : playerUnits;

		var builder = new SkillResultBuilder();

		// 注册表分发：法术槽 UI 拦截，其余走 Lua 脚本
		var ctx = new SkillHandlerContext
		{
			Attacker = attacker,
			TargetCell = targetCell,
			Grid = grid,
			Enemies = enemies,
			Allies = allies,
			Builder = builder,
		};

		if (SpellSlotIds.Contains(skillEffect))
		{
			StubSpellSlot(in ctx);
		}
		else if (SkillRegistry.IsIntrinsicSkillName(skillEffect))
		{
			ExecuteIntrinsicSkill(skillEffect, in ctx);
		}
		else if (LuaSkillBridge.Execute(skillEffect, in ctx))
		{
			// Lua 脚本已处理（成功或失败都由脚本内部通过 builder 设置）
		}
		else
		{
			builder.Fail($"技能 {skillEffect} 逻辑尚未注册");
		}

		return builder.Build();
	}

	// ============================================================================
	// 向后兼容接口
	// ============================================================================

	/// <summary>
	/// T3.2: 执行主动技能，返回强类型结果（直接调用 ExecuteActiveSkill）
	/// </summary>
	public static SkillExecutionResult ExecuteActiveSkillTyped(
		Unit attacker,
		string skillEffect,
		Vector2I targetCell,
		HexGrid? grid,
		IEnumerable<Unit> allUnits,
		IEnumerable<Unit> playerUnits,
		IEnumerable<Unit> enemyUnits
	)
	{
		return ExecuteActiveSkill(attacker, skillEffect, targetCell, grid, allUnits, playerUnits, enemyUnits);
	}

	/// <summary>
	/// 向后兼容：执行主动技能，返回 Godot Dictionary。
	/// 内部调用强类型版本并通过 ToDictionary() 转换。
	/// </summary>
	public static Godot.Collections.Dictionary ExecuteActiveSkillDict(
		Unit attacker,
		string skillEffect,
		Vector2I targetCell,
		HexGrid? grid,
		IEnumerable<Unit> allUnits,
		IEnumerable<Unit> playerUnits,
		IEnumerable<Unit> enemyUnits
	)
	{
		var typed = ExecuteActiveSkill(attacker, skillEffect, targetCell, grid, allUnits, playerUnits, enemyUnits);
		var dict = typed.ToDictionary();
		// 补充技能配置元数据（旧接口需要）
		var cfg = GetSkillConfig(skillEffect);
		dict["action_cost"] = SkillRegistry.GetActionCost(skillEffect, attacker);
		dict["vfx_type"] = cfg.ContainsKey("vfx") ? cfg["vfx"].AsString() : "";
		return dict;
	}

	// ============================================================================
	// Dictionary → 强类型转换（向后兼容，供外部调用方使用）
	// ============================================================================

	/// <summary>
	/// 将 Dictionary 结果转换为强类型 SkillExecutionResult
	/// </summary>
	internal static SkillExecutionResult ConvertToTypedResult(
		Godot.Collections.Dictionary dictResult,
		Unit attacker,
		IEnumerable<Unit> allUnits)
	{
		bool success = dictResult.ContainsKey("success") && dictResult["success"].AsBool();
		if (!success)
		{
			string reason = dictResult.ContainsKey("reason") ? dictResult["reason"].AsString() : "未知原因";
			return SkillExecutionResult.Fail(reason);
		}

		var subResults = new List<SkillSubResult>();

		// 处理 results 数组
		if (dictResult.ContainsKey("results"))
		{
			var resultsArray = (Godot.Collections.Array)dictResult["results"];
			foreach (var item in resultsArray)
			{
				if (item.VariantType == Variant.Type.Dictionary)
				{
					var resultDict = (Godot.Collections.Dictionary)item;
					var subResult = ConvertSingleResult(resultDict, allUnits);
					if (subResult != null)
						subResults.Add(subResult);
				}
			}
		}

		// 处理 status_effects 数组
		if (dictResult.ContainsKey("status_effects"))
		{
			var effectsArray = (Godot.Collections.Array)dictResult["status_effects"];
			foreach (var item in effectsArray)
			{
				if (item.VariantType == Variant.Type.Dictionary)
				{
					var effectDict = (Godot.Collections.Dictionary)item;
					var effectResult = ConvertStatusEffect(effectDict, allUnits);
					if (effectResult != null)
						subResults.Add(effectResult);
				}
			}
		}

		return SkillExecutionResult.Ok(subResults.ToArray());
	}

	private static SkillSubResult? ConvertSingleResult(
		Godot.Collections.Dictionary resultDict,
		IEnumerable<Unit> allUnits)
	{
		if (!resultDict.ContainsKey("type")) return null;

		string type = resultDict["type"].AsString();
		switch (type)
		{
			case "damage":
				return ConvertDamageResult(resultDict, allUnits);
			case "heal":
				return ConvertHealResult(resultDict, allUnits);
			case "teleport":
				return ConvertTeleportResult(resultDict, allUnits);
			case "battle_anchor":
				return ConvertBattleAnchorResult(resultDict);
			default:
				return new ResultText($"未知结果类型: {type}");
		}
	}

	private static BattleAnchorEvent? ConvertBattleAnchorResult(Godot.Collections.Dictionary resultDict)
	{
		if (!resultDict.ContainsKey("anchor_id") || !resultDict.ContainsKey("source"))
			return null;

		string anchorId = resultDict["anchor_id"].AsString();
		string source = resultDict["source"].AsString();
		int q = resultDict.ContainsKey("q") ? resultDict["q"].AsInt32() : 0;
		int r = resultDict.ContainsKey("r") ? resultDict["r"].AsInt32() : 0;
		int duration = resultDict.ContainsKey("duration") ? resultDict["duration"].AsInt32() : -1;
		bool destructible = resultDict.ContainsKey("destructible") && resultDict["destructible"].AsBool();
		int hp = resultDict.ContainsKey("hp") ? resultDict["hp"].AsInt32() : 1;
		return new BattleAnchorEvent(anchorId, source, new Vector2I(q, r), duration, destructible, hp);
	}

	private static DamageEvent? ConvertDamageResult(
		Godot.Collections.Dictionary resultDict,
		IEnumerable<Unit> allUnits)
	{
		if (!resultDict.ContainsKey("target_id") || !resultDict.ContainsKey("damage"))
			return null;

		string targetId = resultDict["target_id"].AsString();
		int damage = resultDict["damage"].AsInt32();
		bool isCritical = resultDict.ContainsKey("is_critical") && resultDict["is_critical"].AsBool();
		bool wasKillingBlow = resultDict.ContainsKey("killing_blow") && resultDict["killing_blow"].AsBool();

		var targetUnit = allUnits.FirstOrDefault(u => u.GetInstanceId().ToString() == targetId || u.Data?.CharacterId.ToString() == targetId);
		if (targetUnit?.Data == null) return null;

		return new DamageEvent(targetUnit.Model, damage, isCritical, wasKillingBlow);
	}

	private static HealEvent? ConvertHealResult(
		Godot.Collections.Dictionary resultDict,
		IEnumerable<Unit> allUnits)
	{
		if (!resultDict.ContainsKey("target_id") || !resultDict.ContainsKey("amount"))
			return null;

		string targetId = resultDict["target_id"].AsString();
		int amount = resultDict["amount"].AsInt32();

		var targetUnit = allUnits.FirstOrDefault(u => u.GetInstanceId().ToString() == targetId || u.Data?.CharacterId.ToString() == targetId);
		if (targetUnit?.Data == null) return null;

		return new HealEvent(targetUnit.Model, amount);
	}

	private static TeleportEvent? ConvertTeleportResult(
		Godot.Collections.Dictionary resultDict,
		IEnumerable<Unit> allUnits)
	{
		if (!resultDict.ContainsKey("unit_id") || !resultDict.ContainsKey("destination"))
			return null;

		string unitId = resultDict["unit_id"].AsString();
		var destDict = (Godot.Collections.Dictionary)resultDict["destination"];
		int destX = destDict.ContainsKey("x") ? destDict["x"].AsInt32() : 0;
		int destY = destDict.ContainsKey("y") ? destDict["y"].AsInt32() : 0;

		var unit = allUnits.FirstOrDefault(u => u.GetInstanceId().ToString() == unitId || u.Data?.CharacterId.ToString() == unitId);
		if (unit?.Data == null) return null;

		Vector2I? previousPos = null;
		if (resultDict.ContainsKey("previous_position"))
		{
			var prevDict = (Godot.Collections.Dictionary)resultDict["previous_position"];
			int prevX = prevDict.ContainsKey("x") ? prevDict["x"].AsInt32() : 0;
			int prevY = prevDict.ContainsKey("y") ? prevDict["y"].AsInt32() : 0;
			previousPos = new Vector2I(prevX, prevY);
		}

		return new TeleportEvent(unit.Model, new Vector2I(destX, destY), previousPos);
	}

	private static StatusEffectApplication? ConvertStatusEffect(
		Godot.Collections.Dictionary effectDict,
		IEnumerable<Unit> allUnits)
	{
		if (!effectDict.ContainsKey("effect_id") || !effectDict.ContainsKey("target_id"))
			return null;

		string effectId = effectDict["effect_id"].AsString();
		string targetId = effectDict["target_id"].AsString();
		int duration = effectDict.ContainsKey("duration") ? effectDict["duration"].AsInt32() : -1;

		var targetUnit = allUnits.FirstOrDefault(u => u.GetInstanceId().ToString() == targetId || u.Data?.CharacterId.ToString() == targetId);
		if (targetUnit?.Data == null) return null;

		return new StatusEffectApplication(effectId, targetUnit.Model, duration);
	}

    // ============================================================================
    // 向后兼容
    // ============================================================================

    public static bool HasQuickCast(Unit unit) => PassiveSkillResolver.HasQuickCast(unit);

	private static void ExecuteIntrinsicSkill(string name, in SkillHandlerContext ctx)
	{
		var attacker = ctx.Attacker;
		var targetCell = ctx.Grid?.GetCell(ctx.TargetCell.X, ctx.TargetCell.Y);
		var target = targetCell?.Occupant;

		// 如果是单体对敌技能且目标有效
		if (target != null && GodotObject.IsInstanceValid(target) && target.CurrentHp > 0)
		{
			var allies = ctx.Allies.ToArray();
			var targetAllies = ctx.Enemies.ToArray();
			float damageMultiplier = 1.0f;
			int accuracyBonus = 0;
			bool isCharge = false;

			if (name == "撕咬")
			{
				damageMultiplier = 1.2f;
			}
			else if (name == "扑击")
			{
				damageMultiplier = 1.0f;
				isCharge = true;
			}
			else if (name == "碾压" || name == "恶魔猛击" || name == "巨拳猛击")
			{
				damageMultiplier = 1.3f;
			}

			var resultDict = CombatResolver.ResolveAttack(
				attacker, target, ctx.Grid, isCharge, false, accuracyBonus, damageMultiplier,
				attackerAllies: allies,
				defenderAllies: targetAllies);
			
			if (resultDict.ContainsKey("hit") && resultDict["hit"].AsBool())
			{
				int dmg = resultDict["damage"].AsInt32();
				bool isCrit = resultDict.ContainsKey("critical") && resultDict["critical"].AsBool();
				bool isKilling = target.CurrentHp - dmg <= 0;

				ctx.Builder.AddDamage(target, dmg, isCrit, isKilling);

				// 附加状态效果
				if (name == "扑击" || name == "碾压" || name == "践踏")
				{
					ctx.Builder.AddStatusEffect("prone", target, 1);
				}
				else if (name == "麻痹毒刺")
				{
					ctx.Builder.AddStatusEffect("stunned", target, 1);
				}
			}
			else
			{
				string reason = resultDict.ContainsKey("reason") ? resultDict["reason"].AsString() : "未命中";
				ctx.Builder.Fail(reason);
			}
		}
		else
		{
			// AOE 技能或者范围效果（比如龙息，哀嚎）
			if (name == "冰霜龙息" || name == "毒雾吐息" || name == "酸液喷吐")
			{
				var enemiesInRange = new List<Unit>();
				foreach (var enemy in ctx.Enemies)
				{
					if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
					int dist = HexUtils.Distance(attacker.GridPos.X, attacker.GridPos.Y, enemy.GridPos.X, enemy.GridPos.Y);
					if (dist <= 3) // 龙息通常为 3 格范围
					{
						enemiesInRange.Add(enemy);
					}
				}

				if (enemiesInRange.Count > 0)
				{
					foreach (var enemy in enemiesInRange)
					{
						int diceCount = Math.Max(1, attacker.Data?.Level / 4 ?? 1);
						int diceSides = 8;
						int damage = 0;
						for (int i = 0; i < diceCount; i++) damage += (int)(GD.Randi() % (uint)diceSides) + 1;
						
						if (attacker.Data != null)
							damage += Math.Max(attacker.Data.Str, attacker.Data.Intel) / 2;

						ctx.Builder.AddDamage(enemy, damage, false, enemy.CurrentHp - damage <= 0);
						
						if (name == "冰霜龙息")
							ctx.Builder.AddStatusEffect("slowed", enemy, 2);
						else if (name == "毒雾吐息" || name == "酸液喷吐")
							ctx.Builder.AddStatusEffect("poisoned", enemy, 3);
					}
				}
				else
				{
					ctx.Builder.Fail("范围内无有效目标");
				}
			}
			else if (name == "恐惧之触" || name == "恐惧凝视" || name == "恐惧威慑" || name == "嗥叫" || name == "亡灵哀嚎")
			{
				int count = 0;
				foreach (var enemy in ctx.Enemies)
				{
					if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
					int dist = HexUtils.Distance(attacker.GridPos.X, attacker.GridPos.Y, enemy.GridPos.X, enemy.GridPos.Y);
					if (dist <= 3)
					{
						ctx.Builder.AddStatusEffect("fear", enemy, 2);
						count++;
					}
				}
				if (count == 0) ctx.Builder.Fail("范围内无有效目标");
			}
			else
			{
				ctx.Builder.Fail("未选中有效目标");
			}
		}
	}
}
