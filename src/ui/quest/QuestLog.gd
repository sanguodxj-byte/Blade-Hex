class_name QuestLog
extends Control

## 任务日志UI - 显示已接取的任务和进度

signal quest_selected(quest: QuestData)
signal quest_abandoned(quest: QuestData)

@onready var active_quest_list: ItemList = $Panel/VBoxContainer/TabContainer/进行中/ActiveQuestList
@onready var completed_quest_list: ItemList = $Panel/VBoxContainer/TabContainer/已完成/CompletedQuestList
@onready var quest_detail: RichTextLabel = $Panel/VBoxContainer/DetailPanel/VBox/QuestDetail
@onready var abandon_button: Button = $Panel/VBoxContainer/DetailPanel/VBox/AbandonButton
@onready var close_button: Button = $Panel/VBoxContainer/CloseButton

var quest_manager: QuestManager
var selected_quest: QuestData = null
var current_tab: int = 0


func _ready() -> void:
	# 连接信号
	active_quest_list.item_selected.connect(_on_active_quest_selected)
	completed_quest_list.item_selected.connect(_on_completed_quest_selected)
	abandon_button.pressed.connect(_on_abandon_pressed)
	close_button.pressed.connect(_on_close_pressed)
	
	# 初始化
	abandon_button.disabled = true
	quest_detail.text = "选择一个任务查看详情"


## 设置任务管理器
func set_quest_manager(manager: QuestManager) -> void:
	quest_manager = manager
	
	# 连接任务管理器信号
	if quest_manager:
		quest_manager.quest_accepted.connect(_on_quest_accepted)
		quest_manager.quest_completed.connect(_on_quest_completed)
		quest_manager.quest_progress_updated.connect(_on_quest_progress_updated)
	
	refresh_quest_lists()


## 刷新任务列表
func refresh_quest_lists() -> void:
	if not quest_manager:
		return
	
	_refresh_active_quests()
	_refresh_completed_quests()


## 刷新进行中的任务
func _refresh_active_quests() -> void:
	active_quest_list.clear()
	
	for quest in quest_manager.active_quests:
		var text = "[%s] %s - %s" % [
			quest.get_type__text(),
			quest.quest_name,
			quest.get_progress__text()
		]
		
		# 添加时间限制提示
		if quest.has_time_limit:
			var remaining = quest.get_remaining_days(quest_manager.game_time)
			text += " (剩余%.1f天)" % remaining
		
		active_quest_list.add_item(text)
		
		# 根据难度设置颜色
		var index = active_quest_list.item_count - 1
		match quest.difficulty:
			QuestData.QuestDifficulty.EASY:
				active_quest_list.set_item_custom_fg_color(index, Color.WHITE)
			QuestData.QuestDifficulty.MEDIUM:
				active_quest_list.set_item_custom_fg_color(index, Color.YELLOW)
			QuestData.QuestDifficulty.HARD:
				active_quest_list.set_item_custom_fg_color(index, Color.ORANGE)
			QuestData.QuestDifficulty.BOSS:
				active_quest_list.set_item_custom_fg_color(index, Color.RED)


## 刷新已完成的任务
func _refresh_completed_quests() -> void:
	completed_quest_list.clear()
	
	for quest_id in quest_manager.completed_quest_ids:
		# 从模板获取任务信息
		if quest_id in quest_manager.quest_templates:
			var quest = quest_manager.quest_templates[quest_id]
			var text = "[%s] %s" % [quest.get_type_text(), quest.quest_name]
			completed_quest_list.add_item(text)
			
			# 已完成任务显示为灰色
			var index = completed_quest_list.item_count - 1
			completed_quest_list.set_item_custom_fg_color(index, Color.GRAY)


## 活跃任务被选中
func _on_active_quest_selected(index: int) -> void:
	if index < 0 or index >= quest_manager.active_quests.size():
		return
	
	selected_quest = quest_manager.active_quests[index]
	_display_quest_detail(selected_quest, true)
	abandon_button.disabled = false
	
	quest_selected.emit(selected_quest)


