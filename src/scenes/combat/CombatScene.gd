# CombatScene.gd
# 战术核心入口脚本 (正式版本)
extends Node3D
class_name CombatScene

signal combat_finished(victory: bool)

var hex_grid: HexGrid
var combat_manager: CombatManager
var camera: Camera3D
var combat_ui: CombatUI
var ai_controller: AIController

var current_action_mode: String = "none" # "none", "move", "attack"
var active_player_unit: Unit = null
var highlighted_cells: Array[HexCell] = []

func _ready():
	_init_environment()
	_init_systems()
	_generate_battlefield()
	_spawn_units()
	
	_play_combat_music()
	
	combat_manager.start_combat()

func _play_combat_music():
	var total_threat = 0.0
	for enemy in combat_manager.enemy_units:
		if is_instance_valid(enemy) and enemy.data:
			total_threat += enemy.data.threat_level
			
	# 根据敌人总威胁度（CR）决定播放哪种战斗曲
	if total_threat >= 3.0:
		# 强力敌人/Boss战曲目
		AudioManager.play_scenario_bgm(AudioManager.Scenario.COMBAT, "boss", 1.0)
	else:
		# 常规战斗曲目
		AudioManager.play_scenario_bgm(AudioManager.Scenario.COMBAT, "normal", 1.0)

func _init_environment():
	# 建立 3D 摄像机
	camera = Camera3D.new()
	# 设置为正交相机 (更符合传统战棋感)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	# 【缩小视野拉近镜头】默认视野拉近到 700 (可以通过滚轮再次缩放)
	camera.size = 700.0 
	
	# 《战场兄弟》风格：纯正面俯视，无 Y 轴偏转。
	# X轴向下看45度，营造俯视感；纸片人在正交+广告牌模式下会完美正对屏幕
	camera.rotation_degrees = Vector3(-45, 0, 0)
	
	# 将摄像机中心对准战术网格中心 (大致是 X=600, Z=500 附近)
	camera.position = Vector3(600, 800, 1000)
	add_child(camera)
	
	# 添加定向光
	var light = DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-60, 30, 0)
	light.shadow_enabled = true
	add_child(light)

func _init_systems():
	# 初始化网格
	hex_grid = HexGrid.new()
	hex_grid.name = "HexGrid"
	add_child(hex_grid)
	
	# 初始化战斗管理器
	combat_manager = CombatManager.new()
	combat_manager.name = "CombatManager"
	combat_manager.turn_started.connect(_on_turn_started)
	combat_manager.combat_ended.connect(_on_combat_ended)
	add_child(combat_manager)
	
	# 初始化 UI
	combat_ui = CombatUI.new()
	add_child(combat_ui)
	combat_ui.action_selected.connect(_on_action_selected)
	combat_ui.spell_selected.connect(_on_spell_selected)
	
	# 初始化 AI 控制器（默认普通难度）
	ai_controller = AIController.new(combat_manager.get_difficulty_config())
	ai_controller.name = "AIController"
	ai_controller.set_combat_scene(self)
	add_child(ai_controller)

var _map_data: BattleMapGenerator.BattleMapData

func _generate_battlefield():
	var generator = BattleMapGenerator.new()
	var template_names = generator.get_template_names()
	var random_template = template_names[randi() % template_names.size()]
	_map_data = generator.generate_from_template(random_template, BattleMapGenerator.BattleSize.MERCENARY)
	
	hex_grid.load_from_map_data(_map_data)
	
	# 连接单元格事件
	for cell_pos in hex_grid.cells:
		var cell: HexCell = hex_grid.cells[cell_pos]
		cell.cell_clicked.connect(_on_cell_clicked)
		cell.cell_mouse_entered.connect(_on_cell_hover)
		cell.cell_mouse_exited.connect(_on_cell_hover_exit)

