using BladeHex.Map;
using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Map;

[GlobalClass]
public partial class RiverRenderer : Node2D
{
    [Export] public float RiverWidth { get; set; } = 80.0f;
    [Export] public Color RiverColor { get; set; } = new(0.15f, 0.35f, 0.7f, 0.85f);
    [Export] public Color RiverEdgeColor { get; set; } = new(0.1f, 0.25f, 0.5f, 0.6f);
    [Export] public string RiverTexturePath { get; set; } = "";
    [Export] public int CurveResolution { get; set; } = 6;
    [Export] public float TextureTileLength { get; set; } = 80.0f;
    [Export] public float SideThickness { get; set; } = 3.0f;
    [Export] public Color SideColor { get; set; } = new(0.05f, 0.15f, 0.35f, 0.9f);
    [Export] public Vector2 ShadowOffset { get; set; } = new(2.0f, 4.0f);
    [Export] public Color ShadowColor { get; set; } = new(0.0f, 0.0f, 0.0f, 0.15f);

    public bool FullyBuilt { get; set; }

    private ChunkManager? _chunkManager;
    private Texture2D? _riverTexture;
    private readonly List<MeshInstance2D> _riverMeshes = new();
    private readonly HashSet<Vector2I> _junctionCoords = new();

    private static readonly Vector2I[] HexOffsets =
    [
        new(1, 0),
        new(0, 1),
        new(-1, 1),
        new(-1, 0),
        new(0, -1),
        new(1, -1),
    ];

    public void Initialize(ChunkManager? chunkManager)
    {
        _chunkManager = chunkManager;
        _riverTexture = string.IsNullOrWhiteSpace(RiverTexturePath)
            ? null
            : TextureAssetResolver.LoadMapTexture(RiverTexturePath, RiverTexturePath);
    }

    public void RebuildFromChunks()
    {
        if (FullyBuilt)
            return;

        ClearRivers();
        if (_chunkManager == null)
            return;

        var riverTiles = new HashSet<Vector2I>();
        foreach (var kvp in _chunkManager.ActiveChunks)
        {
            foreach (var tileKvp in kvp.Value.Tiles)
            {
                if (tileKvp.Value.IsRiver)
                    riverTiles.Add(tileKvp.Key);
            }
        }

        if (riverTiles.Count == 0)
            return;

        foreach (var segment in TraceRiverSegments(riverTiles))
        {
            if (segment.Count < 2)
                continue;

            var pixelPoints = new List<Vector2>(segment.Count);
            foreach (var coord in segment)
                pixelPoints.Add(HexOverworldTile.AxialToPixel(coord.X, coord.Y));

            var smoothed = SmoothPath(pixelPoints);
            if (smoothed.Length > 1)
                CreateRiverMeshStrip(smoothed);
        }

        foreach (var coord in _junctionCoords)
            CreateRiverJunctionMesh(HexOverworldTile.AxialToPixel(coord.X, coord.Y));

        GD.Print($"[RiverRenderer] Rendered {_riverMeshes.Count} river mesh strips from {riverTiles.Count} river tiles.");
    }

    public void ClearRivers()
    {
        foreach (var mesh in _riverMeshes)
        {
            if (GodotObject.IsInstanceValid(mesh))
                mesh.QueueFree();
        }

        _riverMeshes.Clear();
    }

    public void SetRiverTexture(Texture2D? texture)
    {
        _riverTexture = texture;
        RebuildFromChunks();
    }

    private List<List<Vector2I>> TraceRiverSegments(HashSet<Vector2I> riverTiles)
    {
        var segments = new List<List<Vector2I>>();
        if (riverTiles.Count == 0)
            return segments;

        var usedEdges = new HashSet<(Vector2I, Vector2I)>();
        var endpoints = new List<Vector2I>();
        var junctions = new List<Vector2I>();

        foreach (var coord in riverTiles)
        {
            int neighborCount = CountRiverNeighbors(coord, riverTiles);
            if (neighborCount == 1)
                endpoints.Add(coord);
            else if (neighborCount >= 3)
                junctions.Add(coord);
        }

        _junctionCoords.Clear();
        foreach (var junction in junctions)
            _junctionCoords.Add(junction);

        foreach (var start in endpoints)
            TraceFromNode(start, riverTiles, usedEdges, segments);

        foreach (var start in junctions)
            TraceFromNode(start, riverTiles, usedEdges, segments);

        foreach (var start in riverTiles)
            TraceFromNode(start, riverTiles, usedEdges, segments);

        return segments;
    }

