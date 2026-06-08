using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 系统核心服务。纯静态,不持有状态;状态存在 UnitData.Runtime.ActiveBuffs 上。
/// 职责:施加/移除/tick/查询属性/触发事件。
/// </summary>
public static class BuffSystem
{
    // ============================================================
    // 施加
    // ============================================================

    /// <summary>给目标施加一个 buff(从 BuffRegistry 取模板,克隆为实例)</summary>
    public static BuffInstance? Apply(UnitData target, string buffId, int duration = -1, int sourceUnitId = -1, string source = "")
    {
        var template = BuffRegistry.Get(buffId);
        if (template == null) return null;

        var instance = Clone(template);
        if (duration > 0) instance.Duration = duration;
        instance.SourceUnitId = sourceUnitId;
        if (!string.IsNullOrEmpty(source)) instance.Source = source;

        return ApplyInstance(target, instance);
    }

    /// <summary>直接施加一个已构造好的 BuffInstance(不走 Registry)</summary>
    public static BuffInstance? ApplyDirect(UnitData target, BuffInstance instance)
        => ApplyInstance(target, instance);

    // ============================================================
    // 移除
    // ============================================================

    public static bool Remove(UnitData target, string buffId)
    {
        return target.Runtime.ActiveBuffs.RemoveAll(b => b.Id == buffId) > 0;
    }

    public static bool RemoveBySource(UnitData target, string source)
    {
        if (string.IsNullOrEmpty(source)) return false;
        return target.Runtime.ActiveBuffs.RemoveAll(b => b.Source == source) > 0;
    }

    public static void RemoveByTag(UnitData target, string tag)
    {
        target.Runtime.ActiveBuffs.RemoveAll(b => b.Tags.Contains(tag));
    }

    public static void RemoveAll(UnitData target)
    {
        target.Runtime.ActiveBuffs.Clear();
    }

    // ============================================================
    // Tick(回合开始时调用)
    // ============================================================

    /// <summary>
    /// 处理目标身上所有 buff 的 tick:
    /// 1. 触发 OnBuffTick 触发器
    /// 2. 执行 TickEffect(伤害/治疗)
    /// 3. 尝试豁免解除
    /// 4. 持续时间 -1,到 0 自动移除
    /// 返回本次 tick 造成的总伤害(正=伤害,负=治疗)
    /// </summary>
    public static int TickAll(UnitData target)
    {
        return BuffTurnHooks.TickTurnStart(target).NetHpDelta;
    }

    // ============================================================
    // 查询:多乘区属性计算
    // ============================================================

    /// <summary>
    /// 计算目标身上所有 buff 对指定属性的总修正值(已按乘区规则合并)。
    /// 返回: (flatBonus, increasedPercent, moreMultiplier, finalMultiplier, overrideValue)
    /// 调用方用: result = (base + flatBonus) * (1 + increasedPercent) * moreMultiplier * finalMultiplier
    /// 如果 overrideValue.HasValue,直接用它替代整个计算。
    /// </summary>
    public static StatResolveResult ResolveStatModifiers(UnitData target, string stat, string condition = "")
    {
        var result = new StatResolveResult();

        // 同源同层取最高的 tracker
        var bestBySourceBase = new Dictionary<string, float>();

        foreach (var buff in target.Runtime.ActiveBuffs)
        {
            float stackMult = buff.CurrentStacks;
            foreach (var mod in buff.Modifiers)
            {
                if (mod.Stat != stat) continue;
                if (!string.IsNullOrEmpty(mod.Condition) && mod.Condition != condition) continue;

                float value = mod.Value * stackMult;
                string sourceKey = string.IsNullOrEmpty(mod.Source) ? buff.Id : mod.Source;

                switch (mod.Layer)
                {
                    case ModifierLayer.Base:
                        // 同源取最高
                        if (!bestBySourceBase.TryGetValue(sourceKey, out float existing) || value > existing)
                            bestBySourceBase[sourceKey] = value;
                        break;
                    case ModifierLayer.Increased:
                        result.IncreasedPercent += value;
                        break;
                    case ModifierLayer.More:
                        result.MoreMultiplier *= (1f + value);
                        break;
                    case ModifierLayer.FinalMult:
                        result.FinalMultiplier *= (1f + value);
                        break;
                    case ModifierLayer.Override:
                        if (!result.OverrideValue.HasValue || value > result.OverrideValue.Value)
                            result.OverrideValue = value;
                        break;
                }
            }
        }

        result.FlatBonus = bestBySourceBase.Values.Sum();
        return result;
    }

