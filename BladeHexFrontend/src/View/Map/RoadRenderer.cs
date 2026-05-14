// RoadRenderer.cs
// 道路渲染器 — 从 chunk 的 IsRoad 瓦片数据追踪连续路径，贝塞尔平滑后用 Line2D 渲染
// 不依赖骨架数据，不做 MST — 道路已在世界构建 Stage 7.5 中标记到瓦片
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.View.Map;

/// <summary>
/// 道路渲染器 — 从已加载 chunk 的 IsRoad 瓦片追踪连续路径段，
/// 贝塞尔平滑后用 Line2D + 纹理渲染。
/// 随 chunk 加载/卸载动态更新。
/// </summary>
[GlobalClass]
public partial class RoadRenderer : Node2D
{
    // ========================================
    // 导出参数
    // ========================================

    [Export] public float RoadWidth { get; set; } = 32.0f;
    [Export] public Color RoadColor { get; set; } = new Color(0.45f, 0.35f, 0.2f, 1.0f);
    [Export] public string RoadTexturePath { get; set; } = "";
    [Export] public int CurveResolution { get; set; } = 6;

    // ========================================
    // 内部字段
    // ========================================

    private ChunkManager? _chunkManager;
    private Texture2D? _roadTexture;
    private readonly List<Line2D> _roadLines = new();

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(ChunkManager? chunkManager)
    {
        _chunkManager = chunkManager;
        if (!string.IsNullOrEmpty(RoadTexturePath))
            _roadTexture = GD.Load<Texture2D>(RoadTexturePath);
    }

    /// <summary>
    /// 从当前已加载的 chunk 中追踪所有道路段并渲染。
    /// 在 chunk 加载完成后调用。
    /// </summary>
    public void RebuildFromChunks()
    {
        RebuildFromAllKnownTiles();
    }

    /// <summary>
    /// 从所有已知 chunk 数据一次性重建全部道路渲染。
    /// 使用内存缓存中的全部 chunk 确保道路完整连续。
    /// </summary>
    public void RebuildFromAllKnownTiles()
    {
        ClearRoads();
        if (_chunkManager == null) return;

        // 收集所有 chunk 中的道路瓦片（内存缓存 = 全部世界数据）
        var roadTiles = new HashSet<Vector2I>();
        foreach (var kvp in _chunkManager.AllKnownChunks)
        {
            foreach (var tileKvp in kvp.Value.Tiles)
            {
                if (tileKvp.Value.IsRoad)
                    roadTiles.Add(tileKvp.Key);
            }
        }

        if (roadTiles.Count == 0) return;

        // 追踪连续道路段
        var segments = TraceRoadSegments(roadTiles);

        // 每段转像素坐标 → 贝塞尔平滑 → Line2D
        foreach (var segment in segments)
        {
            if (segment.Count < 2) continue;

            var pixelPoints = new List<Vector2>();
            foreach (var coord in segment)
                pixelPoints.Add(HexOverworldTile.AxialToPixel(coord.X, coord.Y));

            var smoothed = SmoothPath(pixelPoints);
            if (smoothed.Length > 1)
                CreateRoadLine(smoothed);
        }

        GD.Print($"[RoadRenderer] 渲染 {_roadLines.Count} 条道路段 (来自 {roadTiles.Count} 个道路瓦片)");
    }

    /// <summary>
    /// 增量更新：新 chunk 加载时，只添加新的道路段（不全量重建）
    /// </summary>
    public void OnNewChunksLoaded(List<ChunkData> newChunks)
    {
        if (_chunkManager == null) return;

        // 检查新 chunk 中是否有道路瓦片
        bool hasNewRoads = false;
        foreach (var chunk in newChunks)
        {
            foreach (var tileKvp in chunk.Tiles)
            {
                if (tileKvp.Value.IsRoad) { hasNewRoads = true; break; }
            }
            if (hasNewRoads) break;
        }

        // 有新道路时全量重建（保证连续性）
        if (hasNewRoads)
            RebuildFromAllKnownTiles();
    }

    // ========================================
    // 道路段追踪
    // ========================================

