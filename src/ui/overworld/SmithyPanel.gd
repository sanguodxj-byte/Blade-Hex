# SmithyPanel.gd
# 铁匠铺面板 — 修理和升级装备
extends CanvasLayer
class_name SmithyPanel

signal smithy_finished()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()
var _root: Control
var _gold_label: Label
var _result_label: RichTextLabel
var _equip_list: VBoxContainer
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

	var panel = _factory.create_panel(Vector2(420, 420), _theme.bg_primary, _theme.border_default)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -210
	panel.offset_top = -220
	panel.offset_right = 210
	panel.offset_bottom = 220
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("铁匠铺", 20))
	vbox.add_child(_factory.create_body_label("经验丰富的铁匠可以帮你修理和强化装备。"))

	var header = HBoxContainer.new()
	var title = _factory.create_body_label("金币:")
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	_gold_label = _factory.create_body_label("0")
	_gold_label.add_theme_color_override("font_color", _theme.text_accent)
	header.add_child(_gold_label)
	vbox.add_child(header)

	vbox.add_child(_factory.create_separator_h())

	# 服务按钮
	var btn_repair = _factory.create_button("全套修理 (30金) — 恢复所有装备耐久", Vector2(380, 40))
	btn_repair.pressed.connect(func(): _do_service("repair", 30))
	vbox.add_child(btn_repair)

	var btn_sharpen = _factory.create_button("磨砺武器 (50金) — 武器伤害+1", Vector2(380, 40))
	btn_sharpen.pressed.connect(func(): _do_service("sharpen", 50))
	vbox.add_child(btn_sharpen)

	var btn_reinforce = _factory.create_button("加固防具 (80金) — AC+1", Vector2(380, 40))
	btn_reinforce.pressed.connect(func(): _do_service("reinforce", 80))
	vbox.add_child(btn_reinforce)

	vbox.add_child(_factory.create_separator_h())
	_result_label = _factory.create_rich_text(Vector2(380, 40))
	vbox.add_child(_result_label)

	var close_btn = _factory.create_button("离开铁匠铺", Vector2(380, 40))
	close_btn.pressed.connect(func(): smithy_finished.emit(); hide_panel())
	vbox.add_child(close_btn)

func show_smithy(economy: EconomyManager = null) -> void:
	_economy = _economy
	_result_label.text = ""
	_gold_label.text = "%d" % (_economy.gold if _economy else 0)
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _do_service(service: String, cost: int) -> void:
	if _economy and not _economy.spend_gold(cost):
		_result_label.text = "[color=red]金币不足！[/color]"
		return
	match service:
		"repair":
			_result_label.text = "[color=green]所有装备已修理完毕，耐久完全恢复。[/color]"
		"sharpen":
			_result_label.text = "[color=green]武器已磨砺，伤害+1！[/color]"
		"reinforce":
			_result_label.text = "[color=green]防具已加固，AC+1！[/color]"
	if _economy:
		_gold_label.text = "%d" % _economy.gold
