# HexOverworldAStar.gd
# 六边形大地图A*寻路系统 — 同时服务于:
#   1. 玩家/实体在大地图上的移动寻路
#   2. 生成器中道路/河流的自动寻路生成
#
# 存储用 Axial, 内部启发函数/距离通过 Cube 计算
# 支持地形移动消耗权重, 可配置是否穿越不可通行地形
class_name HexOverworldAStar
extends RefCounted


## ========================================
## 配置
## ========================================

## 引用的网格数据 (不拥有)
var grid: HexOverworldGrid = null

## 是否允许穿越不可通行地形 (用于生成阶段的河流/道路寻路)
var ignore_passability: bool = false

## 不可通行地形的惩罚权重 (当 ignore_passability=true 时使用)
var impassable_penalty: float = 20.0


## ========================================
## 构造
## ========================================

func _init(pgrid: HexOverworldGrid = null) -> void:
	grid = pgrid


## ========================================
## A* 寻路 — 主接口
## ========================================

## 在六边形网格上执行A*寻路
## start/target: Vector2i 轴向坐标
## 返回: 轴向坐标路径数组 (含起点和终点), 不可达返回空
func find_path(start: Vector2i, target: Vector2i) -> Array[Vector2i]:
	if not grid:
		return []
	if not grid.has_tile(start.x, start.y) or not grid.has_tile(target.x, target.y):
		return []

	# 开放列表: 用数组模拟, 每次 pop 最低 f_score
	var open_set: Array[Vector2i] = [start]
	var in_open: Dictionary = {start: true}

	var came_from: Dictionary = {}      # Vector2i → Vector2i
	var g_score: Dictionary = {}        # Vector2i → float
	var f_score: Dictionary = {}        # Vector2i → float

	g_score[start] = 0.0
	f_score[start] = _heuristic(start, target)

	var max_iterations := grid.tile_count() + 1
	var iteration := 0

	while not open_set.is_empty() and iteration < max_iterations:
		iteration += 1

		# 找 f_score 最小的节点
		var best_idx := 0
		var best_f: float = f_score.get(open_set[0], 999999.0)
		for i in range(1, open_set.size()):
			var f: float = f_score.get(open_set[i], 999999.0)
			if f < best_f:
				best_f = f
				best_idx = i

		var current := open_set[best_idx]
		open_set.remove_at(best_idx)
		in_open.erase(current)

		# 到达目标
		if current == target:
			return _reconstruct_path(came_from, current)

		# 扩展6个邻居
		for dir in range(6):
			var neighbor := HexOverworldTile.get_neighbor(current.x, current.y, dir)
			if not grid.has_tile(neighbor.x, neighbor.y):
				continue

			var n_tile := grid.get_tile(neighbor.x, neighbor.y)
			if not n_tile:
				continue

			# 通行性检查
			var cost := _get_move_cost(n_tile)
			if cost < 0.0:
				continue  # 完全不可通行

			var tentative_g: float = g_score.get(current, 999999.0) + cost

			if tentative_g < g_score.get(neighbor, 999999.0):
				came_from[neighbor] = current
				g_score[neighbor] = tentative_g
				f_score[neighbor] = tentative_g + _heuristic(neighbor, target)

				if not in_open.has(neighbor):
					open_set.append(neighbor)
					in_open[neighbor] = true

	return []  # 不可达


## 寻路到像素坐标 (兼容 OverworldMap 接口)
## 返回像素坐标路径
func find_path_pixels(start_px: Vector2, target_px: Vector2) -> Array[Vector2]:
	var start_tile := grid.get_tile_at_pixel(start_px.x, start_px.y)
	var target_tile := grid.get_tile_at_pixel(target_px.x, target_px.y)

	if not start_tile or not target_tile:
		return []

	# 如果目标不可通行, 找最近的可达瓦片
	var actual_target := target_tile
	if not actual_target.is_passable and not ignore_passability:
		actual_target = _find_nearest_passable(target_tile)
		if not actual_target:
			return []

	var start_coord := start_tile.coord
	var target_coord := actual_target.coord

	var hex_path := find_path(start_coord, target_coord)
	var px_path: Array[Vector2] = []
	for coord in hex_path:
		var tile := grid.get_tile(coord.x, coord.y)
		if tile:
			px_path.append(tile.pixel_pos)

	return px_path


