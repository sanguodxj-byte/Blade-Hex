# CombatManager.gd
# 战斗流程总控类
extends Node
class_name CombatManager

enum CombatState {
	INIT,
	PLAYER_TURN,
	ENEMY_TURN,
	COMBAT_END
}

var current_state: CombatState = CombatState.INIT
var all_units: Array[Unit] = []
var player_units: Array[Unit] = []
var enemy_units: Array[Unit] = []

var active_unit: Unit = null

## AI难度配置
var difficulty_config: AIDifficultyConfig = null

## 状态效果管理器（统一委托）
var status_effect_manager: StatusEffectManager = null

signal turn_started(state: CombatState)
signal combat_ended(victory: bool)
signal skill_used(caster: Unit, skill_effect: String, result: Dictionary)

func _ready():
	# 初始化状态效果管理器
	status_effect_manager = StatusEffectManager.new()
	add_child(status_effect_manager)

## 设置难度
func set_difficulty(config: AIDifficultyConfig):
	difficulty_config = config

## 获取难度配置（如果未设置则创建默认普通难度）
func get_difficulty_config() -> AIDifficultyConfig:
	if difficulty_config == null:
		difficulty_config = AIDifficultyConfig.new()
	return difficulty_config

## 注册参战单位
func register_unit(unit: Unit, is_player: bool):
	all_units.append(unit)
	if is_player:
		player_units.append(unit)
	else:
		enemy_units.append(unit)
	
	# 监听单位死亡信号
	unit.tree_exited.connect(_on_unit_died.bind(unit, is_player))

## 开始战斗
func start_combat():
	# 实际项目中这里可能需要按 initiative 排序
	# 目前简化为玩家先手
	change_state(CombatState.PLAYER_TURN)

func change_state(new_state: CombatState):
	current_state = new_state
	
	if current_state == CombatState.PLAYER_TURN:
		_reset_units_actions(player_units)
		# AI 的控制逻辑
	elif current_state == CombatState.ENEMY_TURN:
		_reset_units_actions(enemy_units)
		# 触发 AI 行动
	
	turn_started.emit(current_state)

## 重置单位行动状态
func _reset_units_actions(units: Array[Unit]):
	for u in units:
		u.has_moved = false
		u.has_acted = false

## 结束当前回合
func end_current_turn():
	if current_state == CombatState.PLAYER_TURN:
		change_state(CombatState.ENEMY_TURN)
	elif current_state == CombatState.ENEMY_TURN:
		change_state(CombatState.PLAYER_TURN)

# ============================================================================
# 主动技能释放
# ============================================================================

## 释放主动技能（主入口）
func use_skill(caster: Unit, skill_effect: String, target_cell: Vector2i, grid = null) -> Dictionary:
	# 前置检查
	if not is_instance_valid(caster) or caster.current_hp <= 0:
		return {"success": false, "reason": "施放者无效"}

	if caster.has_acted:
		# 检查是否可以作为次要行动（quick_cast）
		var cfg = SkillEffectExecutor.get_skill_config(skill_effect)
		if cfg.get("action_cost", "") == "minor":
			return {"success": false, "reason": "已使用过行动"}
		elif not SkillEffectExecutor.has_quick_cast(caster):
			return {"success": false, "reason": "已使用过行动"}

	# 检查是否拥有该技能
	if not caster.has_skill_effect(skill_effect):
		return {"success": false, "reason": "未拥有该技能"}

	# 执行技能
	var result = SkillEffectExecutor.execute_active_skill(
		caster, skill_effect, target_cell, grid,
		all_units, player_units, enemy_units
	)

	if not result.get("success", false):
		return result

	# 标记行动消耗
	var action_cost: String = result.get("action_cost", "major")
	if action_cost == "major":
		caster.has_acted = true
	elif action_cost == "minor":
		caster.has_acted = true  # 次要行动也消耗行动（quick_cast除外单独处理）

	# 播放技能特效
	var vfx_type: String = result.get("vfx_type", "")
	if vfx_type != "" and is_instance_valid(caster.get_parent()):
		VFXManager.play_skill_vfx(caster.get_parent(), caster.global_position, vfx_type)

	# 应用状态效果
	_apply_status_effects(result)

	# 发射信号
	skill_used.emit(caster, skill_effect, result)

	return result


