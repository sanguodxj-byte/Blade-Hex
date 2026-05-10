class_name QuestManager
extends Node

## 委托管理器 - 单例
## 管理所有任务的状态、进度和奖励发放

signal quest_accepted(quest: QuestData)
signal quest_completed(quest: QuestData)
signal quest_failed(quest: QuestData)
signal quest_expired(quest: QuestData)
signal quest_progress_updated(quest: QuestData, progress: int)

## 任务目标点生成信号 — OverworldScene 监听此信号来渲染目标点
signal quest_target_spawned(target_site: QuestTargetSite)
## 任务目标点清理信号
signal quest_target_cleared(quest_id: String)

## 任务模板库（从资源加载）
var quest_templates: Dictionary = {}  # quest_id -> QuestData

## 当前可用任务（布告栏）
var available_quests: Array[QuestData] = []

## 玩家已接取的任务
var active_quests: Array[QuestData] = []

## 已完成的任务ID列表
var completed_quest_ids: Array[String] = []

## 活跃的任务目标点（quest_id → QuestTargetSite）
var active_target_sites: Dictionary = {}

## 游戏时间（秒）
var game_time: float = 0.0

## 玩家数据引用（需要外部设置）
var player_reputation: int = 0
var player_gold: int = 0

## 任务过期检查定时器
var _expiration_check_timer: Timer


func _ready() -> void:
	# 加载任务模板
	_load_quest_templates()
	
	# 生成初始任务
	_generate_initial_quests()
	
	# 设置过期检查定时器（每5秒检查一次）
	_expiration_check_timer = Timer.new()
	_expiration_check_timer.wait_time = 5.0
	_expiration_check_timer.timeout.connect(_check_quest_expiration)
	_expiration_check_timer.autostart = true
	add_child(_expiration_check_timer)


func _process(delta: float) -> void:
	# 更新游戏时间
	game_time += delta


## 加载任务模板
func _load_quest_templates() -> void:
	# TODO: 从资源文件夹加载所有任务模板
	# 这里先创建一些示例任务
	_create_sample_quests()


## 创建示例任务（用于测试）
func _create_sample_quests() -> void:
	# 示例1：哥布林讨伐
	var quest1 = QuestData.new()
	quest1.quest_id = "goblin_extermination_01"
	quest1.quest_name = "清除哥布林"
	quest1.description = "村庄附近的哥布林营地威胁到了村民的安全，需要清除至少8只哥布林。"
	quest1.quest_type = QuestData.QuestType.EXTERMINATION
	quest1.difficulty = QuestData.QuestDifficulty.EASY
	quest1.issuer_name = "绿谷村"
	quest1.issuer_location = Vector2i(2800, 2200)  # 中央平原偏西
	quest1.target_description = "哥布林营地"
	quest1.target_count = 8
	quest1.reward_gold = 150
	quest1.reward_reputation = 5
	quest1.reward_faction = "绿谷村"
	quest_templates[quest1.quest_id] = quest1
	
	# 示例2：商队护送
	var quest2 = QuestData.new()
	quest2.quest_id = "escort_caravan_01"
	quest2.quest_name = "护送商队"
	quest2.description = "护送商队从绿谷村前往银月城，路上可能遭遇强盗。"
	quest2.quest_type = QuestData.QuestType.ESCORT
	quest2.difficulty = QuestData.QuestDifficulty.MEDIUM
	quest2.issuer_name = "商人吉尔伯特"
	quest2.issuer_location = Vector2i(2600, 2000)  # 中央平原
	quest2.target_description = "银月城"
	quest2.target_count = 1
	quest2.reward_gold = 300
	quest2.reward_reputation = 10
	quest2.has_time_limit = true
	quest2.time_limit_days = 5
	quest_templates[quest2.quest_id] = quest2
	
	# 示例3：遗迹探索
	var quest3 = QuestData.new()
	quest3.quest_id = "explore_ruins_01"
	quest3.quest_name = "探索古代遗迹"
	quest3.description = "探索森林深处的古代遗迹，寻找失落的魔法卷轴。"
	quest3.quest_type = QuestData.QuestType.EXPLORATION
	quest3.difficulty = QuestData.QuestDifficulty.HARD
	quest3.issuer_name = "银塔法师会"
	quest3.issuer_location = Vector2i(3000, 1800)  # 中央平原中部
	quest3.target_description = "暗影森林遗迹"
	quest3.target_count = 1
	quest3.reward_gold = 500
	quest3.reward_items = PackedStringArray(["scroll_fireball", "scroll_shield"])
	quest3.reward_reputation = 15
	quest3.required_reputation = 20
	quest_templates[quest3.quest_id] = quest3
	
	# 示例4：村庄防御
	var quest4 = QuestData.new()
	quest4.quest_id = "defend_village_01"
	quest4.quest_name = "防御村庄"
	quest4.description = "牛头人战团正在逼近村庄！守住村庄直到援军到达。"
	quest4.quest_type = QuestData.QuestType.DEFENSE
	quest4.difficulty = QuestData.QuestDifficulty.BOSS
	quest4.issuer_name = "绿谷村长"
	quest4.issuer_location = Vector2i(2800, 2200)  # 中央平原偏西
	quest4.target_description = "绿谷村"
	quest4.target_count = 5  # 守住5波
	quest4.reward_gold = 800
	quest4.reward_reputation = 30
	quest4.has_time_limit = true
	quest4.time_limit_days = 2
	quest_templates[quest4.quest_id] = quest4


