using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.Map;

public partial class OverworldPropRenderer2D : Node2D
{
    private const float SpriteBaseHeight = 200.0f;

    private const float HexInnerRadiusPixels = 105.0f;

    private const float LodHideThreshold = 0.45f;
    private const float LodShowThreshold = 0.55f;

    private const string SpriteBaseDir = "res://BladeHexFrontend/src/assets/tiles/overworld";

    private sealed class TerrainScatterConfig
    {

        public int MaxPerHex;

        public int MinPerHex;

        public float BaseSize;

        public float InteriorSizeBoost;
    }

    private static readonly Dictionary<HexOverworldTile.TerrainType, TerrainScatterConfig> ScatterTable = new()
    {

        [HexOverworldTile.TerrainType.Forest]       = new() { MaxPerHex = 4, MinPerHex = 1, BaseSize = 1.50f, InteriorSizeBoost = 0.4f },
        [HexOverworldTile.TerrainType.DenseForest]  = new() { MaxPerHex = 5, MinPerHex = 2, BaseSize = 1.70f, InteriorSizeBoost = 0.4f },
        [HexOverworldTile.TerrainType.Jungle]       = new() { MaxPerHex = 5, MinPerHex = 2, BaseSize = 1.60f, InteriorSizeBoost = 0.4f },
        [HexOverworldTile.TerrainType.Taiga]        = new() { MaxPerHex = 5, MinPerHex = 2, BaseSize = 1.50f, InteriorSizeBoost = 0.4f },

        [HexOverworldTile.TerrainType.Mountain]     = new() { MaxPerHex = 2, MinPerHex = 1, BaseSize = 7.0f, InteriorSizeBoost = 0.5f },
        [HexOverworldTile.TerrainType.MountainSnow] = new() { MaxPerHex = 2, MinPerHex = 1, BaseSize = 7.0f, InteriorSizeBoost = 0.5f },

        [HexOverworldTile.TerrainType.Hills]      = new() { MaxPerHex = 3, MinPerHex = 1, BaseSize = 1.20f, InteriorSizeBoost = 0.3f },
        [HexOverworldTile.TerrainType.Rocky]      = new() { MaxPerHex = 3, MinPerHex = 1, BaseSize = 1.00f, InteriorSizeBoost = 0.3f },
        [HexOverworldTile.TerrainType.Swamp]      = new() { MaxPerHex = 3, MinPerHex = 1, BaseSize = 0.90f, InteriorSizeBoost = 0.3f },
        [HexOverworldTile.TerrainType.Bog]        = new() { MaxPerHex = 3, MinPerHex = 1, BaseSize = 0.80f, InteriorSizeBoost = 0.3f },
        [HexOverworldTile.TerrainType.Sand]       = new() { MaxPerHex = 2, MinPerHex = 1, BaseSize = 0.70f, InteriorSizeBoost = 0.2f },
        [HexOverworldTile.TerrainType.Savanna]    = new() { MaxPerHex = 3, MinPerHex = 1, BaseSize = 1.00f, InteriorSizeBoost = 0.2f },
        [HexOverworldTile.TerrainType.Wasteland]  = new() { MaxPerHex = 2, MinPerHex = 1, BaseSize = 0.70f, InteriorSizeBoost = 0.2f },
        [HexOverworldTile.TerrainType.Snow]       = new() { MaxPerHex = 2, MinPerHex = 1, BaseSize = 0.60f, InteriorSizeBoost = 0.2f },
        [HexOverworldTile.TerrainType.Ice]        = new() { MaxPerHex = 1, MinPerHex = 1, BaseSize = 0.60f, InteriorSizeBoost = 0.2f },

        [HexOverworldTile.TerrainType.Grassland]  = new() { MaxPerHex = 3, MinPerHex = 1, BaseSize = 0.80f, InteriorSizeBoost = 0.2f },
        [HexOverworldTile.TerrainType.Plains]     = new() { MaxPerHex = 2, MinPerHex = 1, BaseSize = 0.70f, InteriorSizeBoost = 0.2f },

        [HexOverworldTile.TerrainType.DeepWater]    = new() { MaxPerHex = 0, MinPerHex = 0, BaseSize = 0.0f, InteriorSizeBoost = 0.0f },
        [HexOverworldTile.TerrainType.ShallowWater] = new() { MaxPerHex = 0, MinPerHex = 0, BaseSize = 0.0f, InteriorSizeBoost = 0.0f },
        [HexOverworldTile.TerrainType.River]        = new() { MaxPerHex = 0, MinPerHex = 0, BaseSize = 0.0f, InteriorSizeBoost = 0.0f },

        [HexOverworldTile.TerrainType.Road] = new() { MaxPerHex = 0, MinPerHex = 0, BaseSize = 0.0f, InteriorSizeBoost = 0.0f },
    };

    private sealed class PropData
    {
        public Vector2I PrimaryCoord;
        public Vector2I? SecondaryCoord;
        public Vector2 Position;
        public Rect2 SrcRect;
        public Rect2 DstRect;
        public Texture2D? Texture;
        public bool FlipH;
    }

    private readonly Dictionary<string, List<PropData>> _propsByTexture = new();
    private readonly Dictionary<string, Texture2D?> _textureCache = new();
    private readonly HashSet<Vector2I> _loadedTiles = new();
    private readonly Dictionary<Vector2I, int> _mountainPatchSizes = new();
    private readonly Dictionary<Vector2I, int> _mountainDistToEdge = new();
    private readonly Dictionary<Vector2I, Vector2I> _mountainNearestEdgeCoord = new();
    private readonly Dictionary<Vector2I, int> _forestDistToEdge = new();

    private readonly List<RidgeSamplePoint> _ridgeSamplePoints = new();

    private readonly HashSet<int> _processedRidgeIndices = new();
    private readonly Dictionary<Vector2I, RidgeSamplePoint> _ridgeSampleByHex = new();
    private readonly HashSet<Vector2I> _ridgeHexCovered = new();

