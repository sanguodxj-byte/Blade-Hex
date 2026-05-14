// RiverRenderer.cs
// 河流渲染器 — 从 chunk 的 IsRiver 瓦片追踪连续路径，贝塞尔平滑后用 Line2D 渲染
// 叠加在底层河流瓦片之上，遮盖六边形锯齿边缘，提供自然流向感
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 河流渲染器 — 从已加载 chunk 的 IsRiver 瓦片追踪连续路径段，
/// 贝塞尔平滑后用 Line2D + 流动纹理渲染。
/// 宽度较大（80px），覆盖底层六边形瓦片边缘。
/// </summary>
[GlobalClass]
public partial class RiverRenderer : Node2D
{
    // ========================================
    // 导出参数
    // ========================================

    [Export] public float RiverWidth { get; set; } = 80.0f;
    [Export] public Color RiverColor { get; set; } = new Color(0.15f, 0.35f, 0.7f, 0.85f);
    [Export] public Color RiverEdgeColor { get; set; } = new Color(0.1f, 0.25f, 0.5f, 0.6f);
    [Export] public string RiverTexturePath { get; set; } = "";
    [Export] public int CurveResolution { get; set; } = 6;

    // ========================================
    // 内部字段
    // ========================================

    private ChunkManager? _chunkManager;
    private Texture2D? _riverTexture;
    private readonly List<Line2D> _riverLines = new();

    /// <summary>六边形 axial 邻居偏移</summary>
    private static readonly int[][] HexOffsets =
    [
        [1, 0], [0, 1], [-1, 1],
        [-1, 0], [0, -1], [1, -1]
    ];

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(ChunkManager? chunkManager)
    {
        _chunkManager = chunkManager;
        if (!string.IsNullOrEmpty(RiverTexturePath))
            _riverTexture = GD.Load<Texture2D>(RiverTexturePath);
    }

    /// <summary>从当前已加载 chunk 追踪河流并渲染</summary>
    public void RebuildFromChunks()
    {
        ClearRivers();
        if (_chunkManager == null) return;

        // 收集所有已加载 chunk 中的河流瓦片
        var riverTiles = new HashSet<Vector2I>();
        foreach (var kvp in _chunkManager.ActiveChunks)
        {
            foreach (var tileKvp in kvp.Value.Tiles)
            {
                if (tileKvp.Value.IsRiver)
                    riverTiles.Add(tileKvp.Key);
            }
        }

        if (riverTiles.Count == 0) return;

        // 追踪连续河流段
        var segments = TraceRiverSegments(riverTiles);

        // 每段平滑并渲染
        foreach (var segment in segments)
        {
            if (segment.Count < 2) continue;

            var pixelPoints = new List<Vector2>();
            foreach (var coord in segment)
                pixelPoints.Add(HexOverworldTile.AxialToPixel(coord.X, coord.Y));

            var smoothed = SmoothPath(pixelPoints);
            if (smoothed.Length > 1)
                CreateRiverLine(smoothed, segment.Count);
        }

        GD.Print($"[RiverRenderer] 渲染 {_riverLines.Count} 条河流段 (来自 {riverTiles.Count} 个河流瓦片)");
    }

    // ========================================
    // 河流段追踪
    // ========================================

    private List<List<Vector2I>> TraceRiverSegments(HashSet<Vector2I> riverTiles)
    {
        var segments = new List<List<Vector2I>>();
        var visited = new HashSet<Vector2I>();

        // 找端点（1 个河流邻居）作为追踪起点
        var startPoints = new List<Vector2I>();
        foreach (var coord in riverTiles)
        {
            int nbCount = CountRiverNeighbors(coord, riverTiles);
            if (nbCount == 1 || nbCount >= 3)
                startPoints.Add(coord);
        }

        if (startPoints.Count == 0 && riverTiles.Count > 0)
        {
            var enumerator = riverTiles.GetEnumerator();
            enumerator.MoveNext();
            startPoints.Add(enumerator.Current);
        }

        foreach (var start in startPoints)
        {
            if (visited.Contains(start)) continue;

            var neighbors = GetRiverNeighbors(start, riverTiles);
            foreach (var firstStep in neighbors)
            {
                if (visited.Contains(firstStep) && visited.Contains(start)) continue;

                var segment = new List<Vector2I> { start };
                visited.Add(start);

                var current = firstStep;
                var prev = start;

                while (riverTiles.Contains(current) && !visited.Contains(current))
                {
                    segment.Add(current);
                    visited.Add(current);

                    var next = GetNextRiverTile(current, prev, riverTiles);
                    if (next == null) break;
                    prev = current;
                    current = next.Value;
                }

                if (riverTiles.Contains(current) && segment.Count > 1 && current != segment[^1])
                    segment.Add(current);

                if (segment.Count >= 2)
                    segments.Add(segment);
            }
        }

        // 处理未追踪的环路
        foreach (var coord in riverTiles)
        {
            if (visited.Contains(coord)) continue;

            var segment = new List<Vector2I>();
            var current = coord;
            Vector2I? prev = null;

            while (!visited.Contains(current) && riverTiles.Contains(current))
            {
                segment.Add(current);
                visited.Add(current);
                var next = GetNextRiverTile(current, prev ?? current, riverTiles);
                prev = current;
                current = next ?? current;
                if (next == null) break;
            }

            if (segment.Count >= 2)
                segments.Add(segment);
        }

        return segments;
    }

