# QuickCombatScene.gd
# 快速战斗场景 — 从主菜单直接进入，随机生成战斗地图与敌我单位
# 基于 CombatScene，复用全部战斗系统（CombatManager/AIController/CombatUI）
extends Node3D
class_name QuickCombatScene

signal combat_finished(victory: bool)

var hex_grid: HexGrid
var combat_manager: CombatManager
var camera: Camera3D
var combat_ui: CombatUI
var ai_controller: AIController

var current_action_mode: String = "none" # "none", "move", "attack"
var active_player_unit: Unit = null
var highlighted_cells: Array[HexCell] = []

## 生成的地图宽度（用于确定敌人放置位置）
var _map_width: int = 12
var _map_height: int = 10

func _ready():
	seed(randi())
	_init_environment()
	_init_systems()
	_generate_battlefield()
	_spawn_units()
	
	combat_manager.start_combat()

func _init_environment():
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 700.0
	camera.rotation_degrees = Vector3(-45, 0, 0)
	camera.position = Vector3(600, 800, 1000)
	add_child(camera)
	
	var light = DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-60, 30, 0)
	light.shadow_enabled = true
	add_child(light)

func _init_systems():
	hex_grid = HexGrid.new()
	hex_grid.name = "HexGrid"
	add_child(hex_grid)
	
	combat_manager = CombatManager.new()
	combat_manager.name = "CombatManager"
	combat_manager.turn_started.connect(_on_turn_started)
	combat_manager.combat_ended.connect(_on_combat_ended)
	add_child(combat_manager)
	
	combat_ui = CombatUI.new()
	add_child(combat_ui)
	combat_ui.action_selected.connect(_on_action_selected)
	combat_ui.spell_selected.connect(_on_spell_selected)
	
	ai_controller = AIController.new(combat_manager.get_difficulty_config())
	ai_controller.name = "AIController"
	ai_controller.set_combat_scene(self)
	add_child(ai_controller)

# ============================================================
# 随机地图生成
# ============================================================

func _generate_battlefield():
	var generator = BattleMapGenerator.new()
	var template_names = generator.get_template_names()
	var chosen_template = template_names[randi() % template_names.size()]
	var map_data = generator.generate_from_template(
		chosen_template,
		BattleMapGenerator.BattleSize.MERCENARY,
		randi()
	)
	
	_map_width = map_data.width
	_map_height = map_data.height
	
	hex_grid.load_from_map_data(map_data)
	
	# 重新定位摄像机到地图中心
	var center_q = _map_width / 2.0
	var center_r = _map_height / 2.0
	camera.position = Vector3(center_q * 100.0, 800, center_r * 100.0 + 500)
	
	# 连接单元格事件
	for cell_pos in hex_grid.cells:
		var cell: HexCell = hex_grid.cells[cell_pos]
		cell.cell_clicked.connect(_on_cell_clicked)
		cell.cell_mouse_entered.connect(_on_cell_hover)
		cell.cell_mouse_exited.connect(_on_cell_hover_exit)

# ============================================================
# 随机单位生成
# ============================================================

