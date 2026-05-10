# TurnOrderBar.gd
# 回合顺序显示栏 — 显示当前回合所有单位的行动顺序
# 对应策划案 09-UI设计.md → 回合信息栏（当前回合数/阶段指示）
# 对应策划案 03-战术战斗系统 → 先攻系统
extends HBoxContainer
class_name TurnOrderBar

# ============================================================================
# 信号
# ============================================================================
signal unit_clicked(unit: Unit)

# ============================================================================
# 内部
# ============================================================================
var _turn_label: Label
var _unit_icons: Array[Control] = []
var _active_index: int = -1
var _factory: UIFactory

func _ready():
	_factory = UIFactory.new()
	_setup()

func _setup():
	# 回合数标签
	_turn_label = _factory.create_body_label("第1回合", _theme.text_accent)
	_turn_label.custom_minimum_size = Vector2(80, 0)
	add_child(_turn_label)
	
	add_child(_factory.create_separator_v())
	
	# 滚动区域显示单位图标
	var scroll = _factory.create_scroll_container(true)
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	add_child(scroll)
	
	var icon_container = HBoxContainer.new()
	icon_container.add_theme_constant_override("separation", 4)
	scroll.add_child(icon_container)
	icon_container.set_meta("container", true)
	# 存储引用
	set_meta("icon_container", icon_container)

# ============================================================================
# 公开接口
# ============================================================================

## 设置回合数
func set_turn_number(turn: int):
	_turn_label.text = "第%d回合" % turn

## 设置回合阶段文字
func set_phase_text(text: String, color: Color = Color()):
	_turn_label.text = text
	if color != Color():
		_turn_label.add_theme_color_override("font_color", color)

## 设置单位顺序列表
func set_unit_order(units: Array[Unit], active_unit: Unit = null):
	# 清除旧图标
	var container: HBoxContainer = get_meta("icon_container")
	if not container:
		return
	for child in container.get_children():
		child.queue_free()
	_unit_icons.clear()
	
	for unit in units:
		if not is_instance_valid(unit) or not unit.data:
			continue
		var icon = _create_unit_icon(unit, unit == active_unit)
		container.add_child(icon)
		_unit_icons.append(icon)

## 高亮当前行动单位
func set_active_unit(unit: Unit):
	var container: HBoxContainer = get_meta("icon_container")
	if not container:
		return
	
	var idx = 0
	for child in container.get_children():
		var is_active = false
		if is_instance_valid(unit) and child.has_meta("unit_ref"):
			is_active = child.get_meta("unit_ref") == unit
		_update_icon_style(child, is_active)
		idx += 1

# ============================================================================
# 内部方法
# ============================================================================

var _theme: UITheme:
	get: return UITheme.get_instance()

func _create_unit_icon(unit: Unit, is_active: bool) -> PanelContainer:
	var icon := PanelContainer.new()
	icon.custom_minimum_size = Vector2(40, 40)
	icon.set_meta("unit_ref", unit)
	
	var style: StyleBoxFlat
	if is_active:
		style = _theme.make_panel_style(
			Color(0.3, 0.28, 0.1, 0.9), _theme.border_highlight, 2, _theme.radius_sm, 2)
	else:
		var is_enemy = unit.data.is_enemy if unit.data else false
		var bg = Color(0.2, 0.08, 0.08, 0.7) if is_enemy else Color(0.08, 0.12, 0.2, 0.7)
		var border = _theme.border_enemy if is_enemy else _theme.border_friendly
		style = _theme.make_panel_style(bg, border, 1, _theme.radius_sm, 2)
	icon.add_theme_stylebox_override("panel", style)
	
	# 缩略信息
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 0)
	icon.add_child(vbox)
	
	# 名称缩写
	var name_lbl := Label.new()
	name_lbl.text = unit.data.unit_name.left(2) if unit.data else "??"
	name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_lbl.add_theme_font_size_override("font_size", _theme.font_size_xs)
	name_lbl.add_theme_color_override("font_color", _theme.text_primary)
	vbox.add_child(name_lbl)
	
	# HP比例条
	var hp_bar := ProgressBar.new()
	hp_bar.custom_minimum_size = Vector2(30, 3)
	hp_bar.show_percentage = false
	hp_bar.min_value = 0
	hp_bar.max_value = unit.get_max_hp() if is_instance_valid(unit) else 1
	hp_bar.value = unit.current_hp if is_instance_valid(unit) else 0
	_theme.apply_bar_theme(hp_bar, _theme.get_hp_color(
		float(unit.current_hp) / float(max(unit.get_max_hp(), 1))
	), _theme.hp_bar_bg)
	vbox.add_child(hp_bar)
	
	# 点击事件
	icon.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
			unit_clicked.emit(unit)
	)
	
	return icon

func _update_icon_style(icon: Control, is_active: bool):
	var style: StyleBoxFlat = icon.get("theme_override_styles/panel")
	if not style:
		return
	if is_active:
		style.bg_color = Color(0.3, 0.28, 0.1, 0.9)
		style.border_color = _theme.border_highlight
		style.set_border_width_all(2)
	else:
		style.set_border_width_all(1)