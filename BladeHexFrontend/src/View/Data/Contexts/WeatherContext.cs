using Godot;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 天气快照 — 大地图天气切换战斗场景时携带的临时状态。
///
/// 不参与持久化（不存档）；战斗场景在初始化时读取一次，结束后无需写回。
/// </summary>
[GlobalClass]
public partial class WeatherContext : Resource
{
    /// <summary>当前天气类型：-1=Clear, 0=Rain, 1=Snow, 2=Sandstorm。</summary>
    [Export] public int Type { get; set; } = -1;

    /// <summary>天气强度 [0, 1]。</summary>
    [Export] public float Intensity { get; set; }
}
