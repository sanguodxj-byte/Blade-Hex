# ArmorData.gd
# 防具与盾牌数据 — 增加词缀系统支持
# 对应策划案 06-装备与物品.md → 防具系统
extends ItemData
class_name ArmorData

# ============================================================================
# 枚举
# ============================================================================

enum ArmorType { LIGHT, MEDIUM, HEAVY, SHIELD }

# ============================================================================
# 基础属性
# ============================================================================

@export var armor_type: ArmorType = ArmorType.LIGHT
@export var ac_bonus: int = 1
@export var max_dex_bonus: int = 99 # 99 代表无限制 (轻甲)
@export var movement_penalty: int = 0

# ============================================================================
# 装甲值 (Damage Reduction)
# 人形单位穿戴护甲获得DR，减免对应伤害类型的伤害
# 设计: 轻甲低DR全类型均衡，中甲高DR刺/砍，重甲高DR全类型
# ============================================================================

## 砍伤减免（剑/斧 vs 链甲/板甲）
@export var dr_slash: int = 0
## 刺伤减免（箭/矛 vs 镶甲/板甲）
@export var dr_pierce: int = 0
## 钝伤减免（锤/杖 vs 板甲）
@export var dr_crush: int = 0

# ============================================================================
# 装甲耐久
# ============================================================================

## 装甲最大耐久度（额外HP）
@export var max_dr: int = 10

## 穿透阈值（d20对抗的值，决定穿透概率）
## 布甲=3, 皮甲=6, 链甲=11, 板甲=15
@export var dr_threshold: int = 0

# 扩展防具属性（对应策划案 06-装备与物品）
@export var str_required: int = 0          # 最低力量需求
@export var stealth_disadvantage: bool = false # 隐匿检定不利（重甲）
@export var is_destroyable: bool = false   # 可被破坏（木盾可被斧类击碎）
@export var base_ac_override: int = -1     # 固定AC基础值（-1=使用默认10+DEX计算）

# ============================================================================
# 词缀加成（运行时累加）
# ============================================================================

## 词缀带来的额外AC加成
var bonus_ac: int = 0

## 词缀带来的抗性
var bonus_resistance: String = ""

## 词缀带来的免疫
var bonus_immunity: String = ""

## 词缀带来的属性加成
var bonus_str: int = 0
var bonus_dex: int = 0
var bonus_con: int = 0
var bonus_int: int = 0
var bonus_wis: int = 0
var bonus_cha: int = 0

## 词缀带来的HP加成
var bonus_hp: int = 0

## 词缀带来的移动加成
var bonus_move: int = 0

## 词缀带来的特殊效果
var bonus_special_effects: Array[Dictionary] = []


# ============================================================================
# 词缀应用
# ============================================================================

func _apply_affix(affix: EquipmentAffix) -> void:
	bonus_ac += affix.ac_bonus
	bonus_str += affix.str_bonus
	bonus_dex += affix.dex_bonus
	bonus_con += affix.con_bonus
	bonus_int += affix.int_bonus
	bonus_wis += affix.wis_bonus
	bonus_cha += affix.cha_bonus
	bonus_hp += affix.hp_bonus
	bonus_move += affix.move_bonus

	if affix.resistance != "" and bonus_resistance == "":
		bonus_resistance = affix.resistance
	if affix.immunity != "" and bonus_immunity == "":
		bonus_immunity = affix.immunity

	if affix.special_effect != "":
		bonus_special_effects.append({
			"effect": affix.special_effect,
			"value": affix.special_value,
		})


# ============================================================================
# 获取方法（含词缀加成）
# ============================================================================

## 获取总AC加成（基础+词缀）
func get_total_ac_bonus() -> int:
	return ac_bonus + bonus_ac

## 获取总AC（含base_ac_override + 词缀）
func get_total_base_ac() -> int:
	var base = base_ac_override if base_ac_override >= 0 else (10 + ac_bonus)
	return base + bonus_ac

## 获取属性加成文本
func get_stat_bonus_text() -> String:
	var parts: Array[String] = []
	if bonus_str != 0: parts.append("力量%+d" % bonus_str)
	if bonus_dex != 0: parts.append("敏捷%+d" % bonus_dex)
	if bonus_con != 0: parts.append("体质%+d" % bonus_con)
	if bonus_int != 0: parts.append("智力%+d" % bonus_int)
	if bonus_wis != 0: parts.append("感知%+d" % bonus_wis)
	if bonus_cha != 0: parts.append("魅力%+d" % bonus_cha)
	if bonus_hp != 0: parts.append("HP%+d" % bonus_hp)
	if bonus_move != 0: parts.append("移动%+d" % bonus_move)
	if bonus_resistance != "": parts.append("%s抗性" % bonus_resistance)
	return "，".join(parts)

## 获取防具类型显示名
func get_armor_type_name() -> String:
	match armor_type:
		ArmorType.LIGHT: return "轻甲"
		ArmorType.MEDIUM: return "中甲"
		ArmorType.HEAVY: return "重甲"
		ArmorType.SHIELD: return "盾牌"
		_: return "防具"

## 获取完整防具描述
func get_armor_description() -> String:
	var parts: Array[String] = []
	if armor_type == ArmorType.SHIELD:
		parts.append("AC%+d" % get_total_ac_bonus())
	else:
		if base_ac_override >= 0:
			parts.append("AC %d" % (base_ac_override + bonus_ac))
		else:
			parts.append("AC %d+DEX" % (10 + ac_bonus + bonus_ac))
		if max_dex_bonus < 99:
			parts.append("DEX上限%d" % max_dex_bonus)
		if str_required > 0:
			parts.append("需要STR %d" % str_required)
		if stealth_disadvantage:
			parts.append("隐匿不利")
		if movement_penalty != 0:
			parts.append("速度%+d" % -movement_penalty)
	var stat_text = get_stat_bonus_text()
	if stat_text != "":
		parts.append(stat_text)
	return " ".join(parts)