## 生成初始任务
func _generate_initial_quests() -> void:
	print("[QuestManager] 生成初始任务...")
	print("[QuestManager] 任务模板数量: ", quest_templates.size())
	
	# 将所有模板任务添加到可用列表
	for quest_id in quest_templates:
		var quest = quest_templates[quest_id].duplicate_quest()
		available_quests.append(quest)
		print("[QuestManager] 添加任务: ", quest.quest_name)
	
	print("[QuestManager] 可用任务总数: ", available_quests.size())


## 接取任务
func accept_quest(quest: QuestData) -> bool:
	if not quest.can_accept(player_reputation, completed_quest_ids):
		push_warning("无法接取任务: %s - 不满足条件" % quest.quest_name)
		return false
	
	# 从可用列表移除
	var index = available_quests.find(quest)
	if index >= 0:
		available_quests.remove_at(index)
	
	# 添加到活跃列表
	quest.accept(game_time)
	active_quests.append(quest)
	
	# 在大地图上生成任务目标点（建筑瓦片/敌方配置）
	_spawn_quest_target(quest)
	
	quest_accepted.emit(quest)
	return true


## 更新任务进度
func update_quest_progress(quest_id: String, amount: int = 1) -> void:
	for quest in active_quests:
		if quest.quest_id == quest_id:
			var _old_progress = quest.progress
			quest.update_progress(amount)
			
			quest_progress_updated.emit(quest, quest.progress)
			
			# 检查是否完成
			if quest.status == QuestData.QuestStatus.COMPLETED:
				_complete_quest(quest)
			
			break


## 完成任务
func _complete_quest(quest: QuestData) -> void:
	quest.completion_time = game_time
	
	# 发放奖励
	_grant_rewards(quest)
	
	# 清理任务目标点
	_clear_quest_target(quest.quest_id)
	
	# 从活跃列表移除
	var index = active_quests.find(quest)
	if index >= 0:
		active_quests.remove_at(index)
	
	# 添加到已完成列表
	completed_quest_ids.append(quest.quest_id)
	
	quest_completed.emit(quest)


## 任务失败
func fail_quest(quest: QuestData) -> void:
	quest.status = QuestData.QuestStatus.FAILED
	
	# 清理任务目标点
	_clear_quest_target(quest.quest_id)
	
	# 从活跃列表移除
	var index = active_quests.find(quest)
	if index >= 0:
		active_quests.remove_at(index)
	
	# 可以选择重新添加到可用列表
	# available_quests.append(quest)
	
	quest_failed.emit(quest)


## 检查任务过期
func _check_quest_expiration() -> void:
	# 优化：只检查有时间限制的任务
	var expired_quests: Array[QuestData] = []
	
	for quest in active_quests:
		if quest.has_time_limit and quest.check_expiration(game_time):
			expired_quests.append(quest)
	
	# 处理过期任务
	for quest in expired_quests:
		var index = active_quests.find(quest)
		if index >= 0:
			active_quests.remove_at(index)
		
		# 清理任务目标点
		_clear_quest_target(quest.quest_id)
		
		quest_expired.emit(quest)


## 发放奖励
func _grant_rewards(quest: QuestData) -> void:
	# 金币奖励
	player_gold += quest.reward_gold
	
	# 声望奖励
	player_reputation += quest.reward_reputation
	
	# 物品奖励
	for item_id in quest.reward_items:
		# TODO: 添加到玩家背包
		print("获得物品: ", item_id)
	
	print("任务完成奖励: +%d金币, +%d声望" % [quest.reward_gold, quest.reward_reputation])


## 获取指定类型的可用任务
func get_available_quests_by_type(quest_type: QuestData.QuestType) -> Array[QuestData]:
	var result: Array[QuestData] = []
	for quest in available_quests:
		if quest.quest_type == quest_type:
			result.append(quest)
	return result


## 获取指定难度的可用任务
func get_available_quests_by_difficulty(difficulty: QuestData.QuestDifficulty) -> Array[QuestData]:
	var result: Array[QuestData] = []
	for quest in available_quests:
		if quest.difficulty == difficulty:
			result.append(quest)
	return result


## 根据ID查找任务
func find_quest_by_id(quest_id: String) -> QuestData:
	# 在活跃任务中查找
	for quest in active_quests:
		if quest.quest_id == quest_id:
			return quest
	
	# 在可用任务中查找
	for quest in available_quests:
		if quest.quest_id == quest_id:
			return quest
	
	return null


## 检查任务是否已完成
func is_quest_completed(quest_id: String) -> bool:
	return quest_id in completed_quest_ids


