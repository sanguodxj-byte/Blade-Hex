using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// Buff 驱动的目标选择规则。
/// 迁移期集中处理 untargetable / ai_ignore / forced_target_id 等影响目标选择的 modifier，
/// 避免 AI、玩家攻击、技能释放各自解析 ActiveBuffs。
/// </summary>
public static class BuffTargetingRules
{
    public static bool IsDirectlyTargetable(Unit target)
    {
        if (target.Data == null) return true;
        return !HasTruthyModifier(target, "untargetable");
    }

    public static bool ShouldAiIgnore(Unit target)
    {
        if (target.Data == null) return false;
        return HasTruthyModifier(target, "ai_ignore");
    }

    public static Unit? ResolveForcedTarget(Unit actor, IEnumerable<Unit> candidates)
    {
        if (actor.Data == null) return null;

        foreach (var buff in actor.Data.Runtime.ActiveBuffs)
        {
            foreach (var modifier in buff.Modifiers)
            {
                if (modifier.Stat != "forced_target_id" || modifier.Value <= 0f) continue;
                int forcedCharacterId = (int)modifier.Value;
                return candidates.FirstOrDefault(u =>
                    GodotObject.IsInstanceValid(u)
                    && u.CurrentHp > 0
                    && u.Data?.CharacterId == forcedCharacterId);
            }
        }

        return null;
    }

    public static bool HasTruthyModifier(Unit unit, string stat)
    {
        if (unit.Data == null) return false;
        foreach (var buff in unit.Data.Runtime.ActiveBuffs)
        {
            foreach (var modifier in buff.Modifiers)
            {
                if (modifier.Stat == stat && modifier.Value * buff.CurrentStacks != 0f)
                    return true;
            }
        }
        return false;
    }

    public static bool HasModifierValue(Unit unit, string stat, out float value)
    {
        value = 0f;
        if (unit.Data == null) return false;
        bool found = false;
        foreach (var buff in unit.Data.Runtime.ActiveBuffs)
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
}
