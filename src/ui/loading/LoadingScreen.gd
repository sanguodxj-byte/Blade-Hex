# LoadingScreen.gd
# RPG风格加载界面 — 带阶段性描述的条状进度条 + Tips轮播
# 使用方式：
#   LoadingScreen.show_load(_scene_path, phases_type)
#   或在场景切换前调用 LoadingScreen过渡
extends CanvasLayer
class_name LoadingScreen

## 预加载依赖，避免 class_name 解析顺序问题
const _TipsDisplayClass = preload("res://src/ui/loading/TipsDisplay.gd")
const _TipsDataClass = preload("res://src/ui/loading/TipsData.gd")
const _LoadingPhaseDataClass = preload("res://src/ui/loading/LoadingPhaseData.gd")

## 加载完成信号
signal loading_finished

## 加载阶段类型
enum PhaseType {
	NEW_WORLD,     ## 新建世界
	LOAD_SAVE,     ## 加载存档
	COMBAT,        ## 战斗加载
	QUICK_GAME,    ## 快速游戏
	QUICK_COMBAT,  ## 快速战斗
}

## 进度条动画速度
const PROGRESS_SMOOTH_SPEED: float = 2.0

## 内部UI引用
var _bg: ColorRect
var _content_container: VBoxContainer
var _phase_title_label: Label
var _phase_desc_label: RichTextLabel
var _progress_bar: ProgressBar
var _progress_percent_label: Label
var _tips_display
var _decor_line_top: HSeparator
var _decor_line_bottom: HSeparator

## 数据
var _phases: Array = []
var _current_phase = null
var _target_progress: float = 0.0
var _displayed_progress: float = 0.0
var _is_loading: bool = false
var _scene_path: String = ""
var _loading_thread: Thread = null
var _use_thread: bool = false
var _phase_tween: Tween = null
var _animating_phase: bool = false

## 主题引用
var _theme: UITheme:
	get: return UITheme.get_instance()

## ============================================================================
# 单例便捷访问
# ============================================================================
static var _instance = null

static func get_instance():
	if not _instance or not is_instance_valid(_instance):
		_instance = new()
		# 添加到场景树根节点（autoload级别），确保能接收_process
		if Engine.get_main_loop() and Engine.get_main_loop().current_scene:
			Engine.get_main_loop().current_scene.get_tree().root.add_child(_instance)
	return _instance

## 便捷静态方法：显示加载界面并切换场景
static func load_scene(scene_path: String, phase_type: PhaseType = PhaseType.NEW_WORLD,
		_use_thread: bool = false):
	var loader = get_instance()
	loader._start_loading(scene_path, phase_type, _use_thread)
	return loader

func _ready():
	layer = 100  # 确保在最上层
	_build_ui()
	visible = false

## ============================================================================
# UI构建
# ============================================================================
func _build_ui():
	# 全屏深色背景
	_bg = ColorRect.new()
	_bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_bg.color = _theme.bg_primary
	add_child(_bg)
	
	# 主内容区域 — 居中
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	
	# 内容容器
	_content_container = VBoxContainer.new()
	_content_container.custom_minimum_size = Vector2(600, 0)
	_content_container.add_theme_constant_override("separation", 0)
	center.add_child(_content_container)
	
	# ---- 顶部装饰：小标题 ----
	var top_spacer := Control.new()
	top_spacer.custom_minimum_size = Vector2(0, 80)
	_content_container.add_child(top_spacer)
	
	# 阶段标题（大标题，如"大地"）
	_phase_title_label = Label.new()
	_phase_title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_phase_title_label.add_theme_font_size_override("font_size", _theme.font_size_xxl)
	_phase_title_label.add_theme_color_override("font_color", _theme.text_accent)
	_content_container.add_child(_phase_title_label)
	
	# 间距
	var title_gap := Control.new()
	title_gap.custom_minimum_size = Vector2(0, _theme.spacing_lg)
	_content_container.add_child(title_gap)
	
	# ---- 阶段描述文字 ----
	_phase_desc_label = RichTextLabel.new()
	_phase_desc_label.bbcode_enabled = true
	_phase_desc_label.scroll_active = false
	_phase_desc_label.fit_content = true
	_phase_desc_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_phase_desc_label.add_theme_font_size_override("normal_font_size", _theme.font_size_md)
	_phase_desc_label.add_theme_color_override("default_color", _theme.text_secondary)
	_phase_desc_label.add_theme_stylebox_override("normal", _make_empty_style())
	# 居中bbcode
	_content_container.add_child(_phase_desc_label)
	
	# 间距
	var desc_gap := Control.new()
	desc_gap.custom_minimum_size = Vector2(0, 40)
	_content_container.add_child(desc_gap)
	
	# ---- 装饰线 ----
	_decor_line_top = _create_decor_line()
	_content_container.add_child(_decor_line_top)
	
	# 间距
	var line_gap1 := Control.new()
	line_gap1.custom_minimum_size = Vector2(0, _theme.spacing_lg)
	_content_container.add_child(line_gap1)
	
	# ---- 进度条区域 ----
	var progress_container := VBoxContainer.new()
	progress_container.add_theme_constant_override("separation", _theme.spacing_sm)
	_content_container.add_child(progress_container)
	
	# 进度条
	_progress_bar = ProgressBar.new()
	_progress_bar.custom_minimum_size = Vector2(600, 20)
	_progress_bar.min_value = 0.0
	_progress_bar.max_value = 100.0
	_progress_bar.value = 0.0
	_progress_bar.show_percentage = false
	_apply_progress_bar_style()
	progress_container.add_child(_progress_bar)
	
	# 进度百分比文字
	_progress_percent_label = Label.new()
	_progress_percent_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_progress_percent_label.add_theme_font_size_override("font_size", _theme.font_size_sm)
	_progress_percent_label.add_theme_color_override("font_color", _theme.text_muted)
	_progress_percent_label.text = "0%"
	progress_container.add_child(_progress_percent_label)
	
	# 间距
	var line_gap2 := Control.new()
	line_gap2.custom_minimum_size = Vector2(0, _theme.spacing_lg)
	_content_container.add_child(line_gap2)
	
	# ---- 装饰线 ----
	_decor_line_bottom = _create_decor_line()
	_content_container.add_child(_decor_line_bottom)
	
	# 间距 — 推到底部
	var bottom_pusher := Control.new()
	bottom_pusher.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_content_container.add_child(bottom_pusher)
	
	# ---- 底部Tips区域 ----
	_tips_display = _TipsDisplayClass.new()
	_tips_display.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_tips_display.custom_minimum_size = Vector2(0, 40)
	_tips_display.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	_tips_display.offset_top = -60
	_tips_display.offset_bottom = -20
	_tips_display.offset_left = 80
	_tips_display.offset_right = -80
	add_child(_tips_display)

