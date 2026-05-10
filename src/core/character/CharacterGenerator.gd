# CharacterGenerator.gd
# 角色生成基于点数分配的属性系统
# 所有生物统一：1级属性25，每级+1
# 对应策划05-角色与职业.md 角色生成
class_name CharacterGenerator


# ============================================================================
# 角色生成
# ============================================================================

## 生成角色
## race: 种族数据（可选，null则随机）
## level: 目标等级
## seed_val: 随机种子（-1=随机）
static func generate_character(race: RaceData = null, level: int = 1, seed_val: int = -1) -> UnitData:
	if seed_val >= 0:
		seed(seed_val)

	# 随机选族（如果未指定）
	if race == null:
		var races = RaceData.get_all_races()
		race = races[randi() % races.size()]

	# 基于总点数分配基础属性（1级25点，每级+1）
	var base_attrs = _allocate_attrs(level, race)

	# 随机特质
	var traits = _roll_traits()

	# 应用特质修正（加到base上，不消耗分配点数）
	base_attrs = _apply_trait_modifiers(base_attrs, traits)

	# 创建UnitData
	var unit_data = UnitData.new()
	unit_data.unit_name = _generate_name(race)
	unit_data.level = maxi(1, level)
	unit_data.xp = RPGRuleEngine.get_xp_for_level(level)
	unit_data.race = race
	unit_data.character_traits = traits
	unit_data.unspent_attr_points = 0

	# 设置属性（含种族修正，最优解）
	unit_data.str = maxi(1, base_attrs["str"])
	unit_data.dex = maxi(1, base_attrs["dex"])
	unit_data.con = maxi(1, base_attrs["con"])
	unit_data.intel = maxi(1, base_attrs["intel"])
	unit_data.wis = maxi(1, base_attrs["wis"])
	unit_data.cha = maxi(1, base_attrs["cha"])

	# HP = 20 + floor(√CON × Level / 2)，√CON衰减体质收益
	unit_data.base_max_hp = 20 + int(floor(sqrt(unit_data.con) * level / 2.0))
	# 应用矮人韧（HP+Level）
	if "dwarven_resilience" in race.racial_traits:
		unit_data.base_max_hp += level

	unit_data.base_ac = 10
	unit_data.base_move_range = 4
	unit_data.base_initiative = 0

	# 半兽人先2
	if "threat_instinct" in race.racial_traits:
		unit_data.base_initiative += 2

	# 魔力
	unit_data.current_mana = 10 + RPGRuleEngine.get_stat_modifier(unit_data.intel) * 2
	unit_data.casting_ability = "intel"
	unit_data.skill_points = level - 1  # 1�?点，每升1�?1

	# 忠诚
	unit_data.loyalty = 50

	# 应用功能性特质效果
	_apply_functional_traits(unit_data, traits)

	return unit_data


## 生成随机敌方单位（同样使用25+level-1点数系统）
static func generate_random_enemy(cr: float, enemy_type: UnitData.EnemyType, ai_strategy: UnitData.AIStrategy = UnitData.AIStrategy.INSTINCT) -> UnitData:
	var unit_data = UnitData.new()
	unit_data.is_enemy = true
	unit_data.enemy_type = enemy_type
	unit_data.threat_level = cr
	unit_data.ai_strategy = ai_strategy
	unit_data.level = maxi(1, roundi(cr))

	# 使用统一的点数分配系统
	var attrs = _allocate_attrs_for_enemy(unit_data.level, enemy_type)
	unit_data.str = attrs["str"]
	unit_data.dex = attrs["dex"]
	unit_data.con = attrs["con"]
	unit_data.intel = attrs["intel"]
	unit_data.wis = attrs["wis"]
	unit_data.cha = attrs["cha"]
	unit_data.unspent_attr_points = 0

	# 根据敌人类型调整属性性
	match enemy_type:
		UnitData.EnemyType.UNDEAD:
			unit_data.immunities.append("poison")
			unit_data.immunities.append("mind")
		UnitData.EnemyType.BEAST:
			unit_data.ai_strategy = UnitData.AIStrategy.INSTINCT
		UnitData.EnemyType.DEMON:
			unit_data.resistances.append("magic")

	# HP = 20 + floor(√CON × Level / 2)，等级按CR估算
	var est_level = maxi(1, int(cr * 4))
	unit_data.base_max_hp = 20 + int(floor(sqrt(unit_data.con) * est_level / 2.0))
	# AC: 基础10 + 敏捷修正(通过属性分配自然获得)，不再叠加cr*2
	# 让AC更紧凑，配合SRPG保底命中+擦伤体系
	unit_data.base_ac = 10
	unit_data.base_move_range = 4
	unit_data.morale = 0

	return unit_data


