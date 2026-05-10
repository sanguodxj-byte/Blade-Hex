# HexLayoutConfig.gd
# 六边形网格布局配置组件
# 这是一个Resource，允许在Godot编辑器中轻松创建多种网格/纹理对齐预设，
# 通过指定 q=(1,0) 和 r=(0,1) 这两个基向量，它可以兼容任何平顶、尖顶或甚至变形的等距网格拼接。
class_name HexLayoutConfig
extends Resource

@export_group("Texture Settings")
## 纹理的预期宽度
@export var tex_width: float = 313.0
## 纹理的预期高度
@export var tex_height: float = 313.0

@export_group("Grid Vectors")
## Axial (1,0) 对应的像素偏移（通常是右侧或右上方相邻瓦片的中心偏移）
@export var q_vector: Vector2 = Vector2(-136.00, -175.07)

## Axial (0,1) 对应的像素偏移（通常是右下或下方相邻瓦片的中心偏移）
@export var r_vector: Vector2 = Vector2(-267.75, -0.53)


## 将轴向坐标 (q, r) 转换为像素坐标
func axial_to_pixel(q: int, r: int) -> Vector2:
	return Vector2(
		q_vector.x * float(q) + r_vector.x * float(r),
		q_vector.y * float(q) + r_vector.y * float(r)
	)


## 将像素坐标转换为带有小数的轴向坐标 (用于鼠标点选/拾取)
## 这里使用 2x2 逆矩阵求解：
## | q_vector.x  r_vector.x | | q | = | px |
## | q_vector.y  r_vector.y | | r |   | py |
func pixel_to_fractional_axial(px: float, py: float) -> Vector2:
	var det := q_vector.x * r_vector.y - q_vector.y * r_vector.x
	if det == 0.0:
		push_warning("HexLayoutConfig: 行列式为0，无效的基向量。")
		return Vector2.ZERO
	
	var inv_det := 1.0 / det
	var q := (r_vector.y * px - r_vector.x * py) * inv_det
	var r := (-q_vector.y * px + q_vector.x * py) * inv_det
	return Vector2(q, r)
