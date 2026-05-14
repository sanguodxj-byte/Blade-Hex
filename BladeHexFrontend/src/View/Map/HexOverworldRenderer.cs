// HexOverworldRenderer.cs
// T-502: Overworld hex tile MultiMesh batched 2D renderer
// Dictionary<TerrainType, MultiMeshInstance2D> for terrain batching.
// Solid colors from TerrainColorMap (textures will be added in T-504).
// Integrates with ChunkManager for chunk-based tile loading.
// Terrain visibility cache: explored tiles persist when chunks unload.
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.View.Map;

/// <summary>
/// Overworld hex tile batched renderer using MultiMeshInstance2D.
/// One MultiMeshInstance2D per TerrainType (up to 21 buckets).
/// Shared flat-top hex polygon mesh reused across all buckets.
/// Chunk-driven: OnChunksUpdated / OnChunksUnloaded for incremental updates.
/// </summary>
[GlobalClass]
public partial class HexOverworldRenderer : Node2D
{
    // ========================================
    // Constants
    // ========================================

    /// <summary>Hex outer radius — matches HexOverworldTile.HexSize</summary>
    private const float HexRadius = 156.0f;

    // ========================================
    // Per-terrain bucket
    // ========================================

    /// <summary>
    /// One bucket per terrain type. Stores tile positions for MultiMesh rebuild.
    /// </summary>
    private class TerrainBucket
    {
        /// <summary>The MultiMeshInstance2D that renders all tiles of this terrain type.</summary>
        public MultiMeshInstance2D Instance { get; set; } = null!;

        /// <summary>Tile world axial coord → pixel position.</summary>
        public Dictionary<Vector2I, Vector2> TilePositions { get; set; } = new();
    }

    // ========================================
    // Fields
    // ========================================

    /// <summary>ChunkManager reference for active chunk queries (used by RebuildAll).</summary>
    private ChunkManager? _chunkManager;

    /// <summary>FogOfWar reference for explored tile caching.</summary>
    private FogOfWar? _fog;

    /// <summary>Shared flat-top hexagon mesh used by all buckets.</summary>
    private Mesh? _sharedHexMesh;

    /// <summary>Terrain type int → bucket.</summary>
    private readonly Dictionary<int, TerrainBucket> _buckets = new();

    /// <summary>Tile world coord → terrain type int (for unload removal).</summary>
    private readonly Dictionary<Vector2I, int> _tileTerrain = new();

    /// <summary>Chunk coord → set of tile world coords belonging to that chunk (for unload).</summary>
    private readonly Dictionary<Vector2I, HashSet<Vector2I>> _chunkTiles = new();

    /// <summary>
    /// Explored tile cache: stores tile data for all tiles that have been revealed by the fog system.
    /// Persists for the lifetime of the scene — this is the "memory" of what the player has seen.
    /// Tiles in this cache are NOT removed on chunk unload, so they remain rendered (dimmed by fog shader).
    /// </summary>
    private readonly Dictionary<Vector2I, (Vector2 pixelPos, int terrainType)> _exploredTileCache = new();

    // ========================================
    // Public API — Initialization
    // ========================================

    /// <summary>
    /// Initialize the renderer with a ChunkManager reference.
    /// Creates the shared hex mesh and sets node name.
    /// </summary>
    public void Initialize(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;
        Name = "HexOverworldRenderer";
        _sharedHexMesh = CreateHexMesh();
    }

    /// <summary>
    /// Inject the FogOfWar reference for explored tile caching.
    /// Must be called after InitFogOfWar() creates the Fog instance.
    /// </summary>
    public void SetFogOfWar(FogOfWar fog)
    {
        _fog = fog;
    }

    // ========================================
    // Public API — Chunk Events
    // ========================================

