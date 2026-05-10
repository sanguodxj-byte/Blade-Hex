# HexOverworldGenerator.gd
# 六边形大地图结构化生成器 — 战场兄弟风格
# 在游戏世界初始化时按规则和逻辑生成大量六边形瓦片:
#   1. 多层噪声生成高程/湿度/温度基础数据
#   2. 生物群落规则决定每个瓦片的地形类型
#   3. 地形修正与平滑 (海岸线/山脉线/森林边缘)
#   4. A*寻路生成道路 (连接城镇, 贴合地形)
#   5. A*寻路生成河流 (从高地流向低地)
#   6. 地理区域标记 (用于POI放置和遭遇规则)
#
# 坐标约定: 存储 Axial(q,r), 内部计算 Cube(q,r,s) where s=-q-r
class_name HexOverworldGenerator
extends RefCounted


## ========================================
## 生成配置
## ========================================

## 默认地图尺寸 (六边形格数) — 配合 HEX_SIZE=156, 313px纹理
## 64×48 格 ≈ 15000×13000 像素, 足够大地图体验
const DEFAULT_WIDTH: int = 64
const DEFAULT_HEIGHT: int = 48

## 噪声参数 (配合 64×48 格)
const ELEVATION_FREQ: float = 0.06
const MOISTURE_FREQ: float = 0.07
const TEMPERATURE_FREQ: float = 0.025

## 地形阈值
const SEA_LEVEL: float = 0.30         ## 低于此值 → 水域
const SHALLOW_LEVEL: float = 0.35     ## 浅水线
const BEACH_LEVEL: float = 0.38       ## 沙滩线
const MOUNTAIN_LEVEL: float = 0.78    ## 山地线
#define SNOW_LEVEL: float = 0.88      ## 雪线 (基于纬度+高程)

## 生物群落湿度阈值
const DRY_THRESHOLD: float = 0.35
const WET_THRESHOLD: float = 0.65

## 平滑迭代次数
const SMOOTH_PASSES: int = 2

## 河流参数
const RIVER_COUNT_MIN: int = 3
const RIVER_COUNT_MAX: int = 6
const RIVER_MIN_LENGTH: int = 15

## 道路参数
const ROAD_PENALTY_FOREST: float = 3.0
const ROAD_PENALTY_HILL: float = 5.0
const ROAD_PENALTY_SWAMP: float = 8.0


## ========================================
## 地理区域定义
## ========================================

class RegionDef:
	extends RefCounted
	var name: String = ""
	var center_q: float = 0.0   ## 归一化中心 [0,1]
	var center_r: float = 0.0
	var radius_q: float = 0.2
	var radius_r: float = 0.2
	var danger_level: float = 0.0
	var preferred_terrains: Array = []


## ========================================
## 运行时状态
## ========================================

var _noise_elev: FastNoiseLite
var _noise_moist: FastNoiseLite
var _noise_temp: FastNoiseLite
var _noise_detail: FastNoiseLite
var grid: HexOverworldGrid
var _regions: Array[RegionDef] = []
var seed: int = 0


## ========================================
## 主入口
## ========================================

## 生成完整的大地图
## width/height: 六边形格数 (q方向 × r方向)
## world_seed: 随机种子 (-1=随机)
func generate(width: int = DEFAULT_WIDTH, height: int = DEFAULT_HEIGHT, world_seed: int = -1) -> HexOverworldGrid:
	seed = world_seed if world_seed >= 0 else randi()
	seed(seed)

	# 第0步: 初始化噪声
	_init_noise()

	# 第1步: 创建空网格
	grid = HexOverworldGrid.new()
	grid.initialize(width, height)
	grid.seed_value = seed

	# 第2步: 生成基础数据层 (高程/湿度/温度)
	_generate_base_layers(width, height)

	# 第3步: 生物群落规则 → 地形类型
	_assign_biome_terrains(width, height)

	# 第4步: 地形平滑
	_smooth_terrain(SMOOTH_PASSES)

	# 第5步: 海岸线修正
	_fix_coastlines()

	# 第6步: 定义地理区域
	_define_regions(width, height)
	_assign_region_names()

	# 第7步: 生成河流 (从高地流向低地, A*寻路)
	_generate_rivers()

	# 第8步: 生成道路 (连接城镇POI, A*寻路)
	_generate_roads()

	# 第9步: 后处理 (不可通行地形修正移动成本)
	_finalize_terrain()

	print("[HexOverworldGenerator] 生成完成: %d×%d 瓦片, 种子=%d, 河流/道路已生成" % [width, height, seed])
	return grid