    private static int CountRiverNeighbors(Vector2I coord, HashSet<Vector2I> riverTiles)
    {
        int count = 0;
        for (int d = 0; d < 6; d++)
        {
            var nb = new Vector2I(coord.X + HexOffsets[d][0], coord.Y + HexOffsets[d][1]);
            if (riverTiles.Contains(nb)) count++;
        }
        return count;
    }

    private static List<Vector2I> GetRiverNeighbors(Vector2I coord, HashSet<Vector2I> riverTiles)
    {
        var result = new List<Vector2I>();
        for (int d = 0; d < 6; d++)
        {
            var nb = new Vector2I(coord.X + HexOffsets[d][0], coord.Y + HexOffsets[d][1]);
            if (riverTiles.Contains(nb)) result.Add(nb);
        }
        return result;
    }

    private static Vector2I? GetNextRiverTile(Vector2I current, Vector2I prev, HashSet<Vector2I> riverTiles)
    {
        for (int d = 0; d < 6; d++)
        {
            var nb = new Vector2I(current.X + HexOffsets[d][0], current.Y + HexOffsets[d][1]);
            if (nb == prev) continue;
            if (riverTiles.Contains(nb)) return nb;
        }
        return null;
    }

    // ========================================
    // 贝塞尔平滑
    // ========================================

    private Vector2[] SmoothPath(List<Vector2> points)
    {
        if (points.Count <= 2) return points.ToArray();

        // 降采样（每 2 个取 1 个控制点 — 河流比道路更密，少降采样保持弯曲）
        var ctrl = new List<Vector2> { points[0] };
        for (int i = 2; i < points.Count - 1; i += 2)
            ctrl.Add(points[i]);
        ctrl.Add(points[^1]);

        if (ctrl.Count <= 2) return ctrl.ToArray();

        var result = new List<Vector2>();
        for (int i = 0; i < ctrl.Count - 1; i++)
        {
            Vector2 p0 = i > 0 ? ctrl[i - 1] : ctrl[i];
            Vector2 p1 = ctrl[i];
            Vector2 p2 = ctrl[i + 1];
            Vector2 p3 = i + 2 < ctrl.Count ? ctrl[i + 2] : ctrl[i + 1];

            Vector2 b0 = p1;
            Vector2 b1 = p1 + (p2 - p0) / 6.0f;
            Vector2 b2 = p2 - (p3 - p1) / 6.0f;
            Vector2 b3 = p2;

            for (int s = 0; s < CurveResolution; s++)
            {
                float t = (float)s / CurveResolution;
                result.Add(CubicBezier(b0, b1, b2, b3, t));
            }
        }
        result.Add(ctrl[^1]);
        return result.ToArray();
    }

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1.0f - t;
        return u * u * u * p0 + 3.0f * u * u * t * p1 + 3.0f * u * t * t * p2 + t * t * t * p3;
    }

    // ========================================
    // Line2D 渲染
    // ========================================

    private void CreateRiverLine(Vector2[] path, int tileCount)
    {
        // 主河流线（宽，半透明蓝）
        var line = new Line2D();
        line.Width = RiverWidth;
        line.DefaultColor = RiverColor;
        line.JointMode = Line2D.LineJointMode.Round;
        line.BeginCapMode = Line2D.LineCapMode.Round;
        line.EndCapMode = Line2D.LineCapMode.Round;
        line.Antialiased = true;
        line.ZIndex = 0; // 与地形同层（河流瓦片已渲染，曲线覆盖边缘）

        if (_riverTexture != null)
        {
            line.Texture = _riverTexture;
            line.TextureMode = Line2D.LineTextureMode.Tile;
        }

        line.Points = path;
        AddChild(line);
        _riverLines.Add(line);

        // 边缘高光线（更宽、更透明，模拟河岸过渡）
        var edgeLine = new Line2D();
        edgeLine.Width = RiverWidth * 1.4f;
        edgeLine.DefaultColor = RiverEdgeColor;
        edgeLine.JointMode = Line2D.LineJointMode.Round;
        edgeLine.BeginCapMode = Line2D.LineCapMode.Round;
        edgeLine.EndCapMode = Line2D.LineCapMode.Round;
        edgeLine.Antialiased = true;
        edgeLine.ZIndex = -1; // 在主河流线之下

        edgeLine.Points = path;
        AddChild(edgeLine);
        _riverLines.Add(edgeLine);
    }

    // ========================================
    // 公共 API
    // ========================================

    public void ClearRivers()
    {
        foreach (var line in _riverLines)
            if (GodotObject.IsInstanceValid(line))
                line.QueueFree();
        _riverLines.Clear();
    }

    public void SetRiverTexture(Texture2D? texture)
    {
        _riverTexture = texture;
        foreach (var line in _riverLines)
            if (GodotObject.IsInstanceValid(line))
            {
                line.Texture = texture;
                line.TextureMode = texture != null ? Line2D.LineTextureMode.Tile : Line2D.LineTextureMode.None;
            }
    }
}