func _spawn_units():
	# === 玩家方：1-3 个随机角色 ===
	var player_count = randi_range(1, 3)
	var player_units: Array[Unit] = []
	
	for i in range(player_count):
		var unit_data = CharacterGenerator.generate_character()
		var player_unit = Unit.new()
		player_unit.data = unit_data
		
		# 装备基础武器
		_assign_random_equipment(unit_data, true)
		
		player_unit.name = "Player_%d" % i
		var pos = _find_player_deploy_pos(i, player_count)
		_place_unit_at(player_unit, pos.x, pos.y)
		combat_manager.register_unit(player_unit, true)
		player_unit.init_dr()  # 初始化装甲耐久
		player_units.append(player_unit)
	
	active_player_unit = player_units[0]
	
	# === 敌方：2-5 个随机敌人 ===
	var enemy_count = randi_range(2, 5)
	var enemy_squad = EnemyGenerator.generate_encounter(1, player_count, 1.0)
	
	# 如果遭遇生成失败或数量不够，手动补充
	if enemy_squad.size() < enemy_count:
		for i in range(enemy_count - enemy_squad.size()):
			var extra = EnemyGenerator.generate_random_enemy(0.25, 1.0)
			if extra:
				enemy_squad.append(extra)
	
	# 如果敌人太多则截断
	if enemy_squad.size() > enemy_count:
		enemy_squad = enemy_squad.slice(0, enemy_count)
	
	for i in range(enemy_squad.size()):
		var enemy_data = enemy_squad[i]
		if enemy_data == null:
			continue
		
		# 确保敌人属性完整
		if enemy_data.unit_name == "":
			enemy_data.unit_name = "敌人_%d" % i
		enemy_data.is_enemy = true
		
		# 为类人敌人装备随机武器
		if enemy_data.primary_main_hand == null:
			_assign_random_equipment(enemy_data, false)
		
		var enemy_unit = Unit.new()
		enemy_unit.data = enemy_data
		enemy_unit.name = "Enemy_%d" % i
		
		var pos = _find_enemy_deploy_pos(i, enemy_squad.size())
		_place_unit_at(enemy_unit, pos.x, pos.y)
		combat_manager.register_unit(enemy_unit, false)
		combat_ui.register_enemy(enemy_unit)
		enemy_unit.init_dr()  # 初始化装甲耐久
	
	# 初始化视野迷雾
	_update_fov()

## 为单位分配随机基础装备
func _assign_random_equipment(unit_data: UnitData, is_player: bool):
	# 主武器
	var weapon_roll = randf()
	if weapon_roll < 0.4:
		# 近战：长剑
		var sword = WeaponData.new()
		sword.item_name = "长剑"
		sword.damage_dice_count = 1
		sword.damage_dice_sides = 8
		sword.damage_type = WeaponData.DamageType.SLASH
		unit_data.primary_main_hand = sword
	elif weapon_roll < 0.7:
		# 近战：巨斧
		var axe = WeaponData.new()
		axe.item_name = "巨斧"
		axe.damage_dice_count = 1
		axe.damage_dice_sides = 12
		axe.damage_type = WeaponData.DamageType.SLASH
		unit_data.primary_main_hand = axe
	else:
		# 远程：长弓
		var bow = WeaponData.new()
		bow.item_name = "长弓"
		bow.is_ranged = true
		bow.range_cells = 6
		bow.damage_dice_count = 1
		bow.damage_dice_sides = 8
		bow.damage_type = WeaponData.DamageType.PIERCE
		unit_data.primary_main_hand = bow
	
	# 副武器（50%概率）
	if randf() < 0.5:
		var backup = WeaponData.new()
		if unit_data.primary_main_hand and unit_data.primary_main_hand.is_ranged:
			backup.item_name = "短剑"
			backup.damage_dice_count = 1
			backup.damage_dice_sides = 6
			backup.damage_type = WeaponData.DamageType.PIERCE
			backup.is_finesse = true
		else:
			backup.item_name = "短弓"
			backup.is_ranged = true
			backup.range_cells = 5
			backup.damage_dice_count = 1
			backup.damage_dice_sides = 6
			backup.damage_type = WeaponData.DamageType.PIERCE
		unit_data.secondary_main_hand = backup
	
	# 护甲（玩家给链甲，敌人概率给）
	if is_player or randf() < 0.6:
		var armor = ArmorData.new()
		armor.item_name = "链甲" if is_player else "皮甲"
		armor.armor_type = ArmorData.ArmorType.MEDIUM if is_player else ArmorData.ArmorType.LIGHT
		armor.ac_bonus = 4 if is_player else 2
		armor.max_dex_bonus = 2 if is_player else 3
		unit_data.armor = armor

## 寻找玩家部署位置（地图左侧）
func _find_player_deploy_pos(index: int, total: int) -> Vector2i:
	var q = randi_range(1, 3)
	var r_step = max(1, _map_height / (total + 1))
	var r = r_step * (index + 1) - r_step / 2
	r = clampi(r, 0, _map_height - 1)
	
	# 确保目标格子可通行
	for attempt in range(20):
		var cell = hex_grid.get_cell(q, r)
		if cell and cell.occupant == null and (cell.data == null or cell.data.is_passable):
			return Vector2i(q, r)
		q = randi_range(1, 3)
		r = randi_range(0, _map_height - 1)
	
	return Vector2i(2, index * 2)

