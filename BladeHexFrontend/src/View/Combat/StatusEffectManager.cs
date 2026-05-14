using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Events;

namespace BladeHex.Combat;

/// <summary>
/// 状态效果管理器 — 强类型 StatusEffectInstance
/// 唯一数据源：unit.Data.Runtime.ActiveStatusEffects
/// </summary>
public partial class StatusEffectManager : Node
{
    [Signal] public delegate void EffectAppliedEventHandler(Unit unit, string effectId);
    [Signal] public delegate void EffectRemovedEventHandler(Unit unit, string effectId);
    [Signal] public delegate void EffectTickedEventHandler(Unit unit, string effectId, int damage);

    // ============================================================================
    // 施加状态效果
    // ============================================================================

    public bool ApplyEffect(Unit unit, string effectId, int duration = -1, Unit? sourceUnit = null)
    {
        if (string.IsNullOrEmpty(effectId)) return false;
        if (unit.Data == null) return false;

        // 检查免疫
        if (unit.Data.Immunities.Contains(effectId)) return false;

        var effects = unit.Data.Runtime.ActiveStatusEffects;

        // 检查是否已存在同名效果
        var existingInst = effects.FirstOrDefault(e => e.Id == effectId);
        if (existingInst != null)
        {
            if (duration > 0)
                existingInst.Duration = Math.Max(existingInst.Duration, duration);
            return false;
        }

        // 检查交互
        CheckInteractions(unit, effectId);

        // 获取默认数据
        var effectEnumVal = EffectNameToEnum(effectId);
        if ((int)effectEnumVal < 0) return false;

        var effectData = StatusEffectData.CreateEffect(effectEnumVal);
        if (effectData == null) return false;

        // 确定持续时间
        int actualDuration = duration > 0 ? duration : effectData.DefaultDuration;

        // 构建强类型实例
        var inst = new StatusEffectInstance
        {
            Id = effectId,
            Name = effectData.EffectName,
            Duration = actualDuration,
            IsNegative = effectData.IsNegative,
            TickDamageCount = effectData.TickDamageDiceCount,
            TickDamageSides = effectData.TickDamageDiceSides,
            TickDamageType = effectData.TickDamageType,
            SaveToRemove = effectData.SaveToRemove,
            SaveDc = effectData.SaveDc,
            RemovesEffects = (string[])effectData.RemovesEffects.Clone(),
            BreaksOnAttack = effectData.BreaksOnAttack,
            CanSpread = effectData.CanSpread,
            SourceUnitId = sourceUnit != null ? (int)sourceUnit.GetInstanceId() : -1,
        };

        // 复制 stat modifiers（Godot Variant → float，布尔 true=1.0f / false=0.0f）
        foreach (var key in effectData.StatModifiers.Keys)
        {
            var v = effectData.StatModifiers[key];
            float fVal = v.VariantType == Variant.Type.Bool
                ? (v.AsBool() ? 1.0f : 0.0f)
                : v.AsSingle();
            inst.StatModifiers[key.AsString()] = fVal;
        }

        effects.Add(inst);

        EventBus.Instance?.Publish(EventBus.Signals.StatusEffectApplied, new Godot.Collections.Dictionary
        {
            { "unit", unit }, { "effect_id", effectId },
        });
        EmitSignal(SignalName.EffectApplied, unit, effectId);
        return true;
    }

    // ============================================================================
    // 移除状态效果
    // ============================================================================

    public void RemoveEffect(Unit unit, string effectId)
    {
        if (unit.Data == null) return;
        int removed = unit.Data.Runtime.ActiveStatusEffects.RemoveAll(e => e.Id == effectId);
        if (removed > 0)
        {
            EventBus.Instance?.Publish(EventBus.Signals.StatusEffectRemoved, new Godot.Collections.Dictionary
            {
                { "unit", unit }, { "effect_id", effectId },
            });
            EmitSignal(SignalName.EffectRemoved, unit, effectId);
        }
    }

    public void RemoveAllNegative(Unit unit)
    {
        if (unit.Data == null) return;
        var toRemove = unit.Data.Runtime.ActiveStatusEffects
            .Where(e => e.IsNegative).Select(e => e.Id).ToList();
        foreach (var eid in toRemove) RemoveEffect(unit, eid);
    }