## ========================================
## 第0步: 噪声初始化
## ========================================

func _init_noise() -> void:
	_noise_elev = FastNoiseLite.new()
	_noise_elev.noise_type = FastNoiseLite.TYPE_SIMPLEX
	_noise_elev.seed = seed
	_noise_elev.frequency = ELEVATION_FREQ
	_noise_elev.fractal_type = FastNoiseLite.FRACTAL_FBM
	_noise_elev.fractal_octaves = 6
	_noise_elev.fractal_lacunarity = 2.0
	_noise_elev.fractal_gain = 0.5

	_noise_moist = FastNoiseLite.new()
	_noise_moist.noise_type = FastNoiseLite.TYPE_SIMPLEX
	_noise_moist.seed = seed + 1000
	_noise_moist.frequency = MOISTURE_FREQ
	_noise_moist.fractal_type = FastNoiseLite.FRACTAL_FBM
	_noise_moist.fractal_octaves = 4
	_noise_moist.fractal_lacunarity = 2.0
	_noise_moist.fractal_gain = 0.5

	_noise_temp = FastNoiseLite.new()
	_noise_temp.noise_type = FastNoiseLite.TYPE_SIMPLEX
	_noise_temp.seed = seed + 2000
	_noise_temp.frequency = TEMPERATURE_FREQ
	_noise_temp.fractal_type = FastNoiseLite.FRACTAL_FBM
	_noise_temp.fractal_octaves = 3

	_noise_detail = FastNoiseLite.new()
	_noise_detail.noise_type = FastNoiseLite.TYPE_CELLULAR
	_noise_detail.seed = seed + 3000
	_noise_detail.frequency = 0.08
	_noise_detail.cellular_distance_function = FastNoiseLite.DISTANCE_EUCLIDEAN


## ========================================
## 第1步: 基础数据层生成
## ========================================

func _generate_base_layers(width: int, height: int) -> void:
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		var q: int = t.coord.x
		var r: int = t.coord.y

		# 高程: 多层噪声 + 边缘衰减 (形成大陆形状)
		var raw_elev := _noise_elev.get_noise_2d(float(q), float(r))
		var edge_falloff := _calc_edge_falloff(q, r, width, height)
		var elev := (raw_elev + 1.0) * 0.5 * edge_falloff  # [0, 1]
		t.elevation = clampf(elev, 0.0, 1.0)

		# 湿度: 第二层噪声
		var raw_moist := _noise_moist.get_noise_2d(float(q), float(r))
		t.moisture = clampf((raw_moist + 1.0) * 0.5, 0.0, 1.0)

		# 温度: 纬度 + 噪声偏移 (北冷南热)
		var latitude_factor := float(r) / float(height)  # 0=北, 1=南
		var temp_noise := _noise_temp.get_noise_2d(float(q), float(r)) * 0.2
		t.temperature = clampf(latitude_factor + temp_noise, 0.0, 1.0)


## 计算边缘衰减: 让地图边缘沉入海洋, 形成自然大陆
func _calc_edge_falloff(q: int, r: int, width: int, height: int) -> float:
	# 归一化坐标 [0, 1]
	var nx := float(q + height / 2) / float(width)  # 近似: q范围可能包含负值
	var ny := float(r) / float(height)

	# 找到到最近边缘的距离
	var dx := minf(nx, 1.0 - nx)
	var dy := minf(ny, 1.0 - ny)
	var edge_dist := minf(dx, dy)

	# 边缘衰减: 距边缘 < 15% 时开始下降
	if edge_dist < 0.15:
		return edge_dist / 0.15
	return 1.0


## ========================================
## 第2步: 生物群落规则 → 地形类型
## ========================================

## 基于高程/湿度/温度的生物群落决策表
## 参考 Whittaker biome diagram 简化版
func _assign_biome_terrains(_width: int, _height: int):
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		var terrain := _biome_decision(t)
		t.terrain = terrain
		t._update_terrain_properties()


