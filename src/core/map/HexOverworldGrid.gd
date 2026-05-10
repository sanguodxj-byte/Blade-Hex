# HexOverworldGrid.gd
# 六边形大地图网格管理器 — 存储和查询整个大地图的瓦片数据
# 战场兄弟风格: 初始化时由生成器填充, 之后提供高效查询接口
# 纯数据容器, 渲染由 HexOverworldRenderer 负责
class_name HexOverworldGrid
extends RefCounted


## ========================================
## 网格数据
## ========================================

## 所有瓦片: Vector2i(q, r) → HexOverworldTile
var tiles: Dictionary = {}

## 网格尺寸 (轴向坐标范围)
var grid_width: int = 0   ## q 轴方向格数
var grid_height: int = 0  ## r 轴方向格数

## 地图像素边界 (所有瓦片的像素坐标范围)
var map_pixel_width: float = 0.0
var map_pixel_height: float = 0.0

## 生成种子
var seed_value: int = 0


## ========================================
## 初始化
## ========================================

## 创建指定大小的空网格 (矩形六边形网格)
## row 0 在上, col 0 在左; q = col - floor(row/2), r = row
## 生成矩形外观的六角网格 (offset坐标→轴向存储)
func initialize(width: int, height: int) -> void:
	grid_width = width
	grid_height = height
	tiles.clear()

	for col in range(width):
		for row in range(height):
			# Odd-r offset → axial 转换
			var q: int = col - int(row / 2.0)
			var r: int = row
			var tile := HexOverworldTile.create_empty(q, r)
			tiles[Vector2i(q, r)] = tile

	# 计算像素边界
	_calculate_pixel_bounds()


## ========================================
## 瓦片查询
## ========================================

## 获取指定坐标的瓦片 (不存在返回null)
func get_tile(q: int, r: int) -> HexOverworldTile:
	return tiles.get(Vector2i(q, r), null)


func get_tile_at_coord(coord: Vector2i) -> HexOverworldTile:
	return tiles.get(coord, null)


## 检查坐标是否在网格内
func has_tile(q: int, r: int) -> bool:
	return tiles.has(Vector2i(q, r))


## 获取指定瓦片的6个邻居 (仅返回存在的)
func get_neighbors(q: int, r: int) -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for dir in range(6):
		var n_coord := HexOverworldTile.get_neighbor(q, r, dir)
		var tile = tiles.get(n_coord)
		if tile:
			result.append(tile)
	return result


## 获取指定瓦片的可通行邻居
func get_passable_neighbors(q: int, r: int) -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for dir in range(6):
		var n_coord := HexOverworldTile.get_neighbor(q, r, dir)
		var tile = tiles.get(n_coord)
		if tile and tile.is_passable:
			result.append(tile)
	return result


## 获取指定范围内的所有瓦片 (BFS扩展, 六角距离)
func get_tiles_in_range(q: int, r: int, max_range: int) -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	var visited: Dictionary = {}
	var queue: Array[Vector2i] = [Vector2i(q, r)]
	visited[Vector2i(q, r)] = true

	while not queue.is_empty():
		var current := queue.pop_front() as Vector2i
		var tile = tiles.get(current)
		if not tile:
			continue

		if current != Vector2i(q, r):
			result.append(tile)

		if HexOverworldTile.hex_distance(q, r, current.x, current.y) >= max_range:
			continue

		for dir in range(6):
			var n_coord := HexOverworldTile.get_neighbor(current.x, current.y, dir)
			if not visited.has(n_coord) and tiles.has(n_coord):
				visited[n_coord] = true
				queue.append(n_coord)

	return result


## ========================================
## 空间查询
## ========================================

## 通过像素坐标获取最近的瓦片 (像素→分数Axial→Cube舍入→Axial)
func get_tile_at_pixel(px: float, py: float) -> HexOverworldTile:
	var coord := HexOverworldTile.pixel_to_axial(px, py)
	return tiles.get(coord, null)


## 获取指定像素位置附近的可通行瓦片
## 从像素位置出发, Cube 环形扩展搜索
func find_passable_near_pixel(px: float, py: float, max_search_radius: int = 10) -> HexOverworldTile:
	var center := get_tile_at_pixel(px, py)
	if center and center.is_passable:
		return center

	# 起点: 精确像素→Axial
	var start_coord := HexOverworldTile.pixel_to_axial(px, py)
	if center:
		start_coord = center.coord

	# Cube 环形搜索: 逐圈向外找可通行格
	var start_cube := HexOverworldTile.axial_to_cube(start_coord.x, start_coord.y)
	for radius in range(1, max_search_radius + 1):
		var ring := HexOverworldTile.cube_ring(start_cube, radius)
		for cube_coord in ring:
			var axial := HexOverworldTile.cube_to_axial(cube_coord)
			var tile = tiles.get(axial)
			if tile and tile.is_passable:
				return tile

	return null


## ========================================
## 统计查询
## ========================================

## 获取所有可通行瓦片
func get_passable_tiles() -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.is_passable:
			result.append(t)
	return result


## 获取指定地形类型的所有瓦片
func get_tiles_by_terrain(terrain_type: HexOverworldTile.TerrainType) -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.terrain == terrain_type:
			result.append(t)
	return result


