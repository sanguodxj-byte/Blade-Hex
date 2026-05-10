# QuestEncounterData.gd
# 委托遭遇数据组件 — 存储任务目标点关联的敌方单位配置
#
# 当玩家到达任务目标点时, 使用此数据生成战场中的敌方单位
# 配合 UnitTemplateDB 使用敌方模板ID生成完整 UnitData
class_name QuestEncounterData
extends RefCounted


## ========================================
## 单个敌方组配置
## ========================================
class EnemyGroup:
	## 敌方模板ID（对应 UnitTemplateDB 中的模板）
	var template_id: String = ""
	## 该组数量
	var count: int = 1
	## 等级偏移（加在模板等级上）
	var level_offset: int = 0

	func serialize() -> Dictionary:
		return {
			"template_id": template_id,
			"count": count,
			"level_offset": level_offset,
		}

	static func deserialize(data: Dictionary) -> EnemyGroup:
		var g = EnemyGroup.new()
		g.template_id = str(data.get("template_id", ""))
		g.count = int(data.get("count", 1))
		g.level_offset = int(data.get("level_offset", 0))
		return g


## ========================================
## 数据字段
## ========================================

## 敌方组列表
var enemy_groups: Array[EnemyGroup] = []

## 总挑战等级 (CR)
var cr_total: float = 1.0

## 战场环境修饰（如"night", "ambush", "fortified"）
var battle_modifiers: PackedStringArray = []


## ========================================
## 工厂方法
## ========================================

## 从 QuestData + SiteType 生成遭遇配置
static func create_from_quest(quest: QuestData, site_type: int) -> QuestEncounterData:
	var data = QuestEncounterData.new()
	var difficulty := quest.difficulty
	var target_count := quest.target_count

	# 按目标点类型配置敌方模板
	match site_type:
		QuestTargetSite.SiteType.GOBLIN_CAMP:
			data.enemy_groups = _make_groups(
				["goblin_warrior", "goblin_archer", "goblin_chieftain"],
				[0.5, 0.35, 0.15],
				target_count, difficulty
			)
		QuestTargetSite.SiteType.KOBOLD_MINE:
			data.enemy_groups = _make_groups(
				["kobold_trapper", "kobold_sorcerer"],
				[0.6, 0.4],
				target_count, difficulty
			)
		QuestTargetSite.SiteType.MINOTAUR_FORT:
			data.enemy_groups = _make_groups(
				["minotaur_warrior"],
				[1.0],
				mini(target_count, 4), difficulty
			)
		QuestTargetSite.SiteType.CULT_HIDEOUT:
			data.enemy_groups = _make_groups(
				["cultist", "shadow_acolyte"],
				[0.5, 0.5],
				target_count, difficulty
			)
			data.battle_modifiers = PackedStringArray(["dark"])
		QuestTargetSite.SiteType.BANDIT_CAMP:
			data.enemy_groups = _make_groups(
				["bandit_warrior", "bandit_archer"],
				[0.55, 0.45],
				target_count, difficulty
			)
		QuestTargetSite.SiteType.WOLF_DEN:
			data.enemy_groups = _make_groups(
				["wolf", "dire_wolf"],
				[0.7, 0.3],
				target_count, difficulty
			)
		QuestTargetSite.SiteType.RUINS:
			data.enemy_groups = _make_groups(
				["stone_golem", "iron_golem"],
				[0.6, 0.4],
				maxi(target_count / 2, 2), difficulty
			)
		QuestTargetSite.SiteType.TOMB:
			data.enemy_groups = _make_groups(
				["skeleton_warrior", "zombie", "wraith"],
				[0.4, 0.35, 0.25],
				target_count, difficulty
			)
			data.battle_modifiers = PackedStringArray(["undead"])
		QuestTargetSite.SiteType.DRAGON_LAIR:
			data.enemy_groups = _make_groups(
				["dragon"],
				[1.0],
				1, difficulty
			)
			data.cr_total = 10.0 + float(difficulty) * 5.0
		QuestTargetSite.SiteType.DUNGEON_ENTRANCE:
			data.enemy_groups = _make_groups(
				["skeleton_warrior", "cultist"],
				[0.5, 0.5],
				target_count, difficulty
			)
		QuestTargetSite.SiteType.VILLAGE_THREAT:
			# 防御任务：敌方是攻击方
			data.enemy_groups = _make_groups(
				["goblin_warrior", "goblin_archer"],
				[0.6, 0.4],
				target_count * 2, difficulty
			)
			data.battle_modifiers = PackedStringArray(["defense"])
		_:
			# 默认：通用敌人
			data.enemy_groups = _make_groups(
				["goblin_warrior"],
				[1.0],
				target_count, difficulty
			)

	# 按难度计算CR（龙巢已在上方 match 内手动设定，不覆盖）
	if site_type != QuestTargetSite.SiteType.DRAGON_LAIR:
		data.cr_total = _calculate_cr(data.enemy_groups, difficulty)
	return data


