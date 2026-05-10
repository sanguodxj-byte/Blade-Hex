extends SceneTree
## 测试战斗地图生成 — 验证大地图驱动路径 + 模板回退路径

func _init():
	var passed := 0
	var failed := 0
	
	# Test 1: Template fallback path (old behavior)
	print("\n=== Test 1: Template fallback ===")
	var gen = BattleMapGenerator.new()
	var template_names = gen.get_template_names()
	print("  Templates available: %d" % template_names.size())
	assert(template_names.size() > 0, "No templates registered!")
	
	var map1 = gen.generate_from_template("plain_field", BattleMapGenerator.BattleSize.MERCENARY, 42)
	if map1 and map1.cells.size() > 0 and map1.player_deployment.size() > 0 and map1.enemy_deployment.size() > 0:
		print("  PASS: template generate, cells=%d, player_deploy=%d, enemy_deploy=%d" % [map1.cells.size(), map1.player_deployment.size(), map1.enemy_deployment.size()])
		_print_map_stats(map1)
		passed += 1
	else:
		print("  FAIL: template generate returned empty data")
		failed += 1
	
	# Test 2: Connectivity check (template path)
	print("\n=== Test 2: Connectivity (template) ===")
	if _verify_connectivity(map1):
		print("  PASS: player and enemy deployment zones are connected")
		passed += 1
	else:
		print("  FAIL: deployment zones NOT connected!")
		failed += 1
	
	# Test 3: Overworld-driven path with synthetic grid
	print("\n=== Test 3: Overworld-driven generation ===")
	var ow_gen = HexOverworldGenerator.new()
	var ow_grid = ow_gen.generate(10, 8, 12345)
	print("  Overworld grid generated: %d tiles" % ow_grid.get_tile_count())
	
	var center_tile = ow_grid.find_passable_near_pixel(
		ow_grid.get_center_pixel().x,
		ow_grid.get_center_pixel().y,
		5
	)
	var encounter_coord = center_tile.coord if center_tile else Vector2i(5, 4)
	print("  Encounter coord: %s" % str(encounter_coord))
	
	var terrain_type = ow_grid.sample_terrain_at_pixel(
		center_tile.pixel_pos.x if center_tile else 0.0,
		center_tile.pixel_pos.y if center_tile else 0.0
	)
	print("  Terrain at encounter: %d" % terrain_type)
	
	var ctx = BattleContext.create(
		terrain_type,
		BattleMapGenerator.BattleSize.MERCENARY,
		BattleContext.EngagementType.NORMAL,
		42
	)
	ctx.overworld_grid = ow_grid
	ctx.encounter_coord = encounter_coord
	ctx.poi_type = -1
	
	var map2 = gen.generate(ctx)
	if map2 and map2.cells.size() > 0 and map2.player_deployment.size() > 0 and map2.enemy_deployment.size() > 0:
		print("  PASS: overworld-driven generate, cells=%d, player_deploy=%d, enemy_deploy=%d" % [map2.cells.size(), map2.player_deployment.size(), map2.enemy_deployment.size()])
		_print_map_stats(map2)
		passed += 1
	else:
		print("  FAIL: overworld-driven generate returned empty data")
		failed += 1
	
	# Test 4: Connectivity check (overworld path)
	print("\n=== Test 4: Connectivity (overworld) ===")
	if _verify_connectivity(map2):
		print("  PASS: overworld-driven map is connected")
		passed += 1
	else:
		print("  FAIL: overworld-driven map NOT connected!")
		failed += 1
	
	# Test 5: Multiple seeds produce different maps
	print("\n=== Test 5: Seed variance ===")
	var map_a = gen.generate_from_template("forest_ambush", BattleMapGenerator.BattleSize.MERCENARY, 100)
	var map_b = gen.generate_from_template("forest_ambush", BattleMapGenerator.BattleSize.MERCENARY, 200)
	var diff_count := 0
	for key in map_a.cells:
		if map_b.cells.has(key):
			var a: BattleCellData = map_a.cells[key]
			var b: BattleCellData = map_b.cells[key]
			if a.terrain_type != b.terrain_type:
				diff_count += 1
	if diff_count > 0:
		print("  PASS: %d cells differ between seeds 100 vs 200" % diff_count)
		passed += 1
	else:
		print("  FAIL: identical maps from different seeds")
		failed += 1
	
	# Test 6: All templates generate valid maps
	print("\n=== Test 6: All templates ===")
	var all_ok := true
	for tname in template_names:
		var m = gen.generate_from_template(tname, BattleMapGenerator.BattleSize.MERCENARY, 42)
		if not m or m.cells.size() == 0:
			print("  FAIL: template '%s' produced empty map" % tname)
			all_ok = false
		elif m.player_deployment.size() == 0 or m.enemy_deployment.size() == 0:
			print("  FAIL: template '%s' has no deployment zones" % tname)
			all_ok = false
	if all_ok:
		print("  PASS: all %d templates generate valid maps" % template_names.size())
		passed += 1
	else:
		failed += 1
	
	# Summary
	print("\n" + "=" .repeat(40))
	print("RESULTS: %d passed, %d failed" % [passed, failed])
	if failed == 0:
		print("ALL TESTS PASSED")
	else:
		print("SOME TESTS FAILED!")
	quit(0 if failed == 0 else 1)


