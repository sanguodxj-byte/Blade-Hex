# OverworldEntity.gd
# 大地图上的移动AI实体数据模型
# 冒险者、掠夺队、商队、史诗怪物等
extends Resource
class_name OverworldEntity

## 实体类型
enum EntityType {
	ADVENTURER,     ## 冒险者队伍
	RAIDING_PARTY,  ## 外族掠夺队
	CARAVAN,        ## 商队
	EPIC_MONSTER,   ## 史诗怪物（龙/魔像）
	LORD_ARMY,      ## 领主军队
}

## 实体AI状态
enum AIState {
	IDLE,           ## 待机
	PATROLLING,     ## 巡逻
	MOVING_TO_TARGET, ## 向目标移动
	FLEEING,        ## 逃跑
	RETURNING,      ## 返回基地
	ATTACKING,      ## 正在攻击
	BESIEGING,      ## 围攻中
	REINFORCING,    ## 前往回援
	CHASING,        ## 追击敌人
	RECRUITING,     ## 招募中（领主在城镇）
	ESCORting,      ## 护送中（领主护送商队）
}

## 基础字段
@export var entity_name: String = "未知实体"
@export var entity_type: EntityType = EntityType.ADVENTURER
@export var position: Vector2 = Vector2.ZERO
@export var move_speed: float = 200.0  ## 大地图移动速度(px/s)

## 队伍配置
@export var party_size: int = 4         ## 队伍人数
@export var party_level: int = 1        ## 队伍平均等级
@export var combat_power: float = 10.0  ## 综合战力（用于AI评估）

## 关系
@export var faction: String = "neutral"  ## 所属势力
@export var is_hostile_to_player: bool = true

## 行为参数
@export var patrol_radius: float = 300.0  ## 巡逻半径(像素)
@export var vision_range: float = 400.0   ## 视野范围(像素)
@export var home_position: Vector2 = Vector2.ZERO  ## 基地/出发位置
@export var territory_center: Vector2 = Vector2.ZERO  ## 领地中心

## 冒险者专属
@export var adventurer_type: String = "veteran"  ## novice/veteran/elite
@export var gold_carried: int = 50

## 掠夺队专属
@export var source_settlement: OverworldPOI = null  ## 来源聚落
@export var loot_carried: int = 0  ## 携带的战利品

## 商队专属
@export var origin_town: OverworldPOI = null  ## 出发城镇
@export var destination_town: OverworldPOI = null  ## 目标城镇
@export var trade_goods: int = 100  ## 货物价值
var prosperity_contribution: bool = false  ## 商队是否已到达目的地（影响城镇繁荣度）

## 史诗怪物专属
@export var monster_type: String = "dragon"  ## dragon/ancient_golem
@export var territory_radius: float = 500.0  ## 领地范围
@export var is_aggressive: bool = false  ## 是否处于攻击状态

## 领主军队专属
@export var lord_personality: int = 1  ## OverworldPOI.LordPersonality
@export var garrison_size: int = 30     ## 麾下兵力
@export var guarded_poi: OverworldPOI = null  ## 守卫的POI（城堡/城镇）

## 围攻/回援专属
var siege_target: OverworldPOI = null    ## 围攻目标
var reinforce_target: OverworldPOI = null  ## 回援目标
var chase_target: OverworldEntity = null   ## 追击目标

## 运行时状态
var ai_state: AIState = AIState.IDLE
var target_position: Vector2 = Vector2.ZERO
var current_target_entity: OverworldEntity = null
var path: Array[Vector2] = []
var is_moving: bool = false
var days_alive: int = 0
var is_alive: bool = true  ## 实体是否存活

## 获取实体类型显示名
func get_type_name() -> String:
	match entity_type:
		EntityType.ADVENTURER: return "冒险者"
		EntityType.RAIDING_PARTY: return "掠夺队"
		EntityType.CARAVAN: return "商队"
		EntityType.EPIC_MONSTER: return get_monster_display_name()
		EntityType.LORD_ARMY: return "领主军队"
		_: return "未知"

## 获取怪物显示名
func get_monster_display_name() -> String:
	match monster_type:
		"dragon": return "巨龙"
		"ancient_golem": return "远古魔像"
		"undead_lord": return "亡灵领主"
		_: return "史诗怪物"

## 获取实体在大地图上的显示颜色
func get_display_color() -> Color:
	match entity_type:
		EntityType.ADVENTURER: return Color(0.2, 0.8, 0.4)   # 绿色
		EntityType.RAIDING_PARTY: return Color(0.9, 0.3, 0.2)  # 红色
		EntityType.CARAVAN: return Color(0.8, 0.7, 0.2)        # 金色
		EntityType.EPIC_MONSTER: return Color(0.8, 0.2, 0.8)   # 紫色
		EntityType.LORD_ARMY: return Color(0.3, 0.5, 0.9)      # 蓝色
		_: return Color.WHITE

## 评估与另一个实体的战力比
func evaluate_power_ratio(other: OverworldEntity) -> float:
	if other.combat_power <= 0:
		return 10.0
	return combat_power / other.combat_power

## 检测目标是否在视野内
func is_in_vision(target_pos: Vector2) -> bool:
	return position.distance_to(target_pos) <= vision_range

## 检测目标是否在领地内
func is_in_territory(target_pos: Vector2) -> bool:
	if territory_center == Vector2.ZERO:
		return false
	return territory_center.distance_to(target_pos) <= territory_radius

## 获取遭遇的敌方配置
func get_encounter_config() -> Dictionary:
	var config: Dictionary = {"enemies": [], "cr_total": 0.0}
	
	match entity_type:
		EntityType.ADVENTURER:
			config.enemies = ["adventurer_warrior", "adventurer_mage"]
			config.cr_total = party_level * 1.5
		EntityType.RAIDING_PARTY:
			if source_settlement:
				match source_settlement.settlement_race:
					OverworldPOI.SettlementRace.GOBLIN:
						config.enemies = ["goblin_warrior", "goblin_archer"]
						config.cr_total = 2.0 + threat_level() * 1.5
					OverworldPOI.SettlementRace.KOBOLD:
						config.enemies = ["kobold_trapper"]
						config.cr_total = 3.0 + threat_level() * 1.5
					OverworldPOI.SettlementRace.MINOTAUR:
						config.enemies = ["minotaur_warrior"]
						config.cr_total = 5.0
					OverworldPOI.SettlementRace.SHADOW_CULT:
						config.enemies = ["cultist"]
						config.cr_total = 4.0
			else:
				config.enemies = ["goblin_warrior"]
				config.cr_total = 2.0
		EntityType.EPIC_MONSTER:
			match monster_type:
				"dragon":
					config.enemies = ["dragon"]
					config.cr_total = 10.0 + party_level * 2.0
				"ancient_golem":
					config.enemies = ["iron_golem"]
					config.cr_total = 6.0 + party_level * 1.5
				_:
					config.enemies = ["unknown_boss"]
					config.cr_total = 8.0
		EntityType.CARAVAN:
			config.enemies = ["caravan_guard"]
			config.cr_total = 1.0
		EntityType.LORD_ARMY:
			config.enemies = ["soldier", "archer"]
			config.cr_total = party_level * 3.0
	
	return config

## 辅助：获取掠夺队的威胁等级
func threat_level() -> float:
	if source_settlement:
		return source_settlement.threat_level
	return 0.5

## 每日更新
func on_day_passed():
	days_alive += 1
	# 掠夺队存活太久自动返回
	if entity_type == EntityType.RAIDING_PARTY and days_alive > 14:
		ai_state = AIState.RETURNING
		target_position = home_position
