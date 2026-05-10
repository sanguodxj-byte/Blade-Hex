# TownPanel.gd
# 城镇面板 — 进入城镇/村庄时显示设施列表，允许玩家选择交互
extends CanvasLayer
class_name TownPanel

signal facility_selected(facility_type: int)
signal leave_town()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()

var _root: Control
var _town_name_label: Label
var _town_info_label: Label
var _town_desc_label: RichTextLabel
var _facilities_grid: GridContainer
var _current_town: OverworldTown = null

func _ready():
	layer = 25
	_setup_ui()

func _setup_ui():
	_root = Control.new()
	_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.visible = false
	add_child(_root)

	var overlay = ColorRect.new()
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.color = Color(0, 0, 0, 0.6)
	overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(overlay)

	var panel = _factory.create_panel(Vector2(450, 400), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -225
	panel.offset_top = -225
	panel.offset_right = 225
	panel.offset_bottom = 225
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	# 城镇名称
	_town_name_label = _factory.create_title_label("")
	vbox.add_child(_town_name_label)

	# 城镇信息
	_town_info_label = _factory.create_muted_label("")
	vbox.add_child(_town_info_label)

	# 描述
	_town_desc_label = _factory.create_rich_text(Vector2(410, 50))
	vbox.add_child(_town_desc_label)

	vbox.add_child(_factory.create_separator_h())

	# 设施按钮网格
	vbox.add_child(_factory.create_body_label("设施:"))
	_facilities_grid = GridContainer.new()
	_facilities_grid.columns = 2
	_facilities_grid.add_theme_constant_override("h_separation", _theme.spacing_md)
	_facilities_grid.add_theme_constant_override("v_separation", _theme.spacing_md)
	vbox.add_child(_facilities_grid)

	vbox.add_child(_factory.create_separator_h())

	# 离开按钮
	var leave_btn = _factory.create_button("离开城镇", Vector2(410, 40))
	leave_btn.pressed.connect(func(): leave_town.emit(); hide_panel())
	vbox.add_child(leave_btn)

func show_town(town: OverworldTown) -> void:
	_current_town = town
	_town_name_label.text = town.town_name
	var type_text = "村庄" if town.town_type == "village" else "城镇"
	_town_info_label.text = "%s · 繁荣度: %d · 守军: %d" % [type_text, town.prosperity, town.garrison]
	_town_desc_label.text = town.get_description()
	_populate_facilities()
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false
	_current_town = null
	_clear_facilities()

func is_panel_visible() -> bool:
	return _root.visible

func _populate_facilities() -> void:
	_clear_facilities()
	if not _current_town:
		return
	# 确保设施已初始化
	if _current_town.facilities.is_empty():
		if _current_town.town_type == "village":
			_current_town.setup_village_facilities()
		else:
			_current_town.setup_default_facilities()

	for facility in _current_town.facilities:
		if not facility.is_available:
			continue
		var btn = _factory.create_button(facility.facility_name, Vector2(195, 50))
		btn.tooltip_text = facility.description
		var ftype: int = facility.facility_type
		btn.pressed.connect(func(): facility_selected.emit(ftype))
		_facilities_grid.add_child(btn)

func _clear_facilities() -> void:
	for child in _facilities_grid.get_children():
		child.queue_free()

