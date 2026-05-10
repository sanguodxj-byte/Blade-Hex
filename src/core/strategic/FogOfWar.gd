# FogOfWar.gd
# 战争迷雾系统 — 分种族初始揭示 + 三级迷雾 + 存档持久化
#
# 对应策划案 14-大地图设定.md → 六、地图探索系统（战争迷雾）
#
# 迷雾层级:
#   0 = UNEXPLORED  未探索：地形不可见，POI不可见，无法移动
#   1 = REVEALED    已揭示：地形可见，POI可见（静态），敌方实体不可见
#   2 = IN_VISION   当前视野：一切可见
#
# 数据结构:
#   explored_grid: Array[Array[int]]  grid_y][grid_x] → 0/1/2
#   每帧结束时 2 回退为 1（永久揭示）
class_name FogOfWar
extends RefCounted

## 迷雾状态枚举
enum FogState {
	UNEXPLORED = 0,  ## 未探索
	REVEALED   = 1,  ## 已揭示（永久）
	IN_VISION  = 2,  ## 当前视野
}

## 网格数据：grid_y][grid_x] → FogState 值
var explored_grid: Array = []
## 网格尺寸
var grid_w: int = 0
var grid_h: int = 0
## 格子像素大小（与 OverworldMap.cell_size 对齐）
var cell_size: int = 16
## 地图像素尺寸
var map_width_px: int = 0
var map_height_px: int = 0

## 视野半径（像素）— 对应策划案 VISION_RANGE
var vision_range: float = 600.0
## 侦察加成倍率
var scout_multiplier: float = 1.0

## 上一帧标记为 IN_VISION 的格子列表 — 用于高效降级（避免全图扫描）
var _last_vision_cells: Array[Vector2i] = []


## ========================================
## 初始化
## ========================================

## 从地图尺寸创建空迷雾（全未探索），然后按种族揭示初始区域
func initialize(map_w_px: int, map_h_px: int, p_cell_size: int, race_id: int) -> void:
	map_width_px = map_w_px
	map_height_px = map_h_px
	cell_size = p_cell_size
	grid_w = int(map_w_px / float(cell_size))
	grid_h = int(map_h_px / float(cell_size))

	# 初始化全未探索
	explored_grid.clear()
	explored_grid.resize(grid_h)
	for y in range(grid_h):
		var row: Array = []
		row.resize(grid_w)
		row.fill(FogState.UNEXPLORED)
		explored_grid[y] = row

	# 按种族揭示初始区域
	_apply_race_initial_reveal(race_id)


## ========================================
## 种族初始揭示（对应策划案 6.3 出生时已知区域）
## ========================================

## 各种族初始已知区域 — 使用归一化坐标 [0,1] 定义矩形区域
## 与 WorldGenerator 六大区域对齐
static func get_race_initial_regions(race_id: int) -> Array[Dictionary]:
	match race_id:
		RaceData.Race.HUMAN:
			# 中央平原大部 + 西海岸
			return [
				{"x": 0.05, "y": 0.2, "w": 0.85, "h": 0.55},  # 中央平原大部
				{"x": 0.0,  "y": 0.25, "w": 0.15, "h": 0.5},   # 西海岸附近
			]
		RaceData.Race.ELF:
			# 银叶森林全域 + 中央平原西部边缘
			return [
				{"x": 0.0,  "y": 0.2, "w": 0.25, "h": 0.6},    # 银叶森林全域
				{"x": 0.2,  "y": 0.3, "w": 0.1,  "h": 0.2},    # 中央平原西部边缘
			]
		RaceData.Race.DWARF:
			# 霜冠山脉 + 北关隘附近
			return [
				{"x": 0.1,  "y": 0.0, "w": 0.8, "h": 0.25},    # 霜冠山脉
				{"x": 0.15, "y": 0.2, "w": 0.2,  "h": 0.1},    # 北关隘附近（山脉南麓）
			]
		RaceData.Race.HALF_ORC:
			# 丘陵草原 + 中央平原东部边缘
			return [
				{"x": 0.65, "y": 0.25, "w": 0.35, "h": 0.45},  # 丘陵草原
				{"x": 0.55, "y": 0.35, "w": 0.15, "h": 0.15},  # 中央平原东部边缘
			]
		RaceData.Race.HALF_ELF:
			# 中央平原 + 银叶森林边缘
			return [
				{"x": 0.1,  "y": 0.25, "w": 0.6, "h": 0.5},    # 中央平原
				{"x": 0.0,  "y": 0.25, "w": 0.15, "h": 0.4},   # 银叶森林边缘
			]
		_:
			# 默认：中央平原中部一小块
			return [{"x": 0.3, "y": 0.35, "w": 0.4, "h": 0.3}]