## 核心生物群落决策 (基于 Whittaker 生物群系模型)
func _biome_decision(tile: HexOverworldTile) -> HexOverworldTile.TerrainType:
	var e: float = tile.elevation
	var m: float = tile.moisture
	var t: float = tile.temperature

	# === 1. 高程截断: 水域与山脉 ===
	if e < SEA_LEVEL:
		return HexOverworldTile.TerrainType.DEEP_WATER
	if e < SHALLOW_LEVEL:
		return HexOverworldTile.TerrainType.SHALLOW_WATER
	if e < BEACH_LEVEL:
		# 极寒地带的海岸是冰/雪，其他地方是沙滩
		if t < 0.15:
			return HexOverworldTile.TerrainType.ICE
		return HexOverworldTile.TerrainType.SAND

	if e > MOUNTAIN_LEVEL:
		# 山地
		if e > 0.88 or t < 0.25:
			return HexOverworldTile.TerrainType.MOUNTAIN_SNOW
		return HexOverworldTile.TerrainType.MOUNTAIN

	# === 2. 气候与生物群系矩阵 (Whittaker Biome) ===
	
	# 温度带划分
	var is_freezing := t < 0.15
	var is_cold := t >= 0.15 and t < 0.35
	var is_temperate := t >= 0.35 and t < 0.70
	var is_hot := t >= 0.70
	
	# 湿度带划分
	var is_arid := m < 0.25
	var is_dry := m >= 0.25 and m < 0.50
	var is_wet := m >= 0.50 and m < 0.75
	var is_humid := m >= 0.75

	var base_terrain = HexOverworldTile.TerrainType.PLAINS

	# 极寒 (Freezing)
	if is_freezing:
		if is_arid: base_terrain = HexOverworldTile.TerrainType.ICE
		elif is_dry: base_terrain = HexOverworldTile.TerrainType.SNOW
		elif is_wet: base_terrain = HexOverworldTile.TerrainType.SNOW
		else: base_terrain = HexOverworldTile.TerrainType.SNOW
		
	# 寒冷 (Cold)
	elif is_cold:
		if is_arid: base_terrain = HexOverworldTile.TerrainType.ROCKY
		elif is_dry: base_terrain = HexOverworldTile.TerrainType.TAIGA
		elif is_wet: base_terrain = HexOverworldTile.TerrainType.TAIGA
		else: base_terrain = HexOverworldTile.TerrainType.BOG
		
	# 温带 (Temperate)
	elif is_temperate:
		if is_arid: base_terrain = HexOverworldTile.TerrainType.WASTELAND
		elif is_dry: base_terrain = HexOverworldTile.TerrainType.PLAINS
		elif is_wet: base_terrain = HexOverworldTile.TerrainType.FOREST
		else: base_terrain = HexOverworldTile.TerrainType.DENSE_FOREST
		
	# 炎热 (Hot)
	elif is_hot:
		if is_arid: base_terrain = HexOverworldTile.TerrainType.SAND
		elif is_dry: base_terrain = HexOverworldTile.TerrainType.SAVANNA
		elif is_wet: base_terrain = HexOverworldTile.TerrainType.JUNGLE
		else: base_terrain = HexOverworldTile.TerrainType.SWAMP

	# === 3. 高程微调: 丘陵与地形起伏 ===
	# 如果处于较高海拔但没到山脉，变为丘陵地形
	if e > 0.65 and e <= MOUNTAIN_LEVEL:
		# 雪地/极寒/沙漠 的丘陵可以保留地形特质，只是数值和视觉起伏变化，但为简单起见
		# 这里我们可以引入特定类型的丘陵，或者直接用丘陵覆盖
		if base_terrain == HexOverworldTile.TerrainType.SNOW or base_terrain == HexOverworldTile.TerrainType.ICE:
			return HexOverworldTile.TerrainType.SNOW # 雪地丘陵
		elif base_terrain == HexOverworldTile.TerrainType.SAND or base_terrain == HexOverworldTile.TerrainType.WASTELAND:
			return HexOverworldTile.TerrainType.HILLS # 荒芜丘陵
		else:
			return HexOverworldTile.TerrainType.HILLS

	return base_terrain


## ========================================
## 第3步: 地形平滑
## ========================================