func _spawn_units():
	var p_deploy = _map_data.player_deployment.duplicate()
	var e_deploy = _map_data.enemy_deployment.duplicate()
	p_deploy.shuffle()
	e_deploy.shuffle()
	
	# === 创建玩家单位 ===
	var player_unit = Unit.new()
	var player_data = UnitData.new()
	player_data.unit_name = "战士"
	
	# 设置基础六维 (高力量，中等敏捷)
	player_data.str = 16
	player_data.dex = 14
	player_data.con = 15
	player_data.base_max_hp = 10
	player_data.base_ac = 10
	
	# 装备：链甲甲 (AC+4, 最大敏捷+2)
	var chain_mail = ArmorData.new()
	chain_mail.item_name = "链甲"
	chain_mail.armor_type = ArmorData.ArmorType.MEDIUM
	chain_mail.ac_bonus = 4
	chain_mail.max_dex_bonus = 2
	player_data.armor = chain_mail
	
	# 主武器：长剑 (1d8 挥砍)
	var longsword = WeaponData.new()
	longsword.item_name = "长剑"
	longsword.damage_dice_count = 1
	longsword.damage_dice_sides = 8
	longsword.damage_type = WeaponData.DamageType.SLASH
	player_data.primary_main_hand = longsword
	
	# 副武器：长弓 (1d8 穿刺，远程)
	var longbow = WeaponData.new()
	longbow.item_name = "长弓"
	longbow.is_ranged = true
	longbow.range_cells = 6
	longbow.damage_dice_count = 1
	longbow.damage_dice_sides = 8
	longbow.damage_type = WeaponData.DamageType.PIERCE
	player_data.secondary_main_hand = longbow
	
	player_unit.data = player_data
	player_unit.name = "PlayerWarrior"
	
	if p_deploy.size() > 0:
		var pos = p_deploy.pop_back()
		_place_unit_at(player_unit, pos.x, pos.y)
	else:
		_place_unit_at(player_unit, 2, 2)
		
	combat_manager.register_unit(player_unit, true)
	player_unit.init_dr()
	active_player_unit = player_unit 
	
	# === 创建敌方单位 ===
	# 敌方1: 哥布林射手 x2 (CR 1/4, 谨慎AI)
	for i in range(2):
		var enemy_unit = Unit.new()
		var enemy_data = UnitData.new()
		enemy_data.unit_name = "哥布林射手_%d" % (i + 1)
		enemy_data.is_enemy = true
		enemy_data.enemy_type = UnitData.EnemyType.HUMANOID
		enemy_data.threat_level = 0.25
		enemy_data.ai_strategy = UnitData.AIStrategy.CAUTIOUS
		enemy_data.morale = 0
		
		# 哥布林属性 (低力量，高敏捷)
		enemy_data.str = 8
		enemy_data.dex = 16
		enemy_data.con = 10
		enemy_data.intel = 6
		enemy_data.wis = 8
		enemy_data.cha = 6
		enemy_data.base_max_hp = 7
		enemy_data.base_ac = 13
		
		# 武器：短弓 (1d6, 远程)
		var shortbow = WeaponData.new()
		shortbow.item_name = "短弓"
		shortbow.is_ranged = true
		shortbow.range_cells = 6
		shortbow.damage_dice_count = 1
		shortbow.damage_dice_sides = 6
		shortbow.is_finesse = true
		enemy_data.primary_main_hand = shortbow
		
		# 特性
		enemy_data.traits.append("敏捷撤退")
		
		enemy_unit.data = enemy_data
		enemy_unit.name = "EnemyGoblinArcher_" + str(i)
		
		if e_deploy.size() > 0:
			var pos = e_deploy.pop_back()
			_place_unit_at(enemy_unit, pos.x, pos.y)
		else:
			_place_unit_at(enemy_unit, 9, i * 3)
			
		combat_manager.register_unit(enemy_unit, false)
		combat_ui.register_enemy(enemy_unit)
		enemy_unit.init_dr()	
	# 敌方2: 骷髅战士 (CR 1/2, 本能AI)
	var skeleton = Unit.new()
	var skel_data = UnitData.new()
	skel_data.unit_name = "骷髅战士"
	skel_data.is_enemy = true
	skel_data.enemy_type = UnitData.EnemyType.UNDEAD
	skel_data.threat_level = 0.5
	skel_data.ai_strategy = UnitData.AIStrategy.INSTINCT
	skel_data.morale = 0  # 亡灵士气系统特殊处理
	
	skel_data.str = 10
	skel_data.dex = 14
	skel_data.con = 10
	skel_data.intel = 6
	skel_data.wis = 8
	skel_data.cha = 4
	skel_data.base_max_hp = 13
	skel_data.base_ac = 13
	
	# 武器：短剑 (1d6, 近战)
	var shortsword = WeaponData.new()
	shortsword.item_name = "锈蚀短剑"
	shortsword.damage_dice_count = 1
	shortsword.damage_dice_sides = 6
	shortsword.is_finesse = true
	skel_data.primary_main_hand = shortsword
	
	# 免疫/抗性
	skel_data.immunities.append("毒素")
	skel_data.resistances.append("穿刺")
	
	skeleton.data = skel_data
	skeleton.name = "EnemySkeleton"
	
	if e_deploy.size() > 0:
		var pos = e_deploy.pop_back()
		_place_unit_at(skeleton, pos.x, pos.y)
	else:
		_place_unit_at(skeleton, 8, 4)
		
	combat_manager.register_unit(skeleton, false)
	combat_ui.register_enemy(skeleton)
	skeleton.init_dr()
	# 敌方3: 兽人狂战 (CR 1, 鲁莽AI)
	var orc = Unit.new()
	var orc_data = UnitData.new()
	orc_data.unit_name = "兽人狂战"
	orc_data.is_enemy = true
	orc_data.enemy_type = UnitData.EnemyType.HUMANOID
	orc_data.threat_level = 1.0
	orc_data.ai_strategy = UnitData.AIStrategy.RECKLESS
	orc_data.morale = 10
	
	orc_data.str = 16
	orc_data.dex = 12
	orc_data.con = 14
	orc_data.intel = 6
	orc_data.wis = 8
	orc_data.cha = 8
	orc_data.base_max_hp = 15
	orc_data.base_ac = 13
	
	# 武器：巨斧 (1d12, 近战)
	var greataxe = WeaponData.new()
	greataxe.item_name = "巨斧"
	greataxe.damage_dice_count = 1
	greataxe.damage_dice_sides = 12
	orc_data.primary_main_hand = greataxe
	
	# 特性
	orc_data.traits.append("鲁莽攻击")
	
	orc.data = orc_data
	orc.name = "EnemyOrc"
	
	if e_deploy.size() > 0:
		var pos = e_deploy.pop_back()
		_place_unit_at(orc, pos.x, pos.y)
	else:
		_place_unit_at(orc, 7, 6)
		
	combat_manager.register_unit(orc, false)
	combat_ui.register_enemy(orc)
	orc.init_dr()
	# 初始化视野迷雾
	_update_fov()

