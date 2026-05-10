# EquipmentAffix.gd
# 装备词缀系统 — 前缀/后缀词缀，用于动态生成装备属性变化
# 对应策划案 06-装备与物品.md → 装备词缀系统
extends Resource
class_name EquipmentAffix

# ============================================================================
# 词缀类型枚举
# ============================================================================

## 词缀位置（前缀修饰名称前部，后缀修饰名称后部）
enum AffixPosition {
	PREFIX,  # 前缀：如"烈焰"长剑
	SUFFIX,  # 后缀：如长剑"锋利"
}

## 词缀作用目标（词缀影响哪类装备）
enum AffixTarget {
	ANY,       # 任意装备
	WEAPON,    # 仅武器
	ARMOR,     # 仅防具
	SHIELD,    # 仅盾牌
	ACCESSORY, # 仅饰品
}

## 词缀效果类型
enum AffixEffectType {
	FLAT_STAT,       # 固定属性加成（如 STR+2）
	DICE_BONUS,      # 骰子加成（如 伤害+1d4）
	PERCENT_STAT,    # 百分比属性加成（如 伤害+10%）
	CONDITIONAL,     # 条件触发（如 对亡灵伤害+1d6）
	SPECIAL,         # 特殊效果（由运行时处理）
}

# ============================================================================
# 数据字段
# ============================================================================

## 词缀唯一ID
@export var affix_id: String = ""

## 词缀名称（显示用）
@export var affix_name: String = ""

## 词缀描述
@export_multiline var description: String = ""

## 是否为前缀
@export var is_prefix: bool = true

## 词缀作用目标
@export var target: AffixTarget = AffixTarget.ANY

## 效果类型
@export var effect_type: AffixEffectType = AffixEffectType.FLAT_STAT

## 最低物品等级（词缀出现的最低等级）
@export var min_item_level: int = 1

## 最高物品等级
@export var max_item_level: int = 20

## 词缀权重（影响随机抽取概率）
@export var weight: float = 1.0

## 最低稀有度要求（词缀最低出现在什么稀有度）
@export var min_rarity: int = ItemData.Rarity.COMMON

# ============================================================================
# 固定属性加成 (FLAT_STAT)
# ============================================================================

@export var str_bonus: int = 0
@export var dex_bonus: int = 0
@export var con_bonus: int = 0
@export var int_bonus: int = 0
@export var wis_bonus: int = 0
@export var cha_bonus: int = 0

@export var hp_bonus: int = 0
@export var ac_bonus: int = 0
@export var move_bonus: int = 0
@export var initiative_bonus: int = 0

# ============================================================================
# 武器专属加成
# ============================================================================

@export var damage_dice_count_bonus: int = 0   # 额外伤害骰数量
@export var damage_dice_sides_bonus: int = 0   # 额外伤害骰面数
@export var attack_bonus: int = 0               # 命中加成
@export var damage_bonus: int = 0               # 固定伤害加成
@export var crit_range_bonus: int = 0           # 暴击范围扩大（如 20→19-20）
@export var crit_multiplier_bonus: int = 0      # 暴击倍率加成

# ============================================================================
# 防具专属加成
# ============================================================================

@export var resistance: String = ""          # 伤害抗性类型（如 "fire", "cold"）
@export var immunity: String = ""            # 免疫类型

# ============================================================================
# 条件触发效果 (CONDITIONAL)
# ============================================================================

## 条件类型（如 "vs_undead", "vs_cavalry", "low_hp", "mounted"）
@export var condition: String = ""

## 条件触发时的额外伤害骰数
@export var conditional_damage_dice_count: int = 0
@export var conditional_damage_dice_sides: int = 0
@export var conditional_damage_type: String = ""

## 条件触发时的命中加成
@export var conditional_attack_bonus: int = 0

# ============================================================================
# 特殊效果 (SPECIAL)
# ============================================================================

## 特殊效果标识（运行时由系统解读）
@export var special_effect: String = ""
@export var special_value: float = 0.0


# ============================================================================
# 方法
# ============================================================================

