# UnitTemplateDB.gd
# 单位模板数据库 — 120级等级体系
# 每级+1属性点，1级总属性=25
# 等级定位：杂兵1~5, 熟练6~18, 精英18~36, 首领42~66, 传奇78~120
# CR = floor(level/6)，可由模板手动覆盖
class_name UnitTemplateDB
extends RefCounted


# ============================================================================
# 属性分配引擎
# 模板只需指定等级 + 属性分配权重，由引擎计算实际属性值
# ============================================================================

## 属性分配权重 → 实际属性值
## weights: 6项属性的权重（无需归一化），按 str/dex/con/intel/wis/cha 顺序
## level: 等级
## 返回: Dictionary {str, dex, con, intel, wis, cha}
static func distribute_attrs(weights: Array[float], level: int) -> Dictionary:
	var total_points = RPGRuleEngine.get_total_attr_points(level)
	var weight_sum = 0.0
	for w in weights:
		weight_sum += maxi(w, 0.01)  # 避免除零

	var attrs = {}
	var keys = RPGRuleEngine.ATTR_KEYS
	var allocated = 0

	for i in range(6):
		var share = int(round(total_points * weights[i] / weight_sum))
		attrs[keys[i]] = clampi(share, RPGRuleEngine.ATTR_MIN, RPGRuleEngine.ATTR_MAX)
		allocated += attrs[keys[i]]

	# 修正舍入误差：增减最高权重属性直到总值匹配
	var diff = total_points - allocated
	var primary_idx = 0
	var max_w = -1.0
	for i in range(6):
		if weights[i] > max_w:
			max_w = weights[i]
			primary_idx = i

	while diff > 0:
		attrs[keys[primary_idx]] = mini(attrs[keys[primary_idx]] + 1, RPGRuleEngine.ATTR_MAX)
		diff -= 1
	while diff < 0:
		# 从最低权重的属性扣除
		var min_idx = 0
		var min_w = 999.0
		for i in range(6):
			if weights[i] < min_w and attrs[keys[i]] > RPGRuleEngine.ATTR_MIN:
				min_w = weights[i]
				min_idx = i
		attrs[keys[min_idx]] = maxi(attrs[keys[min_idx]] - 1, RPGRuleEngine.ATTR_MIN)
		diff += 1

	return attrs


## 从模板数据构建完整属性（等级 + 权重 + 可选手动覆盖）
## tpl: 模板字典，必须包含 "level" 和 "attr_weights"
## 可选 "attr_overrides" 用于手动覆盖特定属性
static func build_attrs_from_template(tpl: Dictionary) -> Dictionary:
	var attrs = distribute_attrs(tpl["attr_weights"], tpl["level"])
	# 应用手动覆盖
	if tpl.has("attr_overrides"):
		for key in tpl["attr_overrides"]:
			attrs[key] = tpl["attr_overrides"][key]
	return attrs


## 从模板计算HP
## HP = base_hp + CON修正 × 等级
## base_hp 由生物类型决定：杂兵6, 熟练8, 精英10, 首领14, 传奇18
static func calculate_hp_from_template(tpl: Dictionary) -> int:
	var attrs = build_attrs_from_template(tpl)
	var base_hp = tpl.get("base_hp", 10)
	return RPGRuleEngine.calculate_max_hp(base_hp, attrs["con"], tpl["level"])


## 从模板计算CR（默认 floor(level/6)，可手动覆盖）
static func calculate_cr_from_template(tpl: Dictionary) -> float:
	if tpl.has("cr_override"):
		return tpl["cr_override"]
	return RPGRuleEngine.get_cr_from_level(tpl["level"])


## 从模板字典创建 UnitData 实例
static func instantiate_template(tpl: Dictionary) -> UnitData:
	var unit = UnitData.new()
	unit.unit_name = tpl["name"]
	unit.level = tpl["level"]
	unit.is_enemy = true
	unit.enemy_type = tpl["enemy_type"]
	unit.creature_size = tpl.get("creature_size", UnitData.CreatureSize.MEDIUM)
	unit.threat_level = calculate_cr_from_template(tpl)
	unit.ai_strategy = tpl["ai_strategy"]
	unit.morale = tpl.get("morale", 0)

	# 属性
	var attrs = build_attrs_from_template(tpl)
	unit.str = attrs["str"]
	unit.dex = attrs["dex"]
	unit.con = attrs["con"]
	unit.intel = attrs["intel"]
	unit.wis = attrs["wis"]
	unit.cha = attrs["cha"]

	# HP
	unit.base_max_hp = calculate_hp_from_template(tpl)

	# AC（基础10 + 模板AC加成）
	unit.base_ac = 10 + tpl.get("ac_bonus", 0)

	# 移动/先攻
	unit.base_move_range = tpl.get("move_range", 4)
	unit.base_initiative = tpl.get("initiative_bonus", 0)

	# 免疫/抗性/弱点/特性
	unit.immunities = tpl.get("immunities", []).duplicate()
	unit.resistances = tpl.get("resistances", []).duplicate()
	unit.weaknesses = tpl.get("weaknesses", []).duplicate()
	unit.traits = tpl.get("traits", []).duplicate()

	# 天然装甲（非人形）
	unit.natural_dr = tpl.get("natural_dr", 0)
	unit.natural_dr_threshold = tpl.get("natural_dr_threshold", 0)

	# 传奇属性
	unit.legendary_resistance_uses = tpl.get("legendary_resistance_uses", 0)
	unit.legendary_action_points = tpl.get("legendary_action_points", 0)
	unit.legendary_actions = tpl.get("legendary_actions", []).duplicate(true)
	unit.lair_actions = tpl.get("lair_actions", []).duplicate(true)
	unit.phases = tpl.get("phases", []).duplicate(true)
	unit.unique_drop_id = tpl.get("unique_drop_id", "")

	return unit


# ============================================================================
# 属性分配权重预设
# ============================================================================

## 力量型近战（战士/骑士）：STR>CON>DEX>WIS>CHA>INT
static var W_MELEE_BRUISER: Array[float] = [3.0, 1.5, 2.5, 0.5, 1.0, 0.5]
## 敏捷型射手/刺客：DEX>STR>WIS>CON>CHA>INT
static var W_RANGED_AGILITY: Array[float] = [1.0, 3.0, 1.0, 0.5, 1.5, 1.0]
## 法师型：INT>WIS>CHA>DEX>CON>STR
static var W_MAGE: Array[float] = [0.5, 1.0, 1.0, 3.0, 2.0, 1.5]
## 野兽型：STR>DEX>CON>WIS>INT>CHA
static var W_BEAST: Array[float] = [2.5, 2.0, 2.0, 1.0, 0.5, 0.0]
## 巨型/坦克：STR>CON>WIS>DEX>CHA>INT
static var W_TANK: Array[float] = [2.5, 0.5, 3.0, 0.5, 1.5, 1.0]
## 领袖/统帅：CHA>STR>CON>INT>WIS>DEX
static var W_LEADER: Array[float] = [1.5, 1.0, 1.5, 1.5, 1.0, 2.5]
## 构造体：STR>CON>DEX>INT>WIS>CHA
static var W_CONSTRUCT: Array[float] = [3.0, 1.0, 3.0, 0.0, 0.5, 0.0]
## 龙族：全属性均衡偏高
static var W_DRAGON: Array[float] = [2.0, 1.5, 2.0, 1.5, 1.5, 1.5]
## 狡诈型：INT>CHA>DEX>WIS>CON>STR
static var W_CUNNING: Array[float] = [0.5, 1.5, 1.0, 2.5, 1.5, 2.0]


# ============================================================================
# 杂兵模板（等级 1~5）
# 总属性25~29，CR 0~0
# ============================================================================

## 哥布林战士 (Lv.2, CR 0) — 杂兵中的炮灰
static func grunt_goblin_warrior() -> Dictionary:
	return {
		"template_id": "grunt_goblin_warrior",
		"name": "哥布林战士",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 2,
		"attr_weights": [1.5, 2.5, 1.0, 1.0, 0.5, 0.5],
		"base_hp": 6,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"traits": ["群体战术", "卑鄙偷袭"],
		"description": "矮小狡猾的哥布林，数量众多时格外危险。",
	}

## 哥布林弓手 (Lv.2, CR 0)
static func grunt_goblin_archer() -> Dictionary:
	return {
		"template_id": "grunt_goblin_archer",
		"name": "哥布林弓手",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 2,
		"attr_weights": [0.5, 3.0, 0.5, 1.0, 1.0, 0.5],
		"base_hp": 5,
		"ac_bonus": 0,
		"move_range": 5,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"traits": ["远程骚扰", "游击战"],
		"description": "擅长在远处用淬毒箭矢骚扰敌人。",
	}

