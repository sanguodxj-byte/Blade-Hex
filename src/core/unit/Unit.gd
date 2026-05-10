# Unit.gd
# 战斗单位运行时类 (HD-2D 3D版本 - 融合深度 RPG 系统)
extends Node3D
class_name Unit

@export var data: UnitData

var current_hp: int
var grid_pos: Vector2i # 当前六边形坐标

# 状态标记
var has_moved: bool = false
var has_acted: bool = false
var using_primary_weapon: bool = true # 是否正在使用第一套武器组

# 技能盘引用（由 SkillTreeManager 管理）
var skill_tree: CharacterSkillTree = null

var visual_node: Node3D # 用于存储 Sprite3D 或 AnimatedSprite3D

func _ready():
	if data:
		current_hp = get_max_hp()
	_setup_visuals()

func _setup_visuals():
	var tex_height = 120.0
	var current_pixel_size = 1.0
	
	if data and data.sprite_frames:
		# 支持序列帧动画
		var anim_sprite = AnimatedSprite3D.new()
		anim_sprite.sprite_frames = data.sprite_frames
		anim_sprite.pixel_size = 1.0
		anim_sprite.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
		# 假设第一帧的高度为贴图高度
		if data.sprite_frames.get_frame_count("default") > 0:
			var frame_tex = data.sprite_frames.get_frame_texture("default", 0)
			if frame_tex: tex_height = frame_tex.get_height()
		anim_sprite.offset = Vector2(0, tex_height / 2.0)
		anim_sprite.play("default") # 默认播放 default 或 idle
		visual_node = anim_sprite
		current_pixel_size = anim_sprite.pixel_size
	else:
		# 静态图片或占位符
		var sprite = Sprite3D.new()
		if data and data.battle_sprite:
			sprite.texture = data.battle_sprite
			sprite.pixel_size = 1.0 
			tex_height = sprite.texture.get_height() if sprite.texture else 120.0
		else:
			var tex = PlaceholderTexture2D.new()
			tex.size = Vector2(80, 120)
			sprite.texture = tex
			sprite.pixel_size = 1.5
			tex_height = 120.0
			
			if name.begins_with("Player"):
				sprite.modulate = Color(0.2, 0.5, 1.0)
			else:
				sprite.modulate = Color(1.0, 0.2, 0.2)
				
		sprite.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
		sprite.offset = Vector2(0, tex_height / 2.0)
		visual_node = sprite
		current_pixel_size = sprite.pixel_size

	add_child(visual_node)

	var label = Label3D.new()
	label.text = str(current_hp) + "/" + str(get_max_hp())
	label.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	label.pixel_size = 3.0
	label.position = Vector3(0, tex_height * current_pixel_size + 20.0, 0)
	add_child(label)

## 播放动作动画 (如果有 AnimatedSprite3D)
func play_anim(anim_name: String):
	if visual_node is AnimatedSprite3D:
		if visual_node.sprite_frames.has_animation(anim_name):
			visual_node.play(anim_name)
		else:
			visual_node.play("default")

# ==========================================
# RPG 属性与结算计算 (根据 DND/Pathfinder 规则)
# ==========================================

## 计算属性修饰值 (10/11=0, 12/13=+1, 14/15=+2 ...)
func get_stat_modifier(score: int) -> int:
	return RPGRuleEngine.get_stat_modifier(score)

## 计算最大生命值 (base_max_hp + 技能盘HP加成)
## base_max_hp 已由生成器包含 CON 修正，此处不再重复计算
func get_max_hp() -> int:
	if not data: return 1
	var hp = data.base_max_hp
	if skill_tree:
		hp += skill_tree.get_hp_bonus()
	return max(1, hp)

## 计算行动点 AP（基础4 + DEX/CON修正）
## DEX+CON共同影响：floor(√((DEX+CON)/8))
## 1级(20) → +1 = 5AP, 中期(60) → +2 = 6AP, 后期(140) → +4 = 8AP
func get_ap() -> int:
	if not data: return 4
	var base = data.base_ap
	var stat_bonus = int(floor(sqrt((data.dex + data.con) / 8.0)))
	return base + stat_bonus

