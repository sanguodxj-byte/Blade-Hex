# ClassTitleResolver.gd
# 职业称号判定器 — 根据已涉足的属性组合返回职业称号
# 纯标签层，不修改任何数值，仅用于 UI 展示
# 对应策划案 05-角色与职业.md → 职业称号系统
class_name ClassTitleResolver

const SkillNodeData = preload("res://src/core/skill_tree/SkillNodeData.gd")

# ============================================================================
# 属性标志位定义（位运算组合）
# ============================================================================

const FLAG_STR := 1    # 000001
const FLAG_DEX := 2    # 000010
const FLAG_CON := 4    # 000100
const FLAG_INT := 8    # 001000
const FLAG_WIS := 16   # 010000
const FLAG_CHA := 32   # 100000

## Region 枚举 → 位标志映射
static var _region_to_flag: Dictionary = {
	SkillNodeData.Region.STR: FLAG_STR,
	SkillNodeData.Region.DEX: FLAG_DEX,
	SkillNodeData.Region.CON: FLAG_CON,
	SkillNodeData.Region.INT: FLAG_INT,
	SkillNodeData.Region.WIS: FLAG_WIS,
	SkillNodeData.Region.CHA: FLAG_CHA,
}

## 位标志 → 显示用缩写
static var _flag_to_label: Dictionary = {
	FLAG_STR: "STR",
	FLAG_DEX: "DEX",
	FLAG_CON: "CON",
	FLAG_INT: "INT",
	FLAG_WIS: "WIS",
	FLAG_CHA: "CHA",
}

## 标志排列顺序（用于生成显示标签时按固定顺序排列）
const _FLAG_ORDER := [FLAG_STR, FLAG_DEX, FLAG_CON, FLAG_INT, FLAG_WIS, FLAG_CHA]

# ============================================================================
# 63 种职业称号查找表（键 = 涉足属性的位组合值）
# ============================================================================

static var _title_table: Dictionary = {}

