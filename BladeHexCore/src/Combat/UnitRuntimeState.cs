// UnitRuntimeState.cs
// 纯 C# 运行时状态容器 — 不继承 Godot 类型，可脱离场景树使用
// 从 UnitData 中提取的战斗运行时状态，解决 Resource 共享引用污染问题
using Godot;
using System.Collections.Generic;

namespace BladeHex.Combat;

/// <summary>
/// 单位运行时状态 — 每场战斗实例化一次，纯数据容器
/// 所有"当前回合/本场战斗"级别的可变状态都放在这里
/// </summary>
public class UnitRuntimeState
{
    public int CurrentHp;
    public int CurrentMana;
    public float CurrentAp;

    public bool HasMoved;
    public bool HasActed;
    public bool UsingPrimaryWeapon = true;
    public bool NonSpellSkillUsedThisTurn;
    public int ExtraActionsThisTurn;
    public bool TimeWarpUsedThisTurn;

    public int Facing;
    public bool IsDefending;
    public bool IsRangedWeaponLoaded = true;

    public bool AooUsedThisTurn;
    public bool CounterUsedThisTurn;

    public int CurrentDr;
    public int MaxDr;
    public int MountCurrentHp;
    public bool IsMounted;

    public int DeathSaveSuccesses;
    public int DeathSaveFailures;
    public int Loyalty = 50;

    public List<StatusEffectInstance> ActiveStatusEffects = new();
    public Dictionary<string, int> SpellCooldowns = new();

    public int LifeShieldUsedThisCombat;
    public int LifeCircleUsedThisCombat;
    public int LastStandUsedThisCombat;
    public int HeroicCallUsedThisCombat;
    public int ResurrectUsedThisCombat;

    public Vector2I GridPos;

    public void ResetForTurnStart()
    {
        HasMoved = false;
        HasActed = false;
        NonSpellSkillUsedThisTurn = false;
        ExtraActionsThisTurn = 0;
        TimeWarpUsedThisTurn = false;
        AooUsedThisTurn = false;
        CounterUsedThisTurn = false;
        IsDefending = false;
        IsRangedWeaponLoaded = true;
    }
}

/// <summary>
/// 状态效果实例 — 替代 Godot.Collections.Dictionary 的强类型方案
/// </summary>
public class StatusEffectInstance
{
    public string Id = "";
    public string Name = "";
    public int Duration;
    public bool IsNegative;
    public Dictionary<string, float> StatModifiers = new();
    public int TickDamageCount;
    public int TickDamageSides;
    public string TickDamageType = "";
    public string SaveToRemove = "";
    public int SaveDc;
    public string[] RemovesEffects = [];
    public bool BreaksOnAttack;
    public bool CanSpread;
    public int SourceUnitId = -1;

    public Godot.Collections.Dictionary ToGodotDict()
    {
        var mods = new Godot.Collections.Dictionary();
        foreach (var kv in StatModifiers)
            mods[kv.Key] = kv.Value;
        return new Godot.Collections.Dictionary
        {
            { "id", Id }, { "name", Name }, { "duration", Duration },
            { "is_negative", IsNegative }, { "stat_modifiers", mods },
            { "tick_damage_count", TickDamageCount }, { "tick_damage_sides", TickDamageSides },
            { "tick_damage_type", TickDamageType }, { "save_to_remove", SaveToRemove },
            { "save_dc", SaveDc }, { "removes_effects", (string[])RemovesEffects.Clone() },
            { "breaks_on_attack", BreaksOnAttack }, { "can_spread", CanSpread },
        };
    }

    public static StatusEffectInstance FromGodotDict(Godot.Collections.Dictionary dict)
    {
        var inst = new StatusEffectInstance
        {
            Id = dict.ContainsKey("id") ? dict["id"].AsString() : "",
            Name = dict.ContainsKey("name") ? dict["name"].AsString() : "",
            Duration = dict.ContainsKey("duration") ? dict["duration"].AsInt32() : 0,
            IsNegative = dict.ContainsKey("is_negative") && dict["is_negative"].AsBool(),
            TickDamageCount = dict.ContainsKey("tick_damage_count") ? dict["tick_damage_count"].AsInt32() : 0,
            TickDamageSides = dict.ContainsKey("tick_damage_sides") ? dict["tick_damage_sides"].AsInt32() : 0,
            TickDamageType = dict.ContainsKey("tick_damage_type") ? dict["tick_damage_type"].AsString() : "",
            SaveToRemove = dict.ContainsKey("save_to_remove") ? dict["save_to_remove"].AsString() : "",
            SaveDc = dict.ContainsKey("save_dc") ? dict["save_dc"].AsInt32() : 0,
            BreaksOnAttack = dict.ContainsKey("breaks_on_attack") && dict["breaks_on_attack"].AsBool(),
            CanSpread = dict.ContainsKey("can_spread") && dict["can_spread"].AsBool(),
        };
        if (dict.ContainsKey("removes_effects"))
        {
            var arr = dict["removes_effects"].AsGodotArray();
            inst.RemovesEffects = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++) inst.RemovesEffects[i] = arr[i].AsString();
        }
        if (dict.ContainsKey("stat_modifiers"))
        {
            var mods = dict["stat_modifiers"].AsGodotDictionary();
            foreach (var key in mods.Keys) inst.StatModifiers[key.AsString()] = mods[key].AsSingle();
        }
        return inst;
    }
}
