using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Map;

[GlobalClass]
public partial class RoadRenderer : Node2D
{
    [Export] public float RoadWidth { get; set; } = 32.0f;
    [Export] public Color RoadColor { get; set; } = new(0.95f, 0.82f, 0.55f, 1.0f);
    [Export] public string RoadTexturePath { get; set; } = "";
    [Export] public int CurveResolution { get; set; } = 6;
    [Export] public float TextureTileLength { get; set; } = 64.0f;
    [Export] public float SideThickness { get; set; } = 4.0f;
    [Export] public Color SideColor { get; set; } = new(0.45f, 0.38f, 0.22f, 1.0f);
    [Export] public Vector2 ShadowOffset { get; set; } = new(2.0f, 4.0f);
    [Export] public Color ShadowColor { get; set; } = new(0.0f, 0.0f, 0.0f, 0.18f);

    public bool FullyBuilt { get; set; }
    public int RoadCount => _roadMeshes.Count;
    public int DebugJunctionCount => _junctionCoords.Count;

    private ChunkManager? _chunkManager;
    private Texture2D? _roadTexture;
    private readonly List<MeshInstance2D> _roadMeshes = new();
    private readonly HashSet<Vector2I> _junctionCoords = new();
    private readonly Dictionary<Vector2I, Vector2I> _junctionRenderCoords = new();
    private readonly Dictionary<Vector2I, HexOverworldTile> _currentBuildTiles = new();

    private readonly struct RoadClassProfile
    {
        public readonly float WidthMul;
        public readonly float CoreAlpha;
        public readonly float EdgeAlphaFactor;
        public readonly float ColorDarken;
        public readonly float NoiseFreq;
        public readonly float NoiseAmp;
        public readonly float JitterPx;

        public RoadClassProfile(
            float widthMul,
            float coreAlpha,
            float edgeAlphaFactor,
            float colorDarken,
            float noiseFreq,
            float noiseAmp,
            float jitterPx)
        {
            WidthMul = widthMul;
            CoreAlpha = coreAlpha;
            EdgeAlphaFactor = edgeAlphaFactor;
            ColorDarken = colorDarken;
            NoiseFreq = noiseFreq;
            NoiseAmp = noiseAmp;
            JitterPx = jitterPx;
        }
    }

    private static RoadClassProfile GetProfile(int roadClass) => roadClass switch
    {
        2 => new(1.4f, 1.0f, 0.4f, 0.12f, 0.003f, 0.06f, 2.0f),
        0 => new(0.55f, 0.6f, 0.25f, 0.0f, 0.005f, 0.35f, 8.0f),
        _ => new(1.0f, 1.0f, 0.4f, 0.0f, 0.004f, 0.18f, 5.0f),
    };

    public void Initialize(ChunkManager? chunkManager)
    {
        _chunkManager = chunkManager;
        _roadTexture = string.IsNullOrWhiteSpace(RoadTexturePath)
            ? null
            : TextureAssetResolver.LoadMapTexture(RoadTexturePath, RoadTexturePath);
    }

    public void RebuildFromChunks()
    {
        if (FullyBuilt)
            return;

        RebuildFromChunkMap(_chunkManager?.ActiveChunks, "active");
    }

    public void RebuildFromAllKnownTiles()
    {
        RebuildFromChunkMap(_chunkManager?.AllKnownChunks, "known");
    }

