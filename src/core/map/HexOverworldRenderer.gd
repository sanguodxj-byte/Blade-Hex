# HexOverworldRenderer.gd
# 六边形大地图2D渲染器 — preload 纹理 + 分 chunk 按需渲染
#
# 纹理 313×313 以原始像素显示 (render_scale = 1.0)
# 地图按 chunk (8×8 瓦片) 分块管理, 只实例化视口内的 chunk
# 每个 chunk 是一个 Node2D 容器, 包含该 chunk 内所有瓦片的 Sprite
#
# 渲染层级:
#   Layer 0: 地形底图 Sprite
#   Layer 1: 叠加 Sprite (道路/河流/定居点)
#   Layer 2: 迷雾覆盖 (ColorRect)
class_name HexOverworldRenderer
extends Node2D


## ========================================
## preload 所有地形纹理 — 编辑时解析, 运行时零开销
## ========================================

const _TEX_PATHS := {
	# 地形底图
	"grassland_0": "res://src/assets/tiles/hex_terrain/grassland_0.png",
	"forest_0": "res://src/assets/tiles/hex_terrain/forest_0.png",
	"forest_1": "res://src/assets/tiles/hex_terrain/forest_1.png",
	"forest_2": "res://src/assets/tiles/hex_terrain/forest_2.png",
	"rocky_land_0": "res://src/assets/tiles/hex_terrain/rocky_land_0.png",
	"rocky_land_1": "res://src/assets/tiles/hex_terrain/rocky_land_1.png",
	"mountain_cave_0": "res://src/assets/tiles/hex_terrain/mountain_cave_0.png",
	"mountain_cave_1": "res://src/assets/tiles/hex_terrain/mountain_cave_1.png",
	"swamp_0": "res://src/assets/tiles/hex_terrain/swamp_0.png",
	"swamp_1": "res://src/assets/tiles/hex_terrain/swamp_1.png",
	"swamp_2": "res://src/assets/tiles/hex_terrain/swamp_2.png",
	"barren_land_0": "res://src/assets/tiles/hex_terrain/barren_land_0.png",
	"barren_land_1": "res://src/assets/tiles/hex_terrain/barren_land_1.png",
	"wasteland_0": "res://src/assets/tiles/hex_terrain/wasteland_0.png",
	"wasteland_1": "res://src/assets/tiles/hex_terrain/wasteland_1.png",
	"pond_0": "res://src/assets/tiles/hex_terrain/pond_0.png",
	# 叠加层
	"crossroads_0": "res://src/assets/tiles/hex_terrain/crossroads_0.png",
	"bridge_0": "res://src/assets/tiles/hex_terrain/bridge_0.png",
	"village_0": "res://src/assets/tiles/hex_terrain/village_0.png",
	"village_1": "res://src/assets/tiles/hex_terrain/village_1.png",
	"village_2": "res://src/assets/tiles/hex_terrain/village_2.png",
	"castle_0": "res://src/assets/tiles/hex_terrain/castle_0.png",
	"fort_0": "res://src/assets/tiles/hex_terrain/fort_0.png",
	"fort_1": "res://src/assets/tiles/hex_terrain/fort_1.png",
	"fort_2": "res://src/assets/tiles/hex_terrain/fort_2.png",
	"fort_3": "res://src/assets/tiles/hex_terrain/fort_3.png",
	"market_0": "res://src/assets/tiles/hex_terrain/market_0.png",
	"mine_0": "res://src/assets/tiles/hex_terrain/mine_0.png",
	"mine_1": "res://src/assets/tiles/hex_terrain/mine_1.png",
	"ruins_0": "res://src/assets/tiles/hex_terrain/ruins_0.png",
	"ruins_1": "res://src/assets/tiles/hex_terrain/ruins_1.png",
	"ruins_2": "res://src/assets/tiles/hex_terrain/ruins_2.png",
	"ruins_3": "res://src/assets/tiles/hex_terrain/ruins_3.png",
	"ruins_4": "res://src/assets/tiles/hex_terrain/ruins_4.png",
	"ruins_5": "res://src/assets/tiles/hex_terrain/ruins_5.png",
	"ruins_6": "res://src/assets/tiles/hex_terrain/ruins_6.png",
	"docks_0": "res://src/assets/tiles/hex_terrain/docks_0.png",
	"docks_1": "res://src/assets/tiles/hex_terrain/docks_1.png",
	"docks_2": "res://src/assets/tiles/hex_terrain/docks_2.png",
	"camp_0": "res://src/assets/tiles/hex_terrain/camp_0.png",
	"camp_1": "res://src/assets/tiles/hex_terrain/camp_1.png",
	"farmland_0": "res://src/assets/tiles/hex_terrain/farmland_0.png",
	"farmland_1": "res://src/assets/tiles/hex_terrain/farmland_1.png",
	"farmland_2": "res://src/assets/tiles/hex_terrain/farmland_2.png",
	"quarry_0": "res://src/assets/tiles/hex_terrain/quarry_0.png",
	"quarry_1": "res://src/assets/tiles/hex_terrain/quarry_1.png",
	"graveyard_0": "res://src/assets/tiles/hex_terrain/graveyard_0.png",
}

