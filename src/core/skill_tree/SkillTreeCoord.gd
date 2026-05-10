# SkillTreeCoord.gd
# 技能盘坐标组件 — 六边形轴坐标(axial)与像素坐标的双向转换
# 使用 pointy-top 六边形布局
# 轴坐标 (q, r)，cube 坐标 (q, r, s=-q-r)
# hex_to_pixel 直接被 UI 层调用渲染节点位置
extends RefCounted
class_name SkillTreeCoord

## 六边形尺寸（中心到顶点像素距离），决定整个技能盘缩放
var hex_size: float = 40.0

## 六边形6个邻居方向（轴坐标偏移）
const HEX_DIRECTIONS: Array[Vector2i] = [
	Vector2i(1, 0),    # E
	Vector2i(0, 1),    # SE
	Vector2i(-1, 1),   # SW
	Vector2i(-1, 0),   # W
	Vector2i(0, -1),   # NW
	Vector2i(1, -1),   # NE
]

## 6个区域的主轴方向（与 HEX_DIRECTIONS 对齐）
## STR=E(0°), DEX=SE(60°), CON=SW(120°), INT=W(180°), WIS=NW(240°), CHA=NE(300°)
## UI 中 REGION_BASE_ANGLES 的映射: STR=0°, CHA=60°, WIS=120°, INT=180°, CON=240°, DEX=300°

## 区域主题色
const REGION_COLORS: Dictionary = {
	0: Color(0.86, 0.20, 0.18),   # NONE → 白
	1: Color(0.86, 0.20, 0.18),   # STR → 红
	2: Color(0.18, 0.80, 0.44),   # DEX → 绿
	3: Color(0.80, 0.65, 0.20),   # CON → 土黄
	4: Color(0.30, 0.50, 0.90),   # INT → 蓝
	5: Color(0.70, 0.85, 0.30),   # WIS → 浅绿
	6: Color(0.78, 0.30, 0.78),   # CHA → 紫
	7: Color(0.60, 0.60, 0.60),   # TRANSITION → 灰
}

## 节点类型对应视觉半径（相对 hex_size 倍数）
const NODE_RADIUS_SCALE: Dictionary = {
	0: 0.55,   # START
	3: 0.50,   # KEYSTONE
	1: 0.42,   # BIG
	2: 0.28,   # SMALL
}


# ============================================================================
# 坐标转换 — Pointy-Top 六边形
# ============================================================================

## 轴坐标 (q, r) → 像素坐标 (pointy-top)
## x = hex_size * (√3 * q + √3/2 * r)
## y = hex_size * (3/2 * r)
func hex_to_pixel(q: int, r: int) -> Vector2:
	var fq := float(q)
	var fr := float(r)
	return Vector2(
		hex_size * (sqrt(3.0) * fq + sqrt(3.0) / 2.0 * fr),
		hex_size * (1.5 * fr)
	)

## 像素坐标 → 轴坐标（四舍五入到最近格子）
func pixel_to_hex(px: float, py: float) -> Vector2i:
	var q := (sqrt(3.0) / 3.0 * px - 1.0 / 3.0 * py) / hex_size
	var r := (2.0 / 3.0 * py) / hex_size
	return _hex_round(q, r)

func _hex_round(fq: float, fr: float) -> Vector2i:
	var fs := -fq - fr
	var rq := roundi(fq)
	var rr := roundi(fr)
	var rs := roundi(fs)
	var dq: int = absi(rq) - abs(roundi(fq))
	var dr: int = absi(rr) - abs(roundi(fr))
	var ds: int = absi(rs) - abs(roundi(fs))
	if dq > dr and dq > ds:
		rq = -rr - rs
	elif dr > ds:
		rr = -rq - rs
	return Vector2i(rq, rr)


# ============================================================================
# 网格查询
# ============================================================================

## 获取6个邻居坐标
static func get_neighbors(q: int, r: int) -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	for d in HEX_DIRECTIONS:
		result.append(Vector2i(q, r) + d)
	return result

## Cube距离
static func hex_distance(a: Vector2i, b: Vector2i) -> int:
	var dq = absi(a.x - b.x)
	var dr = absi(a.y - b.y)
	var ds = absi((a.x + a.y) - (b.x + b.y))
	return maxi(maxi(dq, dr), ds)

## 到原点的距离
static func hex_ring(pos: Vector2i) -> int:
	return hex_distance(pos, Vector2i.ZERO)


# ============================================================================
# 视觉属性
# ============================================================================

func get_node_radius(node_type: int) -> float:
	return hex_size * NODE_RADIUS_SCALE.get(node_type, 0.3)

func get_region_color(region: int) -> Color:
	return REGION_COLORS.get(region, Color.WHITE)


# ============================================================================
# 位置生成器 — 供后续添加节点使用
# ============================================================================

## 在指定方向上偏移 ring 层
static func make_ring_pos(direction: Vector2i, ring: int) -> Vector2i:
	return direction * ring

## 在指定方向上偏移 ring 层，再加横向偏移 slot
## 横向偏移方向 = direction 顺时针旋转60°（HEX_DIRECTIONS 的下一个）
static func make_offset_pos(direction_idx: int, ring: int, slot: int) -> Vector2i:
	var main_dir = HEX_DIRECTIONS[direction_idx % 6]
	var cw_dir = HEX_DIRECTIONS[(direction_idx + 1) % 6]
	return main_dir * ring + cw_dir * slot
