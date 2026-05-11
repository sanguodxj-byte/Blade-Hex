import re

with open('src/ui/combat/CombatUI.gd', 'r', encoding='utf-8') as f:
    content = f.read()

replacement = """
func _setup_ui():
	# 彻底重构的流式布局（上部/中部/下部）
	var main_vbox = VBoxContainer.new()
	main_vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(main_vbox)
	
	# ================================================================
	# 顶部区域 (Top Area)
	# ================================================================
	var top_area = VBoxContainer.new()
	top_area.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_area.add_theme_constant_override("separation", _theme.spacing_md)
	main_vbox.add_child(top_area)
	
	# 1. 顶栏背景与回合信息 (Turn Order Bar)
	var top_panel = PanelContainer.new()
	top_panel.add_theme_stylebox_override("panel", _theme.make_panel_style(_theme.bg_secondary, _theme.border_friendly, _theme.radius_lg))
	top_area.add_child(top_panel)
	
	var top_hbox := HBoxContainer.new()
	top_panel.add_child(top_hbox)
	
	phase_label = _factory.create_title_label("等待战斗开始...", _theme.text_secondary)
	top_hbox.add_child(phase_label)
	top_hbox.add_child(_factory.create_separator_v())
	
	top_info_label = _factory.create_body_label("当前无操作", _theme.text_muted)
	top_hbox.add_child(top_info_label)
	
	var top_spacer = Control.new()
	top_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top_hbox.add_child(top_spacer)
	
	turn_order_bar = TurnOrderBar.new()
	top_hbox.add_child(turn_order_bar)

	# 1.1 顶栏下方的第二排 (日志与敌方列表)
	var top_second_row = HBoxContainer.new()
	top_second_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_area.add_child(top_second_row)
	
	# 战斗日志 (左上角)
	var log_margin = MarginContainer.new()
	log_margin.add_theme_constant_override("margin_left", _theme.spacing_md)
	log_margin.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	top_second_row.add_child(log_margin)
	
	battle_log = BattleLogPanel.new()
	battle_log.custom_minimum_size = Vector2(280, 160)
	log_margin.add_child(battle_log)
	
	# 透明占位，推开两边
	var row2_spacer = Control.new()
	row2_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row2_spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_second_row.add_child(row2_spacer)
	
	# 敌方列表与信息面板 (右上角)
	var right_vbox = VBoxContainer.new()
	right_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	right_vbox.alignment = BoxContainer.ALIGNMENT_END
	top_second_row.add_child(right_vbox)
	
	# 新增敌方缩略图列表
	var enemy_list_scroll = ScrollContainer.new()
	enemy_list_scroll.custom_minimum_size = Vector2(300, 70)
	enemy_list_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	enemy_list_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	right_vbox.add_child(enemy_list_scroll)
	
	var enemy_list_hbox = HBoxContainer.new()
	enemy_list_hbox.name = "EnemyList"
	enemy_list_hbox.alignment = BoxContainer.ALIGNMENT_END
	enemy_list_scroll.add_child(enemy_list_hbox)
	
	# 敌方详细信息面板紧贴在缩略图下方
	enemy_info_panel = EnemyInfoPanel.new()
	right_vbox.add_child(enemy_info_panel)
	
	# 法术选择面板（初始隐藏）
	spell_select = SpellSelectionPanel.new()
	spell_select.spell_selected.connect(func(s): spell_selected.emit(s))
	spell_select.visible = false
	right_vbox.add_child(spell_select)
	
	# ================================================================
	# 中部区域 (Middle Area) - 纯透明，用于点击地图
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
	bottom_area.add_theme_constant_override("separation", _theme.spacing_md)
	main_vbox.add_child(bottom_area)
	
	# 我方横向缩略图列表
	var ally_list_scroll = ScrollContainer.new()
	ally_list_scroll.custom_minimum_size = Vector2(0, 70)
	ally_list_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	ally_list_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	bottom_area.add_child(ally_list_scroll)
	
	var ally_list_hbox = HBoxContainer.new()
	ally_list_hbox.name = "AllyList"
	ally_list_scroll.add_child(ally_list_hbox)
	
	# 底栏外层间距 (避免贴边)
	var bottom_margin = MarginContainer.new()
	bottom_margin.add_theme_constant_override("margin_left", _theme.spacing_md)
	bottom_margin.add_theme_constant_override("margin_right", _theme.spacing_md)
	bottom_margin.add_theme_constant_override("margin_bottom", _theme.spacing_md)
	bottom_area.add_child(bottom_margin)
	
	var bottom_panel_container = PanelContainer.new()
	bottom_panel_container.add_theme_stylebox_override("panel", _theme.make_panel_style(_theme.bg_secondary, _theme.border_friendly, _theme.radius_lg))
	bottom_margin.add_child(bottom_panel_container)
	
	bottom_panel = bottom_panel_container
	
	var bottom_hbox := HBoxContainer.new()
	bottom_panel_container.add_child(bottom_hbox)
	
	# --- 4a. 头像区 ---
	var avatar_bg = _factory.create_card(Vector2(90, 90), false)
	bottom_hbox.add_child(avatar_bg)
	
	avatar_rect = TextureRect.new()
	avatar_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	avatar_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	avatar_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	avatar_bg.add_child(avatar_rect)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4b. 名称+HP/MP/士气+状态效果+六维属性 (紧凑排布) ---
	var info_col := VBoxContainer.new()
	info_col.add_theme_constant_override("separation", _theme.spacing_xs)
	info_col.custom_minimum_size = Vector2(200, 0)
	bottom_hbox.add_child(info_col)
	
	var name_row := HBoxContainer.new()
	info_col.add_child(name_row)
	
	var char_name = _factory.create_body_label("未选择", _theme.text_accent)
	char_name.add_theme_font_size_override("font_size", _theme.font_size_xl)
	char_name.set_meta("stat_key", "char_name")
	name_row.add_child(char_name)
	stat_labels["char_name"] = char_name
	
	var class_lbl = _factory.create_muted_label("")
	class_lbl.set_meta("stat_key", "class")
	name_row.add_child(class_lbl)
	stat_labels["class"] = class_lbl
	
	# HP条
	var hp_hbox := HBoxContainer.new()
	hp_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	info_col.add_child(hp_hbox)
	hp_hbox.add_child(_factory.create_body_label("HP", _theme.text_secondary))
	var hp_bar = _factory.create_hp_bar(150, _theme.bar_height_lg)
	hp_bar.set_meta("stat_key", "hp_bar")
	hp_hbox.add_child(hp_bar)
	stat_labels["hp_bar"] = hp_bar
	var hp_lbl = _factory.create_body_label("0/0")
	hp_lbl.set_meta("stat_key", "hp_text")
	hp_hbox.add_child(hp_lbl)
	stat_labels["hp_text"] = hp_lbl
	
	# MP条
	var mp_hbox := HBoxContainer.new()
	mp_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	info_col.add_child(mp_hbox)
	mp_hbox.add_child(_factory.create_body_label("MP", _theme.text_magic))
	var mp_bar = _factory.create_mana_bar(150, _theme.bar_height_md)
	mp_bar.set_meta("stat_key", "mp_bar")
	mp_hbox.add_child(mp_bar)
	stat_labels["mp_bar"] = mp_bar
	var mp_lbl = _factory.create_body_label("0/0", _theme.text_magic)
	mp_lbl.set_meta("stat_key", "mp_text")
	mp_hbox.add_child(mp_lbl)
	stat_labels["mp_text"] = mp_lbl
	
	# 六维属性(紧凑3x2)，移动到 MP 下方
	var attr_grid := GridContainer.new()
	attr_grid.columns = 3
	attr_grid.add_theme_constant_override("h_separation", _theme.spacing_lg)
	attr_grid.add_theme_constant_override("v_separation", _theme.spacing_xs)
	info_col.add_child(attr_grid)
	
	_create_attr_label(attr_grid, "str", "力")
	_create_attr_label(attr_grid, "dex", "敏")
	_create_attr_label(attr_grid, "con", "体")
	_create_attr_label(attr_grid, "intel", "智")
	_create_attr_label(attr_grid, "wis", "感")
	_create_attr_label(attr_grid, "cha", "魅")
	
	# 士气条与状态效果
	morale_bar = MoraleBar.new()
	morale_bar.custom_minimum_size = Vector2(200, 16)
	info_col.add_child(morale_bar)
	status_display = StatusEffectDisplay.new()
	info_col.add_child(status_display)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4c. 战斗属性 ---
	var combat_col := VBoxContainer.new()
	combat_col.add_theme_constant_override("separation", _theme.spacing_xs)
	bottom_hbox.add_child(combat_col)
	
	var combat_grid := GridContainer.new()
	combat_grid.columns = 2
	combat_grid.add_theme_constant_override("h_separation", _theme.spacing_lg)
	combat_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	combat_col.add_child(combat_grid)
	
	_create_stat_label(combat_grid, "ac", "护甲", _theme.text_secondary)
	_create_stat_label(combat_grid, "atk_bonus", "命中", _theme.text_friendly)
	_create_stat_label(combat_grid, "damage", "伤害", _theme.text_negative)
	_create_stat_label(combat_grid, "move", "移动", _theme.text_magic)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4d. 武器图片槽 (主副手) ---
	var weapon_hbox = HBoxContainer.new()
	weapon_hbox.add_theme_constant_override("separation", _theme.spacing_md)
	bottom_hbox.add_child(weapon_hbox)
	
	# 主手图片按钮
	var main_hand_btn = Button.new()
	main_hand_btn.custom_minimum_size = Vector2(80, 80)
	main_hand_btn.text = "主手"
	main_hand_btn.pressed.connect(func(): action_selected.emit("swap_weapon"))
	weapon_hbox.add_child(main_hand_btn)
	weapon_primary_label = Label.new() # 保留引用防止报错，实际可将名字作为tooltip或子标签
	weapon_primary_label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	weapon_primary_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	main_hand_btn.add_child(weapon_primary_label)
	
	# 副手图片按钮
	var off_hand_btn = Button.new()
	off_hand_btn.custom_minimum_size = Vector2(80, 80)
	off_hand_btn.text = "副手"
	off_hand_btn.pressed.connect(func(): action_selected.emit("swap_weapon"))
	weapon_hbox.add_child(off_hand_btn)
	weapon_secondary_label = Label.new()
	weapon_secondary_label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	weapon_secondary_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	off_hand_btn.add_child(weapon_secondary_label)
	
	var b_spacer = Control.new()
	b_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom_hbox.add_child(b_spacer)
	
	# --- 4e. 右下角固定结束回合按钮 ---
	var end_turn_btn = Button.new()
	end_turn_btn.text = "结束回合"
	end_turn_btn.custom_minimum_size = Vector2(100, 40)
	end_turn_btn.size_flags_vertical = Control.SIZE_SHRINK_END
	end_turn_btn.pressed.connect(func(): action_selected.emit("end_turn"))
	# 可以加个醒目颜色
	var btn_style = _theme.make_panel_style(_theme.bg_primary, _theme.border_muted, _theme.radius_md)
	end_turn_btn.add_theme_stylebox_override("normal", btn_style)
	bottom_hbox.add_child(end_turn_btn)
	
	# ================================================================
	# 所有绝对定位的悬浮面板/覆盖层（直接挂在 CanvasLayer 下，不受布局限制）
	# ================================================================
	
	# 6. 命中率预览浮窗
	hit_preview_tooltip = HitPreviewTooltip.new()
	hit_preview_tooltip.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	hit_preview_tooltip.visible = false
	add_child(hit_preview_tooltip)
	
	# 7. 地形信息提示
	terrain_tooltip = TerrainTooltip.new()
	terrain_tooltip.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	terrain_tooltip.visible = false
	add_child(terrain_tooltip)
	
	# 9. 角色详情面板（初始隐藏）
	character_detail = CharacterDetailPanel.new()
	character_detail.z_index = 50
	character_detail.skill_tree_requested.connect(_on_character_detail_skill_tree_requested)
	add_child(character_detail)

	# 10. 战斗结果面板（初始隐藏）
	battle_result = BattleResultPanel.new()
	battle_result.z_index = 90
	add_child(battle_result)

	# SkillTreeUI
	skill_tree_ui = SkillTreeUI.new()
	skill_tree_ui.z_index = 60
	skill_tree_ui.visible = false
	add_child(skill_tree_ui)
	
	# 10. ESC 暂停菜单
	esc_menu = PanelContainer.new()
	esc_menu.add_theme_stylebox_override("panel", _theme.make_panel_style(_theme.bg_secondary, _theme.border_friendly, _theme.radius_lg))
	esc_menu.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	esc_menu.hide()
	var esc_vbox = VBoxContainer.new()
	esc_menu.add_child(esc_vbox)
	var esc_title = _factory.create_title_label("已暂停", _theme.text_friendly)
	esc_vbox.add_child(esc_title)
	var resume_btn = _factory.create_action_button("继续", "ESC", "", _theme.text_secondary)
	resume_btn.pressed.connect(func(): _toggle_esc_menu())
	esc_vbox.add_child(resume_btn)
	var quit_btn = _factory.create_action_button("退出到主菜单", "", "", _theme.text_negative)
	quit_btn.pressed.connect(func(): get_tree().change_scene_to_file("res://src/scenes/main_menu/MainMenu.tscn"))
	esc_vbox.add_child(quit_btn)
	add_child(esc_menu)

func set_action_bar_visible(v: bool):
	pass # 遗留接口，现已不再使用
"""

pattern = re.compile(r'func _setup_ui\(\):.*?func set_action_bar_visible\(v: bool\):\s+.*?\s+action_bar\.visible = v', re.DOTALL)
new_content = pattern.sub(replacement, content)

with open('src/ui/combat/CombatUI.gd', 'w', encoding='utf-8') as f:
    f.write(new_content)
print('Replaced _setup_ui logic in CombatUI.gd')
