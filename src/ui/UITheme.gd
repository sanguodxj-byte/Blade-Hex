# UITheme.gd
# UI主题系统 — 集中管理所有设计令牌（颜色、字号、间距、圆角等）
# 支持未来切换为图像UI：所有视觉属性通过此单例访问，替换时只需修改此处
# 对应策划案 09-UI设计.md 的设计原则：信息层级清晰、反馈即时、不暴露骰子
extends Node
class_name UITheme

# ============================================================================
# 单例模式
# ============================================================================
static var _instance: UITheme = null
static func get_instance() -> UITheme:
	if _instance == null or not is_instance_valid(_instance):
		_instance = UITheme.new()
		# 阻止 GC 回收：引用到 Engine 的 meta 中
		if Engine.get_main_loop():
			Engine.get_main_loop().root.call_deferred("add_child", _instance)
	return _instance

# ============================================================================
# 主题模式（预留：未来可切换明暗/图像主题）
# ============================================================================
enum ThemeMode {
	PROCEDURAL_DARK,   ## 当前：程序化深色主题
	IMAGE_BASED,       ## 未来：图像资源主题
}

var current_mode: ThemeMode = ThemeMode.PROCEDURAL_DARK

# ============================================================================
# 调色板 — 核心色彩系统
# ============================================================================

## 背景色
var bg_primary: Color         = Color(0.08, 0.08, 0.10, 0.95)
var bg_secondary: Color      = Color(0.12, 0.12, 0.14, 0.92)
var bg_tertiary: Color       = Color(0.06, 0.06, 0.08, 0.90)
var bg_panel: Color           = Color(0.10, 0.10, 0.12, 0.95)
var bg_card: Color            = Color(0.15, 0.14, 0.18, 0.85)
var bg_card_hover: Color     = Color(0.25, 0.22, 0.30, 0.90)
var bg_overlay: Color        = Color(0, 0, 0, 0.6)
var bg_tooltip: Color        = Color(0.06, 0.05, 0.09, 0.95)

## 边框色
var border_default: Color     = Color(0.3, 0.3, 0.35, 0.6)
var border_highlight: Color  = Color(0.5, 0.45, 0.3, 0.8)
var border_friendly: Color   = Color(0.2, 0.5, 0.8, 0.8)
var border_enemy: Color      = Color(0.6, 0.2, 0.2, 0.8)
var border_magic: Color      = Color(0.4, 0.35, 0.6, 0.8)

## 文字色
var text_primary: Color      = Color(0.95, 0.93, 0.88)
var text_secondary: Color    = Color(0.7, 0.68, 0.63)
var text_muted: Color        = Color(0.5, 0.48, 0.45)
var text_accent: Color       = Color(0.9, 0.8, 0.5)      # 金色强调
var text_positive: Color     = Color(0.3, 0.85, 0.3)
var text_negative: Color     = Color(0.9, 0.3, 0.25)
var text_magic: Color        = Color(0.7, 0.6, 1.0)
var text_warning: Color      = Color(0.9, 0.7, 0.2)

## HP 条颜色
var hp_high: Color           = Color(0.2, 0.75, 0.2)
var hp_mid: Color            = Color(0.85, 0.75, 0.1)
var hp_low: Color            = Color(0.9, 0.15, 0.1)
var hp_bar_bg: Color         = Color(0.15, 0.08, 0.08, 0.7)

## 魔力条颜色
var mana_fill: Color         = Color(0.3, 0.5, 1.0)
var mana_bg: Color           = Color(0.1, 0.1, 0.2, 0.7)

## 士气颜色
var morale_high: Color       = Color(0.2, 0.8, 0.9)
var morale_normal: Color     = Color(0.6, 0.6, 0.6)
var morale_low: Color        = Color(0.9, 0.7, 0.1)
var morale_broken: Color     = Color(0.9, 0.2, 0.1)
var morale_routing: Color    = Color(1.0, 0.1, 0.1)

## 经验条
var xp_fill: Color           = Color(0.6, 0.5, 0.9)
var xp_bg: Color             = Color(0.1, 0.08, 0.15, 0.7)

## 高亮色（六边形网格）
var highlight_move: Color    = Color(0.2, 0.6, 1.0, 0.4)    # 蓝色-移动范围
var highlight_attack: Color  = Color(1.0, 0.2, 0.2, 0.4)   # 红色-攻击范围
var highlight_spell: Color   = Color(1.0, 0.5, 0.0, 0.4)   # 橙色-法术范围
var highlight_select: Color  = Color(1.0, 0.9, 0.2, 0.5)    # 黄色-选中
var highlight_aoe: Color     = Color(0.9, 0.3, 0.9, 0.35)   # 紫色-AOE
var highlight_friendly: Color = Color(0.2, 0.8, 0.4, 0.3)   # 绿色-友方范围

