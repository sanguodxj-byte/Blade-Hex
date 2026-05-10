# TownUI.gd
# 城镇界面系统 — 包含酒馆（招募）、商店（买卖）、休息（恢复）三个子面板
# 对应策划案 04-战略层系统.md → 城镇界面
# 对应策划案 12-种族与招募.md → 招募系统
# 对应策划案 06-装备与物品.md → 商店
extends PanelContainer
class_name TownUI

# ============================================================================
# 信号
# ============================================================================
signal close_requested()
signal recruit_clicked(hero_data: Dictionary)
signal buy_clicked(item_id: String, quantity: int)
signal sell_clicked(item_id: String, quantity: int)
signal rest_clicked(type: String)  # "short" / "long"

# ============================================================================
# Tab枚举
# ============================================================================
enum TownTab {
	TAVERN,
	SHOP,
	REST,
}

# ============================================================================
# 内部
# ============================================================================
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

var _tab_buttons: Dictionary = {}   # TownTab → Button
var _tab_container: PanelContainer
var _content_area: VBoxContainer
var _current_tab: TownTab = TownTab.TAVERN
var _town_name_label: Label

# 酒馆组件
var _tavern_list: VBoxContainer
var _tavern_detail: RichTextLabel
var _recruit_btn: Button

# 商店组件
var _shop_grid: GridContainer
var _shop_detail: RichTextLabel
var _buy_btn: Button
var _sell_grid: GridContainer

# 休息组件
var _short_rest_btn: Button
var _long_rest_btn: Button

# 城镇数据
var _town_data: Dictionary = {}

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
	
	_town_name_label = _factory.create_title_label("城镇", _theme.font_size_xxl)
	_town_name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_town_name_label)
	
	var gold_lbl = _factory.create_body_label("金币: 0", _theme.text_accent)
	gold_lbl.set_meta("stat_key", "gold")
	header.add_child(gold_lbl)
	
	var close_btn = _factory.create_button("离开城镇 (ESC)", Vector2(140, 36))
	close_btn.pressed.connect(func(): visible = false; close_requested.emit())
	header.add_child(close_btn)
	
	main_vbox.add_child(_factory.create_separator_h())
	
	# === Tab栏 ===
	var tab_bar := HBoxContainer.new()
	tab_bar.add_theme_constant_override("separation", _theme.spacing_sm)
	main_vbox.add_child(tab_bar)
	
	_create_tab_button(tab_bar, TownTab.TAVERN, "酒馆 (招募)")
	_create_tab_button(tab_bar, TownTab.SHOP, "商店 (买卖)")
	_create_tab_button(tab_bar, TownTab.REST, "休息 (恢复)")
	
	main_vbox.add_child(_factory.create_separator_h())
	
	# === 内容区 ===
	_tab_container = _factory.create_panel(Vector2.ZERO, _theme.bg_secondary, _theme.border_default)
	_tab_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(_tab_container)
	
	var content_margin = _factory.create_margin(16, 16, 12, 12)
	_tab_container.add_child(content_margin)
	
	_content_area = VBoxContainer.new()
	_content_area.add_theme_constant_override("separation", _theme.spacing_md)
	content_margin.add_child(_content_area)
	
	# 初始化所有子面板内容
	_setup_tavern()
	_setup_shop()
	_setup_rest()
	
	# 默认显示酒馆
	_switch_tab(TownTab.TAVERN)

# ============================================================================
# Tab
# ============================================================================

func _create_tab_button(parent: Control, tab: TownTab, text: String):
	var btn = _factory.create_button(text, Vector2(0, 36))
	btn.pressed.connect(_switch_tab.bind(tab))
	parent.add_child(btn)
	_tab_buttons[tab] = btn

func _switch_tab(tab: TownTab):
	_current_tab = tab
	
	# 更新按钮高亮
	for t in _tab_buttons:
		var btn: Button = _tab_buttons[t]
		var is_active = t == tab
		btn.modulate = Color(1, 1, 1, 1) if is_active else Color(0.6, 0.6, 0.6, 0.7)
	
	# 清除内容
	for child in _content_area.get_children():
		child.visible = false
	
	# 显示对应内容
	match tab:
		TownTab.TAVERN:
			_show_tavern()
		TownTab.SHOP:
			_show_shop()
		TownTab.REST:
			_show_rest()

# ============================================================================
# 酒馆（招募）
# ============================================================================

func _setup_tavern():
	var tavern_hbox := HBoxContainer.new()
	tavern_hbox.add_theme_constant_override("separation", _theme.spacing_lg)
	tavern_hbox.name = "TavernContent"
	_content_area.add_child(tavern_hbox)
	
	# 左侧：可招募英雄列表
	var left_panel = _factory.create_panel(Vector2(280, 0), _theme.bg_card, _theme.border_default)
	tavern_hbox.add_child(left_panel)
	
	var left_vbox := VBoxContainer.new()
	left_vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	left_panel.add_child(left_vbox)
	
	var title = _factory.create_title_label("可招募英雄", _theme.font_size_lg)
	left_vbox.add_child(title)
	
	_tavern_list = VBoxContainer.new()
	_tavern_list.add_theme_constant_override("separation", _theme.spacing_xs)
	var scroll = _factory.create_scroll_container()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.add_child(_tavern_list)
	left_vbox.add_child(scroll)
	
	# 右侧：英雄详情
	var right_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_card, _theme.border_default)
	right_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tavern_hbox.add_child(right_panel)
	
	var right_vbox := VBoxContainer.new()
	right_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	right_panel.add_child(right_vbox)
	
	_tavern_detail = _factory.create_rich_text(Vector2(0, 0))
	_tavern_detail.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_tavern_detail.text = "选择一位英雄查看详情"
	right_vbox.add_child(_tavern_detail)
	
	_recruit_btn = _factory.create_button("招募 (0金)", Vector2(0, 40))
	_recruit_btn.disabled = true
	_recruit_btn.pressed.connect(_on_recruit_clicked)
	right_vbox.add_child(_recruit_btn)

