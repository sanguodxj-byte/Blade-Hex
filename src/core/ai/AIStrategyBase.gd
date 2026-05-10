# AIStrategyBase.gd
# AI策略基类 —— 定义决策模板方法，子类覆盖核心策略逻辑
# 模板流程：士气强制行为 → HP撤退检查 → 评估目标 → 策略决策
extends RefCounted
class_name AIStrategyBase

var difficulty_config: AIDifficultyConfig
var target_evaluator: AITargetEvaluator

func _init(config: AIDifficultyConfig):
	difficulty_config = config
	target_evaluator = AITargetEvaluator.new(config)

## 主入口：决定本回合行为，返回 AIAction
func decide_action(actor: Unit, player_units: Array, enemy_units: Array, hex_grid: HexGrid) -> AIAction:
	# 第1步：士气强制行为（溃逃等）
	var morale_level = actor.data.get_morale_level()
	var forced = _check_morale_override(actor, morale_level, player_units, hex_grid)
	if forced:
		return forced
	
	# 第2步：HP过低撤退检查
	var retreat = _check_retreat(actor, player_units, hex_grid)
	if retreat:
		return retreat
	
	# 第3步：评估目标
	var targets = target_evaluator.evaluate_targets(actor, player_units, hex_grid, enemy_units)
	if targets.is_empty():
		return _decide_idle_action(actor, hex_grid)
	
	# 第4步：策略特定决策（子类实现）
	return _decide_strategy_action(actor, targets, player_units, enemy_units, hex_grid)

## 子类必须覆盖：核心策略逻辑
func _decide_strategy_action(actor: Unit, _scored_targets: Array, _player_units: Array, _enemy_units: Array, _hex_grid: HexGrid):
	push_error("AIStrategyBase._decide_strategy_action 必须被子类覆盖")
	var action := AIAction.new()
	action.actor = actor
	return action

## 士气强制行为检查
func _check_morale_override(actor: Unit, morale_level: int, player_units: Array, hex_grid: HexGrid) -> AIAction:
	# 亡灵永远不会因士气溃逃
	if actor.data.enemy_type == UnitData.EnemyType.UNDEAD:
		return null
	
	if morale_level == UnitData.MoraleLevel.ROUTING:
		var action := AIAction.new()
		action.action_type = AIAction.Type.RETREAT
		action.actor = actor
		action.target_position = AISpatialAnalyzer.find_retreat_position(hex_grid, actor, player_units)
		action.description = "%s 士气崩溃，正在溃逃！" % actor.data.unit_name
		action.priority_score = 100.0
		return action
	
	return null

## HP过低撤退检查
func _check_retreat(actor: Unit, player_units: Array, hex_grid: HexGrid) -> AIAction:
	# 亡灵和鲁莽型不会因低HP撤退
	if actor.data.enemy_type == UnitData.EnemyType.UNDEAD:
		return null
	if actor.data.ai_strategy == UnitData.AIStrategy.RECKLESS:
		return null
	
	var hp_pct := float(actor.current_hp) / float(max(actor.get_max_hp(), 1))
	var threshold := 0.25 * difficulty_config.retreat_threshold_multiplier
	
	if hp_pct <= threshold:
		var morale_level = actor.data.get_morale_level()
		# 士气崩溃时必定撤退，否则50%概率
		if morale_level >= UnitData.MoraleLevel.BROKEN or randf() < 0.5:
			var action := AIAction.new()
			action.action_type = AIAction.Type.RETREAT
			action.actor = actor
			action.target_position = AISpatialAnalyzer.find_retreat_position(hex_grid, actor, player_units)
			action.description = "%s 受到重创，正在撤退！" % actor.data.unit_name
			action.priority_score = 90.0
			return action
	
	return null

## 默认待机行为
func _decide_idle_action(actor: Unit, _hex_grid: HexGrid):
	var action := AIAction.new()
	action.action_type = AIAction.Type.IDLE
	action.actor = actor
	action.description = "%s 待机。" % actor.data.unit_name
	return action

## 创建攻击行动（通用辅助方法）
func _create_attack_action(actor: Unit, target: Unit, hex_grid: HexGrid) -> AIAction:
	var action := AIAction.new()
	action.actor = actor
	action.target_unit = target
	
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var dist := HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, target.grid_pos.x, target.grid_pos.y)
	
	if dist <= atk_range:
		# 当前位置可攻击
		action.action_type = AIAction.Type.ATTACK
		action.attack_position = actor.grid_pos
	else:
		# 需要先移动
		action.action_type = AIAction.Type.MOVE_THEN_ATTACK
		var best_pos = _find_best_attack_position(actor, target, hex_grid)
		action.target_position = best_pos
		action.attack_position = best_pos
		var path = hex_grid.find_path(actor.grid_pos, best_pos)
		action.move_path = path
		# 冲锋判定
		if path.size() >= 3 and difficulty_config.uses_charge:
			action.is_charge = AISpatialAnalyzer.can_charge(hex_grid, path, actor.grid_pos)
	
	action.description = "%s 攻击 %s" % [actor.data.unit_name, target.data.unit_name]
	return action

## 寻找最佳攻击位置（综合评估掩体、高程、包夹）
func _find_best_attack_position(actor: Unit, target: Unit, hex_grid: HexGrid) -> Vector2i:
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var move_range := actor.get_move_range()
	var reachable = hex_grid.get_cells_in_range(actor.grid_pos.x, actor.grid_pos.y, move_range)
	
	var best_pos := actor.grid_pos
	var best_score := -999.0
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null and cell.occupant != actor: continue
		
		var dist_to_target = HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist_to_target > atk_range: continue
		
		var score := 0.0
		
		# 地形防御加成
		if cell.data and cell.data is BattleCellData:
			score += max(0, cell.data.ac_bonus) * 2.0
		score += cell.cover_type * 3.0
		
		# 高程优势
		var elev_adv = AISpatialAnalyzer.get_elevation_advantage(hex_grid, pos, target.grid_pos)
		if elev_adv > 0:
			score += 5.0
		elif elev_adv < 0:
			score -= 3.0
		
		# 包夹加分
		if difficulty_config.uses_flanking:
			var facing = AISpatialAnalyzer.get_attack_facing(pos, target.grid_pos, -1)
			if facing == 2:
				score += 8.0
			elif facing == 1:
				score += 4.0
		
		if score > best_score:
			best_score = score
			best_pos = pos
	
	return best_pos

## 寻找最近的可行攻击位置（不考虑评分，只要能打到）
func _find_nearest_attack_position(actor: Unit, target: Unit, hex_grid: HexGrid) -> Vector2i:
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var move_range := actor.get_move_range()
	var reachable = hex_grid.get_cells_in_range(actor.grid_pos.x, actor.grid_pos.y, move_range)
	
	var best_pos := actor.grid_pos
	var best_dist := 999
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null and cell.occupant != actor: continue
		
		var dist_to_target = HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist_to_target <= atk_range:
			# 选距离最近的
			var move_dist = HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, pos.x, pos.y)
			if move_dist < best_dist:
				best_dist = move_dist
				best_pos = pos
	
	return best_pos