## 稀有度颜色
var rarity_common: Color     = Color(0.7, 0.7, 0.7)
var rarity_uncommon: Color   = Color(0.3, 0.9, 0.3)
var rarity_rare: Color       = Color(0.3, 0.5, 1.0)
var rarity_epic: Color       = Color(0.7, 0.3, 0.9)
var rarity_legendary: Color  = Color(1.0, 0.7, 0.2)

## 属性方向颜色（技能盘6区域）
var region_str: Color        = Color(0.9, 0.3, 0.25)   # 力量-红
var region_dex: Color        = Color(0.3, 0.8, 0.3)    # 敏捷-绿
var region_con: Color        = Color(0.8, 0.7, 0.2)    # 体质-黄
var region_int: Color        = Color(0.4, 0.5, 1.0)    # 智力-蓝
var region_wis: Color        = Color(0.3, 0.8, 0.8)    # 感知-青
var region_cha: Color        = Color(0.8, 0.4, 0.9)    # 魅力-紫

## 法术学派颜色
var school_evocation: Color  = Color(1.0, 0.4, 0.2)
var school_abjuration: Color = Color(0.4, 0.6, 1.0)
var school_illusion: Color   = Color(0.7, 0.5, 1.0)
var school_necromancy: Color = Color(0.5, 0.8, 0.3)
var school_transmutation: Color = Color(0.9, 0.8, 0.2)
var school_enchantment: Color  = Color(0.9, 0.4, 0.7)
var school_divination: Color   = Color(0.3, 0.9, 0.9)
var school_conjuration: Color  = Color(0.6, 0.4, 0.2)

## 种族颜色
var race_human: Color     = Color(0.85, 0.8, 0.7)
var race_elf: Color       = Color(0.5, 0.9, 0.6)
var race_dwarf: Color     = Color(0.8, 0.65, 0.3)
var race_halforc: Color   = Color(0.7, 0.35, 0.3)
var race_halfelf: Color   = Color(0.6, 0.6, 0.9)

# ============================================================================
# 字号系统
# ============================================================================

var font_size_xs: int      = 10
var font_size_sm: int      = 12
var font_size_md: int      = 14
var font_size_lg: int      = 16
var font_size_xl: int      = 20
var font_size_xxl: int     = 24
var font_size_title: int   = 28

# ============================================================================
# 间距系统
# ============================================================================

var spacing_xs: int         = 2
var spacing_sm: int        = 4
var spacing_md: int        = 8
var spacing_lg: int        = 12
var spacing_xl: int        = 16
var spacing_xxl: int       = 24
var spacing_xxxl: int      = 32

# ============================================================================
# 圆角
# ============================================================================

var radius_sm: int         = 2
var radius_md: int         = 4
var radius_lg: int         = 6
var radius_xl: int         = 8
var radius_round: int      = 12

# ============================================================================
# 尺寸规范
# ============================================================================

var button_height: int     = 36
var button_height_lg: int  = 45
var bar_height_sm: int     = 8
var bar_height_md: int     = 12
var bar_height_lg: int     = 16
var icon_size_sm: int      = 24
var icon_size_md: int      = 32
var icon_size_lg: int      = 48
var icon_size_xl: int      = 64
var panel_min_width: int   = 220
var panel_min_width_lg: int = 320
var portrait_size: int      = 80

# ============================================================================
# 动画时长
# ============================================================================

var anim_fast: float       = 0.15
var anim_normal: float     = 0.25
var anim_slow: float       = 0.4
var anim_very_slow: float  = 0.6

# ============================================================================
# 图像资源占位（未来替换）
# ============================================================================

## 按钮图像 — 当前为null（使用程序化样式），未来替换为Texture2D
var btn_normal_texture: Texture2D = null
var btn_hover_texture: Texture2D = null
var btn_pressed_texture: Texture2D = null
var btn_disabled_texture: Texture2D = null

## 面板图像
var panel_bg_texture: Texture2D = null
var card_bg_texture: Texture2D = null
var tooltip_bg_texture: Texture2D = null

## 图标图像
var icon_atlas: Texture2D = null      ## 图标图集（未来）
var portrait_frame: Texture2D = null  ## 头像框（未来）

## 技能盘图像
var skill_node_active_texture: Texture2D = null
var skill_node_inactive_texture: Texture2D = null
var skill_node_locked_texture: Texture2D = null

## 地形图标
var terrain_icon_atlas: Texture2D = null

# ============================================================================
# 辅助方法
# ============================================================================

