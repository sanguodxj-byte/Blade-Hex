## NPC档案数据类
# 描述大地图上人形NPC的完整信息

class_name NPCProfile
extends Resource

## NPC类型枚举
enum NPCType {
	ADVENTURER,        ## 冒险者
	MERCHANT,          ## 商队
	TRAVELER,          ## 旅行者
	WANDERING_KNIGHT,  ## 流浪骑士
	BOUNTY_TARGET,     ## 通缉犯
	HOSTILE_HUMANOID,  ## 敌对人形生物
}

## NPC态度枚举
enum Attitude {
	FRIENDLY,   ## 友好
	NEUTRAL,    ## 中立
	COLD,       ## 冷漠
	HOSTILE,    ## 敌意
}

@export var npc_name: String = "无名旅人"
@export var npc_type: NPCType = NPCType.TRAVELER
@export var attitude: Attitude = Attitude.NEUTRAL
@export var faction: String = ""
@export var relation: int = 0  ## 与玩家的关系值 -100~100
@export var dialogue_intro: String = ""
@export var dialogue_lines: Dictionary = {}  ## 对话节点 key->DialogueEntry
@export var gold: int = 50
@export var recruit_cost: int = 100  ## 招募费用


## 获取NPC类型显示名称
static func get_npc_type_name(type: NPCType) -> String:
	match type:
		NPCType.ADVENTURER:       return "冒险者"
		NPCType.MERCHANT:         return "商队"
		NPCType.TRAVELER:         return "旅行者"
		NPCType.WANDERING_KNIGHT: return "流浪骑士"
		NPCType.BOUNTY_TARGET:    return "通缉犯"
		NPCType.HOSTILE_HUMANOID: return "敌对者"
		_:                        return "未知"


## 获取态度显示文本
func get_attitude_text() -> String:
	match attitude:
		Attitude.FRIENDLY: return "友好"
		Attitude.NEUTRAL:  return "中立"
		Attitude.COLD:     return "冷漠"
		Attitude.HOSTILE:  return "敌意"
		_:                 return "未知"


## 获取NPC的描述文本
func get_description() -> String:
	var type_name := get_npc_type_name(npc_type)
	var attitude_text := get_attitude_text()
	if dialogue_intro != "":
		return dialogue_intro
	match npc_type:
		NPCType.ADVENTURER:
			return "一支%s的冒险者队伍，态度%s。" % [type_name, attitude_text]
		NPCType.MERCHANT:
			return "一支%s，看起来有不少货物。态度%s。" % [type_name, attitude_text]
		NPCType.TRAVELER:
			return "一位%s，似乎在赶路。态度%s。" % [type_name, attitude_text]
		NPCType.WANDERING_KNIGHT:
			return "一位%s，装备精良，神情傲然。态度%s。" % [type_name, attitude_text]
		NPCType.BOUNTY_TARGET:
			return "一名%s，悬赏金额不菲。" % type_name
		NPCType.HOSTILE_HUMANOID:
			return "一群%s，看起来不太友善。" % type_name
		_:
			return "一个%s。" % type_name


## 获取默认对话内容（当没有自定义对话时使用）
func get_default_dialogue() -> Dictionary:
	match npc_type:
		NPCType.ADVENTURER:
			return {
				"greeting": "你好，旅人。我们是冒险者，正在寻找任务委托。",
				"options": ["你们愿意加入我的队伍吗？", "有什么消息吗？", "告辞。"],
				"responses": [
					"加入队伍？嗯，报酬合适的话可以考虑。",
					"听说北边的矿坑里有怪物出没，可能有好东西。",
					"后会有期。"
				]
			}
		NPCType.MERCHANT:
			return {
				"greeting": "欢迎！看看我们的商品吧，价格公道。",
				"options": ["让我看看你的货物。", "需要护送吗？", "告辞。"],
				"responses": [
					"当然，请随意挑选。",
					"如果你能护送我们到下一个城镇，我们会付你报酬。",
					"慢走，欢迎下次光临。"
				]
			}
		NPCType.TRAVELER:
			return {
				"greeting": "哦，你好。我只是在赶路而已。",
				"options": ["附近有什么有趣的地方吗？", "你从哪里来？", "告辞。"],
				"responses": [
					"东边的森林里据说有古老的遗迹，不过很危险。",
					"我从南方的城镇来，那里最近不太太平。",
					"再见，祝旅途平安。"
				]
			}
		NPCType.WANDERING_KNIGHT:
			return {
				"greeting": "哼，又是一个旅人。你看起来还有几分本事。",
				"options": ["我想和你切磋一下。", "你在寻找什么？", "告辞。"],
				"responses": [
					"切磋？有意思。如果你赢了，我给你我的佩剑；如果你输了，留下你的金币。",
					"我在寻找一位强大的对手，证明我的实力。",
					"后会有期。"
				]
			}
		NPCType.BOUNTY_TARGET:
			return {
				"greeting": "你想干什么？别挡路！",
				"options": ["你就是那个通缉犯？", "我可以放你走，但你得付钱。", "告辞。"],
				"responses": [
					"通缉犯？你认错人了！……好吧，你打算怎么办？",
					"哈，识时务。给我一个理由，我就走。",
					"算你聪明，快滚吧。"
				]
			}
		NPCType.HOSTILE_HUMANOID:
			return {
				"greeting": "滚开！这是我们的地盘！",
				"options": ["我不想和你们冲突。", "那就让拳头说话吧。"],
				"responses": [
					"算你识相，快滚！",
					"找死！"
				]
			}
		_:
			return {
				"greeting": "……",
				"options": ["告辞。"],
				"responses": ["……"]
			}
