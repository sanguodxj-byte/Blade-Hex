# OverworldEntityManager.gd
# AI实体行为管理器 —— 完整版
# 包含：围攻、回援、追击、逃跑、AI间战斗结算、领主决策、史诗怪物领地行为
extends Node
class_name OverworldEntityManager

var entities: Array[OverworldEntity] = []
var pois: Array[OverworldPOI] = []
var hex_grid: HexOverworldGrid
var hex_astar: HexOverworldAStar
var player_position: Vector2 = Vector2.ZERO
var _process_enabled: bool = false  ## 禁用自动 _process，由 OverworldScene 手动调用
var current_day: int = 1

## 实体事件信号
signal entity_removed(entity: OverworldEntity)
signal village_attacked(village: OverworldPOI, attacker: OverworldEntity)
signal siege_started(siege_target: OverworldPOI, attacker: OverworldEntity)
signal siege_resolved(siege_target: OverworldPOI, attacker_won: bool, attacker: OverworldEntity)
signal reinforcement_arrived(target_poi: OverworldPOI, reinforcer: OverworldEntity)
signal ai_battle_occurred(attacker: OverworldEntity, defender: OverworldEntity, attacker_won: bool)
signal poi_captured(poi: OverworldPOI, new_faction: String, captor: OverworldEntity)

## 实体间交互距离阈值（基于 HEX_SIZE=156）
const INTERACTION_DIST := 500.0  ## 两个实体触发交互的距离
const SIEGE_APPROACH_DIST := 600.0  ## 接近POI开始围攻的距离
const CHASE_SPEED_MULT := 1.1  ## 追击方速度加成

func set_hex_navigation(grid: HexOverworldGrid, astar: HexOverworldAStar):
	hex_grid = grid
	hex_astar = astar

func load_world(worldpois: Array, worldentities: Array):
	pois = worldpois
	entities = worldentities

func update_player_position(pos: Vector2):
	player_position = pos


# ================================================================
# 每帧更新：实体移动
# ================================================================

func tick_movement(delta: float):
	for entity in entities:
		if not entity.is_moving or not entity.is_alive:
			continue
		
		if entity.path.is_empty():
			entity.is_moving = false
			_on_entity_reached_destination(entity)
			continue
		
		var target_pos = entity.path[0]
		var dir = (target_pos - entity.position).normalized()
		var dist = entity.position.distance_to(target_pos)
		var speed = entity.move_speed
		# 追击加速
		if entity.ai_state == OverworldEntity.AIState.CHASING:
			speed *= CHASE_SPEED_MULT
		var step = speed * delta
		
		if step >= dist:
			entity.position = target_pos
			entity.path.pop_front()
			if entity.path.is_empty():
				entity.is_moving = false
				_on_entity_reached_destination(entity)
		else:
			entity.position += dir * step


# ================================================================
# 每日更新：所有AI决策
# ================================================================

func on_day_passed():
	current_day += 1
	
	# 第1步：更新所有POI
	for poi in pois:
		poi.on_day_passed()
	
	# 第2步：检测实体间交互（追击/碰撞/围攻接近）
	_process_entity_interactions()
	
	# 第3步：每个实体做每日决策
	var to_remove: Array[OverworldEntity] = []
	
	for entity in entities:
		if not entity.is_alive:
			to_remove.append(entity)
			continue
		
		entity.on_day_passed()
		_decide_daily_action(entity)
		
		# 掠夺队存活太久自动消亡
		if entity.entity_type == OverworldEntity.EntityType.RAIDING_PARTY and entity.days_alive > 21:
			to_remove.append(entity)
	
	# 第4步：清理死亡实体
	for entity in to_remove:
		_remove_entity(entity)
	
	# 第5步：结算所有围攻
	_process_sieges()
	
	# 第6步：领主检查是否需要回援
	_process_reinforcement_checks()
	
	# 第7步：聚落产生新掠夺队
	_spawn_new_raiding_parties()
	
	# 第8步：招募（领主在城堡时缓慢增兵）
	_process_recruitment()


# ================================================================
# 实体间交互检测
# ================================================================

