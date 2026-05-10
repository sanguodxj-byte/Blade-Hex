# UnitData.gd
# 单位基础数据资源 (RPG 核心版本)
# 对应策划案 05/06 — 完整装备槽位、敌方模板、词缀加成
extends Resource
class_name UnitData

@export var unit_name: String = "未命名单位"
@export var level: int = 1

# ================================
# 六维属性 (Core Attributes)
# ================================
@export var str: int = 10 # 力量：影响近战命中、伤害、负重
@export var dex: int = 10 # 敏捷：影响远程命中、灵巧武器、AC、先攻
@export var con: int = 10 # 体质：影响最大生命值、强韧豁免
@export var intel: int = 10 # 智力：法师施法属性
@export var wis: int = 10 # 感知：牧师施法属性、意志豁免、侦察
@export var cha: int = 10 # 魅力：统帅值、士气光环、游吟诗人施法

# ================================
# 技能盘数据 (Skill Tree)
# ================================
## 技能盘序列化数据（保存/加载用）
@export var skill_tree_data: Dictionary = {}
## 角色唯一ID（用于关联全局技能盘管理器）
@export var character_id: int = -1

# ================================
# 基础战斗属性 (不含装备加成)
# ================================
@export var base_max_hp: int = 10
@export var base_ac: int = 10
@export var base_ap: int = 4 # 基础行动点 (AP)
@export var base_move_range: int = 4
@export var base_initiative: int = 0

# ================================
# 装甲系统 (Damage Reduction)
# 人形单位由穿戴护甲提供，非人形由天然护甲提供
# DR 是独立于HP的第二层生命值，被击穿前保护HP
# ================================
## 当前装甲耐久（运行时，额外HP）
@export var current_dr: int = 0
## 最大装甲耐久（由装备/天然护甲决定）
var max_dr: int = 0
## 天然装甲耐久（非人形专用，如龙鳞/龟壳）
@export var natural_dr: int = 0
## 天然装甲穿透阈值（非人形专用，d20对抗值）
@export var natural_dr_threshold: int = 0

# ================================
# 种族与特质 (Race & Traits)
# ================================
## 种族数据
@export var race: RaceData
## 角色特质列表
@export var character_traits: Array[TraitData] = []

# ================================
# 经验与等级
# ================================
## 累计经验值
@export var xp: int = 0
## 技能点（剩余未分配）
@export var skill_points: int = 0
## 未分配的属性点（每级+1，由升级系统增加）
var unspent_attr_points: int = 0
## 已使用跳跃次数
@export var jumps_used: int = 0

# ================================
# 法术系统 (Spell System)
# ================================
## 已学习的法术列表
@export var known_spells: Array[SpellData] = []
## 魔力池当前值（运行时）
@export var current_mana: int = 0
## 法术冷却状态（法术ID → 剩余冷却回合数）
var spell_cooldowns: Dictionary = {}
## 施法属性键名（默认"intel"，可被技能盘改为"cha"）
@export var casting_ability: String = "intel"

# ================================
# 坐骑系统 (Mount System)
# ================================
## 当前坐骑
@export var mount: MountData
## 坐骑当前HP（运行时）
var mount_current_hp: int = 0
## 是否骑乘状态
var is_mounted: bool = false

# ================================
# 消耗品背包 (Consumables)
# ================================
## 携带的消耗品列表
@export var consumables: Array[ConsumableData] = []

# ================================
# 装备槽位 (Equipment Slots)
# 对应策划案 06 → 装备槽位总览
# ================================
## 身体防具
@export var armor: ArmorData

## 盾牌槽（独立于武器组，持盾时主武器必须单手）
@export var shield: ArmorData

## 头部防具
@export var helmet: ArmorData

## 饰品槽×2
@export var accessory_1: AccessoryData
@export var accessory_2: AccessoryData

# 武器组 A (主武器配置)
@export var primary_main_hand: WeaponData
@export var primary_off_hand: ItemData # 可以是盾牌(ArmorData)或副手武器(WeaponData)

# 武器组 B (副武器配置，例如弓箭)
@export var secondary_main_hand: WeaponData
@export var secondary_off_hand: ItemData

# ================================
# 技能列表 (Skills)
# ================================
@export var skills: Array[SkillData] = []

@export var portrait: Texture2D
@export var battle_sprite: Texture2D
@export var overworld_sprite: Texture2D
@export var sprite_frames: SpriteFrames

# ================================
# 敌方专属字段 (Enemy-specific)
# ================================

