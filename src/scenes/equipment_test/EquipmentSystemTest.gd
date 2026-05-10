# EquipmentSystemTest.gd
# 装备生成和敌方生成系统测试场景
extends Node

func _ready():
	print("=== 装备生成和敌方生成系统测试 ===\n")
	
	test_equipment_generator()
	test_enemy_generator()
	test_loot_table()
	
	print("\n=== 测试完成 ===")


func test_equipment_generator():
	print("--- 测试装备生成器 ---")
	
	# 测试生成不同稀有度的武器
	print("\n1. 生成普通长剑:")
	var common_sword = EquipmentGenerator.generate_random_weapon(["longsword"], ItemData.Rarity.COMMON, 1, "normal")
	if common_sword:
		print("  名称: %s" % common_sword.get_full_name())
		print("  稀有度: %s" % common_sword.get_rarity_name())
		print("  描述: %s" % common_sword.get_weapon_description())
	
	print("\n2. 生成稀有巨剑:")
	var rare_greatsword = EquipmentGenerator.generate_random_weapon(["greatsword"], ItemData.Rarity.RARE, 5, "hard")
	if rare_greatsword:
		print("  名称: %s" % rare_greatsword.get_full_name())
		print("  稀有度: %s" % rare_greatsword.get_rarity_name())
		print("  描述: %s" % rare_greatsword.get_weapon_description())
		print("  词缀: %s" % rare_greatsword.get_affix_descriptions())
	
	print("\n3. 生成史诗防具:")
	var epic_armor = EquipmentGenerator.generate_random_armor([], ItemData.Rarity.EPIC, 10, "nightmare")
	if epic_armor:
		print("  名称: %s" % epic_armor.get_full_name())
		print("  稀有度: %s" % epic_armor.get_rarity_name())
		print("  描述: %s" % epic_armor.get_armor_description())
		print("  词缀: %s" % epic_armor.get_affix_descriptions())
	
	print("\n4. 生成随机饰品:")
	var accessory = EquipmentGenerator.generate_random_accessory(-1, 8, "hard")
	if accessory:
		print("  名称: %s" % accessory.get_full_name())
		print("  稀有度: %s" % accessory.get_rarity_name())
		print("  效果: %s" % accessory.get_effect_text())


func test_enemy_generator():
	print("\n--- 测试敌方生成器 ---")
	
	# 获取所有敌人模板
	var all_enemies = PrototypeData.get_enemies()
	print("\n可用敌人模板总数: %d" % all_enemies.size())
	
	# 按类型分类统计
	var type_counts = {}
	for key in all_enemies.keys():
		var enemy = all_enemies[key]
		var type_name = enemy.get_enemy_type_name()
		if not type_counts.has(type_name):
			type_counts[type_name] = 0
		type_counts[type_name] += 1
	
	print("\n敌人类型分布:")
	for type_name in type_counts.keys():
		print("  %s: %d 种" % [type_name, type_counts[type_name]])
	
	# 测试各类型代表性敌人
	print("\n=== 野兽类测试 ===")
	_test_enemy("forest_wolf")
	_test_enemy("giant_spider")
	_test_enemy("black_bear")
	
	print("\n=== 亡灵类测试 ===")
	_test_enemy("skeleton_warrior")
	_test_enemy("zombie")
	_test_enemy("ghost")
	
	print("\n=== 魔物类测试 ===")
	_test_enemy("slime")
	_test_enemy("imp")
	_test_enemy("fire_elemental")
	
	print("\n=== 构造体类测试 ===")
	_test_enemy("wood_sentinel")
	_test_enemy("stone_golem")
	
	print("\n=== 巨型类测试 ===")
	_test_enemy("ogre")
	_test_enemy("minotaur")
	
	print("\n=== 龙族类测试 ===")
	_test_enemy("young_red_dragon")
	_test_enemy("adult_red_dragon")
	
	print("\n=== 随机遭遇测试 ===")
	var encounter = EnemyGenerator.generate_encounter(4, 4, 1.0)
	print("4级队伍标准遭遇 (4人):")
	print("  敌人数量: %d" % encounter.size())
	for enemy in encounter:
		print("  - %s (CR %s)" % [enemy.unit_name, enemy.get_cr_text()])


func _test_enemy(template_id: String):
	var enemy = EnemyGenerator.generate_enemy(template_id)
	if enemy:
		print("\n%s:" % enemy.unit_name)
		print("  CR: %s | 类型: %s | 体型: %s" % [enemy.get_cr_text(), enemy.get_enemy_type_name(), enemy.get_size_name()])
		print("  HP: %d | AC: %d | 速度: %d格" % [enemy.base_max_hp, enemy.base_ac, enemy.base_move_range])
		print("  AI策略: %s" % enemy.get_ai_strategy_name())
		if enemy.primary_main_hand:
			print("  武器: %s (%dd%d)" % [enemy.primary_main_hand.item_name, enemy.primary_main_hand.damage_dice_count, enemy.primary_main_hand.damage_dice_sides])
		if not enemy.immunities.is_empty():
			print("  免疫: %s" % ", ".join(enemy.immunities))
		if not enemy.resistances.is_empty():
			print("  抗性: %s" % ", ".join(enemy.resistances))
		if not enemy.weaknesses.is_empty():
			print("  弱点: %s" % ", ".join(enemy.weaknesses))
		if not enemy.traits.is_empty():
			print("  特性: %s" % enemy.traits[0])


func test_loot_table():
	print("\n--- 测试战利品表 ---")
	
	# 测试不同CR敌人的掉落
	print("\n1. 骷髅战士掉落:")
	var skeleton = EnemyGenerator.generate_enemy("skeleton_warrior")
	if skeleton:
		var loot = LootTable.generate_loot(skeleton)
		print("  掉落物品数: %d" % loot.size())
		for item in loot:
			if item:
				print("  - %s" % item.get_full_name())
	
	print("\n2. 兽人狂战掉落:")
	var orc = EnemyGenerator.generate_enemy("orc_berserker")
	if orc:
		var loot = LootTable.generate_loot(orc)
		print("  掉落物品数: %d" % loot.size())
		for item in loot:
			if item:
				print("  - %s" % item.get_full_name())
	
	print("\n3. 模拟10次掉落统计:")
	var drop_stats = {
		"weapon": 0,
		"armor": 0,
		"shield": 0,
		"accessory": 0,
		"consumable": 0,
		"gold": 0,
	}
	
	for i in range(10):
		var test_enemy = EnemyGenerator.generate_enemy("orc_berserker")
		var loot = LootTable.generate_loot(test_enemy)
		for item in loot:
			if item:
				if item is WeaponData:
					drop_stats["weapon"] += 1
				elif item is ArmorData:
					if item.armor_type == ArmorData.ArmorType.SHIELD:
						drop_stats["shield"] += 1
					else:
						drop_stats["armor"] += 1
				elif item is AccessoryData:
					drop_stats["accessory"] += 1
				elif item is ConsumableData:
					drop_stats["consumable"] += 1
				elif item.item_name == "金币":
					drop_stats["gold"] += 1
	
	print("  掉落统计 (10次):")
	print("    武器: %d" % drop_stats["weapon"])
	print("    防具: %d" % drop_stats["armor"])
	print("    盾牌: %d" % drop_stats["shield"])
	print("    饰品: %d" % drop_stats["accessory"])
	print("    消耗品: %d" % drop_stats["consumable"])
	print("    金币: %d" % drop_stats["gold"])
