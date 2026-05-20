// CombatStateMachine.cs
// Pure state machine for the combat lifecycle (Init -> PlayerTurn <-> EnemyTurn -> CombatEnd).
//
// Lives in Core so it can be driven by both:
//   1) the Godot CombatManager facade (Frontend), which forwards events as signals
//   2) the headless simulation harness, which drives the loop directly without scenes
//
// Responsibilities:
//   - Track current phase, turn number, victory flag.
//   - Emit phase-change events via plain C# events (no Godot signal coupling).
//   - Refuse invalid transitions (e.g. cannot leave CombatEnd).
//
// Non-responsibilities:
//   - Resetting unit AP / status. The host (TurnManager / SimulationLoop) does that
//     in response to PhaseChanged. Keeping this class data-free makes it reusable
//     for unit roster types we don't want to depend on (Unit Node3D, BattleUnitModel,
//     future StackUnit, etc.).
using System;

namespace BladeHex.Combat.State;

/// <summary>
/// Pure C# combat lifecycle state machine. Independent of Godot, scenes, and
/// any specific unit type.
/// </summary>
public sealed class CombatStateMachine
{
    /// <summary>Current phase. Starts at <see cref="CombatPhase.Init"/>.</summary>
    public CombatPhase CurrentPhase { get; private set; } = CombatPhase.Init;

    /// <summary>
    /// Number of completed player turns (incremented when the machine enters
    /// PlayerTurn). 0 before combat starts.
    /// </summary>
    public int TurnNumber { get; private set; }

    /// <summary>
    /// Whether combat has ended in victory. Only meaningful when
    /// <see cref="CurrentPhase"/> == <see cref="CombatPhase.CombatEnd"/>.
    /// </summary>
    public bool VictoryFlag { get; private set; }

    /// <summary>Fired AFTER the phase transition is committed.</summary>
    public event Action<CombatPhase>? PhaseChanged;

    /// <summary>Fired when combat ends. Carries the victory flag.</summary>
    public event Action<bool>? CombatEnded;

    /// <summary>
    /// Begin combat. Transitions Init -> PlayerTurn. Idempotent if already past Init.
    /// </summary>
    public void StartCombat()
    {
        if (CurrentPhase != CombatPhase.Init) return;
        TurnNumber = 0;
        Transition(CombatPhase.PlayerTurn);
    }

    /// <summary>
    /// Enter deployment phase. Transitions Init -> Deployment.
    /// </summary>
    public void EnterDeployment()
    {
        if (CurrentPhase != CombatPhase.Init) return;
        TurnNumber = 0;
        Transition(CombatPhase.Deployment);
    }

    /// <summary>
    /// Confirm deployment and start combat. Transitions Deployment -> PlayerTurn.
    /// </summary>
    public void ConfirmDeployment()
    {
        if (CurrentPhase != CombatPhase.Deployment) return;
        Transition(CombatPhase.PlayerTurn);
    }

    /// <summary>
    /// End the current side's turn. Switches PlayerTurn &lt;-&gt; EnemyTurn.
    /// No-op if combat has ended or hasn't started.
    /// </summary>
    public void EndCurrentTurn()
    {
        switch (CurrentPhase)
        {
            case CombatPhase.PlayerTurn:
                Transition(CombatPhase.EnemyTurn);
                break;
            case CombatPhase.EnemyTurn:
                Transition(CombatPhase.PlayerTurn);
                break;
            // Init / CombatEnd: ignore
        }
    }

    /// <summary>
    /// Force end combat with a victory flag. Subsequent calls are no-ops.
    /// </summary>
    public void EndCombat(bool victory)
    {
        if (CurrentPhase == CombatPhase.CombatEnd) return;
        VictoryFlag = victory;
        Transition(CombatPhase.CombatEnd);
        CombatEnded?.Invoke(victory);
    }

    /// <summary>True if combat is over.</summary>
    public bool IsFinished => CurrentPhase == CombatPhase.CombatEnd;

    /// <summary>
    /// Soft phase override used only to back-compat legacy direct ChangeState calls.
    /// Prefer <see cref="StartCombat"/> / <see cref="EndCurrentTurn"/> / <see cref="EndCombat"/>.
    /// </summary>
    internal void ForcePhase(CombatPhase phase)
    {
        Transition(phase);
        if (phase == CombatPhase.CombatEnd)
            CombatEnded?.Invoke(VictoryFlag);
    }

    private void Transition(CombatPhase next)
    {
        CurrentPhase = next;
        if (next == CombatPhase.PlayerTurn) TurnNumber++;
        PhaseChanged?.Invoke(next);
    }
}
