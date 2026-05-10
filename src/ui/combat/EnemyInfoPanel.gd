# EnemyInfoPanel.gd
# 右侧敌方信息面板 - 显示所有可见敌方单位的列表
# 布局：垂直滚动列表，每个条目包含名称/HP条/AC/威胁等级/AI策略标签
extends PanelContainer
class_name EnemyInfoPanel

## 敌方单位条目场景（动态创建）
var enemy_list: VBoxContainer
var enemy_entries: Dictionary = {}  # unit_name -> Control
var scroll_container: ScrollContainer

## 样式常量
const BG_COLOR := Color(0.08, 0.06, 0.1, 0.92)
const BORDER_COLOR := Color(0.4, 0.15, 0.15, 0.8)
const HP_BAR_BG := Color(0.2, 0.1, 0.1, 0.6)
const HP_HIGH := Color(0.2, 0.75, 0.2)
const HP_MID := Color(0.85, 0.75, 0.1)
const HP_LOW := Color(0.9, 0.15, 0.1)
const MORALE_HIGH := Color(0.2, 0.8, 0.9)
const MORALE_NORMAL := Color(0.6, 0.6, 0.6)
const MORALE_LOW := Color(0.9, 0.7, 0.1)
const MORALE_BROKEN := Color(0.9, 0.2, 0.1)
const ENEMY_TYPE_COLORS := {
	UnitData.EnemyType.HUMANOID: Color(0.7, 0.65, 0.55),
	UnitData.EnemyType.BEAST: Color(0.6, 0.5, 0.3),
	UnitData.EnemyType.UNDEAD: Color(0.5, 0.55, 0.7),
	UnitData.EnemyType.DEMON: Color(0.7, 0.3, 0.5),
	UnitData.EnemyType.GIANT: Color(0.8, 0.5, 0.2),
}

signal enemy_hovered(unit: Unit)
signal enemy_unhovered

func _ready():
	_setup_panel()

func _setup_panel():
	# 面板样式
	var style := StyleBoxFlat.new()
	style.bg_color = BG_COLOR
	style.set_border_width_all(2)
	style.border_color = BORDER_COLOR
	style.set_corner_radius_all(4)
	style.set_content_margin_all(6)
	add_theme_stylebox_override("panel", style)
	
	# 固定宽度
	custom_minimum_size = Vector2(220, 0)
	
	# 标题
	var title := Label.new()
	title.text = "— 敌 方 —"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 16)
	title.add_theme_color_override("font_color", Color(0.9, 0.4, 0.4))
	add_child(title)
	
	# 分隔线
	var sep := HSeparator.new()
	sep.add_theme_stylebox_override("separator", _make_line_style(Color(0.4, 0.15, 0.15, 0.5)))
	add_child(sep)
	
	# 滚动容器
	scroll_container = ScrollContainer.new()
	scroll_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll_container.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER
	scroll_container.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	add_child(scroll_container)
	
	enemy_list = VBoxContainer.new()
	enemy_list.add_theme_constant_override("separation", 4)
	scroll_container.add_child(enemy_list)

func _make_line_style(color: Color) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.set_content_margin_all(1)
	return s


## 添加一个敌方单位到列表
func add_enemy(unit: Unit):
	if not unit or not unit.data or enemy_entries.has(unit.name):
		return
	
	var entry := _create_enemy_entry(unit)
	enemy_list.add_child(entry)
	enemy_entries[unit.name] = entry

## 移除一个敌方单位（死亡时）
func remove_enemy(unit: Unit):
	if not unit or not enemy_entries.has(unit.name):
		return
	
	var entry = enemy_entries[unit.name]
	enemy_list.remove_child(entry)
	entry.queue_free()
	enemy_entries.erase(unit.name)

## 更新指定敌方单位的信息
func update_enemy(unit: Unit):
	if not unit or not unit.data or not enemy_entries.has(unit.name):
		return
	
	var entry: Control = enemy_entries[unit.name]
	var data: UnitData = unit.data
	var max_hp: int = unit.get_max_hp()
	var hp_ratio: float = float(unit.current_hp) / float(max(max_hp, 1))
	
	# 更新 HP 条
	var hp_bar: ProgressBar = entry.get_node_or_null("HPBar")
	if hp_bar:
		hp_bar.value = unit.current_hp
		hp_bar.max_value = max_hp
		hp_bar.get("theme_override_styles/fill").bg_color = _get_hp_color(hp_ratio)
	
	# 更新 HP 文本
	var hp_label: Label = entry.get_node_or_null("HPLabel")
	if hp_label:
		hp_label.text = "HP %d/%d" % [unit.current_hp, max_hp]
	
	# 更新士气条
	var morale_bar: ProgressBar = entry.get_node_or_null("MoraleBar")
	if morale_bar and data.is_enemy:
		# 士气范围 -60 ~ +40，映射为进度条 0~100
		var morale_normalized := float(data.morale + 60) / 100.0
		morale_bar.value = morale_normalized * 100
		morale_bar.get("theme_override_styles/fill").bg_color = _get_morale_color(data.get_morale_level())
	
	# 更新士气标签
	var morale_label: Label = entry.get_node_or_null("MoraleLabel")
	if morale_label and data.is_enemy:
		morale_label.text = _get_morale_text(data.get_morale_level())

