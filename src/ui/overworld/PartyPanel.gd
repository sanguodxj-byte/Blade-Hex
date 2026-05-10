# PartyPanel.gd
# 大地图全屏系统界面（角色、物品、队伍编制 - 真实交互版）
extends PanelContainer
class_name PartyPanel

var content_label: Label
var stats_grid: GridContainer
var inv_grid: GridContainer
var roster_vbox: VBoxContainer

var selected_unit_data: UnitData
var economy_manager: Node

func _ready():
	economy_manager = get_node_or_null("/root/EconomyManager")
	_setup_ui()
	if economy_manager:
		# 强制连接信号
		if not economy_manager.inventory_changed.is_connected(refresh_ui):
			economy_manager.inventory_changed.connect(refresh_ui)

func _setup_ui():
	# 占满全屏
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 背景面板样式
	var style = StyleBoxFlat.new()
	style.bg_color = Color(0.12, 0.12, 0.12, 0.98) 
	style.set_border_width_all(4)
	style.border_color = Color(0.4, 0.3, 0.1) 
	add_theme_stylebox_override("panel", style)
	
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 40)
	margin.add_theme_constant_override("margin_right", 40)
	margin.add_theme_constant_override("margin_top", 40)
	margin.add_theme_constant_override("margin_bottom", 40)
	add_child(margin)
	
	var hbox = HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 30)
	margin.add_child(hbox)
	
	# ==============================
	# 左侧：队伍花名册
	# ==============================
	var left_panel = PanelContainer.new()
	left_panel.custom_minimum_size = Vector2(250, 0)
	var left_style = StyleBoxFlat.new()
	left_style.bg_color = Color(0.08, 0.08, 0.08)
	left_panel.add_theme_stylebox_override("panel", left_style)
	hbox.add_child(left_panel)
	
	roster_vbox = VBoxContainer.new()
	left_panel.add_child(roster_vbox)
	
	# ==============================
	# 右侧：详情面板
	# ==============================
	var right_vbox = VBoxContainer.new()
	right_vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_vbox.add_theme_constant_override("separation", 20)
	hbox.add_child(right_vbox)
	
	# 顶部标题与关闭按钮
	var header_hbox = HBoxContainer.new()
	right_vbox.add_child(header_hbox)
	
	content_label = Label.new()
	content_label.text = "角色详情"
	content_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content_label.add_theme_font_size_override("font_size", 28)
	header_hbox.add_child(content_label)
	
	var close_btn = Button.new()
	close_btn.text = " 返回游戏 (ESC) "
	close_btn.custom_minimum_size = Vector2(150, 40)
	close_btn.pressed.connect(func(): visible = false)
	header_hbox.add_child(close_btn)
	
	right_vbox.add_child(HSeparator.new())
	
	# 属性网格
	var section_title = Label.new()
	section_title.text = "【战斗统计与六维属性】"
	section_title.add_theme_color_override("font_color", Color(0.8, 0.7, 0.4))
	right_vbox.add_child(section_title)
	
	stats_grid = GridContainer.new()
	stats_grid.columns = 2
	stats_grid.add_theme_constant_override("h_separation", 80)
	stats_grid.add_theme_constant_override("v_separation", 15)
	right_vbox.add_child(stats_grid)
	
	right_vbox.add_child(HSeparator.new())
	
	# 物品栏
	var inv_title = Label.new()
	inv_title.text = "【队伍背包 - 点击装备到当前角色】"
	inv_title.add_theme_color_override("font_color", Color(0.8, 0.7, 0.4))
	right_vbox.add_child(inv_title)
	
	inv_grid = GridContainer.new()
	inv_grid.columns = 8
	inv_grid.add_theme_constant_override("h_separation", 10)
	inv_grid.add_theme_constant_override("v_separation", 10)
	right_vbox.add_child(inv_grid)

func refresh_ui():
	if not is_inside_tree() or not visible: return
	
	# 1. 刷新左侧名册
	for child in roster_vbox.get_children():
		child.queue_free()
	
	var member_btn = Button.new()
	member_btn.text = "雇佣兵团长 (战士)"
	member_btn.custom_minimum_size = Vector2(0, 60)
	roster_vbox.add_child(member_btn)
	
	# 2. 刷新属性
	for child in stats_grid.get_children():
		child.queue_free()
		
	if selected_unit_data:
		_add_stat("力量 (STR):", str(selected_unit_data.str))
		_add_stat("敏捷 (DEX):", str(selected_unit_data.dex))
		_add_stat("体质 (CON):", str(selected_unit_data.con))
		_add_stat("护甲 (AC):", str(_calculate_preview_ac()))
		_add_stat("主手装备:", selected_unit_data.primary_main_hand.item_name if selected_unit_data.primary_main_hand else "徒手")
		_add_stat("防具装备:", selected_unit_data.armor.item_name if selected_unit_data.armor else "无")
	
	# 3. 刷新背包
	for child in inv_grid.get_children():
		child.queue_free()
		
	if economy_manager:
		for item in economy_manager.player_inventory:
			var item_btn = Button.new()
			item_btn.text = item.item_name
			item_btn.custom_minimum_size = Vector2(80, 80)
			item_btn.pressed.connect(_on_item_clicked.bind(item))
			inv_grid.add_child(item_btn)

func _calculate_preview_ac() -> int:
	if not selected_unit_data: return 10
	var dex_mod = floor((selected_unit_data.dex - 10) / 2.0)
	var ac = 10
	if selected_unit_data.armor:
		ac = 10 + selected_unit_data.armor.ac_bonus
		dex_mod = min(dex_mod, selected_unit_data.armor.max_dex_bonus)
	return ac + dex_mod

func _on_item_clicked(item: ItemData):
	if selected_unit_data and economy_manager:
		selected_unit_data.equip_item(item, economy_manager)
		refresh_ui()

func _add_stat(stat_name: String, value: String):
	var row = HBoxContainer.new()
	var lbl_name = Label.new()
	lbl_name.text = stat_name
	lbl_name.custom_minimum_size = Vector2(150, 0)
	lbl_name.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))
	row.add_child(lbl_name)
	var lbl_val = Label.new()
	lbl_val.text = value
	row.add_child(lbl_val)
	stats_grid.add_child(row)

func open_tab(tab_name: String, unit_data: UnitData = null):
	selected_unit_data = unit_data
	visible = true
	refresh_ui()
	if tab_name == "character":
		content_label.text = "▶ 角色属性面板"
	elif tab_name == "inventory":
		content_label.text = "▶ 队伍战利品与背包"
	elif tab_name == "party":
		content_label.text = "▶ 队伍编制与阵型"
