// HexLayoutConfig.cs
// 六边形网格布局配置组件 — 指定 q/r 基向量，兼容任何平顶、尖顶或变形等距网格
// 迁移自 GDScript HexLayoutConfig.gd
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
    // 网格基向量
    // ========================================

    [ExportGroup("Grid Vectors")]
    [Export] public Vector2 QVector { get; set; } = new(-136.00f, -175.07f);
    [Export] public Vector2 RVector { get; set; } = new(-267.75f, -0.53f);

    // ========================================
    // 坐标转换
    // ========================================

    /// <summary>
    /// 将轴向坐标 (q, r) 转换为像素坐标
    /// </summary>
    public Vector2 AxialToPixel(int q, int r)
    {
        return new Vector2(
            QVector.X * q + RVector.X * r,
            QVector.Y * q + RVector.Y * r
        );
    }

    /// <summary>
    /// 将像素坐标转换为分数轴向坐标 (用于鼠标点选/拾取)
    /// 使用 2x2 逆矩阵求解
    /// </summary>
    public Vector2 PixelToFractionalAxial(float px, float py)
    {
        float det = QVector.X * RVector.Y - QVector.Y * RVector.X;
        if (det == 0.0f)
        {
            GD.PushWarning("HexLayoutConfig: 行列式为0，无效的基向量。");
            return Vector2.Zero;
        }

        float invDet = 1.0f / det;
        float q = (RVector.Y * px - RVector.X * py) * invDet;
        float r = (-QVector.Y * px + QVector.X * py) * invDet;
        return new Vector2(q, r);
    }
}
