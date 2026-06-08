using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 技能盘坐标组件 — 六边形轴坐标(axial)与像素坐标的双向转换
/// 使用 pointy-top 六边形布局
/// </summary>
[GlobalClass]
public partial class SkillTreeCoord : RefCounted
{
    public float HexSize { get; set; } = 40.0f;

    public static readonly Vector2I[] HexDirections =
    [
        new(1, 0),    // E
        new(0, 1),    // SE
        new(-1, 1),   // SW
        new(-1, 0),   // W
        new(0, -1),   // NW
        new(1, -1),   // NE
    ];

    public static readonly Dictionary<int, Color> RegionColors = new()
    {
        { 0, new Color(0.86f, 0.20f, 0.18f) },   // NONE → 白
        { 1, new Color(0.86f, 0.20f, 0.18f) },   // STR → 红
        { 2, new Color(0.18f, 0.80f, 0.44f) },   // DEX → 绿
        { 3, new Color(0.80f, 0.65f, 0.20f) },   // CON → 土黄
        { 4, new Color(0.30f, 0.50f, 0.90f) },   // INT → 蓝
        { 5, new Color(0.70f, 0.85f, 0.30f) },   // WIS → 浅绿
        { 6, new Color(0.78f, 0.30f, 0.78f) },   // CHA → 紫
        { 7, new Color(0.60f, 0.60f, 0.60f) },   // TRANSITION → 灰
    };

    public static readonly Dictionary<int, float> NodeRadiusScale = new()
    {
        { 0, 0.55f },  // START
        { 3, 0.50f },  // KEYSTONE
        { 1, 0.42f },  // BIG
        { 2, 0.28f },  // SMALL
    };

    // ============================================================================
    // 坐标转换 — Pointy-Top 六边形
    // ============================================================================

    /// <summary>轴坐标 (q, r) → 像素坐标 (pointy-top: 顶点朝上下)</summary>
    public Vector2 HexToPixel(int q, int r)
    {
        float fq = q;
        float fr = r;
        // 标准 axial → pixel，然后旋转 -90° 使六边形变为 pointy-top
        float rawX = HexSize * (Mathf.Sqrt(3.0f) * fq + Mathf.Sqrt(3.0f) / 2.0f * fr);
        float rawY = HexSize * (1.5f * fr);
        // 旋转 -90°: (x, y) → (y, -x)
        return new Vector2(rawY, -rawX);
    }

    /// <summary>像素坐标 → 轴坐标（四舍五入到最近格子）</summary>
    public Vector2I PixelToHex(float px, float py)
    {
        float q = (Mathf.Sqrt(3.0f) / 3.0f * px - 1.0f / 3.0f * py) / HexSize;
        float r = (2.0f / 3.0f * py) / HexSize;
        return HexRound(q, r);
    }

    private static Vector2I HexRound(float fq, float fr)
    {
        float fs = -fq - fr;
        int rq = (int)Mathf.Round(fq);
        int rr = (int)Mathf.Round(fr);
        int rs = (int)Mathf.Round(fs);
        int dq = Math.Abs(rq) - (int)Mathf.Round(Mathf.Abs(fq));
        int dr = Math.Abs(rr) - (int)Mathf.Round(Mathf.Abs(fr));
        int ds = Math.Abs(rs) - (int)Mathf.Round(Mathf.Abs(fs));
        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;
        return new Vector2I(rq, rr);
    }

    // ============================================================================
    // 网格查询
    // ============================================================================

    /// <summary>获取6个邻居坐标</summary>
    public static Vector2I[] GetNeighbors(int q, int r)
    {
        var result = new Vector2I[6];
        for (int i = 0; i < 6; i++)
            result[i] = new Vector2I(q, r) + HexDirections[i];
        return result;
    }

    /// <summary>Cube距离</summary>
    public static int HexDistance(Vector2I a, Vector2I b)
    {
        int dq = Math.Abs(a.X - b.X);
        int dr = Math.Abs(a.Y - b.Y);
        int ds = Math.Abs((a.X + a.Y) - (b.X + b.Y));
        return Math.Max(Math.Max(dq, dr), ds);
    }

    /// <summary>到原点的距离</summary>
    public static int HexRing(Vector2I pos) => HexDistance(pos, Vector2I.Zero);

    // ============================================================================
    // 视觉属性
    // ============================================================================

    public float GetNodeRadius(int nodeType) =>
        HexSize * NodeRadiusScale.GetValueOrDefault(nodeType, 0.3f);

    public static Color GetRegionColor(int region) =>
        RegionColors.GetValueOrDefault(region, Colors.White);

    // ============================================================================
    // 位置生成器
    // ============================================================================

    /// <summary>在指定方向上偏移 ring 层</summary>
    public static Vector2I MakeRingPos(Vector2I direction, int ring) => direction * ring;

    /// <summary>在指定方向上偏移 ring 层，再加横向偏移 slot</summary>
    public static Vector2I MakeOffsetPos(int directionIdx, int ring, int slot)
    {
        var mainDir = HexDirections[directionIdx % 6];
        var cwDir = HexDirections[(directionIdx + 1) % 6];
        return mainDir * ring + cwDir * slot;
    }