## preload 字典 — 在脚本加载时一次性完成
var _preloaded: Dictionary = {}

## 地形类型 → 纹理数组 (运行时快速查询)
var _terrain_tex: Dictionary = {}
var _overlay_tex: Dictionary = {}

## 共享着色器材质 — 地形边缘混合
var _terrain_material: ShaderMaterial = null


## ========================================
## 配置
## ========================================

var grid: HexOverworldGrid = null
var fog: FogOfWar = null
var _initialized: bool = false
var _offset_x: float = 0.0
var _offset_y: float = 0.0
var map_pixel_width: float = 0.0
var map_pixel_height: float = 0.0


## ========================================
## Chunk 系统 — 按需加载/卸载
## ========================================

## 每个 chunk 包含 CHUNK_SIZE × CHUNK_SIZE 个瓦片
const CHUNK_SIZE: int = 8

## chunk_key (String "cx,cy") → Node2D 容器
var _active_chunks: Dictionary = {}
## 当前帧的视口矩形 (世界坐标)
var _last_visible_rect: Rect2 = Rect2()
var _camera: Camera2D = null


## ========================================
## 初始化
## ========================================

func _ready() -> void:
	if _preloaded.is_empty():
		_init_terrain_material()
		_preload_all_textures()
		_build_terrain_lookup()


## 加载地形边缘混合着色器，创建共享材质
func _init_terrain_material() -> void:
	var shader := load("res://src/assets/shaders/terrain_edge_blend.gdshader") as Shader
	if shader:
		_terrain_material = ShaderMaterial.new()
		_terrain_material.shader = shader
		print("[HexOverworldRenderer] 地形边缘混合着色器加载成功")
	else:
		push_warning("[HexOverworldRenderer] 地形边缘混合着色器加载失败")


func _preload_all_textures() -> void:
	_preloaded.clear()
	var fail_count: int = 0
	for key in _TEX_PATHS:
		var tex := load(_TEX_PATHS[key]) as Texture2D
		if tex:
			_preloaded[key] = _crop_transparent(tex)
		else:
			fail_count += 1
	print("[HexOverworldRenderer] preload: %d/%d 成功, %d 失败" % [_preloaded.size(), _TEX_PATHS.size(), fail_count])


## 裁剪纹理周围透明像素，返回居中的新纹理
func _crop_transparent(tex: Texture2D) -> ImageTexture:
	var img := tex.get_image()
	var w := img.get_width()
	var h := img.get_height()
	
	# 找非透明区域边界
	var min_x := w
	var max_x := 0
	var min_y := h
	var max_y := 0
	
	for y in range(h):
		for x in range(w):
			if img.get_pixel(x, y).a > 0.01:
				if x < min_x: min_x = x
				if x > max_x: max_x = x
				if y < min_y: min_y = y
				if y > max_y: max_y = y
	
	# 裁剪
	var crop_w := max_x - min_x + 1
	var crop_h := max_y - min_y + 1
	var region := Rect2i(min_x, min_y, crop_w, crop_h)
	var cropped := img.get_region(region)
	
	var new_tex := ImageTexture.create_from_image(cropped)
	return new_tex


