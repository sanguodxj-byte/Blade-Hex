# BattleMapGenerator.gd
# 战斗地图生成器 — 两种生成路径:
#   1. 大地图驱动: 从 HexOverworldGrid 采样遭遇点周围地形，展开为战术地图
#   2. 模板随机: 无大地图数据时（测试/QuickCombat），从模板权重随机生成
# 包含地图模板(BattleMapTemplate)、地图数据(BattleMapData)和生成逻辑
# 战场兄弟(Battle Brothers)风格: 战术地图是大地图遭遇区域的放大版
class_name BattleMapGenerator
extends RefCounted


## 战斗规模（对应策划案 04-战略层系统 → 战场规模）
enum BattleSize {
	MERCENARY,  # 雇佣兵: 12×10, 6英雄
	KNIGHT,     # 骑士: 15×12, 4英雄+4兵种
	LORD,       # 领主: 20×16, 4英雄+7兵种
}

## 战斗规模 → 地图尺寸映射
const SIZE_MAP = {
	BattleSize.MERCENARY: {"width": 12, "height": 10},
	BattleSize.KNIGHT:    {"width": 15, "height": 12},
	BattleSize.LORD:      {"width": 20, "height": 16},
}


## =========================================
## 内部类：战斗地图模板
## =========================================
class BattleMapTemplate:
	var template_name: String = ""
	var primary_terrain: BattleCellData.TerrainType = BattleCellData.TerrainType.PLAINS
	var primary_weight: float = 0.55            # 主地形占比
	var secondary_terrains: Dictionary = {}     # TerrainType → weight
	var has_river: bool = false
	var has_road: bool = false
	var elevation_bias: float = 0.0             # 负=低地多，正=高地多
	var special_features: Array = []            # [{"type": TerrainType, "probability": float}]
	var environment_event: String = ""

	# --- 聚集特征配置 ---
	## 树木/密林簇数量范围 [min, max]
	var tree_cluster_count: Vector2i = Vector2i(2, 5)
	## 树木簇半径范围 [min, max]（六边形步数）
	var tree_cluster_radius: Vector2i = Vector2i(2, 4)
	## 密林核芯数量（在树木簇中生成密林核芯的概率）
	var dense_tree_core_chance: float = 0.3

	## 废墟结构数量范围 [min, max]
	var ruin_structure_count: Vector2i = Vector2i(1, 3)

	## 墙壁结构数量范围（独墙壁/断壁残垣）
	var wall_segment_count: Vector2i = Vector2i(0, 2)
	## 墙壁段长度范围 [min, max]
	var wall_segment_length: Vector2i = Vector2i(2, 5)

	## 地形过渡平滑迭代次数（越大边界越自然）
	var smoothing_passes: int = 2


## =========================================
## 内部类：战斗地图数据（生成结果）
## =========================================
class BattleMapData:
	var width: int = 12
	var height: int = 10
	var cells: Dictionary = {}                  # Vector2i → BattleCellData
	var player_deployment: Array[Vector2i] = []
	var enemy_deployment: Array[Vector2i] = []
	var environment_event: String = ""
	var template_name: String = ""


## =========================================
## 模板注册表 — 预定义的战场模板
## =========================================
var _templates: Dictionary = {}


func _init():
	_register_templates()


