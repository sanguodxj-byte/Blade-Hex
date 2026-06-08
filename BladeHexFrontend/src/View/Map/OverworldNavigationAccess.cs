using System.Collections.Generic;
using BladeHex.Map;
using Godot;

namespace BladeHex.View.Map;

/// <summary>
/// Frontend navigation adapter for the overworld.
/// Keeps chunk-aware and legacy full-grid A* selection out of scene entities.
/// </summary>
public sealed class OverworldNavigationAccess
{
    private readonly OverworldMapAccess _mapAccess;
    private readonly ChunkManager? _chunkManager;
    private readonly ChunkAStar? _chunkAStar;
    private readonly HexOverworldAStar? _hexAStar;

    public OverworldNavigationAccess(
        OverworldMapAccess mapAccess,
        ChunkManager? chunkManager,
        ChunkAStar? chunkAStar,
        HexOverworldAStar? hexAStar)
    {
        _mapAccess = mapAccess;
        _chunkManager = chunkManager;
        _chunkAStar = chunkAStar;
        _hexAStar = hexAStar;
    }

    public bool HasChunkNavigation => _chunkManager != null && _chunkAStar != null;
    public bool HasLegacyNavigation => _hexAStar != null;
    public ChunkAStar.NavigationMode? ChunkMode => _chunkAStar?.Mode;

    public PathResult FindPath(Vector2 fromPx, Vector2 targetPx, bool hasUsableShip, bool isAtSea)
    {
        if (HasChunkNavigation)
            return FindChunkPath(fromPx, targetPx, hasUsableShip, isAtSea);

        if (_hexAStar != null)
            return new PathResult(_hexAStar.FindPathPixels(fromPx, targetPx), false, isAtSea);

        return PathResult.Empty(isAtSea);
    }

    public bool IsLinePassable(Vector2 from, Vector2 to)
    {
        float dist = from.DistanceTo(to);
        int steps = Mathf.Max(2, (int)(dist / 50.0f));

        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            Vector2 sample = from.Lerp(to, t);
            var tile = _mapAccess.GetActiveTileAtPixel(sample);
            if (tile == null || !tile.IsPassable)
                return false;
        }

        return true;
    }

    public bool ShouldLeaveSea(Vector2 position)
    {
        var tile = _mapAccess.GetActiveTileAtPixel(position);
        return tile != null
            && tile.Terrain != HexOverworldTile.TerrainType.DeepWater
            && tile.Terrain != HexOverworldTile.TerrainType.ShallowWater;
    }

    public void SetLandMode()
    {
        if (_chunkAStar != null)
            _chunkAStar.Mode = ChunkAStar.NavigationMode.Land;
    }

    private PathResult FindChunkPath(Vector2 fromPx, Vector2 targetPx, bool hasUsableShip, bool isAtSea)
    {
        var mgr = _chunkManager!;
        var astar = _chunkAStar!;
        var targetAxial = HexOverworldTile.PixelToAxial(targetPx.X, targetPx.Y);
        var targetTile = _mapAccess.GetActiveTile(targetAxial.X, targetAxial.Y);
        bool nextAtSea = isAtSea;

        if (targetTile != null)
        {
            bool targetIsWater = targetTile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                                 targetTile.Terrain == HexOverworldTile.TerrainType.ShallowWater;

            if (targetIsWater && hasUsableShip)
            {
                astar.Mode = ChunkAStar.NavigationMode.Sea;
                nextAtSea = true;
            }
            else
            {
                astar.Mode = ChunkAStar.NavigationMode.Land;
                nextAtSea = false;
            }
        }

        var path = astar.FindPathPixels(fromPx, targetPx, mgr);

        if (path.Length == 0 && targetTile != null && !targetTile.IsPassable)
        {
            var nearPassable = FindNearestPassableTarget(targetAxial, mgr);
            if (nearPassable != targetAxial)
            {
                var altTarget = HexOverworldTile.AxialToPixel(nearPassable.X, nearPassable.Y);
                path = astar.FindPathPixels(fromPx, altTarget, mgr);
            }
        }

        bool targetLoaded = _mapAccess.IsLoaded(targetAxial.X, targetAxial.Y);
        return new PathResult(path, !targetLoaded, nextAtSea);
    }

    private static Vector2I FindNearestPassableTarget(Vector2I coord, ChunkManager mgr)
    {
        var visited = new HashSet<Vector2I> { coord };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(coord);

        int maxSearch = 36;
        while (queue.Count > 0 && maxSearch-- > 0)
        {
            var current = queue.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (visited.Contains(neighbor))
                    continue;

                visited.Add(neighbor);

                var tile = mgr.GetTile(neighbor.X, neighbor.Y);
                if (tile != null && tile.IsPassable &&
                    tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                {
                    return neighbor;
                }

                if (tile != null)
                    queue.Enqueue(neighbor);
            }
        }

        return coord;
    }

    public readonly struct PathResult
    {
        public PathResult(Vector2[] path, bool requiresContinuation, bool isAtSea)
        {
            Path = path;
            RequiresContinuation = requiresContinuation;
            IsAtSea = isAtSea;
        }

        public Vector2[] Path { get; }
        public bool RequiresContinuation { get; }
        public bool IsAtSea { get; }

        public static PathResult Empty(bool isAtSea) => new([], false, isAtSea);
    }
}