## 获取词缀效果描述文本
func get_effect_description() -> String:
	var parts: Array[String] = []

	# 属性加成
	if str_bonus != 0: parts.append("力量%+d" % str_bonus)
	if dex_bonus != 0: parts.append("敏捷%+d" % dex_bonus)
	if con_bonus != 0: parts.append("体质%+d" % con_bonus)
	if int_bonus != 0: parts.append("智力%+d" % int_bonus)
	if wis_bonus != 0: parts.append("感知%+d" % wis_bonus)
	if cha_bonus != 0: parts.append("魅力%+d" % cha_bonus)
	if hp_bonus != 0: parts.append("HP%+d" % hp_bonus)
	if ac_bonus != 0: parts.append("AC%+d" % ac_bonus)
	if move_bonus != 0: parts.append("移动%+d" % move_bonus)
	if initiative_bonus != 0: parts.append("先攻%+d" % initiative_bonus)

	# 武器加成
	if attack_bonus != 0: parts.append("命中%+d" % attack_bonus)
	if damage_bonus != 0: parts.append("伤害%+d" % damage_bonus)
	if damage_dice_count_bonus > 0 and damage_dice_sides_bonus > 0:
		parts.append("+%dd%d伤害" % [damage_dice_count_bonus, damage_dice_sides_bonus])
	if crit_range_bonus != 0: parts.append("暴击范围%+d" % crit_range_bonus)
	if crit_multiplier_bonus != 0: parts.append("暴击倍率%+d" % crit_multiplier_bonus)

	# 抗性/免疫
	if resistance != "": parts.append("%s抗性" % resistance)
	if immunity != "": parts.append("免疫%s" % immunity)

	# 条件触发
	if condition != "":
		var _cond_text = _condition_text(condition)
		if conditional_damage_dice_count > 0:
			parts.append("%s:+%dd%d%s伤害" % [_cond_text, conditional_damage_dice_count, conditional_damage_dice_sides, conditional_damage_type])
		if conditional_attack_bonus != 0:
			parts.append("%s:命中%+d" % [_cond_text, conditional_attack_bonus])

	# 特殊
	if special_effect != "":
		parts.append(_special_text(special_effect, special_value))

	return "，".join(parts) if parts.is_empty() else "，".join(parts)


## 条件文本转换
func _condition_text(cond: String) -> String:
	match cond:
		"vs_undead": return "对亡灵"
		"vs_cavalry": return "对骑兵"
		"vs_beast": return "对野兽"
		"vs_demon": return "对魔物"
		"low_hp": return "低HP时"
		"mounted": return "骑乘时"
		"first_attack": return "首次攻击"
		"flanking": return "包夹时"
		"high_ground": return "高地时"
		_: return cond


## 特殊效果文本转换
func _special_text(effect: String, value: float) -> String:
	match effect:
		"life_steal": return "攻击回复%.0f%%伤害HP" % (value * 100)
		"thorns": return "被击时反弹%.0f伤害" % value
		"cleave": return "击杀时对邻敌造成%.0f伤害" % value
		"chain_lightning": return "命中时跳跃%.0f个目标" % value
		"on_crit_effect": return "暴击时触发额外效果"
		"on_kill_reset": return "击杀时重置行动"
		"extra_attack": return "额外攻击%.0f次" % value
		_: return effect


# ============================================================================
# 静态工厂：预定义词缀库
# ============================================================================

