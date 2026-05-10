# TerrainTooltip.gd
# 地形信息提示 — 悬停六边形格子时显示地形名、移动消耗、防御加值等
# 对应策划案 09-UI设计.md → 六边形网格 → 地形信息tooltip
# 对应策划案 03-战术战斗系统 → 二、地形系统
extends PanelContainer
class_name TerrainTooltip

# ============================================================================
# 内部组件
# ============================================================================
var _terrain_label: Label
var _move_cost_label: Label
var _defense_label: Label
var _cover_label: Label
var _elevation_label: Label
var _special_label: RichTextLabel
var _coord_label: Label

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory: UIFactory

func _ready():
	_factory = UIFactory.new()
	_setup()
	visible = false

func _setup():
	add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_tooltip, _theme.border_highlight, 1, _theme.radius_md, _theme.spacing_md))
	z_index = 100
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	
	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_xs)
	add_child(vbox)
	
	# 地形名
	_terrain_label = _factory.create_body_label("", _theme.text_accent)
	_terrain_label.add_theme_font_size_override("font_size", _theme.font_size_lg)
	vbox.add_child(_terrain_label)
	
	vbox.add_child(_factory.create_separator_h(_theme.border_default))
	
	# 移动消耗
	_move_cost_label = _factory.create_body_label("")
	vbox.add_child(_move_cost_label)
	
	# 防御加成
	_defense_label = _factory.create_body_label("")
	vbox.add_child(_defense_label)
	
	# 掩护等级
	_cover_label = _factory.create_body_label("")
	vbox.add_child(_cover_label)
	
	# 高程
	_elevation_label = _factory.create_body_label("")
	vbox.add_child(_elevation_label)
	
	# 特殊效果
	_special_label = _factory.create_rich_text(Vector2(180, 0))
	vbox.add_child(_special_label)
	
	# 坐标
	_coord_label = _factory.create_muted_label("")
	vbox.add_child(_coord_label)

# ============================================================================
# 公开接口
# ============================================================================

## 地形类型定义（对应策划案03的地形表）
const TERRAIN_INFO := {
	"plains":    {"name": "平地",     "move": 1, "defense": 0,  "cover": "无",   "elev": "平地", "special": ""},
	"grass":     {"name": "草地",     "move": 1, "defense": 0,  "cover": "无",   "elev": "平地", "special": ""},
	"savanna":   {"name": "稀树草原", "move": 1, "defense": 1,  "cover": "无",   "elev": "平地", "special": ""},
	"forest":    {"name": "森林",     "move": 2, "defense": 2,  "cover": "半掩体", "elev": "平地", "special": "潜行加成 / 阻挡穿越视线"},
	"deep_forest":{"name": "密林",    "move": 3, "defense": 3,  "cover": "全掩体", "elev": "平地", "special": "潜行大幅加成 / 阻挡全部视线"},
	"hills":     {"name": "丘陵",     "move": 2, "defense": 2,  "cover": "半掩体", "elev": "高地", "special": "高地优势 / 可越过低矮障碍"},
	"mountain":  {"name": "山地",     "move": 3, "defense": 3,  "cover": "全掩体", "elev": "高地", "special": "高处视野+2 / 不可骑乘"},
	"shallow_water":{"name": "浅水",  "move": 2, "defense": -1, "cover": "无",   "elev": "低地", "special": "火抗+2 / 冰雷弱点"},
	"deep_water":{"name": "深水",     "move": 3, "defense": -2, "cover": "无",   "elev": "低地", "special": "需游泳 / 施法劣势"},
	"swamp":     {"name": "沼泽",     "move": 2, "defense": -1, "cover": "无",   "elev": "低地", "special": "强韧豁免DC12 / 失败中毒"},
	"road":      {"name": "道路",     "move": 1, "defense": 0,  "cover": "无",   "elev": "平地", "special": "移动消耗减半"},
	"sand":      {"name": "沙地",     "move": 2, "defense": 0,  "cover": "无",   "elev": "平地", "special": "冲锋失效"},
	"snow":      {"name": "雪地",     "move": 2, "defense": 0,  "cover": "无",   "elev": "平地", "special": "每回合移动-1格"},
	"wall":      {"name": "墙壁",     "move": -1,"defense": 0,  "cover": "全掩体", "elev": "平地", "special": "不可通过 / 可被攻城器械破坏"},
	"ruins":     {"name": "建筑废墟", "move": 2, "defense": 2,  "cover": "半掩体", "elev": "平地", "special": "可被破坏变平地"},
	"poison_mush":{"name": "毒菇群",  "move": 1, "defense": 0,  "cover": "无",   "elev": "平地", "special": "站上去中毒2回合"},
	"lucky_grass":{"name": "幸运草丛", "move": 1, "defense": 0,  "cover": "无",   "elev": "平地", "special": "暴击率+10%(1次攻击)"},
}

