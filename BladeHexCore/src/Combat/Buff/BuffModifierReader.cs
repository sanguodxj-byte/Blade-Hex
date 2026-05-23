using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff modifier 读取工具。
/// 统一封装 ActiveBuffs/Modifiers 遍历，避免各阶段 hook 重复手写读取规则。
/// </summary>
public static class BuffModifierReader
{
    public static bool HasTruthy(UnitData? unit, string stat)
        => TrySum(unit, stat, out float value) && value != 0f;

    public static bool TrySum(UnitData? unit, string stat, out float value)
    {
        value = 0f;
        if (unit == null) return false;

        bool found = false;
        foreach (var buff in unit.Runtime.ActiveBuffs)
        {
            foreach (var modifier in buff.Modifiers)
            {
                if (modifier.Stat != stat) continue;
                value += modifier.Value * buff.CurrentStacks;
                found = true;
            }
        }

        return found;
    }

    public static float SumOrDefault(UnitData? unit, string stat, float defaultValue = 0f)
        => TrySum(unit, stat, out float value) ? value : defaultValue;

    public static BuffInstance? FirstBuffWithTruthy(UnitData? unit, string stat)
    {
        if (unit == null) return null;
        foreach (var buff in unit.Runtime.ActiveBuffs)
        {
            if (BuffHasTruthy(buff, stat)) return buff;
        }
        return null;
    }

    public static bool BuffHasTruthy(BuffInstance buff, string stat)
        => TrySum(buff, stat, out float value) && value != 0f;

    public static bool TrySum(BuffInstance buff, string stat, out float value)
    {
        value = 0f;
        bool found = false;
        foreach (var modifier in buff.Modifiers)
        {
            if (modifier.Stat != stat) continue;
            value += modifier.Value * buff.CurrentStacks;
            found = true;
        }
        return found;
    }

    public static float SumOrDefault(BuffInstance buff, string stat, float defaultValue = 0f)
        => TrySum(buff, stat, out float value) ? value : defaultValue;

    public static IEnumerable<(BuffInstance Buff, StatModifier Modifier, float Value)> Enumerate(UnitData? unit, string stat)
    {
        if (unit == null) yield break;
        foreach (var buff in unit.Runtime.ActiveBuffs)
        {
            foreach (var modifier in buff.Modifiers)
            {
                if (modifier.Stat != stat) continue;
                yield return (buff, modifier, modifier.Value * buff.CurrentStacks);
            }
        }
    }
}
