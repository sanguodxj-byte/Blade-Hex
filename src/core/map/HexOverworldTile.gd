# HexOverworldTile.gd
# 大地图六边形瓦片数据模型 — 存储单个六角格的完整地形信息
# 战场兄弟风格: 大量瓦片在游戏世界初始化时按规则和逻辑生成
# 纯数据类, 不挂载场景树, 用于生成/寻路/渲染/存档
#
# 坐标约定:
#   存储: Axial (q, r) — 两分量, 紧凑, Dictionary key
#   内部计算: Cube (q, r, s) — s = -q - r, 距离/方向/插值对称且线性
#   像素: 平顶六边形, x = size * 3/2 * q, y = size * (√3/2 * q + √3 * r)
class_name HexOverworldTile
extends RefCounted


## ========================================
## 地形类型枚举
## ========================================
enum TerrainType {
	DEEP_WATER,    ## 深水 — 不可通行
	SHALLOW_WATER, ## 浅水 — 缓慢, 仅船只
	SAND,          ## 沙滩/荒漠
	PLAINS,        ## 平原
	GRASSLAND,     ## 草地
	FOREST,        ## 森林
	DENSE_FOREST,  ## 密林
	JUNGLE,        ## 丛林 (炎热湿润)
	TAIGA,         ## 针叶林 (寒冷干燥/湿润)
	BOG,           ## 冻土沼泽 (寒冷潮湿)
	SWAMP,         ## 沼泽 (温带/炎热潮湿)
	SAVANNA,       ## 稀树草原 (炎热干燥)
	WASTELAND,     ## 荒原 (温带极干)
	ROCKY,         ## 岩石荒地 (寒冷极干)
	HILLS,         ## 丘陵
	MOUNTAIN,      ## 山地 — 不可通行
	MOUNTAIN_SNOW, ## 雪山 — 不可通行
	SNOW,          ## 雪地
	ICE,           ## 冰原 (极寒极干)
	ROAD,          ## 道路 — 快速通行
	RIVER,         ## 河流 — 不可通行, 天然屏障
}


## ========================================
## 六边形外径 (大地图专用, 与纹理像素 313×313 对齐)
## 平顶六边形: 格宽 = 1.5 × HEX_SIZE ≈ 234, 纹理宽 313px 以 1:1 显示
## ========================================
const HEX_SIZE: float = 156.0


## ========================================
## Cube 方向表 — 6个邻居在 Cube 空间的偏移
## 扁平顶(flat-top): +q = 右, +r = 右下, +s(-q-r) = 左下
## ========================================
const CUBE_DIRECTIONS: Array[Vector3i] = [
	Vector3i(+1, 0, -1),  ## 0: 东 (右)
	Vector3i(+1, -1, 0),  ## 1: 东北 (右上)
	Vector3i(0, -1, +1),  ## 2: 西北 (左上)
	Vector3i(-1, 0, +1),  ## 3: 西 (左)
	Vector3i(-1, +1, 0),  ## 4: 西南 (左下)
	Vector3i(0, +1, -1),  ## 5: 东南 (右下)
]


## ========================================
## 坐标与空间 (存储用 Axial)
## ========================================
var coord: Vector2i = Vector2i.ZERO        ## 轴向坐标 (q, r) — 存储/序列化
var pixel_pos: Vector2 = Vector2.ZERO      ## 预计算的世界像素坐标


## ========================================
## 地形属性
## ========================================
var terrain: TerrainType = TerrainType.PLAINS
var elevation: float = 0.0                 ## 连续高程 [0, 1], 来自噪声
var moisture: float = 0.5                  ## 湿度 [0, 1], 来自第二层噪声
var temperature: float = 0.5               ## 温度 [0, 1], 纬度 + 噪声


## ========================================
## 寻路与通行
## ========================================
var is_passable: bool = true
var move_cost: float = 1.0


## ========================================
## 线性特征 (道路/河流)
## ========================================
var is_road: bool = false
var is_river: bool = false
var road_directions: int = 0               ## 道路连接方向位掩码 (bit 0-5)
var river_directions: int = 0              ## 河流流向位掩码 (bit 0-5)


## ========================================
## 兴趣点与定居点
## ========================================
var has_settlement: bool = false
var settlement_type: int = 0               ## 映射到 OverworldPOI.POIType
var poi_id: String = ""                    ## 引用 OverworldPOI 实例
var region_name: String = ""               ## 所属地理区域名称