func _process_entity_interactions():
	# 检测所有实体对之间的距离
	for i in range(entities.size()):
		var a: OverworldEntity = entities[i]
		if not a.is_alive: continue
		
		for j in range(i + 1, entities.size()):
			var b: OverworldEntity = entities[j]
			if not b.is_alive: continue
			
			var dist = a.position.distance_to(b.position)
			
			# 交互距离内
			if dist < INTERACTION_DIST:
				_check_entity_pair_interaction(a, b)
			
			# 视野内的敌对检测（追击决策）
			elif dist < a.vision_range:
				_check_vision_detection(a, b)
			
			if dist < b.vision_range:
				_check_vision_detection(b, a)


func _check_entity_pair_interaction(a: OverworldEntity, b: OverworldEntity):
	# 同阵营不交互
	if a.faction == b.faction: return
	# 都不对玩家敌对时可能不交互
	if not _are_hostile(a, b): return
	
	# 解算战斗
	var result = OverworldAIResolver.resolve_battle(a, b)
	ai_battle_occurred.emit(a, b, result.attacker_won)
	print("[AI战斗] %s" % result.description)
	
	# 处理战败
	if result.attacker_destroyed:
		a.is_alive = false
	if result.defender_destroyed:
		b.is_alive = false
	
	# 败方逃跑
	if not result.attacker_won and not result.attacker_destroyed:
		a.ai_state = OverworldEntity.AIState.FLEEING
		_start_move_to(a, a.home_position)
	if result.attacker_won and not result.defender_destroyed:
		b.ai_state = OverworldEntity.AIState.FLEEING
		_start_move_to(b, b.home_position)


func _check_vision_detection(detector: OverworldEntity, target: OverworldEntity):
	if not _are_hostile(detector, target): return
	if detector.ai_state in [OverworldEntity.AIState.FLEEING, OverworldEntity.AIState.RETURNING]:
		return  # 正在逃跑或返回，不追击
	
	var power_ratio = detector.evaluate_power_ratio(target)
	
	if power_ratio > 1.5:
		# 优势追击
		if detector.ai_state in [OverworldEntity.AIState.IDLE, OverworldEntity.AIState.PATROLLING]:
			detector.ai_state = OverworldEntity.AIState.CHASING
			detector.chase_target = target
			detector.target_position = target.position
			_start_move_to(detector, target.position)
	elif power_ratio < 0.7:
		# 劣势逃跑
		if detector.ai_state in [OverworldEntity.AIState.IDLE, OverworldEntity.AIState.PATROLLING]:
			detector.ai_state = OverworldEntity.AIState.FLEEING
			_start_move_to(detector, detector.home_position)


func _are_hostile(a: OverworldEntity, b: OverworldEntity) -> bool:
	# 敌对关系判定
	if a.faction == b.faction: return false
	# 永远敌对的组合
	var hostile_pairs = {
		"hostile": ["kingdom", "adventurers", "merchants", "elves", "dwarves"],
		"kingdom": ["hostile"],
		"adventurers": ["hostile"],
		"merchants": ["hostile"],
		"elves": ["hostile"],
		"dwarves": ["hostile"],
	}
	if hostile_pairs.has(a.faction) and b.faction in hostile_pairs[a.faction]:
		return true
	if hostile_pairs.has(b.faction) and a.faction in hostile_pairs[b.faction]:
		return true
	return false


# ================================================================
# 围攻系统
# ================================================================

