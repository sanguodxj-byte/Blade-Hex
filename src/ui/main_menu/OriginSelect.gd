# OriginSelect.gd
# 玩家出身选择界面 — 选择种族、手动分配属性点、确认出发
# 属性系统：1级总值25点，每项最低1最高20
extends CanvasLayer
class_name OriginSelect

var _theme: UITheme
var _factory: UIFactory

# 当前状态
var _all_races: Array[RaceData] = []
var _selected_race: RaceData = null
# 基础属性（不含种族/特质修正）— 这是玩家分配的点数
var _base_attrs: Dictionary = {}
var _current_traits: Array[TraitData] = []
var _name_input: LineEdit
# 剩余可分配点数
var _remaining_points: int = 0

# UI 引用
var _race_buttons_container: VBoxContainer
var _race_desc_label: RichTextLabel
var _attr_panel: VBoxContainer
var _trait_panel: VBoxContainer
var _points_label: Label

const LEVEL = 1  # 出身选择固定1级

func _ready():
	_theme = UITheme.get_instance()
	_factory = UIFactory.new()
	_all_races = RaceData.get_all_races()
	_selected_race = _all_races[0]
	_setup_ui()
	_reset_attrs()
	_refresh_all()

func _setup_ui():
	# 1. 全屏背景
	var bg = ColorRect.new()
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.color = Color(0.04, 0.04, 0.06)
	add_child(bg)
	
	# 2. 全屏边距
	var main_margin = MarginContainer.new()
	main_margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_margin.add_theme_constant_override("margin_left", 80)
	main_margin.add_theme_constant_override("margin_right", 80)
	main_margin.add_theme_constant_override("margin_top", 40)
	main_margin.add_theme_constant_override("margin_bottom", 40)
	add_child(main_margin)
	
	# 垂直布局容器（MarginContainer 不做布局，需要 VBoxContainer 管理子节点）
	var main_vbox = VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	main_vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_margin.add_child(main_vbox)
	
	# 3. 顶部标题栏
	var header = HBoxContainer.new()
	header.add_theme_constant_override("separation", 20)
	main_vbox.add_child(header)
	
	var title = Label.new()
	title.text = "选择你的出身"
	title.add_theme_font_size_override("font_size", 48)
	title.add_theme_color_override("font_color", _theme.text_accent)
	header.add_child(title)
	
	# 返回按钮
	var back_btn = Button.new()
	back_btn.text = "返回主菜单"
	back_btn.custom_minimum_size = Vector2(140, 40)
	_theme.apply_button_theme(back_btn)
	back_btn.add_theme_font_size_override("font_size", 16)
	back_btn.pressed.connect(_on_back_pressed)
	back_btn.size_flags_horizontal = Control.SIZE_SHRINK_END
	header.add_child(back_btn)
	
	# 4. 内容区域
	var content = HBoxContainer.new()
	content.add_theme_constant_override("separation", 40)
	content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(content)
	
	# ── 左侧面板：种族选择 ──
	_build_race_panel(content)
	
	# ── 右侧面板：属性分配 + 特质 ──
	_build_attr_panel(content)
	
	# 5. 底部操作栏
	_build_bottom_bar(main_vbox)

# ============================================================================
# 左侧：种族选择面板
# ============================================================================