## ========================================
## 战争迷雾
## ========================================
var visibility: int = 0                    ## 0=未探索, 1=已探索, 2=当前可见


## ========================================
## 工厂方法
## ========================================

## 创建一个完整初始化的瓦片
static func create(q: int, r: int, p_terrain: TerrainType, p_elev: float, p_moist: float, p_temp: float) -> HexOverworldTile:
	var tile = HexOverworldTile.new()
	tile.coord = Vector2i(q, r)
	tile.pixel_pos = axial_to_pixel(q, r)
	tile.terrain = p_terrain
	tile.elevation = p_elev
	tile.moisture = p_moist
	tile.temperature = p_temp
	tile._update_terrain_properties()
	return tile


## 创建空白瓦片 (仅设坐标)
static func create_empty(q: int, r: int) -> HexOverworldTile:
	var tile = HexOverworldTile.new()
	tile.coord = Vector2i(q, r)
	tile.pixel_pos = axial_to_pixel(q, r)
	return tile


## ========================================
## Cube 坐标工具 (所有内部计算)
## ========================================

## Axial → Cube: s = -q - r
static func axial_to_cube(q: int, r: int) -> Vector3i:
	return Vector3i(q, r, -q - r)


## Cube → Axial: 丢弃 s
static func cube_to_axial(cube: Vector3i) -> Vector2i:
	return Vector2i(cube.x, cube.y)


## 获取邻居坐标 (Axial 返回值, Cube 内部计算)
static func get_neighbor(q: int, r: int, direction: int) -> Vector2i:
	var d := direction % 6
	if d < 0:
		d += 6
	var cube := axial_to_cube(q, r)
	var offset: Vector3i = CUBE_DIRECTIONS[d]
	return cube_to_axial(cube + offset)


## 获取邻居的 Cube 坐标
static func get_neighbor_cube(cube: Vector3i, direction: int) -> Vector3i:
	var d := direction % 6
	if d < 0:
		d += 6
	return cube + CUBE_DIRECTIONS[d]


## Cube 距离: max(|dq|, |dr|, |ds|) — 对称且线性
static func cube_distance(a: Vector3i, b: Vector3i) -> int:
	return max(max(absi(a.x - b.x), absi(a.y - b.y)), absi(a.z - b.z))


## Axial 距离 (转 Cube 计算)
static func hex_distance(q1: int, r1: int, q2: int, r2: int) -> int:
	return cube_distance(axial_to_cube(q1, r1), axial_to_cube(q2, r2))


## Cube 舍入: 浮点 Cube → 整数 Cube (保持 q+r+s=0 约束)
static func cube_round(fq: float, fr: float, fs: float) -> Vector3i:
	var rq: float = round(fq)
	var rr: float = round(fr)
	var rs: float = round(fs)

	var q_diff := absf(rq - fq)
	var r_diff := absf(rr - fr)
	var s_diff := absf(rs - fs)

	# 修正舍入误差: 改变误差最大分量以保持约束
	if q_diff > r_diff and q_diff > s_diff:
		rq = -rr - rs
	elif r_diff > s_diff:
		rr = -rq - rs
	else:
		rs = -rq - rr

	return Vector3i(int(rq), int(rr), int(rs))


## Axial 舍入 (转 Cube)
static func axial_round(fq: float, fr: float) -> Vector2i:
	var fs: float = -fq - fr
	var cube := cube_round(fq, fr, fs)
	return cube_to_axial(cube)


## Cube 线性插值 (用于画线/河流走向)
static func cube_lerp(a: Vector3i, b: Vector3i, t: float) -> Vector3:
	return Vector3(
		lerpf(float(a.x), float(b.x), t),
		lerpf(float(a.y), float(b.y), t),
		lerpf(float(a.z), float(b.z), t)
	)


## Cube 画线: 从 a 到 b 画一条六角格线 (用于河流/道路走向参考)
## 算法: Redblobgames cube line drawing, nudge + lerp + round
static func cube_line(a: Vector3i, b: Vector3i) -> Array[Vector2i]:
	var n := cube_distance(a, b)
	var results: Array[Vector2i] = []
	if n == 0:
		results.append(cube_to_axial(a))
		return results

	# Nudge 避免端点落在六角格边界导致舍入歧义
	var a_nudge := Vector3(float(a.x) + 1e-6, float(a.y) + 1e-6, float(a.z) - 2e-6)
	var b_nudge := Vector3(float(b.x) + 1e-6, float(b.y) + 1e-6, float(b.z) - 2e-6)

	var step := 1.0 / float(n)
	for i in range(n + 1):
		var t_val := float(i) * step
		var fq := lerpf(a_nudge.x, b_nudge.x, t_val)
		var fr := lerpf(a_nudge.y, b_nudge.y, t_val)
		var fs := lerpf(a_nudge.z, b_nudge.z, t_val)
		results.append(cube_to_axial(cube_round(fq, fr, fs)))

	return results


