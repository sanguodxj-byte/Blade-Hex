# AISpatialAnalyzer.gd
# 空间分析工具类 —— 为 AI 提供地形、包夹、视线、冲锋等静态分析能力
# 所有方法均为 static，无状态
class_name AISpatialAnalyzer

## 获取攻击方向关系：0=正面, 1=侧翼, 2=背后
## attacker_pos: 攻击者位置, target_pos: 目标位置, target_facing: 目标朝向(0-5)，-1表示未知
static func get_attack_facing(attacker_pos: Vector2i, target_pos: Vector2i, target_facing: int) -> int:
	if target_facing < 0:
		# 未知朝向时，基于位置关系估算（简化：用距离最近的方向判断）
		return 0  # 默认当作正面
	
	# 计算攻击者相对于目标的方向
	var dq := attacker_pos.x - target_pos.x
	var dr := attacker_pos.y - target_pos.y
	var attack_dir := -1
	
	# 匹配6个方向
	var dirs = [
		Vector2i(1, 0), Vector2i(1, -1), Vector2i(0, -1),
		Vector2i(-1, 0), Vector2i(-1, 1), Vector2i(0, 1)
	]
	
	for i in range(6):
		if dirs[i] == Vector2i(dq, dr):
			attack_dir = i
			break
	
	if attack_dir < 0:
		return 0  # 不相邻或无法判断，当作正面
	
	# 朝向系统：正面3格、侧翼2格、背后1格
	# target_facing 是目标面对的方向，正面3格 = facing, (facing+1)%6, (facing+5)%6
	var front_dirs = [target_facing, (target_facing + 1) % 6, (target_facing + 5) % 6]
	var flank_dirs = [(target_facing + 2) % 6, (target_facing + 4) % 6]
	var back_dir = (target_facing + 3) % 6
	
	if attack_dir == back_dir:
		return 2  # 背后
	elif attack_dir in flank_dirs:
		return 1  # 侧翼
	else:
		return 0  # 正面


## 获取两个位置间的高程优势
## 返回: 1=攻击者在高处, -1=攻击者在低处, 0=同高程
static func get_elevation_advantage(hex_grid: HexGrid, from_pos: Vector2i, to_pos: Vector2i) -> int:
	var from_cell = hex_grid.get_cell(from_pos.x, from_pos.y)
	var to_cell = hex_grid.get_cell(to_pos.x, to_pos.y)
	if not from_cell or not to_cell:
		return 0
	if from_cell.elevation > to_cell.elevation:
		return 1
	elif from_cell.elevation < to_cell.elevation:
		return -1
	return 0


## 检查冲锋是否有效（移动3格以上，非沙地/沼泽）
static func can_charge(hex_grid: HexGrid, path: Array, _start_pos: Vector2i):
	if path.size() < 3:
		return false  # 至少移动3格
	
	# 检查路径上的地形是否允许冲锋
	for pos in path:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell:
			continue
		# 沙地和沼泽不可冲锋
		if cell.data and cell.data is BattleCellData:
			var terrain: BattleCellData = cell.data
			if terrain.terrain_type == BattleCellData.TerrainType.SAND:
				return false
			if terrain.terrain_type == BattleCellData.TerrainType.SWAMP:
				return false
	return true


## 评估一个位置的防御价值（掩体 + 高程 + 逃生路线）
## 返回 0.0 ~ 10.0 的评分
static func evaluate_position_defense(hex_grid: HexGrid, pos: Vector2i, threats: Array) -> float:
	var cell = hex_grid.get_cell(pos.x, pos.y)
	if not cell:
		return 0.0
	
	var score := 0.0
	
	# 掩体加成
	score += cell.cover_type * 2.0
	
	# AC加成（地形提供）
	if cell.data and cell.data is BattleCellData:
		score += max(0, cell.data.ac_bonus)
	
	# 高程优势（高地比周围的威胁更高时加分）
	var pos_elev: int = cell.elevation
	for threat in threats:
		if not is_instance_valid(threat) or not threat is Unit:
			continue
		var threat_cell = hex_grid.get_cell(threat.grid_pos.x, threat.grid_pos.y)
		if threat_cell and pos_elev > threat_cell.elevation:
			score += 1.5  # 对每个低处威胁有高程优势
		elif threat_cell and pos_elev < threat_cell.elevation:
			score -= 1.0  # 被俯视不利
	
	# 逃生路线（相邻可通行格数量）
	var escape_routes := 0
	for dir in range(6):
		var nb = HexUtils.get_neighbor(pos.x, pos.y, dir)
		var nb_cell = hex_grid.get_cell(nb.x, nb.y)
		if nb_cell and nb_cell.occupant == null:
			escape_routes += 1
	score += min(escape_routes, 4) * 0.5  # 最多加2分
	
	return clampf(score, 0.0, 10.0)