func _build_race_panel(parent: HBoxContainer):
	var left_panel = PanelContainer.new()
	left_panel.custom_minimum_size = Vector2(400, 0)
	left_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left_panel.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_panel, _theme.border_default, 1, _theme.radius_md, _theme.spacing_lg))
	parent.add_child(left_panel)
	
	var left_vbox = VBoxContainer.new()
	left_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	left_panel.add_child(left_vbox)
	
	# 种族标题
	var race_title = Label.new()
	race_title.text = "种族选择"
	race_title.add_theme_font_size_override("font_size", _theme.font_size_xxl)
	race_title.add_theme_color_override("font_color", _theme.text_accent)
	left_vbox.add_child(race_title)
	
	# 种族按钮列表
	_race_buttons_container = VBoxContainer.new()
	_race_buttons_container.add_theme_constant_override("separation", _theme.spacing_sm)
	left_vbox.add_child(_race_buttons_container)
	
	for race in _all_races:
		var race_btn = _create_race_button(race)
		_race_buttons_container.add_child(race_btn)
	
	# 分隔线
	left_vbox.add_child(_factory.create_separator_h())
	
	# 种族详情描述
	_race_desc_label = RichTextLabel.new()
	_race_desc_label.bbcode_enabled = true
	_race_desc_label.fit_content = true
	_race_desc_label.scroll_active = false
	_race_desc_label.custom_minimum_size = Vector2(0, 120)
	_race_desc_label.add_theme_font_size_override("normal_font_size", _theme.font_size_md)
	_race_desc_label.add_theme_color_override("default_color", _theme.text_secondary)
	left_vbox.add_child(_race_desc_label)
	
	# 分隔线
	left_vbox.add_child(_factory.create_separator_h())
	
	# 角色命名区
	var name_box = VBoxContainer.new()
	name_box.add_theme_constant_override("separation", _theme.spacing_xs)
	left_vbox.add_child(name_box)
	
	var name_label = Label.new()
	name_label.text = "角色名（留空则随机生成）"
	name_label.add_theme_font_size_override("font_size", _theme.font_size_sm)
	name_label.add_theme_color_override("font_color", _theme.text_muted)
	name_box.add_child(name_label)
	
	_name_input = LineEdit.new()
	_name_input.placeholder_text = "输入角色名..."
	_name_input.custom_minimum_size = Vector2(0, 36)
	_name_input.add_theme_font_size_override("font_size", _theme.font_size_md)
	var input_style = StyleBoxFlat.new()
	input_style.bg_color = _theme.bg_card
	input_style.set_border_width_all(1)
	input_style.border_color = _theme.border_default
	input_style.set_corner_radius_all(_theme.radius_md)
	input_style.set_content_margin_all(_theme.spacing_sm)
	_name_input.add_theme_stylebox_override("normal", input_style)
	var focus_style = input_style.duplicate()
	focus_style.border_color = _theme.border_highlight
	_name_input.add_theme_stylebox_override("focus", focus_style)
	name_box.add_child(_name_input)

func _create_race_button(race: RaceData) -> Button:
	var btn = Button.new()
	var summary = _get_race_attr_summary(race)
	btn.text = race.race_name + "  " + summary
	btn.custom_minimum_size = Vector2(0, 48)
	_theme.apply_button_theme(btn)
	btn.add_theme_font_size_override("font_size", _theme.font_size_lg)
	
	var race_color = _get_race_color(race.race_id)
	btn.add_theme_color_override("font_color", race_color)
	btn.add_theme_color_override("font_hover_color", Color(race_color.r + 0.2, race_color.g + 0.2, race_color.b + 0.2))
	
	btn.pressed.connect(_on_race_selected.bind(race))
	btn.set_meta("race_data", race)
	return btn

func _get_race_attr_summary(race: RaceData) -> String:
	var plain_parts: Array[String] = []
	var attrs = {
		"STR": race.str_mod, "DEX": race.dex_mod, "CON": race.con_mod,
		"INT": race.int_mod, "WIS": race.wis_mod, "CHA": race.cha_mod,
	}
	for key in attrs:
		var val = attrs[key]
		if val > 0:
			plain_parts.append("%s+%d" % [key, val])
		elif val < 0:
			plain_parts.append("%s%d" % [key, val])
	return "(" + ", ".join(plain_parts) + ")"