## 注册所有内置模板
func _register_templates():
	# --- 平原战场 ---
	var plain = BattleMapTemplate.new()
	plain.template_name = "plain_field"
	plain.primary_terrain = BattleCellData.TerrainType.GRASSLAND
	plain.primary_weight = 0.50
	plain.secondary_terrains = {
		BattleCellData.TerrainType.PLAINS: 0.20,
		BattleCellData.TerrainType.SAVANNA: 0.15,
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.ROAD: 0.05,
	}
	plain.has_road = true
	plain.elevation_bias = 0.0
	plain.special_features = [
		{"type": BattleCellData.TerrainType.LUCKY_GRASS, "probability": 0.03},
	]
	plain.tree_cluster_count = Vector2i(2, 4)
	plain.tree_cluster_radius = Vector2i(2, 3)
	plain.dense_tree_core_chance = 0.15
	plain.ruin_structure_count = Vector2i(0, 1)
	plain.wall_segment_count = Vector2i(0, 1)
	plain.wall_segment_length = Vector2i(2, 3)
	plain.environment_event = ""
	_templates["plain_field"] = plain

	# --- 森林伏击战 ---
	var forest = BattleMapTemplate.new()
	forest.template_name = "forest_ambush"
	forest.primary_terrain = BattleCellData.TerrainType.FOREST
	forest.primary_weight = 0.45
	forest.secondary_terrains = {
		BattleCellData.TerrainType.DENSE_FOREST: 0.20,
		BattleCellData.TerrainType.GRASSLAND: 0.15,
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.PLAINS: 0.10,
	}
	forest.has_river = false
	forest.elevation_bias = 0.1
	forest.special_features = [
		{"type": BattleCellData.TerrainType.LUCKY_GRASS, "probability": 0.02},
		{"type": BattleCellData.TerrainType.POISON_MUSHROOM, "probability": 0.02},
	]
	forest.tree_cluster_count = Vector2i(4, 8)
	forest.tree_cluster_radius = Vector2i(3, 5)
	forest.dense_tree_core_chance = 0.50
	forest.ruin_structure_count = Vector2i(0, 1)
	forest.environment_event = "fog"
	_templates["forest_ambush"] = forest

	# --- 山地隘口 ---
	var mountain = BattleMapTemplate.new()
	mountain.template_name = "mountain_pass"
	mountain.primary_terrain = BattleCellData.TerrainType.HILLS
	mountain.primary_weight = 0.40
	mountain.secondary_terrains = {
		BattleCellData.TerrainType.MOUNTAIN: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.SNOW: 0.10,
		BattleCellData.TerrainType.RUINS: 0.05,
		BattleCellData.TerrainType.GRASSLAND: 0.10,
	}
	mountain.has_road = true
	mountain.elevation_bias = 0.4
	mountain.special_features = [
		{"type": BattleCellData.TerrainType.RUINS, "probability": 0.05},
	]
	mountain.tree_cluster_count = Vector2i(1, 3)
	mountain.tree_cluster_radius = Vector2i(1, 3)
	mountain.dense_tree_core_chance = 0.10
	mountain.ruin_structure_count = Vector2i(1, 3)
	mountain.wall_segment_count = Vector2i(1, 3)
	mountain.smoothing_passes = 1
	mountain.environment_event = "earthquake"
	_templates["mountain_pass"] = mountain

	# --- 沼泽战 ---
	var swamp = BattleMapTemplate.new()
	swamp.template_name = "swamp_battle"
	swamp.primary_terrain = BattleCellData.TerrainType.SWAMP
	swamp.primary_weight = 0.40
	swamp.secondary_terrains = {
		BattleCellData.TerrainType.SHALLOW_WATER: 0.20,
		BattleCellData.TerrainType.GRASSLAND: 0.15,
		BattleCellData.TerrainType.PLAINS: 0.10,
		BattleCellData.TerrainType.HILLS: 0.05,
		BattleCellData.TerrainType.DENSE_FOREST: 0.10,
	}
	swamp.has_river = true
	swamp.elevation_bias = -0.3
	swamp.special_features = [
		{"type": BattleCellData.TerrainType.POISON_MUSHROOM, "probability": 0.06},
	]
	swamp.tree_cluster_count = Vector2i(2, 5)
	swamp.tree_cluster_radius = Vector2i(2, 4)
	swamp.dense_tree_core_chance = 0.35
	swamp.ruin_structure_count = Vector2i(0, 1)
	swamp.smoothing_passes = 3
	swamp.environment_event = "poison_fog"

	# --- 海岸伏击战 ---
	var coastal = BattleMapTemplate.new()
	coastal.template_name = "coastal_ambush"
	coastal.primary_terrain = BattleCellData.TerrainType.SHALLOW_WATER
	coastal.primary_weight = 0.30
	coastal.secondary_terrains = {
		BattleCellData.TerrainType.SAND: 0.25,
		BattleCellData.TerrainType.GRASSLAND: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.DEEP_WATER: 0.10,
	}
	coastal.has_river = false
	coastal.elevation_bias = -0.2
	coastal.special_features = [
		{"type": BattleCellData.TerrainType.LUCKY_GRASS, "probability": 0.02},
	]
	coastal.tree_cluster_count = Vector2i(1, 3)
	coastal.tree_cluster_radius = Vector2i(2, 3)
	coastal.dense_tree_core_chance = 0.10
	coastal.ruin_structure_count = Vector2i(0, 2)
	coastal.smoothing_passes = 2
	coastal.environment_event = "storm"
	_templates["coastal_ambush"] = coastal
	# --- 沙漠遭遇战 ---
	var desert = BattleMapTemplate.new()
	desert.template_name = "desert_skirmish"
	desert.primary_terrain = BattleCellData.TerrainType.SAND
	desert.primary_weight = 0.50
	desert.secondary_terrains = {
		BattleCellData.TerrainType.PLAINS: 0.20,
		BattleCellData.TerrainType.HILLS: 0.15,
		BattleCellData.TerrainType.SAVANNA: 0.10,
		BattleCellData.TerrainType.RUINS: 0.05,
	}
	desert.has_road = false
	desert.elevation_bias = 0.1
	desert.special_features = [
		{"type": BattleCellData.TerrainType.RUINS, "probability": 0.04},
	]
	desert.tree_cluster_count = Vector2i(0, 1)
	desert.tree_cluster_radius = Vector2i(1, 2)
	desert.dense_tree_core_chance = 0.0
	desert.ruin_structure_count = Vector2i(1, 3)
	desert.wall_segment_count = Vector2i(1, 2)
	desert.wall_segment_length = Vector2i(2, 4)
	desert.smoothing_passes = 1
	desert.environment_event = ""

	# --- 龙巢内部 ---
	var dragon_lair = BattleMapTemplate.new()
	dragon_lair.template_name = "dragon_lair"
	dragon_lair.primary_terrain = BattleCellData.TerrainType.MOUNTAIN
	dragon_lair.primary_weight = 0.30
	dragon_lair.secondary_terrains = {
		BattleCellData.TerrainType.RUINS: 0.20,
		BattleCellData.TerrainType.HILLS: 0.15,
		BattleCellData.TerrainType.PLAINS: 0.10,
		BattleCellData.TerrainType.SHALLOW_WATER: 0.10,  # 熔岩池用浅水代替
		BattleCellData.TerrainType.SAND: 0.10,
		BattleCellData.TerrainType.WALL: 0.05,
	}
	dragon_lair.has_river = false
	dragon_lair.has_road = false
	dragon_lair.elevation_bias = 0.5
	dragon_lair.special_features = [
		{"type": BattleCellData.TerrainType.DEEP_WATER, "probability": 0.06},  # 熔岩
	]
	dragon_lair.tree_cluster_count = Vector2i(0, 0)
	dragon_lair.tree_cluster_radius = Vector2i(1, 2)
	dragon_lair.dense_tree_core_chance = 0.0
	dragon_lair.ruin_structure_count = Vector2i(1, 2)
	dragon_lair.wall_segment_count = Vector2i(1, 3)
	dragon_lair.wall_segment_length = Vector2i(3, 6)
	dragon_lair.smoothing_passes = 0

	dragon_lair.environment_event = "lava_surge"
	_templates["dragon_lair"] = dragon_lair

	# --- 古代墓穴 ---
	var ancient_tomb = BattleMapTemplate.new()
	ancient_tomb.template_name = "ancient_tomb"
	ancient_tomb.primary_terrain = BattleCellData.TerrainType.RUINS
	ancient_tomb.primary_weight = 0.35
	ancient_tomb.secondary_terrains = {
		BattleCellData.TerrainType.WALL: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,     # 石砖地面
		BattleCellData.TerrainType.SHALLOW_WATER: 0.10, # 积水
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.FOREST: 0.05,      # 蔓生植被
		BattleCellData.TerrainType.POISON_MUSHROOM: 0.05,
	}
	ancient_tomb.has_river = false
	ancient_tomb.has_road = false
	ancient_tomb.elevation_bias = -0.2  # 地下空间偏低
	ancient_tomb.tree_cluster_count = Vector2i(0, 1)
	ancient_tomb.tree_cluster_radius = Vector2i(1, 2)
	ancient_tomb.dense_tree_core_chance = 0.05
	ancient_tomb.ruin_structure_count = Vector2i(2, 4)
	ancient_tomb.wall_segment_count = Vector2i(2, 4)
	ancient_tomb.wall_segment_length = Vector2i(3, 6)
	ancient_tomb.smoothing_passes = 1
	ancient_tomb.special_features = [
		{"type": BattleCellData.TerrainType.POISON_MUSHROOM, "probability": 0.05},
		{"type": BattleCellData.TerrainType.WALL, "probability": 0.08},
	]
	ancient_tomb.environment_event = "poison_fog"
	_templates["ancient_tomb"] = ancient_tomb

	# --- 哥布林营地 ---
	var goblin_camp = BattleMapTemplate.new()
	goblin_camp.template_name = "goblin_camp"
	goblin_camp.primary_terrain = BattleCellData.TerrainType.GRASSLAND
	goblin_camp.primary_weight = 0.30
	goblin_camp.secondary_terrains = {
		BattleCellData.TerrainType.FOREST: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.RUINS: 0.15,        # 木质建筑废墟
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.POISON_MUSHROOM: 0.05,
		BattleCellData.TerrainType.SAVANNA: 0.05,
	}
	goblin_camp.has_river = false
	goblin_camp.has_road = false
	goblin_camp.elevation_bias = -0.1
	goblin_camp.tree_cluster_count = Vector2i(2, 4)
	goblin_camp.tree_cluster_radius = Vector2i(2, 3)
	goblin_camp.dense_tree_core_chance = 0.15
	goblin_camp.ruin_structure_count = Vector2i(1, 3)
	goblin_camp.wall_segment_count = Vector2i(0, 2)
	goblin_camp.wall_segment_length = Vector2i(2, 4)
	goblin_camp.smoothing_passes = 2
	goblin_camp.special_features = [
		{"type": BattleCellData.TerrainType.POISON_MUSHROOM, "probability": 0.06},
		{"type": BattleCellData.TerrainType.RUINS, "probability": 0.08},
	]
	goblin_camp.environment_event = ""
	_templates["goblin_camp"] = goblin_camp

	# --- 狗头人矿坑 ---
	var kobold_mine = BattleMapTemplate.new()
	kobold_mine.template_name = "kobold_mine"
	kobold_mine.primary_terrain = BattleCellData.TerrainType.RUINS
	kobold_mine.primary_weight = 0.30
	kobold_mine.secondary_terrains = {
		BattleCellData.TerrainType.WALL: 0.25,        # 矿道墙壁
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.SHALLOW_WATER: 0.10, # 地下水
		BattleCellData.TerrainType.FOREST: 0.05,       # 菌类
		BattleCellData.TerrainType.POISON_MUSHROOM: 0.05,
	}
	kobold_mine.has_river = true
	kobold_mine.has_road = false
	kobold_mine.elevation_bias = -0.3
	kobold_mine.tree_cluster_count = Vector2i(0, 0)
	kobold_mine.tree_cluster_radius = Vector2i(1, 2)
	kobold_mine.dense_tree_core_chance = 0.0
	kobold_mine.ruin_structure_count = Vector2i(2, 4)
	kobold_mine.wall_segment_count = Vector2i(2, 5)
	kobold_mine.wall_segment_length = Vector2i(3, 7)
	kobold_mine.smoothing_passes = 0
	kobold_mine.special_features = [
		{"type": BattleCellData.TerrainType.WALL, "probability": 0.10},
		{"type": BattleCellData.TerrainType.POISON_MUSHROOM, "probability": 0.04},
	]
	kobold_mine.environment_event = "earthquake"
	_templates["kobold_mine"] = kobold_mine

	# --- 牛头人石堡 ---
	var minotaur_fortress = BattleMapTemplate.new()
	minotaur_fortress.template_name = "minotaur_fortress"
	minotaur_fortress.primary_terrain = BattleCellData.TerrainType.HILLS
	minotaur_fortress.primary_weight = 0.30
	minotaur_fortress.secondary_terrains = {
		BattleCellData.TerrainType.RUINS: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.SAND: 0.15,
		BattleCellData.TerrainType.WALL: 0.10,
		BattleCellData.TerrainType.SAVANNA: 0.10,
	}
	minotaur_fortress.has_river = false
	minotaur_fortress.has_road = false
	minotaur_fortress.elevation_bias = 0.3
	minotaur_fortress.tree_cluster_count = Vector2i(0, 2)
	minotaur_fortress.tree_cluster_radius = Vector2i(1, 2)
	minotaur_fortress.dense_tree_core_chance = 0.05
	minotaur_fortress.ruin_structure_count = Vector2i(2, 4)
	minotaur_fortress.wall_segment_count = Vector2i(1, 3)
	minotaur_fortress.wall_segment_length = Vector2i(3, 6)
	minotaur_fortress.smoothing_passes = 1
	minotaur_fortress.special_features = [
		{"type": BattleCellData.TerrainType.RUINS, "probability": 0.08},
	]
	minotaur_fortress.environment_event = ""
	_templates["minotaur_fortress"] = minotaur_fortress

	# --- 暗影教团据点 ---
	var shadow_cult = BattleMapTemplate.new()
	shadow_cult.template_name = "shadow_cult_hideout"
	shadow_cult.primary_terrain = BattleCellData.TerrainType.RUINS
	shadow_cult.primary_weight = 0.30
	shadow_cult.secondary_terrains = {
		BattleCellData.TerrainType.WALL: 0.15,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.SWAMP: 0.15,
		BattleCellData.TerrainType.FOREST: 0.10,
		BattleCellData.TerrainType.POISON_MUSHROOM: 0.10,
		BattleCellData.TerrainType.DENSE_FOREST: 0.05,
	}
	shadow_cult.has_river = false
	shadow_cult.has_road = false
	shadow_cult.elevation_bias = -0.2
	shadow_cult.tree_cluster_count = Vector2i(1, 3)
	shadow_cult.tree_cluster_radius = Vector2i(2, 3)
	shadow_cult.dense_tree_core_chance = 0.20
	shadow_cult.ruin_structure_count = Vector2i(2, 3)
	shadow_cult.wall_segment_count = Vector2i(2, 4)
	shadow_cult.wall_segment_length = Vector2i(2, 5)
	shadow_cult.smoothing_passes = 2
	shadow_cult.special_features = [
		{"type": BattleCellData.TerrainType.POISON_MUSHROOM, "probability": 0.08},
	]
	shadow_cult.environment_event = "poison_fog"
	_templates["shadow_cult_hideout"] = shadow_cult

	# --- 村庄防御战 ---
	var village_def = BattleMapTemplate.new()
	village_def.template_name = "village_defense"
	village_def.primary_terrain = BattleCellData.TerrainType.GRASSLAND
	village_def.primary_weight = 0.35
	village_def.secondary_terrains = {
		BattleCellData.TerrainType.PLAINS: 0.20,
		BattleCellData.TerrainType.ROAD: 0.15,
		BattleCellData.TerrainType.RUINS: 0.10,       # 建筑物
		BattleCellData.TerrainType.SAVANNA: 0.10,
		BattleCellData.TerrainType.HILLS: 0.05,
		BattleCellData.TerrainType.FOREST: 0.05,
	}
	village_def.has_road = true
	village_def.has_river = false
	village_def.elevation_bias = 0.0
	village_def.tree_cluster_count = Vector2i(2, 4)
	village_def.tree_cluster_radius = Vector2i(2, 3)
	village_def.dense_tree_core_chance = 0.10
	village_def.ruin_structure_count = Vector2i(1, 3)
	village_def.wall_segment_count = Vector2i(1, 2)
	village_def.wall_segment_length = Vector2i(2, 4)
	village_def.smoothing_passes = 2
	village_def.special_features = [
		{"type": BattleCellData.TerrainType.LUCKY_GRASS, "probability": 0.03},
	]
	village_def.environment_event = ""
	_templates["village_defense"] = village_def

	# --- 远古遗迹探索 ---
	var ruins_exploration = BattleMapTemplate.new()
	ruins_exploration.template_name = "ruins_exploration"
	ruins_exploration.primary_terrain = BattleCellData.TerrainType.RUINS
	ruins_exploration.primary_weight = 0.35
	ruins_exploration.secondary_terrains = {
		BattleCellData.TerrainType.WALL: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.FOREST: 0.10,
		BattleCellData.TerrainType.SHALLOW_WATER: 0.05,
		BattleCellData.TerrainType.LUCKY_GRASS: 0.05,
	}
	ruins_exploration.has_river = false
	ruins_exploration.has_road = false
	ruins_exploration.elevation_bias = 0.0
	ruins_exploration.tree_cluster_count = Vector2i(0, 2)
	ruins_exploration.tree_cluster_radius = Vector2i(1, 2)
	ruins_exploration.dense_tree_core_chance = 0.10
	ruins_exploration.ruin_structure_count = Vector2i(2, 5)
	ruins_exploration.wall_segment_count = Vector2i(2, 5)
	ruins_exploration.wall_segment_length = Vector2i(3, 7)
	ruins_exploration.smoothing_passes = 1
	ruins_exploration.special_features = [
		{"type": BattleCellData.TerrainType.LUCKY_GRASS, "probability": 0.04},
		{"type": BattleCellData.TerrainType.WALL, "probability": 0.06},
	]
	ruins_exploration.environment_event = ""
	_templates["ruins_exploration"] = ruins_exploration

	# --- 魔像工坊 ---
	var golem_forge = BattleMapTemplate.new()
	golem_forge.template_name = "golem_forge"
	golem_forge.primary_terrain = BattleCellData.TerrainType.RUINS
	golem_forge.primary_weight = 0.30
	golem_forge.secondary_terrains = {
		BattleCellData.TerrainType.WALL: 0.20,
		BattleCellData.TerrainType.PLAINS: 0.15,
		BattleCellData.TerrainType.MOUNTAIN: 0.10,
		BattleCellData.TerrainType.SHALLOW_WATER: 0.10,  # 冷却池
		BattleCellData.TerrainType.HILLS: 0.10,
		BattleCellData.TerrainType.SAND: 0.05,
	}
	golem_forge.has_river = false
	golem_forge.has_road = false
	golem_forge.elevation_bias = 0.2
	golem_forge.tree_cluster_count = Vector2i(0, 0)
	golem_forge.tree_cluster_radius = Vector2i(1, 2)
	golem_forge.dense_tree_core_chance = 0.0
	golem_forge.ruin_structure_count = Vector2i(2, 4)
	golem_forge.wall_segment_count = Vector2i(2, 5)
	golem_forge.wall_segment_length = Vector2i(3, 6)
	golem_forge.smoothing_passes = 0
	golem_forge.special_features = [
		{"type": BattleCellData.TerrainType.WALL, "probability": 0.08},
		{"type": BattleCellData.TerrainType.SHALLOW_WATER, "probability": 0.06},
	]
	golem_forge.environment_event = "lava_surge"
	_templates["golem_forge"] = golem_forge


