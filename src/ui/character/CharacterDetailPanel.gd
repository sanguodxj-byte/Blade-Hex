# CharacterDetailPanel.gd
# 完整角色详情面板 — 显示角色全部信息，包括装备槽、特质、种族等
# 对应策划案 09-UI设计.md → 角色详情面板
# 对应策划案 05-角色与职业 → 属性/特质/技能盘
# 对应策划案 06-装备与物品 → 装备槽位总览
extends PanelContainer
class_name CharacterDetailPanel

# ============================================================================
# 信号
# ============================================================================
signal close_requested()
signal equipment_slot_clicked(slot_name: String)
signal skill_tree_requested()

# ============================================================================
# 内部组件
# ============================================================================
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

# 头部信息
var _portrait: Control
var _name_label: Label
var _race_label: Label
var _level_label: Label
var _tendency_label: Label

# HP/MP条
var _hp_bar: ProgressBar
var _hp_label: Label
var _mana_bar: ProgressBar
var _mana_label: Label
var _xp_bar: ProgressBar
var _xp_label: Label
var _morale_bar: MoraleBar

# 属性
var _attr_labels: Dictionary = {}   # str/dex/con/int/wis/cha → Label
var _attr_mod_labels: Dictionary = {} # 属性修正值

# 装备槽
var _equip_slots: Dictionary = {}   # slot_name → Control

# 特质列表
var _trait_container: VBoxContainer

# 状态效果
var _status_display: StatusEffectDisplay

# 主动技能
var _skill_container: VBoxContainer

# 当前显示的单位
var _current_unit: Unit = null

func _ready():
	_factory = UIFactory.new()
	_setup()
	visible = false

