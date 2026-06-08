# verify_shaders.gd
# General Godot 4.x shader compilation validator script
# Run: godot --headless -s verify_shaders.gd
# 
extends SceneTree

const VERIFY_START_TAG = "[VERIFY_START]:"
const VERIFY_END_TAG = "[VERIFY_END]"

func _init():
	run.call_deferred()

func run():
	# Scan all gdshader files under res://
	var shader_files = []
	find_shaders("res://", shader_files)
	
	print("Starting shader validation. Found %d files..." % shader_files.size())
	
	for file_path in shader_files:
		print("%s %s" % [VERIFY_START_TAG, file_path])
		
		# Load shader resource
		var shader = load(file_path)
		if not shader:
			print("Error: Failed to load shader resource: ", file_path)
			print(VERIFY_END_TAG)
			continue
			
		var code = shader.code
		if code.is_empty():
			# Skip empty or placeholder shader
			print(VERIFY_END_TAG)
			continue
			
		# Resolve shader_type
		var shader_type = "canvas_item"
		if code.contains("shader_type spatial"):
			shader_type = "spatial"
		elif code.contains("shader_type particles"):
			shader_type = "particles"
		elif code.contains("shader_type sky"):
			shader_type = "sky"
		elif code.contains("shader_type fog"):
			shader_type = "fog"
			
		# Create shader material
		var mat = ShaderMaterial.new()
		mat.shader = shader
		
		# Create isolated viewport
		var viewport = SubViewport.new()
		viewport.size = Vector2i(16, 16)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		
		var test_node = null
		
		# Add minimal test node based on shader_type
		if shader_type == "canvas_item":
			var rect = ColorRect.new()
			rect.size = Vector2(16, 16)
			rect.material = mat
			viewport.add_child(rect)
			test_node = rect
		elif shader_type == "spatial":
			var node3d = Node3D.new()
			viewport.add_child(node3d)
			
			var mesh_inst = MeshInstance3D.new()
			mesh_inst.mesh = BoxMesh.new()
			mesh_inst.material_override = mat
			node3d.add_child(mesh_inst)
			
			var camera = Camera3D.new()
			camera.position = Vector3(0, 0, 3)
			node3d.add_child(camera)
			test_node = node3d
		elif shader_type == "particles":
			var particles = GPUParticles2D.new()
			particles.process_material = mat
			particles.emitting = true
			viewport.add_child(particles)
			test_node = particles
		elif shader_type == "sky":
			var node3d = Node3D.new()
			viewport.add_child(node3d)
			
			var world_env = WorldEnvironment.new()
			var env = Environment.new()
			env.background_mode = Environment.BG_SKY
			var sky = Sky.new()
			sky.sky_material = mat
			env.sky = sky
			world_env.environment = env
			node3d.add_child(world_env)
			
			var camera = Camera3D.new()
			node3d.add_child(camera)
			test_node = node3d
		elif shader_type == "fog":
			var node3d = Node3D.new()
			viewport.add_child(node3d)
			
			var fog_volume = FogVolume.new()
			fog_volume.material = mat
			node3d.add_child(fog_volume)
			
			var camera = Camera3D.new()
			node3d.add_child(camera)
			test_node = node3d
			
		# Force compilation on dummy renderer
		RenderingServer.force_draw(false)
		if RenderingServer.has_method("force_sync"):
			RenderingServer.force_sync()
			
		# Use timer timeout instead of frame_post_draw, as frame_post_draw is never emitted in headless mode
		await create_timer(0.01).timeout
		
		# Clean up nodes
		root.remove_child(viewport)
		viewport.free()
		
		print(VERIFY_END_TAG)
		
	print("Shader validation finished.")
	quit(0)

# Recursively scan for gdshader files
func find_shaders(dir_path: String, out_list: Array):
	var dir = DirAccess.open(dir_path)
	if dir:
		dir.list_dir_begin()
		var file_name = dir.get_next()
		while file_name != "":
			if dir.current_is_dir():
				if file_name != "." and file_name != ".." and file_name != ".godot" and file_name != ".git" and file_name != "addons":
					find_shaders(dir_path + file_name + "/", out_list)
			else:
				if file_name.ends_with(".gdshader"):
					out_list.append(dir_path + file_name)
			file_name = dir.get_next()