## =========================================
## 公开接口：根据上下文生成战斗地图
## =========================================

## 生成战斗地图（主入口）
## context: BattleContext 实例
## 当 context.overworld_grid != null 时，从大地图采样地形生成战术地图
## 否则回退到模板随机生成
func generate(context: BattleContext) -> BattleMapData:
	seed(context.seed)
	if context.overworld_grid != null:
		return _generate_from_overworld(context)
	else:
		return _generate_from_template_internal(context)


## =========================================
## 大地图驱动生成路径
## =========================================

## 从大地图遭遇点周围采样地形，展开为战术地图
func _generate_from_overworld(context: BattleContext) -> BattleMapData:
	var size_info = SIZE_MAP.get(context.battle_size, SIZE_MAP[BattleSize.MERCENARY])
	var map_data = BattleMapData.new()
	map_data.width = size_info["width"]
	map_data.height = size_info["height"]

	# 采样半径：雇佣兵=3, 骑士=4, 领主=5
	var sample_radius: int = 3
	match context.battle_size:
		BattleSize.KNIGHT: sample_radius = 4
		BattleSize.LORD: sample_radius = 5

	var grid: HexOverworldGrid = context.overworld_grid
	var center: Vector2i = context.encounter_coord

	# 步骤 1: 采样大地图周围地形
	var sampled_tiles: Array[HexOverworldTile] = []
	var center_tile = grid.get_tile_at_coord(center)
	if center_tile:
		sampled_tiles.append(center_tile)
	for tile in grid.get_tiles_in_range(center.x, center.y, sample_radius):
		sampled_tiles.append(tile)

	# 步骤 2: 将大地图格展开为战术格 (每个大地图格 → 2×2 战术格)
	var terrain_map: Dictionary = {}
	var elevation_map: Dictionary = {}
	var detail_noise = FastNoiseLite.new()
	detail_noise.noise_type = FastNoiseLite.TYPE_SIMPLEX
	detail_noise.seed = randi()
	detail_noise.frequency = 0.15

	# 先填充所有合法战术坐标为默认值
	for q in range(map_data.width):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_data.height - q_offset):
			var key = Vector2i(q, r)
			terrain_map[key] = BattleCellData.TerrainType.GRASSLAND
			elevation_map[key] = 1

	# 用大地图瓦片覆盖对应的战术格
	for tile in sampled_tiles:
		var battle_terrain = _map_overworld_to_battle(tile.terrain)
		var battle_elev = _map_overworld_elevation(tile.elevation)

		# 大地图坐标 × 2 = 战术地图基础坐标
		var base_q = (tile.coord.x - center.x) * 2 + map_data.width / 2
		var base_r = (tile.coord.y - center.y) * 2 + map_data.height / 2

		for dq in range(2):
			for dr in range(2):
				var tq = base_q + dq
				var tr = base_r + dr
				var key = Vector2i(tq, tr)
				if not terrain_map.has(key):
					continue

				# 微变化：用噪声对地形做轻微扰动
				var noise_val = detail_noise.get_noise_2d(float(tq) * 2.0, float(tr) * 2.0)
				var final_terrain = _terrain_micro_variation(battle_terrain, tile.moisture, noise_val)
				var final_elev = battle_elev
				if noise_val < -0.5 and final_elev > 0:
					final_elev -= 1
				elif noise_val > 0.5 and final_elev < 2:
					final_elev += 1

				# 继承道路/河流
				if tile.is_road:
					final_terrain = BattleCellData.TerrainType.ROAD
					final_elev = 1
				elif tile.is_river or tile.terrain == HexOverworldTile.TerrainType.RIVER:
					final_terrain = BattleCellData.TerrainType.SHALLOW_WATER
					final_elev = 0

				terrain_map[key] = final_terrain
				elevation_map[key] = final_elev

	# 步骤 3: 河流相邻格浅水扩散
	var shallow_type = BattleCellData.TerrainType.SHALLOW_WATER
	var keys_snapshot = terrain_map.keys().duplicate()
	for key in keys_snapshot:
		if terrain_map[key] == shallow_type:
			for dir in range(6):
				var neighbor = HexUtils.get_neighbor(key.x, key.y, dir)
				if terrain_map.has(neighbor):
					var n_type = terrain_map[neighbor]
					if n_type != shallow_type and n_type != BattleCellData.TerrainType.DEEP_WATER \
					and n_type != BattleCellData.TerrainType.ROAD and n_type != BattleCellData.TerrainType.WALL:
						if randf() < 0.20:
							terrain_map[neighbor] = shallow_type
							elevation_map[neighbor] = 0

	# 步骤 4: 按 POI 类型放置结构元素
	var poi_type = context.poi_type
	# 根据POI类型确定废墟/墙壁数量
	var ruin_count := Vector2i(0, 1)
	var wall_count := Vector2i(0, 1)
	match poi_type:
		0, 1, 2:  # TOWN, VILLAGE, CASTLE
			ruin_count = Vector2i(1, 3)
			wall_count = Vector2i(1, 2)
		3:  # SETTLEMENT (enemy camps)
			ruin_count = Vector2i(1, 3)
			wall_count = Vector2i(0, 2)
		4:  # LAIR (dragon/ancient tomb/ruins)
			ruin_count = Vector2i(2, 4)
			wall_count = Vector2i(1, 3)

	var fake_template = BattleMapTemplate.new()
	fake_template.ruin_structure_count = ruin_count
	fake_template.wall_segment_count = wall_count
	fake_template.wall_segment_length = Vector2i(2, 4)
	fake_template.smoothing_passes = 2
	_generate_ruin_structures(terrain_map, map_data.width, map_data.height, fake_template)
	_generate_wall_segments(terrain_map, map_data.width, map_data.height, fake_template)

	# 步骤 5: 特殊元素散落
	_scatter_special_features(terrain_map)

	# 步骤 6: 地形平滑
	_smooth_terrain_map(terrain_map, map_data.width, map_data.height, 2)

	# 步骤 7: 创建 BattleCellData
	_finalize_cells(map_data, terrain_map, elevation_map, BattleCellData.TerrainType.GRASSLAND)

	# 步骤 8: 环境事件（从主导地形推断）
	map_data.template_name = "overworld_" + OverworldTerrain.get_name(context.overworld_terrain)
	map_data.environment_event = _derive_environment_event(terrain_map)

	# 步骤 9: 部署区域
	var zones = DeploymentZone.generate_zones(
		map_data.width, map_data.height, context.engagement_type, map_data.cells
	)
	map_data.player_deployment = zones["player"]
	map_data.enemy_deployment = zones["enemy"]

	# 步骤 10: 连通性验证（确保玩家和敌人可互相到达）
	_ensure_connectivity(map_data, context.engagement_type)

	if context.environment_override != "":
		map_data.environment_event = context.environment_override

	return map_data


