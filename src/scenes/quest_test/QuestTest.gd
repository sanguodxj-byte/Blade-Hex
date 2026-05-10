extends Node

## 任务系统测试场景

@onready var quest_manager: QuestManager = $QuestManager
@onready var quest_board: QuestBoard = $UI/QuestBoard
@onready var quest_log: QuestLog = $UI/QuestLog
@onready var info_label: Label = $UI/InfoPanel/InfoLabel
@onready var show_board_button: Button = $UI/ButtonPanel/ShowBoardButton
@onready var show_log_button: Button = $UI/ButtonPanel/ShowLogButton
@onready var simulate_progress_button: Button = $UI/ButtonPanel/SimulateProgressButton


func _ready() -> void:
	# 设置任务管理器
	quest_board.set_quest_manager(quest_manager)
	quest_log.set_quest_manager(quest_manager)
	
	# 连接按钮信号
	show_board_button.pressed.connect(_on_show_board_pressed)
	show_log_button.pressed.connect(_on_show_log_pressed)
	simulate_progress_button.pressed.connect(_on_simulate_progress_pressed)
	
	# 连接任务信号
	quest_manager.quest_accepted.connect(_on_quest_accepted)
	quest_manager.quest_completed.connect(_on_quest_completed)
	quest_manager.quest_progress_updated.connect(_on_quest_progress_updated)
	
	# 初始化玩家数据
	quest_manager.player_gold = 500
	quest_manager.player_reputation = 10
	
	# 隐藏UI
	quest_board.hide()
	quest_log.hide()
	
	_update_info()


func _update_info() -> void:
	var info = "玩家信息:\n"
	info += "金币: %d\n" % quest_manager.player_gold
	info += "声望: %d\n\n" % quest_manager.player_reputation
	info += "可用任务: %d\n" % quest_manager.available_quests.size()
	info += "进行中任务: %d\n" % quest_manager.active_quests.size()
	info += "已完成任务: %d\n" % quest_manager.completed_quest_ids.size()
	
	info_label.text = info


func _on_show_board_pressed() -> void:
	quest_board.show_board()


func _on_show_log_pressed() -> void:
	quest_log.show_log()


func _on_simulate_progress_pressed() -> void:
	# 模拟任务进度
	if quest_manager.active_quests.size() > 0:
		var quest = quest_manager.active_quests[0]
		quest_manager.update_quest_progress(quest.quest_id, 1)
		_update_info()
	else:
		print("没有进行中的任务")


func _on_quest_accepted(quest: QuestData) -> void:
	print("接取任务: ", quest.quest_name)
	_update_info()


func _on_quest_completed(quest: QuestData) -> void:
	print("完成任务: ", quest.quest_name)
	_update_info()


func _on_quest_progress_updated(quest: QuestData, progress: int) -> void:
	print("任务进度更新: %s - %d/%d" % [quest.quest_name, progress, quest.target_count])
	_update_info()


func _input(event: InputEvent) -> void:
	# 快捷键
	if event.is_action_pressed("ui_cancel"):
		if quest_board.visible:
			quest_board.hide()
		elif quest_log.visible:
			quest_log.hide()
	
	# Q键打开布告栏
	if event is InputEventKey and event.pressed and event.keycode == KEY_Q:
		if not quest_board.visible:
			quest_board.show_board()
	
	# L键打开任务日志
	if event is InputEventKey and event.pressed and event.keycode == KEY_L:
		if not quest_log.visible:
			quest_log.show_log()