func _get_race_color(race_id: int) -> Color:
	match race_id:
		RaceData.Race.HUMAN: return _theme.race_human
		RaceData.Race.ELF: return _theme.race_elf
		RaceData.Race.DWARF: return _theme.race_dwarf
		RaceData.Race.HALF_ORC: return _theme.race_halforc
		RaceData.Race.HALF_ELF: return _theme.race_halfelf
		_: return _theme.text_primary

# ============================================================================
# 右侧：属性分配面板
# ============================================================================

func _build_attr_panel(parent: HBoxContainer):
	var right_panel = PanelContainer.new()
	right_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_panel.add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_secondary, _theme.border_default, 1, _theme.radius_md, _theme.spacing_lg))
	parent.add_child(right_panel)
	
	var right_vbox = VBoxContainer.new()
	right_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	right_panel.add_child(right_vbox)
	
	# 属性标题 + 剩余点数
	var header_hbox = HBoxContainer.new()
	header_hbox.add_theme_constant_override("separation", _theme.spacing_xl)
	right_vbox.add_child(header_hbox)
	
	var attr_title = Label.new()
	attr_title.text = "分配属性点"
	attr_title.add_theme_font_size_override("font_size", _theme.font_size_xxl)
	attr_title.add_theme_color_override("font_color", _theme.text_accent)
	header_hbox.add_child(attr_title)
	
	_points_label = Label.new()
	_points_label.add_theme_font_size_override("font_size", _theme.font_size_xxl)
	_points_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_points_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	header_hbox.add_child(_points_label)
	
	# 属性分配区
	_attr_panel = VBoxContainer.new()
	_attr_panel.add_theme_constant_override("separation", _theme.spacing_sm)
	_attr_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_vbox.add_child(_attr_panel)
	
	# 分隔线
	right_vbox.add_child(_factory.create_separator_h())
	
	# 特质标题
	var trait_title = Label.new()
	trait_title.text = "随机特质"
	trait_title.add_theme_font_size_override("font_size", _theme.font_size_lg)
	trait_title.add_theme_color_override("font_color", _theme.text_accent)
	right_vbox.add_child(trait_title)
	
	# 特质显示区
	_trait_panel = VBoxContainer.new()
	_trait_panel.add_theme_constant_override("separation", _theme.spacing_xs)
	_trait_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_vbox.add_child(_trait_panel)

# ============================================================================
# 底部操作栏
# ============================================================================

