import re

with open('src/scenes/combat/CombatScene.gd', 'r', encoding='utf-8') as f:
    content = f.read()

replacement = """
func _on_cell_single_clicked(cell: HexCell):
	if combat_manager.current_state != CombatManager.CombatState.PLAYER_TURN:
		return
	
	# 点击友方单位切换选中
	if cell.occupant and cell.occupant != active_player_unit:
		if combat_manager.player_units.has(cell.occupant) and cell.occupant.current_hp > 0:
			_on_ally_list_unit_selected(cell.occupant)
			return
	
	if not is_instance_valid(active_player_unit):
		return
		
	# 检查是否直接左键攻击敌人 (敌人在范围内直接左键也可以攻击)
	if cell.occupant and cell.occupant != active_player_unit and combat_manager.enemy_units.has(cell.occupant):
		if not active_player_unit.has_acted:
			var target = cell.occupant
			var weapon = active_player_unit.get_main_hand()
			var atk_range = weapon.range_cells if weapon else 1
			var dist = hex_grid.cube_distance(HexUtils.offset_to_cube(active_player_unit.grid_pos), HexUtils.offset_to_cube(target.grid_pos))
			if dist <= atk_range:
				_perform_attack(target)
				return
			else:
				combat_ui.log_message("敌人超出攻击范围，双击尝试移动并攻击。")
		else:
			combat_ui.log_message("本回合已行动过。")
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
			
	elif current_action_mode == "spell":
		if _selected_spell and highlighted_cells.has(cell):
			active_player_unit.play_anim("attack")
			await get_tree().create_timer(0.6 / _anim_speed).timeout
			VFXManager.play_explosion_effect(self, cell.global_position)
			
			var result = _spell_manager.cast_spell(active_player_unit, _selected_spell, cell.grid_pos, hex_grid)
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

func _perform_attack(target: Unit):
	current_action_mode = "none"
	_clear_highlights()
	
	active_player_unit.play_anim("attack")
	await get_tree().create_timer(0.6 / _anim_speed).timeout
	
	var result = CombatResolver.resolve_attack(active_player_unit, target, hex_grid)
	if result["hit"]:
		var dmg = result["damage"]
		var crit_msg = " [color=yellow]暴击！[/color]" if result["critical"] else ""
		var flank_msg = " [包夹]" if result.get("is_flanking", false) else ""
		
		var weapon = active_player_unit.get_main_hand()
		combat_ui.log_message("[color=green]命中！[/color]%s%s 使用 %s 造成 %d 伤害。" % [
			crit_msg, flank_msg,
			weapon.item_name if weapon else "徒手", dmg
		])
		combat_ui.update_enemy_info(target)
		if target.current_hp <= 0:
			combat_ui.log_message("[color=yellow]%s 被击败！[/color]" % target.data.unit_name)
			combat_ui.remove_enemy(target)
			var tcell = hex_grid.get_cell(target.grid_pos.x, target.grid_pos.y)
			if tcell: tcell.occupant = null
	else:
		if result["fumble"]:
			combat_ui.log_message("[color=red]严重失误！[/color]")
		else:
			combat_ui.log_message("[color=red]未命中！[/color] (命中率 %d%%)" % [result["hit_chance_percent"]])
	
	active_player_unit.play_anim("default")
	active_player_unit.has_acted = true

func _on_cell_double_clicked(cell: HexCell):
	if combat_manager.current_state != CombatManager.CombatState.PLAYER_TURN:
		return
	if not is_instance_valid(active_player_unit):
		return
	
	if cell.occupant and cell.occupant != active_player_unit and combat_manager.enemy_units.has(cell.occupant):
		if active_player_unit.has_acted:
			combat_ui.log_message("本回合已行动过。")
			return
		
		var target = cell.occupant
		var weapon = active_player_unit.get_main_hand()
		var atk_range = weapon.range_cells if weapon else 1
		var dist = hex_grid.cube_distance(HexUtils.offset_to_cube(active_player_unit.grid_pos), HexUtils.offset_to_cube(target.grid_pos))
		
		if dist <= atk_range:
			_perform_attack(target)
		else:
			if active_player_unit.has_moved:
				combat_ui.log_message("距离不够且已移动。")
				return
			
			var move_range = active_player_unit.data.base_move_range if active_player_unit.data else 4
			var range_coords = hex_grid.get_cells_in_range(active_player_unit.grid_pos.x, active_player_unit.grid_pos.y, move_range)
			var best_coord = null
			var best_dist = 9999
			
			for coord in range_coords:
				var c = hex_grid.get_cell(coord.x, coord.y)
				if c and c.occupant == null:
					var d = hex_grid.cube_distance(HexUtils.offset_to_cube(coord), HexUtils.offset_to_cube(target.grid_pos))
					if d <= atk_range and d < best_dist:
						best_dist = d
						best_coord = coord
			
			if best_coord != null:
				combat_ui.log_message("自动寻路并攻击！")
				await _move_unit_to(active_player_unit, best_coord.x, best_coord.y)
				active_player_unit.has_moved = true
				_perform_attack(target)
			else:
				combat_ui.log_message("无法移动到可攻击的范围。")

func _on_cell_right_clicked(cell: HexCell):
	if combat_manager.current_state != CombatManager.CombatState.PLAYER_TURN:
		return
	if not is_instance_valid(active_player_unit):
		return
	
	var screen_pos = get_viewport().get_mouse_position()
	# 假如有提供 target，我们可以将其传递给 radial_menu
	var target_unit = cell.occupant if cell.occupant else null
	if combat_ui.has_method("open_radial_menu"):
		combat_ui.open_radial_menu(screen_pos, active_player_unit, _spell_manager, target_unit)
"""

pattern = re.compile(r'func _on_cell_clicked.*?func _on_spell_selected', re.DOTALL)
new_content = pattern.sub(replacement + '\n# ================================\n# 法术选择回调\n# ================================\n\nfunc _on_spell_selected', content)

with open('src/scenes/combat/CombatScene.gd', 'w', encoding='utf-8') as f:
    f.write(new_content)
print('Replaced cell click logic')
