// WeatherController.cs
// 大地图天气系统控制器 — 抽自 OverworldScene3D.Weather partial。
//
// 服务于架构优化 spec R5 — Sprint 6 场景控制器组件化。
//
// 抽取策略（step-by-step，每步独立验证）：
//   Step 1: ✅ 空 controller 文件，不接通运行（编译通过即可）
//   Step 2: ✅ 提供 CalculateGameplayFactors 静态纯函数 helper
//   Step 3: ✅ 提供 CalculateCloudParams + CalculateVisualParams 静态纯函数 helper
//   Step 4: 把字段（_weatherMgr 等）和初始化迁入 controller（待做）
//
// 当前阶段：仅暴露静态纯函数，partial 通过这些 helper 计算参数值，无任何状态。
using Godot;
using BladeHex.View.Environment;

namespace BladeHex.Scenes.Overworld.Components;

/// <summary>
/// 大地图天气控制器（Step 2 — 纯函数 helper）。
/// </summary>
[GlobalClass]
public partial class WeatherController : Node
{
    /// <summary>当前天气下三个游戏性修正因子的输出</summary>
    public readonly record struct GameplayFactors(float Speed, float Vision, float Encounter)
    {
        public static GameplayFactors Default { get; } = new(1.0f, 1.0f, 1.0f);
    }

    /// <summary>
    /// 根据天气类型 + 强度计算移速 / 视野 / 遭遇率三因子。
    /// 纯函数，无副作用，可独立单元测试。
    /// </summary>
    public static GameplayFactors CalculateGameplayFactors(WeatherType weather, float intensity)
    {
        float speed = weather switch
        {
            WeatherType.Rain => Mathf.Lerp(1.0f, 0.75f, intensity),
            WeatherType.Snow => Mathf.Lerp(1.0f, 0.65f, intensity),
            WeatherType.Sandstorm => Mathf.Lerp(1.0f, 0.50f, intensity),
            _ => 1.0f,
        };

        float vision = weather switch
        {
            WeatherType.Rain => Mathf.Lerp(1.0f, 0.70f, intensity),
            WeatherType.Snow => Mathf.Lerp(1.0f, 0.60f, intensity),
            WeatherType.Sandstorm => Mathf.Lerp(1.0f, 0.50f, intensity),
            _ => 1.0f,
        };

        float encounter = weather switch
        {
            WeatherType.Rain => Mathf.Lerp(1.0f, 0.75f, intensity),
            WeatherType.Snow => Mathf.Lerp(1.0f, 0.80f, intensity),
            WeatherType.Sandstorm => Mathf.Lerp(1.0f, 0.60f, intensity),
            _ => 1.0f,
        };

        return new GameplayFactors(speed, vision, encounter);
    }

    /// <summary>当前天气下云层和风系统的视觉参数</summary>
    public readonly record struct CloudParams(
        float Coverage,
        float Opacity,
        Color Color,
        float WindStrength,
        float WindAngle,
        float GustStrength,
        float GustPeriod);

    /// <summary>
    /// 根据天气类型计算云层和风系统的视觉参数。
    /// 纯函数，无副作用。
    /// </summary>
    public static CloudParams CalculateCloudParams(WeatherType weather) => weather switch
    {
        WeatherType.Rain => new CloudParams(
            Coverage: 0.7f, Opacity: 0.35f,
            Color: new Color(0.6f, 0.62f, 0.68f),
            WindStrength: 0.5f, WindAngle: 0.7f,
            GustStrength: 0.4f, GustPeriod: 5.0f),
        WeatherType.Snow => new CloudParams(
            Coverage: 0.6f, Opacity: 0.30f,
            Color: new Color(0.85f, 0.87f, 0.92f),
            WindStrength: 0.2f, WindAngle: 0.35f,
            GustStrength: 0.2f, GustPeriod: 10.0f),
        WeatherType.Sandstorm => new CloudParams(
            Coverage: 0.5f, Opacity: 0.25f,
            Color: new Color(0.8f, 0.7f, 0.5f),
            WindStrength: 1.0f, WindAngle: 0.9f,
            GustStrength: 0.5f, GustPeriod: 3.0f),
        _ => new CloudParams(  // Clear
            Coverage: 0.45f, Opacity: 0.35f,
            Color: new Color(0.95f, 0.95f, 1.0f),
            WindStrength: 0.3f, WindAngle: 0.4f,
            GustStrength: 0.3f, GustPeriod: 8.0f),
    };

    /// <summary>
    /// 当前天气下视觉光照修正参数（叠加在昼夜循环之上）。
    /// </summary>
    public readonly record struct VisualParams(Color Tint, float EnergyMod, float AmbientMod)
    {
        public static VisualParams Identity { get; } = new(Colors.White, 1.0f, 1.0f);
    }

    /// <summary>
    /// 根据天气类型 + 强度计算光照色调 + 强度修正。
    /// 纯函数，无副作用。
    /// </summary>
    public static VisualParams CalculateVisualParams(WeatherType weather, float intensity) => weather switch
    {
        WeatherType.Rain => new VisualParams(
            Tint: new Color(
                Mathf.Lerp(1.0f, 0.72f, intensity),
                Mathf.Lerp(1.0f, 0.75f, intensity),
                Mathf.Lerp(1.0f, 0.82f, intensity)),
            EnergyMod: Mathf.Lerp(1.0f, 0.65f, intensity),
            AmbientMod: Mathf.Lerp(1.0f, 0.75f, intensity)),
        WeatherType.Snow => new VisualParams(
            Tint: new Color(
                Mathf.Lerp(1.0f, 0.88f, intensity),
                Mathf.Lerp(1.0f, 0.92f, intensity),
                Mathf.Lerp(1.0f, 1.05f, intensity)),
            EnergyMod: Mathf.Lerp(1.0f, 0.75f, intensity),
            AmbientMod: Mathf.Lerp(1.0f, 0.85f, intensity)),
        WeatherType.Sandstorm => new VisualParams(
            Tint: new Color(
                Mathf.Lerp(1.0f, 1.1f, intensity),
                Mathf.Lerp(1.0f, 0.85f, intensity),
                Mathf.Lerp(1.0f, 0.55f, intensity)),
            EnergyMod: Mathf.Lerp(1.0f, 0.5f, intensity),
            AmbientMod: Mathf.Lerp(1.0f, 0.6f, intensity)),
        _ => VisualParams.Identity,
    };
}

