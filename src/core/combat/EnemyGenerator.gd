# EnemyGenerator.gd
# 敌方单位生成器 — 120级等级体系
# 等级驱动属性，每级+1属性点，HP=base_hp+CON修正×等级
# CR = floor(level/6)，可由模板覆盖
class_name EnemyGenerator


# ============================================================================
# 核心：从模板ID生成敌人实例
# ============================================================================

## 从模板ID生成敌人实例
## template_id: 敌人模板ID（对应 UnitTemplateDB 中的 template_id）
## level_adjustment: 等级偏移（±N级，用于缩放敌人强度）
## 返回: UnitData 实例
static func generate_enemy(template_id: String, level_adjustment: int = 0) -> UnitData:
	# 在 UnitTemplateDB 中查找模板
	var tpl = UnitTemplateDB.get_template_by_id(template_id)
	
	# 回退：在 PrototypeData 中查找旧式模板
	if tpl.is_empty():
		var proto_enemies = PrototypeData.get_enemies()
		var proto_extended = PrototypeData.get_extended_enemies()
		if proto_enemies.has(template_id):
			var enemy = _deep_copy_unit_data(proto_enemies[template_id])
			if level_adjustment != 0:
				_apply_level_scaling(enemy, level_adjustment)
			return enemy
		elif proto_extended.has(template_id):
			var enemy = _deep_copy_unit_data(proto_extended[template_id])
			if level_adjustment != 0:
				_apply_level_scaling(enemy, level_adjustment)
			return enemy
		else:
			push_error("敌人模板不存在: " + template_id)
			return null
	
	# 从等级制模板实例化
	var enemy = UnitTemplateDB.instantiate_template(tpl)
	
	# 应用等级偏移
	if level_adjustment != 0:
		_apply_level_scaling(enemy, level_adjustment)
	
	# 为类人敌人生成随机装备
	if enemy.enemy_type == UnitData.EnemyType.HUMANOID:
		_equip_random_gear(enemy)
	
	return enemy


## 从模板字典直接生成敌人（用于编程式调用）
static func generate_from_template(tpl: Dictionary, level_adjustment: int = 0) -> UnitData:
	var enemy = UnitTemplateDB.instantiate_template(tpl)
	if level_adjustment != 0:
		_apply_level_scaling(enemy, level_adjustment)
	if enemy.enemy_type == UnitData.EnemyType.HUMANOID:
		_equip_random_gear(enemy)
	return enemy


# ============================================================================
# 等级范围生成
# ============================================================================

## 根据等级范围随机生成敌人
## min_level: 最小等级
## max_level: 最大等级
## enemy_types: 限定敌人类型（空=所有类型）
static func generate_random_enemy_by_level(min_level: int, max_level: int, enemy_types: Array[int] = []) -> UnitData:
	var candidates: Array[Dictionary] = []
	for tpl in UnitTemplateDB.get_all_templates():
		if tpl["level"] >= min_level and tpl["level"] <= max_level:
			if enemy_types.is_empty() or enemy_types.has(tpl["enemy_type"]):
				candidates.append(tpl)
	
	if candidates.is_empty():
		push_error("没有符合条件的敌人模板: Lv %d-%d" % [min_level, max_level])
		return null
	
	var tpl = candidates[randi() % candidates.size()]
	var enemy = UnitTemplateDB.instantiate_template(tpl)
	if enemy.enemy_type == UnitData.EnemyType.HUMANOID:
		_equip_random_gear(enemy)
	return enemy


## 根据CR范围随机生成敌人（向后兼容）
## min_cr: 最小CR
## max_cr: 最大CR
## enemy_types: 限定敌人类型（空=所有类型）
static func generate_random_enemy(min_cr: float, max_cr: float, enemy_types: Array[int] = []) -> UnitData:
	var min_level = RPGRuleEngine.get_level_from_cr(min_cr)
	var max_level = RPGRuleEngine.get_level_from_cr(max_cr)
	return generate_random_enemy_by_level(min_level, max_level, enemy_types)


## 生成敌人小队
static func generate_enemy_squad(template_ids: Array[String], count: int = 1) -> Array[UnitData]:
	var squad: Array[UnitData] = []
	for template_id in template_ids:
		for i in range(count):
			var enemy = generate_enemy(template_id)
			if enemy:
				squad.append(enemy)
	return squad


# ============================================================================
# 遭遇生成（适配120级）
# ============================================================================