## 大地图地形类型 → 战斗地形类型映射
func _map_overworld_to_battle(ow_terrain: int) -> BattleCellData.TerrainType:
	match ow_terrain:
		HexOverworldTile.TerrainType.DEEP_WATER:    return BattleCellData.TerrainType.DEEP_WATER
		HexOverworldTile.TerrainType.SHALLOW_WATER: return BattleCellData.TerrainType.SHALLOW_WATER
		HexOverworldTile.TerrainType.SAND:          return BattleCellData.TerrainType.SAND
		HexOverworldTile.TerrainType.PLAINS:        return BattleCellData.TerrainType.PLAINS
		HexOverworldTile.TerrainType.GRASSLAND:     return BattleCellData.TerrainType.GRASSLAND
		HexOverworldTile.TerrainType.FOREST:        return BattleCellData.TerrainType.FOREST
		HexOverworldTile.TerrainType.DENSE_FOREST:  return BattleCellData.TerrainType.DENSE_FOREST
		HexOverworldTile.TerrainType.HILLS:         return BattleCellData.TerrainType.HILLS
		HexOverworldTile.TerrainType.MOUNTAIN:      return BattleCellData.TerrainType.MOUNTAIN
		HexOverworldTile.TerrainType.SNOW:          return BattleCellData.TerrainType.SNOW
		HexOverworldTile.TerrainType.SWAMP:         return BattleCellData.TerrainType.SWAMP
		HexOverworldTile.TerrainType.SAVANNA:       return BattleCellData.TerrainType.SAVANNA
		HexOverworldTile.TerrainType.ROAD:          return BattleCellData.TerrainType.ROAD
		HexOverworldTile.TerrainType.RIVER:         return BattleCellData.TerrainType.SHALLOW_WATER
		_: return BattleCellData.TerrainType.PLAINS