func _show_tavern():
	var tavern = _content_area.get_node_or_null("TavernContent")
	if tavern:
		tavern.visible = true

func _on_recruit_clicked():
	# 由外部连接信号处理
	pass

# ============================================================================
# 商店
# ============================================================================

func _setup_shop():
	var shop_hbox := HBoxContainer.new()
	shop_hbox.add_theme_constant_override("separation", _theme.spacing_lg)
	shop_hbox.name = "ShopContent"
	_content_area.add_child(shop_hbox)
	
	# 左侧：出售商品
	var left_panel = _factory.create_panel(Vector2(320, 0), _theme.bg_card, _theme.border_default)
	shop_hbox.add_child(left_panel)
	
	var left_vbox := VBoxContainer.new()
	left_vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	left_panel.add_child(left_vbox)
	
	var buy_title = _factory.create_title_label("购买", _theme.font_size_lg)
	left_vbox.add_child(buy_title)
	
	_shop_grid = GridContainer.new()
	_shop_grid.columns = 5
	_shop_grid.add_theme_constant_override("h_separation", _theme.spacing_sm)
	_shop_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	var scroll = _factory.create_scroll_container()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.add_child(_shop_grid)
	left_vbox.add_child(scroll)
	
	# 右侧：详情+出售背包
	var right_vbox := VBoxContainer.new()
	right_vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right_vbox.add_theme_constant_override("separation", _theme.spacing_md)
	shop_hbox.add_child(right_vbox)
	
	_shop_detail = _factory.create_rich_text(Vector2(0, 0))
	_shop_detail.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_shop_detail.text = "选择商品查看详情"
	right_vbox.add_child(_shop_detail)
	
	_buy_btn = _factory.create_button("购买", Vector2(0, 36))
	_buy_btn.disabled = true
	right_vbox.add_child(_buy_btn)
	
	right_vbox.add_child(_factory.create_separator_h())
	
	var sell_title = _factory.create_title_label("出售背包物品", _theme.font_size_md)
	right_vbox.add_child(sell_title)
	
	_sell_grid = GridContainer.new()
	_sell_grid.columns = 6
	_sell_grid.add_theme_constant_override("h_separation", _theme.spacing_sm)
	_sell_grid.add_theme_constant_override("v_separation", _theme.spacing_sm)
	right_vbox.add_child(_sell_grid)

func _show_shop():
	var shop = _content_area.get_node_or_null("ShopContent")
	if shop:
		shop.visible = true

# ============================================================================
# 休息
# ============================================================================

func _setup_rest():
	var rest_vbox := VBoxContainer.new()
	rest_vbox.name = "RestContent"
	rest_vbox.add_theme_constant_override("separation", _theme.spacing_xl)
	_content_area.add_child(rest_vbox)
	
	var rest_title = _factory.create_title_label("营地休息", _theme.font_size_xxl)
	rest_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	rest_vbox.add_child(rest_title)
	
	# 短休息
	var short_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_card, _theme.border_friendly)
	rest_vbox.add_child(short_panel)
	
	var short_vbox := VBoxContainer.new()
	short_vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	short_panel.add_child(short_vbox)
	
	var short_title = _factory.create_title_label("短休息", _theme.font_size_lg)
	short_vbox.add_child(short_title)
	
	var short_desc = _factory.create_body_label("恢复50%魔力，不恢复HP。免费，但仅限每冒险1次。", _theme.text_secondary)
	short_vbox.add_child(short_desc)
	
	_short_rest_btn = _factory.create_button("短休息 (免费)", Vector2(0, 40))
	_short_rest_btn.pressed.connect(func(): rest_clicked.emit("short"))
	short_vbox.add_child(_short_rest_btn)
	
	# 长休息
	var long_panel = _factory.create_panel(Vector2.ZERO, _theme.bg_card, _theme.border_highlight)
	rest_vbox.add_child(long_panel)
	
	var long_vbox := VBoxContainer.new()
	long_vbox.add_theme_constant_override("separation", _theme.spacing_sm)
	long_panel.add_child(long_vbox)
	
	var long_title = _factory.create_title_label("长休息", _theme.font_size_lg)
	long_vbox.add_child(long_title)
	
	var long_desc = _factory.create_body_label("恢复100% HP和魔力，重置所有冷却。消耗1天补给。", _theme.text_secondary)
	long_vbox.add_child(long_desc)
	
	_long_rest_btn = _factory.create_button("长休息 (消耗补给)", Vector2(0, 40))
	_long_rest_btn.pressed.connect(func(): rest_clicked.emit("long"))
	long_vbox.add_child(_long_rest_btn)

func _show_rest():
	var rest = _content_area.get_node_or_null("RestContent")
	if rest:
		rest.visible = true

# ============================================================================
# 公开接口
# ============================================================================

## 打开城镇界面
func open_town(town_name: String, town_data: Dictionary = {}):
	_town_data = _town_data
	_town_name_label.text = town_name
	visible = true
	_switch_tab(TownTab.TAVERN)
	# TODO: 填充酒馆英雄列表、商店物品列表等

## 关闭
func close_town():
	visible = false