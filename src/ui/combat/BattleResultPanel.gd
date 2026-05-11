# BattleResultPanel.gd
# 战斗结果面板 — 显示胜利/失败信息、战利品、经验获得
# 对应 P2 #14 优化项
extends PanelContainer
class_name BattleResultPanel

# ============================================================================
# 信号
# ============================================================================
signal confirmed()

# ============================================================================
# 内部组件
# ============================================================================
var _title_label: Label
var _result_label: Label
var _loot_label: Label
var _xp_label: Label
var _gold_label: Label
var _detail_rich: RichTextLabel
var _confirm_btn: Button
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

func _ready():
	_factory = UIFactory.new()
	_setup()
	visible = false

func _setup():
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	z_index = 100
	
	# 半透明遮罩
	var overlay_bg := StyleBoxFlat.new()
	overlay_bg.bg_color = Color(0.0, 0.0, 0.0, 0.7)
	add_theme_stylebox_override("panel", overlay_bg)
	
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	
	var inner := PanelContainer.new()
	var inner_style := StyleBoxFlat.new()
	inner_style.bg_color = _theme.bg_primary
	inner_style.set_border_width_all(2)
	inner_style.border_color = _theme.border_highlight
	inner_style.set_corner_radius_all(_theme.radius_lg)
	inner_style.set_content_margin_all(30)
	inner.add_theme_stylebox_override("panel", inner_style)
	inner.custom_minimum_size = Vector2(420, 0)
	center.add_child(inner)
	
	var vbox := VBoxContainer.new()
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	vbox.add_theme_constant_override("separation", _theme.spacing_md)
	inner.add_child(vbox)
	
	# 标题
	_title_label = _factory.create_title_label("战斗结束", _theme.font_size_xxl)
	_title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(_title_label)
	
	vbox.add_child(_factory.create_separator_h(_theme.border_highlight))
	
	# 结果文字
	_result_label = Label.new()
	_result_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_result_label.add_theme_font_size_override("font_size", _theme.font_size_xl)
	vbox.add_child(_result_label)
	
	# 分隔
	vbox.add_child(_factory.create_separator_h())
	
	# 奖励区
	var reward_title = _factory.create_title_label("— 战 利 品 —", _theme.font_size_lg)
	reward_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(reward_title)
	
	# XP
	var xp_hbox := HBoxContainer.new()
	xp_hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	xp_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	vbox.add_child(xp_hbox)
	var xp_icon = _factory.create_body_label("⭐", _theme.text_accent)
	xp_hbox.add_child(xp_icon)
	_xp_label = _factory.create_body_label("获得经验: 0", _theme.text_positive)
	xp_hbox.add_child(_xp_label)
	
	# 金币
	var gold_hbox := HBoxContainer.new()
	gold_hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	gold_hbox.add_theme_constant_override("separation", _theme.spacing_sm)
	vbox.add_child(gold_hbox)
	var gold_icon = _factory.create_body_label("💰", _theme.text_accent)
	gold_hbox.add_child(gold_icon)
	_gold_label = _factory.create_body_label("获得金币: 0", _theme.text_accent)
	gold_hbox.add_child(_gold_label)
	
	# 战利品文字
	_loot_label = _factory.create_body_label("", _theme.text_muted)
	_loot_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(_loot_label)
	
	vbox.add_child(_factory.create_separator_h())
	
	# 详细战报
	_detail_rich = _factory.create_rich_text(Vector2(360, 80))
	vbox.add_child(_detail_rich)
	
	# 确认按钮
	_confirm_btn = _factory.create_button("返回大地图", Vector2(200, _theme.button_height_lg))
	_confirm_btn.pressed.connect(func(): confirmed.emit(); visible = false)
	vbox.add_child(_confirm_btn)

# ============================================================================
# 公开接口
# ============================================================================

## 显示胜利结果
func show_victory(xp_gained: int = 0, gold_gained: int = 0, 
		loot_items: Array[String] = [], details: String = ""):
	_title_label.add_theme_color_override("font_color", _theme.text_positive)
	_result_label.text = "🎉 胜 利！"
	_result_label.add_theme_color_override("font_color", _theme.text_accent)
	_xp_label.text = "获得经验: +%d" % xp_gained
	_gold_label.text = "获得金币: +%d" % gold_gained
	if loot_items.is_empty():
		_loot_label.text = ""
	else:
		_loot_label.text = "战利品: " + ", ".join(loot_items)
	_detail_rich.text = "[color=gray]%s[/color]" % details if details != "" else ""
	visible = true

## 显示失败结果
func show_defeat(survivors: int = 0, details: String = ""):
	_title_label.add_theme_color_override("font_color", _theme.text_negative)
	_result_label.text = "💀 全军覆没"
	_result_label.add_theme_color_override("font_color", _theme.text_negative)
	_xp_label.text = ""
	_gold_label.text = ""
	_loot_label.text = "幸存者: %d人" % survivors if survivors > 0 else "无人生还"
	_detail_rich.text = "[color=gray]%s[/color]" % details if details != "" else ""
	_confirm_btn.text = "回到主菜单"
	visible = true

## 隐藏面板
func hide_panel():
	visible = false