## 寻找敌人部署位置（地图右侧）
func _find_enemy_deploy_pos(index: int, total: int) -> Vector2i:
	var max_q = _map_width - 1
	var q = randi_range(max(1, max_q - 3), max_q)
	var r_step = max(1, _map_height / (total + 1))
	var r = r_step * (index + 1) - r_step / 2
	r = clampi(r, 0, _map_height - 1)
	
	for attempt in range(20):
		var cell = hex_grid.get_cell(q, r)
		if cell and cell.occupant == null and (cell.data == null or cell.data.is_passable):
			return Vector2i(q, r)
		q = randi_range(max(1, max_q - 3), max_q)
		r = randi_range(0, _map_height - 1)
	
	return Vector2i(max_q - 1, index * 2)

# ============================================================
# 放置与移动
# ============================================================

func _place_unit_at(unit: Unit, q: int, r: int):
	var cell: HexCell = hex_grid.get_cell(q, r)
	if cell and cell.occupant == null:
		add_child(unit)
		var hex_height = HexUtils.SIZE * 0.5
		unit.position = cell.position + Vector3(0, hex_height / 2.0, 0)
		unit.grid_pos = Vector2i(q, r)
		cell.occupant = unit

func _move_unit_to(unit: Unit, q: int, r: int):
	var old_cell = hex_grid.get_cell(unit.grid_pos.x, unit.grid_pos.y)
	if old_cell: old_cell.occupant = null
	
	var new_cell = hex_grid.get_cell(q, r)
	if new_cell:
		new_cell.occupant = unit
		unit.grid_pos = Vector2i(q, r)
		var hex_height = HexUtils.SIZE * 0.5
		var target_pos = new_cell.position + Vector3(0, hex_height / 2.0, 0)
		
		# 平滑移动动画
		var tween = create_tween()
		tween.tween_property(unit, "position", target_pos, 0.3)\
			.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		await tween.finished
	
	if unit == active_player_unit:
		_update_fov()

# ============================================================
# 战争迷雾
# ============================================================

func _update_fov():
	var vision_range = 5
	if not is_instance_valid(active_player_unit): return
	
	var visible_coords = hex_grid.get_cells_in_range(
		active_player_unit.grid_pos.x, active_player_unit.grid_pos.y, vision_range)
	
	for pos in hex_grid.cells:
		var cell = hex_grid.cells[pos]
		cell.set_shrouded(true)
	
	for enemy in combat_manager.enemy_units:
		if is_instance_valid(enemy):
			enemy.visible = false
	
	for coord in visible_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_shrouded(false)
			if cell.occupant and cell.occupant != active_player_unit \
				and combat_manager.enemy_units.has(cell.occupant):
				cell.occupant.visible = true

# ============================================================
# 高亮系统
# ============================================================

func _clear_highlights():
	for cell in highlighted_cells:
		cell.set_highlight(false)
	highlighted_cells.clear()

func _highlight_move_range(unit: Unit):
	_clear_highlights()
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, 4)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell and cell.occupant == null:
			cell.set_highlight(true, Color(0.2, 0.6, 1.0, 0.4))
			highlighted_cells.append(cell)

func _highlight_attack_range(unit: Unit):
	_clear_highlights()
	var weapon = unit.get_main_hand()
	var atk_range = weapon.range_cells if weapon else 1
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, atk_range)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(1.0, 0.2, 0.2, 0.4))
			highlighted_cells.append(cell)

func _highlight_item_range(unit: Unit):
	_clear_highlights()
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, 1)
	range_coords.append(unit.grid_pos)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(0.2, 0.9, 0.4, 0.4))
			highlighted_cells.append(cell)

func _highlight_spell_range(unit: Unit, spell: SpellData):
	_clear_highlights()
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, spell.range_cells)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(1.0, 0.5, 0.0, 0.4))
			highlighted_cells.append(cell)

# ============================================================
# 流程与事件处理
# ============================================================

func _on_turn_started(state: int):
	current_action_mode = "none"
	_clear_highlights()
	
	if state == CombatManager.CombatState.PLAYER_TURN:
		combat_ui.set_turn_text("=== 玩家回合 ===", Color(0.2, 0.6, 1.0))
		combat_ui.set_action_bar_visible(true)
		combat_ui.update_unit_info(active_player_unit)
		combat_ui.log_message("轮到玩家行动。")
	elif state == CombatManager.CombatState.ENEMY_TURN:
		combat_ui.set_turn_text("=== 敌方回合 ===", Color(1.0, 0.3, 0.3))
		combat_ui.set_action_bar_visible(false)
		combat_ui.log_message("敌方行动中...")
		_execute_ai_turn()

