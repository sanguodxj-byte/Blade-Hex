# SettingsPanel.gd
# 设置界面 — 四个Tab（视频/音频/游戏/控制）
# 使用 UIFactory/UITheme 保持与项目风格一致
# 可嵌入 MainMenu 或 OverworldUI 的 ESC 菜单
extends CanvasLayer
class_name SettingsPanel

signal settings_closed()

## GameSettings C# 类（运行时加载）
var _GS = load("res://src/core/data/GameSettings.cs")

## 内部状态
var _settings
var _factory: UIFactory
var _theme: UITheme
var _current_tab: int = 0
var _tab_buttons: Array[Button] = []
var _content_container: PanelContainer
var _tab_content: VBoxContainer
var _root_control: Control

## 待应用的设置（编辑副本，确认后才写入）
var _edit_settings


func _ready():
	_theme = UITheme.get_instance()
	_factory = UIFactory.new()
	layer = 50
	_load_settings()
	_build_ui()
	_switch_tab(0)


## ========================================
## 初始化
## ========================================

func _load_settings() -> void:
	_settings = _GS.new()
	_settings.load_from_file()
	_edit_settings = _GS.new()
	_copy_settings(_settings, _edit_settings)


func _copy_settings(src, dst) -> void:
	dst.deserialize(src.serialize())


## ========================================
## 外部接口
## ========================================

func show_settings() -> void:
	_load_settings()
	_switch_tab(_current_tab)
	_root_control.visible = true


func hide_settings() -> void:
	_root_control.visible = false
	settings_closed.emit()


func is_panel_visible() -> bool:
	return _root_control != null and _root_control.visible


## ========================================
## UI 构建
## ========================================

func _build_ui() -> void:
	_root_control = Control.new()
	_root_control.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root_control.mouse_filter = Control.MOUSE_FILTER_STOP
	_root_control.visible = false
	add_child(_root_control)

	# 半透明背景遮罩
	var overlay = ColorRect.new()
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.color = Color(0, 0, 0, 0.6)
	overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	_root_control.add_child(overlay)

	# 居中容器
	var center = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root_control.add_child(center)

	# 主面板 800x600
	var main_panel = PanelContainer.new()
	main_panel.custom_minimum_size = Vector2(800, 600)
	main_panel.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_primary, _theme.border_highlight, 2, _theme.radius_lg, 0))
	center.add_child(main_panel)

	var main_vbox = VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 0)
	main_panel.add_child(main_vbox)

	# ── 标题栏 ──
	_build_title_bar(main_vbox)

	# ── Tab 按钮栏 ──
	_build_tab_bar(main_vbox)

	# ── 分割线 ──
	main_vbox.add_child(_factory.create_separator_h())

	# ── 内容区域 ──
	_content_container = PanelContainer.new()
	_content_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_content_container.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_secondary, Color(), 0, 0, _theme.spacing_lg))
	main_vbox.add_child(_content_container)

	_tab_content = VBoxContainer.new()
	_tab_content.add_theme_constant_override("separation", _theme.spacing_md)
	_tab_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content_container.add_child(_tab_content)

	# ── 底部按钮栏 ──
	_build_bottom_bar(main_vbox)


func _build_title_bar(parent: VBoxContainer) -> void:
	var title_margin = MarginContainer.new()
	title_margin.add_theme_constant_override("margin_left", _theme.spacing_lg)
	title_margin.add_theme_constant_override("margin_right", _theme.spacing_lg)
	title_margin.add_theme_constant_override("margin_top", _theme.spacing_lg)
	title_margin.add_theme_constant_override("margin_bottom", _theme.spacing_sm)
	parent.add_child(title_margin)

	var title_hbox = HBoxContainer.new()
	title_hbox.add_theme_constant_override("separation", _theme.spacing_lg)
	title_margin.add_child(title_hbox)

	var title = Label.new()
	title.text = "设 置"
	title.add_theme_font_size_override("font_size", _theme.font_size_xxl)
	title.add_theme_color_override("font_color", _theme.text_accent)
	title_hbox.add_child(title)

	var spacer = Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_hbox.add_child(spacer)

	var close_btn = _factory.create_button("X", Vector2(36, 36))
	close_btn.pressed.connect(hide_settings)
	title_hbox.add_child(close_btn)


