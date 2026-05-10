# SpellData.gd
# 法术数据 — 独立于SkillData的完整法术定义
# 对应策划案 07-法术系统.md
extends Resource
class_name SpellData

# ============================================================================
# 法术体系枚举
# ============================================================================

## 八大法术学派
enum SpellSchool {
	EVOCATION,    # 塑能 — 直接伤害
	ABJURATION,   # 防护 — 防御/减伤
	ILLUSION,     # 幻术 — 欺骗/干扰
	NECROMANCY,   # 死灵 — 生命操控
	TRANSMUTATION,# 变化 — 形态改变
	ENCHANTMENT,  # 附魔 — 心灵操控
	DIVINATION,   # 预言 — 信息/辅助
	CONJURATION,  # 咒唤 — 召唤/创造
}

## 法术环阶（0环=戏法，1-7环）
enum SpellTier {
	CANTRIP,   # 0环 — 戏法
	TIER_1,    # 1环
	TIER_2,    # 2环
	TIER_3,    # 3环
	TIER_4,    # 4环
	TIER_5,    # 5环
	TIER_6,    # 6环
	TIER_7,    # 7环
}

## 法术范围形状
enum SpellShape {
	SINGLE,  # 单体 — 1个目标格子
	RAY,     # 射线 — 从施法者出发的直线
	CONE,    # 锥形 — 施法者面前120°扇形
	SPHERE,  # 球形 — 以目标格为中心
	LINE,    # 线形 — 两点之间的直线
	CROSS,   # 十字 — 以目标格为中心的十字
	SELF,    # 自身 — 以施法者为中心
	TOUCH,   # 触碰 — 近战范围内1个目标
}

## 豁免类型
enum SaveType {
	NONE,      # 无豁免
	STR_SAVE,  # 力量豁免
	DEX_SAVE,  # 敏捷豁免（含半伤）
	CON_SAVE,  # 体质豁免（含半伤）
	INT_SAVE,  # 智力豁免
	WIS_SAVE,  # 感知豁免
	CHA_SAVE,  # 魅力豁免
}

## 解析方式（法术如何命中目标）
enum ResolutionType {
	ATTACK_ROLL,  # 法术攻击检定 vs AC
	SAVE,         # 目标豁免 vs 法术DC
	AUTO_HIT,    # 自动命中，无检定
}

## 施放时机
enum CastingTime {
	MAIN_ACTION,   # 主行动
	MINOR_ACTION,  # 次要行动
	REACTION,      # 反应
}

# ============================================================================
# 基础标识
# ============================================================================

## 法术唯一ID
@export var spell_id: String = ""

## 法术名称
@export var spell_name: String = "未命名法术"

## 法术描述
@export_multiline var description: String = ""

## 所属学派
@export var spell_school: SpellSchool = SpellSchool.EVOCATION

## 环阶
@export var tier: SpellTier = SpellTier.CANTRIP

# ============================================================================
# 施放参数
# ============================================================================

## 魔力消耗（对应策划案：戏法0, 1环3, 2环5, 3环8, 4环12, 5环18）
@export var mana_cost: int = 0

## 冷却回合数（对应策划案：戏法0, 1环1, 2环2, 3环3, 4环4, 5环5）
@export var cooldown_turns: int = 0

## 施放时机
@export var casting_time: CastingTime = CastingTime.MAIN_ACTION

# ============================================================================
# 范围与形状
# ============================================================================

## 法术射程（格子数，0=自身/触碰）
@export var range_cells: int = 6

## 范围形状
@export var shape: SpellShape = SpellShape.SINGLE

## 范围大小（球形半径/锥形长度/线形长度，格数）
@export var shape_size: int = 1

# ============================================================================
# 伤害与效果
# ============================================================================

## 解析方式
@export var resolution_type: ResolutionType = ResolutionType.SAVE

## 豁免类型（当resolution_type == SAVE时有效）
@export var save_type: SaveType = SaveType.NONE

## 伤害骰子数量
@export var damage_dice_count: int = 0

## 伤害骰子面数
@export var damage_dice_sides: int = 0

## 伤害类型（fire/cold/lightning/force/necrotic/radiant/physical）
@export var damage_type: String = "force"

## 治疗量骰子数量（0=非治疗法术）
@export var heal_dice_count: int = 0

## 治疗量骰子面数
@export var heal_dice_sides: int = 0

## 治疗量固定加值（WIS修正等在运行时计算）
@export var heal_bonus: int = 0

## 附加的状态效果ID（如 "burning", "freeze", "poison" 等）
@export var applied_status_effect: String = ""

## 状态效果持续回合数
@export var status_duration: int = 0

## 特殊效果标识（非标准伤害/治疗的效果，如召唤、变形等）
@export var special_effect: String = ""

## 召唤物的HP（如果有）
@export var summon_hp: int = 0

## 召唤物持续回合数
@export var summon_duration: int = 0

# ============================================================================
# 集中与持续
# ============================================================================

## 是否需要集中维持
@export var is_concentration: bool = false

## 持续回合数（非集中法术的固定持续）
@export var duration_turns: int = 0

# ============================================================================
# UI
# ============================================================================

@export var icon: Texture2D

# ============================================================================
# 辅助方法
# ============================================================================

## 获取环阶显示名
func get_tier_name() -> String:
	match tier:
		SpellTier.CANTRIP: return "戏法"
		SpellTier.TIER_1: return "1环"
		SpellTier.TIER_2: return "2环"
		SpellTier.TIER_3: return "3环"
		SpellTier.TIER_4: return "4环"
		SpellTier.TIER_5: return "5环"
		SpellTier.TIER_6: return "6环"
		SpellTier.TIER_7: return "7环"
		_: return "未知"

## 获取学派显示名
func get_school_name() -> String:
	match spell_school:
		SpellSchool.EVOCATION: return "塑能"
		SpellSchool.ABJURATION: return "防护"
		SpellSchool.ILLUSION: return "幻术"
		SpellSchool.NECROMANCY: return "死灵"
		SpellSchool.TRANSMUTATION: return "变化"
		SpellSchool.ENCHANTMENT: return "附魔"
		SpellSchool.DIVINATION: return "预言"
		SpellSchool.CONJURATION: return "咒唤"
		_: return "未知"

## 获取形状显示名
func get_shape_name() -> String:
	match shape:
		SpellShape.SINGLE: return "单体"
		SpellShape.RAY: return "射线"
		SpellShape.CONE: return "锥形"
		SpellShape.SPHERE: return "球形"
		SpellShape.LINE: return "线形"
		SpellShape.CROSS: return "十字"
		SpellShape.SELF: return "自身"
		SpellShape.TOUCH: return "触碰"
		_: return "未知"

## 获取默认冷却（按环阶，策划案规定）
static func get_default_cooldown(spell_tier: SpellTier) -> int:
	return int(spell_tier)  # 环阶值刚好 = 冷却回合数（戏法0, 1环1, 2环2...）

## 获取默认魔力消耗（按环阶）
static func get_default_mana_cost(spell_tier: SpellTier) -> int:
	match spell_tier:
		SpellTier.CANTRIP: return 0
		SpellTier.TIER_1: return 3
		SpellTier.TIER_2: return 5
		SpellTier.TIER_3: return 8
		SpellTier.TIER_4: return 12
		SpellTier.TIER_5: return 18
		SpellTier.TIER_6: return 25
		SpellTier.TIER_7: return 35
		_: return 0