    private readonly Dictionary<string, int> _availableVariants = new();
    private int _worldSeed;
    private HexOverworldGrid? _grid;

    private IReadOnlyDictionary<Vector2I, ChunkData>? _knownChunks;
    private bool _layerVisible = true;
    private bool _dirty = true;

    private float _densityFactor = 1.0f;

    public void Initialize(int worldSeed, HexOverworldGrid grid)
    {
        Name = "OverworldPropRenderer2D";
        _worldSeed = worldSeed;
        _grid = grid;
        _knownChunks = null;

        YSortEnabled = true;
        ZIndex = 30;

        ProbeAvailableVariants();

        PrecalculateMapData();
    }

    public void InitializeFromChunks(int worldSeed, ChunkManager chunkManager)
    {
        Name = "OverworldPropRenderer2D";
        _worldSeed = worldSeed;
        _grid = null;
        _knownChunks = chunkManager.AllKnownChunks;
        GD.Print($"[OverworldPropRenderer2D] InitializeFromChunks: 使用 {_knownChunks.Count} 个已知 chunk 进行预计算");

        YSortEnabled = true;
        ZIndex = 30;

        ProbeAvailableVariants();

        PrecalculateMapData();
    }

    private void ProbeAvailableVariants()
    {
        var allTerrainKeys = new HashSet<string>();
        foreach (var profile in TerrainVisualRegistry.All())
            allTerrainKeys.Add(profile.OverworldKey);

        int totalAvailable = 0;
        foreach (var terrainKey in allTerrainKeys)
        {
            int count = 0;

            for (int v = 0; v < 32; v++)
            {
                string path = $"{SpriteBaseDir}/{terrainKey}_{v}.png";
                if (ResourceLoader.Exists(path))
                    count++;
                else if (v >= 1)
                    break;
            }
            _availableVariants[terrainKey] = count;
            totalAvailable += count;
        }

        var availableTerrains = new List<string>();
        foreach (var kvp in _availableVariants)
            if (kvp.Value > 0) availableTerrains.Add($"{kvp.Key}({kvp.Value})");
        GD.Print($"[OverworldPropRenderer2D] 磁盘可用精灵: {totalAvailable} 个变体, 地形: [{string.Join(", ", availableTerrains)}]");
    }

    private static bool IsForest(HexOverworldTile.TerrainType terrain)
    {
        return terrain == HexOverworldTile.TerrainType.Forest ||
               terrain == HexOverworldTile.TerrainType.DenseForest ||
               terrain == HexOverworldTile.TerrainType.Jungle ||
               terrain == HexOverworldTile.TerrainType.Taiga;
    }

    private void PrecalculateMapData()
    {

        IEnumerable<HexOverworldTile> allTiles;
        if (_knownChunks != null && _knownChunks.Count > 0)
        {
            allTiles = EnumerateKnownTiles();
        }
        else if (_grid != null && _grid.Tiles.Count > 0)
        {
            allTiles = _grid.Tiles.Values;
        }
        else
        {
            GD.PrintErr("[OverworldPropRenderer2D] PrecalculateMapData: 没有可用的全局 tile 数据，预计算跳过！山脉/森林将退化为单点散布。");
            return;
        }

        _mountainPatchSizes.Clear();
        var visitedPatch = new HashSet<Vector2I>();
        var mountainPatches = new List<List<HexOverworldTile>>();
        foreach (var tile in allTiles)
        {
            if (!IsMountain(tile.Terrain)) continue;
            if (visitedPatch.Contains(tile.Coord)) continue;

            var patch = new List<HexOverworldTile>();
            var queue = new Queue<HexOverworldTile>();
            queue.Enqueue(tile);
            visitedPatch.Add(tile.Coord);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                patch.Add(current);

                foreach (var n in GetNeighborTiles(current.Coord))
                {
                    if (IsMountain(n.Terrain) && !visitedPatch.Contains(n.Coord))
                    {
                        visitedPatch.Add(n.Coord);
                        queue.Enqueue(n);
                    }
                }
            }

            int size = patch.Count;
            foreach (var pTile in patch)
                _mountainPatchSizes[pTile.Coord] = size;
            mountainPatches.Add(patch);
        }

        _mountainDistToEdge.Clear();
        _mountainNearestEdgeCoord.Clear();
        var queueDist = new Queue<Vector2I>();
        foreach (var tile in allTiles)
        {
            if (IsMountain(tile.Terrain))
            {
                _mountainDistToEdge[tile.Coord] = 9999;
            }
            else
            {
                _mountainDistToEdge[tile.Coord] = 0;
                _mountainNearestEdgeCoord[tile.Coord] = tile.Coord;
                queueDist.Enqueue(tile.Coord);
            }
        }

        while (queueDist.Count > 0)
        {
            var curr = queueDist.Dequeue();
            int currDist = _mountainDistToEdge[curr];
            Vector2I nearest = _mountainNearestEdgeCoord[curr];

            foreach (var n in GetNeighborTiles(curr))
            {
                if (_mountainDistToEdge.TryGetValue(n.Coord, out var dist))
                {
                    if (currDist + 1 < dist)
                    {
                        _mountainDistToEdge[n.Coord] = currDist + 1;
                        _mountainNearestEdgeCoord[n.Coord] = nearest;
                        queueDist.Enqueue(n.Coord);
                    }
                }
            }
        }

        _forestDistToEdge.Clear();
        var queueForest = new Queue<Vector2I>();
        foreach (var tile in allTiles)
        {
            if (IsForest(tile.Terrain))
            {
                _forestDistToEdge[tile.Coord] = 9999;
            }
            else
            {
                _forestDistToEdge[tile.Coord] = 0;
                queueForest.Enqueue(tile.Coord);
            }
        }

        while (queueForest.Count > 0)
        {
            var curr = queueForest.Dequeue();
            int currDist = _forestDistToEdge[curr];

            foreach (var n in GetNeighborTiles(curr))
            {
                if (_forestDistToEdge.TryGetValue(n.Coord, out var dist))
                {
                    if (currDist + 1 < dist)
                    {
                        _forestDistToEdge[n.Coord] = currDist + 1;
                        queueForest.Enqueue(n.Coord);
                    }
                }
            }
        }