func _process_sieges():
	var sieges_to_resolve: Array = []
	
	# 找到所有正在围攻POI的实体
	for entity in entities:
		if not entity.is_alive: continue
		if entity.ai_state != OverworldEntity.AIState.BESIEGING: continue
		if entity.siege_target == null: continue
		
		var target: OverworldPOI = entity.siege_target
		
		# 围攻时间足够长才结算（至少2天）
		if target.siege_days >= 2:
			sieges_to_resolve.append({"entity": entity, "target": target})
	
	# 结算围攻
	for siege in sieges_to_resolve:
		var entity: OverworldEntity = siege["entity"]
		var target: OverworldPOI = siege["target"]
		
		if not entity.is_alive:
			target.end_siege()
			continue
		
		var result = OverworldAIResolver.resolve_siege(entity, target)
		print("[围攻] %s" % result.description)
		
		if result.attacker_won:
			# 攻方胜：POI被攻占
			var old_faction = target.owning_faction
			target.owning_faction = entity.faction
			target.end_siege()
			siege_resolved.emit(target, true, entity)
			poi_captured.emit(target, entity.faction, entity)
			print("[围攻] %s 攻占 %s（原属%s）" % [entity.entity_name, target.poi_name, old_faction])
			
			# 攻方驻扎
			entity.ai_state = OverworldEntity.AIState.IDLE
			entity.guarded_poi = target
			entity.home_position = target.position
			entity.siege_target = null
		else:
			# 守方胜
			target.end_siege()
			siege_resolved.emit(target, false, entity)
			
			if result.attacker_destroyed:
				entity.is_alive = false
			else:
				# 攻方撤退
				entity.ai_state = OverworldEntity.AIState.FLEEING
				entity.siege_target = null
				_start_move_to(entity, entity.home_position)


# ================================================================
# 回援系统
# ================================================================

func _process_reinforcement_checks():
	# 找到所有需要回援的POI
	for poi in pois:
		if not poi.needs_reinforcement(): continue
		if poi.owning_faction != "kingdom": continue  # 只处理王国领地的回援
		
		# 找到最近的领主军队
		var nearest_lord: OverworldEntity = null
		var nearest_dist := 99999.0
		
		for entity in entities:
			if not entity.is_alive: continue
			if entity.entity_type != OverworldEntity.EntityType.LORD_ARMY: continue
			if entity.faction != "kingdom": continue
			if entity.ai_state in [OverworldEntity.AIState.BESIEGING, OverworldEntity.AIState.REINFORCING]:
				continue  # 正在围攻或已经在回援
			
			var dist = entity.position.distance_to(poi.position)
			if dist < nearest_dist:
				nearest_dist = dist
				nearest_lord = entity
		
		# 只在领主不太远时回援
		if nearest_lord and nearest_dist < 800.0:
			nearest_lord.ai_state = OverworldEntity.AIState.REINFORCING
			nearest_lord.reinforce_target = poi
			nearest_lord.target_position = poi.position
			_start_move_to(nearest_lord, poi.position)
			print("[回援] %s 前往支援 %s" % [nearest_lord.entity_name, poi.poi_name])


# ================================================================
# 招募系统
# ================================================================

func _process_recruitment():
	for entity in entities:
		if not entity.is_alive: continue
		if entity.entity_type != OverworldEntity.EntityType.LORD_ARMY: continue
		
		# 领主在己方城堡时缓慢增兵
		if entity.ai_state == OverworldEntity.AIState.IDLE and entity.guarded_poi:
			var poi = entity.guarded_poi
			if poi.poi_type == OverworldPOI.POIType.CASTLE and poi.owning_faction == entity.faction:
				entity.garrison_size = mini(entity.garrison_size + 2, 80)
				entity.combat_power = entity.garrison_size * entity.party_level * 1.5


# ================================================================
# 每日决策（每个实体）
# ================================================================

func _decide_daily_action(entity: OverworldEntity):
	match entity.entity_type:
		OverworldEntity.EntityType.ADVENTURER: _decide_adventurer(entity)
		OverworldEntity.EntityType.RAIDING_PARTY: _decide_raiding_party(entity)
		OverworldEntity.EntityType.CARAVAN: _decide_caravan(entity)
		OverworldEntity.EntityType.EPIC_MONSTER: _decide_epic_monster(entity)
		OverworldEntity.EntityType.LORD_ARMY: _decide_lord_army(entity)


## ---- 冒险者决策 ----
func _decide_adventurer(entity: OverworldEntity):
	match entity.ai_state:
		OverworldEntity.AIState.IDLE:
			var angle = randf() * TAU
			var dist = randf() * entity.patrol_radius
			var target = entity.home_position + Vector2(cos(angle) * dist, sin(angle) * dist)
			_start_move_to(entity, target)
			entity.ai_state = OverworldEntity.AIState.PATROLLING
		OverworldEntity.AIState.PATROLLING:
			if not entity.is_moving:
				entity.ai_state = OverworldEntity.AIState.IDLE
		OverworldEntity.AIState.FLEEING:
			if not entity.is_moving:
				entity.ai_state = OverworldEntity.AIState.IDLE
		OverworldEntity.AIState.CHASING:
			if entity.chase_target and is_instance_valid(entity.chase_target) and entity.chase_target.is_alive:
				entity.target_position = entity.chase_target.position
				_start_move_to(entity, entity.chase_target.position)
			else:
				entity.chase_target = null
				entity.ai_state = OverworldEntity.AIState.IDLE


