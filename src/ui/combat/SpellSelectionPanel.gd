# SpellSelectionPanel.gd
# 法术选择面板 — 显示施法者可用法术、魔力、冷却状态
# 对应策划案 07-法术系统 → 冷却 UI 表现 + 09-UI设计 → 法术选择
extends Control
class_name SpellSelectionPanel

# ============================================================================
# 信号
# ============================================================================

signal spell_selected(spell: SpellData)
signal spell_cancelled()

# ============================================================================
# 内部组件
# ============================================================================

var panel: PanelContainer
var _mana_bar: ProgressBar
var _mana_label: Label
var _spell_grid: GridContainer
var _cancel_btn: Button
var _spell_buttons: Array[Button] = []
var _caster: Unit = null
var _spell_manager: SpellManager = null

func _ready():
	_setup_ui()
	visible = false

func _setup_ui():
	# 主面板 — 居中偏右
	set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
	offset_left = -280
	offset_top = 60
	offset_bottom = -140
	
	panel = PanelContainer.new()
	var style = StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.08, 0.12, 0.95)
	style.set_border_width_all(2)
	style.border_color = Color(0.4, 0.35, 0.6)
	style.set_corner_radius_all(6)
	panel.add_theme_stylebox_override("panel", style)
	add_child(panel)
	
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 12)
	margin.add_theme_constant_override("margin_right", 12)
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_bottom", 10)
	panel.add_child(margin)
	
	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 8)
	margin.add_child(vbox)
	
	# 标题
	var title = Label.new()
	title.text = "— 选择法术 —"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 16)
	title.add_theme_color_override("font_color", Color(0.7, 0.6, 1.0))
	vbox.add_child(title)
	
	# 魔力条
	var mana_hbox = HBoxContainer.new()
	vbox.add_child(mana_hbox)
	
	var mana_title = Label.new()
	mana_title.text = "魔力:"
	mana_title.add_theme_color_override("font_color", Color(0.5, 0.7, 1.0))
	mana_hbox.add_child(mana_title)
	
	_mana_bar = ProgressBar.new()
	_mana_bar.custom_minimum_size = Vector2(150, 16)
	_mana_bar.show_percentage = false
	var bar_style = StyleBoxFlat.new()
	bar_style.bg_color = Color(0.15, 0.15, 0.2)
	bar_style.set_corner_radius_all(3)
	_mana_bar.add_theme_stylebox_override("background", bar_style)
	var fill_style = StyleBoxFlat.new()
	fill_style.bg_color = Color(0.3, 0.5, 1.0)
	fill_style.set_corner_radius_all(3)
	_mana_bar.add_theme_stylebox_override("fill", fill_style)
	mana_hbox.add_child(_mana_bar)
	
	_mana_label = Label.new()
	_mana_label.text = "0/0"
	_mana_label.add_theme_color_override("font_color", Color(0.5, 0.7, 1.0))
	mana_hbox.add_child(_mana_label)
	
	# 法术网格
	_spell_grid = GridContainer.new()
	_spell_grid.columns = 3
	_spell_grid.add_theme_constant_override("h_separation", 6)
	_spell_grid.add_theme_constant_override("v_separation", 6)
	vbox.add_child(_spell_grid)
	
	# 取消按钮
	_cancel_btn = Button.new()
	_cancel_btn.text = "取消 (Esc)"
	_cancel_btn.custom_minimum_size = Vector2(100, 32)
	_cancel_btn.pressed.connect(func(): spell_cancelled.emit(); visible = false)
	vbox.add_child(_cancel_btn)

# ============================================================================
# 公开接口
# ============================================================================

## 打开法术选择面板
func open(caster: Unit, spell_manager: SpellManager):
	_caster = _caster
	_spell_manager = _spell_manager
	
	# 清空旧按钮
	for btn in _spell_buttons:
		btn.queue_free()
	_spell_buttons.clear()
	
	if not _caster.data:
		return
	
	# 更新魔力显示
	var max_mana = spell_manager.get_max_mana(caster)
	_mana_bar.max_value = max_mana
	_mana_bar.value = _caster.data.current_mana
	_mana_label.text = "%d/%d" % [_caster.data.current_mana, max_mana]
	
	# 创建法术按钮
	for spell in _caster.data.known_spells:
		var btn = Button.new()
		btn.custom_minimum_size = Vector2(80, 60)
		
		# 法术名+魔力消耗
		btn.text = "%s\n(%d)" % [spell.spell_name, spell.mana_cost]
		
		# 冷却中 → 灰暗+冷却数
		var cooldown = _caster.data.spell_cooldowns.get(spell.spell_id, 0)
		if cooldown > 0:
			btn.text += "\n冷却:%d" % cooldown
			btn.disabled = true
			btn.modulate = Color(0.5, 0.5, 0.5, 0.6)
		# 魔力不足 → 灰显
		elif _caster.data.current_mana < spell.mana_cost:
			btn.disabled = true
			btn.modulate = Color(0.4, 0.4, 0.5, 0.6)
		else:
			# 可用 — 按学派着色
			var color = _school_color(spell.spell_school)
			btn.modulate = Color(color, color, color, 1.0)
		
		# 连接信号
		var spell_ref = spell
		btn.pressed.connect(func(): _on_spell_clicked(spell_ref))
		
		# tooltip
		btn.tooltip_text = "%s (%s %s)\n%s\n魔力: %d | 冷却: %d回合" % [
			spell.spell_name, spell.get_tier_name(), spell.get_school_name(),
			spell.description, spell.mana_cost, spell.cooldown_turns
		]
		
		_spell_grid.add_child(btn)
		_spell_buttons.append(btn)
	
	visible = true

## 关闭面板
func close():
	visible = false

func _on_spell_clicked(spell: SpellData):
	spell_selected.emit(spell)
	visible = false

func _school_color(school: int) -> float:
	# 简单映射：不同学派不同灰度亮度
	match school:
		SpellData.SpellSchool.EVOCATION: return 1.0
		SpellData.SpellSchool.ABJURATION: return 0.85
		SpellData.SpellSchool.ILLUSION: return 0.75
		SpellData.SpellSchool.NECROMANCY: return 0.65
		SpellData.SpellSchool.TRANSMUTATION: return 0.9
		SpellData.SpellSchool.ENCHANTMENT: return 0.8
		SpellData.SpellSchool.DIVINATION: return 0.7
		SpellData.SpellSchool.CONJURATION: return 0.88
		_: return 0.75
