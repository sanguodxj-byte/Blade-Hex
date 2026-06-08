// AudioManager.cs
// 全局音频管理组件 (Autoload)
// 负责处理背景音乐(BGM)的无缝切换、淡入淡出，音效(SFX)的池化播放，
// 环境氛围(Ambient)循环播放，以及地形脚步声映射
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.View.AssetSystem;

namespace BladeHex.Audio;

/// <summary>
/// [Autoload Singleton] 全局音频管理器。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>AudioManager</c>。</para>
/// <para>生命周期：应用全局。</para>
/// <para>访问方式：建议通过 <see cref="BladeHex.Data.Globals.Audio"/> 或 <see cref="BladeHex.Data.Globals.AudioOrNull"/>。</para>
/// <para>职责：BGM 交叉淡入淡出、SFX 池化播放、Ambient 循环播放、地形脚步声映射。</para>
/// </summary>
[GlobalClass]
public partial class AudioManager : Node
{
	// ========================================================================
	// Singleton
	// ========================================================================

	public static AudioManager? Instance { get; private set; }

	// ========================================================================
	// 信号
	// ========================================================================

	[Signal] public delegate void BgmTrackFinishedEventHandler(string trackPath);

	// ========================================================================
	// 枚举
	// ========================================================================

	/// <summary>预定义的游戏场景</summary>
	public enum Scenario
	{
		MainMenu,
		Overworld,
		Combat,
		Town,
		Dungeon,
		Event,
		Tavern,
		Victory,
		Defeat
	}

	/// <summary>伤害类型（与 WeaponData.DamageType 对应，用于选择命中音效）</summary>
	public enum DamageType
	{
		Slash,
		Pierce,
		Crush
	}

	// ========================================================================
	// 配置参数
	// ========================================================================

	public const int MaxSfxPlayers = 12;
	public const int MaxAmbientPlayers = 4;
	public const string SfxBasePath = "res://BladeHexFrontend/src/assets/audio/sfx/";
	public const string BgmBasePath = "res://BladeHexFrontend/src/assets/audio/bgm/";
	public const string AmbientBasePath = "res://BladeHexFrontend/src/assets/audio/ambient/";

	// ========================================================================
	// 内部节点
	// ========================================================================

	private AudioStreamPlayer _bgmPlayer1 = null!;
	private AudioStreamPlayer _bgmPlayer2 = null!;
	private AudioStreamPlayer _activeBgmPlayer = null!;

	private readonly List<AudioStreamPlayer> _sfxPlayers = new();
	private int _sfxIndex;

	private readonly List<AudioStreamPlayer> _ambientPlayers = new();
	private int _ambientIndex;

	private Tween? _bgmTween;

	private AudioStream? _currentBgm;
	private string _currentBgmPath = "";

	private readonly Dictionary<string, AudioStreamPlayer> _activeAmbients = new();

	// ========================================================================
	// 音频库
	// ========================================================================

	/// <summary>场景背景音乐库: { Scenario: { variant_name: [track_path, ...] } }</summary>
	private readonly Dictionary<Scenario, Dictionary<string, List<string>>> _bgmPlaylists = new();

	/// <summary>音效库: { sfx_name: [track_path, ...] }</summary>
	private readonly Dictionary<string, List<string>> _sfxLibrary = new();

	/// <summary>组合音效库: { sfx_name: ComboDefinition }</summary>
	private readonly Dictionary<string, SfxCombo> _sfxCombos = new();

	/// <summary>地形脚步声映射: { terrain_key: footstep_sfx_name }</summary>
	private readonly Dictionary<string, string> _footstepMap = new();

	private static readonly Random _rng = new();

	// ========================================================================
	// 生命周期
	// ========================================================================

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		EnsureAudioBuses();

		// BGM 双播放器（交叉淡入淡出）
		_bgmPlayer1 = new AudioStreamPlayer { Bus = "Music" };
		_bgmPlayer1.Finished += () => OnBgmPlayerFinished(_bgmPlayer1);
		AddChild(_bgmPlayer1);

		_bgmPlayer2 = new AudioStreamPlayer { Bus = "Music" };
		_bgmPlayer2.Finished += () => OnBgmPlayerFinished(_bgmPlayer2);
		AddChild(_bgmPlayer2);

		_activeBgmPlayer = _bgmPlayer1;

		// SFX 池
		for (int i = 0; i < MaxSfxPlayers; i++)
		{
			var p = new AudioStreamPlayer { Bus = "SFX" };
			AddChild(p);
			_sfxPlayers.Add(p);
		}

		// Ambient 播放器池
		for (int i = 0; i < MaxAmbientPlayers; i++)
		{
			var p = new AudioStreamPlayer { Bus = "Ambient" };
			AddChild(p);
			_ambientPlayers.Add(p);
		}

		// 应用持久化的音量设置（无存档时使用 GameSettings 默认值 50%）
		ApplyPersistedVolumeSettings();