func _build_tab_bar(parent: VBoxContainer) -> void:
	var tab_margin = MarginContainer.new()
	tab_margin.add_theme_constant_override("margin_left", _theme.spacing_lg)
	tab_margin.add_theme_constant_override("margin_right", _theme.spacing_lg)
	parent.add_child(tab_margin)

	var tab_hbox = HBoxContainer.new()
	tab_hbox.add_theme_constant_override("separation", _theme.spacing_xs)
	tab_margin.add_child(tab_hbox)

	var tab_names = ["视频", "音频", "游戏", "控制"]
	_tab_buttons.clear()
	for i in range(tab_names.size()):
		var btn = _factory.create_button(tab_names[i], Vector2(120, _theme.button_height))
		btn.add_theme_font_size_override("font_size", _theme.font_size_md)
		btn.pressed.connect(_switch_tab.bind(i))
		btn.set_meta("tab_index", i)
		_tab_buttons.append(btn)
		tab_hbox.add_child(btn)


func _build_bottom_bar(parent: VBoxContainer) -> void:
	var bottom_margin = MarginContainer.new()
	bottom_margin.add_theme_constant_override("margin_left", _theme.spacing_lg)
	bottom_margin.add_theme_constant_override("margin_right", _theme.spacing_lg)
	bottom_margin.add_theme_constant_override("margin_top", _theme.spacing_sm)
	bottom_margin.add_theme_constant_override("margin_bottom", _theme.spacing_lg)
	parent.add_child(bottom_margin)

	var bottom_hbox = HBoxContainer.new()
	bottom_hbox.add_theme_constant_override("separation", _theme.spacing_md)
	bottom_hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	bottom_margin.add_child(bottom_hbox)

	var defaults_btn = _factory.create_button("恢复默认", Vector2(130, _theme.button_height_lg))
	defaults_btn.pressed.connect(_on_reset_defaults)
	bottom_hbox.add_child(defaults_btn)

	var spacer = Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom_hbox.add_child(spacer)

	var cancel_btn = _factory.create_button("取消", Vector2(100, _theme.button_height_lg))
	cancel_btn.pressed.connect(_on_cancel)
	bottom_hbox.add_child(cancel_btn)

	var apply_btn = _factory.create_button("应用", Vector2(100, _theme.button_height_lg))
	apply_btn.add_theme_color_override("font_color", _theme.text_accent)
	apply_btn.pressed.connect(_on_apply)
	bottom_hbox.add_child(apply_btn)


## ========================================
## Tab 切换
## ========================================

func _switch_tab(index: int) -> void:
	_current_tab = index

	# 更新 Tab 按钮高亮
	for btn in _tab_buttons:
		var idx: int = btn.get_meta("tab_index", 0)
		if idx == index:
			btn.add_theme_color_override("font_color", _theme.text_accent)
		else:
			btn.remove_theme_color_override("font_color")

	# 清空内容
	for child in _tab_content.get_children():
		child.queue_free()

	# 构建对应 Tab 内容
	match index:
		0: _build_video_tab()
		1: _build_audio_tab()
		2: _build_game_tab()
		3: _build_control_tab()


## ========================================
## 视频设置 Tab
## ========================================

func _build_video_tab() -> void:
	var scroll = _factory.create_scroll_container()
	_tab_content.add_child(scroll)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	scroll.add_child(vbox)

	# 分辨率
	_add_option_row(vbox, "分辨率", _GS.get_resolution_presets().map(
		func(r): return r.label), _edit_settings.resolution_index,
		func(idx): _edit_settings.resolution_index = idx)

	# 全屏模式
	_add_option_row(vbox, "全屏模式", _GS.get_fullscreen_mode_names(),
		_edit_settings.fullscreen_mode,
		func(idx): _edit_settings.fullscreen_mode = idx)

	# 垂直同步
	_add_option_row(vbox, "垂直同步", _GS.get_vsync_mode_names(),
		_edit_settings.vsync_mode,
		func(idx): _edit_settings.vsync_mode = idx)


## ========================================
## 音频设置 Tab
## ========================================

func _build_audio_tab() -> void:
	var scroll = _factory.create_scroll_container()
	_tab_content.add_child(scroll)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	scroll.add_child(vbox)

	# 主音量
	_add_slider_row(vbox, "主音量", _edit_settings.master_volume, 0.0, 1.0, 0.05,
		func(val): _edit_settings.master_volume = val)

	# 音乐音量
	_add_slider_row(vbox, "音乐音量", _edit_settings.music_volume, 0.0, 1.0, 0.05,
		func(val): _edit_settings.music_volume = val)

	# 音效音量
	_add_slider_row(vbox, "音效音量", _edit_settings.sfx_volume, 0.0, 1.0, 0.05,
		func(val): _edit_settings.sfx_volume = val)

	# 环境音量
	_add_slider_row(vbox, "环境音量", _edit_settings.ambient_volume, 0.0, 1.0, 0.05,
		func(val): _edit_settings.ambient_volume = val)


