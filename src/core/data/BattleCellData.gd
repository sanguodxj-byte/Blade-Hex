# BattleCellData.gd
# 战斗地图格子扩展数据 — 包含地形类型枚举和完整地形属性
extends Resource
class_name BattleCellData

## 地形类型枚举，对应策划案 03-战术战斗系统 的地形表
enum TerrainType {
	PLAINS,          # 平地：默认地形
	GRASSLAND,       # 草地
	SAVANNA,         # 稀树草原
	FOREST,          # 森林
	DENSE_FOREST,    # 密林
	HILLS,           # 丘陵
	MOUNTAIN,        # 山地
	SHALLOW_WATER,   # 浅水
	DEEP_WATER,      # 深水
	SWAMP,           # 沼泽
	ROAD,            # 道路
	SAND,            # 沙地
	SNOW,            # 雪地
	WALL,            # 墙壁
	RUINS,           # 建筑废墟
	POISON_MUSHROOM, # 毒菇群
	LUCKY_GRASS,     # 幸运草丛
}

@export var terrain_type: TerrainType = TerrainType.PLAINS
@export var terrain_name: String = "平地"
@export var move_cost: int = 1
@export var ac_bonus: int = 0
@export var cover_level: int = 0        # 0=无掩体, 1=半掩体, 2=全掩体
@export var blocks_line_of_sight: bool = false
@export var elevation: int = 1           # 0=低地, 1=平地, 2=高地
@export var is_passable: bool = true
@export var is_river: bool = false
@export var special_effect: String = ""  # 特殊效果标识
@export var terrain_color: Color = Color.WHITE


## 根据 TerrainType 返回完整属性字典，供生成器使用
## 对应策划案 03-战术战斗系统 → 二、地形系统
static func get_terrain_properties(type: TerrainType) -> Dictionary:
	match type:
		TerrainType.PLAINS:
			return {
				"terrain_name": "平地",
				"move_cost": 1,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "",
				"color": Color(0.85, 0.82, 0.65),  # 灰黄
			}
		TerrainType.GRASSLAND:
			return {
				"terrain_name": "草地",
				"move_cost": 1,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "",
				"color": Color(0.45, 0.72, 0.35),  # 绿色
			}
		TerrainType.SAVANNA:
			return {
				"terrain_name": "稀树草原",
				"move_cost": 1,
				"ac_bonus": 1,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "",
				"color": Color(0.65, 0.78, 0.40),  # 黄绿
			}
		TerrainType.FOREST:
			return {
				"terrain_name": "森林",
				"move_cost": 2,
				"ac_bonus": 2,
				"cover_level": 1,
				"blocks_los": true,
				"is_passable": true,
				"special_effect": "stealth_bonus",
				"color": Color(0.25, 0.55, 0.20),  # 深绿
			}
		TerrainType.DENSE_FOREST:
			return {
				"terrain_name": "密林",
				"move_cost": 3,
				"ac_bonus": 3,
				"cover_level": 2,
				"blocks_los": true,
				"is_passable": true,
				"special_effect": "stealth_major_bonus",
				"color": Color(0.15, 0.40, 0.12),  # 更深绿
			}
		TerrainType.HILLS:
			return {
				"terrain_name": "丘陵",
				"move_cost": 2,
				"ac_bonus": 2,
				"cover_level": 1,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "high_ground_advantage",
				"color": Color(0.72, 0.68, 0.52),  # 土黄
			}
		TerrainType.MOUNTAIN:
			return {
				"terrain_name": "山地",
				"move_cost": 3,
				"ac_bonus": 3,
				"cover_level": 2,
				"blocks_los": true,
				"is_passable": true,
				"special_effect": "no_mount;vision_plus_2",
				"color": Color(0.55, 0.50, 0.45),  # 灰棕
			}
		TerrainType.SHALLOW_WATER:
			return {
				"terrain_name": "浅水",
				"move_cost": 2,
				"ac_bonus": -1,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "fire_resist_2;ice_lightning_weakness",
				"color": Color(0.40, 0.60, 0.85),  # 浅蓝
			}
		TerrainType.DEEP_WATER:
			return {
				"terrain_name": "深水",
				"move_cost": 3,
				"ac_bonus": -2,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "requires_swim;casting_disadvantage",
				"color": Color(0.20, 0.35, 0.70),  # 深蓝
			}
		TerrainType.SWAMP:
			return {
				"terrain_name": "沼泽",
				"move_cost": 2,
				"ac_bonus": -1,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "fortitude_dc12_poison",
				"color": Color(0.45, 0.55, 0.30),  # 暗黄绿
			}
		TerrainType.ROAD:
			return {
				"terrain_name": "道路",
				"move_cost": 1,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "move_cost_half",
				"color": Color(0.75, 0.65, 0.45),  # 棕色
			}
		TerrainType.SAND:
			return {
				"terrain_name": "沙地",
				"move_cost": 2,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "no_charge",
				"color": Color(0.90, 0.82, 0.55),  # 沙黄
			}
		TerrainType.SNOW:
			return {
				"terrain_name": "雪地",
				"move_cost": 2,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "move_minus_1",
				"color": Color(0.92, 0.95, 0.98),  # 白蓝
			}
		TerrainType.WALL:
			return {
				"terrain_name": "墙壁",
				"move_cost": 99,
				"ac_bonus": 0,
				"cover_level": 2,
				"blocks_los": true,
				"is_passable": false,
				"special_effect": "siege_destroyable",
				"color": Color(0.40, 0.40, 0.42),  # 深灰
			}
		TerrainType.RUINS:
			return {
				"terrain_name": "建筑废墟",
				"move_cost": 2,
				"ac_bonus": 2,
				"cover_level": 1,
				"blocks_los": true,
				"is_passable": true,
				"special_effect": "destroyable_to_plains",
				"color": Color(0.58, 0.54, 0.48),  # 灰色
			}
		TerrainType.POISON_MUSHROOM:
			return {
				"terrain_name": "毒菇群",
				"move_cost": 1,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "poison_2_turns",
				"color": Color(0.55, 0.30, 0.60),  # 紫色
			}
		TerrainType.LUCKY_GRASS:
			return {
				"terrain_name": "幸运草丛",
				"move_cost": 1,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "crit_rate_plus_10_one_attack",
				"color": Color(0.30, 0.80, 0.45),  # 亮绿
			}
		_:
			return {
				"terrain_name": "平地",
				"move_cost": 1,
				"ac_bonus": 0,
				"cover_level": 0,
				"blocks_los": false,
				"is_passable": true,
				"special_effect": "",
				"color": Color(0.85, 0.82, 0.65),
			}


## 从 TerrainType 快速创建一个已填充属性的 BattleCellData
static func create_from_type(type: TerrainType, elev: int = 1) -> BattleCellData:
	var data = BattleCellData.new()
	var props = get_terrain_properties(type)
	data.terrain_type = type
	data.terrain_name = props["terrain_name"]
	data.move_cost = props["move_cost"]
	data.ac_bonus = props["ac_bonus"]
	data.cover_level = props["cover_level"]
	data.blocks_line_of_sight = props["blocks_los"]
	data.is_passable = props["is_passable"]
	data.special_effect = props["special_effect"]
	data.terrain_color = props["color"]
	data.elevation = elev
	return data
