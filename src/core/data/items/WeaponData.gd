# WeaponData.gd
# 武器数据，包含伤害骰子和武器特性
# 对应策划案 06-装备与物品.md → 武器系统
extends ItemData
class_name WeaponData

# ============================================================================
# 枚举
# ============================================================================

enum DamageType { SLASH, PIERCE, CRUSH, MAGIC }
enum WeaponCategory { SIMPLE, MARTIAL, EXOTIC }

# ============================================================================
# 基础武器属性
# ============================================================================

@export var damage_dice_count: int = 1
@export var damage_dice_sides: int = 8
@export var damage_type: DamageType = DamageType.SLASH
@export var category: WeaponCategory = WeaponCategory.SIMPLE

# ============================================================================
# 武器特性 (Traits)
# 对应策划案 06 → 武器属性
# ============================================================================

@export var is_two_handed: bool = false
@export var is_finesse: bool = false       # 灵巧武器：可使用敏捷替代力量
@export var is_ranged: bool = false         # 远程武器
@export var range_cells: int = 1            # 射程/触及范围
@export var is_throwing: bool = false       # 投掷武器
@export var throw_range: int = 3            # 投掷射程
@export var needs_reload: bool = false      # 需要装填（如十字弩）
@export var is_blunt: bool = false          # 钝击伤害（对亡灵全额）
@export var is_armor_piercing: bool = false # 破甲（计算命中时目标AC-2）
@export var is_reach: bool = false          # 长柄（近战攻击范围2格）
@export var is_anti_cavalry: bool = false   # 反骑兵（对冲锋目标伤害×2）
@export var is_sweep: bool = false          # 横扫（攻击相邻2个敌人时各-2命中）
@export var str_required: int = 0           # 最低力量需求
@export var is_catalyst: bool = false       # 法术触媒（法杖/魔导书）
@export var spell_dc_bonus: int = 0         # 法术DC加成（魔导书+1）
@export var is_dual_wieldable: bool = false # 可双持（轻巧武器）

# ============================================================================
# 词缀加成（运行时累加）
# ============================================================================

## 词缀带来的额外伤害骰
var bonus_damage_dice_count: int = 0
var bonus_damage_dice_sides: int = 0

## 词缀带来的命中加成
var bonus_attack: int = 0

## 词缀带来的固定伤害加成
var bonus_damage: int = 0

## 词缀带来的暴击范围加成
var bonus_crit_range: int = 0

## 词缀带来的暴击倍率加成
var bonus_crit_multiplier: int = 0

## 词缀带来的条件触发效果
var bonus_conditional_effects: Array[Dictionary] = []


# ============================================================================
# 词缀应用
# ============================================================================

func _apply_affix(affix: EquipmentAffix) -> void:
	# 基础属性加成
	bonus_attack += affix.attack_bonus
	bonus_damage += affix.damage_bonus
	bonus_damage_dice_count += affix.damage_dice_count_bonus
	bonus_damage_dice_sides += affix.damage_dice_sides_bonus
	bonus_crit_range += affix.crit_range_bonus
	bonus_crit_multiplier += affix.crit_multiplier_bonus

	# 条件触发效果
	if affix.condition != "" and (affix.conditional_damage_dice_count > 0 or affix.conditional_attack_bonus != 0):
		bonus_conditional_effects.append({
			"condition": affix.condition,
			"damage_dice_count": affix.conditional_damage_dice_count,
			"damage_dice_sides": affix.conditional_damage_dice_sides,
			"damage_type": affix.conditional_damage_type,
			"attack_bonus": affix.conditional_attack_bonus,
		})

	# 特殊效果
	if affix.special_effect != "":
		bonus_conditional_effects.append({
			"condition": "special",
			"effect": affix.special_effect,
			"value": affix.special_value,
		})


# ============================================================================
# 获取方法（含词缀加成）
# ============================================================================

## 获取总伤害骰数（基础+词缀）
func get_total_damage_dice_count() -> int:
	return damage_dice_count + bonus_damage_dice_count

## 获取总伤害骰面（基础+词缀，词缀骰单独掷）
func get_total_damage_dice_sides() -> int:
	return damage_dice_sides

## 获取总命中加成（词缀）
func get_total_attack_bonus() -> int:
	return bonus_attack

## 获取总固定伤害加成（词缀）
func get_total_damage_bonus() -> int:
	return bonus_damage

## 获取暴击范围（20 - bonus_crit_range）
func get_crit_range() -> int:
	return 20 - bonus_crit_range

## 获取暴击倍率（2 + bonus_crit_multiplier）
func get_crit_multiplier() -> int:
	return 2 + bonus_crit_multiplier

## 获取条件触发效果列表
func get_conditional_effects() -> Array[Dictionary]:
	return bonus_conditional_effects

## 获取完整武器描述（含词缀）
func get_weapon_description() -> String:
	var parts: Array[String] = []
	parts.append("伤害: %dd%d" % [damage_dice_count, damage_dice_sides])
	if bonus_damage_dice_count > 0:
		parts.append("+%dd%d" % [bonus_damage_dice_count, bonus_damage_dice_sides])
	if bonus_damage > 0:
		parts.append("+%d" % bonus_damage)
	if bonus_attack > 0:
		parts.append("命中%+d" % bonus_attack)

	var traits: Array[String] = []
	if is_two_handed: traits.append("双手")
	if is_finesse: traits.append("灵巧")
	if is_ranged: traits.append("远程")
	if is_throwing: traits.append("投掷(%d)" % throw_range)
	if needs_reload: traits.append("装填")
	if is_blunt: traits.append("钝击")
	if is_armor_piercing: traits.append("破甲")
	if is_reach: traits.append("长柄")
	if is_anti_cavalry: traits.append("反骑")
	if is_sweep: traits.append("横扫")
	if is_catalyst: traits.append("触媒")
	if is_dual_wieldable: traits.append("双持")
	if not traits.is_empty():
		parts.append("特性: " + "/".join(traits))

	return " ".join(parts)
