// OverworldScene3D.DayNight.cs
// 昼夜循环 — 通过 DirectionalLight 色温 + 环境光调整
using Godot;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 昼夜循环
    // ========================================

    private DirectionalLight3D? _sunLight;
    private Godot.Environment? _worldEnv;
    private Gradient? _timeGradient;

    private void SetupDayNightCycle()
    {
        // 创建昼夜渐变 — 骑砍风格：夜间偏蓝但仍清晰可辨
        _timeGradient = new Gradient();
        _timeGradient.Offsets = new float[]
        {
            0.00f, 0.20f, 0.25f, 0.30f, 0.42f, 0.70f, 0.75f, 0.80f, 0.85f, 1.00f,
        };
        _timeGradient.Colors = new Color[]
        {
            new(0.35f, 0.35f, 0.50f), // 00:00 午夜 — 蓝灰，仍可辨
            new(0.38f, 0.38f, 0.52f), // 05:00 黎明前
            new(0.65f, 0.50f, 0.40f), // 06:00 日出
            new(0.92f, 0.87f, 0.78f), // 07:00 早晨
            new(1.00f, 1.00f, 0.98f), // 10:00 白昼
            new(0.92f, 0.72f, 0.52f), // 17:00 黄昏
            new(0.72f, 0.48f, 0.38f), // 18:00 日落
            new(0.50f, 0.40f, 0.45f), // 19:00 暮色
            new(0.40f, 0.37f, 0.48f), // 20:00 入夜
            new(0.35f, 0.35f, 0.50f), // 24:00 午夜
        };
    }

    private void UpdateDayNightCycle()
    {
        if (_sunLight == null || _worldEnv == null || EconomyMgr == null || _timeGradient == null)
            return;

        float timeRatio = EconomyMgr.CurrentHour / 24.0f;
        Color tint = _timeGradient.Sample(timeRatio);

        // 能量范围提升：夜间最低 0.5（而非 0.3），保证地图始终可读
        _baseSunEnergy = Mathf.Lerp(0.5f, 1.0f, tint.Luminance);
        _baseAmbientEnergy = Mathf.Lerp(0.35f, 0.55f, tint.Luminance);
        _baseSunColor = tint;
        _baseAmbientColor = tint * 0.7f;

        _sunLight.LightColor = _baseSunColor;
        _sunLight.LightEnergy = _baseSunEnergy;
        _worldEnv.AmbientLightColor = _baseAmbientColor;
        _worldEnv.AmbientLightEnergy = _baseAmbientEnergy;
    }

    // 基础光照值（天气叠加前）
    private float _baseSunEnergy = 0.9f;
    private float _baseAmbientEnergy = 0.5f;
    private Color _baseSunColor = Colors.White;
    private Color _baseAmbientColor = new Color(0.65f, 0.65f, 0.70f);
}