    private void RebuildFromChunkMap(IReadOnlyDictionary<Vector2I, ChunkData>? chunks, string scope)
    {
        ClearRoads();
        _currentBuildTiles.Clear();
        if (_chunkManager == null || chunks == null)
            return;

        var roadTiles = new HashSet<Vector2I>();
        foreach (var kvp in chunks)
        {
            foreach (var tileKvp in kvp.Value.Tiles)
            {
                _currentBuildTiles[tileKvp.Key] = tileKvp.Value;
                if (tileKvp.Value.IsRoad)
                    roadTiles.Add(tileKvp.Key);
            }
        }

        if (roadTiles.Count == 0)
        {
            _currentBuildTiles.Clear();
            return;
        }

        foreach (var segment in TraceRoadSegments(roadTiles))
        {
            if (segment.Count < 2)
                continue;

            int roadClass = 1;
            var pixelPoints = new List<Vector2>(segment.Count);
            foreach (var coord in segment)
            {
                if (_currentBuildTiles.TryGetValue(coord, out var tile))
                    roadClass = Math.Max(roadClass, tile.RoadClassVal);

                pixelPoints.Add(GetRoadRenderPoint(coord));
            }

            var smoothed = SmoothPath(pixelPoints);
            if (smoothed.Length > 1)
                CreateRoadMeshStrip(smoothed, roadClass);
        }

        GD.Print($"[RoadRenderer] Rendered {_roadMeshes.Count} road mesh strips from {roadTiles.Count} {scope} road tiles.");
        _currentBuildTiles.Clear();
    }

    public void OnNewChunksLoaded(List<ChunkData> newChunks)
    {
        if (FullyBuilt || _chunkManager == null)
            return;

        if (newChunks.Count > 0)
            RebuildFromChunks();
    }

    public void ClearRoads()
    {
        foreach (var mesh in _roadMeshes)
        {
            if (GodotObject.IsInstanceValid(mesh))
                mesh.QueueFree();
        }

        _roadMeshes.Clear();
    }

    public void SetRoadTexture(Texture2D? texture)
    {
        _roadTexture = texture;
        RebuildCurrentScope();
    }

    public void SetRoadWidth(float width)
    {
        RoadWidth = width;
        RebuildCurrentScope();
    }

    private void RebuildCurrentScope()
    {
        if (FullyBuilt)
            RebuildFromAllKnownTiles();
        else
            RebuildFromChunks();
    }