## 保存任务数据
func save_quest_data() -> Dictionary:
	var data = {
		"game_time": game_time,
		"player_reputation": player_reputation,
		"player_gold": player_gold,
		"completed_quest_ids": completed_quest_ids,
		"active_quests": [],
		"available_quests": [],
		"active_target_sites": {},
	}
	
	# 保存活跃任务
	for quest in active_quests:
		data["active_quests"].append({
			"quest_id": quest.quest_id,
			"progress": quest.progress,
			"accepted_time": quest.accepted_time,
			"status": quest.status
		})
	
	# 保存可用任务
	for quest in available_quests:
		data["available_quests"].append(quest.quest_id)
	
	# 保存活跃目标点
	for quest_id in active_target_sites:
		var site: QuestTargetSite = active_target_sites[quest_id]
		data["active_target_sites"][quest_id] = site.serialize()
	
	return data


## 加载任务数据
func load_quest_data(data: Dictionary) -> void:
	game_time = data.get("game_time", 0.0)
	player_reputation = data.get("player_reputation", 0)
	player_gold = data.get("player_gold", 0)
	completed_quest_ids = data.get("completed_quest_ids", [])
	
	# 清空当前任务
	active_quests.clear()
	available_quests.clear()
	
	# 恢复活跃任务
	for quest_data in data.get("active_quests", []):
		var quest_id = quest_data["quest_id"]
		if quest_id in quest_templates:
			var quest = quest_templates[quest_id].duplicate_quest()
			quest.progress = quest_data["progress"]
			quest.accepted_time = quest_data["accepted_time"]
			quest.status = quest_data["status"]
			active_quests.append(quest)
	
	# 恢复可用任务
	for quest_id in data.get("available_quests", []):
		if quest_id in quest_templates:
			var quest = quest_templates[quest_id].duplicate_quest()
			available_quests.append(quest)
	
	# 恢复活跃目标点（需要重新发射信号让 OverworldScene 渲染）
	active_target_sites.clear()
	var saved_sites: Dictionary = data.get("active_target_sites", {})
	for quest_id in saved_sites:
		var site := QuestTargetSite.deserialize(saved_sites[quest_id])
		if not site.is_cleared:
			active_target_sites[quest_id] = site
			quest_target_spawned.emit(site)


## ========================================
## 任务目标点生成/清理
## ========================================

## 生成任务目标点（接取任务时调用）
func _spawn_quest_target(quest: QuestData) -> void:
	# 如果任务没有设置世界坐标，根据发布者位置自动生成
	if quest.target_world_position == Vector2.ZERO:
		quest.target_world_position = _generate_target_position(quest)
	
	# 创建目标点数据
	var site := QuestTargetSite.create_from_quest(quest)
	active_target_sites[quest.quest_id] = site
	
	# 通知 OverworldScene 渲染目标点
	quest_target_spawned.emit(site)
	print("[QuestManager] 生成任务目标点: %s (%s) 于 (%d, %d)" % [
		site.site_name, site.get_site_type_name(),
		int(site.world_position.x), int(site.world_position.y)
	])


## 清理任务目标点（完成/失败/过期时调用）
func _clear_quest_target(quest_id: String) -> void:
	if active_target_sites.has(quest_id):
		active_target_sites.erase(quest_id)
		quest_target_cleared.emit(quest_id)


## 获取指定任务的目标点
func get_target_site(quest_id: String) -> QuestTargetSite:
	return active_target_sites.get(quest_id, null)


## 获取所有活跃目标点
func get_all_target_sites() -> Array[QuestTargetSite]:
	var sites: Array[QuestTargetSite] = []
	for site in active_target_sites.values():
		sites.append(site)
	return sites


## 根据任务信息自动生成一个合理的目标世界坐标
## 在发布者附近一定距离内随机选取，避免落在水域或已有POI上
func _generate_target_position(quest: QuestData) -> Vector2:
	# 基于发布者位置偏移
	var issuer_px := Vector2(float(quest.issuer_location.x), float(quest.issuer_location.y))
	
	# 不同任务类型的距离偏好
	var min_dist := 200.0
	var max_dist := 600.0
	match quest.quest_type:
		QuestData.QuestType.EXPLORATION:
			max_dist = 800.0  # 探索型目标更远
		QuestData.QuestType.ESCORT:
			max_dist = 500.0
		QuestData.QuestType.DEFENSE:
			min_dist = 0.0    # 防御型就在发布者位置
			max_dist = 100.0
		_:
			pass
	
	# 如果发布者坐标也为零（占位任务），随机分配到地图中部
	if issuer_px == Vector2.ZERO:
		issuer_px = Vector2(3072.0, 2048.0)  # 6144×4096 的中心
	
	# 多次尝试找一个合法位置
	for attempt in range(30):
		var angle := randf() * TAU
		var dist := randf() * (max_dist - min_dist) + min_dist
		var candidate := issuer_px + Vector2(cos(angle) * dist, sin(angle) * dist)
		# 简单边界检查
		if candidate.x > 80.0 and candidate.x < 6064.0 and \
		   candidate.y > 80.0 and candidate.y < 4016.0:
			return candidate
	
	# 回退：返回发布者附近
	return issuer_px + Vector2(randf() * 200.0 - 100.0, randf() * 200.0 - 100.0)
