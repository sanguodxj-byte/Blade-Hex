using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 伤害阶段钩子。
/// 负责处理进入 HP 前后的通用 Buff 逻辑，保持 BattleUnitModel.ApplyDamage 主流程简洁。
/// </summary>
public static class BuffDamageHooks
{
    public readonly struct HpDamageHookResult
    {
        public int HpDamage { get; init; }
        public int RedirectedHpDamage { get; init; }
        public long RedirectedToUnitId { get; init; }
    }

    public static HpDamageHookResult ApplyBeforeHpDamage(UnitData defender, int hpDamage, bool allowDamageRedirect = true)
    {
        if (hpDamage <= 0) return new HpDamageHookResult { HpDamage = hpDamage };

        hpDamage = ApplyDamageReduction(defender, hpDamage);
        if (hpDamage <= 0) return new HpDamageHookResult { HpDamage = 0 };

        hpDamage = AbsorbWithTempHp(defender, hpDamage);
        if (hpDamage <= 0) return new HpDamageHookResult { HpDamage = 0 };

        hpDamage = AbsorbWithMana(defender, hpDamage);
        if (hpDamage <= 0) return new HpDamageHookResult { HpDamage = 0 };

        return ApplyDamageRedirect(defender, hpDamage, allowDamageRedirect);
    }

    public static int ApplyBeforeDeath(UnitData defender, int currentHp, int hpDamage)
    {
        if (hpDamage <= 0 || currentHp - hpDamage > 0) return hpDamage;

        // con_giant_apex / death_immunity is clamped to 1 HP by BattleUnitModel.ApplyDamage.
        var deathImmune = BuffModifierReader.FirstBuffWithTruthy(defender, "death_immunity");
        if (deathImmune != null)
            return hpDamage;

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

        foreach (var (_, _, value) in BuffModifierReader.Enumerate(defender, "damage_taken"))
            finalMultiplier *= System.Math.Max(0f, 1f + value);

        BuffModifierReader.TrySum(defender, "damage_reduction_flat", out flatReduction);

        int reduced = (int)(hpDamage * finalMultiplier - flatReduction);
        return System.Math.Max(1, reduced);
    }

    private static HpDamageHookResult ApplyDamageRedirect(UnitData defender, int hpDamage, bool allowDamageRedirect)
    {
        if (!allowDamageRedirect)
            return new HpDamageHookResult { HpDamage = hpDamage };

        foreach (var (buff, _, redirectPercent) in BuffModifierReader.Enumerate(defender, "damage_redirect_percent"))
        {
            if (redirectPercent <= 0f) continue;

            long guardianId = ResolveRedirectTargetUnitId(buff);
            if (guardianId <= 0) continue;

            float clampedPercent = System.Math.Clamp(redirectPercent, 0f, 1f);
            int redirected = System.Math.Clamp((int)System.MathF.Ceiling(hpDamage * clampedPercent), 1, hpDamage);
            return new HpDamageHookResult
            {
                HpDamage = hpDamage - redirected,
                RedirectedHpDamage = redirected,
                RedirectedToUnitId = guardianId,
            };
        }

        return new HpDamageHookResult { HpDamage = hpDamage };
    }

    private static long ResolveRedirectTargetUnitId(BuffInstance buff)
    {
        const string guardianSourcePrefix = "guardian:";
        if (buff.Source.StartsWith(guardianSourcePrefix, System.StringComparison.Ordinal)
            && long.TryParse(buff.Source[guardianSourcePrefix.Length..], out long sourceId))
        {
            return sourceId;
        }

        return (long)BuffModifierReader.SumOrDefault(buff, "guardian_id");
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

    /// <summary>v0.8 E3-A: 战法师-法力护盾 — 消耗法力吸收伤害</summary>
    private static int AbsorbWithMana(UnitData defender, int hpDamage)
    {
        if (hpDamage <= 0) return hpDamage;
        if (defender.CurrentMana <= 0) return hpDamage;

        // 检查是否有 mana_shield buff
        bool hasManaShield = false;
        foreach (var buff in defender.Runtime.ActiveBuffs)
        {
            if (buff.Id == "mana_shield")
            {
                hasManaShield = true;
                break;
            }
        }
        if (!hasManaShield) return hpDamage;

        int remainingDamage = hpDamage;

        // 每点法力吸收 2 点伤害（与 CareerSkillResolver.GetManaShieldReduction 一致）
        int manaToUse = System.Math.Min(defender.CurrentMana, (hpDamage + 1) / 2);
        int absorbed = manaToUse * 2;
        defender.CurrentMana -= manaToUse;
        remainingDamage = System.Math.Max(0, remainingDamage - absorbed);

        return remainingDamage;
    }
}
