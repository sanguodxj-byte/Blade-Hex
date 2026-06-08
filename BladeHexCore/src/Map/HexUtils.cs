// HexUtils.cs
// 静态工具类，处理六边形网格数学逻辑 (Axial Coordinates, Flat-top)
using Godot;
using System.Collections.Generic;

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

    /// <summary>轴向坐标距离（曼哈顿距离的六边形版本）</summary>
    public static int AxialDistance(Vector2I a, Vector2I b)
    {
        int dq = a.X - b.X;
        int dr = a.Y - b.Y;
        return (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(dq + dr)) / 2;
    }

    /// <summary>获取以 (cq, cr) 为中心、半径为 radius 的六边形环上所有格子</summary>
    public static List<Vector2I> GetRing(int cq, int cr, int radius)
    {
        var results = new List<Vector2I>();
        if (radius <= 0) { results.Add(new Vector2I(cq, cr)); return results; }

        // 从 direction 4 的方向开始（标准六边形环遍历起点）
        int q = cq + Directions[4].X * radius;
        int r = cr + Directions[4].Y * radius;

        for (int side = 0; side < 6; side++)
        {
            for (int step = 0; step < radius; step++)
            {
                results.Add(new Vector2I(q, r));
                var nb = GetNeighbor(q, r, side);
                q = nb.X; r = nb.Y;
            }
        }
        return results;
    }

    /// <summary>
    /// 获取以 (cq, cr) 为中心、半径为 N 的六边形区域内所有格子（含中心、含边界）。
    /// 总数 = 1 + 3·N·(N+1)。N=0 返回单格。
    /// </summary>
    public static List<Vector2I> GetHexagonCoords(int cq, int cr, int n)
    {
        var results = new List<Vector2I>();
        for (int dq = -n; dq <= n; dq++)
        {
            int r1 = System.Math.Max(-n, -dq - n);
            int r2 = System.Math.Min(n, -dq + n);
            for (int dr = r1; dr <= r2; dr++)
            {
                results.Add(new Vector2I(cq + dq, cr + dr));
            }
        }
        return results;
    }

    /// <summary>同 GetHexagonCoords，中心为 (0, 0)</summary>
    public static List<Vector2I> GetHexagonCoords(int n) => GetHexagonCoords(0, 0, n);

    /// <summary>计算从 from 指向 to 的最接近的朝向方向 (0-5)</summary>
    public static int GetFacingDirection(Vector2I from, Vector2I to)
    {
        if (from == to) return 0;
        
        // 1. 如果是直接的邻居，直接返回方向
        Vector2I diff = to - from;
        for (int i = 0; i < 6; i++)
        {
            if (Directions[i] == diff)
                return i;
        }

        // 2. 如果不是邻居，转换为 2D 像素坐标计算最接近的方向
        Vector2 pFrom = AxialToPixel(from.X, from.Y);
        Vector2 pTo = AxialToPixel(to.X, to.Y);
        Vector2 dirVec = pTo - pFrom;
        
        int bestDir = 0;
        float maxDot = -999999f;
        for (int i = 0; i < 6; i++)
        {
            Vector2 standardVec = AxialToPixel(Directions[i].X, Directions[i].Y).Normalized();
            float dot = dirVec.Normalized().Dot(standardVec);
            if (dot > maxDot)
            {
                maxDot = dot;
                bestDir = i;
            }
        }
        return bestDir;
    }
}
