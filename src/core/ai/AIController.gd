# AIController.gd
# AI 主控制器 —— 编排所有敌方单位的回合行动
# 替换 CombatScene 中硬编码的 _simulate_enemy_turn()
# 职责：策略初始化/分发、失误注入、行动执行、战斗日志输出
extends Node
class_name AIController

signal all_actions_completed

var difficulty_config: AIDifficultyConfig
var strategies: Dictionary = {}  # AIStrategy枚举值 -> AIStrategyBase实例

## 战斗场景引用（需要调用 _move_unit_to 等）
var combat_scene: Node = null

func _init(config: AIDifficultyConfig = null):
	if config == null:
		difficulty_config = AIDifficultyConfig.new()
	else:
		difficulty_config = config
	_init_strategies()

func _init_strategies():
	strategies[UnitData.AIStrategy.RECKLESS] = AIStrategyReckless.new(difficulty_config)
	strategies[UnitData.AIStrategy.CAUTIOUS] = AIStrategyCautious.new(difficulty_config)
	strategies[UnitData.AIStrategy.TACTICAL] = AIStrategyTactical.new(difficulty_config)
	strategies[UnitData.AIStrategy.INSTINCT] = AIStrategyInstinct.new(difficulty_config)

## 设置战斗场景引用（用于调用移动方法）
func set_combat_scene(scene: Node):
	combat_scene = scene

## 主入口：执行所有敌方单位的回合行动
func execute_enemy_turn(enemy_units: Array, player_units: Array, hex_grid: HexGrid, combat_ui: CombatUI):
	# 按策略优先级排序执行（战术>谨慎>鲁莽>本能）
	var sorted_enemies = _sort_by_priority(enemy_units)
	
	for enemy in sorted_enemies:
		if not is_instance_valid(enemy) or enemy.current_hp <= 0:
			continue
		
		# 为当前单位决策
		var action = decide_action_for_unit(enemy, player_units, enemy_units, hex_grid)
		
		# 执行行动
		await _execute_action(action, hex_grid, combat_ui)
		
		# 行动间短暂延迟，增强可读性
		await get_tree().create_timer(0.4).timeout
	
	all_actions_completed.emit()

## 为单个单位决策
func decide_action_for_unit(actor: Unit, player_units: Array, enemy_units: Array, hex_grid: HexGrid) -> AIAction:
	var strategy_key = actor.data.ai_strategy
	var strategy: AIStrategyBase = strategies.get(strategy_key, strategies[UnitData.AIStrategy.INSTINCT])
	
	# 难度失误注入：随机概率降级为本能策略
	if randf() < difficulty_config.mistake_chance:
		strategy = strategies[UnitData.AIStrategy.INSTINCT]
	
	return strategy.decide_action(actor, player_units, enemy_units, hex_grid)

## 执行一个行动计划
func _execute_action(action: AIAction, hex_grid: HexGrid, combat_ui: CombatUI):
	if not action or not is_instance_valid(action.actor):
		return
	
	match action.action_type:
		AIAction.Type.MOVE_THEN_ATTACK:
			await _execute_move(action, hex_grid, combat_ui)
			if is_instance_valid(action.actor) and action.actor.current_hp > 0:
				await _execute_attack(action, hex_grid, combat_ui)
			action.actor.has_moved = true
			action.actor.has_acted = true
		
		AIAction.Type.ATTACK:
			await _execute_attack(action, hex_grid, combat_ui)
			action.actor.has_acted = true
		
		AIAction.Type.MOVE_ONLY:
			await _execute_move(action, hex_grid, combat_ui)
			action.actor.has_moved = true
		
		AIAction.Type.RETREAT:
			combat_ui.log_message("[color=yellow]%s[/color]" % action.description)
			await _execute_move(action, hex_grid, combat_ui)
			action.actor.has_moved = true
			# TODO: 到达地图边缘后移除单位
		
		AIAction.Type.OVERWATCH:
			combat_ui.log_message("%s 进入防御姿态。" % action.actor.data.unit_name)
			action.actor.has_acted = true
		
		AIAction.Type.IDLE:
			combat_ui.log_message("[color=gray]%s[/color]" % action.description)

## 执行移动
func _execute_move(action: AIAction, hex_grid: HexGrid, combat_ui: CombatUI):
	if action.move_path.is_empty():
		# 没有缓存路径，尝试实时寻路
		if action.target_position != Vector2i(-1, -1):
			action.move_path = hex_grid.find_path(action.actor.grid_pos, action.target_position)
		if action.move_path.is_empty():
			return
	
	var final_pos: Vector2i = action.move_path[action.move_path.size() - 1]
	
	# 通过战斗场景执行移动（复用其 _move_unit_to 逻辑）
	if combat_scene and combat_scene.has_method("_move_unit_to"):
		combat_scene._move_unit_to(action.actor, final_pos.x, final_pos.y)
	
	combat_ui.log_message("%s 移动到 (%d, %d)" % [action.actor.data.unit_name, final_pos.x, final_pos.y])

