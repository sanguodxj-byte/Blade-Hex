# FacingSystem.gd
# 朝向系统 + 控制区 + 借机攻击 + 包夹判定
# 对应策划案 03-战术战斗系统 → 四、控制区与借机攻击 / 五、包夹与伏击
class_name FacingSystem


# ============================================================================
# 朝向系统 (Facing)
# ============================================================================

## 获取单位正面3格的坐标
static func get_front_cells(unit_pos: Vector2i, facing: int) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	# 正面 = facing方向 + 左右各偏1个方向
	for offset in [-1, 0, 1]:
		var dir = (facing + offset) % 6
		if dir < 0: dir += 6
		cells.append(HexUtils.get_neighbor(unit_pos.x, unit_pos.y, dir))
	return cells

## 获取单位侧翼2格的坐标
static func get_flank_cells(unit_pos: Vector2i, facing: int) -> Array[Vector2i] :
	var cells: Array[Vector2i] = []
	# 侧翼 = 面对方向左右各偏2个方向
	for offset in [2, -2]:
		var dir = (facing + offset) % 6
		if dir < 0: dir += 6
		cells.append(HexUtils.get_neighbor(unit_pos.x, unit_pos.y, dir))
	return cells

## 获取单位背后1格的坐标
static func get_rear_cell(unit_pos: Vector2i, facing: int) -> Vector2i:
	var rear_dir = (facing + 3) % 6
	return HexUtils.get_neighbor(unit_pos.x, unit_pos.y, rear_dir)

## 攻击方向枚举
enum AttackDirection {
	FRONT,   # 正面 — 非包夹
	FLANK,   # 侧翼 — 包夹
	REAR,    # 背后 — 背刺
}

## 判定攻击方向（攻击者相对于目标的位置）
static func get_attack_direction(attacker_pos: Vector2i, target: Unit) -> AttackDirection:
	var _front_cells = get_front_cells(target.grid_pos, target.data.facing)
	var flank_cells = get_flank_cells(target.grid_pos, target.data.facing)
	var rear_cell = get_rear_cell(target.grid_pos, target.data.facing)

	if attacker_pos == rear_cell:
		return AttackDirection.REAR
	elif attacker_pos in flank_cells:
		return AttackDirection.FLANK
	else:
		return AttackDirection.FRONT


# ============================================================================
# 控制区 (Zone of Control)
# ============================================================================

## 获取单位投射控制区的格子列表
## 普通近战: 正面3格; 防御模式: 全部6格; 远程/法师: 无控制区
static func get_zoc_cells(unit: Unit) -> Array[Vector2i]:
	# 远程单位和法师不投射控制区
	if not _has_zoc(unit):
		return []

	if unit.data.is_defending:
		# 防御模式：全部6个相邻格
		var cells: Array[Vector2i] = []
		for dir in range(6):
			cells.append(HexUtils.get_neighbor(unit.grid_pos.x, unit.grid_pos.y, dir))
		return cells
	else:
		return get_front_cells(unit.grid_pos, unit.data.facing)

## 单位是否有控制区
static func _has_zoc(unit: Unit) -> bool:
	# 远程单位和法师不投射控制区
	var weapon = unit.get_main_hand()
	if weapon and weapon.is_ranged:
		return false
	# 某些技能可以忽略控制区（此处预留）
	return true

## 检查某个格子是否在任何敌方近战单位的控制区内
static func is_in_enemy_zoc(pos: Vector2i, enemy_units: Array[Unit]) -> bool:
	for enemy in enemy_units:
		if not is_instance_valid(enemy) or enemy.current_hp <= 0:
			continue
		if not _has_zoc(enemy):
			continue
		var zoc = get_zoc_cells(enemy)
		if pos in zoc:
			return true
	return false

## 检查单位是否在被敌方控制区内移动离开
static func is_leaving_enemy_zoc(from: Vector2i, to: Vector2i, enemy_units: Array[Unit]) -> bool:
	# 从一个在敌方ZoC内的格子移动到不在该敌方ZoC内的格子
	for enemy in enemy_units:
		if not is_instance_valid(enemy) or enemy.current_hp <= 0:
			continue
		if not _has_zoc(enemy):
			continue
		var zoc = get_zoc_cells(enemy)
		if from in zoc and not to in zoc:
			return true
	return false


# ============================================================================
# 借机攻击 (Attack of Opportunity)
# ============================================================================

## 检查是否触发借机攻击
## 触发条件: (1) 在敌方近战ZoC内移动离开 (2) 在敌方近战ZoC内使用远程攻击
static func should_trigger_aoo(_mover: Unit, from: Vector2i, to: Vector2i, enemy_units: Array):
	for enemy in enemy_units:
		if not is_instance_valid(enemy) or enemy.current_hp <= 0:
			continue
		if enemy.data.aoo_used_this_turn:
			continue
		if not _has_zoc(enemy):
			continue
		var zoc = get_zoc_cells(enemy)
		# 离开ZoC
		if from in zoc and not to in zoc:
			return enemy
	return null

