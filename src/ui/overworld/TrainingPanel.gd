# TrainingPanel.gd
# 训练场面板 — 花费金币提升角色属性和经验
extends CanvasLayer
class_name TrainingPanel

signal training_finished()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()
var _root: Control
var _gold_label: Label
var _result_label: RichTextLabel
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

	var panel = _factory.create_panel(Vector2(400, 380), _theme.bg_primary, _theme.border_default)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -200
	panel.offset_top = -200
	panel.offset_right = 200
	panel.offset_bottom = 200
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("训练场", 20))
	vbox.add_child(_factory.create_body_label("花费金币进行特训，提升角色能力。"))

	var header = HBoxContainer.new()
	var t = _factory.create_body_label("金币:")
	t.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(t)
	_gold_label = _factory.create_body_label("0")
	_gold_label.add_theme_color_override("font_color", _theme.text_accent)
	header.add_child(_gold_label)
	vbox.add_child(header)

	vbox.add_child(_factory.create_separator_h())

	# 训练项目
	var trainings = [
		{"name": "力量特训 (40金)", "cost": 40, "stat": "str", "desc": "力量+1"},
		{"name": "敏捷特训 (40金)", "cost": 40, "stat": "dex", "desc": "敏捷+1"},
		{"name": "体质特训 (40金)", "cost": 40, "stat": "con", "desc": "体质+1"},
		{"name": "智力特训 (40金)", "cost": 40, "stat": "int", "desc": "智力+1"},
		{"name": "综合训练 (100金)", "cost": 100, "stat": "all", "desc": "全属性+1"},
	]
	for t_item in trainings:
		var btn = _factory.create_button(t_item["name"], Vector2(360, 36))
		btn.tooltip_text = t_item["desc"]
		var cost = t_item["cost"]
		var stat = t_item["stat"]
		var desc = t_item["desc"]
		btn.pressed.connect(func(): _train(cost, stat, desc))
		vbox.add_child(btn)

	vbox.add_child(_factory.create_separator_h())
	_result_label = _factory.create_rich_text(Vector2(360, 40))
	vbox.add_child(_result_label)

	var close_btn = _factory.create_button("离开训练场", Vector2(360, 40))
	close_btn.pressed.connect(func(): training_finished.emit(); hide_panel())
	vbox.add_child(close_btn)

func show_training(economy: EconomyManager = null) -> void:
	_economy = _economy
	_result_label.text = ""
	_gold_label.text = "%d" % (_economy.gold if _economy else 0)
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _train(cost: int, _stat: String, desc: String):
	if _economy and not _economy.spend_gold(cost):
		_result_label.text = "[color=red]金币不足！[/color]"
		return
	_result_label.text = "[color=green]训练完成！%s[/color]" % desc
	if _economy:
		_gold_label.text = "%d" % _economy.gold
