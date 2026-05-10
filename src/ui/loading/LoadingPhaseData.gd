# LoadingPhaseData.gd
# 加载阶段数据 — 定义RPG风格的加载描述文本
# 每个阶段对应进度条的一个区间，显示对应的奇幻世界描述
extends RefCounted
class_name LoadingPhaseData

## 单个加载阶段的定义
## progress_start/end: 此阶段覆盖的进度范围 [0.0, 1.0]
## title: 当前操作的大标题
## description: RPG风格的详细描述
class Phase:
	var progress_start: float
	var progress_end: float
	var title: String
	var description: String
	
	func _init(p_start: float, p_end: float, p_title: String, p_desc: String):
		progress_start = p_start
		progress_end = p_end
		title = p_title
		description = p_desc

## 预设：新建世界（新游戏）
static func get_new_world_phases() -> Array[Phase]:
	var phases: Array[Phase] = []
	phases.append(Phase.new(0.00, 0.10, "奠基",
		"混沌退散，天穹与深渊的边界在虚空中缓缓浮现..."))
	phases.append(Phase.new(0.10, 0.20, "大地",
		"正在向大地播撒生命的种子，沃野与荒漠随之成形..."))
	phases.append(Phase.new(0.20, 0.30, "山海",
		"山脉如利齿般刺破云层，冰川消融汇成江河奔涌而出..."))
	phases.append(Phase.new(0.30, 0.40, "烈焰",
		"正在给火山灌满岩浆，灰烬如雪般飘落在焦土之上..."))
	phases.append(Phase.new(0.40, 0.50, "森林",
		"古老的树苗从灰烬中破土而出，转瞬间古木参天..."))
	phases.append(Phase.new(0.50, 0.60, "生灵",
		"飞鸟掠过林梢，走兽踏破晨露，鱼群在溪流中闪烁如碎银..."))
	phases.append(Phase.new(0.60, 0.70, "文明",
		"篝火旁诞生了第一句话，城邦在河流交汇处拔地而起..."))
	phases.append(Phase.new(0.70, 0.80, "魔法",
		"六芒星从天穹坠落，魔力渗透进大地的每一寸脉络..."))
	phases.append(Phase.new(0.80, 0.90, "命运",
		"命运纺者正在编织丝线，将无数个名字系于同一根弦上..."))
	phases.append(Phase.new(0.90, 1.00, "启程",
		"世界已然成形，命运之书翻开了崭新的一页..."))
	return phases

## 预设：加载存档
static func get_load_save_phases() -> Array[Phase]:
	var phases: Array[Phase] = []
	phases.append(Phase.new(0.00, 0.15, "回忆",
		"正在从时间的长河中打捞那些散落的记忆碎片..."))
	phases.append(Phase.new(0.15, 0.30, "重建",
		"山川河流按照记忆中的模样逐一重现..."))
	phases.append(Phase.new(0.30, 0.45, "复苏",
		"沉睡的灵魂正在苏醒，那些曾经并肩的面孔从迷雾中浮现..."))
	phases.append(Phase.new(0.45, 0.60, "还原",
		"昔日的盟友与宿敌各归其位，未完成的战斗还在等你落下最后一子..."))
	phases.append(Phase.new(0.60, 0.75, "重连",
		"商人的账本翻开在那一页，酒馆老板娘留着你的老座位..."))
	phases.append(Phase.new(0.75, 0.90, "命运",
		"命运纺者轻轻拨弄属于你的丝线，将断裂处重新系紧..."))
	phases.append(Phase.new(0.90, 1.00, "归来",
		"晨光穿透云层照亮了你曾走过的路——欢迎回来，冒险者..."))
	return phases

## 预设：战斗加载
static func get_combat_phases() -> Array[Phase]:
	var phases: Array[Phase] = []
	phases.append(Phase.new(0.00, 0.20, "集结",
		"号角撕裂了黎明的寂静，战士们沉默地系紧护甲..."))
	phases.append(Phase.new(0.20, 0.40, "布阵",
		"旗帜在寒风中猎猎作响，盾墙层层叠起，长矛如荆棘密布..."))
	phases.append(Phase.new(0.40, 0.60, "对峙",
		"空气中弥漫着铁锈与魔力的气息，所有人都在等第一滴血落下的声音..."))
	phases.append(Phase.new(0.60, 0.80, "蓄势",
		"弓弦拉满，法杖顶端奥术能量盘旋凝聚，只等一声令下..."))
	phases.append(Phase.new(0.80, 1.00, "交锋",
		"战鼓擂动，大地在万千脚步中颤抖——命运的齿轮开始转动..."))
	return phases

## 预设：快速游戏（较短）
static func get_quick_game_phases() -> Array[Phase]:
	var phases: Array[Phase] = []
	phases.append(Phase.new(0.00, 0.30, "命运骰动",
		"一枚骨骰在虚空中翻转，每一面都映照着一个截然不同的人生..."))
	phases.append(Phase.new(0.30, 0.60, "灵魂降临",
		"一个崭新的灵魂带着尚未褪去的星尘，坠入这具躯壳之中..."))
	phases.append(Phase.new(0.60, 0.85, "世界回应",
		"大地感知到了新的脚步，远方的道路扬起尘土为你铺设前路..."))
	phases.append(Phase.new(0.85, 1.00, "启程",
		"你踩在未曾有人走过的土地上，冒险从这一步开始..."))
	return phases

## 预设：快速战斗
static func get_quick_combat_phases() -> Array[Phase]:
	var phases: Array[Phase] = []
	phases.append(Phase.new(0.00, 0.20, "骰运",
		"命运之骰在掌心咔嗒作响，战场的天平尚未倾斜..."))
	phases.append(Phase.new(0.20, 0.40, "列阵",
		"旗帜猎猎作响，战士们沉默地在各自的位置上站定，等待命运的裁决..."))
	phases.append(Phase.new(0.40, 0.60, "号角",
		"号角声撕裂了黎明的寂静，铁与血的气息弥漫在每一寸空气中..."))
	phases.append(Phase.new(0.60, 0.80, "交锋",
		"战鼓擂动，大地在万千脚步中颤抖——命运的齿轮开始转动..."))
	phases.append(Phase.new(0.80, 1.00, "血战",
		"刀光剑影之间，生死只在一线——唯有胜利者才能书写历史..."))
	return phases

## 根据当前进度获取对应的阶段
static func get_phase_at_progress(phases: Array[Phase], progress: float) -> Phase:
	for phase in phases:
		if progress >= phase.progress_start and progress < phase.progress_end:
			return phase
	# 边界情况：正好 1.0
	if phases.size() > 0 and progress >= 1.0:
		return phases[phases.size() - 1]
	# 默认返回第一个
	if phases.size() > 0:
		return phases[0]
	return null
