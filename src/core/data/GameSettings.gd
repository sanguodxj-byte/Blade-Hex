# GameSettings.gd
# 游戏设置数据模型 — 持久化到 user://settings.dat
#
# 设置分类：
#   视频: 分辨率、全屏、垂直同步、缩放模式
#   音频: 主音量、音乐音量、音效音量、环境音量
#   游戏: 难度、游戏速度、自动保存、战斗动画速度、战斗日志详细度、伤害数字显示
#   控制: 鼠标灵敏度、摄像机边缘滚动、滚动速度
extends RefCounted
class_name GameSettings

## ========================================
## 常量
## ========================================

const SETTINGS_PATH = "user://sword_and_hex_settings.dat"
const CURRENT_VERSION = 1

## ========================================
## 视频设置
## ========================================

enum FullscreenMode {
	WINDOWED,          ## 窗口化
	BORDERLESS,        ## 无边框窗口
	EXCLUSIVE_FS,      ## 独占全屏
}

enum VSyncMode {
	DISABLED,          ## 关闭垂直同步
	ENABLED,           ## 开启垂直同步
	ADAPTIVE,          ## 自适应垂直同步
}

var fullscreen_mode: int = FullscreenMode.WINDOWED
var vsync_mode: int = VSyncMode.ENABLED
var resolution_index: int = 0  ## DisplayServer 支持的分辨率列表的索引

## ========================================
## 音频设置
## ========================================

var master_volume: float = 1.0       ## 主音量 0.0~1.0
var music_volume: float = 0.7        ## 音乐音量 0.0~1.0
var sfx_volume: float = 1.0          ## 音效音量 0.0~1.0
var ambient_volume: float = 0.6      ## 环境音量 0.0~1.0

## ========================================
## 游戏设置
## ========================================

var difficulty: int = 1               ## 0=简单, 1=普通, 2=困难, 3=传奇
var game_speed: float = 2.0           ## 大地图时间流速倍率 (0.5~8.0)
var auto_save: bool = true            ## 自动保存开关
var auto_save_interval: int = 10      ## 自动保存间隔（游戏日）
var combat_anim_speed: float = 1.0    ## 战斗动画速度倍率 (0.5~3.0)
var combat_log_detail: int = 1        ## 0=简洁, 1=标准, 2=详细（含骰子）
var show_damage_numbers: bool = true  ## 显示伤害数字
var show_combat_grid: bool = true     ## 显示战斗网格
var confirm_end_turn: bool = true     ## 结束回合需确认
var show_minimap: bool = true         ## 显示小地图

## ========================================
## 控制设置
## ========================================

var mouse_sensitivity: float = 1.0    ## 鼠标灵敏度 0.1~3.0
var camera_edge_scroll: bool = true   ## 摄像机边缘滚动
var edge_scroll_speed: float = 600.0  ## 边缘滚动速度
var camera_zoom_speed: float = 1.0    ## 缩放速度 0.5~3.0

## ========================================
## 分辨率预设
## ========================================

static func get_resolution_presets() -> Array[Dictionary]:
	return [
		{"label": "1280 x 720",  "w": 1280, "h": 720},
		{"label": "1366 x 768",  "w": 1366, "h": 768},
		{"label": "1600 x 900",  "w": 1600, "h": 900},
		{"label": "1920 x 1080", "w": 1920, "h": 1080},
		{"label": "2560 x 1440", "w": 2560, "h": 1440},
		{"label": "3840 x 2160", "w": 3840, "h": 2160},
	]

static func get_difficulty_names() -> Array[String]:
	return ["简单", "普通", "困难", "传奇"]

static func get_fullscreen_mode_names() -> Array[String]:
	return ["窗口化", "无边框窗口", "独占全屏"]

static func get_vsync_mode_names() -> Array[String]:
	return ["关闭", "开启", "自适应"]

static func get_log_detail_names() -> Array[String]:
	return ["简洁", "标准", "详细"]

## ========================================
## 序列化
## ========================================

func serialize() -> Dictionary:
	return {
		"version": CURRENT_VERSION,
		"video": {
			"fullscreen_mode": fullscreen_mode,
			"vsync_mode": vsync_mode,
			"resolution_index": resolution_index,
		},
		"audio": {
			"master_volume": master_volume,
			"music_volume": music_volume,
			"sfx_volume": sfx_volume,
			"ambient_volume": ambient_volume,
		},
		"game": {
			"difficulty": difficulty,
			"game_speed": game_speed,
			"auto_save": auto_save,
			"auto_save_interval": auto_save_interval,
			"combat_anim_speed": combat_anim_speed,
			"combat_log_detail": combat_log_detail,
			"show_damage_numbers": show_damage_numbers,
			"show_combat_grid": show_combat_grid,
			"confirm_end_turn": confirm_end_turn,
			"show_minimap": show_minimap,
		},
		"control": {
			"mouse_sensitivity": mouse_sensitivity,
			"camera_edge_scroll": camera_edge_scroll,
			"edge_scroll_speed": edge_scroll_speed,
			"camera_zoom_speed": camera_zoom_speed,
		},
	}