## 将 preload 字典映射到 地形枚举 → 纹理数组 的快速查询表
func _build_terrain_lookup() -> void:
	_terrain_tex.clear()
	_overlay_tex.clear()

	_terrain_tex[HexOverworldTile.TerrainType.DEEP_WATER]    = [_safe("pond_0")]
	_terrain_tex[HexOverworldTile.TerrainType.SHALLOW_WATER] = [_safe("pond_0")]
	_terrain_tex[HexOverworldTile.TerrainType.SAND]          = [_safe("wasteland_0"), _safe("wasteland_1")]
	_terrain_tex[HexOverworldTile.TerrainType.PLAINS]        = [_safe("grassland_0")]
	_terrain_tex[HexOverworldTile.TerrainType.GRASSLAND]     = [_safe("grassland_0")]
	_terrain_tex[HexOverworldTile.TerrainType.FOREST]        = [_safe("forest_0"), _safe("forest_1"), _safe("forest_2")]
	_terrain_tex[HexOverworldTile.TerrainType.DENSE_FOREST]  = [_safe("forest_0"), _safe("forest_1"), _safe("forest_2")]
	_terrain_tex[HexOverworldTile.TerrainType.HILLS]         = [_safe("rocky_land_0"), _safe("rocky_land_1")]
	_terrain_tex[HexOverworldTile.TerrainType.MOUNTAIN]      = [_safe("mountain_cave_0"), _safe("mountain_cave_1")]
	_terrain_tex[HexOverworldTile.TerrainType.SNOW]          = [_safe("mountain_cave_0"), _safe("mountain_cave_1")]
	_terrain_tex[HexOverworldTile.TerrainType.SWAMP]         = [_safe("swamp_0"), _safe("swamp_1"), _safe("swamp_2")]
	_terrain_tex[HexOverworldTile.TerrainType.SAVANNA]       = [_safe("barren_land_0"), _safe("barren_land_1")]
	_terrain_tex[HexOverworldTile.TerrainType.ROAD]          = [_safe("crossroads_0")]
	_terrain_tex[HexOverworldTile.TerrainType.RIVER]         = [_safe("pond_0")]

	_overlay_tex["road"]       = [_safe("crossroads_0")]
	_overlay_tex["river"]      = [_safe("bridge_0")]
	_overlay_tex["settlement"] = [_safe("village_0"), _safe("village_1"), _safe("village_2")]
	_overlay_tex["town"]       = [_safe("castle_0")]
	_overlay_tex["fort"]       = [_safe("fort_0"), _safe("fort_1"), _safe("fort_2"), _safe("fort_3")]
	_overlay_tex["market"]     = [_safe("market_0")]
	_overlay_tex["mine"]       = [_safe("mine_0"), _safe("mine_1")]
	_overlay_tex["ruins"]      = [_safe("ruins_0"), _safe("ruins_1"), _safe("ruins_2"), _safe("ruins_3"), _safe("ruins_4"), _safe("ruins_5"), _safe("ruins_6")]
	_overlay_tex["docks"]      = [_safe("docks_0"), _safe("docks_1"), _safe("docks_2")]
	_overlay_tex["camp"]       = [_safe("camp_0"), _safe("camp_1")]
	_overlay_tex["farmland"]   = [_safe("farmland_0"), _safe("farmland_1"), _safe("farmland_2")]
	_overlay_tex["quarry"]     = [_safe("quarry_0"), _safe("quarry_1")]
	_overlay_tex["graveyard"]  = [_safe("graveyard_0")]

	# 移除 preload 失败的 null 条目
	for key in _terrain_tex:
		var filtered: Array = []
		for v in _terrain_tex[key]:
			if v != null:
				filtered.append(v)
		_terrain_tex[key] = filtered
	for key in _overlay_tex:
		var filtered: Array = []
		for v in _overlay_tex[key]:
			if v != null:
				filtered.append(v)
		_overlay_tex[key] = filtered

	var terrain_total: int = 0
	for key in _terrain_tex:
		terrain_total += _terrain_tex[key].size()
	var overlay_total: int = 0
	for key in _overlay_tex:
		overlay_total += _overlay_tex[key].size()
	print("[HexOverworldRenderer] lookup 表: %d 地形纹理 + %d 叠加纹理" % [terrain_total, overlay_total])


