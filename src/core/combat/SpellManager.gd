# SpellManager.gd
# 法术管理器 — 法术施放、魔力管理、冷却、法术解析
# 对应策划案 07-法术系统.md
extends Node
class_name SpellManager

# ============================================================================
# 信号
# ============================================================================

signal spell_cast(caster: Unit, spell: SpellData, targets: Array[Vector2i])
signal spell_hit(target: Unit, damage: int, damage_type: String)
signal spell_missed(target: Unit)
signal spell_healed(target: Unit, amount: int)

# ============================================================================
# 法术可用性检查
# ============================================================================

## 检查施法者是否可以施放指定法术
## 返回: {"can_cast": bool, "reason": String}
func can_cast_spell(caster: Unit, spell: SpellData) -> Dictionary:
	if not caster.data:
		return {"can_cast": false, "reason": "无单位数据"}

	# 1. 需要法术触媒（法杖或魔导书）
	var weapon = caster.get_main_hand()
	if not weapon or not weapon.is_catalyst:
		# 检查副手
		var off = caster.get_off_hand()
		if not (off is WeaponData and off.is_catalyst):
			return {"can_cast": false, "reason": "需要装备法杖或魔导书"}

	# 2. 法术不在冷却中
	if caster.data.spell_cooldowns.get(spell.spell_id, 0) > 0:
		return {"can_cast": false, "reason": "法术冷却中（%d回合）" % caster.data.spell_cooldowns[spell.spell_id]}

	# 3. 魔力足够
	if caster.data.current_mana < spell.mana_cost:
		return {"can_cast": false, "reason": "魔力不足（需要%d，当前%d）" % [spell.mana_cost, caster.data.current_mana]}

	# 4. 不在沉默状态
	# （需要StatusEffectManager引用，此处预留接口）
	# if status_manager and not status_manager.can_cast(caster):
	#     return {"can_cast": false, "reason": "被沉默"}

	return {"can_cast": true, "reason": ""}


# ============================================================================
# 法术施放
# ============================================================================

## 施放法术
## 返回: {"success": bool, "results": Array[Dictionary]}
func cast_spell(caster: Unit, spell: SpellData, target_cell: Vector2i, grid: HexGrid) -> Dictionary:
	# 检查施放条件
	var check = can_cast_spell(caster, spell)
	if not check["can_cast"]:
		return {"success": false, "results": [], "reason": check["reason"]}

	# 消耗魔力
	caster.data.current_mana -= spell.mana_cost

	# 设置冷却
	caster.data.spell_cooldowns[spell.spell_id] = spell.cooldown_turns

	# 计算目标格子
	var target_cells = SpellShapeResolver.get_cells_in_shape(
		spell.shape, target_cell, caster.grid_pos, spell.shape_size, grid
	)

	# 根据解析方式处理效果
	var results: Array[Dictionary] = []

	match spell.resolution_type:
		SpellData.ResolutionType.ATTACK_ROLL:
			results = _resolve_attack_spell(caster, spell, target_cells, grid)
		SpellData.ResolutionType.SAVE:
			results = _resolve_save_spell(caster, spell, target_cells, grid)
		SpellData.ResolutionType.AUTO_HIT:
			results = _resolve_auto_spell(caster, spell, target_cells, grid)

	spell_cast.emit(caster, spell, target_cells)
	return {"success": true, "results": results, "target_cells": target_cells}


# ============================================================================
# 法术解析
# ============================================================================

## 法术攻击检定（vs AC）
func _resolve_attack_spell(caster: Unit, spell: SpellData, target_cells: Array[Vector2i], grid: HexGrid) -> Array[Dictionary]:
	var results: Array[Dictionary] = []

	for cell_pos in target_cells:
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if not cell or not cell.occupant:
			continue

		var target: Unit = cell.occupant
		var _dc = get_spell_dc(caster)

		# 攻击检定
		var roll = RPGRuleEngine.roll_d20()
		var casting_mod = _get_casting_modifier(caster)
		var prof = RPGRuleEngine.get_proficiency_bonus(caster.data.level)
		var total = roll + casting_mod + prof

		var target_ac = target.get_ac()
		var is_hit = total >= target_ac or roll == 20
		var is_crit = roll == 20

		if is_hit:
			var damage = RPGRuleEngine.roll_dice(spell.damage_dice_count, spell.damage_dice_sides)
			damage += casting_mod
			if is_crit:
				damage *= 2

			target.take_damage(damage)
			spell_hit.emit(target, damage, spell.damage_type)

			# 施加状态效果
			if spell.applied_status != "":
				# StatusEffectManager引用预留
				pass

			results.append({"target": target, "hit": true, "critical": is_crit, "damage": damage})
		else:
			spell_missed.emit(target)
			results.append({"target": target, "hit": false, "critical": false, "damage": 0})

	return results