# ============================================================================
# 属分配（点数系统 有生物统# ============================================================================

## 基于总点数分配属性（用于玩/NPC友好单位## total = 25 + (level-1)*1，族修正叠加在分配之上
static func _allocate_attrs(level: int, race: RaceData = null) -> Dictionary:
	var total_points = RPGRuleEngine.get_total_attr_points(level)
	var keys = RPGRuleEngine.ATTR_KEYS

	# 先全部设为最小值
	var attrs = {}
	for key in keys:
		attrs[key] = RPGRuleEngine.ATTR_MIN

	var remaining = total_points - RPGRuleEngine.ATTR_MIN * 6

	# 加权随机分配：种族适合方向权重更高
	var weights = _get_allocation_weights(race)
	var total_weight = 0.0
	for key in keys:
		total_weight += weights.get(key, 1.0)

	# 按权重分配剩余点
	for key in keys:
		var w = weights.get(key, 1.0)
		var allocated = roundi((w / total_weight) * remaining)
		attrs[key] += allocated

	# 修正四舍五入误差：确保总值精确
	var diff = total_points - RPGRuleEngine.get_attrs_sum(attrs)
	while diff > 0:
		# 随机选一到上限的属加1
		var k = keys[randi() % keys.size()]
		if attrs[k] < RPGRuleEngine.ATTR_MAX:
			attrs[k] += 1
			diff -= 1
	while diff < 0:
		var k = keys[randi() % keys.size()]
		if attrs[k] > RPGRuleEngine.ATTR_MIN:
			attrs[k] -= 1
			diff += 1

	# 叠加种族修正
	if race:
		attrs = _apply_race_modifiers(attrs, race)

	return attrs


## 获取属性分配权重（种族倾向）
static func _get_allocation_weights(race: RaceData) -> Dictionary:
	if race == null:
		return {"str": 1.0, "dex": 1.0, "con": 1.0, "intel": 1.0, "wis": 1.0, "cha": 1.0}

	# 基权重都为1，族合方向额加权
	var w = {"str": 1.0, "dex": 1.0, "con": 1.0, "intel": 1.0, "wis": 1.0, "cha": 1.0}

	# 根据种族正修正方向加
	if race.str_mod > 0: w["str"] += float(race.str_mod) * 0.8
	if race.dex_mod > 0: w["dex"] += float(race.dex_mod) * 0.8
	if race.con_mod > 0: w["con"] += float(race.con_mod) * 0.8
	if race.int_mod > 0: w["intel"] += float(race.int_mod) * 0.8
	if race.wis_mod > 0: w["wis"] += float(race.wis_mod) * 0.8
	if race.cha_mod > 0: w["cha"] += float(race.cha_mod) * 0.8

	return w


