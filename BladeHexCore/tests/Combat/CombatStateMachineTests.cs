// CombatStateMachineTests.cs
// Pure-state-machine tests. No Godot scene required.
using System.Collections.Generic;
using BladeHex.Combat.State;

namespace BladeHex.Combat.Tests;

public static class CombatStateMachineTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;
        foreach (var (name, run) in EnumerateTests())
        {
            var (ok, msg) = run();
            if (ok)
            {
                passed++;
                details.Add($"  [PASS] {name}");
            }
            else
            {
                failed++;
                details.Add($"  [FAIL] {name}: {msg}");
            }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string, System.Func<(bool, string)>)> EnumerateTests()
    {
        yield return ("InitialPhase_IsInit", InitialPhase_IsInit);
        yield return ("Start_GoesToPlayerTurn", Start_GoesToPlayerTurn);
        yield return ("Start_IsIdempotent", Start_IsIdempotent);
        yield return ("EndTurn_TogglesSides", EndTurn_TogglesSides);
        yield return ("EndTurn_BeforeStart_NoOp", EndTurn_BeforeStart_NoOp);
        yield return ("EndCombat_FromAnywhere_Terminal", EndCombat_FromAnywhere_Terminal);
        yield return ("EndCombat_IsIdempotent", EndCombat_IsIdempotent);
        yield return ("PhaseChanged_FiresOnTransitionsOnly", PhaseChanged_FiresOnTransitionsOnly);
        yield return ("CombatEnded_FiresWithVictoryFlag", CombatEnded_FiresWithVictoryFlag);
        yield return ("TurnNumber_IncrementsOnPlayerEntry", TurnNumber_IncrementsOnPlayerEntry);
        yield return ("EndCurrentTurn_AfterEnd_NoOp", EndCurrentTurn_AfterEnd_NoOp);
    }

    // ========================================================================
    // Test cases
    // ========================================================================

    private static (bool, string) InitialPhase_IsInit()
    {
        var m = new CombatStateMachine();
        return Expect(m.CurrentPhase == CombatPhase.Init, $"got {m.CurrentPhase}");
    }

    private static (bool, string) Start_GoesToPlayerTurn()
    {
        var m = new CombatStateMachine();
        m.StartCombat();
        return Expect(m.CurrentPhase == CombatPhase.PlayerTurn, $"got {m.CurrentPhase}");
    }

    private static (bool, string) Start_IsIdempotent()
    {
        var m = new CombatStateMachine();
        m.StartCombat();
        m.EndCurrentTurn(); // -> EnemyTurn
        m.StartCombat();    // should NOT reset to PlayerTurn
        return Expect(m.CurrentPhase == CombatPhase.EnemyTurn, $"got {m.CurrentPhase}");
    }

    private static (bool, string) EndTurn_TogglesSides()
    {
        var m = new CombatStateMachine();
        m.StartCombat();
        m.EndCurrentTurn();
        if (m.CurrentPhase != CombatPhase.EnemyTurn)
            return (false, $"after first EndCurrentTurn: {m.CurrentPhase}");
        m.EndCurrentTurn();
        return Expect(m.CurrentPhase == CombatPhase.PlayerTurn, $"after second: {m.CurrentPhase}");
    }

    private static (bool, string) EndTurn_BeforeStart_NoOp()
    {
        var m = new CombatStateMachine();
        m.EndCurrentTurn();
        return Expect(m.CurrentPhase == CombatPhase.Init, $"got {m.CurrentPhase}");
    }

    private static (bool, string) EndCombat_FromAnywhere_Terminal()
    {
        var m = new CombatStateMachine();
        m.StartCombat();
        m.EndCombat(victory: true);
        if (m.CurrentPhase != CombatPhase.CombatEnd)
            return (false, $"phase={m.CurrentPhase}");
        if (!m.VictoryFlag) return (false, "victory flag should be true");
        // No further transitions allowed
        m.EndCurrentTurn();
        return Expect(m.CurrentPhase == CombatPhase.CombatEnd, "should remain CombatEnd");
    }

    private static (bool, string) EndCombat_IsIdempotent()
    {
        var m = new CombatStateMachine();
        m.StartCombat();
        int endedCount = 0;
        m.CombatEnded += _ => endedCount++;
        m.EndCombat(true);
        m.EndCombat(false);  // ignored, victory remains true
        if (m.VictoryFlag != true) return (false, "victory should remain true");
        return Expect(endedCount == 1, $"CombatEnded fired {endedCount} times, expected 1");
    }

    private static (bool, string) PhaseChanged_FiresOnTransitionsOnly()
    {
        var m = new CombatStateMachine();
        var phases = new List<CombatPhase>();
        m.PhaseChanged += p => phases.Add(p);
        m.StartCombat();          // -> PlayerTurn
        m.EndCurrentTurn();       // -> EnemyTurn
        m.EndCurrentTurn();       // -> PlayerTurn
        m.EndCombat(false);       // -> CombatEnd
        var expected = new[] { CombatPhase.PlayerTurn, CombatPhase.EnemyTurn, CombatPhase.PlayerTurn, CombatPhase.CombatEnd };
        if (phases.Count != expected.Length)
            return (false, $"got {phases.Count} events, expected {expected.Length}");
        for (int i = 0; i < expected.Length; i++)
            if (phases[i] != expected[i]) return (false, $"event[{i}]={phases[i]}, expected {expected[i]}");
        return (true, "");
    }

    private static (bool, string) CombatEnded_FiresWithVictoryFlag()
    {
        var m = new CombatStateMachine();
        bool? captured = null;
        m.CombatEnded += v => captured = v;
        m.StartCombat();
        m.EndCombat(victory: false);
        return Expect(captured == false, $"captured={captured}");
    }

    private static (bool, string) TurnNumber_IncrementsOnPlayerEntry()
    {
        var m = new CombatStateMachine();
        m.StartCombat();          // turn 1
        if (m.TurnNumber != 1) return (false, $"after start: {m.TurnNumber}");
        m.EndCurrentTurn();       // -> Enemy
        if (m.TurnNumber != 1) return (false, $"after enemy entry: {m.TurnNumber}");
        m.EndCurrentTurn();       // -> Player turn 2
        return Expect(m.TurnNumber == 2, $"got {m.TurnNumber}");
    }

    private static (bool, string) EndCurrentTurn_AfterEnd_NoOp()
    {
        var m = new CombatStateMachine();
        m.StartCombat();
        m.EndCombat(true);
        m.EndCurrentTurn();
        m.EndCurrentTurn();
        return Expect(m.CurrentPhase == CombatPhase.CombatEnd, $"got {m.CurrentPhase}");
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static (bool, string) Expect(bool ok, string failureMsg)
        => ok ? (true, "") : (false, failureMsg);
}