func _smooth_terrain(passes: int) -> void:
	# 不可被平滑覆盖的地形
	var immune: Dictionary = {
		HexOverworldTile.TerrainType.DEEP_WATER: true,
		HexOverworldTile.TerrainType.SHALLOW_WATER: true,
		HexOverworldTile.TerrainType.ROAD: true,
		HexOverworldTile.TerrainType.RIVER: true,
	}

	for _pass in range(passes):
		var changes: Dictionary = {}

		for tile in grid.tiles.values():
			var t: HexOverworldTile = tile
			if immune.has(t.terrain):
				continue

			# 统计6邻居的地形出现次数
			var counts: Dictionary = {}
			for n_tile in grid.get_neighbors(t.coord.x, t.coord.y):
				if not counts.has(n_tile.terrain):
					counts[n_tile.terrain] = 0
				counts[n_tile.terrain] += 1

			# 如果超过4个邻居是同一种地形, 且不同于自己, 则平滑
			for t_type in counts:
				if counts[t_type] >= 4 and t_type != t.terrain:
					if randf() < 0.6:
						changes[t.coord] = t_type
					break

		# 应用变化
		for coord in changes:
			var tile := grid.get_tile(coord.x, coord.y)
			if tile:
				tile.set_terrain(changes[coord])


## ========================================
## 第4步: 海岸线修正
## ========================================

func _fix_coastlines() -> void:
	# 确保深水旁有浅水缓冲, 浅水旁有沙滩
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		if t.terrain == HexOverworldTile.TerrainType.DEEP_WATER:
			# 检查是否有陆地邻居
			var has_land := false
			for n_tile in grid.get_neighbors(t.coord.x, t.coord.y):
				if n_tile.elevation >= BEACH_LEVEL:
					has_land = true
					break
			# 深水旁有陆地 → 降级为浅水
			if has_land and randf() < 0.7:
				t.set_terrain(HexOverworldTile.TerrainType.SHALLOW_WATER)

	# 确保沙滩旁有浅水而不是深水
	for tile in grid.tiles.values():
		var t2: HexOverworldTile = tile
		if t2.terrain == HexOverworldTile.TerrainType.SAND:
			for n_tile in grid.get_neighbors(t2.coord.x, t2.coord.y):
				if n_tile.terrain == HexOverworldTile.TerrainType.DEEP_WATER:
					if randf() < 0.6:
						n_tile.set_terrain(HexOverworldTile.TerrainType.SHALLOW_WATER)


## ========================================
## 第5步: 地理区域定义
## ========================================

func _define_regions(_width: int, _height: int):
	_regions.clear()

	# 六大区域 (与现有 WorldGenerator 区域对齐)
	# 使用归一化坐标 (q相对grid, r相对grid)

	var mountain := RegionDef.new()
	mountain.name = "霜冠山脉"
	mountain.center_q = 0.5
	mountain.center_r = 0.1
	mountain.radius_q = 0.4
	mountain.radius_r = 0.12
	mountain.danger_level = 0.7
	mountain.preferred_terrains = [HexOverworldTile.TerrainType.MOUNTAIN, HexOverworldTile.TerrainType.SNOW, HexOverworldTile.TerrainType.HILLS]
	_regions.append(mountain)

	var forest := RegionDef.new()
	forest.name = "银叶森林"
	forest.center_q = 0.15
	forest.center_r = 0.45
	forest.radius_q = 0.12
	forest.radius_r = 0.25
	forest.danger_level = 0.3
	forest.preferred_terrains = [HexOverworldTile.TerrainType.FOREST, HexOverworldTile.TerrainType.DENSE_FOREST]
	_regions.append(forest)

	var plains := RegionDef.new()
	plains.name = "中央平原"
	plains.center_q = 0.5
	plains.center_r = 0.5
	plains.radius_q = 0.35
	plains.radius_r = 0.2
	plains.danger_level = 0.1
	plains.preferred_terrains = [HexOverworldTile.TerrainType.PLAINS, HexOverworldTile.TerrainType.GRASSLAND]
	_regions.append(plains)

	var wasteland := RegionDef.new()
	wasteland.name = "焦土荒原"
	wasteland.center_q = 0.75
	wasteland.center_r = 0.85
	wasteland.radius_q = 0.2
	wasteland.radius_r = 0.12
	wasteland.danger_level = 0.8
	wasteland.preferred_terrains = [HexOverworldTile.TerrainType.SAND, HexOverworldTile.TerrainType.SAVANNA]
	_regions.append(wasteland)

	var swamp := RegionDef.new()
	swamp.name = "蛮荒沼泽"
	swamp.center_q = 0.2
	swamp.center_r = 0.85
	swamp.radius_q = 0.18
	swamp.radius_r = 0.12
	swamp.danger_level = 0.5
	swamp.preferred_terrains = [HexOverworldTile.TerrainType.SWAMP]
	_regions.append(swamp)

	var grassland := RegionDef.new()
	grassland.name = "丘陵草原"
	grassland.center_q = 0.85
	grassland.center_r = 0.5
	grassland.radius_q = 0.12
	grassland.radius_r = 0.2
	grassland.danger_level = 0.4
	grassland.preferred_terrains = [HexOverworldTile.TerrainType.SAVANNA, HexOverworldTile.TerrainType.HILLS]
	_regions.append(grassland)


