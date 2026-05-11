# AudioManager.gd
# 全局音频管理组件 (Autoload)
# 负责处理背景音乐(BGM)的无缝切换、淡入淡出，音效(SFX)的池化播放，
# 环境氛围(Ambient)循环播放，以及地形脚步声映射
extends Node

## 预定义的游戏场景
enum Scenario {
	MAIN_MENU,    # 主菜单
	OVERWORLD,    # 大地图旅行
	COMBAT,       # 战术战斗
	TOWN,         # 城镇/村庄内部
	DUNGEON,      # 地牢/洞穴探索
	EVENT,        # 特殊事件/剧情
	TAVERN,       # 酒馆
	VICTORY,      # 胜利结算
	DEFEAT        # 失败结算
}

## 伤害类型（与 WeaponData.DamageType 对应，用于选择命中音效）
enum DamageType {
	SLASH,
	PIERCE,
	CRUSH,
}

# ============================================================================
# 配置参数
# ============================================================================

const MAX_SFX_PLAYERS: int = 12       # 同时最多播放的音效数量
const MAX_AMBIENT_PLAYERS: int = 4    # 同时最多播放的环境音数量
const SFX_BASE_PATH: String = "res://src/assets/audio/sfx/"
const BGM_BASE_PATH: String = "res://src/assets/audio/bgm/"
const AMBIENT_BASE_PATH: String = "res://src/assets/audio/ambient/"

# ============================================================================
# 内部节点
# ============================================================================

var _bgm_player1: AudioStreamPlayer
var _bgm_player2: AudioStreamPlayer
var _active_bgm_player: AudioStreamPlayer

var _sfx_players: Array[AudioStreamPlayer] = []
var _sfx_index: int = 0

var _ambient_players: Array[AudioStreamPlayer] = []
var _ambient_index: int = 0

var _bgm_tween: Tween

# 当前播放的音乐资源路径或实例，用于去重
var _current_bgm: AudioStream = null

# 当前播放的环境音列表（用于去重和停止）
var _active_ambients: Dictionary = {}  # ambient_name -> AudioStreamPlayer

# ============================================================================
# 音频库
# ============================================================================

# 场景背景音乐库
# 数据结构: { Scenario: { "variant_name": [track_path1, track_path2, ...] } }
var _bgm_playlists: Dictionary = {}

# 音效库
# 数据结构: { "sfx_name": [track_path1, track_path2, ...] }
var _sfx_library: Dictionary = {}

# 地形脚步声映射
# 数据结构: { terrain_key: "footstep_sfx_name" }
var _footstep_map: Dictionary = {}


# ============================================================================
# 生命周期
# ============================================================================

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS  # 暂停时也能播放音乐

	# 确保音频总线存在（Godot 默认只有 Master）
	_ensure_audio_buses()

	# 初始化 BGM 双播放器（交叉淡入淡出）
	_bgm_player1 = AudioStreamPlayer.new()
	_bgm_player1.bus = "Music"
	add_child(_bgm_player1)

	_bgm_player2 = AudioStreamPlayer.new()
	_bgm_player2.bus = "Music"
	add_child(_bgm_player2)

	_active_bgm_player = _bgm_player1

	# 初始化 SFX 池
	for i in range(MAX_SFX_PLAYERS):
		var p = AudioStreamPlayer.new()
		p.bus = "SFX"
		add_child(p)
		_sfx_players.append(p)

	# 初始化 Ambient 播放器池
	for i in range(MAX_AMBIENT_PLAYERS):
		var p = AudioStreamPlayer.new()
		p.bus = "Ambient"
		add_child(p)
		_ambient_players.append(p)

	# 注册所有音频资源
	_init_bgm_playlists()
	_init_ui_sfx()
	_init_combat_attack_sfx()
	_init_combat_skill_sfx()
	_init_combat_spell_sfx()
	_init_combat_status_sfx()
	_init_combat_flow_sfx()
	_init_movement_sfx()
	_init_overworld_sfx()
	_init_town_sfx()
	_init_character_sfx()
	_init_quest_sfx()
	_init_footstep_map()


# ============================================================================
# 音频总线
# ============================================================================

## 确保项目所需的音频总线存在
## Godot 4 默认只有 "Master"，需要手动添加 Music/SFX/Ambient
func _ensure_audio_buses() -> void:
	var needed_buses := ["Music", "SFX", "Ambient"]
	for bus_name in needed_buses:
		if AudioServer.get_bus_index(bus_name) < 0:
			AudioServer.add_bus()
			var idx := AudioServer.bus_count - 1
			AudioServer.set_bus_name(idx, bus_name)
			AudioServer.set_bus_send(idx, "Master")


# ============================================================================
# BGM 注册
# ============================================================================

