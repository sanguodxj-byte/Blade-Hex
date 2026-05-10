# HexGrid.gd
# 管理六边形网格的生成、查询和寻路逻辑 (HD-2D 3D版本)
extends Node3D
class_name HexGrid

# 存储单元格数据: { Vector2i(q, r): HexCell }
var cells: Dictionary = {}

var cell_scene = preload("res://src/core/map/HexCell.gd") # 注意：正式环境应为.tscn

func create_cell(q: int, r: int, elevation: int = 1, cover: int = 0):
	var cell_pos = HexUtils.axial_to_world3d(q, r, elevation)
	
	# 实例化 HexCell 并配置
	var cell = cell_scene.new()
	cell.name = "HexCell_%d_%d" % [q, r]
	cell.position = cell_pos
	cell.grid_pos = Vector2i(q, r)
	cell.elevation = elevation
	cell.cover_type = cover
	add_child(cell)
	
	cells[Vector2i(q, r)] = cell


## 从 BattleMapData 加载地图（由 BattleMapGenerator 生成的数据）
## 这是大地图→战斗地图的关键接口
func load_from_map_data(mapdata: BattleMapGenerator.BattleMapData):
	# 清空现有格子
	for cell in cells.values():
		if is_instance_valid(cell):
			cell.queue_free()
	cells.clear()
	
	# 遍历 map_data.cells，为每个格子创建 HexCell 并应用 BattleCellData
	for key in mapdata.cells:
		var cell_data: BattleCellData = mapdata.cells[key]
		var q: int = key.x
		var r: int = key.y
		var elev: int = cell_data.elevation
		var cover: int = cell_data.cover_level
		
		var cell_pos = HexUtils.axial_to_world3d(q, r, elev)
		
		var cell = cell_scene.new()
		cell.name = "HexCell_%d_%d" % [q, r]
		cell.position = cell_pos
		cell.grid_pos = Vector2i(q, r)
		cell.elevation = elev
		cell.cover_type = cover
		# 将 BattleCellData 赋给 HexCell.data，使 HexCell 可读取地形属性
		cell.data = cell_data
		add_child(cell)
		
		cells[key] = cell


## 获取指定坐标的单元格
func get_cell(q: int, r: int):
	return cells.get(Vector2i(q, r))

## 获取范围内的所有坐标 (BFS)
func get_cells_in_range(start_q: int, start_r: int, maxrange: int) -> Array[Vector2i]:
	var in_range: Array[Vector2i] = []
	var visited: Array[Vector2i] = [Vector2i(start_q, start_r)]
	var fringes: Array[Array] = [[Vector2i(start_q, start_r)]]
	
	for k in range(1, maxrange + 1):
		fringes.append([])
		for hex in fringes[k-1]:
			var current_cell = cells[hex]
			for dir in range(6):
				var neighbor = HexUtils.get_neighbor(hex.x, hex.y, dir)
				if cells.has(neighbor) and not visited.has(neighbor):
					var neighbor_cell = cells[neighbor]
					# 高低差判断：相差大于1级则不可跨越
					if abs(neighbor_cell.elevation - current_cell.elevation) > 1:
						continue
					
					visited.append(neighbor)
					in_range.append(neighbor)
					fringes[k].append(neighbor)
	return in_range

## A* 寻路算法，计算两个六边形之间的最短可通行路径
## 使用简化的优先队列实现
func find_path(start_pos: Vector2i, target_pos: Vector2i) -> Array[Vector2i]:
	if not cells.has(start_pos) or not cells.has(target_pos):
		return []
		
	var target_cell = cells[target_pos]
	# 如果目标已被占据，直接返回空路径（也可修改为寻路到相邻格）
	if target_cell.occupant != null:
		return []
	
	# 使用字典模拟优先队列，key为priority的整数部分
	var frontier: Dictionary = {}  # priority_bucket -> Array[Vector2i]
	var min_priority: int = 0
	
	_add_to_frontier(frontier, start_pos, 0.0)
	
	var came_from = {}
	var cost_so_far = {}
	
	came_from[start_pos] = start_pos
	cost_so_far[start_pos] = 0.0
	
	while not frontier.is_empty():
		# 从最小优先级桶中取出一个节点
		var current = _pop_from_frontier(frontier)
		if current == null:
			break
		
		if current == target_pos:
			break
			
		var current_cell = cells[current]
		
		for dir in range(6):
			var next_pos = HexUtils.get_neighbor(current.x, current.y, dir)
			if not cells.has(next_pos):
				continue
				
			var next_cell = cells[next_pos]
			
			# 规则1：高低差过大（>1）视为断崖，不可通行
			if abs(next_cell.elevation - current_cell.elevation) > 1:
				continue
			
			# 规则1.5：不可通行地形（墙壁、深水等）
			if next_cell.data and not next_cell.data.is_passable:
				continue
			
			# 规则2：不能穿过已被其他单位占据的格子
			if next_cell.occupant != null and next_pos != target_pos:
				continue
			
			# 计算移动消耗，默认为1，若有地形数据则读取地形数据
			var move_cost = 1.0
			if next_cell.data:
				move_cost = float(next_cell.data.move_cost)
				
			var new_cost = cost_so_far[current] + move_cost
			
			if not cost_so_far.has(next_pos) or new_cost < cost_so_far[next_pos]:
				cost_so_far[next_pos] = new_cost
				var priority = new_cost + HexUtils.distance(next_pos.x, next_pos.y, target_pos.x, target_pos.y)
				_add_to_frontier(frontier, next_pos, priority)
				came_from[next_pos] = current
				
	# 回溯构建路径
	if not came_from.has(target_pos):
		return [] # 目标不可达
		
	var path: Array[Vector2i] = []
	var current = target_pos
	while current != start_pos:
		path.append(current)
		current = came_from[current]
		
	path.reverse() # 倒序排列，变为从 start -> target
	return path


## 添加节点到优先队列
func _add_to_frontier(frontier: Dictionary, pos: Vector2i, priority: float) -> void:
	var bucket = int(priority * 10)  # 精度到0.1
	if not frontier.has(bucket):
		frontier[bucket] = []
	frontier[bucket].append(pos)


## 从优先队列中取出最小优先级的节点
func _pop_from_frontier(frontier: Dictionary) -> Vector2i:
	if frontier.is_empty():
		return Vector2i(-1, -1)
	
	# 找到最小的bucket
	var min_bucket = 999999
	for bucket in frontier.keys():
		if bucket < min_bucket:
			min_bucket = bucket
	
	var bucket_array = frontier[min_bucket]
	var result = bucket_array.pop_back()
	
	if bucket_array.is_empty():
		frontier.erase(min_bucket)
	
	return result
