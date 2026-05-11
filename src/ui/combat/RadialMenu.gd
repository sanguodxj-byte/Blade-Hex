extends Control
class_name RadialMenu

signal action_selected(action: String)

var radius: float = 70.0
var _buttons: Array[Button] = []
var _theme = UITheme.get_instance()
var _factory = UIFactory.new()

func _ready():
	mouse_filter = Control.MOUSE_FILTER_STOP
	
	# 如果点击空白处，隐藏菜单
	gui_input.connect(func(ev):
		if ev is InputEventMouseButton and ev.pressed:
			hide()
	)

func setup(options: Dictionary):
	# options 字典形如 {"法术": "spell", "物品": "item", ...}
	# 清除旧按钮
	for b in _buttons:
		b.queue_free()
	_buttons.clear()
	
	var count = options.size()
	if count == 0:
		return
		
	var angle_step = TAU / count
	var current_angle = -PI / 2 # 从顶部开始
	
	for label in options:
		var action_name = options[label]
		var btn = Button.new()
		btn.text = label
		btn.custom_minimum_size = Vector2(60, 40)
		
		# 美化按钮
		var style = _theme.make_panel_style(_theme.bg_primary, _theme.border_muted, _theme.radius_round)
		btn.add_theme_stylebox_override("normal", style)
		btn.add_theme_font_size_override("font_size", _theme.font_size_sm)
		
		# 计算位置
		var pos = Vector2(cos(current_angle), sin(current_angle)) * radius
		btn.position = pos - btn.custom_minimum_size / 2
		
		btn.pressed.connect(func():
			action_selected.emit(action_name)
			hide()
		)
		
		add_child(btn)
		_buttons.append(btn)
		current_angle += angle_step

func _draw():
	# 绘制半透明轮盘底图
	draw_circle(Vector2.ZERO, radius + 30, Color(0.05, 0.05, 0.1, 0.8))
	draw_arc(Vector2.ZERO, radius + 30, 0, TAU, 32, _theme.border_default, 2.0, true)

func show_menu(pos: Vector2):
	position = pos
	show()
