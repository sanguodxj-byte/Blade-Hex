// CombatPhase.cs
// Enum for combat lifecycle phases. Lives in Core so headless simulation
// and unit tests can reference phases without pulling in Godot or Frontend.
namespace BladeHex.Combat.State;

/// <summary>
/// Phases of a single tactical battle's lifecycle.
/// </summary>
public enum CombatPhase
{
    /// <summary>Pre-combat: units placed but combat has not started.</summary>
    Init,

    /// <summary>Player deployment: player chooses where to place their units.</summary>
    Deployment,

    /// <summary>Player side acts (legacy compatibility — maps to UnitTurn with player unit).</summary>
    PlayerTurn,

    /// <summary>Enemy side acts (legacy compatibility — maps to UnitTurn with enemy unit).</summary>
    EnemyTurn,

    /// <summary>Individual unit's turn (initiative system).</summary>
    UnitTurn,

    /// <summary>Combat resolved (victory or defeat). Terminal state.</summary>
    CombatEnd,
}