## 根据遭遇难度生成敌人组
## party_level: 玩家队伍平均等级
## party_size: 玩家队伍人数
## difficulty: 难度系数（0.5=简单, 1.0=标准, 1.5=困难, 2.0=致命）
static func generate_encounter(party_level: int, party_size: int, difficulty: float = 1.0) -> Array[UnitData]:
	var encounter: Array[UnitData] = []
	
	# 计算目标等级预算
	var level_budget = _calculate_level_budget(party_level, party_size, difficulty)
	
	# 收集候选模板
	var candidates: Array[Dictionary] = []
	for tpl in UnitTemplateDB.get_all_templates():
		if tpl["level"] <= level_budget * 1.2:  # 允许20%超出
			candidates.append(tpl)
	
	if candidates.is_empty():
		push_error("没有符合条件的敌人模板")
		return encounter
	
	# 随机组合敌人直到达到预算
	var current_level = 0
	var attempts = 0
	while current_level < level_budget and not candidates.is_empty() and attempts < 20:
		var tpl = candidates[randi() % candidates.size()]
		var enemy_level = tpl["level"]
		if current_level + enemy_level <= level_budget * 1.2:
			var enemy = UnitTemplateDB.instantiate_template(tpl)
			if enemy:
				if enemy.enemy_type == UnitData.EnemyType.HUMANOID:
					_equip_random_gear(enemy)
				encounter.append(enemy)
				current_level += enemy_level
		else:
			# 移除太贵的敌人
			candidates.erase(tpl)
		attempts += 1
	
	return encounter


## 计算遭遇等级预算
## 基于队伍等级×人数×难度系数
static func _calculate_level_budget(party_level: int, party_size: int, difficulty: float) -> int:
	# 基础预算 = 队伍等级 × 人数 × 0.8
	# 这意味着标准难度下，4个30级角色面对约96级的敌人（分散为多个）
	var base_budget = party_level * party_size * 0.8
	return int(base_budget * difficulty)


# ============================================================================
# 装备生成
# ============================================================================

## 为敌人装备随机装备
static func _equip_random_gear(enemy: UnitData) -> void:
	var level = enemy.level
	var cr = enemy.threat_level
	var item_level = EquipmentGenerator.get_item_level_from_cr(cr) if cr > 0 else level
	var difficulty = EquipmentGenerator.get_difficulty_from_cr(cr) if cr > 0 else 0.5
	
	# 如果敌人已有武器，保留（模板预设）
	if not enemy.primary_main_hand:
		var weapon_roll = randf()
		if weapon_roll < 0.6:
			enemy.primary_main_hand = EquipmentGenerator.generate_random_weapon(
				["longsword", "greatsword", "spear", "greataxe", "warhammer"],
				-1, item_level, difficulty
			)
		else:
			enemy.primary_main_hand = EquipmentGenerator.generate_random_weapon(
				["longbow", "crossbow", "shortbow"],
				-1, item_level, difficulty
			)
	
	# 生成防具（概率）
	if randf() < 0.7:
		enemy.armor = EquipmentGenerator.generate_random_armor([], -1, item_level, difficulty)
	
	# 生成盾牌（概率，仅近战）
	if enemy.primary_main_hand and not enemy.primary_main_hand.is_two_handed and randf() < 0.3:
		enemy.shield = EquipmentGenerator.generate_random_shield(-1, item_level, difficulty)
	
	# 生成饰品（概率）
	if randf() < 0.4:
		enemy.accessory_1 = EquipmentGenerator.generate_random_accessory(-1, item_level, difficulty)
	if randf() < 0.2:
		enemy.accessory_2 = EquipmentGenerator.generate_random_accessory(-1, item_level, difficulty)
	
	# 刷新饰品加成
	enemy._refresh_accessory_bonuses()


# ============================================================================
# 等级缩放（120级体系核心）
# 不再用百分比缩放，而是真正改变等级并重算属性
# ============================================================================

