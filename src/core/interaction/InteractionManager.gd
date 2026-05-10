## 交互管理器
# 根据实体类型生成可用交互选项，处理玩家选择
# 是大地图交互系统的核心逻辑层

class_name InteractionManager
extends Node

## ============ 信号 ============

## 请求显示交互面板（携带选项列表和实体）
signal interaction_requested(entity, options: Array)
## 交互结束（result: "leave"/"combat"/"trade"/"dialogue"/"rest"/...）
signal interaction_completed(result: String)
## 请求进入战斗（携带 BattleContext）
signal combat_requested(battle_context)
## 请求显示对话（携带 NPCProfile）
signal dialogue_requested(npc_profile: NPCProfile)
## 请求显示交易（携带来源描述）
signal trade_requested(source_name: String)
## 请求显示城镇面板（携带 OverworldTown）
signal town_entered(town)
## 请求显示休息面板
signal rest_requested(facility_type: int)
## 请求显示训练面板
signal train_requested()
## 请求显示治疗面板
signal heal_requested()
## 请求显示竞技场
signal arena_requested()
## 请求显示委托面板
signal quest_requested()
## 请求显示修理面板
signal repair_requested()

## ============ 内部状态 ============

## 当前正在交互的实体（null表示无交互）
var _current_entity = null
## 玩家队伍引用
var player_party: OverworldParty = null
## 六边形地图引用（用于地形采样 → 战斗模板）
var hex_grid: HexOverworldGrid = null
## 当前交互是否已暂停大地图
var _is_paused: bool = false
## 上次交互的实体引用（用于防止反复触发同一实体）
var _last_interacted_entity = null
## 上次交互结束时玩家的位置（用于计算离开距离）
var _last_interaction_player_pos: Vector2 = Vector2.ZERO


## ============ 公共方法 ============

## 触发与指定实体的交互
func trigger_interaction(entity) -> void:
	if _is_paused:
		return
	# 防止短时间内重复与同一实体交互
	if entity == _last_interacted_entity and _is_in_cooldown():
		return
	
	_current_entity = entity
	_is_paused = true
	_last_interacted_entity = entity
	
	# 暂停玩家移动
	if player_party:
		player_party.is_moving = false
	
	# 根据实体类型生成交互选项
	var options = get_interaction_options(entity)
	
	# 发出信号显示交互面板
	interaction_requested.emit(entity, options)


## 根据实体类型获取交互选项
func get_interaction_options(entity) -> Array:
	if entity is OverworldEnemy:
		var enemy: OverworldEnemy = entity
		if enemy.npc_profile and enemy.npc_profile.npc_type != NPCProfile.NPCType.HOSTILE_HUMANOID:
			# 有NPC档案的人形生物
			return _build_humanoid_options(enemy.npc_profile)
		elif enemy.npc_profile and enemy.npc_profile.npc_type == NPCProfile.NPCType.HOSTILE_HUMANOID:
			# 敌对人形
			return _build_hostile_humanoid_options(enemy.npc_profile)
		else:
			# 无NPC档案的非人形生物
			return _build_nonhumanoid_options(enemy)
	elif entity is OverworldTown:
		return _build_town_options(entity)
	else:
		return [InteractionOption.create_leave()]


## 执行选中的交互选项
func execute_option(option: InteractionOption, entity = null) -> void:
	if entity == null:
		entity = _current_entity
	
	var type: int = option.interaction_type
	
	match type:
		# InteractionType.Type.ATTACK = 0
		0:
			_handle_attack(entity)
		# InteractionType.Type.TALK = 1
		1:
			_handle_talk(entity)
		# InteractionType.Type.TRADE = 2
		2:
			_handle_trade(entity)
		# InteractionType.Type.LEAVE = 3
		3:
			_handle_leave()
		# InteractionType.Type.RECRUIT = 4
		4:
			_handle_recruit(entity)
		# InteractionType.Type.DUEL = 5
		5:
			_handle_duel(entity)
		# InteractionType.Type.ESCORT = 6
		6:
			_handle_escort(entity)
		# InteractionType.Type.INFORMATION = 7
		7:
			_handle_information(entity)
		# InteractionType.Type.BOUNTY = 8
		8:
			_handle_bounty(entity)
		# InteractionType.Type.REST = 9
		9:
			rest_requested.emit(TownFacility.FacilityType.TAVERN)
		# InteractionType.Type.TRAIN = 10
		10:
			train_requested.emit()
		# InteractionType.Type.REPAIR = 11
		11:
			repair_requested.emit()
		# InteractionType.Type.HEAL = 12
		12:
			heal_requested.emit()
		# InteractionType.Type.QUEST = 13
		13:
			quest_requested.emit()
		# InteractionType.Type.ARENA = 14
		14:
			arena_requested.emit()
		_:
			_handle_leave()


## 结束交互，恢复大地图控制
func end_interaction() -> void:
	# 记录玩家当前位置，用于冷却检测
	if player_party:
		_last_interaction_player_pos = player_party.position
	_is_paused = false
	_current_entity = null


## 检查是否还在冷却期（玩家还没离开上次交互的实体足够远）
func _is_in_cooldown() -> bool:
	if not player_party or _last_interacted_entity == null:
		return false
	var entity_pos = _last_interacted_entity.position if _last_interacted_entity is Node2D else _last_interaction_player_pos
	var dist = player_party.position.distance_to(entity_pos)
	# 玩家需要离开至少80像素才能再次与同一实体交互
	return dist < 80.0


## ============ 选项构建（私有） ============