## 获取暴击阈值调整（攻击时）
## WIS越高，暴击所需d20越低（20→19→18...）
## 返回值：暴击需要 d20 >= (20 - floor(√(WIS/2)))
func get_crit_threshold() -> int:
	if not data: return 20
	var wis_bonus = int(floor(sqrt(data.wis / 2.0)))
	return maxi(15, 20 - wis_bonus)  # 最低15即暴击（扩大暴击范围）

## 获取受到暴击时的伤害倍率（防御时）
## WIS越高，被暴击伤害越低
## 公式：max(0.2, 1.0 - floor(√(WIS/2)) × 0.1)
## WIS 10→1.0全伤, WIS 40→0.7, WIS 100→0.3
func get_crit_damage_taken_multiplier() -> float:
	if not data: return 1.0
	var wis_bonus = int(floor(sqrt(data.wis / 2.0)))
	return maxf(0.2, 1.0 - wis_bonus * 0.1)

## 计算当前闪避值 AC（纯闪避，不含装甲）
## 人形/非人形统一：AC = 10 + 敏捷修正 + 盾牌格挡 + 技能修正
func get_ac() -> int:
	if not data: return 10
	
	var ac = 10  # 基础闪避值，人形统一为10
	var dex_mod = get_stat_modifier(data.dex)
	
	# 非人形单位可能有天然护甲加成到闪避（如蛇的灵活闪避）
	if data.base_ac != 10:
		ac = data.base_ac  # 非人形由模板设定
	
	# 盾牌提供闪避加成（格挡）
	var off_hand = get_off_hand()
	if off_hand is ArmorData and off_hand.armor_type == ArmorData.ArmorType.SHIELD:
		ac += off_hand.ac_bonus
	
	# 敏捷贡献AC：floor(sqrt(DEX / 2))
	var dex_ac = floorf(sqrt(data.dex / 2.0))
	# 重甲限制敏捷加成
	if data.armor and data.armor.max_dex_bonus > 0:
		dex_ac = min(dex_ac, data.armor.max_dex_bonus)
	
	# 技能盘AC加成
	if skill_tree:
		ac += skill_tree.get_ac_bonus()
	
	return ac + int(dex_ac)

## 计算完整有效AC（含所有运行时修正）
func get_effective_ac(attacker: Unit = null, _grid = null):
	var ac = get_ac()
	
	ac += SkillEffectExecutor.get_passive_ac_bonus(self)
	
	if attacker:
		var weapon = attacker.get_main_hand()
		if weapon and weapon.is_ranged:
			ac += SkillEffectExecutor.get_passive_ranged_ac_bonus(self)
	
	ac -= SkillEffectExecutor.get_keystone_ac_penalty(self)
	
	if data and data.is_defending:
		ac += 2
	
	var morale_effects = MoraleSystem.get_morale_effects(self)
	ac += morale_effects["ac_modifier"]
	
	return ac

## 获取当前装甲耐久度（额外HP）
func get_dr() -> int:
	if not data: return 0
	return maxi(0, data.current_dr)

## 获取装甲穿透阈值（d20对抗的值）
## 人形=护甲dr_threshold，非人形=natural_dr_threshold
func get_dr_threshold() -> int:
	if not data: return 0
	
	var threshold = 0
	
	# 人形：护甲穿透阈值
	if data.armor:
		threshold = max(threshold, data.armor.dr_threshold)
	
	# 非人形：天然护甲阈值
	if data.natural_dr_threshold > 0:
		threshold = max(threshold, data.natural_dr_threshold)
	
	# 装甲损毁后阈值降为0
	if data.current_dr <= 0:
		threshold = 0
	
	return threshold

