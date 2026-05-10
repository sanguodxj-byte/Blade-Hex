# OverworldParty.gd
# 战略层大地图上的队伍实体 (无网格平滑移动版)
extends Node2D
class_name OverworldParty

@export var base_move_speed: float = 300.0
@export var overworld_sprite: Texture2D
@export var overworld_frames: SpriteFrames

var path: Array[Vector2] = []
var is_moving: bool = false

## 六边形地图导航（替代旧 overworld_map）
var hex_grid: HexOverworldGrid
var hex_astar: HexOverworldAStar

## 移动速度组件（由 OverworldScene 初始化注入依赖）
var speed_component: MovementSpeedComponent

var visual_poly: Polygon2D
var visual_sprite: Sprite2D
var visual_anim: AnimatedSprite2D

func _ready():
	_setup_visuals()

func _setup_visuals():
	visual_sprite = Sprite2D.new()
	add_child(visual_sprite)
	
	visual_anim = AnimatedSprite2D.new()
	add_child(visual_anim)
	
	# 初始化几何占位
	visual_poly = Polygon2D.new()
	var points = PackedVector2Array()
	var radius = 15.0
	points.append(Vector2(0, -radius))
	points.append(Vector2(radius * 0.7, 0))
	points.append(Vector2(0, radius))
	points.append(Vector2(-radius * 0.7, 0))
	visual_poly.polygon = points
	visual_poly.color = Color(1.0, 0.8, 0.0) # 金黄色
	add_child(visual_poly)
	
	_update_visual_state()

func _update_visual_state():
	visual_sprite.visible = false
	visual_anim.visible = false
	visual_poly.visible = false
	
	if overworld_frames:
		visual_anim.sprite_frames = overworld_frames
		var h = 60.0
		if overworld_frames.get_frame_count("default") > 0:
			var tex = overworld_frames.get_frame_texture("default", 0)
			if tex: h = tex.get_height()
		visual_anim.position = Vector2(0, -h / 2.0)
		visual_anim.visible = true
		visual_anim.play("default")
	elif overworld_sprite:
		visual_sprite.texture = overworld_sprite
		visual_sprite.position = Vector2(0, -overworld_sprite.get_height() / 2.0)
		visual_sprite.visible = true
	else:
		visual_poly.visible = true

func play_anim(anim_name: String):
	if visual_anim.visible and visual_anim.sprite_frames and visual_anim.sprite_frames.has_animation(anim_name):
		visual_anim.play(anim_name)
	elif visual_anim.visible:
		visual_anim.play("default")

func set_hex_navigation(grid: HexOverworldGrid, astar: HexOverworldAStar):
	hex_grid = grid
	hex_astar = astar

func place_at(px: float, py: float):
	position = Vector2(px, py)
	path.clear()
	is_moving = false

func move_to(target_px: Vector2):
	if not hex_grid or not hex_astar: return
	var new_path = hex_astar.find_path_pixels(position, target_px)
	if new_path.size() > 0:
		path = new_path
		is_moving = true

func _process(delta):
	if not is_moving or path.is_empty():
		return
	
	# 通过速度组件计算最终速度（含地形/季节/昼夜/负重/坐骑/技能修正）
	var current_speed := base_move_speed
	if speed_component:
		current_speed = speed_component.calculate_speed(position)
	
	var target_pos = path[0]
	var dir = (target_pos - position).normalized()
	var dist = position.distance_to(target_pos)
	var step = current_speed * delta
	
	if step >= dist:
		position = target_pos
		path.pop_front()
		if path.is_empty():
			is_moving = false
	else:
		position += dir * step


## 获取当前速度分解（供UI显示）
func get_speed_breakdown() -> Dictionary:
	if speed_component:
		return speed_component.get_speed_breakdown(position)
	return {"base": base_move_speed, "final": base_move_speed}