    private static void TraceFromNode(
        Vector2I start,
        HashSet<Vector2I> riverTiles,
        HashSet<(Vector2I, Vector2I)> usedEdges,
        List<List<Vector2I>> segments)
    {
        foreach (var neighbor in GetRiverNeighbors(start, riverTiles))
        {
            if (usedEdges.Contains(EdgeKey(start, neighbor)))
                continue;

            var segment = new List<Vector2I> { start, neighbor };
            usedEdges.Add(EdgeKey(start, neighbor));

            var previous = start;
            var current = neighbor;

            while (true)
            {
                if (CountRiverNeighbors(current, riverTiles) != 2)
                    break;

                Vector2I? next = null;
                foreach (var candidate in GetRiverNeighbors(current, riverTiles))
                {
                    if (candidate == previous || usedEdges.Contains(EdgeKey(current, candidate)))
                        continue;

                    next = candidate;
                    break;
                }

                if (next == null)
                    break;

                segment.Add(next.Value);
                usedEdges.Add(EdgeKey(current, next.Value));
                previous = current;
                current = next.Value;
            }

            if (segment.Count >= 2)
                segments.Add(segment);
        }
    }

    private static (Vector2I, Vector2I) EdgeKey(Vector2I a, Vector2I b)
    {
        int aHash = a.X * 73856093 ^ a.Y * 19349663;
        int bHash = b.X * 73856093 ^ b.Y * 19349663;
        return aHash < bHash ? (a, b) : (b, a);
    }

    private static int CountRiverNeighbors(Vector2I coord, HashSet<Vector2I> riverTiles)
    {
        int count = 0;
        foreach (var offset in HexOffsets)
        {
            if (riverTiles.Contains(coord + offset))
                count++;
        }

        return count;
    }

    private static List<Vector2I> GetRiverNeighbors(Vector2I coord, HashSet<Vector2I> riverTiles)
    {
        var result = new List<Vector2I>();
        foreach (var offset in HexOffsets)
        {
            var neighbor = coord + offset;
            if (riverTiles.Contains(neighbor))
                result.Add(neighbor);
        }

        return result;
    }

    private Vector2[] SmoothPath(List<Vector2> points)
    {
        if (points.Count <= 2)
            return points.ToArray();

        var controlPoints = new List<Vector2> { points[0] };
        for (int i = 2; i < points.Count - 1; i += 2)
            controlPoints.Add(points[i]);
        controlPoints.Add(points[^1]);

        if (controlPoints.Count <= 2)
            return controlPoints.ToArray();

        var result = new List<Vector2>();
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Vector2 p0 = i > 0 ? controlPoints[i - 1] : controlPoints[i];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[i + 1];
            Vector2 p3 = i + 2 < controlPoints.Count ? controlPoints[i + 2] : controlPoints[i + 1];

            Vector2 b0 = p1;
            Vector2 b1 = p1 + (p2 - p0) / 6.0f;
            Vector2 b2 = p2 - (p3 - p1) / 6.0f;
            Vector2 b3 = p2;

            for (int step = 0; step < CurveResolution; step++)
            {
                float t = (float)step / CurveResolution;
                result.Add(CubicBezier(b0, b1, b2, b3, t));
            }
        }