## 初始化所有场景的背景音乐播放列表
func _init_bgm_playlists() -> void:
	# 主菜单
	add_bgm_variant(Scenario.MAIN_MENU, "default", BGM_BASE_PATH + "main_menu.ogg")

	# 大地图旅行
	add_bgm_variant(Scenario.OVERWORLD, "default", BGM_BASE_PATH + "overworld_travel.ogg")
	add_bgm_variant(Scenario.OVERWORLD, "night", BGM_BASE_PATH + "overworld_night.ogg")
	add_bgm_variant(Scenario.OVERWORLD, "danger", BGM_BASE_PATH + "overworld_danger.ogg")

	# 战斗场景
	add_bgm_variant(Scenario.COMBAT, "normal", BGM_BASE_PATH + "normal_battle.ogg")
	add_bgm_variant(Scenario.COMBAT, "boss", BGM_BASE_PATH + "boss_battle.ogg")
	add_bgm_variant(Scenario.COMBAT, "ambush", BGM_BASE_PATH + "ambush_battle.ogg")

	# 城镇
	add_bgm_variant(Scenario.TOWN, "default", BGM_BASE_PATH + "town_theme.ogg")
	add_bgm_variant(Scenario.TOWN, "capital", BGM_BASE_PATH + "town_capital.ogg")

	# 地牢
	add_bgm_variant(Scenario.DUNGEON, "default", BGM_BASE_PATH + "dungeon_explore.ogg")

	# 剧情/特殊事件
	add_bgm_variant(Scenario.EVENT, "default", BGM_BASE_PATH + "event_story.ogg")

	# 酒馆
	add_bgm_variant(Scenario.TAVERN, "default", BGM_BASE_PATH + "tavern_music.ogg")

	# 胜利/失败结算
	add_bgm_variant(Scenario.VICTORY, "default", BGM_BASE_PATH + "victory_fanfare.ogg")
	add_bgm_variant(Scenario.DEFEAT, "default", BGM_BASE_PATH + "defeat_somber.ogg")


# ============================================================================
# SFX 注册 — 按模块拆分
# ============================================================================

## 注册辅助：批量注册同名变体
func _register_sfx_variants(name: String, paths: Array[String]) -> void:
	for path in paths:
		register_sfx(name, path)

## --- UI 通用 ---
func _init_ui_sfx() -> void:
	register_sfx("ui_click", SFX_BASE_PATH + "ui/ui_click.wav")
	register_sfx("ui_hover", SFX_BASE_PATH + "ui/ui_hover.wav")
	register_sfx("ui_error", SFX_BASE_PATH + "ui/ui_error.wav")
	_register_sfx_variants("ui_panel_open", [
		SFX_BASE_PATH + "ui/ui_panel_open_1.wav",
		SFX_BASE_PATH + "ui/ui_panel_open_2.wav",
	])
	_register_sfx_variants("ui_panel_close", [
		SFX_BASE_PATH + "ui/ui_panel_close_1.wav",
		SFX_BASE_PATH + "ui/ui_panel_close_2.wav",
	])
	register_sfx("ui_tab_switch", SFX_BASE_PATH + "ui/ui_tab_switch.wav")
	_register_sfx_variants("ui_checkbox", [
		SFX_BASE_PATH + "ui/ui_checkbox_on.wav",
		SFX_BASE_PATH + "ui/ui_checkbox_off.wav",
	])
	register_sfx("ui_slider", SFX_BASE_PATH + "ui/ui_slider.wav")
	register_sfx("ui_tooltip_show", SFX_BASE_PATH + "ui/ui_tooltip_show.wav")
	_register_sfx_variants("ui_gold_change", [
		SFX_BASE_PATH + "ui/ui_gold_gain.wav",
		SFX_BASE_PATH + "ui/ui_gold_spend.wav",
	])
	register_sfx("ui_level_up", SFX_BASE_PATH + "ui/ui_level_up.wav")
	_register_sfx_variants("ui_notification", [
		SFX_BASE_PATH + "ui/ui_notification_1.wav",
		SFX_BASE_PATH + "ui/ui_notification_2.wav",
	])
	register_sfx("ui_quest_accept", SFX_BASE_PATH + "ui/ui_quest_accept.wav")
	register_sfx("ui_quest_complete", SFX_BASE_PATH + "ui/ui_quest_complete.wav")
	register_sfx("ui_save", SFX_BASE_PATH + "ui/ui_save.wav")
	register_sfx("ui_load", SFX_BASE_PATH + "ui/ui_load.wav")