## 基于总点数分配敌方属性（根据敌人类型倾向分配）
static func _allocate_attrs_for_enemy(level: int, enemy_type: UnitData.EnemyType) -> Dictionary:
	var total_points = RPGRuleEngine.get_total_attr_points(level)
	var keys = RPGRuleEngine.ATTR_KEYS

	var attrs = {}
	for key in keys:
		attrs[key] = RPGRuleEngine.ATTR_MIN

	var remaining = total_points - RPGRuleEngine.ATTR_MIN * 6

	# 敌人类型 倾向权重
	var weights = {"str": 1.0, "dex": 1.0, "con": 1.0, "intel": 1.0, "wis": 1.0, "cha": 1.0}
	match enemy_type:
		UnitData.EnemyType.BEAST:
			weights["str"] = 2.0; weights["dex"] = 1.5; weights["con"] = 1.5
			weights["intel"] = 0.3; weights["cha"] = 0.3
		UnitData.EnemyType.UNDEAD:
			weights["str"] = 1.5; weights["con"] = 2.0; weights["wis"] = 0.5
		UnitData.EnemyType.DEMON:
			weights["str"] = 1.5; weights["con"] = 1.5; weights["intel"] = 1.5; weights["cha"] = 1.2
		UnitData.EnemyType.GIANT:
			weights["str"] = 3.0; weights["con"] = 2.5; weights["dex"] = 0.5
		UnitData.EnemyType.CONSTRUCT:
			weights["str"] = 2.0; weights["con"] = 2.0; weights["intel"] = 0.2; weights["cha"] = 0.1
		UnitData.EnemyType.DRAGON:
			weights["str"] = 2.5; weights["con"] = 2.5; weights["intel"] = 2.0; weights["cha"] = 1.5
		UnitData.EnemyType.LEGENDARY:
			weights["str"] = 2.0; weights["con"] = 2.0; weights["intel"] = 2.0; weights["wis"] = 2.0; weights["cha"] = 1.5
		_:  # HUMANOID
			weights["str"] = 1.2; weights["dex"] = 1.2; weights["con"] = 1.2
			weights["intel"] = 1.0; weights["wis"] = 1.0; weights["cha"] = 0.8

	var total_weight = 0.0
	for key in keys:
		total_weight += weights[key]

	# 加权分配
	for key in keys:
		var allocated = roundi((weights[key] / total_weight) * remaining)
		attrs[key] += allocated

	# 四舍五入
	var diff = total_points - RPGRuleEngine.get_attrs_sum(attrs)
	while diff > 0:
		var k = keys[randi() % keys.size()]
		if attrs[k] < RPGRuleEngine.ATTR_MAX:
			attrs[k] += 1
			diff -= 1
	while diff < 0:
		var k = keys[randi() % keys.size()]
		if attrs[k] > RPGRuleEngine.ATTR_MIN:
			attrs[k] -= 1
			diff += 1

	return attrs


# ============================================================================
# 应用
# ============================================================================

## 应用种族属性修正
static func _apply_race_modifiers(base: Dictionary, race: RaceData) -> Dictionary:
	base["str"] += race.str_mod
	base["dex"] += race.dex_mod
	base["con"] += race.con_mod
	base["intel"] += race.int_mod
	base["wis"] += race.wis_mod
	base["cha"] += race.cha_mod
	return base

## 应用特质属性修正
static func _apply_trait_modifiers(base: Dictionary, traits: Array[TraitData]) -> Dictionary:
	for _tr in traits:
		base["str"] += _tr.str_mod
		base["dex"] += _tr.dex_mod
		base["con"] += _tr.con_mod
		base["intel"] += _tr.int_mod
		base["wis"] += _tr.wis_mod
		base["cha"] += _tr.cha_mod
	return base

## 应用功能性特质效果
static func _apply_functional_traits(unit_data: UnitData, traits: Array[TraitData]):
	for _tr in traits:
		if _tr.trait_type != TraitData.TraitType.FUNCTIONAL:
			continue
		match _tr.functional_effect:
			"old_wound":
				# 战斗始时HP-10%（运行时处理
				pass
			"alertness":
				unit_data.base_initiative += 3


# ============================================================================
# 特质分配
# ============================================================================

## 随机抽取2-4个特质
static func _roll_traits() -> Array[TraitData]:
	var attr_traits = TraitData.get_attribute_traits()
	var func_traits = TraitData.get_functional_traits()

	var result: Array[TraitData] = []

	# 2-3性特
	var attr_count = randi_range(2, 3)
	for i in range(attr_count):
		var trait_pick = _weighted_random(attr_traits)
		if trait_pick and not result.has(trait_pick):
			result.append(trait_pick)

	# 0-1能特50%概率
	if randf() < 0.5 and func_traits.size() > 0:
		var func_pick = _weighted_random(func_traits)
		if func_pick and not result.has(func_pick):
			result.append(func_pick)

	return result

## 加权随机选择
static func _weighted_random(traits: Array[TraitData]) -> TraitData:
	var total_weight = 0.0
	for t in traits:
		total_weight += t.weight

	var roll = randf() * total_weight
	var cumulative = 0.0
	for t in traits:
		cumulative += t.weight
		if roll <= cumulative:
			return t

	return traits[0] if traits.size() > 0 else null


# ============================================================================
# AI升级
# ============================================================================

