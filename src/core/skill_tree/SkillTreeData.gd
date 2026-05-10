# SkillTreeData.gd
# 技能盘完整图数据 — 150+ 节点，axial 坐标 (q, r)
# Pointy-top 六边形，6 区域从中心放射
# STR=E(1,0) DEX=SE(0,1) CON=SW(-1,1) INT=W(-1,0) WIS=NW(0,-1) CHA=NE(1,-1)
extends Resource
class_name SkillTreeData

const SkillNodeData = preload("res://src/core/skill_tree/SkillNodeData.gd")

var nodes: Dictionary = {}
const START_NODE_ID = "start"

func _init():
	_build_skill_tree()

func _build_skill_tree():
	_build_start_node()
	_build_str_region()
	_build_dex_region()
	_build_con_region()
	_build_int_region()
	_build_wis_region()
	_build_cha_region()
	_build_transition_nodes()
	_build_cross_region_loops()

# ============================================================================
# 位置生成器
# direction_idx: 0=E(STR) 1=SE(DEX) 2=SW(CON) 3=W(INT) 4=NW(WIS) 5=NE(CHA)
# ring: 距中心层数
# slot: 横向偏移（正=顺时针侧, 负=逆时针侧）
# ============================================================================

const DIRS: Array[Vector2i] = [
	Vector2i(1, 0),    # 0: E  → STR
	Vector2i(0, 1),    # 1: SE → DEX
	Vector2i(-1, 1),   # 2: SW → CON
	Vector2i(-1, 0),   # 3: W  → INT
	Vector2i(0, -1),   # 4: NW → WIS
	Vector2i(1, -1),   # 5: NE → CHA
]

func _mp(dir_idx: int, ring: int, slot: int) -> Vector2i:
	var main := DIRS[dir_idx % 6]
	var cw := DIRS[(dir_idx + 1) % 6]
	return main * ring + cw * slot

# ============================================================================
# 启程节点
# ============================================================================

func _build_start_node():
	var n = _make_node("start", "启程", SkillNodeData.NodeType.START,
		SkillNodeData.Region.NONE, 0, [], [],
		{}, "", false, "所有角色的起点。", Vector2i.ZERO)
	n.neighbors = ["str_s01", "dex_s01", "con_s01", "int_s01", "wis_s01", "cha_s01"]

# ============================================================================
# STR 力量区域 — 方向 E(1,0), dir_idx=0
# 扇形: ring递增沿(1,0), slot偏移沿顺时针CW(0,1)
# ============================================================================

func _build_str_region():
	var n
	# Ring 1
	n = _ms("str_s01", "强健体魄", 1, ["start"], {"max_hp": 3}, _mp(0, 1, 0))
	n.neighbors = ["start", "str_s02", "str_s10"]
	# Ring 2
	n = _ms("str_s02", "近战训练", 2, ["str_s01"], {"melee_hit": 1}, _mp(0, 2, 0))
	n.neighbors = ["str_s01", "str_b01"]
	n = _ms("str_s10", "战斗直觉", 2, ["str_s01"], {"melee_damage": 1}, _mp(0, 2, -1))
	n.neighbors = ["str_s01", "str_s06"]
	# Ring 3
	n = _mb("str_b01", "基础剑术", 3, [], ["str_s02"], "melee_hit_plus_1", false, "被动: 近战命中+1", _mp(0, 3, 0))
	n.neighbors = ["str_s02", "str_s03", "str_s06", "str_s04"]
	n = _ms("str_s03", "战斗节奏", 3, ["str_b01"], {"melee_damage": 1}, _mp(0, 3, -1))
	n.neighbors = ["str_b01", "str_b02"]
	n = _ms("str_s06", "武器掌握", 3, ["str_s10"], {"melee_damage": 1}, _mp(0, 3, -2))
	n.neighbors = ["str_s10", "str_b01", "str_s07"]
	# Ring 4
	n = _ms("str_s04", "迅猛之力", 4, ["str_b01"], {"melee_damage": 2}, _mp(0, 4, 0))
	n.neighbors = ["str_b01", "str_b03"]
	n = _mb("str_b02", "连击", 4, [], ["str_s03"], "double_attack", true, "主动: 攻击2次, 第二次-3命中", _mp(0, 4, -1))
	n.neighbors = ["str_s03", "str_s05", "str_s08"]
	n = _ms("str_s08", "战斗韧性", 4, ["str_b02"], {"max_hp": 5}, _mp(0, 4, -2))
	n.neighbors = ["str_b02", "str_b04"]
	n = _ms("str_s07", "致命精准", 4, ["str_s06"], {"critical_rate": 0.03}, _mp(0, 4, -3))
	n.neighbors = ["str_s06", "str_b04"]
	# Ring 5
	n = _mb("str_b03", "旋风斩", 5, [], ["str_s04"], "whirlwind", true, "主动: 攻击周围所有敌人", _mp(0, 5, 0))
	n.neighbors = ["str_s04", "str_s12", "str_s05"]
	n = _ms("str_s05", "狂战士之怒", 5, ["str_b02"], {"critical_rate": 0.05, "melee_damage": 1}, _mp(0, 5, -1))
	n.neighbors = ["str_b02", "str_b03"]
	n = _mb("str_b04", "重甲精通", 5, [], ["str_s08"], "heavy_armor", false, "被动: 护甲+3, 速度-1", _mp(0, 5, -2))
	n.neighbors = ["str_s08", "str_s07", "str_s11"]
	n = _ms("str_s11", "铁壁防御", 5, ["str_b04"], {"ac": 2}, _mp(0, 5, -3))
	n.neighbors = ["str_b04", "str_s15"]
	n = _ms("str_s15", "盾墙", 5, ["str_s11"], {"ac": 1, "all_save": 1}, _mp(0, 5, -4))
	n.neighbors = ["str_s11"]
	# Ring 6
	n = _ms("str_s12", "战意高昂", 6, ["str_b03"], {"melee_damage": 2, "max_hp": 5}, _mp(0, 6, 0))
	n.neighbors = ["str_b03", "str_b05"]
	n = _mb("str_b05", "暴击大师", 6, [], ["str_s05"], "critical_master", false, "被动: 暴击伤害×3", _mp(0, 6, -1))
	n.neighbors = ["str_s05", "str_s14"]
	n = _mb("str_b06", "嗜血", 6, [5], ["str_b04"], "bloodthirst", true, "主动: 近战击杀敌人后立即获得额外行动", _mp(0, 6, -2))
	n.neighbors = ["str_b04", "str_s16"]
	n = _ms("str_s14", "战场怒吼", 6, ["str_b05"], {"morale": 1, "melee_hit": 1}, _mp(0, 6, -3))
	n.neighbors = ["str_b05", "str_ks01"]
	n = _ms("str_s16", "无畏冲锋", 6, ["str_b06"], {"melee_damage": 2, "speed": 1}, _mp(0, 6, -4))
	n.neighbors = ["str_b06", "str_s09"]
	n = _ms("str_s09", "巨力挥击", 6, ["str_s16"], {"melee_damage": 3}, _mp(0, 6, -5))
	n.neighbors = ["str_s16"]
	# Ring 7
	n = _ms("str_s13", "武器大师", 7, ["str_s12"], {"melee_hit": 2, "melee_damage": 2}, _mp(0, 7, 0))
	n.neighbors = ["str_s12", "str_s17"]
	n = _ms("str_s17", "不屈意志", 7, ["str_s13"], {"max_hp": 8, "all_save": 2}, _mp(0, 7, -1))
	n.neighbors = ["str_s13"]
	n = _mk("str_ks01", "狂暴之力", 7, ["str_s14"], "berserk_power", "近战伤害+50%", "AC-3, 不能使用盾牌", {"ac": -3}, _mp(0, 7, -2))
	n.neighbors = ["str_s14"]
	n = _ms("str_s18", "战场本能", 7, ["str_b06"], {"max_hp": 5, "melee_hit": 1}, _mp(0, 7, 1))
	n.neighbors = ["str_b06", "str_b07"]
	# 外扩: Ring 8-9
	n = _mb("str_b07", "战斗怒吼", 8, [7], ["str_s18"], "battle_cry", true, "主动: 怒吼震慑周围敌人使其下回合攻击-2, 同时友军士气+3", _mp(0, 8, 1))
	n.neighbors = ["str_s18", "str_s19"]
	n = _ms("str_s19", "血性狂热", 9, ["str_b07"], {"melee_damage": 2, "max_hp": 5}, _mp(0, 9, 1))
	n.neighbors = ["str_b07"]
	# 血腥漩涡分支 (从str_b03延伸)
	n = _ms("str_s20", "战争践踏", 6, ["str_b03"], {"melee_damage": 2, "ac": -1}, _mp(0, 6, 1))
	n.neighbors = ["str_b03", "str_b08"]
	n = _mb("str_b08", "血腥漩涡", 7, [7], ["str_s20"], "blood_vortex", true, "主动: 横扫周围所有敌人, 每命中1个敌人恢复自身1d6HP", _mp(0, 7, 2))
	n.neighbors = ["str_s20", "str_s21"]
	n = _ms("str_s21", "杀戮本能", 8, ["str_b08"], {"critical_rate": 0.05, "melee_damage": 1}, _mp(0, 8, 2))
	n.neighbors = ["str_b08", "str_s19"]