func _setup():
	# 全屏面板
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_primary, _theme.border_highlight, 3, _theme.radius_lg, 0))
	
	var root_margin = _factory.create_margin(40, 40, 30, 30)
	add_child(root_margin)
	
	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	root_margin.add_child(main_vbox)
	
	# === 顶部标题栏 ===
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation", _theme.spacing_md)
	main_vbox.add_child(header)
	
	var title = _factory.create_title_label("角色详情", _theme.font_size_xxl)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	
	var skill_btn = _factory.create_button("技能盘", Vector2(100, 36))
	skill_btn.pressed.connect(func(): skill_tree_requested.emit())
	header.add_child(skill_btn)
	
	var close_btn = _factory.create_button("返回 (ESC)", Vector2(120, 36))
	close_btn.pressed.connect(func(): visible = false; close_requested.emit())
	header.add_child(close_btn)
	
	main_vbox.add_child(_factory.create_separator_h())
	
	# === 主体：左中右三栏 ===
	var body := HBoxContainer.new()
	body.add_theme_constant_override("separation", _theme.spacing_xl)
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(body)
	
	# --- 左栏：头像+基础信息+HP/MP ---
	var left_col := VBoxContainer.new()
	left_col.custom_minimum_size = Vector2(240, 0)
	left_col.add_theme_constant_override("separation", _theme.spacing_md)
	body.add_child(left_col)
	
	# 头像+名称
	var portrait_row := HBoxContainer.new()
	portrait_row.add_theme_constant_override("separation", _theme.spacing_md)
	left_col.add_child(portrait_row)
	
	_portrait = _factory.create_portrait(80)
	portrait_row.add_child(_portrait)
	
	var id_vbox := VBoxContainer.new()
	id_vbox.add_theme_constant_override("separation", _theme.spacing_xs)
	id_vbox.size_flags_vertical = Control.SIZE_EXPAND_FILL
	portrait_row.add_child(id_vbox)
	
	_name_label = _factory.create_body_label("", _theme.text_primary)
	_name_label.add_theme_font_size_override("font_size", _theme.font_size_xl)
	id_vbox.add_child(_name_label)
	
	_race_label = _factory.create_muted_label("")
	id_vbox.add_child(_race_label)
	
	_level_label = _factory.create_body_label("")
	id_vbox.add_child(_level_label)
	
	_tendency_label = _factory.create_body_label("", _theme.text_accent)
	id_vbox.add_child(_tendency_label)
	
	left_col.add_child(_factory.create_separator_h())
	
	# HP条
	var hp_hbox := HBoxContainer.new()
	hp_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	left_col.add_child(hp_hbox)
	hp_hbox.add_child(_factory.create_body_label("HP:", _theme.text_secondary))
	_hp_bar = _factory.create_hp_bar(150, _theme.bar_height_lg)
	hp_hbox.add_child(_hp_bar)
	_hp_label = _factory.create_body_label("0/0")
	hp_hbox.add_child(_hp_label)
	
	# 魔力条
	var mana_hbox := HBoxContainer.new()
	mana_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	left_col.add_child(mana_hbox)
	mana_hbox.add_child(_factory.create_body_label("MP:", _theme.text_magic))
	_mana_bar = _factory.create_mana_bar(150, _theme.bar_height_md)
	mana_hbox.add_child(_mana_bar)
	_mana_label = _factory.create_body_label("0/0", _theme.text_magic)
	mana_hbox.add_child(_mana_label)
	
	# 经验条
	var xp_hbox := HBoxContainer.new()
	xp_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	left_col.add_child(xp_hbox)
	xp_hbox.add_child(_factory.create_body_label("XP:", _theme.text_secondary))
	_xp_bar = _factory.create_xp_bar(150, _theme.bar_height_sm)
	xp_hbox.add_child(_xp_bar)
	_xp_label = _factory.create_muted_label("0/300")
	xp_hbox.add_child(_xp_label)
	
	# 士气条
	_morale_bar = MoraleBar.new()
	left_col.add_child(_morale_bar)
	
	left_col.add_child(_factory.create_separator_h())
	
	# 六维属性 (3x2网格)
	var attr_section = _factory.create_title_label("属性", _theme.font_size_lg)
	left_col.add_child(attr_section)
	
	var attr_grid := GridContainer.new()
	attr_grid.columns = 2
	attr_grid.add_theme_constant_override("h_separation", _theme.spacing_lg)
	attr_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	left_col.add_child(attr_grid)
	
	_create_attr_entry(attr_grid, "str", "力量 STR")
	_create_attr_entry(attr_grid, "dex", "敏捷 DEX")
	_create_attr_entry(attr_grid, "con", "体质 CON")
	_create_attr_entry(attr_grid, "intel", "智力 INT")
	_create_attr_entry(attr_grid, "wis", "感知 WIS")
	_create_attr_entry(attr_grid, "cha", "魅力 CHA")
	
	# --- 中栏：装备槽+战斗属性 ---
	var center_col := VBoxContainer.new()
	center_col.custom_minimum_size = Vector2(300, 0)
	center_col.add_theme_constant_override("separation", _theme.spacing_md)
	body.add_child(center_col)
	
	# 装备区
	var equip_title = _factory.create_title_label("装备", _theme.font_size_lg)
	center_col.add_child(equip_title)
	
	var equip_grid := GridContainer.new()
	equip_grid.columns = 4
	equip_grid.add_theme_constant_override("h_separation", _theme.spacing_md)
	equip_grid.add_theme_constant_override("v_separation", _theme.spacing_md)
	center_col.add_child(equip_grid)
	
	# 装备槽位（对应策划案06-装备与物品.md的装备槽位总览）
	_create_equip_slot(equip_grid, "main_hand", "主武器")
	_create_equip_slot(equip_grid, "off_hand", "副武器")
	_create_equip_slot(equip_grid, "shield", "盾牌")
	_create_equip_slot(equip_grid, "mount", "坐骑")
	_create_equip_slot(equip_grid, "head", "头部")
	_create_equip_slot(equip_grid, "body", "身体")
	_create_equip_slot(equip_grid, "accessory_1", "饰品1")
	_create_equip_slot(equip_grid, "accessory_2", "饰品2")
	
	center_col.add_child(_factory.create_separator_h())
	
	# 战斗属性
	var combat_title = _factory.create_title_label("战斗属性", _theme.font_size_lg)
	center_col.add_child(combat_title)
	
	var combat_grid := GridContainer.new()
	combat_grid.columns = 2
	combat_grid.add_theme_constant_override("h_separation", _theme.spacing_xl)
	combat_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	center_col.add_child(combat_grid)
	
	# 这些将在update时填充
	var combat_stats = ["护甲(AC)", "攻击加值", "伤害范围", "移动力", "先攻", "暴击率", "射程"]
	for stat in combat_stats:
		var name_l = _factory.create_muted_label(stat)
		combat_grid.add_child(name_l)
		var val_l = _factory.create_body_label("—")
		combat_grid.add_child(val_l)
	
	# --- 右栏：特质+状态+技能 ---
	var right_col := VBoxContainer.new()
	right_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_col.add_theme_constant_override("separation", _theme.spacing_md)
	body.add_child(right_col)
	
	# 特质
	var trait_title = _factory.create_title_label("特质", _theme.font_size_lg)
	right_col.add_child(trait_title)
	
	_trait_container = VBoxContainer.new()
	_trait_container.add_theme_constant_override("separation", _theme.spacing_xs)
	right_col.add_child(_trait_container)
	
	right_col.add_child(_factory.create_separator_h())
	
	# 状态效果
	var status_title = _factory.create_title_label("状态效果", _theme.font_size_lg)
	right_col.add_child(status_title)
	
	_status_display = StatusEffectDisplay.new()
	right_col.add_child(_status_display)
	
	right_col.add_child(_factory.create_separator_h())
	
	# 主动技能列表
	var skill_title = _factory.create_title_label("主动技能", _theme.font_size_lg)
	right_col.add_child(skill_title)
	
	_skill_container = VBoxContainer.new()
	_skill_container.add_theme_constant_override("separation", _theme.spacing_xs)
	var skill_scroll = _factory.create_scroll_container(false)
	skill_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	skill_scroll.add_child(_skill_container)
	right_col.add_child(skill_scroll)

