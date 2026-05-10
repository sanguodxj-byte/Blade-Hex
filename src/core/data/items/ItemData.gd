# ItemData.gd
# 所有物品的基类 — 增加稀有度、物品ID、词缀槽位
# 对应策划案 06-装备与物品.md
extends Resource
class_name ItemData

# ============================================================================
# 稀有度枚举
# ============================================================================

enum Rarity {
	COMMON,     # 普通 — 白色，无词缀
	UNCOMMON,   # 优秀 — 绿色，1个词缀
	RARE,       # 稀有 — 蓝色，2个词缀
	EPIC,       # 史诗 — 紫色，3个词缀
	LEGENDARY,  # 传说 — 橙色，固定唯一效果
}

# ============================================================================
# 数据字段
# ============================================================================

## 物品唯一ID（用于注册表查找）
@export var item_id: String = ""

## 物品名称
@export var item_name: String = "未命名物品"

## 物品描述
@export_multiline var description: String = ""

## 物品图标
@export var icon: Texture2D

## 重量
@export var weight: float = 1.0

## 基础价格（稀有度会影响实际售价）
@export var price: int = 10

## 稀有度
@export var rarity: Rarity = Rarity.COMMON

## 已附加的词缀列表（生成时动态附加）
@export var affixes: Array[EquipmentAffix] = []

## 物品来源/掉落区域标签（用于战利品表筛选）
@export var source_tags: Array[String] = []

## 是否为唯一物品（传说级物品不可重复获取）
@export var is_unique: bool = false

## 物品等级（影响词缀生成范围和缩放）
@export var item_level: int = 1


# ============================================================================
# 稀有度工具方法
# ============================================================================

## 获取稀有度显示名
func get_rarity_name() -> String:
	match rarity:
		Rarity.COMMON: return "普通"
		Rarity.UNCOMMON: return "优秀"
		Rarity.RARE: return "稀有"
		Rarity.EPIC: return "史诗"
		Rarity.LEGENDARY: return "传说"
		_: return "普通"

## 获取稀有度颜色（用于UI显示）
func get_rarity_color() -> Color:
	match rarity:
		Rarity.COMMON: return Color(0.9, 0.9, 0.9)      # 白色
		Rarity.UNCOMMON: return Color(0.3, 0.9, 0.3)    # 绿色
		Rarity.RARE: return Color(0.3, 0.5, 1.0)        # 蓝色
		Rarity.EPIC: return Color(0.7, 0.3, 1.0)        # 紫色
		Rarity.LEGENDARY: return Color(1.0, 0.6, 0.0)   # 橙色
		_: return Color.WHITE

## 获取实际售价（稀有度倍率）
func get_sell_price() -> int:
	var multiplier = 1.0
	match rarity:
		Rarity.COMMON: multiplier = 1.0
		Rarity.UNCOMMON: multiplier = 1.5
		Rarity.RARE: multiplier = 2.5
		Rarity.EPIC: multiplier = 5.0
		Rarity.LEGENDARY: multiplier = 10.0
	return int(price * multiplier)

## 获取词缀数量上限
func get_max_affix_count() -> int:
	match rarity:
		Rarity.COMMON: return 0
		Rarity.UNCOMMON: return 1
		Rarity.RARE: return 2
		Rarity.EPIC: return 3
		Rarity.LEGENDARY: return 0  # 传说级使用固定唯一效果
		_: return 0

## 是否可以附加词缀
func can_add_affix() -> bool:
	return affixes.size() < get_max_affix_count()

## 附加一个词缀
func add_affix(affix: EquipmentAffix) -> bool:
	if not can_add_affix():
		return false
	affixes.append(affix)
	_apply_affix(affix)
	return true

## 生成完整名称（包含词缀前缀/后缀）
func get_full_name() -> String:
	if affixes.is_empty():
		return item_name
	var prefix = ""
	var suffix = ""
	for affix in affixes:
		if affix.is_prefix:
			if prefix != "":
				prefix += "·"
			prefix += affix.affix_name
		else:
			if suffix != "":
				suffix += "·"
			suffix += affix.affix_name
	var result = ""
	if prefix != "":
		result = prefix + " "
	result += item_name
	if suffix != "":
		result += " " + suffix
	return result

## 获取所有词缀效果的文本描述
func get_affix_descriptions() -> String:
	if affixes.is_empty():
		return ""
	var descs: Array[String] = []
	for affix in affixes:
		descs.append(affix.affix_name + ": " + affix.get_effect_description())
	return "\n".join(descs)


# ============================================================================
# 词缀应用（子类可重写）
# ============================================================================

## 将词缀效果应用到物品上（由子类重写以实现具体属性加成）
func _apply_affix(_affix: EquipmentAffix) -> void:
	pass
