# DialoguePanel.gd
# 对话面板 — 与人形NPC交谈时显示对话内容
extends CanvasLayer
class_name DialoguePanel

signal dialogue_finished()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()

var _root: Control
var _npc_name_label: Label
var _npc_info_label: Label
var _dialogue_text: RichTextLabel
var _responses_vbox: VBoxContainer
var _current_profile: NPCProfile
var _dialogue_data: Dictionary

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

	# 对话框面板（底部）
	var panel = _factory.create_panel(Vector2(600, 250), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	panel.offset_left = (1280.0 - 600.0) / 2.0
	panel.offset_top = -280
	panel.offset_right = -(1280.0 - 600.0) / 2.0
	panel.offset_bottom = -15
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	# NPC名称
	_npc_name_label = _factory.create_title_label("", 20)
	vbox.add_child(_npc_name_label)

	# NPC态度
	_npc_info_label = _factory.create_muted_label("")
	vbox.add_child(_npc_info_label)

	# 对话文本
	_dialogue_text = _factory.create_rich_text(Vector2(560, 80))
	vbox.add_child(_dialogue_text)

	# 分割线
	vbox.add_child(_factory.create_separator_h())

	# 回复选项
	_responses_vbox = VBoxContainer.new()
	_responses_vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	vbox.add_child(_responses_vbox)

func show_dialogue(profile: NPCProfile) -> void:
	_current_profile = profile
	_dialogue_data = profile.dialogue_lines if not profile.dialogue_lines.is_empty() else profile.get_default_dialogue()

	_npc_name_label.text = profile.npc_name
	_npc_info_label.text = "%s · %s" % [NPCProfile.get_npc_type_name(profile.npc_type), profile.get_attitude_text()]

	# 显示问候语
	var greeting = _dialogue_data.get("greeting", "……")
	_dialogue_text.text = greeting

	# 显示回复选项
	_populate_responses()
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false
	_current_profile = null
	_clear_responses()

func is_panel_visible() -> bool:
	return _root.visible

func _populate_responses() -> void:
	_clear_responses()
	var options = _dialogue_data.get("options", [])
	var _responses = _dialogue_data.get("responses", [])

	for i in range(options.size()):
		var btn = _factory.create_button(options[i], Vector2(560, 36))
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		var idx = i
		btn.pressed.connect(_on_response_selected.bind(idx))
		_responses_vbox.add_child(btn)

func _clear_responses() -> void:
	for child in _responses_vbox.get_children():
		child.queue_free()

func _on_response_selected(index: int) -> void:
	var responses = _dialogue_data.get("responses", [])
	if index < responses.size():
		_dialogue_text.text = responses[index]
	else:
		_dialogue_text.text = "……"

	_clear_responses()
	# 显示关闭按钮
	var close_btn = _factory.create_button("结束对话", Vector2(560, 36))
	close_btn.pressed.connect(func():
		dialogue_finished.emit()
		hide_panel()
	)
	_responses_vbox.add_child(close_btn)

