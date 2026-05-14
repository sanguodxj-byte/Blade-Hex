// TurnManager.cs
// 回合管理与状态切换
using Godot;
using System;

namespace BladeHex.Combat;

/// <summary>
/// 管理回合切换逻辑，从 CombatManager 中拆分。
/// 职责：回合计数、状态切换、回合开始时的单位重置。
/// </summary>
public class TurnManager
{
    public enum TurnPhase { Init, PlayerTurn, EnemyTurn, CombatEnd }

    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Init;
    public int TurnNumber { get; private set; }

    private readonly UnitRegistry _registry;

    public event Action<TurnPhase>? PhaseChanged;

    public TurnManager(UnitRegistry registry)
    {
        _registry = registry;
    }

    public void StartCombat()
    {
        TurnNumber = 0;
        ChangePhase(TurnPhase.PlayerTurn);
    }

    public void EndCurrentTurn()
    {
        if (CurrentPhase == TurnPhase.PlayerTurn)
            ChangePhase(TurnPhase.EnemyTurn);
        else if (CurrentPhase == TurnPhase.EnemyTurn)
            ChangePhase(TurnPhase.PlayerTurn);
    }

    public void EndCombat()
    {
        CurrentPhase = TurnPhase.CombatEnd;
        PhaseChanged?.Invoke(CurrentPhase);
    }

    private void ChangePhase(TurnPhase newPhase)
    {
        CurrentPhase = newPhase;

        if (newPhase == TurnPhase.PlayerTurn)
        {
            TurnNumber++;
            _registry.ResetActions(_registry.PlayerUnits);
        }
        else if (newPhase == TurnPhase.EnemyTurn)
        {
            _registry.ResetActions(_registry.EnemyUnits);
        }

        PhaseChanged?.Invoke(CurrentPhase);
    }
}
