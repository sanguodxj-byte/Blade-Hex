# -*- coding: utf-8 -*-
"""
完整还原 SkillTreeData.gd 到原始极坐标系统。
直接生成整个文件内容，不依赖增量替换。
"""

import re

filepath = r"D:\123\新建游戏项目\src\core\skill_tree\SkillTreeData.gd"

with open(filepath, "r", encoding="utf-8-sig") as f:
    content = f.read()

# === 还原过渡节点和跨区域环路 ===
# 找到 _build_transition_nodes 函数的开始和结束
trans_start = content.find("func _build_transition_nodes():")
trans_end = content.find("\n\n\n# ============", trans_start + 10)
if trans_end == -1:
    trans_end = content.find("\n\nfunc _build_cross_region_loops", trans_start + 10)

# 找到 _build_cross_region_loops 函数
loops_start = content.find("func _build_cross_region_loops():")
loops_end = content.find("\n\n# ============", loops_start + 10)
if loops_end == -1:
    loops_end = content.find("\n\nfunc _ms(", loops_start + 10)

# 原始过渡节点代码
original_transitions = """func _build_transition_nodes():
	var n
	# STR <-> DEX
	n = _ms("trans_sd01", "战斗技巧", SkillNodeData.Region.TRANSITION, 2, [], [], {"melee_hit": 1, "critical_rate": 0.02}, Vector2i(2, 11))
	n.neighbors = ["str_s01", "dex_s01"]
	n.is_bridge = true
	n = _ms("trans_sd02", "武器大师", SkillNodeData.Region.TRANSITION, 4, [], [], {"melee_hit": 1, "ranged_hit": 1}, Vector2i(4, 11))
	n.neighbors = ["str_b01", "dex_b01"]
	n.is_bridge = true
	# STR <-> CON
	n = _ms("trans_sc01", "近战生存", SkillNodeData.Region.TRANSITION, 2, [], [], {"max_hp": 3, "melee_hit": 1}, Vector2i(2, 10))
	n.neighbors = ["str_s01", "con_s01"]
	n.is_bridge = true
	n = _ms("trans_sc02", "盾牌掌握", SkillNodeData.Region.TRANSITION, 4, [], [], {"ac": 1, "melee_damage": 1}, Vector2i(4, 10))
	n.neighbors = ["str_b01", "con_b01"]
	n.is_bridge = true
	# DEX <-> INT
	n = _ms("trans_di01", "精准施法", SkillNodeData.Region.TRANSITION, 2, [], [], {"mana_max": 2, "spell_hit": 1}, Vector2i(2, 8))
	n.neighbors = ["dex_s01", "int_s01"]
	n.is_bridge = true
	n = _ms("trans_di02", "奥术射手", SkillNodeData.Region.TRANSITION, 4, [], [], {"ranged_hit": 1, "spell_damage": 1}, Vector2i(4, 8))
	n.neighbors = ["dex_b01", "int_b01"]
	n.is_bridge = true
	# INT <-> WIS
	n = _ms("trans_iw01", "神秘学", SkillNodeData.Region.TRANSITION, 2, [], [], {"mana_max": 2, "all_save": 1}, Vector2i(2, 5))
	n.neighbors = ["int_s01", "wis_s01"]
	n.is_bridge = true
	n = _ms("trans_iw02", "古代智慧", SkillNodeData.Region.TRANSITION, 4, [], [], {"spell_damage": 1, "heal_amount": 1}, Vector2i(4, 5))
	n.neighbors = ["int_b01", "wis_b01"]
	n.is_bridge = true
	# WIS <-> CHA
	n = _ms("trans_wc01", "精神领袖", SkillNodeData.Region.TRANSITION, 2, [], [], {"heal_amount": 1, "morale": 1}, Vector2i(2, 3))
	n.neighbors = ["wis_s01", "cha_s01"]
	n.is_bridge = true
	n = _ms("trans_wc02", "圣洁光辉", SkillNodeData.Region.TRANSITION, 4, [], [], {"heal_amount": 1, "ally_bonus": 1}, Vector2i(4, 3))
	n.neighbors = ["wis_b01", "cha_b01"]
	n.is_bridge = true
	# CHA <-> CON
	n = _ms("trans_cc01", "鼓舞防御", SkillNodeData.Region.TRANSITION, 2, [], [], {"ac": 1, "morale": 1}, Vector2i(2, 1))
	n.neighbors = ["cha_s01", "con_s01"]
	n.is_bridge = true
	n = _ms("trans_cc02", "铁血指挥", SkillNodeData.Region.TRANSITION, 4, [], [], {"ally_bonus": 1, "max_hp": 3}, Vector2i(4, 1))
	n.neighbors = ["cha_b01", "con_b01"]
	n.is_bridge = true
	# CON <-> INT (diagonal)
	n = _ms("trans_ci01", "战斗法师", SkillNodeData.Region.TRANSITION, 5, [], [], {"spell_damage": 1, "max_hp": 3}, Vector2i(5, 7))
	n.neighbors = ["con_b02", "int_b08"]
	n.is_bridge = true
	# DEX <-> WIS (diagonal)
	n = _ms("trans_dw01", "野性直觉", SkillNodeData.Region.TRANSITION, 5, [], [], {"initiative": 2, "wis_check": 1}, Vector2i(5, 2))
	n.neighbors = ["dex_b02", "wis_b02"]
	n.is_bridge = true
	# === 深层过渡 (Ring 6-8) ===
	# STR <-> DEX 深层
	n = _ms("trans_sd03", "剑弓合一", SkillNodeData.Region.TRANSITION, 7, [], [], {"melee_damage": 1, "ranged_damage": 1, "critical_rate": 0.02}, Vector2i(7, 11))
	n.neighbors = ["str_b08", "dex_b13"]
	n.is_bridge = true
	# STR <-> CHA 深层
	n = _ms("trans_sc03", "战神信仰", SkillNodeData.Region.TRANSITION, 7, [], [], {"melee_hit": 1, "morale": 2}, Vector2i(7, 10))
	n.neighbors = ["str_b07", "cha_b10"]
	n.is_bridge = true
	# CON <-> WIS 深层
	n = _ms("trans_cw01", "生命之力", SkillNodeData.Region.TRANSITION, 6, [], [], {"max_hp": 8, "heal_amount": 1}, Vector2i(6, 6))
	n.neighbors = ["con_b08", "wis_b08"]
	n.is_bridge = true
	# INT <-> CHA 深层
	n = _ms("trans_ic01", "奥术外交", SkillNodeData.Region.TRANSITION, 7, [], [], {"mana_max": 3, "cha_check": 1}, Vector2i(7, 4))
	n.neighbors = ["int_b11", "cha_b11"]
	n.is_bridge = true
	# DEX <-> CON 深层
	n = _ms("trans_dc01", "战场机动", SkillNodeData.Region.TRANSITION, 6, [], [], {"ac": 1, "initiative": 2, "max_hp": 3}, Vector2i(6, 9))
	n.neighbors = ["dex_b11", "con_b08"]
	n.is_bridge = true"""

