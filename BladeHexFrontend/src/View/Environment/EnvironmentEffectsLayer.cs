// EnvironmentEffectsLayer.cs
// 环境特效渲染层 — 管理天气粒子覆盖层 + 地面特效覆盖层
// 作为 CanvasLayer 添加到 OverworldScene3D，跟随摄像机
using Godot;
using System;

namespace BladeHex.View.Environment;

/// <summary>
/// 环境特效渲染层。
/// 包含两个全屏 ColorRect：
///   1. _particleOverlay — 天气粒子（雨/雪/沙尘暴）
///   2. _groundOverlay  — 地面特效（积水/沙地噪声/雪覆盖）
/// 由 WeatherManager 驱动 shader uniform 更新。
/// </summary>
[GlobalClass]
public partial class EnvironmentEffectsLayer : CanvasLayer
{
    // ========================================
    // 导出参数
    // ========================================

    /// <summary>天气粒子 shader 路径</summary>
    [Export] public string ParticleShaderPath { get; set; } = "res://src/assets/shaders/weather_particles.gdshader";

    /// <summary>地面特效 shader 路径</summary>
    [Export] public string GroundShaderPath { get; set; } = "res://src/assets/shaders/ground_effects.gdshader";

    /// <summary>地面特效延迟启动时间（秒）— 雨开始后多久出现积水</summary>
    [Export] public float GroundEffectDelay { get; set; } = 5.0f;

    /// <summary>地面特效渐入时间（秒）</summary>
    [Export] public float GroundFadeInDuration { get; set; } = 8.0f;

    /// <summary>地面特效渐出时间（秒）</summary>
    [Export] public float GroundFadeOutDuration { get; set; } = 12.0f;

    // ========================================
    // 内部节点
    // ========================================

    private ColorRect _particleOverlay = null!;
    private ColorRect _groundOverlay = null!;
    private ShaderMaterial _particleMaterial = null!;
    private ShaderMaterial _groundMaterial = null!;

    // ========================================
    // 状态
    // ========================================

    private WeatherManager? _weatherManager;
    private Camera2D? _camera;

    // 地面特效状态机
    private bool _groundEffectActive;
    private float _groundDelayTimer;
    private float _groundIntensity; // 当前地面特效强度 [0, 1]
    private bool _groundFadingIn;
    private bool _groundFadingOut;

    // 上一帧的天气类型（检测变化）
    private WeatherType _lastWeatherType = WeatherType.Clear;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 初始化环境特效层。
    /// </summary>
    /// <param name="weatherManager">天气管理器引用</param>
    /// <param name="camera">主摄像机引用（用于地面特效世界坐标同步）</param>
    public void Initialize(WeatherManager weatherManager, Camera2D camera)
    {
        _weatherManager = weatherManager;
        _camera = camera;
    }

    public override void _Ready()
    {
        // 设置 CanvasLayer 层级（在 UI 之下，游戏世界之上）
        Layer = 5;
        FollowViewportEnabled = true;

        // 创建天气粒子覆盖层
        _particleOverlay = new ColorRect();
        _particleOverlay.Name = "ParticleOverlay";
        _particleOverlay.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        _particleOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _particleOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        _particleOverlay.Visible = false;

        // 加载天气粒子 shader
        var particleShader = GD.Load<Shader>(ParticleShaderPath);
        if (particleShader != null)
        {
            _particleMaterial = new ShaderMaterial();
            _particleMaterial.Shader = particleShader;
            _particleOverlay.Material = _particleMaterial;
        }
        else
        {
            GD.PrintErr($"[EnvironmentEffectsLayer] 无法加载天气粒子 shader: {ParticleShaderPath}");
        }

        AddChild(_particleOverlay);

        // 创建地面特效覆盖层
        _groundOverlay = new ColorRect();
        _groundOverlay.Name = "GroundOverlay";
        _groundOverlay.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        _groundOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _groundOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        _groundOverlay.Visible = false;

        // 加载地面特效 shader
        var groundShader = GD.Load<Shader>(GroundShaderPath);
        if (groundShader != null)
        {
            _groundMaterial = new ShaderMaterial();
            _groundMaterial.Shader = groundShader;
            _groundOverlay.Material = _groundMaterial;
        }
        else
        {
            GD.PrintErr($"[EnvironmentEffectsLayer] 无法加载地面特效 shader: {GroundShaderPath}");
        }

        AddChild(_groundOverlay);
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        if (_weatherManager == null) return;

        float dt = (float)delta;

        var activeWeather = _weatherManager.GetActiveWeatherType();
        float effectiveIntensity = _weatherManager.GetEffectiveIntensity();

        // 更新天气粒子
        UpdateParticleOverlay(activeWeather, effectiveIntensity);

        // 检测天气变化 → 触发地面特效
        if (activeWeather != _lastWeatherType)
        {
            OnWeatherTypeChanged(_lastWeatherType, activeWeather);
            _lastWeatherType = activeWeather;
        }

        // 更新地面特效
        UpdateGroundOverlay(activeWeather, effectiveIntensity, dt);

        // 同步摄像机偏移到地面 shader
        UpdateCameraOffset();
    }

    // ========================================
    // 天气粒子更新
    // ========================================