static func _ensure_table() -> void:
	if not _title_table.is_empty():
		return

	# ---- 6 种单属性（基础职业）----
	_title_table[FLAG_STR] = "战士"
	_title_table[FLAG_DEX] = "游侠"
	_title_table[FLAG_CON] = "守卫"
	_title_table[FLAG_INT] = "法师"
	_title_table[FLAG_WIS] = "牧师"
	_title_table[FLAG_CHA] = "诗人"

	# ---- 15 种双属性 ----
	_title_table[FLAG_STR | FLAG_DEX] = "剑舞者"
	_title_table[FLAG_STR | FLAG_CON] = "重战士"
	_title_table[FLAG_STR | FLAG_INT] = "魔剑士"
	_title_table[FLAG_STR | FLAG_WIS] = "圣骑士"
	_title_table[FLAG_STR | FLAG_CHA] = "军阀"
	_title_table[FLAG_DEX | FLAG_CON] = "决斗家"
	_title_table[FLAG_DEX | FLAG_INT] = "秘射手"
	_title_table[FLAG_DEX | FLAG_WIS] = "猎人"
	_title_table[FLAG_DEX | FLAG_CHA] = "浪客"
	_title_table[FLAG_CON | FLAG_INT] = "战法师"
	_title_table[FLAG_CON | FLAG_WIS] = "苦修者"
	_title_table[FLAG_CON | FLAG_CHA] = "铁壁将军"
	_title_table[FLAG_INT | FLAG_WIS] = "贤者"
	_title_table[FLAG_INT | FLAG_CHA] = "术士"
	_title_table[FLAG_WIS | FLAG_CHA] = "神使"

	# ---- 20 种三属性 ----
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON] = "武圣"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_INT] = "魔武者"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_WIS] = "审判官"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CHA] = "战神"
	_title_table[FLAG_STR | FLAG_CON | FLAG_INT] = "铁焰魔战"
	_title_table[FLAG_STR | FLAG_CON | FLAG_WIS] = "神殿骑士"
	_title_table[FLAG_STR | FLAG_CON | FLAG_CHA] = "征服者"
	_title_table[FLAG_STR | FLAG_INT | FLAG_WIS] = "天启骑士"
	_title_table[FLAG_STR | FLAG_INT | FLAG_CHA] = "魔王"
	_title_table[FLAG_STR | FLAG_WIS | FLAG_CHA] = "圣战者"
	_title_table[FLAG_DEX | FLAG_CON | FLAG_INT] = "影法师"
	_title_table[FLAG_DEX | FLAG_CON | FLAG_WIS] = "荒野守望"
	_title_table[FLAG_DEX | FLAG_CON | FLAG_CHA] = "千面客"
	_title_table[FLAG_DEX | FLAG_INT | FLAG_WIS] = "星辰行者"
	_title_table[FLAG_DEX | FLAG_INT | FLAG_CHA] = "幻术师"
	_title_table[FLAG_DEX | FLAG_WIS | FLAG_CHA] = "风语者"
	_title_table[FLAG_CON | FLAG_INT | FLAG_WIS] = "远古守护"
	_title_table[FLAG_CON | FLAG_INT | FLAG_CHA] = "铁幕领主"
	_title_table[FLAG_CON | FLAG_WIS | FLAG_CHA] = "圣盾使"
	_title_table[FLAG_INT | FLAG_WIS | FLAG_CHA] = "天选者"

	# ---- 15 种四属性 ----
	_title_table[FLAG_CON | FLAG_INT | FLAG_WIS | FLAG_CHA] = "智者尊者"
	_title_table[FLAG_DEX | FLAG_INT | FLAG_WIS | FLAG_CHA] = "灵风大师"
	_title_table[FLAG_DEX | FLAG_CON | FLAG_WIS | FLAG_CHA] = "自然统帅"
	_title_table[FLAG_DEX | FLAG_CON | FLAG_INT | FLAG_CHA] = "暗影领主"
	_title_table[FLAG_DEX | FLAG_CON | FLAG_INT | FLAG_WIS] = "沉默之力"
	_title_table[FLAG_STR | FLAG_INT | FLAG_WIS | FLAG_CHA] = "毁灭之主"
	_title_table[FLAG_STR | FLAG_CON | FLAG_WIS | FLAG_CHA] = "铁壁圣骑"
	_title_table[FLAG_STR | FLAG_CON | FLAG_INT | FLAG_CHA] = "霸道魔将"
	_title_table[FLAG_STR | FLAG_CON | FLAG_INT | FLAG_WIS] = "深渊骑士"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_WIS | FLAG_CHA] = "战争之风"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_INT | FLAG_CHA] = "狂风魔将"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_INT | FLAG_WIS] = "独行圣者"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_CHA] = "战争之王"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_WIS] = "铁壁猎手"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_INT] = "万象魔战"

	# ---- 6 种五属性 ----
	_title_table[FLAG_DEX | FLAG_CON | FLAG_INT | FLAG_WIS | FLAG_CHA] = "万灵使者"
	_title_table[FLAG_STR | FLAG_CON | FLAG_INT | FLAG_WIS | FLAG_CHA] = "山岳之主"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_INT | FLAG_WIS | FLAG_CHA] = "星界旅者"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_WIS | FLAG_CHA] = "自然战神"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_INT | FLAG_CHA] = "铁血魔王"
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_INT | FLAG_WIS] = "深渊行者"

	# ---- 1 种全属性 ----
	_title_table[FLAG_STR | FLAG_DEX | FLAG_CON | FLAG_INT | FLAG_WIS | FLAG_CHA] = "万象"


# ============================================================================
# 公共接口
# ============================================================================

## 判定职业称号
## 参数:
##   skill_tree: 角色的技能盘实例
##   character_attrs: 角色属性字典 { "str": int, "dex": int, "con": int, "intel": int, "wis": int, "cha": int }
##                    用于主属性并列时的优先级判定（属性值高者优先），可为空字典
## 返回: Dictionary { "title": String, "flags": int, "label": String }
##   title: 职业称号（如"魔剑士"）
##   flags: 涉足属性的位组合值
##   label: 显示用标签（如"STR+INT"）
static func resolve(skill_tree, character_attrs: Dictionary = {}) -> Dictionary:
	_ensure_table()

	# 1. 计算各区域的大节点数量
	var region_big_count: Dictionary = {}
	for region_key in _region_to_flag.values():
		region_big_count[region_key] = 0

	for node_id: String in skill_tree.activated_nodes:
		var node: SkillNodeData = skill_tree.tree_data.nodes.get(node_id)
		if node == null:
			continue
		# 只统计大节点（BIG / KEYSTONE）的区域涉足
		if node.node_type == SkillNodeData.NodeType.BIG or node.node_type == SkillNodeData.NodeType.KEYSTONE:
			var flag = _region_to_flag.get(node.region, 0)
			if flag != 0:
				region_big_count[flag] = region_big_count.get(flag, 0) + 1

	# 2. 确定涉足标志（大节点数 >= 1 的区域）
	var touched_flags := 0
	for flag in region_big_count:
		if region_big_count[flag] >= 1:
			touched_flags |= flag

	# 3. 无涉足 → 无名者
	if touched_flags == 0:
		return { "title": "无名者", "flags": 0, "label": "" }

	# 4. 查表
	var title: String = _title_table.get(touched_flags, "无名者")
	if title == "":
		title = "无名者"

	# 5. 生成显示标签（按固定顺序排列）
	var label := _build_label(touched_flags, region_big_count, character_attrs)

	return { "title": title, "flags": touched_flags, "label": label }