func _build_bottom_bar(parent: VBoxContainer):
	# 底部操作栏：独立面板背景，与内容区域视觉分隔
	var bottom_panel = PanelContainer.new()
	bottom_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom_panel.add_theme_stylebox_override("panel", _theme.make_panel_style(
		Color(0.06, 0.06, 0.08, 0.85), _theme.border_default, 1, _theme.radius_md, _theme.spacing_md))
	parent.add_child(bottom_panel)
	
	var bottom_hbox = HBoxContainer.new()
	bottom_hbox.add_theme_constant_override("separation", _theme.spacing_xl)
	bottom_hbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom_panel.add_child(bottom_hbox)
	
	# 左侧弹性空间，将按钮推到右侧
	var left_spacer = Control.new()
	left_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom_hbox.add_child(left_spacer)
	
	# 重置属性按钮（次要操作）
	var reset_btn = Button.new()
	reset_btn.text = "重置属性"
	reset_btn.custom_minimum_size = Vector2(140, 44)
	var reset_style = _theme.make_button_style(
		Color(0.15, 0.15, 0.18),
		Color(0.22, 0.22, 0.28),
		Color(0.10, 0.10, 0.13)
	)
	_theme.apply_button_theme(reset_btn, reset_style)
	reset_btn.add_theme_font_size_override("font_size", _theme.font_size_lg)
	reset_btn.add_theme_color_override("font_color", _theme.text_secondary)
	reset_btn.add_theme_color_override("font_hover_color", _theme.text_primary)
	reset_btn.pressed.connect(_on_reset_pressed)
	bottom_hbox.add_child(reset_btn)
	
	# 重随特质按钮（次要操作）
	var reroll_trait_btn = Button.new()
	reroll_trait_btn.text = "重随特质"
	reroll_trait_btn.custom_minimum_size = Vector2(140, 44)
	var reroll_style = _theme.make_button_style(
		Color(0.15, 0.15, 0.18),
		Color(0.22, 0.22, 0.28),
		Color(0.10, 0.10, 0.13)
	)
	_theme.apply_button_theme(reroll_trait_btn, reroll_style)
	reroll_trait_btn.add_theme_font_size_override("font_size", _theme.font_size_lg)
	reroll_trait_btn.add_theme_color_override("font_color", _theme.text_secondary)
	reroll_trait_btn.add_theme_color_override("font_hover_color", _theme.text_primary)
	reroll_trait_btn.pressed.connect(_on_reroll_traits)
	bottom_hbox.add_child(reroll_trait_btn)
	
	# 确认出发按钮（主操作 — 金色高亮 CTA）
	var confirm_btn = Button.new()
	confirm_btn.text = "确认出发"
	confirm_btn.custom_minimum_size = Vector2(200, 44)
	var confirm_style = _theme.make_button_style(
		Color(0.28, 0.22, 0.08),
		Color(0.45, 0.36, 0.12),
		Color(0.18, 0.14, 0.05),
		Color(0.12, 0.12, 0.12, 0.5),
		_theme.radius_lg
	)
	_theme.apply_button_theme(confirm_btn, confirm_style)
	confirm_btn.add_theme_font_size_override("font_size", _theme.font_size_xl)
	confirm_btn.add_theme_color_override("font_color", _theme.text_accent)
	confirm_btn.add_theme_color_override("font_hover_color", Color(1.0, 0.92, 0.65))
	confirm_btn.add_theme_color_override("font_pressed_color", _theme.text_secondary)
	confirm_btn.pressed.connect(_on_confirm_pressed)
	bottom_hbox.add_child(confirm_btn)
	
	# 右侧微小间距
	var right_spacer = Control.new()
	right_spacer.custom_minimum_size = Vector2(_theme.spacing_xs, 0)
	bottom_hbox.add_child(right_spacer)

# ============================================================================
# 属性分配逻辑
# ============================================================================

func _reset_attrs():
	# 平均分配25点到6项属性
	_base_attrs = RPGRuleEngine.create_default_attrs(LEVEL)
	_remaining_points = RPGRuleEngine.get_unspent_points(_base_attrs, LEVEL)

func _get_effective_attr(key: String) -> int:
	# 实际属性 = 基础分配 + 种族修正 + 特质修正
	var val = _base_attrs.get(key, 0)
	if _selected_race:
		val += _get_race_mod_for_key(key)
	for t in _current_traits:
		val += _get_trait_mod_for_key(t, key)
	return maxi(1, val)

func _get_race_mod_for_key(key: String) -> int:
	if not _selected_race: return 0
	match key:
		"str": return _selected_race.str_mod
		"dex": return _selected_race.dex_mod
		"con": return _selected_race.con_mod
		"intel": return _selected_race.int_mod
		"wis": return _selected_race.wis_mod
		"cha": return _selected_race.cha_mod
		_: return 0

func _get_trait_mod_for_key(t: TraitData, key: String) -> int:
	match key:
		"str": return t.str_mod
		"dex": return t.dex_mod
		"con": return t.con_mod
		"intel": return t.int_mod
		"wis": return t.wis_mod
		"cha": return t.cha_mod
		_: return 0

func _can_increase(key: String) -> bool:
	return _remaining_points > 0 and _base_attrs.get(key, 0) < RPGRuleEngine.ATTR_MAX

func _can_decrease(key: String) -> bool:
	return _base_attrs.get(key, 0) > RPGRuleEngine.ATTR_MIN