        _ridgeSamplePoints.Clear();
        _processedRidgeIndices.Clear();
        _ridgeSampleByHex.Clear();
        _ridgeHexCovered.Clear();
        Dictionary<Vector2I, HexOverworldTile> tileLookup;
        if (_knownChunks != null && _knownChunks.Count > 0)
            tileLookup = BuildKnownTileLookup();
        else if (_grid != null)
            tileLookup = new Dictionary<Vector2I, HexOverworldTile>(_grid.Tiles);
        else
            tileLookup = new Dictionary<Vector2I, HexOverworldTile>();

        foreach (var patch in mountainPatches)
        {
            var samples = MountainRidgeExtractor.ExtractRidge(
                patch, tileLookup, _mountainDistToEdge, _mountainNearestEdgeCoord, _worldSeed);
            _ridgeSamplePoints.AddRange(samples);
        }

        // 构建山脊覆盖查找：将采样点映射到 hex 坐标，并标记覆盖范围内的 hex
        foreach (var sp in _ridgeSamplePoints)
        {
            if (!_ridgeSampleByHex.ContainsKey(sp.HexCoord))
                _ridgeSampleByHex[sp.HexCoord] = sp;

            foreach (var ring in HexNeighborRing(sp.HexCoord, 1))
                _ridgeHexCovered.Add(ring);
            _ridgeHexCovered.Add(sp.HexCoord);
        }