# ============================================================================
# DEX 灵巧区域 — 方向 SE(0,1), dir_idx=1
# ============================================================================

func _build_dex_region():
	var n
	# Ring 1
	n = _ms("dex_s01", "轻灵步伐", 1, ["start"], {"ac": 1}, _mp(1, 1, 0))
	n.neighbors = ["start", "dex_s02"]
	# Ring 2
	n = _ms("dex_s02", "迅捷反应", 2, ["dex_s01"], {"initiative": 2}, _mp(1, 2, 0))
	n.neighbors = ["dex_s01", "dex_b01"]
	n = _ms("dex_s14", "灵活身姿", 2, ["dex_s01"], {"ac": 1}, _mp(1, 2, -1))
	n.neighbors = ["dex_s01", "dex_s06"]
	# Ring 3
	n = _mb("dex_b01", "基础射击", 3, [], ["dex_s02"], "ranged_hit_plus_1", false, "被动: 远程命中+1", _mp(1, 3, 0))
	n.neighbors = ["dex_s02", "dex_s03", "dex_s06"]
	n = _ms("dex_s03", "瞄准训练", 3, ["dex_b01"], {"ranged_hit": 1}, _mp(1, 3, -1))
	n.neighbors = ["dex_b01", "dex_b02", "dex_s12"]
	n = _ms("dex_s06", "穿透之力", 3, ["dex_s14"], {"ranged_damage": 1}, _mp(1, 3, -2))
	n.neighbors = ["dex_s14", "dex_b01", "dex_s08", "dex_b05"]
	# Ring 4
	n = _mb("dex_b02", "精准射击", 4, [], ["dex_s03"], "aimed_shot", true, "主动: 瞄准后射击优势+伤害x2", _mp(1, 4, 0))
	n.neighbors = ["dex_s03", "dex_s04", "dex_s13"]
	n = _ms("dex_s12", "精准本能", 4, ["dex_s03"], {"ranged_hit": 1}, _mp(1, 4, -1))
	n.neighbors = ["dex_s03", "dex_s06"]
	n = _mb("dex_b05", "穿透射击", 4, [], ["dex_s06"], "piercing_shot", false, "被动: 箭矢穿透击中后方1个敌人", _mp(1, 4, -2))
	n.neighbors = ["dex_s06", "dex_s07"]
	n = _ms("dex_s08", "暗影步伐", 4, ["dex_s06"], {"critical_rate": 0.02}, _mp(1, 4, -3))
	n.neighbors = ["dex_s06", "dex_b07"]
	# Ring 5
	n = _ms("dex_s04", "速射技巧", 5, ["dex_b02"], {"ranged_damage": 1}, _mp(1, 5, 0))
	n.neighbors = ["dex_b02", "dex_b03"]
	n = _ms("dex_s13", "游侠之道", 5, ["dex_b02"], {"initiative": 2}, _mp(1, 5, -1))
	n.neighbors = ["dex_b02", "dex_b07"]
	n = _ms("dex_s07", "长距瞄准", 5, ["dex_b05"], {"range_bonus": 1}, _mp(1, 5, -2))
	n.neighbors = ["dex_b05", "dex_b06"]
	n = _mb("dex_b07", "隐匿", 5, [], ["dex_s08"], "stealth", true, "主动: 进入潜行状态", _mp(1, 5, -3))
	n.neighbors = ["dex_s08", "dex_s09", "dex_s13"]
	# Ring 6
	n = _mb("dex_b03", "连珠箭", 6, [], ["dex_s04"], "multi_shot", true, "主动: 连射3支箭, 每支-2命中", _mp(1, 6, 0))
	n.neighbors = ["dex_s04", "dex_s05"]
	n = _ms("dex_s05", "鹰眼", 6, ["dex_b03"], {"ranged_hit": 2}, _mp(1, 6, 1))
	n.neighbors = ["dex_b03"]
	n = _mb("dex_b06", "致盲箭", 6, [], ["dex_s07"], "blind_arrow", true, "主动: 命中后目标-4命中(2回合)", _mp(1, 6, -1))
	n.neighbors = ["dex_s07", "dex_s09"]
	n = _ms("dex_s09", "毒蛇之牙", 6, ["dex_b07"], {"critical_rate": 0.03, "ranged_damage": 1}, _mp(1, 6, -2))
	n.neighbors = ["dex_b07", "dex_b06"]
	n = _mb("dex_b11", "陷阱大师", 6, [], ["dex_s13"], "trap_master", true, "主动: 放置陷阱, 触发的敌人停止移动并受1d8伤害", _mp(1, 6, -3))
	n.neighbors = ["dex_s13"]
	# Ring 7
	n = _mb("dex_b04", "剑舞", 7, [], ["dex_s05"], "sword_dance", true, "主动: 对周围所有敌人进行近战攻击", _mp(1, 7, 1))
	n.neighbors = ["dex_s05"]
	n = _ms("dex_s16", "疾风步", 7, ["dex_b11"], {"speed": 2, "ac": 1}, _mp(1, 7, -1))
	n.neighbors = ["dex_b11", "dex_b12"]
	n = _ms("dex_s15", "暗影之舞", 7, ["dex_s09"], {"critical_rate": 0.05, "ac": 1}, _mp(1, 7, -2))
	n.neighbors = ["dex_s09", "dex_b08"]
	n = _ms("dex_s10", "致命毒药", 7, ["dex_b08"], {"ranged_damage": 1}, _mp(1, 7, -3))
	n.neighbors = ["dex_b08", "dex_b09", "dex_b10"]
	n = _mb("dex_b08", "暗影突袭", 7, [], ["dex_s15"], "shadow_strike", true, "主动: 潜行状态下突袭伤害翻倍", _mp(1, 7, -4))
	n.neighbors = ["dex_s15", "dex_s10"]
	n = _mb("dex_b09", "致命一击", 7, [], ["dex_s10"], "deadly_blow", false, "被动: 偷袭伤害+3d6", _mp(1, 7, -5))
	n.neighbors = ["dex_s10", "dex_s11"]
	n = _mb("dex_b10", "毒刃", 7, [], ["dex_s10"], "poison_blade", true, "主动: 攻击附带中毒(每回合1d4, 3回合)", _mp(1, 7, -6))
	n.neighbors = ["dex_s10"]
	# Ring 8
	n = _ms("dex_s11", "幽灵之触", 8, ["dex_b09"], {"critical_rate": 0.05}, _mp(1, 8, -5))
	n.neighbors = ["dex_b09", "dex_ks01"]
	n = _mk("dex_ks01", "幽灵步伐", 8, ["dex_s11"], "ghost_step", "永久获得掩护状态(远程攻击-2命中)", "HP上限-20%", {"max_hp_pct": -0.2}, _mp(1, 8, -6))
	n.neighbors = ["dex_s11"]
	n = _mb("dex_b12", "闪电反射", 8, [], ["dex_s16"], "lightning_reflex", false, "被动: 先攻+5, 每场战斗第一次攻击优势", _mp(1, 8, -1))
	n.neighbors = ["dex_s16", "dex_s17"]
	n = _ms("dex_s17", "幻影连步", 8, ["dex_b12"], {"initiative": 3, "ac": 1}, _mp(1, 9, -1))
	n.neighbors = ["dex_b12"]
	# 外扩分支
	n = _ms("dex_s18", "射手之道", 8, ["dex_b04"], {"ranged_hit": 2}, _mp(1, 8, 1))
	n.neighbors = ["dex_b04", "dex_b13"]
	n = _mb("dex_b13", "流星箭雨", 9, [], ["dex_s18"], "meteor_shower", true, "主动: 向区域倾泻箭雨, 所有目标受2d8伤害", _mp(1, 9, 1))
	n.neighbors = ["dex_s18", "dex_s19"]
	n = _ms("dex_s19", "元素共鸣", 9, ["dex_b13"], {"ranged_hit": 2, "ranged_damage": 2}, _mp(1, 10, 1))
	n.neighbors = ["dex_b13"]