    // ============================================================================
    // 三角形瓦片坐标系 (Triangle Tile / Face addressing)
    // ============================================================================
    //
    // 编码: GridPosition = Vector2I(q*2 + t, r)
    //   q, r = axial hex 格点坐标 (标识三角形所属的"格点对")
    //   t = 0: ▽ 三角形 (顶点 V(q,r), V(q+1,r), V(q,r+1))
    //   t = 1: ▲ 三角形 (顶点 V(q+1,r), V(q,r+1), V(q+1,r+1))
    //
    // 解码: q = X >> 1 (即 X/2 向下取整), t = X & 1, r = Y
    //
    // 邻接 (共享边, 每个三角形恰好 3 个邻居):
    //   ▽(q,r,0):
    //     ▲(q,  r,  1) — 共享边 V(q+1,r)-V(q,r+1)
    //     ▲(q-1,r,  1) — 共享边 V(q,r)-V(q,r+1)
    //     ▲(q,  r-1,1) — 共享边 V(q,r)-V(q+1,r)
    //   ▲(q,r,1):
    //     ▽(q,  r,  0) — 共享边 V(q+1,r)-V(q,r+1)
    //     ▽(q+1,r,  0) — 共享边 V(q+1,r)-V(q+1,r+1)
    //     ▽(q,  r+1,0) — 共享边 V(q,r+1)-V(q+1,r+1)
    // ============================================================================

    /// <summary>编码三角形瓦片坐标为 Vector2I</summary>
    public static Vector2I EncodeTile(int q, int r, int t) => new(q * 2 + t, r);

    /// <summary>从 Vector2I 解码三角形瓦片坐标</summary>
    public static (int q, int r, int t) DecodeTile(Vector2I encoded)
    {
        // 注意: C# 整除对负数向零取整，需要用位运算或 Math.DivRem
        int x = encoded.X;
        int t = ((x % 2) + 2) % 2; // 保证 t ∈ {0, 1}
        int q = (x - t) / 2;
        return (q, encoded.Y, t);
    }

    /// <summary>格点 V(q,r) 的像素坐标 (与 HexToPixel 相同)</summary>
    public Vector2 VertexToPixel(int q, int r) => HexToPixel(q, r);

    /// <summary>三角形瓦片的 3 个顶点像素坐标</summary>
    public Vector2[] TileVertices(Vector2I encoded)
    {
        var (q, r, t) = DecodeTile(encoded);
        if (t == 0)
            return new[] { VertexToPixel(q, r), VertexToPixel(q + 1, r), VertexToPixel(q, r + 1) };
        else
            return new[] { VertexToPixel(q + 1, r), VertexToPixel(q, r + 1), VertexToPixel(q + 1, r + 1) };
    }

    /// <summary>三角形瓦片重心像素坐标</summary>
    public Vector2 TileCentroid(Vector2I encoded)
    {
        var verts = TileVertices(encoded);
        return (verts[0] + verts[1] + verts[2]) / 3.0f;
    }

    /// <summary>获取三角形瓦片的 3 个几何邻居 (共享边)</summary>
    public static Vector2I[] GetTileNeighbors(Vector2I encoded)
    {
        var (q, r, t) = DecodeTile(encoded);
        if (t == 0)
        {
            return new[]
            {
                EncodeTile(q, r, 1),
                EncodeTile(q - 1, r, 1),
                EncodeTile(q, r - 1, 1),
            };
        }
        else
        {
            return new[]
            {
                EncodeTile(q, r, 0),
                EncodeTile(q + 1, r, 0),
                EncodeTile(q, r + 1, 0),
            };
        }
    }

    /// <summary>判断格点 (q,r) 是否在半径 R 的正六边形内</summary>
    public static bool IsVertexInsideHex(int q, int r, int radius)
    {
        int s = -q - r;
        return Math.Abs(q) <= radius && Math.Abs(r) <= radius && Math.Abs(s) <= radius;
    }

    /// <summary>判断三角形瓦片是否完全在半径 R 的六边形内 (3 个顶点都在内)</summary>
    public static bool IsTileInsideHex(Vector2I encoded, int radius)
    {
        var (q, r, t) = DecodeTile(encoded);
        if (t == 0)
            return IsVertexInsideHex(q, r, radius) && IsVertexInsideHex(q + 1, r, radius) && IsVertexInsideHex(q, r + 1, radius);
        else
            return IsVertexInsideHex(q + 1, r, radius) && IsVertexInsideHex(q, r + 1, radius) && IsVertexInsideHex(q + 1, r + 1, radius);
    }

    /// <summary>枚举半径 R 六边形内的所有三角形瓦片</summary>
    public static List<Vector2I> GetAllTiles(int radius)
    {
        var tiles = new List<Vector2I>();
        for (int q = -radius - 1; q <= radius; q++)
            for (int r = -radius - 1; r <= radius; r++)
                for (int t = 0; t <= 1; t++)
                {
                    var enc = EncodeTile(q, r, t);
                    if (IsTileInsideHex(enc, radius))
                        tiles.Add(enc);
                }
        return tiles;
    }
}