## 豁免检定（vs 法术DC）
func _resolve_save_spell(caster: Unit, spell: SpellData, target_cells: Array[Vector2i], grid: HexGrid) -> Array[Dictionary]:
	var results: Array[Dictionary] = []
	var dc = get_spell_dc(caster)

	for cell_pos in target_cells:
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if not cell or not cell.occupant:
			continue

		var target: Unit = cell.occupant
		var ability_score = _get_save_ability_score(target, spell.save_type)
		var prof = RPGRuleEngine.get_proficiency_bonus(target.data.level)

		var save_result = RPGRuleEngine.make_save(ability_score, prof, false, dc)
		var damage = RPGRuleEngine.roll_dice(spell.damage_dice_count, spell.damage_dice_sides)

		if save_result["success"]:
			# 豁免成功 — 半伤（如有伤害）
			if spell.damage_dice_count > 0:
				damage = max(1, damage / 2)
				target.take_damage(damage)
				spell_hit.emit(target, damage, spell.damage_type)
			results.append({"target": target, "hit": true, "saved": true, "damage": damage})
		else:
			# 豁免失败 — 全伤
			if spell.damage_dice_count > 0:
				target.take_damage(damage)
				spell_hit.emit(target, damage, spell.damage_type)

			# 施加状态效果
			if spell.applied_status != "":
				# StatusEffectManager引用预留
				pass

			results.append({"target": target, "hit": true, "saved": false, "damage": damage})

	return results

## 自动命中
func _resolve_auto_spell(caster: Unit, spell: SpellData, target_cells: Array[Vector2i], grid: HexGrid) -> Array[Dictionary]:
	var results: Array[Dictionary] = []

	for cell_pos in target_cells:
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if not cell:
			continue

		# 治疗法术
		if spell.heal_dice_count > 0:
			if cell.occupant:
				var target: Unit = cell.occupant
				var heal = RPGRuleEngine.roll_dice(spell.heal_dice_count, spell.heal_dice_sides)
				heal += _get_casting_modifier(caster) + spell.heal_bonus
				target.current_hp = mini(target.current_hp + heal, target.get_max_hp())
				spell_healed.emit(target, heal)
				results.append({"target": target, "healed": true, "amount": heal})
			continue

		# 伤害法术
		if spell.damage_dice_count > 0:
			if cell.occupant:
				var target: Unit = cell.occupant
				var damage = RPGRuleEngine.roll_dice(spell.damage_dice_count, spell.damage_dice_sides)
				damage += _get_casting_modifier(caster)
				target.take_damage(damage)
				spell_hit.emit(target, damage, spell.damage_type)
				results.append({"target": target, "hit": true, "damage": damage})
			continue

		# 纯效果法术（祝福、护盾等）
		if cell.occupant:
			if spell.applied_status != "":
				# StatusEffectManager引用预留
				pass
			results.append({"target": cell.occupant, "effect": true})

	return results


# ============================================================================
# 法术属性计算
# ============================================================================

## 计算法术DC = 8 + 施法属性修正 + 专精加值
func get_spell_dc(caster: Unit) -> int:
	var ability_score = _get_casting_ability_score(caster)
	var prof = RPGRuleEngine.get_proficiency_bonus(caster.data.level)
	return RPGRuleEngine.calculate_spell_dc(ability_score, prof)

## 获取施法属性修正值
func _get_casting_modifier(caster: Unit) -> int:
	return RPGRuleEngine.get_stat_modifier(_get_casting_ability_score(caster))

## 获取施法属性原始分值
func _get_casting_ability_score(caster: Unit) -> int:
	var ability = caster.data.casting_ability if caster.data else "intel"
	match ability:
		"intel": return caster.data.intel
		"cha": return caster.data.cha
		"wis": return caster.data.wis
		_: return caster.data.intel

## 获取目标用于豁免的属性分值
func _get_save_ability_score(target: Unit, save_type: SpellData.SaveType) -> int:
	match save_type:
		SpellData.SaveType.STR_SAVE: return target.data.str
		SpellData.SaveType.DEX_SAVE: return target.data.dex
		SpellData.SaveType.CON_SAVE: return target.data.con
		SpellData.SaveType.INT_SAVE: return target.data.intel
		SpellData.SaveType.WIS_SAVE: return target.data.wis
		SpellData.SaveType.CHA_SAVE: return target.data.cha
		_: return 10


# ============================================================================
# 魔力管理
# ============================================================================

## 获取最大魔力值
func get_max_mana(caster: Unit) -> int:
	var base = 10
	var int_mod = RPGRuleEngine.get_stat_modifier(_get_casting_ability_score(caster))
	return base + int_mod * 2

## 回合开始魔力自然恢复
func regen_mana(caster: Unit):
	caster.data.current_mana = mini(caster.data.current_mana + 1, get_max_mana(caster))

## 法术冷却tick（回合结束时调用）
func tick_cooldowns(caster: Unit):
	var keys = caster.data.spell_cooldowns.keys()
	for key in keys:
		var val = caster.data.spell_cooldowns[key] - 1
		if val <= 0:
			caster.data.spell_cooldowns.erase(key)
		else:
			caster.data.spell_cooldowns[key] = val

## 获取施法者可用法术列表
func get_spells_for_caster(caster: Unit) -> Array[SpellData]:
	return caster.data.known_spells
