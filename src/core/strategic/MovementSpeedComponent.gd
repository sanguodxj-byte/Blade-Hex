# MovementSpeedComponent.gd
# 大地图行进移动速度组件 — 汇总所有速度修正因子，输出最终移动速度
#
# 修正因子来源（按优先级）：
#   1. 基础速度 (base_speed)
#   2. 地形修正 ( Plains=1.0, Forest=0.7, Mountain=0.5, Swamp=0.5, Water=0.3, Road=1.5, Desert=0.8 )
#   3. 季节修正 ( 春/夏/秋=1.0, 冬=0.5 )
#   4. 昼夜修正 ( 白天=1.0, 夜间=0.75 )
#   5. 队伍负重修正 ( 无负重=1.0, 满载=0.7 )
#   6. 坐骑修正 ( 骑乘=1.3, 无=1.0 )
#   7. 技能盘修正 ( 由 SkillEffectExecutor 提供 )
#
# 使用方式：
#   var speed_comp = MovementSpeedComponent.new()
#   speed_comp.overworld_map = overworld_map
#   speed_comp.economy_manager = economy_manager
#   var final_speed = speed_comp.calculate_speed(player_party.position)
extends RefCounted
class_name MovementSpeedComponent


## ============================================================================
# 配置
## ============================================================================

## 基础移动速度（像素/秒）
@export var base_speed: float = 300.0

## 夜间速度衰减（0~1，1=无衰减）
@export var night_speed_factor: float = 0.75

## 负重影响系数（负重率0→1，乘以此系数得到衰减）
@export var encumbrance_penalty: float = 0.3

## 坐骑速度加成（基础倍率，speed_bonus 格数会映射为额外加成）
@export var mount_base_bonus: float = 1.25

## 每级坐骑speed_bonus的额外速度加成
@export var mount_speed_per_bonus: float = 0.05


## ============================================================================
# 依赖引用
## ============================================================================

## 六边形瓦片地图引用（用于地形采样）
var hex_grid: HexOverworldGrid = null

## 经济管理器引用（用于查询季节/时间）
var economy_manager: EconomyManager = null

## 角色数据引用（用于查询坐骑/负重/技能盘）
var unit_data: UnitData = null


## ============================================================================
# 速度计算
## ============================================================================

## 计算最终移动速度（像素/秒）
## position: 队伍当前世界坐标
## 返回: 最终速度（含所有修正）
func calculate_speed(position: Vector2) -> float:
	var speed := base_speed

	# ---- 1. 地形修正 ----
	speed *= _get_terrain_factor(position)

	# ---- 2. 季节修正 ----
	speed *= _get_season_factor()

	# ---- 3. 昼夜修正 ----
	speed *= _get_day_night_factor()

	# ---- 4. 负重修正 ----
	speed *= _get_encumbrance_factor()

	# ---- 5. 坐骑修正 ----
	speed *= _get_mount_factor()

	# ---- 6. 技能盘修正 ----
	speed *= _get_skill_factor()

	# 保底：最低不低于基础速度的20%
	speed = maxf(speed, base_speed * 0.2)

	return speed


## 获取速度分解报告（供UI调试显示）
func get_speed_breakdown(position: Vector2) -> Dictionary:
	var breakdown := {}
	breakdown["base"] = base_speed
	breakdown["terrain"] = _get_terrain_factor(position)
	breakdown["season"] = _get_season_factor()
	breakdown["day_night"] = _get_day_night_factor()
	breakdown["encumbrance"] = _get_encumbrance_factor()
	breakdown["mount"] = _get_mount_factor()
	breakdown["skill"] = _get_skill_factor()
	breakdown["final"] = calculate_speed(position)

	# 地形名称
	var terrain = _sample_terrain(position)
	breakdown["terrain_name"] = OverworldTerrain.get_name(terrain)

	return breakdown


## ============================================================================
# 各修正因子
## ============================================================================

## 地形修正
func _get_terrain_factor(position: Vector2) -> float:
	var terrain = _sample_terrain(position)
	return OverworldTerrain.get_move_speed_multiplier(terrain)


## 季节修正
func _get_season_factor() -> float:
	if not economy_manager:
		return 1.0
	if economy_manager.get_season() == EconomyManager.Season.WINTER:
		return 0.5
	return 1.0


## 昼夜修正（18:00~06:00 视为夜间）
func _get_day_night_factor() -> float:
	if not economy_manager:
		return 1.0
	var hour = economy_manager.current_hour
	if hour >= 18.0 or hour < 6.0:
		return night_speed_factor
	return 1.0


## 负重修正
## 负重率 = 背包物品数 / 最大容量（无上限时默认无惩罚）
func _get_encumbrance_factor() -> float:
	if not unit_data or not economy_manager:
		return 1.0

	var inventory_count = economy_manager.player_inventory.size()
	# 简化模型：每10件物品速度降5%，最多降30%
	if inventory_count <= 10:
		return 1.0
	var overload = minf(float(inventory_count - 10) / 20.0, 1.0)
	return 1.0 - overload * encumbrance_penalty


## 坐骑修正
func _get_mount_factor() -> float:
	if not unit_data:
		return 1.0
	if unit_data.is_mounted and unit_data.mount:
		# 基础骑乘加成 + 坐骑speed_bonus的额外加成
		var mount_bonus = mount_base_bonus + unit_data.mount.speed_bonus * mount_speed_per_bonus
		return mount_bonus
	return 1.0


## 技能盘修正（移速相关被动）
func _get_skill_factor() -> float:
	if not unit_data:
		return 1.0

	var factor := 1.0

	# 技能盘移速加成（由 CharacterSkillTree 提供）
	# 需要访问全局技能盘管理器获取角色技能树
	var mgr = SkillTreeManager.get_instance()
	if mgr and unit_data.character_id >= 0:
		var tree = mgr.get_skill_tree(unit_data.character_id)
		if tree:
			factor += float(tree.get_speed_bonus()) / 100.0

	# Keystone速度惩罚（diamond_body: -2格 → 映射为速度减少）
	# Keystone速度惩罚（diamond_body: -2格 → 映射为速度减少）
	var keystone_penalty := 0
	if unit_data.skill_tree_data.get("diamond_body", false):
		keystone_penalty += 2
	if unit_data.skill_tree_data.get("life_spring", false):
		keystone_penalty += 1
	factor -= float(keystone_penalty) * 0.1

	return maxf(factor, 0.3)


## ============================================================================
# 辅助
## ============================================================================

## 采样当前位置地形
func _sample_terrain(position: Vector2) -> int:
	if hex_grid:
		return hex_grid.sample_terrain_at_pixel(position.x, position.y)
	return OverworldTerrain.Type.PLAINS