func _place_unit_at(unit: Unit, q: int, r: int):
	var cell: HexCell = hex_grid.get_cell(q, r)
	if cell and cell.occupant == null:
		add_child(unit)
		var hex_height = HexUtils.SIZE * 0.5
		unit.position = cell.position + Vector3(0, hex_height / 2.0, 0)
		unit.grid_pos = Vector2i(q, r)
		cell.occupant = unit

# 移除旧位置的占位
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

# ================================
# 战争迷雾 (FoW)
# ================================

func _update_fov():
	var vision_range = 5 # 假设视野范围是 5 格
	
	if not is_instance_valid(active_player_unit): return
	
	var visible_coords = hex_grid.get_cells_in_range(active_player_unit.grid_pos.x, active_player_unit.grid_pos.y, vision_range)
	
	# 重置所有格子为 shrouded
	for pos in hex_grid.cells:
		var cell = hex_grid.cells[pos]
		cell.set_shrouded(true)
	
	# 重置所有敌人为不可见
	for enemy in combat_manager.enemy_units:
		if is_instance_valid(enemy):
			enemy.visible = false
			
	# 点亮在视野内的格子和敌人
	for coord in visible_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_shrouded(false)
			if cell.occupant and cell.occupant != active_player_unit and combat_manager.enemy_units.has(cell.occupant):
				cell.occupant.visible = true