## 大地图连续高程 → 战术地图3级高程
func _map_overworld_elevation(elev: float) -> int:
	if elev < 0.30:
		return 0  # 低地
	elif elev > 0.65:
		return 2  # 高地
	else:
		return 1  # 平地


## 地形微变化：用噪声+湿度做轻微扰动
func _terrain_micro_variation(base: BattleCellData.TerrainType, moisture: float, noise_val: float) -> BattleCellData.TerrainType:
	if noise_val < 0.65:
		return base  # 大部分保持不变

	# ~10%概率微变
	match base:
		BattleCellData.TerrainType.GRASSLAND:
			if moisture > 0.6:
				return BattleCellData.TerrainType.FOREST
			elif noise_val > 0.85:
				return BattleCellData.TerrainType.LUCKY_GRASS
		BattleCellData.TerrainType.PLAINS:
			if moisture < 0.35:
				return BattleCellData.TerrainType.SAVANNA
		BattleCellData.TerrainType.FOREST:
			if noise_val > 0.85:
				return BattleCellData.TerrainType.DENSE_FOREST
		BattleCellData.TerrainType.SAVANNA:
			if moisture > 0.55:
				return BattleCellData.TerrainType.GRASSLAND
		BattleCellData.TerrainType.SWAMP:
			if noise_val > 0.9:
				return BattleCellData.TerrainType.POISON_MUSHROOM
		_:
			pass
	return base


## 散落特殊元素（大地图路径用）
func _scatter_special_features(terrain_map: Dictionary) -> void:
	var feature_chances: Dictionary = {
		BattleCellData.TerrainType.LUCKY_GRASS: 0.02,
		BattleCellData.TerrainType.POISON_MUSHROOM: 0.01,
	}
	for key in terrain_map:
		var current = terrain_map[key]
		if current == BattleCellData.TerrainType.GRASSLAND or \
		   current == BattleCellData.TerrainType.PLAINS or \
		   current == BattleCellData.TerrainType.SAVANNA:
			for feat_type in feature_chances:
				if randf() < feature_chances[feat_type]:
					terrain_map[key] = feat_type
					break


## 从主导地形推断环境事件
func _derive_environment_event(terrain_map: Dictionary) -> String:
	var counts: Dictionary = {}
	for key in terrain_map:
		var t = terrain_map[key]
		if not counts.has(t):
			counts[t] = 0
		counts[t] += 1

	var dominant: BattleCellData.TerrainType = BattleCellData.TerrainType.PLAINS
	var max_count: int = 0
	for t in counts:
		if counts[t] > max_count:
			max_count = counts[t]
			dominant = t

	match dominant:
		BattleCellData.TerrainType.SWAMP: return "poison_fog"
		BattleCellData.TerrainType.FOREST, BattleCellData.TerrainType.DENSE_FOREST: return "fog"
		BattleCellData.TerrainType.HILLS, BattleCellData.TerrainType.MOUNTAIN: return "earthquake"
		BattleCellData.TerrainType.SHALLOW_WATER, BattleCellData.TerrainType.SAND: return "storm"
		_: return ""


## 连通性验证 — 确保玩家和敌人部署区可互相到达
func _ensure_connectivity(map_data: BattleMapData, engagement_type: int = BattleContext.EngagementType.NORMAL) -> void:
	if map_data.player_deployment.is_empty() or map_data.enemy_deployment.is_empty():
		return

	# BFS 从玩家部署区出发，找所有可达的格子
	var reachable: Dictionary = {}
	var queue: Array[Vector2i] = [map_data.player_deployment[0]]
	reachable[queue[0]] = true

	while not queue.is_empty():
		var current = queue.pop_front()
		for dir in range(6):
			var neighbor = HexUtils.get_neighbor(current.x, current.y, dir)
			if map_data.cells.has(neighbor) and not reachable.has(neighbor):
				var cell: BattleCellData = map_data.cells[neighbor]
				if cell.is_passable:
					reachable[neighbor] = true
					queue.append(neighbor)

	# 检查敌人部署区是否可达
	var need_carve := false
	var carve_target: Vector2i = Vector2i.ZERO
	for ep in map_data.enemy_deployment:
		if not reachable.has(ep):
			need_carve = true
			carve_target = ep
			break

	if not need_carve:
		return

	# 打通一条走廊: BFS 忽略通行性，找最短路径
	var start = map_data.player_deployment[0]
	var end = carve_target
	var path = _find_path_ignoring_passability(map_data, start, end)
	for pos in path:
		if map_data.cells.has(pos):
			var cell: BattleCellData = map_data.cells[pos]
			if not cell.is_passable or cell.terrain_type == BattleCellData.TerrainType.DEEP_WATER:
				map_data.cells[pos] = BattleCellData.create_from_type(BattleCellData.TerrainType.PLAINS, 1)

	# 重新生成部署区（打通走廊后可能新增可部署格）
	var zones = DeploymentZone.generate_zones(
		map_data.width, map_data.height,
		engagement_type,
		map_data.cells
	)
	if zones["player"].size() > 0:
		map_data.player_deployment = zones["player"]
	if zones["enemy"].size() > 0:
		map_data.enemy_deployment = zones["enemy"]