    public void OnUnitAttacked(Unit unit)
    {
        if (unit.Data == null) return;
        var toRemove = unit.Data.Runtime.ActiveStatusEffects
            .Where(e => e.BreaksOnAttack).Select(e => e.Id).ToList();
        foreach (var eid in toRemove) RemoveEffect(unit, eid);
    }

    // ============================================================================
    // 每回合结算
    // ============================================================================

    public void TickEffects(Unit unit)
    {
        if (unit.Data == null) return;
        var effects = unit.Data.Runtime.ActiveStatusEffects;
        var effectsCopy = effects.ToList();
        var toRemove = new List<string>();

        foreach (var inst in effectsCopy)
        {
            if (inst.TickDamageCount > 0 && inst.TickDamageSides != 0)
            {
                if (inst.TickDamageSides < 0)
                {
                    int heal = RPGRuleEngine.RollDice(inst.TickDamageCount, Math.Abs(inst.TickDamageSides));
                    unit.Heal(heal);
                    EmitSignal(SignalName.EffectTicked, unit, inst.Id, -heal);
                }
                else
                {
                    int dmg = RPGRuleEngine.RollDice(inst.TickDamageCount, inst.TickDamageSides);
                    unit.TakeDamage(dmg);
                    EventBus.Instance?.Publish(EventBus.Signals.StatusEffectTicked, new Godot.Collections.Dictionary
                    {
                        { "unit", unit }, { "effect_id", inst.Id }, { "damage", dmg },
                    });
                    EmitSignal(SignalName.EffectTicked, unit, inst.Id, dmg);
                }
            }

            inst.Duration -= 1;

            if (inst.Duration <= 0)
            {
                toRemove.Add(inst.Id);
                continue;
            }

            if (!string.IsNullOrEmpty(inst.SaveToRemove))
            {
                if (AttemptSave(unit, inst)) toRemove.Add(inst.Id);
            }
        }

        foreach (var eid in toRemove) RemoveEffect(unit, eid);
    }

    // ============================================================================
    // 效果查询
    // ============================================================================

    public bool HasEffect(Unit unit, string effectId)
    {
        if (unit.Data == null) return false;
        return unit.Data.Runtime.ActiveStatusEffects.Any(e => e.Id == effectId);
    }

    public Godot.Collections.Array GetActiveEffects(Unit unit)
    {
        if (unit.Data == null) return new Godot.Collections.Array();
        var arr = new Godot.Collections.Array();
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
            arr.Add(inst.ToGodotDict());
        return arr;
    }

    public Godot.Collections.Dictionary GetEffectModifiers(Unit unit)
    {
        var mods = new Godot.Collections.Dictionary();
        if (unit.Data == null) return mods;
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
        {
            foreach (var kv in inst.StatModifiers)
            {
                if (mods.ContainsKey(kv.Key))
                    mods[kv.Key] = mods[kv.Key].AsSingle() + kv.Value;
                else
                    mods[kv.Key] = kv.Value;
            }
        }
        return mods;
    }

    public bool CanAct(Unit unit)
    {
        if (unit.Data == null) return true;
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
        {
            if (inst.StatModifiers.TryGetValue("cannot_act", out float val) && val != 0) return false;
            if (inst.Id == "freeze" || inst.Id == "stun") return false;
        }
        return true;
    }

    public bool CanMove(Unit unit)
    {
        if (unit.Data == null) return true;
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
        {
            if (inst.StatModifiers.TryGetValue("cannot_move", out float val) && val != 0) return false;
            if (inst.Id == "root" || inst.Id == "freeze") return false;
        }
        return true;
    }

    public bool CanCast(Unit unit)
    {
        if (unit.Data == null) return true;
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
        {
            if (inst.StatModifiers.TryGetValue("cannot_cast", out float val) && val != 0) return false;
            if (inst.Id == "silence") return false;
        }
        return true;
    }

