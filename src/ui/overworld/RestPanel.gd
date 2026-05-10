# RestPanel.gd
# 休息面板 — 在酒馆/旅店休息恢复状态
extends CanvasLayer
class_name RestPanel

signal rest_completed(hours: int)

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()

var _root: Control
var _status_label: Label
var _economy_manager: EconomyManager = null

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

	var panel = _factory.create_panel(Vector2(350, 300), _theme.bg_primary, _theme.border_default)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -175
	panel.offset_top = -175
	panel.offset_right = 175
	panel.offset_bottom = 175
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("休息", 20))

	vbox.add_child(_factory.create_body_label("在安全的地方休息，恢复队伍状态。"))

	vbox.add_child(_factory.create_separator_h())

	# 状态信息
	_status_label = _factory.create_body_label("")
	vbox.add_child(_status_label)

	# 休息选项
	var rest_short = _factory.create_button("短暂休息 (免费，恢复30%HP)", Vector2(310, 40))
	rest_short.pressed.connect(func(): _do_rest(0, 0.3))
	vbox.add_child(rest_short)

	var rest_long = _factory.create_button("充分休息 (10金，恢复100%HP)", Vector2(310, 40))
	rest_long.pressed.connect(func(): _do_rest(10, 1.0))
	vbox.add_child(rest_long)

	vbox.add_child(_factory.create_separator_h())

	var close_btn = _factory.create_button("离开", Vector2(310, 40))
	close_btn.pressed.connect(func(): hide_panel())
	vbox.add_child(close_btn)

func show_rest(economy: EconomyManager = null) -> void:
	_economy_manager = economy
	if economy:
		_status_label.text = "当前金币: %d  食物: %.1f" % [economy.gold, economy.food]
	else:
		_status_label.text = ""
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _do_rest(cost: int, hp_ratio: float) -> void:
	if _economy_manager and cost > 0:
		if not _economy_manager.spend_gold(cost):
			_status_label.text = "金币不足！"
			return
	_economy_manager.advance_time(8.0)
	_status_label.text = "队伍已休息完毕，恢复了%.0f%%生命值。" % (hp_ratio * 100)
	rest_completed.emit(8)
	_update_status()

func _update_status() -> void:
	if _economy_manager:
		_status_label.text += "\n当前金币: %d" % _economy_manager.gold