## 应用等级偏移（重新计算属性）
static func _apply_level_scaling(enemy: UnitData, level_adjustment: int) -> void:
	if level_adjustment == 0:
		return
	
	var old_level = enemy.level
	var new_level = clampi(old_level + level_adjustment, 1, RPGRuleEngine.MAX_LEVEL)
	
	if new_level == old_level:
		return
	
	# 计算属性缩放比例
	var old_points = RPGRuleEngine.get_total_attr_points(old_level)
	var new_points = RPGRuleEngine.get_total_attr_points(new_level)
	
	if old_points <= 0:
		return
	
	var scale = float(new_points) / float(old_points)
	
	# 缩放属性（保持分配比例）
	enemy.str = clampi(int(enemy.str * scale), RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
	enemy.dex = clampi(int(enemy.dex * scale), RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
	enemy.con = clampi(int(enemy.con * scale), RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
	enemy.intel = clampi(int(enemy.intel * scale), RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
	enemy.wis = clampi(int(enemy.wis * scale), RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
	enemy.cha = clampi(int(enemy.cha * scale), RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
	
	# 重算HP（base_hp + CON修正×新等级）
	var con_mod = RPGRuleEngine.get_stat_modifier(enemy.con)
	var base_hp = maxi(1, enemy.base_max_hp - RPGRuleEngine.get_stat_modifier(enemy.con) * old_level)
	enemy.base_max_hp = maxi(1, base_hp + con_mod * new_level)
	
	# AC微调（每升一级+0.5，降级同理）
	enemy.base_ac = clampi(enemy.base_ac + int(level_adjustment * 0.5), 8, 30)
	
	# 更新等级和CR
	enemy.level = new_level
	enemy.threat_level = RPGRuleEngine.get_cr_from_level(new_level)


# ============================================================================
# 工具方法
# ============================================================================

## 深拷贝UnitData
static func _deep_copy_unit_data(src: UnitData) -> UnitData:
	var copy = UnitData.new()
	
	# 基础属性
	copy.unit_name = src.unit_name
	copy.level = src.level
	copy.str = src.str
	copy.dex = src.dex
	copy.con = src.con
	copy.intel = src.intel
	copy.wis = src.wis
	copy.cha = src.cha
	
	# 战斗属性
	copy.base_max_hp = src.base_max_hp
	copy.base_ac = src.base_ac
	copy.base_ap = src.base_ap
	copy.base_move_range = src.base_move_range
	copy.base_initiative = src.base_initiative
	
	# 敌方属性
	copy.is_enemy = src.is_enemy
	copy.enemy_type = src.enemy_type
	copy.creature_size = src.creature_size
	copy.threat_level = src.threat_level
	copy.ai_strategy = src.ai_strategy
	copy.morale = src.morale
	copy.enemy_template_id = src.enemy_template_id
	
	# 免疫/抗性/弱点
	copy.immunities = src.immunities.duplicate()
	copy.resistances = src.resistances.duplicate()
	copy.weaknesses = src.weaknesses.duplicate()
	copy.traits = src.traits.duplicate()
	
	# 装甲
	copy.natural_dr = src.natural_dr
	copy.natural_dr_threshold = src.natural_dr_threshold
	
	# 装备（深拷贝）
	if src.primary_main_hand:
		copy.primary_main_hand = EquipmentGenerator._copy_weapon(src.primary_main_hand)
	if src.armor:
		copy.armor = EquipmentGenerator._copy_armor(src.armor)
	if src.shield:
		copy.shield = EquipmentGenerator._copy_armor(src.shield)
	
	# 传奇属性
	copy.legendary_resistance_uses = src.legendary_resistance_uses
	copy.legendary_action_points = src.legendary_action_points
	copy.legendary_actions = src.legendary_actions.duplicate(true)
	copy.lair_actions = src.lair_actions.duplicate(true)
	copy.phases = src.phases.duplicate(true)
	copy.unique_drop_id = src.unique_drop_id
	
	return copy


# ============================================================================
# 预设遭遇
# ============================================================================

## 获取预设遭遇（用于测试或特定场景）
static func get_preset_encounter(encounter_name: String) -> Array[UnitData]:
	match encounter_name:
		"goblin_ambush":
			return generate_enemy_squad(["grunt_goblin_archer"], 3)
		"skeleton_patrol":
			return generate_enemy_squad(["grunt_skeleton_warrior"], 4)
		"orc_raiders":
			return generate_enemy_squad(["std_orc_berserker"], 2)
		"mixed_undead":
			var squad: Array[UnitData] = []
			squad.append_array(generate_enemy_squad(["grunt_skeleton_warrior"], 2))
			squad.append(generate_enemy("std_ghoul"))
			return squad
		"wolf_pack":
			return generate_enemy_squad(["grunt_forest_wolf"], 4)
		"dragon_lair":
			var squad: Array[UnitData] = []
			squad.append(generate_enemy("legend_young_red_dragon"))
			return squad
		_:
			push_error("未知的预设遭遇: " + encounter_name)
			return []


# ============================================================================
# CR工具（向后兼容）
# ============================================================================

## CR转XP（委托给CRExperienceTable）
static func _cr_to_xp(cr: float) -> int:
	return CRExperienceTable.get_xp_for_cr(cr)