func _safe(key: String):
	return _preloaded.get(key)


func _lookup_tex(key: String):
	return _preloaded.get(key)


## ========================================
## 外部接口
## ========================================

func setup(pgrid: HexOverworldGrid) -> void:
	_init_terrain_material()
	_preload_all_textures()
	_build_terrain_lookup()

	grid = pgrid
	_calculate_offset()
	_calculate_map_bounds()
	_initialized = true
	
	# 直接渲染全部瓦片（不用chunk）
	_render_all_tiles()
	
	print("[HexOverworldRenderer] 初始化完成: %d 纹理, 地图 %.0f×%.0f px" % [
		_preloaded.size(), map_pixel_width, map_pixel_height])


## ========================================
## 坐标偏移和边界
## ========================================

func _calculate_offset() -> void:
	if not grid or grid.tiles.is_empty():
		return
	# 确保纹理尺寸已测量
	_tile_render_pos(0, 0)
	var min_x: float = 999999.0
	var min_y: float = 999999.0
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		var pos := _tile_render_pos(t.coord.x, t.coord.y)
		min_x = minf(min_x, pos.x)
		min_y = minf(min_y, pos.y)
	# 更新 offset 使地图从接近 (0,0) 开始
	_offset_x = -min_x + _tex_hex_w
	_offset_y = -min_y + _tex_hex_h


func _calculate_map_bounds() -> void:
	if not grid or grid.tiles.is_empty():
		return
	var min_x: float = 999999.0
	var max_x: float = -999999.0
	var min_y: float = 999999.0
	var max_y: float = -999999.0
	for tile in grid.tiles.values():
		var t: HexOverworldTile = tile
		var pos := _tile_render_pos(t.coord.x, t.coord.y)
		min_x = minf(min_x, pos.x)
		max_x = maxf(max_x, pos.x)
		min_y = minf(min_y, pos.y)
		max_y = maxf(max_y, pos.y)
	map_pixel_width = max_x - min_x + _tex_hex_w * 2.0
	map_pixel_height = max_y - min_y + _tex_hex_h * 2.0


## 一次性渲染所有瓦片
func _render_all_tiles() -> void:
	if not grid or grid.tiles.is_empty():
		return
	var container := Node2D.new()
	container.name = "AllTiles"
	add_child(container)
	
	# 按 y 坐标排序渲染：y 小的先画，y 大的后画（下方覆盖上方）
	var sorted_tiles: Array = []
	for key in grid.tiles:
		sorted_tiles.append(grid.tiles[key])
	sorted_tiles.sort_custom(func(a, b): return _tile_render_pos(a.coord.x, a.coord.y).y < _tile_render_pos(b.coord.x, b.coord.y).y)
	
	var count := 0
	for tile in sorted_tiles:
		var t: HexOverworldTile = tile
		_render_tile_into(container, t)
		count += 1
	print("[HexOverworldRenderer] _render_all_tiles: %d 瓦片, 子节点=%d" % [count, container.get_child_count()])


## 纹理实际六边形宽度（裁剪后）
var _tex_hex_w: float = 0.0
var _tex_hex_h: float = 0.0

## 计算基于纹理实际尺寸的像素位置
func _tile_render_pos(q: int, r: int) -> Vector2:
	if _tex_hex_w <= 0:
		var layout := HexOverworldTile.get_layout()
		_tex_hex_w = layout.tex_width
		_tex_hex_h = layout.tex_height
	var base_pos := HexOverworldTile.axial_to_pixel(q, r)
	return Vector2(base_pos.x + _offset_x, base_pos.y + _offset_y)


## ========================================
## Chunk 坐标转换
## ========================================