## BFS 寻路（忽略通行性），用于连通性打通
func _find_path_ignoring_passability(map_data: BattleMapData, start: Vector2i, end: Vector2i) -> Array[Vector2i]:
	var visited: Dictionary = {}
	var parent: Dictionary = {}
	var queue: Array[Vector2i] = [start]
	visited[start] = true

	while not queue.is_empty():
		var current = queue.pop_front()
		if current == end:
			# 回溯路径
			var path: Array[Vector2i] = []
			var node = end
			while node != start:
				path.append(node)
				node = parent[node]
			path.reverse()
			return path

		for dir in range(6):
			var neighbor = HexUtils.get_neighbor(current.x, current.y, dir)
			if map_data.cells.has(neighbor) and not visited.has(neighbor):
				visited[neighbor] = true
				parent[neighbor] = current
				queue.append(neighbor)

	return []


## 模板随机生成路径（内部）
func _generate_from_template_internal(context: BattleContext) -> BattleMapData:
	var template_name = OverworldTerrain.get_battle_template_name(context.overworld_terrain)
	var template: BattleMapTemplate = _templates.get(template_name, _templates["plain_field"])

	var size_info = SIZE_MAP.get(context.battle_size, SIZE_MAP[BattleSize.MERCENARY])
	var map_data = BattleMapData.new()
	map_data.width = size_info["width"]
	map_data.height = size_info["height"]
	map_data.template_name = template.template_name

	var elevation_map = _generate_elevation_map(map_data.width, map_data.height, template.elevation_bias)
	var terrain_map = _generate_terrain_map(map_data.width, map_data.height, template)
	_apply_linear_features(terrain_map, elevation_map, map_data.width, map_data.height, template)
	_generate_tree_clusters(terrain_map, map_data.width, map_data.height, template)
	_generate_ruin_structures(terrain_map, map_data.width, map_data.height, template)
	_generate_wall_segments(terrain_map, map_data.width, map_data.height, template)
	_apply_special_features(terrain_map, template)
	_smooth_terrain_map(terrain_map, map_data.width, map_data.height, template.smoothing_passes)

	_finalize_cells(map_data, terrain_map, elevation_map, template.primary_terrain)

	var zones = DeploymentZone.generate_zones(
		map_data.width, map_data.height, context.engagement_type, map_data.cells
	)
	map_data.player_deployment = zones["player"]
	map_data.enemy_deployment = zones["enemy"]

	if context.environment_override != "":
		map_data.environment_event = context.environment_override
	else:
		map_data.environment_event = template.environment_event

	# 连通性验证
	_ensure_connectivity(map_data, context.engagement_type)

	return map_data


## 从 terrain_map + elevation_map 创建 BattleCellData 并写入 map_data.cells
func _finalize_cells(
	map_data: BattleMapData,
	terrain_map: Dictionary,
	elevation_map: Dictionary,
	default_terrain: BattleCellData.TerrainType
) -> void:
	for q in range(map_data.width):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_data.height - q_offset):
			var key = Vector2i(q, r)
			var terrain_type: BattleCellData.TerrainType = terrain_map.get(key, default_terrain)
			var elev: int = elevation_map.get(key, 1)
			var cell_data = BattleCellData.create_from_type(terrain_type, elev)

			if terrain_type == BattleCellData.TerrainType.DEEP_WATER:
				cell_data.elevation = 0
			elif terrain_type == BattleCellData.TerrainType.SHALLOW_WATER:
				cell_data.elevation = min(elev, 1)
			elif terrain_type == BattleCellData.TerrainType.SWAMP:
				cell_data.elevation = min(elev, 1)
			elif terrain_type == BattleCellData.TerrainType.MOUNTAIN:
				cell_data.elevation = max(elev, 2)
			elif terrain_type == BattleCellData.TerrainType.HILLS:
				cell_data.elevation = max(elev, 2) if randf() < 0.6 else max(elev, 1)

			map_data.cells[key] = cell_data


## 无上下文时直接生成（用于测试 / QuickCombatScene）
func generate_from_template(template_name: String, size: BattleSize, seed_val: int = 0) -> BattleMapData:
	var context = BattleContext.new()
	context.overworld_terrain = OverworldTerrain.Type.PLAINS
	context.battle_size = size
	context.engagement_type = BattleContext.EngagementType.NORMAL
	context.seed = seed_val if seed_val != 0 else randi()
	seed(context.seed)

	var template: BattleMapTemplate = _templates.get(template_name, _templates["plain_field"])
	var size_info = SIZE_MAP.get(size, SIZE_MAP[BattleSize.MERCENARY])

	var map_data = BattleMapData.new()
	map_data.width = size_info["width"]
	map_data.height = size_info["height"]
	map_data.template_name = template.template_name

	var elevation_map = _generate_elevation_map(map_data.width, map_data.height, template.elevation_bias)
	var terrain_map = _generate_terrain_map(map_data.width, map_data.height, template)
	_apply_linear_features(terrain_map, elevation_map, map_data.width, map_data.height, template)
	_generate_tree_clusters(terrain_map, map_data.width, map_data.height, template)
	_generate_ruin_structures(terrain_map, map_data.width, map_data.height, template)
	_generate_wall_segments(terrain_map, map_data.width, map_data.height, template)
	_apply_special_features(terrain_map, template)
	_smooth_terrain_map(terrain_map, map_data.width, map_data.height, template.smoothing_passes)

	_finalize_cells(map_data, terrain_map, elevation_map, template.primary_terrain)

	var zones = DeploymentZone.generate_zones(
		map_data.width, map_data.height, context.engagement_type, map_data.cells
	)
	map_data.player_deployment = zones["player"]
	map_data.enemy_deployment = zones["enemy"]
	map_data.environment_event = template.environment_event

	# 连通性验证
	_ensure_connectivity(map_data, context.engagement_type)

	return map_data


## =========================================
## 私有方法：生成算法步骤
## =========================================

## 步骤1：用噪声生成高程图
## 返回: { Vector2i(q, r) → int(0/1/2) }
func _generate_elevation_map(width: int, height: int, bias: float) -> Dictionary:
	var elevation_noise = FastNoiseLite.new()
	elevation_noise.noise_type = FastNoiseLite.TYPE_SIMPLEX
	elevation_noise.seed = randi()
	elevation_noise.frequency = 0.08

	var result: Dictionary = {}
	for q in range(width):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, height - q_offset):
			var n = elevation_noise.get_noise_2d(q, r) + bias
			var elev: int = 1  # 默认平地
			if n > 0.35:
				elev = 2  # 高地
			elif n < -0.35:
				elev = 0  # 低地
			result[Vector2i(q, r)] = elev
	return result