## ========================================
## 反序列化
## ========================================

func deserialize(data: Dictionary) -> void:
	var video = data.get("video", {})
	fullscreen_mode = int(video.get("fullscreen_mode", FullscreenMode.WINDOWED))
	vsync_mode = int(video.get("vsync_mode", VSyncMode.ENABLED))
	resolution_index = int(video.get("resolution_index", 0))

	var audio = data.get("audio", {})
	master_volume = float(audio.get("master_volume", 1.0))
	music_volume = float(audio.get("music_volume", 0.7))
	sfx_volume = float(audio.get("sfx_volume", 1.0))
	ambient_volume = float(audio.get("ambient_volume", 0.6))

	var game = data.get("game", {})
	difficulty = int(game.get("difficulty", 1))
	game_speed = float(game.get("game_speed", 2.0))
	auto_save = bool(game.get("auto_save", true))
	auto_save_interval = int(game.get("auto_save_interval", 10))
	combat_anim_speed = float(game.get("combat_anim_speed", 1.0))
	combat_log_detail = int(game.get("combat_log_detail", 1))
	show_damage_numbers = bool(game.get("show_damage_numbers", true))
	show_combat_grid = bool(game.get("show_combat_grid", true))
	confirm_end_turn = bool(game.get("confirm_end_turn", true))
	show_minimap = bool(game.get("show_minimap", true))

	var control = data.get("control", {})
	mouse_sensitivity = float(control.get("mouse_sensitivity", 1.0))
	camera_edge_scroll = bool(control.get("camera_edge_scroll", true))
	edge_scroll_speed = float(control.get("edge_scroll_speed", 600.0))
	camera_zoom_speed = float(control.get("camera_zoom_speed", 1.0))


## ========================================
## 保存/加载
## ========================================

func save_to_file() -> bool:
	var data = serialize()
	var file = FileAccess.open(SETTINGS_PATH, FileAccess.WRITE)
	if file:
		file.store_var(data)
		file.close()
		return true
	push_warning("[GameSettings] 保存设置失败")
	return false


func load_from_file() -> bool:
	if not FileAccess.file_exists(SETTINGS_PATH):
		return false
	var file = FileAccess.open(SETTINGS_PATH, FileAccess.READ)
	if not file:
		return false
	var data = file.get_var()
	file.close()
	if data is Dictionary:
		deserialize(data)
		return true
	return false


## ========================================
## 应用到引擎
## ========================================

func apply_to_engine() -> void:
	# 视频设置
	_apply_video_settings()
	# 音频设置
	_apply_audio_settings()


func _apply_video_settings() -> void:
	# 全屏模式
	match fullscreen_mode:
		FullscreenMode.WINDOWED:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		FullscreenMode.BORDERLESS:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MAXIMIZED)
		FullscreenMode.EXCLUSIVE_FS:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)

	# 垂直同步
	match vsync_mode:
		VSyncMode.DISABLED:
			DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
		VSyncMode.ENABLED:
			DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED)
		VSyncMode.ADAPTIVE:
			DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ADAPTIVE)

	# 分辨率
	var presets = get_resolution_presets()
	if resolution_index >= 0 and resolution_index < presets.size():
		var res = presets[resolution_index]
		if fullscreen_mode == FullscreenMode.WINDOWED:
			DisplayServer.window_set_size(Vector2i(res.w, res.h))


func _apply_audio_settings() -> void:
	# Godot 4 音频总线音量控制
	# 注意：linear_to_db(0) = -inf，需 clamp 下限
	var min_volume := 0.001  # 约 -60dB，听感静音

	# 主总线
	var master_idx = AudioServer.get_bus_index("Master")
	if master_idx >= 0:
		var vol := clampf(master_volume, min_volume, 1.0)
		AudioServer.set_bus_volume_db(master_idx, linear_to_db(vol))
		AudioServer.set_bus_mute(master_idx, master_volume <= 0.001)

	# 音乐总线
	var music_idx = AudioServer.get_bus_index("Music")
	if music_idx >= 0:
		AudioServer.set_bus_volume_db(music_idx, linear_to_db(clampf(music_volume, min_volume, 1.0)))

	# 音效总线
	var sfx_idx = AudioServer.get_bus_index("SFX")
	if sfx_idx >= 0:
		AudioServer.set_bus_volume_db(sfx_idx, linear_to_db(clampf(sfx_volume, min_volume, 1.0)))

	# 环境总线
	var ambient_idx = AudioServer.get_bus_index("Ambient")
	if ambient_idx >= 0:
		AudioServer.set_bus_volume_db(ambient_idx, linear_to_db(clampf(ambient_volume, min_volume, 1.0)))