    /// <summary>
    /// 将散落的道路瓦片分组为连续路径段。
    /// 改进：分叉点不标记为 visited（允许多段共享），避免断裂。
    /// 每段从端点或分叉点开始，沿唯一方向追踪到下一个端点/分叉点。
    /// </summary>
    private List<List<Vector2I>> TraceRoadSegments(HashSet<Vector2I> roadTiles)
    {
        var segments = new List<List<Vector2I>>();
        var visitedEdges = new HashSet<(Vector2I, Vector2I)>(); // 已追踪的边（有向）

        // 找到所有端点（1邻居）和分叉点（3+邻居）
        var junctions = new HashSet<Vector2I>();
        var endpoints = new HashSet<Vector2I>();
        foreach (var coord in roadTiles)
        {
            int n = CountRoadNeighbors(coord, roadTiles);
            if (n == 1) endpoints.Add(coord);
            else if (n >= 3) junctions.Add(coord);
        }

        // 从每个端点和分叉点出发追踪
        var startPoints = new List<Vector2I>();
        startPoints.AddRange(endpoints);
        startPoints.AddRange(junctions);

        // 如果全是直线（无端点无分叉），随便选一个
        if (startPoints.Count == 0 && roadTiles.Count > 0)
        {
            foreach (var t in roadTiles) { startPoints.Add(t); break; }
        }

        foreach (var start in startPoints)
        {
            var neighbors = GetRoadNeighbors(start, roadTiles);
            foreach (var firstStep in neighbors)
            {
                // 检查这条边是否已追踪过
                if (visitedEdges.Contains((start, firstStep))) continue;

                var segment = new List<Vector2I> { start };
                var current = firstStep;
                var prev = start;

                while (true)
                {
                    visitedEdges.Add((prev, current));
                    visitedEdges.Add((current, prev)); // 双向标记
                    segment.Add(current);

                    // 到达端点或分叉点 → 段结束
                    if (endpoints.Contains(current) || junctions.Contains(current))
                        break;

                    // 找下一个（排除来路）
                    var next = GetNextRoadTile(current, prev, roadTiles);
                    if (next == null) break;

                    prev = current;
                    current = next.Value;
                }

                if (segment.Count >= 2)
                    segments.Add(segment);
            }
        }

        return segments;
    }