## 敌人类型分类（对应 08-敌方与AI.md §1.1）
enum EnemyType {
	HUMANOID,     ## 类人：有装备/战术，可被交涉/招募
	BEAST,        ## 野兽：本能驱动/群体
	UNDEAD,       ## 亡灵：免疫心灵/毒素
	DEMON,        ## 魔物：法术抗性/特殊能力
	GIANT,        ## 巨型：高HP/范围攻击
	CONSTRUCT,    ## 构造体：人工制造/远古遗存，无自主意识
	DRAGON,       ## 龙族：远古智慧生物，巅峰战斗体
	LEGENDARY,    ## 传奇：超越常规生态的终极存在
}

## 体型分类（对应 08-敌方与AI.md §1.2）
enum CreatureSize {
	TINY,       ## 微型：占格拥挤，AC+3，HP×0.25
	SMALL,      ## 小型：占1格，AC+1，HP×0.5
	MEDIUM,     ## 中型：占1格
	LARGE,      ## 大型：占4格(2×2)，AC-1，HP×2
	HUGE,       ## 巨型：占9格(3×3)，AC-2，HP×4
	GARGANTUAN, ## 超巨型：占16格(4×4)，AC-3，HP×8
}

## AI 策略风格（对应 08-敌方与AI.md §3.2）
enum AIStrategy {
	RECKLESS,  ## 鲁莽：总是冲锋（兽类/狂战士）
	CAUTIOUS,  ## 谨慎：优先掩体/远程（射手/法师）
	TACTICAL,  ## 战术：包抄/集火（老兵/指挥官）
	INSTINCT,  ## 本能：随机目标（低智怪物）
	TERRITORIAL, ## 领地：不离开指定区域（守卫/魔像）
	CUNNING,   ## 狡诈：针对法师优先/破坏阵型（高智恶魔）
	INTIMIDATE, ## 恐吓：优先使用恐惧/范围攻击（龙/大型怪物）
	BERSERK,   ## 狂暴：HP<50%时伤害翻倍但AC-2（狂暴单位）
}

## 士气等级
enum MoraleLevel {
	HIGH,      ## 高昂 (+20~+40)：暴击率+20%
	NORMAL,    ## 正常 (-19~+19)：无效果
	LOW,       ## 低落 (-39~-20)：失误率+20%
	BROKEN,    ## 崩溃 (-59~-40)：失误率+40%，AC-2
	ROUTING    ## 溃逃 (-60)：强制撤退
}

## 敌方模板ID（用于敌方生成系统）
@export var enemy_template_id: String = ""

@export var is_enemy: bool = false
@export var enemy_type: EnemyType = EnemyType.HUMANOID
@export var creature_size: CreatureSize = CreatureSize.MEDIUM
@export var threat_level: float = 0.0  ## 威胁等级(CR)
@export var ai_strategy: AIStrategy = AIStrategy.INSTINCT
@export var morale: int = 0  ## 士气值：-60 到 +40

## 免疫状态列表（如：poison, mind, fear, fatigue, instant_death）
@export var immunities: Array[String] = []
## 伤害抗性列表（如：piercing, cold, nonmagical_physical）
@export var resistances: Array[String] = []
## 弱点列表（如：fire×1.5, holy×2, lightning_rust）
@export var weaknesses: Array[String] = []
## 特性描述列表
@export var traits: Array[String] = []

# ================================
# 传奇专属字段（CR 13+ 使用）
# ================================
## 传奇抗性次数/战斗
@export var legendary_resistance_uses: int = 0
## 传奇行动点/回合
@export var legendary_action_points: int = 0
## 传奇行动列表
@export var legendary_actions: Array[Dictionary] = []
## 巢穴行动列表
@export var lair_actions: Array[Dictionary] = []
## 多阶段数据（阶段列表）
@export var phases: Array[Dictionary] = []
## 独特掉落物ID
@export var unique_drop_id: String = ""

# ================================
# 战斗运行时状态 (Runtime Combat State)
# ================================
## 朝向（0-5，对应6个方向）
var facing: int = 0
## 是否处于防御模式
var is_defending: bool = false
## 忠诚度（0-100）
var loyalty: int = 50
## 死亡豁免成功次数
var death_save_successes: int = 0
## 死亡豁免失败次数
var death_save_failures: int = 0
## 活跃状态效果列表（运行时，不序列化）
var active_status_effects: Array[Dictionary] = []
## 本回合已使用借机攻击
var aoo_used_this_turn: bool = false
## 本回合已使用反击
var counter_used_this_turn: bool = false

