# TipsData.gd
# 游戏提示数据源 — 可配置的提示文本集合
# 支持分类、权重、条件过滤
extends RefCounted
class_name TipsData

## 单条提示
class Tip:
	var id: String
	var text: String
	var category: String          ## 分类标签（combat, exploration, skill, story, general）
	var priority: int = 0         ## 显示权重，越高越优先
	var game_phase: String = ""   ## 游戏阶段限制（空=不限）
	
	func _init(p_id: String, p_text: String, p_category: String = "general",
			p_priority: int = 0, p_phase: String = ""):
		id = p_id
		text = p_text
		category = p_category
		priority = p_priority
		game_phase = p_phase

## 所有提示数据
var _tips: Array[Tip] = []
## 已显示过的索引（用于轮播不重复）
var _shown_indices: Array[int] = []
## 当前索引
var _current_index: int = -1

func _init():
	_load_default_tips()

## 获取所有提示
func get_all_tips() -> Array[Tip]:
	return _tips

## 获取指定分类的提示
func get_tips_by_category(category: String) -> Array[Tip]:
	var result: Array[Tip] = []
	for tip in _tips:
		if tip.category == category:
			result.append(tip)
	return result

## 获取下一条提示（轮播不重复，全部轮完后重新开始）
func get_next_tip() -> Tip:
	if _tips.is_empty():
		return null
	
	# 如果全部显示过了，重置
	if _shown_indices.size() >= _tips.size():
		_shown_indices.clear()
	
	# 从未显示的中随机选一个
	var available: Array[int] = []
	for i in range(_tips.size()):
		if i not in _shown_indices:
			available.append(i)
	
	if available.is_empty():
		_current_index = 0
	else:
		# 按权重排序后随机（优先高权重）
		available.sort_custom(func(a, b): return _tips[a].priority > _tips[b].priority)
		# 前25%高权重中随机
		var top_count = maxi(1, available.size() / 4.0)
		var pick_range = available.slice(0, top_count - 1)
		_current_index = pick_range[randi() % pick_range.size()]
	
	_shown_indices.append(_current_index)
	return _tips[_current_index]

## 重置轮播状态
func reset_rotation():
	_shown_indices.clear()
	_current_index = -1

## 添加自定义提示
func add_tip(tip: Tip):
	_tips.append(tip)

## 添加自定义提示（便捷方法）
func add_custom_tip(text: String, category: String = "general", priority: int = 0):
	_tips.append(Tip.new("custom_%d" % _tips.size(), text, category, priority))

