# UIFactory.gd
# UI组件工厂 — 统一创建接口，所有UI组件通过此工厂生成
# 预留图像UI切换能力：当UITheme中配置了图像资源时自动切换渲染方式
# 对应策划案 09-UI设计.md 的设计原则
extends RefCounted
class_name UIFactory

## 预加载依赖
const LoadingPhaseDataClass = preload("res://src/ui/loading/LoadingPhaseData.gd")
const TipsDisplayClass = preload("res://src/ui/loading/TipsDisplay.gd")

# ============================================================================
# 便捷访问
# ============================================================================
var _theme: UITheme:
	get: return UITheme.get_instance()

# ============================================================================
# 面板
# ============================================================================

## 创建标准面板
func create_panel(min_size: Vector2 = Vector2.ZERO, bg: Color = Color(),
		border: Color = Color(), content_margin: int = -1) -> PanelContainer:
	var panel := PanelContainer.new()
	if min_size != Vector2.ZERO:
		panel.custom_minimum_size = min_size
	var bg_color = bg if bg != Color() else _theme.bg_panel
	var border_color = border if border != Color() else _theme.border_default
	var margin = content_margin if content_margin >= 0 else _theme.spacing_md
	panel.add_theme_stylebox_override("panel", _theme.make_panel_style(
		bg_color, border_color, 1, _theme.radius_md, margin))
	return panel

## 创建卡片（可悬停高亮）
func create_card(min_size: Vector2 = Vector2.ZERO, hoverable: bool = true) -> PanelContainer:
	var card := PanelContainer.new()
	if min_size != Vector2.ZERO:
		card.custom_minimum_size = min_size
	card.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_card, _theme.border_default, 1, _theme.radius_md, _theme.spacing_sm))
	if hoverable:
		card.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	return card

# ============================================================================
# 按钮
# ============================================================================

## 创建标准按钮
func create_button(text: String, min_size: Vector2 = Vector2(0, 0),
		action_name: String = "") -> Button:
	var btn := Button.new()
	btn.text = text
	if min_size != Vector2.ZERO:
		btn.custom_minimum_size = min_size
	else:
		btn.custom_minimum_size = Vector2(0, _theme.button_height)
	_theme.apply_button_theme(btn)
	# 如果主题有图像资源，切换为TextureButton
	# （未来扩展点：检测 _theme.btn_normal_texture 是否为null）
	return btn

## 创建图标按钮
func create_icon_button(icon_text: String, tooltip: String = "",
		size: int = 36) -> Button:
	var btn := Button.new()
	btn.text = icon_text
	btn.custom_minimum_size = Vector2(size, size)
	_theme.apply_button_theme(btn)
	if tooltip != "":
		btn.tooltip_text = tooltip
	return btn

## 创建操作栏按钮（战斗底部操作面板用）
func create_action_button(label: String, shortcut: String,
		icon: String = "", color: Color = Color()) -> Button:
	var btn := Button.new()
	var display = label + "\n(" + shortcut + ")"
	if icon != "":
		display = icon + " " + display
	btn.text = display
	btn.custom_minimum_size = Vector2(90, 64)
	_theme.apply_button_theme(btn)
	if color != Color():
		btn.add_theme_color_override("font_color", color)
		btn.add_theme_color_override("font_hover_color", Color(color.r + 0.2, color.g + 0.2, color.b + 0.2))
	return btn

# ============================================================================
# 标签
# ============================================================================

## 创建标题标签
func create_title_label(text: String, size: int = -1) -> Label:
	var lbl := Label.new()
	lbl.text = text
	var fs = size if size > 0 else _theme.font_size_xl
	lbl.add_theme_font_size_override("font_size", fs)
	lbl.add_theme_color_override("font_color", _theme.text_accent)
	return lbl

## 创建正文标签
func create_body_label(text: String, color: Color = Color()) -> Label:
	var lbl := Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", _theme.font_size_md)
	var c = color if color != Color() else _theme.text_primary
	lbl.add_theme_color_override("font_color", c)
	return lbl