## 执行攻击（统一通过 CombatResolver 结算）
func _execute_attack(action: AIAction, hex_grid: HexGrid, combat_ui: CombatUI):
	if not is_instance_valid(action.target_unit) or action.target_unit.current_hp <= 0:
		return
	
	var actor := action.actor
	var target := action.target_unit
	
	# 远程全掩体检查（CombatResolver不处理全掩体阻挡，需前置判断）
	var weapon: WeaponData = actor.get_main_hand()
	if weapon and weapon.is_ranged:
		var target_cell = hex_grid.get_cell(target.grid_pos.x, target.grid_pos.y)
		if target_cell and target_cell.cover_type == 2:
			combat_ui.log_message("[color=gray]%s 被 %s 的全掩体阻挡，无法射击。[/color]" % [target.data.unit_name, actor.data.unit_name])
			return
	
	# 攻击检定前播放动画
	actor.play_anim("attack")
	await get_tree().create_timer(0.6).timeout
	
	# 使用 CombatResolver 统一结算（包含所有被动加成、高地、掩体、包夹、冲锋等）
	var result = CombatResolver.resolve_attack(actor, target, hex_grid, action.is_charge)
	
	# AI特有标记：覆盖包夹/背刺信息（AIAction中可能携带额外的战术信息）
	if action.is_backstab and result.get("is_flanking", false):
		result["flank_direction"] = "rear"
	elif action.is_flanking and not result.get("is_flanking", false):
		result["is_flanking"] = true
		result["flank_direction"] = "flank"
	
	if result["hit"]:
		var dmg = result["damage"]
		
		# 构建战斗日志
		var log_parts: Array[String] = []
		log_parts.append("[color=red]%s 命中 %s，造成 %d 伤害[/color]" % [actor.data.unit_name, target.data.unit_name, dmg])
		if result.get("critical", false):
			log_parts.append("[color=yellow]★暴击！[/color]")
		if action.is_charge:
			log_parts.append("[color=orange]冲锋加成！[/color]")
		if result.get("is_flanking", false):
			var flank_dir = result.get("flank_direction", "flank")
			if flank_dir == "rear":
				log_parts.append("[color=orange]背刺！[/color]")
			else:
				log_parts.append("[color=orange]包夹！[/color]")
		if result.get("death_saved", false):
			log_parts.append("[color=cyan]坚韧意志！[/color]")
		if result.get("damage_reduction", 0) > 0:
			log_parts.append("[color=gray]减免%d[/color]" % result["damage_reduction"])
		
		combat_ui.log_message(" ".join(log_parts))
		# damage already applied by CombatResolver.resolve_attack → defender.take_damage
		
		# 更新UI
		if target.data.is_enemy:
			combat_ui.update_enemy_info(target)
		else:
			combat_ui.update_unit_info(target)
		
		# 击杀处理
		if target.current_hp <= 0:
			combat_ui.log_message("[color=yellow]%s 被 %s 击败！[/color]" % [target.data.unit_name, actor.data.unit_name])
			_apply_kill_morale(actor, target, hex_grid)
			var target_cell = hex_grid.get_cell(target.grid_pos.x, target.grid_pos.y)
			if target_cell:
				target_cell.occupant = null
			if target.data.is_enemy:
				combat_ui.remove_enemy(target)
	else:
		if result.get("fumble", false):
			combat_ui.log_message("[color=red]%s 严重失误！[/color]" % actor.data.unit_name)
		else:
			combat_ui.log_message("[color=gray]%s 的攻击未命中 %s。[/color]" % [actor.data.unit_name, target.data.unit_name])
	
	# 回到待机
	actor.play_anim("default")

## 击杀后的士气变动
func _apply_kill_morale(_killer: Unit, _victim: Unit, _hex_grid: HexGrid):
	# 击杀者方士气提升（简化：所有存活友军+5）
	# 被击杀方士气下降（所有存活友军-8）
	# 完整版应按距离递减，此处简化处理
	pass  # TODO: 完整士气系统后续实现

## 按策略优先级排序敌方单位
func _sort_by_priority(enemies: Array) -> Array:
	var priority_order := {
		UnitData.AIStrategy.TACTICAL: 0,
		UnitData.AIStrategy.CAUTIOUS: 1,
		UnitData.AIStrategy.RECKLESS: 2,
		UnitData.AIStrategy.INSTINCT: 3,
	}
	var sorted = enemies.duplicate()
	sorted.sort_custom(func(a, b):
		if not is_instance_valid(a) or not a.data: return false
		if not is_instance_valid(b) or not b.data: return true
		return priority_order.get(a.data.ai_strategy, 99) < priority_order.get(b.data.ai_strategy, 99)
	)
	return sorted
