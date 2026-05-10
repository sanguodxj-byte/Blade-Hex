# OverworldTown.gd
# 大地图上的城镇/据点实体
# 扩展：支持城镇设施列表、繁荣度等交互数据
extends Node2D
class_name OverworldTown

@export var town_sprite: Texture2D
@export var town_frames: SpriteFrames
var town_name: String = "中立城镇"

var visual_poly: Polygon2D
var visual_sprite: Sprite2D
var visual_anim: AnimatedSprite2D

## ============ 交互系统扩展 ============

## 城镇设施列表
var facilities: Array = []  # Array[TownFacility]
## 城镇类型："town" 或 "village"
var town_type: String = "town"
## 繁荣度 0~100
var prosperity: int = 50
## 城镇描述
var town_description: String = ""
## 所属势力
var faction: String = ""
## 守军数量
var garrison: int = 50


func _ready():
	_setup_visuals()

func _setup_visuals():
	visual_sprite = Sprite2D.new()
	add_child(visual_sprite)
	
	visual_anim = AnimatedSprite2D.new()
	add_child(visual_anim)
	
	visual_poly = Polygon2D.new()
	var points = PackedVector2Array()
	var size = 25.0
	
	# 画一个正方形代表城镇
	points.append(Vector2(-size, -size))
	points.append(Vector2(size, -size))
	points.append(Vector2(size, size))
	points.append(Vector2(-size, size))
	
	visual_poly.polygon = points
	# 根据城镇类型设置颜色
	if town_type == "village":
		visual_poly.color = Color(0.3, 0.5, 0.3)  # 绿色=村庄
	else:
		visual_poly.color = Color(0.2, 0.4, 0.8)  # 蓝色=城镇
	add_child(visual_poly)
	
	var label = Label.new()
	label.text = town_name
	label.position = Vector2(-40, size + 5)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.custom_minimum_size = Vector2(80, 20)
	label.add_theme_font_size_override("font_size", 14)
	add_child(label)
	
	_update_visual_state()

func _update_visual_state():
	visual_sprite.visible = false
	visual_anim.visible = false
	visual_poly.visible = false
	
	if town_frames:
		visual_anim.sprite_frames = town_frames
		var h = 80.0
		if town_frames.get_frame_count("default") > 0:
			var tex = town_frames.get_frame_texture("default", 0)
			if tex: h = tex.get_height()
		visual_anim.position = Vector2(0, -h / 2.0)
		visual_anim.visible = true
		visual_anim.play("default")
	elif town_sprite:
		visual_sprite.texture = town_sprite
		visual_sprite.position = Vector2(0, -town_sprite.get_height() / 2.0)
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

## 初始化默认城镇设施
func setup_default_facilities() -> void:
	facilities = TownFacility.create_default_facilities()


## 初始化村庄设施（简化版）
func setup_village_facilities() -> void:
	facilities = TownFacility.create_village_facilities()
	town_type = "village"


## 根据设施类型获取设施
func get_facility_by_type(type: int) -> TownFacility:  # type: TownFacility.FacilityType
	for facility in facilities:
		if facility.facility_type == type:
			return facility
	return null


## 获取城镇描述
func get_description() -> String:
	if town_description != "":
		return town_description
	var type_text = "村庄" if town_type == "village" else "城镇"
	var prosper_text = "繁荣" if prosperity >= 60 else ("一般" if prosperity >= 30 else "萧条")
	return "一座%s的%s，守军约%d人。" % [prosper_text, type_text, garrison]
