# TerritoryUI.gd
# 领地管理UI — 管理城堡、税收、建设、势力关系
# 对应策划案 04-战略层系统.md → 领地管理
# 对应策划案 11-军队系统.md → 招募与维护
# 对应策划案 05 Phase5 → 领地经营
extends PanelContainer
class_name TerritoryUI

# ============================================================================
# 信号
# ============================================================================
signal close_requested()
signal build_clicked(building_id: String)
signal upgrade_castle_clicked()
signal tax_changed(rate: float)

# ============================================================================
# 城堡等级（对应策划案04-战略层系统.md）
# ============================================================================
enum CastleLevel {
	WOOD_FENCE,    ## 木栅村庄 — 木墙、拒马，守军50人
	STONE_FORT,    ## 石堡 — 石墙、箭塔，守军150人
	CITADEL,       ## 要塞 — 高墙、双塔、护城河，守军300人
}

const CASTLE_INFO := {
	CastleLevel.WOOD_FENCE: {"name": "木栅村庄", "defense": "木墙、拒马", "garrison": 50, "upgrade_cost": 500},
	CastleLevel.STONE_FORT: {"name": "石堡", "defense": "石墙、箭塔", "garrison": 150, "upgrade_cost": 2000},
	CastleLevel.CITADEL: {"name": "要塞", "defense": "高墙、双塔、护城河", "garrison": 300, "upgrade_cost": -1},
}

# ============================================================================
# 建筑类型
# ============================================================================
const BUILDINGS := {
	"training_ground": {"name": "训练场", "desc": "提升新兵质量", "cost": 300, "level": 1},
	"magic_tower":    {"name": "法塔",   "desc": "法师招募和法术研究", "cost": 800, "level": 2},
	"trade_route":    {"name": "商路",   "desc": "增加收入", "cost": 400, "level": 1},
	"iron_mine":      {"name": "铁矿",   "desc": "提供铁资源", "cost": 350, "level": 1},
	"gold_mine":      {"name": "金矿",   "desc": "提供金资源", "cost": 600, "level": 2},
	"barracks":       {"name": "兵营",   "desc": "招募士兵", "cost": 250, "level": 1},
	"smithy":         {"name": "铁匠铺", "desc": "打造装备", "cost": 200, "level": 1},
	"tavern":         {"name": "酒馆",   "desc": "招募英雄", "cost": 150, "level": 1},
	"wall_repair":    {"name": "城墙修复","desc": "修复战斗损毁", "cost": 100, "level": 1},
}

# ============================================================================
# 内部
# ============================================================================
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

var _castle_level: CastleLevel = CastleLevel.WOOD_FENCE
var _territory_name_label: Label
var _castle_info_label: RichTextLabel
var _upgrade_btn: Button
var _income_labels: Dictionary = {}
var _expense_labels: Dictionary = {}
var _building_grid: GridContainer
var _faction_container: VBoxContainer

func _ready():
	_factory = UIFactory.new()
	_setup()
	visible = false

