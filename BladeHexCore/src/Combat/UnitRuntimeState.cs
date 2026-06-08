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
    public int OldTimerTriggeredThisCombat; // 老兵-临危不乱：HP<50% 首次触发标记 (2026-05-27)
    public int HeadShotPendingTurns;        // wis_b02 爆头突袭：下次攻击必定暴击的剩余回合
    public int DeathblowFocusPendingTurns;  // wis_b09 死灵之锋：击杀后下次攻击 +20% 伤害的剩余回合
    public int SkillTreeCritMeleeDamagePendingTurns; // str_p06: 暴击后下次近战伤害 +5%
    public int SkillTreeKillCritPendingTurns;        // wis_p08: 击杀后下次攻击暴击率 +5%
    public bool WeaponSwitchedThisTurn;     // sim AI：每回合最多切换 1 次武器（防 AP 抖动）

    // ====================
    // v1 职业技能运行时状态
    // ====================

    // -- 移动类 --
    public int CareerMovedCellsThisCombat;      // 本场战斗累计移动格数 (荒原之心、风语者)
    public int CareerMovedCellsThisTurn;         // 本回合移动格数 (重战士、征服者、战争之风)
    public int CareerSameHeightMoveChain;        // 连续同高度移动计数 (荒原之心)
    public int CareerFreeMoveCellsRemaining;     // 奔袭/免费移动剩余格数 (游骑兵)
    public int CareerFreeMoveNoAooCellsRemaining; // 免借机移动剩余格数

    // -- 攻击/暴击类 --
    public int CareerWindwalkerCritBonus;        // 风语者本场累计暴击率加成(格数)
    public bool CareerNextAttackGuaranteedCrit;   // 下一次攻击必定暴击标记 (战争之风)
    public float CareerNextAttackDamageMultiplier; // 下一次攻击伤害倍率
    public float CareerNextMeleeDamageMultiplier;  // 下一次近战伤害倍率
    public int CareerNextAttackHitBonus;         // 下一次攻击命中加成

    // -- 法术类 --
    public bool CareerNextSpellFreeAp;           // 下一次法术不消耗 AP (鏖战骑士)
    public bool CareerNextSpellFreeMana;         // 下一次法术不消耗法力 (秘院贤师)
    public float CareerSpellDamageBonusThisTurn; // 本回合法术伤害累计加成 (焰风之怒)
    public bool CareerArcaneWarFreeSpellPending; // 近战命中后等待免费法术 (鏖战骑士)

    // -- 防御/自身类 --
    public int CareerIllusionStacks;             // 幻术师幻影层数
    public bool CareerAntiMagicActive;           // 敌法师本场法术免疫激活
    public int CareerWarchiefDamageBonusTurns;   // 荒原之心狂暴剩余回合
    public bool CareerWarchiefDamageBonusTriggered; // 荒原之心: 是否已触发伤害加成 (防止重复触发)
    public bool CareerWeaponSwitchAndRangedFreePending; // 钢弦骑士近战后免费切换+远程
    public bool CareerGuardianFirstHitUsedThisTurn; // 守卫: 本回合是否已受到首次攻击
    public bool CareerBladeDancerExtraAttackUsedThisTurn; // 剑舞者: 本回合是否已触发额外攻击
    public int CareerGrandmasterStacks;          // 武圣: 近战命中后累计层数 (最多10层)
    public int CareerSpellweaverMeleeStacks;     // 魔武者: 近战→法术循环近战命中层数
    public int CareerSpellweaverSpellStacks;     // 魔武者: 法术→近战循环法术命中层数
    public int CareerManaShieldUsedThisTurn;     // 战法师: 本回合已用魔力护盾吸收量

    // -- 唤星者 --
    public bool CareerStarcallerSpellUsedThisTurn;   // 本回合是否已施放过法术

    // -- 万象 --
    public int CareerParagonCostsMask;           // 位域: bit0=无法移动, bit1=无法施法, bit2=无法攻击
    public bool CareerParagonExhausted;          // 三个代价抽完, 本场不可再用

    // -- 五属性主动技能临时状态 --
    public int CareerMountainThroneTurns;        // 山岳之王: 不动如山剩余回合 (0=未激活)
    public bool CareerWrathArmorPenActive;       // 荒芜化身: 本场战斗护甲穿透激活
    public ulong CareerLoneShadowLockedTarget;   // 孤星之影: 锁定目标的 InstanceId (0=未锁定)
    public int CareerIronEdictPendingCount;      // 铁血之令: 友军待触发的未命中转暴击次数
    public ulong CareerArcaneArcherLastTargetId; // 秘射手: 上一次攻击目标 InstanceId (0=无)
    public int CareerArcaneArcherStacks;         // 秘射手: 对当前目标连续命中次数 (最大3)
    public bool CareerConquerorAoeMeleePending;  // 征服者: 移动≥5后下一次近战为AOE
    public bool CareerShadowShroudActive;        // 影匿者: 本回合阴影斗篷激活

    // ====================
    // Data.Set() 迁移字段 — 原通过 Variant 属性系统写入的运行时标记
    // ====================
    public int ArcaneResonanceStacks;
    public long VengeanceTargetId;
    public bool SoulGuardianUsed;
    public bool FortifyActive;
    public bool ImmortalBodyUsed;
    public bool SpellReflectUsedThisTurn;
    public bool FateEyeUsedThisTurn;
    public bool LightningReflexFirstAttackUsed;
    public bool IsWounded;
    public bool KeystoneUndyingBodyUsed;
    public int KeystoneRecentCritTurns;

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

    /// <summary>回合开始时重置回合级字段</summary>
    public void ResetForTurnStart()
    {
        HasMoved = false;
        HasActed = false;
        NonSpellSkillUsedThisTurn = false;
        ExtraActionsThisTurn = 0;
        TimeWarpUsedThisTurn = false;
        AooUsedThisTurn = false;
        IsDefending = false;
        IsRangedWeaponLoaded = true;

        // v1 职业回合级重置
        CareerMovedCellsThisTurn = 0;
        CareerSpellDamageBonusThisTurn = 0f;
        CareerNextAttackGuaranteedCrit = false;
        CareerNextAttackDamageMultiplier = 0f;
        CareerNextMeleeDamageMultiplier = 0f;
        CareerNextAttackHitBonus = 0;
        CareerNextSpellFreeAp = false;
        CareerNextSpellFreeMana = false;
        CareerArcaneWarFreeSpellPending = false;
        CareerWeaponSwitchAndRangedFreePending = false;
        CareerGuardianFirstHitUsedThisTurn = false;
        CareerBladeDancerExtraAttackUsedThisTurn = false;
        CareerManaShieldUsedThisTurn = 0;
        CareerStarcallerSpellUsedThisTurn = false;
    }

    /// <summary>战斗开始时重置全场级字段</summary>
    public void ResetForCombatStart()
    {
        // v1 职业战斗级重置
        CareerMovedCellsThisCombat = 0;
        CareerSameHeightMoveChain = 0;
        CareerFreeMoveCellsRemaining = 0;
        CareerFreeMoveNoAooCellsRemaining = 0;
        CareerWindwalkerCritBonus = 0;
        CareerWarchiefDamageBonusTurns = 0;
        CareerWarchiefDamageBonusTriggered = false;
        CareerIllusionStacks = 0;
        CareerGrandmasterStacks = 0;
        CareerSpellweaverMeleeStacks = 0;
        CareerSpellweaverSpellStacks = 0;
        CareerAntiMagicActive = false;
        CareerParagonCostsMask = 0;
        CareerParagonExhausted = false;
        CareerMountainThroneTurns = 0;
        CareerWrathArmorPenActive = false;
        CareerLoneShadowLockedTarget = 0;
        CareerIronEdictPendingCount = 0;
        CareerArcaneArcherLastTargetId = 0;
        CareerArcaneArcherStacks = 0;
        CareerConquerorAoeMeleePending = false;
        CareerShadowShroudActive = false;
        KeystoneUndyingBodyUsed = false;
        KeystoneRecentCritTurns = 0;
        SkillTreeCritMeleeDamagePendingTurns = 0;
        SkillTreeKillCritPendingTurns = 0;

        ResetForTurnStart();
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
