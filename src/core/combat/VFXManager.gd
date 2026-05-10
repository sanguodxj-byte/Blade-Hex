# VFXManager.gd
# 战术战斗特效管理器 (静态工具)
extends Node
class_name VFXManager

## 播放受击火花效果
static func play_hit_effect(parent: Node, pos: Vector3):
	var particles = GPUParticles3D.new()
	parent.add_child(particles)
	particles.global_position = pos + Vector3(0, 50, 0) # 角色中心高度
	
	# 配置粒子发射器
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 15
	particles.explosiveness = 1.0
	particles.lifetime = 0.4
	
	# 粒子材质
	var mat = ParticleProcessMaterial.new()
	mat.direction = Vector3(0, 1, 0)
	mat.spread = 180.0
	mat.initial_velocity_min = 100.0
	mat.initial_velocity_max = 200.0
	mat.gravity = Vector3(0, -500, 0)
	mat.scale_min = 2.0
	mat.scale_max = 5.0
	mat.color = Color(1, 0.8, 0.2) # 金色火花
	particles.process_material = mat
	
	# 绘制形状
	var mesh = QuadMesh.new()
	mesh.size = Vector2(5, 5)
	var draw_mat = StandardMaterial3D.new()
	draw_mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	draw_mat.billboard_mode = StandardMaterial3D.BILLBOARD_ENABLED
	draw_mat.albedo_color = Color(1, 0.8, 0.2)
	particles.draw_pass_1 = mesh
	particles.material_override = draw_mat
	
	# 播放完自动销毁
	var timer = parent.get_tree().create_timer(1.0)
	timer.timeout.connect(particles.queue_free)

## 播放死亡烟尘效果
static func play_death_effect(parent: Node, pos: Vector3):
	var particles = GPUParticles3D.new()
	parent.add_child(particles)
	particles.global_position = pos
	
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 30
	particles.explosiveness = 0.9
	particles.lifetime = 0.8
	
	var mat = ParticleProcessMaterial.new()
	mat.spread = 180.0
	mat.initial_velocity_min = 50.0
	mat.initial_velocity_max = 100.0
	mat.gravity = Vector3(0, 50, 0) # 缓缓上升
	mat.scale_min = 5.0
	mat.scale_max = 15.0
	mat.color = Color(0.3, 0.3, 0.3, 0.6) # 灰黑色烟尘
	particles.process_material = mat
	
	var mesh = QuadMesh.new()
	var draw_mat = StandardMaterial3D.new()
	draw_mat.transparency = StandardMaterial3D.TRANSPARENCY_ALPHA
	draw_mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	draw_mat.billboard_mode = StandardMaterial3D.BILLBOARD_ENABLED
	particles.draw_pass_1 = mesh
	particles.material_override = draw_mat
	
	var timer = parent.get_tree().create_timer(1.2)
	timer.timeout.connect(particles.queue_free)

## 播放火球术爆炸效果
static func play_explosion_effect(parent: Node, pos: Vector3):
	var particles = GPUParticles3D.new()
	parent.add_child(particles)
	particles.global_position = pos + Vector3(0, 10, 0)
	
	particles.emitting = true
	particles.one_shot = true
	particles.amount = 50
	particles.explosiveness = 1.0
	particles.lifetime = 0.6
	
	var mat = ParticleProcessMaterial.new()
	mat.spread = 180.0
	mat.initial_velocity_min = 150.0
	mat.initial_velocity_max = 300.0
	mat.scale_min = 10.0
	mat.scale_max = 25.0
	# 颜色渐变：从亮橙到暗红
	mat.color = Color(1.0, 0.4, 0.1) 
	particles.process_material = mat
	
	var mesh = QuadMesh.new()
	var draw_mat = StandardMaterial3D.new()
	draw_mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	draw_mat.billboard_mode = StandardMaterial3D.BILLBOARD_ENABLED
	particles.draw_pass_1 = mesh
	particles.material_override = draw_mat
	
	var timer = parent.get_tree().create_timer(1.0)
	timer.timeout.connect(particles.queue_free)


# ============================================================================
# 技能特效 — 根据 vfx_type 字符串分派不同视觉效果
# ============================================================================

