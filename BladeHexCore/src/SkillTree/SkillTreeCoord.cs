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

    /// <summary>轴坐标 (q, r) → 像素坐标 (pointy-top)</summary>
    public Vector2 HexToPixel(int q, int r)
    {
        float fq = q;
        float fr = r;
        return new Vector2(
            HexSize * (Mathf.Sqrt(3.0f) * fq + Mathf.Sqrt(3.0f) / 2.0f * fr),
            HexSize * (1.5f * fr)
        );
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
}