## --- 战斗：攻击与伤害 ---
func _init_combat_attack_sfx() -> void:
	# 砍伤 (SLASH)
	_register_sfx_variants("combat_sword_hit", [
		SFX_BASE_PATH + "combat/attack/sword_hit_1.wav",
		SFX_BASE_PATH + "combat/attack/sword_hit_2.wav",
		SFX_BASE_PATH + "combat/attack/sword_hit_3.wav",
	])
	_register_sfx_variants("combat_sword_miss", [
		SFX_BASE_PATH + "combat/attack/sword_miss_1.wav",
		SFX_BASE_PATH + "combat/attack/sword_miss_2.wav",
	])
	_register_sfx_variants("combat_sword_crit", [
		SFX_BASE_PATH + "combat/attack/sword_crit_1.wav",
		SFX_BASE_PATH + "combat/attack/sword_crit_2.wav",
	])

	# 刺伤 (PIERCE)
	_register_sfx_variants("combat_pierce_hit", [
		SFX_BASE_PATH + "combat/attack/pierce_hit_1.wav",
		SFX_BASE_PATH + "combat/attack/pierce_hit_2.wav",
	])

	# 钝伤 (CRUSH)
	_register_sfx_variants("combat_crush_hit", [
		SFX_BASE_PATH + "combat/attack/crush_hit_1.wav",
		SFX_BASE_PATH + "combat/attack/crush_hit_2.wav",
	])

	# 弓箭
	_register_sfx_variants("combat_arrow_fire", [
		SFX_BASE_PATH + "combat/attack/arrow_fire_1.wav",
		SFX_BASE_PATH + "combat/attack/arrow_fire_2.wav",
	])
	_register_sfx_variants("combat_arrow_hit", [
		SFX_BASE_PATH + "combat/attack/arrow_hit_1.wav",
		SFX_BASE_PATH + "combat/attack/arrow_hit_2.wav",
	])
	register_sfx("combat_arrow_miss", SFX_BASE_PATH + "combat/attack/arrow_miss.wav")

	# 通用
	_register_sfx_variants("combat_graze", [
		SFX_BASE_PATH + "combat/attack/graze_1.wav",
		SFX_BASE_PATH + "combat/attack/graze_2.wav",
	])
	register_sfx("combat_fumble", SFX_BASE_PATH + "combat/attack/fumble.wav")
	register_sfx("combat_death_save", SFX_BASE_PATH + "combat/attack/death_save.wav")
	_register_sfx_variants("combat_death", [
		SFX_BASE_PATH + "combat/attack/death_1.wav",
		SFX_BASE_PATH + "combat/attack/death_2.wav",
	])
	_register_sfx_variants("combat_armor_hit", [
		SFX_BASE_PATH + "combat/attack/armor_hit_1.wav",
		SFX_BASE_PATH + "combat/attack/armor_hit_2.wav",
	])
	register_sfx("combat_armor_break", SFX_BASE_PATH + "combat/attack/armor_break.wav")

## --- 战斗：技能（与 VFXManager.VFX_COLORS 一一对应） ---
func _init_combat_skill_sfx() -> void:
	# 近战系
	_register_sfx_variants("skill_melee_combo", [
		SFX_BASE_PATH + "combat/skill/melee_combo_1.wav",
		SFX_BASE_PATH + "combat/skill/melee_combo_2.wav",
	])
	register_sfx("skill_whirlwind", SFX_BASE_PATH + "combat/skill/whirlwind.wav")
	_register_sfx_variants("skill_shield_bash", [
		SFX_BASE_PATH + "combat/skill/shield_bash_1.wav",
		SFX_BASE_PATH + "combat/skill/shield_bash_2.wav",
	])
	register_sfx("skill_blood_vortex", SFX_BASE_PATH + "combat/skill/blood_vortex.wav")
	register_sfx("skill_poison_blade", SFX_BASE_PATH + "combat/skill/poison_blade.wav")

	# 远程系
	register_sfx("skill_aimed_shot", SFX_BASE_PATH + "combat/skill/aimed_shot.wav")
	register_sfx("skill_double_shot", SFX_BASE_PATH + "combat/skill/double_shot.wav")
	register_sfx("skill_scatter_shot", SFX_BASE_PATH + "combat/skill/scatter_shot.wav")
	register_sfx("skill_trick_arrow", SFX_BASE_PATH + "combat/skill/trick_arrow.wav")

	# 魔法系
	register_sfx("skill_mana_shield", SFX_BASE_PATH + "combat/skill/mana_shield.wav")
	register_sfx("skill_time_warp", SFX_BASE_PATH + "combat/skill/time_warp.wav")
	register_sfx("skill_holy_judgment", SFX_BASE_PATH + "combat/skill/holy_judgment.wav")
	register_sfx("skill_nature_wrath", SFX_BASE_PATH + "combat/skill/nature_wrath.wav")

	# 治疗系
	_register_sfx_variants("skill_heal", [
		SFX_BASE_PATH + "combat/skill/heal_1.wav",
		SFX_BASE_PATH + "combat/skill/heal_2.wav",
	])
	register_sfx("skill_mass_heal", SFX_BASE_PATH + "combat/skill/mass_heal.wav")
	register_sfx("skill_holy_shield", SFX_BASE_PATH + "combat/skill/holy_shield.wav")
	register_sfx("skill_blessing", SFX_BASE_PATH + "combat/skill/blessing.wav")

	# 辅助系
	_register_sfx_variants("skill_war_cry", [
		SFX_BASE_PATH + "combat/skill/war_cry_1.wav",
		SFX_BASE_PATH + "combat/skill/war_cry_2.wav",
	])
	register_sfx("skill_stealth", SFX_BASE_PATH + "combat/skill/stealth.wav")
	register_sfx("skill_shadow_clone", SFX_BASE_PATH + "combat/skill/shadow_clone.wav")
	_register_sfx_variants("skill_taunt", [
		SFX_BASE_PATH + "combat/skill/taunt_1.wav",
		SFX_BASE_PATH + "combat/skill/taunt_2.wav",
	])
	register_sfx("skill_bulwark", SFX_BASE_PATH + "combat/skill/bulwark.wav")
	register_sfx("skill_rally", SFX_BASE_PATH + "combat/skill/rally.wav")
	register_sfx("skill_intimidate", SFX_BASE_PATH + "combat/skill/intimidate.wav")
	register_sfx("skill_heroic_call", SFX_BASE_PATH + "combat/skill/heroic_call.wav")
	register_sfx("skill_inspire", SFX_BASE_PATH + "combat/skill/inspire.wav")
	register_sfx("skill_dispel", SFX_BASE_PATH + "combat/skill/dispel.wav")