## 骷髅战士 (Lv.3, CR 0) — 亡灵炮灰
static func grunt_skeleton_warrior() -> Dictionary:
	return {
		"template_id": "grunt_skeleton_warrior",
		"name": "骷髅战士",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 3,
		"attr_weights": [1.5, 2.0, 1.0, 0.5, 0.5, 0.0],
		"base_hp": 6,
		"ac_bonus": 2,
		"move_range": 5,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"natural_dr": 12, "natural_dr_threshold": 3,
		"immunities": ["poison", "mind"],
		"resistances": ["pierce"],
		"traits": ["亡灵坚韧", "不知疲倦"],
		"description": "被黑暗魔法唤醒的骸骨战士，没有痛觉和恐惧。",
	}

## 森林狼 (Lv.3, CR 0) — 低级野兽
static func grunt_forest_wolf() -> Dictionary:
	return {
		"template_id": "grunt_forest_wolf",
		"name": "森林狼",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 3,
		"attr_weights": W_BEAST,
		"base_hp": 7,
		"ac_bonus": 1,
		"move_range": 8,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"natural_dr": 8, "natural_dr_threshold": 2,
		"traits": ["嗅觉追踪", "群猎：相邻每有1个友方狼，攻击+1"],
		"description": "成群出没的森林狼，嗅觉灵敏，善于包围猎物。",
	}

## 腐尸 (Lv.4, CR 0) — 亡灵肉盾
static func grunt_zombie() -> Dictionary:
	return {
		"template_id": "grunt_zombie",
		"name": "腐尸",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 4,
		"attr_weights": [2.0, 0.5, 2.0, 0.5, 0.5, 0.0],
		"base_hp": 8,
		"ac_bonus": -2,
		"move_range": 4,
		"initiative_bonus": -3,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"natural_dr": 4, "natural_dr_threshold": 1,
		"immunities": ["poison", "mind"],
		"traits": ["腐烂之躯：近战攻击者中毒"],
		"description": "行动迟缓的腐尸，但肉体异常坚韧。",
	}

## 史莱姆 (Lv.3, CR 0) — 吸收型杂兵
static func grunt_slime() -> Dictionary:
	return {
		"template_id": "grunt_slime",
		"name": "史莱姆",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 3,
		"attr_weights": [1.0, 0.5, 3.0, 0.0, 0.5, 0.0],
		"base_hp": 7,
		"ac_bonus": -2,
		"move_range": 4,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"natural_dr": 30, "natural_dr_threshold": 4,
		"immunities": ["poison", "mind"],
		"resistances": ["pierce", "slash"],
		"traits": ["分裂：HP<50%时分裂为2个小史莱姆", "腐蚀：近战攻击者武器受损"],
		"description": "黏液构成的生物，物理攻击对它效果甚微。",
	}

## 小恶魔 (Lv.4, CR 0)
static func grunt_imp() -> Dictionary:
	return {
		"template_id": "grunt_imp",
		"name": "小恶魔",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 4,
		"attr_weights": [0.5, 2.5, 1.5, 1.0, 1.0, 2.0],
		"base_hp": 5,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"creature_size": UnitData.CreatureSize.TINY,
		"natural_dr": 6, "natural_dr_threshold": 1,
		"immunities": ["fire", "poison"],
		"resistances": ["cold"],
		"traits": ["飞行：无视地形", "隐身：1次/战斗"],
		"description": "体型微小的恶魔，善于飞行和隐身偷袭。",
	}

## 熔岩史莱姆 (Lv.5, CR 0)
static func grunt_lava_slime() -> Dictionary:
	return {
		"template_id": "grunt_lava_slime",
		"name": "熔岩史莱姆",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 5,
		"attr_weights": [1.0, 0.5, 3.0, 0.0, 0.5, 0.0],
		"base_hp": 8,
		"ac_bonus": -1,
		"move_range": 4,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"natural_dr": 35, "natural_dr_threshold": 5,
		"immunities": ["fire"],
		"resistances": ["physical"],
		"traits": ["液态体", "灼热：近战攻击者受1d4火焰", "分裂"],
		"description": "从火山裂缝中涌出的液态火球，触碰即灼伤。",
	}


# ============================================================================
# 熟练模板（等级 6~17）
# 总属性30~41，CR 1~2
# ============================================================================

## 哥布林首领 (Lv.8, CR 1) — 哥布林中的战术核心
static func standard_goblin_chieftain() -> Dictionary:
	return {
		"template_id": "std_goblin_chieftain",
		"name": "哥布林首领",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 8,
		"attr_weights": W_LEADER,
		"base_hp": 8,
		"ac_bonus": 2,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"immunities": ["fear"],
		"traits": ["领袖气场", "战吼：友方攻击+2持续1回合"],
		"description": "哥布林部落中最强壮的首领，狡猾而残忍。",
	}

## 兽人狂战 (Lv.9, CR 1) — 力量型人形
static func standard_orc_berserker() -> Dictionary:
	return {
		"template_id": "std_orc_berserker",
		"name": "兽人狂战",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 9,
		"attr_weights": W_MELEE_BRUISER,
		"base_hp": 8,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"morale": 10,
		"traits": ["鲁莽攻击：攻击优势但被攻击也有优势"],
		"description": "嗜血的兽人战士，崇尚绝对的力量。",
	}

## 巨型蜘蛛 (Lv.10, CR 1) — 野兽/毒系
static func standard_giant_spider() -> Dictionary:
	return {
		"template_id": "std_giant_spider",
		"name": "巨型蜘蛛",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 10,
		"attr_weights": [1.0, 3.0, 1.5, 0.5, 1.0, 0.0],
		"base_hp": 8,
		"ac_bonus": 2,
		"move_range": 8,
		"initiative_bonus": 4,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"natural_dr": 20, "natural_dr_threshold": 4,
		"traits": ["蛛网行走：无视蛛网地形惩罚", "吐丝：射程4格，目标缚足2回合"],
		"description": "体型如牛的巨型蜘蛛，毒液能让成年男子瞬间麻痹。",
	}

## 食尸鬼 (Lv.11, CR 1) — 亡灵中坚
static func standard_ghoul() -> Dictionary:
	return {
		"template_id": "std_ghoul",
		"name": "食尸鬼",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 11,
		"attr_weights": [2.0, 2.0, 1.5, 0.5, 0.5, 0.0],
		"base_hp": 8,
		"ac_bonus": 1,
		"move_range": 6,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"immunities": ["poison", "mind"],
		"resistances": ["necrotic"],
		"traits": ["腐臭爪击", "麻痹之咬：命中后DC12强韧麻痹1回合"],
		"description": "以腐肉为食的亡灵，爪牙带有麻痹毒素。",
	}

## 黑熊 (Lv.12, CR 2) — 中型野兽
static func standard_black_bear() -> Dictionary:
	return {
		"template_id": "std_black_bear",
		"name": "洞穴巨熊",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 12,
		"attr_weights": W_BEAST,
		"base_hp": 10,
		"ac_bonus": 2,
		"move_range": 6,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 40, "natural_dr_threshold": 6,
		"traits": ["多重攻击：熊掌+啃咬", "狂暴：HP<25%时攻击+2，AC-2"],
		"description": "体型巨大的洞穴熊，一掌可拍碎铠甲。",
	}

## 巨蛛 (Lv.12, CR 2)
static func standard_giant_scorpion() -> Dictionary:
	return {
		"template_id": "std_giant_scorpion",
		"name": "沙漠巨蝎",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 12,
		"attr_weights": [2.0, 1.5, 2.0, 0.0, 1.0, 0.0],
		"base_hp": 10,
		"ac_bonus": 3,
		"move_range": 6,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 50, "natural_dr_threshold": 7,
		"traits": ["毒尾穿刺：命中附带中毒DC13", "钳制：命中后目标缚足"],
		"description": "沙漠中潜伏的巨蝎，尾针的毒液致命无比。",
	}

## 巨狼 (Lv.14, CR 2) — 群猎野兽
static func standard_dire_wolf() -> Dictionary:
	return {
		"template_id": "std_dire_wolf",
		"name": "巨狼",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 14,
		"attr_weights": W_BEAST,
		"base_hp": 9,
		"ac_bonus": 2,
		"move_range": 10,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.INSTINCT,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 30, "natural_dr_threshold": 5,
		"traits": ["群猎：相邻每有1个友方狼，攻击+2", "扑击：冲锋命中后目标倒地"],
		"description": "比普通狼大一倍的巨狼，群猎时致命异常。",
	}

## 鹰身女妖 (Lv.13, CR 2) — 飞行野兽
static func standard_harpy() -> Dictionary:
	return {
		"template_id": "std_harpy",
		"name": "鹰身女妖",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 13,
		"attr_weights": [1.0, 2.5, 1.0, 0.5, 1.0, 2.0],
		"base_hp": 7,
		"ac_bonus": 1,
		"move_range": 6,
		"initiative_bonus": 4,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"traits": ["飞行：无视地形", "魅惑之歌：DC13WIS否则向其移动"],
		"description": "半人半鸟的鹰身女妖，歌声能迷惑旅人。",
	}

## 地狱犬 (Lv.15, CR 2) — 火系魔物
static func standard_hellhound() -> Dictionary:
	return {
		"template_id": "std_hellhound",
		"name": "地狱犬",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 15,
		"attr_weights": W_BEAST,
		"base_hp": 9,
		"ac_bonus": 2,
		"move_range": 7,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"natural_dr": 15, "natural_dr_threshold": 3,
		"immunities": ["fire"],
		"resistances": ["magic"],
		"traits": ["火焰吐息：锥形3格4d6火焰冷却2回合", "追踪灵魂"],
		"description": "来自深渊的猎犬，口中喷吐着不灭的地狱之火。",
	}