## 技能特效配色表
const VFX_COLORS: Dictionary = {
	# 近战系
	"melee_combo": Color(1.0, 0.6, 0.1),
	"whirlwind": Color(0.9, 0.3, 0.1),
	"shield_bash": Color(0.7, 0.7, 0.8),
	"blood_vortex": Color(0.8, 0.1, 0.1),
	"poison_blade": Color(0.2, 0.8, 0.2),
	# 远程系
	"aimed_shot": Color(1.0, 1.0, 0.3),
	"double_shot": Color(0.9, 0.9, 0.3),
	"scatter_shot": Color(0.9, 0.7, 0.2),
	"trick_arrow": Color(0.5, 0.2, 1.0),
	# 魔法系
	"mana_shield": Color(0.3, 0.5, 1.0),
	"time_warp": Color(0.6, 0.2, 0.9),
	"holy_judgment": Color(1.0, 1.0, 0.6),
	"nature_wrath": Color(0.3, 0.7, 0.2),
	# 治疗系
	"heal": Color(0.3, 1.0, 0.5),
	"mass_heal": Color(0.3, 1.0, 0.5),
	"holy_shield": Color(0.8, 0.9, 1.0),
	"blessing": Color(1.0, 1.0, 0.8),
	# 辅助系
	"war_cry": Color(1.0, 0.4, 0.1),
	"stealth": Color(0.3, 0.3, 0.4),
	"shadow_clone": Color(0.4, 0.3, 0.7),
	"taunt": Color(1.0, 0.3, 0.0),
	"bulwark": Color(0.6, 0.7, 0.9),
	"rally": Color(1.0, 0.9, 0.3),
	"intimidate": Color(0.6, 0.0, 0.2),
	"heroic_call": Color(1.0, 0.85, 0.3),
	"inspire": Color(1.0, 0.9, 0.5),
	"dispel": Color(0.9, 0.9, 1.0),
}

## 技能特效粒子数量表
const VFX_PARTICLE_COUNT: Dictionary = {
	"whirlwind": 40,
	"blood_vortex": 35,
	"scatter_shot": 30,
	"mass_heal": 25,
	"heroic_call": 30,
	"inspire": 20,
}


## 播放技能特效（主入口）
static func play_skill_vfx(parent: Node, pos: Vector3, vfx_type: String):
	if vfx_type == "" or vfx_type == null:
		return
	match vfx_type:
		"heal", "mass_heal":
			_play_heal_vfx(parent, pos, vfx_type)
		"holy_shield", "mana_shield", "bulwark", "blessing":
			_play_shield_vfx(parent, pos, vfx_type)
		"stealth", "shadow_clone":
			_play_stealth_vfx(parent, pos, vfx_type)
		"whirlwind", "blood_vortex":
			_play_aoe_melee_vfx(parent, pos, vfx_type)
		"aimed_shot", "double_shot", "scatter_shot", "trick_arrow":
			_play_ranged_vfx(parent, pos, vfx_type)
		"war_cry", "taunt", "intimidate":
			_play_shockwave_vfx(parent, pos, vfx_type)
		_:
			_play_generic_skill_vfx(parent, pos, vfx_type)


static func _play_generic_skill_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(1, 1, 1))
	var count = VFX_PARTICLE_COUNT.get(vfx_type, 20)
	var particles = _create_particles(parent, pos + Vector3(0, 50, 0), count, 0.6, color)
	_auto_destroy(parent, particles, 1.0)


static func _play_heal_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(0.3, 1.0, 0.5))
	var count = VFX_PARTICLE_COUNT.get(vfx_type, 20)
	var particles = _create_particles(parent, pos + Vector3(0, 30, 0), count, 0.8, color)
	var mat: ParticleProcessMaterial = particles.process_material
	mat.direction = Vector3(0, 1, 0)
	mat.spread = 30.0
	mat.initial_velocity_min = 60.0
	mat.initial_velocity_max = 120.0
	mat.gravity = Vector3(0, -30, 0)
	_auto_destroy(parent, particles, 1.2)