func _assign_region_names() -> void:
	var width: int = grid.grid_width
	var height: int = grid.grid_height

	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		# 归一化坐标 (近似)
		var nq := float(t.coord.x + height / 2) / float(width)
		var nr := float(t.coord.y) / float(height)
		nq = clampf(nq, 0.0, 1.0)
		nr = clampf(nr, 0.0, 1.0)

		# 只在陆地上分配区域名
		if t.terrain == HexOverworldTile.TerrainType.DEEP_WATER or \
		   t.terrain == HexOverworldTile.TerrainType.SHALLOW_WATER:
			continue

		var best_region: RegionDef = null
		var best_score: float = -1.0

		for region in _regions:
			var dq := (nq - region.center_q) / maxf(region.radius_q, 0.01)
			var dr := (nr - region.center_r) / maxf(region.radius_r, 0.01)
			var dist_sq := dq * dq + dr * dr

			# 高斯权重
			var score := exp(-dist_sq * 2.0)

			# 地形匹配加成
			if t.terrain in region.preferred_terrains:
				score *= 1.5

			if score > best_score:
				best_score = score
				best_region = region

		if best_region and best_score > 0.3:
			t.region_name = best_region.name


## ========================================
## 第6步: 河流生成 (A*寻路)
## ========================================

func _generate_rivers() -> void:
	var river_count := randi_range(RIVER_COUNT_MIN, RIVER_COUNT_MAX)
	var astar := HexOverworldAStar.new()
	astar.grid = grid
	astar.ignore_passability = true
	astar.impassable_penalty = 15.0

	# 找到高海拔区域作为河流源头
	var high_tiles: Array[HexOverworldTile] = []
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		if t.elevation > 0.65 and t.is_passable and not t.is_river:
			high_tiles.append(t)

	# 找到水域边缘作为河流入海口
	var water_edge_tiles: Array[HexOverworldTile] = []
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		if t.terrain == HexOverworldTile.TerrainType.SHALLOW_WATER:
			# 检查是否有陆地邻居
			for n in grid.get_neighbors(t.coord.x, t.coord.y):
				if n.is_passable and n.terrain != HexOverworldTile.TerrainType.SAND:
					water_edge_tiles.append(n)
					break

	if high_tiles.is_empty() or water_edge_tiles.is_empty():
		return

	var rivers_placed := 0
	var used_sources: Dictionary = {}

	for _attempt in range(river_count * 5):
		if rivers_placed >= river_count:
			break

		# 随机选源头
		var source := high_tiles[randi() % high_tiles.size()]
		if used_sources.has(source.coord):
			continue

		# 找最近的入海口
		var target := water_edge_tiles[randi() % water_edge_tiles.size()]

		# 用最低高程路径寻路
		var path := astar.find_lowest_elevation_path(source.coord, target.coord)

		if path.size() < RIVER_MIN_LENGTH:
			continue

		# 沿路径标记河流
		_mark_river_path(path)

		used_sources[source.coord] = true
		rivers_placed += 1

	print("[HexOverworldGenerator] 生成 %d 条河流" % rivers_placed)