## AI自动加点到目标等级
## 使用点数系统：重算属性总值25+(level-1)
static func _ai_auto_level(unit_data: UnitData, target_level: int):
	# 设置XP和等
	unit_data.xp = RPGRuleEngine.get_xp_for_level(target_level)
	unit_data.level = target_level
	unit_data.skill_points = target_level - 1  # 已全部分�?
	# 重算属性（含种族修正））
	var race = unit_data.race
	var attrs = _allocate_attrs(target_level, race)
	unit_data.str = maxi(1, attrs["str"])
	unit_data.dex = maxi(1, attrs["dex"])
	unit_data.con = maxi(1, attrs["con"])
	unit_data.intel = maxi(1, attrs["intel"])
	unit_data.wis = maxi(1, attrs["wis"])
	unit_data.cha = maxi(1, attrs["cha"])
	unit_data.unspent_attr_points = 0

	# HP = 20 + (CON × Level) / 2
	var con_mod = RPGRuleEngine.get_stat_modifier(unit_data.con)
	unit_data.base_max_hp = 20 + int(floor(sqrt(unit_data.con) * target_level / 2.0))
	if unit_data.race and "dwarven_resilience" in unit_data.race.racial_traits:
		unit_data.base_max_hp += target_level

	# 更新魔力
	unit_data.current_mana = 10 + RPGRuleEngine.get_stat_modifier(unit_data.intel) * 2


# ============================================================================
# 辅助
# ============================================================================

## 确定倾向（最高属性对应的标签）
static func determine_tendency(unit_data: UnitData) -> String:
	var attrs = {
		"str": unit_data.str,
		"dex": unit_data.dex,
		"con": unit_data.con,
		"intel": unit_data.intel,
		"wis": unit_data.wis,
		"cha": unit_data.cha,
	}

	var best_key = "str"
	var best_val = -1
	for key in attrs:
		if attrs[key] > best_val:
			best_val = attrs[key]
			best_key = key

	match best_key:
		"str": return "力量倾向"
		"dex": return "灵巧倾向"
		"con": return "体魄倾向"
		"intel": return "智力倾向"
		"wis": return "感知倾向"
		"cha": return "魅力倾向"
		_: return "力量倾向"

## 随机生成名字（简单版）
static func _generate_name(race: RaceData) -> String:
	var first_names_by_race = {
		"human": ["阿尔弗雷德", "伊莎贝拉", "威廉", "艾琳娜", "加雷斯"],
		"elf": ["艾洛温", "塞兰迪尔", "莉瑞尔", "瑟兰迪尔", "伊芙瑞特"],
		"dwarf": ["索林", "布洛克", "巴林", "多林", "甘达林"],
		"half_orc": ["格罗姆", "乌加什", "夏卡", "穆卡尔", "扎格拉"],
		"half_elf": ["艾拉", "艾登", "米瑞尔", "塔尔", "莉娅"],
	}

	var race_key = "human"
	match race.race_id:
		RaceData.Race.HUMAN: race_key = "human"
		RaceData.Race.ELF: race_key = "elf"
		RaceData.Race.DWARF: race_key = "dwarf"
		RaceData.Race.HALF_ORC: race_key = "half_orc"
		RaceData.Race.HALF_ELF: race_key = "half_elf"

	var names = first_names_by_race.get(race_key, first_names_by_race["human"])
	return names[randi() % names.size()]


# ============================================================================
# 模板生成系统
# ============================================================================

## 从模板生成完整单位（使用点数系统，模板中的str/dex等作为权重提示）
static func generate_from_template(tpl: Dictionary, level: int = -1) -> UnitData:
	var unit_data = UnitData.new()
	unit_data.enemy_template_id = tpl.get("template_id", "")
	unit_data.unit_name = tpl.get("name", "未知单位")
	unit_data.is_enemy = true
	unit_data.enemy_type = tpl.get("enemy_type", UnitData.EnemyType.BEAST)
	unit_data.threat_level = tpl.get("cr", 1.0)
	unit_data.ai_strategy = tpl.get("ai_strategy", UnitData.AIStrategy.INSTINCT)

	var target_level = level if level > 0 else max(1, roundi(tpl.get("cr", 1.0)))
	unit_data.level = target_level

	# 使用统一的点数分配系统（根据敌人类型倾向）
	var attrs = _allocate_attrs_for_enemy(target_level, unit_data.enemy_type)
	unit_data.str = attrs["str"]
	unit_data.dex = attrs["dex"]
	unit_data.con = attrs["con"]
	unit_data.intel = attrs["intel"]
	unit_data.wis = attrs["wis"]
	unit_data.cha = attrs["cha"]
	unit_data.unspent_attr_points = 0

	var con_mod = RPGRuleEngine.get_stat_modifier(unit_data.con)
	unit_data.base_max_hp = 20 + int(floor(sqrt(unit_data.con) * target_level / 2.0)) + tpl.get("hp_bonus", 0)
	unit_data.base_ac = 10 + tpl.get("ac_bonus", 0)
	unit_data.base_move_range = 4
	unit_data.base_initiative = tpl.get("initiative_bonus", 0)
	unit_data.morale = 0

	for r in tpl.get("resistances", []):
		unit_data.resistances.append(r)
	for im in tpl.get("immunities", []):
		unit_data.immunities.append(im)
	for t in tpl.get("traits", []):
		unit_data.traits.append(t)

	var spell_ids = tpl.get("spells", [])
	if spell_ids.size() > 0:
		unit_data.current_mana = 10 + RPGRuleEngine.get_stat_modifier(unit_data.intel) * target_level
		unit_data.casting_ability = "intel"
		_assign_spells(unit_data, spell_ids, target_level)

	_assign_skills(unit_data, tpl.get("skills", []))
	return unit_data


