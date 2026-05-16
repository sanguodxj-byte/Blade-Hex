// OverworldScene3D.Roads.cs
// 道路 3D 渲染 — Curve3D.SampleBaked() + SurfaceTool 连续条带
// 利用 Godot 内置贝塞尔曲线插值生成平滑道路，按 chunk 动态加载
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 道路渲染配置
    // ========================================

    private const float RoadWidth = 0.30f;
    private const float RoadY = 0.06f;
    private const float CurveBakeInterval = 0.2f; // Curve3D 烘焙精度（越小越平滑）

    /// <summary>已渲染道路的 chunk 坐标集合</summary>
    private readonly HashSet<Vector2I> _renderedRoadChunks = new();

    /// <summary>所有道路 mesh 节点</summary>
    private readonly List<MeshInstance3D> _roadMeshes = new();

    /// <summary>全局道路 tile 索引（跨 chunk 查询邻居）</summary>
    private readonly Dictionary<Vector2I, HexOverworldTile> _allRoadTiles = new();

    // ========================================
    // 公共入口
    // ========================================

    /// <summary>初始化时渲染已加载 chunk 的道路</summary>
    private void RenderRoadsAndRivers()
    {
        if (_chunkManager == null)
        {
            // 非 chunk 模式
            var allTiles = new List<HexOverworldTile>();
            foreach (var tile in _grid.GetRoadTiles())
                allTiles.Add(tile);
            IndexRoadTiles(allTiles);
            BuildAndAddRoadMesh(allTiles);
            GD.Print($"[OverworldScene3D] 道路: {allTiles.Count} tiles (Curve3D+SurfaceTool)");
            return;
        }

        // Chunk 模式
        foreach (var kvp in _chunkManager.ActiveChunks)
            LoadRoadsForChunk(kvp.Key, kvp.Value);

        var gen = _chunkManager.Generator;
        if (gen != null)
        {
            int cw = gen.WorldWidth / ChunkData.ChunkSize;
            int ch = gen.WorldHeight / ChunkData.ChunkSize;
            for (int cq = 0; cq < cw; cq++)
                for (int cr = 0; cr < ch; cr++)
                {
                    var coord = new Vector2I(cq, cr);
                    if (_renderedRoadChunks.Contains(coord)) continue;
                    if (_chunkManager.TryGetFromCache(coord, out var chunk))
                        LoadRoadsForChunk(coord, chunk);
                }
        }

        GD.Print($"[OverworldScene3D] 道路: {_allRoadTiles.Count} tiles, {_renderedRoadChunks.Count} chunks");
    }

    /// <summary>新 chunk 加载时追加道路</summary>
    private void OnNewChunkRoads(ChunkData chunk, Vector2I chunkCoord)
    {
        if (_renderedRoadChunks.Contains(chunkCoord)) return;
        LoadRoadsForChunk(chunkCoord, chunk);
    }

    // ========================================
    // Chunk 加载
    // ========================================

    private void LoadRoadsForChunk(Vector2I chunkCoord, ChunkData chunk)
    {
        if (_renderedRoadChunks.Contains(chunkCoord)) return;
        _renderedRoadChunks.Add(chunkCoord);

        var chunkRoadTiles = new List<HexOverworldTile>();
        foreach (var tile in chunk.Tiles.Values)
        {
            if (!tile.IsRoad) continue;
            chunkRoadTiles.Add(tile);
            _allRoadTiles[tile.Coord] = tile;
        }

        if (chunkRoadTiles.Count == 0) return;
        BuildAndAddRoadMesh(chunkRoadTiles);
    }

    private void IndexRoadTiles(List<HexOverworldTile> tiles)
    {
        foreach (var tile in tiles)
            _allRoadTiles[tile.Coord] = tile;
    }

    // ========================================
    // Mesh 构建：链追踪 → Curve3D 采样 → SurfaceTool 条带
    // ========================================

    private void BuildAndAddRoadMesh(List<HexOverworldTile> sourceTiles)
    {
        var chains = TraceRoadChains(sourceTiles);
        if (chains.Count == 0) return;

        // 用 SurfaceTool 构建所有链的条带到一个 mesh
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var chain in chains)
        {
            if (chain.Count < 2) continue;
            AppendChainStrip(st, chain);
        }

        st.GenerateNormals();
        var mesh = st.Commit();

        if (mesh == null) return;

        // 材质
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.50f, 0.40f, 0.25f);
        mat.Roughness = 0.9f;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mesh.SurfaceSetMaterial(0, mat);

        var instance = new MeshInstance3D();
        instance.Mesh = mesh;
        instance.Name = "RoadMesh";
        AddChild(instance);
        _roadMeshes.Add(instance);
    }

    /// <summary>
    /// 将一条道路链通过 Curve3D 采样生成平滑点序列，
    /// 然后用 SurfaceTool 生成连续三角形条带。
    /// </summary>
    private void AppendChainStrip(SurfaceTool st, List<Vector3> chain)
    {
        // 1. 创建 Curve3D 并添加点（带自动贝塞尔控制柄）
        var curve = new Curve3D();
        curve.BakeInterval = CurveBakeInterval;

        for (int i = 0; i < chain.Count; i++)
        {
            Vector3 tangent;
            if (i == 0)
                tangent = chain[1] - chain[0];
            else if (i == chain.Count - 1)
                tangent = chain[i] - chain[i - 1];
            else
                tangent = (chain[i + 1] - chain[i - 1]) * 0.5f;

            // 控制柄 = 切线 / 3（标准三次贝塞尔平滑）
            curve.AddPoint(chain[i], -tangent / 3.0f, tangent / 3.0f);
        }

        // 2. 获取 Godot 烘焙的均匀采样点
        var bakedPoints = curve.GetBakedPoints();
        if (bakedPoints.Length < 2) return;

        // 3. 生成条带顶点对（左右两侧）
        float halfW = RoadWidth * 0.5f;
        var leftVerts = new Vector3[bakedPoints.Length];
        var rightVerts = new Vector3[bakedPoints.Length];

        for (int i = 0; i < bakedPoints.Length; i++)
        {
            // 切线方向
            Vector3 tangent;
            if (i == 0)
                tangent = (bakedPoints[1] - bakedPoints[0]).Normalized();
            else if (i == bakedPoints.Length - 1)
                tangent = (bakedPoints[i] - bakedPoints[i - 1]).Normalized();
            else
                tangent = (bakedPoints[i + 1] - bakedPoints[i - 1]).Normalized();

            // 水平垂直方向（Y-up 叉积）
            var perp = new Vector3(-tangent.Z, 0, tangent.X).Normalized() * halfW;

            leftVerts[i] = bakedPoints[i] - perp;
            rightVerts[i] = bakedPoints[i] + perp;
        }

        // 4. 生成三角形条带
        for (int i = 0; i < bakedPoints.Length - 1; i++)
        {
            var bl = leftVerts[i];
            var br = rightVerts[i];
            var tl = leftVerts[i + 1];
            var tr = rightVerts[i + 1];

            // 三角形 1: bl → tl → br
            st.SetNormal(Vector3.Up);
            st.AddVertex(bl);
            st.SetNormal(Vector3.Up);
            st.AddVertex(tl);
            st.SetNormal(Vector3.Up);
            st.AddVertex(br);

            // 三角形 2: br → tl → tr
            st.SetNormal(Vector3.Up);
            st.AddVertex(br);
            st.SetNormal(Vector3.Up);
            st.AddVertex(tl);
            st.SetNormal(Vector3.Up);
            st.AddVertex(tr);
        }
    }

    // ========================================
    // 道路链追踪
    // ========================================

    private List<List<Vector3>> TraceRoadChains(List<HexOverworldTile> sourceTiles)
    {
        var chains = new List<List<Vector3>>();
        var visitedEdges = new HashSet<long>();
        var sourceSet = new HashSet<Vector2I>();
        foreach (var t in sourceTiles) sourceSet.Add(t.Coord);

        foreach (var tile in sourceTiles)
        {
            var neighbors = GetRoadNeighbors(tile);
            foreach (var neighbor in neighbors)
            {
                long edgeKey = MakeEdgeKey(tile.Coord, neighbor.Coord);
                if (visitedEdges.Contains(edgeKey)) continue;

                var chain = TraceSingleChain(tile, neighbor, visitedEdges, sourceSet);
                if (chain.Count >= 2)
                    chains.Add(chain);
            }
        }

        return chains;
    }

    private List<Vector3> TraceSingleChain(HexOverworldTile start, HexOverworldTile next,
        HashSet<long> visitedEdges, HashSet<Vector2I> sourceSet)
    {
        var chain = new List<Vector3>();
        chain.Add(CoordConverter.PixelToWorld3D(start.PixelPos) + new Vector3(0, RoadY, 0));

        var prev = start;
        var current = next;

        while (true)
        {
            visitedEdges.Add(MakeEdgeKey(prev.Coord, current.Coord));
            chain.Add(CoordConverter.PixelToWorld3D(current.PixelPos) + new Vector3(0, RoadY, 0));

            // 跨 chunk 边界只延伸一步
            if (!sourceSet.Contains(current.Coord)) break;

            // 找下一个未访问的邻居（不限于 2-连接，交叉口也继续）
            var neighbors = GetRoadNeighbors(current);
            HexOverworldTile? nextTile = null;

            foreach (var n in neighbors)
            {
                if (n.Coord == prev.Coord) continue;
                if (!visitedEdges.Contains(MakeEdgeKey(current.Coord, n.Coord)))
                {
                    nextTile = n;
                    break;
                }
            }

            if (nextTile == null) break;
            prev = current;
            current = nextTile;
        }

        return chain;
    }

    private List<HexOverworldTile> GetRoadNeighbors(HexOverworldTile tile)
    {
        var result = new List<HexOverworldTile>();

        // 检查所有 6 个方向的邻居，只要邻居也是道路就视为连接
        // 不依赖 RoadDirections 位掩码（可能因 A* 路径跳跃而不完整）
        for (int dir = 0; dir < 6; dir++)
        {
            var nCoord = HexOverworldTile.GetNeighbor(tile.Coord.X, tile.Coord.Y, dir);
            if (_allRoadTiles.TryGetValue(nCoord, out var neighbor))
                result.Add(neighbor);
        }

        return result;
    }

    // ========================================
    // 工具
    // ========================================

    private static long MakeEdgeKey(Vector2I a, Vector2I b)
    {
        if (a.X > b.X || (a.X == b.X && a.Y > b.Y))
            (a, b) = (b, a);
        return ((long)a.X << 48) | ((long)(a.Y & 0xFFFF) << 32) | ((long)b.X << 16) | (long)(b.Y & 0xFFFF);
    }
}
