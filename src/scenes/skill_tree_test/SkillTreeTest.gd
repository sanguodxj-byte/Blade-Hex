# SkillTreeTest.gd
# 技能盘系统测试场景 —— 验证连通网络、点亮逻辑、跳跃机制
extends Node

# ============================================================================
# 测试角色ID
# ============================================================================

const TEST_CHAR_ID = 9999

# ============================================================================
# 测试入口
# ============================================================================

func _ready():
	print("=".repeat(60))
	print("  技能盘系统测试")
	print("=".repeat(60))

	var mgr = SkillTreeManager.get_instance()
	if not mgr:
		print("[ERROR] SkillTreeManager 未初始化（请确保已注册为 Autoload）")
		return

	# 打印图数据概况
	print("\n[信息] 技能盘节点总数: %d" % mgr.tree_data.get_node_count())
	print("[信息] 大节点数(含Keystone): %d" % mgr.tree_data.get_big_nodes().size())
	var region_map = [
		["STR", SkillNodeData.Region.STR],
		["DEX", SkillNodeData.Region.DEX],
		["CON", SkillNodeData.Region.CON],
		["INT", SkillNodeData.Region.INT],
		["WIS", SkillNodeData.Region.WIS],
		["CHA", SkillNodeData.Region.CHA],
	]
	for entry in region_map:
		var region_name = entry[0]
		var region_enum = entry[1]
		var nodes = mgr.tree_data.get_nodes_by_region(region_enum)
		print("  区域 %s: %d 个节点" % [region_name, nodes.size()])

	# --- 测试1：纯近战战士 Build (STR主方向) ---
	print("\n--- 测试1：纯近战战士 Build ---")
	test_warrior_build(mgr)

	# --- 测试2：混合魔剑士 Build (STR + INT) ---
	print("\n--- 测试2：混合魔剑士 Build ---")
	test_spellblade_build(mgr)

	# --- 测试3：AI自动加点 (摸索期) ---
	print("\n--- 测试3：AI自动加点（摸索期）---")
	test_ai_allocate(mgr, 0.4, "str", "con")

	# --- 测试4：AI自动加点 (成熟期) ---
	print("\n--- 测试4：AI自动加点（成熟期）---")
	test_ai_allocate(mgr, 0.9, "int", "wis")

	# --- 测试5：Keystone代价验证 ---
	print("\n--- 测试5：Keystone代价验证 ---")
	test_keystone(mgr)

	# --- 测试6：过渡节点/环路 ---
	print("\n--- 测试6：过渡节点与环路 ---")
	test_transitions(mgr)

	print("\n" + "=".repeat(60))
	print("  测试完毕")
	print("=".repeat(60))


# ============================================================================
# 测试用例
# ============================================================================

func test_warrior_build(mgr: SkillTreeManager):
	# 创建10级角色
	mgr.create_skill_tree(TEST_CHAR_ID, 1)
	mgr.init_character_level(TEST_CHAR_ID, 10)
	var tree = mgr.get_skill_tree(TEST_CHAR_ID)

	print("初始状态: %d技能点, %d跳跃" % [tree.available_skill_points, tree.get_remaining_jumps()])

	# 纯近战路径：启程 → str_s01 → str_s02 → str_b01 → str_s03 → str_b02 → str_s04 → str_b03
	var warrior_path = [
		"str_s01", "str_s02", "str_b01",  # 入门
		"str_s03", "str_b02",             # 连击
		"str_s04", "str_b03",             # 旋风斩
	]

	for node_id in warrior_path:
		var node = mgr.tree_data.nodes[node_id]
		var result: Dictionary
		result = tree.try_activate_node(node_id)

		if result.success:
			print("  ✓ %s" % result.message)
		else:
			print("  ✗ %s — %s" % [node.node_name, result.message])

	# 显示剩余
	print("剩余技能点: %d, 剩余跳跃: %d/%d" % [tree.available_skill_points, tree.get_remaining_jumps(), tree.total_jumps])
	print("累计属性: HP+%d, 近战命中+%d, 近战伤害+%d, AC+%d, 暴击率+%.0f%%" % [
		tree.get_hp_bonus(), tree.get_melee_hit_bonus(), tree.get_melee_damage_bonus(),
		tree.get_ac_bonus(), tree.get_critical_rate_bonus() * 100
	])
	print("主动技能: ", _get_node_names(tree.get_active_skills()))

	# 清理
	mgr.remove_skill_tree(TEST_CHAR_ID)


