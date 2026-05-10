# SpellShapeResolver.gd
# 法术范围形状解析器 — 在六边形网格上计算各种法术范围形状
# 对应策划案 07-法术系统 → 六、范围形状定义
class_name SpellShapeResolver


## 获取指定形状范围内的所有格子坐标
## shape: SpellData.SpellShape 枚举值
## origin: 目标格子（或施法者位置，取决于形状类型）
## caster_pos: 施法者位置
## size: 范围大小（半径/长度/锥形长度等）
## grid: HexGrid引用（用于检查LOS和边界）
static func get_cells_in_shape(shape: int, origin: Vector2i, caster_pos: Vector2i, size: int, grid: HexGrid) -> Array[Vector2i]:
	match shape:
		0:  # SINGLE
			return _shape_single(origin, grid)
		1:  # RAY
			return _shape_ray(caster_pos, origin, size, grid)
		2:  # CONE
			return _shape_cone(caster_pos, origin, size, grid)
		3:  # SPHERE
			return _shape_sphere(origin, size, grid)
		4:  # LINE
			return _shape_line(caster_pos, origin, size, grid)
		5:  # CROSS
			return _shape_cross(origin, size, grid)
		6:  # SELF
			return _shape_self(caster_pos, size, grid)
		7:  # TOUCH
			return _shape_touch(caster_pos, grid)
		_:
			return [origin]


# ============================================================================
# 形状实现
# ============================================================================

## 单体 — 1个目标格子
static func _shape_single(target: Vector2i, grid: HexGrid) -> Array[Vector2i]:
	if grid.cells.has(target):
		return [target]
	return []

## 射线 — 从施法者出发经过目标的直线，N格长
static func _shape_ray(caster: Vector2i, target: Vector2i, length: int, grid: HexGrid) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	var direction = _get_hex_direction(caster, target)

	var current = caster
	for i in range(length):
		current = HexUtils.get_neighbor(current.x, current.y, direction)
		if grid.cells.has(current):
			cells.append(current)
		else:
			break  # 出界停止

	return cells

## 锥形 — 施法者面前120°扇形，N格长
static func _shape_cone(caster: Vector2i, target: Vector2i, length: int, grid: HexGrid) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	var main_dir = _get_hex_direction(caster, target)

	# 锥形覆盖主方向及其左右各1个方向（3个方向共120°）
	var dirs = [
		main_dir,
		(main_dir + 1) % 6,
		(main_dir + 5) % 6,  # -1 mod 6
	]

	# 在每个方向上扩展length格
	for dir in dirs:
		var current = caster
		for i in range(length):
			current = HexUtils.get_neighbor(current.x, current.y, dir)
			if grid.cells.has(current):
				cells.append(current)

	return cells

## 球形 — 以目标格为中心，N格半径
## 对应策案：半径1=7格, 半径2=19格, 半径3=37格
static func _shape_sphere(center: Vector2i, radius: int, grid: HexGrid) -> Array[Vector2i]:
	var cells: Array[Vector2i] = [center]

	var visited: Array = [center]
	var fringes: Array = [[center]]

	for k in range(1, radius + 1):
		fringes.append([])
		for hex in fringes[k - 1]:
			for dir in range(6):
				var neighbor = HexUtils.get_neighbor(hex.x, hex.y, dir)
				if grid.cells.has(neighbor) and not visited.has(neighbor):
					visited.append(neighbor)
					cells.append(neighbor)
					fringes[k].append(neighbor)

	return cells

## 线形 — 两点之间的直线，N格长
static func _shape_line(from: Vector2i, to: Vector2i, max_length: int, grid: HexGrid) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	var hex_line = _get_hex_line(from, to)

	var count = 0
	for cell in hex_line:
		if cell == from:
			continue  # 跳过起点
		if grid.cells.has(cell):
			cells.append(cell)
			count += 1
			if count >= max_length:
				break
		else:
			break

	return cells

## 十字 — 以目标格为中心的十字形，N格长
static func _shape_cross(center: Vector2i, length: int, grid: HexGrid) -> Array[Vector2i]:
	var cells: Array[Vector2i] = [center]

	for dir in range(6):
		var current = center
		for i in range(length):
			current = HexUtils.get_neighbor(current.x, current.y, dir)
			if grid.cells.has(current):
				cells.append(current)

	return cells

## 自身 — 以施法者为中心
static func _shape_self(caster: Vector2i, radius: int, grid: HexGrid) -> Array[Vector2i]:
	if radius <= 0:
		return [caster]
	return _shape_sphere(caster, radius, grid)

## 触碰 — 近战范围内的1个目标
static func _shape_touch(caster: Vector2i, grid: HexGrid) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for dir in range(6):
		var neighbor = HexUtils.get_neighbor(caster.x, caster.y, dir)
		if grid.cells.has(neighbor):
			cells.append(neighbor)
	return cells


# ============================================================================
# 辅助方法
# ============================================================================

## 获取从一个六角格指向另一个六角格的方向（0-5）
static func _get_hex_direction(from: Vector2i, to: Vector2i) -> int:
	var _diff = to - from
	var best_dir = 0
	var best_dist = 999999

	for dir in range(6):
		var offset = HexUtils.DIRECTIONS[dir]
		var candidate = from + offset
		var dist = HexUtils.distance(candidate.x, candidate.y, to.x, to.y)
		if dist < best_dist:
			best_dist = dist
			best_dir = dir

	return best_dir

## 六边形网格上的直线算法（简易Bresenham）
static func _get_hex_line(from: Vector2i, to: Vector2i) -> Array[Vector2i]:
	var line: Array[Vector2i] = [from]
	var dist = HexUtils.distance(from.x, from.y, to.x, to.y)

	if dist == 0:
		return line

	var current = from
	for i in range(dist):
		var dir = _get_hex_direction(current, to)
		current = HexUtils.get_neighbor(current.x, current.y, dir)
		line.append(current)

	return line