func _execute_ai_turn():
	await get_tree().create_timer(0.3).timeout
	
	if not is_instance_valid(active_player_unit) or active_player_unit.current_hp <= 0:
		# 检查是否还有存活的玩家单位
		var alive = false
		for p in combat_manager.player_units:
			if is_instance_valid(p) and p.current_hp > 0:
				alive = true
				active_player_unit = p
				break
		if not alive:
			combat_manager.end_current_turn()
			return
	
	var alive_enemies: Array[Unit] = []
	for e in combat_manager.enemy_units:
		if is_instance_valid(e) and e.current_hp > 0:
			alive_enemies.append(e)
	
	var alive_players: Array[Unit] = []
	for p in combat_manager.player_units:
		if is_instance_valid(p) and p.current_hp > 0:
			alive_players.append(p)
	
	ai_controller.all_actions_completed.connect(
		func(): combat_manager.end_current_turn(),
		CONNECT_ONE_SHOT
	)
	
	ai_controller.execute_enemy_turn(alive_enemies, alive_players, hex_grid, combat_ui)

var _combat_ended := false

func _on_combat_ended(victory: bool):
	if _combat_ended:
		return
	_combat_ended = true
	
	_clear_highlights()
	
	if victory:
		combat_ui.set_turn_text("战斗胜利！", Color.GREEN)
	else:
		combat_ui.set_turn_text("战斗失败，全军覆没！", Color.RED)
	
	combat_ui.set_action_bar_visible(false)
	
	await get_tree().create_timer(1.5).timeout
	
	# 返回主菜单
	get_tree().change_scene_to_file("res://src/ui/main_menu/main_menu.tscn")

# ============================================================
# 行动选择
# ============================================================

func _on_action_selected(action: String):
	current_action_mode = action
	_clear_highlights()
	
	if action == "retreat":
		combat_ui.log_message("队伍选择了撤退...")
		_on_combat_ended(false)
		return
	
	if not is_instance_valid(active_player_unit):
		return
	
	if action == "swap_weapon":
		active_player_unit.switch_weapon_set()
		combat_ui.update_unit_info(active_player_unit)
		var weapon = active_player_unit.get_main_hand()
		combat_ui.log_message("切换武器！当前武器为：【%s】。" % [weapon.item_name if weapon else "徒手"])
		current_action_mode = "none"
	elif action == "move":
		if active_player_unit.has_moved:
			combat_ui.log_message("本回合已移动过。")
			current_action_mode = "none"
		else:
			combat_ui.log_message("选择移动：请点击蓝色高亮空地。")
			_highlight_move_range(active_player_unit)
	elif action == "attack":
		if active_player_unit.has_acted:
			combat_ui.log_message("本回合已行动过。")
			current_action_mode = "none"
		else:
			var weapon = active_player_unit.get_main_hand()
			combat_ui.log_message("选择攻击：当前武器【%s】(射程 %d)。请点击红色高亮敌人。" % [weapon.item_name if weapon else "徒手", weapon.range_cells if weapon else 1])
			_highlight_attack_range(active_player_unit)
	elif action == "spell":
		if active_player_unit.has_acted:
			combat_ui.log_message("本回合已行动过。")
			current_action_mode = "none"
		elif active_player_unit.data.known_spells.is_empty():
			combat_ui.log_message("未学习任何法术。")
			current_action_mode = "none"
		else:
			combat_ui.log_message("打开法术选择面板...")
			var spell_mgr = SpellManager.new()
			add_child(spell_mgr)
			combat_ui.open_spell_panel(active_player_unit, spell_mgr)
	elif action == "item":
		if active_player_unit.has_acted:
			combat_ui.log_message("本回合已行动过。")
			current_action_mode = "none"
		elif active_player_unit.data.consumables.is_empty():
			combat_ui.log_message("背包中没有消耗品。")
			current_action_mode = "none"
		else:
			current_action_mode = "item"
			combat_ui.log_message("选择物品：请点击相邻的友方单位或自身使用药水。")
			_highlight_item_range(active_player_unit)
	elif action == "defend":
		if active_player_unit.data:
			active_player_unit.data.is_defending = true
			combat_ui.log_message("[color=cyan]进入防御模式！[/color] AC+2，免疫包夹。")
			active_player_unit.has_acted = true
			current_action_mode = "none"
	elif action == "skill":
		_on_action_selected("spell")
	elif action == "end_turn":
		combat_ui.log_message("玩家结束回合。")
		current_action_mode = "none"
		combat_manager.end_current_turn()