## 像素世界坐标 → chunk 坐标
func _pixel_to_chunk(wx: float, wy: float) -> Vector2i:
	# 先转回网格像素空间, 再用精确的像素→轴向转换
	var local_x := wx - _offset_x
	var local_y := wy - _offset_y
	var axial := HexOverworldTile.pixel_to_axial(local_x, local_y)
	var cx := int(floorf(float(axial.x) / float(CHUNK_SIZE)))
	var cy := int(floorf(float(axial.y) / float(CHUNK_SIZE)))
	return Vector2i(cx, cy)


## ========================================
## 每帧更新: chunk 按需加载/卸载
## ========================================

func _process(_delta: float) -> void:
	pass  # chunk 系统暂时禁用


func _get_visible_rect() -> Rect2:
	_camera = _find_camera()
	var margin := HexOverworldTile.HEX_SIZE * 4.0
	if not _camera:
		var vp := get_viewport().get_visible_rect().size
		return Rect2(position.x - vp.x * 0.5 - margin, position.y - vp.y * 0.5 - margin, vp.x + margin * 2.0, vp.y + margin * 2.0)
	var cam_pos := _camera.global_position
	var vp_size := get_viewport().get_visible_rect().size / _camera.zoom
	return Rect2(
		cam_pos.x - vp_size.x * 0.5 - margin,
		cam_pos.y - vp_size.y * 0.5 - margin,
		vp_size.x + margin * 2.0,
		vp_size.y + margin * 2.0
	)


func _find_camera() -> Camera2D:
	if _camera and is_instance_valid(_camera):
		return _camera
	var vp := get_viewport()
	if vp:
		var cameras := vp.find_children("*", "Camera2D")
		if cameras.size() > 0:
			_camera = cameras[0] as Camera2D
			return _camera
	return null


func _update_chunks(visible_rect: Rect2) -> void:
	# 四角 + 中心全部采样, 确保 chunk 覆盖完整
	var corners := [
		_pixel_to_chunk(visible_rect.position.x, visible_rect.position.y),
		_pixel_to_chunk(visible_rect.end.x, visible_rect.position.y),
		_pixel_to_chunk(visible_rect.position.x, visible_rect.end.y),
		_pixel_to_chunk(visible_rect.end.x, visible_rect.end.y),
	]
	var min_cx: int = corners[0].x
	var max_cx: int = corners[0].x
	var min_cy: int = corners[0].y
	var max_cy: int = corners[0].y
	for c in corners:
		min_cx = mini(min_cx, c.x)
		max_cx = maxi(max_cx, c.x)
		min_cy = mini(min_cy, c.y)
		max_cy = maxi(max_cy, c.y)

	var needed: Dictionary = {}
	for cy in range(min_cy, max_cy + 1):
		for cx in range(min_cx, max_cx + 1):
			needed["%d,%d" % [cx, cy]] = true

	var to_remove: Array = []
	for key in _active_chunks:
		if not needed.has(key):
			to_remove.append(key)
	for key in to_remove:
		_unload_chunk(key)

	for key in needed:
		if not _active_chunks.has(key):
			_load_chunk(key)


## ========================================
## Chunk 加载/卸载
## ========================================

func _load_chunk(key: String) -> void:
	var parts := key.split(",")
	var cx: int = int(parts[0])
	var cy: int = int(parts[1])

	var chunk_node := Node2D.new()
	chunk_node.name = "Chunk_%d_%d" % [cx, cy]
	add_child(chunk_node)

	# 遍历该 chunk 范围内的所有格子
	var q_start: int = cx * CHUNK_SIZE
	var r_start: int = cy * CHUNK_SIZE
	var q_end: int = q_start + CHUNK_SIZE
	var r_end: int = r_start + CHUNK_SIZE
	
	var _tile_count := 0

	for q in range(q_start, q_end):
		for r in range(r_start, r_end):
			var tile: HexOverworldTile = grid.tiles.get(Vector2i(q, r))
			if not tile:
				continue
			_render_tile_into(chunk_node, tile)
			_tile_count += 1

	_active_chunks[key] = chunk_node


func _unload_chunk(key: String) -> void:
	var chunk_node: Node2D = _active_chunks[key]
	chunk_node.queue_free()
	_active_chunks.erase(key)