        int mtnCount = _mountainDistToEdge.Count;
        int forestCount = _forestDistToEdge.Count;
        GD.Print($"[OverworldPropRenderer2D] PrecalculateMapData 完成: 山脉坐标={mtnCount}, 林地坐标={forestCount}, 山脊采样点={_ridgeSamplePoints.Count}, 山脊覆盖hex={_ridgeHexCovered.Count}, 连通块={mountainPatches.Count}");
    }

    private IEnumerable<HexOverworldTile> GetNeighborTiles(Vector2I coord)
    {

        if (_knownChunks != null)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                var nCoord = HexOverworldTile.GetNeighbor(coord.X, coord.Y, dir);
                var nTile = GetKnownTile(nCoord);
                if (nTile != null)
                    yield return nTile;
            }
            yield break;
        }

        if (_grid != null)
        {
            foreach (var n in _grid.GetNeighbors(coord.X, coord.Y))
                yield return n;
        }
    }

    private HexOverworldTile? GetTileFromAny(Vector2I coord)
    {
        var knownTile = GetKnownTile(coord);
        if (knownTile != null)
            return knownTile;
        return _grid?.GetTile(coord.X, coord.Y);
    }

    private IEnumerable<HexOverworldTile> EnumerateKnownTiles()
    {
        if (_knownChunks == null)
            yield break;

        foreach (var chunk in _knownChunks.Values)
        {
            foreach (var tile in chunk.Tiles.Values)
                yield return tile;
        }
    }

    private HexOverworldTile? GetKnownTile(Vector2I coord)
    {
        if (_knownChunks == null)
            return null;

        var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
        return _knownChunks.TryGetValue(chunkCoord, out var chunk)
            ? chunk.GetTile(coord.X, coord.Y)
            : null;
    }

    private Dictionary<Vector2I, HexOverworldTile> BuildKnownTileLookup()
    {
        var lookup = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var tile in EnumerateKnownTiles())
            lookup[tile.Coord] = tile;
        return lookup;
    }

    public void LoadPropsForTiles(IEnumerable<HexOverworldTile> tiles)
    {

        bool hasData = (_grid != null && _grid.Tiles.Count > 0) || (_knownChunks != null && _knownChunks.Count > 0);
        if (!hasData) return;

        bool anyNew = false;
        int processedCount = 0;
        int skippedConfig = 0;
        int skippedNoAsset = 0;
        int skippedZeroCount = 0;
        int spritesAdded = 0;
        var mountainTilesToProcess = new List<HexOverworldTile>();
        var forestTilesToProcess = new List<HexOverworldTile>();

        foreach (var tile in tiles)
        {
            if (!_loadedTiles.Add(tile.Coord)) continue;
            processedCount++;

            if (IsMountain(tile.Terrain))
            {
                mountainTilesToProcess.Add(tile);
                continue;
            }

            if (IsForest(tile.Terrain))
            {
                forestTilesToProcess.Add(tile);
                continue;
            }

            if (!ScatterTable.TryGetValue(tile.Terrain, out var config) || config.MaxPerHex <= 0)
            { skippedConfig++; continue; }

            var profile = TerrainVisualRegistry.Get(tile.Terrain);
            string terrainKey = profile.OverworldKey;

            string actualKey = ResolveTerrainKeyWithFallback(terrainKey);
            int diskVariantCount = _availableVariants.GetValueOrDefault(actualKey, 0);
            if (diskVariantCount <= 0) { skippedNoAsset++; continue; }

            float interiorFactor = ComputeInteriorFactor(tile);

            float nutrientMul = ComputeNutrientMultiplier(tile);

            int maxPerHex = config.MaxPerHex;
            int minPerHex = config.MinPerHex;
            float baseSize = config.BaseSize;
            float interiorSizeBoost = config.InteriorSizeBoost;
            float customInnerRadius = HexInnerRadiusPixels;

            int spriteCount = Mathf.RoundToInt(
                Mathf.Lerp(minPerHex, maxPerHex, interiorFactor) * nutrientMul);
            if (spriteCount <= 0) { skippedZeroCount++; continue; }

            float combined = tile.Temperature * tile.Nutrient;
            float envSizeBoost = Mathf.Lerp(0.80f, 1.20f, combined);
            float sizeMultiplier = (1.0f + interiorSizeBoost * interiorFactor) * envSizeBoost;

            ScatterPropsInHex(tile, actualKey, diskVariantCount,
                               spriteCount, baseSize * sizeMultiplier,
                               baseSize, customInnerRadius);
            spritesAdded += spriteCount;
            anyNew = true;
        }

        if (mountainTilesToProcess.Count > 0)
        {
            anyNew = true;
            foreach (var tile in mountainTilesToProcess)
            {
                if (_ridgeSampleByHex.TryGetValue(tile.Coord, out var sp))
                {
                    // 沿山脊骨架线放置：使用 MountainRidgeExtractor 计算的精确位置和缩放
                    PlaceSpriteFromRidgeSample(tile, sp);
                    spritesAdded++;
                }
                else if (_ridgeHexCovered.Contains(tile.Coord))
                {
                    // 被山脊覆盖但无直接采样点的 hex：由相邻采样点的精灵视觉覆盖，跳过
                    continue;
                }
                else
                {
                    bool isRidge = IsRidgeTile(tile);

                    if (isRidge)
                    {
                        // 未被山脊骨架覆盖的 ridge tile（回退路径）
                        AddMountainProp(tile, tile.PixelPos, isRidge: true, tileB: null);
                        spritesAdded++;

                        foreach (var n in GetNeighborTiles(tile.Coord))
                        {
                            if (!_loadedTiles.Contains(n.Coord)) continue;
                            if (IsMountain(n.Terrain) && IsRidgeTile(n))
                            {
                                bool isResponsible = tile.Coord.X < n.Coord.X || (tile.Coord.X == n.Coord.X && tile.Coord.Y < n.Coord.Y);
                                if (isResponsible)
                                {
                                    Vector2 midPos = (tile.PixelPos + n.PixelPos) * 0.5f;
                                    AddMountainProp(tile, midPos, isRidge: true, tileB: n);
                                    spritesAdded++;
                                }
                            }
                        }
                    }
                    else
                    {
                        AddMountainProp(tile, tile.PixelPos, isRidge: false, tileB: null);
                        spritesAdded++;
                    }
                }
            }
        }

        if (forestTilesToProcess.Count > 0)
        {
            anyNew = true;
            foreach (var tile in forestTilesToProcess)
            {
                int dist = _forestDistToEdge.GetValueOrDefault(tile.Coord, 1);
                float nutrientMul = ComputeNutrientMultiplier(tile);

                var config = ScatterTable[tile.Terrain];
                float baseScale = config.BaseSize;

                if (dist >= 2 && nutrientMul >= 0.85f)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        uint pSalt = (uint)((tile.Coord.X * 73856093) ^ (tile.Coord.Y * 19349663) ^ (i * 83492791));
                        float pAngle = (Hash(pSalt) / (float)uint.MaxValue) * Mathf.Tau;
                        float pRadius = Mathf.Sqrt(Hash(pSalt + 0xABCDu) / (float)uint.MaxValue) * 45.0f;
                        Vector2 offsetPos = tile.PixelPos + new Vector2(Mathf.Cos(pAngle) * pRadius, Mathf.Sin(pAngle) * pRadius);
                        AddForestTreeProp(tile, offsetPos, baseScale, tileB: null);
                        spritesAdded++;
                    }

                    foreach (var n in GetNeighborTiles(tile.Coord))
                    {
                        if (!_loadedTiles.Contains(n.Coord)) continue;
                        if (IsForest(n.Terrain) && _forestDistToEdge.GetValueOrDefault(n.Coord, 1) >= 2)
                        {
                            bool isResponsible = tile.Coord.X < n.Coord.X || (tile.Coord.X == n.Coord.X && tile.Coord.Y < n.Coord.Y);
                            if (isResponsible)
                            {
                                Vector2 midPos = (tile.PixelPos + n.PixelPos) * 0.5f;
                                AddForestTreeProp(tile, midPos, baseScale, tileB: n);
                                spritesAdded++;
                            }
                        }
                    }
                }
                else
                {
                    int treeCount = Mathf.RoundToInt(
                        Mathf.Lerp(config.MinPerHex, config.MaxPerHex, ComputeInteriorFactor(tile)) * nutrientMul);

                    if (dist == 1 && treeCount > 2) treeCount = 2;

                    for (int i = 0; i < treeCount; i++)
                    {
                        uint pSalt = (uint)((tile.Coord.X * 73856093) ^ (tile.Coord.Y * 19349663) ^ (i * 83492791) ^ 0xEEEEu);
                        float pAngle = (Hash(pSalt) / (float)uint.MaxValue) * Mathf.Tau;
                        float pRadius = Mathf.Sqrt(Hash(pSalt + 0xABCDu) / (float)uint.MaxValue) * HexInnerRadiusPixels;
                        Vector2 offsetPos = tile.PixelPos + new Vector2(Mathf.Cos(pAngle) * pRadius, Mathf.Sin(pAngle) * pRadius);
                        AddForestTreeProp(tile, offsetPos, baseScale, tileB: null);
                        spritesAdded++;
                    }
                }
            }
        }

        GD.Print($"[OverworldPropRenderer2D] LoadProps: processed={processedCount}, skipped(config={skippedConfig}, noAsset={skippedNoAsset}, zeroCount={skippedZeroCount}), spritesAdded={spritesAdded}, totalProps={PropCount}");

        if (anyNew)
        {
            _dirty = true;
            QueueRedraw();
        }
    }

    public void ClearAll()
    {
        _propsByTexture.Clear();
        _loadedTiles.Clear();
        _dirty = true;
        QueueRedraw();
    }

    public void UnloadTiles(IEnumerable<Vector2I> coords)
    {
        var coordSet = coords is HashSet<Vector2I> existingSet
            ? existingSet
            : new HashSet<Vector2I>(coords);
        if (coordSet.Count == 0)
            return;

        bool anyRemoved = false;
        foreach (var coord in coordSet)
            anyRemoved |= _loadedTiles.Remove(coord);

        foreach (var props in _propsByTexture.Values)
        {
            int before = props.Count;
            props.RemoveAll(prop =>
                coordSet.Contains(prop.PrimaryCoord)
                || (prop.SecondaryCoord.HasValue && coordSet.Contains(prop.SecondaryCoord.Value)));
            anyRemoved |= props.Count != before;
        }

        if (!anyRemoved)
            return;

        _dirty = true;
        QueueRedraw();
    }

    public void ReloadTiles(IEnumerable<HexOverworldTile> tiles)
    {
        var tileList = new List<HexOverworldTile>();
        var coords = new HashSet<Vector2I>();
        foreach (var tile in tiles)
        {
            if (!coords.Add(tile.Coord))
                continue;

            tileList.Add(tile);
        }

        if (tileList.Count == 0)
            return;

        UnloadTiles(coords);
        LoadPropsForTiles(tileList);
    }

    public int PropCount
    {
        get
        {
            int count = 0;
            foreach (var kvp in _propsByTexture) count += kvp.Value.Count;
            return count;
        }
    }

    public void UpdateLOD(float zoomLevel)
    {

        float newDensity;
        if (zoomLevel < 0.45f)
            newDensity = 0.0f;
        else if (zoomLevel < 0.6f)
            newDensity = (zoomLevel - 0.45f) / 0.15f * 0.4f;
        else if (zoomLevel < 1.0f)
            newDensity = 0.4f + (zoomLevel - 0.6f) / 0.4f * 0.6f;
        else
            newDensity = 1.0f;

        if (Mathf.Abs(newDensity - _densityFactor) < 0.05f) return;

        _densityFactor = newDensity;
        Visible = _densityFactor > 0.01f;

        if (Visible)
        {
            _dirty = true;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!_dirty || !Visible) return;

        foreach (var kvp in _propsByTexture)
        {
            var texture = GetOrLoadTexture(kvp.Key);
            if (texture == null) continue;

            foreach (var prop in kvp.Value)
            {

                uint hash = Hash((uint)(prop.Position.X * 73856093) ^ (uint)(prop.Position.Y * 19349663));
                float normalized = hash / (float)uint.MaxValue;
                if (normalized > _densityFactor) continue;

                var srcRect = new Rect2(0, 0, texture.GetWidth(), texture.GetHeight());
                var dstRect = prop.DstRect;

                if (prop.FlipH)
                {
                    srcRect = new Rect2(srcRect.Size.X, 0, -srcRect.Size.X, srcRect.Size.Y);
                }

                DrawTextureRectRegion(texture, dstRect, srcRect);
            }
        }

        _dirty = false;
    }

    private static bool IsMountainKey(string key)
    {
        return key == "mountain" || key == "mountain_snow";
    }

    private string ResolveTerrainKeyWithFallback(string terrainKey)
    {
        string resolved = ResolveTerrainKeyWithFallbackInternal(terrainKey);

        if (!IsMountainKey(terrainKey) && IsMountainKey(resolved))
        {
            if (_availableVariants.GetValueOrDefault("grassland", 0) > 0) return "grassland";
            if (_availableVariants.GetValueOrDefault("plains", 0) > 0) return "plains";
            return terrainKey;
        }
        return resolved;
    }

    private string ResolveTerrainKeyWithFallbackInternal(string terrainKey)
    {
        if (_availableVariants.GetValueOrDefault(terrainKey, 0) > 0)
            return terrainKey;

        string fallback = terrainKey switch
        {
            "dense_forest" => "forest",
            "jungle"       => "forest",
            "taiga"        => "forest",
            "mountain_snow" => "mountain",
            "rocky"        => "hills",
            "savanna"      => "plains",
            "wasteland"    => "plains",
            "swamp"        => "plains",
            "bog"          => "plains",
            "snow"         => "plains",
            "ice"          => "plains",
            "sand"         => "plains",
            "grassland"    => "plains",
            "shallow_water" => "plains",
            "deep_water"   => "plains",
            "river"        => "plains",
            _              => "plains",
        };

        if (_availableVariants.GetValueOrDefault(fallback, 0) > 0) return fallback;
        if (_availableVariants.GetValueOrDefault("plains", 0) > 0) return "plains";
        if (_availableVariants.GetValueOrDefault("grassland", 0) > 0) return "grassland";
        if (_availableVariants.GetValueOrDefault("hills", 0) > 0) return "hills";
        return terrainKey;
    }

    private float ComputeInteriorFactor(HexOverworldTile tile)
    {
        int sameCount = 0;
        foreach (var n in GetNeighborTiles(tile.Coord))
        {
            if (n.Terrain == tile.Terrain) sameCount++;
        }
        return sameCount / 6.0f;
    }

    private static float ComputeNutrientMultiplier(HexOverworldTile tile)
    {

        float combined = tile.Temperature * tile.Nutrient;

        float sensitivity = tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Forest or
            HexOverworldTile.TerrainType.DenseForest or
            HexOverworldTile.TerrainType.Jungle => 0.9f,

            HexOverworldTile.TerrainType.Grassland or
            HexOverworldTile.TerrainType.Plains or
            HexOverworldTile.TerrainType.Savanna or
            HexOverworldTile.TerrainType.Taiga => 0.7f,

            HexOverworldTile.TerrainType.Swamp or
            HexOverworldTile.TerrainType.Bog => 0.5f,

            HexOverworldTile.TerrainType.Hills or
            HexOverworldTile.TerrainType.Rocky => 0.35f,

            HexOverworldTile.TerrainType.Mountain or
            HexOverworldTile.TerrainType.MountainSnow => 0.25f,

            _ => 0.15f,
        };

        return Mathf.Lerp(1.0f - sensitivity * 0.6f, 1.0f + sensitivity * 0.5f, combined);
    }

    private void ScatterPropsInHex(HexOverworldTile tile, string terrainKey,
                                    int variantCount, int spriteCount, float baseSize,
                                    float configBaseSize, float innerRadius = HexInnerRadiusPixels)
    {

        float cx = tile.PixelPos.X;
        float cy = tile.PixelPos.Y;

        float hexRowHeight = 156.0f * Mathf.Sqrt(3.0f);
        float yAnchorBias = configBaseSize > 2.0f
            ? -(configBaseSize - 1.5f) * hexRowHeight * 0.35f
            : 0.0f;

        for (int i = 0; i < spriteCount; i++)
        {

            uint salt = (uint)((tile.Coord.X * 73856093) ^ (tile.Coord.Y * 19349663) ^ (i * 83492791));
            uint h1 = Hash(salt);
            uint h2 = Hash(salt + 0xABCDu);
            uint h3 = Hash(salt + 0x1234u);
            uint h4 = Hash(salt + 0xDEADu);

            float angle = (h1 / (float)uint.MaxValue) * Mathf.Tau;
            float radius = Mathf.Sqrt(h2 / (float)uint.MaxValue) * innerRadius;

            float offsetX = Mathf.Cos(angle) * radius;
            float offsetY = Mathf.Sin(angle) * radius;

            if (yAnchorBias < 0)
            {
                uint h6 = Hash(salt + 0xCAFEu);
                float jitter = 0.6f + (h6 / (float)uint.MaxValue) * 0.8f;
                offsetY += yAnchorBias * jitter;
            }

            int variant = PickAvailableVariant(terrainKey, h3, variantCount);
            if (variant < 0) continue;

            string spriteKey = $"{terrainKey}_{variant}";
            var texture = GetOrLoadTexture(spriteKey);
            if (texture == null) continue;

            float sizeJitterRange = (terrainKey == "mountain" || terrainKey == "mountain_snow") ? 0.80f : 0.30f;
            float scale = baseSize * ((1.0f - sizeJitterRange * 0.5f) + (h4 / (float)uint.MaxValue) * sizeJitterRange);

            if (configBaseSize > 2.0f && _grid != null)
            {
                int dist = _mountainDistToEdge.GetValueOrDefault(tile.Coord, 1);

                float maxScale = 2.5f + (dist - 1) * 2.0f;
                scale = Mathf.Min(scale, maxScale);
            }

            bool flipH = (h4 & 1u) != 0;

            uint h5 = Hash(salt + 0xBEEFu);
            float rotation = ((h5 / (float)uint.MaxValue) - 0.5f) * 0.52f;

            AddToPropsList(spriteKey, texture, tile, offsetX, offsetY, scale, flipH, rotation);
        }
    }

    private int PickAvailableVariant(string terrainKey, uint hashSeed, int maxVariants)
    {
        int count = Mathf.Max(maxVariants, 1);
        int preferred = (int)(hashSeed % (uint)count);
        for (int offset = 0; offset < count; offset++)
        {
            int v = (preferred + offset) % count;
            if (TextureExists($"{terrainKey}_{v}")) return v;
        }
        return -1;
    }

    private bool TextureExists(string spriteKey)
    {
        if (_textureCache.TryGetValue(spriteKey, out var cached))
            return cached != null;
        return GetOrLoadTexture(spriteKey) != null;
    }

    private void AddToPropsList(string spriteKey, Texture2D texture,
        HexOverworldTile tile, float offsetX, float offsetY,
        float scale, bool flipH, float rotation)
    {
        if (!_propsByTexture.TryGetValue(spriteKey, out var props))
        {
            props = new List<PropData>();
            _propsByTexture[spriteKey] = props;
        }

        float texW = texture.GetWidth();
        float texH = texture.GetHeight();
        float drawH = SpriteBaseHeight * scale;
        float drawW = drawH * (texW / texH);

        float posX = tile.PixelPos.X + offsetX - drawW * 0.5f;
        float posY = tile.PixelPos.Y + offsetY - drawH;

        var prop = new PropData
        {
            PrimaryCoord = tile.Coord,
            Position = new Vector2(tile.PixelPos.X + offsetX, tile.PixelPos.Y + offsetY),
            SrcRect = new Rect2(0, 0, texW, texH),
            DstRect = new Rect2(posX, posY, drawW, drawH),
            Texture = texture,
            FlipH = flipH,
        };

        props.Add(prop);
    }

    /// <summary>
    /// 根据山脊采样点数据放置山脉精灵。
    /// 使用 RidgeSamplePoint 的精确位置和距离场缩放，并施加双重缩放约束防止纹理外溢。
    /// </summary>
    private void PlaceSpriteFromRidgeSample(HexOverworldTile tile, RidgeSamplePoint sp)
    {
        var profile = TerrainVisualRegistry.Get(tile.Terrain);
        string terrainKey = profile.OverworldKey;
        string actualKey = ResolveTerrainKeyWithFallback(terrainKey);
        int diskVariantCount = _availableVariants.GetValueOrDefault(actualKey, 0);
        if (diskVariantCount <= 0) return;

        // 确定性哈希
        uint salt = (uint)((Mathf.RoundToInt(sp.Position.X) * 73856093) ^
                           (Mathf.RoundToInt(sp.Position.Y) * 19349663));
        uint h1 = Hash(salt);
        uint h2 = Hash(salt + 0xABCDu);
        uint h3 = Hash(salt + 0x1234u);
        uint h4 = Hash(salt + 0xDEADu);
        uint h5 = Hash(salt + 0xBEEFu);

        // 轻微确定性抖动（打破机械感，但幅度比 AddMountainProp 小）
        float jitterAngle = (h1 / (float)uint.MaxValue) * Mathf.Tau;
        float jitterRadius = Mathf.Sqrt(h2 / (float)uint.MaxValue) * 8.0f;
        Vector2 finalPos = sp.Position + new Vector2(Mathf.Cos(jitterAngle) * jitterRadius, Mathf.Sin(jitterAngle) * jitterRadius);

        // 使用 RidgeSamplePoint 建议的缩放，结合环境因子微调
        float combinedEnv = tile.Temperature * tile.Nutrient;
        float envSizeBoost = Mathf.Lerp(0.85f, 1.15f, combinedEnv);
        float scale = sp.SuggestedScale * envSizeBoost;

        int variant = PickAvailableVariant(actualKey, h3, diskVariantCount);
        if (variant < 0) return;

        string spriteKey = $"{actualKey}_{variant}";
        var texture = GetOrLoadTexture(spriteKey);
        if (texture == null) return;

        float aspectRatio = (float)texture.GetWidth() / texture.GetHeight();

        // 双重距离场缩放约束（与 AddMountainProp 保持一致）
        float distPixels = Mathf.Max(0f, sp.DistToEdgePixels);
        const float margin = 60.0f;

        float maxScale = 2.0f * (distPixels + margin) / (SpriteBaseHeight * aspectRatio);
        scale = Mathf.Min(scale, maxScale);
        scale = Mathf.Max(scale, 1.5f);

        // 半宽约束：精灵半宽不超过到边界的距离 + 安全余量
        float safeHalfWidth = SpriteBaseHeight * scale * aspectRatio * 0.5f;
        float maxAllowedHalfWidth = distPixels + 60f;
        if (safeHalfWidth > maxAllowedHalfWidth)
        {
            scale = 2.0f * maxAllowedHalfWidth / (SpriteBaseHeight * aspectRatio);
            scale = Mathf.Max(scale, 1.5f);
        }

        // Y 轴锚定偏移（大型山脉精灵向上偏移以增强视觉纵深）
        float configBaseSize = 7.0f;
        float hexRowHeight = 156.0f * Mathf.Sqrt(3.0f);
        float yAnchorBias = -(configBaseSize - 1.5f) * hexRowHeight * 0.35f;
        float jitterY = 0.6f + (h5 / (float)uint.MaxValue) * 0.8f;
        finalPos.Y += yAnchorBias * jitterY;

        bool flipH = (h4 & 1u) != 0;
        float rotation = ((h5 / (float)uint.MaxValue) - 0.5f) * 0.20f;

        AddToPropsListDirect(spriteKey, texture, tile.Coord, null, finalPos, scale, flipH, rotation);
    }

    /// <summary>
    /// 返回指定 hex 坐标的 1-ring 邻居坐标（6 个方向）。
    /// </summary>
    private static IEnumerable<Vector2I> HexNeighborRing(Vector2I coord, int ring)
    {
        // axial 坐标 6 方向偏移
        int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, -1 }, new[] { -1, 1 } };
        foreach (var d in dirs)
            yield return new Vector2I(coord.X + d[0] * ring, coord.Y + d[1] * ring);
    }

    private static bool IsMountain(HexOverworldTile.TerrainType terrain)
    {
        return terrain == HexOverworldTile.TerrainType.Mountain ||
               terrain == HexOverworldTile.TerrainType.MountainSnow;
    }

    private bool IsRidgeTile(HexOverworldTile tile)
    {
        int myDist = _mountainDistToEdge.GetValueOrDefault(tile.Coord, 0);
        if (myDist <= 0) return false;

        if (myDist >= 2) return true;

        int maxNeighborDist = 0;
        foreach (var n in GetNeighborTiles(tile.Coord))
        {
            if (IsMountain(n.Terrain))
            {
                int nDist = _mountainDistToEdge.GetValueOrDefault(n.Coord, 0);
                if (nDist > maxNeighborDist) maxNeighborDist = nDist;
            }
        }

        return myDist >= maxNeighborDist;
    }

    private void AddMountainProp(HexOverworldTile tileA, Vector2 pos, bool isRidge, HexOverworldTile? tileB)
    {

        var profile = TerrainVisualRegistry.Get(tileA.Terrain);
        string terrainKey = profile.OverworldKey;
        string actualKey = ResolveTerrainKeyWithFallback(terrainKey);
        int diskVariantCount = _availableVariants.GetValueOrDefault(actualKey, 0);
        if (diskVariantCount <= 0) return;

        uint salt = (uint)((Mathf.RoundToInt(pos.X) * 73856093) ^ (Mathf.RoundToInt(pos.Y) * 19349663));
        uint h1 = Hash(salt);
        uint h2 = Hash(salt + 0xABCDu);
        uint h3 = Hash(salt + 0x1234u);
        uint h4 = Hash(salt + 0xDEADu);
        uint h5 = Hash(salt + 0xBEEFu);

        float jitterAngle = (h1 / (float)uint.MaxValue) * Mathf.Tau;
        float jitterRadius = Mathf.Sqrt(h2 / (float)uint.MaxValue) * 15.0f;
        Vector2 finalPos = pos + new Vector2(Mathf.Cos(jitterAngle) * jitterRadius, Mathf.Sin(jitterAngle) * jitterRadius);

        float distPixels = 9999f;
        if (_mountainNearestEdgeCoord.TryGetValue(tileA.Coord, out var edgeA))
        {
            var edgeTile = GetTileFromAny(edgeA);
            if (edgeTile != null)
            {
                float d = finalPos.DistanceTo(edgeTile.PixelPos) - HexInnerRadiusPixels;
                if (d < distPixels) distPixels = d;
            }
        }
        if (tileB != null && _mountainNearestEdgeCoord.TryGetValue(tileB.Coord, out var edgeB))
        {
            var edgeTile = GetTileFromAny(edgeB);
            if (edgeTile != null)
            {
                float d = finalPos.DistanceTo(edgeTile.PixelPos) - HexInnerRadiusPixels;
                if (d < distPixels) distPixels = d;
            }
        }
        if (distPixels < 0.0f) distPixels = 0.0f;

        float baseSize = isRidge ? 7.0f : 3.5f;

        int patchSize = _mountainPatchSizes.GetValueOrDefault(tileA.Coord, 1);
        float patchMul = 0.55f + Mathf.Log(patchSize) * 0.125f;
        patchMul = Mathf.Clamp(patchMul, 0.5f, 1.4f);

        float combinedEnv = tileA.Temperature * tileA.Nutrient;
        float envSizeBoost = Mathf.Lerp(0.80f, 1.20f, combinedEnv);

        float sizeJitterRange = 0.60f;
        float scale = baseSize * ((1.0f - sizeJitterRange * 0.5f) + (h4 / (float)uint.MaxValue) * sizeJitterRange) * envSizeBoost * patchMul;

        const float margin = 60.0f;
        int variant = PickAvailableVariant(actualKey, h3, diskVariantCount);
        if (variant < 0) return;

        string spriteKey = $"{actualKey}_{variant}";
        var texture = GetOrLoadTexture(spriteKey);
        if (texture == null) return;

        float aspectRatio = (float)texture.GetWidth() / texture.GetHeight();

        float maxScale = 2.0f * (distPixels + margin) / (SpriteBaseHeight * aspectRatio);
        scale = Mathf.Min(scale, maxScale);
        scale = Mathf.Max(scale, 1.2f);

        float safeHalfWidth = SpriteBaseHeight * scale * aspectRatio * 0.5f;
        float maxAllowedHalfWidth = distPixels + 60f;
        if (safeHalfWidth > maxAllowedHalfWidth)
        {
            scale = 2.0f * maxAllowedHalfWidth / (SpriteBaseHeight * aspectRatio);
            scale = Mathf.Max(scale, 1.2f);
        }

        float configBaseSize = 7.0f;
        float hexRowHeight = 156.0f * Mathf.Sqrt(3.0f);
        float yAnchorBias = -(configBaseSize - 1.5f) * hexRowHeight * 0.35f;

        float jitterY = 0.6f + (h5 / (float)uint.MaxValue) * 0.8f;
        finalPos.Y += yAnchorBias * jitterY;

        bool flipH = (h4 & 1u) != 0;
        float rotation = ((h5 / (float)uint.MaxValue) - 0.5f) * 0.28f;

        AddToPropsListDirect(spriteKey, texture, tileA.Coord, tileB?.Coord, finalPos, scale, flipH, rotation);
    }

    private void AddForestTreeProp(HexOverworldTile tileA, Vector2 pos, float baseScale, HexOverworldTile? tileB)
    {
        var profile = TerrainVisualRegistry.Get(tileA.Terrain);
        string terrainKey = profile.OverworldKey;
        string actualKey = ResolveTerrainKeyWithFallback(terrainKey);
        int diskVariantCount = _availableVariants.GetValueOrDefault(actualKey, 0);
        if (diskVariantCount <= 0) return;

        uint salt = (uint)((Mathf.RoundToInt(pos.X) * 73856093) ^ (Mathf.RoundToInt(pos.Y) * 19349663));
        uint h1 = Hash(salt);
        uint h2 = Hash(salt + 0xABCDu);
        uint h3 = Hash(salt + 0x1234u);
        uint h4 = Hash(salt + 0xDEADu);
        uint h5 = Hash(salt + 0xBEEFu);

        float jitterAngle = (h1 / (float)uint.MaxValue) * Mathf.Tau;
        float jitterRadius = Mathf.Sqrt(h2 / (float)uint.MaxValue) * 12.0f;
        Vector2 finalPos = pos + new Vector2(Mathf.Cos(jitterAngle) * jitterRadius, Mathf.Sin(jitterAngle) * jitterRadius);

        float combinedEnv = tileA.Temperature * tileA.Nutrient;
        if (tileB != null)
        {

            combinedEnv = (combinedEnv + tileB.Temperature * tileB.Nutrient) * 0.5f;
        }
        float envSizeBoost = Mathf.Lerp(0.80f, 1.25f, combinedEnv);

        int distA = _forestDistToEdge.GetValueOrDefault(tileA.Coord, 1);
        int distB = tileB != null ? _forestDistToEdge.GetValueOrDefault(tileB.Coord, 1) : distA;
        float distMin = Mathf.Min(distA, distB);
        float edgeScaleMul = (distMin == 1) ? 0.85f : 1.0f;

        float sizeJitterRange = 0.30f;
        float scale = baseScale * ((1.0f - sizeJitterRange * 0.5f) + (h4 / (float)uint.MaxValue) * sizeJitterRange) * envSizeBoost * edgeScaleMul;

        int variant = PickAvailableVariant(actualKey, h3, diskVariantCount);
        if (variant < 0) return;

        string spriteKey = $"{actualKey}_{variant}";
        var texture = GetOrLoadTexture(spriteKey);
        if (texture == null) return;

        bool flipH = (h4 & 1u) != 0;
        float rotation = ((h5 / (float)uint.MaxValue) - 0.5f) * 0.52f;

        AddToPropsListDirect(spriteKey, texture, tileA.Coord, tileB?.Coord, finalPos, scale, flipH, rotation);
    }

    private void AddToPropsListDirect(string spriteKey, Texture2D texture,
        Vector2I primaryCoord, Vector2I? secondaryCoord, Vector2 finalPos, float scale, bool flipH, float rotation)
    {
        if (!_propsByTexture.TryGetValue(spriteKey, out var props))
        {
            props = new List<PropData>();
            _propsByTexture[spriteKey] = props;
        }

        float texW = texture.GetWidth();
        float texH = texture.GetHeight();
        float drawH = SpriteBaseHeight * scale;
        float drawW = drawH * (texW / texH);

        float posX = finalPos.X - drawW * 0.5f;
        float posY = finalPos.Y - drawH;

        var prop = new PropData
        {
            PrimaryCoord = primaryCoord,
            SecondaryCoord = secondaryCoord,
            Position = finalPos,
            SrcRect = new Rect2(0, 0, texW, texH),
            DstRect = new Rect2(posX, posY, drawW, drawH),
            Texture = texture,
            FlipH = flipH,
        };

        props.Add(prop);
    }

    private static readonly object _loadLock = new();

    private Texture2D? GetOrLoadTexture(string spriteKey)
    {
        lock (_loadLock)
        {
            if (_textureCache.TryGetValue(spriteKey, out var cached))
                return cached;

            string path = $"{SpriteBaseDir}/{spriteKey}.png";
            if (!ResourceLoader.Exists(path))
            {
                _textureCache[spriteKey] = null;
                return null;
            }

            var texture = TextureAssetResolver.LoadMapTexture(spriteKey, path);
            _textureCache[spriteKey] = texture;
            return texture;
        }
    }

    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
}
