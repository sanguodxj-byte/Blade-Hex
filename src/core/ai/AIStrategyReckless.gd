# AIStrategyReckless.gd
# 鲁莽策略 —— 总是冲向最近敌人，优先冲锋，从低HP不撤退
# 适用于：狂战士、兽类、野蛮人
extends AIStrategyBase
class_name AIStrategyReckless

func _decide_strategy_action(actor: Unit, scored_targets: Array, _player_units: Array, _enemy_units: Array, hex_grid: HexGrid):
	# 鲁莽策略无视评分，总是选最近的敌人
	var closest_target: Unit = null
	var closest_dist := 999
	
	for entry in scored_targets:
		var target: Unit = entry["unit"]
		var dist := HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist < closest_dist:
			closest_dist = dist
			closest_target = target
	
	if not closest_target:
		return _decide_idle_action(actor, hex_grid)
	
	# 如果手持远程但敌人在近战范围，切近战
	var weapon: WeaponData = actor.get_main_hand()
	if weapon and weapon.is_ranged and closest_dist <= 1:
		if actor.data.secondary_main_hand and not actor.data.secondary_main_hand.is_ranged:
			actor.switch_weapon_set()
	
	# 创建攻击行动
	var action = _create_attack_action(actor, closest_target, hex_grid)
	
	# 鲁莽加成：尽量拉长移动距离以获得冲锋
	if action.action_type == AIAction.Type.MOVE_THEN_ATTACK and action.move_path.size() > 0:
		var move_range := actor.get_move_range()
		var path = hex_grid.find_path(actor.grid_pos, closest_target.grid_pos)
		if path.size() >= 3:
			# 取路径上移动范围内的最远点
			var charge_idx := mini(move_range - 1, path.size() - 1)
			# 但要确保这个位置能打到目标（相邻）
			var charge_pos: Vector2i = path[charge_idx]
			var charge_cell = hex_grid.get_cell(charge_pos.x, charge_pos.y)
			var charge_dist := HexUtils.distance(charge_pos.x, charge_pos.y, closest_target.grid_pos.x, closest_target.grid_pos.y)
			var cur_weapon: WeaponData = actor.get_main_hand()
			var cur_range := cur_weapon.range_cells if cur_weapon else 1
			
			if charge_cell and charge_cell.occupant == null and charge_dist <= cur_range:
				action.target_position = charge_pos
				action.attack_position = charge_pos
				action.move_path = hex_grid.find_path(actor.grid_pos, charge_pos)
				action.is_charge = true
	
	action.description = "%s 狂暴地冲向 %s！" % [actor.data.unit_name, closest_target.data.unit_name]
	return action

## 覆盖：鲁莽型不会因低HP撤退（只有溃逃才退）
func _check_retreat(_actor: Unit, _player_units: Array, _hex_grid: HexGrid):
	# 鲁莽永远不会撤退，哪怕只剩1滴血
	# 只有士气溃逃(_check_morale_override)才会让他们跑
	return null
