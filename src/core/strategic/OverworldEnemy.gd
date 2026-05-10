# OverworldEnemy.gd
# 大地图上的敌军队伍实体 (无网格版)
# 扩展：支持NPCProfile，区分人形/非人形交互
extends Node2D
class_name OverworldEnemy

@export var overworld_sprite: Texture2D
@export var overworld_frames: SpriteFrames

var visual_poly: Polygon2D
var visual_sprite: Sprite2D
var visual_anim: AnimatedSprite2D

## ============ 交互系统扩展 ============

## NPC档案（为null则视为非人形生物）
var npc_profile: NPCProfile = null
## 是否为敌对状态（影响交互选项）
var is_hostile: bool = true
## 自定义显示名称（无NPC档案时使用）
var display_name: String = ""
## 自定义描述文本
var description_text: String = ""
## 敌人类型（来自 UnitData.EnemyType，用于判断人形/非人形）
var enemy_type: int = 1  # 默认BEAST=1


func _ready():
	_setup_visuals()

func _setup_visuals():
	visual_sprite = Sprite2D.new()
	add_child(visual_sprite)
	
	visual_anim = AnimatedSprite2D.new()
	add_child(visual_anim)
	
	visual_poly = Polygon2D.new()
	var points = PackedVector2Array()
	var radius = 15.0
	
	# 画一个倒三角形代表敌人
	points.append(Vector2(0, radius))
	points.append(Vector2(radius * 0.7, -radius * 0.5))
	points.append(Vector2(-radius * 0.7, -radius * 0.5))
	
	visual_poly.polygon = points
	# 根据是否为人形NPC设置不同颜色
	if npc_profile:
		visual_poly.color = Color(0.9, 0.7, 0.2)  # 黄色=人形NPC
	else:
		visual_poly.color = Color(0.9, 0.2, 0.2)  # 红色=非人形
	add_child(visual_poly)
	
	# 添加名称标签
	var label = Label.new()
	label.text = get_display_name()
	label.position = Vector2(-40, -25)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.custom_minimum_size = Vector2(80, 20)
	label.add_theme_font_size_override("font_size", 12)
	add_child(label)
	
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

func place_at(px: float, py: float):
	position = Vector2(px, py)


## ============ 交互辅助方法 ============

## 获取实体类型（"nonhumanoid" 或 "humanoid"）
func get_entity_type() -> String:
	if npc_profile:
		return "humanoid"
	# 根据 UnitData.EnemyType 判断：HUMANOID=0 为人形
	if enemy_type == 0:  # UnitData.EnemyType.HUMANOID
		return "humanoid"
	return "nonhumanoid"


## 获取显示名称
func get_display_name() -> String:
	if npc_profile:
		return npc_profile.npc_name
	if display_name != "":
		return display_name
	return "未知敌人"


## 获取描述文本
func get_description() -> String:
	if npc_profile:
		return npc_profile.get_description()
	if description_text != "":
		return description_text
	return "一个危险的生物，看起来不太友好。"
