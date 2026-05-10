# StatusEffectData.gd
# 状态效果数据 — 所有战斗中的正面/负面状态效果定义
# 对应策划案 03-战术战斗系统 → 七、战斗状态效果
extends Resource
class_name StatusEffectData

# ============================================================================
# 状态效果枚举（13个负面 + 7个正面）
# ============================================================================

enum EffectId {
	# 负面状态
	POISON,       # 中毒 — 每回合1d4伤害，3回合
	BURNING,      # 燃烧 — 每回合1d6伤害，可蔓延，3回合
	FREEZE,       # 冰冻 — 本回合不可行动，AC-2，1回合
	FEAR,         # 恐惧 — 必须远离源，不可攻击，2回合
	SILENCE,      # 沉默 — 不能施法，2回合
	BLIND,        # 致盲 — 近战劣势，远程必须相邻，2回合
	STUN,         # 眩晕 — 本回合只能移动或攻击（二选一），1回合
	BLEED,        # 流血 — 每回合1d4伤害，可叠加，至止血
	SLOW,         # 减速 — 移动速度-2，2回合
	ROOT,         # 缚足 — 不能移动，可攻击，2回合
	CHARMED,      # 魅惑 — 不能攻击施法者，1回合
	CONFUSED,     # 困惑 — 随机行动，1回合
	WET,          # 潮湿 — 中性状态，可被其他效果利用
	
	# 正面状态
	BLESS,        # 祝福 — 攻击/豁免+1d4，3回合
	SHIELD,       # 护盾 — AC+5，1回合
	HASTE,        # 加速 — 移动+2，额外次要行动，3回合
	REGEN,        # 再生 — 每回合恢复1d6 HP，3回合
	INVISIBILITY, # 隐身 — 不可被瞄准，攻击后解除
	PHANTOM,      # 幻影 — 攻击者需先命中幻影(AC12)
	TEMP_HP,      # 临时HP — 额外HP层，先于本体消耗
}

# ============================================================================
# 数据字段
# ============================================================================

## 效果ID
@export var effect_id: EffectId = EffectId.POISON

## 效果名称
@export var effect_name: String = ""

## 效果描述
@export_multiline var description: String = ""

## 是否负面
@export var is_negative: bool = true

## 默认持续回合数
@export var default_duration: int = 3

## 每回合伤害骰子（0=无持续伤害）
@export var tick_damage_dice_count: int = 0
@export var tick_damage_dice_sides: int = 0
@export var tick_damage_type: String = ""  # "poison", "fire", "bleed"

## 属性修正（AC、速度、攻击等）
@export var stat_modifiers: Dictionary = {}
# 示例: {"ac": -2, "speed": -2, "attack": -1, "melee_disadvantage": true}

## 可通过豁免解除（豁免类型）
@export var save_to_remove: String = ""  # "fortitude"/"reflex"/"will" 或空

## 解除豁免DC
@export var save_dc: int = 12

## 可解除的其他效果（如燃烧解除冰冻）
@export var removes_effects: Array[String] = []

## 互斥标签（同标签的效果互斥或交互）
@export var cancel_tag: String = ""
# "fire" — 火系（燃烧、火油）
# "ice" — 冰系（冰冻、潮湿+冰）
# "wet" — 潮湿系

## 是否攻击后解除（隐身）
@export var breaks_on_attack: bool = false

## 是否可蔓延到相邻格（燃烧）
@export var can_spread: bool = false

# ============================================================================
# 静态工厂：预定义所有效果
# ============================================================================