func _mark_river_path(path: Array[Vector2i]) -> void:
	for i in range(path.size()):
		var coord := path[i]
		var tile := grid.get_tile(coord.x, coord.y)
		if not tile:
			continue

		tile.is_river = true
		tile.set_terrain(HexOverworldTile.TerrainType.RIVER)

		# 设置河流方向
		if i > 0:
			var dir_from := _get_direction(path[i - 1], coord)
			if dir_from >= 0:
				tile.river_directions = tile.set_direction_bit(tile.river_directions, dir_from)
		if i < path.size() - 1:
			var dir_to := _get_direction(coord, path[i + 1])
			if dir_to >= 0:
				tile.river_directions = tile.set_direction_bit(tile.river_directions, dir_to)

		# 河流有概率拓宽: 相邻格变为浅水
		if randf() < 0.2:
			for dir in range(6):
				var n_coord := HexOverworldTile.get_neighbor(coord.x, coord.y, dir)
				var n_tile := grid.get_tile(n_coord.x, n_coord.y)
				if n_tile and n_tile.is_passable and not n_tile.is_river and not n_tile.is_road:
					if randf() < 0.3:
						n_tile.set_terrain(HexOverworldTile.TerrainType.SHALLOW_WATER)
						n_tile.is_river = true


## ========================================
## 第7步: 道路生成 (A*寻路)
## ========================================

func _generate_roads() -> void:
	# 收集所有适合作为道路节点的位置:
	#   - 现有定居点 (来自WorldGenerator的POI)
	#   - 安全区域的中心点
	var road_nodes: Array[Vector2i] = []

	# 策略: 在各区域的可通行中心位置放置虚拟路点
	for region in _regions:
		if region.danger_level > 0.6:
			continue  # 危险区域不修路

		var region_tiles: Array[HexOverworldTile] = []
		for tile in grid.tiles.values():
			var t: HexOverworldTile = tile
			if t.region_name == region.name and t.is_passable and not t.is_river:
				region_tiles.append(t)

		if region_tiles.is_empty():
			continue

		# 取区域中心附近的可通行瓦片作为路点
		var center_idx := region_tiles.size() / 2
		road_nodes.append(region_tiles[center_idx].coord)

	# 再加一些随机路点 (模拟驿站/十字路口)
	var passable := grid.get_passable_tiles()
	for _i in range(road_nodes.size()):
		if passable.is_empty():
			break
		var candidate := passable[randi() % passable.size()]
		if not candidate.is_river:
			road_nodes.append(candidate.coord)

	if road_nodes.size() < 2:
		return

	# 用 Dijkstra 从第一个节点找路径到所有其他节点
	var astar := HexOverworldAStar.new()
	astar.grid = grid
	astar.ignore_passability = false

	# 自定义移动成本: 道路偏好平地, 避开森林/沼泽
	# (通过临时修改 move_cost 实现)
	_apply_road_cost_modifier()

	var paths := astar.find_paths_to_multiple(road_nodes[0], road_nodes.slice(1))

	# 沿路径标记道路
	var roads_placed := 0
	for target_coord in paths:
		var path: Array[Vector2i] = paths[target_coord]
		if path.is_empty():
			continue
		_mark_road_path(path)
		roads_placed += 1

	# 恢复原始 move_cost
	_restore_original_costs()

	print("[HexOverworldGenerator] 生成 %d 条道路" % roads_placed)


func _apply_road_cost_modifier() -> void:
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		# 保存原始值
		t.set_meta("original_move_cost", t.move_cost)

		var cost := t.move_cost
		match t.terrain:
			HexOverworldTile.TerrainType.FOREST:
				cost = ROAD_PENALTY_FOREST
			HexOverworldTile.TerrainType.DENSE_FOREST:
				cost = ROAD_PENALTY_FOREST * 1.5
			HexOverworldTile.TerrainType.HILLS:
				cost = ROAD_PENALTY_HILL
			HexOverworldTile.TerrainType.SWAMP:
				cost = ROAD_PENALTY_SWAMP
			HexOverworldTile.TerrainType.PLAINS:
				cost = 1.0
			HexOverworldTile.TerrainType.GRASSLAND:
				cost = 1.0
			HexOverworldTile.TerrainType.SAVANNA:
				cost = 1.2
		t.move_cost = cost