## --- 战斗：法术（8 大学派 × 施放/命中） ---
func _init_combat_spell_sfx() -> void:
	var schools := ["fire", "ice", "lightning", "earth", "holy", "shadow", "arcane", "nature"]
	for school in schools:
		register_sfx("spell_%s_cast" % school, SFX_BASE_PATH + "combat/spell/%s_cast.wav" % school)
		register_sfx("spell_%s_impact" % school, SFX_BASE_PATH + "combat/spell/%s_impact.wav" % school)

	# 法术失败
	register_sfx("spell_no_mana", SFX_BASE_PATH + "combat/spell/no_mana.wav")
	register_sfx("spell_cooldown", SFX_BASE_PATH + "combat/spell/cooldown.wav")

## --- 战斗：状态效果 ---
func _init_combat_status_sfx() -> void:
	_register_sfx_variants("status_burning", [
		SFX_BASE_PATH + "combat/status/burning_1.wav",
		SFX_BASE_PATH + "combat/status/burning_2.wav",
	])
	register_sfx("status_freezing", SFX_BASE_PATH + "combat/status/freezing.wav")
	register_sfx("status_poison", SFX_BASE_PATH + "combat/status/poison.wav")
	register_sfx("status_bleed", SFX_BASE_PATH + "combat/status/bleed.wav")
	register_sfx("status_stun", SFX_BASE_PATH + "combat/status/stun.wav")
	register_sfx("status_root", SFX_BASE_PATH + "combat/status/root.wav")
	register_sfx("status_cure", SFX_BASE_PATH + "combat/status/cure.wav")
	register_sfx("status_rally", SFX_BASE_PATH + "combat/status/rally.wav")
	register_sfx("status_rout", SFX_BASE_PATH + "combat/status/rout.wav")

## --- 战斗：流程与环境事件 ---
func _init_combat_flow_sfx() -> void:
	# 回合流程
	register_sfx("combat_turn_start", SFX_BASE_PATH + "combat/flow/turn_start_player.wav")
	register_sfx("combat_enemy_turn", SFX_BASE_PATH + "combat/flow/turn_start_enemy.wav")
	register_sfx("combat_victory", SFX_BASE_PATH + "combat/flow/victory.wav")
	register_sfx("combat_defeat", SFX_BASE_PATH + "combat/flow/defeat.wav")

	# 战斗事件
	register_sfx("combat_counter", SFX_BASE_PATH + "combat/flow/counter_attack.wav")
	register_sfx("combat_aoo", SFX_BASE_PATH + "combat/flow/attack_of_opportunity.wav")
	register_sfx("combat_flanking", SFX_BASE_PATH + "combat/flow/flanking.wav")
	register_sfx("combat_charge", SFX_BASE_PATH + "combat/flow/charge.wav")
	register_sfx("combat_mount_charge", SFX_BASE_PATH + "combat/flow/mount_charge.wav")

	# 环境事件（对应 EnvironmentEventSystem）
	register_sfx("combat_env_storm", SFX_BASE_PATH + "combat/flow/env_storm.wav")
	register_sfx("combat_env_fog", SFX_BASE_PATH + "combat/flow/env_fog.wav")
	register_sfx("combat_env_quake", SFX_BASE_PATH + "combat/flow/env_earthquake.wav")
	register_sfx("combat_env_poison_fog", SFX_BASE_PATH + "combat/flow/env_poison_fog.wav")
	register_sfx("combat_env_lava", SFX_BASE_PATH + "combat/flow/env_lava.wav")