## 检查远程攻击是否在敌方近战ZoC内（被贴身）
static func is_ranged_in_melee_zoc(unit: Unit, enemy_units: Array[Unit]) -> bool:
	if not _has_zoc(unit):  # 自己有没有远程武器由外部判断
		return false
	for enemy in enemy_units:
		if not is_instance_valid(enemy) or enemy.current_hp <= 0:
			continue
		if not _has_zoc(enemy):
			continue
		var zoc = get_zoc_cells(enemy)
		if unit.grid_pos in zoc:
			return true
	return false


# ============================================================================
# 包夹与多人夹击 (Flanking & Surrounding)
# ============================================================================

## 获取包夹加成
## 侧翼: 伤害+25%, 士气-3
## 背后: 伤害+50%, 士气-5, 不可反击
static func get_flanking_bonus(attacker_pos: Vector2i, target: Unit) -> Dictionary:
	# 防御模式下的单位免疫包夹
	if target.data.is_defending:
		return {"damage_multiplier": 1.0, "morale_change": 0, "can_counter": true}

	var direction = get_attack_direction(attacker_pos, target)
	match direction:
		AttackDirection.FLANK:
			return {"damage_multiplier": 1.25, "morale_change": -3, "can_counter": true}
		AttackDirection.REAR:
			return {"damage_multiplier": 1.5, "morale_change": -5, "can_counter": false}
		_:
			return {"damage_multiplier": 1.0, "morale_change": 0, "can_counter": true}

## 获取多人夹击加成
## 目标周围不同方向的友方数量 → 额外加成
static func get_surrounding_bonus(target: Unit, attacker_allies: Array[Unit]) -> Dictionary:
	# 收集目标周围不同方向的友方单位
	var occupied_dirs: Array[int] = []
	for ally in attacker_allies:
		if not is_instance_valid(ally) or ally.current_hp <= 0:
			continue
		var dist = HexUtils.distance(ally.grid_pos.x, ally.grid_pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist == 1:
			# 计算方向
			var _diff = ally.grid_pos - target.grid_pos
			for dir in range(6):
				if HexUtils.get_neighbor(target.grid_pos.x, target.grid_pos.y, dir) == ally.grid_pos:
					if not dir in occupied_dirs:
						occupied_dirs.append(dir)
					break

	var count = occupied_dirs.size()
	# count包含攻击者本身，所以1=无加成，2+=加成
	if count >= 4:
		return {"hit_bonus": 3, "ac_reduction": 2, "damage_bonus": 0.1}
	elif count >= 3:
		return {"hit_bonus": 2, "ac_reduction": 1, "damage_bonus": 0.0}
	elif count >= 2:
		return {"hit_bonus": 1, "ac_reduction": 0, "damage_bonus": 0.0}
	else:
		return {"hit_bonus": 0, "ac_reduction": 0, "damage_bonus": 0.0}


# ============================================================================
# 冲锋检测 (Charge Detection)
# ============================================================================

## 检查移动路径是否构成冲锋（移动3格以上后发起近战攻击）
static func is_charge(move_path: Array[Vector2i]) -> bool:
	return move_path.size() >= 3

## 获取冲锋伤害加成
## 步行冲锋: 伤害+25%, 优势
## 骑乘冲锋: 伤害+50%, 优势（由坐骑加成额外叠加）
static func get_charge_bonus(unit: Unit, is_charge_move: bool) -> Dictionary:
	if not is_charge_move:
		return {"damage_multiplier": 1.0, "has_advantage": false}

	var base_mult = 1.25
	if unit.data.is_mounted and unit.data.mount:
		base_mult += unit.data.mount.charge_damage_bonus  # 叠加坐骑加成

	return {"damage_multiplier": base_mult, "has_advantage": true}

## 检查冲锋是否有效（不能在沙地/沼泽冲锋，骑乘不能在密林冲锋）
static func can_charge(unit: Unit, grid: HexGrid, path: Array[Vector2i]) -> bool:
	# 检查路径上的地形
	for cell_pos in path:
		var cell = grid.get_cell(cell_pos.x, cell_pos.y)
		if not cell:
			continue
		if cell.data:
			# 沙地/沼泽不可冲锋
			if cell.data.terrain_type in [BattleCellData.TerrainType.SAND, BattleCellData.TerrainType.SWAMP]:
				return false
			# 骑乘不可在密林/山地冲锋
			if unit.data.is_mounted:
				if cell.data.terrain_type in [BattleCellData.TerrainType.DENSE_FOREST, BattleCellData.TerrainType.MOUNTAIN]:
					return false
	return true


# ============================================================================
# 反击 (Retaliation)
# ============================================================================

## 获取反击伤害倍率
## 正常反击: 50%; 防御模式: 100%; 被包夹时不可反击
static func get_counter_attack_multiplier(defender: Unit, attacker_pos: Vector2i) -> float:
	# 被包夹（侧翼/背后）时不可反击
	var flank = get_flanking_bonus(attacker_pos, defender)
	if not flank["can_counter"]:
		return 0.0
	# 本回合已用过反击
	if defender.data.counter_used_this_turn:
		return 0.0
	# 防御模式: 100%伤害
	if defender.data.is_defending:
		return 1.0
	return 0.5
