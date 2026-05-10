# RaceData.gd
# 种族数据资源 — 5个可玩种族的属性修正、种族特性、初始好感
# 对应策划案 12-种族与招募.md + 05-角色与职业.md
extends Resource
class_name RaceData

# ============================================================================
# 种族枚举
# ============================================================================

enum Race {
	HUMAN,
	ELF,
	DWARF,
	HALF_ORC,
	HALF_ELF,
}

# ============================================================================
# 数据字段
# ============================================================================

## 种族ID
@export var race_id: Race = Race.HUMAN

## 种族名称
@export var race_name: String = "人类"

## 属性修正
@export var str_mod: int = 0
@export var dex_mod: int = 0
@export var con_mod: int = 0
@export var int_mod: int = 0
@export var wis_mod: int = 0
@export var cha_mod: int = 0

## 种族特性列表
@export var racial_traits: Array[String] = []

## 种族特性描述
@export_multiline var traits_description: String = ""

## 招募难度系数（1.0=标准，越高越难）
@export var recruitment_difficulty: float = 1.0

## 初始好感度表（种族ID → 好感值）
## 键: "human"/"elf"/"dwarf"/"half_orc"/"half_elf"
@export var starting_favor: Dictionary = {}

## 适合的职业倾向
@export var suitable_tendencies: Array[String] = []

# ============================================================================
# 静态工厂：返回5个硬编码种族
# ============================================================================

static func get_all_races() -> Array[RaceData]:
	var races: Array[RaceData] = []
	races.append(_create_human())
	races.append(_create_elf())
	races.append(_create_dwarf())
	races.append(_create_half_orc())
	races.append(_create_half_elf())
	return races

static func get_race_by_id(id: Race) -> RaceData:
	var all = get_all_races()
	for r in all:
		if r.race_id == id:
			return r
	return all[0]

static func get_race_name(id: Race) -> String:
	match id:
		Race.HUMAN: return "人类"
		Race.ELF: return "精灵"
		Race.DWARF: return "矮人"
		Race.HALF_ORC: return "半兽人"
		Race.HALF_ELF: return "半精灵"
		_: return "未知"

# ============================================================================
# 各种族定义
# ============================================================================

static func _create_human() -> RaceData:
	var r = RaceData.new()
	r.race_id = Race.HUMAN
	r.race_name = "人类"
	r.str_mod = 1; r.dex_mod = 1; r.con_mod = 1; r.int_mod = 1; r.wis_mod = 1; r.cha_mod = 1
	r.racial_traits.assign(["versatile"])  # 多才多艺：额外获得1个技能点
	r.traits_description = "多才多艺：额外获得1个技能点。全属性+1。"
	r.recruitment_difficulty = 0.5
	r.starting_favor = {"human": 20, "elf": 5, "dwarf": 10, "half_orc": -5, "half_elf": 5}
	r.suitable_tendencies.assign(["全能"])
	return r

static func _create_elf() -> RaceData:
	var r = RaceData.new()
	r.race_id = Race.ELF
	r.race_name = "精灵"
	r.dex_mod = 2; r.int_mod = 1; r.con_mod = -1
	r.racial_traits.assign(["dark_vision", "elf_weapon_proficiency"])
	r.traits_description = "黑暗视觉：夜间/洞穴无惩罚。精灵武器熟练：长剑/长弓+1命中。DEX+2, INT+1, CON-1。"
	r.recruitment_difficulty = 1.0
	r.starting_favor = {"human": 5, "elf": 25, "dwarf": 0, "half_orc": -15, "half_elf": 15}
	r.suitable_tendencies.assign(["法师", "游侠", "游荡者"])
	return r

static func _create_dwarf() -> RaceData:
	var r = RaceData.new()
	r.race_id = Race.DWARF
	r.race_name = "矮人"
	r.con_mod = 2; r.str_mod = 1; r.dex_mod = -1
	r.racial_traits.assign(["poison_resistance", "dwarven_resilience"])
	r.traits_description = "毒素抗性：强韧豁免优势。矮人韧性：HP+1/级。CON+2, STR+1, DEX-1。"
	r.recruitment_difficulty = 1.0
	r.starting_favor = {"human": 10, "elf": 0, "dwarf": 25, "half_orc": -20, "half_elf": 5}
	r.suitable_tendencies.assign(["战士", "圣骑士", "牧师"])
	return r

static func _create_half_orc() -> RaceData:
	var r = RaceData.new()
	r.race_id = Race.HALF_ORC
	r.race_name = "半兽人"
	r.str_mod = 2; r.con_mod = 1; r.int_mod = -2; r.cha_mod = -1
	r.racial_traits.assign(["rage", "threat_instinct"])
	r.traits_description = "狂暴：HP低于50%时伤害+2。威胁直觉：先攻+2。STR+2, CON+1, INT-2, CHA-1。"
	r.recruitment_difficulty = 2.0
	r.starting_favor = {"human": -10, "elf": -20, "dwarf": -15, "half_orc": 20, "half_elf": -5}
	r.suitable_tendencies.assign(["战士", "野蛮人", "游侠"])
	return r

static func _create_half_elf() -> RaceData:
	var r = RaceData.new()
	r.race_id = Race.HALF_ELF
	r.race_name = "半精灵"
	r.cha_mod = 2
	r.racial_traits.assign(["dual_heritage", "social_talent"])
	r.traits_description = "双重血统：人类和精灵聚居地都视为友好。社交天赋：交涉/招募价格-10%。CHA+2，自选2项+1。"
	r.recruitment_difficulty = 0.8
	r.starting_favor = {"human": 10, "elf": 15, "dwarf": 5, "half_orc": -5, "half_elf": 25}
	r.suitable_tendencies.assign(["圣骑士", "牧师", "游荡者", "法师"])
	return r
