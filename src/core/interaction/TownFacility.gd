## 城镇设施数据类
# 描述城镇中的可交互设施

class_name TownFacility
extends RefCounted

## 设施类型枚举
enum FacilityType {
	CASTLE,    ## 城堡/领主厅
	MARKET,    ## 市场
	TAVERN,    ## 酒馆
	ARENA,     ## 竞技场
	SMITHY,    ## 铁匠铺
	TRAINING,  ## 训练场
	TEMPLE,    ## 神殿
}

## 设施名称
var facility_name: String = ""
## 设施类型
var facility_type: FacilityType = FacilityType.MARKET
## 是否可用
var is_available: bool = true
## 设施描述
var description: String = ""
## 对应的交互类型
var interaction_type: int = -1  # InteractionType.Type


func _init(p_name: String = "", p_type: FacilityType = FacilityType.MARKET, p_available: bool = true) -> void:
	facility_name = p_name
	facility_type = p_type
	is_available = p_available
	description = _get_default_description(p_type)
	interaction_type = _get_default_interaction_type(p_type)


## 获取设施类型的显示名称
static func get_type_name(type: FacilityType) -> String:
	match type:
		FacilityType.CASTLE:   return "城堡"
		FacilityType.MARKET:   return "市场"
		FacilityType.TAVERN:   return "酒馆"
		FacilityType.ARENA:    return "竞技场"
		FacilityType.SMITHY:   return "铁匠铺"
		FacilityType.TRAINING: return "训练场"
		FacilityType.TEMPLE:   return "神殿"
		_:                      return "未知"


## 获取设施类型的图标名称
static func get_type_icon(type: FacilityType) -> String:
	match type:
		FacilityType.CASTLE:   return "castle"
		FacilityType.MARKET:   return "store"
		FacilityType.TAVERN:   return "beer"
		FacilityType.ARENA:    return "trophy"
		FacilityType.SMITHY:   return "anvil"
		FacilityType.TRAINING: return "dumbbell"
		FacilityType.TEMPLE:   return "church"
		_:                      return "building"


## 创建标准城镇设施列表
static func create_default_facilities() -> Array[TownFacility]:
	var facilities: Array[TownFacility] = []
	facilities.append(TownFacility.new("领主厅", FacilityType.CASTLE))
	facilities.append(TownFacility.new("市场", FacilityType.MARKET))
	facilities.append(TownFacility.new("酒馆", FacilityType.TAVERN))
	facilities.append(TownFacility.new("竞技场", FacilityType.ARENA))
	facilities.append(TownFacility.new("铁匠铺", FacilityType.SMITHY))
	facilities.append(TownFacility.new("训练场", FacilityType.TRAINING))
	facilities.append(TownFacility.new("神殿", FacilityType.TEMPLE))
	return facilities


## 创建村庄设施列表（简化版）
static func create_village_facilities() -> Array[TownFacility]:
	var facilities: Array[TownFacility] = []
	facilities.append(TownFacility.new("布告栏", FacilityType.CASTLE))
	facilities.append(TownFacility.new("杂货铺", FacilityType.MARKET))
	facilities.append(TownFacility.new("旅店", FacilityType.TAVERN))
	return facilities


## 获取默认描述
static func _get_default_description(type: FacilityType) -> String:
	match type:
		FacilityType.CASTLE:   return "领主的居所，可以领取委托和报告任务"
		FacilityType.MARKET:   return "各种商品琳琅满目，可以购买和出售物品"
		FacilityType.TAVERN:   return "冒险者的聚集地，可以招募伙伴和打听消息"
		FacilityType.ARENA:    return "展示实力的地方，赢得比赛获取奖品和声望"
		FacilityType.SMITHY:   return "经验丰富的铁匠，可以修理和升级装备"
		FacilityType.TRAINING: return "训练场，花费金币提升经验"
		FacilityType.TEMPLE:   return "神圣的殿堂，可以治疗伤病和购买圣水"
		_:                      return ""


## 获取默认交互类型
static func _get_default_interaction_type(type: FacilityType) -> int:
	# 返回 InteractionType.Type 值
	match type:
		FacilityType.CASTLE:   return 3   # QUEST
		FacilityType.MARKET:   return 2   # TRADE
		FacilityType.TAVERN:   return 1   # TALK (招募+打听)
		FacilityType.ARENA:    return 14  # ARENA
		FacilityType.SMITHY:   return 11  # REPAIR
		FacilityType.TRAINING: return 10  # TRAIN
		FacilityType.TEMPLE:   return 12  # HEAL
		_:                      return -1