func _increase_attr(key: String):
	if _can_increase(key):
		_base_attrs[key] += 1
		_remaining_points -= 1
		_refresh_attr_display()

func _decrease_attr(key: String):
	if _can_decrease(key):
		_base_attrs[key] -= 1
		_remaining_points += 1
		_refresh_attr_display()

# ============================================================================
# 显示更新
# ============================================================================

func _refresh_all():
	_refresh_race_desc()
	_refresh_attr_display()
	_refresh_trait_display()
	_highlight_selected_race()

func _refresh_race_desc():
	if not _selected_race:
		return
	var desc = _selected_race.traits_description
	var tendency = "适合倾向: " + ", ".join(_selected_race.suitable_tendencies)
	_race_desc_label.text = "[color=%s]%s[/color]\n\n[color=%s]%s[/color]" % [
		_theme.text_primary.to_html(false), desc,
		_theme.text_muted.to_html(false), tendency
	]

func _refresh_attr_display():
	# 更新剩余点数
	if _points_label:
		if _remaining_points > 0:
			_points_label.text = "剩余: %d 点" % _remaining_points
			_points_label.add_theme_color_override("font_color", _theme.text_accent)
		else:
			_points_label.text = "已全部分配"
			_points_label.add_theme_color_override("font_color", _theme.text_positive)
	
	# 清空旧内容
	if _attr_panel:
		for child in _attr_panel.get_children():
			child.queue_free()
	
	if not _attr_panel:
		return
	
	var stat_config = [
		{"key": "str", "name": "力量 STR", "color": _theme.region_str},
		{"key": "dex", "name": "敏捷 DEX", "color": _theme.region_dex},
		{"key": "con", "name": "体质 CON", "color": _theme.region_con},
		{"key": "intel", "name": "智力 INT", "color": _theme.region_int},
		{"key": "wis", "name": "感知 WIS", "color": _theme.region_wis},
		{"key": "cha", "name": "魅力 CHA", "color": _theme.region_cha},
	]
	
	for cfg in stat_config:
		var key = cfg["key"]
		var base_val = _base_attrs.get(key, 0)
		var effective_val = _get_effective_attr(key)
		var mod = RPGRuleEngine.get_stat_modifier(effective_val)
		var race_mod = _get_race_mod_for_key(key)
		
		var hbox = HBoxContainer.new()
		hbox.add_theme_constant_override("separation", _theme.spacing_md)
		hbox.set_meta("attr_key", key)
		
		# 属性名
		var name_lbl = Label.new()
		name_lbl.text = cfg["name"]
		name_lbl.custom_minimum_size = Vector2(130, 0)
		name_lbl.add_theme_font_size_override("font_size", _theme.font_size_lg)
		name_lbl.add_theme_color_override("font_color", cfg["color"])
		hbox.add_child(name_lbl)
		
		# 减号按钮
		var minus_btn = Button.new()
		minus_btn.text = "-"
		minus_btn.custom_minimum_size = Vector2(36, 36)
		_theme.apply_button_theme(minus_btn)
		minus_btn.add_theme_font_size_override("font_size", 20)
		minus_btn.disabled = not _can_decrease(key)
		minus_btn.pressed.connect(_decrease_attr.bind(key))
		hbox.add_child(minus_btn)
		
		# 基础值
		var base_lbl = Label.new()
		base_lbl.text = str(base_val)
		base_lbl.custom_minimum_size = Vector2(36, 0)
		base_lbl.add_theme_font_size_override("font_size", _theme.font_size_xxl)
		base_lbl.add_theme_color_override("font_color", _theme.text_primary)
		base_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		hbox.add_child(base_lbl)
		
		# 加号按钮
		var plus_btn = Button.new()
		plus_btn.text = "+"
		plus_btn.custom_minimum_size = Vector2(36, 36)
		_theme.apply_button_theme(plus_btn)
		plus_btn.add_theme_font_size_override("font_size", 20)
		plus_btn.disabled = not _can_increase(key)
		plus_btn.pressed.connect(_increase_attr.bind(key))
		hbox.add_child(plus_btn)
		
		# 种族修正提示
		var race_lbl = Label.new()
		if race_mod != 0:
			var sign = "+" if race_mod > 0 else ""
			race_lbl.text = "(%s%d)" % [sign, race_mod]
		else:
			race_lbl.text = ""
		race_lbl.custom_minimum_size = Vector2(50, 0)
		race_lbl.add_theme_font_size_override("font_size", _theme.font_size_md)
		var race_mod_color = _theme.text_positive if race_mod > 0 else (_theme.text_negative if race_mod < 0 else _theme.text_muted)
		race_lbl.add_theme_color_override("font_color", race_mod_color)
		hbox.add_child(race_lbl)
		
		# = 最终值
		var eq_lbl = Label.new()
		eq_lbl.text = "= %d" % effective_val
		eq_lbl.custom_minimum_size = Vector2(50, 0)
		eq_lbl.add_theme_font_size_override("font_size", _theme.font_size_lg)
		eq_lbl.add_theme_color_override("font_color", _theme.text_accent)
		hbox.add_child(eq_lbl)
		
		# 修正值
		var mod_text = "(%s%d)" % ["+" if mod >= 0 else "", mod]
		var mod_lbl = Label.new()
		mod_lbl.text = mod_text
		mod_lbl.add_theme_font_size_override("font_size", _theme.font_size_md)
		var mod_color = _theme.text_positive if mod > 0 else (_theme.text_negative if mod < 0 else _theme.text_muted)
		mod_lbl.add_theme_color_override("font_color", mod_color)
		hbox.add_child(mod_lbl)
		
		# 属性条（基于最终值）
		var bar = ProgressBar.new()
		bar.custom_minimum_size = Vector2(160, 16)
		bar.max_value = 20.0
		bar.value = float(effective_val)
		bar.show_percentage = false
		var bar_styles = _theme.make_bar_style(cfg["color"], Color(0.1, 0.1, 0.12, 0.7))
		bar.add_theme_stylebox_override("fill", bar_styles.fill)
		bar.add_theme_stylebox_override("background", bar_styles.background)
		bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		hbox.add_child(bar)
		
		_attr_panel.add_child(hbox)