## 获取所有前缀词缀
static func get_prefix_affixes() -> Array[EquipmentAffix]:
	var result: Array[EquipmentAffix] = []
	result.append(_make_prefix("flaming", "烈焰", "火焰伤害加成", AffixTarget.WEAPON, 3, 0.8,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, "fire"))
	result.append(_make_prefix("frost", "寒冰", "冰冷伤害加成", AffixTarget.WEAPON, 3, 0.8,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, "cold"))
	result.append(_make_prefix("shocking", "电弧", "闪电伤害加成", AffixTarget.WEAPON, 5, 0.6,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, "lightning"))
	result.append(_make_prefix("brutal", "残暴", "近战伤害加成", AffixTarget.WEAPON, 1, 1.0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, ""))
	result.append(_make_prefix("precise", "精准", "命中加成", AffixTarget.WEAPON, 1, 1.0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""))
	result.append(_make_prefix("sturdy", "坚固", "防具AC加成", AffixTarget.ARMOR, 1, 1.0,
		0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, ""))
	result.append(_make_prefix("agile", "敏捷", "防具敏捷加成", AffixTarget.ARMOR, 1, 0.8,
		0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""))
	result.append(_make_prefix("mighty", "力量", "力量加成", AffixTarget.ANY, 1, 1.0,
		1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""))
	result.append(_make_prefix("wise", "智慧", "智力加成", AffixTarget.ANY, 1, 1.0,
		0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""))
	result.append(_make_prefix("holy", "圣洁", "对亡灵伤害加成", AffixTarget.WEAPON, 5, 0.5,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "",
		"vs_undead", 1, 6, "radiant"))
	result.append(_make_prefix("cavalry_slayer", "骑杀", "对骑兵伤害加成", AffixTarget.WEAPON, 3, 0.5,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "",
		"vs_cavalry", 1, 8, "pierce"))
	result.append(_make_prefix("vital", "生命", "HP加成", AffixTarget.ANY, 1, 1.0,
		0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""))
	result.append(_make_prefix("swift", "迅捷", "移动加成", AffixTarget.ANY, 1, 0.7,
		0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, ""))
	return result


## 获取所有后缀词缀
static func get_suffix_affixes() -> Array[EquipmentAffix]:
	var result: Array[EquipmentAffix] = []
	result.append(_make_suffix("of_power", "力量", "力量加成", AffixTarget.ANY, 1, 1.0,
		2, 0, 0, 0, 0, 0, 0, 0, 0))
	result.append(_make_suffix("of_agility", "灵巧", "敏捷加成", AffixTarget.ANY, 1, 1.0,
		0, 2, 0, 0, 0, 0, 0, 0, 0))
	result.append(_make_suffix("of_vitality", "活力", "体质加成", AffixTarget.ANY, 1, 1.0,
		0, 0, 2, 0, 0, 0, 0, 0, 0))
	result.append(_make_suffix("of_intellect", "睿智", "智力加成", AffixTarget.ANY, 1, 1.0,
		0, 0, 0, 2, 0, 0, 0, 0, 0))
	result.append(_make_suffix("of_wisdom", "洞察", "感知加成", AffixTarget.ANY, 1, 1.0,
		0, 0, 0, 0, 2, 0, 0, 0, 0))
	result.append(_make_suffix("of_command", "统帅", "魅力加成", AffixTarget.ANY, 1, 0.8,
		0, 0, 0, 0, 0, 2, 0, 0, 0))
	result.append(_make_suffix("of_the_bear", "巨熊", "力量+体质加成", AffixTarget.ARMOR, 3, 0.5,
		1, 0, 1, 0, 0, 0, 0, 0, 0))
	result.append(_make_suffix("of_the_eagle", "苍鹰", "敏捷+感知加成", AffixTarget.ARMOR, 3, 0.5,
		0, 1, 0, 0, 1, 0, 0, 0, 0))
	result.append(_make_suffix("of_fire_resist", "耐火", "火焰抗性", AffixTarget.ARMOR, 3, 0.6,
		0, 0, 0, 0, 0, 0, 0, 0, 0, "fire"))
	result.append(_make_suffix("of_cold_resist", "耐寒", "冰冷抗性", AffixTarget.ARMOR, 3, 0.6,
		0, 0, 0, 0, 0, 0, 0, 0, 0, "cold"))
	result.append(_make_suffix("of_sharpness", "锋利", "武器伤害加成", AffixTarget.WEAPON, 1, 1.0,
		0, 0, 0, 0, 0, 0, 0, 1, 0))
	result.append(_make_suffix("of_smiting", "猛击", "武器伤害骰加成", AffixTarget.WEAPON, 5, 0.5,
		0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 1, 4))
	result.append(_make_suffix("of_initiative", "先机", "先攻加成", AffixTarget.ACCESSORY, 1, 0.8,
		0, 0, 0, 0, 0, 0, 0, 0, 0, "", 2))
	result.append(_make_suffix("of_life_steal", "吸血", "攻击回复HP", AffixTarget.WEAPON, 8, 0.3,
		0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 0, 0, "life_steal", 0.1))
	return result


