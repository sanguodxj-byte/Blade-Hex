# AIStrategyInstinct.gd
# 本能策略 —— 随机目标、无战术、追击到死
# 适用于：低智力怪物（野兽群、虫类等）
extends AIStrategyBase
class_name AIStrategyInstinct

func _decide_strategy_action(actor: Unit, scored_targets: Array, _player_units: Array, _enemy_units: Array, hex_grid: HexGrid):
	# 本能策略：从评分目标中随机选一个
	var idx := randi() % scored_targets.size()
	var target: Unit = scored_targets[idx]["unit"]
	
	# 直接追击攻击，无战术
	var action = _create_attack_action(actor, target, hex_grid)
	action.description = "%s 本能地扑向 %s！" % [actor.data.unit_name, target.data.unit_name]
	return action

## 覆盖：本能生物不会因低HP撤退（只有士气溃逃才退）
func _check_retreat(_actor: Unit, _player_units: Array, _hex_grid: HexGrid):
	return null