## 创建次要标签
func create_muted_label(text: String) -> Label:
	var lbl := Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", _theme.font_size_sm)
	lbl.add_theme_color_override("font_color", _theme.text_muted)
	return lbl

## 创建属性名-值对
func create_stat_pair(stat_name: String, value: String,
		name_color: Color = Color(), value_color: Color = Color()) -> HBoxContainer:
	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	
	var name_lbl := Label.new()
	name_lbl.text = stat_name + ":"
	name_lbl.add_theme_color_override("font_color",
		name_color if name_color != Color() else _theme.text_secondary)
	name_lbl.custom_minimum_size = Vector2(100, 0)
	hbox.add_child(name_lbl)
	
	var val_lbl := Label.new()
	val_lbl.text = value
	val_lbl.add_theme_color_override("font_color",
		value_color if value_color != Color() else _theme.text_primary)
	val_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hbox.add_child(val_lbl)
	
	return hbox

# ============================================================================
# 进度条
# ============================================================================

## 创建HP条
func create_hp_bar(width: float = 120, height: int = -1) -> ProgressBar:
	var bar := ProgressBar.new()
	var h = height if height > 0 else _theme.bar_height_md
	bar.custom_minimum_size = Vector2(width, h)
	bar.show_percentage = false
	_theme.apply_bar_theme(bar, _theme.hp_high, _theme.hp_bar_bg)
	return bar

## 创建魔力条
func create_mana_bar(width: float = 120, height: int = -1) -> ProgressBar:
	var bar := ProgressBar.new()
	var h = height if height > 0 else _theme.bar_height_md
	bar.custom_minimum_size = Vector2(width, h)
	bar.show_percentage = false
	_theme.apply_bar_theme(bar, _theme.mana_fill, _theme.mana_bg)
	return bar

## 创建经验条
func create_xp_bar(width: float = 120, height: int = -1) -> ProgressBar:
	var bar := ProgressBar.new()
	var h = height if height > 0 else _theme.bar_height_sm
	bar.custom_minimum_size = Vector2(width, h)
	bar.show_percentage = false
	_theme.apply_bar_theme(bar, _theme.xp_fill, _theme.xp_bg)
	return bar

## 创建自定义颜色进度条
func create_bar(fill_color: Color, bg_color: Color = Color(),
		width: float = 120, height: int = -1) -> ProgressBar:
	var bar := ProgressBar.new()
	var h = height if height > 0 else _theme.bar_height_md
	bar.custom_minimum_size = Vector2(width, h)
	bar.show_percentage = false
	var bg = bg_color if bg_color != Color() else Color(0.1, 0.1, 0.12)
	_theme.apply_bar_theme(bar, fill_color, bg)
	return bar

# ============================================================================
# 容器
# ============================================================================

## 创建带内边距的容器
func create_margin(left: int = -1, right: int = -1, top: int = -1, bottom: int = -1) -> MarginContainer:
	var m := MarginContainer.new()
	m.add_theme_constant_override("margin_left", left if left >= 0 else _theme.spacing_lg)
	m.add_theme_constant_override("margin_right", right if right >= 0 else _theme.spacing_lg)
	m.add_theme_constant_override("margin_top", top if top >= 0 else _theme.spacing_md)
	m.add_theme_constant_override("margin_bottom", bottom if bottom >= 0 else _theme.spacing_md)
	return m

## 创建水平分割线
func create_separator_h(color: Color = Color()) -> HSeparator:
	var sep := HSeparator.new()
	var c = color if color != Color() else _theme.border_default
	var style := StyleBoxFlat.new()
	style.bg_color = c
	style.set_content_margin_all(1)
	sep.add_theme_stylebox_override("separator", style)
	return sep

## 创建垂直分割线
func create_separator_v(color: Color = Color()) -> VSeparator:
	var sep := VSeparator.new()
	var c = color if color != Color() else _theme.border_default
	var style := StyleBoxFlat.new()
	style.bg_color = c
	style.set_content_margin_all(1)
	sep.add_theme_stylebox_override("separator", style)
	return sep

