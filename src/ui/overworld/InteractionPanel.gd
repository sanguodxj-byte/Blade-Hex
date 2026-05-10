# InteractionPanel.gd
# 遭遇交互主面板 — 玩家遭遇敌人/城镇时显示可用交互选项
extends CanvasLayer
class_name InteractionPanel

signal option_selected(option: InteractionOption)
signal close_requested()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()

var _root: Control
var _title_label: Label
var _desc_label: RichTextLabel
var _info_label: Label
var _options_vbox: VBoxContainer
var _current_entity = null

func _ready():
	layer = 20
	_setup_ui()

func _setup_ui():
	_root = Control.new()
	_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.visible = false
	add_child(_root)

	# 半透明遮罩
	var overlay = ColorRect.new()
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.color = Color(0, 0, 0, 0.5)
	overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	overlay.gui_input.connect(_on_overlay_input)
	_root.add_child(overlay)

	# 中央面板
	var panel = _factory.create_panel(Vector2(400, 0), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -200
	panel.offset_top = -200
	panel.offset_right = 200
	panel.offset_bottom = 200
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 20, 20)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	# 标题
	_title_label = _factory.create_title_label("")
	vbox.add_child(_title_label)

	# 态度/类型信息
	_info_label = _factory.create_muted_label("")
	vbox.add_child(_info_label)

	# 描述
	_desc_label = _factory.create_rich_text(Vector2(360, 60))
	vbox.add_child(_desc_label)

	# 分割线
	vbox.add_child(_factory.create_separator_h())

	# 选项列表容器
	_options_vbox = VBoxContainer.new()
	_options_vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	vbox.add_child(_options_vbox)

func show_for_entity(entity, options: Array) -> void:
	_current_entity = entity
	_title_label.text = _get_entity_name(entity)
	_info_label.text = _get_entity_info(entity)
	_desc_label.text = _get_entity_description(entity)
	_populate_options(options)
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false
	_current_entity = null
	_clear_options()

func is_panel_visible() -> bool:
	return _root.visible

func _get_entity_name(entity) -> String:
	if entity is OverworldEnemy:
		return entity.get_display_name()
	if entity is OverworldTown:
		return entity.town_name
	return "未知"

func _get_entity_info(entity) -> String:
	if entity is OverworldEnemy and entity.npc_profile:
		var profile = entity.npc_profile
		return "%s · %s" % [NPCProfile.get_npc_type_name(profile.npc_type), profile.get_attitude_text()]
	if entity is OverworldTown:
		var type_text = "村庄" if entity.town_type == "village" else "城镇"
		return "%s · 繁荣度: %d" % [type_text, entity.prosperity]
	return ""

func _get_entity_description(entity) -> String:
	if entity is OverworldEnemy:
		return entity.get_description()
	if entity is OverworldTown:
		return entity.get_description()
	return ""

func _populate_options(options: Array) -> void:
	_clear_options()
	for opt in options:
		var btn = _factory.create_button(opt.label, Vector2(360, 44))
		btn.tooltip_text = opt.tooltip if opt.tooltip else ""
		btn.disabled = not opt.enabled
		if not opt.enabled:
			btn.modulate.a = 0.4
		var captured_opt = opt
		btn.pressed.connect(func(): option_selected.emit(captured_opt))
		_options_vbox.add_child(btn)

func _clear_options() -> void:
	for child in _options_vbox.get_children():
		child.queue_free()

func _on_overlay_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		close_requested.emit()
		hide_panel()

