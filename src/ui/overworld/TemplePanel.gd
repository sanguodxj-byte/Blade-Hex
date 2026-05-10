# TemplePanel.gd
# 神殿面板 — 治疗伤病、购买圣水、净化诅咒
extends CanvasLayer
class_name TemplePanel

signal temple_finished()

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

	var panel = _factory.create_panel(Vector2(400, 360), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -200
	panel.offset_top = -190
	panel.offset_right = 200
	panel.offset_bottom = 190
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("神殿", 20))
	vbox.add_child(_factory.create_body_label("神圣的力量可以治愈伤病，净化邪恶。"))

	var header = HBoxContainer.new()
	var t = _factory.create_body_label("金币:")
	t.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(t)
	_gold_label = _factory.create_body_label("0")
	_gold_label.add_theme_color_override("font_color", _theme.text_accent)
	header.add_child(_gold_label)
	vbox.add_child(header)
	vbox.add_child(_factory.create_separator_h())

	var btn_minor = _factory.create_button("轻度治疗 (15金) — 恢复50%HP", Vector2(360, 40))
	btn_minor.pressed.connect(func(): _heal(15, 0.5, "轻度治疗"))
	vbox.add_child(btn_minor)

	var btn_major = _factory.create_button("深度治疗 (40金) — 恢复100%HP", Vector2(360, 40))
	btn_major.pressed.connect(func(): _heal(40, 1.0, "深度治疗"))
	vbox.add_child(btn_major)

	var btn_purify = _factory.create_button("净化诅咒 (60金) — 移除所有负面状态", Vector2(360, 40))
	btn_purify.pressed.connect(func(): _purify())
	vbox.add_child(btn_purify)

	var btn_water = _factory.create_button("购买圣水 (25金) — 对亡灵额外伤害", Vector2(360, 40))
	btn_water.pressed.connect(func(): _buy_holy_water())
	vbox.add_child(btn_water)

	vbox.add_child(_factory.create_separator_h())
	_result_label = _factory.create_rich_text(Vector2(360, 40))
	vbox.add_child(_result_label)

	var close_btn = _factory.create_button("离开神殿", Vector2(360, 40))
	close_btn.pressed.connect(func(): temple_finished.emit(); hide_panel())
	vbox.add_child(close_btn)

func show_temple(economy: EconomyManager = null) -> void:
	_economy = _economy
	_result_label.text = ""
	_gold_label.text = "%d" % (_economy.gold if _economy else 0)
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _heal(cost: int, ratio: float, name: String) -> void:
	if _economy and not _economy.spend_gold(cost):
		_result_label.text = "[color=red]金币不足！[/color]"
		return
	_result_label.text = "[color=green]%s完成！恢复了%.0f%%生命值。[/color]" % [name, ratio * 100]
	_update_gold()

func _purify() -> void:
	if _economy and not _economy.spend_gold(60):
		_result_label.text = "[color=red]金币不足！[/color]"
		return
	_result_label.text = "[color=green]神圣的光芒笼罩全身，所有诅咒已净化。[/color]"
	_update_gold()

func _buy_holy_water() -> void:
	if _economy and not _economy.spend_gold(25):
		_result_label.text = "[color=red]金币不足！[/color]"
		return
	_result_label.text = "[color=green]获得圣水×1。对亡灵类敌人造成额外1d6神圣伤害。[/color]"
	_update_gold()

func _update_gold() -> void:
	if _economy:
		_gold_label.text = "%d" % _economy.gold
