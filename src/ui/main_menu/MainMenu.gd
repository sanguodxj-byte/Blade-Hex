# MainMenu.gd
# 游戏主入口界面 (正式版本：优化适配与居中布局)
extends CanvasLayer
class_name MainMenu

const VERSION_STRING = "v0.2.1-Alpha"

## 预加载，避免 class_name 解析顺序问题
const LoadingScreenClass = preload("res://src/ui/loading/LoadingScreen.gd")
const SettingsPanelClass = preload("res://src/ui/main_menu/SettingsPanel.gd")
const CharacterGeneratorClass = preload("res://src/core/character/CharacterGenerator.gd")

var save_manager: Node
var settings_panel

func _ready():
	# 播放主菜单背景音乐
	AudioManager.play_scenario_bgm(AudioManager.Scenario.MAIN_MENU, "default", 2.0)
	
	_init_save_manager()
	_init_settings_panel()
	_setup_ui()

func _init_settings_panel():
	settings_panel = SettingsPanelClass.new()
	add_child(settings_panel)
	settings_panel.settings_closed.connect(_on_settings_closed)

func _init_save_manager():
	var save_script = load("res://src/core/data/SaveManager.gd")
	if save_script:
		save_manager = save_script.new()
		add_child(save_manager)

func _setup_ui():
	# 1. 基础背景
	var bg = ColorRect.new()
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.color = Color(0.04, 0.04, 0.06) 
	add_child(bg)
	
	# 2. 全屏边距容器 (确保内容不贴边，且自适应分辨率)
	var main_margin = MarginContainer.new()
	main_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_margin.add_theme_constant_override("margin_left", 80)
	main_margin.add_theme_constant_override("margin_right", 80)
	main_margin.add_theme_constant_override("margin_top", 60)
	main_margin.add_theme_constant_override("margin_bottom", 60)
	add_child(main_margin)
	
	# 3. 版本号 (右上角)
	var version_label = Label.new()
	version_label.text = VERSION_STRING
	version_label.size_flags_horizontal = Control.SIZE_SHRINK_END
	version_label.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	version_label.modulate.a = 0.4
	main_margin.add_child(version_label)

	# 4. 版权信息 (底部中心)
	var footer = Label.new()
	footer.text = "© 2026 剑与六芒星 Sword & Hex. 保留所有权利。"
	footer.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	footer.size_flags_vertical = Control.SIZE_SHRINK_END
	footer.modulate.a = 0.3
	main_margin.add_child(footer)

	# 5. 核心内容居中容器 (自动处理垂直和水平居中，绝不偏斜)
	var center_cont = CenterContainer.new()
	center_cont.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_margin.add_child(center_cont)
	
	var main_vbox = VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 100) # 标题与按钮的超大间距
	center_cont.add_child(main_vbox)
	
	# 5.1 标题区
	var title_vbox = VBoxContainer.new()
	title_vbox.add_theme_constant_override("separation", 15)
	main_vbox.add_child(title_vbox)
	
	var title_label = Label.new()
	title_label.text = "剑 与 六 芒 星"
	title_label.add_theme_font_size_override("font_size", 110) # 增大字体
	title_label.add_theme_color_override("font_color", Color(0.95, 0.85, 0.6))
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title_vbox.add_child(title_label)
	
	var subtitle = Label.new()
	subtitle.text = "SWORD & HEX"
	subtitle.add_theme_font_size_override("font_size", 32)
	subtitle.add_theme_color_override("font_color", Color(0.5, 0.5, 0.6))
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title_vbox.add_child(subtitle)

	# 5.2 按钮区
	var menu_vbox = VBoxContainer.new()
	menu_vbox.add_theme_constant_override("separation", 25)
	menu_vbox.custom_minimum_size = Vector2(350, 0) # 按钮加宽
	main_vbox.add_child(menu_vbox)
	
	_create_menu_button("新的起点", "new_game", menu_vbox)
	_create_menu_button("快速游戏", "quick_game", menu_vbox)
	_create_menu_button("快速战斗", "quick_combat", menu_vbox)
	
	var continue_btn = _create_menu_button("继续旅程", "continue", menu_vbox)
	if not save_manager.has_save():
		continue_btn.disabled = true
		continue_btn.modulate.a = 0.4
	
	_create_menu_button("设置", "settings", menu_vbox)
	_create_menu_button("退出", "exit", menu_vbox)

func _create_menu_button(text: String, action_name: String, parent: Control) -> Button:
	var btn = Button.new()
	btn.text = text
	btn.custom_minimum_size = Vector2(250, 60) # 按钮加高
	btn.add_theme_font_size_override("font_size", 24)
	
	# 正式版按钮样式
	var style = StyleBoxFlat.new()
	style.bg_color = Color(0.1, 0.1, 0.1, 0.6)
	style.set_border_width_all(2)
	style.border_color = Color(0.4, 0.35, 0.25)
	style.corner_radius_top_left = 4
	style.corner_radius_bottom_right = 4
	btn.add_theme_stylebox_override("normal", style)
	
	var hover_style = style.duplicate()
	hover_style.bg_color = Color(0.2, 0.18, 0.15, 0.8)
	hover_style.border_color = Color(0.9, 0.8, 0.5)
	btn.add_theme_stylebox_override("hover", hover_style)
	
	btn.pressed.connect(_on_menu_button_pressed.bind(action_name))
	parent.add_child(btn)
	return btn

func _on_menu_button_pressed(action_name: String):
	match action_name:
		"new_game":
			GlobalState.is_loading_save = false
			GlobalState.is_quick_game = false
			save_manager.delete_save()
			get_tree().change_scene_to_file("res://src/ui/main_menu/origin_select.tscn")
		"quick_game":
			GlobalState.is_loading_save = false
			GlobalState.is_quick_game = true
			save_manager.delete_save()
			# 快速游戏：随机生成角色直接进入大地图
			var CharacterGenerator = load("res://src/core/character/CharacterGenerator.gd")
			var unit_data = CharacterGenerator.generate_character()
			GlobalState.player_origin = {
				"race": unit_data.race,
				"unit_data": unit_data,
			}
			LoadingScreenClass.load_scene("res://src/scenes/overworld/overworld_scene.tscn",
				LoadingScreenClass.PhaseType.QUICK_GAME)
		"continue":
			GlobalState.is_loading_save = true
			GlobalState.is_quick_game = false
			GlobalState.loaded_data = save_manager.load_game_data()
			LoadingScreenClass.load_scene("res://src/scenes/overworld/overworld_scene.tscn",
				LoadingScreenClass.PhaseType.LOAD_SAVE)
		"quick_combat":
			GlobalState.is_loading_save = false
			GlobalState.is_quick_game = false
			LoadingScreenClass.load_scene("res://src/scenes/combat/QuickCombatScene.tscn",
				LoadingScreenClass.PhaseType.QUICK_COMBAT)
		"settings":
			settings_panel.show_settings()
		"exit":
			get_tree().quit()

func _on_settings_closed():
	pass # 设置关闭后无需特殊处理，主菜单仍在