## 创建标准面板样式
func make_panel_style(bg: Color = bg_panel, border: Color = border_default, 
		border_width: int = 1, corner_radius: int = radius_md, 
		content_margin: int = spacing_md) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = bg
	style.set_border_width_all(border_width)
	style.border_color = border
	style.set_corner_radius_all(corner_radius)
	style.set_content_margin_all(content_margin)
	return style

## 创建标准按钮样式
func make_button_style(bg_normal: Color = Color(0.18, 0.17, 0.22),
		bg_hover: Color = Color(0.28, 0.26, 0.34),
		bg_pressed: Color = Color(0.12, 0.11, 0.15),
		bg_disabled: Color = Color(0.12, 0.12, 0.12, 0.5),
		corner_radius: int = radius_md) -> Dictionary:
	return {
		"normal": _make_btn_style(bg_normal, border_default, corner_radius),
		"hover": _make_btn_style(bg_hover, border_highlight, corner_radius),
		"pressed": _make_btn_style(bg_pressed, border_highlight, corner_radius),
		"disabled": _make_btn_style(bg_disabled, Color(0.2, 0.2, 0.2, 0.3), corner_radius),
	}

func _make_btn_style(bg: Color, border: Color, cr: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.set_border_width_all(1)
	s.border_color = border
	s.set_corner_radius_all(cr)
	s.set_content_margin_all(spacing_sm)
	return s

## 创建进度条样式
func make_bar_style(fill_color: Color, bg_color: Color = Color(0.1, 0.1, 0.12),
		corner_radius: int = radius_sm) -> Dictionary:
	return {
		"fill": _make_bar_fill(fill_color, corner_radius),
		"background": _make_bar_bg(bg_color, corner_radius),
	}

func _make_bar_fill(color: Color, cr: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.set_corner_radius_all(cr)
	return s

func _make_bar_bg(color: Color, cr: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.set_corner_radius_all(cr)
	return s

## 获取属性方向颜色（匹配 SkillNodeData.Region 枚举值）
func get_region_color(region: int) -> Color:
	match region:
		1: return region_str   # STR
		2: return region_dex   # DEX
		3: return region_con   # CON
		4: return region_int   # INT
		5: return region_wis   # WIS
		6: return region_cha   # CHA
		_: return text_muted   # NONE/TRANSITION/其他

## 获取HP颜色
func get_hp_color(ratio: float) -> Color:
	if ratio > 0.6: return hp_high
	elif ratio > 0.3: return hp_mid
	else: return hp_low

## 获取士气颜色
func get_morale_color(level: int) -> Color:
	match level:
		0: return morale_high
		1: return morale_normal
		2: return morale_low
		3: return morale_broken
		4: return morale_routing
		_: return morale_normal

## 获取法术学派颜色
func get_school_color(school: int) -> Color:
	match school:
		0: return school_evocation
		1: return school_abjuration
		2: return school_illusion
		3: return school_necromancy
		4: return school_transmutation
		5: return school_enchantment
		6: return school_divination
		7: return school_conjuration
		_: return text_muted

## 获取稀有度颜色
func get_rarity_color(rarity: int) -> Color:
	match rarity:
		0: return rarity_common
		1: return rarity_uncommon
		2: return rarity_rare
		3: return rarity_epic
		4: return rarity_legendary
		_: return rarity_common

## 获取种族颜色
func get_race_color(race: String) -> Color:
	match race:
		"human": return race_human
		"elf": return race_elf
		"dwarf": return race_dwarf
		"halforc": return race_halforc
		"halfelf": return race_halfelf
		_: return text_muted

## 应用按钮主题样式
func apply_button_theme(btn: Button, styles: Dictionary = make_button_style()) -> void:
	if btn.has_theme_stylebox_override("normal"):
		btn.remove_theme_stylebox_override("normal")
	btn.add_theme_stylebox_override("normal", styles.normal)
	btn.add_theme_stylebox_override("hover", styles.hover)
	btn.add_theme_stylebox_override("pressed", styles.pressed)
	btn.add_theme_stylebox_override("disabled", styles.disabled)
	btn.add_theme_color_override("font_color", text_primary)
	btn.add_theme_color_override("font_hover_color", text_accent)
	btn.add_theme_color_override("font_pressed_color", text_secondary)
	btn.add_theme_color_override("font_disabled_color", Color(0.4, 0.4, 0.4))

## 应用进度条主题样式
func apply_bar_theme(bar: ProgressBar, fill_color: Color, bg_color: Color = Color(0.1, 0.1, 0.12)) -> void:
	var styles := make_bar_style(fill_color, bg_color)
	bar.add_theme_stylebox_override("fill", styles.fill)
	bar.add_theme_stylebox_override("background", styles.background)