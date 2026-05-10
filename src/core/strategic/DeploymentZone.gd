# DeploymentZone.gd
# 部署区域生成器 — 根据战斗规模和交战类型生成双方部署坐标
# 对应策划案 03-战术战斗系统 → 八、视野与战争迷雾(伏击部署)
class_name DeploymentZone
extends RefCounted


## 根据战斗规模和交战类型生成部署区域
## map_width/map_height: 战场尺寸
## engagement: BattleContext.EngagementType
## cells: 地图格子数据 (Vector2i → BattleCellData)，用于筛选可通行格子
## 返回: { "player": Array[Vector2i], "enemy": Array[Vector2i] }
static func generate_zones(
	map_width: int,
	map_height: int,
	engagement: int,  # BattleContext.EngagementType
	cells: Dictionary
) -> Dictionary:
	match engagement:
		0:  # BattleContext.EngagementType.NORMAL
			return _generate_normal(map_width, map_height, cells)
		1:  # BattleContext.EngagementType.AMBUSH (玩家伏击敌人)
			return _generate_ambush(map_width, map_height, cells)
		2:  # BattleContext.EngagementType.AMBUSHED (玩家被伏击)
			return _generate_ambushed(map_width, map_height, cells)
		_:
			return _generate_normal(map_width, map_height, cells)


## 正常遭遇：玩家在左2行，敌人在右2行
static func _generate_normal(map_width: int, map_height: int, cells: Dictionary) -> Dictionary:
	var player: Array[Vector2i] = []
	var enemy: Array[Vector2i] = []

	# 玩家部署区：q=0 和 q=1 的可通行格子
	for q in range(2):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_height - q_offset):
			var key = Vector2i(q, r)
			if _is_deployable(cells, key):
				player.append(key)

	# 敌方部署区：q=width-2 和 q=width-1 的可通行格子
	for q in range(map_width - 2, map_width):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_height - q_offset):
			var key = Vector2i(q, r)
			if _is_deployable(cells, key):
				enemy.append(key)

	return {"player": player, "enemy": enemy}


## 玩家伏击敌人：玩家分散在有利位置，敌人集中在一侧
## 模拟策划案中的伏击场景：伏击方分散部署已占据有利位置
static func _generate_ambush(map_width: int, map_height: int, cells: Dictionary) -> Dictionary:
	var player: Array[Vector2i] = []
	var enemy: Array[Vector2i] = []

	# 敌方被伏击：集中部署在左侧狭小区域（q=1-3），阵型混乱
	for q in range(1, 4):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_height - q_offset):
			var key = Vector2i(q, r)
			if _is_deployable(cells, key):
				enemy.append(key)

	# 玩家伏击方：分散在地图中间和右侧的有利位置
	var mid_q = map_width / 2
	for q in range(mid_q - 1, map_width - 1):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_height - q_offset):
			var key = Vector2i(q, r)
			if _is_deployable(cells, key):
				# 优先选择高程较高（有利地形）的位置
				var cell_data: BattleCellData = cells[key]
				if cell_data.elevation >= 1 and not cell_data.blocks_line_of_sight:
					player.append(key)

	# 如果有利位置不够，补充所有可通行位置
	if player.size() < 4:
		for q in range(mid_q - 1, map_width - 1):
			var q_offset = int(floor(q / 2.0))
			for r in range(-q_offset, map_height - q_offset):
				var key = Vector2i(q, r)
				if _is_deployable(cells, key) and not player.has(key):
						player.append(key)

	return {"player": player, "enemy": enemy}


## 玩家被伏击：玩家集中在一侧阵型混乱，敌人分散在有利位置
## 被伏击方首回合 AC-2（措手不及），由 CombatManager 处理
static func _generate_ambushed(map_width: int, map_height: int, cells: Dictionary) -> Dictionary:
	var player: Array[Vector2i] = []
	var enemy: Array[Vector2i] = []

	# 玩家被伏击：集中部署在左侧狭小区域（q=0-2），阵型混乱
	for q in range(3):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_height - q_offset):
			var key = Vector2i(q, r)
			if _is_deployable(cells, key):
				player.append(key)

	# 敌方伏击方：分散在地图中间和右侧的有利位置
	var mid_q = map_width / 2
	for q in range(mid_q - 1, map_width):
		var q_offset = int(floor(q / 2.0))
		for r in range(-q_offset, map_height - q_offset):
			var key = Vector2i(q, r)
			if _is_deployable(cells, key):
				var cell_data: BattleCellData = cells[key]
				if cell_data.elevation >= 1 and not cell_data.blocks_line_of_sight:
					enemy.append(key)

	# 如果有利位置不够，补充所有可通行位置
	if enemy.size() < 4:
		for q in range(mid_q - 1, map_width):
			var q_offset = int(floor(q / 2.0))
			for r in range(-q_offset, map_height - q_offset):
				var key = Vector2i(q, r)
				if _is_deployable(cells, key) and not enemy.has(key):
					enemy.append(key)

	return {"player": player, "enemy": enemy}


## 判断一个格子是否适合部署单位
static func _is_deployable(cells: Dictionary, key: Vector2i) -> bool:
	if not cells.has(key):
		return false
	var cell_data: BattleCellData = cells[key]
	if not cell_data.is_passable:
		return false
	# 深水不可部署
	if cell_data.terrain_type == BattleCellData.TerrainType.DEEP_WATER:
		return false
	# 墙壁不可部署
	if cell_data.terrain_type == BattleCellData.TerrainType.WALL:
		return false
	return true
