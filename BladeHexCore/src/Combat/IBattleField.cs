// IBattleField.cs
// Abstraction over per-tile battlefield data needed by LOS, facing,
// and similar pure-rule calculations.
//
// Frontend's HexGrid + HexCell implement an adapter over this; headless
// simulation provides a flat in-memory implementation.
using BladeHex.Data;
using Godot;

namespace BladeHex.Combat;

/// <summary>Read-only per-tile data needed by combat rules.</summary>
public interface IBattleField
{
    /// <summary>Terrain elevation: 0=low, 1=normal, 2=hill, 3=mountain.</summary>
    int GetElevation(Vector2I pos);

    /// <summary>True if this tile blocks line of sight (mountain, dense forest).</summary>
    bool BlocksLineOfSight(Vector2I pos);

    /// <summary>Cover level on this tile: 0=none, 1=half, 2=full.</summary>
    int GetCoverLevel(Vector2I pos);

    /// <summary>Underlying terrain type (for shallow-water river-cross penalty etc.).</summary>
    BattleCellData.TerrainType GetTerrainType(Vector2I pos);

    /// <summary>True if the tile exists on this battlefield.</summary>
    bool IsValid(Vector2I pos);
}
