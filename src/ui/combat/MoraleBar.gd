# MoraleBar.gd
# 士气条UI — 显示单位士气值和士气等级，颜色编码
# 对应策划案 03-战术战斗系统 → 六、士气系统
extends Control
class_name MoraleBar

# ============================================================================
# 内部组件
# ============================================================================

var _bar: ProgressBar
var _label: Label
var _level_label: Label

func _ready():
	_setup()

func _setup():
	# 布局
	custom_minimum_size = Vector2(120, 18)
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 士气条
	_bar = ProgressBar.new()
	_bar.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_bar.show_percentage = false
	_bar.min_value = -60
	_bar.max_value = 40
	_bar.value = 0
	
	var bg_style = StyleBoxFlat.new()
	bg_style.bg_color = Color(0.15, 0.15, 0.15, 0.8)
	bg_style.set_corner_radius_all(3)
	_bar.add_theme_stylebox_override("background", bg_style)
	
	add_child(_bar)
	
	# 数值标签
	_label = Label.new()
	_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_label.add_theme_font_size_override("font_size", 11)
	_label.text = "士气: 0"
	_label.z_index = 1
	add_child(_label)

# ============================================================================
# 更新显示
# ============================================================================

## 更新士气条显示
func update_morale(morale_value: int):
	_bar.value = morale_value
	_label.text = "士气: %d" % morale_value
	
	# 颜色编码
	var fill_style = StyleBoxFlat.new()
	fill_style.set_corner_radius_all(3)
	
	var level = _get_morale_level(morale_value)
	match level:
		0:  # HIGH
			fill_style.bg_color = Color(0.2, 0.8, 0.3)
			_label.add_theme_color_override("font_color", Color.WHITE)
		1:  # NORMAL
			fill_style.bg_color = Color(0.7, 0.7, 0.7)
			_label.add_theme_color_override("font_color", Color.WHITE)
		2:  # LOW
			fill_style.bg_color = Color(0.9, 0.7, 0.1)
			_label.add_theme_color_override("font_color", Color.BLACK)
		3:  # BROKEN
			fill_style.bg_color = Color(0.9, 0.2, 0.1)
			_label.add_theme_color_override("font_color", Color.WHITE)
		4:  # ROUTING
			fill_style.bg_color = Color(0.9, 0.0, 0.0)
			# 溃逃时闪烁效果预留
			_label.add_theme_color_override("font_color", Color.YELLOW)
	
	_bar.add_theme_stylebox_override("fill", fill_style)

func _get_morale_level(morale: int) -> int:
	if morale >= 20: return 0      # HIGH
	elif morale >= -19: return 1   # NORMAL
	elif morale >= -39: return 2   # LOW
	elif morale >= -59: return 3   # BROKEN
	else: return 4                # ROUTING
