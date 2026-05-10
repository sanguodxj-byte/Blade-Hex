# RPGRuleEngine.gd
# RPG规则引擎 — 静态工具类，封装所有骰子、检定、等级、专精规则
# 对应策划案 02-RPG系统.md
# 等级上限120级，每级+1属性点
class_name RPGRuleEngine


# ============================================================================
# 枚举
# ============================================================================

enum SaveType {
	FORTITUDE,  # 强韧豁免 — 基于CON
	REFLEX,     # 反射豁免 — 基于DEX
	WILL,       # 意志豁免 — 基于WIS
}

# ============================================================================
# 常量 — 120级体系核心参数
# ============================================================================

## 等级上限
const MAX_LEVEL: int = 120

## 1级基础属性总点数
const BASE_ATTR_TOTAL: int = 25

## 每级额外获得的自由属性点
const ATTR_PER_LEVEL: int = 1

## 每项属性最低值
const ATTR_MIN: int = 1

## 每项属性上限（不含种族/特质加成）
const ATTR_MAX: int = 40

## 六维属性键名列表
static var ATTR_KEYS: Array[String] = ["str", "dex", "con", "intel", "wis", "cha"]

# ============================================================================
# 经验值与等级表（120级）
# 核心：每级XP需求渐进增长，全程节奏均匀
# 公式：每级升级所需XP = 300 + (level - 1) × 200
# 即 1→2 需300, 2→3 需500, 10→11 需2100, 50→51 需9900, 100→101 需19900
# ============================================================================

## 生成累计经验值表（1~120级）
static var _xp_table_cache: Array[int] = []

static func _ensure_xp_table() -> void:
	if _xp_table_cache.size() >= MAX_LEVEL:
		return
	_xp_table_cache.clear()
	var cumulative: int = 0
	_xp_table_cache.append(0)  # Level 1 = 0 XP
	for lv in range(2, MAX_LEVEL + 1):
		var needed = 300 + (lv - 2) * 200
		cumulative += needed
		_xp_table_cache.append(cumulative)

## 获取120级累计经验值表
static func get_xp_table() -> Array[int]:
	_ensure_xp_table()
	return _xp_table_cache

## 获取指定等级的累计经验值
static func get_xp_for_level(level: int) -> int:
	if level < 1 or level > MAX_LEVEL:
		return 0
	_ensure_xp_table()
	return _xp_table_cache[level - 1]

## 根据累计XP反查当前等级
static func get_level_from_xp(xp: int) -> int:
	_ensure_xp_table()
	for i in range(_xp_table_cache.size() - 1, -1, -1):
		if xp >= _xp_table_cache[i]:
			return i + 1
	return 1

## 获取升到下一级所需的额外经验值
static func get_xp_to_next_level(current_xp: int) -> int:
	var current_level = get_level_from_xp(current_xp)
	if current_level >= MAX_LEVEL:
		return 0
	var next_level = current_level + 1
	return get_xp_for_level(next_level) - current_xp


# ============================================================================
# 专精加值表（适配120级）
# 每16级提升1点，1级=+2，120级=+9
# ============================================================================

## 获取专精加值（按等级区间）
static func get_proficiency_bonus(level: int) -> int:
	if level <= 0:
		return 0
	elif level <= 4:
		return 2
	elif level <= 12:
		return 3
	elif level <= 20:
		return 4
	elif level <= 32:
		return 5
	elif level <= 48:
		return 6
	elif level <= 64:
		return 7
	elif level <= 80:
		return 8
	elif level <= 96:
		return 9
	elif level <= 120:
		return 10
	else:
		return 10



# ============================================================================
# 属性修正值
# 10/11=0, 12/13=+1, 14/15=+2 ... 与标准D&D一致
# ============================================================================

## 计算属性修饰值
static func get_stat_modifier(score: int) -> int:
	return floor((score - 10) / 2.0)


# ============================================================================
# 属性点数系统（120级核心）
# 所有生物统一：1级属性总值=25，每级+1自由点
# 120级时总属性=25+119=144，平均每项24
# ============================================================================

## 计算指定等级的总属性点数（基础+升级）
static func get_total_attr_points(level: int) -> int:
	return BASE_ATTR_TOTAL + (maxi(1, level) - 1) * ATTR_PER_LEVEL

