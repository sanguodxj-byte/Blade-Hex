# AIStrategyTactical.gd
# 战术策略 —— 集火、包夹机动、与友方协同、善用地形
# 适用于：老兵、指挥官、有组织的敌军
extends AIStrategyBase
class_name AIStrategyTactical

func _decide_strategy_action(actor: Unit, scored_targets: Array, player_units: Array, enemy_units: Array, hex_grid: HexGrid) -> AIAction:
	# 战术策略：选集火目标（友方已经在攻击的目标优先）
	var focus_target = _select_focus_target(scored_targets, enemy_units, hex_grid)
	
	if not focus_target:
		# 没有集火目标，选评分最高的
		if scored_targets.is_empty():
			return _decide_idle_action(actor, hex_grid)
		focus_target = scored_targets[0]["unit"]
	
	# 尝试找包夹位置
	if difficulty_config.uses_flanking:
		var flank_positions = AISpatialAnalyzer.find_flanking_positions(hex_grid, focus_target, actor, actor.get_move_range())
		
		if flank_positions.size() > 0:
			# 选最佳包夹位置（背后优先）
			for entry in flank_positions:
				var flank_pos: Vector2i = entry["position"]
				var flank_facing: int = entry["facing"]
				if _is_position_reachable(actor, flank_pos, hex_grid):
					return _build_flank_attack_action(actor, focus_target, flank_pos, flank_facing, hex_grid)
	
	# 没有包夹机会，标准攻击（但选择最优位置）
	var action = _create_attack_action(actor, focus_target, hex_grid)
	
	# 战术加成：考虑控制区，避免触发借机攻击
	if difficulty_config.uses_zone_of_control and action.move_path.size() > 0:
		var aoos = AISpatialAnalyzer.count_opportunity_attacks(hex_grid, action.move_path, player_units)
		if aoos > 0:
			# 有借机攻击风险，尝试找更安全的路径
			var safer_action = _find_safer_approach(actor, focus_target, player_units, hex_grid)
			if safer_action:
				return safer_action
			# 没有更安全的路径，仍然执行但标记风险
			action.description = "%s 冒险攻击 %s" % [actor.data.unit_name, focus_target.data.unit_name]
			return action
	
	action.description = "%s 协同攻击 %s" % [actor.data.unit_name, focus_target.data.unit_name]
	return action

## 选择集火目标：友方已经在攻击的目标优先
func _select_focus_target(scored_targets: Array, enemy_units: Array, hex_grid: HexGrid) -> Unit:
	if scored_targets.is_empty():
		return null
	
	# 对每个评分目标计算"友方关注程度"
	var best_target: Unit = null
	var best_score := -999.0
	
	for entry in scored_targets:
		var target: Unit = entry["unit"]
		var base_score: float = entry["score"]
		
		# 计算有多少友方已经在这个目标附近
		var adjacent_allies = AISpatialAnalyzer.count_adjacent_allies(hex_grid, target.grid_pos, enemy_units)
		var focus_bonus: float = adjacent_allies * 3.0
		
		var total_score = base_score + focus_bonus
		if total_score > best_score:
			best_score = total_score
			best_target = target
	
	return best_target

## 检查位置是否可达（空地且在移动范围内）
func _is_position_reachable(actor: Unit, pos: Vector2i, hex_grid: HexGrid) -> bool:
	var cell = hex_grid.get_cell(pos.x, pos.y)
	if not cell:
		return false
	if cell.occupant != null and cell.occupant != actor:
		return false
	var dist := HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, pos.x, pos.y)
	return dist <= actor.get_move_range()

## 构建包夹攻击行动
func _build_flank_attack_action(actor: Unit, target: Unit, flank_pos: Vector2i, flank_facing: int, hex_grid: HexGrid) -> AIAction:
	var action := AIAction.new()
	action.actor = actor
	action.target_unit = target
	action.target_position = flank_pos
	action.attack_position = flank_pos
	action.move_path = hex_grid.find_path(actor.grid_pos, flank_pos)
	
	if flank_facing == 2:
		action.is_backstab = true
		action.description = "%s 绕后偷袭 %s！" % [actor.data.unit_name, target.data.unit_name]
	else:
		action.is_flanking = true
		action.description = "%s 侧翼包抄 %s！" % [actor.data.unit_name, target.data.unit_name]
	
	# 冲锋检查
	if action.move_path.size() >= 3 and difficulty_config.uses_charge:
		action.is_charge = AISpatialAnalyzer.can_charge(hex_grid, action.move_path, actor.grid_pos)
	
	return action

## 尝试找更安全的接近路径（避免借机攻击）
func _find_safer_approach(actor: Unit, target: Unit, player_units: Array, hex_grid: HexGrid) -> AIAction:
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var move_range := actor.get_move_range()
	var reachable = hex_grid.get_cells_in_range(actor.grid_pos.x, actor.grid_pos.y, move_range)
	
	var best_pos := Vector2i(-1, -1)
	var best_score := -999.0
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null and cell.occupant != actor: continue
		
		var dist_to_target = HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist_to_target > atk_range: continue
		
		var path = hex_grid.find_path(actor.grid_pos, pos)
		var aoos = AISpatialAnalyzer.count_opportunity_attacks(hex_grid, path, player_units)
		
		# 借机攻击越少越好，掩体越多越好
		var score: float = -aoos * 5.0
		score += cell.cover_type * 2.0
		
		if score > best_score:
			best_score = score
			best_pos = pos
	
	if best_pos == Vector2i(-1, -1):
		return null
	
	var action := AIAction.new()
	action.action_type = AIAction.Type.MOVE_THEN_ATTACK
	action.actor = actor
	action.target_unit = target
	action.target_position = best_pos
	action.attack_position = best_pos
	action.move_path = hex_grid.find_path(actor.grid_pos, best_pos)
	action.description = "%s 安全接近 %s" % [actor.data.unit_name, target.data.unit_name]
	return action