## 非人形生物：袭击/离开
func _build_nonhumanoid_options(_enemy: OverworldEnemy):
	var options: Array = []
	options.append(InteractionOption.create_attack())
	options.append(InteractionOption.create_leave())
	return options


## 友好/中立人形NPC：交谈/交易/袭击/离开 + 特殊选项
func _build_humanoid_options(profile: NPCProfile) -> Array:
	var options: Array = []
	
	# 基础选项
	options.append(InteractionOption.create_talk())
	
	# 商队和冒险者可以交易
	if profile.npc_type == NPCProfile.NPCType.MERCHANT or profile.npc_type == NPCProfile.NPCType.ADVENTURER:
		options.append(InteractionOption.create_trade())
	
	# 根据NPC类型添加特殊选项
	match profile.npc_type:
		NPCProfile.NPCType.ADVENTURER:
			options.append(InteractionOption.create_recruit())
			options.append(InteractionOption.create_information())
		NPCProfile.NPCType.MERCHANT:
			options.append(InteractionOption.create_escort())
		NPCProfile.NPCType.TRAVELER:
			options.append(InteractionOption.create_information())
		NPCProfile.NPCType.WANDERING_KNIGHT:
			options.append(InteractionOption.create_duel())
		NPCProfile.NPCType.BOUNTY_TARGET:
			options.append(InteractionOption.create_bounty())
	
	# 袭击（有声望惩罚提示）
	var attack_opt := InteractionOption.create_attack()
	attack_opt.tooltip = "袭击人形NPC会降低声望"
	options.append(attack_opt)
	
	# 离开
	options.append(InteractionOption.create_leave())
	
	return options


## 敌对人形：交谈/袭击/离开
func _build_hostile_humanoid_options(_profile: NPCProfile):
	var options: Array = []
	options.append(InteractionOption.create_talk())
	options.append(InteractionOption.create_attack())
	options.append(InteractionOption.create_leave())
	return options


## 城镇：设施列表 + 离开
func _build_town_options(town: OverworldTown) -> Array:
	var options: Array = []
	
	for facility in town.facilities:
		if facility.is_available:
			var opt := InteractionOption.new(
				TownFacility.get_type_name(facility.facility_type).to_lower(),
				facility.facility_name,
				facility.interaction_type,
				facility.description
			)
			opt.icon_name = TownFacility.get_type_icon(facility.facility_type)
			opt.metadata = {"facility_type": facility.facility_type}
			options.append(opt)
	
	# 离开城镇
	options.append(InteractionOption.create_leave())
	return options


## ============ 选项执行处理（私有） ============

## 处理袭击
func _handle_attack(entity) -> void:
	if entity is OverworldEnemy and hex_grid:
		var terrain_type := hex_grid.sample_terrain_at_pixel(entity.position.x, entity.position.y)
		var ctx = BattleContext.create(terrain_type, BattleMapGenerator.BattleSize.MERCENARY, BattleContext.EngagementType.NORMAL)
		ctx.encounter_position = Vector2i(int(entity.position.x), int(entity.position.y))
		combat_requested.emit(ctx)
	else:
		interaction_completed.emit("attack_no_target")


## 处理交谈
func _handle_talk(entity) -> void:
	if entity is OverworldEnemy and entity.npc_profile:
		dialogue_requested.emit(entity.npc_profile)
	else:
		interaction_completed.emit("talk_failed")


## 处理交易
func _handle_trade(entity) -> void:
	var source_name = "未知"
	if entity is OverworldEnemy and entity.npc_profile:
		source_name = entity.npc_profile.npc_name
	elif entity is OverworldTown:
		source_name = entity.town_name
	trade_requested.emit(source_name)


## 处理离开
func _handle_leave() -> void:
	end_interaction()
	interaction_completed.emit("leave")


## 处理招募
func _handle_recruit(entity) -> void:
	if entity is OverworldEnemy and entity.npc_profile:
		# 简化实现：直接显示对话来处理招募
		dialogue_requested.emit(entity.npc_profile)
	else:
		interaction_completed.emit("recruit_failed")


## 处理决斗
func _handle_duel(entity) -> void:
	if entity is OverworldEnemy and hex_grid:
		var terrain_type := hex_grid.sample_terrain_at_pixel(entity.position.x, entity.position.y)
		var ctx = BattleContext.create(terrain_type, BattleMapGenerator.BattleSize.MERCENARY, BattleContext.EngagementType.NORMAL)
		ctx.encounter_position = Vector2i(int(entity.position.x), int(entity.position.y))
		combat_requested.emit(ctx)
	else:
		interaction_completed.emit("duel_failed")


## 处理护送
func _handle_escort(entity) -> void:
	if entity is OverworldEnemy and entity.npc_profile:
		dialogue_requested.emit(entity.npc_profile)
	else:
		interaction_completed.emit("escort_failed")


## 处理打听情报
func _handle_information(entity) -> void:
	if entity is OverworldEnemy and entity.npc_profile:
		dialogue_requested.emit(entity.npc_profile)
	else:
		interaction_completed.emit("info_failed")


## 处理缉拿
func _handle_bounty(entity) -> void:
	if entity is OverworldEnemy and hex_grid:
		var terrain_type := hex_grid.sample_terrain_at_pixel(entity.position.x, entity.position.y)
		var ctx = BattleContext.create(terrain_type, BattleMapGenerator.BattleSize.MERCENARY, BattleContext.EngagementType.NORMAL)
		ctx.encounter_position = Vector2i(int(entity.position.x), int(entity.position.y))
		combat_requested.emit(ctx)
	else:
		interaction_completed.emit("bounty_failed")
