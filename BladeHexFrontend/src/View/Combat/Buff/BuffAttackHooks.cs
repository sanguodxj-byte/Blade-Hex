using Godot;
using BladeHex.Combat.Buff;

namespace BladeHex.Combat;

/// <summary>
/// Buff 攻击阶段钩子。
/// 负责处理幻影/镜像等会在攻击命中前改写攻击流程的效果。
/// </summary>
public static class BuffAttackHooks
{
    public static bool TryResolvePhantomInterception(Unit defender, Godot.Collections.Dictionary result)
    {
        if (defender.Data == null) return false;

        var phantom = FindPhantomBuff(defender);
        if (phantom == null) return false;

        float redirectChance = GetModifierValue(phantom, "redirect_chance", defaultValue: 1.0f);
        if (redirectChance < 1.0f && GD.Randf() > redirectChance) return false;

        int phantomAc = (int)GetModifierValue(phantom, "phantom_ac", defaultValue: 12f);
        int roll = BladeHex.Data.RPGRuleEngine.RollDice(1, 20);
        bool hitPhantom = roll >= phantomAc;

        result["phantom_intercept"] = true;
        result["phantom_roll"] = roll;
        result["phantom_ac"] = phantomAc;
        result["hit"] = false;
        result["damage"] = 0;
        result["reason"] = hitPhantom ? "攻击被幻影抵消" : "攻击被幻影干扰";

        if (hitPhantom)
            ConsumeOnePhantom(defender, phantom);

        return true;
    }

    private static BladeHex.Combat.Buff.BuffInstance? FindPhantomBuff(Unit unit)
    {
        foreach (var buff in unit.Data!.Runtime.ActiveBuffs)
        {
            if (buff.Id == "phantom" || BuffModifierReader.BuffHasTruthy(buff, "phantom_ac"))
                return buff;
        }
        return null;
    }

    private static float GetModifierValue(BladeHex.Combat.Buff.BuffInstance buff, string stat, float defaultValue)
        => BuffModifierReader.SumOrDefault(buff, stat, defaultValue);

    private static void ConsumeOnePhantom(Unit unit, BladeHex.Combat.Buff.BuffInstance phantom)
    {
        var countModifier = phantom.Modifiers.Find(m => m.Stat == "phantom_count");
        if (countModifier != null)
        {
            countModifier.Value -= 1;
            if (countModifier.Value <= 0)
                unit.Data!.Runtime.ActiveBuffs.Remove(phantom);
            return;
        }

        unit.Data!.Runtime.ActiveBuffs.Remove(phantom);
    }
}
