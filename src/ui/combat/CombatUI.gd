# CombatUI.gd
# 终极版战术战斗用户界面 - 悬浮层级天地流布局
extends CanvasLayer
class_name CombatUI

# ============================================================================
# 信号与组件
# ============================================================================
signal action_selected(action_name: String)
signal spell_selected(spell: SpellData)
signal enemy_hovered_in_panel(unit: Unit)
signal unit_selected_in_list(unit: Unit)

var turn_order_bar: TurnOrderBar
var enemy_info_panel: EnemyInfoPanel
var hit_preview_tooltip: HitPreviewTooltip
var terrain_tooltip: TerrainTooltip
var battle_log: BattleLogPanel
var character_detail: CharacterDetailPanel
var skill_tree_ui: SkillTreeUI
var spell_select: SpellSelectionPanel
var battle_result: BattleResultPanel
var radial_menu: RadialMenu

var bottom_panel: PanelContainer
var avatar_rect: TextureRect
var attr_labels: Dictionary = {}
var stat_labels: Dictionary = {}
var weapon_primary_label: Label
var weapon_secondary_label: Label
var top_info_label: Label
var phase_label: Label
var esc_menu: PanelContainer

var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

func _ready():
	_factory = UIFactory.new()
	_setup_ui()

func _setup_ui():
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
	
	# --- 1. 顶栏 (回合顺序) ---
	turn_order_bar = TurnOrderBar.new()
	turn_order_bar.custom_minimum_size = Vector2(0, 60)
	main_vbox.add_child(turn_order_bar)
	
	# --- 2. 顶部内容区 (日志与敌方对称) ---
	var top_content = HBoxContainer.new()
	top_content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	main_vbox.add_child(top_content)
	
	battle_log = BattleLogPanel.new()
	battle_log.custom_minimum_size = Vector2(300, 140)
	top_content.add_child(battle_log)
	
	var top_spacer = Control.new()
	top_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top_spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_content.add_child(top_spacer)
	
	var enemy_vbox = VBoxContainer.new()
	enemy_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	enemy_vbox.alignment = BoxContainer.ALIGNMENT_END
	top_content.add_child(enemy_vbox)
	
	var enemy_list_hbox = HBoxContainer.new()
	enemy_list_hbox.name = "EnemyList"
	enemy_list_hbox.alignment = BoxContainer.ALIGNMENT_END
	enemy_list_hbox.add_theme_constant_override("separation", 5)
	enemy_vbox.add_child(enemy_list_hbox)
	
	enemy_info_panel = EnemyInfoPanel.new()
	enemy_info_panel.custom_minimum_size = Vector2(280, 0)
	enemy_vbox.add_child(enemy_info_panel)
	
	# --- 3. 中部战术区 (弹性占位) ---
	var middle_spacer = Control.new()
	middle_spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	middle_spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	main_vbox.add_child(middle_spacer)
	
	# --- 4. 底部交互层 (我方列表 + 结束回合) ---
	# 使用 MarginContainer 让两个组件可以独立定位而不互相挤压
	var interaction_layer = MarginContainer.new()
	interaction_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	main_vbox.add_child(interaction_layer)
	
	# 4a. 结束回合 (靠右对齐，并通过底部的 Spacer 向上移动)
	var end_turn_vbox = VBoxContainer.new()
	end_turn_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	end_turn_vbox.size_flags_horizontal = Control.SIZE_SHRINK_END # 靠右
	interaction_layer.add_child(end_turn_vbox)
	
	var end_turn_btn = Button.new()
	end_turn_btn.text = "结束回合"
	end_turn_btn.custom_minimum_size = Vector2(100, 45)
	end_turn_btn.pressed.connect(func(): action_selected.emit("end_turn"))
	var btn_style = _theme.make_panel_style(_theme.bg_secondary, _theme.border_friendly, _theme.radius_md)
	end_turn_btn.add_theme_stylebox_override("normal", btn_style)
	end_turn_vbox.add_child(end_turn_btn)
	
	# 这个 Spacer 决定了按钮距离下方列表的高度
	var btn_lift_spacer = Control.new()
	btn_lift_spacer.custom_minimum_size = Vector2(0, 30)
	end_turn_vbox.add_child(btn_lift_spacer)
	
	# 4b. 我方角色列表 (靠左对齐，贴着底部面板)
	var ally_list_hbox = HBoxContainer.new()
	ally_list_hbox.name = "AllyList"
	ally_list_hbox.add_theme_constant_override("separation", 5)
	ally_list_hbox.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN # 靠左
	ally_list_hbox.size_flags_vertical = Control.SIZE_SHRINK_END # 靠下
	interaction_layer.add_child(ally_list_hbox)
	
	# --- 5. 底部信息面板 ---
	bottom_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_primary, _theme.border_default)
	main_vbox.add_child(bottom_panel)
	
	var bottom_margin = _factory.create_margin(12, 12, 10, 10)
	bottom_panel.add_child(bottom_margin)
	
	var bottom_hbox := HBoxContainer.new()
	bottom_hbox.add_theme_constant_override("separation", _theme.spacing_lg)
	bottom_margin.add_child(bottom_hbox)
	
	# 头像
	var avatar_bg = _factory.create_card(Vector2(80, 80), false)
	bottom_hbox.add_child(avatar_bg)
	avatar_rect = TextureRect.new()
	avatar_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	avatar_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	avatar_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	avatar_bg.add_child(avatar_rect)
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# 属性信息 (HP/MP + 六维)
	var info_col := VBoxContainer.new()
	info_col.add_theme_constant_override("separation", 2)
	bottom_hbox.add_child(info_col)
	
	var char_name = _factory.create_body_label("未选择", _theme.text_accent)
	char_name.set_meta("stat_key", "char_name")
	info_col.add_child(char_name)
	stat_labels["char_name"] = char_name
	
	var hp_bar = _factory.create_hp_bar(140, 8)
	hp_bar.set_meta("stat_key", "hp_bar")
	info_col.add_child(hp_bar)
	stat_labels["hp_bar"] = hp_bar
	
	var mp_bar = _factory.create_mana_bar(140, 6)
	mp_bar.set_meta("stat_key", "mp_bar")
	info_col.add_child(mp_bar)
	stat_labels["mp_bar"] = mp_bar
	
	var attr_grid := GridContainer.new()
	attr_grid.columns = 3
	attr_grid.add_theme_constant_override("h_separation", 12)
	info_col.add_child(attr_grid)
	_create_attr_label(attr_grid, "str", "力")
	_create_attr_label(attr_grid, "dex", "敏")
	_create_attr_label(attr_grid, "con", "体")
	_create_attr_label(attr_grid, "intel", "智")
	_create_attr_label(attr_grid, "wis", "感")
	_create_attr_label(attr_grid, "cha", "魅")
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# 战斗数值
	var combat_grid := GridContainer.new()
	combat_grid.columns = 2
	combat_grid.add_theme_constant_override("h_separation", 15)
	bottom_hbox.add_child(combat_grid)
	_create_stat_label(combat_grid, "ac", "防御", "10")
	_create_stat_label(combat_grid, "atk", "命中", "+0")
	_create_stat_label(combat_grid, "dmg", "伤害", "1-3")
	_create_stat_label(combat_grid, "mov", "移动", "4格")
	
	bottom_hbox.add_child(_factory.create_separator_v())
	
	# 武器图片槽
	var weapon_vbox = VBoxContainer.new()
	weapon_vbox.alignment = BoxContainer.ALIGNMENT_CENTER
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
	
	# 快捷操作栏
	var quick_actions = HBoxContainer.new()
	quick_actions.name = "QuickActions"
	quick_actions.add_theme_constant_override("separation", 6)
	bottom_hbox.add_child(quick_actions)
	for i in range(6):
		var p = _factory.create_card(Vector2(45, 45), true)
		p.modulate.a = 0.3
		quick_actions.add_child(p)

	var b_spacer = Control.new()
	b_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom_hbox.add_child(b_spacer)

	# --- 6. 悬浮/弹出组件 ---
	hit_preview_tooltip = HitPreviewTooltip.new()
	hit_preview_tooltip.visible = false
	add_child(hit_preview_tooltip)
	terrain_tooltip = TerrainTooltip.new()
	terrain_tooltip.visible = false
	add_child(terrain_tooltip)
	character_detail = CharacterDetailPanel.new()
	character_detail.visible = false
	add_child(character_detail)
	radial_menu = RadialMenu.new()
	radial_menu.visible = false
	radial_menu.action_selected.connect(func(act): action_selected.emit(act))
	add_child(radial_menu)

	_setup_esc_menu()

