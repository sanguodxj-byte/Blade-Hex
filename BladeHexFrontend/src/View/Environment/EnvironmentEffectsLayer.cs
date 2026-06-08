using BladeHex.View.AssetSystem;
using Godot;

namespace BladeHex.View.Environment;

[GlobalClass]
public partial class EnvironmentEffectsLayer : CanvasLayer
{
    [Export] public string ParticleShaderPath { get; set; } = "res://BladeHexFrontend/src/assets/shaders/weather_particles.gdshader";
    [Export] public string GroundShaderPath { get; set; } = "res://BladeHexFrontend/src/assets/shaders/ground_effects.gdshader";
    [Export] public float GroundEffectDelay { get; set; } = 5.0f;
    [Export] public float GroundFadeInDuration { get; set; } = 8.0f;
    [Export] public float GroundFadeOutDuration { get; set; } = 12.0f;

    private ColorRect _particleOverlay = null!;
    private ColorRect _groundOverlay = null!;
    private ShaderMaterial? _particleMaterial;
    private ShaderMaterial? _groundMaterial;

    private WeatherManager? _weatherManager;
    private Camera2D? _camera;

    private bool _groundEffectActive;
    private float _groundDelayTimer;
    private float _groundIntensity;
    private bool _groundFadingIn;
    private bool _groundFadingOut;
    private WeatherType _lastWeatherType = WeatherType.Clear;

    public void Initialize(WeatherManager weatherManager, Camera2D camera)
    {
        _weatherManager = weatherManager;
        _camera = camera;
    }

    public override void _Ready()
    {
        Layer = 5;
        FollowViewportEnabled = true;

        _particleOverlay = CreateOverlay("ParticleOverlay");
        _particleMaterial = CreateMaterial("weather_particles", ParticleShaderPath);
        if (_particleMaterial != null)
            _particleOverlay.Material = _particleMaterial;
        AddChild(_particleOverlay);

        _groundOverlay = CreateOverlay("GroundOverlay");
        _groundMaterial = CreateMaterial("ground_effects", GroundShaderPath);
        if (_groundMaterial != null)
            _groundOverlay.Material = _groundMaterial;
        AddChild(_groundOverlay);
    }

    public override void _Process(double delta)
    {
        if (_weatherManager == null)
            return;

        float dt = (float)delta;
        var activeWeather = _weatherManager.GetActiveWeatherType();
        float effectiveIntensity = _weatherManager.GetEffectiveIntensity();

        UpdateParticleOverlay(activeWeather, effectiveIntensity);

        if (activeWeather != _lastWeatherType)
        {
            OnWeatherTypeChanged(activeWeather);
            _lastWeatherType = activeWeather;
        }

        UpdateGroundOverlay(activeWeather, effectiveIntensity, dt);
        UpdateCameraOffset();
    }

    public void SetGroundIntensity(float value)
    {
        _groundIntensity = Mathf.Clamp(value, 0.0f, 1.0f);
        _groundEffectActive = value > 0.0f;
        _groundFadingIn = false;
        _groundFadingOut = false;
    }

    public float GetGroundIntensity() => _groundIntensity;

    private static ColorRect CreateOverlay(string name)
    {
        var rect = new ColorRect
        {
            Name = name,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return rect;
    }

    private static ShaderMaterial? CreateMaterial(string shaderId, string shaderPath)
    {
        var shader = ShaderAssetResolver.Load(shaderId, shaderPath);
        if (shader == null)
        {
            GD.PrintErr($"[EnvironmentEffectsLayer] Failed to load shader: {shaderPath}");
            return null;
        }

        return new ShaderMaterial { Shader = shader };
    }

    private void UpdateParticleOverlay(WeatherType weather, float intensity)
    {
        if (_particleMaterial == null)
            return;

        bool shouldShow = weather != WeatherType.Clear && intensity > 0.01f;
        _particleOverlay.Visible = shouldShow;
        if (!shouldShow)
            return;

        _particleMaterial.SetShaderParameter("weather_type", (int)weather);
        _particleMaterial.SetShaderParameter("intensity", intensity);

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

    private void OnWeatherTypeChanged(WeatherType newWeather)
    {
        if (newWeather == WeatherType.Clear)
        {
            _groundFadingIn = false;
            _groundFadingOut = true;
            return;
        }

        _groundDelayTimer = GroundEffectDelay;
        _groundEffectActive = true;
        _groundFadingIn = false;
        _groundFadingOut = false;
    }

    private void UpdateGroundOverlay(WeatherType weather, float weatherIntensity, float dt)
    {
        if (_groundMaterial == null)
            return;

        if (_groundEffectActive && _groundDelayTimer > 0.0f)
        {
            _groundDelayTimer -= dt;
            if (_groundDelayTimer <= 0.0f)
                _groundFadingIn = true;
        }

        if (_groundFadingIn)
        {
            _groundIntensity += dt / GroundFadeInDuration;
            if (_groundIntensity >= 1.0f)
            {
                _groundIntensity = 1.0f;
                _groundFadingIn = false;
            }
        }

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

        bool shouldShow = _groundIntensity > 0.01f;
        _groundOverlay.Visible = shouldShow;
        if (!shouldShow)
            return;

        GroundEffectMode groundMode = weather switch
        {
            WeatherType.Rain => GroundEffectMode.Puddles,
            WeatherType.Sandstorm => GroundEffectMode.SandNoise,
            WeatherType.Snow => GroundEffectMode.SnowCover,
            _ => GroundEffectMode.Puddles,
        };

        _groundMaterial.SetShaderParameter("ground_mode", (int)groundMode);
        _groundMaterial.SetShaderParameter("effect_intensity", _groundIntensity * weatherIntensity);
        ApplyGroundModeParameters(groundMode);
    }

    private void ApplyGroundModeParameters(GroundEffectMode groundMode)
    {
        var intensity = _weatherManager!.CurrentIntensity;
        switch (groundMode)
        {
            case GroundEffectMode.Puddles:
                float puddleCoverage = intensity switch
                {
                    WeatherIntensity.Light => 0.2f,
                    WeatherIntensity.Moderate => 0.4f,
                    WeatherIntensity.Heavy => 0.65f,
                    _ => 0.4f,
                };
                _groundMaterial!.SetShaderParameter("puddle_coverage", puddleCoverage);
                break;

            case GroundEffectMode.SnowCover:
                float snowCoverage = intensity switch
                {
                    WeatherIntensity.Light => 0.3f,
                    WeatherIntensity.Moderate => 0.6f,
                    WeatherIntensity.Heavy => 0.85f,
                    _ => 0.6f,
                };
                _groundMaterial!.SetShaderParameter("snow_coverage", snowCoverage);
                break;

            case GroundEffectMode.SandNoise:
                float driftSpeed = intensity switch
                {
                    WeatherIntensity.Light => 0.1f,
                    WeatherIntensity.Moderate => 0.3f,
                    WeatherIntensity.Heavy => 0.6f,
                    _ => 0.3f,
                };
                _groundMaterial!.SetShaderParameter("sand_drift_speed", driftSpeed);
                break;
        }
    }

    private void UpdateCameraOffset()
    {
        if (_camera == null || _groundMaterial == null)
            return;

        _groundMaterial.SetShaderParameter("camera_offset", _camera.GlobalPosition);
    }
}