## 加载默认提示数据
func _load_default_tips():
	# ============ 战斗提示 ============
	_tips.append(Tip.new("combat_1", "占据高地不只是谚语——地形高度差会为攻击者提供真实的命中加成。聪明的将军从不放弃山丘。", "combat", 3))
	_tips.append(Tip.new("combat_2", "当士气的最后一道防线崩溃，士兵将丢盔弃甲、四散奔逃，再也无法接受任何指令。不要让他们走到那一步。", "combat", 4))
	_tips.append(Tip.new("combat_3", "正面强攻是勇者的选择，但侧翼突袭是智者的选择。从侧面发起的攻击会让守备者措手不及，大幅削弱防御效果。", "combat", 3))
	_tips.append(Tip.new("combat_4", "低阶法术只需消耗魔力即可施展，但真正改变战局的力量——那些让天地变色的禁术——需要消耗珍贵的法术位。好钢要用在刀刃上。", "combat", 2))
	_tips.append(Tip.new("combat_5", "冲锋不止是一种移动方式。带着速度的重量撞击敌阵，惯性会转化为额外伤害。移动的距离越长，冲击的力道越重。", "combat", 3))
	_tips.append(Tip.new("combat_6", "光明与生命的力量对亡灵而言如同烈焰灼身。治疗法术落在骷髅与幽灵身上，会产生意想不到的\"逆转\"效果。", "combat", 5))
	_tips.append(Tip.new("combat_7", "战场上最致命的武器不是刀剑，而是预判。观察行动顺序，提前将盾牌朝向正确的方向，胜利往往取决于比敌人多想一步。", "combat", 2))
	_tips.append(Tip.new("combat_8", "老练的法师知道：最好的法术不是伤害最高的，而是能改变战场本身的。点燃草地、冻结河流、掀起迷雾——让地形成为你的盟友。", "combat", 4))
	_tips.append(Tip.new("combat_9", "后排的弓箭手看似安全，但一旦被骑兵或刺客贴身，他们将毫无还手之力。永远不要让远程单位孤军奋战。", "combat", 3))
	_tips.append(Tip.new("combat_10", "一个站在原地不动的战士，很快就会变成一个躺在地上不动的战士。保持移动，利用掩体，让敌人追逐你的影子。", "combat", 3))
	
	# ============ 探索提示 ============
	_tips.append(Tip.new("explore_1", "大地图上的每一处异常都值得调查——被遗弃的营地、不自然的岩石排列、飞鸟盘旋不去的空地。宝物和支线故事往往藏在最不起眼的角落。", "exploration", 2))
	_tips.append(Tip.new("explore_2", "同样的距离，平原上半日可达，山地却需要三天。行军路线的规划不是小事——补给耗尽在荒野中意味着死亡。", "exploration", 3))
	_tips.append(Tip.new("explore_3", "城镇是冒险者的港湾：铁匠修复破损的装备，酒馆交换最新的情报，佣兵公会则随时有等着你的人手——只要你付得起价钱。", "exploration", 3))
	_tips.append(Tip.new("explore_4", "当太阳沉入地平线，荒野便不再属于旅人。夜行军不仅速度更慢，伏击的概率也会翻倍。如果必须在夜间赶路，至少派斥候先行。", "exploration", 4))
	_tips.append(Tip.new("explore_5", "某些遗迹的入口被古老的机关封锁，需要特定的技能才能开启。如果暂时进不去，记下位置——终有一天你会具备开启它的能力。", "exploration", 3))
	_tips.append(Tip.new("explore_6", "路边的旅人和流浪商人往往掌握着地图上找不到的信息。停下来聊几句，也许能省去数日的弯路。", "exploration", 2))
	_tips.append(Tip.new("explore_7", "天气不只是装饰。暴雨会熄灭火焰法术，浓雾会遮蔽远程视线，而雷电交加时，站在高处的人会成为最醒目的目标。", "exploration", 4))
	
	# ============ 技能盘提示 ============
	_tips.append(Tip.new("skill_1", "技能盘上点亮每一颗星辰都需要付出不可撤回的代价。没有后悔药，没有重来——正因如此，每一次选择才弥足珍贵。", "skill", 5))
	_tips.append(Tip.new("skill_2", "即便最长寿的精灵穷尽一生，也只能点亮技能盘上不到三分之一的星辰。\"全都要\"从来不是一个选项——取舍，才是真正的游戏。", "skill", 5))
	_tips.append(Tip.new("skill_3", "跳跃是穿越技能盘的捷径，能让你瞬间抵达远处的节点，跳过漫长的路径——但这种力量一生只有四次。用在哪，决定了你是谁。", "skill", 4))
	_tips.append(Tip.new("skill_4", "代价型大节点散发着诱人的光芒，承诺着远超寻常的力量。但仔细阅读代价——有些东西一旦失去，就再也回不来了。", "skill", 4))
	_tips.append(Tip.new("skill_5", "技能盘的六大区域并非彼此隔绝的孤岛。区域交界处的过渡节点是通往新方向的桥梁，只需付出不多的代价便能涉足新的领域。", "skill", 3))
	_tips.append(Tip.new("skill_6", "六芒星的六个顶点各代表一种力量：力量(STR)、灵巧(DEX)、体魄(CON)、智力(INT)、感知(WIS)、魅力(CHA)。你的旅途从中心启程，方向由你决定。", "skill", 2))
	_tips.append(Tip.new("skill_7", "通往同一个大节点的路径不止一条。左路经过火焰，右路经过坚冰——殊途同归，但沿途的收获截然不同。仔细观察，选一条最契合你的路。", "skill", 3))
	_tips.append(Tip.new("skill_8", "纯方向build和混合build都是可行的道路。专注一个方向能抵达该领域的巅峰，而跨领域探索虽然无法登顶，却能获得意想不到的化学反应。", "skill", 3))
	
	# ============ 角色提示 ============
	_tips.append(Tip.new("char_1", "种族不只是外表的差异。矮人天生坚韧，精灵感知敏锐，半兽人体魄强健——你的血脉会为冒险的起点染上第一抹底色。", "character", 2))
	_tips.append(Tip.new("char_2", "经验值是冒险的副产物——战斗、探索、完成委托，每一次经历都在塑造你的角色。当你感到自己变强时，技能点已在口袋中等待分配。", "character", 3))
	_tips.append(Tip.new("char_3", "武器决定你如何战斗，护甲决定你能承受多少，饰品则往往藏着扭转战局的奇效。三个装备槽位，三种截然不同的策略取向。", "character", 2))
	_tips.append(Tip.new("char_4", "职业称号不过是世人对你的称呼——\"魔剑士\"或\"圣骑士\"只取决于你在技能盘上的足迹。称号不会限制你的能力，它只是在描述你已经成为的那个人。", "character", 3))
	_tips.append(Tip.new("char_5", "属性修正值看似微小，但在骰子落下的那一刻，+1与-1之间的差距，往往就是成功与失败的边界。", "character", 4))
	
	# ============ 故事提示 ============
	_tips.append(Tip.new("story_1", "六芒星是这个世界最古老的力量象征。传说在第一纪元的末日之战中，六芒星的力量将整块大陆撕裂成了今天的模样。", "story", 1))
	_tips.append(Tip.new("story_2", "大陆上流传着一个关于\"无冕者\"的传说——一个没有种族、没有出身、没有命运的旅人，据说他至今仍在某处流浪。", "story", 1))
	_tips.append(Tip.new("story_3", "精灵记住了所有的历史，矮人建造了不灭的城池，人类则在废墟上不断重建——每个种族都有自己面对过去的方式。", "story", 1))
	_tips.append(Tip.new("story_4", "在某些偏远村庄的酒馆里，老人会在深夜压低声音讲述关于\"第七芒\"的传说。大多数听众只当那是醉话，但知情者会选择沉默。", "story", 2))
	_tips.append(Tip.new("story_5", "魔法不是无代价的恩赐。每一次施展法术都会在施法者的灵魂上留下细微的刻痕，积累到一定程度时……没有人知道会发生什么。", "story", 2))
	
	# ============ 通用提示 ============
	_tips.append(Tip.new("general_1", "经验丰富的冒险者有一个共同的习惯：在踏入未知的危险之前，先找一个安全的角落存档。这个世界不会因为你的死亡而停下脚步。", "general", 3))
	_tips.append(Tip.new("general_2", "右键点击任何单位可以查看它的详细信息——属性、装备、技能，甚至隐藏的弱点。知己知彼，方能百战不殆。", "general", 2))
	_tips.append(Tip.new("general_3", "按 Tab 键可以切换显示信息的层级——从简洁到详尽，选择最适合当前局势的信息密度。战场上的混乱中，少即是多。", "general", 1))