func test_spellblade_build(mgr: SkillTreeManager):
	# 创建8级角色，混合STR+INT
	mgr.create_skill_tree(TEST_CHAR_ID, 1)
	mgr.init_character_level(TEST_CHAR_ID, 8)
	var tree = mgr.get_skill_tree(TEST_CHAR_ID)

	print("初始状态: %d技能点, %d跳跃" % [tree.available_skill_points, tree.get_remaining_jumps()])

	# 先走近战基础 + 再用跳跃切到法术
	var spellblade_path = [
		"str_s01", "str_s02", "str_b01",  # 近战入门
		"int_s01",                         # 开始往法术走
	]

	for node_id in spellblade_path:
		var node = mgr.tree_data.nodes[node_id]
		var result = tree.try_activate_node(node_id)
		if result.success:
			print("  ✓ %s" % result.message)
		else:
			print("  ✗ %s — %s" % [node.node_name, result.message])

	# 跳跃拿到以太感知
	var jump_result = tree.try_jump_activate("int_b01")
	if jump_result.success:
		print("  ✓ [跳跃] %s" % jump_result.message)
	else:
		print("  ✗ [跳跃] %s — %s" % [mgr.tree_data.nodes["int_b01"].node_name, jump_result.message])

	# 再点法术精通
	var spell_result = tree.try_activate_node("int_s03")
	if spell_result.success:
		print("  ✓ %s" % spell_result.message)
	var mast_result = tree.try_activate_node("int_b02")
	if mast_result.success:
		print("  ✓ %s" % mast_result.message)

	print("剩余技能点: %d, 剩余跳跃: %d/%d" % [tree.available_skill_points, tree.get_remaining_jumps(), tree.total_jumps])
	print("累计属性: HP+%d, 近战命中+%d, 近战伤害+%d, 魔力上限+%d" % [
		tree.get_hp_bonus(), tree.get_melee_hit_bonus(), tree.get_melee_damage_bonus(),
		tree.get_mana_max_bonus()
	])
	print("主动技能: ", _get_node_names(tree.get_active_skills()))

	mgr.remove_skill_tree(TEST_CHAR_ID)


func test_ai_allocate(mgr: SkillTreeManager, accuracy: float, primary: String, secondary: String):
	mgr.create_skill_tree(TEST_CHAR_ID, 1)
	mgr.init_character_level(TEST_CHAR_ID, 5)
	var tree = mgr.get_skill_tree(TEST_CHAR_ID)

	print("AI准确度: %.0f%%, 主方向: %s, 副方向: %s" % [accuracy * 100, primary, secondary])
	print("分配前: %d技能点" % tree.available_skill_points)

	tree.ai_allocate_points(accuracy, primary, secondary)

	print("分配后: %d技能点, 已点亮%d节点" % [tree.available_skill_points, tree.get_activated_count()])
	var names: Array[String] = []
	for nid in tree.activated_nodes:
		if nid != "start" and mgr.tree_data.nodes.has(nid):
			names.append(mgr.tree_data.nodes[nid].node_name)
	print("点亮节点: ", names)

	mgr.remove_skill_tree(TEST_CHAR_ID)


func test_keystone(mgr: SkillTreeManager):
	mgr.create_skill_tree(TEST_CHAR_ID, 1)
	mgr.init_character_level(TEST_CHAR_ID, 20)
	var tree = mgr.get_skill_tree(TEST_CHAR_ID)

	print("20级角色: %d技能点, %d跳跃" % [tree.available_skill_points, tree.get_remaining_jumps()])

	# 手动点亮到狂暴之力（STR Keystone）
	var keystone_path = [
		"str_s01", "str_s02", "str_b01",
		"str_s03", "str_b02",
		"str_s05", "str_b05", "str_s14", "str_ks01",
	]

	for node_id in keystone_path:
		var node = mgr.tree_data.nodes[node_id]
		var result = tree.try_activate_node(node_id)
		if result.success:
			print("  ✓ %s" % result.message)
		else:
			print("  ✗ %s — %s" % [node.node_name, result.message])

	# 检查Keystone的代价
	var ks = mgr.tree_data.nodes["str_ks01"]
	print("\nKeystone [%s]:" % ks.node_name)
	print("  增益: %s" % ks.description)
	print("  代价: %s" % ks.keystone_cost)
	print("  代价属性: ", tree.accumulated_costs)
	print("  累计属性: HP+%d, 近战伤害+%d, AC%+d" % [
		tree.get_hp_bonus(), tree.get_melee_damage_bonus(), tree.get_ac_bonus()
	])

	mgr.remove_skill_tree(TEST_CHAR_ID)


func test_transitions(mgr: SkillTreeManager):
	mgr.create_skill_tree(TEST_CHAR_ID, 1)
	mgr.init_character_level(TEST_CHAR_ID, 5)
	var tree = mgr.get_skill_tree(TEST_CHAR_ID)

	print("过渡节点列表:")
	for node_id in mgr.tree_data.nodes:
		var node: SkillNodeData = mgr.tree_data.nodes[node_id]
		if node.is_bridge:
			print("  [%s] %s ←→ %s" % [node.node_id, node.node_name, node.neighbors])

	# 测试从STR过渡到CON
	var path = ["str_s01", "trans_sc01", "con_s01"]
	for node_id in path:
		var node = mgr.tree_data.nodes[node_id]
		var result = tree.try_activate_node(node_id)
		if result.success:
			print("  ✓ %s (%s)" % [result.message, node.region_name()])
		else:
			print("  ✗ %s — %s" % [node.node_name, result.message])

	print("通过过渡节点后: HP+%d, 近战命中+%d, AC+%d" % [
		tree.get_hp_bonus(), tree.get_melee_hit_bonus(), tree.get_ac_bonus()
	])

	# 验证环路：DEX到STR的交叉连接
	print("\n验证环路连接:")
	var sd_nodes = ["trans_sd01", "trans_sd02"]
	for nid in sd_nodes:
		if mgr.tree_data.nodes.has(nid):
			var node: SkillNodeData = mgr.tree_data.nodes[nid]
			print("  %s 连接: %s" % [node.node_name, node.neighbors])

	mgr.remove_skill_tree(TEST_CHAR_ID)


# ============================================================================
# 辅助
# ============================================================================

func _get_node_names(node_list: Array) -> Array[String]:
	var names: Array[String] = []
	for n in node_list:
		names.append(n.node_name)
	return names

