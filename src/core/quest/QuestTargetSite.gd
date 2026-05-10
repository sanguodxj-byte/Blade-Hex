# QuestTargetSite.gd
# 委托任务目标点 — 大地图上接取任务时生成的建筑瓦片/地标数据模型
#
# 设计思路:
#   玩家在城镇接取委托后, 世界地图上会在对应位置生成一个任务目标点
#   (如哥布林营地、废弃矿坑、遗迹入口等)
#   目标点携带敌方配置数据, 玩家到达后可触发战斗
#
# 与现有系统的关系:
#   QuestData 持有 target_world_position (世界坐标)
#   QuestTargetSite 由 QuestManager 在接取任务时创建
#   OverworldScene 负责渲染和玩家接近检测
class_name QuestTargetSite
extends RefCounted


## ========================================
## 目标点类型（决定大地图上的视觉样式）
## ========================================
enum SiteType {
	GOBLIN_CAMP,      ## 哥布林营地 — 简陋帐篷/篝火
	KOBOLD_MINE,      ## 狗头人矿坑 — 矿洞入口
	MINOTAUR_FORT,    ## 牛头人石堡 — 巨石建筑
	CULT_HIDEOUT,     ## 暗影教团据点 — 黑色祭坛
	BANDIT_CAMP,      ## 强盗营地 — 木栅营地
	WOLF_DEN,         ## 狼穴 — 洞穴入口
	DUNGEON_ENTRANCE, ## 地下城入口 — 废弃入口
	RUINS,            ## 遗迹 — 倾塌的石柱
	TOMB,             ## 墓穴 — 封印的石门
	DRAGON_LAIR,      ## 龙巢 — 山顶洞穴
	SIGNPOST,         ## 路标/集结点 — 护送/防御型
	VILLAGE_THREAT,   ## 受威胁村庄 — 村庄标记（防御型任务）
}


## ========================================
## 核心数据
## ========================================

## 关联的任务ID
var quest_id: String = ""

## 目标点名称（大地图上显示）
var site_name: String = ""

## 目标点类型
var site_type: SiteType = SiteType.GOBLIN_CAMP

## 大地图世界坐标（像素）
var world_position: Vector2 = Vector2.ZERO

## 遭遇数据（敌方配置）
var encounter_data: QuestEncounterData = null

## 是否已被完成/清理
var is_cleared: bool = false

## 是否在玩家视野内（运行时）
var is_visible_to_player: bool = false

## 难度星级 (1~5)
var danger_stars: int = 1


## ========================================
## 工厂方法
## ========================================

## 从 QuestData 生成一个匹配类型的目标点
static func create_from_quest(quest: QuestData) -> QuestTargetSite:
	var site = QuestTargetSite.new()
	site.quest_id = quest.quest_id
	site.site_name = quest.target_description
	site.world_position = quest.target_world_position
	site.danger_stars = _difficulty_to_stars(quest.difficulty)

	# 根据任务类型推断目标点类型
	site.site_type = _infer_site_type(quest)
	# 根据任务配置生成遭遇数据
	site.encounter_data = QuestEncounterData.create_from_quest(quest, site.site_type)
	return site


## ========================================
## 遭遇配置获取
## ========================================

## 获取用于战斗生成的敌方配置 (兼容 BattleContext/OverworldEntity 接口)
func get_encounter_config() -> Dictionary:
	if not encounter_data:
		return {"enemies": [], "cr_total": 0.0}
	return encounter_data.to_encounter_config()


## 获取战场模板名（传入 BattleMapGenerator 使用）
func get_battle_template_name() -> String:
	match site_type:
		SiteType.GOBLIN_CAMP:      return "goblin_camp"
		SiteType.KOBOLD_MINE:      return "kobold_mine"
		SiteType.MINOTAUR_FORT:    return "minotaur_fortress"
		SiteType.CULT_HIDEOUT:     return "shadow_cult_hideout"
		SiteType.BANDIT_CAMP:      return "bandit_camp"
		SiteType.WOLF_DEN:         return "wolf_den"
		SiteType.DUNGEON_ENTRANCE: return "dungeon_entrance"
		SiteType.RUINS:            return "ruins_exploration"
		SiteType.TOMB:             return "ancient_tomb"
		SiteType.DRAGON_LAIR:      return "dragon_lair"
		SiteType.SIGNPOST:         return "plain_field"
		SiteType.VILLAGE_THREAT:   return "village_defense"
		_:                          return "plain_field"


