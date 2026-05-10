# BattleContext.gd
# 战斗上下文 — 封装从大地图到战斗场景的所有传递信息
# 在 OverworldMap 遭遇时创建，传递给 CombatTest/CombatManager 使用
class_name BattleContext
extends RefCounted


## 交战类型
## 对应策划案 03-战术战斗系统 → 八、视野与战争迷雾
enum EngagementType {
	NORMAL,    # 正常遭遇：双方在地图两端部署
	AMBUSH,    # 玩家伏击敌人：玩家分散有利，敌人集中混乱
	AMBUSHED,  # 玩家被伏击：玩家集中混乱，敌人分散有利，首回合AC-2
}


## 大地图遭遇点的地形类型
var overworld_terrain: OverworldTerrain.Type = OverworldTerrain.Type.PLAINS

## 战斗规模（影响地图尺寸和参战单位数）
var battle_size: BattleMapGenerator.BattleSize = BattleMapGenerator.BattleSize.MERCENARY

## 交战类型
var engagement_type: EngagementType = EngagementType.NORMAL

## 随机种子（固定时可重现同一张地图）
var seed: int = 0

## 环境事件覆盖（为空则使用模板默认）
var environment_override: String = ""

## 遭遇点的大地图坐标（用于调试和日志）
var encounter_position: Vector2i = Vector2i.ZERO

## 大地图网格引用 — 传入后 BattleMapGenerator 将从大地图采样地形
var overworld_grid: HexOverworldGrid = null

## 遭遇点在大地图上的轴向坐标 (q, r)
var encounter_coord: Vector2i = Vector2i.ZERO

## 遭遇点 POI 类型（-1 = 野外遭遇，无 POI）
var poi_type: int = -1


## 工厂方法：创建战斗上下文
static func create(
	terrain: OverworldTerrain.Type,
	size: BattleMapGenerator.BattleSize,
	engagement: EngagementType,
	seed_val: int = 0
) -> BattleContext:
	var ctx = BattleContext.new()
	ctx.overworld_terrain = terrain
	ctx.battle_size = size
	ctx.engagement_type = engagement
	ctx.seed = seed_val if seed_val != 0 else randi()
	return ctx


## 从大地图噪声值自动推断地形并创建上下文
static func create_from_noise(
	noise_value: float,
	size: BattleMapGenerator.BattleSize = BattleMapGenerator.BattleSize.MERCENARY,
	engagement: EngagementType = EngagementType.NORMAL,
	seed_val: int = 0
) -> BattleContext:
	var terrain = OverworldTerrain.from_noise(noise_value)
	return create(terrain, size, engagement, seed_val)


## 获取上下文描述（用于调试和 UI 显示）
func get_description() -> String:
	var terrain_name = OverworldTerrain.get_name(overworld_terrain)
	var size_name = ""
	match battle_size:
		BattleMapGenerator.BattleSize.MERCENARY: size_name = "雇佣兵"
		BattleMapGenerator.BattleSize.KNIGHT: size_name = "骑士"
		BattleMapGenerator.BattleSize.LORD: size_name = "领主"
	var engagement_name = ""
	match engagement_type:
		EngagementType.NORMAL: engagement_name = "正常遭遇"
		EngagementType.AMBUSH: engagement_name = "伏击"
		EngagementType.AMBUSHED: engagement_name = "被伏击"
	return "%s规模·%s·%s" % [size_name, terrain_name, engagement_name]
