# AITargetEvaluator.gd
# 目标评分引擎 —— 为 AI 评估所有可见敌方单位作为攻击目标的优先级
extends RefCounted
class_name AITargetEvaluator

var difficulty_config: AIDifficultyConfig

func _init(config: AIDifficultyConfig):
	difficulty_config = config

## 评估所有可见玩家单位作为潜在目标
## 返回 Array of {unit: Unit, score: float}，按 score 降序
func evaluate_targets(actor: Unit, player_units: Array, hex_grid: HexGrid, _all_enemy_units: Array):
	var results: Array = []
	
	for pu in player_units:
		if not is_instance_valid(pu) or not pu is Unit:
			continue
		if pu.current_hp <= 0:
			continue
		
		var threat := _calculate_threat_score(pu)
		var vuln := _calculate_vulnerability_score(pu, actor, hex_grid)
		var strategic := _calculate_strategic_value(pu, actor)
		var reach_bonus := 0.0
		
		# 能直接攻击到的目标加分
		if can_attack_from_position(actor, pu, hex_grid, actor.grid_pos):
			reach_bonus = 5.0
		elif can_reach_target(actor, pu, hex_grid):
			reach_bonus = 2.0
		
		# 综合评分（难度影响权重分配精度）
		var accuracy = difficulty_config.target_selection_accuracy
		var noise = (1.0 - accuracy) * randf_range(-3.0, 3.0)
		
		var score = threat * 0.3 + vuln * 0.3 + strategic * 0.2 + reach_bonus * 0.2 + noise
		results.append({"unit": pu, "score": score})
	
	# 按评分降序
	results.sort_custom(func(a, b): return a["score"] > b["score"])
	return results

## 威胁评分：目标对AI方的威胁程度
func _calculate_threat_score(target: Unit) -> float:
	var score := 0.0
	
	# HP 高的单位更有威胁（存活能力强）
	var hp_ratio = float(target.current_hp) / float(max(target.get_max_hp(), 1))
	score += hp_ratio * 3.0
	
	# 攻击力高的更有威胁
	var weapon: WeaponData = target.get_main_hand()
	if weapon:
		var max_dmg = weapon.damage_dice_count * weapon.damage_dice_sides
		score += min(max_dmg, 15.0) * 0.3
	else:
		score += 0.5  # 徒手威胁低
	
	# 远程单位威胁加成（可以安全输出）
	if weapon and weapon.is_ranged:
		score += 2.0
	
	# AC高的单位威胁较高（难杀=持续威胁）
	score += min(target.get_ac() - 10, 10) * 0.2
	
	return score

## 脆弱性评分：目标被击杀的难易度
func _calculate_vulnerability_score(target: Unit, actor: Unit, hex_grid: HexGrid) -> float:
	var score := 0.0
	
	# HP越低越脆弱
	var hp_ratio = float(target.current_hp) / float(max(target.get_max_hp(), 1))
	score += (1.0 - hp_ratio) * 5.0  # 低血量大加分
	
	# AC越低越脆弱
	var target_ac = target.get_ac()
	var atk_bonus = actor.get_attack_bonus()
	var hit_advantage = atk_bonus - target_ac
	score += clampf(hit_advantage * 0.5, -2.0, 4.0)
	
	# 掩体减少脆弱性（远程更难命中）
	var target_cell = hex_grid.get_cell(target.grid_pos.x, target.grid_pos.y)
	if target_cell:
		score -= target_cell.cover_type * 1.5
	
	return score

## 战略价值：击杀的意义（收割残血、孤立目标）
func _calculate_strategic_value(target: Unit, actor: Unit) -> float:
	var score := 0.0
	
	# 濒死目标（可以收割）价值极高
	var hp_ratio = float(target.current_hp) / float(max(target.get_max_hp(), 1))
	if hp_ratio <= 0.25:
		score += 6.0  # 收割优先
	elif hp_ratio <= 0.5:
		score += 2.0
	
	# 低HP=可能一击击杀
	var estimated_max_dmg := 1
	var weapon: WeaponData = actor.get_main_hand()
	if weapon:
		estimated_max_dmg = weapon.damage_dice_count * weapon.damage_dice_sides + actor.get_stat_modifier(actor.data.str)
	
	if target.current_hp <= estimated_max_dmg:
		score += 4.0  # 可能一击必杀
	
	return score

## 检查本回合能否到达目标
func can_reach_target(actor: Unit, target: Unit, _hex_grid: HexGrid):
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var move_range := actor.get_move_range()
	var total_reach := move_range + atk_range
	var dist := HexUtils.distance(actor.grid_pos.x, actor.grid_pos.y, target.grid_pos.x, target.grid_pos.y)
	return dist <= total_reach

## 检查从指定位置能否攻击到目标
func can_attack_from_position(actor: Unit, target: Unit, hex_grid: HexGrid, from_pos: Vector2i) -> bool:
	var weapon: WeaponData = actor.get_main_hand()
	var atk_range: int
	if weapon:
		atk_range = AISpatialAnalyzer.get_effective_range(hex_grid, actor, from_pos, target.grid_pos)
	else:
		atk_range = 1
	var dist := HexUtils.distance(from_pos.x, from_pos.y, target.grid_pos.x, target.grid_pos.y)
	return dist <= atk_range