## 绕原点的六角环: 半径 ring_r 的所有格子 (Cube 空间)
static func cube_ring(center: Vector3i, ring_r: int) -> Array[Vector3i]:
	if ring_r == 0:
		return [center]
	var results: Array[Vector3i] = []
	var current := center + CUBE_DIRECTIONS[4] * ring_r  # 起始: 西南方向
	for side in range(6):
		for step in range(ring_r):
			results.append(current)
			current = get_neighbor_cube(current, side)
	return results


## Axial 环
static func hex_ring(q: int, r: int, radius: int) -> Array[Vector2i]:
	var ring := cube_ring(axial_to_cube(q, r), radius)
	var results: Array[Vector2i] = []
	for c in ring:
		results.append(cube_to_axial(c))
	return results


## ========================================
## 像素坐标转换 (基于配置组件)
## ========================================

static var _layout: HexLayoutConfig = null

static func get_layout() -> HexLayoutConfig:
	if _layout == null:
		_layout = HexLayoutConfig.new()
		# 默认使用通过目测对齐的参数
		_layout.tex_width = 313.0
		_layout.tex_height = 313.0
		_layout.q_vector = Vector2(-136.00, -175.07)
		_layout.r_vector = Vector2(-267.75, -0.53)
	return _layout

## 允许全局替换布局（例如在加载不同地图或更换材质包时）
static func set_layout(layout: HexLayoutConfig) -> void:
	_layout = layout

## 轴向 → 像素 (通过布局组件处理)
static func axial_to_pixel(q: int, r: int) -> Vector2:
	return get_layout().axial_to_pixel(q, r)


## 像素 → 分数轴向 (通过布局组件逆矩阵处理)
static func pixel_to_fractional_axial(px: float, py: float) -> Vector2:
	return get_layout().pixel_to_fractional_axial(px, py)


## 像素 → 最近六角格 Axial 坐标
static func pixel_to_axial(px: float, py: float) -> Vector2i:
	var frac := pixel_to_fractional_axial(px, py)
	return axial_round(frac.x, frac.y)


## ========================================
## 地形属性自动计算
## ========================================

func _update_terrain_properties() -> void:
	match terrain:
		TerrainType.DEEP_WATER:
			is_passable = false;  move_cost = 99.0
		TerrainType.SHALLOW_WATER:
			is_passable = true;   move_cost = 3.0
		TerrainType.SAND:
			is_passable = true;   move_cost = 1.5
		TerrainType.PLAINS:
			is_passable = true;   move_cost = 1.0
		TerrainType.GRASSLAND:
			is_passable = true;   move_cost = 1.0
		TerrainType.FOREST:
			is_passable = true;   move_cost = 1.5
		TerrainType.DENSE_FOREST:
			is_passable = true;   move_cost = 2.5
		TerrainType.HILLS:
			is_passable = true;   move_cost = 2.0
		TerrainType.MOUNTAIN:
			is_passable = false;  move_cost = 99.0
		TerrainType.SNOW:
			is_passable = true;   move_cost = 2.0
		TerrainType.SWAMP:
			is_passable = true;   move_cost = 2.5
		TerrainType.SAVANNA:
			is_passable = true;   move_cost = 1.0
		TerrainType.ROAD:
			is_passable = true;   move_cost = 0.5
		TerrainType.RIVER:
			is_passable = false;  move_cost = 99.0


func set_terrain(new_terrain: TerrainType) -> void:
	terrain = new_terrain
	_update_terrain_properties()


## ========================================
## 方向位操作
## ========================================

func has_direction_bit(directions: int, dir: int) -> bool:
	return (directions & (1 << dir)) != 0

func set_direction_bit(directions_var: int, dir: int) -> int:
	return directions_var | (1 << dir)


## ========================================
## 渲染色 & 纹理资源
## ========================================

## 地形纹理根目录
const TEXTURE_BASE_PATH: String = "res://src/assets/tiles/hex_terrain"