## 寻找最佳掩体射击位置（在移动范围内，能攻击到目标，且有掩体）
## 返回 Array of {position: Vector2i, defense_score: float}
static func find_cover_positions(hex_grid: HexGrid, unit: Unit, target: Unit, move_range: int) -> Array:
	var results: Array = []
	var weapon: WeaponData = unit.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var reachable = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, move_range)
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null and cell.occupant != unit: continue
		
		var dist = HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist > atk_range: continue
		
		var def_score = evaluate_position_defense(hex_grid, pos, [target])
		results.append({"position": pos, "defense_score": def_score})
	
	# 按防御评分降序排列
	results.sort_custom(func(a, b): return a["defense_score"] > b["defense_score"])
	return results


## 寻找包夹位置（在移动范围内，能攻击到目标的侧翼/背后）
## 返回 Array of {position: Vector2i, facing: int} (facing: 0=正面, 1=侧翼, 2=背后)
static func find_flanking_positions(hex_grid: HexGrid, target: Unit, attacker: Unit, move_range: int) -> Array:
	var results: Array = []
	var weapon: WeaponData = attacker.get_main_hand()
	var atk_range := weapon.range_cells if weapon else 1
	var reachable = hex_grid.get_cells_in_range(attacker.grid_pos.x, attacker.grid_pos.y, move_range)
	var target_facing := -1  # 当前原型没有 facing 字段，暂用 -1
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null and cell.occupant != attacker: continue
		
		var dist = HexUtils.distance(pos.x, pos.y, target.grid_pos.x, target.grid_pos.y)
		if dist > atk_range: continue
		
		var facing = get_attack_facing(pos, target.grid_pos, target_facing)
		if facing > 0:  # 侧翼或背后
			results.append({"position": pos, "facing": facing})
	
	# 背后优先
	results.sort_custom(func(a, b): return a["facing"] > b["facing"])
	return results


## 计算目标相邻的友方数量（用于夹击加成评估）
static func count_adjacent_allies(hex_grid: HexGrid, target_pos: Vector2i, ally_units: Array) -> int:
	var count := 0
	for dir in range(6):
		var nb = HexUtils.get_neighbor(target_pos.x, target_pos.y, dir)
		var cell = hex_grid.get_cell(nb.x, nb.y)
		if cell and cell.occupant:
			for ally in ally_units:
				if is_instance_valid(ally) and ally is Unit and cell.occupant == ally:
					count += 1
					break
	return count


## 计算沿路径移动会触发多少次借机攻击
static func count_opportunity_attacks(hex_grid: HexGrid, path: Array[Vector2i], enemy_units: Array) -> int:
	var count := 0
	for pos in path:
		# 检查这个位置周围的敌方近战单位
		for dir in range(6):
			var nb = HexUtils.get_neighbor(pos.x, pos.y, dir)
			var cell = hex_grid.get_cell(nb.x, nb.y)
			if not cell or not cell.occupant: continue
			for enemy in enemy_units:
				if is_instance_valid(enemy) and enemy is Unit and cell.occupant == enemy:
					var weapon: WeaponData = enemy.get_main_hand()
					if weapon == null or not weapon.is_ranged:
						count += 1
						break
	return count


## 寻找最近的撤退位置（地图边缘或远离敌人的位置）
static func find_retreat_position(hex_grid: HexGrid, unit: Unit, player_units: Array) -> Vector2i:
	var best_pos := unit.grid_pos
	var best_score := -999.0
	
	var reachable = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, unit.get_move_range())
	
	for pos in reachable:
		var cell = hex_grid.get_cell(pos.x, pos.y)
		if not cell: continue
		if cell.occupant != null: continue
		
		var score := 0.0
		
		# 远离所有玩家单位的总距离（越大越好）
		var total_dist := 0
		for pu in player_units:
			if is_instance_valid(pu) and pu is Unit:
				total_dist += HexUtils.distance(pos.x, pos.y, pu.grid_pos.x, pu.grid_pos.y)
		score += total_dist * 1.0
		
		# 掩体加分
		score += cell.cover_type * 2.0
		
		if score > best_score:
			best_score = score
			best_pos = pos
	
	return best_pos


## 获取考虑高程的有效射程
static func get_effective_range(hex_grid: HexGrid, attacker: Unit, from_pos: Vector2i, target_pos: Vector2i) -> int:
	var weapon: WeaponData = attacker.get_main_hand()
	var base_range := weapon.range_cells if weapon else 1
	var elev = get_elevation_advantage(hex_grid, from_pos, target_pos)
	
	if elev > 0:
		# 高地射击：射程+1（高→平）或 +2（高→低）
		var target_cell = hex_grid.get_cell(target_pos.x, target_pos.y)
		if target_cell and target_cell.elevation == 0:
			base_range += 2
		else:
			base_range += 1
	elif elev < 0:
		base_range = max(1, base_range - 1)
	
	return base_range