## 骷髅弓手 (Lv.10, CR 1)
static func standard_skeleton_archer() -> Dictionary:
	return {
		"template_id": "std_skeleton_archer",
		"name": "骷髅弓手",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 10,
		"attr_weights": [0.5, 3.0, 1.0, 0.5, 0.5, 0.0],
		"base_hp": 6,
		"ac_bonus": 2,
		"move_range": 5,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"natural_dr": 12, "natural_dr_threshold": 3,
		"immunities": ["poison", "mind"],
		"traits": ["亡灵坚韧", "不知疲倦"],
		"description": "被黑暗魔法唤醒的骸骨射手，箭术精准。",
	}

## 狮鹫 (Lv.15, CR 2) — 飞行野兽精英
static func standard_griffin() -> Dictionary:
	return {
		"template_id": "std_griffin",
		"name": "狮鹫",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 15,
		"attr_weights": [2.0, 2.0, 1.5, 0.5, 1.5, 0.5],
		"base_hp": 10,
		"ac_bonus": 3,
		"move_range": 8,
		"initiative_bonus": 4,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"immunities": ["fear"],
		"traits": ["飞行：无视地形", "鹰眼：远程攻击+1", "俯冲攻击：冲锋伤害+1d8"],
		"description": "雄鹰与雄狮的结合体，天空的王者。",
	}

## 巨魔 (Lv.17, CR 2) — 再生型
static func standard_troll() -> Dictionary:
	return {
		"template_id": "std_troll",
		"name": "森林巨魔",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 17,
		"attr_weights": [3.0, 0.5, 3.0, 0.5, 1.0, 0.5],
		"base_hp": 10,
		"ac_bonus": 1,
		"move_range": 6,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 35, "natural_dr_threshold": 5,
		"resistances": ["physical"],
		"weaknesses": ["fire×1.5", "acid×1.5"],
		"traits": ["再生：每回合恢复等级×1HP", "巨力：攻击附带推后1格"],
		"description": "力大无穷且拥有恐怖再生能力的巨魔。",
	}


# ============================================================================
# 精英模板（等级 18~36）
# 总属性42~60，CR 3~6
# ============================================================================

## 食人魔 (Lv.18, CR 3) — 巨型杂兵
static func elite_ogre() -> Dictionary:
	return {
		"template_id": "elite_ogre",
		"name": "食人魔",
		"enemy_type": UnitData.EnemyType.GIANT,
		"level": 18,
		"attr_weights": W_TANK,
		"base_hp": 12,
		"ac_bonus": 2,
		"move_range": 7,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 55, "natural_dr_threshold": 7,
		"traits": ["厚皮：物理伤害-3", "巨力：攻击附带推后1格"],
		"description": "笨重但力大无穷的食人魔，一根木棍就能横扫战场。",
	}

## 牛头人 (Lv.24, CR 4) — 迷宫守护者
static func elite_minotaur() -> Dictionary:
	return {
		"template_id": "elite_minotaur",
		"name": "牛头人",
		"enemy_type": UnitData.EnemyType.GIANT,
		"level": 24,
		"attr_weights": [3.0, 1.0, 2.5, 0.5, 1.5, 0.5],
		"base_hp": 12,
		"ac_bonus": 3,
		"move_range": 8,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 70, "natural_dr_threshold": 9,
		"traits": ["冲锋：冲锋时伤害+2d6", "迷宫直觉：不会迷路"],
		"description": "半人半牛的迷宫守护者，冲锋势不可挡。",
	}

## 石像鬼 (Lv.24, CR 4) — 魔法抗性恶魔
static func elite_gargoyle() -> Dictionary:
	return {
		"template_id": "elite_gargoyle",
		"name": "石像鬼",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 24,
		"attr_weights": W_TANK,
		"base_hp": 12,
		"ac_bonus": 5,
		"move_range": 6,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"natural_dr": 80, "natural_dr_threshold": 10,
		"resistances": ["physical", "magic"],
		"immunities": ["poison"],
		"traits": ["石化伪装：第一轮攻击优势", "飞行", "魔法抗性：法术豁免+2"],
		"description": "伪装成石像的恶魔，一旦靠近就会苏醒发起突袭。",
	}

## 腐化树人 (Lv.24, CR 4) — 自然坦克
static func elite_corrupted_treant() -> Dictionary:
	return {
		"template_id": "elite_corrupted_treant",
		"name": "腐化树人",
		"enemy_type": UnitData.EnemyType.BEAST,
		"level": 24,
		"attr_weights": [3.0, 0.5, 3.0, 0.5, 1.5, 0.0],
		"base_hp": 14,
		"ac_bonus": 2,
		"move_range": 4,
		"initiative_bonus": -3,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 90, "natural_dr_threshold": 11,
		"resistances": ["physical"],
		"immunities": ["mind"],
		"traits": ["树皮护甲：额外DR20", "根须缠绕：射程3格缚足2回合", "腐化孢子：周围1格中毒"],
		"description": "被黑暗力量腐化的远古树人，树皮坚硬如铁。",
	}

## 毒蛇女妖 (Lv.25, CR 4) — 魅惑型
static func elite_lamia() -> Dictionary:
	return {
		"template_id": "elite_lamia",
		"name": "毒蛇女妖",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 25,
		"attr_weights": W_CUNNING,
		"base_hp": 10,
		"ac_bonus": 2,
		"move_range": 7,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"resistances": ["poison"],
		"immunities": ["poison"],
		"traits": ["蛇身：移动不受困难地形影响", "毒牙：命中中毒DC15", "魅惑凝视：DC15WIS否则被控制1回合"],
		"description": "半人半蛇的诱惑者，目光能令猎物丧失战意。",
	}

## 暗影刺客 (Lv.24, CR 4) — 冒险者刺客
static func elite_shadow_assassin() -> Dictionary:
	return {
		"template_id": "elite_shadow_assassin",
		"name": "暗影刺客",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 24,
		"attr_weights": W_RANGED_AGILITY,
		"base_hp": 8,
		"ac_bonus": 2,
		"move_range": 6,
		"initiative_bonus": 6,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"traits": ["潜行大师：首轮隐身", "致命一击：暴击范围19-20", "暗影步：传送4格"],
		"description": "来自暗影公会的精英刺客，擅长从暗处给予致命一击。",
	}

## 亡灵骑士 (Lv.30, CR 5) — 亡灵精英
static func elite_death_knight() -> Dictionary:
	return {
		"template_id": "elite_death_knight",
		"name": "亡灵骑士",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 30,
		"attr_weights": W_MELEE_BRUISER,
		"base_hp": 12,
		"ac_bonus": 4,
		"move_range": 6,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"natural_dr": 90, "natural_dr_threshold": 10,
		"immunities": ["poison", "mind", "fear"],
		"resistances": ["necrotic", "cold"],
		"traits": ["骑士冲锋：冲锋时伤害×1.5", "恐惧光环：3格内敌方恐惧", "寒冰之剑：命中附带1d6冰霜"],
		"description": "堕落的骑士被黑暗力量复活，手持散发寒气的漆黑长剑。",
	}

## 火元素 (Lv.30, CR 5) — 元素毁灭者
static func elite_fire_elemental() -> Dictionary:
	return {
		"template_id": "elite_fire_elemental",
		"name": "火元素",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 30,
		"attr_weights": [1.0, 2.0, 2.0, 0.5, 1.5, 0.5],
		"base_hp": 10,
		"ac_bonus": 4,
		"move_range": 8,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.BERSERK,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 70, "natural_dr_threshold": 8,
		"immunities": ["fire", "poison", "mind", "fear"],
		"weaknesses": ["cold×2"],
		"traits": ["灵焰之躯：近战攻击者受1d6火焰反击", "火焰爆发：半径2格6d6火焰，冷却3回合"],
		"description": "纯粹的火焰元素体，接触即灼伤。",
	}

## 冰元素 (Lv.30, CR 5)
static func elite_ice_elemental() -> Dictionary:
	return {
		"template_id": "elite_ice_elemental",
		"name": "冰元素",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 30,
		"attr_weights": W_TANK,
		"base_hp": 12,
		"ac_bonus": 5,
		"move_range": 6,
		"initiative_bonus": 0,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 100, "natural_dr_threshold": 10,
		"immunities": ["cold", "poison", "mind"],
		"weaknesses": ["fire×1.5"],
		"traits": ["冰霜之躯：近战攻击者受1d6冰霜+减速", "冰锥风暴：锥形4格6d6冰霜，冷却3回合"],
		"description": "极寒的冰元素体，触碰即冰冻。",
	}

