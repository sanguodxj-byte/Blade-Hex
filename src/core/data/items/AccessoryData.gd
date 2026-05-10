# AccessoryData.gd
# 饰品数据 — 戒指、项链等，提供属性加成和特殊效果
# 对应策划案 06-装备与物品.md → 装备槽位总览（饰品×2）
extends ItemData
class_name AccessoryData

# ============================================================================
# 饰品类型枚举
# ============================================================================

enum AccessoryType {
	RING,       # 戒指
	AMULET,     # 项链
	CLOAK,      # 斗篷
	BELT,       # 腰带
	BRACER,     # 护腕
}

# ============================================================================
# 数据字段
# ============================================================================

## 饰品类型
@export var accessory_type: AccessoryType = AccessoryType.RING

## 属性加成（固定值）
@export var str_bonus: int = 0
@export var dex_bonus: int = 0
@export var con_bonus: int = 0
@export var int_bonus: int = 0
@export var wis_bonus: int = 0
@export var cha_bonus: int = 0

## 战斗属性加成
@export var hp_bonus: int = 0
@export var ac_bonus: int = 0
@export var move_bonus: int = 0
@export var initiative_bonus: int = 0

## 伤害抗性类型
@export var resistance: String = ""

## 免疫类型
@export var immunity: String = ""

## 特殊效果标识
@export var special_effect: String = ""
@export var special_value: float = 0.0


# ============================================================================
# 方法
# ============================================================================

## 获取饰品类型显示名
func get_accessory_type_name() -> String:
	match accessory_type:
		AccessoryType.RING: return "戒指"
		AccessoryType.AMULET: return "项链"
		AccessoryType.CLOAK: return "斗篷"
		AccessoryType.BELT: return "腰带"
		AccessoryType.BRACER: return "护腕"
		_: return "饰品"

## 获取效果描述文本
func get_effect_text() -> String:
	var parts: Array[String] = []
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
	if resistance != "": parts.append("%s抗性" % resistance)
	if immunity != "": parts.append("免疫%s" % immunity)
	if special_effect != "":
		parts.append(_special_effect_text(special_effect, special_value))
	return "，".join(parts)


## 应用词缀效果到饰品
func _apply_affix(affix: EquipmentAffix) -> void:
	str_bonus += affix.str_bonus
	dex_bonus += affix.dex_bonus
	con_bonus += affix.con_bonus
	int_bonus += affix.int_bonus
	wis_bonus += affix.wis_bonus
	cha_bonus += affix.cha_bonus
	hp_bonus += affix.hp_bonus
	ac_bonus += affix.ac_bonus
	move_bonus += affix.move_bonus
	initiative_bonus += affix.initiative_bonus
	if affix.resistance != "" and resistance == "":
		resistance = affix.resistance
	if affix.special_effect != "" and special_effect == "":
		special_effect = affix.special_effect
		special_value = affix.special_value


func _special_effect_text(effect: String, value: float) -> String:
	match effect:
		"life_steal": return "攻击回复%.0f%%伤害HP" % (value * 100)
		"thorns": return "被击时反弹%.0f伤害" % value
		"extra_hp_percent": return "HP+%.0f%%" % (value * 100)
		"damage_reduction": return "伤害减免%.0f" % value
		"spell_dc_bonus": return "法术DC%+.0f" % value
		"shop_discount": return "商店价格-%.0f%%" % (value * 100)
		"recruit_discount": return "招募价格-%.0f%%" % (value * 100)
		"flanking_bonus": return "包夹时命中%+.0f" % value
		_: return effect


# ============================================================================
# 静态工厂：预定义饰品
# ============================================================================

static func get_all_accessories() -> Array[AccessoryData]:
	var result: Array[AccessoryData] = []
	result.append(_create_ring_of_power())
	result.append(_create_amulet_of_vitality())
	result.append(_create_cloak_of_protection())
	result.append(_create_belt_of_giant_strength())
	result.append(_create_bracer_of_archery())
	return result

static func get_ring_of_power() -> AccessoryData:
	return _create_ring_of_power()

static func get_amulet_of_vitality() -> AccessoryData:
	return _create_amulet_of_vitality()

static func get_cloak_of_protection() -> AccessoryData:
	return _create_cloak_of_protection()

static func get_belt_of_giant_strength() -> AccessoryData:
	return _create_belt_of_giant_strength()

static func get_bracer_of_archery() -> AccessoryData:
	return _create_bracer_of_archery()

static func _create_ring_of_power() -> AccessoryData:
	var a = AccessoryData.new()
	a.item_id = "ring_of_power"
	a.item_name = "力量戒指"
	a.accessory_type = AccessoryType.RING
	a.str_bonus = 2
	a.price = 120
	a.rarity = Rarity.UNCOMMON
	a.description = "一枚镶嵌红宝石的戒指，佩戴者感到力量涌动。"
	return a

static func _create_amulet_of_vitality() -> AccessoryData:
	var a = AccessoryData.new()
	a.item_id = "amulet_of_vitality"
	a.item_name = "活力项链"
	a.accessory_type = AccessoryType.AMULET
	a.con_bonus = 2
	a.hp_bonus = 5
	a.price = 150
	a.rarity = Rarity.UNCOMMON
	a.description = "一条散发着温暖光芒的项链。"
	return a

static func _create_cloak_of_protection() -> AccessoryData:
	var a = AccessoryData.new()
	a.item_id = "cloak_of_protection"
	a.item_name = "防护斗篷"
	a.accessory_type = AccessoryType.CLOAK
	a.ac_bonus = 1
	a.resistance = "magic"
	a.price = 250
	a.rarity = Rarity.RARE
	a.description = "一层薄薄的魔法防护环绕着穿戴者。"
	return a

static func _create_belt_of_giant_strength() -> AccessoryData:
	var a = AccessoryData.new()
	a.item_id = "belt_of_giant_strength"
	a.item_name = "巨人力量腰带"
	a.accessory_type = AccessoryType.BELT
	a.str_bonus = 4
	a.price = 800
	a.rarity = Rarity.EPIC
	a.description = "蕴含巨人力量的腰带，佩戴者力大无穷。"
	return a

static func _create_bracer_of_archery() -> AccessoryData:
	var a = AccessoryData.new()
	a.item_id = "bracer_of_archery"
	a.item_name = "射术护腕"
	a.accessory_type = AccessoryType.BRACER
	a.dex_bonus = 2
	a.initiative_bonus = 2
	a.price = 180
	a.rarity = Rarity.UNCOMMON
	a.description = "为射手设计的精致护腕，增强灵活性和反应速度。"
	return a