## 获取显示颜色
func get_display_color() -> Color:
	match site_type:
		SiteType.GOBLIN_CAMP:      return Color(0.6, 0.45, 0.2)
		SiteType.KOBOLD_MINE:      return Color(0.5, 0.4, 0.3)
		SiteType.MINOTAUR_FORT:    return Color(0.75, 0.3, 0.15)
		SiteType.CULT_HIDEOUT:     return Color(0.4, 0.15, 0.55)
		SiteType.BANDIT_CAMP:      return Color(0.55, 0.35, 0.2)
		SiteType.WOLF_DEN:         return Color(0.5, 0.5, 0.45)
		SiteType.DUNGEON_ENTRANCE: return Color(0.35, 0.35, 0.45)
		SiteType.RUINS:            return Color(0.6, 0.55, 0.35)
		SiteType.TOMB:             return Color(0.4, 0.4, 0.5)
		SiteType.DRAGON_LAIR:      return Color(0.85, 0.6, 0.1)
		SiteType.SIGNPOST:         return Color(0.3, 0.6, 0.8)
		SiteType.VILLAGE_THREAT:   return Color(0.8, 0.2, 0.2)
		_:                          return Color(0.7, 0.5, 0.3)


## 获取目标点类型的中文名
func get_site_type_name() -> String:
	match site_type:
		SiteType.GOBLIN_CAMP:      return "哥布林营地"
		SiteType.KOBOLD_MINE:      return "狗头人矿坑"
		SiteType.MINOTAUR_FORT:    return "牛头人石堡"
		SiteType.CULT_HIDEOUT:     return "暗影教团据点"
		SiteType.BANDIT_CAMP:      return "强盗营地"
		SiteType.WOLF_DEN:         return "狼穴"
		SiteType.DUNGEON_ENTRANCE: return "地下城入口"
		SiteType.RUINS:            return "遗迹"
		SiteType.TOMB:             return "墓穴"
		SiteType.DRAGON_LAIR:      return "龙巢"
		SiteType.SIGNPOST:         return "集结点"
		SiteType.VILLAGE_THREAT:   return "受威胁村庄"
		_:                          return "未知地点"


## ========================================
## 序列化
## ========================================

func serialize() -> Dictionary:
	var data := {
		"quest_id": quest_id,
		"site_name": site_name,
		"site_type": site_type,
		"world_position": [world_position.x, world_position.y],
		"is_cleared": is_cleared,
		"danger_stars": danger_stars,
	}
	if encounter_data:
		data["encounter_data"] = encounter_data.serialize()
	return data


static func deserialize(data: Dictionary) -> QuestTargetSite:
	var site = QuestTargetSite.new()
	site.quest_id = str(data.get("quest_id", ""))
	site.site_name = str(data.get("site_name", ""))
	site.site_type = int(data.get("site_type", 0))
	var pos = data.get("world_position", [0.0, 0.0])
	site.world_position = Vector2(float(pos[0]), float(pos[1]))
	site.is_cleared = bool(data.get("is_cleared", false))
	site.danger_stars = int(data.get("danger_stars", 1))
	if data.has("encounter_data"):
		site.encounter_data = QuestEncounterData.deserialize(data["encounter_data"])
	return site


## ========================================
## 内部工具
## ========================================

## 根据任务类型和描述推断目标点类型
static func _infer_site_type(quest: QuestData) -> int:
	var desc: String = quest.target_description.to_lower()
	var quest_type: int = quest.quest_type

	# 先按描述关键词匹配
	if "哥布林" in desc or "goblin" in desc:
		return SiteType.GOBLIN_CAMP
	if "狗头人" in desc or "kobold" in desc or "矿" in desc:
		return SiteType.KOBOLD_MINE
	if "牛头人" in desc or "minotaur" in desc:
		return SiteType.MINOTAUR_FORT
	if "教团" in desc or "cult" in desc or "暗影" in desc:
		return SiteType.CULT_HIDEOUT
	if "强盗" in desc or "bandit" in desc:
		return SiteType.BANDIT_CAMP
	if "狼" in desc or "wolf" in desc:
		return SiteType.WOLF_DEN
	if "龙" in desc or "dragon" in desc:
		return SiteType.DRAGON_LAIR
	if "遗迹" in desc or "ruins" in desc:
		return SiteType.RUINS
	if "墓穴" in desc or "墓" in desc or "tomb" in desc:
		return SiteType.TOMB
	if "地下" in desc or "地牢" in desc or "dungeon" in desc:
		return SiteType.DUNGEON_ENTRANCE

	# 再按任务类型回退
	match quest_type:
		QuestData.QuestType.EXTERMINATION:
			return SiteType.GOBLIN_CAMP  # 默认营地
		QuestData.QuestType.EXPLORATION:
			return SiteType.RUINS        # 默认遗迹
		QuestData.QuestType.ESCORT:
			return SiteType.SIGNPOST     # 路标
		QuestData.QuestType.DEFENSE:
			return SiteType.VILLAGE_THREAT  # 受威胁村庄
		QuestData.QuestType.EMERGENCY:
			return SiteType.CULT_HIDEOUT    # 默认教团
		_:
			return SiteType.GOBLIN_CAMP


## 难度枚举 → 星级
static func _difficulty_to_stars(diff: int) -> int:
	match diff:
		QuestData.QuestDifficulty.EASY:   return 1
		QuestData.QuestDifficulty.MEDIUM: return 2
		QuestData.QuestDifficulty.HARD:   return 3
		QuestData.QuestDifficulty.BOSS:   return 5
		_:                                return 1
