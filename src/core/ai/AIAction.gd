# AIAction.gd
# AI 决策的输出数据类 —— 表示一个敌方单位计划执行的动作
class_name AIAction

enum Type {
	MOVE_THEN_ATTACK,  ## 先移动再攻击
	ATTACK,            ## 从当前位置攻击
	MOVE_ONLY,         ## 仅移动（重新定位/巡逻）
	RETREAT,           ## 向撤退点逃跑
	OVERWATCH,         ## 进入防御姿态
	USE_SKILL,         ## 使用技能/能力
	IDLE               ## 待机
}

var action_type: Type = Type.IDLE
var actor: Unit = null
var target_unit: Unit = null
var target_position: Vector2i = Vector2i(-1, -1)
var attack_position: Vector2i = Vector2i(-1, -1)
var priority_score: float = 0.0
var description: String = ""
var move_path: Array[Vector2i] = []

## 冲锋标记
var is_charge: bool = false

## 包夹信息
var is_flanking: bool = false
var is_backstab: bool = false
