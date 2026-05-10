# SkillData.gd
# 技能与法术数据
extends Resource
class_name SkillData

@export var skill_name: String = "未命名技能"
@export_multiline var description: String = ""
@export var icon: Texture2D

@export var ap_cost: int = 1 # 行动点消耗 (主行动/次要行动等，后续可细化)
@export var range_cells: int = 1
@export var cooldown: int = 0