func _make_empty_style() -> StyleBoxEmpty:
	var style := StyleBoxEmpty.new()
	style.set_content_margin_all(0)
	return style

func _create_decor_line() -> HSeparator:
	var sep := HSeparator.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(_theme.border_highlight.r, _theme.border_highlight.g,
		_theme.border_highlight.b, 0.3)
	style.set_content_margin_all(1)
	sep.add_theme_stylebox_override("separator", style)
	return sep

func _apply_progress_bar_style():
	# 进度条填充 — 金色渐变感
	var fill_style := StyleBoxFlat.new()
	fill_style.bg_color = _theme.text_accent
	fill_style.set_corner_radius_all(_theme.radius_sm)
	fill_style.shadow_color = Color(_theme.text_accent.r, _theme.text_accent.g,
		_theme.text_accent.b, 0.3)
	fill_style.shadow_size = 4
	_progress_bar.add_theme_stylebox_override("fill", fill_style)
	
	# 进度条背景
	var bg_style := StyleBoxFlat.new()
	bg_style.bg_color = Color(0.08, 0.08, 0.10, 0.9)
	bg_style.set_border_width_all(1)
	bg_style.border_color = _theme.border_default
	bg_style.set_corner_radius_all(_theme.radius_sm)
	_progress_bar.add_theme_stylebox_override("background", bg_style)

## ============================================================================
# 加载流程
# ============================================================================
func _start_loading(scene_path: String, phase_type: PhaseType, use_thread: bool):
	_scene_path = scene_path
	_use_thread = use_thread
	_target_progress = 0.0
	_displayed_progress = 0.0
	_is_loading = true
	
	# 选择阶段数据
	match phase_type:
		PhaseType.NEW_WORLD:
			_phases = _LoadingPhaseDataClass.get_new_world_phases()
		PhaseType.LOAD_SAVE:
			_phases = _LoadingPhaseDataClass.get_load_save_phases()
		PhaseType.COMBAT:
			_phases = _LoadingPhaseDataClass.get_combat_phases()
		PhaseType.QUICK_GAME:
			_phases = _LoadingPhaseDataClass.get_quick_game_phases()
		PhaseType.QUICK_COMBAT:
			_phases = _LoadingPhaseDataClass.get_quick_combat_phases()
	
	_current_phase = null
	
	# 显示界面
	visible = true
	
	# 启动Tips
	_tips_display.set_tips_data(_TipsDataClass.new())
	_tips_display.start()
	
	# 淡入动画（通过内容容器的 modulate）
	_content_container.modulate.a = 0.0
	var tween := create_tween()
	tween.tween_property(_content_container, "modulate:a", 1.0, 0.3)
	tween.tween_callback(func(): _begin_actual_load())

## 开始实际加载
func _begin_actual_load():
	if _use_thread and _loading_thread == null:
		_loading_thread = Thread.new()
		_loading_thread.start(_threaded_load.bind(_scene_path))
	else:
		# 简单加载：模拟进度推进
		_simulate_load()