    private static int CountRoadNeighbors(Vector2I coord, HashSet<Vector2I> roadTiles)
    {
        int count = 0;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
            if (roadTiles.Contains(nb)) count++;
        }
        return count;
    }

    private static List<Vector2I> GetRoadNeighbors(Vector2I coord, HashSet<Vector2I> roadTiles)
    {
        var result = new List<Vector2I>();
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, d);
            if (roadTiles.Contains(nb)) result.Add(nb);
        }
        return result;
    }

    private static Vector2I? GetNextRoadTile(Vector2I current, Vector2I prev, HashSet<Vector2I> roadTiles)
    {
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(current.X, current.Y, d);
            if (nb == prev) continue;
            if (roadTiles.Contains(nb)) return nb;
        }
        return null;
    }

    // ========================================
    // 贝塞尔平滑
    // ========================================

    /// <summary>Catmull-Rom 样条平滑路径点</summary>
    private Vector2[] SmoothPath(List<Vector2> points)
    {
        if (points.Count <= 2) return points.ToArray();

        // 降采样（每 3 个取 1 个控制点）
        var ctrl = new List<Vector2> { points[0] };
        for (int i = 3; i < points.Count - 1; i += 3)
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

            // Catmull-Rom → 三次贝塞尔控制点
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

    private void CreateRoadLine(Vector2[] path)
    {
        var line = new Line2D();
        line.Width = RoadWidth;
        line.DefaultColor = RoadColor;
        line.JointMode = Line2D.LineJointMode.Round;
        line.BeginCapMode = Line2D.LineCapMode.Round;
        line.EndCapMode = Line2D.LineCapMode.Round;
        line.Antialiased = true;
        line.ZIndex = 1; // 在地形瓦片(Z=0)之上

        if (_roadTexture != null)
        {
            line.Texture = _roadTexture;
            line.TextureMode = Line2D.LineTextureMode.Tile;
        }

        line.Points = path;
        AddChild(line);
        _roadLines.Add(line);
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>当前渲染的道路段数量</summary>
    public int RoadCount => _roadLines.Count;

    public void ClearRoads()
    {
        foreach (var line in _roadLines)
            if (GodotObject.IsInstanceValid(line))
                line.QueueFree();
        _roadLines.Clear();
    }

    public void SetRoadTexture(Texture2D? texture)
    {
        _roadTexture = texture;
        foreach (var line in _roadLines)
            if (GodotObject.IsInstanceValid(line))
            {
                line.Texture = texture;
                line.TextureMode = texture != null ? Line2D.LineTextureMode.Tile : Line2D.LineTextureMode.None;
            }
    }

    public void SetRoadWidth(float width)
    {
        RoadWidth = width;
        foreach (var line in _roadLines)
            if (GodotObject.IsInstanceValid(line))
                line.Width = width;
    }

    /// <summary>
    /// 视觉回退：当 chunk 中没有道路瓦片数据时（旧存档），
    /// 用 MST 连接聚落并直接画贝塞尔曲线（不修改瓦片数据）。
    /// </summary>
    public void GenerateFallbackRoads(System.Collections.Generic.List<BladeHex.Strategic.OverworldPOI> pois)
    {
        ClearRoads();

        var settlements = new System.Collections.Generic.List<Vector2>();
        foreach (var poi in pois)
        {
            if (poi.PoiTypeEnum == BladeHex.Strategic.OverworldPOI.POIType.Town
                || poi.PoiTypeEnum == BladeHex.Strategic.OverworldPOI.POIType.Village
                || poi.PoiTypeEnum == BladeHex.Strategic.OverworldPOI.POIType.Castle)
            {
                settlements.Add(poi.Position);
            }
        }

        if (settlements.Count < 2) return;

        // Prim MST
        var inTree = new System.Collections.Generic.HashSet<int> { 0 };
        var candidates = new System.Collections.Generic.HashSet<int>();
        for (int i = 1; i < settlements.Count; i++) candidates.Add(i);

        while (candidates.Count > 0)
        {
            float bestDist = float.MaxValue;
            int bestFrom = -1, bestTo = -1;
            foreach (int from in inTree)
            {
                foreach (int to in candidates)
                {
                    float d = settlements[from].DistanceTo(settlements[to]);
                    if (d < bestDist) { bestDist = d; bestFrom = from; bestTo = to; }
                }
            }
            if (bestTo < 0) break;

            // 生成贝塞尔曲线
            var path = GenerateBezierDirect(settlements[bestFrom], settlements[bestTo]);
            if (path.Length > 1)
                CreateRoadLine(path);

            inTree.Add(bestTo);
            candidates.Remove(bestTo);
        }

        GD.Print($"[RoadRenderer] 回退模式: 生成 {_roadLines.Count} 条视觉道路");
    }

    /// <summary>两点间直接贝塞尔曲线（带随机弯曲）</summary>
    private static Vector2[] GenerateBezierDirect(Vector2 from, Vector2 to)
    {
        Vector2 mid = (from + to) * 0.5f;
        Vector2 dir = (to - from).Normalized();
        Vector2 perp = new Vector2(-dir.Y, dir.X);

        float hash = Mathf.Sin(from.X * 0.01f + to.Y * 0.013f) * 0.5f + 0.5f;
        float offset = (hash - 0.5f) * from.DistanceTo(to) * 0.15f;

        Vector2 ctrl1 = from.Lerp(mid, 0.33f) + perp * offset;
        Vector2 ctrl2 = from.Lerp(mid, 0.66f) - perp * offset * 0.5f;

        int steps = Mathf.Max(8, (int)(from.DistanceTo(to) / 80.0f));
        var result = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float u = 1.0f - t;
            result[i] = u * u * u * from + 3.0f * u * u * t * ctrl1 + 3.0f * u * t * t * ctrl2 + t * t * t * to;
        }
        return result;
    }
}