## 已完成任务被选中
func _on_completed_quest_selected(index: int) -> void:
	if index < 0 or index >= quest_manager.completed_quest_ids.size():
		return
	
	var quest_id = quest_manager.completed_quest_ids[index]
	if quest_id in quest_manager.quest_templates:
		selected_quest = quest_manager.quest_templates[quest_id]
		_display_quest_detail(selected_quest, false)
		abandon_button.disabled = true
		
		quest_selected.emit(selected_quest)


## 显示任务详情
func _display_quest_detail(quest: QuestData, is_active: bool) -> void:
	var detail_text = ""
	
	# 标题
	detail_text += "[b][font_size=18]%s[/font_size][/b]\n\n" % quest.quest_name
	
	# 基本信息
	detail_text += "[color=gray]类型:[/color] %s\n" % quest.get_type_text()
	detail_text += "[color=gray]难度:[/color] %s\n" % quest.get_difficulty_text()
	detail_text += "[color=gray]发布者:[/color] %s\n\n" % quest.issuer_name
	
	# 描述
	detail_text += "[b]任务描述:[/b]\n%s\n\n" % quest.description
	
	# 目标和进度
	if is_active:
		detail_text += "[b]当前进度:[/b]\n"
		detail_text += "%s / %s\n\n" % [quest.progress, quest.target_count]
		
		# 进度条
		var progress_percent = float(quest.progress) / float(quest.target_count) * 100.0
		detail_text += "[color=cyan]完成度: %.1f%%[/color]\n\n" % progress_percent
		
		# 时间限制
		if quest.has_time_limit:
			var remaining = quest.get_remaining_days(quest_manager.game_time)
			var color = "orange" if remaining < 1.0 else "yellow"
			detail_text += "[color=%s]剩余时间: %.1f天[/color]\n\n" % [color, remaining]
	else:
		detail_text += "[color=green][b]任务已完成[/b][/color]\n\n"
	
	# 奖励
	detail_text += "[b]奖励:[/b]\n"
	detail_text += "[color=yellow]金币:[/color] %d\n" % quest.reward_gold
	if quest.reward_reputation > 0:
		detail_text += "[color=cyan]声望:[/color] +%d (%s)\n" % [quest.reward_reputation, quest.reward_faction]
	if quest.reward_items.size() > 0:
		detail_text += "[color=magenta]物品:[/color] %s\n" % ", ".join(quest.reward_items)
	
	quest_detail.text = detail_text


## 放弃按钮被点击
func _on_abandon_pressed() -> void:
	if not selected_quest:
		return
	
	# 确认对话框
	var confirm = ConfirmationDialog.new()
	confirm.dialog_text = "确定要放弃任务 '%s' 吗？" % selected_quest.quest_name
	confirm.confirmed.connect(func():
		quest_manager.fail_quest(selected_quest)
		quest_abandoned.emit(selected_quest)
		refresh_quest_lists()
		selected_quest = null
		quest_detail.text = "任务已放弃"
		abandon_button.disabled = true
		confirm.queue_free()
	)
	confirm.canceled.connect(func(): confirm.queue_free())
	add_child(confirm)
	confirm.popup_centered()


## 关闭按钮被点击
func _on_close_pressed() -> void:
	hide()


## 显示任务日志
func show_log() -> void:
	refresh_quest_lists()
	show()


## 任务接取时的回调
func _on_quest_accepted(_quest: QuestData):
	refresh_quest_lists()


## 任务完成时的回调
func _on_quest_completed(quest: QuestData) -> void:
	refresh_quest_lists()
	
	# 显示完成通知
	_show_completion_notification(quest)


## 任务进度更新时的回调
func _on_quest_progress_updated(quest: QuestData, _progress: int):
	# 如果当前选中的是这个任务，刷新详情
	if selected_quest and selected_quest.quest_id == quest.quest_id:
		_display_quest_detail(quest, true)
	
	# 刷新列表
	_refresh_active_quests()


## 显示任务完成通知
func _show_completion_notification(quest: QuestData) -> void:
	var notification = AcceptDialog.new()
	notification.dialog_text = "任务完成！\n\n%s\n\n奖励:\n金币: %d\n声望: +%d" % [
		quest.quest_name,
		quest.reward_gold,
		quest.reward_reputation
	]
	notification.title = "任务完成"
	notification.confirmed.connect(func(): notification.queue_free())
	add_child(notification)
	notification.popup_centered()