## 寻路并返回道路需要标记的方向信息 (用于生成器)
## 返回: Array[Dictionary], 每项包含 {coord, direction_to_next}
func find_path_with_directions(start: Vector2i, target: Vector2i) -> Array[Dictionary]:
	var path := find_path(start, target)
	if path.is_empty():
		return []

	var result: Array[Dictionary] = []
	for i in range(path.size()):
		var entry: Dictionary = {"coord": path[i], "direction_to_next": -1}
		if i < path.size() - 1:
			var current := path[i]
			var next := path[i + 1]
			entry.direction_to_next = _get_direction(current, next)
		result.append(entry)

	return result


## ========================================
## 最低成本路径 (用于河流生成 — 沿低地流动)
## ========================================

## 找到从 start 到 target 的最低高程路径
## 用于河流: 优先走低处, 避免攀山
func find_lowest_elevation_path(start: Vector2i, target: Vector2i) -> Array[Vector2i]:
	if not grid:
		return []
	if not grid.has_tile(start.x, start.y) or not grid.has_tile(target.x, target.y):
		return []

	var open_set: Array[Vector2i] = [start]
	var in_open: Dictionary = {start: true}
	var came_from: Dictionary = {}
	var g_score: Dictionary = {}
	var f_score: Dictionary = {}

	g_score[start] = 0.0
	f_score[start] = _heuristic(start, target)

	var max_iterations := grid.tile_count() + 1
	var iteration := 0

	while not open_set.is_empty() and iteration < max_iterations:
		iteration += 1

		var best_idx := 0
		var best_f: float = f_score.get(open_set[0], 999999.0)
		for i in range(1, open_set.size()):
			var f: float = f_score.get(open_set[i], 999999.0)
			if f < best_f:
				best_f = f
				best_idx = i

		var current := open_set[best_idx]
		open_set.remove_at(best_idx)
		in_open.erase(current)

		if current == target:
			return _reconstruct_path(came_from, current)

		for dir in range(6):
			var neighbor := HexOverworldTile.get_neighbor(current.x, current.y, dir)
			if not grid.has_tile(neighbor.x, neighbor.y):
				continue

			var n_tile := grid.get_tile(neighbor.x, neighbor.y)
			if not n_tile:
				continue

			# 河流路径成本: 优先走低地, 攀高惩罚大
			var elev_cost: float = n_tile.elevation * 10.0
			# 水域降低成本 (河流喜欢经过已有的水)
			if n_tile.terrain == HexOverworldTile.TerrainType.DEEP_WATER or \
			   n_tile.terrain == HexOverworldTile.TerrainType.SHALLOW_WATER:
				elev_cost *= 0.3

			# 距离成本
			var dist_cost: float = 1.0
			# 避免极端地形
			if n_tile.terrain == HexOverworldTile.TerrainType.MOUNTAIN:
				elev_cost += 30.0

			var tentative_g: float = g_score.get(current, 999999.0) + elev_cost + dist_cost

			if tentative_g < g_score.get(neighbor, 999999.0):
				came_from[neighbor] = current
				g_score[neighbor] = tentative_g
				f_score[neighbor] = tentative_g + _heuristic(neighbor, target)

				if not in_open.has(neighbor):
					open_set.append(neighbor)
					in_open[neighbor] = true

	return []


## ========================================
## Dijkstra — 用于道路生成 (找经过已有POI的路径)
## ========================================