func _setup_esc_menu():
	esc_menu = PanelContainer.new()
	esc_menu.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	esc_menu.visible = false
	add_child(esc_menu)
	var center = CenterContainer.new()
	esc_menu.add_child(center)
	var resume = Button.new()
	resume.text = "返回战斗"
	resume.pressed.connect(func(): esc_menu.visible = false)
	center.add_child(resume)

# --- 辅助方法 ---
func _create_attr_label(parent, id, text):
	var l_name = _factory.create_muted_label(text)
	l_name.custom_minimum_size = Vector2(25, 0)
	parent.add_child(l_name)
	var l_val = _factory.create_body_label("10")
	parent.add_child(l_val)
	attr_labels[id] = l_val

func _create_stat_label(parent, id, text, val):
	var l_name = _factory.create_muted_label(text)
	parent.add_child(l_name)
	var l_val = _factory.create_body_label(val)
	parent.add_child(l_val)
	stat_labels[id] = l_val

# --- 逻辑接口 ---
func update_unit_info(unit: Unit):
	if not unit or not unit.data: return
	stat_labels["char_name"].text = unit.data.unit_name
	attr_labels["str"].text = str(unit.data.str)
	attr_labels["dex"].text = str(unit.data.dex)
	attr_labels["con"].text = str(unit.data.con)
	attr_labels["intel"].text = str(unit.data.intel)
	attr_labels["wis"].text = str(unit.data.wis)
	attr_labels["cha"].text = str(unit.data.cha)
	stat_labels["ac"].text = str(unit.get_ac())
	stat_labels["atk"].text = str(unit.get_attack_bonus())
	stat_labels["hp_bar"].max_value = unit.get_max_hp()
	stat_labels["hp_bar"].value = unit.current_hp
	stat_labels["mp_bar"].max_value = max(unit.data.current_mana, 1)
	stat_labels["mp_bar"].value = unit.data.current_mana
	weapon_primary_label.text = unit.data.primary_main_hand.item_name if unit.data.primary_main_hand else "徒手"
	weapon_secondary_label.text = unit.data.secondary_main_hand.item_name if unit.data.secondary_main_hand else "无"

