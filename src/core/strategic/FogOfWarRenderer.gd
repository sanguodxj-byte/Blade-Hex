# FogOfWarRenderer.gd
# 战争迷雾渲染器 — 将 FogOfWar 网格数据可视化为三级迷雾效果
#
# 视觉表现（对应策划案 14-大地图设定.md → 6.2 迷雾层级）:
#   UNEXPLORED (0): 半透明深色覆盖 + 羊皮纸纹理质感
#   REVEALED   (1): 轻微暗化覆盖（远处无实时信息的朦胧感）
#   IN_VISION  (2): 无覆盖，完全可见
#
# 实现方式：每帧基于摄像机可视区域局部更新纹理
# 使用 BackBufferCopy + ShaderMaterial 实现高质量迷雾覆盖
extends Node2D
class_name FogOfWarRenderer

## 迷雾引用
var fog: FogOfWar

## 渲染参数
## 未探索区域颜色（深褐色半透明，模拟羊皮纸+未知感）
var color_unexplored: Color = Color(0.12, 0.10, 0.08, 0.92)
## 已揭示区域颜色（轻微暗化）
var color_revealed: Color = Color(0.0, 0.0, 0.0, 0.30)
## 视野内颜色（完全透明）
var color_vision: Color = Color(0.0, 0.0, 0.0, 0.0)

## 内部渲染状态
var image: Image
var texture: ImageTexture
var _sprite: Sprite2D
var _needs_full_redraw: bool = true

## 网格尺寸缓存
var grid_w: int = 0
var grid_h: int = 0
var cell_size: int = 16
## 上一帧玩家位置的网格坐标 — 用于检测是否需要更新
var _last_player_gx: int = -999
var _last_player_gy: int = -999


func _ready():
	z_index = 100  # 确保在地图瓦片之上、UI之下
	z_as_relative = false


## ========================================
## 初始化
## ========================================

func setup(fog_data: FogOfWar) -> void:
	fog = fog_data
	grid_w = fog.grid_w
	grid_h = fog.grid_h
	cell_size = fog.cell_size

	# 调试模式：跳过全图迷雾纹理渲染，由 HexOverworldRenderer per-tile 调试颜色代替
	# 避免双重迷雾覆盖干扰调试
	return

	# 创建全图纹理（每个格子 = 1像素，通过缩放到地图尺寸）
	image = Image.create(grid_w, grid_h, false, Image.FORMAT_RGBA8)
	texture = ImageTexture.create_from_image(image)
	_needs_full_redraw = true

	# 清理旧子节点
	for child in get_children():
		if is_instance_valid(child):
			child.queue_free()

	# 使用 Sprite2D 显示迷雾纹理（Sprite2D 在 Node2D 下工作正常）
	_sprite = Sprite2D.new()
	_sprite.texture = texture
	_sprite.centered = false
	_sprite.scale = Vector2(
		float(fog.map_width_px) / float(grid_w),
		float(fog.map_height_px) / float(grid_h)
	)
	_sprite.position = Vector2.ZERO
	_sprite.z_index = 0
	# 挂载迷雾柔化着色器
	var fog_shader := load("res://src/assets/shaders/fog_of_war.gdshader") as Shader
	if fog_shader:
		var fog_mat := ShaderMaterial.new()
		fog_mat.shader = fog_shader
		_sprite.material = fog_mat
	else:
		push_warning("[FogOfWarRenderer] 迷雾柔化着色器加载失败")
	add_child(_sprite)


## ========================================
## 每帧渲染
## ========================================

func update_render(camera_pos: Vector2, _viewport_size: Vector2, _camera_zoom: float):
	if not fog or not image:
		return

	# 检查玩家网格位置是否发生变化 — 没变化则跳过渲染
	var player_gx := int(camera_pos.x / float(cell_size))
	var player_gy := int(camera_pos.y / float(cell_size))

	if not _needs_full_redraw and player_gx == _last_player_gx and player_gy == _last_player_gy:
		return

	_last_player_gx = player_gx
	_last_player_gy = player_gy

	# 全图重绘（初始化或 mark_dirty 触发）
	if _needs_full_redraw:
		_render_region(0, 0, grid_w - 1, grid_h - 1)
		_needs_full_redraw = false
		return

	# 计算需要更新的区域：玩家视野范围 + 边缘缓冲
	var effective_range := fog.vision_range * fog.scout_multiplier
	var range_cells := int(effective_range / float(cell_size)) + 2  # +2 额外缓冲

	var gx_start := maxi(player_gx - range_cells, 0)
	var gy_start := maxi(player_gy - range_cells, 0)
	var gx_end := mini(player_gx + range_cells, grid_w - 1)
	var gy_end := mini(player_gy + range_cells, grid_h - 1)

	_render_region(gx_start, gy_start, gx_end, gy_end)


## 渲染指定网格区域并更新纹理
func _render_region(gx_start: int, gy_start: int, gx_end: int, gy_end: int) -> void:
	# 边界保护
	gx_start = maxi(gx_start, 0)
	gy_start = maxi(gy_start, 0)
	gx_end = mini(gx_end, grid_w - 1)
	gy_end = mini(gy_end, grid_h - 1)

	for gy in range(gy_start, gy_end + 1):
		for gx in range(gx_start, gx_end + 1):
			var state: int = fog.explored_grid[gy][gx]
			var color: Color

			if state == FogOfWar.FogState.IN_VISION:
				color = color_vision
			elif state == FogOfWar.FogState.REVEALED:
				color = color_revealed
			else:
				color = color_unexplored

			image.set_pixel(gx, gy, color)

	# Godot 4 中更新 ImageTexture：需要重新创建
	# 注意：此处性能可接受，因为只在玩家移动到新格子时触发
	texture = ImageTexture.create_from_image(image)
	if _sprite:
		_sprite.texture = texture


## ========================================
## 工具
## ========================================

## 强制下一帧全图重绘
func mark_dirty() -> void:
	_needs_full_redraw = true
