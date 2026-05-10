# QuestTargetVisual.gd
# 委托目标点大地图可视化组件
# 在 OverworldScene 中渲染任务标记: 脉冲光圈 + 图标 + 任务名称
#
# 画风: 战场兄弟式古朴风格, 使用几何形状+低饱和色, 不使用花哨特效
#   - 标记底座: 与目标点类型匹配的几何形状
#   - 脉冲光圈: 缓慢扩大/淡出的圆环, 提示"这里有个任务"
#   - 名称标签: 任务目标描述
extends Node2D
class_name QuestTargetVisual


## ========================================
## 配置
## ========================================

## 脉冲动画周期（秒）
const PULSE_PERIOD: float = 2.0
## 脉冲最大半径
const PULSE_MAX_RADIUS: float = 40.0
## 脉冲最小半径
const PULSE_MIN_RADIUS: float = 15.0
## 检测玩家接近的距离（像素）
const APPROACH_DIST: float = 60.0


## ========================================
## 引用
## ========================================

## 关联的目标点数据
var target_site: QuestTargetSite = null

## 视觉子节点
var _base_poly: Polygon2D
var _pulse_ring: Polygon2D
var label: Label
var _danger_label: Label

## 脉冲计时器
var _pulse_time: float = 0.0


## ========================================
## 生命周期
## ========================================

func _ready() -> void:
	_setup_visuals()


func _process(delta: float) -> void:
	# 脉冲动画
	_pulse_time = fmod(_pulse_time + delta, PULSE_PERIOD)
	_update_pulse()


## ========================================
## 公共接口
## ========================================

## 用目标点数据初始化视觉
func setup(site: QuestTargetSite) -> void:
	target_site = site
	position = site.world_position

	if not is_node_ready():
		await ready

	# 更新视觉内容
	_apply_site_style(site)


## ========================================
## 内部: 视觉搭建
## ========================================

func _setup_visuals() -> void:
	# 底座几何（目标点主体标记）
	_base_poly = Polygon2D.new()
	add_child(_base_poly)

	# 脉冲光圈（外环动画）
	_pulse_ring = Polygon2D.new()
	add_child(_pulse_ring)

	# 任务名称标签
	label = Label.new()
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 12)
	label.position = Vector2(-60, 20)
	label.custom_minimum_size = Vector2(120, 20)
	add_child(label)

	# 危险度标签（星级）
	_danger_label = Label.new()
	_danger_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_danger_label.add_theme_font_size_override("font_size", 11)
	_danger_label.position = Vector2(-30, -35)
	_danger_label.custom_minimum_size = Vector2(60, 16)
	add_child(_danger_label)

	# 初始形状
	_set_base_shape_hex(16.0)
	_update_ring_shape(PULSE_MIN_RADIUS, Color.WHITE)


## 应用目标点类型的视觉样式
func _apply_site_style(site: QuestTargetSite) -> void:
	var color := site.get_display_color()

	# 底座颜色
	_base_poly.color = color

	# 底座形状（按类型区分）
	match site.site_type:
		QuestTargetSite.SiteType.GOBLIN_CAMP, \
		QuestTargetSite.SiteType.BANDIT_CAMP:
			_set_base_shape_triangle(18.0)
		QuestTargetSite.SiteType.KOBOLD_MINE, \
		QuestTargetSite.SiteType.DUNGEON_ENTRANCE, \
		QuestTargetSite.SiteType.TOMB:
			_set_base_shape_diamond(16.0)
		QuestTargetSite.SiteType.MINOTAUR_FORT, \
		QuestTargetSite.SiteType.DRAGON_LAIR:
			_set_base_shape_hex(22.0)
		QuestTargetSite.SiteType.CULT_HIDEOUT:
			_set_base_shape_pentagon(18.0)
		_:
			_set_base_shape_hex(16.0)

	# 名称
	label.text = site.site_name
	label.add_theme_color_override("font_color", Color(1.0, 1.0, 0.85))

	# 危险度星级
	var stars: String = ""
	for i in range(site.danger_stars):
		stars += "*"
	_danger_label.text = stars
	_danger_label.add_theme_color_override("font_color", Color(1.0, 0.8, 0.3))


## ========================================
## 内部: 形状生成
## ========================================

## 六边形底座
func _set_base_shape_hex(radius: float) -> void:
	var points := PackedVector2Array()
	for i in range(6):
		var angle := TAU * float(i) / 6.0 - PI / 6.0
		points.append(Vector2(cos(angle) * radius, sin(angle) * radius))
	_base_poly.polygon = points


## 三角形底座（营地类）
func _set_base_shape_triangle(size: float) -> void:
	var points := PackedVector2Array()
	points.append(Vector2(0, -size))
	points.append(Vector2(size * 0.866, size * 0.5))
	points.append(Vector2(-size * 0.866, size * 0.5))
	_base_poly.polygon = points


## 菱形底座（洞穴/遗迹类）
func _set_base_shape_diamond(size: float) -> void:
	var points := PackedVector2Array()
	points.append(Vector2(0, -size))
	points.append(Vector2(size * 0.7, 0))
	points.append(Vector2(0, size))
	points.append(Vector2(-size * 0.7, 0))
	_base_poly.polygon = points


## 五边形底座（教团类）
func _set_base_shape_pentagon(radius: float) -> void:
	var points := PackedVector2Array()
	for i in range(5):
		var angle := TAU * float(i) / 5.0 - PI / 2.0
		points.append(Vector2(cos(angle) * radius, sin(angle) * radius))
	_base_poly.polygon = points


## ========================================
## 内部: 脉冲动画
## ========================================

func _update_pulse() -> void:
	if not target_site or target_site.is_cleared:
		_pulse_ring.visible = false
		return

	# 正弦波驱动脉冲半径和透明度
	var t := _pulse_time / PULSE_PERIOD
	var radius := lerpf(PULSE_MIN_RADIUS, PULSE_MAX_RADIUS, t)
	var alpha := lerpf(0.6, 0.0, t)

	var ring_color := target_site.get_display_color()
	ring_color.a = alpha
	_update_ring_shape(radius, ring_color)


## 更新光圈形状（正多边形）
func _update_ring_shape(radius: float, color: Color) -> void:
	var segments := 24
	var points := PackedVector2Array()

	for i in range(segments):
		var angle := TAU * float(i) / float(segments)
		points.append(Vector2(cos(angle) * radius, sin(angle) * radius))

	_pulse_ring.polygon = points
	_pulse_ring.color = color


## ========================================
## 清理标记
## ========================================

## 标记为已完成（淡出效果）
func mark_cleared() -> void:
	_pulse_ring.visible = false
	_base_poly.color = Color(0.3, 0.3, 0.3, 0.4)
	label.add_theme_color_override("font_color", Color(0.5, 0.5, 0.5, 0.5))
	_danger_label.visible = false
