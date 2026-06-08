using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Tests;

public static class SpellTargetRulesTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(HealingTargets_AlliesOnly), HealingTargets_AlliesOnly);
        yield return Run(nameof(DamageTargets_EnemiesOnly), DamageTargets_EnemiesOnly);
        yield return Run(nameof(StatusCanDeclare_Enemies), StatusCanDeclare_Enemies);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (System.Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    private static (bool, string) HealingTargets_AlliesOnly()
    {
        var caster = new UnitData { IsEnemy = false };
        var ally = new UnitData { IsEnemy = false };
        var enemy = new UnitData { IsEnemy = true };
        var heal = new SpellData { HealDiceCount = 1, HealDiceSides = 8 };

        if (!SpellTargetRules.IsValidTarget(caster, ally, heal))
            return (false, "healing spell should accept allied targets");
        if (SpellTargetRules.IsValidTarget(caster, enemy, heal))
            return (false, "healing spell must not accept enemy targets");
        return (true, "");
    }

    private static (bool, string) DamageTargets_EnemiesOnly()
    {
        var caster = new UnitData { IsEnemy = false };
        var ally = new UnitData { IsEnemy = false };
        var enemy = new UnitData { IsEnemy = true };
        var damage = new SpellData { DamageDiceCount = 1, DamageDiceSides = 8 };

        if (!SpellTargetRules.IsValidTarget(caster, enemy, damage))
            return (false, "damage spell should accept enemy targets");
        if (SpellTargetRules.IsValidTarget(caster, ally, damage))
            return (false, "damage spell should not affect allies by default");
        return (true, "");
    }

    private static (bool, string) StatusCanDeclare_Enemies()
    {
        var caster = new UnitData { IsEnemy = false };
        var ally = new UnitData { IsEnemy = false };
        var enemy = new UnitData { IsEnemy = true };
        var control = new SpellData
        {
            AppliedStatusEffect = "charmed",
            targetAffinity = SpellData.SpellTargetAffinity.Enemies,
        };

        if (!SpellTargetRules.IsValidTarget(caster, enemy, control))
            return (false, "enemy control spell should accept enemy targets");
        if (SpellTargetRules.IsValidTarget(caster, ally, control))
            return (false, "enemy control spell should not accept allied targets");
        return (true, "");
    }
}