## --- 移动与交互 ---
func _init_movement_sfx() -> void:
	# 脚步声变体（每种地形多个变体，播放时 random_pitch）
	_register_sfx_variants("move_footstep_grass", [
		SFX_BASE_PATH + "move/footstep_grass_1.wav",
		SFX_BASE_PATH + "move/footstep_grass_2.wav",
		SFX_BASE_PATH + "move/footstep_grass_3.wav",
		SFX_BASE_PATH + "move/footstep_grass_4.wav",
	])
	_register_sfx_variants("move_footstep_stone", [
		SFX_BASE_PATH + "move/footstep_stone_1.wav",
		SFX_BASE_PATH + "move/footstep_stone_2.wav",
		SFX_BASE_PATH + "move/footstep_stone_3.wav",
		SFX_BASE_PATH + "move/footstep_stone_4.wav",
	])
	_register_sfx_variants("move_footstep_snow", [
		SFX_BASE_PATH + "move/footstep_snow_1.wav",
		SFX_BASE_PATH + "move/footstep_snow_2.wav",
		SFX_BASE_PATH + "move/footstep_snow_3.wav",
	])
	_register_sfx_variants("move_footstep_mud", [
		SFX_BASE_PATH + "move/footstep_mud_1.wav",
		SFX_BASE_PATH + "move/footstep_mud_2.wav",
		SFX_BASE_PATH + "move/footstep_mud_3.wav",
	])
	_register_sfx_variants("move_footstep_wood", [
		SFX_BASE_PATH + "move/footstep_wood_1.wav",
		SFX_BASE_PATH + "move/footstep_wood_2.wav",
		SFX_BASE_PATH + "move/footstep_wood_3.wav",
	])
	_register_sfx_variants("move_footstep_water", [
		SFX_BASE_PATH + "move/footstep_water_1.wav",
		SFX_BASE_PATH + "move/footstep_water_2.wav",
		SFX_BASE_PATH + "move/footstep_water_3.wav",
	])
	_register_sfx_variants("move_footstep_sand", [
		SFX_BASE_PATH + "move/footstep_sand_1.wav",
		SFX_BASE_PATH + "move/footstep_sand_2.wav",
		SFX_BASE_PATH + "move/footstep_sand_3.wav",
	])

	# 单位选择与路径
	register_sfx("move_unit_select", SFX_BASE_PATH + "move/unit_select.wav")
	register_sfx("move_unit_deselect", SFX_BASE_PATH + "move/unit_deselect.wav")
	register_sfx("move_cell_highlight", SFX_BASE_PATH + "move/cell_highlight.wav")
	register_sfx("move_path_confirm", SFX_BASE_PATH + "move/path_confirm.wav")
	register_sfx("move_weapon_switch", SFX_BASE_PATH + "move/weapon_switch.wav")

## --- 大地图战略层 ---
func _init_overworld_sfx() -> void:
	register_sfx("ow_travel_start", SFX_BASE_PATH + "overworld/travel_start.wav")
	register_sfx("ow_travel_stop", SFX_BASE_PATH + "overworld/travel_stop.wav")
	_register_sfx_variants("ow_encounter", [
		SFX_BASE_PATH + "overworld/encounter_1.wav",
		SFX_BASE_PATH + "overworld/encounter_2.wav",
	])
	register_sfx("ow_town_enter", SFX_BASE_PATH + "overworld/town_enter.wav")
	register_sfx("ow_town_leave", SFX_BASE_PATH + "overworld/town_leave.wav")
	register_sfx("ow_combat_trigger", SFX_BASE_PATH + "overworld/combat_trigger.wav")
	register_sfx("ow_fog_reveal", SFX_BASE_PATH + "overworld/fog_reveal.wav")
	_register_sfx_variants("ow_day_cycle", [
		SFX_BASE_PATH + "overworld/day_cycle_dawn.wav",
		SFX_BASE_PATH + "overworld/day_cycle_dusk.wav",
	])
	register_sfx("ow_season_change", SFX_BASE_PATH + "overworld/season_change.wav")
	register_sfx("ow_enemy_sighted", SFX_BASE_PATH + "overworld/enemy_sighted.wav")
	register_sfx("ow_poi_discover", SFX_BASE_PATH + "overworld/poi_discover.wav")

## --- 城镇交互 ---
func _init_town_sfx() -> void:
	register_sfx("town_trade_buy", SFX_BASE_PATH + "town/trade_buy.wav")
	register_sfx("town_trade_sell", SFX_BASE_PATH + "town/trade_sell.wav")
	register_sfx("town_trade_fail", SFX_BASE_PATH + "town/trade_fail.wav")
	register_sfx("town_rest_inn", SFX_BASE_PATH + "town/rest_inn.wav")
	register_sfx("town_temple_heal", SFX_BASE_PATH + "town/temple_heal.wav")
	register_sfx("town_smithy_upgrade", SFX_BASE_PATH + "town/smithy_upgrade.wav")
	register_sfx("town_smithy_fail", SFX_BASE_PATH + "town/smithy_fail.wav")
	register_sfx("town_recruit", SFX_BASE_PATH + "town/recruit.wav")
	register_sfx("town_train", SFX_BASE_PATH + "town/train.wav")
	register_sfx("town_arena_fight", SFX_BASE_PATH + "town/arena_fight.wav")
	register_sfx("town_arena_win", SFX_BASE_PATH + "town/arena_win.wav")
	register_sfx("town_repair", SFX_BASE_PATH + "town/repair.wav")

## --- 角色 / 技能树 ---
func _init_character_sfx() -> void:
	register_sfx("char_node_activate", SFX_BASE_PATH + "character/node_activate.wav")
	register_sfx("char_node_locked", SFX_BASE_PATH + "character/node_locked.wav")
	register_sfx("char_spell_learn", SFX_BASE_PATH + "character/spell_learn.wav")
	register_sfx("char_equip_change", SFX_BASE_PATH + "character/equip_change.wav")
	register_sfx("char_equip_fail", SFX_BASE_PATH + "character/equip_fail.wav")
	register_sfx("char_stat_increase", SFX_BASE_PATH + "character/stat_increase.wav")

