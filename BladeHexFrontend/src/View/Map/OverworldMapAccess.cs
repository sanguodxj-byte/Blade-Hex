using System.Collections.Generic;
using BladeHex.Map;
using Godot;

namespace BladeHex.View.Map;

/// <summary>
/// Frontend map access adapter for the overworld.
/// Hides chunk-mode and legacy full-grid lookup differences from scene code.
/// </summary>
public sealed class OverworldMapAccess
{
    private readonly ChunkManager? _chunkManager;
    private readonly HexOverworldGrid? _grid;

    public OverworldMapAccess(ChunkManager? chunkManager, HexOverworldGrid? grid)
    {
        _chunkManager = chunkManager;
        _grid = grid;
    }

    public bool IsChunkMode => _chunkManager != null;

    public IReadOnlyDictionary<Vector2I, ChunkData> ActiveChunks =>
        _chunkManager?.ActiveChunks ?? EmptyChunks;

    public HexOverworldTile? GetActiveTile(int q, int r)
    {
        if (_chunkManager != null)
            return _chunkManager.GetTile(q, r);

        return _grid?.GetTile(q, r);
    }

    /// <summary>
    /// Explicit cache/full-grid fallback for context lookups that must survive chunk unloads.
    /// Do not use for default rendering or movement.
    /// </summary>
    public HexOverworldTile? GetKnownTileFromCache(int q, int r)
    {
        if (_chunkManager != null)
            return _chunkManager.GetTileAnywhere(q, r);

        return _grid?.GetTile(q, r);
    }

    public HexOverworldTile? GetActiveTileAtPixel(Vector2 position)
    {
        var axial = HexOverworldTile.PixelToAxial(position.X, position.Y);
        return GetActiveTile(axial.X, axial.Y);
    }

    public bool IsLoaded(int q, int r)
    {
        if (_chunkManager != null)
            return _chunkManager.IsLoaded(q, r);

        return _grid?.GetTile(q, r) != null;
    }

    private static readonly IReadOnlyDictionary<Vector2I, ChunkData> EmptyChunks =
        new Dictionary<Vector2I, ChunkData>();
}