## 获取单位可用的主动技能列表
func get_available_skills(unit: Unit) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if not unit.skill_tree:
		return result
	var active_skills = unit.get_active_skill_nodes()
	for node in active_skills:
		if node.skill_effect != "" and SkillEffectExecutor.is_active_skill(node.skill_effect):
			var cfg = SkillEffectExecutor.get_skill_config(node.skill_effect)
			result.append({
				"skill_effect": node.skill_effect,
				"node_id": node.node_id,
				"node_name": node.node_name,
				"description": node.description,
				"target_type": cfg.get("target", -1),
				"action_cost": cfg.get("action_cost", "major"),
				"vfx_type": cfg.get("vfx", ""),
				"category": cfg.get("category", -1),
			})
	return result


## 获取技能的目标格子（用于 AI 或 UI 辅助选择）
func get_skill_target_cells(caster: Unit, skill_effect: String, _grid):
	var cfg = SkillEffectExecutor.get_skill_config(skill_effect)
	if cfg.is_empty():
		return []
	var target_type: int = cfg.get("target", -1)
	var result_cells: Array[Vector2i] = []

	match target_type:
		SkillEffectExecutor.TargetType.SELF:
			result_cells.append(caster.grid_pos)
		SkillEffectExecutor.TargetType.SINGLE_ENEMY, SkillEffectExecutor.TargetType.RANGED_SINGLE:
			var enemies = enemy_units if not caster.data.is_enemy else player_units
			for e in enemies:
				if is_instance_valid(e) and e.current_hp > 0:
					result_cells.append(e.grid_pos)
		SkillEffectExecutor.TargetType.SINGLE_ALLY:
			var allies = player_units if not caster.data.is_enemy else enemy_units
			for a in allies:
				if is_instance_valid(a) and a.current_hp > 0:
					result_cells.append(a.grid_pos)
		_, -1:
			pass

	return result_cells


# ============================================================================
# 状态效果应用（处理 SkillEffectExecutor 返回的状态效果指令）
# ============================================================================

## 应用技能执行结果中的状态效果（委托给 StatusEffectManager）
func _apply_status_effects(skill_result: Dictionary):
	var effects = skill_result.get("status_effects", [])
	for eff in effects:
		if not eff is Dictionary:
			continue
		var target_unit = eff.get("target", null)
		if not is_instance_valid(target_unit) or not target_unit is Unit:
			continue

		var effect_id: String = eff.get("effect_id", "")
		var special: String = eff.get("special", "")

		# 特殊处理：移除效果
		if special == "remove_effects":
			var remove_ids = eff.get("remove_ids", [])
			for rid in remove_ids:
				status_effect_manager.remove_effect(target_unit, rid)
			continue

		if special == "remove_all_negative":
			status_effect_manager.remove_all_negative(target_unit)
			continue

		# 普通状态效果：委托给 StatusEffectManager
		if effect_id != "":
			var duration: int = eff.get("duration", -1)
			var stat_mods = eff.get("stat_modifiers", {})
			status_effect_manager.apply_effect(target_unit, effect_id, duration)
			# 额外的 stat_modifiers 合并到已有效果
			if not stat_mods.is_empty():
				for existing in target_unit.data.active_status_effects:
					if existing["id"] == effect_id:
						for key in stat_mods:
							existing["stat_modifiers"][key] = stat_mods[key]
						break

## 单位死亡回调，检查胜负条件
func _on_unit_died(unit: Unit, is_player: bool):
	all_units.erase(unit)
	if is_player:
		player_units.erase(unit)
		if player_units.is_empty():
			_end_combat(false)
	else:
		enemy_units.erase(unit)
		if enemy_units.is_empty():
			_end_combat(true)

func _end_combat(victory: bool):
	current_state = CombatState.COMBAT_END
	combat_ended.emit(victory)
