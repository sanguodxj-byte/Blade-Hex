# SkillRegistry.gd
# 技能注册表 — 纯数据类，存储所有技能效果配置
# 从 SkillEffectExecutor 中提取，作为技能数据的单一真相源
class_name SkillRegistry


# ============================================================================
# 技能分类与目标类型（与 SkillEffectExecutor 共享）
# ============================================================================

enum SkillCategory {
	MELEE_ACTIVE,    # 近战主动
	RANGED_ACTIVE,   # 远程主动
	MAGIC_ACTIVE,    # 法术主动
	HEAL_ACTIVE,     # 治疗主动
	SUPPORT_ACTIVE,  # 辅助主动
	PASSIVE,         # 被动（常驻修正）
	KEYSTONE,        # 代价型被动
	OUT_OF_COMBAT,   # 非战斗效果（商店折扣等，仅标记）
}

enum TargetType {
	SELF,            # 自身
	SINGLE_ENEMY,    # 单个敌人
	SINGLE_ALLY,     # 单个友军（含自身）
	ALL_ADJACENT,    # 周围所有（六邻格）
	AOE_SMALL,       # 小范围 AoE（半径1）
	AOE_CONE,        # 锥形范围
	RANGED_SINGLE,   # 远程单体
	RANGED_AOE,      # 远程 AoE
	ALL_ALLIES,      # 所有友军
}


# ============================================================================
# 技能注册表数据 — 单一真相源
# ============================================================================