## 创建滚动容器
func create_scroll_container(horizontal: bool = false) -> ScrollContainer:
	var sc := ScrollContainer.new()
	sc.size_flags_vertical = Control.SIZE_EXPAND_FILL
	sc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER if not horizontal else ScrollContainer.SCROLL_MODE_AUTO
	sc.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	return sc

# ============================================================================
# 头像
# ============================================================================

## 创建头像区域
func create_portrait(size: int = -1) -> Control:
	var s = size if size > 0 else _theme.portrait_size
	# 外框
	var container := PanelContainer.new()
	container.custom_minimum_size = Vector2(s, s)
	container.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_card, _theme.border_highlight, 2, _theme.radius_md, 2))
	
	# 内部图像区域
	var rect := TextureRect.new()
	rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	# 预留：如果主题有头像框图像，在此应用
	if _theme.portrait_frame:
		rect.texture = _theme.portrait_frame
	container.add_child(rect)
	
	# 存储引用以便后续设置头像
	container.set_meta("portrait_rect", rect)
	return container

# ============================================================================
# 装备槽
# ============================================================================

## 创建装备槽位
func create_equipment_slot(slot_name: String, size: int = -1) -> PanelContainer:
	var s = size if size > 0 else _theme.icon_size_lg
	var slot := PanelContainer.new()
	slot.custom_minimum_size = Vector2(s, s)
	slot.tooltip_text = slot_name
	slot.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_card, _theme.border_default, 1, _theme.radius_md, 2))
	
	# 图标区
	var icon_rect := TextureRect.new()
	icon_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	icon_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	slot.add_child(icon_rect)
	
	# 槽位名标签
	var name_lbl := Label.new()
	name_lbl.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	name_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_lbl.text = slot_name
	name_lbl.add_theme_font_size_override("font_size", _theme.font_size_xs)
	name_lbl.add_theme_color_override("font_color", _theme.text_muted)
	slot.add_child(name_lbl)
	
	slot.set_meta("icon_rect", icon_rect)
	slot.set_meta("name_label", name_lbl)
	return slot

# ============================================================================
# 物品格子
# ============================================================================

## 创建物品格子
func create_item_slot(size: int = -1) -> Panel:
	var s = size if size > 0 else _theme.icon_size_lg
	var slot := Panel.new()
	slot.custom_minimum_size = Vector2(s, s)
	slot.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_card, _theme.border_default, 1, _theme.radius_sm, 2))
	return slot

# ============================================================================
# 富文本
# ============================================================================

## 创建BBCode富文本
func create_rich_text(min_size: Vector2 = Vector2.ZERO) -> RichTextLabel:
	var rt := RichTextLabel.new()
	if min_size != Vector2.ZERO:
		rt.custom_minimum_size = min_size
	rt.bbcode_enabled = true
	rt.scroll_active = false
	rt.fit_content = true
	return rt

# ============================================================================
# Tab按钮组
# ============================================================================

## 创建标签页按钮组
func create_tab_bar(tabs: Array[String]) -> HBoxContainer:
	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 2)
	for i in range(tabs.size()):
		var btn := create_button(tabs[i], Vector2(0, 30))
		btn.add_theme_font_size_override("font_size", _theme.font_size_sm)
		btn.set_meta("tab_index", i)
		hbox.add_child(btn)
	return hbox

# ============================================================================
# 加载界面
# ============================================================================

## 创建加载进度条（独立组件，可嵌入任意容器）
func create_loading_bar(width: float = 400.0, height: int = -1) -> ProgressBar:
	var bar := ProgressBar.new()
	var h = height if height > 0 else _theme.bar_height_lg
	bar.custom_minimum_size = Vector2(width, h)
	bar.min_value = 0.0
	bar.max_value = 100.0
	bar.value = 0.0
	bar.show_percentage = false
	# 金色填充 + 发光阴影
	var fill_style := StyleBoxFlat.new()
	fill_style.bg_color = _theme.text_accent
	fill_style.set_corner_radius_all(_theme.radius_sm)
	fill_style.shadow_color = Color(_theme.text_accent.r, _theme.text_accent.g,
		_theme.text_accent.b, 0.3)
	fill_style.shadow_size = 4
	bar.add_theme_stylebox_override("fill", fill_style)
	# 深色背景 + 边框
	var bg_style := StyleBoxFlat.new()
	bg_style.bg_color = Color(0.08, 0.08, 0.10, 0.9)
	bg_style.set_border_width_all(1)
	bg_style.border_color = _theme.border_default
	bg_style.set_corner_radius_all(_theme.radius_sm)
	bar.add_theme_stylebox_override("background", bg_style)
	return bar

