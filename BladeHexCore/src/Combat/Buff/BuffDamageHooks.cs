using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 伤害阶段钩子。
/// 负责处理进入 HP 前后的通用 Buff 逻辑，保持 BattleUnitModel.ApplyDamage 主流程简洁。
/// </summary>
public static class BuffDamageHooks
{
    public static int ApplyBeforeHpDamage(UnitData defender, int hpDamage)
    {
        if (hpDamage <= 0) return hpDamage;

        hpDamage = ApplyDamageReduction(defender, hpDamage);
        if (hpDamage <= 0) return 0;

        hpDamage = AbsorbWithTempHp(defender, hpDamage);
        return hpDamage;
    }

    public static int ApplyBeforeDeath(UnitData defender, int currentHp, int hpDamage)
    {
        if (hpDamage <= 0 || currentHp - hpDamage > 0) return hpDamage;

        var guard = BuffModifierReader.FirstBuffWithTruthy(defender, "guarded");
        if (guard == null) return hpDamage;

        defender.Runtime.ActiveBuffs.Remove(guard);
        return System.Math.Max(0, currentHp - 1);
    }

    private static int ApplyDamageReduction(UnitData defender, int hpDamage)
    {
        float finalMultiplier = 1.0f;
        float flatReduction = 0f;

        foreach (var (_, _, value) in BuffModifierReader.Enumerate(defender, "damage_reduction_percent"))
            finalMultiplier *= System.Math.Max(0f, 1f - value);

        foreach (var (_, _, value) in BuffModifierReader.Enumerate(defender, "damage_taken_final_mult"))
            finalMultiplier *= System.Math.Max(0f, value);

        BuffModifierReader.TrySum(defender, "damage_reduction_flat", out flatReduction);

        int reduced = (int)(hpDamage * finalMultiplier - flatReduction);
        return System.Math.Max(1, reduced);
    }

    private static int AbsorbWithTempHp(UnitData defender, int hpDamage)
    {
        int remainingDamage = hpDamage;
        var toRemove = new List<BuffInstance>();

        foreach (var buff in defender.Runtime.ActiveBuffs)
        {
            if (remainingDamage <= 0) break;

            foreach (var modifier in buff.Modifiers)
            {
                if (modifier.Stat != "temp_hp_amount" || modifier.Value <= 0) continue;

                int absorb = System.Math.Min((int)modifier.Value, remainingDamage);
                modifier.Value -= absorb;
                remainingDamage -= absorb;

                if (modifier.Value <= 0)
                    toRemove.Add(buff);
                break;
            }
        }

        foreach (var buff in toRemove)
            defender.Runtime.ActiveBuffs.Remove(buff);

        return remainingDamage;
    }
}