# ============================================================================
# CON 体魄区域 — 方向 SW(-1,1), dir_idx=2
# ============================================================================

func _build_con_region():
	var n
	# Ring 1
	n = _ms("con_s01", "强韧体质", 1, ["start"], {"max_hp": 5}, _mp(2, 1, 0))
	n.neighbors = ["start", "con_s02"]
	# Ring 2
	n = _ms("con_s02", "坚固体格", 2, ["con_s01"], {"ac": 1}, _mp(2, 2, 0))
	n.neighbors = ["con_s01", "con_b01"]
	n = _ms("con_s08", "铁壁之心", 2, ["con_s01"], {"ac": 1}, _mp(2, 2, -1))
	n.neighbors = ["con_s01", "con_s06"]
	# Ring 3
	n = _mb("con_b01", "盾击", 3, [], ["con_s02"], "shield_bash", true, "主动: 攻击+推开目标1格", _mp(2, 3, 0))
	n.neighbors = ["con_s02", "con_s03", "con_s06"]
	n = _ms("con_s03", "厚甲训练", 3, ["con_b01"], {"ac": 1}, _mp(2, 3, -1))
	n.neighbors = ["con_b01", "con_b02"]
	n = _ms("con_s06", "格挡本能", 3, ["con_s08"], {"all_save": 1}, _mp(2, 3, -2))
	n.neighbors = ["con_s08", "con_b01"]
	# Ring 4
	n = _mb("con_b02", "坚壁清野", 4, [], ["con_s03"], "fortify", false, "被动: 受到伤害时AC+2(1回合)", _mp(2, 4, 0))
	n.neighbors = ["con_s03", "con_s09", "con_s04"]
	n = _ms("con_s09", "体力充沛", 4, ["con_b02"], {"max_hp": 5}, _mp(2, 4, -1))
	n.neighbors = ["con_b02", "con_b05"]
	n = _mb("con_b05", "铁壁", 4, [], ["con_s06"], "iron_wall", false, "被动: 受到物理伤害-3", _mp(2, 4, -2))
	n.neighbors = ["con_s06", "con_s05"]
	# Ring 5
	n = _ms("con_s04", "生命之泉", 5, ["con_b02"], {"max_hp": 8}, _mp(2, 5, 0))
	n.neighbors = ["con_b02", "con_b03"]
	n = _ms("con_s05", "再生之力", 5, ["con_b05"], {"max_hp": 5, "heal_amount": 1}, _mp(2, 5, -1))
	n.neighbors = ["con_b05", "con_s10"]
	n = _ms("con_s10", "不灭意志", 5, ["con_s05"], {"all_save": 2, "max_hp": 5}, _mp(2, 5, -2))
	n.neighbors = ["con_s05", "con_s07"]
	n = _ms("con_s07", "元素抗性", 5, ["con_s10"], {"ac": 1, "all_save": 1}, _mp(2, 5, -3))
	n.neighbors = ["con_s10"]
	# Ring 6
	n = _mb("con_b03", "不屈", 6, [], ["con_s04"], "unyielding", false, "机制: HP低于25%时伤害减半", _mp(2, 6, 0))
	n.neighbors = ["con_s04", "con_s12"]
	n = _mb("con_b04", "生命之盾", 6, [], ["con_s09"], "life_shield", true, "主动: 获得等于最大HP30%的临时护盾(3回合)", _mp(2, 6, -1))
	n.neighbors = ["con_s09", "con_s11"]
	n = _ms("con_s11", "铁血战士", 6, ["con_b04"], {"max_hp": 10, "melee_damage": 1}, _mp(2, 6, -2))
	n.neighbors = ["con_b04", "con_ks01"]
	# Ring 7
	n = _mk("con_ks01", "不朽之躯", 7, ["con_s11"], "immortal_body", "HP低于0时1/战斗概率=体质修正恢复1HP", "移动速度-2", {"speed": -2}, _mp(2, 7, -3))
	n.neighbors = ["con_s11"]
	n = _ms("con_s12", "活力涌动", 7, ["con_b03"], {"max_hp": 10, "heal_amount": 1}, _mp(2, 7, 0))
	n.neighbors = ["con_b03", "con_b07"]
	n = _mb("con_b07", "生命之环", 8, [], ["con_s12"], "life_circle", true, "主动: 治疗周围所有友军2d10+体质修正HP", _mp(2, 8, 0))
	n.neighbors = ["con_s12", "con_s13"]
	n = _ms("con_s13", "坚不可摧", 9, ["con_b07"], {"ac": 2, "all_save": 2}, _mp(2, 9, 0))
	n.neighbors = ["con_b07"]
	# 外扩分支
	n = _ms("con_s14", "厚皮", 6, ["con_s06"], {"ac": 1}, _mp(2, 6, 1))
	n.neighbors = ["con_s06", "con_b08"]
	n = _mb("con_b08", "巨人之力", 7, [], ["con_s14"], "giant_strength", false, "被动: 近战伤害+3, 可使用双手武器单手", _mp(2, 7, 1))
	n.neighbors = ["con_s14", "con_s15"]
	n = _ms("con_s15", "山岳之躯", 7, ["con_b08"], {"ac": 2, "max_hp": 10}, _mp(2, 7, -1))
	n.neighbors = ["con_b08", "con_b09"]
	n = _mb("con_b09", "最后阵地", 8, [7], ["con_s15"], "last_stand", false, "机制: HP低于25%时自动获得AC+5和伤害+50%, 直到HP恢复至25%以上", _mp(2, 8, -1))
	n.neighbors = ["con_s15"]

