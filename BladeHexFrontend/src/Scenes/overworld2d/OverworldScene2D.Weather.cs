// OverworldScene2D.Weather.cs
// 天气系统 — 从 OverworldScene3D.Weather.cs 迁移
// 删除 3D 粒子，保留 2D 粒子
using Godot;
using BladeHex.Map;
using BladeHex.View.Environment;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 天气系统字段
    // ========================================

    private WeatherManager? _weatherMgr;
    private WeatherParticles2D? _weatherParticles2D;

    /// <summary>沙尘暴屏幕色调覆盖</summary>
    private ColorRect? _sandstormTintRect;
    private CanvasLayer? _sandstormTintLayer;
    private float _sandstormTintAlpha;

    // 天气游戏性修正值
    public float WeatherSpeedFactor { get; private set; } = 1.0f;
    public float WeatherVisionFactor { get; private set; } = 1.0f;
    public float WeatherEncounterFactor { get; private set; } = 1.0f;

    // ========================================
    // 初始化
    // ========================================

    private void InitWeatherSystem()
    {
        // 天气状态机（Autoload）— 容错获取
        _weatherMgr = Globals.WeatherOrNull;
        if (_weatherMgr == null)
        {
            GD.PrintErr("[OverworldScene2D] WeatherManager Autoload 不存在，跳过天气系统初始化");
            return;
        }
        _weatherMgr.WeatherChanged += OnWeatherChanged;

        // 2D 天气粒子（CanvasLayer 方案）
        _weatherParticles2D = new WeatherParticles2D();
        _weatherParticles2D.Name = "WeatherParticles2D";
        AddChild(_weatherParticles2D);

        // 沙尘暴屏幕色调层
        _sandstormTintLayer = new CanvasLayer { Name = "SandstormTintLayer", Layer = 6 };
        AddChild(_sandstormTintLayer);
        _sandstormTintRect = new ColorRect();
        _sandstormTintRect.Name = "SandstormTint";
        _sandstormTintRect.Color = new Color(0.78f, 0.62f, 0.38f, 0.0f);
        _sandstormTintRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _sandstormTintRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _sandstormTintLayer.AddChild(_sandstormTintRect);

        // 同步当前天气
        if (_weatherMgr.CurrentWeather != WeatherType.Clear)
            OnWeatherChanged(0, (int)_weatherMgr.CurrentWeather);

        GD.Print($"[OverworldScene2D] 天气系统就绪: 当前 {_weatherMgr.CurrentWeather}");
    }

    // ========================================
    // 每帧更新
    // ========================================

    private void UpdateWeather(float dt)
    {
        if (_weatherMgr == null) return;

        // 天气自动循环
        bool timeFlowing = (_playerMoving || IsWaiting) && !IsTimePaused;
        if (timeFlowing && EconomyMgr != null)
        {
            float deltaHours = dt * GameTimeScale;
            if (IsWaiting && !_playerMoving) deltaHours *= 8.0f;
            int season = (int)EconomyMgr.GetSeason();
            UpdateWeatherTerrainContext();
            _weatherMgr.TickWeatherCycle(season, deltaHours);
        }

        // 更新视觉效果
        UpdateWeatherVisuals(dt);

        // 更新游戏性修正值
        UpdateWeatherGameplayFactors();
    }

    // ========================================
    // 视觉效果
    // ========================================

    private void OnWeatherChanged(int oldWeather, int newWeather)
    {
        var weatherType = (WeatherType)newWeather;
        string weatherName = weatherType switch
        {
            WeatherType.Rain => "🌧 雨天",
            WeatherType.Snow => "🌨 雪天",
            WeatherType.Sandstorm => "🌪 沙尘暴",
            _ => "☀ 晴天",
        };

        // 更新 UI 天气显示
        _overworldUi?.UpdateWeatherDisplay(weatherName);

        // 更新粒子系统
        if (_weatherParticles2D != null)
        {
            if (weatherType == WeatherType.Clear)
                _weatherParticles2D.StopAll();
            else
                _weatherParticles2D.SetWeather(weatherType, _weatherMgr!.GetEffectiveIntensity());
        }

        // 更新音频
        if (_envAudio != null)
        {
            var audioWeather = weatherType switch
            {
                WeatherType.Rain => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Rain,
                WeatherType.Snow => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Snow,
                WeatherType.Sandstorm => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Sandstorm,
                _ => BladeHex.Audio.EnvironmentAudioComponent.WeatherType.Clear,
            };
            _envAudio.SetWeather(audioWeather);
        }

        UpdateWeatherGameplayFactors();

        GD.Print($"[OverworldScene2D/Weather] 天气变化: {(WeatherType)oldWeather} → {weatherType}");
    }

    private void UpdateWeatherVisuals(float dt)
    {
        if (_weatherMgr == null) return;

        var activeWeather = _weatherMgr.GetActiveWeatherType();
        float intensity = _weatherMgr.GetEffectiveIntensity();

        // 粒子强度
        bool particlesEnabled = Globals.StateOrNull?.Get("weather_particles_enabled").AsBool() ?? true;
        if (_weatherParticles2D != null)
        {
            if (!particlesEnabled || activeWeather == WeatherType.Clear || intensity < 0.01f)
                _weatherParticles2D.StopAll();
            else
                _weatherParticles2D.SetWeather(activeWeather, intensity);
        }

        // 沙尘暴屏幕雾罩
        float targetTintAlpha = activeWeather == WeatherType.Sandstorm
            ? intensity * 0.2f
            : 0.0f;
        _sandstormTintAlpha = Mathf.MoveToward(_sandstormTintAlpha, targetTintAlpha, dt * 0.15f);

        if (_sandstormTintRect != null)
        {
            _sandstormTintRect.Color = new Color(0.78f, 0.62f, 0.38f, _sandstormTintAlpha);
            _sandstormTintRect.Visible = _sandstormTintAlpha > 0.005f;
        }
    }

    // ========================================
    // 游戏性影响
    // ========================================

    private void UpdateWeatherGameplayFactors()
    {
        if (_weatherMgr == null)
        {
            WeatherSpeedFactor = 1.0f;
            WeatherVisionFactor = 1.0f;
            WeatherEncounterFactor = 1.0f;
            SyncWeatherSpeedFactor();
            return;
        }

        var factors = BladeHex.Scenes.Overworld.Components.WeatherController.CalculateGameplayFactors(
            _weatherMgr.GetActiveWeatherType(),
            _weatherMgr.GetEffectiveIntensity());
        WeatherSpeedFactor = factors.Speed;
        WeatherVisionFactor = factors.Vision;
        WeatherEncounterFactor = factors.Encounter;
        SyncWeatherSpeedFactor();
    }

    private void SyncWeatherSpeedFactor()
    {
        if (PlayerParty?.SpeedComponent != null)
            PlayerParty.SpeedComponent.WeatherSpeedFactor = WeatherSpeedFactor;

        if (EntityMgr?.SimCtx != null)
            EntityMgr.SimCtx.WeatherSpeedFactor = WeatherSpeedFactor;
    }

    // ========================================
    // 地形上下文
    // ========================================

    private void UpdateWeatherTerrainContext()
    {
        if (_weatherMgr == null) return;

        HexOverworldTile? tile = _mapAccess.GetActiveTileAtPixel(_playerPixelPos);

        if (tile == null) return;

        var t = tile.Terrain;
        _weatherMgr.IsInSnowTerrain = t == HexOverworldTile.TerrainType.Snow
            || t == HexOverworldTile.TerrainType.Ice
            || t == HexOverworldTile.TerrainType.MountainSnow
            || t == HexOverworldTile.TerrainType.Taiga;

        _weatherMgr.IsInDesertTerrain = t == HexOverworldTile.TerrainType.Sand
            || t == HexOverworldTile.TerrainType.Wasteland
            || t == HexOverworldTile.TerrainType.Savanna;
    }

    // ========================================
    // 清理
    // ========================================

    public override void _ExitTree()
    {
        if (_weatherMgr != null)
        {
            _weatherMgr.WeatherChanged -= OnWeatherChanged;
            _weatherMgr = null;
        }

        // 退订全局事件
        BladeHex.Events.EventBus.Instance?.Unsubscribe(BladeHex.Events.EventBus.Signals.DayPassed, OnDayPassedFief);

        if (EntityMgr?.WorldEngine != null)
        {
            EntityMgr.WorldEngine.NewsAdded -= OnNewsAdded;
        }

        // 清理临时交互节点
        CleanupCurrentTownNode();
        CleanupCurrentEnemyNode();
    }

    // ========================================
    // 调试 API
    // ========================================

    public void DebugSetWeather(WeatherType weather, WeatherIntensity intensity = WeatherIntensity.Moderate)
    {
        _weatherMgr?.SetWeatherImmediate(weather, intensity);
    }

    public WeatherType GetCurrentWeather()
    {
        return _weatherMgr?.CurrentWeather ?? WeatherType.Clear;
    }

    public float GetCurrentWeatherIntensity()
    {
        return _weatherMgr?.GetEffectiveIntensity() ?? 0.0f;
    }
}