## --- 任务 ---
func _init_quest_sfx() -> void:
	register_sfx("quest_new", SFX_BASE_PATH + "quest/quest_new.wav")
	register_sfx("quest_accept", SFX_BASE_PATH + "quest/quest_accept.wav")
	register_sfx("quest_progress", SFX_BASE_PATH + "quest/quest_progress.wav")
	register_sfx("quest_complete", SFX_BASE_PATH + "quest/quest_complete.wav")
	register_sfx("quest_fail", SFX_BASE_PATH + "quest/quest_fail.wav")
	register_sfx("quest_expire", SFX_BASE_PATH + "quest/quest_expire.wav")

## --- 地形脚步声映射 ---
func _init_footstep_map() -> void:
	# BattleCellData.TerrainType -> 脚步声 SFX 名称
	_footstep_map = {
		"plains": "move_footstep_grass",
		"grassland": "move_footstep_grass",
		"savanna": "move_footstep_grass",
		"forest": "move_footstep_grass",
		"dense_forest": "move_footstep_grass",
		"hills": "move_footstep_stone",
		"mountain": "move_footstep_stone",
		"shallow_water": "move_footstep_water",
		"deep_water": "move_footstep_water",
		"swamp": "move_footstep_mud",
		"road": "move_footstep_stone",
		"sand": "move_footstep_sand",
		"snow": "move_footstep_snow",
		"wall": "move_footstep_stone",
		"ruins": "move_footstep_stone",
		"poison_mushroom": "move_footstep_grass",
		"lucky_grass": "move_footstep_grass",
		# OverworldTerrain.Type -> 脚步声（大地图用）
		"ow_plains": "move_footstep_grass",
		"ow_forest": "move_footstep_grass",
		"ow_mountain": "move_footstep_stone",
		"ow_swamp": "move_footstep_mud",
		"ow_water": "move_footstep_water",
		"ow_road": "move_footstep_stone",
		"ow_desert": "move_footstep_sand",
	}


# ============================================================================
# BGM 接口
# ============================================================================

## 为特定场景和变体注册一首曲目（如果多次注册同一变体，将放入数组中随机播放）
func add_bgm_variant(scenario: Scenario, variant: String, stream_path: String) -> void:
	if not _bgm_playlists.has(scenario):
		_bgm_playlists[scenario] = {}

	if not _bgm_playlists[scenario].has(variant):
		_bgm_playlists[scenario][variant] = []

	var tracks: Array = _bgm_playlists[scenario][variant]
	if not tracks.has(stream_path):
		tracks.append(stream_path)


## 按场景和变体播放音乐 (自动从该变体中随机选取一首)
## variant: "default", "boss", "night", "day" 等
func play_scenario_bgm(scenario: Scenario, variant: String = "default", crossfade_time: float = 1.5) -> void:
	if not _bgm_playlists.has(scenario):
		push_warning("AudioManager: 场景 ", scenario, " 未注册任何音乐。")
		return

	var variants = _bgm_playlists[scenario]
	var tracks_to_play: Array = []

	# 如果请求的变体存在，从中选择
	if variants.has(variant) and not variants[variant].is_empty():
		tracks_to_play = variants[variant]
	# 退而求其次使用默认变体
	elif variants.has("default") and not variants["default"].is_empty():
		push_warning("AudioManager: 找不到变体 '", variant, "'，回退到 'default'。")
		tracks_to_play = variants["default"]
	else:
		push_warning("AudioManager: 场景 ", scenario, " 中找不到变体 '", variant, "' 或 'default'。")
		return

	# 如果有多首曲目，随机选取一首
	var track_path = tracks_to_play[randi() % tracks_to_play.size()]
	play_bgm(track_path, crossfade_time)


## 播放背景音乐，支持自动交叉淡入淡出
func play_bgm(stream, crossfade_time: float = 1.5) -> void:
	var audio_stream: AudioStream = _get_audio_stream(stream)

	if not audio_stream:
		push_warning("AudioManager: 无法播放BGM，无效的流或路径 - ", stream)
		return

	# 避免重复播放相同的音乐
	if _current_bgm == audio_stream and _active_bgm_player.playing:
		return

	_current_bgm = audio_stream

	var fading_out_player = _active_bgm_player
	var fading_in_player = _bgm_player2 if _active_bgm_player == _bgm_player1 else _bgm_player1

	_active_bgm_player = fading_in_player
	_active_bgm_player.stream = audio_stream
	_active_bgm_player.play()

	if _bgm_tween and _bgm_tween.is_valid():
		_bgm_tween.kill()

	_bgm_tween = create_tween()

	if crossfade_time > 0:
		_active_bgm_player.volume_db = -80.0
		_bgm_tween.tween_property(_active_bgm_player, "volume_db", 0.0, crossfade_time).set_trans(Tween.TRANS_SINE)
		if fading_out_player.playing:
			_bgm_tween.parallel().tween_property(fading_out_player, "volume_db", -80.0, crossfade_time).set_trans(Tween.TRANS_SINE)
	else:
		_active_bgm_player.volume_db = 0.0
		if fading_out_player.playing:
			fading_out_player.stop()
			fading_out_player.volume_db = 0.0

	_bgm_tween.finished.connect(func():
		if fading_out_player != _active_bgm_player:
			fading_out_player.stop()
			fading_out_player.volume_db = 0.0
	)


