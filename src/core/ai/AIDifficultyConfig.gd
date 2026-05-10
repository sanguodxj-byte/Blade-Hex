# AIDifficultyConfig.gd
# 难度配置 —— 通过计算属性影响 AI 决策质量
# 难度越高：目标选择越精准、越善用地形/包夹、失误率越低
extends Resource
class_name AIDifficultyConfig

enum Difficulty {
	EASY,       ## 简单：AI笨拙，忽略地形/包夹，随机选目标，40%失误率
	NORMAL,     ## 普通：使用策略风格，有基本战术意识，15%失误率
	HARD,       ## 困难：完整战术意识，善用地形和包夹，5%失误率
	LEGENDARY   ## 传奇：完美决策，协同集火，0失误率
}

@export var difficulty: Difficulty = Difficulty.NORMAL

## 目标选择精度：0.0=随机，1.0=完美
var target_selection_accuracy: float:
	get:
		match difficulty:
			Difficulty.EASY: return 0.3
			Difficulty.NORMAL: return 0.7
			Difficulty.HARD: return 0.9
			Difficulty.LEGENDARY: return 1.0
			_: return 0.7

## AI 是否考虑地形加成
var uses_terrain: bool:
	get: return difficulty >= Difficulty.HARD

## AI 是否尝试包夹机动
var uses_flanking: bool:
	get: return difficulty >= Difficulty.NORMAL

## AI 是否协同集火
var uses_focus_fire: bool:
	get: return difficulty >= Difficulty.HARD

## 撤退HP阈值倍率（基础阈值25%，乘以这个系数）
var retreat_threshold_multiplier: float:
	get:
		match difficulty:
			Difficulty.EASY: return 0.5       # 12.5%HP才撤退
			Difficulty.NORMAL: return 1.0     # 25%HP
			Difficulty.HARD: return 1.0       # 25%HP
			Difficulty.LEGENDARY: return 0.8  # 20%HP（更聪明地保存兵力）
			_: return 1.0

## AI 做出"失误"（降级为本能策略）的概率
var mistake_chance: float:
	get:
		match difficulty:
			Difficulty.EASY: return 0.4
			Difficulty.NORMAL: return 0.15
			Difficulty.HARD: return 0.05
			Difficulty.LEGENDARY: return 0.0
			_: return 0.15

## AI 是否利用冲锋加成
var uses_charge: bool:
	get: return difficulty >= Difficulty.NORMAL

## AI 是否考虑控制区
var uses_zone_of_control: bool:
	get: return difficulty >= Difficulty.HARD

## 获取难度显示名
func get_difficulty_name() -> String:
	match difficulty:
		Difficulty.EASY: return "简单"
		Difficulty.NORMAL: return "普通"
		Difficulty.HARD: return "困难"
		Difficulty.LEGENDARY: return "传奇"
		_: return "普通"