# ============================================================================
# INT 智力区域 — 方向 W(-1,0), dir_idx=3
# ============================================================================

func _build_int_region():
	var n
	# Ring 1
	n = _ms("int_s01", "魔力觉醒", 1, ["start"], {"mana_max": 3}, _mp(3, 1, 0))
	n.neighbors = ["start", "int_s02"]
	# Ring 2
	n = _ms("int_s02", "奥术基础", 2, ["int_s01"], {"mana_max": 2}, _mp(3, 2, 0))
	n.neighbors = ["int_s01", "int_b01"]
	n = _ms("int_s15", "元素亲和", 2, ["int_s01"], {"spell_hit": 1}, _mp(3, 2, -1))
	n.neighbors = ["int_s01", "int_s06"]
	# Ring 3
	n = _mb("int_b01", "法术强化", 3, [], ["int_s02"], "spell_hit_plus_1", false, "被动: 法术命中+1", _mp(3, 3, 0))
	n.neighbors = ["int_s02", "int_s03", "int_s06"]
	n = _ms("int_s03", "魔力涌流", 3, ["int_b01"], {"mana_max": 3}, _mp(3, 3, -1))
	n.neighbors = ["int_b01", "int_b02"]
	n = _ms("int_s06", "法力护盾", 3, ["int_s15"], {"mana_max": 2, "ac": 1}, _mp(3, 3, -2))
	n.neighbors = ["int_s15", "int_b01"]
	# Ring 4
	n = _mb("int_b02", "奥术爆发", 4, [], ["int_s03"], "arcane_burst", true, "主动: 对目标造成2d8奥术伤害", _mp(3, 4, 0))
	n.neighbors = ["int_s03", "int_s04", "int_s08"]
	n = _ms("int_s13", "魔力回涌", 4, ["int_b01"], {"mana_max": 3}, _mp(3, 4, -1))
	n.neighbors = ["int_b01"]
	n = _mb("int_b08", "魔力汲取", 4, [], ["int_s06"], "mana_drain", true, "主动: 汲取目标法力恢复自身", _mp(3, 4, -2))
	n.neighbors = ["int_s06", "int_s07"]
	# Ring 5
	n = _ms("int_s04", "法术穿透", 5, ["int_b02"], {"spell_damage": 1}, _mp(3, 5, 0))
	n.neighbors = ["int_b02", "int_b03"]
	n = _ms("int_s08", "奥术护盾", 5, ["int_b02"], {"mana_max": 3, "ac": 1}, _mp(3, 5, -1))
	n.neighbors = ["int_b02", "int_b04"]
	n = _ms("int_s07", "元素精通", 5, ["int_b08"], {"spell_damage": 1, "mana_max": 2}, _mp(3, 5, -2))
	n.neighbors = ["int_b08", "int_b03"]
	# Ring 6
	n = _mb("int_b03", "连锁闪电", 6, [], ["int_s04"], "chain_lightning", true, "主动: 闪电跳跃攻击最多3个目标", _mp(3, 6, 0))
	n.neighbors = ["int_s04", "int_s07", "int_b04"]
	n = _mb("int_b04", "法术反射", 6, [], ["int_s08"], "spell_reflect", false, "被动: 1次/回合反射敌方法术", _mp(3, 6, -1))
	n.neighbors = ["int_s08", "int_s14"]
	n = _ms("int_s14", "奥术精通", 6, ["int_b04"], {"spell_damage": 1, "mana_max": 3}, _mp(3, 6, -2))
	n.neighbors = ["int_b04", "int_b09"]
	n = _mb("int_b09", "时间扭曲", 6, [], ["int_s14"], "time_warp", true, "主动: 重新获得本回合主行动", _mp(3, 6, -3))
	n.neighbors = ["int_s14", "int_s12"]
	# Ring 7
	n = _ms("int_s09", "法术大师", 7, ["int_b03"], {"spell_hit": 2, "spell_damage": 1}, _mp(3, 7, -1))
	n.neighbors = ["int_b03"]
	n = _ms("int_s12", "专注之心", 7, ["int_b09"], {"spell_damage": 2}, _mp(3, 7, -3))
	n.neighbors = ["int_b09", "int_ks01"]
	n = _mb("int_b05", "奥术炸弹", 8, [], ["int_s09"], "arcane_bomb", true, "主动: 范围奥术爆炸3d6伤害", _mp(3, 8, -1))
	n.neighbors = ["int_s09"]
	n = _mk("int_ks01", "绝对专注", 8, ["int_s12"], "absolute_focus", "法术DC+4", "不能学习其他体系的法术", {}, _mp(3, 8, -3))
	n.neighbors = ["int_s12"]
	# 外扩分支
	n = _ms("int_s16", "学者智慧", 7, ["int_b04"], {"mana_max": 5, "all_save": 1}, _mp(3, 7, 0))
	n.neighbors = ["int_b04", "int_b10"]
	n = _mb("int_b10", "知识就是力量", 8, [], ["int_s16"], "knowledge_power", false, "被动: 法术伤害额外+智力修正", _mp(3, 8, 0))
	n.neighbors = ["int_s16", "int_s17"]
	n = _ms("int_s17", "护盾强化", 8, ["int_b10"], {"mana_max": 5, "ac": 1}, _mp(3, 9, 0))
	n.neighbors = ["int_b10"]
	n = _ms("int_s18", "奥术回响", 7, ["int_s14"], {"mana_max": 3}, _mp(3, 7, -2))
	n.neighbors = ["int_s14", "int_b11"]
	n = _mb("int_b11", "虚空之门", 8, [], ["int_s18"], "void_gate", true, "主动: 传送至视野内任意位置", _mp(3, 8, -2))
	n.neighbors = ["int_s18"]
	n = _ms("int_s19", "预知未来", 7, ["int_b09"], {"all_save": 2, "initiative": 2}, _mp(3, 7, -4))
	n.neighbors = ["int_b09", "int_b12"]
	n = _mb("int_b12", "命运之眼", 8, [], ["int_s19"], "fate_eye", false, "机制: 每场战斗重掷1次失败的豁免", _mp(3, 8, -4))
	n.neighbors = ["int_s19", "int_s20"]
	n = _ms("int_s20", "时空裂隙", 9, ["int_b12"], {"mana_max": 10, "speed": 2}, _mp(3, 9, -4))
	n.neighbors = ["int_b12"]