func _refresh_trait_display():
	if not _trait_panel:
		return
	for child in _trait_panel.get_children():
		child.queue_free()
	
	if _current_traits.is_empty():
		var empty_lbl = Label.new()
		empty_lbl.text = "（无特质）"
		empty_lbl.add_theme_color_override("font_color", _theme.text_muted)
		_trait_panel.add_child(empty_lbl)
		return
	
	for t in _current_traits:
		var hbox = HBoxContainer.new()
		hbox.add_theme_constant_override("separation", _theme.spacing_md)
		
		var name_lbl = Label.new()
		name_lbl.text = t.trait_name
		name_lbl.custom_minimum_size = Vector2(100, 0)
		name_lbl.add_theme_font_size_override("font_size", _theme.font_size_md)
		var is_negative = _is_negative_trait(t)
		name_lbl.add_theme_color_override("font_color",
			_theme.text_negative if is_negative else _theme.text_positive)
		hbox.add_child(name_lbl)
		
		var desc_lbl = Label.new()
		desc_lbl.text = t.description
		desc_lbl.add_theme_font_size_override("font_size", _theme.font_size_sm)
		desc_lbl.add_theme_color_override("font_color", _theme.text_secondary)
		desc_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		hbox.add_child(desc_lbl)
		
		_trait_panel.add_child(hbox)

func _is_negative_trait(t: TraitData) -> bool:
	var total_mod = t.str_mod + t.dex_mod + t.con_mod + t.int_mod + t.wis_mod + t.cha_mod
	if total_mod < 0:
		return true
	var negative_effects = ["old_wound", "gluttony", "timid", "xenophobia"]
	if t.functional_effect in negative_effects:
		return true
	return false

