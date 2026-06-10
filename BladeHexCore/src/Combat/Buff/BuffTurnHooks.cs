using System.Collections.Generic;
using BladeHex.Combat;
using BladeHex.Data;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 回合阶段钩子。
/// 负责回合开始时的 tick 数值、豁免解除与持续时间递减，
/// 让 BuffSystem 保持门面职责。
/// </summary>
public static class BuffTurnHooks
{
    public readonly struct TickResult
    {
        public int NetHpDelta { get; init; }
        public IReadOnlyList<string> RemovedBuffIds { get; init; }
        public IReadOnlyList<string> TickedBuffIds { get; init; }
    }

    public static TickResult TickTurnStart(UnitData target)
    {
        int netHpDelta = 0;
        var toRemove = new List<BuffInstance>();
        var ticked = new List<string>();

        foreach (var buff in target.Runtime.ActiveBuffs.ToArray())
        {
            if (buff.OnTick != null)
            {
                int tickValue = ResolveTickValue(target, buff.OnTick) * buff.CurrentStacks;
                netHpDelta += buff.OnTick.IsHeal ? -tickValue : tickValue;
                ticked.Add(buff.Id);
            }

            if (ShouldRemoveBySave(target, buff))
            {
                toRemove.Add(buff);
                continue;
            }

            if (ShouldExpireByDuration(buff))
                toRemove.Add(buff);
        }

        var removedIds = new List<string>();
        foreach (var buff in toRemove)
        {
            if (target.Runtime.ActiveBuffs.Remove(buff))
                removedIds.Add(buff.Id);
        }

        return new TickResult
        {
            NetHpDelta = netHpDelta,
            RemovedBuffIds = removedIds,
            TickedBuffIds = ticked,
        };
    }

    private static bool ShouldRemoveBySave(UnitData target, BuffInstance buff)
    {
        if (string.IsNullOrEmpty(buff.SaveToRemove)) return false;
        int abilityScore = buff.SaveToRemove switch
        {
            "fortitude" => CombatStats.GetEffectiveCon(target),
            "reflex" => CombatStats.GetEffectiveDex(target),
            "will" => CombatStats.GetEffectiveWis(target),
            _ => 10,
        };
        var result = RPGRuleEngine.MakeSave(abilityScore, CombatStats.GetSaveBonus(target), false, buff.SaveDc);
        return result.ContainsKey("success") && result["success"].AsBool();
    }

    private static bool ShouldExpireByDuration(BuffInstance buff)
    {
        if (buff.Duration <= 0) return false;
        buff.Duration--;
        return buff.Duration <= 0;
    }

    private static int ResolveTickValue(UnitData target, TickEffect tick)
    {
        if (tick.TargetMaxHpPercent > 0.0f)
            return System.Math.Max(1, (int)System.MathF.Ceiling(CombatStats.GetMaxHp(target) * tick.TargetMaxHpPercent));

        return RollTick(tick);
    }

    private static int RollTick(TickEffect tick)
        => RPGRuleEngine.RollDice(tick.DiceCount, tick.DiceSides);
}