## 恶魔卫士 (Lv.36, CR 6) — 魔物中坚
static func elite_demon_guard() -> Dictionary:
	return {
		"template_id": "elite_demon_guard",
		"name": "恶魔卫士",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 36,
		"attr_weights": W_TANK,
		"base_hp": 14,
		"ac_bonus": 4,
		"move_range": 5,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 100, "natural_dr_threshold": 11,
		"resistances": ["magic", "fire", "cold"],
		"immunities": ["poison", "fear"],
		"traits": ["魔法抗性：法术豁免+3", "重击：暴击范围18-20", "恐惧光环：3格内敌方恐惧"],
		"description": "身披甲壳的深渊卫士，魔法几乎无法伤害它。",
	}

## 冰霜魔女 (Lv.36, CR 6) — 法师型精英
static func elite_frost_witch() -> Dictionary:
	return {
		"template_id": "elite_frost_witch",
		"name": "冰霜魔女",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 36,
		"attr_weights": W_MAGE,
		"base_hp": 10,
		"ac_bonus": 4,
		"move_range": 5,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"resistances": ["nonmagical_physical"],
		"immunities": ["cold", "poison", "mind"],
		"weaknesses": ["fire×1.5"],
		"traits": ["冰霜光环：周围1格每回合受1d6冰霜", "不死之身：HP归零化为冰雕3回合后满血复活，火焰可阻止"],
		"description": "操纵冰霜的魔女，不碎冰就无法真正杀死她。",
	}

## 暗影审判官 (Lv.30, CR 5) — 战术型人形
static func elite_shadow_inquisitor() -> Dictionary:
	return {
		"template_id": "elite_shadow_inquisitor",
		"name": "暗影审判官",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 30,
		"attr_weights": W_LEADER,
		"base_hp": 10,
		"ac_bonus": 5,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["necrotic"],
		"immunities": ["fear", "charm"],
		"traits": ["暗影步：传送4格", "指挥光环：3格内友方攻击+1", "不屈信仰：免疫恐惧和魅惑"],
		"description": "暗影教团的精锐审判官，暗影步绕后集火。",
	}

## 梦魇兽 (Lv.30, CR 5) — 心灵魔物
static func elite_nightmare_beast() -> Dictionary:
	return {
		"template_id": "elite_nightmare_beast",
		"name": "梦魇兽",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 30,
		"attr_weights": W_CUNNING,
		"base_hp": 10,
		"ac_bonus": 2,
		"move_range": 7,
		"initiative_bonus": 4,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["magic", "physical"],
		"immunities": ["mind", "fear"],
		"traits": ["梦魇之体：50%闪避非魔法攻击", "心灵侵蚀：攻击附带WIS DC14恐惧", "虚化：1次/战斗免疫所有伤害1回合"],
		"description": "从噩梦中诞生的生物，以恐惧为食。",
	}

## 牛头人酋长 (Lv.36, CR 6) — 牛头人领袖
static func elite_minotaur_chieftain() -> Dictionary:
	return {
		"template_id": "elite_minotaur_chieftain",
		"name": "牛头人酋长",
		"enemy_type": UnitData.EnemyType.GIANT,
		"level": 36,
		"attr_weights": [3.0, 1.0, 2.5, 0.5, 1.5, 1.0],
		"base_hp": 14,
		"ac_bonus": 4,
		"move_range": 8,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.INTIMIDATE,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 90, "natural_dr_threshold": 10,
		"traits": ["冲锋领主：冲锋伤害+3d6", "怒吼：4格内敌方攻击-2持续2回合", "领袖光环：友方攻击+2", "厚皮：物理伤害-3"],
		"description": "牛头人部落的至强战神，冲锋时如山崩地裂。",
	}

## 木质哨兵 (Lv.18, CR 3) — 木质构造体
static func construct_wooden_sentinel() -> Dictionary:
	return {
		"template_id": "con_wooden_sentinel",
		"name": "木质哨兵",
		"enemy_type": UnitData.EnemyType.CONSTRUCT,
		"level": 18,
		"attr_weights": W_CONSTRUCT,
		"base_hp": 10,
		"ac_bonus": 4,
		"move_range": 5,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.TERRITORIAL,
		"natural_dr": 50, "natural_dr_threshold": 6,
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death"],
		"weaknesses": ["fire×1.5"],
		"traits": ["魔法抗性：法术豁免+2", "不动如山：免疫推撞、缚足"],
		"description": "古代遗留的木质守护者，遵循古老指令守卫特定区域。",
	}

## 石魔像 (Lv.24, CR 4) — 石质构造体
static func construct_stone_golem() -> Dictionary:
	return {
		"template_id": "con_stone_golem",
		"name": "石魔像",
		"enemy_type": UnitData.EnemyType.CONSTRUCT,
		"level": 24,
		"attr_weights": W_CONSTRUCT,
		"base_hp": 14,
		"ac_bonus": 6,
		"move_range": 4,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.TERRITORIAL,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 160, "natural_dr_threshold": 14,
		"immunities": ["poison", "mind", "fatigue", "instant_death", "necrotic"],
		"weaknesses": ["lightning_ac_minus_2"],
		"traits": ["魔法免疫：2环以下法术完全免疫", "不动如山：免疫一切控制"],
		"description": "石质构造体，缓慢但坚不可摧。",
	}


# ============================================================================
# 首领模板（等级 42~66）
# 总属性66~90，CR 7~11
# ============================================================================

## 铁魔像 (Lv.48, CR 8) — 顶级构造体
static func boss_iron_golem() -> Dictionary:
	return {
		"template_id": "boss_iron_golem",
		"name": "铁魔像",
		"enemy_type": UnitData.EnemyType.CONSTRUCT,
		"level": 48,
		"attr_weights": W_CONSTRUCT,
		"base_hp": 18,
		"ac_bonus": 8,
		"move_range": 4,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.TERRITORIAL,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 250, "natural_dr_threshold": 18,
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death", "fire", "cold", "necrotic"],
		"weaknesses": ["lightning_speed_halved_ac_minus_3"],
		"traits": ["远古魔法免疫：3环以下法术完全免疫", "不动如山：免疫一切控制", "减伤外壳：物理伤害-5", "修复：每回合恢复20HP"],
		"description": "铁质构造体，几乎无懈可击。",
	}

## 食人魔酋长 (Lv.48, CR 8) — 巨型首领
static func boss_ogre_chief() -> Dictionary:
	return {
		"template_id": "boss_ogre_chief",
		"name": "食人魔酋长",
		"enemy_type": UnitData.EnemyType.GIANT,
		"level": 48,
		"attr_weights": W_TANK,
		"base_hp": 16,
		"ac_bonus": 3,
		"move_range": 8,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 100, "natural_dr_threshold": 11,
		"traits": ["厚皮：物理伤害-5", "嗜血：击杀后立即获得1次额外攻击", "横扫：近战范围所有敌方受攻击，冷却2回合"],
		"description": "食人魔部落中最强大的酋长，嗜血成性。",
	}

## 死灵将军 (Lv.54, CR 9) — 亡灵指挥官
static func boss_death_general() -> Dictionary:
	return {
		"template_id": "boss_death_general",
		"name": "死灵将军",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 54,
		"attr_weights": W_LEADER,
		"base_hp": 14,
		"ac_bonus": 6,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["piercing", "slashing"],
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death"],
		"weaknesses": ["holy×1.5", "radiant×1.5"],
		"traits": ["亡灵统帅：场上每有1个友方亡灵，攻击+1", "死亡光环：3格内敌方每回合受1d6暗影", "不死意志：首次致死伤害时1HP存活", "反击：被近战攻击时反击1次"],
		"description": "亡灵军团的至高统帅，后排施法召唤亡灵大军。",
	}

## 山丘巨人 (Lv.54, CR 9) — 纯粹暴力
static func boss_hill_giant() -> Dictionary:
	return {
		"template_id": "boss_hill_giant",
		"name": "山丘巨人",
		"enemy_type": UnitData.EnemyType.GIANT,
		"level": 54,
		"attr_weights": [3.0, 0.5, 3.0, 0.5, 1.0, 0.5],
		"base_hp": 18,
		"ac_bonus": 3,
		"move_range": 6,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.INTIMIDATE,
		"creature_size": UnitData.CreatureSize.HUGE,
		"natural_dr": 180, "natural_dr_threshold": 15,
		"resistances": ["nonmagical_physical"],
		"immunities": ["fear"],
		"traits": ["岩石之躯：物理伤害-5", "不可阻挡：无视控制区", "地震践踏：半径2格倒地+3d8钝击"],
		"description": "山丘间行走的活堡垒，一拳可将人击飞到墙壁上。",
	}

## 深渊恶魔 (Lv.54, CR 9) — 传送突袭型
static func boss_abyssal_demon() -> Dictionary:
	return {
		"template_id": "boss_abyssal_demon",
		"name": "深渊恶魔",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 54,
		"attr_weights": [2.0, 1.5, 2.0, 1.0, 1.0, 2.0],
		"base_hp": 16,
		"ac_bonus": 5,
		"move_range": 6,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 120, "natural_dr_threshold": 12,
		"resistances": ["cold", "lightning", "nonmagical_physical"],
		"immunities": ["fire", "poison"],
		"weaknesses": ["radiant×1.5", "holy_spell_dc_plus_2"],
		"traits": ["深渊再生：每回合恢复等级×0.5HP", "传送：传送至视野内任意位置", "混乱光环：4格内敌方攻击-1"],
		"description": "深渊军团的高阶恶魔，传送突袭后排。",
	}

