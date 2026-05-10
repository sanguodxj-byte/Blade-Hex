# CRExperienceTable.gd
# 威胁等级(CR)经验表 + 遭遇预算系统
# 对应策划案 08-敌方与AI.md §4
# 适配120级体系：CR = floor(level / 6)

class_name CRExperienceTable
extends RefCounted

# ============================================================================
# CR → 经验值 完整对照表（CR 0~20）
# CR由等级派生：CR = floor(level / 6)
# CR 0=1~5级, CR 1=6~11级, CR 2=12~17级 ... CR 20=120级
# ============================================================================

## CR到经验值的完整映射
static var _cr_to_xp: Dictionary = {
	0.0: 10,
	0.25: 25,
	0.5: 50,
	1.0: 200,
	2.0: 450,
	3.0: 700,
	4.0: 1100,
	5.0: 1800,
	6.0: 2300,
	7.0: 2900,
	8.0: 3900,
	9.0: 5000,
	10.0: 5900,
	11.0: 7200,
	12.0: 8400,
	13.0: 10000,
	14.0: 11500,
	15.0: 13000,
	16.0: 15000,
	17.0: 18000,
	18.0: 20000,
	19.0: 22000,
	20.0: 25000,
}

## 根据CR获取经验值
static func get_xp_for_cr(cr: float) -> int:
	if _cr_to_xp.has(cr):
		return _cr_to_xp[cr]
	# CR 21+ 的外推公式
	if cr >= 21.0:
		return int(33000 + (cr - 21.0) * 5000)
	return 10

## 计算多敌人遭遇的总经验
static func get_encounter_total_xp(enemy_cr_list: Array[float]) -> int:
	var total: int = 0
	for cr in enemy_cr_list:
		total += get_xp_for_cr(cr)
	return total

## 根据等级获取CR（核心派生公式）
static func get_cr_from_level(level: int) -> float:
	return RPGRuleEngine.get_cr_from_level(level)

## 根据CR获取推荐等级
static func get_level_from_cr(cr: float) -> int:
	return RPGRuleEngine.get_level_from_cr(cr)


# ============================================================================
# CR 等级定位（适配120级）
# ============================================================================

enum CRLevel {
	GRUNT,      ## 杂兵 CR 0~0.5 (等级1~3)
	STANDARD,   ## 熟练 CR 1~3 (等级6~17)
	ELITE,      ## 精英 CR 4~7 (等级24~47)
	BOSS,       ## 首领 CR 8~12 (等级48~72)
	LEGENDARY,  ## 传奇 CR 13~20 (等级78~120)
	MYTHIC,     ## 神话 CR 21+ (超120级内容)
}

## 获取CR的等级定位
static func get_cr_level(cr: float) -> int:
	if cr < 1.0:
		return CRLevel.GRUNT
	elif cr <= 3.0:
		return CRLevel.STANDARD
	elif cr <= 7.0:
		return CRLevel.ELITE
	elif cr <= 12.0:
		return CRLevel.BOSS
	elif cr <= 20.0:
		return CRLevel.LEGENDARY
	else:
		return CRLevel.MYTHIC

## 获取CR等级定位显示名
static func get_cr_level_name(cr: float) -> String:
	match get_cr_level(cr):
		CRLevel.GRUNT: return "杂兵"
		CRLevel.STANDARD: return "熟练"
		CRLevel.ELITE: return "精英"
		CRLevel.BOSS: return "首领"
		CRLevel.LEGENDARY: return "传奇"
		CRLevel.MYTHIC: return "神话"
		_: return "未知"


# ============================================================================
# 遭遇CR预算（适配120级等级体系）
# ============================================================================

enum EncounterDifficulty {
	EASY,       ## 轻松：队伍等级×0.5
	STANDARD,   ## 标准：队伍等级×1.0
	HARD,       ## 困难：队伍等级×1.5
	DEADLY,     ## 致命：队伍等级×2.0+
	BOSS_FIGHT, ## Boss：队伍等级+20~30级单体
}

## 计算遭遇难度预算CR（基于队伍平均等级）
static func get_encounter_budget(party_avg_level: int, difficulty: int) -> float:
	var party_cr = RPGRuleEngine.get_cr_from_level(party_avg_level)
	match difficulty:
		EncounterDifficulty.EASY:
			return party_cr * 0.5
		EncounterDifficulty.STANDARD:
			return party_cr * 1.0
		EncounterDifficulty.HARD:
			return party_cr * 1.5
		EncounterDifficulty.DEADLY:
			return party_cr * 2.0
		EncounterDifficulty.BOSS_FIGHT:
			return RPGRuleEngine.get_cr_from_level(party_avg_level + 24)
		_:
			return party_cr * 1.0

## 评估当前遭遇的实际难度
static func assess_encounter(party_avg_level: int, enemy_cr_list: Array[float]) -> int:
	var budget: int = get_encounter_total_xp(enemy_cr_list)
	var party_cr: float = RPGRuleEngine.get_cr_from_level(party_avg_level)
	var party_xp_threshold: int = get_xp_for_cr(party_cr)
	
	# 多敌人倍率调整
	var enemy_count: int = enemy_cr_list.size()
	var adjusted_threshold: float = party_xp_threshold
	if enemy_count >= 5:
		adjusted_threshold *= 2.0
	elif enemy_count >= 3:
		adjusted_threshold *= 1.5
	
	if budget <= party_xp_threshold * 0.5:
		return EncounterDifficulty.EASY
	elif budget <= party_xp_threshold:
		return EncounterDifficulty.STANDARD
	elif budget <= party_xp_threshold * 1.5:
		return EncounterDifficulty.HARD
	else:
		return EncounterDifficulty.DEADLY

## 获取遭遇难度显示名
static func get_difficulty_name(difficulty: int) -> String:
	match difficulty:
		EncounterDifficulty.EASY: return "轻松"
		EncounterDifficulty.STANDARD: return "标准"
		EncounterDifficulty.HARD: return "困难"
		EncounterDifficulty.DEADLY: return "致命"
		EncounterDifficulty.BOSS_FIGHT: return "Boss"
		_: return "未知"


# ============================================================================
# CR 对等规则（适配120级）
# ============================================================================

## 判断是否可以对战（CR差距过大时建议撤退）
static func can_engage(party_avg_level: int, highest_enemy_cr: float) -> Dictionary:
	var party_cr: float = RPGRuleEngine.get_cr_from_level(party_avg_level)
	var diff: float = highest_enemy_cr - party_cr
	if diff <= 2.0:
		return {"can_fight": true, "warning": ""}
	elif diff <= 5.0:
		return {"can_fight": true, "warning": "极难遭遇，建议做好准备！"}
	else:
		return {"can_fight": false, "warning": "敌人远超队伍实力，强烈建议撤退！"}