## 停止背景音乐
func stop_bgm(fade_out_time: float = 1.0) -> void:
	_current_bgm = null

	if _bgm_tween and _bgm_tween.is_valid():
		_bgm_tween.kill()

	if fade_out_time > 0:
		_bgm_tween = create_tween()
		_bgm_tween.tween_property(_active_bgm_player, "volume_db", -80.0, fade_out_time).set_trans(Tween.TRANS_SINE)
		_bgm_tween.finished.connect(func():
			_active_bgm_player.stop()
			_active_bgm_player.volume_db = 0.0
		)
	else:
		_active_bgm_player.stop()
		_active_bgm_player.volume_db = 0.0


# ============================================================================
# SFX 通用接口
# ============================================================================

## 注册一个命名音效。同一名称多次注册会形成列表，播放时随机抽取。
func register_sfx(sfx_name: String, stream_path: String) -> void:
	if not _sfx_library.has(sfx_name):
		_sfx_library[sfx_name] = []

	var tracks: Array = _sfx_library[sfx_name]
	if not tracks.has(stream_path):
		tracks.append(stream_path)


## 按名称播放预先注册的音效
func play_sfx_name(sfx_name: String, volume_db: float = 0.0, pitch_scale: float = 1.0) -> void:
	if not _sfx_library.has(sfx_name) or _sfx_library[sfx_name].is_empty():
		push_warning("AudioManager: 找不到注册的音效名称 - ", sfx_name)
		return

	var tracks = _sfx_library[sfx_name]
	var track_path = tracks[randi() % tracks.size()]

	play_sfx(track_path, volume_db, pitch_scale)


## 按名称播放音效，并自带随机音高 (极其适合脚步声/挥砍声)
func play_sfx_name_random_pitch(sfx_name: String, volume_db: float = 0.0, min_pitch: float = 0.9, max_pitch: float = 1.1) -> void:
	play_sfx_name(sfx_name, volume_db, randf_range(min_pitch, max_pitch))


## 播放音效（基础接口）
func play_sfx(stream, volume_db: float = 0.0, pitch_scale: float = 1.0) -> void:
	var audio_stream: AudioStream = _get_audio_stream(stream)

	if not audio_stream:
		return

	var player = _sfx_players[_sfx_index]
	player.stream = audio_stream
	player.volume_db = volume_db
	player.pitch_scale = pitch_scale
	player.play()

	_sfx_index = (_sfx_index + 1) % MAX_SFX_PLAYERS


## 播放带有随机音高的音效
func play_sfx_random_pitch(stream, volume_db: float = 0.0, min_pitch: float = 0.9, max_pitch: float = 1.1) -> void:
	play_sfx(stream, volume_db, randf_range(min_pitch, max_pitch))


# ============================================================================
# SFX 高级接口
# ============================================================================

## 按伤害类型播放攻击命中音效
## dmg_type: AudioManager.DamageType 枚举
func play_attack_hit_sfx(dmg_type: int, is_crit: bool = false) -> void:
	if is_crit:
		match dmg_type:
			DamageType.SLASH:
				play_sfx_name("combat_sword_crit")
			DamageType.PIERCE:
				play_sfx_name("combat_pierce_hit")
			DamageType.CRUSH:
				play_sfx_name("combat_crush_hit")
		return

	match dmg_type:
		DamageType.SLASH:
			play_sfx_name("combat_sword_hit")
		DamageType.PIERCE:
			play_sfx_name("combat_pierce_hit")
		DamageType.CRUSH:
			play_sfx_name("combat_crush_hit")


## 按伤害类型播放攻击未中音效
func play_attack_miss_sfx(dmg_type: int) -> void:
	match dmg_type:
		DamageType.SLASH:
			play_sfx_name("combat_sword_miss")
		DamageType.PIERCE, DamageType.CRUSH:
			play_sfx_name("combat_arrow_miss")  # 共用挥空声


## 按地形类型播放脚步声（战斗地图 BattleCellData.TerrainType）
func play_footstep_by_terrain(terrain_name: String) -> void:
	var sfx_name: String = _footstep_map.get(terrain_name.to_lower(), "move_footstep_grass")
	play_sfx_name_random_pitch(sfx_name)


## 按地形枚举播放脚步声（大地图 OverworldTerrain.Type）
func play_overworld_footstep(ow_terrain_name: String) -> void:
	var key: String = "ow_" + ow_terrain_name.to_lower()
	var sfx_name: String = _footstep_map.get(key, "move_footstep_grass")
	play_sfx_name_random_pitch(sfx_name)


## 按技能 vfx_type 播放技能音效（与 VFXManager 的 vfx_type 一一对应）
func play_skill_sfx(vfx_type: String) -> void:
	var sfx_name: String = "skill_" + vfx_type
	if _sfx_library.has(sfx_name):
		play_sfx_name(sfx_name)
	else:
		push_warning("AudioManager: 技能音效未注册 - ", sfx_name)


## 按法术学派播放法术音效
func play_spell_cast_sfx(school: String) -> void:
	var sfx_name: String = "spell_%s_cast" % school.to_lower()
	if _sfx_library.has(sfx_name):
		play_sfx_name(sfx_name)
	else:
		push_warning("AudioManager: 法术施放音效未注册 - ", sfx_name)