## 获取地形基础纹理文件名前缀 (不含 _N.png 后缀)
static func terrain_texture_name(t: TerrainType) -> String:
	match t:
		TerrainType.DEEP_WATER:    return "pond"
		TerrainType.SHALLOW_WATER: return "pond"
		TerrainType.SAND:          return "wasteland"
		TerrainType.PLAINS:        return "grassland"
		TerrainType.GRASSLAND:     return "grassland"
		TerrainType.FOREST:        return "forest"
		TerrainType.DENSE_FOREST:  return "forest"
		TerrainType.HILLS:         return "rocky_land"
		TerrainType.MOUNTAIN:      return "mountain_cave"
		TerrainType.SNOW:          return "mountain_cave"
		TerrainType.SWAMP:         return "swamp"
		TerrainType.SAVANNA:       return "barren_land"
		TerrainType.ROAD:          return "crossroads"
		TerrainType.RIVER:         return "pond"
		_:                         return "grassland"

## 获取该地形类型的最大变体数 (实际存在的 _N.png 文件数)
static func terrain_variant_count(t: TerrainType) -> int:
	match t:
		TerrainType.DEEP_WATER:    return 1
		TerrainType.SHALLOW_WATER: return 1
		TerrainType.SAND:          return 2
		TerrainType.PLAINS:        return 1
		TerrainType.GRASSLAND:     return 1
		TerrainType.FOREST:        return 3
		TerrainType.DENSE_FOREST:  return 3
		TerrainType.HILLS:         return 2
		TerrainType.MOUNTAIN:      return 2
		TerrainType.SNOW:          return 2
		TerrainType.SWAMP:         return 3
		TerrainType.SAVANNA:       return 2
		TerrainType.ROAD:          return 1
		TerrainType.RIVER:         return 1
		_:                         return 1

## 获取叠加层纹理文件名前缀
static func overlay_texture_name(overlay_type: String) -> String:
	match overlay_type:
		"road":       return "crossroads"
		"river":      return "bridge"
		"settlement": return "village"
		"town":       return "castle"
		"fort":       return "fort"
		"market":     return "market"
		"mine":       return "mine"
		"ruins":      return "ruins"
		"docks":      return "docks"
		"camp":       return "camp"
		"farmland":   return "farmland"
		"quarry":     return "quarry"
		"graveyard":  return "graveyard"
		_:            return ""

## 获取叠加层变体数
static func overlay_variant_count(overlay_type: String) -> int:
	match overlay_type:
		"road":       return 1
		"river":      return 1
		"settlement": return 3
		"town":       return 1
		"fort":       return 4
		"market":     return 1
		"mine":       return 2
		"ruins":      return 7
		"docks":      return 3
		"camp":       return 2
		"farmland":   return 3
		"quarry":     return 2
		"graveyard":  return 1
		_:            return 1

## 获取完整纹理路径
static func get_terrain_texture_path(t: TerrainType, variant: int = 0) -> String:
	var name := terrain_texture_name(t)
	var max_v := terrain_variant_count(t)
	return "%s/%s_%d.png" % [TEXTURE_BASE_PATH, name, variant % maxi(max_v, 1)]

## 获取叠加层纹理路径
static func get_overlay_texture_path(overlay_type: String, variant: int = 0) -> String:
	var name := overlay_texture_name(overlay_type)
	if name == "":
		return ""
	var max_v := overlay_variant_count(overlay_type)
	return "%s/%s_%d.png" % [TEXTURE_BASE_PATH, name, variant % maxi(max_v, 1)]


func get_terrain_color() -> Color:
	return _terrain_color_map(terrain)

func get_terrain_color_with_height() -> Color:
	var base := get_terrain_color()
	var height_tweak := elevation * 0.15
	return Color(
		clampf(base.r + height_tweak, 0.0, 1.0),
		clampf(base.g + height_tweak, 0.0, 1.0),
		clampf(base.b + height_tweak * 0.5, 0.0, 1.0)
	)


## ========================================
## 序列化 (Axial: q, r)
## ========================================

func serialize() -> Dictionary:
	return {
		"q": coord.x,
		"r": coord.y,
		"terrain": terrain,
		"elevation": elevation,
		"moisture": moisture,
		"temperature": temperature,
		"is_road": is_road,
		"is_river": is_river,
		"road_dirs": road_directions,
		"river_dirs": river_directions,
		"has_settlement": has_settlement,
		"settlement_type": settlement_type,
		"poi_id": poi_id,
		"region_name": region_name,
		"visibility": visibility,
	}