func _restore_original_costs() -> void:
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		if t.has_meta("original_move_cost"):
			t.move_cost = t.get_meta("original_move_cost")
			t.remove_meta("original_move_cost")


func _mark_road_path(path: Array[Vector2i]) -> void:
	for i in range(path.size()):
		var coord := path[i]
		var tile := grid.get_tile(coord.x, coord.y)
		if not tile:
			continue

		# 不在河流上铺路
		if tile.is_river:
			continue

		tile.is_road = true
		# 道路保留原始地形, 但标记道路覆盖

		# 设置道路方向
		if i > 0:
			var dir_from := _get_direction(path[i - 1], coord)
			if dir_from >= 0:
				tile.road_directions = tile.set_direction_bit(tile.road_directions, dir_from)
		if i < path.size() - 1:
			var dir_to := _get_direction(coord, path[i + 1])
			if dir_to >= 0:
				tile.road_directions = tile.set_direction_bit(tile.road_directions, dir_to)

		# 更新通行性: 道路降低移动成本
		tile.move_cost = 0.5


## ========================================
## 第8步: 后处理
## ========================================

func _finalize_terrain() -> void:
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		# 确保不可通行地形的 is_passable 和 move_cost 正确
		t._update_terrain_properties()

		# 道路覆盖
		if t.is_road and t.is_passable:
			t.move_cost = 0.5

		# 确保河流不可通行
		if t.is_river:
			t.is_passable = false
			t.move_cost = 99.0


## ========================================
## 辅助方法
## ========================================

## 获取两相邻瓦片之间的方向 (0-5), 使用 Cube 方向表查找
func _get_direction(from: Vector2i, to: Vector2i) -> int:
	var diff_cube := HexOverworldTile.axial_to_cube(to.x, to.y) - HexOverworldTile.axial_to_cube(from.x, from.y)
	for i in range(6):
		if diff_cube == HexOverworldTile.CUBE_DIRECTIONS[i]:
			return i
	return -1


## 获取已定义的区域列表 (供 WorldGenerator 使用)
func get_regions() -> Array[RegionDef]:
	return _regions


## 获取网格 (生成后的结果)
func get_grid() -> HexOverworldGrid:
	return grid


## ========================================
## 增量操作: 在已生成的地图上放置POI
## ========================================

## 在指定区域放置一个定居点, 并返回所在瓦片
func place_settlement_at(q: int, r: int, poi_type: int, poi_name: String) -> HexOverworldTile:
	var tile := grid.get_tile(q, r)
	if not tile:
		return null

	# 确保可通行
	if not tile.is_passable:
		# 搜索附近可通行格
		tile = grid.find_passable_near_pixel(tile.pixel_pos.x, tile.pixel_pos.y, 5)
		if not tile:
			return null

	tile.has_settlement = true
	tile.settlement_type = poi_type
	tile.poi_id = poi_name
	return tile


## 在指定区域内随机找一个适合放置定居点的瓦片
func find_settlement_position(region_name: String, min_distance: int = 10) -> HexOverworldTile:
	var candidates: Array[HexOverworldTile] = []

	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		if t.region_name != region_name:
			continue
		if not t.is_passable:
			continue
		if t.is_river:
			continue
		if t.has_settlement:
			continue
		# 偏好平原和草地
		if t.terrain == HexOverworldTile.TerrainType.PLAINS or \
		   t.terrain == HexOverworldTile.TerrainType.GRASSLAND or \
		   t.terrain == HexOverworldTile.TerrainType.SAVANNA:
			candidates.append(t)

	if candidates.is_empty():
		# 放宽条件
		for tile in grid.tiles.values():
			var t2: HexOverworldTile = tile
			if t2.region_name == region_name and t2.is_passable and not t2.has_settlement:
				candidates.append(t2)

	if candidates.is_empty():
		return null

	# 随机选, 但检查距离
	candidates.shuffle()
	var placed: Array[Vector2i] = []
	for tile in grid.get_settlement_tiles():
		placed.append(tile.coord)

	for candidate in candidates:
		var too_close := false
		for p in placed:
			if HexOverworldTile.hex_distance(candidate.coord.x, candidate.coord.y, p.x, p.y) < min_distance:
				too_close = true
				break
		if not too_close:
			return candidate

	# 距离限制放宽
	return candidates[0] if not candidates.is_empty() else null
