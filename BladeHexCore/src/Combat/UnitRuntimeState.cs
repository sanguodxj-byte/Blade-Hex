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

    /// <summary>新 Buff 系统的活跃 buff 列表(替代 ActiveStatusEffects,逐步迁移)</summary>
    public List<BladeHex.Combat.Buff.BuffInstance> ActiveBuffs = new();
    public Dictionary<string, int> SpellCooldowns = new();

    public int LifeShieldUsedThisCombat;
    public int LifeCircleUsedThisCombat;
    public int LastStandUsedThisCombat;
    public int HeroicCallUsedThisCombat;
    public int ResurrectUsedThisCombat;
    public int ManaSurgeUsedThisCombat;     // wis_b01 法力涌动 (2026-05-17)
    public int AssassinateUsedThisCombat;   // wis_b07 暗杀 (2026-05-17)
    public int HeadShotPendingTurns;        // wis_b02 爆头突袭：下次攻击必定暴击的剩余回合
    public int DeathblowFocusPendingTurns;  // wis_b09 死灵之锋：击杀后下次攻击 +20% 伤害的剩余回合
    public bool WeaponSwitchedThisTurn;     // sim AI：每回合最多切换 1 次武器（防 AP 抖动）

    // 临时 buff (sim 用): 每个字段是"剩余轮数"，0 表示无 buff
    public int BuffAttackBonusTurns;   // +N 命中
    public int BuffAttackBonusValue;   // 加成值
    public int BuffAcBonusTurns;
    public int BuffAcBonusValue;
    public int BuffTempHp;             // 临时 HP（受伤时优先扣临时）
    public int DebuffAttackPenaltyTurns; // 攻击 -N
    public int DebuffAttackPenaltyValue;

    public Vector2I GridPos;

    /// <summary>
    /// Optional reference to this unit's skill tree. Headless / sim path sets
    /// this from <c>SkillTreeAllocator.AllocateForUnit</c>; live game path
    /// keeps the tree on <c>Unit.SkillTree</c> (Frontend) and mirrors it here
    /// when entering combat. Pure rule code reads stat bonuses through this.
    /// </summary>
    public BladeHex.Strategic.CharacterSkillTree? SkillTree;

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
