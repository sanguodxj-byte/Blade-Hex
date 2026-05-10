# HexCellData.gd
# 地形数据资源
extends Resource
class_name HexCellData

@export var terrain_name: String = "平地"
@export var move_cost: int = 1
@export var ac_bonus: int = 0
@export var cover_type: String = "无" # 无, 半掩体, 全掩体
@export var terrain_color: Color = Color.WHITE
@export var icon: Texture2D
