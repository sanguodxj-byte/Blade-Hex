import re

with open('src/ui/combat/CombatUI.gd', 'r', encoding='utf-8') as f:
    content = f.read()

# Define the new _setup_ui function
new_setup_ui = """
func _setup_ui():
	# Root layout with margins to prevent elements touching screen edges
	var root = MarginContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_theme_constant_override("margin_left", _theme.spacing_md)
	root.add_theme_constant_override("margin_right", _theme.spacing_md)
	root.add_theme_constant_override("margin_top", _theme.spacing_md)
	root.add_theme_constant_override("margin_bottom", _theme.spacing_md)
	add_child(root)
	
	var main_vbox = VBoxContainer.new()
	main_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(main_vbox)
	
	# ================================================================
	# 顶部区域 (Top Area)
	# ================================================================
	var top_area = HBoxContainer.new()
	top_area.mouse_filter = Control.MOUSE_FILTER_IGNORE
	main_vbox.add_child(top_area)
	
	# 战斗日志 (左上角)
	battle_log = BattleLogPanel.new()
	battle_log.custom_minimum_size = Vector2(280, 140)
	top_area.add_child(battle_log)
	
	# 弹性占位，将敌方信息推向右侧
	var top_spacer = Control.new()
	top_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top_spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_area.add_child(top_spacer)
	
	# 敌方栏 (右上角)
	var enemy_vbox = VBoxContainer.new()
	enemy_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	enemy_vbox.alignment = BoxContainer.ALIGNMENT_END
	top_area.add_child(enemy_vbox)
	
	# 敌方缩略图列表 (右对齐)
	var enemy_list_hbox = HBoxContainer.new()
	enemy_list_hbox.name = "EnemyList"
	enemy_list_hbox.alignment = BoxContainer.ALIGNMENT_END
	enemy_list_hbox.add_theme_constant_override("separation", 5)
	enemy_vbox.add_child(enemy_list_hbox)
	
	# 敌方信息面板 (紧贴缩略图下方)
	enemy_info_panel = EnemyInfoPanel.new()
	enemy_info_panel.custom_minimum_size = Vector2(260, 0)
	enemy_vbox.add_child(enemy_info_panel)
	
	# ================================================================
	# 中部弹性区域 (Middle Area) - 纯透明
	# ================================================================
	var middle_spacer = Control.new()
	middle_spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	middle_spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	main_vbox.add_child(middle_spacer)
	
	# ================================================================
	# 底部区域 (Bottom Area)
	# ================================================================
	var bottom_area = VBoxContainer.new()
	bottom_area.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bottom_area.add_theme_constant_override("separation", 5)
	main_vbox.add_child(bottom_area)
	
	# 我方缩略图列表 (左对齐)
	var ally_list_hbox = HBoxContainer.new()
	ally_list_hbox.name = "AllyList"
	ally_list_hbox.add_theme_constant_override("separation", 5)
	bottom_area.add_child(ally_list_hbox)
	
	# 底栏面板
	bottom_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_primary, _theme.border_default)
	bottom_area.add_child(bottom_panel)
	
	var bottom_margin = _factory.create_margin(12, 12, 10, 10)
	bottom_panel.add_child(bottom_margin)
	
	var bottom_hbox := HBoxContainer.new()
	bottom_hbox.add_theme_constant_override("separation", _theme.spacing_lg)
	bottom_margin.add_child(bottom_hbox)
	
	# --- 4a. 头像区 ---
	var avatar_bg = _factory.create_card(Vector2(80, 80), false)
	bottom_hbox.add_child(avatar_bg)
	avatar_rect = TextureRect.new()
	avatar_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	avatar_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	avatar_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	avatar_bg.add_child(avatar_rect)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4b. 信息列 (名称 + HP/MP + 六维) ---
	var info_col := VBoxContainer.new()
	info_col.add_theme_constant_override("separation", 2)
	bottom_hbox.add_child(info_col)
	
	var char_name = _factory.create_body_label("未选择", _theme.text_accent)
	char_name.set_meta("stat_key", "char_name")
	info_col.add_child(char_name)
	stat_labels["char_name"] = char_name
	
	# HP & MP Bars (Compact)
	var hp_bar = _factory.create_hp_bar(140, 8)
	hp_bar.set_meta("stat_key", "hp_bar")
	info_col.add_child(hp_bar)
	stat_labels["hp_bar"] = hp_bar
	
	var mp_bar = _factory.create_mana_bar(140, 6)
	mp_bar.set_meta("stat_key", "mp_bar")
	info_col.add_child(mp_bar)
	stat_labels["mp_bar"] = mp_bar
	
	# 六维属性 (紧贴 MP 下方)
	var attr_grid := GridContainer.new()
	attr_grid.columns = 3
	attr_grid.add_theme_constant_override("h_separation", 12)
	attr_grid.add_theme_constant_override("v_separation", 0)
	info_col.add_child(attr_grid)
	_create_attr_label(attr_grid, "str", "力")
	_create_attr_label(attr_grid, "dex", "敏")
	_create_attr_label(attr_grid, "con", "体")
	_create_attr_label(attr_grid, "intel", "智")
	_create_attr_label(attr_grid, "wis", "感")
	_create_attr_label(attr_grid, "cha", "魅")
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4c. 战斗属性 ---
	var combat_grid := GridContainer.new()
	combat_grid.columns = 2
	combat_grid.add_theme_constant_override("h_separation", 15)
	combat_grid.add_theme_constant_override("v_separation", 2)
	bottom_hbox.add_child(combat_grid)
	_create_stat_label(combat_grid, "ac", "防御", "10")
	_create_stat_label(combat_grid, "atk", "命中", "+0")
	_create_stat_label(combat_grid, "dmg", "伤害", "1-3")
	_create_stat_label(combat_grid, "mov", "移动", "4格")
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4d. 武器槽 (缩小到 50x50) ---
	var weapon_vbox = VBoxContainer.new()
	weapon_vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	weapon_vbox.add_theme_constant_override("separation", 4)
	bottom_hbox.add_child(weapon_vbox)
	
	var main_hand = _factory.create_card(Vector2(50, 50), true)
	main_hand.gui_input.connect(func(ev): if ev is InputEventMouseButton and ev.pressed: action_selected.emit("swap_weapon"))
	weapon_vbox.add_child(main_hand)
	weapon_primary_label = _factory.create_muted_label("主手", _theme.font_size_xs)
	main_hand.add_child(weapon_primary_label)
	
	var off_hand = _factory.create_card(Vector2(50, 50), true)
	off_hand.gui_input.connect(func(ev): if ev is InputEventMouseButton and ev.pressed: action_selected.emit("swap_weapon"))
	weapon_vbox.add_child(off_hand)
	weapon_secondary_label = _factory.create_muted_label("副手", _theme.font_size_xs)
	off_hand.add_child(weapon_secondary_label)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4e. 快捷技能/法术栏 (新预留空间) ---
	var quick_actions = HBoxContainer.new()
	quick_actions.name = "QuickActions"
	quick_actions.add_theme_constant_override("separation", 6)
	bottom_hbox.add_child(quick_actions)
	
	# 占位符按钮，后续可通过代码动态添加
	for i in range(5):
		var placeholder = _factory.create_card(Vector2(45, 45), true)
		placeholder.modulate.a = 0.3
		quick_actions.add_child(placeholder)
	
	var b_spacer = Control.new()
	b_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	b_spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bottom_hbox.add_child(b_spacer)
	
	# --- 4f. 结束回合按钮 (右下角) ---
	var end_turn_btn = Button.new()
	end_turn_btn.text = "结束回合"
	end_turn_btn.custom_minimum_size = Vector2(100, 45)
	end_turn_btn.pressed.connect(func(): action_selected.emit("end_turn"))
	var btn_style = _theme.make_panel_style(_theme.bg_secondary, _theme.border_friendly, _theme.radius_md)
	end_turn_btn.add_theme_stylebox_override("normal", btn_style)
	bottom_hbox.add_child(end_turn_btn)
	
	# ================================================================
	# 其他悬浮组件
	# ================================================================
	
	# 回合顺序条 (悬浮顶部中央)
	turn_order_bar = TurnOrderBar.new()
	turn_order_bar.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	turn_order_bar.custom_minimum_size = Vector2(0, 60)
	turn_order_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(turn_order_bar)

	# 命中率/地形提示
	hit_preview_tooltip = HitPreviewTooltip.new()
	hit_preview_tooltip.visible = false
	add_child(hit_preview_tooltip)
	
	terrain_tooltip = TerrainTooltip.new()
	terrain_tooltip.visible = false
	add_child(terrain_tooltip)
	
	# 详情面板与轮盘
	character_detail = CharacterDetailPanel.new()
	character_detail.visible = false
	add_child(character_detail)
	
	radial_menu = RadialMenu.new()
	radial_menu.visible = false
	radial_menu.action_selected.connect(func(act): action_selected.emit(act))
	add_child(radial_menu)

	# ESC 暂停菜单
	_setup_esc_menu()

func _setup_esc_menu():
	esc_menu = PanelContainer.new()
	esc_menu.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	esc_menu.visible = false
	add_child(esc_menu)
	# ... (简化的暂停菜单逻辑，保留核心按钮)
	var center = CenterContainer.new()
	esc_menu.add_child(center)
	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 10)
	center.add_child(vbox)
	var resume = Button.new()
	resume.text = "返回战斗"
	resume.pressed.connect(func(): esc_menu.visible = false)
	vbox.add_child(resume)
	var quit = Button.new()
	quit.text = "退出"
	quit.pressed.connect(func(): get_tree().quit())
	vbox.add_child(quit)
"""

# Replace the entire _setup_ui function and everything up to the end of _setup_esc_menu (if it exists)
# or just target the section we know is messy.
content = re.sub(r'func _setup_ui\(\):.*?func _setup_confirm_dialog\(root\):', new_setup_ui + '\n\nfunc _setup_confirm_dialog(root):', content, flags=re.DOTALL)

# Also fix the set_action_bar_visible which might error if action_bar is removed
content = re.sub(r'func set_action_bar_visible\(isvisible: bool\):.*?action_bar\.visible = isvisible', 'func set_action_bar_visible(isvisible: bool):\n\tpass', content, flags=re.DOTALL)

with open('src/ui/combat/CombatUI.gd', 'w', encoding='utf-8') as f:
    f.write(content)
print('CombatUI.gd layout updated successfully.')
