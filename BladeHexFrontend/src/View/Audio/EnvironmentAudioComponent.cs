// EnvironmentAudioComponent.cs
// 动态环境音频组件（天气、日夜、生态群系、城镇接近与间歇性BGM）
// 职责：根据当前的大地图位置、天气和时间，平滑切换对应的背景音乐和环境音效(白噪音)。
// 支持《旷野之息》式的"留白"随机播放机制，让音乐在播放完后停顿一段时间再播放。
using Godot;
using System;

namespace BladeHex.Audio;

/// <summary>
/// 动态环境音频组件 — 管理天气、日夜、生态群系、城镇接近的音频切换。
/// 支持间歇性 BGM 播放（留白机制）。
/// </summary>
[GlobalClass]
public partial class EnvironmentAudioComponent : Node
{
    // ========================================================================
    // 枚举定义
    // ========================================================================

    public enum WeatherType
    {
        Clear, Cloudy, Rain, Storm, Snow, Fog, Blizzard, Sandstorm, Heatwave, MagicStorm
    }

    public enum TimeOfDay
    {
        Day, Night
    }

    public enum BiomeType
    {
        Plains, Forest, Mountain, Swamp, Desert, Snowland
    }

    public enum ProximityType
    {
        None, Town, DungeonEntrance, Ruins, Camp
    }

    // ========================================================================
    // 当前状态
    // ========================================================================

    public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
    public TimeOfDay CurrentTime { get; private set; } = TimeOfDay.Day;
    public BiomeType CurrentBiome { get; private set; } = BiomeType.Plains;
    public ProximityType CurrentProximity { get; private set; } = ProximityType.None;
    public int CurrentScenario { get; set; } = (int)AudioManager.Scenario.Overworld;

    // ========================================================================
    // 配置选项
    // ========================================================================

    [Export] public bool EnableBgmIntervals { get; set; } = true;
    [Export] public float MinSilenceTime { get; set; } = 60.0f;
    [Export] public float MaxSilenceTime { get; set; } = 240.0f;
    /// <summary>BGM 结束后，再次随机延长静音的概率（0~1）— 用于偶发的"长留白"</summary>
    [Export] public float ExtendedSilenceChance { get; set; } = 0.35f;
    /// <summary>触发长留白时的额外秒数（追加在普通静音之后）</summary>
    [Export] public float ExtendedSilenceBonus { get; set; } = 180.0f;

    // ========================================================================
    // 内部引用
    // ========================================================================

    private AudioManager? _audioManager;
    private Timer? _silenceTimer;
    private Timer? _thunderTimer;

    private static readonly Random _rng = new();

    // ========================================================================
    // 初始化
    // ========================================================================

    public override void _Ready()
    {
        _audioManager = AudioManager.Instance;
        if (_audioManager == null)
        {
            GD.PushWarning("EnvironmentAudioComponent: 未找到 AudioManager 单例。");
            return;
        }

        RegisterEnvironmentAudio();

        // 监听 BGM 播放完毕
        _audioManager.BgmTrackFinished += OnBgmTrackFinished;

        // 初始化静音定时器
        _silenceTimer = new Timer { OneShot = true };
        _silenceTimer.Timeout += OnSilenceTimerTimeout;
        AddChild(_silenceTimer);
    }

    public override void _ExitTree()
    {
        if (_audioManager != null)
            _audioManager.BgmTrackFinished -= OnBgmTrackFinished;
    }

    private void RegisterEnvironmentAudio()
    {
        if (_audioManager == null) return;

        // 天气 — overworld_rain + overworld_rain_2 已在 AudioManager.InitBgmPlaylists 注册
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "storm", AudioManager.BgmBasePath + "overworld_storm.ogg");

        // 生态群系 (Biome) BGM 变体
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_forest", AudioManager.BgmBasePath + "biome_forest.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_mountain", AudioManager.BgmBasePath + "overworld_travel.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_swamp", AudioManager.BgmBasePath + "overworld_night.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_desert", AudioManager.BgmBasePath + "biome_desert.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_snowland", AudioManager.BgmBasePath + "biome_snowland.ogg");