    public void GenerateFallbackRoads(List<OverworldPOI> pois)
    {
        ClearRoads();

        var settlements = new List<OverworldPOI>();
        foreach (var poi in pois)
        {
            if (poi.PoiTypeEnum == OverworldPOI.POIType.Town
                || poi.PoiTypeEnum == OverworldPOI.POIType.Village
                || poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
            {
                settlements.Add(poi);
            }
        }

        if (settlements.Count < 2)
            return;

        var inTree = new HashSet<int> { 0 };
        var candidates = new HashSet<int>();
        for (int i = 1; i < settlements.Count; i++)
            candidates.Add(i);

        while (candidates.Count > 0)
        {
            float bestDist = float.MaxValue;
            int bestFrom = -1;
            int bestTo = -1;

            foreach (int from in inTree)
            {
                foreach (int to in candidates)
                {
                    float distance = settlements[from].Position.DistanceTo(settlements[to].Position);
                    if (distance < bestDist)
                    {
                        bestDist = distance;
                        bestFrom = from;
                        bestTo = to;
                    }
                }
            }

            if (bestTo < 0)
                break;

            var fromPoi = settlements[bestFrom];
            var toPoi = settlements[bestTo];
            int roadClass = IsMajorSettlement(fromPoi) || IsMajorSettlement(toPoi) ? 2 : 1;
            var path = GenerateBezierDirect(fromPoi.Position, toPoi.Position);
            if (path.Length > 1)
                CreateRoadMeshStrip(path, roadClass);

            inTree.Add(bestTo);
            candidates.Remove(bestTo);
        }

        GD.Print($"[RoadRenderer] Fallback generated {_roadMeshes.Count} visual road meshes.");
    }

    private static bool IsMajorSettlement(OverworldPOI poi)
    {
        return poi.PoiTypeEnum == OverworldPOI.POIType.Town
            || poi.PoiTypeEnum == OverworldPOI.POIType.Castle;
    }

    private Vector2 GetRoadRenderPoint(Vector2I coord)
    {
        if (_junctionRenderCoords.TryGetValue(coord, out var junctionCoord))
            return HexOverworldTile.AxialToPixel(junctionCoord.X, junctionCoord.Y);

        return HexOverworldTile.AxialToPixel(coord.X, coord.Y);
    }

    private List<List<Vector2I>> TraceRoadSegments(HashSet<Vector2I> roadTiles)
    {
        var segments = new List<List<Vector2I>>();
        var visitedEdges = new HashSet<(Vector2I, Vector2I)>();
        var rawJunctions = new HashSet<Vector2I>();
        var endpoints = new HashSet<Vector2I>();

        foreach (var coord in roadTiles)
        {
            int neighborCount = CountRoadNeighbors(coord, roadTiles);
            if (neighborCount == 1)
                endpoints.Add(coord);
            else if (neighborCount >= 3)
                rawJunctions.Add(coord);
        }

        NormalizeJunctionCoords(rawJunctions);

        var startPoints = new List<Vector2I>();
        startPoints.AddRange(endpoints);
        startPoints.AddRange(rawJunctions);

        if (startPoints.Count == 0)
        {
            foreach (var tile in roadTiles)
            {
                startPoints.Add(tile);
                break;
            }
        }

        foreach (var start in startPoints)
        {
            foreach (var firstStep in GetRoadNeighbors(start, roadTiles))
            {
                if (visitedEdges.Contains((start, firstStep)))
                    continue;

                var segment = new List<Vector2I> { start };
                var previous = start;
                var current = firstStep;

                while (true)
                {
                    visitedEdges.Add((previous, current));
                    visitedEdges.Add((current, previous));
                    segment.Add(current);

                    if (endpoints.Contains(current) || rawJunctions.Contains(current))
                        break;

                    var next = GetNextRoadTile(current, previous, roadTiles);
                    if (next == null)
                        break;

                    previous = current;
                    current = next.Value;
                }

                if (segment.Count >= 2)
                    segments.Add(segment);
            }
        }

        return segments;
    }

    private void NormalizeJunctionCoords(HashSet<Vector2I> rawJunctions)
    {
        _junctionCoords.Clear();
        _junctionRenderCoords.Clear();

        var canonicalCoords = new List<Vector2I>();
        var orderedJunctions = new List<Vector2I>(rawJunctions);
        orderedJunctions.Sort((a, b) => a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

        foreach (var junction in orderedJunctions)
        {
            Vector2I canonical = junction;
            bool foundNearby = false;

            foreach (var existing in canonicalCoords)
            {
                if (HexOverworldTile.HexDistance(junction.X, junction.Y, existing.X, existing.Y) <= 1)
                {
                    canonical = existing;
                    foundNearby = true;
                    break;
                }
            }

            if (!foundNearby)
            {
                canonicalCoords.Add(junction);
                _junctionCoords.Add(junction);
            }

            _junctionRenderCoords[junction] = canonical;
        }
    }

    private int CountRoadNeighbors(Vector2I coord, HashSet<Vector2I> roadTiles)
    {
        return GetRoadNeighbors(coord, roadTiles).Count;
    }

    private List<Vector2I> GetRoadNeighbors(Vector2I coord, HashSet<Vector2I> roadTiles)
    {
        var result = new List<Vector2I>();
        for (int direction = 0; direction < 6; direction++)
        {
            var neighbor = HexOverworldTile.GetNeighbor(coord.X, coord.Y, direction);
            if (roadTiles.Contains(neighbor) && AreRoadTilesConnected(coord, neighbor))
                result.Add(neighbor);
        }

        return result;
    }

    private Vector2I? GetNextRoadTile(Vector2I current, Vector2I previous, HashSet<Vector2I> roadTiles)
    {
        foreach (var neighbor in GetRoadNeighbors(current, roadTiles))
        {
            if (neighbor != previous)
                return neighbor;
        }

        return null;
    }

    private bool AreRoadTilesConnected(Vector2I a, Vector2I b)
    {
        bool hasDirectionData = false;

        if (_currentBuildTiles.TryGetValue(a, out var tileA) && tileA.RoadDirections != 0)
        {
            hasDirectionData = true;
            if (HasRoadDirectionTo(tileA.RoadDirections, a, b))
                return true;
        }

        if (_currentBuildTiles.TryGetValue(b, out var tileB) && tileB.RoadDirections != 0)
        {
            hasDirectionData = true;
            if (HasRoadDirectionTo(tileB.RoadDirections, b, a))
                return true;
        }

        return !hasDirectionData;
    }

    private static bool HasRoadDirectionTo(int directions, Vector2I from, Vector2I to)
    {
        for (int direction = 0; direction < 6; direction++)
        {
            if ((directions & (1 << direction)) == 0)
                continue;

            var neighbor = HexOverworldTile.GetNeighbor(from.X, from.Y, direction);
            if (neighbor == to)
                return true;
        }

        return false;
    }

    private Vector2[] SmoothPath(List<Vector2> points)
    {
        if (points.Count <= 2)
            return points.ToArray();

        var controlPoints = new List<Vector2> { points[0] };
        for (int i = 3; i < points.Count - 1; i += 3)
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

    private void CreateRoadMeshStrip(Vector2[] path, int roadClass)
    {
        if (path.Length < 2)
            return;

        var profile = GetProfile(roadClass);
        float classWidth = RoadWidth * profile.WidthMul;
        Color classColor = new(
            RoadColor.R * (1.0f - profile.ColorDarken),
            RoadColor.G * (1.0f - profile.ColorDarken * 0.9f),
            RoadColor.B * (1.0f - profile.ColorDarken * 0.8f),
            RoadColor.A * profile.CoreAlpha);

        var processedPath = JitterPath(path, profile.JitterPx);
        if (processedPath.Length < 2 || !HasDrawableLength(processedPath))
            return;

        if (ShadowOffset.LengthSquared() > 0.01f)
        {
            Color edgeShadowColor = new(ShadowColor.R, ShadowColor.G, ShadowColor.B, ShadowColor.A * profile.EdgeAlphaFactor);
            CreateSingleMeshStripWithOffset(processedPath, classWidth * 1.4f, edgeShadowColor, 42, profile.NoiseFreq, profile.NoiseAmp, ShadowOffset);
            CreateSingleMeshStripWithOffset(processedPath, classWidth, ShadowColor, 43, profile.NoiseFreq, profile.NoiseAmp, ShadowOffset);
        }

        if (SideThickness > 0.1f)
        {
            Color sideEdgeColor = new(SideColor.R, SideColor.G, SideColor.B, SideColor.A * profile.EdgeAlphaFactor);
            Vector2 sideOffset = new(0.0f, SideThickness);
            CreateSingleMeshStripWithOffset(processedPath, classWidth * 1.4f, sideEdgeColor, 44, profile.NoiseFreq, profile.NoiseAmp, sideOffset);
            CreateSingleMeshStripWithOffset(processedPath, classWidth, SideColor, 45, profile.NoiseFreq, profile.NoiseAmp, sideOffset);
        }

        Color edgeColor = new(classColor.R, classColor.G, classColor.B, classColor.A * profile.EdgeAlphaFactor);
        CreateSingleMeshStrip(processedPath, classWidth * 1.4f, edgeColor, 46, profile.NoiseFreq, profile.NoiseAmp);
        CreateSingleMeshStrip(processedPath, classWidth, classColor, 47, profile.NoiseFreq, profile.NoiseAmp);
    }

    private void CreateSingleMeshStrip(Vector2[] path, float width, Color color, int zIndex, float noiseFreq, float noiseAmp)
    {
        CreateSingleMeshStripWithOffset(path, width, color, zIndex, noiseFreq, noiseAmp, Vector2.Zero);
    }

    private static bool HasDrawableLength(Vector2[] path)
    {
        for (int i = 1; i < path.Length; i++)
        {
            if (path[i].DistanceSquaredTo(path[i - 1]) > 1.0f)
                return true;
        }

        return false;
    }

    private void CreateSingleMeshStripWithOffset(Vector2[] path, float width, Color color, int zIndex, float noiseFreq, float noiseAmp, Vector2 vertexOffset)
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

            float currentWidth = width * (1.0f + OrganicNoise(cumulativeLength, noiseFreq, noiseAmp));
            float halfWidth = currentWidth * 0.5f;
            float u = cumulativeLength / TextureTileLength;

            vertices[i * 2] = path[i] + normal * halfWidth + vertexOffset;
            vertices[i * 2 + 1] = path[i] - normal * halfWidth + vertexOffset;
            uvs[i * 2] = new Vector2(u, 0.0f);
            uvs[i * 2 + 1] = new Vector2(u, 1.0f);
            colors[i * 2] = color;
            colors[i * 2 + 1] = color;
        }

        var meshInstance = CreateMeshInstance(vertices, uvs, colors, BuildStripIndices(path.Length), zIndex);
        if (_roadTexture != null)
        {
            var shaderMaterial = new ShaderMaterial();
            shaderMaterial.Shader = GetOrCreateRoadShader();
            shaderMaterial.SetShaderParameter("road_texture", _roadTexture);
            meshInstance.Material = shaderMaterial;
        }
        else
        {
            meshInstance.Material = new CanvasItemMaterial();
        }

        AddChild(meshInstance);
        _roadMeshes.Add(meshInstance);
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

    private static Vector2[] JitterPath(Vector2[] path, float strength)
    {
        if (path.Length < 3 || strength < 0.1f)
            return path;

        var result = new Vector2[path.Length];
        result[0] = path[0];
        result[^1] = path[^1];

        float cumulativeLength = 0.0f;
        for (int i = 1; i < path.Length - 1; i++)
        {
            cumulativeLength += path[i].DistanceTo(path[i - 1]);
            Vector2 tangent = (path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            float offset = OrganicNoise(cumulativeLength + i * 17.3f, 0.004f, strength);
            result[i] = path[i] + normal * offset;
        }

        return result;
    }

    private static float OrganicNoise(float distance, float baseFrequency, float amplitude)
    {
        float value = 0.0f;
        float amp = amplitude;
        float frequency = baseFrequency;

        for (int octave = 0; octave < 3; octave++)
        {
            value += (SmoothNoise1D(distance, frequency) - 0.5f) * 2.0f * amp;
            frequency *= 2.3f;
            amp *= 0.45f;
        }

        return value;
    }

    private static float SmoothNoise1D(float x, float frequency)
    {
        float scaled = x * frequency;
        float whole = Mathf.Floor(scaled);
        float fraction = scaled - whole;
        fraction = fraction * fraction * (3.0f - 2.0f * fraction);
        return Mathf.Lerp(Hash1D(whole), Hash1D(whole + 1.0f), fraction);
    }

    private static float Hash1D(float value)
    {
        float raw = Mathf.Sin(value * 127.1f + 311.7f) * 43758.5453f;
        return raw - Mathf.Floor(raw);
    }

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1.0f - t;
        return u * u * u * p0
            + 3.0f * u * u * t * p1
            + 3.0f * u * t * t * p2
            + t * t * t * p3;
    }

    private static Vector2[] GenerateBezierDirect(Vector2 from, Vector2 to)
    {
        Vector2 mid = (from + to) * 0.5f;
        Vector2 direction = (to - from).Normalized();
        Vector2 perpendicular = new(-direction.Y, direction.X);

        float hash = Mathf.Sin(from.X * 0.01f + to.Y * 0.013f) * 0.5f + 0.5f;
        float offset = (hash - 0.5f) * from.DistanceTo(to) * 0.15f;

        Vector2 ctrl1 = from.Lerp(mid, 0.33f) + perpendicular * offset;
        Vector2 ctrl2 = from.Lerp(mid, 0.66f) - perpendicular * offset * 0.5f;

        int steps = Mathf.Max(8, (int)(from.DistanceTo(to) / 80.0f));
        var result = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            result[i] = CubicBezier(from, ctrl1, ctrl2, to, t);
        }

        return result;
    }

    private static Shader? _roadShaderCache;

    private static Shader GetOrCreateRoadShader()
    {
        if (_roadShaderCache != null)
            return _roadShaderCache;

        _roadShaderCache = new Shader
        {
            Code = """
shader_type canvas_item;

uniform sampler2D road_texture : repeat_enable, filter_linear;

void fragment() {
    vec4 tex = texture(road_texture, UV);
    COLOR = tex * COLOR;
}
""",
        };
        return _roadShaderCache;
    }
}