# ============================================================================
# WIS 感知区域 — 方向 NW(0,-1), dir_idx=4
# ============================================================================

func _build_wis_region():
	var n
	# Ring 1
	n = _ms("wis_s01", "治愈之心", 1, ["start"], {"heal_amount": 1}, _mp(4, 1, 0))
	n.neighbors = ["start", "wis_s02"]
	# Ring 2
	n = _ms("wis_s02", "虔诚信仰", 2, ["wis_s01"], {"mana_max": 2}, _mp(4, 2, 0))
	n.neighbors = ["wis_s01", "wis_b01"]
	n = _ms("wis_s09", "洞察之力", 2, ["wis_s01"], {"wis_check": 1}, _mp(4, 2, -1))
	n.neighbors = ["wis_s01", "wis_s06"]
	# Ring 3
	n = _mb("wis_b01", "基础治疗", 3, [], ["wis_s02"], "basic_heal", true, "主动: 治疗1d8+感知修正HP", _mp(4, 3, 0))
	n.neighbors = ["wis_s02", "wis_s03", "wis_s06"]
	n = _ms("wis_s03", "净化之触", 3, ["wis_b01"], {"wis_check": 1}, _mp(4, 3, -1))
	n.neighbors = ["wis_b01", "wis_b02"]
	n = _ms("wis_s06", "神圣庇护", 3, ["wis_s09"], {"ac": 1}, _mp(4, 3, -2))
	n.neighbors = ["wis_s09", "wis_b01", "wis_s08"]
	# Ring 4
	n = _mb("wis_b02", "群体治疗", 4, [], ["wis_s03"], "group_heal", true, "主动: 治疗周围所有友军1d6+感知修正", _mp(4, 4, 0))
	n.neighbors = ["wis_s03", "wis_s04", "wis_b04"]
	n = _mb("wis_b04", "神圣光芒", 4, [], ["wis_s06"], "holy_light", true, "主动: 照亮黑暗区域, 亡灵受1d10伤害", _mp(4, 4, -1))
	n.neighbors = ["wis_s06", "wis_s08"]
	n = _ms("wis_s08", "坚韧灵魂", 4, ["wis_s06"], {"all_save": 1}, _mp(4, 4, -2))
	n.neighbors = ["wis_s06", "wis_b06"]
	# Ring 5
	n = _ms("wis_s04", "神恩", 5, ["wis_b02"], {"heal_amount": 2}, _mp(4, 5, 0))
	n.neighbors = ["wis_b02", "wis_b03"]
	n = _ms("wis_s07", "驱散邪恶", 5, ["wis_b04"], {"wis_check": 2}, _mp(4, 5, -1))
	n.neighbors = ["wis_b04", "wis_b06"]
	n = _mb("wis_b06", "守护之灵", 5, [], ["wis_s08"], "guardian_spirit", true, "主动: 召唤守护灵为友军挡一次致命攻击", _mp(4, 5, -2))
	n.neighbors = ["wis_s08", "wis_s07"]
	# Ring 6
	n = _mb("wis_b03", "复活", 6, [], ["wis_s04"], "resurrect", true, "主动: 复活1名阵亡队友(半HP)", _mp(4, 6, 0))
	n.neighbors = ["wis_s04", "wis_s05"]
	n = _mb("wis_b05", "神圣制裁", 6, [], ["wis_s07"], "divine_judgment", true, "主动: 对邪恶目标造成3d10神圣伤害", _mp(4, 6, -1))
	n.neighbors = ["wis_s07", "wis_s10"]
	n = _ms("wis_s10", "信仰之盾", 6, ["wis_b05"], {"ac": 2, "heal_amount": 1}, _mp(4, 6, -2))
	n.neighbors = ["wis_b05"]
	# Ring 7
	n = _ms("wis_s05", "大治愈术", 7, ["wis_b03"], {"heal_amount": 3, "mana_max": 5}, _mp(4, 7, 0))
	n.neighbors = ["wis_b03", "wis_ks01"]
	n = _mk("wis_ks01", "神之手", 7, ["wis_s05"], "divine_hand", "治疗效果+50%", "不能造成任何伤害", {"spell_damage": -99}, _mp(4, 7, -1))
	n.neighbors = ["wis_s05"]
	# 外扩分支
	n = _ms("wis_s11", "先知之眼", 8, ["wis_ks01"], {"wis_check": 3}, _mp(4, 8, 0))
	n.neighbors = ["wis_ks01", "wis_b07"]
	n = _mb("wis_b07", "神谕", 9, [], ["wis_s11"], "oracle", true, "主动: 揭示隐藏的陷阱/宝藏/敌人弱点", _mp(4, 9, 0))
	n.neighbors = ["wis_s11", "wis_s12"]
	n = _ms("wis_s12", "圣光之盾", 9, ["wis_b07"], {"ac": 2, "heal_amount": 1}, _mp(4, 10, 0))
	n.neighbors = ["wis_b07"]
	n = _ms("wis_s13", "自然之怒", 6, ["wis_s10"], {"spell_damage": 1}, _mp(4, 6, 1))
	n.neighbors = ["wis_s10", "wis_b08"]
	n = _mb("wis_b08", "元素风暴", 7, [], ["wis_s13"], "elemental_storm", true, "主动: 召唤自然之力攻击区域内所有敌人2d8", _mp(4, 7, 1))
	n.neighbors = ["wis_s13", "wis_s14"]
	n = _ms("wis_s14", "荆棘之环", 7, ["wis_b08"], {"ac": 1, "wis_check": 1}, _mp(4, 7, -2))
	n.neighbors = ["wis_b08", "wis_b09"]
	n = _mb("wis_b09", "灵魂守护", 8, [7], ["wis_s14"], "soul_guardian", false, "机制: 当友军HP降至0时自动触发, 恢复其1d10+WIS修正HP, 每场战斗限1次", _mp(4, 8, -2))
	n.neighbors = ["wis_s14"]