## 快速获取称号名称（简化接口）
static func get_title(skill_tree, character_attrs: Dictionary = {}) -> String:
	return resolve(skill_tree, character_attrs).get("title", "无名者")


## 获取各区域的大节点统计（用于 UI 展示投资比例）
static func get_region_stats(skill_tree) -> Dictionary:
	var result := {
		"STR": { "big": 0, "small": 0 },
		"DEX": { "big": 0, "small": 0 },
		"CON": { "big": 0, "small": 0 },
		"INT": { "big": 0, "small": 0 },
		"WIS": { "big": 0, "small": 0 },
		"CHA": { "big": 0, "small": 0 },
	}

	var region_name_map = {
		SkillNodeData.Region.STR: "STR",
		SkillNodeData.Region.DEX: "DEX",
		SkillNodeData.Region.CON: "CON",
		SkillNodeData.Region.INT: "INT",
		SkillNodeData.Region.WIS: "WIS",
		SkillNodeData.Region.CHA: "CHA",
	}

	for node_id: String in skill_tree.activated_nodes:
		var node: SkillNodeData = skill_tree.tree_data.nodes.get(node_id)
		if node == null:
			continue
		var rname: String = region_name_map.get(node.region, "")
		if rname == "":
			continue
		if node.node_type == SkillNodeData.NodeType.BIG or node.node_type == SkillNodeData.NodeType.KEYSTONE:
			result[rname]["big"] += 1
		elif node.node_type == SkillNodeData.NodeType.SMALL:
			result[rname]["small"] += 1

	return result


# ============================================================================
# 内部辅助
# ============================================================================

## 构建显示标签（主属性排最前，其余按固定顺序）
## 主属性 = 大节点数最多的区域；并列时取角色属性值最高的
static func _build_label(touched_flags: int, region_big_count: Dictionary, character_attrs: Dictionary) -> String:
	# 收集涉足的标志
	var touched_list: Array[int] = []
	for flag in _FLAG_ORDER:
		if touched_flags & flag:
			touched_list.append(flag)

	if touched_list.is_empty():
		return ""

	# 找主属性（大节点数最多，并列取属性值最高）
	var primary_flag := touched_list[0]
	var primary_count: int = region_big_count.get(primary_flag, 0)

	for flag in touched_list:
		var count: int = region_big_count.get(flag, 0)
		if count > primary_count:
			primary_flag = flag
			primary_count = count
		elif count == primary_count and not character_attrs.is_empty():
			# 并列时比较角色属性值
			var val_new = _get_attr_value(flag, character_attrs)
			var val_cur = _get_attr_value(primary_flag, character_attrs)
			if val_new > val_cur:
				primary_flag = flag

	# 构建标签：主属性在前，其余按固定顺序
	var parts: Array[String] = []
	parts.append(_flag_to_label.get(primary_flag, "?"))
	for flag in _FLAG_ORDER:
		if flag != primary_flag and touched_flags & flag:
			parts.append(_flag_to_label.get(flag, "?"))

	return "+".join(parts)


## 位标志 → 角色属性值
static func _get_attr_value(flag: int, character_attrs: Dictionary) -> int:
	match flag:
		FLAG_STR: return character_attrs.get("str", 10)
		FLAG_DEX: return character_attrs.get("dex", 10)
		FLAG_CON: return character_attrs.get("con", 10)
		FLAG_INT: return character_attrs.get("intel", 10)
		FLAG_WIS: return character_attrs.get("wis", 10)
		FLAG_CHA: return character_attrs.get("cha", 10)
		_: return 10