# ============================================================
# 格子点击
# ============================================================

func _on_cell_clicked(cell: HexCell):
	if combat_manager.current_state != CombatManager.CombatState.PLAYER_TURN:
		return
	
	# 点击友方单位切换选中
	if cell.occupant and cell.occupant != active_player_unit:
		if combat_manager.player_units.has(cell.occupant) and cell.occupant.current_hp > 0:
			_clear_highlights()
			current_action_mode = "none"
			active_player_unit = cell.occupant
			combat_ui.update_unit_info(active_player_unit)
			combat_ui.log_message("选中 %s。" % active_player_unit.data.unit_name)
			_update_fov()
			return
	
	if not is_instance_valid(active_player_unit):
		return
	
	if current_action_mode == "move":
		if highlighted_cells.has(cell) and cell.occupant == null:
			_move_unit_to(active_player_unit, cell.grid_pos.x, cell.grid_pos.y)
			combat_ui.log_message("玩家移动到 " + str(cell.grid_pos))
			active_player_unit.has_moved = true
			current_action_mode = "none"
			_clear_highlights()
		else:
			combat_ui.log_message("无法移动到该目标点。")
	
	elif current_action_mode == "attack":
		if highlighted_cells.has(cell) and cell.occupant != null and cell.occupant != active_player_unit:
			var target = cell.occupant
			
			active_player_unit.play_anim("attack")
			await get_tree().create_timer(0.6).timeout
			
			var result = CombatResolver.resolve_attack(active_player_unit, target, hex_grid)
			
			if result["hit"]:
				var dmg = result["damage"]
				var crit_msg = " [color=yellow]暴击！[/color]" if result["critical"] else ""
				var flank_msg = ""
				if result.get("is_flanking", false):
					flank_msg = " [包夹]"
				
				var weapon = active_player_unit.get_main_hand()
				combat_ui.log_message("[color=green]命中！[/color]%s%s 使用 %s 造成 %d 伤害。" % [
					crit_msg, flank_msg,
					weapon.item_name if weapon else "徒手", dmg
				])
				combat_ui.update_enemy_info(target)
				if target.current_hp <= 0:
					combat_ui.log_message("[color=yellow]%s 被击败！[/color]" % target.data.unit_name)
					combat_ui.remove_enemy(target)
					cell.occupant = null
			else:
				if result["fumble"]:
					combat_ui.log_message("[color=red]严重失误！[/color]")
				else:
					combat_ui.log_message("[color=red]未命中！[/color] (命中率 %d%%)" % [result["hit_chance_percent"]])
			
			active_player_unit.play_anim("default")
			active_player_unit.has_acted = true
			current_action_mode = "none"
			_clear_highlights()
		else:
			combat_ui.log_message("无效的攻击目标。")
	
	elif current_action_mode == "spell":
		if _selected_spell and highlighted_cells.has(cell):
			active_player_unit.play_anim("attack")
			await get_tree().create_timer(0.6).timeout
			
			var spell_mgr = SpellManager.new()
			add_child(spell_mgr)
			
			VFXManager.play_explosion_effect(self, cell.global_position)
			
			var result = spell_mgr.cast_spell(active_player_unit, _selected_spell, cell.grid_pos, hex_grid)
			if result["success"]:
				for r in result["results"]:
					if r.get("hit", false):
						if r.get("healed", false):
							combat_ui.log_message("[color=cyan]%s 被治疗了 %d HP。[/color]" % [r["target"].data.unit_name, r["amount"]])
						else:
							combat_ui.log_message("[color=orange]法术命中 %s！造成 %d 伤害。[/color]" % [r["target"].data.unit_name, r.get("damage", 0)])
							combat_ui.update_enemy_info(r["target"])
							if r["target"].current_hp <= 0:
								combat_ui.log_message("[color=yellow]%s 被击败！[/color]" % r["target"].data.unit_name)
								combat_ui.remove_enemy(r["target"])
								var tcell = hex_grid.get_cell(r["target"].grid_pos.x, r["target"].grid_pos.y)
								if tcell: tcell.occupant = null
					else:
						combat_ui.log_message("[color=red]法术未命中 %s。[/color]" % [r["target"].data.unit_name if r.get("target") else "目标"])
				combat_ui.log_message("[color=orange]释放【%s】。[/color]" % _selected_spell.spell_name)
			else:
				combat_ui.log_message("[color=red]施法失败：%s[/color]" % result.get("reason", "未知原因"))
			
			active_player_unit.play_anim("default")
			_selected_spell = null
			active_player_unit.has_acted = true
			current_action_mode = "none"
			_clear_highlights()
		else:
			combat_ui.log_message("目标点不在射程内。")
			_selected_spell = null
			current_action_mode = "none"
			_clear_highlights()
	
	elif current_action_mode == "skill":
		pass
	
	elif current_action_mode == "item":
		if highlighted_cells.has(cell):
			if cell.occupant and not cell.occupant.data.is_enemy:
				var target = cell.occupant
				var potions = active_player_unit.data.consumables.filter(
					func(c): return c.consumable_type == ConsumableData.ConsumableType.HEALING_POTION
				)
				if not potions.is_empty():
					var potion = potions[0]
					var result = ConsumableManager.use_consumable(target, potion, cell.grid_pos, hex_grid)
					if result["success"]:
						combat_ui.log_message("[color=green]%s 使用了%s，恢复 %d HP。[/color]" % [
							target.data.unit_name, potion.item_name, result["amount"]])
						combat_ui.update_unit_info(target)
						active_player_unit.has_acted = true
					else:
						combat_ui.log_message("使用失败。")
				else:
					combat_ui.log_message("没有可用的治疗药水。")
				current_action_mode = "none"
				_clear_highlights()
			else:
				combat_ui.log_message("无效的目标。")