## 获取最大装甲耐久度
func get_max_dr() -> int:
	if not data: return 0
	
	var dr = 0
	
	# 人形：护甲提供DR
	if data.armor:
		dr += data.armor.max_dr
	
	# 非人形：天然护甲
	if data.natural_dr > 0:
		dr += data.natural_dr
	
	# 盾牌也提供额外DR
	var off_hand = get_off_hand()
	if off_hand is ArmorData and off_hand.armor_type == ArmorData.ArmorType.SHIELD:
		dr += off_hand.max_dr
	
	return dr

## 初始化装甲耐久（战斗开始时调用）
func init_dr():
	if data:
		data.max_dr = get_max_dr()
		data.current_dr = data.max_dr

## 装甲承受伤害，返回实际DR伤害量
func take_dr_damage(amount: int) -> int:
	if not data or data.current_dr <= 0:
		return 0
	var actual = mini(amount, data.current_dr)
	data.current_dr -= actual
	if data.current_dr < 0:
		data.current_dr = 0
	return actual

## 装甲是否已损毁
func is_armor_destroyed() -> bool:
	if not data: return true
	return data.current_dr <= 0

## 获取当前持有的主手武器
func get_main_hand() -> WeaponData:
	return data.primary_main_hand if using_primary_weapon else data.secondary_main_hand

## 获取当前持有的副手物品
func get_off_hand() -> ItemData:
	return data.primary_off_hand if using_primary_weapon else data.secondary_off_hand

## 切换武器组
func switch_weapon_set():
	using_primary_weapon = !using_primary_weapon

## 计算命中加成 (属性修正 + 熟练 + 技能盘加成)
func get_attack_bonus() -> int:
	if not data: return 0
	
	var weapon = get_main_hand()
	var str_mod = get_stat_modifier(data.str)
	var dex_mod = get_stat_modifier(data.dex)
	
	var proficiency_bonus = RPGRuleEngine.get_proficiency_bonus(data.level) if data else 2
	
	var bonus = 0
	if weapon:
		if weapon.is_ranged:
			bonus = dex_mod + proficiency_bonus
			# 技能盘远程命中加成
			if skill_tree:
				bonus += skill_tree.get_ranged_hit_bonus()
		elif weapon.is_finesse and dex_mod > str_mod:
			bonus = dex_mod + proficiency_bonus # 灵巧武器且敏捷高，用敏捷
			if skill_tree:
				bonus += skill_tree.get_melee_hit_bonus()
		else:
			bonus = str_mod + proficiency_bonus
			if skill_tree:
				bonus += skill_tree.get_melee_hit_bonus()
	else:
		bonus = str_mod + proficiency_bonus
		if skill_tree:
			bonus += skill_tree.get_melee_hit_bonus()
			
	return bonus

## 掷骰伤害 — 武器骰 + 属性修正 + 技能盘加成
## 武器骰使用 Nd20，骰子数 = 武器基础骰数 + 等级加成
func roll_damage() -> Dictionary:
	var weapon = get_main_hand()
	var str_mod = get_stat_modifier(data.str)
	
	var dmg_dice = 0
	var d_text = "徒手(1d20)"
	
	# 等级带来的额外伤害骰数
	var level_extra = 0
	if data:
		# 每15级多1个d20（与 RPGRuleEngine.get_damage_dice_count 对齐）
		level_extra = RPGRuleEngine.get_damage_dice_count(data.level) - 1
	
	if weapon:
		# 武器基础骰（保留武器类型差异）
		for i in range(weapon.damage_dice_count):
			dmg_dice += randi_range(1, weapon.damage_dice_sides)
		
		# 等级额外 Nd20 伤害
		if level_extra > 0:
			var nd20_result = RPGRuleEngine.roll_nd20(level_extra)
			dmg_dice += nd20_result["total"]
		
		d_text = "%dd%d" % [weapon.damage_dice_count, weapon.damage_dice_sides]
		if level_extra > 0:
			d_text += "+%dd20" % level_extra
		
		# 伤害修正统一使用 STR
		if skill_tree:
			if weapon.is_ranged:
				str_mod += skill_tree.get_ranged_damage_bonus()
			else:
				str_mod += skill_tree.get_melee_damage_bonus()
	else:
		# 徒手：1d20 + 等级额外
		dmg_dice = randi_range(1, 20)
		if level_extra > 0:
			var nd20_result = RPGRuleEngine.roll_nd20(level_extra)
			dmg_dice += nd20_result["total"]
		if level_extra > 0:
			d_text = "徒手(%dd20)" % (1 + level_extra)
		# 徒手也用 STR
		if skill_tree:
			str_mod += skill_tree.get_melee_damage_bonus()
	
	var total_dmg = max(1, dmg_dice + str_mod)
	return {
		"dice": dmg_dice,
		"mod": str_mod,
		"total": total_dmg,
		"text": d_text
	}

