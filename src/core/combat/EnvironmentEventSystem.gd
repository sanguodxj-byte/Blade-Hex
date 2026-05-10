# EnvironmentEventSystem.gd
# 环境事件系统 — 战场环境效果（暴风雨、浓雾、地震等）
# 对应策划案 03-战术战斗系统 → 十、战场环境事件
extends Node
class_name EnvironmentEventSystem

# ============================================================================
# 环境事件类型
# ============================================================================

enum EnvironmentEventType {
	STORM,       # 暴风雨
	FOG,         # 浓雾
	EARTHQUAKE,  # 地震
	POISON_FOG,  # 毒雾
	HOLY_LIGHT,  # 圣光
	LAVA,        # 熔岩涌动
}

# ============================================================================
# 事件配置
# ============================================================================

## 事件触发间隔（每N回合触发一次）
const EVENT_INTERVALS = {
	EnvironmentEventType.STORM: 3,
	EnvironmentEventType.FOG: 1,       # 持续效果
	EnvironmentEventType.EARTHQUAKE: 5,
	EnvironmentEventType.POISON_FOG: 2,
	EnvironmentEventType.HOLY_LIGHT: 1, # 每回合
	EnvironmentEventType.LAVA: 3,
}

## 事件与模板的映射
static func get_applicable_events(template_name: String) -> Array:
	match template_name:
		"swamp_battle":
			return [EnvironmentEventType.STORM, EnvironmentEventType.FOG]
		"forest_ambush":
			return [EnvironmentEventType.FOG]
		"mountain_pass":
			return [EnvironmentEventType.EARTHQUAKE]
		"coastal_ambush":
			return [EnvironmentEventType.STORM]
		_:
			return []

# ============================================================================
# 事件状态
# ============================================================================

## 活跃事件计时器（事件类型 → 剩余回合数）
var event_timers: Dictionary = {}

## 当前活跃的环境事件
var active_events: Array[EnvironmentEventType] = []

# ============================================================================
# 事件处理
# ============================================================================

## 回合结束时处理环境事件
func process_events(round_number: int, grid: HexGrid, all_units: Array[Unit]):
	for event_type in active_events:
		var interval = EVENT_INTERVALS.get(event_type, 1)
		if round_number > 0 and round_number % interval != 0:
			continue

		match event_type:
			EnvironmentEventType.STORM:
				_process_storm(grid, all_units)
			EnvironmentEventType.FOG:
				_process_fog(grid, all_units)
			EnvironmentEventType.EARTHQUAKE:
				_process_earthquake(grid)
			EnvironmentEventType.POISON_FOG:
				_process_poison_fog(grid, all_units)
			EnvironmentEventType.HOLY_LIGHT:
				_process_holy_light(all_units)
			EnvironmentEventType.LAVA:
				_process_lava(grid, all_units)

## 激活环境事件
func activate_event(event_type: EnvironmentEventType):
	if not active_events.has(event_type):
		active_events.append(event_type)

## 移除环境事件
func deactivate_event(event_type: EnvironmentEventType):
	active_events.erase(event_type)


# ============================================================================
# 各事件实现
# ============================================================================

## 暴风雨：所有露天单位潮湿，远程命中率-20%
func _process_storm(grid: HexGrid, all_units: Array[Unit]):
	for unit in all_units:
		if not is_instance_valid(unit) or unit.current_hp <= 0:
			continue
		# 检查是否在露天（不在建筑/洞穴内）
		var cell = grid.get_cell(unit.grid_pos.x, unit.grid_pos.y)
		if cell and cell.data:
			if cell.data.terrain_type != BattleCellData.TerrainType.RUINS:
				# 施加潮湿状态
				# status_manager.apply_effect(unit, "wet", 3)
				pass

## 浓雾：所有单位视野减半，远程射程-2
func _process_fog(_grid: HexGrid, _all_units: Array):
	# 浓雾是持续效果，在视野计算时处理
	# 此处标记状态即可
	pass

## 地震：随机破坏2-3个格子变为废墟
func _process_earthquake(grid: HexGrid):
	var destroy_count = randi_range(2, 3)
	var all_cells = grid.cells.keys()
	if all_cells.size() == 0:
		return

	for i in range(destroy_count):
		var random_cell_pos = all_cells[randi() % all_cells.size()]
		var cell = grid.get_cell(random_cell_pos.x, random_cell_pos.y)
		if cell and cell.data:
			# 不破坏墙壁和深水
			if cell.data.terrain_type in [BattleCellData.TerrainType.WALL, BattleCellData.TerrainType.DEEP_WATER]:
				continue
			# 变为废墟
			var new_data = BattleCellData.create_from_type(BattleCellData.TerrainType.RUINS, cell.elevation)
			cell.data = new_data

## 毒雾：低地区域单位中毒
func _process_poison_fog(grid: HexGrid, all_units: Array[Unit]):
	for unit in all_units:
		if not is_instance_valid(unit) or unit.current_hp <= 0:
			continue
		var cell = grid.get_cell(unit.grid_pos.x, unit.grid_pos.y)
		if cell and cell.elevation == 0:  # 低地区域
			# 施加中毒状态
			# status_manager.apply_effect(unit, "poison", 2)
			pass

## 圣光：友方牧师/神圣单位每回合恢复1d4 HP
func _process_holy_light(all_units: Array[Unit]):
	for unit in all_units:
		if not is_instance_valid(unit) or unit.current_hp <= 0:
			continue
		if unit.data.is_enemy:
			continue
		# 检查是否是治疗/辅助方向的角色（简化：有治疗法术的）
		for spell in unit.data.known_spells:
			if spell.heal_dice_count > 0:
				var heal = RPGRuleEngine.roll_dice(1, 4)
				unit.current_hp = mini(unit.current_hp + heal, unit.get_max_hp())
				break

## 熔岩涌动：随机格子变为熔岩
func _process_lava(grid: HexGrid, _all_units: Array):
	var all_cells = grid.cells.keys()
	if all_cells.size() == 0:
		return

	# 随机1-2个格子变为危险区域
	for i in range(randi_range(1, 2)):
		var random_pos = all_cells[randi() % all_cells.size()]
		var cell = grid.get_cell(random_pos.x, random_pos.y)
		if not cell:
			continue
		# 站在上面的单位受到2d6火伤
		if cell.occupant and is_instance_valid(cell.occupant):
			var dmg = RPGRuleEngine.roll_dice(2, 6)
			cell.occupant.take_damage(dmg)