    /// <summary>
    /// Called when new chunks are loaded by ChunkManager.UpdateChunks().
    /// Renders ALL tiles from the chunk — fog visibility is handled by the FogOfWarRenderer overlay.
    /// Chunk loading is purely logical; rendering is decoupled from chunk state.
    /// </summary>
    public void OnChunksUpdated(List<ChunkData> newChunks)
    {
        if (_sharedHexMesh == null)
            return;

        var affectedTypes = new HashSet<int>();

        foreach (var chunk in newChunks)
        {
            var chunkCoord = chunk.ChunkCoord;

            // Ensure chunk tracking entry exists
            if (!_chunkTiles.ContainsKey(chunkCoord))
                _chunkTiles[chunkCoord] = new HashSet<Vector2I>();

            foreach (var kvp in chunk.Tiles)
            {
                var tileCoord = kvp.Key;
                var tile = kvp.Value;
                int terrainType = (int)tile.Terrain;

                // Track tile → terrain mapping
                _tileTerrain[tileCoord] = terrainType;
                _chunkTiles[chunkCoord].Add(tileCoord);

                // Always render — fog shader overlay handles visibility
                if (!_exploredTileCache.ContainsKey(tileCoord))
                {
                    AddTileToBucket(terrainType, tileCoord, tile.PixelPos);
                    affectedTypes.Add(terrainType);
                    _exploredTileCache[tileCoord] = (tile.PixelPos, terrainType);
                }
            }
        }

        // Rebuild every bucket that received new tiles
        foreach (var tt in affectedTypes)
        {
            RebuildBucket(tt);
        }
    }

    /// <summary>
    /// Called when chunks are unloaded (player moved out of range).
    /// Tiles are NEVER removed from rendering — they persist permanently.
    /// Chunk unload only affects logical state (pathfinding, encounters), not visuals.
    /// </summary>
    public void OnChunksUnloaded(HashSet<Vector2I> unloadedCoords)
    {
        // 只清理 chunk 跟踪数据，不移除任何渲染 tile
        foreach (var chunkCoord in unloadedCoords)
        {
            _chunkTiles.Remove(chunkCoord);
        }
    }

    // ========================================
    // Public API — Full Rebuild
    // ========================================

    /// <summary>
    /// Full rebuild from all currently active chunks in the ChunkManager.
    /// Clears all existing buckets and re-populates from ActiveChunks.
    /// </summary>
    public void RebuildAll()
    {
        ClearAllBuckets();

        if (_chunkManager == null || _sharedHexMesh == null)
            return;

        var affectedTypes = new HashSet<int>();

        foreach (var kvp in _chunkManager.ActiveChunks)
        {
            var chunk = kvp.Value;
            var chunkCoord = chunk.ChunkCoord;

            if (!_chunkTiles.ContainsKey(chunkCoord))
                _chunkTiles[chunkCoord] = new HashSet<Vector2I>();

            foreach (var tileKvp in chunk.Tiles)
            {
                var tileCoord = tileKvp.Key;
                var tile = tileKvp.Value;
                int terrainType = (int)tile.Terrain;

                _tileTerrain[tileCoord] = terrainType;
                _chunkTiles[chunkCoord].Add(tileCoord);
                AddTileToBucket(terrainType, tileCoord, tile.PixelPos);
                affectedTypes.Add(terrainType);
            }
        }

        foreach (var tt in affectedTypes)
        {
            RebuildBucket(tt);
        }
    }

    // ========================================
    // Public API — Camera Visibility (stub)
    // ========================================

    /// <summary>
    /// Update MultiMeshInstance2D visibility based on camera AABB.
    /// 当前实现：所有 tile 始终渲染（MultiMesh GPU 合批，性能开销极低）。
    /// 迷雾遮挡由 FogOfWarRenderer shader overlay 处理。
    /// 如果未来地图规模超过 10000+ tile 导致 GPU 瓶颈，可在此实现 chunk 级剔除。
    /// </summary>
    public void UpdateVisibility(Rect2 cameraAABB)
    {
        // MultiMesh 渲染已经是 GPU 合批，无需 CPU 侧剔除。
        // 实测 256×192 tile 地图（~49000 tile）在中端 GPU 上无压力。
    }