    /// <summary>
    /// 获取目标某 stat 的 buff 百分比乘区合并倍率(Increased 累加% × More 独乘 × FinalMult 独乘)。
    /// 不含 Base/FlatBonus(那是平加值,由 GetBuffStatBonus 单独取)。无任何百分比修正时返回 1.0。
    /// 用于 "damage" 等需要把 +X% buff 折进伤害末端倍率的场景。
    /// </summary>
    public static float ResolveMultiplier(UnitData target, string stat, string condition = "")
    {
        var r = ResolveStatModifiers(target, stat, condition);
        return (1f + r.IncreasedPercent) * r.MoreMultiplier * r.FinalMultiplier;
    }

    /// <summary>快捷:检查目标是否有指定 buff</summary>
    public static bool HasBuff(UnitData target, string buffId)
        => target.Runtime.ActiveBuffs.Any(b => b.Id == buffId);

    /// <summary>快捷:检查目标是否有指定标签的 buff</summary>
    public static bool HasTag(UnitData target, string tag)
        => target.Runtime.ActiveBuffs.Any(b => b.Tags.Contains(tag));

    /// <summary>获取指定 buff 的当前层数(0=没有)</summary>
    public static int GetStacks(UnitData target, string buffId)
        => target.Runtime.ActiveBuffs.Find(b => b.Id == buffId)?.CurrentStacks ?? 0;

    // ============================================================
    // 触发器
    // ============================================================

    /// <summary>触发目标身上所有 buff 的指定事件。返回所有触发的效果字符串列表。</summary>
    public static List<string> FireTriggers(UnitData target, TriggerEvent triggerEvent)
    {
        var effects = new List<string>();
        foreach (var buff in target.Runtime.ActiveBuffs)
        {
            foreach (var trigger in buff.Triggers)
            {
                if (trigger.Event != triggerEvent) continue;
                if (trigger.MaxTriggersPerCombat >= 0 && trigger.CurrentTriggerCount >= trigger.MaxTriggersPerCombat) continue;
                if (trigger.Chance < 1.0f && RPGRuleEngine.RollDice(1, 100) > (int)(trigger.Chance * 100)) continue;
                // 条件检查(简化:暂不实现复杂条件解析)
                trigger.CurrentTriggerCount++;
                effects.Add(trigger.Effect);
            }
        }
        return effects;
    }

    // ============================================================
    // 叠加层数操作
    // ============================================================

