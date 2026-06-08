// WaterStripRenderer.cs
// 战斗场景水面渲染器 — 用 ImmediateMesh 沿连通水域 hex 生成平滑三角带。
// 每个水域 hex 中心作为路径节点，相邻水域之间生成带宽度的条带。
// 孤立水域 hex 用圆盘覆盖。配合半透明蓝色材质 + 简单 UV 偏移模拟流动。
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class WaterStripRenderer : Node3D
{
    private const float StripWidth = 0.55f;   // 条带宽度（hex 半径比例）
    private static readonly float YOffset = CombatLayerHeight.TextureLayer;
    private const float PoolRadius = 0.55f;   // 孤立水域圆盘半径（hex 半径比例）

    private static readonly Color WaterColor = new(0.12f, 0.35f, 0.65f, 0.80f);
    private static readonly Color ShallowColor = new(0.20f, 0.50f, 0.70f, 0.70f);

    private MeshInstance3D? _stripMesh;
    private MeshInstance3D? _poolMesh;
    private StandardMaterial3D? _material;
    private float _uvOffset;

    private static readonly HashSet<BattleCellData.TerrainType> WaterTypes = new()
    {
        BattleCellData.TerrainType.ShallowWater,
        BattleCellData.TerrainType.DeepWater,
        BattleCellData.TerrainType.River,
    };

    /// <summary>渲染水面</summary>
    public void Render(HexGrid hexGrid)
    {
        if (hexGrid == null) return;

        // 收集水域 cell
        var waterCells = new Dictionary<Vector2I, HexCell>();
        foreach (var kvp in hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell?.Data == null) continue;
            if (WaterTypes.Contains(cell.Data.terrainType))
                waterCells[kvp.Key] = cell;
        }

        if (waterCells.Count == 0) return;

        _material = new StandardMaterial3D();
        _material.AlbedoColor = WaterColor;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        // 找连通边
        var edges = new List<(Vector3 from, Vector3 to)>();
        var connectedCells = new HashSet<Vector2I>();
        var processedEdges = new HashSet<(int, int, int, int)>();

        float hexHeight = HexUtils.Size * 0.5f;

        foreach (var kvp in waterCells)
        {
            var coord = kvp.Key;
            var cell = kvp.Value;
            var posA = cell.Position + new Vector3(0, hexHeight / 2.0f + YOffset, 0);

            for (int dir = 0; dir < 6; dir++)
            {
                var nb = HexUtils.GetNeighbor(coord.X, coord.Y, dir);
                if (!waterCells.TryGetValue(nb, out var nbCell)) continue;

                // 只在同高度水域之间连条带：高差水域若连一条斜带，会从高格顶面斜搭到低格顶面、
                // 沿崖面垂下形成"高地 hex 侧面蓝色三角"。高差处不连，两格各自按孤立水域出平圆盘。
                if (cell.Elevation != nbCell.Elevation) continue;

                var edgeKey = coord.X <= nb.X || (coord.X == nb.X && coord.Y <= nb.Y)
                    ? (coord.X, coord.Y, nb.X, nb.Y)
                    : (nb.X, nb.Y, coord.X, coord.Y);
                if (!processedEdges.Add(edgeKey)) continue;

                var posB = nbCell.Position + new Vector3(0, hexHeight / 2.0f + YOffset, 0);
                edges.Add((posA, posB));
                connectedCells.Add(coord);
                connectedCells.Add(nb);
            }
        }

        // 生成条带 mesh（三角带）
        if (edges.Count > 0)
        {
            var immMesh = new ImmediateMesh();
            immMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            float halfWidth = HexUtils.Size * StripWidth * 0.5f;

            foreach (var (from, to) in edges)
            {
                // 计算垂直于条带方向的偏移
                var dir = (to - from).Normalized();
                var perp = new Vector3(-dir.Z, 0, dir.X) * halfWidth;

                // 两端各加一点延伸，让条带覆盖 hex 中心区域
                var extFrom = from - dir * (halfWidth * 0.3f);
                var extTo = to + dir * (halfWidth * 0.3f);

                var v0 = extFrom + perp;
                var v1 = extFrom - perp;
                var v2 = extTo + perp;
                var v3 = extTo - perp;

                // 三角形 1
                immMesh.SurfaceSetNormal(Vector3.Up);
                immMesh.SurfaceSetUV(new Vector2(0, 0));
                immMesh.SurfaceAddVertex(v0);
                immMesh.SurfaceSetUV(new Vector2(1, 0));
                immMesh.SurfaceAddVertex(v1);
                immMesh.SurfaceSetUV(new Vector2(0, 1));
                immMesh.SurfaceAddVertex(v2);

                // 三角形 2
                immMesh.SurfaceSetUV(new Vector2(1, 0));
                immMesh.SurfaceAddVertex(v1);
                immMesh.SurfaceSetUV(new Vector2(1, 1));
                immMesh.SurfaceAddVertex(v3);
                immMesh.SurfaceSetUV(new Vector2(0, 1));
                immMesh.SurfaceAddVertex(v2);
            }

            immMesh.SurfaceEnd();

            _stripMesh = new MeshInstance3D();
            _stripMesh.Mesh = immMesh;
            _stripMesh.MaterialOverride = _material;
            _stripMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            AddChild(_stripMesh);
        }

        // 孤立水域（没有相邻水域的 cell）用圆盘
        var isolatedCells = waterCells.Keys.Where(k => !connectedCells.Contains(k)).ToList();
        if (isolatedCells.Count > 0)
        {
            var poolImmMesh = new ImmediateMesh();
            poolImmMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

            float radius = HexUtils.Size * PoolRadius;
            int segments = 12;

            foreach (var coord in isolatedCells)
            {
                var cell = waterCells[coord];
                var center = cell.Position + new Vector3(0, hexHeight / 2.0f + YOffset, 0);

                // 扇形三角形组成圆盘
                for (int i = 0; i < segments; i++)
                {
                    float a1 = Mathf.Tau * i / segments;
                    float a2 = Mathf.Tau * (i + 1) / segments;

                    var p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                    var p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0, Mathf.Sin(a2) * radius);

                    poolImmMesh.SurfaceSetNormal(Vector3.Up);
                    poolImmMesh.SurfaceSetUV(new Vector2(0.5f, 0.5f));
                    poolImmMesh.SurfaceAddVertex(center);
                    poolImmMesh.SurfaceSetUV(new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f));
                    poolImmMesh.SurfaceAddVertex(p1);
                    poolImmMesh.SurfaceSetUV(new Vector2(Mathf.Cos(a2) * 0.5f + 0.5f, Mathf.Sin(a2) * 0.5f + 0.5f));
                    poolImmMesh.SurfaceAddVertex(p2);
                }
            }

            poolImmMesh.SurfaceEnd();

            _poolMesh = new MeshInstance3D();
            _poolMesh.Mesh = poolImmMesh;
            _poolMesh.MaterialOverride = _material;
            _poolMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            AddChild(_poolMesh);
        }

        GD.Print($"[WaterStripRenderer] 水面: {edges.Count} 条连接, {isolatedCells.Count} 个孤立水域");
    }

    public override void _Process(double delta)
    {
        // UV 偏移模拟流动（简单但有效）
        if (_material == null) return;
        _uvOffset += (float)delta * 0.15f;
        if (_uvOffset > 1f) _uvOffset -= 1f;
        _material.Uv1Offset = new Vector3(_uvOffset, 0, 0);
    }

    public void Clear()
    {
        _stripMesh?.QueueFree();
        _poolMesh?.QueueFree();
        _stripMesh = null;
        _poolMesh = null;
    }
}