func _verify_connectivity(map_data: BattleMapGenerator.BattleMapData) -> bool:
	if map_data.player_deployment.is_empty() or map_data.enemy_deployment.is_empty():
		return false
	
	# BFS from player deployment
	var reachable: Dictionary = {}
	var queue: Array[Vector2i] = [map_data.player_deployment[0]]
	reachable[queue[0]] = true
	
	while not queue.is_empty():
		var current = queue.pop_front()
		for dir in range(6):
			var neighbor = HexUtils.get_neighbor(current.x, current.y, dir)
			if map_data.cells.has(neighbor) and not reachable.has(neighbor):
				var cell: BattleCellData = map_data.cells[neighbor]
				if cell.is_passable:
					reachable[neighbor] = true
					queue.append(neighbor)
	
	# Check if ANY enemy deployment is reachable
	for ep in map_data.enemy_deployment:
		if reachable.has(ep):
			return true
	return false


func _print_map_stats(map_data: BattleMapGenerator.BattleMapData) -> void:
	var terrain_counts: Dictionary = {}
	for key in map_data.cells:
		var cell: BattleCellData = map_data.cells[key]
		var t = cell.terrain_type
		if not terrain_counts.has(t):
			terrain_counts[t] = 0
		terrain_counts[t] += 1
	
	var elev_counts = {0: 0, 1: 0, 2: 0}
	for key in map_data.cells:
		var cell: BattleCellData = map_data.cells[key]
		elev_counts[cell.elevation] = elev_counts.get(cell.elevation, 0) + 1
	
	print("    Terrain distribution:")
	var names = {
		0: "PLAINS", 1: "GRASSLAND", 2: "SAVANNA", 3: "FOREST", 4: "DENSE_FOREST",
		5: "HILLS", 6: "MOUNTAIN", 7: "SHALLOW_WATER", 8: "DEEP_WATER", 9: "SWAMP",
		10: "ROAD", 11: "SAND", 12: "SNOW", 13: "WALL", 14: "RUINS",
		15: "POISON_MUSHROOM", 16: "LUCKY_GRASS"
	}
	for t in terrain_counts:
		var name = names.get(t, "?%d" % t)
		print("      %s: %d" % [name, terrain_counts[t]])
	print("    Elevation: low=%d flat=%d high=%d" % [elev_counts[0], elev_counts[1], elev_counts[2]])
	print("    Environment: %s" % map_data.environment_event)