## 随机生成领主
static func generate_random_lord(level: int = 8) -> UnitData:
	var templates = UnitTemplateDB.get_lord_templates()
	var tpl = templates[randi() % templates.size()]
	return generate_from_template(tpl, level)


## 随机生成冒险者
static func generate_random_adventurer(level: int = -1) -> UnitData:
	var templates = UnitTemplateDB.get_adventurer_templates()
	var tpl = templates[randi() % templates.size()]
	var cr = tpl.get("cr", 1.0)
	var target_level = level if level > 0 else max(1, roundi(cr) + randi_range(-1, 1))
	return generate_from_template(tpl, target_level)


## 随机生成怪物（按CR范围）
static func generate_random_monster(min_cr: float = 0.25, max_cr: float = 20.0) -> UnitData:
	var templates = UnitTemplateDB.get_templates_by_cr(min_cr, max_cr)
	if templates.is_empty():
		templates = UnitTemplateDB.get_monster_templates()
	var tpl = templates[randi() % templates.size()]
	return generate_from_template(tpl)


## 随机生成传生物
static func generate_random_legendary(level: int = 15) -> UnitData:
	var templates = UnitTemplateDB.get_legendary_templates()
	var tpl = templates[randi() % templates.size()]
	return generate_from_template(tpl, level)


## 根据配置生成敌方队伍
static func generate_encounter_party(party_cr_total: float, partysize: int = -1) -> Array[UnitData]:
	var units: Array[UnitData] = []
	var party_size = partysize
	if party_size < 0:
		party_size = randi_range(2, 6)
	var cr_per_unit = party_cr_total / float(party_size)
	for i in range(party_size):
		var templates = UnitTemplateDB.get_templates_by_cr(cr_per_unit * 0.5, cr_per_unit * 2.0)
		if templates.is_empty():
			units.append(generate_random_enemy(cr_per_unit, UnitData.EnemyType.BEAST))
		else:
			var tpl = templates[randi() % templates.size()]
			units.append(generate_from_template(tpl))
	return units


# ============================================================================
# 法术分配
# ============================================================================

static func _assign_spells(unit_data: UnitData, spell_ids: Array, level: int) -> void:
	for spell_id in spell_ids:
		var spell = _create_spell_by_id(spell_id, level)
		if spell:
			unit_data.known_spells.append(spell)