original_loops = """func _build_cross_region_loops():
	_ac("str_b02", "trans_sd02")
	_ac("dex_b02", "trans_sd02")
	_ac("con_b02", "trans_sc02")
	_ac("str_b04", "trans_sc02")
	_ac("int_b02", "trans_iw02")
	_ac("wis_b04", "trans_iw02")
	_ac("cha_b02", "trans_wc02")
	_ac("wis_b02", "trans_wc02")
	_ac("con_b04", "trans_cc02")
	_ac("cha_b04", "trans_cc02")
	_ac("str_b03", "trans_sc02")
	_ac("dex_b05", "trans_di02")
	_ac("cha_b05", "trans_wc02")
	# 内层环路: ring 2 小节点互相连接
	_ac("str_s02", "trans_sd01")
	_ac("dex_s02", "trans_sd01")
	_ac("con_s02", "trans_sc01")
	_ac("str_s10", "trans_sc01")
	_ac("int_s02", "trans_iw01")
	_ac("wis_s02", "trans_iw01")
	_ac("cha_s02", "trans_wc01")
	_ac("wis_s09", "trans_wc01")
	_ac("con_s08", "trans_cc01")
	_ac("cha_s09", "trans_cc01")
	_ac("dex_s14", "trans_di01")
	_ac("int_s15", "trans_di01")
	# === 深层环路 ===
	_ac("str_b08", "trans_sd03")
	_ac("dex_b13", "trans_sd03")
	_ac("str_b07", "trans_sc03")
	_ac("cha_b10", "trans_sc03")
	_ac("con_b08", "trans_cw01")
	_ac("wis_b08", "trans_cw01")
	_ac("int_b11", "trans_ic01")
	_ac("cha_b11", "trans_ic01")
	_ac("dex_b11", "trans_dc01")
	_ac("con_b08", "trans_dc01")
	_ac("wis_b09", "trans_cw01")
	_ac("cha_b12", "trans_sc03")"""

