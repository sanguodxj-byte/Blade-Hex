// CombatSunLight.cs
// 战斗场景太阳光组件 — 根据当前时刻模拟太阳位置、角度、色温。
//
// 太阳轨迹:
//   - 6:00 日出:东方地平线(仰角 5°),暖橙色,低能量
//   - 12:00 正午:正南偏高(仰角 60°),白色,最大能量
//   - 18:00 日落:西方地平线(仰角 5°),暖红色,低能量
//   - 夜间:月光(仰角 30°,偏蓝,极低能量)
//
// 用法:挂到战斗场景,Initialize 时传入 DirectionalLight3D + 当前小时。
// 每帧调 Tick() 或一次性调 SetHour() 即可。
using Godot;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class CombatSunLight : Node
{
    private DirectionalLight3D? _light;
    private float _currentHour = 12f;

    // 太阳轨迹参数
    private const float SunriseHour = 6f;
    private const float SunsetHour = 18f;
    private const float NoonHour = 12f;

    // 能量范围
    private const float MaxEnergy = 0.85f;   // 正午
    private const float MinEnergy = 0.15f;   // 夜间(月光)
    private const float SunriseEnergy = 0.4f;

    // 仰角范围(度)
    private const float MaxElevation = 60f;  // 正午
    private const float HorizonElevation = 5f; // 日出/日落
    private const float NightElevation = 30f;  // 月光

    // 方位角:日出=东(90°) → 正午=南(180°) → 日落=西(270°)
    private const float SunriseAzimuth = 90f;
    private const float NoonAzimuth = 180f;
    private const float SunsetAzimuth = 270f;

    // 色温渐变
    private Gradient? _colorGradient;

    public void Initialize(DirectionalLight3D light, float hour)
    {
        _light = light;
        _currentHour = hour;
        BuildColorGradient();
        ApplyLighting();
    }

    /// <summary>设置时刻并立即更新光照(战斗场景通常不需要每帧更新)</summary>
    public void SetHour(float hour)
    {
        _currentHour = Mathf.Clamp(hour, 0f, 24f);
        ApplyLighting();
    }

    /// <summary>每帧更新(如果战斗中有时间流逝)</summary>
    public void Tick(float hour)
    {
        if (Mathf.Abs(hour - _currentHour) < 0.01f) return;
        _currentHour = hour;
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        if (_light == null || _colorGradient == null) return;

        bool isDaytime = _currentHour >= SunriseHour && _currentHour <= SunsetHour;

        float elevation, azimuth, energy;
        Color color;

        if (isDaytime)
        {
            // 白天:太阳从东方升起经正南到西方落下
            float dayProgress = (_currentHour - SunriseHour) / (SunsetHour - SunriseHour); // 0~1

            // 仰角:日出 5° → 正午 60° → 日落 5°(抛物线)
            elevation = HorizonElevation + (MaxElevation - HorizonElevation) * Mathf.Sin(dayProgress * Mathf.Pi);

            // 方位角:东 90° → 南 180° → 西 270°(线性)
            azimuth = Mathf.Lerp(SunriseAzimuth, SunsetAzimuth, dayProgress);

            // 能量:日出低 → 正午高 → 日落低(sin 曲线)
            energy = Mathf.Lerp(SunriseEnergy, MaxEnergy, Mathf.Sin(dayProgress * Mathf.Pi));

            // 色温:从渐变采样
            color = _colorGradient.Sample(dayProgress);
        }
        else
        {
            // 夜间:月光,固定方向偏高
            elevation = NightElevation;
            azimuth = 150f; // 偏东南
            energy = MinEnergy;
            color = new Color(0.4f, 0.45f, 0.6f); // 冷蓝月光
        }

        // 应用到 DirectionalLight3D
        // Godot 的 DirectionalLight3D 方向由 RotationDegrees 决定:
        // X = 仰角(负值=向下照), Y = 方位角
        _light.RotationDegrees = new Vector3(-elevation, azimuth, 0);
        _light.LightEnergy = energy;
        _light.LightColor = color;

        // 阴影设置:低角度时阴影更长更柔和
        _light.ShadowEnabled = true;
        _light.ShadowBias = elevation < 20f ? 0.05f : 0.02f;
    }

    private void BuildColorGradient()
    {
        // 白天色温渐变:日出暖橙 → 上午暖白 → 正午纯白 → 下午暖白 → 日落暖红
        _colorGradient = new Gradient
        {
            Offsets = new float[] { 0.0f, 0.15f, 0.35f, 0.65f, 0.85f, 1.0f },
            Colors = new Color[]
            {
                new(1.0f, 0.6f, 0.3f),   // 日出:暖橙
                new(1.0f, 0.85f, 0.7f),  // 早晨:暖白
                new(1.0f, 0.98f, 0.95f), // 正午:近纯白
                new(1.0f, 0.98f, 0.95f), // 下午:近纯白
                new(1.0f, 0.75f, 0.5f),  // 傍晚:暖黄
                new(0.95f, 0.5f, 0.3f),  // 日落:暖红
            },
        };
    }
}
