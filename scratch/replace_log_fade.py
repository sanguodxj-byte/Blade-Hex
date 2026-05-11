import re

with open('src/ui/combat/BattleLogPanel.gd', 'r', encoding='utf-8') as f:
    content = f.read()

replacement = """
var _fade_timer: Timer
var _fade_tween: Tween
var _is_hovered: bool = false

func _ready():
	_factory = UIFactory.new()
	_setup()
	
	_fade_timer = Timer.new()
	_fade_timer.wait_time = 4.0
	_fade_timer.one_shot = true
	_fade_timer.timeout.connect(_on_fade_timer_timeout)
	add_child(_fade_timer)
	
	mouse_entered.connect(func():
		_is_hovered = true
		_wake_up()
	)
	mouse_exited.connect(func():
		_is_hovered = false
		_fade_timer.start()
	)
	
	# 初始化不可见
	modulate.a = 0.0

func _wake_up():
	if _fade_tween and _fade_tween.is_valid():
		_fade_tween.kill()
	modulate.a = 1.0
	if not _is_hovered:
		_fade_timer.start()

func _on_fade_timer_timeout():
	if _is_hovered:
		return
	if _fade_tween and _fade_tween.is_valid():
		_fade_tween.kill()
	_fade_tween = create_tween()
	_fade_tween.tween_property(self, "modulate:a", 0.0, 1.0)

func _setup():
	custom_minimum_size = Vector2(0, 100)
	add_theme_stylebox_override("panel", _theme.make_panel_style(
		_theme.bg_tertiary, _theme.border_default, _theme.radius_md))
	
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
	_wake_up()
"""

pattern = re.compile(r'func _ready\(\).*?func add_entry\(text: String, category: String = "info"\):', re.DOTALL)
new_content = pattern.sub(replacement, content)

with open('src/ui/combat/BattleLogPanel.gd', 'w', encoding='utf-8') as f:
    f.write(new_content)
print('Replaced BattleLogPanel logic with fade-out effect')
