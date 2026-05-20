// FlatBattleField.cs
// Trivial IBattleField implementation for headless simulation:
//   - All tiles exist and are plains.
//   - No elevation differences, no cover, no rivers.
//
// This is enough for a simulator that doesn't need terrain to drive
// damage/HP balance discovery. Future sim improvements can introduce a
// SparseBattleField that overlays specific tiles with cover/elevation.
using BladeHex.Data;
using Godot;

namespace BladeHex.Combat.Headless;

/// <summary>Flat plains battlefield with optional bounds.</summary>
public sealed class FlatBattleField : IBattleField
{
    public int GetElevation(Vector2I pos) => 1;
    public bool BlocksLineOfSight(Vector2I pos) => false;
    public int GetCoverLevel(Vector2I pos) => 0;
    public BattleCellData.TerrainType GetTerrainType(Vector2I pos) => BattleCellData.TerrainType.Plains;
    public bool IsValid(Vector2I pos) => true;
}
