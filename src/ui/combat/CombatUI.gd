# CombatUI.gd
# 深度优化版战术战斗用户界面
# 对应策划案 09-UI设计.md 的完整布局 + 所有策划功能
# 布局：顶部回合顺序栏 | 左侧角色列表 | 中央地图 | 右侧敌方面板 | 底部操作面板+日志
# 预留图像UI：所有视觉通过 UITheme/UIFactory 访问，替换时只需修改主题
extends CanvasLayer
class_name CombatUI

# ============================================================================
# 信号
# ============================================================================
signal action_selected(action_name: String)
signal spell_selected(spell: SpellData)
signal enemy_hovered_in_panel(unit: Unit)
signal unit_selected_in_list(unit: Unit)

# ============================================================================
# 子面板引用
# ============================================================================
var turn_order_bar: TurnOrderBar
var enemy_info_panel: EnemyInfoPanel
var hit_preview_tooltip: HitPreviewTooltip
var terrain_tooltip: TerrainTooltip
var battle_log: BattleLogPanel
var character_detail: CharacterDetailPanel
var skill_tree_ui: SkillTreeUI
var spell_select: SpellSelectionPanel

# ============================================================================
# 底部面板组件
# ============================================================================
var bottom_panel: PanelContainer
var avatar_rect: TextureRect
var attr_labels: Dictionary = {}
var stat_labels: Dictionary = {}
var weapon_primary_label: Label
var weapon_secondary_label: Label
var action_bar: HBoxContainer
var status_display: StatusEffectDisplay
var morale_bar: MoraleBar

# ============================================================================
# 顶部信息
# ============================================================================
var top_info_label: Label
var phase_label: Label

# ============================================================================
# ESC菜单
# ============================================================================
var esc_menu: PanelContainer

# ============================================================================
# 辅助
# ============================================================================
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

func _ready():
	_factory = UIFactory.new()
	_setup_ui()