static func _play_shield_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(0.8, 0.9, 1.0))
	var particles = _create_particles(parent, pos + Vector3(0, 50, 0), 25, 1.0, color)
	var mat: ParticleProcessMaterial = particles.process_material
	mat.direction = Vector3(0, 1, 0)
	mat.spread = 60.0
	mat.initial_velocity_min = 30.0
	mat.initial_velocity_max = 60.0
	mat.gravity = Vector3(0, -20, 0)
	_auto_destroy(parent, particles, 1.5)


static func _play_stealth_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(0.3, 0.3, 0.4))
	var particles = _create_particles(parent, pos + Vector3(0, 50, 0), 30, 0.8, color)
	var mat: ParticleProcessMaterial = particles.process_material
	mat.spread = 180.0
	mat.initial_velocity_min = 40.0
	mat.initial_velocity_max = 80.0
	mat.gravity = Vector3(0, 20, 0)
	_auto_destroy(parent, particles, 1.2)


static func _play_aoe_melee_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(0.9, 0.3, 0.1))
	var count = VFX_PARTICLE_COUNT.get(vfx_type, 40)
	var particles = _create_particles(parent, pos + Vector3(0, 20, 0), count, 0.5, color)
	var mat: ParticleProcessMaterial = particles.process_material
	mat.spread = 180.0
	mat.initial_velocity_min = 150.0
	mat.initial_velocity_max = 250.0
	mat.gravity = Vector3(0, -200, 0)
	_auto_destroy(parent, particles, 1.0)


static func _play_ranged_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(1.0, 1.0, 0.3))
	var particles = _create_particles(parent, pos + Vector3(0, 50, 0), 15, 0.3, color)
	var mat: ParticleProcessMaterial = particles.process_material
	mat.direction = Vector3(1, 0, 0)
	mat.spread = 15.0
	mat.initial_velocity_min = 300.0
	mat.initial_velocity_max = 500.0
	mat.gravity = Vector3(0, -50, 0)
	_auto_destroy(parent, particles, 0.8)


static func _play_shockwave_vfx(parent: Node, pos: Vector3, vfx_type: String):
	var color = VFX_COLORS.get(vfx_type, Color(1.0, 0.4, 0.1))
	var particles = _create_particles(parent, pos + Vector3(0, 30, 0), 30, 0.4, color)
	var mat: ParticleProcessMaterial = particles.process_material
	mat.spread = 180.0
	mat.initial_velocity_min = 100.0
	mat.initial_velocity_max = 200.0
	mat.gravity = Vector3(0, -100, 0)
	mat.scale_min = 5.0
	mat.scale_max = 12.0
	_auto_destroy(parent, particles, 0.8)


# ============================================================================
# VFX 工具方法
# ============================================================================

static func _create_particles(parent: Node, pos: Vector3, amount: int, lifetime: float, color: Color) -> GPUParticles3D:
	var particles = GPUParticles3D.new()
	parent.add_child(particles)
	particles.global_position = pos
	particles.emitting = true
	particles.one_shot = true
	particles.amount = amount
	particles.explosiveness = 0.9
	particles.lifetime = lifetime
	var mat = ParticleProcessMaterial.new()
	mat.spread = 180.0
	mat.initial_velocity_min = 80.0
	mat.initial_velocity_max = 160.0
	mat.gravity = Vector3(0, -300, 0)
	mat.scale_min = 3.0
	mat.scale_max = 8.0
	mat.color = color
	particles.process_material = mat
	var mesh = QuadMesh.new()
	mesh.size = Vector2(5, 5)
	var draw_mat = StandardMaterial3D.new()
	draw_mat.shading_mode = StandardMaterial3D.SHADING_MODE_UNSHADED
	draw_mat.billboard_mode = StandardMaterial3D.BILLBOARD_ENABLED
	draw_mat.transparency = StandardMaterial3D.TRANSPARENCY_ALPHA
	draw_mat.albedo_color = color
	particles.draw_pass_1 = mesh
	particles.material_override = draw_mat
	return particles


static func _auto_destroy(parent: Node, particles: GPUParticles3D, delay: float):
	var timer = parent.get_tree().create_timer(delay)
	timer.timeout.connect(particles.queue_free)