## 获取所有词缀（前缀+后缀）
static func get_all_affixes() -> Array[EquipmentAffix]:
	var result: Array[EquipmentAffix] = []
	result.append_array(get_prefix_affixes())
	result.append_array(get_suffix_affixes())
	return result


## 根据目标类型筛选可用词缀
static func get_affixes_for_target(target: AffixTarget, item_level: int, rarity: int) -> Array[EquipmentAffix]:
	var _all = get_all_affixes()
	var result: Array[EquipmentAffix] = []
	for affix in _all:
		if (affix.target == AffixTarget.ANY or affix.target == target) and \
		   item_level >= affix.min_item_level and \
		   item_level <= affix.max_item_level and \
		   rarity >= affix.min_rarity:
			result.append(affix)
	return result


# ============================================================================
# 内部工厂
# ============================================================================

static func _make_prefix(id: String, name: String, desc: String, target: AffixTarget, min_lvl: int, w: float,
	_str: int, _dex: int, _con: int, _int: int, _wis: int, _cha: int,
	_hp: int, _ac: int, _move: int, _init: int,
	_atk: int, _dmg: int, _dmg_dc: int, _dmg_ds: int,
	_crit_r: int, _crit_m: int, _resist: String = "",
	_cond: String = "", _cond_dc: int = 0, _cond_ds: int = 0, _cond_dt: String = "") -> EquipmentAffix:
	var _a = EquipmentAffix.new()
	_a.affix_id = id
	_a.affix_name = name
	_a.description = desc
	_a.is_prefix = true
	_a.target = target
	_a.min_item_level = min_lvl
	_a.weight = w
	_a.str_bonus = _str
	_a.dex_bonus = _dex
	_a.con_bonus = _con
	_a.int_bonus = _int
	_a.wis_bonus = _wis
	_a.cha_bonus = _cha
	_a.hp_bonus = _hp
	_a.ac_bonus = _ac
	_a.move_bonus = _move
	_a.initiative_bonus = _init
	_a.attack_bonus = _atk
	_a.damage_bonus = _dmg
	_a.damage_dice_count_bonus = _dmg_dc
	_a.damage_dice_sides_bonus = _dmg_ds
	_a.crit_range_bonus = _crit_r
	_a.crit_multiplier_bonus = _crit_m
	_a.resistance = _resist
	_a.condition = _cond
	_a.conditional_damage_dice_count = _cond_dc
	_a.conditional_damage_dice_sides = _cond_ds
	_a.conditional_damage_type = _cond_dt
	return _a


static func _make_suffix(id: String, name: String, desc: String, target: AffixTarget, min_lvl: int, w: float,
	_str: int, _dex: int, _con: int, _int: int, _wis: int, _cha: int,
	_hp: int, _ac: int, _move: int, _resist: String = "",
	_init: int = 0, _dmg_dc: int = 0, _dmg_ds: int = 0,
	_special: String = "", _special_val: float = 0.0) -> EquipmentAffix:
	var _a = EquipmentAffix.new()
	_a.affix_id = id
	_a.affix_name = name
	_a.description = desc
	_a.is_prefix = false
	_a.target = target
	_a.min_item_level = min_lvl
	_a.weight = w
	_a.str_bonus = _str
	_a.dex_bonus = _dex
	_a.con_bonus = _con
	_a.int_bonus = _int
	_a.wis_bonus = _wis
	_a.cha_bonus = _cha
	_a.hp_bonus = _hp
	_a.ac_bonus = _ac
	_a.move_bonus = _move
	_a.resistance = _resist
	_a.initiative_bonus = _init
	_a.damage_dice_count_bonus = _dmg_dc
	_a.damage_dice_sides_bonus = _dmg_ds
	_a.special_effect = _special
	_a.special_value = _special_val
	return _a