func _apply_race_initial_reveal(race_id: int) -> void:
	var regions = get_race_initial_regions(race_id)
	for region in regions:
		var x_start := int(region.x * grid_w)
		var y_start := int(region.y * grid_h)
		var x_end := mini(int((region.x + region.w) * grid_w), grid_w)
		var y_end := mini(int((region.y + region.h) * grid_h), grid_h)
		for gy in range(y_start, y_end):
			for gx in range(x_start, x_end):
				if gy >= 0 and gy < grid_h and gx >= 0 and gx < grid_w:
					explored_grid[gy][gx] = FogState.REVEALED


## ========================================
## 每帧更新
## ========================================

## 更新视野：以 player_pos 为中心，VISION_RANGE 内的格子设为 IN_VISION
## 同时将这些格子永久标记为 REVEALED
func update_vision(player_pos: Vector2) -> void:
	# 第1步：仅降级上一帧标记为 IN_VISION 的格子（O(n) n=视野格子数，非全图）
	for cell in _last_vision_cells:
		if cell.y >= 0 and cell.y < grid_h and cell.x >= 0 and cell.x < grid_w:
			if explored_grid[cell.y][cell.x] == FogState.IN_VISION:
				explored_grid[cell.y][cell.x] = FogState.REVEALED
	_last_vision_cells.clear()

	# 第2步：以玩家为中心，视野范围内的格子设为 IN_VISION
	var effective_range := vision_range * scout_multiplier
	var center_gx := int(player_pos.x / float(cell_size))
	var center_gy := int(player_pos.y / float(cell_size))
	var range_cells := int(effective_range / float(cell_size)) + 1

	var range_sq := effective_range * effective_range

	# 只遍历视野范围内的格子（避免全图遍历）
	var y_min := maxi(center_gy - range_cells, 0)
	var y_max := mini(center_gy + range_cells, grid_h - 1)
	var x_min := maxi(center_gx - range_cells, 0)
	var x_max := mini(center_gx + range_cells, grid_w - 1)

	for gy in range(y_min, y_max + 1):
		for gx in range(x_min, x_max + 1):
			var px := (gx + 0.5) * cell_size
			var py := (gy + 0.5) * cell_size
			var dx := px - player_pos.x
			var dy := py - player_pos.y
			if dx * dx + dy * dy <= range_sq:
				explored_grid[gy][gx] = FogState.IN_VISION
				_last_vision_cells.append(Vector2i(gx, gy))


## ========================================
## 揭示接口（供外部调用）
## ========================================

## 揭示指定像素位置周围的区域（购买地图、NPC情报等）
func reveal_area(center_px: Vector2, radius_px: float) -> void:
	var center_gx := int(center_px.x / float(cell_size))
	var center_gy := int(center_px.y / float(cell_size))
	var range_cells := int(radius_px / float(cell_size)) + 1
	var range_sq := radius_px * radius_px

	var y_min := maxi(center_gy - range_cells, 0)
	var y_max := mini(center_gy + range_cells, grid_h - 1)
	var x_min := maxi(center_gx - range_cells, 0)
	var x_max := mini(center_gx + range_cells, grid_w - 1)

	for gy in range(y_min, y_max + 1):
		for gx in range(x_min, x_max + 1):
			var px := (gx + 0.5) * cell_size
			var py := (gy + 0.5) * cell_size
			var dx := px - center_px.x
			var dy := py - center_px.y
			if dx * dx + dy * dy <= range_sq:
				if explored_grid[gy][gx] == FogState.UNEXPLORED:
					explored_grid[gy][gx] = FogState.REVEALED


## 揭示指定区域名对应的整块区域（购买区域地图）
func reveal_region_by_name(region_name: String) -> void:
	var region_rect = _get_region_rect_by_name(region_name)
	if region_rect.is_empty():
		return
	var x_start := int(region_rect.x * grid_w)
	var y_start := int(region_rect.y * grid_h)
	var x_end := mini(int((region_rect.x + region_rect.w) * grid_w), grid_w)
	var y_end := mini(int((region_rect.y + region_rect.h) * grid_h), grid_h)
	for gy in range(y_start, y_end):
		for gx in range(x_start, x_end):
			if gy >= 0 and gy < grid_h and gx >= 0 and gx < grid_w:
				if explored_grid[gy][gx] == FogState.UNEXPLORED:
					explored_grid[gy][gx] = FogState.REVEALED


