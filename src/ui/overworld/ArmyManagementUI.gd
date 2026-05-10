# ArmyManagementUI.gd
# 军队管理UI — 管理兵种堆叠、装备方案、招募士兵
# 对应策划案 11-军队系统.md → 装备方案、堆叠规则、招募与维护
# 对应策划案 04-战略层系统.md → 阶段2/3的军队系统
extends PanelContainer
class_name ArmyManagementUI

# ============================================================================
# 信号
# ============================================================================
signal close_requested()
signal recruit_soldiers(equip_scheme: String, count: int)
signal change_scheme(stack_id: String, new_scheme: String)
signal dismiss_soldiers(stack_id: String, count: int)

# ============================================================================
# 内部
# ============================================================================
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

var _stack_list: VBoxContainer
var _stack_detail: RichTextLabel
var _army_summary: VBoxContainer
var _scheme_buttons: HBoxContainer

# 装备方案数据（对应策划案11-军队系统.md的装备方案表）
const EQUIP_SCHEMES := {
	"militia":      {"name": "民兵",     "weapon": "短剑",   "armor": "皮甲", "mount": "",    "shield": "",     "cost": 5,  "identity": "轻步兵"},
	"spearman":     {"name": "枪兵",     "weapon": "长枪",   "armor": "锁甲", "mount": "",    "shield": "",     "cost": 15, "identity": "反骑步兵"},
	"sword_shield": {"name": "剑盾兵",   "weapon": "长剑",   "armor": "锁甲", "mount": "",    "shield": "铁盾", "cost": 18, "identity": "防御步兵"},
	"archer":       {"name": "弓手",     "weapon": "长弓+匕首","armor":"皮甲","mount": "",    "shield": "",     "cost": 20, "identity": "远程步兵"},
	"crossbowman":  {"name": "弩手",     "weapon": "十字弩+短剑","armor":"锁甲","mount":"","shield": "",       "cost": 22, "identity": "重装远程"},
	"light_cav":    {"name": "轻骑",     "weapon": "长剑",   "armor": "皮甲", "mount": "军马","shield": "",     "cost": 25, "identity": "骑兵"},
	"lance_cav":    {"name": "枪骑",     "weapon": "长枪",   "armor": "锁甲", "mount": "军马","shield": "",     "cost": 30, "identity": "反骑骑兵"},
	"horse_archer": {"name": "骑射手",   "weapon": "短弓+匕首","armor":"皮甲","mount":"军马","shield": "",      "cost": 28, "identity": "远程骑兵"},
	"heavy_cav":    {"name": "重骑",     "weapon": "长剑",   "armor": "板甲", "mount": "战马","shield": "铁盾", "cost": 55, "identity": "重装骑兵"},
	"mage_corps":   {"name": "法师团",   "weapon": "法杖",   "armor": "皮甲", "mount": "",    "shield": "",     "cost": 30, "identity": "法术单位"},
}

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
	
	var title = _factory.create_title_label("军队管理", _theme.font_size_xxl)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	
	var army_size = _factory.create_body_label("总兵力: 0", _theme.text_accent)
	header.add_child(army_size)
	
	var upkeep = _factory.create_body_label("日薪饷: 0金", _theme.text_warning)
	header.add_child(upkeep)
	
	var close_btn = _factory.create_button("返回 (ESC)", Vector2(120, 36))
	close_btn.pressed.connect(func(): visible = false; close_requested.emit())
	header.add_child(close_btn)
	
	main_vbox.add_child(_factory.create_separator_h())
	
	# === 主体：左中右 ===
	var body := HBoxContainer.new()
	body.add_theme_constant_override("separation", _theme.spacing_lg)
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(body)
	
	# --- 左栏：军队编制 ---
	var left_panel = _factory.create_panel(Vector2(280, 0), _theme.bg_secondary, _theme.border_default)
	body.add_child(left_panel)
	
	var left_vbox := VBoxContainer.new()
	left_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	left_panel.add_child(left_vbox)
	
	var stack_title = _factory.create_title_label("军队编制", _theme.font_size_lg)
	left_vbox.add_child(stack_title)
	
	_stack_list = VBoxContainer.new()
	_stack_list.add_theme_constant_override("separation", _theme.spacing_xs)
	var stack_scroll = _factory.create_scroll_container()
	stack_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	stack_scroll.add_child(_stack_list)
	left_vbox.add_child(stack_scroll)
	
	# 招募按钮
	var recruit_btn = _factory.create_button("招募新兵", Vector2(0, 40))
	recruit_btn.pressed.connect(_on_recruit_pressed)
	left_vbox.add_child(recruit_btn)
	
	# --- 中栏：堆叠详情 ---
	var center_panel = _factory.create_panel(Vector2(300, 0), _theme.bg_secondary, _theme.border_default)
	body.add_child(center_panel)
	
	var center_vbox := VBoxContainer.new()
	center_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	center_panel.add_child(center_vbox)
	
	var detail_title = _factory.create_title_label("单位详情", _theme.font_size_lg)
	center_vbox.add_child(detail_title)
	
	_stack_detail = _factory.create_rich_text(Vector2(0, 0))
	_stack_detail.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_stack_detail.text = "选择一个堆叠查看详情"
	center_vbox.add_child(_stack_detail)
	
	# 换装区域
	var scheme_title = _factory.create_body_label("更换装备方案:", _theme.text_secondary)
	center_vbox.add_child(scheme_title)
	
	_scheme_buttons = HBoxContainer.new()
	_scheme_buttons.add_theme_constant_override("separation", _theme.spacing_xs)
	center_vbox.add_child(_scheme_buttons)
	
	# 填充方案按钮
	_populate_scheme_buttons()
	
	# 解散按钮
	var dismiss_btn = _factory.create_button("解雇士兵", Vector2(0, 36))
	dismiss_btn.add_theme_color_override("font_color", _theme.text_negative)
	center_vbox.add_child(dismiss_btn)
	
	# --- 右栏：军队总览 ---
	var right_panel = _factory.create_panel(Vector2(240, 0), _theme.bg_secondary, _theme.border_default)
	right_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_child(right_panel)
	
	var right_vbox := VBoxContainer.new()
	right_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	right_panel.add_child(right_vbox)
	
	var summary_title = _factory.create_title_label("军队总览", _theme.font_size_lg)
	right_vbox.add_child(summary_title)
	
	_army_summary = VBoxContainer.new()
	_army_summary.add_theme_constant_override("separation", _theme.spacing_sm)
	right_vbox.add_child(_army_summary)
	
	# 克制关系速查
	right_vbox.add_child(_factory.create_separator_h())
	var counter_title = _factory.create_title_label("克制速查", _theme.font_size_md)
	right_vbox.add_child(counter_title)
	
	var counter_text := ""
	counter_text += "[color=red]长枪 → 克制冲锋[/color]\n"
	counter_text += "[color=green]坐骑 → 冲锋加成[/color]\n"
	counter_text += "[color=blue]远程 → 克制无盾[/color]\n"
	counter_text += "[color=yellow]重甲 → 克制远程[/color]\n"
	counter_text += "[color=purple]范围法术 → 克制密集[/color]\n"
	counter_text += "[color=cyan]高速度 → 克制法师[/color]\n"
	counter_text += "[color=orange]巨斧/弩 → 破甲[/color]\n"
	
	var counter_label = _factory.create_rich_text(Vector2(0, 0))
	counter_label.text = counter_text
	counter_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_vbox.add_child(counter_label)