func _highlight_selected_race():
	if not _race_buttons_container:
		return
	for btn in _race_buttons_container.get_children():
		if not btn is Button:
			continue
		var race_data = btn.get_meta("race_data") as RaceData
		if race_data == _selected_race:
			var highlight = StyleBoxFlat.new()
			highlight.bg_color = Color(0.2, 0.18, 0.12, 0.6)
			highlight.set_border_width_all(2)
			highlight.border_color = _theme.text_accent
			highlight.set_corner_radius_all(_theme.radius_md)
			highlight.set_content_margin_all(_theme.spacing_sm)
			btn.add_theme_stylebox_override("normal", highlight)
		else:
			var styles = _theme.make_button_style()
			btn.add_theme_stylebox_override("normal", styles.normal)

# ============================================================================
# 事件处理
# ============================================================================

func _on_race_selected(race: RaceData):
	_selected_race = race
	_refresh_all()

func _on_reset_pressed():
	_reset_attrs()
	_refresh_attr_display()

func _on_reroll_traits():
	_current_traits = CharacterGenerator._roll_traits()
	_refresh_trait_display()
	# 特质修正影响最终属性显示，刷新
	_refresh_attr_display()

func _on_confirm_pressed():
	var unit_data = _build_player_unit()
	GlobalState.is_loading_save = false
	GlobalState.is_quick_game = false
	GlobalState.player_origin = {
		"race": _selected_race,
		"unit_data": unit_data,
	}
	LoadingScreen.load_scene("res://src/scenes/overworld/overworld_scene.tscn",
		LoadingScreen.PhaseType.NEW_WORLD)

func _on_back_pressed():
	get_tree().change_scene_to_file("res://src/ui/main_menu/main_menu.tscn")

# ============================================================================
# 角色构建
# ============================================================================

func _build_player_unit() -> UnitData:
	var unit_data = UnitData.new()
	
	# 角色名
	var char_name = _name_input.text.strip_edges()
	if char_name == "":
		char_name = CharacterGenerator._generate_name(_selected_race)
	unit_data.unit_name = char_name
	
	# 等级与经验
	unit_data.level = LEVEL
	unit_data.xp = 0
	unit_data.skill_points = 0
	unit_data.unspent_attr_points = 0
	
	# 种族与特质
	unit_data.race = _selected_race
	unit_data.character_traits = _current_traits
	
	# 六维属性 = 最终值（基础分配 + 种族修正 + 特质修正，最低1）
	unit_data.str = _get_effective_attr("str")
	unit_data.dex = _get_effective_attr("dex")
	unit_data.con = _get_effective_attr("con")
	unit_data.intel = _get_effective_attr("intel")
	unit_data.wis = _get_effective_attr("wis")
	unit_data.cha = _get_effective_attr("cha")
	
	# 基础HP = 10 + CON修正 × 等级
	var con_mod = RPGRuleEngine.get_stat_modifier(unit_data.con)
	unit_data.base_max_hp = 10 + con_mod * LEVEL
	if _selected_race and "dwarven_resilience" in _selected_race.racial_traits:
		unit_data.base_max_hp += LEVEL
	
	unit_data.base_ac = 10
	unit_data.base_move_range = 4
	unit_data.base_initiative = 0
	
	if _selected_race and "threat_instinct" in _selected_race.racial_traits:
		unit_data.base_initiative += 2
	
	unit_data.current_mana = 10 + RPGRuleEngine.get_stat_modifier(unit_data.intel) * 2
	unit_data.casting_ability = "intel"
	
	if _selected_race and "versatile" in _selected_race.racial_traits:
		unit_data.skill_points += 1
	
	unit_data.loyalty = 100
	
	CharacterGenerator._apply_functional_traits(unit_data, _current_traits)
	
	return unit_data