# ============================================================================
# CHA 魅力区域 — 方向 NE(1,-1), dir_idx=5
# ============================================================================

func _build_cha_region():
	var n
	# Ring 1
	n = _ms("cha_s01", "鼓舞士气", 1, ["start"], {"morale": 1}, _mp(5, 1, 0))
	n.neighbors = ["start", "cha_s02"]
	# Ring 2
	n = _ms("cha_s02", "领袖气质", 2, ["cha_s01"], {"cha_check": 1}, _mp(5, 2, 0))
	n.neighbors = ["cha_s01", "cha_b01"]
	n = _ms("cha_s09", "交际手腕", 2, ["cha_s01"], {"morale": 1}, _mp(5, 2, -1))
	n.neighbors = ["cha_s01", "cha_s06"]
	# Ring 3
	n = _mb("cha_b01", "指挥", 3, [], ["cha_s02"], "command", true, "主动: 指令1名友军立即行动", _mp(5, 3, 0))
	n.neighbors = ["cha_s02", "cha_s03", "cha_s06"]
	n = _ms("cha_s03", "威压", 3, ["cha_b01"], {"cha_check": 1}, _mp(5, 3, -1))
	n.neighbors = ["cha_b01", "cha_b02"]
	n = _ms("cha_s06", "团结之力", 3, ["cha_s09"], {"ally_bonus": 1}, _mp(5, 3, -2))
	n.neighbors = ["cha_s09", "cha_b01", "cha_b04"]
	# Ring 4
	n = _mb("cha_b02", "集结号令", 4, [], ["cha_s03"], "rally", true, "主动: 所有友军下回合攻击+2", _mp(5, 4, 0))
	n.neighbors = ["cha_s03", "cha_s04"]
	n = _ms("cha_s10", "声东击西", 4, ["cha_s06"], {"cha_check": 1, "initiative": 1}, _mp(5, 4, -1))
	n.neighbors = ["cha_s06", "cha_b06"]
	n = _mb("cha_b04", "外交官", 4, [], ["cha_s06"], "diplomat", false, "被动: 商店价格-15%", _mp(5, 4, -2))
	n.neighbors = ["cha_s06", "cha_s14"]
	# Ring 5
	n = _ms("cha_s04", "统率之力", 5, ["cha_b02"], {"ally_bonus": 1}, _mp(5, 5, 0))
	n.neighbors = ["cha_b02", "cha_b03"]
	n = _mb("cha_b06", "暗影交易", 5, [], ["cha_s10"], "shadow_deal", true, "主动: 贿赂敌人使其1回合不攻击", _mp(5, 5, -1))
	n.neighbors = ["cha_s10", "cha_s07"]
	n = _ms("cha_s07", "鼓舞之歌", 5, ["cha_b06"], {"morale": 2, "ally_bonus": 1}, _mp(5, 5, -2))
	n.neighbors = ["cha_b06", "cha_b05"]
	# Ring 6
	n = _mb("cha_b03", "统帅光环", 6, [], ["cha_s04"], "command_aura", false, "被动: 周围友军攻击+1 AC+1", _mp(5, 6, 0))
	n.neighbors = ["cha_s04", "cha_s11"]
	n = _mb("cha_b05", "威压", 6, [], ["cha_s07"], "intimidate", true, "主动: 敌人攻击检定-2(3回合), WIS豁免", _mp(5, 6, -1))
	n.neighbors = ["cha_s07", "cha_s08"]
	n = _ms("cha_s11", "王者风范", 6, ["cha_b03"], {"ally_bonus": 1, "morale": 1}, _mp(5, 6, -2))
	n.neighbors = ["cha_b03", "cha_s12"]
	n = _ms("cha_s12", "领袖魅力", 6, ["cha_s11"], {"morale": 2, "ally_bonus": 1}, _mp(5, 6, -3))
	n.neighbors = ["cha_s11", "cha_b10"]
	# Ring 7
	n = _ms("cha_s08", "王者之心", 7, ["cha_b05"], {"ally_bonus": 1}, _mp(5, 7, -1))
	n.neighbors = ["cha_b05", "cha_ks01"]
	n = _mk("cha_ks01", "君临天下", 7, ["cha_s08"], "royal_presence", "范围内友军全豁免+2不会恐慌", "自身HP-20%", {"max_hp_pct": -0.2}, _mp(5, 8, -1))
	n.neighbors = ["cha_s08"]
	n = _mb("cha_b10", "英雄号召", 7, [7], ["cha_s12"], "heroic_call", true, "主动: 插下战旗, 周围友军获得攻击+2和AC+1持续3回合", _mp(5, 7, -2))
	n.neighbors = ["cha_s12", "cha_s13"]
	n = _ms("cha_s13", "传奇号召", 8, ["cha_b10"], {"morale": 3, "cha_check": 2}, _mp(5, 8, -2))
	n.neighbors = ["cha_b10"]
	# 外扩分支
	n = _ms("cha_s14", "商业嗅觉", 5, ["cha_b04"], {"cha_check": 1, "morale": 1}, _mp(5, 5, 1))
	n.neighbors = ["cha_b04", "cha_b11"]
	n = _mb("cha_b11", "商业帝国", 6, [5], ["cha_s14"], "merchant_empire", false, "机制: 每次战斗结束额外获得敌人等级x5金币, 商店出现稀有物品概率+15%", _mp(5, 6, 1))
	n.neighbors = ["cha_s14"]
	n = _ms("cha_s15", "仇恨刻印", 7, ["cha_s08"], {"morale": 2, "melee_damage": 1}, _mp(5, 7, 0))
	n.neighbors = ["cha_b03", "cha_b12"]
	n = _mb("cha_b12", "复仇誓言", 8, [7], ["cha_s15"], "vow_of_vengeance", false, "机制: 标记1个敌人为复仇目标, 对其造成的伤害+25%, 目标死亡时恢复全队10%HP", _mp(5, 8, 0))
	n.neighbors = ["cha_s15", "cha_s16"]
	n = _ms("cha_s16", "血债血偿", 9, ["cha_b12"], {"morale": 2, "ally_bonus": 2}, _mp(5, 9, 0))
	n.neighbors = ["cha_b12"]