## ========================================
## 单个瓦片渲染 (放入 chunk 容器)
## ========================================

func _render_tile_into(parent: Node2D, tile: HexOverworldTile) -> void:
	var world_pos := _tile_render_pos(tile.coord.x, tile.coord.y)

	var poly := Polygon2D.new()
	var points := PackedVector2Array()
	
	# 生成平顶六边形顶点
	var layout := HexOverworldTile.get_layout()
	var half_w := layout.tex_width / 2.0
	# 对于平顶六边形，外接圆半径 size = width / 2
	# 或者使用 layout 的 q_vector, r_vector 反推，但这里仅作纯色预览，直接用标准公式
	var size := half_w
	var sqrt3_half := 0.866025
	
	# 这里为了简单，我们用标准平顶六边形的6个角
	points.append(Vector2(-half_w * 0.5, -size * sqrt3_half))
	points.append(Vector2(half_w * 0.5, -size * sqrt3_half))
	points.append(Vector2(half_w, 0))
	points.append(Vector2(half_w * 0.5, size * sqrt3_half))
	points.append(Vector2(-half_w * 0.5, size * sqrt3_half))
	points.append(Vector2(-half_w, 0))

	poly.polygon = points
	poly.color = _debug_terrain_color(tile.terrain)
	poly.position = world_pos
	
	# 根据高度微调亮度 (地形雕刻预览)
	var h_tweak := tile.elevation * 0.2
	poly.color = Color(poly.color.r + h_tweak, poly.color.g + h_tweak, poly.color.b + h_tweak)

	poly.z_index = 0
	parent.add_child(poly)

	# --- 叠加层 (纯色多边形或线条替代) ---
	if tile.is_river:
		poly.color = _debug_terrain_color(HexOverworldTile.TerrainType.RIVER)

	if tile.is_road:
		var r_poly := Polygon2D.new()
		r_poly.polygon = points
		r_poly.color = _debug_terrain_color(HexOverworldTile.TerrainType.ROAD)
		r_poly.color.a = 0.5
		r_poly.position = world_pos
		r_poly.z_index = 1
		parent.add_child(r_poly)

	if tile.has_settlement:
		var s_poly := Polygon2D.new()
		var p2 := PackedVector2Array()
		p2.append(Vector2(-15, -15))
		p2.append(Vector2(15, -15))
		p2.append(Vector2(15, 15))
		p2.append(Vector2(-15, 15))
		s_poly.polygon = p2
		s_poly.color = Color(1.0, 0.0, 0.0) # 红色方块代表定居点
		s_poly.position = world_pos
		s_poly.z_index = 2
		parent.add_child(s_poly)


## 调试：地形类型 → 纯色映射
func _debug_terrain_color(terrain_type: int) -> Color:
	match terrain_type:
		HexOverworldTile.TerrainType.DEEP_WATER:    return Color(0.05, 0.1, 0.4)    # 深蓝
		HexOverworldTile.TerrainType.SHALLOW_WATER: return Color(0.1, 0.3, 0.7)     # 浅蓝
		HexOverworldTile.TerrainType.SAND:          return Color(0.9, 0.85, 0.5)    # 沙黄
		HexOverworldTile.TerrainType.PLAINS:        return Color(0.4, 0.75, 0.3)    # 浅绿
		HexOverworldTile.TerrainType.GRASSLAND:     return Color(0.3, 0.7, 0.25)    # 绿色
		HexOverworldTile.TerrainType.FOREST:        return Color(0.15, 0.5, 0.15)   # 深绿
		HexOverworldTile.TerrainType.DENSE_FOREST:  return Color(0.08, 0.35, 0.08)  # 更深绿
		HexOverworldTile.TerrainType.HILLS:         return Color(0.6, 0.55, 0.35)   # 土黄
		HexOverworldTile.TerrainType.MOUNTAIN:      return Color(0.5, 0.45, 0.4)    # 灰棕
		HexOverworldTile.TerrainType.SNOW:          return Color(0.9, 0.93, 0.98)   # 白色
		HexOverworldTile.TerrainType.SWAMP:         return Color(0.3, 0.4, 0.2)     # 暗绿
		HexOverworldTile.TerrainType.SAVANNA:       return Color(0.75, 0.7, 0.35)   # 黄褐
		HexOverworldTile.TerrainType.ROAD:          return Color(0.7, 0.6, 0.4)     # 棕色
		HexOverworldTile.TerrainType.RIVER:         return Color(0.15, 0.35, 0.75)  # 蓝色
		_: return Color(1.0, 0.0, 1.0)  # 洋红=未知地形（醒目）


