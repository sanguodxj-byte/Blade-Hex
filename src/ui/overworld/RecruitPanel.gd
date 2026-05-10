# RecruitPanel.gd
# 招募面板 — 邀请NPC加入队伍
extends CanvasLayer
class_name RecruitPanel

signal recruit_finished(hired: bool)

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()
var _root: Control
var _npc_name_label: Label
var _npc_info_label: Label
var _desc_label: RichTextLabel
var _cost_label: Label
var _result_label: RichTextLabel
var _current_profile: NPCProfile
var _economy: EconomyManager

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

	var panel = _factory.create_panel(Vector2(400, 350), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -200
	panel.offset_top = -185
	panel.offset_right = 200
	panel.offset_bottom = 185
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("招募", 20))

	_npc_name_label = _factory.create_body_label("")
	vbox.add_child(_npc_name_label)

	_npc_info_label = _factory.create_muted_label("")
	vbox.add_child(_npc_info_label)

	_desc_label = _factory.create_rich_text(Vector2(360, 80))
	vbox.add_child(_desc_label)

	vbox.add_child(_factory.create_separator_h())

	var cost_row = HBoxContainer.new()
	var cost_text = _factory.create_body_label("招募费用:")
	cost_text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	cost_row.add_child(cost_text)
	_cost_label = _factory.create_body_label("0金")
	_cost_label.add_theme_color_override("font_color", _theme.text_accent)
	cost_row.add_child(_cost_label)
	vbox.add_child(cost_row)

	_result_label = _factory.create_rich_text(Vector2(360, 40))
	vbox.add_child(_result_label)

	var btn_row = HBoxContainer.new()
	btn_row.add_theme_constant_override("separation", _theme.spacing_md)

	var hire_btn = _factory.create_button("招募", Vector2(175, 40))
	hire_btn.pressed.connect(func(): _do_recruit())

	var decline_btn = _factory.create_button("婉拒", Vector2(175, 40))
	decline_btn.pressed.connect(func(): recruit_finished.emit(false); hide_panel())

	btn_row.add_child(hire_btn)
	btn_row.add_child(decline_btn)
	vbox.add_child(btn_row)

func show_recruit(profile: NPCProfile, economy: EconomyManager = null) -> void:
	_current_profile = profile
	_economy = _economy
	_npc_name_label.text = profile.npc_name
	_npc_info_label.text = "%s · %s · 携带金币: %d" % [
		NPCProfile.get_npc_type_name(profile.npc_type),
		profile.get_attitude_text(),
		profile.gold
	]
	_desc_label.text = profile.get_description()
	_cost_label.text = "%d金" % profile.recruit_cost
	_result_label.text = ""
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _do_recruit() -> void:
	if not _current_profile:
		return
	var cost = _current_profile.recruit_cost
	if _economy and not _economy.spend_gold(cost):
		_result_label.text = "[color=red]金币不足！需要 %d 金。[/color]" % cost
		return
	_result_label.text = "[color=green]%s 加入了你的队伍！[/color]" % _current_profile.npc_name
	recruit_finished.emit(true)
	# 2秒后自动关闭
	await get_tree().create_timer(1.5).timeout
	hide_panel()