## 远古石魔像 (Lv.60, CR 10) — 终极构造体
static func boss_ancient_stone_golem() -> Dictionary:
	return {
		"template_id": "boss_ancient_stone_golem",
		"name": "远古石魔像",
		"enemy_type": UnitData.EnemyType.CONSTRUCT,
		"level": 60,
		"attr_weights": W_CONSTRUCT,
		"base_hp": 20,
		"ac_bonus": 8,
		"move_range": 4,
		"initiative_bonus": -3,
		"ai_strategy": UnitData.AIStrategy.TERRITORIAL,
		"creature_size": UnitData.CreatureSize.HUGE,
		"natural_dr": 300, "natural_dr_threshold": 20,
		"resistances": ["physical"],
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death", "fire", "cold", "necrotic", "lightning", "radiant"],
		"traits": ["远古魔法免疫：4环以下法术完全免疫", "不动如山", "岩石再生：每回合恢复30HP", "远古守卫：HP<30%时伤害×3"],
		"description": "远古文明最强大的守卫，免疫几乎所有伤害类型。",
	}


# ============================================================================
# 领主模板（等级 48~72）
# 总属性72~96，CR 8~12
# ============================================================================

## 骑士团长 (Lv.48, CR 8)
static func lord_knight_commander() -> Dictionary:
	return {
		"template_id": "lord_knight_commander",
		"name": "骑士团长",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 48,
		"attr_weights": W_MELEE_BRUISER,
		"base_hp": 14,
		"ac_bonus": 6,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"immunities": ["fear"],
		"traits": ["重甲精通：穿着重甲时AC额外+2", "领导光环：4格内友方命中+2", "不屈意志：首次致死伤害时1HP存活"],
		"description": "久经沙场的骑士团长，身穿精钢板甲。",
	}

## 暗影领主 (Lv.60, CR 10)
static func lord_shadow_dominus() -> Dictionary:
	return {
		"template_id": "lord_shadow_dominus",
		"name": "暗影领主",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 60,
		"attr_weights": W_CUNNING,
		"base_hp": 12,
		"ac_bonus": 5,
		"move_range": 5,
		"initiative_bonus": 5,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["necrotic"],
		"immunities": ["poison", "fear"],
		"traits": ["暗影亲和：暗影中隐身+攻击优势", "黑暗统御：4格内敌方攻击-2", "恐惧光环：首次看到需WIS豁免否则恐惧"],
		"description": "被黑暗力量腐化的堕落贵族，统帅着恐惧军团。",
	}

## 蛮族酋长 (Lv.48, CR 8)
static func lord_barbarian_chieftain() -> Dictionary:
	return {
		"template_id": "lord_barbarian_chieftain",
		"name": "蛮族酋长",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 48,
		"attr_weights": [3.0, 1.0, 2.5, 0.5, 1.0, 1.0],
		"base_hp": 16,
		"ac_bonus": 2,
		"move_range": 6,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"resistances": ["physical"],
		"immunities": ["fear"],
		"traits": ["狂暴之力：HP<50%时攻击+4", "铁壁体魄：CON修正×2计算HP", "战争怒吼：全友方攻击+3持续2回合"],
		"description": "蛮荒之地的霸主，凭借绝对的力量碾碎一切。",
	}

## 精灵游侠将军 (Lv.54, CR 9)
static func lord_elf_ranger_general() -> Dictionary:
	return {
		"template_id": "lord_elf_ranger_general",
		"name": "精灵游侠将军",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 54,
		"attr_weights": W_RANGED_AGILITY,
		"base_hp": 10,
		"ac_bonus": 6,
		"move_range": 6,
		"initiative_bonus": 6,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"resistances": ["magic"],
		"immunities": ["charm"],
		"traits": ["鹰眼：远程攻击+2", "自然之友：森林地形移动+2", "精灵优雅：AC额外+2"],
		"description": "银叶森林的守护者，箭无虚发。",
	}

## 矮人要塞王 (Lv.54, CR 9)
static func lord_dwarf_fortress_king() -> Dictionary:
	return {
		"template_id": "lord_dwarf_fortress_king",
		"name": "矮人要塞王",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 54,
		"attr_weights": W_TANK,
		"base_hp": 16,
		"ac_bonus": 8,
		"move_range": 4,
		"initiative_bonus": -1,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["physical", "fire"],
		"immunities": ["poison", "fear"],
		"traits": ["矮人韧性：物理伤害-3", "锻造大师：装备AC+2", "山岳之盾：可掩护后方友方"],
		"description": "矮人山脉深处的要塞之王，坚如磐石。",
	}

## 沙漠苏丹 (Lv.48, CR 8)
static func lord_desert_sultan() -> Dictionary:
	return {
		"template_id": "lord_desert_sultan",
		"name": "沙漠苏丹",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 48,
		"attr_weights": W_LEADER,
		"base_hp": 12,
		"ac_bonus": 4,
		"move_range": 6,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["fire"],
		"immunities": ["disease"],
		"traits": ["沙漠之子：沙漠地形移动+3", "交易大师：贿赂敌方单位倒戈概率", "骑兵统帅：骑乘时攻击+2"],
		"description": "黄沙帝国的苏丹，麾下骑兵如风般席卷一切。",
	}

## 亡灵君王 (Lv.66, CR 11)
static func lord_lich_king() -> Dictionary:
	return {
		"template_id": "lord_lich_king",
		"name": "亡灵君王",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 66,
		"attr_weights": W_MAGE,
		"base_hp": 14,
		"ac_bonus": 5,
		"move_range": 5,
		"initiative_bonus": 4,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["necrotic", "cold", "magic"],
		"immunities": ["poison", "disease", "mind", "fear"],
		"traits": ["巫妖之躯：命匣不毁则不死", "灵魂容器：HP归零时命匣消耗复活", "亡灵统御：全场亡灵攻击+3"],
		"description": "跨越生死的巫妖之王，灵魂囚于命匣。",
	}

## 海盗女王 (Lv.48, CR 8)
static func lord_pirate_queen() -> Dictionary:
	return {
		"template_id": "lord_pirate_queen",
		"name": "海盗女王",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 48,
		"attr_weights": [1.0, 2.5, 1.0, 1.5, 1.0, 2.5],
		"base_hp": 12,
		"ac_bonus": 4,
		"move_range": 6,
		"initiative_bonus": 5,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"immunities": ["fear"],
		"traits": ["海风之子：水地形移动+3", "双持弯刀：攻击2次但各-2命中", "船长的威望：友方士气+20"],
		"description": "七海的霸主，率领海盗舰队劫掠海岸。",
	}

## 炎魔将军 (Lv.60, CR 10) — 深渊魔物领主
static func lord_balor() -> Dictionary:
	return {
		"template_id": "lord_balor",
		"name": "炎魔将军",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 60,
		"attr_weights": [2.5, 1.0, 2.5, 1.0, 1.0, 2.0],
		"base_hp": 18,
		"ac_bonus": 5,
		"move_range": 6,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 150, "natural_dr_threshold": 14,
		"resistances": ["fire", "cold", "magic"],
		"immunities": ["fire", "poison", "fear", "disease"],
		"traits": ["火焰领域：周围2格每回合受2d6火焰", "飞行", "魔法抗性：法术豁免+4", "恐惧光环"],
		"description": "深渊军团的至高将军，所到之处化为灰烬。",
	}

## 亡灵法师 (Lv.36, CR 6) — 亡灵召唤师
static func boss_necromancer() -> Dictionary:
	return {
		"template_id": "boss_necromancer",
		"name": "亡灵法师",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 36,
		"attr_weights": W_MAGE,
		"base_hp": 8,
		"ac_bonus": 2,
		"move_range": 5,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"resistances": ["necrotic"],
		"immunities": ["poison", "disease"],
		"traits": ["亡灵统御：全场亡灵攻击+1", "生命汲取：命中回复伤害50%HP"],
		"description": "操控亡灵的死灵法师，能不断召唤骷髅军团。",
	}

## 幽魂女妖 (Lv.30, CR 5) — 亡灵控制型
static func boss_banshee() -> Dictionary:
	return {
		"template_id": "boss_banshee",
		"name": "幽魂女妖",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 30,
		"attr_weights": [0.5, 2.0, 1.0, 1.5, 2.0, 2.5],
		"base_hp": 7,
		"ac_bonus": 3,
		"move_range": 6,
		"initiative_bonus": 5,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"resistances": ["necrotic", "physical"],
		"immunities": ["poison", "mind"],
		"traits": ["虚体：免疫非魔法物理伤害", "哀嚎：3格内DC14WIS恐惧2回合", "灵魂汲取：命中附带等级×0.5暗影伤害"],
		"description": "悲惨的亡灵女妖，其哀嚎能令活人失去战意。",
	}


