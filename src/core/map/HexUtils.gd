# HexUtils.gd
# 静态工具类，处理六边形网格数学逻辑 (Axial Coordinates, Flat-top)
class_name HexUtils

# 6个方向的偏移量 (Axial: q, r)
const DIRECTIONS = [
	Vector2i(1, 0), Vector2i(1, -1), Vector2i(0, -1),
	Vector2i(-1, 0), Vector2i(-1, 1), Vector2i(0, 1)
]

# 平顶六边形的布局常量
const SIZE = 96.0 # 六边形外径 (放大以匹配 80x120 像素角色 sprite)
const WIDTH = 2.0 * SIZE
const HEIGHT = sqrt(3.0) * SIZE
const HORIZONTAL_SPACING = WIDTH * 0.75
const VERTICAL_SPACING = HEIGHT

## 轴向坐标转像素坐标 (2D)
static func axial_to_pixel(q: int, r: int) -> Vector2:
	var x = SIZE * (3.0 / 2.0 * q)
	var y = SIZE * (sqrt(3.0) / 2.0 * q + sqrt(3.0) * r)
	return Vector2(x, y)

## 轴向坐标转世界坐标 (3D HD-2D使用)
## elevation_level: 0=低地, 1=平地, 2=高地. 我们用每级一个固定的高度差
static func axial_to_world3d(q: int, r: int, elevation_level: int = 1) -> Vector3:
	var pos2d = axial_to_pixel(q, r)
	# 在 Godot 3D 中，X 和 Z 是地平面，Y 是高度
	var height_step = SIZE * 0.5 # 每级高程对应的高度差
	return Vector3(pos2d.x, elevation_level * height_step, pos2d.y)

## 像素坐标转轴向坐标 (返回浮点 Vector2，需要进一步舍入)
static func pixel_to_fractional_axial(pixel: Vector2) -> Vector2:
	var q = (2.0 / 3.0 * pixel.x) / SIZE
	var r = (-1.0 / 3.0 * pixel.x + sqrt(3.0) / 3.0 * pixel.y) / SIZE
	return Vector2(q, r)

## 舍入浮点轴向坐标到最近的整数坐标
static func hex_round(frac: Vector2) -> Vector2i:
	var q = frac.x
	var r = frac.y
	var s = -q - r
	
	var rq = round(q)
	var rr = round(r)
	var rs = round(s)
	
	var q_diff = abs(rq - q)
	var r_diff = abs(rr - r)
	var s_diff = abs(rs - s)
	
	if q_diff > r_diff and q_diff > s_diff:
		rq = -rr - rs
	elif r_diff > s_diff:
		rr = -rq - rs
	else:
		rs = -rq - rr
		
	return Vector2i(int(rq), int(rr))

## 获取邻居坐标
static func get_neighbor(q: int, r: int, direction: int) -> Vector2i:
	var offset = DIRECTIONS[direction % 6]
	return Vector2i(q + offset.x, r + offset.y)

## 计算两个六边形之间的距离
static func distance(q1: int, r1: int, q2: int, r2: int) -> int:
	return (abs(q1 - q2) + abs(q1 + r1 - q2 - r2) + abs(r1 - r2)) / 2
