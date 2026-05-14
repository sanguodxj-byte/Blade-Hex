// HexLayoutConfig.cs
// 六边形网格布局配置组件 — 指定 q/r 基向量，兼容任何平顶、尖顶或变形等距网格
using Godot;

namespace BladeHex.Map;

/// <summary>
/// 六边形网格布局配置 — 通过基向量定义轴向坐标到像素坐标的映射
/// </summary>
[GlobalClass]
public partial class HexLayoutConfig : Resource
{
    // ========================================
    // 纹理设置
    // ========================================

    [ExportGroup("Texture Settings")]
    [Export] public float TexWidth { get; set; } = 313.0f;
    [Export] public float TexHeight { get; set; } = 313.0f;

    // ========================================
    // 网格基向量 — 使用 offset 风格像素布局（视觉矩形）
    // 平顶六边形，偶数列不偏移，奇数列向下偏移半格
    // 实际计算在 AxialToPixel 中用 offset 公式，基向量仅作兼容保留
    // ========================================

    [ExportGroup("Grid Vectors")]
    [Export] public Vector2 QVector { get; set; } = new(234.0f, 0.0f);
    [Export] public Vector2 RVector { get; set; } = new(0.0f, 270.17f);

    /// <summary>六边形外径</summary>
    public float HexSize => 156.0f;

    // ========================================
    // 坐标转换 — offset 风格（视觉矩形，非菱形）
    // ========================================

    /// <summary>
    /// 将轴向坐标 (q, r) 转换为像素坐标（矩形布局）
    /// 使用 even-q offset 布局：奇数列向下偏移半格
    /// 这样地图在屏幕上呈矩形而非菱形
    /// </summary>
    public Vector2 AxialToPixel(int q, int r)
    {
        float size = HexSize;
        // 水平间距：每列 3/2 * size
        float x = size * 1.5f * q;
        // 垂直间距：每行 sqrt(3) * size，奇数列偏移半行
        float rowHeight = size * Mathf.Sqrt(3.0f);
        float y = rowHeight * r;
        // 奇数列向下偏移半格（even-q offset）
        if ((q & 1) != 0)
            y += rowHeight * 0.5f;
        return new Vector2(x, y);
    }

    /// <summary>
    /// 将像素坐标转换为分数轴向坐标 (用于鼠标点选/拾取)
    /// 对应 even-q offset 布局的逆变换
    /// </summary>
    public Vector2 PixelToFractionalAxial(float px, float py)
    {
        float size = HexSize;
        float rowHeight = size * Mathf.Sqrt(3.0f);

        // 先算 q（水平方向简单）
        float q = px / (size * 1.5f);
        int qRound = Mathf.RoundToInt(q);

        // 补偿奇数列偏移
        float adjustedY = py;
        if ((qRound & 1) != 0)
            adjustedY -= rowHeight * 0.5f;

        float r = adjustedY / rowHeight;
        return new Vector2(q, r);
    }
}