    private void UpdateParticleOverlay(WeatherType weather, float intensity)
    {
        if (_particleMaterial == null) return;

        bool shouldShow = weather != WeatherType.Clear && intensity > 0.01f;
        _particleOverlay.Visible = shouldShow;

        if (!shouldShow) return;

        // 设置 shader uniform（粒子数量为编译期常量，不再通过 uniform 控制）
        _particleMaterial.SetShaderParameter("weather_type", (int)weather);
        _particleMaterial.SetShaderParameter("intensity", intensity);

        // 沙尘暴雾密度随强度变化
        if (weather == WeatherType.Sandstorm)
        {
            float fogDensity = _weatherManager!.CurrentIntensity switch
            {
                WeatherIntensity.Light => 0.15f,
                WeatherIntensity.Moderate => 0.3f,
                WeatherIntensity.Heavy => 0.5f,
                _ => 0.3f,
            };
            _particleMaterial.SetShaderParameter("sand_fog_density", fogDensity);
        }
    }

    // ========================================
    // 地面特效更新
    // ========================================

    private void OnWeatherTypeChanged(WeatherType oldWeather, WeatherType newWeather)
    {
        if (newWeather == WeatherType.Clear)
        {
            // 天气结束 → 地面特效渐出
            _groundFadingIn = false;
            _groundFadingOut = true;
        }
        else
        {
            // 新天气开始 → 延迟后地面特效渐入
            _groundDelayTimer = GroundEffectDelay;
            _groundEffectActive = true;
            _groundFadingIn = false;
            _groundFadingOut = false;
        }
    }

    private void UpdateGroundOverlay(WeatherType weather, float weatherIntensity, float dt)
    {
        if (_groundMaterial == null) return;

        // 延迟计时
        if (_groundEffectActive && _groundDelayTimer > 0.0f)
        {
            _groundDelayTimer -= dt;
            if (_groundDelayTimer <= 0.0f)
            {
                _groundFadingIn = true;
            }
        }

        // 渐入
        if (_groundFadingIn)
        {
            _groundIntensity += dt / GroundFadeInDuration;
            if (_groundIntensity >= 1.0f)
            {
                _groundIntensity = 1.0f;
                _groundFadingIn = false;
            }
        }

        // 渐出
        if (_groundFadingOut)
        {
            _groundIntensity -= dt / GroundFadeOutDuration;
            if (_groundIntensity <= 0.0f)
            {
                _groundIntensity = 0.0f;
                _groundFadingOut = false;
                _groundEffectActive = false;
            }
        }

        // 显示/隐藏
        bool shouldShow = _groundIntensity > 0.01f;
        _groundOverlay.Visible = shouldShow;

        if (!shouldShow) return;

        // 设置地面模式
        GroundEffectMode groundMode = weather switch
        {
            WeatherType.Rain => GroundEffectMode.Puddles,
            WeatherType.Sandstorm => GroundEffectMode.SandNoise,
            WeatherType.Snow => GroundEffectMode.SnowCover,
            _ => GroundEffectMode.Puddles,
        };

        _groundMaterial.SetShaderParameter("ground_mode", (int)groundMode);
        _groundMaterial.SetShaderParameter("effect_intensity", _groundIntensity * weatherIntensity);

        // 根据天气强度调整地面参数
        var intensity = _weatherManager!.CurrentIntensity;
        switch (groundMode)
        {
            case GroundEffectMode.Puddles:
                float coverage = intensity switch
                {
                    WeatherIntensity.Light => 0.2f,
                    WeatherIntensity.Moderate => 0.4f,
                    WeatherIntensity.Heavy => 0.65f,
                    _ => 0.4f,
                };
                _groundMaterial.SetShaderParameter("puddle_coverage", coverage);
                break;

            case GroundEffectMode.SnowCover:
                float snowCoverage = intensity switch
                {
                    WeatherIntensity.Light => 0.3f,
                    WeatherIntensity.Moderate => 0.6f,
                    WeatherIntensity.Heavy => 0.85f,
                    _ => 0.6f,
                };
                _groundMaterial.SetShaderParameter("snow_coverage", snowCoverage);
                break;

            case GroundEffectMode.SandNoise:
                float driftSpeed = intensity switch
                {
                    WeatherIntensity.Light => 0.1f,
                    WeatherIntensity.Moderate => 0.3f,
                    WeatherIntensity.Heavy => 0.6f,
                    _ => 0.3f,
                };
                _groundMaterial.SetShaderParameter("sand_drift_speed", driftSpeed);
                break;
        }
    }

    // ========================================
    // 摄像机同步
    // ========================================

    private void UpdateCameraOffset()
    {
        if (_camera == null || _groundMaterial == null) return;

        // 将摄像机世界位置传递给地面 shader，使纹理锚定在世界空间
        var camPos = _camera.GlobalPosition;
        _groundMaterial.SetShaderParameter("camera_offset", new Vector2(camPos.X, camPos.Y));
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 强制设置地面特效强度（调试用）
    /// </summary>
    public void SetGroundIntensity(float value)
    {
        _groundIntensity = Mathf.Clamp(value, 0.0f, 1.0f);
        _groundEffectActive = value > 0.0f;
        _groundFadingIn = false;
        _groundFadingOut = false;
    }

    /// <summary>
    /// 获取当前地面特效强度
    /// </summary>
    public float GetGroundIntensity() => _groundIntensity;
}