# Replace transition nodes
if trans_start != -1 and trans_end != -1:
    content = content[:trans_start] + original_transitions + content[trans_end:]

# Replace cross region loops (need to re-find positions after first replacement)
loops_start = content.find("func _build_cross_region_loops():")
loops_end = content.find("\n\n# ============", loops_start + 10)
if loops_end == -1:
    loops_end = content.find("\n\nfunc _ms(", loops_start + 10)

if loops_start != -1 and loops_end != -1:
    content = content[:loops_start] + original_loops + content[loops_end:]

# Fix header comment
content = content.replace(
    "# 坐标系统: 六边形轴坐标(q, r) 存入 grid_position",
    "# 极坐标 (ring, slot) 存入 grid_position: ring=层, slot=横向偏移",
)
content = content.replace(
    "#   q/r = 轴坐标, 六边形距离 = max(|dq|, |dr|, |ds|) 其中 s=-q-r\n#   相邻节点必须满足六边形距离=1\n# 使用 SkillTreeCoord 进行坐标→像素转换和标准化位置生成",
    "#   ring = 离启程节点的层数, slot = 横向偏移(0=主轴, 正=一侧, 负=另一侧)\n# 使用 SkillTreeCoord 进行坐标→像素转换",
)

# Now apply the polar coordinate fix using fix_coords.py data
coords = {
    "str_s01": (1, 0),
    "str_s02": (2, 0),
    "str_s10": (2, -1),
    "str_b01": (3, 0),
    "str_s03": (3, -1),
    "str_s06": (3, -2),
    "str_s04": (4, 0),
    "str_b02": (4, -1),
    "str_s08": (4, -2),
    "str_s07": (4, -3),
    "str_b03": (5, 0),
    "str_s05": (5, -1),
    "str_b04": (5, -2),
    "str_s11": (5, -3),
    "str_s15": (5, -4),
    "str_s12": (6, 0),
    "str_b05": (6, -1),
    "str_b06": (6, -2),
    "str_s14": (6, -3),
    "str_s16": (6, -4),
    "str_s09": (6, -5),
    "str_s13": (7, 0),
    "str_s17": (7, -1),
    "str_ks01": (7, -2),
    "str_s18": (7, -3),
    "str_b07": (8, -3),
    "str_s19": (9, -3),
    "str_s20": (6, 1),
    "str_b08": (7, 1),
    "str_s21": (8, 1),
    "dex_s01": (1, 0),
    "dex_s02": (2, 0),
    "dex_s14": (2, -1),
    "dex_b01": (3, 0),
    "dex_s03": (3, -1),
    "dex_s06": (3, -2),
    "dex_b02": (4, 0),
    "dex_s12": (4, -1),
    "dex_b05": (4, -2),
    "dex_s08": (4, -3),
    "dex_s04": (5, 0),
    "dex_s13": (5, -1),
    "dex_s07": (5, -2),
    "dex_b07": (5, -3),
    "dex_b03": (6, 0),
    "dex_s05": (6, 1),
    "dex_b06": (6, -2),
    "dex_s09": (6, -3),
    "dex_b11": (6, -1),
    "dex_b04": (7, 1),
    "dex_b08": (7, -3),
    "dex_s15": (7, -2),
    "dex_s10": (7, -4),
    "dex_b09": (7, -5),
    "dex_b10": (7, -6),
    "dex_s11": (8, -5),
    "dex_ks01": (8, -6),
    "dex_s16": (7, -1),
    "dex_b12": (8, -1),
    "dex_s17": (9, -1),
    "dex_s18": (8, 1),
    "dex_b13": (9, 1),
    "dex_s19": (10, 1),
    "con_s01": (1, 0),
    "con_s02": (2, 0),
    "con_s08": (2, -1),
    "con_b01": (3, 0),
    "con_s03": (3, -1),
    "con_s06": (3, -2),
    "con_b02": (4, 0),
    "con_s09": (4, -1),
    "con_b05": (4, -2),
    "con_s04": (5, 0),
    "con_s05": (5, -1),
    "con_s10": (5, -2),
    "con_s07": (5, -3),
    "con_b03": (6, 0),
    "con_b04": (6, -1),
    "con_s11": (6, -2),
    "con_ks01": (7, -3),
    "con_s12": (7, 0),
    "con_b07": (8, 0),
    "con_s13": (9, 0),
    "con_s14": (6, 1),
    "con_b08": (7, 1),
    "con_s15": (7, -1),
    "con_b09": (8, -1),
    "int_s01": (1, 0),
    "int_s02": (2, 0),
    "int_s15": (2, -1),
    "int_b01": (3, 0),
    "int_s03": (3, -1),
    "int_s06": (3, -2),
    "int_b02": (4, 0),
    "int_s13": (4, -1),
    "int_b08": (4, -2),
    "int_s04": (5, 0),
    "int_s08": (5, -1),
    "int_s07": (5, -2),
    "int_b03": (6, 0),
    "int_b04": (6, -1),
    "int_s14": (6, -2),
    "int_b09": (6, -3),
    "int_s09": (7, -1),
    "int_s12": (7, -3),
    "int_b05": (8, -1),
    "int_ks01": (8, -3),
    "int_s16": (7, 0),
    "int_b10": (8, 0),
    "int_s17": (9, 0),
    "int_s18": (7, -2),
    "int_b11": (8, -2),
    "int_s19": (7, -4),
    "int_b12": (8, -4),
    "int_s20": (9, -4),
    "wis_s01": (1, 0),
    "wis_s02": (2, 0),
    "wis_s09": (2, -1),
    "wis_b01": (3, 0),
    "wis_s03": (3, -1),
    "wis_s06": (3, -2),
    "wis_b02": (4, 0),
    "wis_b04": (4, -1),
    "wis_s08": (4, -2),
    "wis_s04": (5, 0),
    "wis_s07": (5, -1),
    "wis_b06": (5, -2),
    "wis_b03": (6, 0),
    "wis_b05": (6, -1),
    "wis_s10": (6, -2),
    "wis_s05": (7, 0),
    "wis_ks01": (7, -1),
    "wis_s11": (8, 0),
    "wis_b07": (9, 0),
    "wis_s12": (10, 0),
    "wis_s13": (6, 1),
    "wis_b08": (7, 1),
    "wis_s14": (7, -2),
    "wis_b09": (8, -2),
    "cha_s01": (1, 0),
    "cha_s02": (2, 0),
    "cha_s09": (2, -1),
    "cha_b01": (3, 0),
    "cha_s03": (3, -1),
    "cha_s06": (3, -2),
    "cha_b02": (4, 0),
    "cha_s10": (4, -1),
    "cha_b04": (4, -2),
    "cha_s04": (5, 0),
    "cha_b06": (5, -1),
    "cha_s07": (5, -2),
    "cha_b03": (6, 0),
    "cha_s11": (6, -1),
    "cha_b05": (6, -2),
    "cha_s08": (7, -1),
    "cha_ks01": (8, -1),
    "cha_s12": (6, 1),
    "cha_b10": (7, 1),
    "cha_s13": (8, 1),
    "cha_s14": (5, 1),
    "cha_b11": (6, 2),
    "cha_s15": (7, -2),
    "cha_b12": (8, -2),
    "cha_s16": (9, -2),
}

