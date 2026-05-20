// TerrainBattleField.cs
// Sparse IBattleField implementation for testing terrain-influenced combat.
// Default tile is plains; specific tiles can be overridden with cover or
// elevation by writing to the dictionary directly.
using System.Collections.Generic;
using BladeHex.Data;
using Godot;

namespace BladeHex.Combat.Headless;

/// <summary>
/// Sparse battlefield: every tile is plains by default. Specific tiles can
/// be tagged with elevation, cover, or terrain via <see cref="SetTile"/>.
/// Used by tests / sim scenarios that want a few hills or trees without
/// allocating a full 2D array.
/// </summary>
public sealed class TerrainBattleField : IBattleField
{
    public sealed class Tile
    {
        public int Elevation = 1;
        public bool BlocksLos = false;
        public int CoverLevel = 0;
        public BattleCellData.TerrainType TerrainType = BattleCellData.TerrainType.Plains;
    }

    private readonly Dictionary<Vector2I, Tile> _tiles = new();

    public Tile SetTile(Vector2I pos)
    {
        if (!_tiles.TryGetValue(pos, out var t))
        {
            t = new Tile();
            _tiles[pos] = t;
        }
        return t;
    }

    public int GetElevation(Vector2I pos)
        => _tiles.TryGetValue(pos, out var t) ? t.Elevation : 1;

    public bool BlocksLineOfSight(Vector2I pos)
        => _tiles.TryGetValue(pos, out var t) && t.BlocksLos;

    public int GetCoverLevel(Vector2I pos)
        => _tiles.TryGetValue(pos, out var t) ? t.CoverLevel : 0;

    public BattleCellData.TerrainType GetTerrainType(Vector2I pos)
        => _tiles.TryGetValue(pos, out var t) ? t.TerrainType : BattleCellData.TerrainType.Plains;

    public bool IsValid(Vector2I pos) => true;
}