func _populate_scheme_buttons():
	for scheme_id in EQUIP_SCHEMES:
		var scheme: Dictionary = EQUIP_SCHEMES[scheme_id]
		var btn = _factory.create_icon_button(scheme.name.left(2), scheme.name)
		btn.set_meta("scheme_id", scheme_id)
		_scheme_buttons.add_child(btn)

func _on_recruit_pressed():
	# 弹出招募选择 — 由外部连接信号处理
	pass

# ============================================================================
# 公开接口
# ============================================================================

## 打开军队管理界面
func open_army():
	visible = true
	_refresh_stack_list()

## 关闭
func close_army():
	visible = false

## 刷新堆叠列表
func _refresh_stack_list():
	for child in _stack_list.get_children():
		child.queue_free()
	# TODO: 从实际军队数据填充
	_add_stack_entry("枪兵 × 24", "反骑步兵", 24)
	_add_stack_entry("弓手 × 18", "远程步兵", 18)

func _add_stack_entry(name: String, identity: String, count: int):
	var entry = _factory.create_card(Vector2(0, 50))
	var vbox := VBoxContainer.new()
	entry.add_child(vbox)
	
	var name_l = _factory.create_body_label(name, _theme.text_primary)
	vbox.add_child(name_l)
	
	var id_l = _factory.create_muted_label(identity + " | %d人" % count)
	vbox.add_child(id_l)
	
	_stack_list.add_child(entry)