        // 地标接近 (Proximity) BGM 变体（prox_ruins_3 已移作boss战斗）
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_town", AudioManager.BgmBasePath + "overworld_travel.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_ruins", AudioManager.BgmBasePath + "prox_ruins.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_ruins", AudioManager.BgmBasePath + "prox_ruins_2.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_camp", AudioManager.BgmBasePath + "prox_camp.ogg");
    }

    // ========================================================================
    // 核心状态修改接口
    // ========================================================================

    public void SetWeather(WeatherType newWeather, float fadeTime = 3.0f)
    {
        if (CurrentWeather == newWeather) return;
        var oldWeather = CurrentWeather;
        CurrentWeather = newWeather;
        TransitionAmbient(oldWeather, newWeather, fadeTime);
        EvaluateAndPlayBgm(fadeTime);
    }

    public void SetTimeOfDay(TimeOfDay newTime, float fadeTime = 3.0f)
    {
        if (CurrentTime == newTime) return;
        var oldTime = CurrentTime;
        CurrentTime = newTime;
        TransitionTimeAmbient(oldTime, newTime, fadeTime);
        EvaluateAndPlayBgm(fadeTime);
    }

    public void SetBiome(BiomeType newBiome, float fadeTime = 5.0f)
    {
        if (CurrentBiome == newBiome) return;
        CurrentBiome = newBiome;
        // 切换地形底噪（仅在天气平静时）
        if (CurrentWeather == WeatherType.Clear || CurrentWeather == WeatherType.Cloudy)
            PlayTerrainAmbient();
        EvaluateAndPlayBgm(fadeTime);
    }

    public void SetProximity(ProximityType newProximity, float fadeTime = 2.0f)
    {
        if (CurrentProximity == newProximity) return;
        var oldProximity = CurrentProximity;
        CurrentProximity = newProximity;

        // 进入城镇/营地时自动切换场景底噪
        if (newProximity == ProximityType.Town)
            SetSceneAmbient(CurrentTime == TimeOfDay.Night ? SceneAmbients.TownNight : SceneAmbients.Town);
        else if (newProximity == ProximityType.Camp)
            SetSceneAmbient(SceneAmbients.Camp);
        else if (oldProximity == ProximityType.Town || oldProximity == ProximityType.Camp)
            ClearSceneAmbient(fadeTime);

        EvaluateAndPlayBgm(fadeTime);
    }

    public void SetScenario(int scenarioEnum, float fadeTime = 2.0f)
    {
        CurrentScenario = scenarioEnum;
        EvaluateAndPlayBgm(fadeTime);
    }

    // ========================================================================
    // 留白式 BGM 播放逻辑
    // ========================================================================

    private void EvaluateAndPlayBgm(float fadeTime = 3.0f)
    {
        if (_audioManager == null) return;

        // 如果处于静音倒计时中，打破静音
        if (_silenceTimer != null && _silenceTimer.TimeLeft > 0)
            _silenceTimer.Stop();

        string variant = DetermineBgmVariant();
        _audioManager.PlayScenarioBgm(CurrentScenario, variant, fadeTime);
    }

    private string DetermineBgmVariant()
    {
        // 1. 优先级最高：接近特定的重要地标
        switch (CurrentProximity)
        {
            case ProximityType.Town: return "prox_town";
            case ProximityType.Ruins: return "prox_ruins";
            case ProximityType.Camp: return "prox_camp";
        }

        // 2. 次高优先级：极端天气
        switch (CurrentWeather)
        {
            case WeatherType.Storm: return "storm";
            case WeatherType.Rain: return "rain";
            case WeatherType.Snow: return "biome_snowland";
        }

        // 3. 夜晚
        if (CurrentTime == TimeOfDay.Night)
            return "night";

        // 4. 最低优先级：基础的生态群系音乐
        return CurrentBiome switch
        {
            BiomeType.Forest => "biome_forest",
            BiomeType.Mountain => "biome_mountain",
            BiomeType.Swamp => "biome_swamp",
            BiomeType.Desert => "biome_desert",
            BiomeType.Snowland => "biome_snowland",
            _ => "default"
        };
    }

    private void OnBgmTrackFinished(string trackPath)
    {
        if (!EnableBgmIntervals) return;

        // 城镇或战斗中不需要静音
        if (CurrentProximity != ProximityType.None || CurrentScenario == (int)AudioManager.Scenario.Combat)
        {
            EvaluateAndPlayBgm(1.0f);
            return;
        }

        // 基础静音时长（60~240 秒随机）
        float randomSilence = (float)(_rng.NextDouble() * (MaxSilenceTime - MinSilenceTime) + MinSilenceTime);

        // 概率性长留白：偶尔追加额外静音时间（让玩家更长时间沉浸在环境音里）
        if (_rng.NextDouble() < ExtendedSilenceChance)
            randomSilence += (float)(_rng.NextDouble() * ExtendedSilenceBonus);

        _silenceTimer?.Start(randomSilence);
    }

    private void OnSilenceTimerTimeout()
    {
        EvaluateAndPlayBgm(2.0f);
    }

    // ========================================================================
    // 环境底噪(Ambient) 与 随机音效
    // ========================================================================

    /// <summary>
    /// 天气变化时切换环境底噪。
    /// 天气音效优先级最高，会覆盖地形底噪。
    /// </summary>
    private void TransitionAmbient(WeatherType oldWeather, WeatherType newWeather, float fadeTime)
    {
        if (_audioManager == null) return;

        // 停止旧天气音效
        string oldAmbient = GetAmbientForWeather(oldWeather);
        if (!string.IsNullOrEmpty(oldAmbient))
            _audioManager.StopAmbient(oldAmbient, fadeTime);

        // 播放新天气音效
        string newAmbient = GetAmbientForWeather(newWeather);
        if (!string.IsNullOrEmpty(newAmbient))
            _audioManager.PlayAmbient(newAmbient, -8.0f);

        // 天气结束时恢复地形底噪
        if (newWeather == WeatherType.Clear || newWeather == WeatherType.Cloudy)
            PlayTerrainAmbient();
        else
            StopTerrainAmbient(fadeTime);

        // 雷暴管理
        if (newWeather == WeatherType.Storm)
            StartRandomThunder();
        else if (oldWeather == WeatherType.Storm)
            StopRandomThunder();
    }

    /// <summary>
    /// 昼夜切换时更新环境底噪。
    /// 仅在无天气干扰时生效。
    /// </summary>
    private void TransitionTimeAmbient(TimeOfDay oldTime, TimeOfDay newTime, float fadeTime)
    {
        if (_audioManager == null) return;

        // 停止旧的昼夜底噪
        string oldAmbient = GetAmbientForTime(oldTime);
        if (!string.IsNullOrEmpty(oldAmbient))
            _audioManager.StopAmbient(oldAmbient, fadeTime);

        // 仅在天气平静时播放昼夜+地形环境音
        if (CurrentWeather == WeatherType.Clear || CurrentWeather == WeatherType.Cloudy)
        {
            string newAmbient = GetAmbientForTime(newTime);
            if (!string.IsNullOrEmpty(newAmbient))
                _audioManager.PlayAmbient(newAmbient, -15.0f);
            PlayTerrainAmbient();
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 地形底噪（与天气互斥，天气优先）
    // ────────────────────────────────────────────────────────────────

    private string _activeTerrainAmbient = "";

    private void PlayTerrainAmbient()
    {
        if (_audioManager == null) return;
        string newTerrain = GetAmbientForBiome(CurrentBiome);
        if (newTerrain == _activeTerrainAmbient) return;

        if (!string.IsNullOrEmpty(_activeTerrainAmbient))
            _audioManager.StopAmbient(_activeTerrainAmbient, 3.0f);

        _activeTerrainAmbient = newTerrain;
        if (!string.IsNullOrEmpty(newTerrain))
            _audioManager.PlayAmbient(newTerrain, -14.0f);
    }

    private void StopTerrainAmbient(float fadeTime)
    {
        if (_audioManager == null || string.IsNullOrEmpty(_activeTerrainAmbient)) return;
        _audioManager.StopAmbient(_activeTerrainAmbient, fadeTime);
        _activeTerrainAmbient = "";
    }

    // ────────────────────────────────────────────────────────────────
    // 场景底噪（城镇、酒馆等室内场景）
    // ────────────────────────────────────────────────────────────────

    private string _activeSceneAmbient = "";

    /// <summary>
    /// 进入特殊场景（城镇、酒馆等）时调用，播放对应环境底噪。
    /// 会停止地形底噪。
    /// </summary>
    public void SetSceneAmbient(string ambientName, float volumeDb = -10.0f)
    {
        if (_audioManager == null) return;

        // 停止地形底噪
        StopTerrainAmbient(2.0f);

        // 停止旧场景底噪
        if (!string.IsNullOrEmpty(_activeSceneAmbient) && _activeSceneAmbient != ambientName)
            _audioManager.StopAmbient(_activeSceneAmbient, 2.0f);

        _activeSceneAmbient = ambientName;
        if (!string.IsNullOrEmpty(ambientName))
            _audioManager.PlayAmbient(ambientName, volumeDb);
    }

    /// <summary>
    /// 离开特殊场景时调用，恢复地形底噪。
    /// </summary>
    public void ClearSceneAmbient(float fadeTime = 2.0f)
    {
        if (_audioManager == null) return;

        if (!string.IsNullOrEmpty(_activeSceneAmbient))
        {
            _audioManager.StopAmbient(_activeSceneAmbient, fadeTime);
            _activeSceneAmbient = "";
        }

        // 恢复地形底噪（如果天气允许）
        if (CurrentWeather == WeatherType.Clear || CurrentWeather == WeatherType.Cloudy)
            PlayTerrainAmbient();
    }

    // ────────────────────────────────────────────────────────────────
    // 环境音效名称映射
    // ────────────────────────────────────────────────────────────────

    /// <summary>天气 → 环境音效（优先级最高）</summary>
    private static string GetAmbientForWeather(WeatherType weather)
    {
        return weather switch
        {
            WeatherType.Rain => "ambient_rain",
            WeatherType.Storm => "ambient_storm",         // 含雷声底噪
            WeatherType.Snow => "ambient_mountain",       // 风雪声（复用山地风声）
            WeatherType.Fog => "",                        // 雾天无特殊底噪，保留地形音
            WeatherType.Blizzard => "ambient_storm",      // 暴风雪
            WeatherType.Sandstorm => "ambient_desert",    // 沙尘暴
            WeatherType.Heatwave => "",                   // 热浪无特殊底噪
            WeatherType.MagicStorm => "ambient_storm",    // 魔法风暴
            // ── 预留 ──
            // WeatherType.Hail => "ambient_hail",
            // WeatherType.AcidRain => "ambient_acid_rain",
            _ => ""
        };
    }

    /// <summary>昼夜 → 环境底噪（低优先级，天气覆盖时不播放）</summary>
    private static string GetAmbientForTime(TimeOfDay time)
    {
        return time switch
        {
            TimeOfDay.Night => "ambient_night",
            // ── 预留 ──
            // TimeOfDay.Dawn => "ambient_dawn",
            // TimeOfDay.Dusk => "ambient_rural_night",
            _ => "" // 白天由地形底噪覆盖
        };
    }

    /// <summary>地形/生态群系 → 环境底噪</summary>
    private static string GetAmbientForBiome(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Plains => "ambient_plains",
            BiomeType.Forest => "ambient_forest",
            BiomeType.Mountain => "ambient_mountain",
            BiomeType.Swamp => "ambient_forest_2",        // 沼泽用森林变体（潮湿感）
            BiomeType.Desert => "ambient_desert",
            BiomeType.Snowland => "ambient_mountain",     // 雪地复用山地风声
            // ── 预留 ──
            // BiomeType.Coast => "ambient_ocean",
            // BiomeType.Volcanic => "ambient_volcanic",
            // BiomeType.Jungle => "ambient_jungle",
            _ => "ambient_plains"
        };
    }

    /// <summary>
    /// 场景环境音效名称常量 — 供外部调用 SetSceneAmbient 时使用。
    /// </summary>
    public static class SceneAmbients
    {
        public const string Town = "ambient_rural_night";         // 城镇（人声、脚步、远处铁匠）
        public const string TownNight = "ambient_rural_night_2";  // 城镇夜晚
        public const string Tavern = "ambient_forest";            // 酒馆（预留：ambient_tavern）
        public const string Market = "ambient_plains";            // 市场（预留：ambient_market）
        public const string Dungeon = "ambient_ocean";            // 地牢（回声、水滴，复用海洋的空旷感）
        public const string Camp = "ambient_night";               // 营地（夜晚虫鸣+篝火）
        // ── 预留（需要新音效文件） ──
        // public const string Tavern = "ambient_tavern";         // 酒馆（人声嘈杂、杯碟、笑声）
        // public const string Forge = "ambient_forge";           // 铁匠铺（锤击、火焰）
        // public const string Church = "ambient_church";         // 教堂（回声、唱诗）
        // public const string Harbor = "ambient_ocean";          // 港口（海浪、海鸥、绳索）
        // public const string Library = "ambient_library";       // 图书馆（翻书、低语）
        // public const string Sewer = "ambient_sewer";           // 下水道（水流、老鼠）
    }

    private void StartRandomThunder()
    {
        if (_thunderTimer == null)
        {
            _thunderTimer = new Timer { OneShot = true };
            _thunderTimer.Timeout += OnThunderStrike;
            AddChild(_thunderTimer);
        }
        ScheduleNextThunder();
    }

    private void StopRandomThunder()
    {
        _thunderTimer?.Stop();
    }

    private void OnThunderStrike()
    {
        if (CurrentWeather == WeatherType.Storm)
        {
            _audioManager?.PlaySfxNameRandomPitch("combat_env_storm", -2.0f, 0.8f, 1.2f);
            ScheduleNextThunder();
        }
    }

    private void ScheduleNextThunder()
    {
        float delay = (float)(_rng.NextDouble() * 10.0 + 5.0); // 5~15 seconds
        _thunderTimer?.Start(delay);
    }
}
