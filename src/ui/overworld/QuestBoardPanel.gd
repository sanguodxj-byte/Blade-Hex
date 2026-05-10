# QuestBoardPanel.gd
# 委托面板 — 领取和查看可用的任务委托
extends CanvasLayer
class_name QuestBoardPanel

signal quest_accepted(quest_id: String)
signal board_closed()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()
var _root: Control
var _quest_list: VBoxContainer
var _result_label: RichTextLabel
var _accepted: Dictionary = {} ## 已接受的委托

## 预设委托模板
var _quest_templates: Array = []

func _ready():
	layer = 25
	_init_quests()
	_setup_ui()

func _init_quests():
	_quest_templates = [
		{
			"id": "q_goblin_camp",
			"title": "清剿哥布林营地",
			"desc": "附近的哥布林营地频繁骚扰村庄，领主悬赏讨伐。",
			"reward_gold": 80,
			"reward_xp": 50,
			"difficulty": "简单",
		},
		{
			"id": "q_undead_tomb",
			"title": "净化亡者墓穴",
			"desc": "古墓中的亡灵不断涌出，威胁周边安全。",
			"reward_gold": 150,
			"reward_xp": 100,
			"difficulty": "中等",
		},
		{
			"id": "q_dragon_slayer",
			"title": "讨伐霜冠巨龙",
			"desc": "霜冠山脉的巨龙开始袭击商队，急需勇士讨伐。",
			"reward_gold": 500,
			"reward_xp": 300,
			"difficulty": "困难",
		},
		{
			"id": "q_caravan_escort",
			"title": "护送商队",
			"desc": "一支商队需要护卫，安全护送到目的地即可获得报酬。",
			"reward_gold": 60,
			"reward_xp": 30,
			"difficulty": "简单",
		},
		{
			"id": "q_lost_relic",
			"title": "寻找远古遗物",
			"desc": "学者委托你深入矮人遗迹，寻找一件失落的古代遗物。",
			"reward_gold": 200,
			"reward_xp": 120,
			"difficulty": "中等",
		},
	]

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

	var panel = _factory.create_panel(Vector2(450, 450), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -225
	panel.offset_top = -235
	panel.offset_right = 225
	panel.offset_bottom = 235
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("委托板", 20))
	vbox.add_child(_factory.create_body_label("领主发布的悬赏任务，完成任务可获得金币和经验。"))
	vbox.add_child(_factory.create_separator_h())

	var scroll = _factory.create_scroll_container(false)
	vbox.add_child(scroll)

	_quest_list = VBoxContainer.new()
	_quest_list.add_theme_constant_override("separation", _theme.spacing_md)
	scroll.add_child(_quest_list)

	vbox.add_child(_factory.create_separator_h())
	_result_label = _factory.create_rich_text(Vector2(410, 40))
	vbox.add_child(_result_label)

	var close_btn = _factory.create_button("离开", Vector2(410, 40))
	close_btn.pressed.connect(func(): board_closed.emit(); hide_panel())
	vbox.add_child(close_btn)

func show_board() -> void:
	_result_label.text = ""
	_populate_quests()
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _populate_quests() -> void:
	for child in _quest_list.get_children():
		child.queue_free()
	for q in _quest_templates:
		var card = _factory.create_card(Vector2(410, 0), false)
		card.mouse_filter = Control.MOUSE_FILTER_PASS
		var inner_margin = _factory.create_margin(10, 10, 8, 8)
		inner_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		card.add_child(inner_margin)
		var qvbox = VBoxContainer.new()
		qvbox.add_theme_constant_override("separation", _theme.spacing_xs)
		qvbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		inner_margin.add_child(qvbox)

		var title_row = HBoxContainer.new()
		var title_lbl = _factory.create_body_label(q["title"])
		title_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		title_row.add_child(title_lbl)
		var diff_lbl = _factory.create_muted_label("[%s]" % q["difficulty"])
		title_row.add_child(diff_lbl)
		qvbox.add_child(title_row)

		var desc_lbl = _factory.create_muted_label(q["desc"])
		qvbox.add_child(desc_lbl)

		var reward_row = HBoxContainer.new()
		reward_row.add_child(_factory.create_muted_label("奖励: %d金 / %d经验" % [q["reward_gold"], q["reward_xp"]]))
		reward_row.add_child(_factory.create_separator_h())

		var accept_btn = _factory.create_button("接受", Vector2(80, 28))
		if _accepted.has(q["id"]):
			accept_btn.disabled = true
			accept_btn.text = "已接受"
		var qid = q["id"]
		accept_btn.pressed.connect(func(): _accept_quest(qid))
		reward_row.add_child(accept_btn)
		qvbox.add_child(reward_row)

		_quest_list.add_child(card)

func _accept_quest(quest_id: String) -> void:
	_accepted[quest_id] = true
	_result_label.text = "[color=green]已接受委托！在大地图上寻找目标完成它。[/color]"
	quest_accepted.emit(quest_id)
	_populate_quests()
