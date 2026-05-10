# GlobalState.gd
# 全局状态管理单例，用于跨场景数据传递
extends Node

var is_loading_save: bool = false
var loaded_data: Dictionary = {}

## 快速游戏模式（跳过出身选择，随机生成角色）
var is_quick_game: bool = false

## 玩家选择的出身数据（由 OriginSelectScene 设置）
## 包含: race: RaceData, unit_data: UnitData
var player_origin: Dictionary = {}
