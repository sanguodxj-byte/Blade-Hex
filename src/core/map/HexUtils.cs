// HexUtils.cs
// 静态工具类，处理六边形网格数学逻辑 (Axial Coordinates, Flat-top)
// 迁移自 GDScript HexUtils.gd
using Godot;

namespace BladeHex.Map;

/// <summary>
/// 六边形网格静态数学工具 — Axial坐标系，平顶(Flat-top)朝向
/// </summary>
public static class HexUtils
{
    // 6个方向的偏移量 (Axial: q, r)
    public static readonly Vector2I[] Directions =
    [
        new(1, 0), new(1, -1), new(0, -1),
        new(-1, 0), new(-1, 1), new(0, 1),
    ];

    // 平顶六边形的布局常量
    public const float Size = 96.0f;
    public const float Width = 2.0f * Size;
    public static readonly float Height = Mathf.Sqrt(3.0f) * Size;
    public static readonly float HorizontalSpacing = Width * 0.75f;
    public static readonly float VerticalSpacing = Height;

    // ========================================
    // 坐标转换
    // ========================================

    /// <summary>轴向坐标转像素坐标 (2D)</summary>
    public static Vector2 AxialToPixel(int q, int r)
    {
        float x = Size * (3.0f / 2.0f * q);
        float y = Size * (Mathf.Sqrt(3.0f) / 2.0f * q + Mathf.Sqrt(3.0f) * r);
        return new Vector2(x, y);
    }

    /// <summary>轴向坐标转世界坐标 (3D HD-2D使用)</summary>
    public static Vector3 AxialToWorld3D(int q, int r, int elevationLevel = 1)
    {
        var pos2d = AxialToPixel(q, r);
        float heightStep = Size * 0.5f;
        return new Vector3(pos2d.X, elevationLevel * heightStep, pos2d.Y);
    }

    /// <summary>像素坐标转浮点轴向坐标</summary>
    public static Vector2 PixelToFractionalAxial(Vector2 pixel)
    {
        float q = (2.0f / 3.0f * pixel.X) / Size;
        float r = (-1.0f / 3.0f * pixel.X + Mathf.Sqrt(3.0f) / 3.0f * pixel.Y) / Size;
        return new Vector2(q, r);
    }

    /// <summary>舍入浮点轴向坐标到最近的整数坐标</summary>
    public static Vector2I HexRound(Vector2 frac)
    {
        float q = frac.X;
        float r = frac.Y;
        float s = -q - r;

        float rq = Mathf.Round(q);
        float rr = Mathf.Round(r);
        float rs = Mathf.Round(s);

        float qDiff = Mathf.Abs(rq - q);
        float rDiff = Mathf.Abs(rr - r);
        float sDiff = Mathf.Abs(rs - s);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;
        else
            rs = -rq - rr;

        return new Vector2I((int)rq, (int)rr);
    }

    // ========================================
    // 邻居与距离
    // ========================================

    /// <summary>获取邻居坐标</summary>
    public static Vector2I GetNeighbor(int q, int r, int direction)
    {
        var offset = Directions[direction % 6];
        return new Vector2I(q + offset.X, r + offset.Y);
    }

    /// <summary>计算两个六边形之间的距离</summary>
    public static int Distance(int q1, int r1, int q2, int r2)
    {
        return (Mathf.Abs(q1 - q2) + Mathf.Abs(q1 + r1 - q2 - r2) + Mathf.Abs(r1 - r2)) / 2;
    }

    /// <summary>获取所有相邻格子坐标</summary>
    public static Vector2I[] GetNeighbors(int q, int r)
    {
        var neighbors = new Vector2I[6];
        for (int i = 0; i < 6; i++)
            neighbors[i] = GetNeighbor(q, r, i);
        return neighbors;
    }
}
