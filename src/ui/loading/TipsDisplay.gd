# TipsDisplay.gd
# Tips提示显示组件 — 自动轮播 + 淡入淡出动画
# 使用时作为子节点添加到任意容器中
extends Control
class_name TipsDisplay

## 预加载依赖
const TipsDataClass = preload("res://src/ui/loading/TipsData.gd")

## 信号
signal tip_changed(tip)

## 配置
@export var cycle_interval: float = 5.0
@export var fade_duration: float = 0.6
@export var show_icon: bool = true
@export var auto_start: bool = true

## 内部引用
var _tips_data
var _tip_label: RichTextLabel
var _icon_label: Label
var _container: HBoxContainer
var tween: Tween
var timer: Timer
var _is_running: bool = false

## 分类过滤（空=全部显示）
var filter_category: String = ""

func _ready():
	_build_ui()
	if auto_start:
		start()

## 构建UI
func _build_ui():
	var ui_theme := UITheme.get_instance()
	if ui_theme == null:
		push_error("TipsDisplay: UITheme.get_instance() returned null")
		return
	
	# 容器
	_container = HBoxContainer.new()
	_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_container.add_theme_constant_override("separation", ui_theme.spacing_md)
	_container.alignment = BoxContainer.ALIGNMENT_CENTER
	add_child(_container)
	
	# 图标
	_icon_label = Label.new()
	_icon_label.text = "💡"
	_icon_label.add_theme_font_size_override("font_size", ui_theme.font_size_lg)
	_icon_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_icon_label.visible = show_icon
	_container.add_child(_icon_label)
	
	# 提示文本
	_tip_label = RichTextLabel.new()
	_tip_label.bbcode_enabled = true
	_tip_label.scroll_active = false
	_tip_label.fit_content = true
	_tip_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_tip_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_tip_label.add_theme_font_size_override("normal_font_size", ui_theme.font_size_sm)
	_tip_label.add_theme_color_override("default_color", ui_theme.text_secondary)
	# 透明背景
	_tip_label.add_theme_stylebox_override("normal", _make_transparent_style())
	_container.add_child(_tip_label)
	
	# 定时器
	timer = Timer.new()
	timer.one_shot = false
	timer.wait_time = cycle_interval
	timer.timeout.connect(_on_cycle_timer)
	add_child(timer)

func _make_transparent_style() -> StyleBoxEmpty:
	var style := StyleBoxEmpty.new()
	style.set_content_margin_all(0)
	return style

## 设置数据源
func set_tips_data(data) -> void:
	_tips_data = data
	_tips_data.reset_rotation()

## 设置分类过滤
func set_category_filter(category: String):
	filter_category = category
	if _tips_data:
		_tips_data.reset_rotation()

## 开始轮播
func start():
	if _is_running:
		return
	_is_running = true
	if not _tips_data:
		_tips_data = TipsDataClass.new()
	_show_next_tip()
	timer.start()

## 停止轮播
func stop():
	_is_running = false
	timer.stop()

## 显示下一条提示
func _show_next_tip():
	var tip
	if filter_category != "":
		var filtered = _tips_data.get_tips_by_category(filter_category)
		if filtered.is_empty():
			tip = _tips_data.get_next_tip()
		else:
			# 从过滤后的列表中随机选
			tip = filtered[randi() % filtered.size()]
	else:
		tip = _tips_data.get_next_tip()
	
	if tip:
		_animate_tip_change(tip)

## 淡入淡出动画切换提示
func _animate_tip_change(tip):
	# 淡出
	if tween and tween.is_valid():
		tween.kill()
	tween = create_tween()
	if tween == null:
		return
	tween.set_parallel(false)
	
	# 与LoadingScreen阶段文字统一节奏
	var fade_out_time := 0.2
	var fade_in_time := 0.3
	var hold_time := 0.7
	
	# 匀速淡出
	tween.tween_property(_container, "modulate:a", 0.0, fade_out_time)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	
	# 更新文本
	tween.tween_callback(func():
		if _tip_label:
			_tip_label.text = "[i]%s[/i]" % tip.text
		tip_changed.emit(tip)
	)
	
	# 匀速淡入
	tween.tween_property(_container, "modulate:a", 1.0, fade_in_time)\
		.set_trans(Tween.TRANS_LINEAR).set_ease(Tween.EASE_IN)
	
	# 停留（完全显示后保持可见）
	tween.tween_interval(hold_time)

## 轮播定时回调
func _on_cycle_timer():
	_show_next_tip()

## 手动触发下一条
func show_next():
	_show_next_tip()

## 设置轮播间隔
func set_cycle_interval(seconds: float):
	cycle_interval = seconds
	timer.wait_time = seconds