# ================================
# 词缀加成缓存（运行时由装备计算）
# ================================
## 来自饰品的属性加成缓存
var accessory_str_bonus: int = 0
var accessory_dex_bonus: int = 0
var accessory_con_bonus: int = 0
var accessory_int_bonus: int = 0
var accessory_wis_bonus: int = 0
var accessory_cha_bonus: int = 0
var accessory_hp_bonus: int = 0
var accessory_ac_bonus: int = 0
var accessory_move_bonus: int = 0
var accessory_initiative_bonus: int = 0


## 获取士气等级
func get_morale_level() -> int:
	if morale >= 20:
		return MoraleLevel.HIGH
	elif morale >= -19:
		return MoraleLevel.NORMAL
	elif morale >= -39:
		return MoraleLevel.LOW
	elif morale >= -59:
		return MoraleLevel.BROKEN
	else:
		return MoraleLevel.ROUTING

## 获取敌人类型显示名
func get_enemy_type_name() -> String:
	match enemy_type:
		EnemyType.HUMANOID: return "类人"
		EnemyType.BEAST: return "野兽"
		EnemyType.UNDEAD: return "亡灵"
		EnemyType.DEMON: return "魔物"
		EnemyType.GIANT: return "巨型"
		EnemyType.CONSTRUCT: return "构造体"
		EnemyType.DRAGON: return "龙族"
		EnemyType.LEGENDARY: return "传奇"
		_: return "未知"

## 获取AI策略显示名
func get_ai_strategy_name() -> String:
	match ai_strategy:
		AIStrategy.RECKLESS: return "鲁莽"
		AIStrategy.CAUTIOUS: return "谨慎"
		AIStrategy.TACTICAL: return "战术"
		AIStrategy.INSTINCT: return "本能"
		AIStrategy.TERRITORIAL: return "领地"
		AIStrategy.CUNNING: return "狡诈"
		AIStrategy.INTIMIDATE: return "恐吓"
		AIStrategy.BERSERK: return "狂暴"
		_: return "未知"

## 获取体型显示名
func get_size_name() -> String:
	match creature_size:
		CreatureSize.TINY: return "微型"
		CreatureSize.SMALL: return "小型"
		CreatureSize.MEDIUM: return "中型"
		CreatureSize.LARGE: return "大型"
		CreatureSize.HUGE: return "巨型"
		CreatureSize.GARGANTUAN: return "超巨型"
		_: return "未知"

## 获取CR显示文本
func get_cr_text() -> String:
	if threat_level == 0:
		return "CR 0"
	elif threat_level < 1:
		var denominator = roundi(1.0 / threat_level)
		return "CR 1/%d" % denominator
	else:
		return "CR %d" % roundi(threat_level)


# ================================
# 装备逻辑
# ================================

## 装备一个物品到对应槽位
func equip_item(item: ItemData, economy_manager: Node):
	if item is ArmorData:
		var armor_item = item as ArmorData
		if armor_item.armor_type == ArmorData.ArmorType.SHIELD:
			# 盾牌装到盾牌槽
			if shield: unequip_item("shield", economy_manager)
			shield = armor_item
		else:
			if armor: unequip_item("armor", economy_manager)
			armor = armor_item
		if economy_manager: economy_manager.remove_item(item)
	elif item is WeaponData:
		# 默认装在主手组 A
		if primary_main_hand: unequip_item("primary_main", economy_manager)
		primary_main_hand = item
		if economy_manager: economy_manager.remove_item(item)
	elif item is AccessoryData:
		# 饰品装到第一个空槽，都满则替换第一个
		if not accessory_1:
			accessory_1 = item
		elif not accessory_2:
			accessory_2 = item
		else:
			unequip_item("accessory_1", economy_manager)
			accessory_1 = item
		if economy_manager: economy_manager.remove_item(item)
	_refresh_accessory_bonuses()

## 卸下指定槽位的装备
func unequip_item(slot: String, economy_manager: Node):
	match slot:
		"armor":
			if armor:
				if economy_manager: economy_manager.add_item(armor)
				armor = null
		"shield":
			if shield:
				if economy_manager: economy_manager.add_item(shield)
				shield = null
		"helmet":
			if helmet:
				if economy_manager: economy_manager.add_item(helmet)
				helmet = null
		"accessory_1":
			if accessory_1:
				if economy_manager: economy_manager.add_item(accessory_1)
				accessory_1 = null
		"accessory_2":
			if accessory_2:
				if economy_manager: economy_manager.add_item(accessory_2)
				accessory_2 = null
		"primary_main":
			if primary_main_hand:
				if economy_manager: economy_manager.add_item(primary_main_hand)
				primary_main_hand = null
		"primary_off":
			if primary_off_hand:
				if economy_manager: economy_manager.add_item(primary_off_hand)
				primary_off_hand = null
		"secondary_main":
			if secondary_main_hand:
				if economy_manager: economy_manager.add_item(secondary_main_hand)
				secondary_main_hand = null
		"secondary_off":
			if secondary_off_hand:
				if economy_manager: economy_manager.add_item(secondary_off_hand)
				secondary_off_hand = null
	_refresh_accessory_bonuses()

