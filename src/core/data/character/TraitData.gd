# TraitData.gd
# 特质数据资源 — 角色生成时随机获得的特质
# 对应策划案 05-角色与职业.md → 随机特质
extends Resource
class_name TraitData

# ============================================================================
# 枚举
# ============================================================================

enum TraitType {
	ATTRIBUTE,   # 属性类特质（增减基础属性值）
	FUNCTIONAL,  # 功能性特质（提供独特效果）
}

# ============================================================================
# 数据字段
# ============================================================================

## 特质ID
@export var trait_id: String = ""

## 特质名称
@export var trait_name: String = ""

## 特质描述
@export_multiline var description: String = ""

## 特质类型
@export var trait_type: TraitType = TraitType.ATTRIBUTE

## 属性修正
@export var str_mod: int = 0
@export var dex_mod: int = 0
@export var con_mod: int = 0
@export var int_mod: int = 0
@export var wis_mod: int = 0
@export var cha_mod: int = 0

## 非属性效果（功能性特质）
## 可能的值: "dark_vision", "iron_stomach", "adaptability", "thick_skin",
##           "indomitable", "ether_resonance", "premonition", "old_wound",
##           "gluttony", "timid", "xenophobia",
##           "long_arm", "eagle_eye", "spell_memory", "commander", "affinity",
##           "sorcerer_blood"
@export var functional_effect: String = ""

## 效果数值（用于缩放效果）
@export var effect_value: float = 0.0

## 随机抽取权重
@export var weight: float = 1.0

## AI加点方向微调（对某属性方向的权重加成）
@export var ai_direction_bonus: Dictionary = {}  # e.g. {"str": 0.2}

# ============================================================================
# 静态工厂：返回所有预定义特质
# ============================================================================

