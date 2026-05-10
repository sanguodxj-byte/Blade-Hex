# EconomyManager.gd
# 全局经济与库存单例
extends Node
class_name EconomyManager

signal resources_changed
signal inventory_changed

# 核心资源与时间
var gold: int = 1000
var food: float = 20.0
var max_food: float = 40.0
var daily_wage: int = 50

# 时间系统 (小时制)
var current_hour: float = 8.0 
var days_passed: int = 1
var month: int = 1
var year: int = 1250 

enum Season { SPRING, SUMMER, FALL, WINTER }

# 玩家背包
var player_inventory: Array[ItemData] = []

func get_season() -> int:
	if month <= 3: return Season.SPRING
	elif month <= 6: return Season.SUMMER
	elif month <= 9: return Season.FALL
	else: return Season.WINTER

func get_season_name() -> String:
	match get_season():
		Season.SPRING: return "春季"
		Season.SUMMER: return "夏季"
		Season.FALL: return "秋季"
		Season.WINTER: return "冬季"
		_: return "未知"

func advance_time(hours: float):
	current_hour += hours
	while current_hour >= 24.0:
		current_hour -= 24.0
		_on_day_passed()

func _on_day_passed():
	days_passed += 1
	spend_gold(daily_wage)
	if days_passed > 30:
		days_passed = 1
		month += 1
		if month > 12:
			month = 1
			year += 1
	resources_changed.emit()

func add_gold(amount: int):
	gold += amount
	resources_changed.emit()

func spend_gold(amount: int) -> bool:
	if gold >= amount:
		gold -= amount
		resources_changed.emit()
		return true
	return false

func consume_food(amount: float):
	food = max(0.0, food - amount)
	resources_changed.emit()

func add_item(item: ItemData):
	player_inventory.append(item)
	inventory_changed.emit()

func remove_item(item: ItemData):
	player_inventory.erase(item)
	inventory_changed.emit()

func advance_day():
	_on_day_passed()