func _setup():
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_primary, _theme.border_highlight, 3, _theme.radius_lg, 0))
	
	var root_margin = _factory.create_margin(40, 40, 30, 30)
	add_child(root_margin)
	
	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	root_margin.add_child(main_vbox)
	
	# === 顶部 ===
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation", _theme.spacing_md)
	main_vbox.add_child(header)
	
	_territory_name_label = _factory.create_title_label("领地管理", _theme.font_size_xxl)
	_territory_name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_territory_name_label)
	
	var close_btn = _factory.create_button("返回 (ESC)", Vector2(120, 36))
	close_btn.pressed.connect(func(): visible = false; close_requested.emit())
	header.add_child(close_btn)
	
	main_vbox.add_child(_factory.create_separator_h())
	
	# === 主体：三栏 ===
	var body := HBoxContainer.new()
	body.add_theme_constant_override("separation", _theme.spacing_lg)
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(body)
	
	# --- 左栏：城堡+收入/支出 ---
	var left_col := VBoxContainer.new()
	left_col.custom_minimum_size = Vector2(280, 0)
	left_col.add_theme_constant_override("separation", _theme.spacing_md)
	body.add_child(left_col)
	
	# 城堡信息
	var castle_title = _factory.create_title_label("城堡", _theme.font_size_lg)
	left_col.add_child(castle_title)
	
	_castle_info_label = _factory.create_rich_text(Vector2(0, 0))
	left_col.add_child(_castle_info_label)
	
	_upgrade_btn = _factory.create_button("升级城堡 (500金)", Vector2(0, 36))
	_upgrade_btn.pressed.connect(func(): upgrade_castle_clicked.emit())
	left_col.add_child(_upgrade_btn)
	
	left_col.add_child(_factory.create_separator_h())
	
	# 收入
	var income_title = _factory.create_title_label("收入", _theme.font_size_md)
	left_col.add_child(income_title)
	
	_create_income_entry(left_col, "village_tax", "村庄税收", "+0金/天")
	_create_income_entry(left_col, "trade_route", "商路关税", "+0金/天")
	_create_income_entry(left_col, "mine_output", "矿场产出", "+0金/天")
	_create_income_entry(left_col, "quest_income", "雇佣委托", "+0金/天")
	
	var total_income = _factory.create_body_label("总收入: +0金/天", _theme.text_positive)
	total_income.set_meta("stat_key", "total_income")
	left_col.add_child(total_income)
	_income_labels["total_income"] = total_income
	
	left_col.add_child(_factory.create_separator_h())
	
	# 支出
	var expense_title = _factory.create_title_label("支出", _theme.font_size_md)
	left_col.add_child(expense_title)
	
	_create_expense_entry(left_col, "army_pay", "军队薪饷", "-0金/天")
	_create_expense_entry(left_col, "supply", "补给消耗", "-0金/天")
	_create_expense_entry(left_col, "castle_maint", "城堡维护", "-0金/天")
	_create_expense_entry(left_col, "hero_salary", "英雄薪资", "-0金/天")
	
	var total_expense = _factory.create_body_label("总支出: -0金/天", _theme.text_negative)
	total_expense.set_meta("stat_key", "total_expense")
	left_col.add_child(total_expense)
	_expense_labels["total_expense"] = total_expense
	
	# --- 中栏：建设 ---
	var center_col := VBoxContainer.new()
	center_col.custom_minimum_size = Vector2(300, 0)
	center_col.add_theme_constant_override("separation", _theme.spacing_md)
	body.add_child(center_col)
	
	var build_title = _factory.create_title_label("领地建设", _theme.font_size_lg)
	center_col.add_child(build_title)
	
	_building_grid = GridContainer.new()
	_building_grid.columns = 3
	_building_grid.add_theme_constant_override("h_separation", _theme.spacing_md)
	_building_grid.add_theme_constant_override("v_separation", _theme.spacing_md)
	center_col.add_child(_building_grid)
	
	# 填充建筑按钮
	_populate_buildings()
	
	# --- 右栏：势力关系 ---
	var right_col := VBoxContainer.new()
	right_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_col.add_theme_constant_override("separation", _theme.spacing_md)
	body.add_child(right_col)
	
	var faction_title = _factory.create_title_label("势力关系", _theme.font_size_lg)
	right_col.add_child(faction_title)
	
	_faction_container = VBoxContainer.new()
	_faction_container.add_theme_constant_override("separation", _theme.spacing_sm)
	right_col.add_child(_faction_container)
	
	# 初始填充势力
	_add_faction_entry("中央王国", "中立", Color.GRAY)
	_add_faction_entry("银叶精灵", "友好", Color.GREEN)
	_add_faction_entry("霜冠矮人", "冷淡", Color.YELLOW)
	_add_faction_entry("暗影教团", "敌对", Color.RED)
	_add_faction_entry("焦土兽人", "交战", Color.RED)
	
	right_col.add_child(_factory.create_separator_h())
	
	# 阶段信息
	var phase_title = _factory.create_title_label("当前阶段", _theme.font_size_md)
	right_col.add_child(phase_title)
	
	var phase_info = _factory.create_rich_text(Vector2(0, 0))
	phase_info.text = "[color=yellow]雇佣兵团长[/color]\n\n"
	phase_info.text += "[color=gray]晋升条件:[/color]\n"
	phase_info.text += "• 骑士：声望达标 + 完成主线委托\n"
	phase_info.text += "• 领主：占领城堡 + 高声望\n\n"
	phase_info.text += "[color=gray]当前功能:[/color]\n"
	phase_info.text += "小队RPG战斗 / 酒馆招募 / 委托"
	phase_info.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_col.add_child(phase_info)

# ============================================================================
# 辅助创建
# ============================================================================

func _create_income_entry(parent: Control, key: String, name: String, default: String):
	var pair = _factory.create_stat_pair(name, default, _theme.text_secondary, _theme.text_positive)
	parent.add_child(pair)
	_income_labels[key] = pair

func _create_expense_entry(parent: Control, key: String, name: String, default: String):
	var pair = _factory.create_stat_pair(name, default, _theme.text_secondary, _theme.text_negative)
	parent.add_child(pair)
	_expense_labels[key] = pair

func _populate_buildings():
	for build_id in BUILDINGS:
		var build_info: Dictionary = BUILDINGS[build_id]
		var btn = _factory.create_button("%s\n(%d金)" % [build_info.name, build_info.cost], Vector2(90, 64))
		btn.tooltip_text = "%s\n%s\n需求: 领地等级%d" % [build_info.name, build_info.desc, build_info.level]
		btn.set_meta("build_id", build_id)
		btn.pressed.connect(func(): build_clicked.emit(build_id))
		_building_grid.add_child(btn)

func _add_faction_entry(faction_name: String, relation: String, color: Color):
	var entry := HBoxContainer.new()
	entry.add_theme_constant_override("separation", _theme.spacing_md)
	_faction_container.add_child(entry)
	
	var name_l = _factory.create_body_label(faction_name)
	name_l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	entry.add_child(name_l)
	
	var rel_l = _factory.create_body_label(relation, color)
	entry.add_child(rel_l)

# ============================================================================
# 公开接口
# ============================================================================

## 打开领地管理
func open_territory(territory_name: String = "我的领地", castle_level: CastleLevel = CastleLevel.WOOD_FENCE):
	_territory_name_label.text = territory_name
	_castle_level = castle_level
	visible = true
	_update_castle_info()

## 关闭
func close_territory():
	visible = false

func _update_castle_info():
	var info = CASTLE_INFO.get(_castle_level, {})
	_castle_info_label.text = "[b]%s[/b]\n" % info.get("name", "未知")
	_castle_info_label.text += "[color=gray]防御:[/color] %s\n" % info.get("defense", "")
	_castle_info_label.text += "[color=gray]守军上限:[/color] %d人\n" % info.get("garrison", 0)
	
	var upgrade_cost = info.get("upgrade_cost", -1)
	if upgrade_cost > 0:
		_upgrade_btn.text = "升级城堡 (%d金)" % upgrade_cost
		_upgrade_btn.disabled = false
	else:
		_upgrade_btn.text = "已达最高等级"
		_upgrade_btn.disabled = true