# ================================
# 高亮系统
# ================================

func _clear_highlights():
	for cell in highlighted_cells:
		cell.set_highlight(false)
	highlighted_cells.clear()

func _highlight_move_range(unit: Unit):
	_clear_highlights()
	# 获取移动范围内的格子 (假设固定为 4 格)
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, 4)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell and cell.occupant == null:
			cell.set_highlight(true, Color(0.2, 0.6, 1.0, 0.4)) # 蓝色高亮
			highlighted_cells.append(cell)

func _highlight_attack_range(unit: Unit):
	_clear_highlights()
	var weapon = unit.get_main_hand()
	var atk_range = weapon.range_cells if weapon else 1
	
	# 根据武器射程高亮范围
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, atk_range)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(1.0, 0.2, 0.2, 0.4)) # 红色高亮
			highlighted_cells.append(cell)

func _highlight_item_range(unit: Unit):
	_clear_highlights()
	# 物品使用范围 = 自身 + 相邻1格
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, 1)
	range_coords.append(unit.grid_pos)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(0.2, 0.9, 0.4, 0.4)) # 绿色高亮
			highlighted_cells.append(cell)

func _highlight_spell_range(unit: Unit, spell: SpellData):
	_clear_highlights()
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, spell.range_cells)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(1.0, 0.5, 0.0, 0.4)) # 橙色高亮
			highlighted_cells.append(cell)

# ================================
# 流程与事件处理
# ================================

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

## 使用 AIController 执行敌方回合（替换原来的硬编码AI）
func _execute_ai_turn():
	await get_tree().create_timer(0.3).timeout
	
	# 检查是否还有存活的玩家单位
	if not is_instance_valid(active_player_unit) or active_player_unit.current_hp <= 0:
		combat_manager.end_current_turn()
		return
	
	# 收集当前存活的单位列表（AI执行过程中可能有单位死亡）
	var alive_enemies: Array[Unit] = []
	for e in combat_manager.enemy_units:
		if is_instance_valid(e) and e.current_hp > 0:
			alive_enemies.append(e)
	
	var alive_players: Array[Unit] = []
	for p in combat_manager.player_units:
		if is_instance_valid(p) and p.current_hp > 0:
			alive_players.append(p)
	
	# 连接AI完成信号，自动结束回合
	ai_controller.all_actions_completed.connect(
		func(): combat_manager.end_current_turn(),
		CONNECT_ONE_SHOT
	)
	
	# 执行AI回合
	ai_controller.execute_enemy_turn(alive_enemies, alive_players, hex_grid, combat_ui)

func _on_combat_ended(victory: bool):
	_clear_highlights()
	
	if victory:
		combat_ui.set_turn_text("战斗胜利！", Color.GREEN)
		# 【任务系统联动】如果获胜，更新杀怪任务进度
		_report_combat_results_to_quests()
	else:
		combat_ui.set_turn_text("战斗失败，全军覆没！", Color.RED)
		
	combat_ui.set_action_bar_visible(false)
	
	# 增加一个 1.5 秒的延迟，让玩家能看清战斗结束的提示，然后再切回大地图
	await get_tree().create_timer(1.5).timeout
	combat_finished.emit(victory)

