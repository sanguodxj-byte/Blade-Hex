// OverworldScene3D.Weather.cs
// 天气系统 — WeatherManager + 粒子特效 + 视觉色调 + 游戏性影响
using Godot;
using BladeHex.Map;
using BladeHex.View.Environment;
using BladeHex.View.Map;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
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

    // ========================================
    // 天气游戏性修正值（每帧缓存，供其他系统读取）
    // ========================================

    /// <summary>天气移速修正因子 (0.5~1.0)</summary>
    public float WeatherSpeedFactor { get; private set; } = 1.0f;

    /// <summary>天气视野修正因子 (0.6~1.0)</summary>
    public float WeatherVisionFactor { get; private set; } = 1.0f;

    /// <summary>天气遭遇率修正因子 (0.7~1.5)</summary>
    public float WeatherEncounterFactor { get; private set; } = 1.0f;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>初始化天气管理器和粒子系统</summary>
    private void InitWeatherSystem()
    {
        // 天气状态机
        _weatherMgr = new WeatherManager();
        _weatherMgr.Name = "WeatherManager";
        AddChild(_weatherMgr);
        _weatherMgr.WeatherChanged += OnWeatherChanged;

        // 2D 天气粒子（CanvasLayer 方案，始终在 3D 场景之上）
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

        GD.Print("[OverworldScene3D] 天气系统初始化完成");
    }

    // ========================================
    // 每帧更新
    // ========================================

    /// <summary>天气系统每帧更新（在 _Process 中调用）</summary>
    private void UpdateWeather(float dt)
    {
        if (_weatherMgr == null) return;

        // 天气自动循环（移动或等待时 tick）
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

    /// <summary>天气变化回调 — 更新 UI + 粒子</summary>
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

        // 更新云层（天气联动）
        UpdateCloudLayerForWeather(weatherType);

        GD.Print($"[OverworldScene3D/Weather] 天气变化: {(WeatherType)oldWeather} → {weatherType}");
    }

    /// <summary>更新天气视觉效果（光照色调 + 沙尘暴雾罩）</summary>
    private void UpdateWeatherVisuals(float dt)
    {
        if (_weatherMgr == null || _sunLight == null || _worldEnv == null) return;

        var activeWeather = _weatherMgr.GetActiveWeatherType();
        float intensity = _weatherMgr.GetEffectiveIntensity();

        // --- 光照色调修正 ---
        // 天气会叠加在昼夜循环之上，降低亮度并偏移色温
        Color weatherTint = Colors.White;
        float energyMod = 1.0f;
        float ambientMod = 1.0f;

        switch (activeWeather)
        {
            case WeatherType.Rain:
                // 雨天：偏蓝灰，降低亮度
                weatherTint = new Color(
                    Mathf.Lerp(1.0f, 0.72f, intensity),
                    Mathf.Lerp(1.0f, 0.75f, intensity),
                    Mathf.Lerp(1.0f, 0.82f, intensity));
                energyMod = Mathf.Lerp(1.0f, 0.65f, intensity);
                ambientMod = Mathf.Lerp(1.0f, 0.75f, intensity);
                break;

            case WeatherType.Snow:
                // 雪天：偏冷白，轻微降低亮度
                weatherTint = new Color(
                    Mathf.Lerp(1.0f, 0.88f, intensity),
                    Mathf.Lerp(1.0f, 0.92f, intensity),
                    Mathf.Lerp(1.0f, 1.05f, intensity));
                energyMod = Mathf.Lerp(1.0f, 0.75f, intensity);
                ambientMod = Mathf.Lerp(1.0f, 0.85f, intensity);
                break;

            case WeatherType.Sandstorm:
                // 沙尘暴：偏黄褐，大幅降低亮度
                weatherTint = new Color(
                    Mathf.Lerp(1.0f, 1.1f, intensity),
                    Mathf.Lerp(1.0f, 0.85f, intensity),
                    Mathf.Lerp(1.0f, 0.55f, intensity));
                energyMod = Mathf.Lerp(1.0f, 0.5f, intensity);
                ambientMod = Mathf.Lerp(1.0f, 0.6f, intensity);
                break;
        }

        // 应用天气色调到光照（叠加在昼夜循环之上）
        _sunLight.LightEnergy = _baseSunEnergy * energyMod;
        _worldEnv.AmbientLightEnergy = _baseAmbientEnergy * ambientMod;
        _sunLight.LightColor = _baseSunColor * weatherTint;
        _worldEnv.AmbientLightColor = _baseAmbientColor * weatherTint * 0.8f;

        // --- 沙尘暴屏幕雾罩 ---
        float targetTintAlpha = activeWeather == WeatherType.Sandstorm
            ? intensity * 0.2f  // 最大 20% 不透明度
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

    /// <summary>更新天气对游戏性的影响因子</summary>
    private void UpdateWeatherGameplayFactors()
    {
        if (_weatherMgr == null)
        {
            WeatherSpeedFactor = 1.0f;
            WeatherVisionFactor = 1.0f;
            WeatherEncounterFactor = 1.0f;
            return;
        }

        var weather = _weatherMgr.GetActiveWeatherType();
        float intensity = _weatherMgr.GetEffectiveIntensity();

        // --- 移速修正 ---
        // 雨天：轻度-5%, 中度-15%, 重度-25%
        // 雪天：轻度-10%, 中度-20%, 重度-35%
        // 沙尘暴：轻度-15%, 中度-30%, 重度-50%
        WeatherSpeedFactor = weather switch
        {
            WeatherType.Rain => Mathf.Lerp(1.0f, 0.75f, intensity),
            WeatherType.Snow => Mathf.Lerp(1.0f, 0.65f, intensity),
            WeatherType.Sandstorm => Mathf.Lerp(1.0f, 0.50f, intensity),
            _ => 1.0f,
        };

        // --- 视野修正 ---
        // 雨天：轻度-10%, 中度-20%, 重度-30%
        // 雪天：轻度-10%, 中度-25%, 重度-40%
        // 沙尘暴：轻度-20%, 中度-35%, 重度-50%
        WeatherVisionFactor = weather switch
        {
            WeatherType.Rain => Mathf.Lerp(1.0f, 0.70f, intensity),
            WeatherType.Snow => Mathf.Lerp(1.0f, 0.60f, intensity),
            WeatherType.Sandstorm => Mathf.Lerp(1.0f, 0.50f, intensity),
            _ => 1.0f,
        };

        // --- 遭遇率修正 ---
        // 雨天：遭遇率降低（敌人也不想淋雨）
        // 雪天：遭遇率降低
        // 沙尘暴：遭遇率大幅降低（视野差，双方都难发现对方）
        // 但夜间+恶劣天气 = 被伏击概率上升（由战斗系统处理）
        WeatherEncounterFactor = weather switch
        {
            WeatherType.Rain => Mathf.Lerp(1.0f, 0.75f, intensity),
            WeatherType.Snow => Mathf.Lerp(1.0f, 0.80f, intensity),
            WeatherType.Sandstorm => Mathf.Lerp(1.0f, 0.60f, intensity),
            _ => 1.0f,
        };
    }

    // ========================================
    // 地形上下文
    // ========================================

    /// <summary>更新天气管理器的地形上下文（雪地/沙漠判定）</summary>
    private void UpdateWeatherTerrainContext()
    {
        if (_weatherMgr == null) return;

        HexOverworldTile? tile = null;
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
            tile = _chunkManager.GetTile(axial.X, axial.Y);
        }
        else
        {
            tile = _grid.GetTileAtPixel(_playerPixelPos.X, _playerPixelPos.Y);
        }

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
    // 战斗天气传递
    // ========================================

    /// <summary>将当前天气写入 GlobalState，供战斗场景读取</summary>
    private void WriteWeatherToGlobalState()
    {
        var gs = BladeHex.Data.Globals.StateOrNull;
        if (gs == null || _weatherMgr == null) return;

        gs.Weather.Type = (int)_weatherMgr.CurrentWeather;
    }

    // ========================================
    // 调试 API
    // ========================================

    /// <summary>强制设置天气（调试用）</summary>
    public void DebugSetWeather(WeatherType weather, WeatherIntensity intensity = WeatherIntensity.Moderate)
    {
        _weatherMgr?.SetWeatherImmediate(weather, intensity);
    }

    /// <summary>获取当前天气类型</summary>
    public WeatherType GetCurrentWeather()
    {
        return _weatherMgr?.CurrentWeather ?? WeatherType.Clear;
    }

    /// <summary>获取当前天气强度</summary>
    public float GetCurrentWeatherIntensity()
    {
        return _weatherMgr?.GetEffectiveIntensity() ?? 0.0f;
    }

    /// <summary>天气联动更新云层</summary>
    private void UpdateCloudLayerForWeather(WeatherType weather)
    {
        if (_cloudLayer == null) return;

        switch (weather)
        {
            case WeatherType.Rain:
                _cloudLayer.SetCoverage(0.7f);
                _cloudLayer.SetOpacity(0.35f);
                _cloudLayer.SetCloudColor(new Color(0.6f, 0.62f, 0.68f));
                _windSystem?.SetWind(0.5f, 0.7f);
                _windSystem?.SetGust(0.4f, 5.0f);
                break;
            case WeatherType.Snow:
                _cloudLayer.SetCoverage(0.6f);
                _cloudLayer.SetOpacity(0.30f);
                _cloudLayer.SetCloudColor(new Color(0.85f, 0.87f, 0.92f));
                _windSystem?.SetWind(0.2f, 0.35f);
                _windSystem?.SetGust(0.2f, 10.0f);
                break;
            case WeatherType.Sandstorm:
                _cloudLayer.SetCoverage(0.5f);
                _cloudLayer.SetOpacity(0.25f);
                _cloudLayer.SetCloudColor(new Color(0.8f, 0.7f, 0.5f));
                _windSystem?.SetWind(1.0f, 0.9f);
                _windSystem?.SetGust(0.5f, 3.0f);
                break;
            default: // Clear
                _cloudLayer.SetCoverage(0.45f);
                _cloudLayer.SetOpacity(0.35f);
                _cloudLayer.SetCloudColor(new Color(0.95f, 0.95f, 1.0f));
                _windSystem?.SetWind(0.3f, 0.4f);
                _windSystem?.SetGust(0.3f, 8.0f);
                break;
        }
    }
}