static func create_effect(id: EffectId) -> StatusEffectData:
	var e = StatusEffectData.new()
	e.effect_id = id
	match id:
		# ====== 负面状态 ======
		EffectId.POISON:
			e.effect_name = "中毒"
			e.description = "每回合开始受到1d4伤害"
			e.is_negative = true
			e.default_duration = 3
			e.tick_damage_dice_count = 1; e.tick_damage_dice_sides = 4
			e.tick_damage_type = "poison"
			e.save_to_remove = "fortitude"; e.save_dc = 12
		EffectId.BURNING:
			e.effect_name = "燃烧"
			e.description = "每回合开始受到1d6伤害，可蔓延至相邻"
			e.is_negative = true
			e.default_duration = 3
			e.tick_damage_dice_count = 1; e.tick_damage_dice_sides = 6
			e.tick_damage_type = "fire"
			e.cancel_tag = "fire"
			e.removes_effects = ["freeze"]
			e.can_spread = true
		EffectId.FREEZE:
			e.effect_name = "冰冻"
			e.description = "本回合不可行动，AC-2"
			e.is_negative = true
			e.default_duration = 1
			e.stat_modifiers = {"ac": -2, "cannot_act": true}
			e.cancel_tag = "ice"
			e.removes_effects = ["burning"]
		EffectId.FEAR:
			e.effect_name = "恐惧"
			e.description = "必须向远离源的方向移动，不可主动攻击"
			e.is_negative = true
			e.default_duration = 2
			e.save_to_remove = "will"; e.save_dc = 15
		EffectId.SILENCE:
			e.effect_name = "沉默"
			e.description = "不能施放法术"
			e.is_negative = true
			e.default_duration = 2
			e.stat_modifiers = {"cannot_cast": true}
		EffectId.BLIND:
			e.effect_name = "致盲"
			e.description = "近战攻击劣势，远程攻击必须相邻"
			e.is_negative = true
			e.default_duration = 2
			e.stat_modifiers = {"melee_disadvantage": true, "ranged_range_override": 1}
		EffectId.STUN:
			e.effect_name = "眩晕"
			e.description = "本回合只能移动或攻击（二选一）"
			e.is_negative = true
			e.default_duration = 1
			e.stat_modifiers = {"action_restricted": true}
		EffectId.BLEED:
			e.effect_name = "流血"
			e.description = "每回合开始受到1d4伤害，可叠加"
			e.is_negative = true
			e.default_duration = 99  # 至止血
			e.tick_damage_dice_count = 1; e.tick_damage_dice_sides = 4
			e.tick_damage_type = "bleed"
		EffectId.SLOW:
			e.effect_name = "减速"
			e.description = "移动速度-2（最小1）"
			e.is_negative = true
			e.default_duration = 2
			e.stat_modifiers = {"speed": -2}
		EffectId.ROOT:
			e.effect_name = "缚足"
			e.description = "不能移动，可攻击"
			e.is_negative = true
			e.default_duration = 2
			e.stat_modifiers = {"cannot_move": true}
			e.save_to_remove = "fortitude"; e.save_dc = 15
		EffectId.CHARMED:
			e.effect_name = "魅惑"
			e.description = "不能攻击施法者"
			e.is_negative = true
			e.default_duration = 1
		EffectId.CONFUSED:
			e.effect_name = "困惑"
			e.description = "随机行动"
			e.is_negative = true
			e.default_duration = 1
		EffectId.WET:
			e.effect_name = "潮湿"
			e.description = "中性状态，火焰抗性+2，冰霜/雷电弱点"
			e.is_negative = false
			e.default_duration = 3
			e.cancel_tag = "wet"
			e.removes_effects = ["burning"]
			e.stat_modifiers = {"fire_resist": 2, "ice_weakness": true, "lightning_weakness": true}
		
		# ====== 正面状态 ======
		EffectId.BLESS:
			e.effect_name = "祝福"
			e.description = "攻击/豁免+1d4"
			e.is_negative = false
			e.default_duration = 3
			e.stat_modifiers = {"attack_bonus_dice": 4, "save_bonus_dice": 4}
		EffectId.SHIELD:
			e.effect_name = "护盾"
			e.description = "AC+5"
			e.is_negative = false
			e.default_duration = 1
			e.stat_modifiers = {"ac": 5}
		EffectId.HASTE:
			e.effect_name = "加速"
			e.description = "移动+2，额外1次次要行动"
			e.is_negative = false
			e.default_duration = 3
			e.stat_modifiers = {"speed": 2, "extra_minor_action": true}
		EffectId.REGEN:
			e.effect_name = "再生"
			e.description = "每回合开始恢复1d6 HP"
			e.is_negative = false
			e.default_duration = 3
			e.tick_damage_dice_count = 1; e.tick_damage_dice_sides = -6  # 负值=治疗
		EffectId.INVISIBILITY:
			e.effect_name = "隐身"
			e.description = "不可被直接瞄准，AOE有效，攻击后解除"
			e.is_negative = false
			e.default_duration = 99
			e.breaks_on_attack = true
		EffectId.PHANTOM:
			e.effect_name = "幻影"
			e.description = "攻击者需先命中幻影(AC12)，命中则消耗幻影"
			e.is_negative = false
			e.default_duration = 99
			e.stat_modifiers = {"phantom_ac": 12}
		EffectId.TEMP_HP:
			e.effect_name = "临时HP"
			e.description = "额外HP层，先于本体HP消耗"
			e.is_negative = false
			e.default_duration = 99
	return e

# ============================================================================
# 状态交互规则
# 对应策划案 03-战术战斗系统 → 状态交互
# ============================================================================

## 检查两个效果的交互结果
## 返回: {"action": String, "value": Variant}
## action: "cancel_both" | "cancel_a" | "cancel_b" | "extend_b" | "boost_damage" | "spread" | "none"
static func get_interaction(effect_a: String, effect_b: String) -> Dictionary:
	# 燃烧 + 冰冻 → 互相解除
	if (effect_a == "burning" and effect_b == "freeze") or \
	   (effect_a == "freeze" and effect_b == "burning"):
		return {"action": "cancel_both"}
	
	# 燃烧 + 油类 → 燃烧伤害翻倍
	if effect_a == "burning" and effect_b == "wet":
		return {"action": "none"}
	if effect_b == "burning" and effect_a == "wet":
		return {"action": "none"}
	
	# 潮湿 + 冰系 → 冰冻持续时间+1
	if effect_a == "wet" and effect_b == "freeze":
		return {"action": "extend_b", "value": 1}
	if effect_b == "wet" and effect_a == "freeze":
		return {"action": "extend_a", "value": 1}
	
	# 潮湿 + 雷系 → 雷电伤害+50%
	if effect_a == "wet" and effect_b == "lightning_damage":
		return {"action": "boost_damage", "value": 1.5}
	if effect_b == "wet" and effect_a == "lightning_damage":
		return {"action": "boost_damage", "value": 1.5}
	
	# 中毒 + 燃烧 → 毒雾扩散
	if effect_a == "poison" and effect_b == "burning":
		return {"action": "spread", "value": "poison_cloud"}
	if effect_b == "poison" and effect_a == "burning":
		return {"action": "spread", "value": "poison_cloud"}
	
	# 隐身 + 攻击 → 解除隐身（由breaks_on_attack处理）
	
	# 沉默 + 法术 → 法术完全不可用（由cannot_cast处理）
	
	# 恐惧 + 士气崩溃 → 优先溃逃
	if effect_a == "fear" and effect_b == "morale_rout":
		return {"action": "cancel_a"}  # 溃逃覆盖恐惧
	if effect_b == "fear" and effect_a == "morale_rout":
		return {"action": "cancel_b"}
	
	return {"action": "none"}


## 获取效果显示名
static func get_effect_name(id: EffectId) -> String:
	var e = create_effect(id)
	return e.effect_name