func _setup_ui():
	var root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	
	# ================================================================
	# 1. 顶部回合顺序栏
	# ================================================================
	var top_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_overlay, _theme.border_default)
	top_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	top_panel.grow_vertical = Control.GROW_DIRECTION_BEGIN
	root.add_child(top_panel)
	
	var top_hbox := HBoxContainer.new()
	top_hbox.add_theme_constant_override("separation", _theme.spacing_md)
	top_panel.add_child(top_hbox)
	
	# 回合/阶段信息
	var info_vbox := VBoxContainer.new()
	info_vbox.add_theme_constant_override("separation", 0)
	top_hbox.add_child(info_vbox)
	
	top_info_label = _factory.create_body_label("等待战斗开始...", _theme.text_accent)
	top_info_label.add_theme_font_size_override("font_size", _theme.font_size_lg)
	top_info_label.custom_minimum_size = Vector2(200, 0)
	info_vbox.add_child(top_info_label)
	
	phase_label = _factory.create_muted_label("我方回合")
	info_vbox.add_child(phase_label)
	
	top_hbox.add_child(_factory.create_separator_v())
	
	# 回合顺序条
	turn_order_bar = TurnOrderBar.new()
	turn_order_bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	turn_order_bar.unit_clicked.connect(func(unit): unit_selected_in_list.emit(unit))
	top_hbox.add_child(turn_order_bar)
	
	# ================================================================
	# 2. 右侧敌方信息面板
	# ================================================================
	enemy_info_panel = EnemyInfoPanel.new()
	enemy_info_panel.set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
	enemy_info_panel.offset_left = -240
	enemy_info_panel.offset_top = 50
	enemy_info_panel.offset_bottom = -180
	enemy_info_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(enemy_info_panel)
	enemy_info_panel.enemy_hovered.connect(func(unit): enemy_hovered_in_panel.emit(unit))
	
	# ================================================================
	# 3. 左侧我方角色列表
	# ================================================================
	var left_panel = _factory.create_panel(Vector2(180, 0), _theme.bg_secondary, _theme.border_friendly)
	left_panel.set_anchors_and_offsets_preset(Control.PRESET_LEFT_WIDE)
	left_panel.offset_right = 190
	left_panel.offset_top = 50
	left_panel.offset_bottom = -180
	root.add_child(left_panel)
	
	var left_vbox := VBoxContainer.new()
	left_vbox.add_theme_constant_override("separation", _theme.spacing_xs)
	left_panel.add_child(left_vbox)
	
	var ally_title = _factory.create_title_label("— 我 方 —", _theme.font_size_lg)
	ally_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ally_title.add_theme_color_override("font_color", _theme.border_friendly)
	left_vbox.add_child(ally_title)
	
	left_vbox.add_child(_factory.create_separator_h(_theme.border_friendly))
	
	# 角色列表容器（运行时动态添加）
	var ally_scroll = _factory.create_scroll_container()
	ally_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left_vbox.add_child(ally_scroll)
	
	var ally_list = VBoxContainer.new()
	ally_list.name = "AllyList"
	ally_list.add_theme_constant_override("separation", _theme.spacing_xs)
	ally_scroll.add_child(ally_list)
	
	# ================================================================
	# 4. 底部控制面板（角色信息+操作栏）
	# ================================================================
	bottom_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_primary, _theme.border_default)
	bottom_panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	bottom_panel.grow_vertical = Control.GROW_DIRECTION_BEGIN
	root.add_child(bottom_panel)
	
	var bottom_margin = _factory.create_margin(10, 10, 8, 8)
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
	
	# --- 4b. 名称+HP/MP/士气+状态效果 ---
	var info_col := VBoxContainer.new()
	info_col.add_theme_constant_override("separation", _theme.spacing_xs)
	info_col.custom_minimum_size = Vector2(200, 0)
	bottom_hbox.add_child(info_col)
	
	var name_row := HBoxContainer.new()
	info_col.add_child(name_row)
	
	var char_name = _factory.create_body_label("未选择", _theme.text_accent)
	char_name.add_theme_font_size_override("font_size", _theme.font_size_lg)
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
	var hp_bar = _factory.create_hp_bar(130, _theme.bar_height_lg)
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
	var mp_bar = _factory.create_mana_bar(130, _theme.bar_height_md)
	mp_bar.set_meta("stat_key", "mp_bar")
	mp_hbox.add_child(mp_bar)
	stat_labels["mp_bar"] = mp_bar
	var mp_lbl = _factory.create_body_label("0/0", _theme.text_magic)
	mp_lbl.set_meta("stat_key", "mp_text")
	mp_hbox.add_child(mp_lbl)
	stat_labels["mp_text"] = mp_lbl
	
	# 士气条
	morale_bar = MoraleBar.new()
	morale_bar.custom_minimum_size = Vector2(180, 16)
	info_col.add_child(morale_bar)
	
	# 状态效果
	status_display = StatusEffectDisplay.new()
	info_col.add_child(status_display)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4c. 六维属性(紧凑3x2) ---
	var attr_grid := GridContainer.new()
	attr_grid.columns = 3
	attr_grid.add_theme_constant_override("h_separation", _theme.spacing_md)
	attr_grid.add_theme_constant_override("v_separation", _theme.spacing_xs)
	bottom_hbox.add_child(attr_grid)
	
	_create_attr_label(attr_grid, "str", "力量")
	_create_attr_label(attr_grid, "dex", "敏捷")
	_create_attr_label(attr_grid, "con", "体质")
	_create_attr_label(attr_grid, "intel", "智力")
	_create_attr_label(attr_grid, "wis", "感知")
	_create_attr_label(attr_grid, "cha", "魅力")
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4d. 战斗属性+武器 ---
	var combat_col := VBoxContainer.new()
	combat_col.add_theme_constant_override("separation", _theme.spacing_xs)
	bottom_hbox.add_child(combat_col)
	
	var combat_grid := GridContainer.new()
	combat_grid.columns = 2
	combat_grid.add_theme_constant_override("h_separation", _theme.spacing_lg)
	combat_grid.add_theme_constant_override("v_separation", _theme.spacing_xs)
	combat_col.add_child(combat_grid)
	
	_create_stat_label(combat_grid, "ac", "护甲(AC)", "10")
	_create_stat_label(combat_grid, "atk", "攻击加值", "+0")
	_create_stat_label(combat_grid, "dmg", "伤害范围", "1-3")
	_create_stat_label(combat_grid, "mov", "移动力", "4格")
	_create_stat_label(combat_grid, "init", "先攻", "+0")
	_create_stat_label(combat_grid, "crit", "暴击率", "5%")
	
	# 武器
	var weapon_vbox := HBoxContainer.new()
	weapon_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	combat_col.add_child(weapon_vbox)
	
	weapon_primary_label = _factory.create_body_label("[主] 徒手")
	weapon_vbox.add_child(weapon_primary_label)
	
	weapon_secondary_label = _factory.create_body_label("[副] 无", _theme.text_muted)
	weapon_vbox.add_child(weapon_secondary_label)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# --- 4e. 操作按钮栏 ---
	action_bar = HBoxContainer.new()
	action_bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	action_bar.alignment = BoxContainer.ALIGNMENT_END
	action_bar.add_theme_constant_override("separation", _theme.spacing_sm)
	bottom_hbox.add_child(action_bar)
	
	_create_action_button("移动", "M", "move", _theme.border_friendly)
	_create_action_button("攻击", "A", "attack", _theme.text_negative)
	_create_action_button("法术", "S", "spell", _theme.text_magic)
	_create_action_button("物品", "I", "item", _theme.text_accent)
	_create_action_button("换武器", "X", "swap_weapon", _theme.text_secondary)
	_create_action_button("防御", "D", "defend", _theme.text_positive)
	_create_action_button("结束回合", "Space", "end_turn", _theme.text_muted)
	
	set_action_bar_visible(false)
	
	# ================================================================
	# 5. 战斗日志（底部左侧浮动）
	# ================================================================
	battle_log = BattleLogPanel.new()
	battle_log.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	battle_log.offset_left = 10
	battle_log.offset_top = -200
	battle_log.offset_right = 320
	battle_log.offset_bottom = -140
	battle_log.z_index = 5
	root.add_child(battle_log)
	
	# ================================================================
	# 6. 命中率预览浮窗
	# ================================================================
	hit_preview_tooltip = HitPreviewTooltip.new()
	hit_preview_tooltip.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	hit_preview_tooltip.visible = false
	root.add_child(hit_preview_tooltip)
	
	# ================================================================
	# 7. 地形信息提示
	# ================================================================
	terrain_tooltip = TerrainTooltip.new()
	terrain_tooltip.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	terrain_tooltip.visible = false
	root.add_child(terrain_tooltip)
	
	# ================================================================
	# 8. 法术选择面板（初始隐藏）
	# ================================================================
	spell_select = SpellSelectionPanel.new()
	spell_select.set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
	spell_select.offset_left = -280
	spell_select.offset_top = 60
	spell_select.offset_bottom = -140
	spell_select.visible = false
	spell_select.spell_selected.connect(func(spell): spell_selected.emit(spell))
	root.add_child(spell_select)
	
	# ================================================================
	# 9. 角色详情面板（初始隐藏，点击角色列表或快捷键打开）
	# ================================================================
	character_detail = CharacterDetailPanel.new()
	character_detail.z_index = 50
	character_detail.skill_tree_requested.connect(_on_character_detail_skill_tree_requested)
	root.add_child(character_detail)

	# SkillTreeUI
	skill_tree_ui = SkillTreeUI.new()
	skill_tree_ui.z_index = 60
	skill_tree_ui.visible = false
	root.add_child(skill_tree_ui)
	
	# ================================================================
	# 10. ESC 暂停菜单
	# ================================================================
	esc_menu = PanelContainer.new()
	var esc_bg := StyleBoxFlat.new()
	esc_bg.bg_color = Color(0.0, 0.0, 0.0, 0.6)
	esc_bg.set_border_width_all(2)
	esc_bg.border_color = _theme.border_highlight
	esc_bg.set_corner_radius_all(_theme.radius_md)
	esc_menu.add_theme_stylebox_override("panel", esc_bg)
	esc_menu.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	esc_menu.visible = false
	root.add_child(esc_menu)
	
	var esc_center := CenterContainer.new()
	esc_center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	esc_menu.add_child(esc_center)
	
	var esc_inner := PanelContainer.new()
	var esc_inner_bg := StyleBoxFlat.new()
	esc_inner_bg.bg_color = _theme.bg_primary
	esc_inner_bg.set_border_width_all(2)
	esc_inner_bg.border_color = _theme.border_highlight
	esc_inner_bg.set_corner_radius_all(_theme.radius_md)
	esc_inner_bg.set_content_margin_all(30)
	esc_inner.add_theme_stylebox_override("panel", esc_inner_bg)
	esc_center.add_child(esc_inner)
	
	var esc_vbox := VBoxContainer.new()
	esc_vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	esc_vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	esc_vbox.custom_minimum_size = Vector2(220, 0)
	esc_inner.add_child(esc_vbox)
	
	var esc_title = _factory.create_title_label("- 战斗暂停 -", _theme.font_size_xl)
	esc_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	esc_vbox.add_child(esc_title)
	
	var resume_btn = _factory.create_button("返回战斗", Vector2(200, _theme.button_height_lg))
	resume_btn.pressed.connect(func(): esc_menu.visible = false)
	esc_vbox.add_child(resume_btn)
	
	var char_btn = _factory.create_button("角色详情 (C)", Vector2(200, _theme.button_height_lg))
	char_btn.pressed.connect(func(): 
		esc_menu.visible = false
		if character_detail and not character_detail.visible:
			character_detail.visible = true
	)
	esc_vbox.add_child(char_btn)
	
	var settings_btn = _factory.create_button("设置", Vector2(200, _theme.button_height_lg))
	esc_vbox.add_child(settings_btn)
	
	var retreat_btn = _factory.create_button("撤退 (认输)", Vector2(200, _theme.button_height_lg))
	retreat_btn.add_theme_color_override("font_color", _theme.text_negative)
	retreat_btn.pressed.connect(func():
		esc_menu.visible = false
		action_selected.emit("retreat")
	)
	esc_vbox.add_child(retreat_btn)
	
	var main_menu_btn = _factory.create_button("回到主菜单", Vector2(200, _theme.button_height_lg))
	main_menu_btn.pressed.connect(func():
		esc_menu.visible = false
		get_tree().change_scene_to_file("res://src/ui/main_menu/main_menu.tscn")
	)
	esc_vbox.add_child(main_menu_btn)
	
	var exit_btn = _factory.create_button("退出游戏", Vector2(200, _theme.button_height_lg))
	exit_btn.add_theme_color_override("font_color", _theme.text_negative)
	exit_btn.pressed.connect(func():
		get_tree().quit()
	)
	esc_vbox.add_child(exit_btn)

