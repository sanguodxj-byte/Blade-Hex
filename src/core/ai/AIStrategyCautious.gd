# AIStrategyCautious.gd
# 谨慎策略 —— 优先掩体、保持距离、放风筝、受威胁时后撤
# 适用于：弓箭手、法师、远程单位
extends AIStrategyBase
class_name AIStrategyCautious

func _decide_strategy_action(actor: Unit, scored_targets: Array, player_units: Array, _enemy_units: Array, hex_grid: HexGrid) -> AIAction:
	# 选评分最高的目标
	var best_entry = scored_targets[0]
	var target: Unit = best_entry["unit"]
	
	var weapon: WeaponData = actor.get_main_hand()
	var is_ranged := weapon != null and weapon.is_ranged
	var atk_range := weapon.range_cells if weapon else 1
	var dist := HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, target.grid_pos.x, target.grid_pos.y)
	
	# 如果是近战武器但有远程备选，切换远程
	if not is_ranged:
		if actor.data.secondary_main_hand and actor.data.secondary_main_hand.is_ranged:
			actor.switch_weapon_set()
			weapon = actor.get_main_hand()
			is_ranged = true
			atk_range = weapon.range_cells
	
	if dist <= atk_range:
		# 在射程内 —— 检查是否应该换到更好的掩体位置再打
		if difficulty_config.uses_terrain:
			var better_pos = _find_better_cover_position(actor, target, hex_grid)
			if better_pos != actor.grid_pos:
				var path = hex_grid.find_path(actor.grid_pos, better_pos)
				if path.size() > 0:
					var move_action := AIAction.new()
					move_action.action_type = AIAction.Type.MOVE_THEN_ATTACK
					move_action.actor = actor
					move_action.target_unit = target
					move_action.target_position = better_pos
					move_action.attack_position = better_pos
					move_action.move_path = path
					move_action.description = "%s 移动到掩体后射击 %s" % [actor.data.unit_name, target.data.unit_name]
					return move_action
		
		# 当前位置就很好，直接攻击
		var action := AIAction.new()
		action.action_type = AIAction.Type.ATTACK
		action.actor = actor
		action.target_unit = target
		action.attack_position = actor.grid_pos
		action.description = "%s 远程攻击 %s" % [actor.data.unit_name, target.data.unit_name]
		return action
	else:
		# 不在射程内 —— 需要移动
		# 谨慎策略：先检查是否有近战敌人靠近，如果有则优先放风筝
		if is_ranged:
			var nearest_melee_dist = _get_nearest_melee_distance(actor, player_units, hex_grid)
			if nearest_melee_dist <= 2:
				# 有近战威胁！风筝：远离近战同时尽量保持对目标的射程
				var kite_pos = _find_kite_position(actor, target, player_units, hex_grid)
				if kite_pos != Vector2i(-1, -1):
					var kite_dist := HexUtils.distance(kite_pos.x, kite_pos.y, target.grid_pos.x, target.grid_pos.y)
					if kite_dist <= atk_range:
						# 风筝位置还能打到目标
						var kite_attack := AIAction.new()
						kite_attack.action_type = AIAction.Type.MOVE_THEN_ATTACK
						kite_attack.actor = actor
						kite_attack.target_unit = target
						kite_attack.target_position = kite_pos
						kite_attack.attack_position = kite_pos
						kite_attack.move_path = hex_grid.find_path(actor.grid_pos, kite_pos)
						kite_attack.description = "%s 风筝移动并射击 %s" % [actor.data.unit_name, target.data.unit_name]
						return kite_attack
					else:
						# 风筝位置打不到，纯移动
						var kite_move := AIAction.new()
						kite_move.action_type = AIAction.Type.MOVE_ONLY
						kite_move.actor = actor
						kite_move.target_position = kite_pos
						kite_move.move_path = hex_grid.find_path(actor.grid_pos, kite_pos)
						kite_move.description = "%s 风筝后撤" % actor.data.unit_name
						return kite_move
		
		# 无近战威胁，正常接近并攻击
		var move_action = _create_attack_action(actor, target, hex_grid)
		# 优先选掩体位置
		if difficulty_config.uses_terrain:
			var cover_positions = AISpatialAnalyzer.find_cover_positions(hex_grid, actor, target, actor.get_move_range())
			if cover_positions.size() > 0:
				var best_cover: Dictionary = cover_positions[0]
				move_action.target_position = best_cover["position"]
				move_action.attack_position = best_cover["position"]
				move_action.move_path = hex_grid.find_path(actor.grid_pos, best_cover["position"])
		move_action.description = "%s 移动并攻击 %s" % [actor.data.unit_name, target.data.unit_name]
		return move_action

## 寻找比当前位置更好的掩体射击位置
func _find_better_cover_position(actor: Unit, target: Unit, hex_grid: HexGrid) -> Vector2i:
	var current_defense := AISpatialAnalyzer.evaluate_position_defense(hex_grid, actor.grid_pos, [target])
	var move_range := actor.get_move_range()
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var reachable = hex_grid.get_cells_in_range(actor.grid_pos.x, actor.grid_pos.y, move_range)
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null and cell.occupant != actor: continue
		
		var dist = HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist > atk_range: continue
		
		var pos_defense := AISpatialAnalyzer.evaluate_position_defense(hex_grid, pos, [target])
		if pos_defense > current_defense + 1.0:
			return pos
	
	return actor.grid_pos

## 获取最近近战敌人的距离
func _get_nearest_melee_distance(actor: Unit, player_units: Array, _hex_grid: HexGrid):
	var nearest := 999
	for pu in player_units:
		if not is_instance_valid(pu) or not pu is Unit:
			continue
		if pu.current_hp <= 0:
			continue
		var weapon: WeaponData = pu.get_main_hand()
		# 近战单位或距离很近的单位都算威胁
		var dist := HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, pu.grid_pos.x, pu.grid_pos.y)
		if dist < nearest:
			if weapon == null or not weapon.is_ranged:
				nearest = dist
	return nearest

## 寻找风筝位置：远离近战威胁，同时尽量在射程内
func _find_kite_position(actor: Unit, target: Unit, threats: Array, hex_grid: HexGrid) -> Vector2i:
	var move_range := actor.get_move_range()
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var reachable = hex_grid.get_cells_in_range(actor.grid_pos.x, actor.grid_pos.y, move_range)
	
	var best_pos := Vector2i(-1, -1)
	var best_score := -999.0
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null: continue
		
		var score := 0.0
		
		# 远离近战威胁
		for threat in threats:
			if not is_instance_valid(threat) or not threat is Unit:
				continue
			var threat_weapon: WeaponData = threat.get_main_hand()
			if threat_weapon and threat_weapon.is_ranged:
				continue  # 远程不算近战威胁
			var dist := HexUtils.distance(pos.x, pos.y, threat.grid_pos.x, threat.grid_pos.y)
			score += dist * 1.5
		
		# 能打到目标加分
		var target_dist := HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if target_dist <= atk_range:
			score += 10.0
		
		# 掩体加分
		score += cell.cover_type * 2.0
		
		if score > best_score:
			best_score = score
			best_pos = pos
	
	return best_pos