## 高亮指定敌方（被悬停/选中时）
func highlight_enemy(unit: Unit, highlighted: bool):
	if not unit or not enemy_entries.has(unit.name):
		return
	
	var entry: PanelContainer = enemy_entries[unit.name]
	var style: StyleBoxFlat = entry.get("theme_override_styles/panel")
	if style:
		if highlighted:
			style.bg_color = Color(0.5, 0.15, 0.15, 0.7)
			style.border_color = Color(0.9, 0.4, 0.4)
		else:
			style.bg_color = Color(0.15, 0.08, 0.1, 0.6)
			style.border_color = Color(0.3, 0.15, 0.15, 0.4)


## 创建单个敌方条目
func _create_enemy_entry(unit: Unit) -> PanelContainer:
	var data: UnitData = unit.data
	var max_hp: int = unit.get_max_hp()
	var hp_ratio: float = float(unit.current_hp) / float(max(max_hp, 1))
	
	var entry := PanelContainer.new()
	var entry_style := StyleBoxFlat.new()
	entry_style.bg_color = Color(0.15, 0.08, 0.1, 0.6)
	entry_style.set_border_width_all(1)
	entry_style.border_color = Color(0.3, 0.15, 0.15, 0.4)
	entry_style.set_corner_radius_all(3)
	entry_style.set_content_margin_all(5)
	entry.add_theme_stylebox_override("panel", entry_style)
	
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 2)
	entry.add_child(vbox)
	
	# 第一行：名称 + 威胁等级标签
	var row1 := HBoxContainer.new()
	vbox.add_child(row1)
	
	var name_label := Label.new()
	name_label.text = data.unit_name
	name_label.add_theme_font_size_override("font_size", 13)
	name_label.add_theme_color_override("font_color", Color(0.95, 0.85, 0.8))
	name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	row1.add_child(name_label)
	
	# CR标签
	if data.is_enemy:
		var cr_label := Label.new()
		cr_label.text = data.get_cr_text()
		cr_label.add_theme_font_size_override("font_size", 11)
		cr_label.add_theme_color_override("font_color", Color(0.9, 0.75, 0.3))
		row1.add_child(cr_label)
	
	# 第二行：敌人类型标签 + AI策略标签
	if data.is_enemy:
		var row1b := HBoxContainer.new()
		row1b.add_theme_constant_override("separation", 4)
		vbox.add_child(row1b)
		
		var type_label := Label.new()
		type_label.text = "[ %s ]" % data.get_enemy_type_name()
		type_label.add_theme_font_size_override("font_size", 10)
		type_label.add_theme_color_override("font_color", ENEMY_TYPE_COLORS.get(data.enemy_type, Color.GRAY))
		row1b.add_child(type_label)
		
		var strat_label := Label.new()
		strat_label.text = "AI: %s" % data.get_ai_strategy_name()
		strat_label.add_theme_font_size_override("font_size", 10)
		strat_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		row1b.add_child(strat_label)
	
	# 第三行：HP 条
	var hp_hbox := HBoxContainer.new()
	vbox.add_child(hp_hbox)
	
	var hp_bar := ProgressBar.new()
	hp_bar.name = "HPBar"
	hp_bar.min_value = 0
	hp_bar.max_value = max_hp
	hp_bar.value = unit.current_hp
	hp_bar.custom_minimum_size = Vector2(120, 12)
	hp_bar.show_percentage = false
	
	var hp_bg := StyleBoxFlat.new()
	hp_bg.bg_color = HP_BAR_BG
	hp_bg.set_corner_radius_all(2)
	hp_bar.add_theme_stylebox_override("background", hp_bg)
	
	var hp_fill := StyleBoxFlat.new()
	hp_fill.bg_color = _get_hp_color(hp_ratio)
	hp_fill.set_corner_radius_all(2)
	hp_bar.add_theme_stylebox_override("fill", hp_fill)
	
	hp_hbox.add_child(hp_bar)
	
	var hp_label := Label.new()
	hp_label.name = "HPLabel"
	hp_label.text = "HP %d/%d" % [unit.current_hp, max_hp]
	hp_label.add_theme_font_size_override("font_size", 10)
	hp_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.8))
	hp_hbox.add_child(hp_label)
	
	# 第四行：士气条（仅敌方显示）
	if data.is_enemy:
		var morale_hbox := HBoxContainer.new()
		vbox.add_child(morale_hbox)
		
		var morale_icon := Label.new()
		morale_icon.text = "士气"
		morale_icon.add_theme_font_size_override("font_size", 9)
		morale_icon.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		morale_hbox.add_child(morale_icon)
		
		var morale_bar := ProgressBar.new()
		morale_bar.name = "MoraleBar"
		morale_bar.min_value = 0
		morale_bar.max_value = 100
		var morale_normalized := float(data.morale + 60) / 100.0
		morale_bar.value = morale_normalized * 100
		morale_bar.custom_minimum_size = Vector2(80, 8)
		morale_bar.show_percentage = false
		
		var morale_bg := StyleBoxFlat.new()
		morale_bg.bg_color = Color(0.15, 0.15, 0.15, 0.5)
		morale_bg.set_corner_radius_all(1)
		morale_bar.add_theme_stylebox_override("background", morale_bg)
		
		var morale_fill := StyleBoxFlat.new()
		morale_fill.bg_color = _get_morale_color(data.get_morale_level())
		morale_fill.set_corner_radius_all(1)
		morale_bar.add_theme_stylebox_override("fill", morale_fill)
		
		morale_hbox.add_child(morale_bar)
		
		var morale_label := Label.new()
		morale_label.name = "MoraleLabel"
		morale_label.text = _get_morale_text(data.get_morale_level())
		morale_label.add_theme_font_size_override("font_size", 9)
		morale_label.add_theme_color_override("font_color", _get_morale_color(data.get_morale_level()))
		morale_hbox.add_child(morale_label)
	
	# 第五行：AC/速度信息
	var row_bottom := HBoxContainer.new()
	row_bottom.add_theme_constant_override("separation", 10)
	vbox.add_child(row_bottom)
	
	var ac_label := Label.new()
	ac_label.text = "AC %d" % unit.get_ac()
	ac_label.add_theme_font_size_override("font_size", 10)
	ac_label.add_theme_color_override("font_color", Color(0.6, 0.7, 0.8))
	row_bottom.add_child(ac_label)
	
	var speed_label := Label.new()
	speed_label.text = "速度 %d格" % data.base_move_range
	speed_label.add_theme_font_size_override("font_size", 10)
	speed_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
	row_bottom.add_child(speed_label)
	
	# 免疫/抗性标签
	if data.is_enemy and (data.immunities.size() > 0 or data.resistances.size() > 0):
		var resist_row := HBoxContainer.new()
		resist_row.add_theme_constant_override("separation", 3)
		vbox.add_child(resist_row)
		
		for imm in data.immunities:
			var imm_label := Label.new()
			imm_label.text = "[免疫:%s]" % imm
			imm_label.add_theme_font_size_override("font_size", 9)
			imm_label.add_theme_color_override("font_color", Color(0.4, 0.7, 0.9))
			resist_row.add_child(imm_label)
		
		for res in data.resistances:
			var res_label := Label.new()
			res_label.text = "[抗性:%s]" % res
			res_label.add_theme_font_size_override("font_size", 9)
			res_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.4))
			resist_row.add_child(res_label)
	
	# 悬停信号
	entry.gui_input.connect(_on_entry_input.bind(unit))
	
	return entry

