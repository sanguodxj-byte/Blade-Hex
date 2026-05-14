// WeatherType.cs
// 天气类型枚举与天气数据定义
namespace BladeHex.View.Environment;

/// <summary>
/// 天气类型枚举
/// </summary>
public enum WeatherType
{
    /// <summary>晴天（无特效）</summary>
    Clear = -1,
    /// <summary>雨天</summary>
    Rain = 0,
    /// <summary>雪天</summary>
    Snow = 1,
    /// <summary>沙尘暴</summary>
    Sandstorm = 2,
}

/// <summary>
/// 地面特效模式（与 ground_effects.gdshader 的 ground_mode 对应）
/// </summary>
public enum GroundEffectMode
{
    /// <summary>积水（雨天）</summary>
    Puddles = 0,
    /// <summary>沙地噪声（沙尘暴）</summary>
    SandNoise = 1,
    /// <summary>雪覆盖</summary>
    SnowCover = 2,
}

/// <summary>
/// 天气强度等级
/// </summary>
public enum WeatherIntensity
{
    Light,
    Moderate,
    Heavy,
}
