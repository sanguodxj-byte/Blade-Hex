# OverworldPOI.gd
# 大地图兴趣点(POI)数据模型 —— 所有地图上可交互的固定位置实体
# 城镇、村庄、城堡、外族聚落、龙巢、墓穴/遗迹均基于此
extends Resource
class_name OverworldPOI

## POI类型枚举
enum POIType {
	TOWN,        ## 城镇：完整设施（酒馆/商店/铁匠/任务板）
	VILLAGE,     ## 村庄：简易设施，委托来源，可被袭击
	CASTLE,      ## 城堡：防御设施，兵营，可攻占
	SETTLEMENT,  ## 外族聚落：哥布林营地/狗头人矿坑/牛头人石堡
	LAIR,        ## 巢穴：龙巢/远古遗迹/墓穴
}

## 外族聚落子类型
enum SettlementRace {
	GOBLIN,      ## 哥布林营地
	KOBOLD,      ## 狗头人矿坑
	MINOTAUR,    ## 牛头人石堡
	SHADOW_CULT, ## 暗影教团据点
}

## 巢穴子类型
enum LairType {
	DRAGON_LAIR,    ## 龙巢
	ANCIENT_TOMB,   ## 古代墓穴
	RUINS,          ## 远古遗迹
	GOLEM_FORGE,    ## 魔像工坊
}

## 基础字段
@export var poi_name: String = "未命名地点"
@export var poi_type: POIType = POIType.VILLAGE
@export var position: Vector2 = Vector2.ZERO  ## 大地图像素坐标
@export var owning_faction: String = "neutral"  ## 所属势力
@export var prosperity: int = 50  ## 繁荣度 0-100

## 外族聚落专属
@export var settlement_race: SettlementRace = SettlementRace.GOBLIN
@export var threat_level: float = 0.5  ## 聚落威胁等级（影响掠夺队强度）
@export var raid_interval_days: int = 7  ## 每隔多少天产生一队掠夺队
@export var max_raiding_parties: int = 2  ## 同时存在的最大掠夺队数

## 巢穴专属
@export var lair_type: LairType = LairType.ANCIENT_TOMB
@export var lair_level: int = 1  ## 巢穴等级（1-5），影响怪物强度和掉落
@export var is_cleared: bool = false  ## 是否已被清除

## 城镇/村庄/城堡
@export var has_tavern: bool = false
@export var has_shop: bool = false
@export var has_blacksmith: bool = false
@export var has_quest_board: bool = true
@export var has_barracks: bool = false

## 城堡防御
@export var castle_defense_level: int = 1  ## 1=木栅, 2=石堡, 3=要塞
@export var garrison_max: int = 50
@export var garrison_current: int = 20

## 运行时
var days_since_last_raid: int = 0  ## 距上次产生掠夺队的天数
var active_raiding_parties: int = 0  ## 当前活跃的掠夺队数量

## 围攻状态
var is_under_siege: bool = false         ## 是否正在被围攻
var siege_by: OverworldEntity = null     ## 围攻者
var siege_days: int = 0                  ## 已被围攻天数
var last_attacked_by: OverworldEntity = null  ## 最近攻击者
var last_attacked_day: int = 0           ## 最近被攻击的日期

## 领主性格（仅用于城堡/城镇，影响领主军队的AI决策）
enum LordPersonality {
	CAUTIOUS,   ## 谨慎：巡逻范围小，遇强敌撤退
	BALANCED,   ## 均衡：正常巡逻和战斗
	AGGRESSIVE, ## 激进：扩张巡逻，少撤退
}
@export var lord_personality: LordPersonality = LordPersonality.BALANCED

## 获取POI类型显示名
func get_type_name() -> String:
	match poi_type:
		POIType.TOWN: return "城镇"
		POIType.VILLAGE: return "村庄"
		POIType.CASTLE: return "城堡"
		POIType.SETTLEMENT: return get_settlement_race_name()
		POIType.LAIR: return get_lair_type_name()
		_: return "未知"

## 获取外族种族显示名
func get_settlement_race_name() -> String:
	match settlement_race:
		SettlementRace.GOBLIN: return "哥布林营地"
		SettlementRace.KOBOLD: return "狗头人矿坑"
		SettlementRace.MINOTAUR: return "牛头人石堡"
		SettlementRace.SHADOW_CULT: return "暗影教团据点"
		_: return "外族聚落"

## 获取巢穴类型显示名
func get_lair_type_name() -> String:
	match lair_type:
		LairType.DRAGON_LAIR: return "龙巢"
		LairType.ANCIENT_TOMB: return "古代墓穴"
		LairType.RUINS: return "远古遗迹"
		LairType.GOLEM_FORGE: return "魔像工坊"
		_: return "未知巢穴"