## ---- 掠夺队决策 ----
func _decide_raiding_party(entity: OverworldEntity):
	match entity.ai_state:
		OverworldEntity.AIState.MOVING_TO_TARGET:
			if entity.is_moving: return
			# 到达目标 → 袭击
			_raid_settlement_or_village(entity)
			entity.ai_state = OverworldEntity.AIState.RETURNING
			_start_move_to(entity, entity.home_position)
		OverworldEntity.AIState.RETURNING:
			if entity.is_moving: return
			if entity.source_settlement:
				entity.source_settlement.on_raid_party_destroyed()
			entity.is_alive = false
		OverworldEntity.AIState.FLEEING:
			if entity.is_moving: return
			entity.ai_state = OverworldEntity.AIState.RETURNING
			_start_move_to(entity, entity.home_position)
		OverworldEntity.AIState.CHASING:
			if entity.chase_target and is_instance_valid(entity.chase_target) and entity.chase_target.is_alive:
				_start_move_to(entity, entity.chase_target.position)
			else:
				entity.chase_target = null
				# 继续向原目标前进
				entity.ai_state = OverworldEntity.AIState.MOVING_TO_TARGET

func _raid_settlement_or_village(entity: OverworldEntity):
	for poi in pois:
		if poi.position.distance_to(entity.position) < 60.0:
			if poi.poi_type in [OverworldPOI.POIType.VILLAGE, OverworldPOI.POIType.TOWN]:
				var result = OverworldAIResolver.resolve_raid(entity, poi)
				village_attacked.emit(poi, entity)
				poi.on_attacked(entity, current_day)
				print("[袭击] %s" % result.description)
				if result.raider_destroyed:
					entity.is_alive = false
				break


## ---- 商队决策 ----
func _decide_caravan(entity: OverworldEntity):
	match entity.ai_state:
		OverworldEntity.AIState.MOVING_TO_TARGET:
			if entity.is_moving: return
			if entity.destination_town:
				entity.prosperity_contribution = true
				entity.destination_town.prosperity = mini(100, entity.destination_town.prosperity + 2)
			var tmp = entity.origin_town
			entity.origin_town = entity.destination_town
			entity.destination_town = tmp
			if entity.destination_town:
				entity.target_position = entity.destination_town.position
				_start_move_to(entity, entity.target_position)
		OverworldEntity.AIState.IDLE:
			if entity.destination_town:
				entity.target_position = entity.destination_town.position
				_start_move_to(entity, entity.target_position)
				entity.ai_state = OverworldEntity.AIState.MOVING_TO_TARGET
		OverworldEntity.AIState.FLEEING:
			if entity.is_moving: return
			entity.ai_state = OverworldEntity.AIState.IDLE


## ---- 史诗怪物决策 ----
func _decide_epic_monster(entity: OverworldEntity):
	# 史诗怪物有领地意识
	# 1. 检测入侵者
	var intruder = _find_intruder_in_territory(entity)
	if intruder:
		entity.is_aggressive = true
		entity.ai_state = OverworldEntity.AIState.CHASING
		entity.chase_target = intruder
		_start_move_to(entity, intruder.position)
		return
	
	entity.is_aggressive = false
	
	match entity.ai_state:
		OverworldEntity.AIState.IDLE, OverworldEntity.AIState.PATROLLING:
			if not entity.is_in_territory(entity.position) or not entity.is_moving:
				var angle = randf() * TAU
				var dist = randf() * entity.territory_radius * 0.6
				var target = entity.territory_center + Vector2(cos(angle) * dist, sin(angle) * dist)
				_start_move_to(entity, target)
				entity.ai_state = OverworldEntity.AIState.PATROLLING
		OverworldEntity.AIState.CHASING:
			if entity.chase_target and is_instance_valid(entity.chase_target) and entity.chase_target.is_alive:
				# 追击超出领地范围则放弃
				if entity.is_in_territory(entity.chase_target.position):
					_start_move_to(entity, entity.chase_target.position)
				else:
					entity.chase_target = null
					entity.ai_state = OverworldEntity.AIState.IDLE
			else:
				entity.chase_target = null
				entity.ai_state = OverworldEntity.AIState.IDLE