    /// <summary>增加指定 buff 的层数(不超过 MaxStacks)。若无该 buff 则无操作。</summary>
    public static void IncrementStacks(UnitData target, string buffId)
    {
        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == buffId);
        if (buff != null && buff.CurrentStacks < buff.MaxStacks)
            buff.CurrentStacks++;
    }

    /// <summary>设置指定 buff 的层数(钳制在 1~MaxStacks 之间)。若无该 buff 则无操作。</summary>
    public static void SetStacks(UnitData target, string buffId, int count)
    {
        var buff = target.Runtime.ActiveBuffs.Find(b => b.Id == buffId);
        if (buff != null)
            buff.CurrentStacks = Math.Clamp(count, 1, buff.MaxStacks);
    }

    // ============================================================
    // 修饰器操作
    // ============================================================

    /// <summary>安全递减目标 buff 上指定 stat 的首个 modifier 值。
    /// 当 modifier 值 ≤ 0 时自动移除该 buff。
    /// 返回 true 表示 buff 已被移除。</summary>
    public static bool ConsumeModifierStack(UnitData target, BuffInstance buff, string stat)
    {
        var mod = buff.Modifiers.Find(m => m.Stat == stat);
        if (mod != null)
        {
            mod.Value -= 1;
            if (mod.Value <= 0)
            {
                target.Runtime.ActiveBuffs.Remove(buff);
                return true;
            }
            return false;
        }
        // 无指定 modifier 则直接移除
        target.Runtime.ActiveBuffs.Remove(buff);
        return true;
    }

    /// <summary>按引用移除 buff。</summary>
    public static bool RemoveBuffInstance(UnitData target, BuffInstance buff)
        => target.Runtime.ActiveBuffs.Remove(buff);

    // ============================================================
    // 内部工具
    // ============================================================

    private static BuffInstance? ApplyInstance(UnitData target, BuffInstance instance)
    {
        if (!CanApply(target, instance))
            return null;

        if (instance.CancelTags.Length > 0)
            target.Runtime.ActiveBuffs.RemoveAll(b => b.Tags.Any(t => instance.CancelTags.Contains(t)));

        var existing = target.Runtime.ActiveBuffs.Find(b =>
            b.Id == instance.Id
            && b.Source == instance.Source
            && b.SourceUnitId == instance.SourceUnitId);
        if (existing != null)
        {
            if (existing.CurrentStacks < existing.MaxStacks)
                existing.CurrentStacks++;
            existing.Duration = Math.Max(existing.Duration, instance.Duration);
            return existing;
        }

        target.Runtime.ActiveBuffs.Add(instance);
        return instance;
    }

    private static bool CanApply(UnitData target, BuffInstance instance)
    {
        if (!instance.IsNegative && !SkillTreeKeystoneResolver.CanReceivePositiveBuff(target))
            return false;

        if (instance.Tags.Any(t => t == "fear") || instance.Id == "fear")
        {
            if (SkillTreeKeystoneResolver.IsImmuneToFear(target))
                return false;
            var immuneMod = ResolveStatModifiers(target, "immune_fear");
            if (immuneMod.OverrideValue.HasValue && immuneMod.OverrideValue.Value >= 1f)
                return false;
        }

        if (instance.Tags.Any(t => t == "mind") || instance.Id.Contains("mind", StringComparison.Ordinal))
        {
            if (SkillTreeKeystoneResolver.IsImmuneToMind(target))
                return false;
        }

        if (instance.IsNegative)
        {
            if (SkillTreeKeystoneResolver.IsImmuneToNegative(target))
                return false;
            var immuneNeg = ResolveStatModifiers(target, "immune_negative");
            if (immuneNeg.OverrideValue.HasValue && immuneNeg.OverrideValue.Value >= 1f)
                return false;
        }

        return true;
    }

    private static BuffInstance Clone(BuffInstance template)
    {
        return new BuffInstance
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IconId = template.IconId,
            IsNegative = template.IsNegative,
            Tags = (string[])template.Tags.Clone(),
            Duration = template.Duration,
            MaxStacks = template.MaxStacks,
            CurrentStacks = 1,
            Modifiers = template.Modifiers.Select(m => new StatModifier
            {
                Stat = m.Stat, Layer = m.Layer, Value = m.Value,
                Condition = m.Condition, Source = m.Source,
            }).ToList(),
            Triggers = template.Triggers.Select(t => new BuffTrigger
            {
                Event = t.Event, Effect = t.Effect, Condition = t.Condition,
                Chance = t.Chance, MaxTriggersPerCombat = t.MaxTriggersPerCombat,
                CurrentTriggerCount = 0,
            }).ToList(),
            OnTick = template.OnTick,
            Source = template.Source,
            CancelTags = (string[])template.CancelTags.Clone(),
            SaveToRemove = template.SaveToRemove,
            SaveDc = template.SaveDc,
            BreaksOnAttack = template.BreaksOnAttack,
            CanSpread = template.CanSpread,
            PersistOnDeath = template.PersistOnDeath,
        };
    }
}

/// <summary>属性修正解算结果</summary>
public struct StatResolveResult
{
    public float FlatBonus;
    public float IncreasedPercent;   // 所有 Increased 层加法合并
    public float MoreMultiplier;     // 所有 More 层独立相乘(初始 1.0)
    public float FinalMultiplier;    // 所有 FinalMult 层独立相乘(初始 1.0)
    public float? OverrideValue;     // 有值时直接替代计算

    public StatResolveResult()
    {
        FlatBonus = 0;
        IncreasedPercent = 0;
        MoreMultiplier = 1.0f;
        FinalMultiplier = 1.0f;
        OverrideValue = null;
    }

    /// <summary>把 base 值通过本结果的乘区规则计算出最终值</summary>
    public float Apply(float baseValue)
    {
        if (OverrideValue.HasValue) return OverrideValue.Value;
        return (baseValue + FlatBonus) * (1f + IncreasedPercent) * MoreMultiplier * FinalMultiplier;
    }

    public int ApplyInt(int baseValue) => (int)Apply(baseValue);
}
