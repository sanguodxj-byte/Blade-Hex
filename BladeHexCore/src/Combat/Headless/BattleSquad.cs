// BattleSquad.cs
// A side participating in a headless battle. Wraps a list of BattleUnitModel
// and tracks per-unit grid positions in a flat dictionary.
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat.Headless;

/// <summary>
/// One side of a headless battle. Owns its <see cref="BattleUnitModel"/>s and
/// a parallel position map (since BattleUnitModel does not store grid coords).
/// </summary>
public sealed class BattleSquad
{
    public string SideName { get; }
    public bool IsPlayerSide { get; }
    public List<BattleUnitModel> Units { get; } = new();
    public Dictionary<BattleUnitModel, Vector2I> Positions { get; } = new();

    public BattleSquad(string sideName, bool isPlayerSide)
    {
        SideName = sideName;
        IsPlayerSide = isPlayerSide;
    }

    public void AddUnit(BattleUnitModel unit, Vector2I startPos)
    {
        Units.Add(unit);
        Positions[unit] = startPos;
        unit.Runtime.GridPos = startPos;
        // Compute max HP including skill-tree HP bonus (if a tree was attached)
        int maxHp = unit.GetMaxHp() + (unit.Runtime.SkillTree?.GetHpBonus() ?? 0);
        unit.Runtime.CurrentHp = maxHp;
        unit.InitDr();
        unit.EnsureApInitialized();
    }

    public IEnumerable<BattleUnitModel> AliveUnits =>
        Units.Where(u => u.Runtime.CurrentHp > 0);

    public bool HasAlive => AliveUnits.Any();

    public int AliveCount => AliveUnits.Count();

    public int InitialCount { get; private set; }

    public void LockInitialCount() => InitialCount = Units.Count;
}
