using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 状态效果管理器 — 负责状态效果的施加、每回合结算、交互和移除
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

        // 检查是否已存在同名效果
        foreach (var existing in unit.Data.ActiveStatusEffects)
        {
            if (existing["id"].AsString() == effectId)
            {
                if (duration > 0)
                {
                    int currentDur = existing["duration"].AsInt32();
                    existing["duration"] = Math.Max(currentDur, duration);
                }
                return false;
            }
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

        // 添加效果
        var effectInstance = new Godot.Collections.Dictionary
        {
            { "id", effectId },
            { "name", effectData.EffectName },
            { "duration", actualDuration },
            { "is_negative", effectData.IsNegative },
            { "stat_modifiers", effectData.StatModifiers.Duplicate() },
            { "tick_damage_count", effectData.TickDamageDiceCount },
            { "tick_damage_sides", effectData.TickDamageDiceSides },
            { "tick_damage_type", effectData.TickDamageType },
            { "save_to_remove", effectData.SaveToRemove },
            { "save_dc", effectData.SaveDc },
            { "removes_effects", (string[])effectData.RemovesEffects.Clone() },
            { "breaks_on_attack", effectData.BreaksOnAttack },
            { "can_spread", effectData.CanSpread },
            { "source", sourceUnit != null ? Variant.From(sourceUnit) : new Variant() }
        };

        unit.Data.ActiveStatusEffects.Add(effectInstance);
        EmitSignal(SignalName.EffectApplied, unit, effectId);
        return true;
    }

    // ============================================================================
    // 移除状态效果
    // ============================================================================

    public void RemoveEffect(Unit unit, string effectId)
    {
        if (unit.Data == null) return;
        int toRemove = -1;
        for (int i = 0; i < unit.Data.ActiveStatusEffects.Count; i++)
        {
            if (unit.Data.ActiveStatusEffects[i]["id"].AsString() == effectId)
            {
                toRemove = i;
                break;
            }
        }
        if (toRemove >= 0)
        {
            unit.Data.ActiveStatusEffects.RemoveAt(toRemove);
            EmitSignal(SignalName.EffectRemoved, unit, effectId);
        }
    }

    public void RemoveAllNegative(Unit unit)
    {
        if (unit.Data == null) return;
        var toRemove = new List<string>();
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            if (effect["is_negative"].AsBool())
                toRemove.Add(effect["id"].AsString());
        }
        foreach (var eid in toRemove) RemoveEffect(unit, eid);
    }

    public void OnUnitAttacked(Unit unit)
    {
        if (unit.Data == null) return;
        var toRemove = new List<string>();
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            if (effect["breaks_on_attack"].AsBool())
                toRemove.Add(effect["id"].AsString());
        }
        foreach (var eid in toRemove) RemoveEffect(unit, eid);
    }

    // ============================================================================
    // 每回合结算
    // ============================================================================

    public void TickEffects(Unit unit)
    {
        if (unit.Data == null) return;
        var effectsCopy = unit.Data.ActiveStatusEffects.ToList();
        foreach (var effect in effectsCopy)
        {
            string eid = effect["id"].AsString();

            int dmgCount = effect["tick_damage_count"].AsInt32();
            int dmgSides = effect["tick_damage_sides"].AsInt32();

            if (dmgCount > 0 && dmgSides != 0)
            {
                if (dmgSides < 0)
                {
                    int heal = RPGRuleEngine.RollDice(dmgCount, Math.Abs(dmgSides));
                    unit.CurrentHp = Math.Min(unit.CurrentHp + heal, unit.GetMaxHp());
                    EmitSignal(SignalName.EffectTicked, unit, eid, -heal);
                }
                else
                {
                    int dmg = RPGRuleEngine.RollDice(dmgCount, dmgSides);
                    unit.TakeDamage(dmg);
                    EmitSignal(SignalName.EffectTicked, unit, eid, dmg);
                }
            }

            int dur = effect["duration"].AsInt32() - 1;
            effect["duration"] = dur;

            if (dur <= 0)
            {
                RemoveEffect(unit, eid);
                continue;
            }

            string saveType = effect["save_to_remove"].AsString();
            if (!string.IsNullOrEmpty(saveType))
            {
                if (AttemptSave(unit, effect)) RemoveEffect(unit, eid);
            }
        }
    }

    // ============================================================================
    // 效果查询
    // ============================================================================

    public bool HasEffect(Unit unit, string effectId)
    {
        if (unit.Data == null) return false;
        return unit.Data.ActiveStatusEffects.Any(e => e["id"].AsString() == effectId);
    }

    public Godot.Collections.Array GetActiveEffects(Unit unit)
    {
        if (unit.Data == null) return new Godot.Collections.Array();
        var arr = new Godot.Collections.Array();
        foreach (var effect in unit.Data.ActiveStatusEffects)
            arr.Add(effect);
        return arr;
    }

    public Godot.Collections.Dictionary GetEffectModifiers(Unit unit)
    {
        var mods = new Godot.Collections.Dictionary();
        if (unit.Data == null) return mods;
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            var effectMods = effect["stat_modifiers"].AsGodotDictionary();
            foreach (var key in effectMods.Keys)
            {
                string k = key.AsString();
                if (mods.ContainsKey(k))
                {
                    // 假设修正值都是 int 或 float
                    var v1 = mods[k];
                    var v2 = effectMods[key];
                    if (v1.VariantType == Variant.Type.Int && v2.VariantType == Variant.Type.Int)
                        mods[k] = v1.AsInt32() + v2.AsInt32();
                    else
                        mods[k] = v1.AsSingle() + v2.AsSingle();
                }
                else
                {
                    mods[k] = effectMods[key];
                }
            }
        }
        return mods;
    }

    public bool CanAct(Unit unit)
    {
        if (unit.Data == null) return true;
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            var mods = effect["stat_modifiers"].AsGodotDictionary();
            if (mods.ContainsKey("cannot_act") && mods["cannot_act"].AsBool()) return false;
            string eid = effect["id"].AsString();
            if (eid == "freeze" || eid == "stun") return false;
        }
        return true;
    }

    public bool CanMove(Unit unit)
    {
        if (unit.Data == null) return true;
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            var mods = effect["stat_modifiers"].AsGodotDictionary();
            if (mods.ContainsKey("cannot_move") && mods["cannot_move"].AsBool()) return false;
            string eid = effect["id"].AsString();
            if (eid == "root" || eid == "freeze") return false;
        }
        return true;
    }

    public bool CanCast(Unit unit)
    {
        if (unit.Data == null) return true;
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            var mods = effect["stat_modifiers"].AsGodotDictionary();
            if (mods.ContainsKey("cannot_cast") && mods["cannot_cast"].AsBool()) return false;
            string eid = effect["id"].AsString();
            if (eid == "silence") return false;
        }
        return true;
    }

    public bool HasMeleeDisadvantage(Unit unit)
    {
        if (unit.Data == null) return false;
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            var mods = effect["stat_modifiers"].AsGodotDictionary();
            if (mods.ContainsKey("melee_disadvantage") && mods["melee_disadvantage"].AsBool()) return true;
        }
        return false;
    }

    public int GetRangedRangeOverride(Unit unit)
    {
        if (unit.Data == null) return -1;
        foreach (var effect in unit.Data.ActiveStatusEffects)
        {
            var mods = effect["stat_modifiers"].AsGodotDictionary();
            if (mods.ContainsKey("ranged_range_override")) return mods["ranged_range_override"].AsInt32();
        }
        return -1;
    }

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
        var effectsCopy = unit.Data.ActiveStatusEffects.ToList();
        foreach (var existing in effectsCopy)
        {
            var interaction = StatusEffectData.GetInteraction(newEffectId, existing["id"].AsString());
            string action = interaction["action"].AsString();
            switch (action)
            {
                case "cancel_both":
                    RemoveEffect(unit, existing["id"].AsString());
                    break;
                case "cancel_b":
                    RemoveEffect(unit, existing["id"].AsString());
                    break;
                case "extend_b":
                    int dur = existing["duration"].AsInt32() + interaction["value"].AsInt32();
                    existing["duration"] = dur;
                    break;
            }
        }
    }

    private bool AttemptSave(Unit unit, Godot.Collections.Dictionary effect)
    {
        if (unit.Data == null) return false;
        string saveType = effect["save_to_remove"].AsString();
        int dc = effect["save_dc"].AsInt32();

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
