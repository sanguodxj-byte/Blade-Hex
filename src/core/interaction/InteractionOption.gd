## 交互选项数据类
# 描述一个可选择的交互选项

class_name InteractionOption
extends RefCounted

## 唯一标识
var id: String = ""
## 显示文本
var label: String = ""
## 图标名称
var icon_name: String = ""
## 是否可用
var enabled: bool = true
## 悬停提示
var tooltip: String = ""
## 交互类型
var interaction_type: int = -1  # InteractionType.Type
## 额外元数据
var metadata: Dictionary = {}


func _init(pid: String = "", plabel: String = "", p_type: int = -1, ptooltip: String = "") -> void:
	id = pid
	label = plabel
	interaction_type = p_type
	tooltip = ptooltip


## 工厂方法：创建袭击选项
static func create_attack() -> InteractionOption:
	var opt := InteractionOption.new(
		"attack",
		InteractionType.get_display_name(InteractionType.Type.ATTACK),
		InteractionType.Type.ATTACK,
		InteractionType.get_description(InteractionType.Type.ATTACK)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.ATTACK)
	return opt


## 工厂方法：创建交谈选项
static func create_talk() -> InteractionOption:
	var opt := InteractionOption.new(
		"talk",
		InteractionType.get_display_name(InteractionType.Type.TALK),
		InteractionType.Type.TALK,
		InteractionType.get_description(InteractionType.Type.TALK)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.TALK)
	return opt


## 工厂方法：创建交易选项
static func create_trade() -> InteractionOption:
	var opt := InteractionOption.new(
		"trade",
		InteractionType.get_display_name(InteractionType.Type.TRADE),
		InteractionType.Type.TRADE,
		InteractionType.get_description(InteractionType.Type.TRADE)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.TRADE)
	return opt


## 工厂方法：创建离开选项
static func create_leave() -> InteractionOption:
	var opt := InteractionOption.new(
		"leave",
		InteractionType.get_display_name(InteractionType.Type.LEAVE),
		InteractionType.Type.LEAVE,
		InteractionType.get_description(InteractionType.Type.LEAVE)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.LEAVE)
	return opt


## 工厂方法：创建招募选项
static func create_recruit() -> InteractionOption:
	var opt := InteractionOption.new(
		"recruit",
		InteractionType.get_display_name(InteractionType.Type.RECRUIT),
		InteractionType.Type.RECRUIT,
		InteractionType.get_description(InteractionType.Type.RECRUIT)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.RECRUIT)
	return opt


## 工厂方法：创建决斗选项
static func create_duel() -> InteractionOption:
	var opt := InteractionOption.new(
		"duel",
		InteractionType.get_display_name(InteractionType.Type.DUEL),
		InteractionType.Type.DUEL,
		InteractionType.get_description(InteractionType.Type.DUEL)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.DUEL)
	return opt


## 工厂方法：创建打听情报选项
static func create_information() -> InteractionOption:
	var opt := InteractionOption.new(
		"information",
		InteractionType.get_display_name(InteractionType.Type.INFORMATION),
		InteractionType.Type.INFORMATION,
		InteractionType.get_description(InteractionType.Type.INFORMATION)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.INFORMATION)
	return opt


## 工厂方法：创建缉拿选项
static func create_bounty() -> InteractionOption:
	var opt := InteractionOption.new(
		"bounty",
		InteractionType.get_display_name(InteractionType.Type.BOUNTY),
		InteractionType.Type.BOUNTY,
		InteractionType.get_description(InteractionType.Type.BOUNTY)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.BOUNTY)
	return opt


## 工厂方法：创建护送选项
static func create_escort() -> InteractionOption:
	var opt := InteractionOption.new(
		"escort",
		InteractionType.get_display_name(InteractionType.Type.ESCORT),
		InteractionType.Type.ESCORT,
		InteractionType.get_description(InteractionType.Type.ESCORT)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.ESCORT)
	return opt


## 工厂方法：创建休息选项
static func create_rest() -> InteractionOption:
	var opt := InteractionOption.new(
		"rest",
		InteractionType.get_display_name(InteractionType.Type.REST),
		InteractionType.Type.REST,
		InteractionType.get_description(InteractionType.Type.REST)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.REST)
	return opt


## 工厂方法：创建训练选项
static func create_train() -> InteractionOption:
	var opt := InteractionOption.new(
		"train",
		InteractionType.get_display_name(InteractionType.Type.TRAIN),
		InteractionType.Type.TRAIN,
		InteractionType.get_description(InteractionType.Type.TRAIN)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.TRAIN)
	return opt


## 工厂方法：创建治疗选项
static func create_heal() -> InteractionOption:
	var opt := InteractionOption.new(
		"heal",
		InteractionType.get_display_name(InteractionType.Type.HEAL),
		InteractionType.Type.HEAL,
		InteractionType.get_description(InteractionType.Type.HEAL)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.HEAL)
	return opt


## 工厂方法：创建委托选项
static func create_quest() -> InteractionOption:
	var opt := InteractionOption.new(
		"quest",
		InteractionType.get_display_name(InteractionType.Type.QUEST),
		InteractionType.Type.QUEST,
		InteractionType.get_description(InteractionType.Type.QUEST)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.QUEST)
	return opt


## 工厂方法：创建竞技场选项
static func create_arena() -> InteractionOption:
	var opt := InteractionOption.new(
		"arena",
		InteractionType.get_display_name(InteractionType.Type.ARENA),
		InteractionType.Type.ARENA,
		InteractionType.get_description(InteractionType.Type.ARENA)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.ARENA)
	return opt


## 工厂方法：创建修理选项
static func create_repair() -> InteractionOption:
	var opt := InteractionOption.new(
		"repair",
		InteractionType.get_display_name(InteractionType.Type.REPAIR),
		InteractionType.Type.REPAIR,
		InteractionType.get_description(InteractionType.Type.REPAIR)
	)
	opt.icon_name = InteractionType.get_icon_name(InteractionType.Type.REPAIR)
	return opt