# ============================================================
# 法术选择回调
# ============================================================

func _on_spell_selected(spell: SpellData):
	if not is_instance_valid(active_player_unit): return
	combat_ui.close_spell_panel()
	combat_ui.log_message("[color=orange]选择法术：%s[/color] — 请点击射程内的目标。" % spell.spell_name)
	current_action_mode = "spell"
	_highlight_spell_range(active_player_unit, spell)
	_selected_spell = spell

var _selected_spell: SpellData = null

# ============================================================
# 视角控制
# ============================================================

func _unhandled_input(event):
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			camera.size = clamp(camera.size * 0.9, 300.0, 2000.0)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			camera.size = clamp(camera.size * 1.1, 300.0, 2000.0)

func _process(delta):
	if not camera: return
	
	var cam_speed = 800.0 * delta * (camera.size / 1000.0)
	var move_vec = Vector3.ZERO
	
	if Input.is_key_pressed(KEY_W): move_vec.z -= 1
	if Input.is_key_pressed(KEY_S): move_vec.z += 1
	if Input.is_key_pressed(KEY_A): move_vec.x -= 1
	if Input.is_key_pressed(KEY_D): move_vec.x += 1
	
	if move_vec.length() > 0:
		camera.position += move_vec.normalized() * cam_speed

# ============================================================
# 悬停预览（攻击模式下显示命中率/伤害）
# ============================================================

func _on_cell_hover(cell: HexCell):
	if combat_manager.current_state != CombatManager.CombatState.PLAYER_TURN:
		return
	if current_action_mode != "attack" and current_action_mode != "spell":
		return
	if not is_instance_valid(active_player_unit):
		return
	if not cell.occupant or cell.occupant == active_player_unit:
		return
	if not combat_manager.enemy_units.has(cell.occupant):
		return
	
	var target = cell.occupant
	var mouse_pos = get_viewport().get_mouse_position()
	combat_ui.show_hit_preview(mouse_pos, active_player_unit, target)

func _on_cell_hover_exit(cell: HexCell):
	combat_ui.hide_hit_preview()