## ========================================
## 游戏设置 Tab
## ========================================

func _build_game_tab() -> void:
	var scroll = _factory.create_scroll_container()
	_tab_content.add_child(scroll)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	scroll.add_child(vbox)

	# 难度
	_add_option_row(vbox, "游戏难度", _GS.get_difficulty_names(), _edit_settings.difficulty,
		func(idx): _edit_settings.difficulty = idx)

	# 游戏速度
	_add_slider_row(vbox, "大地图时间流速", _edit_settings.game_speed, 0.5, 8.0, 0.5,
		func(val): _edit_settings.game_speed = val)

	# 战斗动画速度
	_add_slider_row(vbox, "战斗动画速度", _edit_settings.combat_anim_speed, 0.5, 3.0, 0.25,
		func(val): _edit_settings.combat_anim_speed = val)

	# 战斗日志详细度
	_add_option_row(vbox, "战斗日志详细度", _GS.get_log_detail_names(),
		_edit_settings.combat_log_detail,
		func(idx): _edit_settings.combat_log_detail = idx)

	# 自动保存
	_add_check_row(vbox, "自动保存", _edit_settings.auto_save,
		func(val): _edit_settings.auto_save = val)

	# 自动保存间隔
	_add_slider_row(vbox, "自动保存间隔（天）", float(_edit_settings.auto_save_interval), 5.0, 30.0, 5.0,
		func(val): _edit_settings.auto_save_interval = int(val))

	# 显示伤害数字
	_add_check_row(vbox, "显示伤害数字", _edit_settings.show_damage_numbers,
		func(val): _edit_settings.show_damage_numbers = val)

	# 显示战斗网格
	_add_check_row(vbox, "显示战斗网格", _edit_settings.show_combat_grid,
		func(val): _edit_settings.show_combat_grid = val)

	# 结束回合需确认
	_add_check_row(vbox, "结束回合需确认", _edit_settings.confirm_end_turn,
		func(val): _edit_settings.confirm_end_turn = val)

	# 显示小地图
	_add_check_row(vbox, "显示小地图", _edit_settings.show_minimap,
		func(val): _edit_settings.show_minimap = val)


## ========================================
## 控制设置 Tab
## ========================================

func _build_control_tab() -> void:
	var scroll = _factory.create_scroll_container()
	_tab_content.add_child(scroll)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	scroll.add_child(vbox)

	# 鼠标灵敏度
	_add_slider_row(vbox, "鼠标灵敏度", _edit_settings.mouse_sensitivity, 0.1, 3.0, 0.1,
		func(val): _edit_settings.mouse_sensitivity = val)

	# 摄像机边缘滚动
	_add_check_row(vbox, "摄像机边缘滚动", _edit_settings.camera_edge_scroll,
		func(val): _edit_settings.camera_edge_scroll = val)

	# 边缘滚动速度
	_add_slider_row(vbox, "边缘滚动速度", _edit_settings.edge_scroll_speed, 200.0, 1200.0, 50.0,
		func(val): _edit_settings.edge_scroll_speed = val)

	# 缩放速度
	_add_slider_row(vbox, "缩放速度", _edit_settings.camera_zoom_speed, 0.5, 3.0, 0.25,
		func(val): _edit_settings.camera_zoom_speed = val)


## ========================================
## 通用行组件
## ========================================

## 下拉选择行
func _add_option_row(parent: VBoxContainer, label_text: String, options: Array,
		current: int, on_change: Callable) -> void:
	var hbox = HBoxContainer.new()
	hbox.add_theme_constant_override("separation", _theme.spacing_md)
	parent.add_child(hbox)

	var label = Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(180, 0)
	label.add_theme_font_size_override("font_size", _theme.font_size_md)
	label.add_theme_color_override("font_color", _theme.text_primary)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hbox.add_child(label)

	var option_btn = OptionButton.new()
	option_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	option_btn.custom_minimum_size = Vector2(0, _theme.button_height)
	_theme.apply_button_theme(option_btn)
	for opt in options:
		if opt is String:
			option_btn.add_item(opt)
		else:
			option_btn.add_item(str(opt))
	option_btn.selected = clampi(current, 0, options.size() - 1)
	option_btn.item_selected.connect(on_change)
	hbox.add_child(option_btn)


