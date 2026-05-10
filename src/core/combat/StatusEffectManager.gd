# StatusEffectManager.gd
# 状态效果管理器 — 负责状态效果的施加、每回合结算、交互和移除
# 对应策划案 03-战术战斗系统 → 七、战斗状态效果
extends Node
class_name StatusEffectManager

# ============================================================================
# 信号
# ============================================================================

signal effect_applied(unit: Unit, effect_id: String)
signal effect_removed(unit: Unit, effect_id: String)
signal effect_ticked(unit: Unit, effect_id: String, damage: int)

# ============================================================================
# 施加状态效果
# ============================================================================

## 给单位施加状态效果
func apply_effect(unit: Unit, effectid: String, duration: int = -1, sourceunit: Unit = null) -> bool:
	if effectid.is_empty():
		return false

	# 检查免疫
	if unit.data and effectid in unit.data.immunities:
		return false

	# 检查是否已存在同名效果
	for existing in unit.data.active_status_effects:
		if existing["id"] == effectid:
			# 刷新持续时间（取较大值）
			if duration > 0:
				existing["duration"] = maxi(existing["duration"], duration)
			return false

	# 检查交互（如燃烧+冰冻互相解除）
	_check_interactions(unit, effectid)

	# 获取默认数据
	var effect_enum_val = _effect_name_to_enum(effectid)
	if effect_enum_val < 0:
		return false

	var effect_data = StatusEffectData.create_effect(effect_enum_val)

	# 确定持续时间
	var actual_duration = duration if duration > 0 else effect_data.default_duration

	# 添加效果
	var effect_instance = {
		"id": effectid,
		"name": effect_data.effect_name,
		"duration": actual_duration,
		"is_negative": effect_data.is_negative,
		"stat_modifiers": effect_data.stat_modifiers.duplicate(),
		"tick_damage_count": effect_data.tick_damage_dice_count,
		"tick_damage_sides": effect_data.tick_damage_dice_sides,
		"tick_damage_type": effect_data.tick_damage_type,
		"save_to_remove": effect_data.save_to_remove,
		"save_dc": effect_data.save_dc,
		"removes_effects": effect_data.removes_effects.duplicate(),
		"breaks_on_attack": effect_data.breaks_on_attack,
		"can_spread": effect_data.can_spread,
		"source": sourceunit,
	}

	unit.data.active_status_effects.append(effect_instance)
	effect_applied.emit(unit, effectid)
	return true


# ============================================================================
# 移除状态效果
# ============================================================================

## 移除指定状态效果
func remove_effect(unit: Unit, effectid: String):
	var to_remove = -1
	for i in range(unit.data.active_status_effects.size()):
		if unit.data.active_status_effects[i]["id"] == effectid:
			to_remove = i
			break
	if to_remove >= 0:
		unit.data.active_status_effects.remove_at(to_remove)
		effect_removed.emit(unit, effectid)

## 移除所有负面效果
func remove_all_negative(unit: Unit):
	var to_remove: Array[String] = []
	for effect in unit.data.active_status_effects:
		if effect["is_negative"]:
			to_remove.append(effect["id"])
	for eid in to_remove:
		remove_effect(unit, eid)

## 攻击后检查（隐身等效果在攻击后解除）
func on_unit_attacked(unit: Unit):
	var to_remove: Array[String] = []
	for effect in unit.data.active_status_effects:
		if effect["breaks_on_attack"]:
			to_remove.append(effect["id"])
	for eid in to_remove:
		remove_effect(unit, eid)


# ============================================================================
# 每回合结算
# ============================================================================

## 回合开始时结算所有效果（毒/燃烧/流血/再生等）
func tick_effects(unit: Unit):
	var effects_copy = unit.data.active_status_effects.duplicate(true)
	for effect in effects_copy:
		var eid: String = effect["id"]

		# 持续伤害（毒1d4, 燃烧1d6, 流血1d4）
		if effect["tick_damage_count"] > 0 and effect["tick_damage_sides"] != 0:
			if effect["tick_damage_sides"] < 0:
				# 负值 = 治疗（再生）
				var heal = RPGRuleEngine.roll_dice(effect["tick_damage_count"], abs(effect["tick_damage_sides"]))
				unit.current_hp = mini(unit.current_hp + heal, unit.get_max_hp())
				effect_ticked.emit(unit, eid, -heal)
			else:
				var dmg = RPGRuleEngine.roll_dice(effect["tick_damage_count"], effect["tick_damage_sides"])
				unit.take_damage(dmg)
				effect_ticked.emit(unit, eid, dmg)

				# 燃烧蔓延
				if effect["can_spread"] and is_instance_valid(unit.get_parent()):
					# TODO: 蔓延逻辑需要访问HexGrid，此处预留
					pass

		# 减少持续时间
		effect["duration"] -= 1
		if effect["duration"] <= 0:
			remove_effect(unit, eid)
			continue

		# 检查豁免解除
		if effect["save_to_remove"] != "":
			var save_result = _attempt_save(unit, effect)
			if save_result:
				remove_effect(unit, eid)


