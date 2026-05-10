# BattleLogPanel.gd
# 战斗日志面板 — 滚动文本显示攻击结果、伤害数字、状态变化
# 对应策划案 09-UI设计.md → 战斗日志 / 伤害数字弹出 / 状态变化通知
# 设计原则：不暴露骰子术语，显示概率和直观结果
extends PanelContainer
class_name BattleLogPanel

# ============================================================================
# 信号
# ============================================================================
signal log_hovered(entry_index: int)

# ============================================================================
# 常量
# ============================================================================
const MAX_ENTRIES := 200

# ============================================================================
# 内部
# ============================================================================
var _log: RichTextLabel
var _scroll: ScrollContainer
var _entries: Array[String] = []
var _factory: UIFactory
var _theme: UITheme:
	get: return UITheme.get_instance()

func _ready():
	_factory = UIFactory.new()
	_setup()

func _setup():
	custom_minimum_size = Vector2(0, 100)
	add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_tertiary, _theme.border_default, 1, _theme.radius_md, _theme.spacing_sm))
	
	_scroll = _factory.create_scroll_container(false)
	_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(_scroll)
	
	_log = _factory.create_rich_text()
	_log.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_log.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_log.scroll_active = true
	_log.scroll_following = true
	_log.bbcode_enabled = true
	_log.custom_minimum_size = Vector2(0, 80)
	_scroll.add_child(_log)

# ============================================================================
# 公开接口
# ============================================================================

## 添加日志条目
func add_entry(text: String, category: String = "info"):
	var bbcode := _format_entry(text, category)
	_entries.append(bbcode)
	
	# 超出上限时移除最旧的
	if _entries.size() > MAX_ENTRIES:
		_entries.pop_front()
	
	_refresh_display()

## 记录攻击结果
func log_attack(attacker_name: String, target_name: String, 
		hit: bool, damage: int = 0, is_critical: bool = false, is_miss: bool = false):
	if is_miss:
		add_entry("%s 攻击 %s → 失误!" % [attacker_name, target_name], "miss")
	elif hit:
		if is_critical:
			add_entry("★ %s 命中 %s！暴击！造成 %d 伤害" % [attacker_name, target_name, damage], "critical")
		else:
			add_entry("%s 命中 %s，造成 %d 伤害" % [attacker_name, target_name, damage], "hit")
	else:
		add_entry("%s 攻击 %s → 未命中" % [attacker_name, target_name], "miss")

## 记录法术施放
func log_spell(caster_name: String, spell_name: String, 
		target_name: String = "", damage: int = 0, hit: bool = true):
	if target_name == "":
		add_entry("%s 施放了 %s" % [caster_name, spell_name], "spell")
	elif hit:
		add_entry("%s 对 %s 施放 %s，造成 %d 伤害" % [caster_name, target_name, spell_name, damage], "spell")
	else:
		add_entry("%s 对 %s 施放 %s → 抵抗" % [caster_name, target_name, spell_name], "miss")

## 记录状态变化
func log_status(unit_name: String, status: String, gained: bool = true):
	if gained:
		add_entry("%s 获得 %s" % [unit_name, status], "status_gain")
	else:
		add_entry("%s 解除 %s" % [unit_name, status], "status_loss")

## 记录士气变化
func log_morale(unit_name: String, morale_text: String, is_positive: bool = true):
	if is_positive:
		add_entry("%s 士气%s" % [unit_name, morale_text], "morale_up")
	else:
		add_entry("%s 士气%s" % [unit_name, morale_text], "morale_down")

## 记录单位死亡
func log_death(unit_name: String, is_player: bool = true):
	if is_player:
		add_entry("✘ %s 倒下！" % unit_name, "death_ally")
	else:
		add_entry("✘ %s 被击败！" % unit_name, "death_enemy")

## 记录回合信息
func log_turn(text: String):
	add_entry(text, "turn")

## 记录移动
func log_move(unit_name: String, from_pos: String, to_pos: String):
	add_entry("%s 移动 %s → %s" % [unit_name, from_pos, to_pos], "move")

## 清空日志
func clear_log():
	_entries.clear()
	_refresh_display()

# ============================================================================
# 内部方法
# ============================================================================

func _format_entry(text: String, category: String) -> String:
	var color := _theme.text_primary
	match category:
		"hit": color = _theme.text_positive
		"miss": color = _theme.text_negative
		"critical": color = _theme.text_accent
		"spell": color = _theme.text_magic
		"status_gain": color = Color(0.3, 0.8, 0.3)
		"status_loss": color = Color(0.8, 0.5, 0.3)
		"morale_up": color = Color(0.3, 0.8, 0.9)
		"morale_down": color = Color(0.9, 0.5, 0.2)
		"death_ally": color = Color(0.9, 0.3, 0.3)
		"death_enemy": color = Color(0.9, 0.7, 0.2)
		"turn": color = _theme.text_accent
		"move": color = _theme.text_muted
		"info": color = _theme.text_secondary
	return "[color=%s]%s[/color]" % [color.to_html(false), text]

func _refresh_display():
	var full_text = ""
	for entry in _entries:
		full_text += entry + "\n"
	_log.text = full_text
	
	# 自动滚到底部
	await get_tree().process_frame
	_scroll.scroll_vertical = _scroll.get_v_scroll_bar().max_value