# ==========================================
# 技能盘集成方法
# ==========================================

## 绑定技能盘
func bind_skill_tree(pskill_tree: CharacterSkillTree):
	skill_tree = pskill_tree

## 获取技能盘提供的移动力加成
func get_move_range() -> int:
	var move = data.base_move_range if data else 4
	if skill_tree:
		move += skill_tree.get_speed_bonus()
	return max(1, move)

## 获取技能盘提供的先攻加成
func get_initiative() -> int:
	var init = data.base_initiative if data else 0
	if skill_tree:
		init += skill_tree.get_initiative_bonus()
	return init

## 检查是否拥有某个技能盘效果
func has_skill_effect(effect_name: String) -> bool:
	if not skill_tree:
		return false
	return skill_tree.has_skill_effect(effect_name)

## 获取主动技能列表
func get_active_skill_nodes() -> Array[SkillNodeData]:
	if not skill_tree:
		return []
	return skill_tree.get_active_skills()

## 获取当前职业称号信息
## 返回: Dictionary { "title": String, "flags": int, "label": String }
##   title: "魔剑士", label: "STR+INT"
func get_class_title() -> Dictionary:
	if not skill_tree:
		return { "title": "无名者", "flags": 0, "label": "" }
	var attrs := _get_attrs_dict()
	return skill_tree.get_class_title(attrs)

## 快速获取职业称号名称
func get_class_title_name() -> String:
	if not skill_tree:
		return "无名者"
	return skill_tree.get_class_title_name(_get_attrs_dict())

## 获取各区域技能投资统计（用于 UI 展示）
func get_skill_region_stats() -> Dictionary:
	if not skill_tree:
		return {}
	return skill_tree.get_region_stats()

## 将 UnitData 属性转为字典供 ClassTitleResolver 使用
func _get_attrs_dict() -> Dictionary:
	if not data:
		return {}
	return {
		"str": data.str,
		"dex": data.dex,
		"con": data.con,
		"intel": data.intel,
		"wis": data.wis,
		"cha": data.cha,
	}

# ==========================================
# 动作执行
# ==========================================

func roll_d20() -> int:
	return randi_range(1, 20)

func attack_check(target_ac: int) -> Dictionary:
	var roll = roll_d20()
	var bonus = get_attack_bonus()
	var total = roll + bonus
	
	var is_critical = (roll == 20)
	var is_miss = (roll == 1)
	var is_hit = is_critical or (!is_miss and total >= target_ac)
	
	return {
		"hit": is_hit,
		"critical": is_critical,
		"roll": roll,
		"bonus": bonus,
		"total": total
	}

func take_damage(amount: int):
	current_hp -= amount
	current_hp = max(0, current_hp)
	_update_hp_label()
	
	if current_hp <= 0:
		VFXManager.play_death_effect(get_parent(), global_position)
		play_anim("die")
		# 延迟销毁，确保死亡动画能播完 (假设动画长度 1s 左右)
		await get_tree().create_timer(1.0).timeout
		die()
	else:
		VFXManager.play_hit_effect(get_parent(), global_position)
		play_anim("hit")
		# 受击动作播放完后回到待机 (简单逻辑：0.5s 后回 idle)
		await get_tree().create_timer(0.5).timeout
		play_anim("default")

func _update_hp_label():
	for child in get_children():
		if child is Label3D:
			child.text = str(current_hp) + "/" + str(get_max_hp())

func die():
	queue_free()