## 创建加载阶段描述区域（标题 + 描述文本）
func create_loading_phase_display() -> VBoxContainer:
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	
	# 阶段标题
	var title := Label.new()
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", _theme.font_size_xxl)
	title.add_theme_color_override("font_color", _theme.text_accent)
	vbox.add_child(title)
	vbox.set_meta("title_label", title)
	
	# 阶段描述
	var desc := RichTextLabel.new()
	desc.bbcode_enabled = true
	desc.scroll_active = false
	desc.fit_content = true
	desc.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	desc.add_theme_font_size_override("normal_font_size", _theme.font_size_md)
	desc.add_theme_color_override("default_color", _theme.text_secondary)
	var empty_style := StyleBoxEmpty.new()
	empty_style.set_content_margin_all(0)
	desc.add_theme_stylebox_override("normal", empty_style)
	vbox.add_child(desc)
	vbox.set_meta("desc_label", desc)
	
	return vbox

## 创建完整加载界面（嵌入式，不含CanvasLayer）
func create_loading_screen_embedded(phase_type: int = 0) -> VBoxContainer:
	var phases: Array
	match phase_type:
		0: phases = LoadingPhaseDataClass.get_new_world_phases()
		1: phases = LoadingPhaseDataClass.get_load_save_phases()
		2: phases = LoadingPhaseDataClass.get_combat_phases()
		3: phases = LoadingPhaseDataClass.get_quick_game_phases()
		_: phases = LoadingPhaseDataClass.get_new_world_phases()
	
	var root := VBoxContainer.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_theme_constant_override("separation", 0)
	root.alignment = BoxContainer.ALIGNMENT_CENTER
	
	# 顶部间距
	var top_spacer := Control.new()
	top_spacer.custom_minimum_size = Vector2(0, 60)
	root.add_child(top_spacer)
	
	# 阶段描述区域
	var phase_display := create_loading_phase_display()
	phase_display.custom_minimum_size = Vector2(500, 0)
	root.add_child(phase_display)
	
	# 间距
	var gap := Control.new()
	gap.custom_minimum_size = Vector2(0, 30)
	root.add_child(gap)
	
	# 装饰线
	root.add_child(create_separator_h(Color(
		_theme.border_highlight.r, _theme.border_highlight.g,
		_theme.border_highlight.b, 0.3)))
	
	# 间距
	var gap2 := Control.new()
	gap2.custom_minimum_size = Vector2(0, _theme.spacing_lg)
	root.add_child(gap2)
	
	# 进度条
	var bar := create_loading_bar(500)
	bar.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	root.add_child(bar)
	
	# 百分比
	var pct := Label.new()
	pct.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	pct.add_theme_font_size_override("font_size", _theme.font_size_sm)
	pct.add_theme_color_override("font_color", _theme.text_muted)
	pct.text = "0%"
	root.add_child(pct)
	
	# 间距
	var gap3 := Control.new()
	gap3.custom_minimum_size = Vector2(0, _theme.spacing_lg)
	root.add_child(gap3)
	
	# 装饰线
	root.add_child(create_separator_h(Color(
		_theme.border_highlight.r, _theme.border_highlight.g,
		_theme.border_highlight.b, 0.3)))
	
	# 弹性间距推到底部
	var pusher := Control.new()
	pusher.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root.add_child(pusher)
	
	# Tips组件
	var tips = TipsDisplayClass.new()
	tips.custom_minimum_size = Vector2(500, 30)
	tips.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	root.add_child(tips)
	
	# 存储引用
	root.set_meta("progress_bar", bar)
	root.set_meta("percent_label", pct)
	root.set_meta("phase_display", phase_display)
	root.set_meta("tips_display", tips)
	root.set_meta("phases", phases)
	
	return root