    public bool HasMeleeDisadvantage(Unit unit)
    {
        if (unit.Data == null) return false;
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
        {
            if (inst.StatModifiers.TryGetValue("melee_disadvantage", out float val) && val != 0) return true;
        }
        return false;
    }

    public int GetRangedRangeOverride(Unit unit)
    {
        if (unit.Data == null) return -1;
        foreach (var inst in unit.Data.Runtime.ActiveStatusEffects)
        {
            if (inst.StatModifiers.TryGetValue("ranged_range_override", out float val)) return (int)val;
        }
        return -1;
    }

    /// <summary>获取单位的强类型效果列表（直接访问 Runtime）</summary>
    public List<StatusEffectInstance> GetTypedEffects(Unit unit)
    {
        if (unit.Data == null) return new List<StatusEffectInstance>();
        return unit.Data.Runtime.ActiveStatusEffects;
    }

    public float GetHealingMultiplier(Unit unit) => 1.0f;
    public bool IsForbiddenFromDealingDamage() => false;
    public float GetMediumWeaponArmorDamageMultiplier() => 1.0f;

    // ============================================================================
    // 内部方法
    // ============================================================================

    private void CheckInteractions(Unit unit, string newEffectId)
    {
        var enumVal = EffectNameToEnum(newEffectId);
        if ((int)enumVal < 0) return;
        var newData = StatusEffectData.CreateEffect(enumVal);
        if (newData == null) return;

        foreach (var removes in newData.RemovesEffects)
        {
            if (HasEffect(unit, removes)) RemoveEffect(unit, removes);
        }

        if (unit.Data == null) return;
        var effectsCopy = unit.Data.Runtime.ActiveStatusEffects.ToList();
        foreach (var existing in effectsCopy)
        {
            var interaction = StatusEffectData.GetInteraction(newEffectId, existing.Id);
            string action = interaction["action"].AsString();
            switch (action)
            {
                case "cancel_both":
                case "cancel_b":
                    RemoveEffect(unit, existing.Id);
                    break;
                case "extend_b":
                    existing.Duration += interaction["value"].AsInt32();
                    break;
            }
        }
    }

    private bool AttemptSave(Unit unit, StatusEffectInstance inst)
    {
        if (unit.Data == null) return false;
        string saveType = inst.SaveToRemove;
        int dc = inst.SaveDc;

        int abilityScore = 10;
        switch (saveType)
        {
            case "fortitude": abilityScore = unit.Data.Con; break;
            case "reflex": abilityScore = unit.Data.Dex; break;
            case "will": abilityScore = unit.Data.Wis; break;
        }

        int prof = RPGRuleEngine.GetProficiencyBonus(unit.Data.Level);
        var result = RPGRuleEngine.MakeSave(abilityScore, prof, false, dc);
        return result.ContainsKey("success") && result["success"].AsBool();
    }

    private StatusEffectData.EffectId EffectNameToEnum(string name) => name switch
    {
        "poison" => StatusEffectData.EffectId.Poison,
        "burning" => StatusEffectData.EffectId.Burning,
        "freeze" => StatusEffectData.EffectId.Freeze,
        "fear" => StatusEffectData.EffectId.Fear,
        "silence" => StatusEffectData.EffectId.Silence,
        "blind" => StatusEffectData.EffectId.Blind,
        "stun" => StatusEffectData.EffectId.Stun,
        "bleed" => StatusEffectData.EffectId.Bleed,
        "slow" => StatusEffectData.EffectId.Slow,
        "root" => StatusEffectData.EffectId.Root,
        "charmed" => StatusEffectData.EffectId.Charmed,
        "confused" => StatusEffectData.EffectId.Confused,
        "wet" => StatusEffectData.EffectId.Wet,
        "bless" => StatusEffectData.EffectId.Bless,
        "shield" => StatusEffectData.EffectId.Shield,
        "haste" => StatusEffectData.EffectId.Haste,
        "regen" => StatusEffectData.EffectId.Regen,
        "invisibility" => StatusEffectData.EffectId.Invisibility,
        "phantom" => StatusEffectData.EffectId.Phantom,
        "temp_hp" => StatusEffectData.EffectId.TempHp,
        _ => (StatusEffectData.EffectId)(-1)
    };
}