## ========================================
## 纹理选择
## ========================================

func _pick_terrain_texture(terrain_type: int, variant: int) -> Texture2D:
	var arr: Array = _terrain_tex.get(terrain_type, [])
	if arr.size() > 0:
		return arr[variant % arr.size()]
	# 后备: 灰色纯色纹理
	return _make_fallback(Color(0.5, 0.5, 0.5))


func _pick_overlay_texture(overlay_key: String, variant: int) -> Texture2D:
	var arr: Array = _overlay_tex.get(overlay_key, [])
	if arr.size() > 0:
		return arr[variant % arr.size()]
	return _make_fallback(Color(0.5, 0.5, 0.5, 0.5))


func _settlement_type_to_overlay(settlement_type: int) -> String:
	# 映射 OverworldPOI.POIType → 叠加纹理键
	match settlement_type:
		2: return "town"       # TOWN
		1: return "village"    # VILLAGE
		3: return "fort"       # CASTLE
		4: return "fort"       # FORTRESS
		_: return "village"


## ========================================
## 后备纹理
## ========================================

var _fallback_cache: Dictionary = {}

func _make_fallback(color: Color) -> ImageTexture:
	var key := Color8(int(color.r * 255), int(color.g * 255), int(color.b * 255), int(color.a * 255))
	if _fallback_cache.has(key):
		return _fallback_cache[key]
	var img := Image.create(313, 313, false, Image.FORMAT_RGBA8)
	img.fill(color)
	var tex := ImageTexture.create_from_image(img)
	_fallback_cache[key] = tex
	return tex


## ========================================
## 变体选择 (确定性哈希)
## ========================================

func _get_variant_for_tile(tile: HexOverworldTile) -> int:
	var h := (tile.coord.x * 374761393 + tile.coord.y * 668265263) & 0x7FFFFFFF
	var max_v := HexOverworldTile.terrain_variant_count(tile.terrain)
	return h % maxi(max_v, 1)


## ========================================
## 公共接口
## ========================================

func render_full() -> void:
	mark_dirty()

func mark_dirty() -> void:
	# 卸载所有 chunk, 下帧重新加载
	for key in _active_chunks:
		var node: Node2D = _active_chunks[key]
		node.queue_free()
	_active_chunks.clear()
	_last_visible_rect = Rect2()

func update_fog(tiles_to_update: Array[HexOverworldTile] = []) -> void:
	for tile in tiles_to_update:
		var chunk_key := _tile_to_chunk_key(tile)
		if _active_chunks.has(chunk_key):
			# 重建该 chunk (迷雾状态已变)
			_unload_chunk(chunk_key)
			_load_chunk(chunk_key)


## 轴向坐标 → chunk key
func _tile_to_chunk_key(tile: HexOverworldTile) -> String:
	var cx := int(floorf(float(tile.coord.x) / float(CHUNK_SIZE)))
	var cy := int(floorf(float(tile.coord.y) / float(CHUNK_SIZE)))
	return "%d,%d" % [cx, cy]

func get_texture_size() -> Vector2:
	return Vector2(map_pixel_width, map_pixel_height)

func get_pixel_offset() -> Vector2:
	return Vector2(_offset_x, _offset_y)

func get_tile_at_world_pos(world_pos: Vector2) -> HexOverworldTile:
	if not grid:
		return null
	var px := world_pos.x - _offset_x
	var py := world_pos.y - _offset_y
	return grid.get_tile_at_pixel(px, py)

func is_dirty() -> bool:
	return false

func has_textures() -> bool:
	return _preloaded.size() > 0
