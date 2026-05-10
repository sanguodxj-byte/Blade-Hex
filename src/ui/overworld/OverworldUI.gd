# OverworldUI.gd
# 深度优化版大地图UI — 包含所有策划功能面板
# 对应策划案 09-UI设计.md → 战略层UI
# 对应策划案 04-战略层系统.md → 大地图/城镇/遭遇
# 底部功能栏(骑砍式) + 全屏子面板(队伍/角色/物品/技能盘/城镇/军队/领地)
extends CanvasLayer
class_name OverworldUI

# ============================================================================
# 信号
# ============================================================================
signal menu_opened(menu_name: String)
signal party_clicked()
signal character_clicked()
signal inventory_clicked()

# ============================================================================
# 子面板
# ============================================================================
var party_panel: PartyPanel
var character_detail: CharacterDetailPanel
var skill_tree_ui: SkillTreeUI
var town_ui: TownUI
var army_ui: ArmyManagementUI
var territory_ui: TerritoryUI
var quest_log: QuestLog
var settings_panel: SettingsPanel

# ============================================================================
# 属性标签
# ============================================================================
var info_label: Label
var day_label: Label
var gold_label: Label
var food_label: Label
var speed_label: Label
var morale_label: Label
var bottom_bar: HBoxContainer
var esc_menu: PanelContainer
var minimap_container: PanelContainer

# ============================================================================
# 外部系统引用 (由 OverworldScene 或 _ready 初始化)
# ============================================================================
var economy_manager: Node = null
var save_manager: Node = null
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

func _ready():
	_init_save_manager()
	_factory = UIFactory.new()
	_setup_ui()

func _init_save_manager():
	var save_script = load("res://src/core/data/SaveManager.gd")
	if save_script:
		save_manager = save_script.new()
		add_child(save_manager)

	# 设置面板（独立 CanvasLayer，ESC菜单中打开）
	settings_panel = SettingsPanel.new()
	add_child(settings_panel)

func _setup_ui():
	var root = Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)
	
	# 1. 顶部信息栏
	var top_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_overlay, Color())
	top_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	top_panel.mouse_filter = Control.MOUSE_FILTER_PASS
	root.add_child(top_panel)
	
	var top_hbox := HBoxContainer.new()
	top_hbox.add_theme_constant_override("separation", _theme.spacing_lg)
	top_panel.add_child(top_hbox)
	
	day_label = _factory.create_body_label("1250年 1月 1日", _theme.text_accent)
	top_hbox.add_child(day_label)
	top_hbox.add_child(_factory.create_separator_v())
	
	gold_label = _factory.create_body_label("金币: 1000", _theme.text_accent)
	top_hbox.add_child(gold_label)
	
	food_label = _factory.create_body_label("食物: 20/40", _theme.text_secondary)
	top_hbox.add_child(food_label)
	
	speed_label = _factory.create_body_label("季节: 春季", _theme.text_secondary)
	top_hbox.add_child(speed_label)
	
	morale_label = _factory.create_body_label("时间: 08:00", _theme.text_secondary)
	top_hbox.add_child(morale_label)
	
	# 2. 底部功能栏
	var bottom_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_primary, _theme.border_default)
	bottom_panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	bottom_panel.grow_vertical = Control.GROW_DIRECTION_BEGIN
	bottom_panel.mouse_filter = Control.MOUSE_FILTER_PASS
	root.add_child(bottom_panel)
	
	var bottom_margin = _factory.create_margin(10, 10, 15, 15)
	bottom_panel.add_child(bottom_margin)
	
	bottom_bar = HBoxContainer.new()
	bottom_bar.alignment = BoxContainer.ALIGNMENT_CENTER
	bottom_bar.add_theme_constant_override("separation", _theme.spacing_sm)
	bottom_margin.add_child(bottom_bar)
	
	_create_bar_button("队 伍", "party", _theme.border_friendly)
	_create_bar_button("角 色", "character", _theme.text_primary)
	_create_bar_button("物 品", "inventory", _theme.text_accent)
	_create_bar_button("技能盘", "skill_tree", _theme.text_magic)
	_create_bar_button("任 务", "quests", _theme.text_warning)
	_create_bar_button("营 地", "camp", _theme.text_positive)
	
	# 3. 子面板初始化
	party_panel = PartyPanel.new()
	party_panel.visible = false
	root.add_child(party_panel)
	
	character_detail = CharacterDetailPanel.new()
	character_detail.visible = false
	character_detail.skill_tree_requested.connect(_on_character_detail_skill_tree_requested)
	root.add_child(character_detail)
	
	skill_tree_ui = SkillTreeUI.new()
	skill_tree_ui.visible = false
	root.add_child(skill_tree_ui)
	
	town_ui = TownUI.new()
	town_ui.visible = false
	root.add_child(town_ui)
	
	# 任务日志界面 (从 TSCCN 加载)
	var log_scene = load("res://src/ui/quest/QuestLog.tscn")
	if log_scene:
		quest_log = log_scene.instantiate()
		quest_log.visible = false
		root.add_child(quest_log)
		# 自动绑定 QuestManager
		var parent = get_parent()
		if parent and parent.has_variable("quest_manager"):
			quest_log.set_quest_manager(parent.quest_manager)
	
	# 4. ESC 系统菜单
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
	
	var esc_title = _factory.create_title_label("- 系统菜单 -", _theme.font_size_xl)
	esc_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	esc_vbox.add_child(esc_title)
	
	_create_esc_button("保存游戏", "save", esc_vbox)
	_create_esc_button("加载游戏", "load", esc_vbox)
	_create_esc_button("设置", "settings", esc_vbox)
	_create_esc_button("返回游戏", "resume", esc_vbox)
	
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