func _on_entry_input(event: InputEvent, unit: Unit):
	if event is InputEventMouseMotion:
		if event.relative == Vector2.ZERO:  # 进入
			enemy_hovered.emit(unit)
	elif event is InputEventMouseButton and event.pressed:
		pass  # 可扩展：点击选中敌方

func _get_hp_color(ratio: float) -> Color:
	if ratio > 0.6:
		return HP_HIGH
	elif ratio > 0.3:
		return HP_MID
	else:
		return HP_LOW

func _get_morale_color(level: int) -> Color:
	match level:
		UnitData.MoraleLevel.HIGH: return MORALE_HIGH
		UnitData.MoraleLevel.NORMAL: return MORALE_NORMAL
		UnitData.MoraleLevel.LOW: return MORALE_LOW
		UnitData.MoraleLevel.BROKEN, UnitData.MoraleLevel.ROUTING: return MORALE_BROKEN
		_: return MORALE_NORMAL

func _get_morale_text(level: int) -> String:
	match level:
		UnitData.MoraleLevel.HIGH: return "高昂"
		UnitData.MoraleLevel.NORMAL: return "正常"
		UnitData.MoraleLevel.LOW: return "低落"
		UnitData.MoraleLevel.BROKEN: return "崩溃!"
		UnitData.MoraleLevel.ROUTING: return "溃逃!!"
		_: return "正常"