static func deserialize(data: Dictionary) -> HexOverworldTile:
	var tile = HexOverworldTile.new()
	tile.coord = Vector2i(int(data.get("q", 0)), int(data.get("r", 0)))
	tile.pixel_pos = axial_to_pixel(tile.coord.x, tile.coord.y)
	tile.terrain = int(data.get("terrain", TerrainType.PLAINS))
	tile.elevation = float(data.get("elevation", 0.0))
	tile.moisture = float(data.get("moisture", 0.5))
	tile.temperature = float(data.get("temperature", 0.5))
	tile.is_road = bool(data.get("is_road", false))
	tile.is_river = bool(data.get("is_river", false))
	tile.road_directions = int(data.get("road_dirs", 0))
	tile.river_directions = int(data.get("river_dirs", 0))
	tile.has_settlement = bool(data.get("has_settlement", false))
	tile.settlement_type = int(data.get("settlement_type", 0))
	tile.poi_id = str(data.get("poi_id", ""))
	tile.region_name = str(data.get("region_name", ""))
	tile.visibility = int(data.get("visibility", 0))
	tile._update_terrain_properties()
	return tile


## ========================================
## 工具
## ========================================

static func terrain_to_string(t: TerrainType) -> String:
	match t:
		TerrainType.DEEP_WATER:    return "深水"
		TerrainType.SHALLOW_WATER: return "浅水"
		TerrainType.SAND:          return "荒漠"
		TerrainType.PLAINS:        return "平原"
		TerrainType.GRASSLAND:     return "草地"
		TerrainType.FOREST:        return "森林"
		TerrainType.DENSE_FOREST:  return "密林"
		TerrainType.JUNGLE:        return "丛林"
		TerrainType.TAIGA:         return "针叶林"
		TerrainType.BOG:           return "冻土沼泽"
		TerrainType.HILLS:         return "丘陵"
		TerrainType.MOUNTAIN:      return "山脉"
		TerrainType.MOUNTAIN_SNOW: return "雪山"
		TerrainType.SNOW:          return "雪地"
		TerrainType.ICE:           return "冰原"
		TerrainType.SWAMP:         return "沼泽"
		TerrainType.SAVANNA:       return "稀树草原"
		TerrainType.WASTELAND:     return "荒原"
		TerrainType.ROCKY:         return "岩石荒地"
		TerrainType.ROAD:          return "道路"
		TerrainType.RIVER:         return "河流"
		_:                         return "未知"


static func _terrain_color_map(t: TerrainType) -> Color:
	match t:
		TerrainType.DEEP_WATER:    return Color(0.18, 0.30, 0.55)
		TerrainType.SHALLOW_WATER: return Color(0.30, 0.45, 0.70)
		TerrainType.SAND:          return Color(0.85, 0.75, 0.50)
		TerrainType.PLAINS:        return Color(0.72, 0.68, 0.48)
		TerrainType.GRASSLAND:     return Color(0.55, 0.70, 0.35)
		TerrainType.FOREST:        return Color(0.22, 0.45, 0.18)
		TerrainType.DENSE_FOREST:  return Color(0.12, 0.30, 0.08)
		TerrainType.JUNGLE:        return Color(0.15, 0.35, 0.10)
		TerrainType.TAIGA:         return Color(0.25, 0.35, 0.30)
		TerrainType.BOG:           return Color(0.35, 0.40, 0.38)
		TerrainType.HILLS:         return Color(0.58, 0.52, 0.38)
		TerrainType.MOUNTAIN:      return Color(0.40, 0.38, 0.42)
		TerrainType.MOUNTAIN_SNOW: return Color(0.85, 0.88, 0.92)
		TerrainType.SNOW:          return Color(0.92, 0.95, 0.98)
		TerrainType.ICE:           return Color(0.75, 0.85, 0.95)
		TerrainType.SWAMP:         return Color(0.38, 0.48, 0.28)
		TerrainType.SAVANNA:       return Color(0.70, 0.65, 0.30)
		TerrainType.WASTELAND:     return Color(0.65, 0.55, 0.45)
		TerrainType.ROCKY:         return Color(0.45, 0.45, 0.50)
		TerrainType.ROAD:          return Color(0.65, 0.55, 0.38)
		TerrainType.RIVER:         return Color(0.25, 0.42, 0.68)
		_:                         return Color(0.5, 0.5, 0.5)