## 滑条行
func _add_slider_row(parent: VBoxContainer, label_text: String, current: float,
		min_val: float, max_val: float, step: float, on_change: Callable) -> void:
	var hbox = HBoxContainer.new()
	hbox.add_theme_constant_override("separation", _theme.spacing_md)
	parent.add_child(hbox)

	var label = Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(180, 0)
	label.add_theme_font_size_override("font_size", _theme.font_size_md)
	label.add_theme_color_override("font_color", _theme.text_primary)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hbox.add_child(label)

	var slider = HSlider.new()
	slider.min_value = min_val
	slider.max_value = max_val
	slider.step = step
	slider.value = current
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	slider.custom_minimum_size = Vector2(200, 0)

	# 滑条样式
	var grabber_style = StyleBoxFlat.new()
	grabber_style.bg_color = _theme.text_accent
	grabber_style.set_corner_radius_all(4)
	slider.add_theme_stylebox_override("grabber_area", grabber_style)

	var fill_style = StyleBoxFlat.new()
	fill_style.bg_color = _theme.text_accent
	fill_style.set_corner_radius_all(2)
	slider.add_theme_stylebox_override("fill", fill_style)

	var bg_style = StyleBoxFlat.new()
	bg_style.bg_color = Color(0.2, 0.2, 0.22)
	bg_style.set_corner_radius_all(2)
	bg_style.set_content_margin_all(3)
	slider.add_theme_stylebox_override("slider", bg_style)

	hbox.add_child(slider)

	# 数值显示
	var val_label = Label.new()
	val_label.custom_minimum_size = Vector2(60, 0)
	val_label.add_theme_font_size_override("font_size", _theme.font_size_md)
	val_label.add_theme_color_override("font_color", _theme.text_accent)
	val_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	val_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER

	# 格式化显示值
	_format_slider_value(val_label, current, step)
	slider.value_changed.connect(func(val):
		_format_slider_value(val_label, val, step)
		on_change.call(val)
	)
	hbox.add_child(val_label)


## 格式化滑条数值显示
func _format_slider_value(label: Label, val: float, step: float) -> void:
	if step >= 1.0:
		label.text = "%d" % int(val)
	elif step >= 0.1:
		label.text = "%.1f" % val
	else:
		label.text = "%.2f" % val


## 勾选行
func _add_check_row(parent: VBoxContainer, label_text: String, current: bool,
		on_change: Callable) -> void:
	var hbox = HBoxContainer.new()
	hbox.add_theme_constant_override("separation", _theme.spacing_md)
	parent.add_child(hbox)

	var label = Label.new()
	label.text = label_text
	label.custom_minimum_size = Vector2(180, 0)
	label.add_theme_font_size_override("font_size", _theme.font_size_md)
	label.add_theme_color_override("font_color", _theme.text_primary)
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hbox.add_child(label)

	var check_btn = CheckButton.new()
	check_btn.button_pressed = current
	check_btn.add_theme_font_size_override("font_size", _theme.font_size_md)
	check_btn.add_theme_color_override("font_color", _theme.text_secondary)

	# 勾选框样式
	var cb_normal = StyleBoxFlat.new()
	cb_normal.bg_color = Color(0.15, 0.14, 0.18)
	cb_normal.set_border_width_all(1)
	cb_normal.border_color = _theme.border_default
	cb_normal.set_corner_radius_all(3)
	cb_normal.set_content_margin_all(6)
	check_btn.add_theme_stylebox_override("normal", cb_normal)

	var cb_pressed = StyleBoxFlat.new()
	cb_pressed.bg_color = Color(0.25, 0.22, 0.15)
	cb_pressed.set_border_width_all(1)
	cb_pressed.border_color = _theme.text_accent
	cb_pressed.set_corner_radius_all(3)
	cb_pressed.set_content_margin_all(6)
	check_btn.add_theme_stylebox_override("pressed", cb_pressed)

	check_btn.toggled.connect(on_change)
	hbox.add_child(check_btn)


## ========================================
## 事件处理
## ========================================

func _on_apply() -> void:
	# 将编辑副本写入正式设置
	_copy_settings(_edit_settings, _settings)
	_settings.save_to_file()
	_settings.apply_to_engine()
	hide_settings()


func _on_cancel() -> void:
	# 丢弃编辑，恢复原始设置
	_copy_settings(_settings, _edit_settings)
	hide_settings()


func _on_reset_defaults() -> void:
	_edit_settings = _GS.new()
	_switch_tab(_current_tab)


## ESC 关闭
func _unhandled_input(event: InputEvent) -> void:
	if not _root_control or not _root_control.visible:
		return
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
		_on_cancel()
		get_viewport().set_input_as_handled()