# ============================================================================
# 辅助创建
# ============================================================================

func _create_attr_label(parent: Control, id: String, text: String):
	var name_l = _factory.create_muted_label(text)
	name_l.custom_minimum_size = Vector2(35, 0)
	parent.add_child(name_l)
	
	var val_l = _factory.create_body_label("10")
	val_l.custom_minimum_size = Vector2(22, 0)
	parent.add_child(val_l)
	attr_labels[id] = val_l

func _create_stat_label(parent: Control, id: String, text: String, default_val: String):
	var name_l = _factory.create_muted_label(text)
	parent.add_child(name_l)
	
	var val_l = _factory.create_body_label(default_val)
	parent.add_child(val_l)
	stat_labels[id] = val_l

func _create_action_button(label: String, shortcut: String, action_name: String, color: Color):
	var btn = _factory.create_action_button(label, shortcut, "", color)
	btn.pressed.connect(func(): action_selected.emit(action_name))
	action_bar.add_child(btn)

# ============================================================================
# 输入
# ============================================================================

func _unhandled_input(event):
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_ESCAPE:
			if skill_tree_ui and skill_tree_ui.visible:
				skill_tree_ui.visible = false
			elif character_detail and character_detail.visible:
				character_detail.visible = false
			elif spell_select and spell_select.visible:
				spell_select.visible = false
			else:
				esc_menu.visible = !esc_menu.visible
			get_viewport().set_input_as_handled()
		elif event.keycode == KEY_C:
			if character_detail:
				character_detail.visible = !character_detail.visible
			get_viewport().set_input_as_handled()