## ========================================
## 导出为遭遇配置 (兼容 OverworldEntity.get_encounter_config 接口)
## ========================================

func to_encounter_config() -> Dictionary:
	var enemy_ids: PackedStringArray = []
	for group in enemy_groups:
		for i in range(group.count):
			enemy_ids.append(group.template_id)
	return {
		"enemies": enemy_ids,
		"cr_total": cr_total,
		"battle_modifiers": battle_modifiers,
	}


## ========================================
## 序列化
## ========================================

func serialize() -> Dictionary:
	var groups_data: Array = []
	for g in enemy_groups:
		groups_data.append(g.serialize())
	return {
		"enemy_groups": groups_data,
		"cr_total": cr_total,
		"battle_modifiers": battle_modifiers,
	}


static func deserialize(data: Dictionary) -> QuestEncounterData:
	var enc = QuestEncounterData.new()
	enc.cr_total = float(data.get("cr_total", 1.0))
	enc.battle_modifiers = PackedStringArray(data.get("battle_modifiers", []))
	var groups_data: Array = data.get("enemy_groups", [])
	for gd in groups_data:
		enc.enemy_groups.append(EnemyGroup.deserialize(gd))
	return enc


## ========================================
## 内部工具
## ========================================

## 按权重比例分配目标数量到各组
static func _make_groups(template_ids: PackedStringArray, weights: Array[float], totalcount: int, difficulty: int) -> Array[EnemyGroup]:
	var groups: Array[EnemyGroup] = []
	var remaining := totalcount

	for i in range(template_ids.size()):
		var group = EnemyGroup.new()
		group.template_id = template_ids[i]

		if i == template_ids.size() - 1:
			# 最后一组拿剩余全部
			group.count = maxi(remaining, 1)
		else:
			var weight: float = weights[i] if i < weights.size() else 1.0 / float(template_ids.size())
			group.count = maxi(roundi(float(totalcount) * weight), 1)
			remaining = maxi(remaining - group.count, 1)

		# 困难/BOSS难度提升等级
		match difficulty:
			QuestData.QuestDifficulty.HARD:   group.level_offset = 1
			QuestData.QuestDifficulty.BOSS:   group.level_offset = 2
			_:                                group.level_offset = 0

		groups.append(group)

	return groups


## 根据敌方组估算总CR
static func _calculate_cr(groups: Array[EnemyGroup], difficulty: int) -> float:
	var total_units := 0
	for g in groups:
		total_units += g.count
	var base_cr := float(total_units) * 0.5
	var diff_mult := 1.0
	match difficulty:
		QuestData.QuestDifficulty.EASY:   diff_mult = 0.8
		QuestData.QuestDifficulty.MEDIUM: diff_mult = 1.0
		QuestData.QuestDifficulty.HARD:   diff_mult = 1.5
		QuestData.QuestDifficulty.BOSS:   diff_mult = 3.0
		_:                                diff_mult = 1.0
	return base_cr * diff_mult