## 线程加载（用于重场景）
func _threaded_load(path: String):
	var err := ResourceLoader.load_threaded_request(path)
	if err != OK:
		push_error("LoadingScreen: 无法请求加载资源: %s (错误码: %d)" % [path, err])
		_target_progress = 1.0
		call_deferred("_finish_load")
		return
	
	var progress_array := PackedFloat32Array([0.0])
	while true:
		var status := ResourceLoader.load_threaded_get_status(path, progress_array)
		match status:
			ResourceLoader.THREAD_LOAD_IN_PROGRESS:
				_target_progress = progress_array[0]
				OS.delay_msec(50)
			ResourceLoader.THREAD_LOAD_LOADED:
				_target_progress = 1.0
				break
			_:
				push_error("LoadingScreen: 加载失败: %s" % path)
				_target_progress = 1.0
				break
	
	# 在主线程完成场景切换
	call_deferred("_finish_load")

## 模拟加载进度（用于轻场景/转场）
func _simulate_load():
	var tween := create_tween()
	var total_duration := 6.0  # 总模拟时间（秒），确保Tips至少轮播一次
	var steps := _phases.size()
	if steps == 0:
		steps = 1
	
	var step_duration := total_duration / float(steps)
	
	for i in range(steps):
		var target_val: float = (float(i) + 1.0) / float(steps)
		tween.tween_property(self, "_target_progress", target_val, step_duration * 0.8)
		tween.tween_interval(step_duration * 0.2)
	
	# 最后一步到100%，然后等待短暂停留后切换场景
	tween.tween_property(self, "_target_progress", 1.0, 0.3)
	tween.tween_interval(0.5)
	tween.tween_callback(_finish_load)

## 完成加载，切换场景
func _finish_load():
	_is_loading = false
	_do_scene_transition()

## 手动设置进度（外部驱动模式）
func set_progress(value: float):
	_target_progress = clampf(value, 0.0, 1.0)

## ============================================================================
# 帧更新
# ============================================================================
func _process(delta):
	if not _is_loading and not visible:
		return
	
	# 进度条视觉平滑（仅用于显示，不影响阶段判定）
	_displayed_progress = lerpf(_displayed_progress, _target_progress, 
		PROGRESS_SMOOTH_SPEED * delta)
	
	# 更新进度条UI
	var percent := _displayed_progress * 100.0
	_progress_bar.value = percent
	_progress_percent_label.text = "%d%%" % int(round(percent))
	
	# 阶段文字用实际进度判定，保证切换节奏均匀
	_update_phase_text(_target_progress)

func _update_phase_text(progress: float):
	var phase := _LoadingPhaseDataClass.get_phase_at_progress(_phases, progress)
	if phase == null:
		return
	
	if _current_phase == null or _current_phase.title != phase.title:
		_current_phase = phase
		_animate_phase_change(phase)

func _animate_phase_change(phase):
	# 杀掉旧动画
	if _phase_tween and _phase_tween.is_valid():
		_phase_tween.kill()
	
	var fade_out_time := 0.2
	var fade_in_time := 0.3
	var hold_time := 0.7
	
	_phase_tween = create_tween().set_parallel(false)
	
	# 标题与描述同时淡出（并行）
	_phase_tween.set_parallel(true)
	_phase_tween.tween_property(_phase_title_label, "modulate:a", 0.0, fade_out_time)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	_phase_tween.tween_property(_phase_desc_label, "modulate:a", 0.0, fade_out_time)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	_phase_tween.set_parallel(false)
	
	# 更新内容
	_phase_tween.tween_callback(func():
		_phase_title_label.text = phase.title
		_phase_desc_label.text = "[center][i]%s[/i][/center]" % phase.description
	)
	
	# 标题与描述同时淡入（并行）
	_phase_tween.set_parallel(true)
	_phase_tween.tween_property(_phase_title_label, "modulate:a", 1.0, fade_in_time)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	_phase_tween.tween_property(_phase_desc_label, "modulate:a", 1.0, fade_in_time)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	_phase_tween.set_parallel(false)
	
	# 停留（完全显示后保持可见）
	_phase_tween.tween_interval(hold_time)

## 执行场景切换
func _do_scene_transition():
	if _loading_thread:
		if _loading_thread.is_alive():
			# 线程还在运行，稍后重试
			await get_tree().create_timer(0.1).timeout
			_do_scene_transition()
			return
		
		var resource = ResourceLoader.load_threaded_get(_scene_path)
		_loading_thread.wait_to_finish()
		_loading_thread = null
		
		if resource:
			get_tree().change_scene_to_packed(resource)
		else:
			push_error("LoadingScreen: 加载场景失败: %s" % _scene_path)
			get_tree().change_scene_to_file(_scene_path)
	else:
		get_tree().change_scene_to_file(_scene_path)
	
	# 匀速淡出（CanvasLayer 没有 modulate，对背景和内容容器淡出）
	var tween := create_tween()
	tween.tween_property(_content_container, "modulate:a", 0.0, 0.4)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	tween.parallel().tween_property(_bg, "color:a", 0.0, 0.4)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	tween.tween_callback(func():
		visible = false
		_tips_display.stop()
		loading_finished.emit()
	)