# ============================================================================
# 公开接口 — 角色信息更新
# ============================================================================

func update_unit_info(unit: Unit):
	if not unit or not unit.data: return
	
	# 名称
	if stat_labels.has("char_name"):
		stat_labels["char_name"].text = unit.data.unit_name
	
	# 六维
	attr_labels["str"].text = str(unit.data.str)
	attr_labels["dex"].text = str(unit.data.dex)
	attr_labels["con"].text = str(unit.data.con)
	attr_labels["intel"].text = str(unit.data.intel)
	attr_labels["wis"].text = str(unit.data.wis)
	attr_labels["cha"].text = str(unit.data.cha)
	
	# 战斗属性
	var bonus = unit.get_attack_bonus()
	stat_labels["atk"].text = ("+" if bonus >= 0 else "") + str(bonus)
	stat_labels["ac"].text = str(unit.get_ac())
	stat_labels["mov"].text = str(unit.data.base_move_range) + "格"
	
	var dmg_info = unit.roll_damage()
	stat_labels["dmg"].text = dmg_info.text + ("+" if dmg_info.mod >= 0 else "") + str(dmg_info.mod)
	
	# HP
	var max_hp = unit.get_max_hp()
	if stat_labels.has("hp_bar"):
		var hp_bar: ProgressBar = stat_labels["hp_bar"]
		hp_bar.max_value = max_hp
		hp_bar.value = unit.current_hp
		_theme.apply_bar_theme(hp_bar, _theme.get_hp_color(float(unit.current_hp) / float(max(max_hp, 1))))
	if stat_labels.has("hp_text"):
		stat_labels["hp_text"].text = "%d/%d" % [unit.current_hp, max_hp]
	
	# MP
	var max_mp = max(unit.data.current_mana, 1)
	if stat_labels.has("mp_bar") and unit.data:
		var mp_bar: ProgressBar = stat_labels["mp_bar"]
		mp_bar.max_value = max_mp
		mp_bar.value = unit.data.current_mana
	if stat_labels.has("mp_text") and unit.data:
		stat_labels["mp_text"].text = "%d/%d" % [unit.data.current_mana, max_mp]
	
	# 士气
	morale_bar.update_morale(unit.data.morale)
	
	# 武器
	var main_w = unit.data.primary_main_hand
	var sec_w = unit.data.secondary_main_hand
	var is_p = unit.using_primary_weapon
	
	var m_text = main_w.item_name if main_w else "徒手"
	var s_text = sec_w.item_name if sec_w else "无"
	
	weapon_primary_label.text = ("[▶] " if is_p else "[ ] ") + m_text
	weapon_primary_label.add_theme_color_override("font_color", Color.WHITE if is_p else _theme.text_muted)
	
	weapon_secondary_label.text = ("[▶] " if not is_p else "[ ] ") + s_text
	weapon_secondary_label.add_theme_color_override("font_color", Color.WHITE if not is_p else _theme.text_muted)
	
	# 同步到角色详情面板
	if character_detail:
		character_detail._current_unit = unit
		character_detail.update_display()