static func get_all_traits() -> Array[TraitData]:
	var traits: Array[TraitData] = []

	# ====== 属性类特质（19个）======
	traits.append(_make("brute_force", "蛮力", "天生力气大，适合近战", 2, 0, 0, 0, 0, 0, 1.0, {"str": 0.2}))
	traits.append(_make("long_arms", "长臂", "近战射程+1（可攻击2格）", 0, 0, 0, 0, 0, 0, 0.5, {"str": 0.2}, "long_arm"))
	traits.append(_make("quick_hands", "快手", "灵活，适合远程/闪避", 0, 2, 0, 0, 0, 0, 1.0, {"dex": 0.2}))
	traits.append(_make("eagle_eye", "猫眼", "远程命中+1", 0, 0, 0, 0, 0, 0, 0.5, {"dex": 0.2}, "eagle_eye"))
	traits.append(_make("thick_bones", "硬骨头", "抗打，HP多", 0, 0, 2, 0, 0, 0, 1.0, {"con": 0.1}))
	traits.append(_make("sturdy", "壮硕", "耐打但笨重，速度-1", 0, 0, 1, 0, 0, 0, 0.8, {"con": 0.1}, "speed", -1.0))
	traits.append(_make("great_mind", "过人慧心", "聪明，适合法术", 0, 0, 0, 2, 0, 0, 1.0, {"int": 0.2}))
	traits.append(_make("spell_memory", "博闻强记", "法术位+1（每阶）", 0, 0, 0, 0, 0, 0, 0.3, {"int": 0.3}, "spell_memory"))
	traits.append(_make("keen_intuition", "敏锐直觉", "洞察力强，适合治疗/侦察", 0, 0, 0, 0, 2, 0, 1.0, {"wis": 0.2}))
	traits.append(_make("alertness", "警觉", "先攻+3，被动侦察范围+1", 0, 0, 0, 0, 0, 0, 0.5, {"wis": 0.1}, "alertness"))
	traits.append(_make("born_leader", "天生统帅", "领袖气质，适合指挥", 0, 0, 0, 0, 0, 2, 1.0, {"cha": 0.2}))
	traits.append(_make("affinity", "亲和力", "商店价格-15%，招募价格-10%", 0, 0, 0, 0, 0, 0, 0.5, {"cha": 0.1}, "affinity"))
	traits.append(_make("reckless_brave", "蛮勇", "能打但鲁莽", 1, 0, 1, 0, -2, 0, 0.7, {"str": 0.1}))
	traits.append(_make("sorcerer_blood", "术士血脉", "天生施法者", 0, 0, 0, 0, 0, 2, 0.3, {"int": 0.3}, "sorcerer_blood"))
	traits.append(_make("frail_build", "瘦弱", "力量不足但灵活", -1, 1, 0, 0, 0, 0, 0.8))
	traits.append(_make("sluggish", "迟缓", "慢但结实", 0, -2, 1, 0, 0, 0, 0.8))
	traits.append(_make("fragile", "脆弱", "容易受伤", 0, 0, -2, 0, 0, 0, 0.8))
	traits.append(_make("dull", "愚钝", "不适合法术", 0, 0, 0, -2, 0, 0, 0.8))
	traits.append(_make("clumsy", "笨拙", "不适合远程", 0, -1, 0, 0, 0, 0, 0.8, {}, "ranged_hit_minus_1"))
	traits.append(_make("bad_talker", "笨嘴拙舌", "社交困难", 0, 0, 0, 0, 0, -2, 0.8))

	# ====== 功能性特质（11个，较少见）======
	traits.append(_make_func("night_vision", "夜视", "获得黑暗视觉，洞穴/夜间无惩罚", "dark_vision", 0.5, {"wis": 0.1}))
	traits.append(_make_func("iron_stomach", "铁胃", "免疫食物中毒，长途行军安全", "iron_stomach", 0.5))
	traits.append(_make_func("adaptability", "适应力", "疲劳惩罚减半，长途行军优势", "adaptability", 0.4))
	traits.append(_make_func("thick_skin_trait", "厚皮", "受到物理伤害-1，全局减伤", "thick_skin", 0.3))
	traits.append(_make_func("indomitable", "不屈", "HP归零时50%概率保持1HP（每战1次）", "indomitable", 0.2))
	traits.append(_make_func("ether_resonance", "以太共鸣", "施法时恢复1d4 HP", "ether_resonance", 0.3, {"int": 0.1}))
	traits.append(_make_func("premonition", "预感", "被伏击时自动获得一轮准备", "premonition", 0.3))
	traits.append(_make_func("old_injury", "旧伤", "战斗开始时HP-10%", "old_wound", 0.6, {"con": 0.1}))
	traits.append(_make_func("gluttony", "贪吃", "补给消耗×1.5", "gluttony", 0.5))
	traits.append(_make_func("timid", "胆小", "HP<50%时攻击-1", "timid", 0.5))
	traits.append(_make_func("xenophobia", "仇外", "与外族队友在一起时忠诚度-10", "xenophobia", 0.4))

	return traits

## 获取所有属性类特质
static func get_attribute_traits() -> Array[TraitData]:
	var all = get_all_traits()
	var result: Array[TraitData] = []
	for t in all:
		if t.trait_type == TraitType.ATTRIBUTE:
			result.append(t)
	return result

## 获取所有功能性特质
static func get_functional_traits() -> Array[TraitData]:
	var all = get_all_traits()
	var result: Array[TraitData] = []
	for t in all:
		if t.trait_type == TraitType.FUNCTIONAL:
			result.append(t)
	return result

# ============================================================================
# 内部工厂方法
# ============================================================================

static func _make(id: String, name: String, desc: String, _str: int, _dex: int, _con: int, _int: int, _wis: int, _cha: int, w: float, ai_dir: Dictionary = {}, func_eff: String = "", eff_val: float = 0.0) -> TraitData:
	var t = TraitData.new()
	t.trait_id = id
	t.trait_name = name
	t.description = desc
	t.trait_type = TraitType.ATTRIBUTE
	t.str_mod = _str
	t.dex_mod = _dex
	t.con_mod = _con
	t.int_mod = _int
	t.wis_mod = _wis
	t.cha_mod = _cha
	t.functional_effect = func_eff
	t.effect_value = eff_val
	t.weight = w
	t.ai_direction_bonus = ai_dir
	return t

static func _make_func(id: String, name: String, desc: String, effect: String, w: float, ai_dir: Dictionary = {}) -> TraitData:
	var t = TraitData.new()
	t.trait_id = id
	t.trait_name = name
	t.description = desc
	t.trait_type = TraitType.FUNCTIONAL
	t.functional_effect = effect
	t.weight = w
	t.ai_direction_bonus = ai_dir
	return t