# ============================================================================
# 效果查询
# ============================================================================

## 单位是否有指定效果
func has_effect(unit: Unit, effectid: String) -> bool:
	for effect in unit.data.active_status_effects:
		if effect["id"] == effectid:
			return true
	return false

## 获取所有活跃效果
func get_active_effects(unit: Unit) -> Array[Dictionary]:
	return unit.data.active_status_effects

## 获取所有效果累加的属性修正
func get_effect_modifiers(unit: Unit) -> Dictionary:
	var mods: Dictionary = {}
	for effect in unit.data.active_status_effects:
		for key in effect["stat_modifiers"]:
			if mods.has(key):
				mods[key] += effect["stat_modifiers"][key]
			else:
				mods[key] = effect["stat_modifiers"][key]
	return mods

## 单位是否可以行动（冰冻、眩晕检查）
func can_act(unit: Unit) -> bool:
	for effect in unit.data.active_status_effects:
		if effect["stat_modifiers"].get("cannot_act", false):
			return false
		if effect["id"] == "freeze" or effect["id"] == "stun":
			return false
	return true

## 单位是否可以移动（缚足检查）
func can_move(unit: Unit) -> bool:
	for effect in unit.data.active_status_effects:
		if effect["stat_modifiers"].get("cannot_move", false):
			return false
		if effect["id"] == "root" or effect["id"] == "freeze":
			return false
	return true

## 单位是否可以施法（沉默检查）
func can_cast(unit: Unit) -> bool:
	for effect in unit.data.active_status_effects:
		if effect["stat_modifiers"].get("cannot_cast", false):
			return false
		if effect["id"] == "silence":
			return false
	return true

## 获取近战攻击是否有劣势
func has_melee_disadvantage(unit: Unit) -> bool:
	for effect in unit.data.active_status_effects:
		if effect["stat_modifiers"].get("melee_disadvantage", false):
			return true
	return false

## 获取远程射程覆盖（致盲时射程变1）
func get_ranged_range_override(unit: Unit) -> int:
	for effect in unit.data.active_status_effects:
		if effect["stat_modifiers"].has("ranged_range_override"):
			return effect["stat_modifiers"]["ranged_range_override"]
	return -1  # -1表示无覆盖


# ============================================================================
# 内部方法
# ============================================================================

## 检查效果交互
func _check_interactions(unit: Unit, new_effect_id: String):
	# 检查新效果是否解除已有效果
	var new_data = StatusEffectData.create_effect(_effect_name_to_enum(new_effect_id))
	if new_data == null:
		return

	for removes in new_data.removes_effects:
		if has_effect(unit, removes):
			remove_effect(unit, removes)

	# 检查交互表
	for existing in unit.data.active_status_effects.duplicate():
		var interaction = StatusEffectData.get_interaction(new_effect_id, existing["id"])
		match interaction["action"]:
			"cancel_both":
				remove_effect(unit, existing["id"])
				# 新效果也不施加（通过返回标记）
			"cancel_a":
				# 新效果被取消
				pass
			"cancel_b":
				remove_effect(unit, existing["id"])
			"extend_b":
				existing["duration"] += int(interaction["value"])

## 豁免解除尝试
func _attempt_save(unit: Unit, effect: Dictionary) -> bool:
	var save_type: String = effect["save_to_remove"]
	var dc: int = effect["save_dc"]

	var ability_score = 10
	match save_type:
		"fortitude": ability_score = unit.data.con
		"reflex": ability_score = unit.data.dex
		"will": ability_score = unit.data.wis

	var prof = RPGRuleEngine.get_proficiency_bonus(unit.data.level)
	var result = RPGRuleEngine.make_save(ability_score, prof, false, dc)
	return result["success"]

## 效果名转枚举值
func _effect_name_to_enum(name: String) -> int:
	var mapping = {
		"poison": StatusEffectData.EffectId.POISON,
		"burning": StatusEffectData.EffectId.BURNING,
		"freeze": StatusEffectData.EffectId.FREEZE,
		"fear": StatusEffectData.EffectId.FEAR,
		"silence": StatusEffectData.EffectId.SILENCE,
		"blind": StatusEffectData.EffectId.BLIND,
		"stun": StatusEffectData.EffectId.STUN,
		"bleed": StatusEffectData.EffectId.BLEED,
		"slow": StatusEffectData.EffectId.SLOW,
		"root": StatusEffectData.EffectId.ROOT,
		"charmed": StatusEffectData.EffectId.CHARMED,
		"confused": StatusEffectData.EffectId.CONFUSED,
		"wet": StatusEffectData.EffectId.WET,
		"bless": StatusEffectData.EffectId.BLESS,
		"shield": StatusEffectData.EffectId.SHIELD,
		"haste": StatusEffectData.EffectId.HASTE,
		"regen": StatusEffectData.EffectId.REGEN,
		"invisibility": StatusEffectData.EffectId.INVISIBILITY,
		"phantom": StatusEffectData.EffectId.PHANTOM,
		"temp_hp": StatusEffectData.EffectId.TEMP_HP,
	}
	return mapping.get(name, -1)