## 刷新饰品加成缓存
func _refresh_accessory_bonuses():
	accessory_str_bonus = 0
	accessory_dex_bonus = 0
	accessory_con_bonus = 0
	accessory_int_bonus = 0
	accessory_wis_bonus = 0
	accessory_cha_bonus = 0
	accessory_hp_bonus = 0
	accessory_ac_bonus = 0
	accessory_move_bonus = 0
	accessory_initiative_bonus = 0

	for acc in [accessory_1, accessory_2]:
		if acc and acc is AccessoryData:
			accessory_str_bonus += acc.str_bonus
			accessory_dex_bonus += acc.dex_bonus
			accessory_con_bonus += acc.con_bonus
			accessory_int_bonus += acc.int_bonus
			accessory_wis_bonus += acc.wis_bonus
			accessory_cha_bonus += acc.cha_bonus
			accessory_hp_bonus += acc.hp_bonus
			accessory_ac_bonus += acc.ac_bonus
			accessory_move_bonus += acc.move_bonus
			accessory_initiative_bonus += acc.initiative_bonus


# ================================
# 装备加成计算接口
# ================================

## 获取装备带来的总AC加成（防具+盾牌+饰品+词缀）
func get_equipment_ac_bonus() -> int:
	var bonus = 0
	if armor:
		bonus += armor.bonus_ac
	if shield:
		bonus += shield.bonus_ac
	if helmet:
		bonus += helmet.bonus_ac
	bonus += accessory_ac_bonus
	return bonus

## 获取装备带来的总HP加成
func get_equipment_hp_bonus() -> int:
	var bonus = 0
	if armor:
		bonus += armor.bonus_hp
	if helmet:
		bonus += helmet.bonus_hp
	bonus += accessory_hp_bonus
	return bonus

## 获取装备带来的总移动加成
func get_equipment_move_bonus() -> int:
	var bonus = 0
	if armor:
		bonus += armor.bonus_move
	bonus += accessory_move_bonus
	# 防具减速
	if armor and armor.movement_penalty > 0:
		bonus -= armor.movement_penalty
	return bonus

## 获取装备带来的总先攻加成
func get_equipment_initiative_bonus() -> int:
	return accessory_initiative_bonus

## 获取所有装备的抗性列表
func get_equipment_resistances() -> Array[String]:
	var res: Array[String] = []
	if armor and armor.bonus_resistance != "":
		if not res.has(armor.bonus_resistance): res.append(armor.bonus_resistance)
	if helmet and helmet.bonus_resistance != "":
		if not res.has(helmet.bonus_resistance): res.append(helmet.bonus_resistance)
	for acc in [accessory_1, accessory_2]:
		if acc and acc is AccessoryData and acc.resistance != "":
			if not res.has(acc.resistance): res.append(acc.resistance)
	return res

## 获取所有装备的免疫列表
func get_equipment_immunities() -> Array[String]:
	var imm: Array[String] = []
	if armor and armor.bonus_immunity != "":
		if not imm.has(armor.bonus_immunity): imm.append(armor.bonus_immunity)
	if helmet and helmet.bonus_immunity != "":
		if not imm.has(helmet.bonus_immunity): imm.append(helmet.bonus_immunity)
	for acc in [accessory_1, accessory_2]:
		if acc and acc is AccessoryData and acc.immunity != "":
			if not imm.has(acc.immunity): imm.append(acc.immunity)
	return imm

## 获取所有已装备物品列表
func get_all_equipped_items() -> Array[ItemData]:
	var items: Array[ItemData] = []
	if armor: items.append(armor)
	if shield: items.append(shield)
	if helmet: items.append(helmet)
	if primary_main_hand: items.append(primary_main_hand)
	if primary_off_hand: items.append(primary_off_hand)
	if secondary_main_hand: items.append(secondary_main_hand)
	if secondary_off_hand: items.append(secondary_off_hand)
	if accessory_1: items.append(accessory_1)
	if accessory_2: items.append(accessory_2)
	return items
