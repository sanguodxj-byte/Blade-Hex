// HexPathRenderer.cs
// 通用 hex 连通条带渲染器 — 用于河流、道路等需要"方向感"的地形。
// 原理：找到同类型 hex 的连通关系，在相邻 hex 之间生成条带 mesh 连接。
// 每个 hex 中心放一个圆形节点，相邻同类 hex 之间放一个矩形条带。
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.View.Combat;

/// <summary>
/// Hex 连通条带渲染器。
/// 用于河流（蓝色半透明 + 流动 UV）和道路（棕色/灰色石板）。
/// </summary>
[GlobalClass]
public partial class HexPathRenderer : Node3D
{
    /// <summary>条带类型配置</summary>
    public class PathStyle
    {
        public Color Color = new(0.2f, 0.5f, 0.9f, 0.8f);
        public float Width = 0.35f;       // 条带宽度（hex 半径的比例）
        public float YOffset = CombatLayerHeight.TextureLayer;
        public float NodeRadius = 0.4f;   // 中心节点半径（hex 半径的比例）
        public bool Unshaded = false;     // 是否无光照
    }

    // 预定义样式
    public static readonly PathStyle WaterStyle = new()
    {
        Color = new Color(0.15f, 0.4f, 0.75f, 0.85f),
        Width = 0.45f,
        YOffset = CombatLayerHeight.TextureLayer,
        NodeRadius = 0.5f,
        Unshaded = true,
    };

    public static readonly PathStyle RoadStyle = new()
    {
        Color = new Color(0.55f, 0.45f, 0.35f, 0.9f),
        Width = 0.3f,
        YOffset = CombatLayerHeight.TextureLayer,
        NodeRadius = 0.35f,
        Unshaded = false,
    };

    private readonly List<MeshInstance3D> _meshes = new();

    /// <summary>
    /// 渲染指定地形类型的连通条带。
    /// </summary>
    public void Render(HexGrid hexGrid, HashSet<BattleCellData.TerrainType> terrainTypes, PathStyle style)
    {
        if (hexGrid == null) return;

        // 1. 收集所有目标地形的 cell
        var targetCells = new Dictionary<Vector2I, HexCell>();
        foreach (var kvp in hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell?.Data == null) continue;
            if (terrainTypes.Contains(cell.Data.terrainType))
                targetCells[kvp.Key] = cell;
        }

        if (targetCells.Count == 0) return;

        // 2. 为每个目标 cell 放置中心节点（圆盘）
        var material = CreateMaterial(style);
        foreach (var kvp in targetCells)
        {
            var cell = kvp.Value;
            PlaceNode(cell, style, material);
        }

        // 3. 为相邻的目标 cell 之间放置条带
        var processedEdges = new HashSet<long>();
        foreach (var kvp in targetCells)
        {
            var coord = kvp.Key;
            var cell = kvp.Value;

            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexUtils.GetNeighbor(coord.X, coord.Y, dir);
                if (!targetCells.TryGetValue(neighbor, out var neighborCell)) continue;

                // 避免重复（每条边只画一次）
                long edgeKey = PackEdgeKey(coord, neighbor);
                if (!processedEdges.Add(edgeKey)) continue;

                PlaceStrip(cell, neighborCell, style, material);
            }
        }

        GD.Print($"[HexPathRenderer] 渲染 {targetCells.Count} 个节点, {processedEdges.Count} 条连接");
    }

    /// <summary>清除所有渲染</summary>
    public void Clear()
    {
        foreach (var mesh in _meshes)
        {
            if (GodotObject.IsInstanceValid(mesh))
                mesh.QueueFree();
        }
        _meshes.Clear();
    }

    // ========================================
    // 内部
    // ========================================

    private void PlaceNode(HexCell cell, PathStyle style, StandardMaterial3D material)
    {
        float radius = HexUtils.Size * style.NodeRadius;

        var mesh = new MeshInstance3D();
        var cylinder = new CylinderMesh();
        cylinder.TopRadius = radius;
        cylinder.BottomRadius = radius;
        cylinder.Height = 0.5f;
        mesh.Mesh = cylinder;
        mesh.MaterialOverride = material;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        float hexHeight = HexUtils.Size * 0.5f;
        mesh.Position = cell.Position + new Vector3(0, hexHeight / 2.0f + style.YOffset, 0);

        AddChild(mesh);
        _meshes.Add(mesh);
    }

    private void PlaceStrip(HexCell from, HexCell to, PathStyle style, StandardMaterial3D material)
    {
        float hexHeight = HexUtils.Size * 0.5f;
        var posA = from.Position + new Vector3(0, hexHeight / 2.0f + style.YOffset, 0);
        var posB = to.Position + new Vector3(0, hexHeight / 2.0f + style.YOffset, 0);

        var center = (posA + posB) * 0.5f;
        float length = posA.DistanceTo(posB);
        float width = HexUtils.Size * style.Width;

        var mesh = new MeshInstance3D();
        var box = new BoxMesh();
        box.Size = new Vector3(width, 0.5f, length);
        mesh.Mesh = box;
        mesh.MaterialOverride = material;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        mesh.Position = center;

        // 旋转条带朝向目标
        var dir = (posB - posA).Normalized();
        float angle = Mathf.Atan2(dir.X, dir.Z);
        mesh.RotationDegrees = new Vector3(0, Mathf.RadToDeg(angle), 0);

        AddChild(mesh);
        _meshes.Add(mesh);
    }

    private static StandardMaterial3D CreateMaterial(PathStyle style)
    {
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = style.Color;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (style.Unshaded)
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        return mat;
    }

    private static long PackEdgeKey(Vector2I a, Vector2I b)
    {
        // 保证 (a,b) 和 (b,a) 产生相同 key
        var min = a.X < b.X || (a.X == b.X && a.Y < b.Y) ? a : b;
        var max = min == a ? b : a;
        return ((long)(min.X + 1000) << 32) | ((long)(min.Y + 1000) << 16) | ((long)(max.X + 1000) << 8) | (long)(max.Y + 1000);
    }
}