# ============================================================================
# 过渡节点 — 位于相邻区域交界，使用两区域方向的插值位置
# ============================================================================

func _build_transition_nodes():
	var n
	# STR(0) <-> DEX(1): 中间方向 = E+SE=(1,1)
	n = _ms("trans_sd01", "战斗技巧", 2, ["str_s01", "dex_s01"], {"melee_hit": 1, "critical_rate": 0.02}, Vector2i(1, 1))
	n.is_bridge = true
	n = _ms("trans_sd02", "武器大师", 4, ["str_b01", "dex_b01"], {"melee_hit": 1, "ranged_hit": 1}, Vector2i(2, 2))
	n.is_bridge = true
	# STR(0) <-> CON(2): 中间方向 = E+SW=(0,1)
	n = _ms("trans_sc01", "近战生存", 2, ["str_s01", "con_s01"], {"max_hp": 3, "melee_hit": 1}, Vector2i(0, 1))
	n.is_bridge = true
	n = _ms("trans_sc02", "盾牌掌握", 4, ["str_b01", "con_b01"], {"ac": 1, "melee_damage": 1}, Vector2i(-1, 2))
	n.is_bridge = true
	# DEX(1) <-> INT(3): 中间方向 = SE+W=(-1,1)
	n = _ms("trans_di01", "精准施法", 2, ["dex_s01", "int_s01"], {"mana_max": 2, "spell_hit": 1}, Vector2i(-1, 1))
	n.is_bridge = true
	n = _ms("trans_di02", "奥术射手", 4, ["dex_b01", "int_b01"], {"ranged_hit": 1, "spell_damage": 1}, Vector2i(-2, 2))
	n.is_bridge = true
	# INT(3) <-> WIS(4): 中间方向 = W+NW=(-1,-1)
	n = _ms("trans_iw01", "神秘学", 2, ["int_s01", "wis_s01"], {"mana_max": 2, "all_save": 1}, Vector2i(-1, -1))
	n.is_bridge = true
	n = _ms("trans_iw02", "古代智慧", 4, ["int_b01", "wis_b01"], {"spell_damage": 1, "heal_amount": 1}, Vector2i(-1, -2))
	n.is_bridge = true
	# WIS(4) <-> CHA(5): 中间方向 = NW+NE=(1,-2)
	n = _ms("trans_wc01", "精神领袖", 2, ["wis_s01", "cha_s01"], {"heal_amount": 1, "morale": 1}, Vector2i(1, -2))
	n.is_bridge = true
	n = _ms("trans_wc02", "圣洁光辉", 4, ["wis_b01", "cha_b01"], {"heal_amount": 1, "ally_bonus": 1}, Vector2i(2, -3))
	n.is_bridge = true
	# CHA(5) <-> CON(2): 对角方向，通过环绕路径
	n = _ms("trans_cc01", "鼓舞防御", 2, ["cha_s01", "con_s01"], {"ac": 1, "morale": 1}, Vector2i(0, 0))
	n.is_bridge = true
	n.neighbors = ["start"]
	n = _ms("trans_cc02", "铁血指挥", 4, ["cha_b01", "con_b01"], {"ally_bonus": 1, "max_hp": 3}, Vector2i(-2, 1))
	n.is_bridge = true
	# 深层过渡
	n = _ms("trans_sd03", "剑弓合一", 7, ["str_b08", "dex_b13"], {"melee_damage": 1, "ranged_damage": 1, "critical_rate": 0.02}, _mp(0, 7, 2))
	n.is_bridge = true
	n = _ms("trans_sc03", "战神信仰", 7, ["str_b07", "cha_b10"], {"melee_hit": 1, "morale": 2}, Vector2i(3, -2))
	n.is_bridge = true
	n = _ms("trans_cw01", "生命之力", 6, ["con_b08", "wis_b08"], {"max_hp": 8, "heal_amount": 1}, Vector2i(-2, 0))
	n.is_bridge = true
	n = _ms("trans_ic01", "奥术外交", 7, ["int_b11", "cha_b11"], {"mana_max": 3, "cha_check": 1}, Vector2i(-3, -1))
	n.is_bridge = true
	n = _ms("trans_dc01", "战场机动", 6, ["dex_b11", "con_b08"], {"ac": 1, "initiative": 2, "max_hp": 3}, Vector2i(-3, 3))
	n.is_bridge = true
	# 对角过渡
	n = _ms("trans_ci01", "战斗法师", 5, ["con_b02", "int_b08"], {"spell_damage": 1, "max_hp": 3}, Vector2i(-3, 1))
	n.is_bridge = true
	n = _ms("trans_dw01", "野性直觉", 5, ["dex_b02", "wis_b02"], {"initiative": 2, "wis_check": 1}, Vector2i(-1, -1))
	n.is_bridge = true