func play_spell_impact_sfx(school: String) -> void:
	var sfx_name: String = "spell_%s_impact" % school.to_lower()
	if _sfx_library.has(sfx_name):
		play_sfx_name(sfx_name)
	else:
		push_warning("AudioManager: 法术命中音效未注册 - ", sfx_name)


## 按序播放音效序列（用于连击/多段技能）
## names: 音效名称数组，interval: 间隔秒数
func play_sfx_sequence(names: Array[String], interval: float = 0.15, volume_db: float = 0.0) -> void:
	if names.is_empty():
		return

	# 立即播放第一个
	play_sfx_name(names[0], volume_db)

	# 后续音效用 Tween 延迟播放
	if names.size() > 1:
		var tween = create_tween()
		for i in range(1, names.size()):
			tween.tween_callback(play_sfx_name.bind(names[i], volume_db)).set_delay(interval)


# ============================================================================
# Ambient 接口
# ============================================================================

## 播放环境氛围音（循环）
func play_ambient(ambient_name: String, volume_db: float = -6.0) -> void:
	# 已经在播放则跳过
	if _active_ambients.has(ambient_name):
		return

	var stream_path: String = AMBIENT_BASE_PATH + ambient_name + ".ogg"
	var audio_stream: AudioStream = _get_audio_stream(stream_path)
	if not audio_stream:
		push_warning("AudioManager: 环境音文件不存在 - ", stream_path)
		return

	# 设置循环
	if audio_stream is AudioStreamOggVorbis:
		audio_stream.loop = true
	elif audio_stream is AudioStreamMP3:
		audio_stream.loop = true

	var player = _ambient_players[_ambient_index]
	player.stream = audio_stream
	player.volume_db = volume_db
	player.play()

	_active_ambients[ambient_name] = player
	_ambient_index = (_ambient_index + 1) % MAX_AMBIENT_PLAYERS


## 停止指定环境音
func stop_ambient(ambient_name: String, fade_out_time: float = 1.0) -> void:
	if not _active_ambients.has(ambient_name):
		return

	var player: AudioStreamPlayer = _active_ambients[ambient_name]

	if fade_out_time > 0:
		var tween = create_tween()
		tween.tween_property(player, "volume_db", -80.0, fade_out_time).set_trans(Tween.TRANS_SINE)
		tween.tween_callback(func():
			player.stop()
			player.volume_db = 0.0
		)
	else:
		player.stop()
		player.volume_db = 0.0

	_active_ambients.erase(ambient_name)


## 停止所有环境音
func stop_all_ambients(fade_out_time: float = 1.0) -> void:
	for ambient_name in _active_ambients.keys():
		stop_ambient(ambient_name, fade_out_time)


# ============================================================================
# 全局控制
# ============================================================================

## 停止所有音频（BGM + SFX + Ambient）
func stop_all(fade_out_time: float = 1.0) -> void:
	stop_bgm(fade_out_time)
	stop_all_ambients(fade_out_time)
	# SFX 池中的音效无法优雅停止（正在播放中），等待自然结束即可


## 设置总线音量 (0.0 ~ 1.0)
func set_bus_volume(bus_name: String, linear: float) -> void:
	var idx := AudioServer.get_bus_index(bus_name)
	if idx >= 0:
		AudioServer.set_bus_volume_db(idx, linear_to_db(linear))


## 获取总线音量 (0.0 ~ 1.0)
func get_bus_volume(bus_name: String) -> float:
	var idx := AudioServer.get_bus_index(bus_name)
	if idx >= 0:
		return db_to_linear(AudioServer.get_bus_volume_db(idx))
	return 0.0


## 静音/取消静音指定总线
func set_bus_mute(bus_name: String, mute: bool) -> void:
	var idx := AudioServer.get_bus_index(bus_name)
	if idx >= 0:
		AudioServer.set_bus_mute(idx, mute)


# ============================================================================
# 音频加载辅助
# ============================================================================

## 内部辅助：将字符串路径转换为 AudioStream (支持内建 res:// 和外部绝对路径)
func _get_audio_stream(stream) -> AudioStream:
	if stream is AudioStream:
		return stream

	if stream is String:
		if stream.begins_with("res://") or stream.begins_with("uid://"):
			if not ResourceLoader.exists(stream):
				push_warning("AudioManager: 资源文件不存在 - ", stream)
				return null
			return load(stream) as AudioStream
		else:
			return load_external_audio(stream)

	return null


## 动态加载外部音频文件 (支持 .mp3 和 .ogg)
func load_external_audio(path: String) -> AudioStream:
	if not FileAccess.file_exists(path):
		push_warning("AudioManager: 找不到外部音频文件 - ", path)
		return null

	var extension = path.get_extension().to_lower()

	if extension == "ogg":
		return AudioStreamOggVorbis.load_from_file(path)
	elif extension == "mp3":
		var file = FileAccess.open(path, FileAccess.READ)
		if file:
			var mp3_stream = AudioStreamMP3.new()
			mp3_stream.data = file.get_buffer(file.get_length())
			return mp3_stream
	else:
		push_warning("AudioManager: 不支持的外部音频格式，目前仅支持 ogg 和 mp3 - ", extension)

	return null