    /// <summary>
    /// Legacy stub — no longer needed since all tiles are always rendered.
    /// Fog visibility is handled entirely by the FogOfWarRenderer shader overlay.
    /// </summary>
    public void SyncWithFog()
    {
        // No-op: tiles are always rendered, fog shader handles visibility
    }

    // ========================================
    // Internal — Bucket Management
    // ========================================

    /// <summary>
    /// Add a single tile's position to the appropriate terrain bucket.
    /// Creates the bucket and its MultiMeshInstance2D if it doesn't exist yet.
    /// </summary>
    private void AddTileToBucket(int terrainType, Vector2I tileCoord, Vector2 pixelPos)
    {
        if (!_buckets.TryGetValue(terrainType, out var bucket))
        {
            bucket = new TerrainBucket();
            bucket.Instance = new MultiMeshInstance2D();
            bucket.Instance.Name = $"Terrain_{((HexOverworldTile.TerrainType)terrainType).ToString()}";

            var profile = TerrainVisualRegistry.Get((HexOverworldTile.TerrainType)terrainType);

            // 尝试加载地形纹理（有纹理则启用纹理模式，否则回退纯色）
            var terrainTex = TryLoadOverworldTexture(profile);
            bool hasTexture = terrainTex != null;

            // 应用六边形瓦片 shader（SDF 裁剪 + 边缘羽化）
            var shader = GD.Load<Shader>("res://src/assets/shaders/overworld_hex_tile.gdshader");
            if (shader != null)
            {
                var mat = new ShaderMaterial();
                mat.Shader = shader;
                mat.SetShaderParameter("use_texture", hasTexture);
                mat.SetShaderParameter("feather_width", 0.08f);
                mat.SetShaderParameter("edge_darken", 0.06f);

                if (hasTexture)
                {
                    mat.SetShaderParameter("terrain_texture", terrainTex!);
                    mat.SetShaderParameter("texture_scale", 1.0f);
                }

                bucket.Instance.Material = mat;
            }

            // 纹理模式下不需要 Modulate 着色（纹理自带颜色），纯色模式仍用 DominantColor
            bucket.Instance.Modulate = hasTexture ? Colors.White : profile.DominantColor;

            if (hasTexture)
                GD.Print($"[HexOverworldRenderer] 纹理模式: {profile.DisplayName} ({profile.OverworldKey})");

            AddChild(bucket.Instance);
            _buckets[terrainType] = bucket;
        }

        bucket.TilePositions[tileCoord] = pixelPos;
    }

    /// <summary>
    /// 尝试加载大地图地形贴图（variant 0）。
    /// 仅从 overworld/ 目录加载新资产，不回退旧 hex_terrain 资产。
    /// </summary>
    private static Texture2D? TryLoadOverworldTexture(TerrainVisualProfile profile)
    {
        string path = $"{HexOverworldTile.OverworldTextureBasePath}/{profile.OverworldKey}_0.png";
        if (ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);
        return null;
    }

    /// <summary>
    /// Rebuild the MultiMesh for a single terrain bucket from its stored positions.
    /// Creates a new MultiMesh with one instance per tile position.
    /// </summary>
    private void RebuildBucket(int terrainType)
    {
        if (!_buckets.TryGetValue(terrainType, out var bucket))
            return;

        int count = bucket.TilePositions.Count;
        var mmi = bucket.Instance;

        if (count == 0)
        {
            mmi.Multimesh = null;
            return;
        }

        // Create or resize the MultiMesh
        var mm = mmi.Multimesh;
        if (mm == null || mm.InstanceCount != count)
        {
            mm = new MultiMesh();
            mm.Mesh = _sharedHexMesh;
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform2D;
            mm.UseColors = false; // Tinting via MultiMeshInstance2D.Modulate instead
            mm.InstanceCount = count;
            mmi.Multimesh = mm;
        }

        // Fill instance transforms (position only, no rotation/scale)
        int i = 0;
        foreach (var kvp in bucket.TilePositions)
        {
            Vector2 pos = kvp.Value;
            var xform = Transform2D.Identity.Translated(pos);
            mm.SetInstanceTransform2D(i, xform);
            i++;
        }
    }