## 步骤2：根据模板权重填充地形
## 返回: { Vector2i(q, r) → TerrainType }
func _generate_terrain_map(width: int, height: int, template: BattleMapTemplate) -> Dictionary:
	var terrain_noise = FastNoiseLite.new()
	terrain_noise.noise_type = FastNoiseLite.TYPE_SIMPLEX
	terrain_noise.seed = randi()
	terrain_noise.frequency = 0.12

	# 构建累积权重表用于采样
	var total_secondary_weight: float = 0.0
	for w in template.secondary_terrains.values():
		total_secondary_weight += w

	var result: Dictionary = {}
	for q in range(width):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, height - q_offset):
			var key = Vector2i(q, r)
			var n = terrain_noise.get_noise_2d(q * 3.7, r * 3.7)  # 缩放以获得不同尺度

			# 主地形 vs 副地形的分界
			if n < (template.primary_weight * 2.0 - 1.0):
				# 使用主地形
				result[key] = template.primary_terrain
			else:
				# 按权重随机选择副地形
				var roll = randf() * total_secondary_weight
				var cumulative: float = 0.0
				var chosen = template.primary_terrain  # fallback
				for terrain_type in template.secondary_terrains:
					cumulative += template.secondary_terrains[terrain_type]
					if roll <= cumulative:
						chosen = terrain_type
						break
				result[key] = chosen

	return result


## 步骤3：应用线性特征（河流、道路）
func _apply_linear_features(
	terrain_map: Dictionary,
	elevation_map: Dictionary,
	width: int, height: int,
	template: BattleMapTemplate
) -> void:
	# 横向道路（穿越地图中央）
	if template.has_road:
		var road_r = height / 2
		for q in range(width):
			var q_offset = int(floor(q / 2.0))
			var r = road_r - q_offset
			var key = Vector2i(q, r)
			if terrain_map.has(key):
				terrain_map[key] = BattleCellData.TerrainType.ROAD
				elevation_map[key] = 1  # 道路为平地

	# 纵向河流（穿越地图中央偏侧）
	if template.has_river:
		var river_q = width / 3
		for r_offset in range(height + 2):
			var q_offset = int(floor(river_q / 2.0))
			var r = r_offset - q_offset - 1
			var key = Vector2i(river_q, r)
			if terrain_map.has(key):
				terrain_map[key] = BattleCellData.TerrainType.SHALLOW_WATER
				elevation_map[key] = 0
				# 河流相邻格标记为河流边
				for dir in range(6):
					var neighbor = HexUtils.get_neighbor(river_q, r, dir)
					if terrain_map.has(neighbor):
						var _n_data = terrain_map[neighbor]
						# 河流边的格子也可能变成浅水
						if randf() < 0.15:
							terrain_map[neighbor] = BattleCellData.TerrainType.SHALLOW_WATER
							elevation_map[neighbor] = 0


## 步骤7：放置散落特殊元素
func _apply_special_features(terrain_map: Dictionary, template: BattleMapTemplate) -> void:
	for feature in template.special_features:
		var feature_type: BattleCellData.TerrainType = feature["type"]
		var probability: float = feature["probability"]

		for key in terrain_map:
			# 只在普通地形上放置特殊元素
			var current = terrain_map[key]
			if current == BattleCellData.TerrainType.GRASSLAND or \
			   current == BattleCellData.TerrainType.PLAINS or \
			   current == BattleCellData.TerrainType.SAVANNA:
				if randf() < probability:
					terrain_map[key] = feature_type


## =========================================
## 新增：地形聚集生成算法
## =========================================

## 步骤4：生成树木聚集区域（自然聚落感）
## 用 BFS 扩展方式生成聚簇，中心密集、边缘稀疏
func _generate_tree_clusters(
	terrain_map: Dictionary,
	width: int, height: int,
	template: BattleMapTemplate
) -> void:
	var forest_type = BattleCellData.TerrainType.FOREST
	var dense_type = BattleCellData.TerrainType.DENSE_FOREST

	# 收集所有可用于种树的格子
	var plantable: Array[Vector2i] = []
	for key in terrain_map:
		var t: BattleCellData.TerrainType = terrain_map[key]
		if t == BattleCellData.TerrainType.PLAINS or \
		   t == BattleCellData.TerrainType.GRASSLAND or \
		   t == BattleCellData.TerrainType.SAVANNA:
			plantable.append(key)

	if plantable.is_empty():
		return

	var cluster_count = randi_range(template.tree_cluster_count.x, template.tree_cluster_count.y)
	var used_cells: Dictionary = {}

	for _i in range(cluster_count):
		# 随机选一个种子点
		var seed_cell = plantable[randi() % plantable.size()]
		if used_cells.has(seed_cell):
			continue

		var cluster_radius = randi_range(template.tree_cluster_radius.x, template.tree_cluster_radius.y)
		# BFS 扩展树木簇
		var queue: Array[Vector2i] = [seed_cell]
		var visited: Dictionary = {seed_cell: true}
		var placed_in_cluster: int = 0
		var max_cells = int(cluster_radius * cluster_radius * 2.0)

		while not queue.is_empty() and placed_in_cluster < max_cells:
			var current = queue.pop_front()

			# 无论当前格是否放置，都先扩展邻居（保证BFS能到达远处有效格子）
			for dir in range(6):
				var neighbor = HexUtils.get_neighbor(current.x, current.y, dir)
				if not visited.has(neighbor) and terrain_map.has(neighbor):
					visited[neighbor] = true
					queue.append(neighbor)

			if used_cells.has(current) or current not in terrain_map:
				continue

			var current_terrain = terrain_map[current]
			if current_terrain != BattleCellData.TerrainType.PLAINS and \
			   current_terrain != BattleCellData.TerrainType.GRASSLAND and \
			   current_terrain != BattleCellData.TerrainType.SAVANNA:
				continue

			var dist = HexUtils.distance(seed_cell.x, seed_cell.y, current.x, current.y)
			if dist > cluster_radius:
				continue

			# 距离越远放置概率越低（中心密集、边缘稀疏）
			var place_chance = 1.0 - (float(dist) / float(cluster_radius + 1)) * 0.6
			if randf() < place_chance:
				# 核芯区域有一定概率生成密林
				if dist <= 1 and randf() < template.dense_tree_core_chance:
					terrain_map[current] = dense_type
				else:
					terrain_map[current] = forest_type
				used_cells[current] = true
				placed_in_cluster += 1


