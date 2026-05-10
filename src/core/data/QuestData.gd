class_name QuestData
extends Resource

## 委托/任务数据定义
## 根据策划案 01-世界观.md 的委托系统设计

enum QuestType {
	EXTERMINATION,  # 讨伐型：清除哥布林/狼群
	ESCORT,         # 护送型：护送商队/NPC
	EXPLORATION,    # 探索型：探索遗迹/洞穴
	DEFENSE,        # 防御型：守卫村庄
	EMERGENCY       # 紧急型：特殊事件触发
}

enum QuestStatus {
	AVAILABLE,      # 可接取
	ACTIVE,         # 进行中
	COMPLETED,      # 已完成
	FAILED,         # 失败
	EXPIRED         # 过期
}

enum QuestDifficulty {
	EASY,           # 简单（入门级）
	MEDIUM,         # 中等
	HARD,           # 困难
	BOSS            # BOSS级
}

## 基础信息
@export var quest_id: String = ""
@export var quest_name: String = ""
@export_multiline var description: String = ""
@export var quest_type: QuestType = QuestType.EXTERMINATION
@export var difficulty: QuestDifficulty = QuestDifficulty.EASY

## 发布信息
@export var issuer_name: String = ""  # 发布者名称（村庄/NPC）
@export var issuer_location: Vector2i  # 发布者位置（大地图坐标）

## 目标信息
@export var target_location: Vector2i  # 目标位置（网格坐标）
## 大地图世界坐标（像素），接取任务时由 QuestManager 根据任务类型/发布位置自动生成
## 若为 Vector2.ZERO，则 accept 时会自动在发布者附近随机生成一个合理位置
@export var target_world_position: Vector2 = Vector2.ZERO
@export var target_description: String = ""  # 目标描述
@export_range(1, 100) var target_count: int = 1  # 目标数量（如击杀数）

## 奖励
@export var reward_gold: int = 0
@export var reward_items: PackedStringArray  # 物品ID列表
@export var reward_reputation: int = 0  # 声望奖励
@export var reward_faction: String = ""  # 影响的势力

## 时间限制
@export var has_time_limit: bool = false
@export var time_limit_days: int = 0  # 游戏内天数

## 前置条件
@export var required_reputation: int = 0  # 需要的声望
@export var required_quests: PackedStringArray  # 前置任务ID

## 运行时状态（不序列化）
var status: QuestStatus = QuestStatus.AVAILABLE
var progress: int = 0  # 当前进度
var accepted_time: float = 0.0  # 接取时间（游戏时间）
var completion_time: float = 0.0  # 完成时间


func _init() -> void:
	resource_name = "QuestData"


## 检查是否可接取
func can_accept(player_reputation: int, completed_quests: Array[String]) -> bool:
	# 检查声望要求
	if player_reputation < required_reputation:
		return false
	
	# 检查前置任务
	for req_quest in required_quests:
		if req_quest not in completed_quests:
			return false
	
	return status == QuestStatus.AVAILABLE


## 接取任务
func accept(current_game_time: float) -> void:
	status = QuestStatus.ACTIVE
	progress = 0
	accepted_time = current_game_time


## 更新进度
func update_progress(amount: int) -> void:
	progress = min(progress + amount, target_count)
	
	# 检查是否完成
	if progress >= target_count:
		status = QuestStatus.COMPLETED


## 检查是否过期
func check_expiration(current_game_time: float) -> bool:
	if not has_time_limit:
		return false
	
	if status != QuestStatus.ACTIVE:
		return false
	
	var elapsed_days = (current_game_time - accepted_time) / 86400.0  # 转换为天数
	if elapsed_days > time_limit_days:
		status = QuestStatus.EXPIRED
		return true
	
	return false


## 获取剩余时间（天数）
func get_remaining_days(current_game_time: float) -> float:
	if not has_time_limit:
		return -1.0
	
	var elapsed_days = (current_game_time - accepted_time) / 86400.0
	return max(0.0, time_limit_days - elapsed_days)


## 获取难度描述
func get_difficulty_text() -> String:
	match difficulty:
		QuestDifficulty.EASY:
			return "简单"
		QuestDifficulty.MEDIUM:
			return "中等"
		QuestDifficulty.HARD:
			return "困难"
		QuestDifficulty.BOSS:
			return "BOSS级"
		_:
			return "未知"


## 获取类型描述
func get_type_text() -> String:
	match quest_type:
		QuestType.EXTERMINATION:
			return "讨伐"
		QuestType.ESCORT:
			return "护送"
		QuestType.EXPLORATION:
			return "探索"
		QuestType.DEFENSE:
			return "防御"
		QuestType.EMERGENCY:
			return "紧急"
		_:
			return "未知"


## 获取进度文本
func get_progress_text() -> String:
	return "%d / %d" % [progress, target_count]


## 创建副本（用于实例化）
func duplicate_quest() -> QuestData:
	var new_quest = QuestData.new()
	
	# 复制所有属性
	new_quest.quest_id = quest_id
	new_quest.quest_name = quest_name
	new_quest.description = description
	new_quest.quest_type = quest_type
	new_quest.difficulty = difficulty
	
	new_quest.issuer_name = issuer_name
	new_quest.issuer_location = issuer_location
	
	new_quest.target_location = target_location
	new_quest.target_world_position = target_world_position
	new_quest.target_description = target_description
	new_quest.target_count = target_count
	
	new_quest.reward_gold = reward_gold
	new_quest.reward_items = reward_items  # PackedStringArray是值类型，赋值即复制
	new_quest.reward_reputation = reward_reputation
	new_quest.reward_faction = reward_faction
	
	new_quest.has_time_limit = has_time_limit
	new_quest.time_limit_days = time_limit_days
	
	new_quest.required_reputation = required_reputation
	new_quest.required_quests = required_quests  # PackedStringArray是值类型
	
	return new_quest
