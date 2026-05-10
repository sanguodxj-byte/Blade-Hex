# EnemyUnitBar.gd
# 3D战场上敌方单位头顶的HP条 + 士气指示器
# 挂载在 Unit 节点上，使用 SubViewport + Sprite3D 实现HD-2D风格
extends Node3D
class_name EnemyUnitBar

## HP条颜色梯度
const HP_HIGH := Color(0.2, 0.75, 0.2)
const HP_MID := Color(0.85, 0.75, 0.1)
const HP_LOW := Color(0.9, 0.15, 0.1)
const HP_BG := Color(0.15, 0.08, 0.08, 0.7)
const HP_BORDER := Color(0.3, 0.15, 0.15, 0.6)

## 士气颜色
const MORALE_COLORS := {
	0: Color(0.2, 0.8, 0.9),   # HIGH
	1: Color(0.5, 0.5, 0.5),   # NORMAL
	2: Color(0.9, 0.7, 0.1),   # LOW
	3: Color(0.9, 0.2, 0.1),   # BROKEN
	4: Color(1.0, 0.1, 0.1),   # ROUTING
}

## 控件引用
var _canvas: CanvasLayer
var _hp_bar: ProgressBar
var _hp_label: Label3D
var _morale_indicator: ColorRect
var _status_container: HBoxContainer
var _name_label: Label3D
var _root_control: Control

## 条目宽度/高度
const BAR_WIDTH := 70.0
const BAR_HEIGHT := 7.0
const MORALE_SIZE := 5.0

## 用于跟踪状态变化
var _last_hp: int = -1
var _last_max_hp: int = -1
var _last_morale_level: int = -1
var _active_status_effects: Dictionary = {}  # effect_name -> Control

func _ready():
	_create_bar()

func _create_bar():
	# 使用 Control 节点作为容器，通过 Label3D 渲染到3D空间
	# 实际在3D场景中用 Sprite3D + SubViewport 方式显示
	# 但为了简化原型阶段，先用 Label3D 组合
	
	# 名称标签（最上层）
	_name_label = Label3D.new()
	_name_label.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	_name_label.pixel_size = 2.5
	_name_label.font_size = 16
	_name_label.outline_size = 3
	_name_label.modulate = Color(0.95, 0.75, 0.7)
	_name_label.position = Vector3(0, 0, 0)
	add_child(_name_label)
	
	# HP数值标签
	_hp_label = Label3D.new()
	_hp_label.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	_hp_label.pixel_size = 2.5
	_hp_label.font_size = 14
	_hp_label.outline_size = 2
	_hp_label.modulate = Color.WHITE
	_hp_label.position = Vector3(0, -7, 0)
	add_child(_hp_label)
	
	# 士气指示点（小圆点）
	_morale_indicator = ColorRect.new()
	# 由于 ColorRect 不能直接放到3D，这里用一个小的 MeshInstance3D 球体代替
	var morale_sphere = MeshInstance3D.new()
	var sphere_mesh = SphereMesh.new()
	sphere_mesh.radius = 0.15
	sphere_mesh.height = 0.3
	morale_sphere.mesh = sphere_mesh
	morale_sphere.position = Vector3(1.5, -7, 0)
	# 存储引用以便更新颜色
	morale_sphere.name = "MoraleSphere"
	add_child(morale_sphere)


## 更新血条显示
func update_display(unit: Unit):
	if not unit or not unit.data:
		return
	
	var max_hp := unit.get_max_hp()
	var current_hp := unit.current_hp
	
	# 避免无变化时重复更新
	if current_hp == _last_hp and max_hp == _last_max_hp:
		return
	_last_hp = current_hp
	_last_max_hp = max_hp
	
	# 更新名称
	_name_label.text = unit.data.unit_name
	
	# 更新HP文本
	var hp_ratio := float(current_hp) / float(max(max_hp, 1))
	var hp_color: Color
	if hp_ratio > 0.6:
		hp_color = HP_HIGH
	elif hp_ratio > 0.3:
		hp_color = HP_MID
	else:
		hp_color = HP_LOW
	
	_hp_label.text = "%d/%d" % [current_hp, max_hp]
	_hp_label.modulate = hp_color
	
	# 低血量闪烁效果（HP < 25%）
	if hp_ratio < 0.25 and current_hp > 0:
		_start_low_hp_flash()
	else:
		_stop_low_hp_flash()
	
	# 更新士气指示
	if unit.data.is_enemy:
		update_morale_indicator(unit.data.get_morale_level())


## 更新士气指示器
func update_morale_indicator(morale_level: int):
	if morale_level == _last_morale_level:
		return
	_last_morale_level = morale_level
	
	var morale_sphere = get_node_or_null("MoraleSphere")
	if not morale_sphere:
		return
	
	# 使用材质颜色表示士气
	var mat = StandardMaterial3D.new()
	mat.albedo_color = MORALE_COLORS.get(morale_level, Color.GRAY)
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	morale_sphere.material_override = mat
	
	# 溃逃时球体变大闪烁
	if morale_level == UnitData.MoraleLevel.ROUTING:
		morale_sphere.scale = Vector3(1.5, 1.5, 1.5)


## 添加状态效果指示（在名称旁显示简短文字）
func add_status_effect(effect_name: String, display_text: String, color: Color):
	if _active_status_effects.has(effect_name):
		return
	
	var effect_label = Label3D.new()
	effect_label.text = display_text
	effect_label.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	effect_label.pixel_size = 2.0
	effect_label.font_size = 12
	effect_label.outline_size = 2
	effect_label.modulate = color
	# 状态效果显示在名称上方，向右偏移排列
	effect_label.position = Vector3(_active_status_effects.size() * 1.2, 5, 0)
	add_child(effect_label)
	_active_status_effects[effect_name] = effect_label


## 移除状态效果
func remove_status_effect(effect_name: String):
	if not _active_status_effects.has(effect_name):
		return
	
	var effect_label = _active_status_effects[effect_name]
	effect_label.queue_free()
	_active_status_effects.erase(effect_name)
	
	# 重新排列剩余状态效果
	var idx := 0
	for key in _active_status_effects:
		var label: Label3D = _active_status_effects[key]
		label.position = Vector3(idx * 1.2, 5, 0)
		idx += 1


## 低血量闪烁
var _flash_tween: Tween

func _start_low_hp_flash():
	if _flash_tween and _flash_tween.is_valid():
		return
	_flash_tween = create_tween()
	if _flash_tween == null:
		return
	_flash_tween.set_loops()
	_flash_tween.tween_property(_hp_label, "modulate", Color(1.0, 0.3, 0.3), 0.4)
	_flash_tween.tween_property(_hp_label, "modulate", HP_LOW, 0.4)

func _stop_low_hp_flash():
	if _flash_tween and _flash_tween.is_valid():
		_flash_tween.kill()
		_flash_tween = null
