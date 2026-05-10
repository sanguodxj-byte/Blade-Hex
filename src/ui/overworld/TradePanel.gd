# TradePanel.gd
# 交易面板 — 与NPC或城镇商店进行交易
extends CanvasLayer
class_name TradePanel

signal trade_finished()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()

var _root: Control
var _title_label: Label
var _gold_label: Label
var _shop_grid: GridContainer
var _inventory_grid: GridContainer
var _source_name: String = ""
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

	var panel = _factory.create_panel(Vector2(500, 450), _theme.bg_primary, _theme.border_default)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -250
	panel.offset_top = -250
	panel.offset_right = 250
	panel.offset_bottom = 250
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	# 标题行
	var header = HBoxContainer.new()
	_title_label = _factory.create_title_label("交易", 20)
	_title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_title_label)
	_gold_label = _factory.create_body_label("金币: ---")
	_gold_label.add_theme_color_override("font_color", _theme.text_accent)
	header.add_child(_gold_label)
	vbox.add_child(header)

	vbox.add_child(_factory.create_separator_h())

	# 商店商品区
	vbox.add_child(_factory.create_body_label("商店商品:"))
	_shop_grid = GridContainer.new()
	_shop_grid.columns = 4
	_shop_grid.add_theme_constant_override("h_separation", _theme.spacing_sm)
	_shop_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	vbox.add_child(_shop_grid)

	vbox.add_child(_factory.create_separator_h())

	# 玩家背包区
	vbox.add_child(_factory.create_body_label("你的物品:"))
	_inv_scroll = _factory.create_scroll_container(false)
	vbox.add_child(_inv_scroll)

	_inventory_grid = GridContainer.new()
	_inventory_grid.columns = 4
	_inventory_grid.add_theme_constant_override("h_separation", _theme.spacing_sm)
	_inventory_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	_inv_scroll.add_child(_inventory_grid)

	vbox.add_child(_factory.create_separator_h())

	# 关闭按钮
	var close_btn = _factory.create_button("离开商店", Vector2(460, 40))
	close_btn.pressed.connect(func(): trade_finished.emit(); hide_panel())
	vbox.add_child(close_btn)

var _inv_scroll: ScrollContainer

func show_trade(source_name: String, economy: EconomyManager = null) -> void:
	_source_name = _source_name
	_economy_manager = economy
	_title_label.text = "交易 — %s" % _source_name
	_update_gold()
	_populate_shop()
	_populate_inventory()
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _update_gold() -> void:
	if _economy_manager:
		_gold_label.text = "金币: %d" % _economy_manager.gold
	else:
		_gold_label.text = "金币: ---"

func _populate_shop() -> void:
	for child in _shop_grid.get_children():
		child.queue_free()
	# 预设商品
	var shop_items = [
		{"name": "治疗药剂", "price": 25, "desc": "恢复 2d4+2 HP"},
		{"name": "磨刀石", "price": 50, "desc": "武器伤害+1（一次战斗）"},
		{"name": "干粮", "price": 10, "desc": "恢复5点食物"},
		{"name": "绷带", "price": 15, "desc": "恢复1d6 HP"},
	]
	for item in shop_items:
		var slot = _factory.create_item_slot(80)
		var vbox = VBoxContainer.new()
		vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		var name_lbl = _factory.create_muted_label(item["name"])
		name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		vbox.add_child(name_lbl)
		var price_lbl = _factory.create_muted_label("%d金" % item["price"])
		price_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		price_lbl.add_theme_color_override("font_color", _theme.text_accent)
		vbox.add_child(price_lbl)
		slot.add_child(vbox)
		slot.tooltip_text = item["desc"]
		_shop_grid.add_child(slot)

func _populate_inventory() -> void:
	for child in _inventory_grid.get_children():
		child.queue_free()
	if _economy_manager and _economy_manager.has_method("get_inventory"):
		var items = _economy_manager.get_inventory()
		for item in items:
			var slot = _factory.create_item_slot(80)
			var name_lbl = _factory.create_muted_label(item.item_name if item else "?")
			name_lbl.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			slot.add_child(name_lbl)
			_inventory_grid.add_child(slot)
	else:
		var empty_lbl = _factory.create_muted_label("背包为空")
		_inventory_grid.add_child(empty_lbl)