		// 注册所有音频资源
		InitBgmPlaylists();
		InitUiSfx();
		InitCombatAttackSfx();
		InitCombatSkillSfx();
		InitCombatSpellSfx();
		InitCombatStatusSfx();
		InitCombatFlowSfx();
		InitMovementSfx();
		InitOverworldSfx();
		InitTownSfx();
		InitCharacterSfx();
		InitQuestSfx();
		InitFootstepMap();
		InitSfxCombos();
	}

	// ========================================================================
	// 音频总线
	// ========================================================================

	private static void EnsureAudioBuses()
	{
		string[] neededBuses = { "Music", "SFX", "Ambient" };
		foreach (string busName in neededBuses)
		{
			if (AudioServer.GetBusIndex(busName) < 0)
			{
				AudioServer.AddBus();
				int idx = AudioServer.BusCount - 1;
				AudioServer.SetBusName(idx, busName);
				AudioServer.SetBusSend(idx, "Master");
			}
		}
	}

	/// <summary>
	/// 启动时应用一次玩家音量设置；无存档时落到 GameSettings 默认（50%）。
	/// 这样首次启动也不会以 0dB 全开音量轰击玩家。
	/// 仅应用音频部分，视频设置由场景/启动流程独立处理。
	/// </summary>
	private static void ApplyPersistedVolumeSettings()
	{
		var settings = new BladeHex.Data.GameSettings();
		settings.LoadFromFile(); // 不存在时保留默认值
		const float minVolume = 0.001f;

		int masterIdx = AudioServer.GetBusIndex("Master");
		if (masterIdx >= 0)
		{
			float vol = Mathf.Clamp(settings.MasterVolume, minVolume, 1.0f);
			AudioServer.SetBusVolumeDb(masterIdx, Mathf.LinearToDb(vol));
			AudioServer.SetBusMute(masterIdx, settings.MasterVolume <= 0.001f);
		}
		int musicIdx = AudioServer.GetBusIndex("Music");
		if (musicIdx >= 0)
			AudioServer.SetBusVolumeDb(musicIdx, Mathf.LinearToDb(Mathf.Clamp(settings.MusicVolume, minVolume, 1.0f)));
		int sfxIdx = AudioServer.GetBusIndex("SFX");
		if (sfxIdx >= 0)
			AudioServer.SetBusVolumeDb(sfxIdx, Mathf.LinearToDb(Mathf.Clamp(settings.SfxVolume, minVolume, 1.0f)));
		int ambientIdx = AudioServer.GetBusIndex("Ambient");
		if (ambientIdx >= 0)
			AudioServer.SetBusVolumeDb(ambientIdx, Mathf.LinearToDb(Mathf.Clamp(settings.AmbientVolume, minVolume, 1.0f)));
	}

	// ========================================================================
	// BGM 注册
	// ========================================================================

	private void InitBgmPlaylists()
	{
		// 主菜单
		AddBgmVariant(Scenario.MainMenu, "default", BgmBasePath + "main_menu.ogg");

		// 大地图旅行（昼）— overworld_travel 实际是舒缓旅行曲，不适合做默认
		AddBgmVariant(Scenario.Overworld, "default", BgmBasePath + "overworld_night.ogg");

		// 大地图夜晚
		AddBgmVariant(Scenario.Overworld, "night", BgmBasePath + "overworld_night.ogg");

		// 大地图下雨（overworld_travel 实际是下雨氛围曲）
		AddBgmVariant(Scenario.Overworld, "rain", BgmBasePath + "overworld_rain.ogg");
		AddBgmVariant(Scenario.Overworld, "rain", BgmBasePath + "overworld_rain_2.ogg");

		// 大地图危险/雷暴
		AddBgmVariant(Scenario.Overworld, "danger", BgmBasePath + "overworld_storm.ogg");

		// 战斗场景 — 普通战斗（2首变体随机）
		AddBgmVariant(Scenario.Combat, "normal", BgmBasePath + "normal_battle.ogg");
		AddBgmVariant(Scenario.Combat, "normal", BgmBasePath + "normal_battle_2.ogg");
		// Boss战斗（领主/传奇生物，3首变体随机）
		AddBgmVariant(Scenario.Combat, "boss", BgmBasePath + "boss_battle.ogg");
		AddBgmVariant(Scenario.Combat, "boss", BgmBasePath + "boss_battle_2.ogg");
		AddBgmVariant(Scenario.Combat, "boss", BgmBasePath + "boss_battle_3.ogg");
		// 雨中战斗
		AddBgmVariant(Scenario.Combat, "rain", BgmBasePath + "rain_battle.ogg");
		// 伏击复用普通战斗
		AddBgmVariant(Scenario.Combat, "ambush", BgmBasePath + "normal_battle.ogg");
		AddBgmVariant(Scenario.Combat, "ambush", BgmBasePath + "normal_battle_2.ogg");

		// 城镇
		AddBgmVariant(Scenario.Town, "default", BgmBasePath + "overworld_travel.ogg");
		AddBgmVariant(Scenario.Town, "capital", BgmBasePath + "overworld_travel.ogg");

		// 地牢 — 接近遗迹音乐（暗黑氛围，prox_ruins_3已移作boss战斗）
		AddBgmVariant(Scenario.Dungeon, "default", BgmBasePath + "prox_ruins.ogg");
		AddBgmVariant(Scenario.Dungeon, "default", BgmBasePath + "prox_ruins_2.ogg");

		// 剧情事件 — event_dramatic 已移作普通战斗，事件用calm
		AddBgmVariant(Scenario.Event, "default", BgmBasePath + "event_calm.ogg");
		AddBgmVariant(Scenario.Event, "calm", BgmBasePath + "event_calm.ogg");

		// 酒馆
		AddBgmVariant(Scenario.Tavern, "default", BgmBasePath + "overworld_travel.ogg");

		// 胜利 — 资源缺失，复用普通战斗
		AddBgmVariant(Scenario.Victory, "default", BgmBasePath + "normal_battle.ogg");

		// 失败 — 2个变体
		AddBgmVariant(Scenario.Defeat, "default", BgmBasePath + "defeat_somber.ogg");
		AddBgmVariant(Scenario.Defeat, "default", BgmBasePath + "defeat_somber_2.ogg");

		// 种族专属 BGM (5种)
		AddBgmVariant(Scenario.Overworld, "race_human", BgmBasePath + "race_human.ogg");
		AddBgmVariant(Scenario.Overworld, "race_elf", BgmBasePath + "race_elf.ogg");
		AddBgmVariant(Scenario.Overworld, "race_dwarf", BgmBasePath + "race_dwarf.ogg");
		AddBgmVariant(Scenario.Overworld, "race_halforc", BgmBasePath + "race_halforc.ogg");
		AddBgmVariant(Scenario.Overworld, "race_halfelf", BgmBasePath + "race_halfelf.ogg");
	}

	/// <summary>
	/// 播放种族专属 BGM (用于出身选择/领土进入/大地图随机触发)
	/// </summary>
	/// <param name="raceId">种族ID: human/elf/dwarf/halforc/halfelf</param>
	/// <param name="crossfadeTime">淡入时长</param>
	public void PlayRaceBgm(string raceId, float crossfadeTime = 1.5f)
	{
		string variant = $"race_{raceId.ToLower()}";
		PlayScenarioBgm(Scenario.Overworld, variant, crossfadeTime);
	}

	// ========================================================================
	// SFX 注册 — 按模块拆分
	// ========================================================================

	private void RegisterSfxVariants(string name, params string[] paths)
	{
		foreach (string path in paths)
			RegisterSfx(name, path);
	}

	private void InitUiSfx()
	{
		string uiBase = SfxBasePath + "ui/";
		RegisterSfx("ui_click", uiBase + "ui_click.mp3");
		RegisterSfx("ui_hover", uiBase + "ui_click.mp3");
		RegisterSfx("ui_error", uiBase + "ui_error.mp3");
		RegisterSfx("ui_panel_open", uiBase + "ui_panel_open.mp3");
		RegisterSfx("ui_panel_close", uiBase + "ui_panel_close.mp3");
		RegisterSfx("ui_tab_switch", uiBase + "ui_tab_switch.mp3");
		RegisterSfx("ui_checkbox", uiBase + "ui_checkbox.mp3");
		RegisterSfx("ui_gold_change", uiBase + "ui_gold_change.mp3");
		RegisterSfx("ui_notification", uiBase + "ui_notification.mp3");
		RegisterSfx("ui_quest_accept", uiBase + "ui_quest_accept.mp3");
		RegisterSfx("ui_save", uiBase + "ui_save.mp3");
		RegisterSfx("ui_load", uiBase + "ui_load.mp3");
		// ui_level_up, ui_quest_complete — 无独立审核音效，不注册
	}

	private void InitCombatAttackSfx()
	{
		// 砍伤 (SLASH) — 4个变体
		RegisterSfxVariants("combat_sword_hit",
			SfxBasePath + "combat/attack/sword_hit_1.mp3",
			SfxBasePath + "combat/attack/sword_hit_2.mp3",
			SfxBasePath + "combat/attack/sword_hit_3.mp3",
			SfxBasePath + "combat/attack/sword_hit_4.mp3");
		RegisterSfxVariants("combat_sword_miss",
			SfxBasePath + "combat/attack/sword_miss_1.wav",
			SfxBasePath + "combat/attack/sword_miss_2.mp3",
			SfxBasePath + "combat/attack/sword_miss_3.mp3",
			SfxBasePath + "combat/attack/sword_miss_4.mp3");
		RegisterSfxVariants("combat_sword_crit",
			SfxBasePath + "combat/attack/sword_crit_1.mp3",
			SfxBasePath + "combat/attack/sword_crit_2.mp3");

		// 刺伤 (PIERCE)
		RegisterSfxVariants("combat_pierce_hit",
			SfxBasePath + "combat/attack/pierce_hit_1.mp3",
			SfxBasePath + "combat/attack/pierce_hit_2.mp3");
		RegisterSfxVariants("combat_pierce_crit",
			SfxBasePath + "combat/attack/pierce_hit_1.mp3",
			SfxBasePath + "combat/attack/pierce_hit_2.mp3");
		RegisterSfxVariants("combat_pierce_miss",
			SfxBasePath + "combat/attack/pierce_miss_1.mp3",
			SfxBasePath + "combat/attack/pierce_miss_2.mp3");

		// 钝伤 (CRUSH)
		RegisterSfxVariants("combat_crush_hit",
			SfxBasePath + "combat/attack/crush_hit_1.mp3",
			SfxBasePath + "combat/attack/crush_hit_2.mp3");
		RegisterSfxVariants("combat_crush_crit",
			SfxBasePath + "combat/attack/crush_hit_1.mp3",
			SfxBasePath + "combat/attack/crush_hit_2.mp3");
		RegisterSfxVariants("combat_crush_miss",
			SfxBasePath + "combat/attack/crush_miss_1.mp3",
			SfxBasePath + "combat/attack/crush_miss_2.mp3");

		// 盾牌格挡
		RegisterSfxVariants("combat_shield_block",
			SfxBasePath + "combat/attack/shield_block_1.mp3",
			SfxBasePath + "combat/attack/shield_block_2.mp3");

		// 闪避
		RegisterSfxVariants("combat_dodge",
			SfxBasePath + "combat/attack/dodge_1.mp3",
			SfxBasePath + "combat/attack/dodge_2.mp3");

		// 弓箭 — 5个射击变体 + 4个命中变体
		RegisterSfxVariants("combat_arrow_fire",
			SfxBasePath + "combat/attack/arrow_fire_1.mp3",
			SfxBasePath + "combat/attack/arrow_fire_2.mp3",
			SfxBasePath + "combat/attack/arrow_fire_3.mp3",
			SfxBasePath + "combat/attack/arrow_fire_4.mp3",
			SfxBasePath + "combat/attack/arrow_fire_5.mp3");
		RegisterSfxVariants("combat_arrow_hit",
			SfxBasePath + "combat/attack/arrow_hit_1.mp3",
			SfxBasePath + "combat/attack/arrow_hit_2.mp3",
			SfxBasePath + "combat/attack/arrow_hit_3.mp3",
			SfxBasePath + "combat/attack/arrow_hit_4.mp3");
		RegisterSfx("combat_arrow_miss", SfxBasePath + "combat/attack/arrow_miss.mp3");

		// 通用
		RegisterSfxVariants("combat_graze",
			SfxBasePath + "combat/attack/graze_1.mp3",
			SfxBasePath + "combat/attack/graze_2.mp3");
		RegisterSfx("combat_death_save", SfxBasePath + "combat/attack/death_save.mp3");
		RegisterSfxVariants("combat_death",
			SfxBasePath + "combat/attack/death_1.mp3",
			SfxBasePath + "combat/attack/death_2.mp3",
			SfxBasePath + "combat/attack/death_3.mp3");
		RegisterSfxVariants("combat_armor_hit",
			SfxBasePath + "combat/attack/armor_hit_1.mp3",
			SfxBasePath + "combat/attack/armor_hit_2.mp3");
		RegisterSfxVariants("combat_armor_break",
			SfxBasePath + "combat/attack/armor_break.mp3",
			SfxBasePath + "combat/attack/armor_break_2.mp3");
	}

	private void InitCombatSkillSfx()
	{
		// 近战系 — 3个连击变体
		RegisterSfxVariants("skill_melee_combo",
			SfxBasePath + "combat/skill/melee_combo_1.mp3",
			SfxBasePath + "combat/skill/melee_combo_2.mp3",
			SfxBasePath + "combat/skill/melee_combo_3.mp3");
		RegisterSfx("skill_whirlwind", SfxBasePath + "combat/skill/whirlwind.mp3");
		RegisterSfxVariants("skill_shield_bash",
			SfxBasePath + "combat/skill/shield_bash_1.mp3",
			SfxBasePath + "combat/skill/shield_bash_2.mp3");
		RegisterSfx("skill_blood_vortex", SfxBasePath + "combat/skill/blood_vortex.mp3");
		RegisterSfx("skill_poison_blade", SfxBasePath + "combat/skill/poison_blade.mp3");

		// 远程系 — double_shot/trick_arrow 各2个变体
		RegisterSfxVariants("skill_double_shot",
			SfxBasePath + "combat/skill/double_shot.mp3",
			SfxBasePath + "combat/skill/double_shot_2.mp3");
		RegisterSfxVariants("skill_trick_arrow",
			SfxBasePath + "combat/skill/trick_arrow.mp3",
			SfxBasePath + "combat/skill/trick_arrow_2.mp3");

		// 魔法系
		RegisterSfx("skill_time_warp", SfxBasePath + "combat/skill/time_warp.mp3");
		RegisterSfx("skill_arcane_judgment", SfxBasePath + "combat/skill/holy_judgment.mp3");
		RegisterSfx("skill_nature_wrath", SfxBasePath + "combat/skill/nature_wrath.mp3");

		// 治疗系
		RegisterSfxVariants("skill_heal",
			SfxBasePath + "combat/skill/heal_1.mp3",
			SfxBasePath + "combat/skill/heal_2.mp3");
		RegisterSfx("skill_mass_heal", SfxBasePath + "combat/skill/mass_heal.mp3");
		RegisterSfx("skill_blessing", SfxBasePath + "combat/skill/blessing.mp3");
		// skill_mana_shield, skill_arcane_shield — 无独立审核音效，不注册

		// 辅助系
		RegisterSfxVariants("skill_war_cry",
			SfxBasePath + "combat/skill/war_cry_1.mp3",
			SfxBasePath + "combat/skill/war_cry_2.mp3");
		RegisterSfx("skill_stealth", SfxBasePath + "combat/skill/stealth.mp3");
	}

	private void InitCombatSpellSfx()
	{
		string spellBase = SfxBasePath + "combat/spell/";
		string[] schools = { "fire", "ice", "lightning", "earth", "holy", "shadow", "arcane", "nature" };
		foreach (string school in schools)
		{
			RegisterSfx($"spell_{school}_cast", spellBase + $"{school}_cast.mp3");
			RegisterSfx($"spell_{school}_impact", spellBase + $"{school}_impact.mp3");
		}

		// spell_no_mana, spell_cooldown — 无独立审核音效，不注册
	}

	private void InitCombatStatusSfx()
	{
		RegisterSfx("status_burning", SfxBasePath + "combat/status/burning.mp3");
		RegisterSfx("status_freezing", SfxBasePath + "combat/status/freezing.mp3");
		RegisterSfx("status_poison", SfxBasePath + "combat/status/poison.mp3");
		RegisterSfx("status_bleed", SfxBasePath + "combat/status/bleed.mp3");
		RegisterSfx("status_stun", SfxBasePath + "combat/status/stun.mp3");
		RegisterSfx("status_root", SfxBasePath + "combat/status/root.mp3");
		// status_cure, status_rally, status_rout — 无独立审核音效，不注册
	}

	private void InitCombatFlowSfx()
	{
		RegisterSfx("combat_turn_start", SfxBasePath + "combat/flow/turn_start.mp3");
		RegisterSfx("combat_enemy_turn", SfxBasePath + "combat/flow/enemy_turn.mp3");
		RegisterSfx("combat_victory", SfxBasePath + "combat/flow/victory.mp3");
		RegisterSfx("combat_defeat", SfxBasePath + "combat/flow/defeat.mp3");

		// 战斗事件 — 无独立审核音效，不注册
		// combat_counter, combat_aoo, combat_flanking, combat_charge, combat_mount_charge

		// 环境事件
		RegisterSfx("combat_env_storm", SfxBasePath + "combat/env/storm.mp3");
		RegisterSfx("combat_env_fog", SfxBasePath + "combat/env/fog.mp3");
		RegisterSfx("combat_env_quake", SfxBasePath + "combat/env/quake.mp3");
		RegisterSfx("combat_env_poison_fog", SfxBasePath + "combat/env/poison_fog.mp3");
		RegisterSfx("combat_env_lava", SfxBasePath + "combat/env/lava.mp3");
	}

	private void InitMovementSfx()
	{
		string mvBase = SfxBasePath + "movement/";
		RegisterSfx("move_footstep_grass", mvBase + "footstep_grass.mp3");
		RegisterSfx("move_footstep_stone", mvBase + "footstep_stone.mp3");
		RegisterSfx("move_footstep_snow", mvBase + "footstep_snow.mp3");
		RegisterSfx("move_footstep_mud", mvBase + "footstep_mud.mp3");
		RegisterSfx("move_footstep_wood", mvBase + "footstep_wood.mp3");
		RegisterSfx("move_footstep_water", mvBase + "footstep_water.mp3");
		RegisterSfx("move_footstep_sand", mvBase + "footstep_sand.mp3"); // combine 审核通过

		// 单位选择与路径
		RegisterSfx("move_unit_select", mvBase + "unit_select.mp3");
		RegisterSfx("move_unit_deselect", mvBase + "unit_deselect.mp3");
		RegisterSfx("move_path_confirm", mvBase + "path_confirm.mp3");
		RegisterSfx("move_weapon_switch", mvBase + "weapon_switch.mp3");
	}

	private void InitOverworldSfx()
	{
		string owBase = SfxBasePath + "overworld/";
		RegisterSfx("ow_travel_start", owBase + "travel_start.mp3");
		RegisterSfx("ow_travel_stop", owBase + "travel_stop.mp3");
		RegisterSfx("ow_fog_reveal", owBase + "fog_reveal.mp3");
		RegisterSfx("ow_town_leave", owBase + "town_leave.mp3");
		RegisterSfx("ow_day_cycle", owBase + "day_cycle.mp3");
		RegisterSfx("ow_poi_discover", owBase + "poi_discover.mp3");
		// ow_encounter, ow_combat_trigger, ow_enemy_sighted, ow_town_enter — 无独立审核音效，不注册
	}

	private void InitTownSfx()
	{
		string townBase = SfxBasePath + "town/";
		RegisterSfx("town_trade_buy", townBase + "trade_buy.mp3");
		RegisterSfx("town_trade_sell", townBase + "trade_sell.mp3");
		RegisterSfx("town_trade_fail", townBase + "trade_fail.mp3");
		RegisterSfx("town_rest_inn", townBase + "rest_inn.mp3");
		RegisterSfx("town_train", townBase + "train.mp3");
		RegisterSfx("town_repair", townBase + "repair.mp3");
		RegisterSfx("town_recruit", townBase + "recruit.mp3");
		// town_temple_heal, town_smithy_upgrade, town_arena_fight, town_arena_win — 无独立审核音效，不注册
	}

	private void InitCharacterSfx()
	{
		string charBase = SfxBasePath + "character/";
		RegisterSfx("char_equip_change", charBase + "equip_change.mp3");
		RegisterSfx("char_equip_fail", charBase + "equip_fail.mp3");
		RegisterSfx("char_stat_increase", charBase + "stat_increase.mp3");
		// char_node_activate, char_spell_learn — 无独立审核音效，不注册
	}

	private void InitQuestSfx()
	{
		string questBase = SfxBasePath + "quest/";
		RegisterSfx("quest_new", questBase + "quest_new.mp3");
		RegisterSfx("quest_progress", questBase + "quest_progress.mp3");
		RegisterSfx("quest_fail", questBase + "quest_fail.mp3");
		RegisterSfx("quest_expire", questBase + "quest_expire.mp3");
		// quest_accept, quest_complete — 无独立审核音效，不注册
	}

	private void InitFootstepMap()
	{
		// BattleCellData.TerrainType -> 脚步声 SFX 名称
		_footstepMap["plains"] = "move_footstep_grass";
		_footstepMap["grassland"] = "move_footstep_grass";
		_footstepMap["savanna"] = "move_footstep_grass";
		_footstepMap["forest"] = "move_footstep_grass";
		_footstepMap["dense_forest"] = "move_footstep_grass";
		_footstepMap["hills"] = "move_footstep_stone";
		_footstepMap["mountain"] = "move_footstep_stone";
		_footstepMap["shallow_water"] = "move_footstep_water";
		_footstepMap["deep_water"] = "move_footstep_water";
		_footstepMap["swamp"] = "move_footstep_mud";
		_footstepMap["road"] = "move_footstep_stone";
		_footstepMap["sand"] = "move_footstep_sand";
		_footstepMap["snow"] = "move_footstep_snow";
		_footstepMap["wall"] = "move_footstep_stone";
		_footstepMap["ruins"] = "move_footstep_stone";
		_footstepMap["poison_mushroom"] = "move_footstep_grass";
		_footstepMap["lucky_grass"] = "move_footstep_grass";
		// OverworldTerrain.Type -> 脚步声（大地图用）
		_footstepMap["ow_plains"] = "move_footstep_grass";
		_footstepMap["ow_forest"] = "move_footstep_grass";
		_footstepMap["ow_mountain"] = "move_footstep_stone";
		_footstepMap["ow_swamp"] = "move_footstep_mud";
		_footstepMap["ow_water"] = "move_footstep_water";
		_footstepMap["ow_road"] = "move_footstep_stone";
		_footstepMap["ow_desert"] = "move_footstep_sand";
	}

	/// <summary>
	/// 注册组合音效 — 将复杂音效拆分为多个原子片段按时间序列播放。
	/// 原子片段在各 Init*Sfx 方法中已注册为普通 SFX。
	/// </summary>
	private void InitSfxCombos()
	{
		// 仅注册通过 Gemini 审核的组合音效

		// move_footstep_sand: 两步沙地脚步 — combine 审核通过 (score=8)
		// 已作为独立文件存在，无需 combo

		// combat_env_poison_fog: 液体冒泡 + 嘶嘶气体 — combine 审核通过 (score=8)
		// 已作为独立文件存在，无需 combo
	}

	// ========================================================================
	// BGM 接口
	// ========================================================================

	/// <summary>为特定场景和变体注册一首曲目</summary>
	public void AddBgmVariant(Scenario scenario, string variant, string streamPath)
	{
		if (!_bgmPlaylists.ContainsKey(scenario))
			_bgmPlaylists[scenario] = new Dictionary<string, List<string>>();

		if (!_bgmPlaylists[scenario].ContainsKey(variant))
			_bgmPlaylists[scenario][variant] = new List<string>();

		var tracks = _bgmPlaylists[scenario][variant];
		if (!tracks.Contains(streamPath))
			tracks.Add(streamPath);
	}

	/// <summary>按场景和变体播放音乐 (自动从该变体中随机选取一首)</summary>
	public void PlayScenarioBgm(Scenario scenario, string variant = "default", float crossfadeTime = 1.5f)
	{
		if (!_bgmPlaylists.ContainsKey(scenario))
		{
			GD.PushWarning($"AudioManager: 场景 {scenario} 未注册任何音乐。");
			return;
		}

		var variants = _bgmPlaylists[scenario];
		List<string>? tracksToPlay = null;

		if (variants.TryGetValue(variant, out var vTracks) && vTracks.Count > 0)
		{
			tracksToPlay = vTracks;
		}
		else if (variants.TryGetValue("default", out var defTracks) && defTracks.Count > 0)
		{
			GD.PushWarning($"AudioManager: 找不到变体 '{variant}'，回退到 'default'。");
			tracksToPlay = defTracks;
		}
		else
		{
			GD.PushWarning($"AudioManager: 场景 {scenario} 中找不到变体 '{variant}' 或 'default'。");
			return;
		}

		string trackPath = tracksToPlay[_rng.Next(tracksToPlay.Count)];
		PlayBgm(trackPath, crossfadeTime);
	}

	/// <summary>
	/// 按场景和变体播放音乐 (int 重载，供 EnvironmentAudioComponent 使用)
	/// </summary>
	public void PlayScenarioBgm(int scenarioInt, string variant = "default", float crossfadeTime = 1.5f)
	{
		PlayScenarioBgm((Scenario)scenarioInt, variant, crossfadeTime);
	}

	private void OnBgmPlayerFinished(AudioStreamPlayer player)
	{
		if (player == _activeBgmPlayer)
			EmitSignal(SignalName.BgmTrackFinished, _currentBgmPath);
	}

	/// <summary>播放背景音乐，支持自动交叉淡入淡出</summary>
	public void PlayBgm(string streamPath, float crossfadeTime = 1.5f)
	{
		var audioStream = GetAudioStream(streamPath);
		if (audioStream == null)
		{
			GD.PushWarning($"AudioManager: 无法播放BGM，无效的流或路径 - {streamPath}");
			return;
		}

		// 避免重复播放相同的音乐
		if (_currentBgm == audioStream && _activeBgmPlayer.Playing)
			return;

		_currentBgm = audioStream;
		_currentBgmPath = streamPath;

		var fadingOutPlayer = _activeBgmPlayer;
		var fadingInPlayer = _activeBgmPlayer == _bgmPlayer1 ? _bgmPlayer2 : _bgmPlayer1;

		_activeBgmPlayer = fadingInPlayer;
		_activeBgmPlayer.Stream = audioStream;

		if (_bgmTween != null && _bgmTween.IsValid())
			_bgmTween.Kill();

		if (crossfadeTime > 0)
		{
			_activeBgmPlayer.VolumeDb = -80.0f;
			_activeBgmPlayer.Play();
			_bgmTween = CreateTween();
			_bgmTween.TweenProperty(_activeBgmPlayer, "volume_db", 0.0f, crossfadeTime)
				.SetTrans(Tween.TransitionType.Sine);
			if (fadingOutPlayer.Playing)
			{
				_bgmTween.Parallel().TweenProperty(fadingOutPlayer, "volume_db", -80.0f, crossfadeTime)
					.SetTrans(Tween.TransitionType.Sine);
			}
			var capturedFadingOut = fadingOutPlayer;
			_bgmTween.TweenCallback(Callable.From(() =>
			{
				if (capturedFadingOut != _activeBgmPlayer && capturedFadingOut.Playing)
				{
					capturedFadingOut.Stop();
					capturedFadingOut.VolumeDb = 0.0f;
				}
			}));
		}
		else
		{
			if (fadingOutPlayer.Playing)
			{
				fadingOutPlayer.Stop();
				fadingOutPlayer.VolumeDb = 0.0f;
			}
			_activeBgmPlayer.VolumeDb = 0.0f;
			_activeBgmPlayer.Play();
		}
	}

	/// <summary>播放背景音乐（AudioStream 重载）</summary>
	public void PlayBgm(AudioStream stream, float crossfadeTime = 1.5f)
	{
		if (stream == null)
		{
			GD.PushWarning("AudioManager: 无法播放BGM，无效的流。");
			return;
		}

		if (_currentBgm == stream && _activeBgmPlayer.Playing)
			return;

		_currentBgm = stream;
		_currentBgmPath = stream.ResourcePath ?? "";

		var fadingOutPlayer = _activeBgmPlayer;
		var fadingInPlayer = _activeBgmPlayer == _bgmPlayer1 ? _bgmPlayer2 : _bgmPlayer1;

		_activeBgmPlayer = fadingInPlayer;
		_activeBgmPlayer.Stream = stream;

		if (_bgmTween != null && _bgmTween.IsValid())
			_bgmTween.Kill();

		if (crossfadeTime > 0)
		{
			_activeBgmPlayer.VolumeDb = -80.0f;
			_activeBgmPlayer.Play();
			_bgmTween = CreateTween();
			_bgmTween.TweenProperty(_activeBgmPlayer, "volume_db", 0.0f, crossfadeTime)
				.SetTrans(Tween.TransitionType.Sine);
			if (fadingOutPlayer.Playing)
			{
				_bgmTween.Parallel().TweenProperty(fadingOutPlayer, "volume_db", -80.0f, crossfadeTime)
					.SetTrans(Tween.TransitionType.Sine);
			}
			var capturedFadingOut = fadingOutPlayer;
			_bgmTween.TweenCallback(Callable.From(() =>
			{
				if (capturedFadingOut != _activeBgmPlayer && capturedFadingOut.Playing)
				{
					capturedFadingOut.Stop();
					capturedFadingOut.VolumeDb = 0.0f;
				}
			}));
		}
		else
		{
			if (fadingOutPlayer.Playing)
			{
				fadingOutPlayer.Stop();
				fadingOutPlayer.VolumeDb = 0.0f;
			}
			_activeBgmPlayer.VolumeDb = 0.0f;
			_activeBgmPlayer.Play();
		}
	}

	/// <summary>停止背景音乐</summary>
	public void StopBgm(float fadeOutTime = 1.0f)
	{
		_currentBgm = null;
		_currentBgmPath = "";

		if (_bgmTween != null && _bgmTween.IsValid())
			_bgmTween.Kill();

		if (fadeOutTime > 0)
		{
			_bgmTween = CreateTween();
			_bgmTween.TweenProperty(_activeBgmPlayer, "volume_db", -80.0f, fadeOutTime)
				.SetTrans(Tween.TransitionType.Sine);
			_bgmTween.Finished += () =>
			{
				_activeBgmPlayer.Stop();
				_activeBgmPlayer.VolumeDb = 0.0f;
			};
		}
		else
		{
			_activeBgmPlayer.Stop();
			_activeBgmPlayer.VolumeDb = 0.0f;
		}
	}

	// ========================================================================
	// SFX 通用接口
	// ========================================================================

	/// <summary>注册一个命名音效。同一名称多次注册会形成列表，播放时随机抽取。</summary>
	public void RegisterSfx(string sfxName, string streamPath)
	{
		if (!_sfxLibrary.ContainsKey(sfxName))
			_sfxLibrary[sfxName] = new List<string>();

		var tracks = _sfxLibrary[sfxName];
		if (!tracks.Contains(streamPath))
			tracks.Add(streamPath);
	}

	/// <summary>按名称播放预先注册的音效（自动检测是否为组合音效）</summary>
	public void PlaySfxName(string sfxName, float volumeDb = 0.0f, float pitchScale = 1.0f)
	{
		// 优先检查是否为组合音效
		if (_sfxCombos.ContainsKey(sfxName))
		{
			PlaySfxCombo(sfxName, volumeDb);
			return;
		}

		if (!_sfxLibrary.TryGetValue(sfxName, out var tracks) || tracks.Count == 0)
		{
			GD.PushWarning($"AudioManager: 找不到注册的音效名称 - {sfxName}");
			return;
		}

		string trackPath = tracks[_rng.Next(tracks.Count)];
		PlaySfx(trackPath, volumeDb, pitchScale);
	}

	/// <summary>按名称播放音效，并自带随机音高</summary>
	public void PlaySfxNameRandomPitch(string sfxName, float volumeDb = 0.0f, float minPitch = 0.9f, float maxPitch = 1.1f)
	{
		float pitch = (float)(_rng.NextDouble() * (maxPitch - minPitch) + minPitch);
		PlaySfxName(sfxName, volumeDb, pitch);
	}

	/// <summary>播放音效（基础接口，路径版）</summary>
	public void PlaySfx(string streamPath, float volumeDb = 0.0f, float pitchScale = 1.0f)
	{
		var audioStream = GetAudioStream(streamPath);
		if (audioStream == null) return;
		PlaySfx(audioStream, volumeDb, pitchScale);
	}

	/// <summary>播放音效（基础接口，AudioStream 版）</summary>
	public void PlaySfx(AudioStream stream, float volumeDb = 0.0f, float pitchScale = 1.0f)
	{
		if (stream == null) return;

		// 优先使用空闲的播放器
		AudioStreamPlayer? player = null;
		for (int i = 0; i < MaxSfxPlayers; i++)
		{
			var candidate = _sfxPlayers[(_sfxIndex + i) % MaxSfxPlayers];
			if (!candidate.Playing)
			{
				player = candidate;
				_sfxIndex = (_sfxIndex + i + 1) % MaxSfxPlayers;
				break;
			}
		}

		if (player == null)
		{
			player = _sfxPlayers[_sfxIndex];
			_sfxIndex = (_sfxIndex + 1) % MaxSfxPlayers;
		}

		player.Stream = stream;
		player.VolumeDb = volumeDb;
		player.PitchScale = pitchScale;
		player.Play();
	}

	/// <summary>播放带有随机音高的音效</summary>
	public void PlaySfxRandomPitch(string streamPath, float volumeDb = 0.0f, float minPitch = 0.9f, float maxPitch = 1.1f)
	{
		float pitch = (float)(_rng.NextDouble() * (maxPitch - minPitch) + minPitch);
		PlaySfx(streamPath, volumeDb, pitch);
	}

	// ========================================================================
	// SFX 高级接口
	// ========================================================================

	/// <summary>按伤害类型播放攻击命中音效</summary>
	public void PlayAttackHitSfx(DamageType dmgType, bool isCrit = false)
	{
		PlayAttackHitSfx((int)dmgType, isCrit);
	}

	/// <summary>按伤害类型播放攻击命中音效 (int 重载)</summary>
	public void PlayAttackHitSfx(int dmgType, bool isCrit = false)
	{
		if (isCrit)
		{
			switch ((DamageType)dmgType)
			{
				case DamageType.Slash: PlaySfxName("combat_sword_crit"); return;
				case DamageType.Pierce: PlaySfxName("combat_pierce_crit"); return;
				case DamageType.Crush: PlaySfxName("combat_crush_crit"); return;
			}
			return;
		}

		switch ((DamageType)dmgType)
		{
			case DamageType.Slash: PlaySfxName("combat_sword_hit"); break;
			case DamageType.Pierce: PlaySfxName("combat_pierce_hit"); break;
			case DamageType.Crush: PlaySfxName("combat_crush_hit"); break;
		}
	}

	/// <summary>按伤害类型播放攻击未中音效</summary>
	public void PlayAttackMissSfx(DamageType dmgType)
	{
		PlayAttackMissSfx((int)dmgType);
	}

	/// <summary>按伤害类型播放攻击未中音效 (int 重载)</summary>
	public void PlayAttackMissSfx(int dmgType)
	{
		switch ((DamageType)dmgType)
		{
			case DamageType.Slash:
				PlaySfxName("combat_sword_miss");
				break;
			case DamageType.Pierce:
				PlaySfxName("combat_pierce_miss");
				break;
			case DamageType.Crush:
				PlaySfxName("combat_crush_miss");
				break;
		}
	}

	/// <summary>按地形类型播放脚步声（战斗地图）</summary>
	public void PlayFootstepByTerrain(string terrainName)
	{
		string key = terrainName.ToLower();
		string sfxName = _footstepMap.TryGetValue(key, out var name) ? name : "move_footstep_grass";
		PlaySfxNameRandomPitch(sfxName);
	}

	/// <summary>按地形枚举播放脚步声（大地图）</summary>
	public void PlayOverworldFootstep(string owTerrainName)
	{
		string key = "ow_" + owTerrainName.ToLower();
		string sfxName = _footstepMap.TryGetValue(key, out var name) ? name : "move_footstep_grass";
		PlaySfxNameRandomPitch(sfxName);
	}

	/// <summary>按技能 vfx_type 播放技能音效</summary>
	public void PlaySkillSfx(string vfxType)
	{
		string sfxName = "skill_" + vfxType;
		if (_sfxLibrary.ContainsKey(sfxName))
			PlaySfxName(sfxName);
		else
			GD.PushWarning($"AudioManager: 技能音效未注册 - {sfxName}");
	}

	/// <summary>按法术学派播放法术施放音效</summary>
	public void PlaySpellCastSfx(string school)
	{
		string sfxName = $"spell_{school.ToLower()}_cast";
		if (_sfxLibrary.ContainsKey(sfxName))
			PlaySfxName(sfxName);
		else
			GD.PushWarning($"AudioManager: 法术施放音效未注册 - {sfxName}");
	}

	/// <summary>按法术学派播放法术命中音效</summary>
	public void PlaySpellImpactSfx(string school)
	{
		string sfxName = $"spell_{school.ToLower()}_impact";
		if (_sfxLibrary.ContainsKey(sfxName))
			PlaySfxName(sfxName);
		else
			GD.PushWarning($"AudioManager: 法术命中音效未注册 - {sfxName}");
	}

	/// <summary>按序播放音效序列（用于连击/多段技能）</summary>
	public void PlaySfxSequence(string[] names, float interval = 0.15f, float volumeDb = 0.0f)
	{
		if (names.Length == 0) return;

		PlaySfxName(names[0], volumeDb);

		if (names.Length > 1)
		{
			var tween = CreateTween();
			for (int i = 1; i < names.Length; i++)
			{
				int idx = i;
				tween.TweenCallback(Callable.From(() => PlaySfxName(names[idx], volumeDb))).SetDelay(interval);
			}
		}
	}

	// ========================================================================
	// SFX Combo 系统 — 多个短音效按时间序列组合播放
	// ========================================================================

	/// <summary>组合音效定义</summary>
	public struct SfxCombo
	{
		/// <summary>组成片段的 SFX 名称列表</summary>
		public string[] Parts;
		/// <summary>每个片段相对于起始的延迟（秒），长度与 Parts 相同</summary>
		public float[] Delays;
		/// <summary>每个片段的音量偏移（dB），null 则全部使用默认</summary>
		public float[]? Volumes;
	}

	/// <summary>注册一个组合音效（多个原子音效按时间序列播放）</summary>
	public void RegisterSfxCombo(string comboName, string[] parts, float[] delays, float[]? volumes = null)
	{
		_sfxCombos[comboName] = new SfxCombo
		{
			Parts = parts,
			Delays = delays,
			Volumes = volumes
		};
	}

	/// <summary>播放组合音效</summary>
	public void PlaySfxCombo(string comboName, float volumeDb = 0.0f)
	{
		if (!_sfxCombos.TryGetValue(comboName, out var combo))
		{
			GD.PushWarning($"AudioManager: 找不到组合音效 - {comboName}");
			return;
		}

		var tween = CreateTween();
		for (int i = 0; i < combo.Parts.Length; i++)
		{
			int idx = i;
			float vol = combo.Volumes != null && idx < combo.Volumes.Length ? combo.Volumes[idx] : volumeDb;
			float delay = combo.Delays[idx];

			if (delay <= 0 && idx == 0)
			{
				PlaySfxName(combo.Parts[0], vol);
			}
			else
			{
				float capturedVol = vol;
				tween.TweenCallback(Callable.From(() => PlaySfxName(combo.Parts[idx], capturedVol)))
					.SetDelay(delay);
			}
		}
	}

	// ========================================================================
	// Ambient 接口
	// ========================================================================

	/// <summary>播放环境氛围音（循环）— 自动尝试 .ogg → .mp3 后缀</summary>
	public void PlayAmbient(string ambientName, float volumeDb = -6.0f)
	{
		if (_activeAmbients.ContainsKey(ambientName))
			return;

		// 优先尝试 .ogg (转换后的格式)
		string streamPath = AmbientBasePath + ambientName + ".ogg";
		var audioStream = GetAudioStream(streamPath);
		if (audioStream == null)
		{
			// 回退到 .mp3
			streamPath = AmbientBasePath + ambientName + ".mp3";
			audioStream = GetAudioStream(streamPath);
		}
		if (audioStream == null)
		{
			GD.PushWarning($"AudioManager: 环境音文件不存在 - {ambientName}");
			return;
		}

		// 设置循环
		if (audioStream is AudioStreamOggVorbis ogg)
			ogg.Loop = true;
		else if (audioStream is AudioStreamMP3 mp3)
			mp3.Loop = true;

		// 优先使用空闲的播放器
		AudioStreamPlayer? player = null;
		for (int i = 0; i < MaxAmbientPlayers; i++)
		{
			var candidate = _ambientPlayers[(_ambientIndex + i) % MaxAmbientPlayers];
			if (!candidate.Playing)
			{
				player = candidate;
				_ambientIndex = (_ambientIndex + i + 1) % MaxAmbientPlayers;
				break;
			}
		}

		if (player == null)
		{
			player = _ambientPlayers[_ambientIndex];
			_ambientIndex = (_ambientIndex + 1) % MaxAmbientPlayers;
			// 从 _activeAmbients 中移除被覆盖的条目
			string? keyToRemove = null;
			foreach (var kvp in _activeAmbients)
			{
				if (kvp.Value == player)
				{
					keyToRemove = kvp.Key;
					break;
				}
			}
			if (keyToRemove != null)
				_activeAmbients.Remove(keyToRemove);
		}

		player.Stream = audioStream;
		player.VolumeDb = volumeDb;
		player.Play();

		_activeAmbients[ambientName] = player;
	}

	/// <summary>停止指定环境音</summary>
	public void StopAmbient(string ambientName, float fadeOutTime = 1.0f)
	{
		if (!_activeAmbients.TryGetValue(ambientName, out var player))
			return;

		if (fadeOutTime > 0)
		{
			var tween = CreateTween();
			tween.TweenProperty(player, "volume_db", -80.0f, fadeOutTime)
				.SetTrans(Tween.TransitionType.Sine);
			tween.TweenCallback(Callable.From(() =>
			{
				player.Stop();
				player.VolumeDb = 0.0f;
			}));
		}
		else
		{
			player.Stop();
			player.VolumeDb = 0.0f;
		}

		_activeAmbients.Remove(ambientName);
	}

	/// <summary>停止所有环境音</summary>
	public void StopAllAmbients(float fadeOutTime = 1.0f)
	{
		foreach (string ambientName in _activeAmbients.Keys.ToList())
			StopAmbient(ambientName, fadeOutTime);
	}

	// ========================================================================
	// 全局控制
	// ========================================================================

	/// <summary>停止所有音频（BGM + SFX + Ambient）</summary>
	public void StopAll(float fadeOutTime = 1.0f)
	{
		StopBgm(fadeOutTime);
		StopAllAmbients(fadeOutTime);
	}

	/// <summary>设置总线音量 (0.0 ~ 1.0)</summary>
	public void SetBusVolume(string busName, float linear)
	{
		int idx = AudioServer.GetBusIndex(busName);
		if (idx >= 0)
			AudioServer.SetBusVolumeDb(idx, Mathf.LinearToDb(linear));
	}

	/// <summary>获取总线音量 (0.0 ~ 1.0)</summary>
	public float GetBusVolume(string busName)
	{
		int idx = AudioServer.GetBusIndex(busName);
		if (idx >= 0)
			return Mathf.DbToLinear(AudioServer.GetBusVolumeDb(idx));
		return 0.0f;
	}

	/// <summary>静音/取消静音指定总线</summary>
	public void SetBusMute(string busName, bool mute)
	{
		int idx = AudioServer.GetBusIndex(busName);
		if (idx >= 0)
			AudioServer.SetBusMute(idx, mute);
	}

	// ========================================================================
	// 音频加载辅助
	// ========================================================================

	private static AudioStream? GetAudioStream(string path)
	{
		return AudioAssetResolver.LoadAny(path);
	}

	/// <summary>动态加载外部音频文件 (支持 .mp3 和 .ogg)</summary>
	public static AudioStream? LoadExternalAudio(string path)
	{
		return AudioAssetResolver.LoadExternalAudio(path);
	}

	/// <summary>检查是否注册了指定名称的音效（含组合音效）</summary>
	public bool HasSfx(string sfxName)
	{
		return (_sfxLibrary.ContainsKey(sfxName) && _sfxLibrary[sfxName].Count > 0)
			|| _sfxCombos.ContainsKey(sfxName);
	}
}