## 辅助：向任务系统汇报战果
func _report_combat_results_to_quests():
	# 获取大地图场景中的 QuestManager (假设还在 root 下或父节点中)
	var qm = get_parent().get_node_or_null("QuestManager")
	if is_instance_valid(qm):
		# 目前简化处理：只要赢了，就给当前活跃的“讨伐”类任务增加进度
		# 在更复杂的系统中，应根据杀死的具体怪物 ID (如 goblin_id) 来匹配
		for quest in qm.active_quests:
			if quest.quest_type == QuestData.QuestType.EXTERMINATION:
				# 假设本场战斗消灭了 3 只哥布林
				qm.update_quest_progress(quest.quest_id, 3)
				print("任务进度已更新: ", quest.quest_name)

func _highlight_skill_range(unit: Unit):
	_clear_highlights()
	# 假设火球术射程是 5 格
	var range_coords = hex_grid.get_cells_in_range(unit.grid_pos.x, unit.grid_pos.y, 5)
	for coord in range_coords:
		var cell = hex_grid.get_cell(coord.x, coord.y)
		if cell:
			cell.set_highlight(true, Color(1.0, 0.5, 0.0, 0.4)) # 橙色高亮表示法术射程

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
		# Legacy — kept for backward compat, redirects to spell
		_on_action_selected("spell")
	elif action == "end_turn":
		combat_ui.log_message("玩家结束回合。")
		current_action_mode = "none"
		combat_manager.end_current_turn()

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
			
			# 播放攻击动画并等待
			active_player_unit.play_anim("attack")
			await get_tree().create_timer(0.6).timeout
			
			# 使用 CombatResolver 统一结算
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
				# damage already applied by CombatResolver
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
			
			# 回到待机
			active_player_unit.play_anim("default")
			
			active_player_unit.has_acted = true
			current_action_mode = "none"
			_clear_highlights()
		else:
			combat_ui.log_message("无效的攻击目标。")
			
	elif current_action_mode == "spell":
		if _selected_spell and highlighted_cells.has(cell):
			# 播放施法动画
			active_player_unit.play_anim("attack")
			await get_tree().create_timer(0.6).timeout
			
			var spell_mgr = SpellManager.new()
			add_child(spell_mgr)
			
			# 触发爆炸粒子特效
			VFXManager.play_explosion_effect(self, cell.global_position)
			
			var result = spell_mgr.cast_spell(active_player_unit, _selected_spell, cell.grid_pos, hex_grid)
			if result["success"]:
				# ... (处理结果)
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
			
			# 回到待机
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
		pass  # Legacy — redirect handled by "spell" action above

	elif current_action_mode == "item":
		if highlighted_cells.has(cell):
			if cell.occupant and not cell.occupant.data.is_enemy:
				var target = cell.occupant
				var potions = active_player_unit.data.consumables.filter(func(c): return c.consumable_type == ConsumableData.ConsumableType.HEALING_POTION)
				if not potions.is_empty():
					var potion = potions[0]
					var result = ConsumableManager.use_consumable(target, potion, cell.grid_pos, hex_grid)
					if result["success"]:
						combat_ui.log_message("[color=green]%s 使用了%s，恢复 %d HP。[/color]" % [target.data.unit_name, potion.item_name, result["amount"]])
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

# ================================
# 法术选择回调
# ================================

func _on_spell_selected(spell: SpellData):
	if not is_instance_valid(active_player_unit): return
	combat_ui.close_spell_panel()
	combat_ui.log_message("[color=orange]选择法术：%s[/color] — 请点击射程内的目标。" % spell.spell_name)
	current_action_mode = "spell"
	_highlight_spell_range(active_player_unit, spell)
	# 存储当前选中的法术以供 cell_clicked 使用
	_selected_spell = spell

var _selected_spell: SpellData = null

# ================================
# 视角控制 (WASD 平移，滚轮缩放)
# ================================

func _unhandled_input(event):
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			# 正交相机的视野大小，数字越小越放大
			camera.size = clamp(camera.size * 0.9, 300.0, 2000.0)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			camera.size = clamp(camera.size * 1.1, 300.0, 2000.0)

func _process(delta):
	if not camera: return
	
	# 平移速度随当前缩放大小自适应
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