func log_message(msg):
	if battle_log: battle_log.add_entry(msg)

func set_action_bar_visible(v): pass

func _find_ally_list() -> HBoxContainer:
	return find_child("AllyList", true, false) as HBoxContainer

func _find_enemy_list() -> HBoxContainer:
	return find_child("EnemyList", true, false) as HBoxContainer

func _create_thumbnail_entry(unit: Unit, is_enemy: bool) -> PanelContainer:
	var entry := PanelContainer.new()
	entry.set_meta("unit_ref", unit)
	entry.custom_minimum_size = Vector2(60, 70)
	var style = StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.1, 0.1, 0.8)
	style.set_border_width_all(2)
	style.border_color = _theme.border_negative if is_enemy else _theme.border_friendly
	entry.add_theme_stylebox_override("panel", style)
	var v = VBoxContainer.new()
	entry.add_child(v)
	var name = Label.new()
	name.text = unit.data.unit_name.substr(0, 3)
	name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(name)
	var hp = ProgressBar.new()
	hp.custom_minimum_size = Vector2(50, 5)
	hp.max_value = unit.get_max_hp()
	hp.value = unit.current_hp
	v.add_child(hp)
	return entry

func register_ally(unit):
	var l = _find_ally_list()
	if l: l.add_child(_create_thumbnail_entry(unit, false))

func register_enemy(unit):
	var l = _find_enemy_list()
	if l: l.add_child(_create_thumbnail_entry(unit, true))
	enemy_info_panel.add_enemy(unit)

func update_enemy_info(unit):
	enemy_info_panel.update_enemy(unit)

func remove_enemy(unit):
	enemy_info_panel.remove_enemy(unit)

func open_radial_menu(pos, unit, _sm, _tu = null):
	var opts = {"防御": "defend", "等待": "wait", "取消": "none"}
	radial_menu.setup(opts)
	radial_menu.show_menu(pos)

func update_turn_order(units, active, turn):
	if turn_order_bar:
		turn_order_bar.set_turn_number(turn)
		turn_order_bar.set_unit_order(units, active)
