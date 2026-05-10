class_name QuestBoard
extends Control

## 布告栏UI - 显示可用任务列表

signal quest_selected(quest: QuestData)
signal quest_accepted(quest: QuestData)

@onready var quest_list: ItemList = $Panel/VBoxContainer/QuestList
@onready var quest_detail: RichTextLabel = $Panel/VBoxContainer/DetailPanel/VBox/QuestDetail
@onready var accept_button: Button = $Panel/VBoxContainer/DetailPanel/VBox/AcceptButton
@onready var close_button: Button = $Panel/VBoxContainer/CloseButton

var quest_manager: QuestManager
var selected_quest: QuestData = null


func _ready() -> void:
	# 连接信号
	quest_list.item_selected.connect(_on_quest_selected)
	accept_button.pressed.connect(_on_accept_pressed)
	close_button.pressed.connect(_on_close_pressed)
	
	# 初始化
	accept_button.disabled = true
	quest_detail.text = "选择一个任务查看详情"


## 设置任务管理器
func set_quest_manager(manager: QuestManager) -> void:
	quest_manager = manager
	print("[QuestBoard] 设置任务管理器")
	if quest_manager:
		print("[QuestBoard] 可用任务数量: ", quest_manager.available_quests.size())
		print("[QuestBoard] 任务模板数量: ", quest_manager.quest_templates.size())
	refresh_quest_list()


## 刷新任务列表
func refresh_quest_list() -> void:
	if not quest_manager:
		return
	
	# 优化：只在任务数量变化时才完全重建
	var current_count = quest_manager.available_quests.size()
	if quest_list.item_count != current_count:
		_rebuild_quest_list()
	else:
		_update_quest_list()


## 完全重建列表（任务数量变化时）
func _rebuild_quest_list() -> void:
	print("[QuestBoard] 重建任务列表，任务数: ", quest_manager.available_quests.size())
	quest_list.clear()
	
	for quest in quest_manager.available_quests:
		_add_quest_item(quest)
	
	print("[QuestBoard] 列表项数量: ", quest_list.item_count)


## 更新现有列表项（任务数量未变时）
func _update_quest_list() -> void:
	for i in range(quest_manager.available_quests.size()):
		var quest = quest_manager.available_quests[i]
		_update_quest_item(i, quest)


## 添加任务项到列表
func _add_quest_item(quest: QuestData) -> void:
	# 检查是否可接取
	var can_accept = quest.can_accept(
		quest_manager.player_reputation,
		quest_manager.completed_quest_ids
	)
	
	# 构建显示文本
	var text = "[%s] %s" % [quest.get_difficulty_text(), quest.quest_name]
	if not can_accept:
		text += " (条件不足)"
	
	quest_list.add_item(text)
	
	# 设置颜色
	var index = quest_list.item_count - 1
	_set_quest_item_color(index, quest, can_accept)


## 更新任务项
func _update_quest_item(index: int, quest: QuestData) -> void:
	var can_accept = quest.can_accept(
		quest_manager.player_reputation,
		quest_manager.completed_quest_ids
	)
	
	var text = "[%s] %s" % [quest.get_difficulty_text(), quest.quest_name]
	if not can_accept:
		text += " (条件不足)"
	
	quest_list.set_item_text(index, text)
	_set_quest_item_color(index, quest, can_accept)


## 设置任务项颜色
func _set_quest_item_color(index: int, quest: QuestData, can_accept: bool) -> void:
	if not can_accept:
		quest_list.set_item_custom_fg_color(index, Color.GRAY)
	else:
		match quest.difficulty:
			QuestData.QuestDifficulty.EASY:
				quest_list.set_item_custom_fg_color(index, Color.WHITE)
			QuestData.QuestDifficulty.MEDIUM:
				quest_list.set_item_custom_fg_color(index, Color.YELLOW)
			QuestData.QuestDifficulty.HARD:
				quest_list.set_item_custom_fg_color(index, Color.ORANGE)
			QuestData.QuestDifficulty.BOSS:
				quest_list.set_item_custom_fg_color(index, Color.RED)


## 任务被选中
func _on_quest_selected(index: int) -> void:
	if index < 0 or index >= quest_manager.available_quests.size():
		return
	
	selected_quest = quest_manager.available_quests[index]
	_display_quest_detail(selected_quest)
	
	# 检查是否可接取
	var can_accept = selected_quest.can_accept(
		quest_manager.player_reputation,
		quest_manager.completed_quest_ids
	)
	accept_button.disabled = not can_accept
	
	quest_selected.emit(selected_quest)


## 显示任务详情
func _display_quest_detail(quest: QuestData) -> void:
	var detail_text = ""
	
	# 标题
	detail_text += "[b][font_size=18]%s[/font_size][/b]\n\n" % quest.quest_name
	
	# 基本信息
	detail_text += "[color=gray]类型:[/color] %s\n" % quest.get_type_text()
	detail_text += "[color=gray]难度:[/color] %s\n" % quest.get_difficulty_text()
	detail_text += "[color=gray]发布者:[/color] %s\n\n" % quest.issuer_name
	
	# 描述
	detail_text += "[b]任务描述:[/b]\n%s\n\n" % quest.description
	
	# 目标
	detail_text += "[b]目标:[/b]\n"
	detail_text += "%s (%d)\n\n" % [quest.target_description, quest.target_count]
	
	# 奖励
	detail_text += "[b]奖励:[/b]\n"
	detail_text += "[color=yellow]金币:[/color] %d\n" % quest.reward_gold
	if quest.reward_reputation > 0:
		detail_text += "[color=cyan]声望:[/color] +%d (%s)\n" % [quest.reward_reputation, quest.reward_faction]
	if quest.reward_items.size() > 0:
		detail_text += "[color=magenta]物品:[/color] %s\n" % ", ".join(quest.reward_items)
	detail_text += "\n"
	
	# 时间限制
	if quest.has_time_limit:
		detail_text += "[color=orange]时间限制:[/color] %d天\n\n" % quest.time_limit_days
	
	# 前置条件
	if quest.required_reputation > 0:
		var has_rep = quest_manager.player_reputation >= quest.required_reputation
		var color = "green" if has_rep else "red"
		detail_text += "[color=%s]需要声望:[/color] %d (当前: %d)\n" % [
			color, quest.required_reputation, quest_manager.player_reputation
		]
	
	if quest.required_quests.size() > 0:
		detail_text += "[color=gray]前置任务:[/color]\n"
		for req_quest_id in quest.required_quests:
			var completed = quest_manager.is_quest_completed(req_quest_id)
			var status = "✓" if completed else "✗"
			detail_text += "  %s %s\n" % [status, req_quest_id]
	
	quest_detail.text = detail_text


## 接取按钮被点击
func _on_accept_pressed() -> void:
	if not selected_quest:
		return
	
	if quest_manager.accept_quest(selected_quest):
		quest_accepted.emit(selected_quest)
		refresh_quest_list()
		
		# 清空选择
		selected_quest = null
		quest_detail.text = "任务已接取！"
		accept_button.disabled = true


## 关闭按钮被点击
func _on_close_pressed() -> void:
	hide()


## 显示布告栏
func show_board() -> void:
	refresh_quest_list()
	show()
