// OverworldPropData.cs
// 大地图场景物体（prop）放置数据 — 纯 Core 层模型
// 岩石、树木、山脉剪影等 2D 精灵的位置和属性
using Godot;

namespace BladeHex.Map;

/// <summary>
/// 单个大地图 prop 的放置信息。
/// 由 OverworldPropScatter 生成，由 OverworldPropRenderer 渲染为 Sprite3D billboard。
/// </summary>
public sealed class OverworldPropData
{
    /// <summary>prop 资源 ID（对应 OverworldPropRegistry 中的贴图）</summary>
    public string PropId = "";

    /// <summary>所在 tile 的轴向坐标</summary>
    public Vector2I TileCoord;

    /// <summary>相对于 tile 中心的像素偏移（用于同一格内多个 prop 不重叠）</summary>
    public Vector2 PixelOffset;

    /// <summary>缩放倍率（0.7~1.3 随机变化增加自然感）</summary>
    public float Scale = 1.0f;

    /// <summary>是否水平翻转（增加变化）</summary>
    public bool FlipH = false;

    /// <summary>Y 轴排序偏移（用于前后遮挡）</summary>
    public float SortOffset = 0.0f;
}
