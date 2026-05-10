# SkillNodeData.gd
# 技能盘节点数据 —— 表示技能盘上的单个节点
# grid_position 存储 axial 坐标 (q, r)，由 SkillTreeCoord.hex_to_pixel 转像素
extends Resource
class_name SkillNodeData

# ============================================================================
# 节点标识
# ============================================================================

## 节点唯一ID（如 "str_s01", "dex_ks01"）
@export var node_id: String = ""

## 节点显示名称
@export var node_name: String = ""

## 节点类型
enum NodeType {
	SMALL,     # 小节点（数值加成）
	BIG,       # 大节点（主动技能/核心被动）
	KEYSTONE,  # 代价型大节点（有负面效果）
	START      # 启程节点（中心起点）
}
@export var node_type: NodeType = NodeType.SMALL

# ============================================================================
# 节点状态（已废弃 — 状态由 CharacterSkillTree.activated_set / available_set 管理）
# 保留枚举仅供 UI 兼容过渡
# ============================================================================

enum State {
	LOCKED,    # 未解锁，灰色
	AVAILABLE, # 可解锁，高亮
	UNLOCKED   # 已解锁，点亮
}

# ============================================================================
# 所属区域
# ============================================================================

enum Region {
	NONE,      # 无归属（启程节点）
	STR,       # 力量 — 近战/伤害
	DEX,       # 灵巧 — 远程/暴击
	CON,       # 体魄 — 防御/生存
	INT,       # 智力 — 法术/奥术
	WIS,       # 感知 — 治疗/辅助
	CHA,       # 魅力 — 指挥/社交
	TRANSITION # 过渡节点（区域交界）
}
@export var region: Region = Region.NONE

# ============================================================================
# 拓扑连接
# ============================================================================

## 相邻节点ID列表（连通图中的邻居）
var neighbors: Array = []

## 该节点是否位于区域交界处（过渡节点）
@export var is_bridge: bool = false

## 深度层级（离启程节点的最短距离，0=启程本身）
@export var depth: int = 0

# ============================================================================
# 解锁条件
# ============================================================================

## 需要的最低角色等级（0=无限制）
@export var required_level: int = 0

## 前置节点ID列表（必须点亮这些才能解锁此节点）
var prerequisites: Array = []

# ============================================================================
# 节点效果 — 小节点用属性加成
# ============================================================================

## 属性加成（小节点主要用这个）
@export var stat_bonuses: Dictionary = {}
# 示例：{ "max_hp": 5, "ac": 1, "melee_hit": 1, "melee_damage": 2,
#         "ranged_hit": 1, "critical_rate": 0.03, "speed": 1,
#         "mana_max": 5, "initiative": 2, "all_save": 1, "range_bonus": 1,
#         "morale": 1, "cha_check": 1 }

# ============================================================================
# 节点效果 — 大节点用技能效果
# ============================================================================

## 技能效果定义（大节点/Keystone用）
@export var skill_effect: String = ""
# 示例：
#   "melee_hit_plus_1"     — 被动：近战命中+1
#   "double_attack"        — 主动：连击（主行动攻击2次，第二次-3命中）
#   "whirlwind"            — 主动：旋风斩（攻击周围所有敌人）
#   "critical_x3"          — 被动：暴击伤害×3
#   "iron_wall"            — 被动：受到物理伤害-3
#   "ether_sense"          — 被动：获得2个1环法术位
#   "quick_cast"           — 被动：1次/战斗法术作为次要行动

## 是否是主动技能
@export var is_active_skill: bool = false

## 技能描述
@export_multiline var description: String = ""

# ============================================================================
# Keystone 代价（仅 KEYSTONE 类型）
# ============================================================================

## 代价描述
@export_multiline var keystone_cost: String = ""

## 代价属性加成（负值 = 惩罚）
@export var cost_bonuses: Dictionary = {}

# ============================================================================
# UI
# ============================================================================

## axial 坐标 (q, r)，pointy-top 六边形。UI 通过 SkillTreeCoord.hex_to_pixel(q, r) 转像素
@export var grid_position: Vector2i = Vector2i.ZERO

## 图标路径
@export var icon_path: String = ""

## 节点运行时状态（由 SkillTreeData 构建，不再被每角色逻辑修改）
var is_activated: bool = false


# ============================================================================
# 辅助方法
# ============================================================================

## 获取节点的完整效果描述
func get_effect_text() -> String:
	if node_type == NodeType.SMALL:
		return _get_stat_bonus_text()
	elif node_type == NodeType.KEYSTONE:
		return description + "\n[代价] " + keystone_cost
	else:
		return description


## 获取小节点属性加成文本
func _get_stat_bonus_text() -> String:
	var parts: Array[String] = []
	
	var stat_names = {
		"max_hp": "最大生命",
		"ac": "护甲",
		"melee_hit": "近战命中",
		"melee_damage": "近战伤害",
		"ranged_hit": "远程命中",
		"ranged_damage": "远程伤害",
		"critical_rate": "暴击率",
		"speed": "移动速度",
		"mana_max": "魔力上限",
		"initiative": "先攻",
		"all_save": "全豁免",
		"range_bonus": "射程",
		"morale": "士气",
		"cha_check": "魅力检定",
		"wis_check": "感知检定",
		"spell_hit": "法术命中",
		"spell_damage": "法术伤害",
		"heal_amount": "治疗量",
		"ally_bonus": "友军加成",
	}
	
	for key in stat_bonuses:
		var val = stat_bonuses[key]
		var name_str = stat_names.get(key, key)
		if typeof(val) == TYPE_FLOAT:
			if absf(val) < 1.0:
				parts.append("%s%+.0f%%" % [name_str, val * 100])
			else:
				parts.append("%s%+.0f" % [name_str, val])
		elif val > 0:
			parts.append("%s+%d" % [name_str, val])
		elif val < 0:
			parts.append("%s%d" % [name_str, val])
	
	return "、".join(parts) if parts.size() > 0 else "无加成"


## 检查该节点是否可以被点亮（不考虑相邻关系，只看等级和前置）
func can_be_unlocked(character_level: int, activated_nodes) -> bool:
	if is_activated:
		return false
	if required_level > character_level:
		return false
	for prereq in prerequisites:
		if activated_nodes is Dictionary:
			if not activated_nodes.has(prereq):
				return false
		else:
			if prereq not in activated_nodes:
				return false
	return true


## 检查是否与已点亮节点相邻（支持 Array 或 Dictionary）
func is_adjacent_to_activated(activated_nodes) -> bool:
	for neighbor_id in neighbors:
		if activated_nodes is Dictionary:
			if activated_nodes.has(neighbor_id):
				return true
		else:
			if neighbor_id in activated_nodes:
				return true
	return false


## 获取区域名称
func region_name() -> String:
	match region:
		Region.STR: return "STR"
		Region.DEX: return "DEX"
		Region.CON: return "CON"
		Region.INT: return "INT"
		Region.WIS: return "WIS"
		Region.CHA: return "CHA"
		Region.TRANSITION: return "过渡"
		_: return "无"