func _find_intruder_in_territory(monster: OverworldEntity) -> OverworldEntity:
	for entity in entities:
		if entity == monster or not entity.is_alive: continue
		if not monster.is_in_territory(entity.position): continue
		if _are_hostile(monster, entity):
			return entity
	# 玩家也在领地内？
	if monster.is_in_territory(player_position):
		# 返回一个虚拟敌人标记（实际由OverworldScene检测）
		pass
	return null


## ---- 领主军队决策（最复杂的AI） ----
func _decide_lord_army(entity: OverworldEntity):
	# 优先级链：
	# 1. 正在回援 → 继续前进
	# 2. 正在围攻 → 检查围攻状态
	# 3. 正在追击 → 继续
	# 4. 正在逃跑 → 继续
	# 5. 检查领地是否受威胁
	# 6. 检查是否有宣战目标
	# 7. 无紧急事务 → 巡逻
	
	match entity.ai_state:
		OverworldEntity.AIState.REINFORCING:
			_decide_lord_reinforcing(entity)
			return
		OverworldEntity.AIState.BESIEGING:
			_decide_lord_besieging(entity)
			return
		OverworldEntity.AIState.FLEEING:
			if entity.is_moving: return
			entity.ai_state = OverworldEntity.AIState.IDLE
			return
		OverworldEntity.AIState.CHASING:
			_decide_lord_chasing(entity)
			return
		OverworldEntity.AIState.RECRUITING:
			_decide_lord_recruiting(entity)
			return
	
	# ===== 空闲/巡逻状态下的优先级检查 =====
	
	# 优先级1：领地受威胁 → 回防
	var threatened_poi = _find_threatened_friendly_poi(entity)
	if threatened_poi:
		entity.ai_state = OverworldEntity.AIState.REINFORCING
		entity.reinforce_target = threatened_poi
		_start_move_to(entity, threatened_poi.position)
		print("[领主] %s 回防 %s" % [entity.entity_name, threatened_poi.poi_name])
		return
	
	# 优先级2：发现可攻击的外族聚落 → 围攻
	if _should_lord_attack(entity):
		var target_settlement = _find_attack_target(entity)
		if target_settlement:
			entity.ai_state = OverworldEntity.AIState.BESIEGING
			entity.siege_target = target_settlement
			target_settlement.begin_siege(entity)
			_start_move_to(entity, target_settlement.position)
			siege_started.emit(target_settlement, entity)
			print("[领主] %s 开始围攻 %s" % [entity.entity_name, target_settlement.poi_name])
			return
	
	# 优先级3：巡逻/驻扎
	if entity.guarded_poi:
		# 有守卫目标，在附近巡逻
		var angle = randf() * TAU
		var dist = randf() * entity.patrol_radius * 0.5
		var target = entity.guarded_poi.position + Vector2(cos(angle) * dist, sin(angle) * dist)
		_start_move_to(entity, target)
		entity.ai_state = OverworldEntity.AIState.PATROLLING
	else:
		# 无守卫目标，在home附近巡逻
		if entity.ai_state == OverworldEntity.AIState.IDLE:
			var angle = randf() * TAU
			var dist = randf() * entity.patrol_radius
			var target = entity.home_position + Vector2(cos(angle) * dist, sin(angle) * dist)
			_start_move_to(entity, target)
			entity.ai_state = OverworldEntity.AIState.PATROLLING
		elif not entity.is_moving:
			entity.ai_state = OverworldEntity.AIState.IDLE