## 从一个出发点找到多个目标点的最短路径树
## 返回: {Vector2i target → Array[Vector2i] path}
func find_paths_to_multiple(start: Vector2i, targets: Array[Vector2i]) -> Dictionary:
	if not grid:
		return {}

	# Dijkstra 从 start 扩展到所有目标
	var distances: Dictionary = {}   # Vector2i → float
	var came_from: Dictionary = {}   # Vector2i → Vector2i
	var visited: Dictionary = {}     # Vector2i → bool

	distances[start] = 0.0
	var remaining_targets: Dictionary = {}
	for t in targets:
		remaining_targets[t] = true

	var open: Array[Vector2i] = [start]
	var max_iterations := grid.tile_count() + 1
	var iteration := 0

	while not open.is_empty() and iteration < max_iterations:
		iteration += 1

		# Pop minimum distance
		var best_idx := 0
		var best_d: float = distances.get(open[0], 999999.0)
		for i in range(1, open.size()):
			var d: float = distances.get(open[i], 999999.0)
			if d < best_d:
				best_d = d
				best_idx = i

		var current := open[best_idx]
		open.remove_at(best_idx)

		if visited.has(current):
			continue
		visited[current] = true

		# 检查是否到达某个目标
		remaining_targets.erase(current)

		for dir in range(6):
			var neighbor := HexOverworldTile.get_neighbor(current.x, current.y, dir)
			if not grid.has_tile(neighbor.x, neighbor.y) or visited.has(neighbor):
				continue

			var n_tile := grid.get_tile(neighbor.x, neighbor.y)
			if not n_tile:
				continue

			var cost := _get_move_cost(n_tile)
			if cost < 0.0:
				continue

			var new_dist: float = distances.get(current, 999999.0) + cost
			if new_dist < distances.get(neighbor, 999999.0):
				distances[neighbor] = new_dist
				came_from[neighbor] = current
				open.append(neighbor)

	# 为每个目标重建路径
	var result: Dictionary = {}
	for t in targets:
		if came_from.has(t):
			result[t] = _reconstruct_path(came_from, t)

	return result


## ========================================
## 内部方法
## ========================================

## 六角距离启发函数 (Cube 空间: max(|dq|, |dr|, |ds|))
func _heuristic(a: Vector2i, b: Vector2i) -> float:
	return float(HexOverworldTile.cube_distance(
		HexOverworldTile.axial_to_cube(a.x, a.y),
		HexOverworldTile.axial_to_cube(b.x, b.y)
	))


## 获取移动成本, 不可通行返回 -1
func _get_move_cost(tile: HexOverworldTile) -> float:
	if ignore_passability:
		if tile.is_passable:
			return tile.move_cost
		else:
			return impassable_penalty
	if not tile.is_passable:
		return -1.0
	return tile.move_cost


## 回溯路径
func _reconstruct_path(came_from: Dictionary, current: Vector2i) -> Array[Vector2i]:
	var path: Array[Vector2i] = [current]
	while came_from.has(current):
		current = came_from[current]
		path.append(current)
	path.reverse()
	return path


## 判断从 a 到 b 走的是哪个方向 (0-5), Cube 方向表查找
func _get_direction(a: Vector2i, b: Vector2i) -> int:
	var diff_cube := HexOverworldTile.axial_to_cube(b.x, b.y) - HexOverworldTile.axial_to_cube(a.x, a.y)
	for i in range(6):
		if diff_cube == HexOverworldTile.CUBE_DIRECTIONS[i]:
			return i
	return -1


## 找最近的可通行瓦片
func _find_nearest_passable(tile: HexOverworldTile) -> HexOverworldTile:
	var visited: Dictionary = {tile.coord: true}
	var queue: Array[Vector2i] = [tile.coord]

	while not queue.is_empty():
		var current := queue.pop_front() as Vector2i
		for dir in range(6):
			var n_coord := HexOverworldTile.get_neighbor(current.x, current.y, dir)
			if visited.has(n_coord) or not grid.has_tile(n_coord.x, n_coord.y):
				continue
			visited[n_coord] = true
			var n_tile := grid.get_tile(n_coord.x, n_coord.y)
			if n_tile.is_passable:
				return n_tile
			queue.append(n_coord)

	return null
