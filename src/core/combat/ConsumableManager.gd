# ConsumableManager.gd
# 消耗品管理器 — 处理药水使用、投掷物投掷、卷轴施放
# 对应策划案 06-装备与物品 → 物品与消耗品
class_name ConsumableManager

# ============================================================================
# 信号
# ============================================================================

signal consumable_used(user: Unit, item: ConsumableData, result: Dictionary)

# ============================================================================
# 使用消耗品
# ============================================================================

## 使用消耗品
## 返回: {"success": bool, "effect": String, "amount": int, "targets_affected": int}
static func use_consumable(user: Unit, item: ConsumableData, target_cell: Vector2i = Vector2i.ZERO, grid: HexGrid = null) -> Dictionary:
	var result = {"success": false, "effect": "", "amount": 0, "targets_affected": 0}

	match item.consumable_type:
		ConsumableData.ConsumableType.HEALING_POTION, \
		ConsumableData.ConsumableType.STRONG_HEALING:
			result = _use_healing_potion(user, item)

		ConsumableData.ConsumableType.ANTIDOTE:
			result = _use_antidote(user, item)

		ConsumableData.ConsumableType.FIRE_OIL:
			result = _use_thrown_item(user, item, target_cell, grid, "fire")

		ConsumableData.ConsumableType.HOLY_WATER:
			result = _use_thrown_item(user, item, target_cell, grid, "holy")

		ConsumableData.ConsumableType.SPELL_SCROLL:
			result = _use_scroll(user, item, target_cell, grid)

		ConsumableData.ConsumableType.WHETSTONE:
			result = _use_whetstone(user, item)

	# 从背包移除
	if result["success"]:
		_remove_from_inventory(user, item)

	return result


# ============================================================================
# 各类型处理
# ============================================================================

## 使用治疗药水
static func _use_healing_potion(user: Unit, item: ConsumableData) -> Dictionary:
	var heal = RPGRuleEngine.roll_dice(item.heal_dice_count, item.heal_dice_sides) + item.heal_bonus
	user.current_hp = mini(user.current_hp + heal, user.get_max_hp())
	return {"success": true, "effect": "heal", "amount": heal, "targets_affected": 1}

## 使用解毒剂
static func _use_antidote(_user: Unit, _item: ConsumableData):
	# 移除中毒状态（需要StatusEffectManager引用，此处预留）
	# status_manager.remove_effect(user, "poison")
	return {"success": true, "effect": "cure_poison", "amount": 0, "targets_affected": 1}

## 使用投掷物
static func _use_thrown_item(_user: Unit, item: ConsumableData, target_cell: Vector2i, grid: HexGrid, damage_type: String):
	if not grid:
		return {"success": false, "effect": "no_grid", "amount": 0, "targets_affected": 0}

	var affected = 0
	var total_damage = 0

	# 获取AOE范围内的格子
	var target_cells: Array[Vector2i] = [target_cell]
	if item.aoe_radius > 0:
		var aoe = grid.get_cells_in_range(target_cell.x, target_cell.y, item.aoe_radius)
		target_cells.append_array(aoe)

	for cell_pos in target_cells:
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if not cell or not cell.occupant:
			continue

		var target: Unit = cell.occupant

		# 圣水只对亡灵有效
		if damage_type == "holy":
			if target.data.enemy_type != UnitData.EnemyType.UNDEAD:
				continue

		var dmg = RPGRuleEngine.roll_dice(item.damage_dice_count, item.damage_dice_sides)
		target.take_damage(dmg)
		total_damage += dmg
		affected += 1

		# 火油瓶施加燃烧状态
		if damage_type == "fire" and item.applied_status != "":
			# status_manager.apply_effect(target, "burning", 3)
			pass

	return {"success": true, "effect": damage_type + "_damage", "amount": total_damage, "targets_affected": affected}

## 使用卷轴
static func _use_scroll(user: Unit, item: ConsumableData, _target_cell: Vector2i, _grid: HexGrid):
	if not item.linked_spell:
		return {"success": false, "effect": "no_spell", "amount": 0, "targets_affected": 0}

	# 卷轴施法不消耗使用者魔力
	var spell = item.linked_spell
	# 简化处理：直接计算伤害
	var damage = RPGRuleEngine.roll_dice(spell.damage_dice_count, spell.damage_dice_sides)
	if spell.heal_dice_count > 0:
		var heal = RPGRuleEngine.roll_dice(spell.heal_dice_count, spell.heal_dice_sides)
		user.current_hp = mini(user.current_hp + heal, user.get_max_hp())
		return {"success": true, "effect": "scroll_heal", "amount": heal, "targets_affected": 1}

	return {"success": true, "effect": "scroll_cast", "amount": damage, "targets_affected": 1}

## 使用磨刀石（战斗外）
static func _use_whetstone(_user: Unit, _item: ConsumableData) -> Dictionary:
	# 战斗外使用，近战伤害+1（持续本场战斗）
	# 运行时标记，此处预留
	return {"success": true, "effect": "melee_damage_up", "amount": 1, "targets_affected": 1}


# ============================================================================
# 辅助
# ============================================================================

## 从背包移除消耗品
static func _remove_from_inventory(user: Unit, item: ConsumableData):
	var idx = user.data.consumables.find(item)
	if idx >= 0:
		user.data.consumables.remove_at(idx)