## 获取POI对应的战场模板名
func get_battle_template_name() -> String:
	match poi_type:
		POIType.SETTLEMENT:
			match settlement_race:
				SettlementRace.GOBLIN: return "goblin_camp"
				SettlementRace.KOBOLD: return "kobold_mine"
				SettlementRace.MINOTAUR: return "minotaur_fortress"
				SettlementRace.SHADOW_CULT: return "shadow_cult_hideout"
				_: return "plain_field"
		POIType.LAIR:
			match lair_type:
				LairType.DRAGON_LAIR: return "dragon_lair"
				LairType.ANCIENT_TOMB: return "ancient_tomb"
				LairType.RUINS: return "ruins_exploration"
				LairType.GOLEM_FORGE: return "golem_forge"
				_: return "plain_field"
		POIType.VILLAGE:
			return "village_defense"
		_:
			return "plain_field"

## 获取遭遇的敌方配置（根据POI类型和等级）
func get_encounter_config() -> Dictionary:
	var config: Dictionary = {"enemies": [], "cr_total": 0.0}
	
	match poi_type:
		POIType.SETTLEMENT:
			match settlement_race:
				SettlementRace.GOBLIN:
					config.enemies = ["goblin_warrior", "goblin_archer", "goblin_chieftain"]
					config.cr_total = 2.0 + threat_level * 2.0
				SettlementRace.KOBOLD:
					config.enemies = ["kobold_trapper", "kobold_sorcerer"]
					config.cr_total = 3.0 + threat_level * 2.0
				SettlementRace.MINOTAUR:
					config.enemies = ["minotaur_warrior"]
					config.cr_total = 5.0 + threat_level * 3.0
				SettlementRace.SHADOW_CULT:
					config.enemies = ["cultist", "shadow_acolyte"]
					config.cr_total = 4.0 + threat_level * 3.0
		POIType.LAIR:
			match lair_type:
				LairType.DRAGON_LAIR:
					config.enemies = ["dragon"]
					config.cr_total = 10.0 * lair_level
				LairType.ANCIENT_TOMB:
					config.enemies = ["skeleton_warrior", "zombie", "wraith"]
					config.cr_total = 3.0 * lair_level
				LairType.RUINS:
					config.enemies = ["stone_golem", "iron_golem"]
					config.cr_total = 4.0 * lair_level
				LairType.GOLEM_FORGE:
					config.enemies = ["fire_golem", "iron_golem"]
					config.cr_total = 5.0 * lair_level
	return config

## 检查聚落是否应该产生掠夺队
func should_spawn_raid_party() -> bool:
	if poi_type != POIType.SETTLEMENT:
		return false
	if active_raiding_parties >= max_raiding_parties:
		return false
	if days_since_last_raid < raid_interval_days:
		return false
	if is_under_siege:
		return false  # 被围攻时不产生掠夺队
	return true

## 评估POI的防御力量
func get_defense_power() -> float:
	var power := 0.0
	match poi_type:
		POIType.TOWN:
			power = 10.0 + prosperity * 0.3
		POIType.VILLAGE:
			power = 3.0 + prosperity * 0.1
		POIType.CASTLE:
			power = float(garrison_current) * 1.5
			match castle_defense_level:
				1: power += 15.0  # 木栅
				2: power += 35.0  # 石堡
				3: power += 60.0  # 要塞
		POIType.SETTLEMENT:
			power = threat_level * 15.0 + 5.0
		POIType.LAIR:
			power = lair_level * 10.0
	return power

## 开始被围攻
func begin_siege(attacker: OverworldEntity):
	is_under_siege = true
	siege_by = attacker
	siege_days = 0

## 结束围攻
func end_siege():
	is_under_siege = false
	siege_by = null
	siege_days = 0

## 被攻击（袭击/掠夺）
func on_attacked(attacker: OverworldEntity, current_day: int):
	last_attacked_by = attacker
	last_attacked_day = current_day

## 检查是否有领主军队应该回援（由EntityManager调用）
func needs_reinforcement() -> bool:
	if is_under_siege:
		return true
	if last_attacked_day > 0:
		return true  # 曾被攻击，可能需要驻防
	return prosperity < 30  # 繁荣度过低

## 标记已产生掠夺队
func on_raid_party_spawned():
	days_since_last_raid = 0
	active_raiding_parties += 1

## 标记掠夺队被消灭
func on_raid_party_destroyed():
	active_raiding_parties = max(0, active_raiding_parties - 1)

## 每日更新
func on_day_passed():
	if poi_type == POIType.SETTLEMENT:
		days_since_last_raid += 1
	# 繁荣度自然恢复
	if prosperity < 50 and not is_under_siege:
		prosperity = min(50, prosperity + 1)
	# 围攻天数递增
	if is_under_siege:
		siege_days += 1
		prosperity = maxi(0, prosperity - 2)  # 围攻消耗繁荣度
	# 守军自然恢复
	if poi_type == POIType.CASTLE and garrison_current < garrison_max:
		garrison_current = mini(garrison_max, garrison_current + 2)