## 根据 WorldGenerator 区域名获取归一化矩形
func _get_region_rect_by_name(region_name: String) -> Dictionary:
	match region_name:
		"霜冠山脉":
			return {"x": 0.1, "y": 0.0, "w": 0.8, "h": 0.2}
		"银叶森林":
			return {"x": 0.0, "y": 0.2, "w": 0.25, "h": 0.6}
		"中央平原":
			return {"x": 0.1, "y": 0.25, "w": 0.8, "h": 0.5}
		"丘陵草原":
			return {"x": 0.7, "y": 0.25, "w": 0.3, "h": 0.45}
		"焦土荒原":
			return {"x": 0.5, "y": 0.75, "w": 0.5, "h": 0.25}
		"蛮荒沼泽":
			return {"x": 0.0, "y": 0.75, "w": 0.4, "h": 0.25}
		_:
			return {}


## 设置侦察加成（队伍中有游侠/斥候时调用）
func set_scout_multiplier(multiplier: float) -> void:
	scout_multiplier = clampf(multiplier, 1.0, 3.0)


## ========================================
## 查询接口
## ========================================

## 获取指定像素坐标的迷雾状态
func get_fog_state_at(px: float, py: float) -> int:
	var gx := int(px / float(cell_size))
	var gy := int(py / float(cell_size))
	if gy < 0 or gy >= grid_h or gx < 0 or gx >= grid_w:
		return FogState.UNEXPLORED
	return explored_grid[gy][gx]


## 指定位置是否已被揭示（含视野内）
func is_revealed(px: float, py: float) -> bool:
	return get_fog_state_at(px, py) >= FogState.REVEALED


## 指定位置是否在当前视野内
func is_in_vision(px: float, py: float) -> bool:
	return get_fog_state_at(px, py) == FogState.IN_VISION


## 指定位置是否未探索
func is_unexplored(px: float, py: float) -> bool:
	return get_fog_state_at(px, py) == FogState.UNEXPLORED


## 获取探索进度百分比（0.0 ~ 1.0）
func get_exploration_progress() -> float:
	var total := grid_w * grid_h
	if total == 0:
		return 0.0
	var explored := 0
	for gy in range(grid_h):
		for gx in range(grid_w):
			if explored_grid[gy][gx] >= FogState.REVEALED:
				explored += 1
	return float(explored) / float(total)


## ========================================
## 序列化（存档/读档）
## ========================================

## 导出为可序列化的 Dictionary
## explored_grid 压缩为行程编码（RLE）数组以节省存档体积
func serialize() -> Dictionary:
	# RLE 压缩：[值, 连续长度, 值, 连续长度, ...]
	var rle_data: Array = []
	var current_val := -1
	var run_length := 0

	for gy in range(grid_h):
		for gx in range(grid_w):
			var val: int = explored_grid[gy][gx]
			if val == current_val:
				run_length += 1
			else:
				if current_val >= 0:
					rle_data.append(current_val)
					rle_data.append(run_length)
				current_val = val
				run_length = 1
	# 最后一段
	if current_val >= 0:
		rle_data.append(current_val)
		rle_data.append(run_length)

	return {
		"grid_w": grid_w,
		"grid_h": grid_h,
		"cell_size": cell_size,
		"vision_range": vision_range,
		"rle_data": rle_data,
	}


## 从存档数据恢复
static func deserialize(data: Dictionary):
	var fog = new()
	fog.grid_w = int(data.get("grid_w", 0))
	fog.grid_h = int(data.get("grid_h", 0))
	fog.cell_size = int(data.get("cell_size", 16))
	fog.vision_range = float(data.get("vision_range", 600.0))

	if fog.grid_w <= 0 or fog.grid_h <= 0:
		return fog

	# 初始化空网格
	fog.explored_grid.clear()
	fog.explored_grid.resize(fog.grid_h)
	for y in range(fog.grid_h):
		var row: Array = []
		row.resize(fog.grid_w)
		row.fill(FogState.UNEXPLORED)
		fog.explored_grid[y] = row

	# RLE 解码
	var rle_data = data.get("rle_data", [])
	var idx := 0
	var gx := 0
	var gy := 0
	while idx + 1 < rle_data.size():
		var val: int = int(rle_data[idx])
		var length: int = int(rle_data[idx + 1])
		idx += 2
		for _i in range(length):
			if gy < fog.grid_h and gx < fog.grid_w:
				fog.explored_grid[gy][gx] = val
			gx += 1
			if gx >= fog.grid_w:
				gx = 0
				gy += 1

	return fog