# ============================================================================
# 跨区域环路连接
# ============================================================================

func _build_cross_region_loops():
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
	# 内层环路
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
	# 深层环路
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
	_ac("cha_b12", "trans_sc03")
	# STR 环路: 嗜血→杀戮本能
	_ac("str_s21", "str_s19")

# ============================================================================
# 辅助构建方法
# ============================================================================

func _ms(id: String, nm: String, dep: int,
		prereqs: Array, bonuses: Dictionary, gp: Vector2i) -> SkillNodeData:
	var node = SkillNodeData.new()
	node.node_id = id; node.node_name = nm
	node.node_type = SkillNodeData.NodeType.SMALL
	node.region = _region_from_id(id)
	node.depth = dep
	node.prerequisites = prereqs; node.stat_bonuses = bonuses
	node.grid_position = gp
	node.description = node._get_stat_bonus_text()
	nodes[id] = node
	return node

func _mb(id: String, nm: String, dep: int,
		lreq: Array, prereqs: Array, eff: String, active: bool,
		desc: String, gp: Vector2i) -> SkillNodeData:
	var node = SkillNodeData.new()
	node.node_id = id; node.node_name = nm
	node.node_type = SkillNodeData.NodeType.BIG
	node.region = _region_from_id(id)
	node.depth = dep
	if lreq.size() > 0: node.required_level = lreq[0]
	node.prerequisites = prereqs; node.skill_effect = eff
	node.is_active_skill = active; node.description = desc
	node.grid_position = gp
	nodes[id] = node
	return node

func _mk(id: String, nm: String, dep: int,
		prereqs: Array, eff: String,
		benefit: String, cost_desc: String, cost: Dictionary,
		gp: Vector2i) -> SkillNodeData:
	var node = SkillNodeData.new()
	node.node_id = id; node.node_name = nm
	node.node_type = SkillNodeData.NodeType.KEYSTONE
	node.region = _region_from_id(id)
	node.depth = dep
	node.prerequisites = prereqs; node.skill_effect = eff
	node.is_active_skill = false; node.description = benefit
	node.keystone_cost = cost_desc; node.cost_bonuses = cost
	node.grid_position = gp
	nodes[id] = node
	return node

## 根据 node_id 前缀自动推断区域
func _region_from_id(id: String) -> int:
	if id.begins_with("str_"): return SkillNodeData.Region.STR
	if id.begins_with("dex_"): return SkillNodeData.Region.DEX
	if id.begins_with("con_"): return SkillNodeData.Region.CON
	if id.begins_with("int_"): return SkillNodeData.Region.INT
	if id.begins_with("wis_"): return SkillNodeData.Region.WIS
	if id.begins_with("cha_"): return SkillNodeData.Region.CHA
	if id.begins_with("trans_"): return SkillNodeData.Region.TRANSITION
	return SkillNodeData.Region.NONE

func _make_node(id: String, nm: String, ntype: int, reg: int, dep: int,
		lreq: Array, prereqs: Array,
		bonuses: Dictionary, eff: String, active: bool, desc: String,
		gp: Vector2i) -> SkillNodeData:
	var node = SkillNodeData.new()
	node.node_id = id; node.node_name = nm
	node.node_type = ntype; node.region = reg; node.depth = dep
	if lreq.size() > 0: node.required_level = lreq[0]
	node.prerequisites = prereqs; node.stat_bonuses = bonuses
	node.skill_effect = eff; node.is_active_skill = active
	node.description = desc; node.grid_position = gp
	nodes[id] = node
	return node

func _ac(a: String, b: String):
	if nodes.has(a) and nodes.has(b):
		if b not in nodes[a].neighbors: nodes[a].neighbors.append(b)
		if a not in nodes[b].neighbors: nodes[b].neighbors.append(a)

# ============================================================================
# 查询方法
# ============================================================================

func get_start_node() -> SkillNodeData:
	return nodes.get(START_NODE_ID, null)

func get_all_nodes() -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node in nodes.values(): result.append(node)
	return result

func get_nodes_by_region(reg: int) -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node in nodes.values():
		if node.region == reg: result.append(node)
	return result

func get_big_nodes() -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node in nodes.values():
		if node.node_type == SkillNodeData.NodeType.BIG or node.node_type == SkillNodeData.NodeType.KEYSTONE:
			result.append(node)
	return result

func get_keystones() -> Array[SkillNodeData]:
	var result: Array[SkillNodeData] = []
	for node in nodes.values():
		if node.node_type == SkillNodeData.NodeType.KEYSTONE: result.append(node)
	return result

func get_node_count() -> int:
	return nodes.size()
