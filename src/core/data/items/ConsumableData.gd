# ConsumableData.gd
# 消耗品数据 — 战斗中可使用的药剂、投掷物、卷轴等
# 对应策划案 06-装备与物品 → 物品与消耗品
extends ItemData
class_name ConsumableData

# ============================================================================
# 消耗品类型
# ============================================================================

enum ConsumableType {
	HEALING_POTION,   # 治疗药水
	STRONG_HEALING,   # 强效治疗药水
	ANTIDOTE,         # 解毒剂
	FIRE_OIL,         # 火油瓶
	HOLY_WATER,       # 圣水
	SPELL_SCROLL,     # 法术卷轴
	WHETSTONE,        # 磨刀石（战斗外）
}

# ============================================================================
# 数据字段
# ============================================================================

## 消耗品类型
@export var consumable_type: ConsumableType = ConsumableType.HEALING_POTION

## 治疗骰子
@export var heal_dice_count: int = 0
@export var heal_dice_sides: int = 0
@export var heal_bonus: int = 0

## 可解除的状态效果ID列表
@export var removes_status: Array[String] = []

## 伤害骰子（投掷物用）
@export var damage_dice_count: int = 0
@export var damage_dice_sides: int = 0
@export var damage_type: String = ""

## 范围伤害半径（0=单体，1=周围1格）
@export var aoe_radius: int = 0

## 投掷射程（格子）
@export var throw_range: int = 4

## 关联法术（卷轴用）
@export var linked_spell: SpellData

## 使用时机：main_action / minor_action
@export var use_action: String = "main_action"

## 战斗外使用
@export var usable_outside_combat: bool = false

## 使用后附带的状态效果
@export var applied_status: String = ""
@export var applied_status_duration: int = 0


# ============================================================================
# 辅助方法
# ============================================================================

## 获取消耗品类型显示名
func get_consumable_type_name() -> String:
	match consumable_type:
		ConsumableType.HEALING_POTION: return "治疗药水"
		ConsumableType.STRONG_HEALING: return "强效治疗药水"
		ConsumableType.ANTIDOTE: return "解毒剂"
		ConsumableType.FIRE_OIL: return "火油瓶"
		ConsumableType.HOLY_WATER: return "圣水"
		ConsumableType.SPELL_SCROLL: return "法术卷轴"
		ConsumableType.WHETSTONE: return "磨刀石"
		_: return "未知"

## 是否是投掷物
func is_throwable() -> bool:
	return consumable_type in [ConsumableType.FIRE_OIL, ConsumableType.HOLY_WATER]

## 获取效果文本描述
func get_effect_text() -> String:
	match consumable_type:
		ConsumableType.HEALING_POTION:
			return "恢复%dd%d+%d HP" % [heal_dice_count, heal_dice_sides, heal_bonus]
		ConsumableType.STRONG_HEALING:
			return "恢复%dd%d+%d HP" % [heal_dice_count, heal_dice_sides, heal_bonus]
		ConsumableType.ANTIDOTE:
			return "解除中毒状态"
		ConsumableType.FIRE_OIL:
			return "投掷至目标格，范围内%dd%d火伤×%d轮" % [damage_dice_count, damage_dice_sides, applied_status_duration]
		ConsumableType.HOLY_WATER:
			return "投掷至目标格，亡灵%dd%d伤害" % [damage_dice_count, damage_dice_sides]
		ConsumableType.SPELL_SCROLL:
			if linked_spell:
				return "施放一次%s" % linked_spell.spell_name
			return "施放一次法术"
		ConsumableType.WHETSTONE:
			return "本场战斗近战伤害+1"
		_: return ""
