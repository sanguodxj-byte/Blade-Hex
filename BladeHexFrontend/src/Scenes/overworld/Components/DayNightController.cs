// DayNightController.cs
// 大地图昼夜循环控制器 — 通过 DirectionalLight 色温 + 环境光调整。
//
// 职责：
//   - 维护一天 24 小时的颜色渐变
//   - 每帧从 EconomyMgr.CurrentHour 采样色温
//   - 暴露 BaseSunEnergy / BaseAmbientEnergy / BaseSun/AmbientColor 给 WeatherController 叠加修正
//
// 服务于架构优化 spec R5 — Sprint 6 场景控制器组件化。
using Godot;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld.Components;

[GlobalClass]
public partial class DayNightController : Node
{
    // ========================================
    // 引用（通过 Initialize 注入）
    // ========================================

    private DirectionalLight3D? _sunLight;
    private Godot.Environment? _worldEnv;
    private EconomyManager? _economy;
    private Gradient? _timeGradient;

    // ========================================
    // 暴露给天气叠加层
    // ========================================

    /// <summary>当前帧的基础太阳光强度（天气未叠加前）</summary>
    public float BaseSunEnergy { get; private set; } = 0.9f;

    /// <summary>当前帧的基础环境光强度（天气未叠加前）</summary>
    public float BaseAmbientEnergy { get; private set; } = 0.5f;

    /// <summary>当前帧的基础太阳光色温（天气未叠加前）</summary>
    public Color BaseSunColor { get; private set; } = Colors.White;

    /// <summary>当前帧的基础环境光色温（天气未叠加前）</summary>
    public Color BaseAmbientColor { get; private set; } = new(0.65f, 0.65f, 0.70f);

    // ========================================
    // 初始化与每帧更新
    // ========================================

    /// <summary>由 OverworldScene3D 在 _Ready 阶段调用，注入依赖</summary>
    public void Initialize(DirectionalLight3D sunLight, Godot.Environment worldEnv, EconomyManager economy)
    {
        _sunLight = sunLight;
        _worldEnv = worldEnv;
        _economy = economy;
        BuildGradient();
    }

    /// <summary>由 OverworldScene3D._Process 调用，更新光照</summary>
    public void Tick()
    {
        if (_sunLight == null || _worldEnv == null || _economy == null || _timeGradient == null)
            return;

        float timeRatio = _economy.CurrentHour / 24.0f;
        Color tint = _timeGradient.Sample(timeRatio);

        // 能量范围：夜间最低 0.5（而非 0.3），保证地图始终可读
        BaseSunEnergy = Mathf.Lerp(0.5f, 1.0f, tint.Luminance);
        BaseAmbientEnergy = Mathf.Lerp(0.35f, 0.55f, tint.Luminance);
        BaseSunColor = tint;
        BaseAmbientColor = tint * 0.7f;

        _sunLight.LightColor = BaseSunColor;
        _sunLight.LightEnergy = BaseSunEnergy;
        _worldEnv.AmbientLightColor = BaseAmbientColor;
        _worldEnv.AmbientLightEnergy = BaseAmbientEnergy;
    }

    // ========================================
    // 内部
    // ========================================

    private void BuildGradient()
    {
        // 创建昼夜渐变 — 骑砍风格：夜间偏蓝但仍清晰可辨
        _timeGradient = new Gradient
        {
            Offsets = new float[]
            {
                0.00f, 0.20f, 0.25f, 0.30f, 0.42f, 0.70f, 0.75f, 0.80f, 0.85f, 1.00f,
            },
            Colors = new Color[]
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
            },
        };
    }
}