static func _create_spell_by_id(spell_id: String, level: int) -> SpellData:
	var spell = SpellData.new()
	spell.spell_id = spell_id
	match spell_id:
		"fireball":
			spell.spell_name = "火球术"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_3
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 2
			spell.range_cells = 8; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.DEX_SAVE
			spell.damage_dice_count = 6 + floori(level / 3.0); spell.damage_dice_sides = 6; spell.damage_type = "fire"
		"magic_missile":
			spell.spell_name = "魔导飞弹"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_1
			spell.shape = SpellData.SpellShape.SINGLE; spell.range_cells = 10
			spell.resolution_type = SpellData.ResolutionType.AUTO_HIT
			spell.damage_dice_count = 4; spell.damage_dice_sides = 4; spell.damage_type = "force"
		"ice_storm":
			spell.spell_name = "冰暴术"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_4
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 3
			spell.range_cells = 10; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.CON_SAVE
			spell.damage_dice_count = 4 + floori(level / 4.0); spell.damage_dice_sides = 8; spell.damage_type = "cold"
		"frost_breath":
			spell.spell_name = "霜冻吐息"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_5
			spell.shape = SpellData.SpellShape.CONE; spell.shape_size = 4
			spell.range_cells = 4; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.CON_SAVE
			spell.damage_dice_count = 8 + floori(level / 3.0); spell.damage_dice_sides = 8; spell.damage_type = "cold"
			spell.applied_status_effect = "freeze"; spell.status_duration = 1
		"blizzard":
			spell.spell_name = "暴风雪"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_6
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 4
			spell.range_cells = 12; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.CON_SAVE
			spell.damage_dice_count = 10 + floori(level / 2.0); spell.damage_dice_sides = 8; spell.damage_type = "cold"
		"inferno":
			spell.spell_name = "炼狱"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_6
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 3
			spell.range_cells = 10; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.DEX_SAVE
			spell.damage_dice_count = 12 + floori(level / 2.0); spell.damage_dice_sides = 8; spell.damage_type = "fire"
		"meteor_strike":
			spell.spell_name = "陨石坠落"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_7
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 3
			spell.range_cells = 12; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.DEX_SAVE
			spell.damage_dice_count = 15 + floori(level / 2.0); spell.damage_dice_sides = 8; spell.damage_type = "fire"
		"nature_bolt":
			spell.spell_name = "nature_bolt"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_2
			spell.shape = SpellData.SpellShape.SINGLE; spell.range_cells = 8
			spell.resolution_type = SpellData.ResolutionType.ATTACK_ROLL
			spell.damage_dice_count = 3 + floori(level / 4.0); spell.damage_dice_sides = 8; spell.damage_type = "force"
		"life_drain":
			spell.spell_name = "生命汲取"
			spell.spell_school = SpellData.SpellSchool.NECROMANCY
			spell.tier = SpellData.SpellTier.TIER_2
			spell.shape = SpellData.SpellShape.SINGLE; spell.range_cells = 6
			spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.CON_SAVE
			spell.damage_dice_count = 3 + floori(level / 4.0); spell.damage_dice_sides = 6; spell.damage_type = "necrotic"
			spell.heal_dice_count = 3 + floori(level / 4.0); spell.heal_dice_sides = 6
		"raise_dead":
			spell.spell_name = "raise_dead"
			spell.spell_school = SpellData.SpellSchool.NECROMANCY
			spell.tier = SpellData.SpellTier.TIER_3
			spell.shape = SpellData.SpellShape.SELF; spell.range_cells = 3
			spell.resolution_type = SpellData.ResolutionType.AUTO_HIT
			spell.special_effect = "summon"; spell.summon_hp = 10 + level * 3; spell.summon_duration = 5
		"bone_spear":
			spell.spell_name = "骨矛"
			spell.spell_school = SpellData.SpellSchool.NECROMANCY
			spell.tier = SpellData.SpellTier.TIER_1
			spell.shape = SpellData.SpellShape.RAY; spell.range_cells = 8
			spell.resolution_type = SpellData.ResolutionType.ATTACK_ROLL
			spell.damage_dice_count = 2 + floori(level / 4.0); spell.damage_dice_sides = 8; spell.damage_type = "necrotic"
		"shield":
			spell.spell_name = "魔法护盾"
			spell.spell_school = SpellData.SpellSchool.ABJURATION
			spell.tier = SpellData.SpellTier.TIER_1
			spell.shape = SpellData.SpellShape.SELF; spell.range_cells = 0
			spell.resolution_type = SpellData.ResolutionType.AUTO_HIT
			spell.special_effect = "shield"; spell.duration_turns = 2
		"entangle":
			spell.spell_name = "藤蔓束缚"
			spell.spell_school = SpellData.SpellSchool.TRANSMUTATION
			spell.tier = SpellData.SpellTier.TIER_1
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 2
			spell.range_cells = 6; spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.STR_SAVE
			spell.applied_status_effect = "entangled"; spell.status_duration = 2
		"holy_light":
			spell.spell_name = "holy_light"
			spell.spell_school = SpellData.SpellSchool.ABJURATION
			spell.tier = SpellData.SpellTier.TIER_1
			spell.shape = SpellData.SpellShape.SINGLE; spell.range_cells = 6
			spell.resolution_type = SpellData.ResolutionType.AUTO_HIT
			spell.heal_dice_count = 2 + floori(level / 4.0); spell.heal_dice_sides = 8; spell.heal_bonus = 2
		"smite":
			spell.spell_name = "smite"
			spell.spell_school = SpellData.SpellSchool.ABJURATION
			spell.tier = SpellData.SpellTier.TIER_2
			spell.shape = SpellData.SpellShape.TOUCH; spell.range_cells = 1
			spell.resolution_type = SpellData.ResolutionType.ATTACK_ROLL
			spell.damage_dice_count = 3 + floori(level / 4.0); spell.damage_dice_sides = 8; spell.damage_type = "radiant"
		"healing_light":
			spell.spell_name = "治愈之光"
			spell.spell_school = SpellData.SpellSchool.ABJURATION
			spell.tier = SpellData.SpellTier.TIER_3
			spell.shape = SpellData.SpellShape.SPHERE; spell.shape_size = 2
			spell.range_cells = 0; spell.resolution_type = SpellData.ResolutionType.AUTO_HIT
			spell.heal_dice_count = 4 + floori(level / 3.0); spell.heal_dice_sides = 8; spell.heal_bonus = 3
		"moonbeam":
			spell.spell_name = "月光射线"
			spell.spell_school = SpellData.SpellSchool.EVOCATION
			spell.tier = SpellData.SpellTier.TIER_2
			spell.shape = SpellData.SpellShape.LINE; spell.range_cells = 8
			spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.CON_SAVE
			spell.damage_dice_count = 3 + floori(level / 4.0); spell.damage_dice_sides = 8; spell.damage_type = "radiant"
		"dark_command":
			spell.spell_name = "黑暗统御"
			spell.spell_school = SpellData.SpellSchool.ENCHANTMENT
			spell.tier = SpellData.SpellTier.TIER_3
			spell.shape = SpellData.SpellShape.SINGLE; spell.range_cells = 6
			spell.resolution_type = SpellData.ResolutionType.SAVE
			spell.save_type = SpellData.SaveType.WIS_SAVE
			spell.applied_status_effect = "charmed"; spell.status_duration = 2
		"shadow_bolt":
			spell.spell_name = "shadow_bolt"
			spell.spell_school = SpellData.SpellSchool.NECROMANCY
			spell.tier = SpellData.SpellTier.TIER_1
			spell.shape = SpellData.SpellShape.SINGLE; spell.range_cells = 10
			spell.resolution_type = SpellData.ResolutionType.ATTACK_ROLL
			spell.damage_dice_count = 2 + floori(level / 4.0); spell.damage_dice_sides = 8; spell.damage_type = "necrotic"
		_:
			spell.spell_name = "shadow_bolt"
			spell.tier = SpellData.SpellTier.CANTRIP
	spell.mana_cost = SpellData.get_default_mana_cost(spell.tier)
	spell.cooldown_turns = SpellData.get_default_cooldown(spell.tier)
	return spell


