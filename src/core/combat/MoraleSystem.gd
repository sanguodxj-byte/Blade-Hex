# MoraleSystem.gd
# 士气系统 — 事件驱动的士气变化、士气光环、溃逃判定
# 对应策划案 03-战术战斗系统 → 六、士气系统
extends Node
class_name MoraleSystem

# ============================================================================
# 信号
# ============================================================================

signal morale_changed(unit: Unit, old_value: int, new_value: int)
signal morale_rout(unit: Unit)

# ============================================================================
# 士气事件处理
# ============================================================================

## 单位被击杀 — 距离衰减影响周围单位士气
func on_unit_killed(killed: Unit, _killer: Unit, all_units: Array):
	for unit in all_units:
		if not is_instance_valid(unit) or unit.current_hp <= 0:
			continue
		if unit == killed:
			continue

		var dist = HexUtils.distance(unit.grid_pos.x, unit.grid_pos.y, killed.grid_pos.x, killed.grid_pos.y)

		# 被杀的是友方还是敌方
		var is_ally = _is_same_side(unit, killed)

		if is_ally:
			# 友方死亡 → 士气下降
			var change = _ally_death_morale_change(dist, killed.data.is_enemy == false)  # 英雄=非敌人
			_change_morale(unit, change)
		else:
			# 敌方被杀 → 士气上升
			var change = _enemy_kill_morale_change(dist)
			_change_morale(unit, change)

## 单位被暴击命中
func on_unit_crit_hit(target: Unit, _attacker: Unit):
	_change_morale(target, -5)

## 单位被包夹攻击
func on_unit_flanked(target: Unit, _attacker: Unit):
	_change_morale(target, -3)

## 回合开始检查重创（单方损失过半）
func on_turn_start_heavy_losses(side_units: Array[Unit], initial_count: int):
	var alive_count = 0
	for unit in side_units:
		if is_instance_valid(unit) and unit.current_hp > 0:
			alive_count += 1

	# 损失超过50%
	if initial_count > 0 and alive_count <= initial_count / 2:
		for unit in side_units:
			if is_instance_valid(unit) and unit.current_hp > 0:
				_change_morale(unit, -15)

## 英雄士气光环（每回合开始）
func on_hero_aura(hero: Unit, allies: Array[Unit]):
	if not is_instance_valid(hero) or hero.current_hp <= 0:
		return

	# 只有非敌方（玩家英雄）有光环
	if hero.data.is_enemy:
		return

	var cha_mod = RPGRuleEngine.get_stat_modifier(hero.data.cha)
	var aura_range = 1 + max(0, cha_mod)  # 高CHA扩大光环范围

	for ally in allies:
		if not is_instance_valid(ally) or ally.current_hp <= 0:
			continue
		if ally == hero:
			continue
		var dist = HexUtils.distance(ally.grid_pos.x, ally.grid_pos.y, hero.grid_pos.x, hero.grid_pos.y)
		if dist <= aura_range:
			_change_morale(ally, 3)

## 不利地形士气衰减
func on_bad_terrain(unit: Unit):
	_change_morale(unit, -1)

## 战斗胜利
func on_victory(all_units: Array[Unit]):
	for unit in all_units:
		if is_instance_valid(unit) and unit.current_hp > 0:
			_change_morale(unit, 5)


# ============================================================================
# 士气效果查询
# ============================================================================

## 获取士气等级效果
## 对应策划案士气等级表
static func get_morale_effects(unit: Unit) -> Dictionary:
	var level = unit.data.get_morale_level()
	match level:
		UnitData.MoraleLevel.HIGH:
			return {"crit_bonus": 0.20, "fumble_rate": 0.0, "ac_modifier": 0, "name": "高昂"}
		UnitData.MoraleLevel.NORMAL:
			return {"crit_bonus": 0.0, "fumble_rate": 0.0, "ac_modifier": 0, "name": "正常"}
		UnitData.MoraleLevel.LOW:
			return {"crit_bonus": 0.0, "fumble_rate": 0.20, "ac_modifier": 0, "name": "低落"}
		UnitData.MoraleLevel.BROKEN:
			return {"crit_bonus": 0.0, "fumble_rate": 0.40, "ac_modifier": -2, "name": "崩溃"}
		UnitData.MoraleLevel.ROUTING:
			return {"crit_bonus": 0.0, "fumble_rate": 1.0, "ac_modifier": -2, "name": "溃逃"}
		_:
			return {"crit_bonus": 0.0, "fumble_rate": 0.0, "ac_modifier": 0, "name": "正常"}

## 检查是否溃逃
func check_rout(unit: Unit) -> bool:
	return unit.data.get_morale_level() == UnitData.MoraleLevel.ROUTING


# ============================================================================
# 内部方法
# ============================================================================

## 修改士气值（带范围钳制和信号发射）
func _change_morale(unit: Unit, amount: int):
	var old_val = unit.data.morale
	unit.data.morale = clampi(unit.data.morale + amount, -60, 40)

	if unit.data.morale != old_val:
		morale_changed.emit(unit, old_val, unit.data.morale)

		# 检查是否新触发溃逃
		if unit.data.morale <= -60 and old_val > -60:
			morale_rout.emit(unit)

## 判断两个单位是否同阵营
func _is_same_side(a: Unit, b: Unit) -> bool:
	return a.data.is_enemy == b.data.is_enemy

## 友方死亡的士气变化（按距离衰减）
func _ally_death_morale_change(dist: int, is_hero: bool) -> int:
	if is_hero:
		if dist <= 1: return -6
		return -4
	if dist <= 1: return -10
	elif dist <= 2: return -8
	elif dist <= 3: return -6
	else: return 0

## 击杀敌方的士气提升（按距离衰减）
func _enemy_kill_morale_change(dist: int) -> int:
	if dist <= 1: return 10
	elif dist <= 2: return 8
	elif dist <= 3: return 6
	else: return 0
