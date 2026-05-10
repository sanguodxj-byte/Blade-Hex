# CombatMaterialManager.gd
# 战斗场景 3D 材质管理器
# 负责缓存和生成带有地形纹理的 3D 材质，支持不同高度的专属贴图
class_name CombatMaterialManager
extends RefCounted

static var _instance: CombatMaterialManager = null

var _materials: Dictionary = {}

static func get_instance() -> CombatMaterialManager:
	if _instance == null:
		_instance = CombatMaterialManager.new()
	return _instance

## 根据地形类型和高程获取对应的 3D 材质
func get_material(terrain_type: BattleCellData.TerrainType, elevation: int) -> StandardMaterial3D:
	var key = "%d_%d" % [terrain_type, elevation]
	if _materials.has(key):
		return _materials[key]
		
	var mat = StandardMaterial3D.new()
	var tex_name = _get_texture_name(terrain_type)
	
	# 尝试加载带高度差的专属贴图 (如 grassland_elev1_0.png)
	# 如果找不到，则回退到默认的基础贴图 (grassland_0.png)
	var tex_path = "res://src/assets/tiles/hex_terrain/%s_elev%d_0.png" % [tex_name, elevation]
	var tex = load(tex_path) as Texture2D
	
	if not tex:
		tex_path = "res://src/assets/tiles/hex_terrain/%s_0.png" % tex_name
		tex = load(tex_path) as Texture2D
		
	var has_texture := false
	if tex:
		mat.albedo_texture = tex
		# 使用最近邻过滤，保持像素风
		mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
		# 开启三面映射 (Triplanar Mapping)，防止2D图片贴在3D圆柱体侧面时发生极度拉伸
		mat.uv1_triplanar = true
		mat.uv1_scale = Vector3(0.01, 0.01, 0.01) # 根据贴图尺寸适当缩放UV
		has_texture = true
	
	# 混合颜色：获取地形的基础颜色
	var base_color: Color
	if has_texture:
		base_color = Color.WHITE # 如果有贴图，基础色必须是白色，否则乘法叠加会变黑
	else:
		var props = BattleCellData.get_terrain_properties(terrain_type)
		base_color = props["color"]
	
	# 根据高程进行轻微的明暗调整
	if elevation == 0:
		base_color = base_color.darkened(0.2)
	elif elevation == 2:
		base_color = base_color.lightened(0.2)
		
	mat.albedo_color = base_color
	
	_materials[key] = mat
	return mat

## 映射战斗地形类型到大地图的纹理前缀
func _get_texture_name(type: BattleCellData.TerrainType) -> String:
	match type:
		BattleCellData.TerrainType.PLAINS: return "grassland"
		BattleCellData.TerrainType.GRASSLAND: return "grassland"
		BattleCellData.TerrainType.SAVANNA: return "barren_land"
		BattleCellData.TerrainType.FOREST: return "forest"
		BattleCellData.TerrainType.DENSE_FOREST: return "forest"
		BattleCellData.TerrainType.HILLS: return "rocky_land"
		BattleCellData.TerrainType.MOUNTAIN: return "mountain_cave"
		BattleCellData.TerrainType.SHALLOW_WATER: return "pond"
		BattleCellData.TerrainType.DEEP_WATER: return "pond"
		BattleCellData.TerrainType.SWAMP: return "swamp"
		BattleCellData.TerrainType.ROAD: return "crossroads"
		BattleCellData.TerrainType.SAND: return "wasteland"
		BattleCellData.TerrainType.SNOW: return "mountain_cave"
		BattleCellData.TerrainType.WALL: return "castle"
		BattleCellData.TerrainType.RUINS: return "ruins"
		BattleCellData.TerrainType.POISON_MUSHROOM: return "swamp"
		BattleCellData.TerrainType.LUCKY_GRASS: return "grassland"
		_: return "grassland"