## 计算当前属性总值
static func get_attrs_sum(attrs: Dictionary) -> int:
	var total = 0
	for key in ATTR_KEYS:
		total += attrs.get(key, 0)
	return total

## 计算剩余未分配点数
static func get_unspent_points(attrs: Dictionary, level: int) -> int:
	return get_total_attr_points(level) - get_attrs_sum(attrs)

## 检查属性是否合法（总值不超，单项在范围内）
static func is_attrs_valid(attrs: Dictionary, level: int) -> bool:
	if get_unspent_points(attrs, level) < 0:
		return false
	for key in ATTR_KEYS:
		var val = attrs.get(key, 0)
		if val < ATTR_MIN or val > ATTR_MAX:
			return false
	return true

## 创建均匀初始属性分配（用于初始化）
static func create_default_attrs(level: int) -> Dictionary:
	var total = get_total_attr_points(level)
	var base = total / 6
	var remainder = total % 6
	var attrs = {}
	var keys = ATTR_KEYS
	for i in range(6):
		attrs[keys[i]] = base + (1 if i < remainder else 0)
	return attrs


# ============================================================================
# 等级 → CR 映射（敌人等级体系核心）
# CR = floor(level / 6)
# 1级=CR0, 6级=CR1, 12级=CR2, 30级=CR5, 60级=CR10, 120级=CR20
# ============================================================================

## 根据等级计算威胁等级(CR)
static func get_cr_from_level(level: int) -> float:
	if level <= 0:
		return 0.0
	return floorf(level / 6.0)

## 根据CR反推推荐等级（CR×6，用于遭遇设计）
static func get_level_from_cr(cr: float) -> int:
	return maxi(1, int(cr * 6))


# ============================================================================
# HP 计算公式（120级体系）
# HP = 基础HP + CON修正 × 等级
# 基础HP: 玩家=10, 杂兵=6~8, 精英=12, Boss=16, 传奇=20
# ============================================================================

## 计算最大HP（基础HP + CON修正×等级）
## base_hp: 生物类型的HP基数
## con_score: CON属性原始值
## level: 等级
static func calculate_max_hp(base_hp: int, con_score: int, level: int) -> int:
	var con_mod = get_stat_modifier(con_score)
	return maxi(1, base_hp + con_mod * level)


# ============================================================================
# AC 计算参考值（用于模板设计参考）
# AC = 10 + DEX贡献(floor(sqrt(DEX/2))) + 护甲 + 盾牌 + 技能
# ============================================================================

## 计算DEX对AC的贡献
static func calculate_dex_ac(dex: int, max_dex_bonus: int = 99) -> int:
	var dex_ac = int(floorf(sqrt(dex / 2.0)))
	return mini(dex_ac, max_dex_bonus)


# ============================================================================
# d20 命中率体系（适配120级）
# 核心：d20 + 攻击加成 vs 目标AC
# 全期目标命中率 ~70%，通过数值曲线保证
# ============================================================================

## 计算命中率百分比（0.0~1.0）
static func calculate_hit_chance(attack_bonus: int, target_ac: int, has_advantage: bool, has_disadvantage: bool) -> float:
	if has_advantage and has_disadvantage:
		has_advantage = false
		has_disadvantage = false

	var needed = target_ac - attack_bonus
	var normal_chance = clampf((21.0 - needed) / 20.0, 0.0, 1.0)

	if has_advantage:
		return 1.0 - (1.0 - normal_chance) * (1.0 - normal_chance)
	elif has_disadvantage:
		return normal_chance * normal_chance
	else:
		return normal_chance

## 掷1个d20（命中专用）
static func roll_d20() -> int:
	return randi_range(1, 20)

## 掷N面骰子（通用）
static func roll_dice(count: int, sides: int) -> int:
	var total = 0
	for i in range(count):
		total += randi_range(1, sides)
	return total

## 掷 Nd20（伤害专用）
## 骰子数随等级增长，伤害自然缩放
## 返回: { "rolls": Array[int], "total": int, "count": int }
static func roll_nd20(count: int) -> Dictionary:
	count = maxi(1, count)
	var rolls: Array[int] = []
	var total := 0
	for i in range(count):
		var r = randi_range(1, 20)
		rolls.append(r)
		total += r
	return {
		"rolls": rolls,
		"total": total,
		"count": count,
	}

