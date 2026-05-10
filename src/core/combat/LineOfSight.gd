# LineOfSight.gd
# 视线系统 — 六边形网格上的视线检查、掩体判定
# 对应策划案 03-战术战斗系统 → 三、视线与掩护
class_name LineOfSight


# ============================================================================
# 视线检查
# ============================================================================

## 检查两点之间是否有视线
## 规则：森林/密林/山地/墙壁/废墟阻挡穿越视线
## 单位不阻挡视线
## 高处单位可越过低处障碍物
static func has_los(from: Vector2i, to: Vector2i, grid: HexGrid) -> bool:
	if from == to:
		return true

	var line = _get_hex_line(from, to)
	var from_cell = grid.get_cell(from.x, from.y)
	var from_elev = from_cell.elevation if from_cell else 1

	# 跳过起点和终点
	for i in range(1, line.size() - 1):
		var cell_pos = line[i]
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if not cell:
			continue

		# 高处可越过低矮障碍
		if _can_see_over(cell, from_elev):
			continue

		# 检查是否阻挡视线
		if cell.data and cell.data.blocks_line_of_sight:
			return false
		if cell.cover_type >= 2:
			# 全掩体阻挡视线
			return false

	return true

## 检查目标是否受掩体保护
## 返回掩体等级（0=无, 1=半掩体, 2=全掩体）
static func get_cover_level(target_pos: Vector2i, _attacker_pos: Vector2i, grid: HexGrid):
	var target_cell = grid.get_cell(target_pos.x, target_pos.y)
	if not target_cell or not target_cell.data:
		return 0

	# 检查目标自身所在地的掩体
	var cover = target_cell.data.cover_level

	# 如果目标在防御模式，额外获得半掩体效果
	# （由调用方处理）

	return cover

## 检查是否可以过肩射击（越过1排友军射击）
static func can_over_shoulder(caster_pos: Vector2i, target_pos: Vector2i, grid: HexGrid, ally_units: Array[Unit]) -> bool:
	var line = _get_hex_line(caster_pos, target_pos)

	# 计算路径上的友军数量
	var ally_count = 0
	for i in range(1, line.size() - 1):
		var cell_pos = line[i]
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if cell and cell.occupant:
			if ally_units.has(cell.occupant):
				ally_count += 1

	# 最多越1排友军
	return ally_count == 1


# ============================================================================
# 视野范围
# ============================================================================

## 获取单位的视野范围
## 默认6格，丘陵/山地+2，森林-2
static func get_vision_range(unit: Unit, grid: HexGrid) -> int:
	var base_vision = 6

	var cell = grid.get_cell(unit.grid_pos.x, unit.grid_pos.y)
	if cell and cell.data:
		match cell.data.terrain_type:
			BattleCellData.TerrainType.HILLS, BattleCellData.TerrainType.MOUNTAIN:
				base_vision += 2
			BattleCellData.TerrainType.FOREST, BattleCellData.TerrainType.DENSE_FOREST:
				base_vision -= 2

	# 夜间-2（预留）
	# if is_night:
	#     base_vision -= 2

	# 黑暗视觉（精灵等种族不减少夜间视野）
	if unit.data.race and "dark_vision" in unit.data.race.racial_traits:
		base_vision = max(base_vision, 6)

	return maxi(2, base_vision)


# ============================================================================
# 地形战斗效果
# ============================================================================

## 获取高地优势加成
## 对应策划案 03 → 高程系统
static func get_high_ground_bonus(attacker_pos: Vector2i, defender_pos: Vector2i, grid: HexGrid) -> Dictionary:
	var attacker_cell = grid.get_cell(attacker_pos.x, attacker_pos.y)
	var defender_cell = grid.get_cell(defender_pos.x, defender_pos.y)

	if not attacker_cell or not defender_cell:
		return {"advantage": false, "disadvantage": false, "range_bonus": 0}

	var atk_elev = attacker_cell.elevation
	var def_elev = defender_cell.elevation

	if atk_elev > def_elev + 1:
		# 高地 → 低地
		return {"advantage": true, "disadvantage": false, "range_bonus": 2}
	elif atk_elev > def_elev:
		# 高地 → 平地
		return {"advantage": true, "disadvantage": false, "range_bonus": 1}
	elif atk_elev < def_elev:
		# 低地 → 高地
		return {"advantage": false, "disadvantage": true, "range_bonus": 0}
	else:
		return {"advantage": false, "disadvantage": false, "range_bonus": 0}

## 检查渡河攻击惩罚
static func has_river_crossing_penalty(attacker_pos: Vector2i, defender_pos: Vector2i, grid: HexGrid) -> bool:
	var line = _get_hex_line(attacker_pos, defender_pos)
	for cell_pos in line:
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if cell and cell.data:
			if cell.data.is_river or cell.data.terrain_type == BattleCellData.TerrainType.SHALLOW_WATER:
				return true
	return false


# ============================================================================
# 内部方法
# ============================================================================

## 六边形直线（Bresenham简化版）
static func _get_hex_line(from: Vector2i, to: Vector2i) -> Array[Vector2i]:
	var line: Array[Vector2i] = [from]
	var dist = HexUtils.distance(from.x, from.y, to.x, to.y)
	if dist == 0:
		return line

	var current = from
	for i in range(dist):
		var dir = _nearest_direction(current, to)
		current = HexUtils.get_neighbor(current.x, current.y, dir)
		line.append(current)

	return line

## 找到从from指向to最近的方向
static func _nearest_direction(from: Vector2i, to: Vector2i) -> int:
	var best_dir = 0
	var best_dist = 999999
	for dir in range(6):
		var candidate = HexUtils.get_neighbor(from.x, from.y, dir)
		var dist = HexUtils.distance(candidate.x, candidate.y, to.x, to.y)
		if dist < best_dist:
			best_dist = dist
			best_dir = dir
	return best_dir

## 高处能否越过障碍物
static func _can_see_over(cell: HexCell, observerelevation: int) -> bool:
	if not cell:
		return false
	return observerelevation > cell.elevation + 1
