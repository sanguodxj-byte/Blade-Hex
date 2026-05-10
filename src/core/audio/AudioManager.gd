# AudioManager.gd
# 全局音频管理组件 (Autoload)
# 负责处理背景音乐(BGM)的无缝切换、淡入淡出，以及音效(SFX)的池化播放
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

# 配置参数
const MAX_SFX_PLAYERS = 10  # 同时最多播放的音效数量

# 内部节点
var _bgm_player1: AudioStreamPlayer
var _bgm_player2: AudioStreamPlayer
var _active_bgm_player: AudioStreamPlayer

var _sfx_players: Array[AudioStreamPlayer] = []
var _sfx_index: int = 0

var _bgm_tween: Tween

# 当前播放的音乐资源路径或实例，用于去重
var _current_bgm: AudioStream = null

# 场景背景音乐库
# 数据结构: { Scenario: { "variant_name": [track_path1, track_path2, ...] } }
var _bgm_playlists: Dictionary = {}

# 音效库
# 数据结构: { "sfx_name": [track_path1, track_path2, ...] }
var _sfx_library: Dictionary = {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS # 暂停时也能播放音乐
	
	# 初始化BGM播放器
	_bgm_player1 = AudioStreamPlayer.new()
	_bgm_player1.bus = "Music"
	add_child(_bgm_player1)
	
	_bgm_player2 = AudioStreamPlayer.new()
	_bgm_player2.bus = "Music"
	add_child(_bgm_player2)
	
	_active_bgm_player = _bgm_player1
	
	# 初始化SFX池
	for i in range(MAX_SFX_PLAYERS):
		var p = AudioStreamPlayer.new()
		p.bus = "SFX"
		add_child(p)
		_sfx_players.append(p)
		
	_init_default_playlists()
	_init_default_sfx()

## ==========================================
## 场景级 BGM 播放接口 (支持变体)
## ==========================================

## 初始化默认的音乐播放列表配置 (子类或外部可覆盖/扩展)
func _init_default_playlists() -> void:
	# 主菜单
	add_bgm_variant(Scenario.MAIN_MENU, "default", "res://src/assets/audio/bgm/main_menu.ogg")
	
	# 大地图旅行
	add_bgm_variant(Scenario.OVERWORLD, "default", "res://src/assets/audio/bgm/overworld_travel.ogg")
	
	# 战斗场景
	add_bgm_variant(Scenario.COMBAT, "normal", "res://src/assets/audio/bgm/normal_battle.ogg")
	add_bgm_variant(Scenario.COMBAT, "boss", "res://src/assets/audio/bgm/boss_battle.ogg")
	
	# 城镇
	add_bgm_variant(Scenario.TOWN, "default", "res://src/assets/audio/bgm/town_theme.ogg")
	
	# 酒馆
	add_bgm_variant(Scenario.TAVERN, "default", "res://src/assets/audio/bgm/tavern_music.ogg")

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

## ==========================================
## 音效名称映射接口
## ==========================================

func _init_default_sfx() -> void:
	# UI 音效
	register_sfx("ui_click", "res://src/assets/audio/sfx/ui_click.wav")
	register_sfx("ui_hover", "res://src/assets/audio/sfx/ui_hover.wav")
	register_sfx("ui_error", "res://src/assets/audio/sfx/ui_error.wav")
	
	# 战斗音效 (支持同一名称注册多个变体，调用时自动随机)
	register_sfx("sword_hit", "res://src/assets/audio/sfx/sword_hit_1.wav")
	register_sfx("sword_hit", "res://src/assets/audio/sfx/sword_hit_2.wav")
	
	register_sfx("footstep", "res://src/assets/audio/sfx/footstep_1.wav")
	register_sfx("footstep", "res://src/assets/audio/sfx/footstep_2.wav")

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

## ==========================================
## 基础播放接口
## ==========================================

## 播放背景音乐，支持自动交叉淡入淡出
## stream: 要播放的音频流 (AudioStream 或 Resource 路径，支持外部绝对路径 .mp3/.ogg)
## crossfade_time: 淡入淡出时间(秒)，0 表示直接切换
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


## 播放音效
## stream: 要播放的音频流或路径
## volume_db: 音量偏移
## pitch_scale: 音高调节 (可以用于随机音高增加变化)
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


## 播放带有随机音高的音效，可避免重复音效显得呆板
func play_sfx_random_pitch(stream, volume_db: float = 0.0, min_pitch: float = 0.9, max_pitch: float = 1.1) -> void:
	play_sfx(stream, volume_db, randf_range(min_pitch, max_pitch))

## ==========================================
## 外部音频加载辅助方法
## ==========================================

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
