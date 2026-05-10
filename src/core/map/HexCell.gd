# HexCell.gd
# 战术网格中单个六边形的可视化与交互类 (HD-2D 3D版本)
extends Area3D
class_name HexCell

@export var data: BattleCellData

var grid_pos: Vector2i # 轴向坐标 (q, r)
var elevation: int = 1 # 高程等级: 0低地, 1平地, 2高地
var cover_type: int = 0 # 掩体等级: 0无, 1半掩体(森林/树木), 2全掩体(巨石/墙壁)
var occupant: Unit = null

# 可视化节点
var mesh_instance: MeshInstance3D
var highlight_mesh: MeshInstance3D
var cover_mesh_instance: MeshInstance3D

var _base_albedo_color: Color

signal cell_clicked(cell: HexCell)
signal cell_mouse_entered(cell: HexCell)
signal cell_mouse_exited(cell: HexCell)

func _ready():
	_setup_visuals()
	input_event.connect(_on_input_event)
	mouse_entered.connect(_on_mouse_entered)
	mouse_exited.connect(_on_mouse_exited)

func _setup_visuals():
	var hex_radius = HexUtils.SIZE
	var hex_height = HexUtils.SIZE * 0.5 # 六棱柱的厚度

	# 创建用于点击碰撞的形状
	var collision_shape = CollisionShape3D.new()
	var cyl_shape = CylinderShape3D.new()
	cyl_shape.radius = hex_radius * 0.95
	cyl_shape.height = hex_height
	collision_shape.shape = cyl_shape
	collision_shape.rotation_degrees = Vector3(0, 30, 0) # 旋转 30 度变为平顶
	add_child(collision_shape)
	
	# 创建基础六棱柱绘制
	mesh_instance = MeshInstance3D.new()
	var mesh = CylinderMesh.new()
	mesh.radial_segments = 6 # 6个边就是六棱柱
	mesh.rings = 1
	mesh.top_radius = hex_radius
	mesh.bottom_radius = hex_radius
	mesh.height = hex_height
	mesh_instance.mesh = mesh
	mesh_instance.rotation_degrees = Vector3(0, 30, 0) # 旋转 30 度变为平顶
	
	var material: StandardMaterial3D
	if data:
		# 从材质管理器获取材质，并复制一份以便单独处理迷雾变暗而不影响其他同类格子
		material = CombatMaterialManager.get_instance().get_material(data.terrain_type, elevation).duplicate()
	else:
		material = StandardMaterial3D.new()
		# 默认根据高低差给不同深浅的灰色，以便直观区分地形起伏
		if elevation == 0:
			material.albedo_color = Color(0.3, 0.3, 0.3) # 低地深灰
		elif elevation == 2:
			material.albedo_color = Color(0.7, 0.7, 0.7) # 高地浅灰
		else:
			material.albedo_color = Color(0.5, 0.5, 0.5) # 平地中灰
			
	_base_albedo_color = material.albedo_color
	mesh_instance.material_override = material
	add_child(mesh_instance)
	
	# 生成掩体视觉效果
	if cover_type > 0:
		cover_mesh_instance = MeshInstance3D.new()
		var c_mesh = BoxMesh.new()
		var c_mat = StandardMaterial3D.new()
		if cover_type == 1:
			# 半掩体：模拟树丛/灌木 (绿色矮方块)
			c_mesh.size = Vector3(hex_radius * 0.6, hex_height * 1.5, hex_radius * 0.6)
			c_mat.albedo_color = Color(0.2, 0.5, 0.2)
		else:
			# 全掩体：模拟巨石/高墙 (灰色高方块)
			c_mesh.size = Vector3(hex_radius * 0.8, hex_height * 3.0, hex_radius * 0.8)
			c_mat.albedo_color = Color(0.4, 0.4, 0.4)
			
		cover_mesh_instance.mesh = c_mesh
		cover_mesh_instance.material_override = c_mat
		cover_mesh_instance.position = Vector3(0, hex_height / 2.0 + c_mesh.size.y / 2.0, 0)
		add_child(cover_mesh_instance)
	
	# 创建高亮多边形 (稍微大一点点)
	highlight_mesh = MeshInstance3D.new()
	var hl_mesh = mesh.duplicate()
	hl_mesh.top_radius = hex_radius * 1.05
	hl_mesh.bottom_radius = hex_radius * 1.05
	hl_mesh.height = hex_height * 1.05
	highlight_mesh.mesh = hl_mesh
	highlight_mesh.rotation_degrees = Vector3(0, 30, 0) # 同样旋转
	
	var hl_mat = StandardMaterial3D.new()
	hl_mat.albedo_color = Color(1, 1, 1, 0.3)
	hl_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	hl_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	highlight_mesh.material_override = hl_mat
	highlight_mesh.visible = false
	add_child(highlight_mesh)

func set_highlight(active: bool, color: Color = Color(1, 1, 1, 0.3)):
	if highlight_mesh.material_override:
		highlight_mesh.material_override.albedo_color = color
	highlight_mesh.visible = active

func set_shrouded(is_shrouded: bool):
	var mat = mesh_instance.material_override as StandardMaterial3D
	if not mat: return
	
	if is_shrouded:
		# 在战争迷雾中，降低亮度和透明度，或直接变黑
		mat.albedo_color = _base_albedo_color.darkened(0.8)
		if cover_mesh_instance and cover_mesh_instance.material_override:
			var c_mat = cover_mesh_instance.material_override as StandardMaterial3D
			c_mat.albedo_color = c_mat.albedo_color.darkened(0.8)
	else:
		# 恢复原始颜色
		mat.albedo_color = _base_albedo_color
				
		if cover_mesh_instance and cover_mesh_instance.material_override:
			var c_mat = cover_mesh_instance.material_override as StandardMaterial3D
			if cover_type == 1:
				c_mat.albedo_color = Color(0.2, 0.5, 0.2)
			else:
				c_mat.albedo_color = Color(0.4, 0.4, 0.4)

func _on_input_event(_camera: Node, event: InputEvent, _event_position: Vector3, _normal: Vector3, _shape_idx: int):
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		cell_clicked.emit(self)

func _on_mouse_entered():
	cell_mouse_entered.emit(self)

func _on_mouse_exited():
	cell_mouse_exited.emit(self)