func _decide_lord_reinforcing(entity: OverworldEntity):
	if entity.is_moving: return
	if entity.reinforce_target:
		var dist = entity.position.distance_to(entity.reinforce_target.position)
		if dist < SIEGE_APPROACH_DIST:
			# 到达回援目标
			reinforcement_arrived.emit(entity.reinforce_target, entity)
			print("[回援] %s 到达 %s" % [entity.entity_name, entity.reinforce_target.poi_name])
			
			# 如果POI被围攻，与围攻方战斗
			if entity.reinforce_target.is_under_siege and entity.reinforce_target.siege_by:
				var besieger = entity.reinforce_target.siege_by
				var result = OverworldAIResolver.resolve_battle(entity, besieger)
				ai_battle_occurred.emit(entity, besieger, result.attacker_won)
				print("[回援战斗] %s" % result.description)
				
				if result.defender_destroyed:
					besieger.is_alive = false
					entity.reinforce_target.end_siege()
				elif result.attacker_destroyed:
					entity.is_alive = false
				else:
					# 双方都存活，围攻方可能撤退
					if besieger.combat_power < entity.combat_power * 0.5:
						besieger.ai_state = OverworldEntity.AIState.FLEEING
						_start_move_to(besieger, besieger.home_position)
						entity.reinforce_target.end_siege()
			
			entity.reinforce_target = null
			entity.guarded_poi = entity.reinforce_target  # 驻守这里
	entity.ai_state = OverworldEntity.AIState.IDLE

func _decide_lord_besieging(entity: OverworldEntity):
	if entity.siege_target == null or not entity.siege_target.is_under_siege:
		entity.siege_target = null
		entity.ai_state = OverworldEntity.AIState.IDLE
		return
	# 围攻中等待结算（由 _process_sieges 处理）
	# 每天不动，等围攻天数够了自动结算

func _decide_lord_chasing(entity: OverworldEntity):
	if entity.chase_target and is_instance_valid(entity.chase_target) and entity.chase_target.is_alive:
		# 追击距离太远则放弃
		if entity.position.distance_to(entity.chase_target.position) > entity.vision_range * 1.5:
			entity.chase_target = null
			entity.ai_state = OverworldEntity.AIState.IDLE
		else:
			_start_move_to(entity, entity.chase_target.position)
	else:
		entity.chase_target = null
		entity.ai_state = OverworldEntity.AIState.IDLE

func _decide_lord_recruiting(entity: OverworldEntity):
	# 招募状态在城堡时自动增兵（由 _process_recruitment 处理）
	# 招募一段时间后回到巡逻
	if entity.days_alive % 5 == 0:
		entity.ai_state = OverworldEntity.AIState.IDLE

func _should_lord_attack(entity: OverworldEntity) -> bool:
	# 性格决定攻击倾向
	match entity.lord_personality:
		OverworldPOI.LordPersonality.AGGRESSIVE:
			return entity.combat_power > 15.0
		OverworldPOI.LordPersonality.BALANCED:
			return entity.combat_power > 25.0
		OverworldPOI.LordPersonality.CAUTIOUS:
			return entity.combat_power > 40.0
		_:
			return entity.combat_power > 25.0

func _find_threatened_friendly_poi(entity: OverworldEntity) -> OverworldPOI:
	var closest: OverworldPOI = null
	var closest_dist := 99999.0
	
	for poi in pois:
		if poi.owning_faction != entity.faction: continue
		if not poi.is_under_siege and not poi.needs_reinforcement(): continue
		
		var dist = entity.position.distance_to(poi.position)
		# 性格决定回援距离
		var max_reinforce_dist = 600.0
		match entity.lord_personality:
			OverworldPOI.LordPersonality.CAUTIOUS: max_reinforce_dist = 400.0
			OverworldPOI.LordPersonality.AGGRESSIVE: max_reinforce_dist = 900.0
		
		if dist < min(closest_dist, max_reinforce_dist):
			closest_dist = dist
			closest = poi
	
	return closest