# ============================================================================
# 传奇模板（等级 78~120）
# 总属性102~144，CR 13~20
# ============================================================================

## 青年红龙·焰息者 (Lv.78, CR 13)
static func legendary_young_red_dragon() -> Dictionary:
	return {
		"template_id": "legend_young_red_dragon",
		"name": "青年红龙·焰息者",
		"enemy_type": UnitData.EnemyType.DRAGON,
		"level": 78,
		"attr_weights": W_DRAGON,
		"base_hp": 18,
		"ac_bonus": 7,
		"move_range": 6,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.INTIMIDATE,
		"creature_size": UnitData.CreatureSize.HUGE,
		"natural_dr": 200, "natural_dr_threshold": 16,
		"immunities": ["fire", "fear"],
		"weaknesses": ["cold×1.5"],
		"traits": ["龙族恐惧：首次看到需WIS豁免否则恐惧", "飞行：无视地形和控制区", "烈焰之躯：近战攻击者受1d6火焰反击"],
		"legendary_resistance_uses": 1,
		"legendary_action_points": 0,
		"description": "尚在成长的年轻红龙，吐息已足以威胁整支队伍。",
	}

## 成年红龙·烬牙 (Lv.90, CR 15)
static func legendary_adult_red_dragon() -> Dictionary:
	return {
		"template_id": "legend_adult_red_dragon",
		"name": "成年红龙·烬牙",
		"enemy_type": UnitData.EnemyType.DRAGON,
		"level": 90,
		"attr_weights": W_DRAGON,
		"base_hp": 20,
		"ac_bonus": 8,
		"move_range": 6,
		"initiative_bonus": 0,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"creature_size": UnitData.CreatureSize.GARGANTUAN,
		"natural_dr": 300, "natural_dr_threshold": 20,
		"immunities": ["fire", "fear"],
		"weaknesses": ["cold×1.5"],
		"traits": ["龙族恐惧", "烈焰之躯：近战攻击者受2d6火焰反击", "飞行", "厚鳞：物理伤害-5", "龙族魔法"],
		"legendary_resistance_uses": 2,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "检测", "cost": 1, "desc": "察觉一个隐藏或隐身的单位"},
			{"name": "尾扫", "cost": 1, "desc": "身后3格扇形，DC22强韧倒地"},
			{"name": "翼击", "cost": 2, "desc": "前后左右4格内中型以下推后4格+倒地"},
			{"name": "喷火", "cost": 2, "desc": "锥形3格8d6火焰"},
		],
		"lair_actions": [
			{"name": "熔岩涌动", "desc": "随机3格喷出熔岩，站上去3d6火焰+着火"},
			{"name": "火山灰", "desc": "全场非火焰免疫单位视线-4格"},
			{"name": "热浪", "desc": "全场敌方DC18强韧否则力竭1回合"},
		],
		"description": "霜冠山脉火口中的成年红龙，空中施法消耗，落地吐息清场。",
	}

## 远古霜龙·冰葬 (Lv.102, CR 17)
static func legendary_ancient_frost_dragon() -> Dictionary:
	return {
		"template_id": "legend_ancient_frost_dragon",
		"name": "远古霜龙·冰葬",
		"enemy_type": UnitData.EnemyType.DRAGON,
		"level": 102,
		"attr_weights": W_DRAGON,
		"base_hp": 22,
		"ac_bonus": 9,
		"move_range": 6,
		"initiative_bonus": 0,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"creature_size": UnitData.CreatureSize.GARGANTUAN,
		"natural_dr": 400, "natural_dr_threshold": 22,
		"immunities": ["cold", "fear", "poison"],
		"weaknesses": ["fire×1.5"],
		"traits": ["龙族恐惧", "冰霜之躯：近战攻击者受2d6冰霜+减速", "厚鳞：物理伤害-5", "飞行",
			"远古智慧：法术豁免+3", "冰封领域：周围2格每回合受1d6冰霜", "冰霜再生：每回合恢复30HP",
			"不死寒冬：HP归零时化为冰棺3回合后复活"],
		"legendary_resistance_uses": 3,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "检测", "cost": 1, "desc": "全场真实视觉"},
			{"name": "冰霜之翼", "cost": 1, "desc": "4格DC23强韧倒地+2d8冰霜"},
			{"name": "寒冰囚笼", "cost": 2, "desc": "射程6格，DC23强韧冰冻2回合"},
			{"name": "冰晶风暴", "cost": 3, "desc": "半径3格8d6冰霜+DC23 DEX半伤"},
		],
		"lair_actions": [
			{"name": "冰刺爆发", "desc": "随机4格冰刺，4d6穿刺+缚足"},
			{"name": "白雾弥漫", "desc": "全场视野减半2回合，远程射程-4"},
			{"name": "冰桥崩塌", "desc": "一段地面崩塌为深坑，坠落6d6+倒地"},
		],
		"description": "冰川深处的远古霜龙，不死寒冬令其HP归零时化为冰棺3回合后复活。",
	}

## 远古巫妖·无名者 (Lv.96, CR 16)
static func legendary_lich() -> Dictionary:
	return {
		"template_id": "legend_lich",
		"name": "远古巫妖·无名者",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 96,
		"attr_weights": W_MAGE,
		"base_hp": 14,
		"ac_bonus": 6,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"resistances": ["piercing", "slashing", "nonmagical_physical"],
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death", "cold", "necrotic"],
		"weaknesses": ["radiant×2", "holy×2"],
		"traits": ["命匣：命匣不毁则不死", "死亡光环：4格内敌方每回合受1d6暗影", "亡灵统帅：全场亡灵攻击+3",
			"不死之身：HP归零时命匣消耗满血复活", "魔法大师：法术豁免+5", "动作如潮：每战斗1次额外行动"],
		"legendary_resistance_uses": 3,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "暗影飞弹", "cost": 1, "desc": "3发暗影飞弹，每发1d8+5自动命中"},
			{"name": "空间裂隙", "cost": 1, "desc": "传送至视野内任意位置"},
			{"name": "亡者唤醒", "cost": 2, "desc": "复活1个尸体为骷髅战士"},
			{"name": "灵魂虹吸", "cost": 2, "desc": "射程4格4d6暗影+治疗自身"},
		],
		"lair_actions": [
			{"name": "亡者苏醒", "desc": "1d4具尸体复活为骷髅战士"},
			{"name": "暗影涌动", "desc": "全场非暗影免疫DC20强韧恐惧1回合"},
			{"name": "诅咒之地", "desc": "随机2格变为诅咒地面"},
			{"name": "石棺封闭", "desc": "一个出口被石棺封堵2回合"},
		],
		"description": "远古巫妖，命匣隐藏在地图某处。不找命匣就是无限消耗战。",
	}

## 死亡骑士王 (Lv.90, CR 15)
static func legendary_death_knight_king() -> Dictionary:
	return {
		"template_id": "legend_death_knight_king",
		"name": "死亡骑士王",
		"enemy_type": UnitData.EnemyType.UNDEAD,
		"level": 90,
		"attr_weights": W_MELEE_BRUISER,
		"base_hp": 18,
		"ac_bonus": 8,
		"move_range": 6,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 250, "natural_dr_threshold": 18,
		"resistances": ["piercing", "slashing", "fire"],
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death", "cold"],
		"weaknesses": ["radiant×1.5", "holy×1.5"],
		"traits": ["死亡冲锋：冲锋伤害+4d6", "不死军势：击杀后召唤1个骷髅战士",
			"冷铁护甲：AC额外+3", "恐惧光环：4格内敌方恐惧", "反击大师：被近战攻击时反击", "幽灵战马：骑乘时移动+4"],
		"legendary_resistance_uses": 3,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "军令", "cost": 1, "desc": "1个友方亡灵立即移动4格+攻击"},
			{"name": "暗影斩", "cost": 1, "desc": "亡灵之剑攻击+3d6暗影"},
			{"name": "亡者号令", "cost": 2, "desc": "视野内所有友方亡灵攻击+2"},
			{"name": "死亡旋风", "cost": 3, "desc": "半径2格2d8+6挥砍+2d6暗影"},
		],
		"description": "亡灵军团的至高骑士王，指挥亡灵军团冲锋。",
	}

## 熔岩领主·灼心 (Lv.90, CR 15)
static func legendary_lava_lord() -> Dictionary:
	return {
		"template_id": "legend_lava_lord",
		"name": "熔岩领主·灼心",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 90,
		"attr_weights": [2.5, 1.0, 2.5, 1.0, 1.0, 1.5],
		"base_hp": 20,
		"ac_bonus": 7,
		"move_range": 5,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.INTIMIDATE,
		"creature_size": UnitData.CreatureSize.HUGE,
		"natural_dr": 280, "natural_dr_threshold": 18,
		"immunities": ["fire", "poison", "mind", "fear", "instant_death"],
		"weaknesses": ["cold×2"],
		"traits": ["熔岩之躯：近战攻击者受3d6火焰", "热力场：周围2格每回合受2d6火焰",
			"岩浆再生：熔岩地形上每回合恢复50HP", "火山意志：火焰法术伤害×2",
			"元素裂解：被冰霜攻击后AC-3持续2回合"],
		"legendary_resistance_uses": 2,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "灼热凝视", "cost": 1, "desc": "射程8格，DC21强韧着火2回合"},
			{"name": "岩浆涌出", "cost": 1, "desc": "周围1格变为熔岩地形"},
			{"name": "火焰鞭笞", "cost": 2, "desc": "射程4格拖拽至相邻+3d6火焰+缚足"},
			{"name": "火山之怒", "cost": 3, "desc": "半径2格8d6火焰+地面变熔岩"},
		],
		"lair_actions": [
			{"name": "熔岩喷涌", "desc": "随机4格喷出熔岩，4d6火焰+着火"},
			{"name": "火山震颤", "desc": "随机2格塌陷为熔岩池"},
			{"name": "烈焰风暴", "desc": "全场非火焰免疫受3d6火焰"},
		],
		"description": "火山核心的元素领主，改造地形为熔岩海。",
	}

