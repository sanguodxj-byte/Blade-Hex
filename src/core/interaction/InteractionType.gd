## 交互类型枚举
# 定义大地图上所有可能的交互类型

class_name InteractionType

enum Type {
	ATTACK,       ## 袭击/战斗
	TALK,         ## 交谈
	TRADE,        ## 交易
	LEAVE,        ## 离开
	RECRUIT,      ## 招募
	DUEL,         ## 决斗
	ESCORT,       ## 护送
	INFORMATION,  ## 打听情报
	BOUNTY,       ## 缉拿
	REST,         ## 休息
	TRAIN,        ## 训练
	REPAIR,       ## 修理装备
	HEAL,         ## 治疗
	QUEST,        ## 领取委托
	ARENA,        ## 竞技场
}


## 获取交互类型的显示名称
static func get_display_name(type: Type) -> String:
	match type:
		Type.ATTACK:      return "袭击"
		Type.TALK:        return "交谈"
		Type.TRADE:       return "交易"
		Type.LEAVE:       return "离开"
		Type.RECRUIT:     return "招募"
		Type.DUEL:        return "决斗"
		Type.ESCORT:      return "护送"
		Type.INFORMATION: return "打听情报"
		Type.BOUNTY:      return "缉拿"
		Type.REST:        return "休息"
		Type.TRAIN:       return "训练"
		Type.REPAIR:      return "修理"
		Type.HEAL:        return "治疗"
		Type.QUEST:       return "委托"
		Type.ARENA:       return "竞技场"
		_:                return "未知"


## 获取交互类型的描述文本
static func get_description(type: Type) -> String:
	match type:
		Type.ATTACK:      return "向对方发起攻击，进入战术战斗"
		Type.TALK:        return "与对方交谈，了解更多信息"
		Type.TRADE:       return "查看对方的商品，进行交易"
		Type.LEAVE:       return "不做任何事，继续前进"
		Type.RECRUIT:     return "邀请对方加入你的队伍"
		Type.DUEL:        return "向对方发起一对一决斗挑战"
		Type.ESCORT:      return "接受护送委托，护送商队前往目的地"
		Type.INFORMATION: return "向对方打听周围的情报和传闻"
		Type.BOUNTY:      return "将通缉犯缉拿归案，送交领主领赏"
		Type.REST:        return "在安全的地方休息，恢复队伍状态"
		Type.TRAIN:       return "花费金币进行训练，获取经验"
		Type.REPAIR:      return "修理受损的装备"
		Type.HEAL:        return "接受治疗，恢复生命值和状态"
		Type.QUEST:       return "查看可领取的委托任务"
		Type.ARENA:       return "参加竞技场比赛，赢取奖品和声望"
		_:                return ""


## 获取交互类型的图标名称（用于UI图标映射）
static func get_icon_name(type: Type) -> String:
	match type:
		Type.ATTACK:      return "sword"
		Type.TALK:        return "chat"
		Type.TRADE:       return "coins"
		Type.LEAVE:       return "exit"
		Type.RECRUIT:     return "user_plus"
		Type.DUEL:        return "swords"
		Type.ESCORT:      return "shield"
		Type.INFORMATION: return "info"
		Type.BOUNTY:      return "target"
		Type.REST:        return "bed"
		Type.TRAIN:       return "dumbbell"
		Type.REPAIR:      return "wrench"
		Type.HEAL:        return "heart"
		Type.QUEST:       return "scroll"
		Type.ARENA:       return "trophy"
		_:                return "question"