func _find_attack_target(entity: OverworldEntity) -> OverworldPOI:
	var closest: OverworldPOI = null
	var closest_dist := 99999.0
	
	for poi in pois:
		if poi.owning_faction == entity.faction: continue  # 不打自己人
		if poi.poi_type not in [OverworldPOI.POIType.SETTLEMENT, OverworldPOI.POIType.LAIR]: continue
		if poi.is_under_siege: continue  # 已被围攻的跳过
		
		var def_power = poi.get_defense_power()
		# 只攻击有把握的
		if def_power > entity.combat_power * 0.8:
			match entity.lord_personality:
				OverworldPOI.LordPersonality.CAUTIOUS:
					continue  # 谨慎型不打
				OverworldPOI.LordPersonality.AGGRESSIVE:
					if def_power > entity.combat_power * 1.2: continue
				_:
					if def_power > entity.combat_power: continue
		
		var dist = entity.position.distance_to(poi.position)
		if dist < closest_dist:
			closest_dist = dist
			closest = poi
	
	return closest


# ================================================================
# 到达目的地回调
# ================================================================

func _on_entity_reached_destination(entity: OverworldEntity):
	# 检查是否接近可围攻的POI
	if entity.ai_state == OverworldEntity.AIState.BESIEGING and entity.siege_target:
		var dist = entity.position.distance_to(entity.siege_target.position)
		if dist < SIEGE_APPROACH_DIST:
			# 确认围攻状态
			if not entity.siege_target.is_under_siege:
				entity.siege_target.begin_siege(entity)
				siege_started.emit(entity.siege_target, entity)
	
	# 检查是否到达回援目标
	if entity.ai_state == OverworldEntity.AIState.REINFORCING and entity.reinforce_target:
		pass # 将在 _decide_lord_reinforcing 中处理


# ================================================================
# 掠夺队生成
# ================================================================

func _spawn_new_raiding_parties():
	for poi in pois:
		if not poi.should_spawn_raid_party(): continue
		
		var current_count = 0
		for e in entities:
			if e.entity_type == OverworldEntity.EntityType.RAIDING_PARTY and e.source_settlement == poi:
				current_count += 1
		
		if current_count < poi.max_raiding_parties:
			var generator = WorldGenerator.new()
			generator.pois = pois
			var party = generator._create_raiding_party(poi)
			if party:
				entities.append(party)
				poi.on_raid_party_spawned()
				print("[世界] %s 产生新的掠夺队" % poi.poi_name)


# ================================================================
# 辅助方法
# ================================================================

func _start_move_to(entity: OverworldEntity, target: Vector2):
	if not hex_grid or not hex_astar: return
	var new_path = hex_astar.find_path_pixels(entity.position, target)
	if new_path.size() > 0:
		entity.path = new_path
		entity.is_moving = true
		entity.target_position = target

func _remove_entity(entity: OverworldEntity):
	entities.erase(entity)
	if entity.is_alive:
		entity.is_alive = false
	entity_removed.emit(entity)

func check_player_encounters(player_pos: Vector2) -> OverworldEntity:
	var closest: OverworldEntity = null
	var closest_dist := 700.0  # 遭遇检测距离（基于 HEX_SIZE=156）
	for entity in entities:
		if not entity.is_alive: continue
		# 排除商队（商队通过POI进入交互，不直接触发遭遇）
		if entity.entity_type == OverworldEntity.EntityType.CARAVAN: continue
		var dist = player_pos.distance_to(entity.position)
		if dist < closest_dist:
			closest_dist = dist
			closest = entity
	return closest

func check_player_poi_enter(player_pos: Vector2, is_moving: bool) -> OverworldPOI:
	# 不再限制 is_moving — 玩家在移动中接近POI也应触发
	# 检测距离从35扩大到60，补偿格子化寻路的终点偏移
	var closest: OverworldPOI = null
	var closest_dist := 60.0
	for poi in pois:
		var dist = player_pos.distance_to(poi.position)
		if dist < closest_dist:
			closest_dist = dist
			closest = poi
	return closest

func get_visible_entities(player_pos: Vector2, vision_range: float) -> Array:
	var visible: Array = []
	for entity in entities:
		if entity.is_alive and player_pos.distance_to(entity.position) <= vision_range:
			visible.append(entity)
	return visible

func get_visible_pois(player_pos: Vector2, vision_range: float) -> Array:
	var visible: Array = []
	for poi in pois:
		if player_pos.distance_to(poi.position) <= vision_range:
			visible.append(poi)
	return visible