# ============================================================================
# 能分# ============================================================================

static func _assign_skills(unit_data: UnitData, skill_names: Array) -> void:
	for skill_name in skill_names:
		var skill = SkillData.new()
		skill.skill_name = skill_name
		skill.description = _get_skill_description(skill_name)
		skill.ap_cost = _get_skill_ap_cost(skill_name)
		skill.range_cells = _get_skill_range(skill_name)
		skill.cooldown = _get_skill_cooldown(skill_name)
		unit_data.skills.append(skill)


static func _get_skill_description(name: String) -> String:
	match name:
		"挥砍连击": return "连续挥砍两次，每次造成武器伤害"
		"盾墙": return "举起盾牌，本回合AC+4但无法移动"
		"号令冲锋": return "激励周围友方发起冲锋，范围内友方移动力+2"
		"坚守阵地": return "固守当前位置，获得坚韧状态（伤害减免25%）"
		"暗影步": return "传送至阴影处，下次攻击造成额外暗影伤害"
		"心灵压制": return "用意志力压制目标，使其眩晕1回合"
		"恐惧术": return "散发恐惧气息，使周围敌人士气降低"
		"狂暴冲锋": return "狂暴状态下冲锋，沿途敌人被击退并受到伤害"
		"旋风斩": return "旋转攻击周围所有敌人"
		"战吼": return "发出震天怒吼，提升自身和友方攻击力"
		"嗜血": return "击杀敌人后恢复生命值"
		"连射": return "连续射出多支箭矢"
		"狙击要害": return "瞄准要害射击，暴击率大幅提升"
		"影遁": return "融入阴影，提升闪避率"
		"精准射击": return "精准射击，忽略部分掩护加成"
		"设置陷阱": return "在脚下放置陷阱"
		"闪避": return "增加闪避率直到下回合"
		"法杖打击": return "用法杖进行近战攻击"
		"魔法护盾": return "张开魔法屏障，临时提升AC"
		"圣光斩": return "以神圣之力灌注武器进行近战攻击"
		"治疗之手": return "触碰治疗一名友方单位"
		"神圣护盾": return "展开神圣护盾，为自身和附近友方提供保护"
		"驱邪": return "驱散亡灵或解除诅咒"
		"猛击": return "全力猛击"
		"投石": return "投掷石块进行远程攻击"
		"卑鄙刺击": return "趁敌人不备进行偷袭"
		"毒镖": return "发射淬毒飞镖"
		"狂暴": return "进入狂暴状态，攻击+50%但防御下降"
		"呼唤援兵": return "呼唤更多哥布林加入战斗"
		"撕咬": return "用牙齿撕裂敌人"
		"扑击": return "扑向目标将其扑倒"
		"嗥叫": return "发出嗥叫呼唤同伴"
		"熊抱": return "用双臂抱住敌人进行碾压"
		"撕裂": return "用利爪撕裂目标"
		"践踏": return "践踏周围敌人"
		"骷髅召唤": return "召唤骷髅战士加入战斗"
		"亡灵诅咒": return "诅咒目标使其属性降低"
		"毒雾吐息": return "喷吐毒雾锥形范围攻击"
		"火焰冲锋": return "全身燃烧冲向敌人"
		"恶魔猛击": return "充满黑暗力量的重击"
		"恐惧凝视": return "用恶魔之眼震慑目标"
		"黑暗劈斩": return "以黑暗之力劈斩目标"
		"亡灵哀嚎": return "发出能令活人恐惧的哀嚎"
		"灵魂汲取": return "吸取目标的灵魂能量"
		"恐惧之触": return "触碰目标使其陷入恐惧"
		"冰霜龙息": return "喷吐极寒龙息，冻结大范围区域"
		"尾击": return "用巨尾横扫周围敌人"
		"翼击": return "展开巨翼击退周围敌人"
		"碾压": return "碾压周围的小型敌人"
		"恐惧威慑": return "散发恐怖气场，使敌人士气下降"
		"烈焰风暴": return "召唤烈焰风暴覆盖战场"
		"岩石投掷": return "投掷巨石攻击远处目标"
		"地震": return "猛烈践踏引发局部地震"
		"巨拳猛击": return "以巨拳猛击地面造成冲击波"
		"月华剑舞": return "在月光下施展连续斩击"
		"星辰之力": return "引导星辰之力释放能量"
		"精灵之歌": return "吟唱精灵歌谣，治疗友方"
		"时空裂隙": return "撕裂时空进行短距离传送"
		"酸液喷吐": return "喷吐腐蚀性酸液"
		"吞噬": return "吞噬小型敌人恢复生命"
		"钻地突袭": return "钻入地下后从下方突袭"
		"尾鞭": return "用尾部鞭击敌人"
		"麻痹毒刺": return "蛰刺附带麻痹毒素"
		"吞噬尸体": return "吞食尸体恢复生命"
		_: return ""


static func _get_skill_ap_cost(name: String) -> int:
	match name:
		"盾墙", "坚守阵地", "影遁", "魔法护盾", "神圣护盾": return 0
		"烈焰风暴", "地震", "时空裂隙": return 2
		_: return 1



static func _get_skill_range(name: String) -> int:
	match name:
		"连射", "精准射击", "投石", "毒镖", "岩石投掷": return 8
		"号令冲锋", "恐惧威慑", "亡灵哀嚎", "战吼", "嗥叫": return 4
		_: return 1


static func _get_skill_cooldown(name: String) -> int:
	match name:
		"号令冲锋", "呼唤援兵", "狂暴", "地震", "时空裂隙", "烈焰风暴": return 3
		"冰霜龙息", "毒雾吐息", "恐惧威慑", "骷髅召唤", "亡灵诅咒": return 4
		"旋风斩", "暗影步", "恐惧术", "战吼": return 2
		_: return 1