## 深渊领主·谎言之王 (Lv.108, CR 18)
static func legendary_abyssal_lord() -> Dictionary:
	return {
		"template_id": "legend_abyssal_lord",
		"name": "深渊领主·谎言之王",
		"enemy_type": UnitData.EnemyType.DEMON,
		"level": 108,
		"attr_weights": [2.0, 1.5, 2.0, 2.0, 1.5, 2.5],
		"base_hp": 18,
		"ac_bonus": 8,
		"move_range": 6,
		"initiative_bonus": 4,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"creature_size": UnitData.CreatureSize.LARGE,
		"natural_dr": 300, "natural_dr_threshold": 20,
		"resistances": ["cold", "lightning", "necrotic", "nonmagical_physical"],
		"immunities": ["fire", "poison", "fear"],
		"weaknesses": ["radiant×1.5", "holy_spell_dc_plus_3"],
		"traits": ["魔法抗性：法术豁免+4", "深渊再生：每回合恢复40HP",
			"传送：传送至视野内任意位置", "混乱光环：5格内敌方攻击-2",
			"恶魔契约：击杀敌方时召唤1个小恶魔", "谎言之盾：50%闪避非魔法远程",
			"不死之身：命匣碎裂后满血复活1次"],
		"legendary_resistance_uses": 3,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "暗影之语", "cost": 1, "desc": "射程6格DC23 WIS混乱1回合"},
			{"name": "空间裂隙", "cost": 1, "desc": "传送至视野内任意位置"},
			{"name": "深渊之手", "cost": 2, "desc": "射程8格拖拽至相邻+2d6暗影+缚足"},
			{"name": "虚伪之镜", "cost": 2, "desc": "创造自身幻影(HP50)，被击破爆发6d6暗影"},
			{"name": "谎言崩塌", "cost": 3, "desc": "半径3格12d6暗影+DC23 WIS魅惑2回合"},
		],
		"lair_actions": [
			{"name": "深渊凝视", "desc": "随机1个敌方DC23 WIS魅惑1回合"},
			{"name": "裂隙拉扯", "desc": "靠近边缘敌方被拉向中心2格"},
			{"name": "低语诱惑", "desc": "全场敌方DC21 WIS攻击-2"},
			{"name": "恶魔增援", "desc": "召唤2个小恶魔(Lv.30)"},
		],
		"description": "谎言之王，支配凝视控制最强输出打队友。",
	}

## 觉醒远古机兵·守望者 (Lv.108, CR 18)
static func legendary_awakened_sentinel() -> Dictionary:
	return {
		"template_id": "legend_awakened_sentinel",
		"name": "觉醒远古机兵·守望者",
		"enemy_type": UnitData.EnemyType.CONSTRUCT,
		"level": 108,
		"attr_weights": W_CONSTRUCT,
		"base_hp": 22,
		"ac_bonus": 10,
		"move_range": 4,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.TERRITORIAL,
		"creature_size": UnitData.CreatureSize.GARGANTUAN,
		"natural_dr": 400, "natural_dr_threshold": 22,
		"resistances": ["physical"],
		"immunities": ["poison", "mind", "fear", "fatigue", "instant_death",
			"fire", "cold", "necrotic", "lightning", "radiant", "charm", "confusion"],
		"traits": ["远古魔法免疫：5环以下法术完全免疫", "不动如山：免疫一切控制",
			"减伤外壳：物理伤害-8", "自我修复：每回合恢复40HP",
			"能量护盾：额外100HP吸收层", "远古守卫：HP<30%时伤害×3"],
		"legendary_resistance_uses": 3,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "检测", "cost": 1, "desc": "全场震动感知，揭示所有隐藏单位"},
			{"name": "重击", "cost": 1, "desc": "铁拳攻击1次，伤害翻倍"},
			{"name": "护盾刷新", "cost": 2, "desc": "恢复能量护盾(30HP吸收)"},
			{"name": "歼灭光束·散射", "cost": 3, "desc": "3道光束各射程6格12d6力场"},
		],
		"lair_actions": [
			{"name": "防御矩阵", "desc": "随机2道墙壁升起/降下"},
			{"name": "能量脉冲", "desc": "全场敌方DC22强韧眩晕1回合"},
			{"name": "机械修复", "desc": "守望者恢复30HP"},
			{"name": "陷阱激活", "desc": "随机3格远古陷阱6d6力场+缚足"},
		],
		"phases": [
			{"name": "第一形态", "hp_threshold": 0.30, "desc": "缓慢碾压+能量炮+能量爆发"},
			{"name": "第二形态·核心暴走", "hp_threshold": 0.0,
				"desc": "速度8格，AC降至18，攻击+2命中但自伤2d6，歼灭光束冷却变2回合，蓄力过载自爆"},
		],
		"description": "远古文明最强守卫，HP<30%时核心暴露伤害×3。",
	}

## 陨星之龙·末日降临 (Lv.120, CR 20) — 终极Boss
static func legendary_meteor_dragon() -> Dictionary:
	return {
		"template_id": "legend_meteor_dragon",
		"name": "陨星之龙·末日降临",
		"enemy_type": UnitData.EnemyType.DRAGON,
		"level": 120,
		"attr_weights": [2.0, 1.5, 2.0, 1.5, 1.5, 2.0],
		"base_hp": 24,
		"ac_bonus": 10,
		"move_range": 6,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.CUNNING,
		"creature_size": UnitData.CreatureSize.GARGANTUAN,
		"natural_dr": 500, "natural_dr_threshold": 24,
		"resistances": ["nonmagical_physical"],
		"immunities": ["fire", "cold", "lightning", "poison", "fear", "instant_death"],
		"traits": ["龙族恐惧", "厚鳞：物理伤害-8", "陨星之躯：每回合全场受1d6力场震动",
			"飞行", "远古魔法：法术豁免+5", "陨星共鸣：被攻击时10%概率陨石坠落"],
		"legendary_resistance_uses": 3,
		"legendary_action_points": 3,
		"legendary_actions": [
			{"name": "检测", "cost": 1, "desc": "全场真实视觉"},
			{"name": "尾扫", "cost": 1, "desc": "身后4格扇形DC24强韧倒地+3d10+10钝击"},
			{"name": "翼击风暴", "cost": 2, "desc": "全方向4格DC24推后6格+倒地+3d8+10"},
			{"name": "迷你吐息", "cost": 2, "desc": "锥形4格12d6火焰DC24 DEX半伤"},
			{"name": "星陨碎片", "cost": 2, "desc": "3个目标各受8d6力场DC24 DEX半伤"},
			{"name": "龙族威压", "cost": 3, "desc": "半径6格DC24 WIS恐惧2回合+麻痹1回合"},
		],
		"lair_actions": [
			{"name": "陨石雨", "desc": "随机6格坠落陨石6d6火焰+力场+变深坑"},
			{"name": "地震", "desc": "全场DC22强韧倒地"},
			{"name": "放射", "desc": "全场敌方2d6力场辐射无豁免"},
			{"name": "重力扭曲", "desc": "全场敌方速度-2，飞行高度-1"},
		],
		"phases": [
			{"name": "第一阶段·陨星之翼", "hp_threshold": 0.50,
				"desc": "飞行状态，空中施法+吐息，额外传奇飞行移动"},
			{"name": "第二阶段·坠落之龙", "hp_threshold": 0.20,
				"desc": "落地AC+2攻击+2，每回合全场1d6力场震动波，攻击+3d6火焰"},
			{"name": "第三阶段·超新星", "hp_threshold": 0.0,
				"desc": "每回合自伤3d6，星陨冷却2回合，蓄力超新星(3回合全场40d6)"},
		],
		"unique_drop_id": "drop_meteor_dragon_crystal",
		"description": "阿瓦隆尼亚终极挑战。三阶段Boss：空中轰炸→地面暴怒→超新星倒计时。",
	}


# ============================================================================
# 冒险者模板（等级 6~30）
# ============================================================================

## 新手佣兵 (Lv.6, CR 1)
static func adventurer_novice_mercenary() -> Dictionary:
	return {
		"template_id": "adv_novice_merc",
		"name": "新手佣兵",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 6,
		"attr_weights": W_MELEE_BRUISER,
		"base_hp": 8,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 0,
		"ai_strategy": UnitData.AIStrategy.RECKLESS,
		"description": "刚入行的佣兵，装备简陋但干劲十足。",
	}