## 步骤5：生成废墟建筑结构
## 采用 L形、矩形、十字形 等布局模式生成废墟群落
func _generate_ruin_structures(
	terrain_map: Dictionary,
	width: int, height: int,
	template: BattleMapTemplate
) -> void:
	var ruin_type = BattleCellData.TerrainType.RUINS
	var wall_type = BattleCellData.TerrainType.WALL

	# 收集可用于放置废墟的格子
	var buildable: Array[Vector2i] = []
	for key in terrain_map:
		var t = terrain_map[key]
		if t == BattleCellData.TerrainType.PLAINS or \
		   t == BattleCellData.TerrainType.GRASSLAND or \
		   t == BattleCellData.TerrainType.HILLS or \
		   t == BattleCellData.TerrainType.SAND:
			buildable.append(key)

	if buildable.is_empty():
		return

	var structure_count = randi_range(template.ruin_structure_count.x, template.ruin_structure_count.y)
	var used_cells: Dictionary = {}

	# 六边形轴向坐标的建筑布局模式
	# (1,0)=东, (0,1)=东南, (-1,1)=西南 — 六边形自然方向
	var patterns: Array = [
		# 一字型沿方向0（东）: 3格
		[
			Vector2i(0,0), Vector2i(1,0), Vector2i(2,0)
		],
		# L形: 方向0两步 + 方向5(东南)一步
		[
			Vector2i(0,0), Vector2i(1,0), Vector2i(1,1)
		],
		# 2x2蜂巢: 六边形自然填充
		[
			Vector2i(0,0), Vector2i(1,0), Vector2i(0,1), Vector2i(1,-1)
		],
		# T形: 方向0一步 + 方向3(西)一步 + 方向5(东南)一步
		[
			Vector2i(0,0), Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1)
		],
		# 十字形: 四个交替方向
		[
			Vector2i(0,0), Vector2i(1,0), Vector2i(-1,0), Vector2i(0,-1), Vector2i(0,1)
		],
	]

	for _i in range(structure_count):
		var anchor = buildable[randi() % buildable.size()]
		if used_cells.has(anchor):
			continue

		var pattern = patterns[randi() % patterns.size()]
		var valid_placement = true

		# 检查所有偏移位置是否有效
		var cells_to_place: Array[Vector2i] = []
		for offset in pattern:
			var cell = Vector2i(anchor.x + offset.x, anchor.y + offset.y)
			if not terrain_map.has(cell):
				valid_placement = false
				break
			var t = terrain_map[cell]
			# 不覆盖水域、山地、墙壁
			if t == BattleCellData.TerrainType.DEEP_WATER or \
			   t == BattleCellData.TerrainType.SHALLOW_WATER or \
			   t == BattleCellData.TerrainType.MOUNTAIN or \
			   t == BattleCellData.TerrainType.WALL:
				valid_placement = false
				break
			if used_cells.has(cell):
				valid_placement = false
				break
			cells_to_place.append(cell)

		if not valid_placement:
			continue

		# 放置废墟，核心格是废墟，外围30%概率变为墙壁
		for idx in range(cells_to_place.size()):
			var cell = cells_to_place[idx]
			if idx == 0:
				# 核心格：永远是废墟
				terrain_map[cell] = ruin_type
				used_cells[cell] = true
			else:
				# 外围格：70%废墟，30%断壁残垣（墙壁）
				if randf() < 0.3:
					terrain_map[cell] = wall_type
				else:
					terrain_map[cell] = ruin_type
				used_cells[cell] = true




## 步骤6：生成独立墙壁/断壁残垣段
## 沿随机方向生成线段型墙壁结构，模拟坍塌的城墙、围栏等
func _generate_wall_segments(
	terrain_map: Dictionary,
	width: int, height: int,
	template: BattleMapTemplate
) -> void:
	var wall_type = BattleCellData.TerrainType.WALL
	var ruin_type = BattleCellData.TerrainType.RUINS

	var segment_count = randi_range(template.wall_segment_count.x, template.wall_segment_count.y)

	# 收集可放置墙壁的格子
	var placeable: Array[Vector2i] = []
	for key in terrain_map:
		var t = terrain_map[key]
		if t == BattleCellData.TerrainType.PLAINS or \
		   t == BattleCellData.TerrainType.GRASSLAND or \
		   t == BattleCellData.TerrainType.HILLS:
			placeable.append(key)

	if placeable.is_empty():
		return

	var used_cells: Dictionary = {}

	for _i in range(segment_count):
		var start_cell = placeable[randi() % placeable.size()]
		if used_cells.has(start_cell):
			continue

		var segment_length = randi_range(template.wall_segment_length.x, template.wall_segment_length.y)
		# 随机选择一个起始方向（0-5）
		var direction = randi() % 6
		var current_cell = start_cell

		for step in range(segment_length):
			if not terrain_map.has(current_cell):
				break

			var t = terrain_map[current_cell]
			# 不覆盖已有结构
			if t == BattleCellData.TerrainType.WALL or \
			   t == BattleCellData.TerrainType.RUINS or \
			   t == BattleCellData.TerrainType.DEEP_WATER or \
			   t == BattleCellData.TerrainType.SHALLOW_WATER or \
			   t == BattleCellData.TerrainType.MOUNTAIN or \
			   t == BattleCellData.TerrainType.FOREST or \
			   t == BattleCellData.TerrainType.DENSE_FOREST:
				break

			if used_cells.has(current_cell):
				break

			# 越靠近末端，坍塌概率越高（模拟断壁残垣渐变效果）
			var collapse_chance = float(step) / float(segment_length) * 0.5
			if randf() < collapse_chance:
				terrain_map[current_cell] = ruin_type
			else:
				terrain_map[current_cell] = wall_type
			used_cells[current_cell] = true

			# 向当前方向前进
			current_cell = HexUtils.get_neighbor(current_cell.x, current_cell.y, direction)

			# 每步有20%概率改变方向（模拟自然弯曲的墙壁）
			if randf() < 0.20:
				direction = (direction + (1 if randf() < 0.5 else -1)) % 6
				if direction < 0:
					direction += 6


## 步骤8：地形过渡平滑
## 减少尖锐的地形边界，使过渡更自然
## 核心逻辑：如果一个格子的邻居中有超过半数是另一种地形，
## 且该地形与本格不同，则有一定概率变为邻居的地形
func _smooth_terrain_map(
	terrain_map: Dictionary,
	width: int, height: int,
	passes: int
) -> void:
	# 不可被平滑吞没的地形（稀有特征保留）
	var immune_types: Dictionary = {
		BattleCellData.TerrainType.WALL: true,
		BattleCellData.TerrainType.RUINS: true,
		BattleCellData.TerrainType.DEEP_WATER: true,
		BattleCellData.TerrainType.SHALLOW_WATER: true,
		BattleCellData.TerrainType.POISON_MUSHROOM: true,
		BattleCellData.TerrainType.LUCKY_GRASS: true,
	}

	for _pass in range(passes):
		var changes: Dictionary = {}

		for key in terrain_map:
			var current_type = terrain_map[key]

			# 跳过不可平滑的地形
			if immune_types.has(current_type):
				continue

			# 统计6个邻居的地形类型
			var neighbor_counts: Dictionary = {}
			var total_neighbors: int = 0

			for dir in range(6):
				var neighbor = HexUtils.get_neighbor(key.x, key.y, dir)
				if terrain_map.has(neighbor):
					var n_type = terrain_map[neighbor]
					if not neighbor_counts.has(n_type):
						neighbor_counts[n_type] = 0
					neighbor_counts[n_type] += 1
					total_neighbors += 1

			if total_neighbors == 0:
				continue



			# 如果超过3个邻居是同一种地形，且不同于当前格，则考虑平滑
			for n_type in neighbor_counts:
				if neighbor_counts[n_type] >= 3 and n_type != current_type:
					# 50%概率平滑（保留一些边界特征）
					if randf() < 0.5:
						changes[key] = n_type
						break

		# 应用变化
		for key in changes:
			terrain_map[key] = changes[key]


## 获取所有可用模板名
func get_template_names() -> Array:
	return _templates.keys()


## 获取指定模板
func get_template(name: String) -> BattleMapTemplate:
	return _templates.get(name)