    /// <summary>
    /// Remove MultiMeshInstance2D nodes for buckets that have no tiles.
    /// </summary>
    private void RemoveEmptyBuckets()
    {
        var toRemove = new List<int>();

        foreach (var kvp in _buckets)
        {
            if (kvp.Value.TilePositions.Count == 0)
            {
                kvp.Value.Instance.QueueFree();
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var key in toRemove)
        {
            _buckets.Remove(key);
        }
    }

    /// <summary>
    /// Clear all buckets, tile tracking, and chunk tracking. Used by RebuildAll.
    /// </summary>
    private void ClearAllBuckets()
    {
        foreach (var kvp in _buckets)
        {
            kvp.Value.Instance.QueueFree();
        }

        _buckets.Clear();
        _tileTerrain.Clear();
        _chunkTiles.Clear();
    }

    // ========================================
    // Hex Mesh Creation
    // ========================================

    /// <summary>
    /// Create a flat-top hexagon mesh (2D, centered at origin).
    /// Triangle fan: center + 6 perimeter vertices.
    /// UVs map the hexagon to [0,1]² for future texture support (T-504).
    /// </summary>
    private static Mesh CreateHexMesh()
    {
        float R = HexRadius;
        float H = R * Mathf.Sqrt(3.0f) / 2.0f; // half-height of flat-top hex

        // Flat-top hex vertices (centered at origin, Godot Y-down).
        // Order: center, then clockwise from right.
        Vector2[] verts =
        [
            Vector2.Zero,                // 0: center
            new Vector2(R, 0.0f),        // 1: right
            new Vector2(R * 0.5f, H),    // 2: bottom-right
            new Vector2(-R * 0.5f, H),   // 3: bottom-left
            new Vector2(-R, 0.0f),       // 4: left
            new Vector2(-R * 0.5f, -H),  // 5: top-left
            new Vector2(R * 0.5f, -H),   // 6: top-right
        ];

        // Triangle fan: 6 triangles
        int[] tris =
        [
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 5,
            0, 5, 6,
            0, 6, 1,
        ];

        // UV bounds: the hex fits within 2R × 2H
        float halfW = R;
        float halfH = H;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < verts.Length; i++)
        {
            var v = verts[i];

            // UV: map [-R, R] → [0, 1] for X, [-H, H] → [0, 1] for Y
            float u = 0.5f + v.X / (2.0f * halfW);
            float vCoord = 0.5f + v.Y / (2.0f * halfH);

            st.SetColor(Colors.White);
            st.SetUV(new Vector2(u, vCoord));
            st.AddVertex(new Vector3(v.X, v.Y, 0.0f));
        }

        for (int i = 0; i < tris.Length; i += 3)
        {
            st.AddIndex(tris[i]); st.AddIndex(tris[i + 1]); st.AddIndex(tris[i + 2]);
        }

        st.GenerateNormals();

        var mesh = st.Commit();
        mesh.ResourceName = "HexOverworldTileMesh";
        return mesh;
    }

    // ========================================
    // Terrain Color (debug helper, currently unused after switching to SSOT)
    // ========================================

    /// <summary>
    /// Get the dominant color for a terrain type (via SSOT TerrainVisualRegistry).
    /// Kept as public helper for debug overlays / minimap; not called by the batched path anymore.
    /// </summary>
    public static Color GetTerrainColor(int terrainType)
    {
        return TerrainVisualRegistry.Get((HexOverworldTile.TerrainType)terrainType).DominantColor;
    }
}