static var _SKILL_REGISTRY: Dictionary = {
	# ================================================================
	# STR 力量区域 — 主动技能
	# ================================================================
	"double_attack": {
		"category": SkillCategory.MELEE_ACTIVE,
		"target": TargetType.SINGLE_ENEMY,
		"name": "连击",
		"description": "攻击2次，第二次-3命中",
		"vfx": "melee_combo",
		"action_cost": "major",
	},
	"whirlwind": {
		"category": SkillCategory.MELEE_ACTIVE,
		"target": TargetType.ALL_ADJACENT,
		"name": "旋风斩",
		"description": "攻击周围所有敌人",
		"vfx": "whirlwind",
		"action_cost": "major",
	},
	"battle_cry": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.ALL_ADJACENT,
		"name": "战斗怒吼",
		"description": "震慑周围敌人下回合攻击-2，友军士气+3",
		"vfx": "war_cry",
		"action_cost": "major",
	},
	"blood_vortex": {
		"category": SkillCategory.MELEE_ACTIVE,
		"target": TargetType.ALL_ADJACENT,
		"name": "血腥漩涡",
		"description": "横扫周围所有敌人，每命中1个恢复1d6 HP",
		"vfx": "blood_vortex",
		"action_cost": "major",
	},
	# ================================================================
	# STR 力量区域 — 被动技能
	# ================================================================
	"melee_hit_plus_1": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "基础剑术",
		"description": "近战命中+1",
	},
	"weapon_mastery": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "剑术精通",
		"description": "近战伤害+STR修正x1.5",
	},
	"critical_x3": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "重击",
		"description": "暴击伤害x3",
	},
	"iron_will": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "坚韧意志",
		"description": "致命伤害时强韧豁免DC15存活于1HP",
	},
	# ================================================================
	# STR — Keystone
	# ================================================================
	"berserk_power": {
		"category": SkillCategory.KEYSTONE,
		"target": TargetType.SELF,
		"name": "狂暴之力",
		"description": "近战伤害+50%",
		"cost": "AC-3，不能使用盾牌",
	},
	# ================================================================
	# DEX 灵巧区域 — 主动技能
	# ================================================================
	"aimed_shot": {
		"category": SkillCategory.RANGED_ACTIVE,
		"target": TargetType.RANGED_SINGLE,
		"name": "精准射击",
		"description": "瞄准后射击优势+伤害x2",
		"vfx": "aimed_shot",
		"action_cost": "major",
	},
	"double_shot": {
		"category": SkillCategory.RANGED_ACTIVE,
		"target": TargetType.RANGED_SINGLE,
		"name": "双重射击",
		"description": "射击2个目标各-2命中",
		"vfx": "double_shot",
		"action_cost": "major",
	},
	"scatter_shot": {
		"category": SkillCategory.RANGED_ACTIVE,
		"target": TargetType.AOE_CONE,
		"name": "散射",
		"description": "锥形范围射击",
		"vfx": "scatter_shot",
		"action_cost": "major",
	},
	"stealth": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SELF,
		"name": "隐匿",
		"description": "进入潜行状态",
		"vfx": "stealth",
		"action_cost": "major",
	},
	"shadow_clone": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SELF,
		"name": "影分身",
		"description": "位移+残影，下次攻击自动闪避1次",
		"vfx": "shadow_clone",
		"action_cost": "major",
	},
	"trick_arrow": {
		"category": SkillCategory.RANGED_ACTIVE,
		"target": TargetType.RANGED_SINGLE,
		"name": "元素箭",
		"description": "1d10+随机debuff(失明/倒地/震慑)",
		"vfx": "trick_arrow",
		"action_cost": "major",
	},
	"poison_blade": {
		"category": SkillCategory.MELEE_ACTIVE,
		"target": TargetType.SINGLE_ENEMY,
		"name": "毒刃",
		"description": "攻击附带中毒(每回合1d4，3回合)",
		"vfx": "poison_blade",
		"action_cost": "major",
	},
	# ================================================================
	# DEX — 被动技能
	# ================================================================
	"ranged_hit_plus_1": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "基础射击",
		"description": "远程命中+1",
	},
	"piercing_shot": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "穿透射击",
		"description": "箭矢穿透击中后方1个敌人",
	},
	"dodge_master": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "闪避大师",
		"description": "AC+DEX修正(可与轻甲叠加)",
	},
	"sniper": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "狙击",
		"description": "射程+2，高处+1伤害",
	},
	"sneak_attack": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "偷袭",
		"description": "有优势时攻击额外+2d6伤害",
	},
	"deadly_blow": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "致命一击",
		"description": "偷袭伤害+3d6",
	},
	# ================================================================
	# DEX — Keystone
	# ================================================================
	"ghost_step": {
		"category": SkillCategory.KEYSTONE,
		"target": TargetType.SELF,
		"name": "幽灵步伐",
		"description": "永久获得掩护(远程攻击-2命中)",
		"cost": "HP上限-20%",
	},
	# ================================================================
	# CON 体魄区域 — 主动技能
	# ================================================================
	"shield_bash": {
		"category": SkillCategory.MELEE_ACTIVE,
		"target": TargetType.SINGLE_ENEMY,
		"name": "盾击",
		"description": "攻击+推开目标1格",
		"vfx": "shield_bash",
		"action_cost": "major",
	},
	"taunt": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.ALL_ADJACENT,
		"name": "嘲讽",
		"description": "强制周围敌人攻击自己",
		"vfx": "taunt",
		"action_cost": "major",
	},
	"unyielding_bulwark": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SELF,
		"name": "不屈壁垒",
		"description": "受伤减半+临时HP=WIS修正x5",
		"vfx": "bulwark",
		"action_cost": "major",
	},
	"field_medic": {
		"category": SkillCategory.HEAL_ACTIVE,
		"target": TargetType.SINGLE_ALLY,
		"name": "战地医疗",
		"description": "恢复友军2d8+CON修正HP，解除流血/中毒",
		"vfx": "heal",
		"action_cost": "major",
	},
	# ================================================================
	# CON — 被动技能
	# ================================================================
	"hold_ground": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "坚守",
		"description": "不移动时AC+2",
	},
	"iron_wall": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "铁壁",
		"description": "受到物理伤害-3",
	},
	"counter_attack": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "反击",
		"description": "被近战命中后自动反击1次",
	},
	"last_stand": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "最后阵地",
		"description": "HP低于25%时AC+5和伤害+50%",
	},
	# ================================================================
	# CON — Keystone
	# ================================================================
	"diamond_body": {
		"category": SkillCategory.KEYSTONE,
		"target": TargetType.SELF,
		"name": "金刚不坏",
		"description": "所有伤害减免+3",
		"cost": "速度-2",
	},
	# ================================================================
	# INT 智力区域 — 主动技能
	# ================================================================
	"mana_shield": {
		"category": SkillCategory.MAGIC_ACTIVE,
		"target": TargetType.SELF,
		"name": "魔力护盾",
		"description": "消耗5魔力获得护盾，吸收魔力x10伤害",
		"vfx": "mana_shield",
		"action_cost": "major",
	},
	"time_warp": {
		"category": SkillCategory.MAGIC_ACTIVE,
		"target": TargetType.SELF,
		"name": "时间扭曲",
		"description": "消耗10魔力获得额外次要行动(1/战斗)",
		"vfx": "time_warp",
		"action_cost": "major",
	},
	# ================================================================
	# INT — 被动技能
	# ================================================================
	"ether_sense": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "以太感知",
		"description": "获得施法能力，魔力上限+10",
	},
	"spell_mastery": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "法术精通",
		"description": "法术DC+2，获得2环法术",
	},
	"spell_penetration": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "法术穿透",
		"description": "目标抗性检定-2",
	},
	"range_expand": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "范围扩张",
		"description": "范围法术范围+1格",
	},
	"spell_slot_2": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "高阶法术位(2环)",
		"description": "获得2环法术，魔力上限+5",
	},
	"spell_slot_3": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "高阶法术位(3环)",
		"description": "获得3环法术，魔力上限+5",
	},
	"quick_cast": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "快速施法",
		"description": "1次/战斗法术作为次要行动",
	},
	"arcane_resonance": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "奥术共鸣",
		"description": "每次施法后下次法术伤害+20%，可叠加2次",
	},
	# ================================================================
	# INT — Keystone
	# ================================================================
	"absolute_focus": {
		"category": SkillCategory.KEYSTONE,
		"target": TargetType.SELF,
		"name": "绝对专注",
		"description": "法术DC+4",
		"cost": "不能学习其他体系的法术",
	},
	# ================================================================
	# WIS 感知区域 — 主动技能
	# ================================================================
	"basic_heal": {
		"category": SkillCategory.HEAL_ACTIVE,
		"target": TargetType.SINGLE_ALLY,
		"name": "基础治疗",
		"description": "恢复友军1d8+WIS修正HP",
		"vfx": "heal",
		"action_cost": "major",
	},
	"blessing": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SINGLE_ALLY,
		"name": "祈福",
		"description": "友军+1d4攻击/豁免3回合",
		"vfx": "blessing",
		"action_cost": "major",
	},
	"holy_shield": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SINGLE_ALLY,
		"name": "神圣护盾",
		"description": "友军获得临时HP=WIS修正x3",
		"vfx": "holy_shield",
		"action_cost": "major",
	},
	"mass_heal": {
		"category": SkillCategory.HEAL_ACTIVE,
		"target": TargetType.AOE_SMALL,
		"name": "群体治疗",
		"description": "恢复范围内所有友军HP",
		"vfx": "mass_heal",
		"action_cost": "major",
	},
	"dispel": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.AOE_SMALL,
		"name": "驱散",
		"description": "解除范围内负面状态/亡灵",
		"vfx": "dispel",
		"action_cost": "major",
	},
	"holy_judgment": {
		"category": SkillCategory.MAGIC_ACTIVE,
		"target": TargetType.SINGLE_ENEMY,
		"name": "圣光审判",
		"description": "亡灵/恶魔3d8，其他1d8+恢复伤害一半HP",
		"vfx": "holy_judgment",
		"action_cost": "major",
	},
	"natures_wrath": {
		"category": SkillCategory.MAGIC_ACTIVE,
		"target": TargetType.SINGLE_ENEMY,
		"name": "自然之怒",
		"description": "绊索陷阱束缚敌人1回合+1d6穿刺",
		"vfx": "nature_wrath",
		"action_cost": "major",
	},
	# ================================================================
	# WIS — 被动技能
	# ================================================================
	"nature_affinity": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "自然亲和",
		"description": "治疗法术额外恢复1d4 HP",
	},
	"life_spring": {
		"category": SkillCategory.KEYSTONE,
		"target": TargetType.SELF,
		"name": "生命之泉",
		"description": "每回合结束恢复周围友军1d6 HP",
		"cost": "自身移动速度-1",
	},
	"soul_guardian": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "灵魂守护",
		"description": "友军HP降至0时自动恢复1d10+WIS修正(1/战斗)",
	},
	# ================================================================
	# CHA 魅力区域 — 主动技能
	# ================================================================
	"war_cry": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.ALL_ADJACENT,
		"name": "战吼",
		"description": "范围内友军下次攻击+2伤害",
		"vfx": "war_cry",
		"action_cost": "major",
	},
	"inspire": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.ALL_ALLIES,
		"name": "鼓舞士气",
		"description": "所有友军士气+2持续3回合",
		"vfx": "inspire",
		"action_cost": "major",
	},
	"rally": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.AOE_SMALL,
		"name": "号召力",
		"description": "恢复范围内友军士气至满",
		"vfx": "rally",
		"action_cost": "major",
	},
	"intimidate": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SINGLE_ENEMY,
		"name": "威压",
		"description": "敌人攻击检定-2(3回合)，WIS豁免",
		"vfx": "intimidate",
		"action_cost": "major",
	},
	"heroic_call": {
		"category": SkillCategory.SUPPORT_ACTIVE,
		"target": TargetType.SELF,
		"name": "英雄号召",
		"description": "插战旗，友军攻击+2 AC+1持续3回合",
		"vfx": "heroic_call",
		"action_cost": "major",
	},
	# ================================================================
	# CHA — 被动技能
	# ================================================================
	"diplomacy": {
		"category": SkillCategory.OUT_OF_COMBAT,
		"target": TargetType.SELF,
		"name": "外交手腕",
		"description": "商店价格-20%，招募价格-15%",
	},
	"command_aura": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "统帅光环",
		"description": "周围友军攻击+1 AC+1",
	},
	"merchant_empire": {
		"category": SkillCategory.OUT_OF_COMBAT,
		"target": TargetType.SELF,
		"name": "商业帝国",
		"description": "战斗结束额外金币+稀有物品概率+15%",
	},
	"vow_of_vengeance": {
		"category": SkillCategory.PASSIVE,
		"target": TargetType.SELF,
		"name": "复仇誓言",
		"description": "标记复仇目标，对其伤害+25%，目标死亡时恢复全队10%HP",
	},
	# ================================================================
	# CHA — Keystone
	# ================================================================
	"royal_presence": {
		"category": SkillCategory.KEYSTONE,
		"target": TargetType.SELF,
		"name": "君临天下",
		"description": "范围内友军全豁免+2不会恐慌",
		"cost": "自身HP-20%",
	},
}