        result.Add(controlPoints[^1]);
        return result.ToArray();
    }

    private void CreateRiverMeshStrip(Vector2[] path)
    {
        if (path.Length < 2)
            return;

        var processedPath = RetractPathEnds(path, RiverWidth * 0.8f);
        if (processedPath.Length < 2)
            return;

        if (ShadowOffset.LengthSquared() > 0.01f)
        {
            CreateSingleMeshStripWithOffset(processedPath, RiverWidth * 1.4f, ShadowColor, 38, ShadowOffset);
            CreateSingleMeshStripWithOffset(processedPath, RiverWidth, ShadowColor, 39, ShadowOffset);
        }

        if (SideThickness > 0.1f)
        {
            Vector2 sideOffset = new(0.0f, SideThickness);
            CreateSingleMeshStripWithOffset(processedPath, RiverWidth * 1.4f, SideColor, 40, sideOffset);
            CreateSingleMeshStripWithOffset(processedPath, RiverWidth, SideColor, 41, sideOffset);
        }

        CreateSingleMeshStrip(processedPath, RiverWidth * 1.4f, RiverEdgeColor, 42);
        CreateSingleMeshStrip(processedPath, RiverWidth, RiverColor, 43);
    }

    private void CreateRiverJunctionMesh(Vector2 center)
    {
        if (ShadowOffset.LengthSquared() > 0.01f)
        {
            CreateSingleCircleMesh(center + ShadowOffset, RiverWidth * 1.4f * 0.5f, ShadowColor, 38);
            CreateSingleCircleMesh(center + ShadowOffset, RiverWidth * 0.5f, ShadowColor, 39);
        }

        if (SideThickness > 0.1f)
        {
            Vector2 sideOffset = new(0.0f, SideThickness);
            CreateSingleCircleMesh(center + sideOffset, RiverWidth * 1.4f * 0.5f, SideColor, 40);
            CreateSingleCircleMesh(center + sideOffset, RiverWidth * 0.5f, SideColor, 41);
        }

        CreateSingleCircleMesh(center, RiverWidth * 1.4f * 0.5f, RiverEdgeColor, 42);
        CreateSingleCircleMesh(center, RiverWidth * 0.5f, RiverColor, 43);
    }

    private void CreateSingleMeshStrip(Vector2[] path, float width, Color color, int zIndex)
    {
        CreateSingleMeshStripWithOffset(path, width, color, zIndex, Vector2.Zero);
    }

    private void CreateSingleMeshStripWithOffset(Vector2[] path, float width, Color color, int zIndex, Vector2 vertexOffset)
    {
        if (path.Length < 2)
            return;

        int vertCount = path.Length * 2;
        var vertices = new Vector2[vertCount];
        var uvs = new Vector2[vertCount];
        var colors = new Color[vertCount];
        float cumulativeLength = 0.0f;

        for (int i = 0; i < path.Length; i++)
        {
            Vector2 tangent = GetPathTangent(path, i);
            Vector2 normal = new(-tangent.Y, tangent.X);

            if (i > 0)
                cumulativeLength += path[i].DistanceTo(path[i - 1]);

            float noise = 0.925f
                + 0.09f * Mathf.Sin(cumulativeLength * 0.007f)
                + 0.035f * Mathf.Sin(cumulativeLength * 0.023f)
                + 0.01f * Mathf.Sin(cumulativeLength * 0.075f);
            float halfWidth = width * noise * 0.5f;
            float u = cumulativeLength / TextureTileLength;

            vertices[i * 2] = path[i] + normal * halfWidth + vertexOffset;
            vertices[i * 2 + 1] = path[i] - normal * halfWidth + vertexOffset;
            uvs[i * 2] = new Vector2(u, 0.0f);
            uvs[i * 2 + 1] = new Vector2(u, 1.0f);
            colors[i * 2] = color;
            colors[i * 2 + 1] = color;
        }

        var meshInstance = CreateMeshInstance(vertices, uvs, colors, BuildStripIndices(path.Length), zIndex);
        if (_riverTexture != null)
        {
            var shaderMaterial = new ShaderMaterial();
            shaderMaterial.Shader = GetOrCreateRiverShader();
            shaderMaterial.SetShaderParameter("river_texture", _riverTexture);
            meshInstance.Material = shaderMaterial;
        }
        else
        {
            meshInstance.Material = new CanvasItemMaterial();
        }

        AddChild(meshInstance);
        _riverMeshes.Add(meshInstance);
    }

    private void CreateSingleCircleMesh(Vector2 center, float radius, Color color, int zIndex)
    {
        const int segments = 24;
        var vertices = new Vector2[segments + 1];
        var uvs = new Vector2[segments + 1];
        var colors = new Color[segments + 1];

        vertices[0] = center;
        uvs[0] = new Vector2(0.5f, 0.5f);
        colors[0] = color;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            vertices[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            uvs[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle));
            colors[i + 1] = color;
        }

        var indices = new int[segments * 3];
        int index = 0;
        for (int i = 0; i < segments; i++)
        {
            indices[index++] = 0;
            indices[index++] = i + 1;
            indices[index++] = i == segments - 1 ? 1 : i + 2;
        }

        var meshInstance = CreateMeshInstance(vertices, uvs, colors, indices, zIndex);
        if (_riverTexture != null)
        {
            var shaderMaterial = new ShaderMaterial();
            shaderMaterial.Shader = GetOrCreateRiverShader();
            shaderMaterial.SetShaderParameter("river_texture", _riverTexture);
            meshInstance.Material = shaderMaterial;
        }
        else
        {
            meshInstance.Material = new CanvasItemMaterial();
        }

        AddChild(meshInstance);
        _riverMeshes.Add(meshInstance);
    }

    private Vector2[] RetractPathEnds(Vector2[] path, float retractDist)
    {
        if (path.Length < 2)
            return path;

        var tempPath = new List<Vector2>(path);
        RetractPathStart(tempPath, retractDist);
        if (tempPath.Count < 2)
            return Array.Empty<Vector2>();

        RetractPathEnd(tempPath, retractDist);
        return tempPath.ToArray();
    }

    private void RetractPathStart(List<Vector2> path, float retractDist)
    {
        if (!IsNearJunction(path[0], out _))
            return;

        float accumulated = 0.0f;
        int cutIndex = 0;
        for (int i = 1; i < path.Count; i++)
        {
            float distance = path[i].DistanceTo(path[i - 1]);
            accumulated += distance;
            if (accumulated >= retractDist)
            {
                float overrun = accumulated - retractDist;
                path[i] = path[i - 1].Lerp(path[i], 1.0f - overrun / distance);
                cutIndex = i;
                break;
            }
        }

        if (cutIndex > 0)
            path.RemoveRange(0, cutIndex);
        else
            path.Clear();
    }

    private void RetractPathEnd(List<Vector2> path, float retractDist)
    {
        if (!IsNearJunction(path[^1], out _))
            return;

        float accumulated = 0.0f;
        int cutIndex = path.Count - 1;
        for (int i = path.Count - 2; i >= 0; i--)
        {
            float distance = path[i].DistanceTo(path[i + 1]);
            accumulated += distance;
            if (accumulated >= retractDist)
            {
                float overrun = accumulated - retractDist;
                path[i] = path[i + 1].Lerp(path[i], 1.0f - overrun / distance);
                cutIndex = i;
                break;
            }
        }

        if (cutIndex < path.Count - 1)
            path.RemoveRange(cutIndex + 1, path.Count - 1 - cutIndex);
        else
            path.Clear();
    }

    private bool IsNearJunction(Vector2 position, out Vector2 junctionCenter)
    {
        junctionCenter = Vector2.Zero;
        const float limit = 156.0f * 0.65f;

        foreach (var coord in _junctionCoords)
        {
            Vector2 center = HexOverworldTile.AxialToPixel(coord.X, coord.Y);
            if (position.DistanceTo(center) < limit)
            {
                junctionCenter = center;
                return true;
            }
        }

        return false;
    }

    private static MeshInstance2D CreateMeshInstance(Vector2[] vertices, Vector2[] uvs, Color[] colors, int[] indices, int zIndex)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        return new MeshInstance2D
        {
            Mesh = mesh,
            ZIndex = zIndex,
        };
    }

    private static int[] BuildStripIndices(int pathLength)
    {
        var indices = new int[(pathLength - 1) * 6];
        int index = 0;
        for (int i = 0; i < pathLength - 1; i++)
        {
            int left = i * 2;
            int right = i * 2 + 1;
            int nextLeft = (i + 1) * 2;
            int nextRight = (i + 1) * 2 + 1;

            indices[index++] = left;
            indices[index++] = nextLeft;
            indices[index++] = right;
            indices[index++] = right;
            indices[index++] = nextLeft;
            indices[index++] = nextRight;
        }

        return indices;
    }

    private static Vector2 GetPathTangent(Vector2[] path, int index)
    {
        if (index == 0)
            return (path[1] - path[0]).Normalized();

        if (index == path.Length - 1)
            return (path[index] - path[index - 1]).Normalized();

        return (path[index + 1] - path[index - 1]).Normalized();
    }

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1.0f - t;
        return u * u * u * p0
            + 3.0f * u * u * t * p1
            + 3.0f * u * t * t * p2
            + t * t * t * p3;
    }

    private static Shader? _riverShaderCache;

    private static Shader GetOrCreateRiverShader()
    {
        if (_riverShaderCache != null)
            return _riverShaderCache;

        _riverShaderCache = new Shader
        {
            Code = """
shader_type canvas_item;

uniform sampler2D river_texture : repeat_enable, filter_linear;

void fragment() {
    vec4 tex = texture(river_texture, UV);
    COLOR = tex * COLOR;
}
""",
        };
        return _riverShaderCache;
    }
}
