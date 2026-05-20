using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

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

        // 互斥:移除目标身上含 CancelTags 的 buff
        if (instance.CancelTags.Length > 0)
        {
            target.Runtime.ActiveBuffs.RemoveAll(b =>
                b.Tags.Any(t => instance.CancelTags.Contains(t)));
        }

        // 同源同 ID 不叠加:刷新持续时间 / 增加层数
        var existing = target.Runtime.ActiveBuffs.Find(b => b.Id == buffId && b.Source == instance.Source);
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

    /// <summary>直接施加一个已构造好的 BuffInstance(不走 Registry)</summary>
    public static void ApplyDirect(UnitData target, BuffInstance instance)
    {
        if (instance.CancelTags.Length > 0)
            target.Runtime.ActiveBuffs.RemoveAll(b => b.Tags.Any(t => instance.CancelTags.Contains(t)));

        var existing = target.Runtime.ActiveBuffs.Find(b => b.Id == instance.Id && b.Source == instance.Source);
        if (existing != null)
        {
            if (existing.CurrentStacks < existing.MaxStacks) existing.CurrentStacks++;
            existing.Duration = Math.Max(existing.Duration, instance.Duration);
            return;
        }
        target.Runtime.ActiveBuffs.Add(instance);
    }

    // ============================================================
    // 移除
    // ============================================================

    public static bool Remove(UnitData target, string buffId)
    {
        return target.Runtime.ActiveBuffs.RemoveAll(b => b.Id == buffId) > 0;
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
        int totalDamage = 0;
        var toRemove = new List<BuffInstance>();

        foreach (var buff in target.Runtime.ActiveBuffs)
        {
            // Tick 伤害/治疗
            if (buff.OnTick != null)
            {
                int tickValue = RollTick(buff.OnTick) * buff.CurrentStacks;
                if (buff.OnTick.IsHeal)
                    totalDamage -= tickValue; // 负值=治疗
                else
                    totalDamage += tickValue;
            }

            // 豁免尝试
            if (!string.IsNullOrEmpty(buff.SaveToRemove))
            {
                // 简化:掷 d20 >= SaveDc 则解除
                int roll = RPGRuleEngine.RollDice(1, 20);
                if (roll >= buff.SaveDc)
                {
                    toRemove.Add(buff);
                    continue;
                }
            }

            // 持续时间递减
            if (buff.Duration > 0)
            {
                buff.Duration--;
                if (buff.Duration <= 0)
                    toRemove.Add(buff);
            }
        }

        foreach (var b in toRemove)
            target.Runtime.ActiveBuffs.Remove(b);

        return totalDamage;
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
    // 内部工具
    // ============================================================

    private static int RollTick(TickEffect tick)
    {
        return RPGRuleEngine.RollDice(tick.DiceCount, tick.DiceSides);
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