## 老练猎人 (Lv.18, CR 3)
static func adventurer_veteran_hunter() -> Dictionary:
	return {
		"template_id": "adv_veteran_hunter",
		"name": "老练猎人",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 18,
		"attr_weights": W_RANGED_AGILITY,
		"base_hp": 8,
		"ac_bonus": 2,
		"move_range": 5,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"traits": ["追踪者：揭示3格内隐身单位", "陷阱专家：设置1个陷阱/战斗"],
		"description": "经验丰富的猎人，擅长远程攻击和陷阱。",
	}

## 战斗法师 (Lv.24, CR 4)
static func adventurer_battle_mage() -> Dictionary:
	return {
		"template_id": "adv_battle_mage",
		"name": "战斗法师",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 24,
		"attr_weights": W_MAGE,
		"base_hp": 7,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 2,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"resistances": ["magic"],
		"traits": ["法术亲和：施法DC+2", "魔力涌动：每回合恢复1点魔力"],
		"description": "受过军事训练的法师，能在战场上释放毁灭性法术。",
	}

## 精英圣骑士 (Lv.30, CR 5)
static func adventurer_elite_paladin() -> Dictionary:
	return {
		"template_id": "adv_elite_paladin",
		"name": "精英圣骑士",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 30,
		"attr_weights": W_LEADER,
		"base_hp": 12,
		"ac_bonus": 5,
		"move_range": 5,
		"initiative_bonus": 0,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["necrotic"],
		"immunities": ["fear", "disease"],
		"traits": ["神圣誓言：对亡灵伤害+2d8", "光之庇护：被攻击时5%完全闪避"],
		"description": "虔诚的圣骑士，以神圣之力守护同伴。",
	}

## 战鼓萨满 (Lv.18, CR 3)
static func adventurer_war_drum_shaman() -> Dictionary:
	return {
		"template_id": "adv_war_drum_shaman",
		"name": "战鼓萨满",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 18,
		"attr_weights": [1.0, 1.0, 1.5, 1.5, 2.5, 1.0],
		"base_hp": 10,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 1,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["magic"],
		"immunities": ["fear"],
		"traits": ["先祖之灵：每回合恢复2HP给最低HP友方", "自然链接：自然地形移动+2"],
		"description": "部落的灵魂导师，敲响战鼓激励同伴。",
	}

## 重装佣兵 (Lv.24, CR 4)
static func adventurer_heavy_mercenary() -> Dictionary:
	return {
		"template_id": "adv_heavy_mercenary",
		"name": "重装佣兵",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 24,
		"attr_weights": W_TANK,
		"base_hp": 14,
		"ac_bonus": 6,
		"move_range": 4,
		"initiative_bonus": -2,
		"ai_strategy": UnitData.AIStrategy.TACTICAL,
		"resistances": ["physical"],
		"traits": ["铁壁：防御时AC+4", "不屈：首次致死伤害时1HP存活"],
		"description": "身穿重甲的防御型佣兵，如同一座不可动摇的堡垒。",
	}

## 吟游诗人 (Lv.18, CR 3)
static func adventurer_bard() -> Dictionary:
	return {
		"template_id": "adv_bard",
		"name": "吟游诗人",
		"enemy_type": UnitData.EnemyType.HUMANOID,
		"level": 18,
		"attr_weights": W_CUNNING,
		"base_hp": 7,
		"ac_bonus": 1,
		"move_range": 5,
		"initiative_bonus": 3,
		"ai_strategy": UnitData.AIStrategy.CAUTIOUS,
		"immunities": ["charm"],
		"traits": ["音乐之力：友方攻击+2持续2回合", "鼓舞人心：友方士气+10"],
		"description": "用音乐改变战场的吟游诗人。",
	}


# ============================================================================
# 模板查询接口
# ============================================================================

## 获取所有模板
static func get_all_templates() -> Array[Dictionary]:
	var all: Array[Dictionary] = []
	all.append_array(get_grunt_templates())
	all.append_array(get_standard_templates())
	all.append_array(get_elite_templates())
	all.append_array(get_construct_templates())
	all.append_array(get_boss_templates())
	all.append_array(get_lord_templates())
	all.append_array(get_adventurer_templates())
	all.append_array(get_legendary_templates())
	return all

## 获取所有杂兵模板（Lv 1~5）
static func get_grunt_templates() -> Array[Dictionary]:
	return [
		grunt_goblin_warrior(), grunt_goblin_archer(),
		grunt_skeleton_warrior(), grunt_forest_wolf(),
		grunt_zombie(), grunt_slime(), grunt_imp(),
		grunt_lava_slime(),
	]

## 获取所有熟练模板（Lv 6~17）
static func get_standard_templates() -> Array[Dictionary]:
	return [
		standard_goblin_chieftain(), standard_orc_berserker(),
		standard_giant_spider(), standard_ghoul(),
		standard_black_bear(), standard_giant_scorpion(),
		standard_dire_wolf(), standard_harpy(),
		standard_hellhound(), standard_skeleton_archer(),
		standard_griffin(), standard_troll(),
	]

## 获取所有精英模板（Lv 18~36）
static func get_elite_templates() -> Array[Dictionary]:
	return [
		elite_ogre(), elite_minotaur(), elite_gargoyle(),
		elite_corrupted_treant(), elite_lamia(),
		elite_shadow_assassin(), elite_death_knight(),
		elite_fire_elemental(), elite_ice_elemental(),
		elite_demon_guard(), elite_frost_witch(),
		elite_shadow_inquisitor(), elite_nightmare_beast(),
		elite_minotaur_chieftain(),
	]

## 获取所有构造体模板
static func get_construct_templates() -> Array[Dictionary]:
	return [
		construct_wooden_sentinel(), construct_stone_golem(),
	]

## 获取所有首领模板（Lv 42~66）
static func get_boss_templates() -> Array[Dictionary]:
	return [
		boss_iron_golem(), boss_ogre_chief(),
		boss_death_general(), boss_hill_giant(),
		boss_abyssal_demon(), boss_ancient_stone_golem(),
		boss_necromancer(), boss_banshee(),
	]

## 获取所有领主模板（Lv 48~72）
static func get_lord_templates() -> Array[Dictionary]:
	return [
		lord_knight_commander(), lord_shadow_dominus(),
		lord_barbarian_chieftain(), lord_elf_ranger_general(),
		lord_dwarf_fortress_king(), lord_desert_sultan(),
		lord_lich_king(), lord_pirate_queen(),
		lord_balor(),
	]

## 获取所有怪物模板（非领主/非冒险者/非传奇）
static func get_monster_templates() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	result.append_array(get_grunt_templates())
	result.append_array(get_standard_templates())
	result.append_array(get_elite_templates())
	result.append_array(get_construct_templates())
	result.append_array(get_boss_templates())
	return result

## 获取所有冒险者模板（Lv 6~30）
static func get_adventurer_templates() -> Array[Dictionary]:
	return [
		adventurer_novice_mercenary(), adventurer_veteran_hunter(),
		adventurer_battle_mage(), adventurer_elite_paladin(),
		adventurer_war_drum_shaman(), adventurer_heavy_mercenary(),
		adventurer_bard(),
	]

## 获取所有传奇生物模板（Lv 78~120）
static func get_legendary_templates() -> Array[Dictionary]:
	return [
		legendary_young_red_dragon(), legendary_adult_red_dragon(),
		legendary_ancient_frost_dragon(), legendary_lich(),
		legendary_death_knight_king(), legendary_lava_lord(),
		legendary_abyssal_lord(), legendary_awakened_sentinel(),
		legendary_meteor_dragon(),
	]

## 获取所有具有传奇行动的模板
static func get_legendary_action_templates() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for tpl in get_legendary_templates():
		if tpl.get("legendary_action_points", 0) > 0:
			result.append(tpl)
	return result

## 获取所有具有多阶段的模板
static func get_multi_phase_templates() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for tpl in get_all_templates():
		if tpl.get("phases", []).size() > 0:
			result.append(tpl)
	return result

## 按等级范围筛选模板
static func get_templates_by_level(min_level: int, max_level: int) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for tpl in get_all_templates():
		if tpl["level"] >= min_level and tpl["level"] <= max_level:
			result.append(tpl)
	return result

## 按CR范围筛选模板
static func get_templates_by_cr(min_cr: float, max_cr: float) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for tpl in get_all_templates():
		var cr = calculate_cr_from_template(tpl)
		if cr >= min_cr and cr <= max_cr:
			result.append(tpl)
	return result

## 按敌人类型筛选模板
static func get_templates_by_type(enemy_type: int) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for tpl in get_all_templates():
		if tpl["enemy_type"] == enemy_type:
			result.append(tpl)
	return result

## 按模板ID查找
static func get_template_by_id(template_id: String) -> Dictionary:
	for tpl in get_all_templates():
		if tpl["template_id"] == template_id:
			return tpl
	return {}