# ============================================================================
# 公开接口 — 控制
# ============================================================================

func set_turn_text(text: String, color: Color = Color.WHITE):
	top_info_label.text = text
	top_info_label.add_theme_color_override("font_color", color)

func set_phase_text(text: String, color: Color = Color()):
	phase_label.text = text
	if color != Color():
		phase_label.add_theme_color_override("font_color", color)

func set_action_bar_visible(isvisible: bool):
	action_bar.visible = isvisible

func log_message(msg: String):
	if battle_log:
		battle_log.add_entry(msg)

# ============================================================================
# 公开接口 — 敌方
# ============================================================================

func register_enemy(unit: Unit):
	enemy_info_panel.add_enemy(unit)

func remove_enemy(unit: Unit):
	enemy_info_panel.remove_enemy(unit)

func update_enemy_info(unit: Unit):
	enemy_info_panel.update_enemy(unit)

func highlight_enemy(unit: Unit, highlighted: bool):
	enemy_info_panel.highlight_enemy(unit, highlighted)

func show_hit_preview(global_mouse_pos: Vector2, attacker: Unit, target: Unit, 
		cover_type: int = 0, elevation_diff: int = 0, 
		has_flanking: bool = false, has_sneak: bool = false):
	hit_preview_tooltip.show_preview(attacker, target, cover_type, elevation_diff, has_flanking, has_sneak)
	hit_preview_tooltip.position = global_mouse_pos + Vector2(15, 15)

func hide_hit_preview():
	hit_preview_tooltip.hide_preview()

# ============================================================================
# 公开接口 — 地形
# ============================================================================

func show_terrain_info(global_pos: Vector2, terrain_type: String, 
		coord: Vector2i = Vector2i(-1, -1), occupant: String = ""):
	terrain_tooltip.show_terrain_info(global_pos, terrain_type, coord, occupant)

func hide_terrain_info():
	terrain_tooltip.hide_tooltip()

# ============================================================================
# 技能盘
# ============================================================================

## 从角色详情面板请求打开技能盘
func _on_character_detail_skill_tree_requested():
	if not character_detail or not character_detail.visible:
		return
	character_detail.visible = false
	_open_skill_tree_for_current_unit()

## 打开当前选中角色的技能盘
func _open_skill_tree_for_current_unit():
	var mgr = SkillTreeManager.get_instance()
	if not mgr:
		return
	var unit = character_detail._current_unit if character_detail else null
	if not unit or not is_instance_valid(unit) or not unit.data:
		return
	var char_id = unit.data.get_instance_id()
	var char_tree = mgr.get_skill_tree(char_id)
	if not char_tree:
		char_tree = mgr.create_skill_tree(char_id, unit.data.level)
		mgr.init_character_level(char_id, unit.data.level)
	skill_tree_ui.open_skill_tree(char_tree, mgr.tree_data)

# ============================================================================
# 法术面板
# ============================================================================

func open_spell_panel(caster: Unit, spell_manager: SpellManager):
	if spell_select:
		spell_select.open(caster, spell_manager)
		spell_select.visible = true

func close_spell_panel():
	if spell_select:
		spell_select.close()
		spell_select.visible = false