# ============================================================================
# 创建辅助
# ============================================================================

func _create_attr_entry(parent: GridContainer, id: String, display_name: String):
	var name_l = _factory.create_muted_label(display_name)
	name_l.custom_minimum_size = Vector2(100, 0)
	parent.add_child(name_l)
	
	var val_row := HBoxContainer.new()
	val_row.add_theme_constant_override("separation", _theme.spacing_sm)
	parent.add_child(val_row)
	
	var val_l = _factory.create_body_label("10")
	val_l.custom_minimum_size = Vector2(30, 0)
	val_row.add_child(val_l)
	_attr_labels[id] = val_l
	
	var mod_l = _factory.create_body_label("(+0)", _theme.text_muted)
	val_row.add_child(mod_l)
	_attr_mod_labels[id] = mod_l

func _create_equip_slot(parent: GridContainer, slot_id: String, slot_name: String):
	var slot = _factory.create_equipment_slot(slot_name, 64)
	slot.set_meta("slot_id", slot_id)
	# 点击切换/查看装备
	slot.gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT:
			equipment_slot_clicked.emit(slot_id)
	)
	parent.add_child(slot)
	_equip_slots[slot_id] = slot

# ============================================================================
# 公开接口
# ============================================================================

## 打开角色详情面板
func show_detail(unit: Unit):
	_current_unit = unit
	visible = true
	_update_all()

## 关闭
func hide_detail():
	visible = false
	_current_unit = null

## 刷新所有信息
func update_display():
	if _current_unit and is_instance_valid(_current_unit):
		_update_all()

# ============================================================================
# 内部更新
# ============================================================================

func _update_all():
	var unit = _current_unit
	if not unit or not is_instance_valid(unit) or not unit.data:
		return
	var data: UnitData = unit.data
	
	# 基本信息
	_name_label.text = data.unit_name
	_race_label.text = _get_race_text(data)
	_level_label.text = "等级 %d" % data.level
	_tendency_label.text = _get_tendency_text(data)
	
	# HP
	var max_hp = unit.get_max_hp()
	_hp_bar.max_value = max_hp
	_hp_bar.value = unit.current_hp
	_hp_label.text = "%d/%d" % [unit.current_hp, max_hp]
	_theme.apply_bar_theme(_hp_bar, _theme.get_hp_color(float(unit.current_hp) / float(max(max_hp, 1))))
	
	# 魔力
	var max_mana = max(data.current_mana, 1)
	_mana_bar.max_value = max_mana
	_mana_bar.value = data.current_mana
	_mana_label.text = "%d/%d" % [data.current_mana, max_mana]
	
	# 经验
	var max_xp = _get_xp_for_level(data.level + 1)
	_xp_bar.max_value = max_xp
	_xp_bar.value = data.xp
	_xp_label.text = "%d/%d" % [data.xp, max_xp]
	
	# 士气
	_morale_bar.update_morale(data.morale)
	
	# 六维
	_update_attr("str", data.str)
	_update_attr("dex", data.dex)
	_update_attr("con", data.con)
	_update_attr("intel", data.intel)
	_update_attr("wis", data.wis)
	_update_attr("cha", data.cha)

func _update_attr(id: String, value: int):
	if _attr_labels.has(id):
		_attr_labels[id].text = str(value)
	var mod = RPGRuleEngine.get_stat_modifier(value)
	var sign = "+" if mod >= 0 else ""
	if _attr_mod_labels.has(id):
		_attr_mod_labels[id].text = "(%s%d)" % [sign, mod]
		_attr_mod_labels[id].add_theme_color_override("font_color",
			_theme.text_positive if mod > 0 else (_theme.text_negative if mod < 0 else _theme.text_muted))

func _get_race_text(data: UnitData) -> String:
	if data.race and data.race.race_name != "":
		return data.race.race_name
	return "人类"

func _get_tendency_text(data: UnitData) -> String:
	# 根据最高属性返回倾向标签
	var attrs := {
		"力量": data.str, "敏捷": data.dex, "体质": data.con,
		"智力": data.intel, "感知": data.wis, "魅力": data.cha
	}
	var max_attr = ""
	var max_val = -999
	for key in attrs:
		if attrs[key] > max_val:
			max_val = attrs[key]
			max_attr = key
	return "倾向: %s" % max_attr

func _get_xp_for_level(level: int) -> int:
	# 对应策划案02的等级表
	var table := [0, 300, 900, 1800, 3000, 4500, 6600, 9300]
	if level - 1 < table.size():
		return table[level - 1]
	return level * 1500