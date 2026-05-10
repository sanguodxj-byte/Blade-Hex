# ArenaPanel.gd
# 竞技场面板 — 参加比赛赢取金币和声望
extends CanvasLayer
class_name ArenaPanel

signal arena_finished()

var _theme: UITheme:
	get: return UITheme.get_instance()
var _factory := UIFactory.new()
var _root: Control
var _result_label: RichTextLabel
var _gold_label: Label
var _economy: EconomyManager

func _ready():
	layer = 25
	_setup_ui()

func _setup_ui():
	_root = Control.new()
	_root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_root.visible = false
	add_child(_root)

	var overlay = ColorRect.new()
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.color = Color(0, 0, 0, 0.6)
	overlay.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(overlay)

	var panel = _factory.create_panel(Vector2(400, 380), _theme.bg_primary, _theme.border_highlight)
	panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	panel.offset_left = -200
	panel.offset_top = -200
	panel.offset_right = 200
	panel.offset_bottom = 200
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_root.add_child(panel)

	var margin = _factory.create_margin(20, 20, 15, 15)
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", _theme.spacing_lg)
	vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_child(vbox)

	vbox.add_child(_factory.create_title_label("竞技场", 20))
	vbox.add_child(_factory.create_body_label("在这里展示你的实力，赢取金币和声望。"))
	vbox.add_child(_factory.create_separator_h())

	# 难度选择
	var diff_label = _factory.create_body_label("选择对手:")
	vbox.add_child(diff_label)

	var btn_easy = _factory.create_button("新手挑战 (报名费: 20金, 奖金: 50金)", Vector2(360, 40))
	btn_easy.pressed.connect(func(): _fight(20, 50, 1))
	vbox.add_child(btn_easy)

	var btn_med = _factory.create_button("精英挑战 (报名费: 50金, 奖金: 150金)", Vector2(360, 40))
	btn_med.pressed.connect(func(): _fight(50, 150, 3))
	vbox.add_child(btn_med)

	var btn_hard = _factory.create_button("冠军挑战 (报名费: 100金, 奖金: 400金)", Vector2(360, 40))
	btn_hard.pressed.connect(func(): _fight(100, 400, 5))
	vbox.add_child(btn_hard)

	vbox.add_child(_factory.create_separator_h())

	_gold_label = _factory.create_body_label("")
	vbox.add_child(_gold_label)

	_result_label = _factory.create_rich_text(Vector2(360, 50))
	vbox.add_child(_result_label)

	var close_btn = _factory.create_button("离开竞技场", Vector2(360, 40))
	close_btn.pressed.connect(func(): arena_finished.emit(); hide_panel())
	vbox.add_child(close_btn)

func show_arena(economy: EconomyManager = null) -> void:
	_economy = _economy
	_result_label.text = ""
	_gold_label.text = "当前金币: %d" % (_economy.gold if _economy else 0)
	_root.visible = true

func hide_panel() -> void:
	_root.visible = false

func is_panel_visible() -> bool:
	return _root.visible

func _fight(entry_fee: int, prize: int, difficulty: int) -> void:
	if _economy and not _economy.spend_gold(entry_fee):
		_result_label.text = "[color=red]金币不足，无法报名！[/color]"
		return
	# 简化战斗：50% + 10%*等级 差异获胜
	var win_chance = 0.5 + difficulty * 0.05
	var won = randf() < win_chance
	if won:
		if _economy:
			_economy.earn_gold(prize)
		_result_label.text = "[color=green]胜利！你获得了 %d 金币！[/color]" % prize
	else:
		_result_label.text = "[color=red]败北... 你被击败了，报名费已损失。[/color]"
	if _economy:
		_gold_label.text = "当前金币: %d" % _economy.gold