func _create_bar_button(text: String, action_name: String, color: Color):
	var btn = _factory.create_button(text, Vector2(100, _theme.button_height_lg))
	btn.add_theme_color_override("font_color", color)
	btn.pressed.connect(_on_button_pressed.bind(action_name))
	bottom_bar.add_child(btn)

func _create_esc_button(text: String, action_name: String, parent: Control):
	var btn = _factory.create_button(text, Vector2(200, _theme.button_height_lg))
	btn.pressed.connect(_on_button_pressed.bind(action_name))
	parent.add_child(btn)

func _on_button_pressed(action_name: String):
	esc_menu.visible = false
	match action_name:
		"resume": pass
		"save":
			var parent = get_parent()
			var context = {
				"economy": economy_manager if economy_manager else parent.economy_manager,
				"player_party": parent.player_party,
				"player_unit": parent.player_unit_data,
				"fog_of_war": parent.fog_of_war if parent.has_variable("fog_of_war") else null,
				"player_race_id": parent.player_race_id if parent.has_variable("player_race_id") else 0,
			}
			save_manager.save_game(context)
		"load": menu_opened.emit("load_game")
		"party":
			_close_all_panels()
			party_panel.open_tab("party", get_parent().player_unit_data)
		"character":
			_close_all_panels()
			party_panel.open_tab("character", get_parent().player_unit_data)
		"inventory":
			_close_all_panels()
			party_panel.open_tab("inventory", get_parent().player_unit_data)
		"quests":
			_close_all_panels()
			if quest_log:
				quest_log.show_log()
		"camp":
			# 切换骑砍式等待模式
			var parent = get_parent()
			if parent.has_variable("is_waiting"):
				parent.is_waiting = !parent.is_waiting
				if parent.is_waiting:
					# 停止移动，原地等待
					parent.player_party.is_moving = false
					update_top_info_status("正在扎营等待...")
				else:
					update_top_info_status("")
		"skill_tree":
			_close_all_panels()
			_open_skill_tree_for_player()
		"settings":
			esc_menu.visible = false
			settings_panel.show_settings()
		_: menu_opened.emit(action_name)

## 辅助：更新顶部状态文字
func update_top_info_status(status: String):
	# 这里可以动态修改 UI 上的某个标签，比如 speed_label
	if status != "":
		speed_label.text = status
		speed_label.add_theme_color_override("font_color", Color.YELLOW)
	else:
		# 恢复季节显示
		if economy_manager:
			speed_label.text = "季节: " + economy_manager.get_season_name()
			speed_label.remove_theme_color_override("font_color")

func _close_all_panels():
	party_panel.visible = false
	character_detail.visible = false
	skill_tree_ui.visible = false
	town_ui.visible = false

func _unhandled_input(event):
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		# 优先处理设置面板的 ESC
		if settings_panel and settings_panel.is_panel_visible():
			settings_panel.hide_settings()
			get_viewport().set_input_as_handled()
			return
		if party_panel.visible or character_detail.visible or skill_tree_ui.visible or town_ui.visible:
			_close_all_panels()
		else:
			esc_menu.visible = !esc_menu.visible
		get_viewport().set_input_as_handled()

func update_top_info(year: int, month: int, day: int, season: String, clock: String, gold: int, food: int, food_max: int):
	day_label.text = "%d年 %d月 %d日" % [year, month, day]
	gold_label.text = "金币: %d" % gold
	food_label.text = "食物: %d/%d" % [food, food_max]
	speed_label.text = "季节: %s" % season
	morale_label.text = "时间: %s" % clock

func open_town(town_name: String, town_data: Dictionary = {}):
	_close_all_panels()
	town_ui.open_town(town_name, town_data)

## 打开玩家角色的技能盘
func _open_skill_tree_for_player():
	var mgr = SkillTreeManager.get_instance()
	if not mgr:
		return
	
	var player_data = get_parent().player_unit_data
	if not player_data:
		return
	
	# 获取或创建角色技能盘
	var char_id = player_data.get_instance_id()
	var char_tree = mgr.get_skill_tree(char_id)
	if not char_tree:
		char_tree = mgr.create_skill_tree(char_id, player_data.level)
		mgr.init_character_level(char_id, player_data.level)
	
	skill_tree_ui.open_skill_tree(char_tree, mgr.tree_data)

## 从角色详情面板请求打开技能盘
func _on_character_detail_skill_tree_requested():
	if not character_detail.visible:
		return
	_close_all_panels()
	_open_skill_tree_for_player()