# ============================================================================
# 查询接口
# ============================================================================

## 获取技能配置（安全访问）
static func get_skill_config(skill_effect: String) -> Dictionary:
	if _SKILL_REGISTRY.has(skill_effect):
		return _SKILL_REGISTRY[skill_effect]
	return {}


## 判断技能是否为主动技能
static func is_active_skill(skill_effect: String) -> bool:
	var cfg = get_skill_config(skill_effect)
	if cfg.is_empty():
		return false
	var cat: int = cfg.get("category", -1)
	return cat in [
		SkillCategory.MELEE_ACTIVE, SkillCategory.RANGED_ACTIVE,
		SkillCategory.MAGIC_ACTIVE, SkillCategory.HEAL_ACTIVE,
		SkillCategory.SUPPORT_ACTIVE,
	]


## 判断技能是否为被动技能
static func is_passive_skill(skill_effect: String) -> bool:
	var cfg = get_skill_config(skill_effect)
	if cfg.is_empty():
		return false
	var cat: int = cfg.get("category", -1)
	return cat in [SkillCategory.PASSIVE, SkillCategory.KEYSTONE, SkillCategory.OUT_OF_COMBAT]


## 获取所有主动技能列表
static func get_all_active_skill_ids() -> Array[String]:
	var result: Array[String] = []
	for key in _SKILL_REGISTRY:
		if is_active_skill(key):
			result.append(key)
	return result