## 根据等级获取伤害骰子数
## 1级=1d20, 每20级多1个d20
## 1~20=1d20, 21~40=2d20, 41~60=3d20, 61~80=4d20, 81~100=5d20, 101~120=6d20
static func get_damage_dice_count(level: int) -> int:
	if level <= 0:
		return 1
	return mini(6, 1 + (level - 1) / 20)

## 优势掷骰（掷两次取较高）
static func roll_with_advantage() -> Dictionary:
	var r1 = randi_range(1, 20)
	var r2 = randi_range(1, 20)
	return {"result": maxi(r1, r2), "roll1": r1, "roll2": r2}

## 劣势掷骰（掷两次取较低）
static func roll_with_disadvantage() -> Dictionary:
	var r1 = randi_range(1, 20)
	var r2 = randi_range(1, 20)
	return {"result": mini(r1, r2), "roll1": r1, "roll2": r2}



# ============================================================================
# 豁免检定
# 对应策划案 02-RPG系统 → 豁免检定
# ============================================================================

## 执行一次豁免检定
## ability_score: 对应属性的原始分值
## proficiency_bonus: 角色的专精加值
## is_proficient: 是否精通此豁免
## dc: 难度等级
## has_advantage / has_disadvantage: 优劣势
## 返回: {"success": bool, "roll": int, "modifier": int, "total": int, "dc": int}
static func make_save(ability_score: int, proficiency_bonus: int, is_proficient: bool, dc: int, has_advantage: bool = false, has_disadvantage: bool = false) -> Dictionary:
	# 优势劣势互相抵消
	if has_advantage and has_disadvantage:
		has_advantage = false
		has_disadvantage = false

	var modifier = get_stat_modifier(ability_score)
	if is_proficient:
		modifier += proficiency_bonus

	var roll: int
	if has_advantage:
		roll = roll_with_advantage()["result"]
	elif has_disadvantage:
		roll = roll_with_disadvantage()["result"]
	else:
		roll = roll_d20()

	var total = roll + modifier

	return {
		"success": total >= dc,
		"roll": roll,
		"modifier": modifier,
		"total": total,
		"dc": dc,
	}

## 获取豁免类型对应的属性键名
static func get_save_ability(save_type: SaveType) -> String:
	match save_type:
		SaveType.FORTITUDE: return "con"
		SaveType.REFLEX: return "dex"
		SaveType.WILL: return "wis"
		_: return "con"


# ============================================================================
# 法术DC计算
# 对应策划案 07-法术系统 → 施放规则
# ============================================================================

## 计算法术难度等级
## DC = 8 + 施法属性修正 + 专精加值
static func calculate_spell_dc(casting_ability_score: int, proficiency_bonus: int) -> int:
	return 8 + get_stat_modifier(casting_ability_score) + proficiency_bonus


# ============================================================================
# 伤势惩罚
# 对应策划案 05-角色与职业 → 角色状态
# ============================================================================

## 获取伤势检定惩罚（基于当前HP百分比）
static func get_wound_penalty(hp_percent: float) -> Dictionary:
	if hp_percent >= 0.5:
		return {"all_checks": 0, "name": "健康"}
	elif hp_percent >= 0.25:
		return {"all_checks": -1, "name": "轻伤"}
	elif hp_percent > 0.0:
		return {"all_checks": -2, "name": "重伤"}
	else:
		return {"all_checks": 0, "name": "濒死"}


# ============================================================================
# 难度等级DC参考表
# 对应策划案 02-RPG系统 → 难度等级(DC)
# ============================================================================

enum DifficultyDC {
	VERY_EASY = 5,
	EASY = 10,
	MEDIUM = 15,
	HARD = 20,
	VERY_HARD = 25,
	LEGENDARY = 30,
}

## 获取DC的玩家感知描述
static func get_dc_description(dc: int) -> String:
	if dc <= 5:
		return "轻而易举"
	elif dc <= 10:
		return "应该没问题"
	elif dc <= 15:
		return "有把握"
	elif dc <= 20:
		return "有些冒险"
	elif dc <= 25:
		return "希望渺茫"
	else:
		return "近乎不可能"
