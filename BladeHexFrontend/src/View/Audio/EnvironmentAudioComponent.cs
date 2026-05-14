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

        // 天气
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "rain", AudioManager.BgmBasePath + "overworld_rain.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "storm", AudioManager.BgmBasePath + "overworld_storm.ogg");

        // 生态群系 (Biome) BGM 变体
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_forest", AudioManager.BgmBasePath + "biome_forest.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_mountain", AudioManager.BgmBasePath + "overworld_travel.ogg"); // 资源缺失，复用
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_swamp", AudioManager.BgmBasePath + "overworld_night.ogg"); // 复用夜晚的阴郁感
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_desert", AudioManager.BgmBasePath + "biome_desert.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "biome_snowland", AudioManager.BgmBasePath + "biome_snowland.ogg");

        // 地标接近 (Proximity) BGM 变体
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_town", AudioManager.BgmBasePath + "overworld_travel.ogg"); // 资源缺失，复用
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_ruins", AudioManager.BgmBasePath + "prox_ruins.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_ruins", AudioManager.BgmBasePath + "prox_ruins_2.ogg");
        _audioManager.AddBgmVariant(AudioManager.Scenario.Overworld, "prox_ruins", AudioManager.BgmBasePath + "prox_ruins_3.ogg");
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
        EvaluateAndPlayBgm(fadeTime);
    }

    public void SetProximity(ProximityType newProximity, float fadeTime = 2.0f)
    {
        if (CurrentProximity == newProximity) return;
        CurrentProximity = newProximity;
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

    private void TransitionAmbient(WeatherType oldWeather, WeatherType newWeather, float fadeTime)
    {
        if (_audioManager == null) return;

        string oldAmbient = GetAmbientNameForWeather(oldWeather);
        if (!string.IsNullOrEmpty(oldAmbient))
            _audioManager.StopAmbient(oldAmbient, fadeTime);

        string newAmbient = GetAmbientNameForWeather(newWeather);
        if (!string.IsNullOrEmpty(newAmbient))
            _audioManager.PlayAmbient(newAmbient, -10.0f);

        if (newWeather == WeatherType.Storm)
            StartRandomThunder();
        else if (oldWeather == WeatherType.Storm)
            StopRandomThunder();
    }

    private void TransitionTimeAmbient(TimeOfDay oldTime, TimeOfDay newTime, float fadeTime)
    {
        if (_audioManager == null) return;

        string oldAmbient = oldTime == TimeOfDay.Day ? "ambient_forest" : "ambient_night";
        string newAmbient = newTime == TimeOfDay.Day ? "ambient_forest" : "ambient_night";

        _audioManager.StopAmbient(oldAmbient, fadeTime);

        // 仅在天气平静时播放昼夜环境音
        if (CurrentWeather == WeatherType.Clear || CurrentWeather == WeatherType.Cloudy)
            _audioManager.PlayAmbient(newAmbient, -15.0f);
    }

    private static string GetAmbientNameForWeather(WeatherType weather)
    {
        return weather switch
        {
            WeatherType.Rain => "ambient_rain",
            WeatherType.Storm => "ambient_storm",
            WeatherType.Snow => "ambient_mountain", // 复用山地风声
            WeatherType.Fog => "ambient_forest",
            WeatherType.Blizzard => "ambient_storm",
            WeatherType.Sandstorm => "ambient_desert",
            WeatherType.Heatwave => "ambient_desert",
            WeatherType.MagicStorm => "ambient_storm",
            _ => ""
        };
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