## 获取所有道路瓦片
func get_road_tiles() -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.is_road:
			result.append(t)
	return result


## 获取所有河流瓦片
func get_river_tiles() -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.is_river:
			result.append(t)
	return result


## 获取所有定居点瓦片
func get_settlement_tiles() -> Array[HexOverworldTile]:
	var result: Array[HexOverworldTile] = []
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.has_settlement:
			result.append(t)
	return result


## 瓦片总数
func tile_count() -> int:
	return tiles.size()


## ========================================
## 序列化
## ========================================

func serialize() -> Dictionary:
	var tiles_data: Array = []
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		tiles_data.append(t.serialize())
	return {
		"grid_width": grid_width,
		"grid_height": grid_height,
		"seed": seed_value,
		"tiles": tiles_data,
	}


static func deserialize(data: Dictionary) -> HexOverworldGrid:
	var grid = HexOverworldGrid.new()
	grid.grid_width = int(data.get("grid_width", 0))
	grid.grid_height = int(data.get("grid_height", 0))
	grid.seed_value = int(data.get("seed", 0))

	var tiles_data: Array = data.get("tiles", [])
	for tile_data in tiles_data:
		var tile := HexOverworldTile.deserialize(tile_data)
		grid.tiles[tile.coord] = tile

	grid._calculate_pixel_bounds()
	return grid


## ========================================
## 地形采样 (兼容 OverworldMap 接口)
## ========================================

## 返回大地图地形类型 (映射到 OverworldTerrain.Type)
func sample_terrain_at_pixel(px: float, py: float) -> int:
	var tile = get_tile_at_pixel(px, py)
	if not tile:
		return 0  # PLAINS
	return _hex_terrain_to_overworld(tile.terrain)


## 检查像素位置是否可通行
func is_passable_at_pixel(px: float, py: float) -> bool:
	var tile = get_tile_at_pixel(px, py)
	return tile != null and tile.is_passable


## 获取地图中心像素坐标
func get_center_pixel() -> Vector2:
	return Vector2(map_pixel_width * 0.5, map_pixel_height * 0.5)


## 获取有效起始位置 (第一个城镇附近或可通行位置)
func get_valid_start_pos() -> Vector2:
	# 优先找定居点
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.has_settlement and t.is_passable:
			return t.pixel_pos
	# 回退: 找地图中心附近的可通行格
	var center := get_center_pixel()
	var best: HexOverworldTile = null
	var best_dist: float = 999999.0
	for tile in tiles.values():
		var t: HexOverworldTile = tile
		if t.is_passable:
			var d := t.pixel_pos.distance_to(center)
			if d < best_dist:
				best_dist = d
				best = t
	if best:
		return best.pixel_pos
	return center


## ========================================
## 内部方法
## ========================================

func _calculate_pixel_bounds() -> void:
	var min_x: float = 999999.0
	var max_x: float = -999999.0
	var min_y: float = 999999.0
	var max_y: float = -999999.0

	for tile in tiles.values():
		var t: HexOverworldTile = tile
		min_x = minf(min_x, t.pixel_pos.x)
		max_x = maxf(max_x, t.pixel_pos.x)
		min_y = minf(min_y, t.pixel_pos.y)
		max_y = maxf(max_y, t.pixel_pos.y)

	map_pixel_width = max_x - min_x + HexOverworldTile.HEX_SIZE * 2.0
	map_pixel_height = max_y - min_y + HexOverworldTile.HEX_SIZE * 2.0


## HexOverworldTile.TerrainType → OverworldTerrain.Type 映射
static func _hex_terrain_to_overworld(hex_terrain: int) -> int:
	# OverworldTerrain.Type 枚举值: PLAINS=0, FOREST=1, MOUNTAIN=2, SWAMP=3, WATER=4, ROAD=5, DESERT=6
	match hex_terrain:
		HexOverworldTile.TerrainType.DEEP_WATER:    return 4    # WATER
		HexOverworldTile.TerrainType.SHALLOW_WATER: return 4    # WATER
		HexOverworldTile.TerrainType.SAND:          return 6    # DESERT
		HexOverworldTile.TerrainType.PLAINS:        return 0    # PLAINS
		HexOverworldTile.TerrainType.GRASSLAND:     return 0    # PLAINS
		HexOverworldTile.TerrainType.FOREST:        return 1    # FOREST
		HexOverworldTile.TerrainType.DENSE_FOREST:  return 1    # FOREST
		HexOverworldTile.TerrainType.HILLS:         return 2    # MOUNTAIN
		HexOverworldTile.TerrainType.MOUNTAIN:      return 2    # MOUNTAIN
		HexOverworldTile.TerrainType.SNOW:          return 2    # MOUNTAIN
		HexOverworldTile.TerrainType.SWAMP:         return 3    # SWAMP
		HexOverworldTile.TerrainType.SAVANNA:       return 0    # PLAINS
		HexOverworldTile.TerrainType.ROAD:          return 5    # ROAD
		HexOverworldTile.TerrainType.RIVER:         return 4    # WATER
		_:                                         return 0