## 显示地形信息
func show_terrain_info(global_pos: Vector2, terrain_type: String, 
		coord: Vector2i = Vector2i(-1, -1), occupant_name: String = "",
		cover_override: int = -1, elevation_override: int = -999):
	visible = true
	
	var info = TERRAIN_INFO.get(terrain_type, {"name": terrain_type, "move": 1, "defense": 0, "cover": "无", "elev": "平地", "special": ""})
	
	_terrain_label.text = info.name
	
	# 移动消耗
	if info.move < 0:
		_move_cost_label.text = "移动: 不可通过"
		_move_cost_label.add_theme_color_override("font_color", _theme.text_negative)
	else:
		_move_cost_label.text = "移动消耗: %d" % info.move
		_move_cost_label.add_theme_color_override("font_color",
			_theme.text_negative if info.move >= 3 else _theme.text_primary)
	
	# 防御加成
	if info.defense > 0:
		_defense_label.text = "防御加成: +%d AC" % info.defense
		_defense_label.add_theme_color_override("font_color", _theme.text_positive)
	elif info.defense < 0:
		_defense_label.text = "防御惩罚: %d AC" % info.defense
		_defense_label.add_theme_color_override("font_color", _theme.text_negative)
	else:
		_defense_label.text = "防御加成: —"
		_defense_label.add_theme_color_override("font_color", _theme.text_muted)
	
	# 掩护
	_cover_label.text = "掩护: %s" % info.cover
	match info.cover:
		"全掩体": _cover_label.add_theme_color_override("font_color", _theme.text_positive)
		"半掩体": _cover_label.add_theme_color_override("font_color", _theme.text_warning)
		_: _cover_label.add_theme_color_override("font_color", _theme.text_muted)
	
	# 高程
	_elevation_label.text = "高程: %s" % info.elev
	match info.elev:
		"高地": _elevation_label.add_theme_color_override("font_color", _theme.text_positive)
		"低地": _elevation_label.add_theme_color_override("font_color", _theme.text_negative)
		_: _elevation_label.add_theme_color_override("font_color", _theme.text_muted)
	
	# 特殊效果
	if info.special != "":
		_special_label.text = "[color=%s]%s[/color]" % [_theme.text_accent.to_html(false), info.special]
		_special_label.visible = true
	else:
		_special_label.visible = false
	
	# 占据者
	if occupant_name != "":
		_special_label.text += "\n[color=cyan]占据: %s[/color]" % occupant_name
	
	# 坐标
	if coord.x >= 0:
		_coord_label.text = "(%d, %d)" % [coord.x, coord.y]
	else:
		_coord_label.text = ""
	
	# 定位
	position = global_pos + Vector2(15, 15)
	# 边界修正
	await get_tree().process_frame
	var vp_size = get_viewport().get_visible_rect().size
	if position.x + size.x > vp_size.x:
		position.x = global_pos.x - size.x - 10
	if position.y + size.y > vp_size.y:
		position.y = global_pos.y - size.y - 10

## 隐藏
func hide_tooltip():
	visible = false