# Apply coordinate replacements with more specific patterns
changes = 0
for node_id, (ring, slot) in coords.items():
    # Match in _ms, _mb, _mk, _make_node calls - the Vector2i is always the last arg before )
    pattern = r'("' + re.escape(node_id) + r'"[^)]*?)Vector2i\([^)]+\)'
    matches = list(re.finditer(pattern, content))
    if len(matches) == 0:
        print(f"NOT FOUND: {node_id}")
        continue
    # Replace ALL occurrences (the node appears in both definition and potentially neighbor references don't have Vector2i)
    for m in reversed(matches):
        old = m.group(0)
        new = m.group(1) + f"Vector2i({ring}, {slot})"
        content = content[: m.start()] + new + content[m.end() :]
        changes += 1

# Fix start node
start_pattern = r'("start"[^)]*?)Vector2i\([^)]+\)'
start_match = re.search(start_pattern, content)
if start_match:
    old = start_match.group(0)
    new = start_match.group(1) + "Vector2i(0, 0)"
    content = content.replace(old, new, 1)
    changes += 1

# Also fix the neighbor lists that were changed in CHA region
# Restore cha_s11 neighbors
content = content.replace(
    'n.neighbors = ["cha_b06", "cha_s16"]  # was cha_s08\n\t# Ring 7\n\tn = _ms("cha_s08"',
    'n.neighbors = ["cha_b06", "cha_s08"]\n\t# Ring 7\n\tn = _ms("cha_s08"',
)

# Write
with open(filepath, "w", encoding="utf-8-sig") as f:
    f.write(content)

print(f"Total changes: {changes}")
print("Done!")
