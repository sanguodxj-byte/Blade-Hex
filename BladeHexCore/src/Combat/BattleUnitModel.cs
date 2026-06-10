// BattleUnitModel.cs
// 战斗单位数学模型 — Core 层桥接 UnitData 与 CombatStats
// 所有战斗数值计算均委托至 CombatStats（单一真相源）
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 战斗单位数学模型 — 纯逻辑层
/// 持有 UnitData 引用，通过 CombatStats 提供所有战斗属性查询接口
/// </summary>
public partial class BattleUnitModel
{
    public UnitData Data { get; }
    public UnitRuntimeState Runtime { get; }

    public BattleUnitModel(UnitData data)
    {
        Data = data;
        Runtime = data.Runtime;
    }

    // ====================
    // 武器槽位
    // ====================

    public bool UsingPrimaryWeapon => Runtime.UsingPrimaryWeapon;

    /// <summary>获取当前主手武器</summary>
    public ItemData? GetMainHand() => CombatStats.GetMainHand(Data, UsingPrimaryWeapon);

    /// <summary>获取当前副手物品</summary>
    public ItemData? GetOffHand() => CombatStats.GetOffHand(Data, UsingPrimaryWeapon);

    // ====================
    // 生命与行动点
    // ====================

    /// <summary>最大 HP = 基础HP + CON修正 × 等级 + 装备HP加成 + 饰品HP加成</summary>
    public int GetMaxHp() => CombatStats.GetMaxHp(Data);

    /// <summary>最大 AP = 基础AP + DEX修正 + (CON修正 / 2) - 护甲AP惩罚</summary>
    public int GetMaxAp() => CombatStats.GetMaxAp(Data);

    /// <summary>确保 AP 已初始化（回合开始时调用）</summary>
    public void EnsureApInitialized() => CombatStats.EnsureApInitialized(Data, Runtime);

    /// <summary>读取当前 AP（无副作用）</summary>
    public float GetAp() => CombatStats.GetAp(Runtime);

    // ====================
    // 暴击系统
    // ====================

    /// <summary>暴击阈值 = max(15, 20 - floor(sqrt(WIS / 2)))</summary>
    public int GetCritThreshold() => CombatStats.GetCritThreshold(Data);

    /// <summary>暴击受伤倍率 = max(0.2, 1.0 - floor(sqrt(WIS / 2)) * 0.1)</summary>
    public float GetCritDamageTakenMultiplier() => CombatStats.GetCritDamageTakenMultiplier(Data);

    // ====================
    // AC 与 DR
    // ====================

    /// <summary>
    /// 基础 AC = BaseAc + DEX修正(受MaxDexBonus限制) + Armor.AcBonus + Shield.AcBonus + floor(sqrt(totalDr))
    /// </summary>
    public int GetAc() => CombatStats.GetAc(Data, UsingPrimaryWeapon);

    /// <summary>
    /// 有效 AC = 基础 AC + 被动技能加成 + 防御姿态加值
    /// passiveAcBonus 由调用方从 Frontend 提取传入
    /// </summary>
    public int GetEffectiveAc(bool isDefending, int passiveAcBonus) =>
        CombatStats.GetEffectiveAc(Data, UsingPrimaryWeapon, isDefending, passiveAcBonus);

    /// <summary>当前 DR 值（不低于 0）</summary>
    public int GetDr() => CombatStats.GetDr(Data);

    /// <summary>DR 穿透阈值 = max(armorDrThreshold, naturalDrThreshold)</summary>
    public int GetDrThreshold() => CombatStats.GetDrThreshold(Data);

    /// <summary>最大 DR = NaturalDr + ArmorDr + ShieldDr</summary>
    public int GetMaxDr() => CombatStats.GetMaxDr(Data, UsingPrimaryWeapon);

    /// <summary>初始化 DR（战斗开始时调用）</summary>
    public void InitDr() => CombatStats.InitDr(Data);

    /// <summary>承受 DR 伤害，返回实际扣除的 DR 值</summary>
    public int TakeDrDamage(int amount) => CombatStats.TakeDrDamage(Data, amount);

    /// <summary>所有防具的剩余装甲值总和</summary>
    public int GetTotalCurrentArmorPoints() => CombatStats.GetTotalCurrentArmorPoints(Data);

    // ====================
    // 攻击与伤害
    // ====================

    /// <summary>攻击加值 = 专精加值 + 武器命中加成</summary>
    public int GetAttackBonus() => CombatStats.GetAttackBonus(Data, UsingPrimaryWeapon);

    /// <summary>
    /// 掷骰伤害
    /// 返回 Dictionary: dice, multiplier, str_bonus_pct, mastery_bonus_pct, total, text, weapon_subtype
    /// </summary>
    public Godot.Collections.Dictionary RollDamage() => CombatStats.RollDamage(Data, UsingPrimaryWeapon);

    // ====================
    // 移动
    // ====================

    /// <summary>移动范围 = 基础 + 装备加成 + 饰品加成 + 坐骑加成</summary>
    public int GetMoveRange() => CombatStats.GetMoveRange(Data);

    // ====================
    // 护甲 AP 惩罚
    // ====================

    /// <summary>护甲 AP 惩罚 = armor + shield + helmet 的 ApPenalty 总和</summary>
    public int GetArmorApPenalty() => CombatStats.GetArmorApPenalty(Data);

    // ====================
    // 静态工具
    // ====================

    /// <summary>属性修正 = floor(sqrt(score / 2))</summary>
    public static int GetStatModifier(int score) => CombatStats.GetStatModifier(score);
}

// ============================================================================
// Runtime State API — 统一封装 UnitRuntimeState + UnitData 运行时字段
// 所有 Frontend→Core 的运行时数据写入必须通过此处，禁止直接写 Data.Runtime.*
// ============================================================================

public partial class BattleUnitModel
{
    // ====================
    // Turn Flags (每回合重置)
    // ====================

    public bool HasMoved { get => Runtime.HasMoved; set => Runtime.HasMoved = value; }
    public bool HasActed { get => Runtime.HasActed; set => Runtime.HasActed = value; }
    public bool NonSpellSkillUsedThisTurn { get => Runtime.NonSpellSkillUsedThisTurn; set => Runtime.NonSpellSkillUsedThisTurn = value; }
    public bool TimeWarpUsedThisTurn { get => Runtime.TimeWarpUsedThisTurn; set => Runtime.TimeWarpUsedThisTurn = value; }
    public bool AooUsedThisTurn { get => Runtime.AooUsedThisTurn; set => Runtime.AooUsedThisTurn = value; }
    public bool IsRangedWeaponLoaded { get => Runtime.IsRangedWeaponLoaded; set => Runtime.IsRangedWeaponLoaded = value; }
    public int ExtraActionsThisTurn { get => Runtime.ExtraActionsThisTurn; set => Runtime.ExtraActionsThisTurn = value; }
    public bool WeaponSwitchedThisTurn { get => Runtime.WeaponSwitchedThisTurn; set => Runtime.WeaponSwitchedThisTurn = value; }

    // ====================
    // Combat Flags (本场战斗一次性标记)
    // ====================

    public int LifeShieldUsedThisCombat { get => Runtime.LifeShieldUsedThisCombat; set => Runtime.LifeShieldUsedThisCombat = value; }
    public int LifeCircleUsedThisCombat { get => Runtime.LifeCircleUsedThisCombat; set => Runtime.LifeCircleUsedThisCombat = value; }
    public int LastStandUsedThisCombat { get => Runtime.LastStandUsedThisCombat; set => Runtime.LastStandUsedThisCombat = value; }
    public int HeroicCallUsedThisCombat { get => Runtime.HeroicCallUsedThisCombat; set => Runtime.HeroicCallUsedThisCombat = value; }
    public int ResurrectUsedThisCombat { get => Runtime.ResurrectUsedThisCombat; set => Runtime.ResurrectUsedThisCombat = value; }
    public int ManaSurgeUsedThisCombat { get => Runtime.ManaSurgeUsedThisCombat; set => Runtime.ManaSurgeUsedThisCombat = value; }
    public int AssassinateUsedThisCombat { get => Runtime.AssassinateUsedThisCombat; set => Runtime.AssassinateUsedThisCombat = value; }
    public int OldTimerTriggeredThisCombat { get => Runtime.OldTimerTriggeredThisCombat; set => Runtime.OldTimerTriggeredThisCombat = value; }

    // ====================
    // 法力 (Mana)
    // ====================

    public int CurrentMana
    {
        get => Data.CurrentMana;
        set
        {
            int clamped = System.Math.Max(0, value);
            Data.CurrentMana = clamped;
            Runtime.CurrentMana = clamped;
        }
    }

    /// <summary>增加法力（不低于 0）</summary>
    public void AddMana(int amount)
    {
        CurrentMana += amount;
    }

    /// <summary>消耗法力，不足时返回 false 并拒绝消耗</summary>
    public bool SpendMana(int amount)
    {
        if (CurrentMana < amount) return false;
        CurrentMana -= amount;
        return true;
    }

    // ====================
    // 朝向 (Facing) / 防御姿态
    // ====================

    public int Facing { get => Runtime.Facing; set => Runtime.Facing = value; }

    /// <summary>当前行动点（float）</summary>
    public float CurrentAp { get => Runtime.CurrentAp; set => Runtime.CurrentAp = value; }
    public void SetFacing(int direction) => Runtime.Facing = direction;

    public bool IsDefending { get => Runtime.IsDefending; set => Runtime.IsDefending = value; }

    // ====================
    // 技能树引用
    // ====================

    public BladeHex.Strategic.CharacterSkillTree? SkillTree
    {
        get => Runtime.SkillTree;
        set => Runtime.SkillTree = value;
    }

    // ====================
    // Buff 操作
    // ====================

    /// <summary>活跃 Buff 列表（只读，Frontend 禁止直接修改）</summary>
    public System.Collections.Generic.IReadOnlyList<BladeHex.Combat.Buff.BuffInstance> ActiveBuffs
        => Runtime.ActiveBuffs;

    public void AddBuff(BladeHex.Combat.Buff.BuffInstance buff) => Runtime.ActiveBuffs.Add(buff);

    public bool RemoveBuff(System.Predicate<BladeHex.Combat.Buff.BuffInstance> predicate)
        => Runtime.ActiveBuffs.RemoveAll(predicate) > 0;

    /// <summary>按 ID 移除 Buff</summary>
    public bool RemoveBuffById(string buffId)
        => Runtime.ActiveBuffs.RemoveAll(b => b.Id == buffId) > 0;

    /// <summary>批量移除 Buff</summary>
    public int RemoveAllBuffs(System.Predicate<BladeHex.Combat.Buff.BuffInstance> predicate)
        => Runtime.ActiveBuffs.RemoveAll(predicate);

    /// <summary>清除全部 Buff</summary>
    public void ClearBuffs() => Runtime.ActiveBuffs.Clear();

    /// <summary>活跃 Buff 数量</summary>
    public int BuffCount => Runtime.ActiveBuffs.Count;

    /// <summary>是否存在指定 ID 的 Buff</summary>
    public bool HasBuff(string buffId)
        => System.Linq.Enumerable.Any(Runtime.ActiveBuffs, b => b.Id == buffId);

    /// <summary>查找指定 ID 的 Buff</summary>
    public BladeHex.Combat.Buff.BuffInstance? FindBuff(string buffId)
        => System.Linq.Enumerable.FirstOrDefault(Runtime.ActiveBuffs, b => b.Id == buffId);

    /// <summary>查找符合条件的 Buff 列表</summary>
    public System.Collections.Generic.List<BladeHex.Combat.Buff.BuffInstance> FindAllBuffs(
        System.Predicate<BladeHex.Combat.Buff.BuffInstance> predicate)
        => Runtime.ActiveBuffs.FindAll(predicate);

    public void IncrementBuffStacks(string buffId)
        => Buff.BuffSystem.IncrementStacks(Data, buffId);

    public void SetBuffStacks(string buffId, int count)
        => Buff.BuffSystem.SetStacks(Data, buffId, count);

    // ====================
    // StatusEffect 操作
    // ====================

    /// <summary>活跃状态效果列表（只读，Frontend 禁止直接修改）</summary>
    public System.Collections.Generic.IReadOnlyList<StatusEffectInstance> ActiveStatusEffects
        => Runtime.ActiveStatusEffects;

    public void AddStatusEffect(StatusEffectInstance effect) => Runtime.ActiveStatusEffects.Add(effect);

    public bool RemoveStatusEffect(string id)
        => Runtime.ActiveStatusEffects.RemoveAll(e => e.Id == id) > 0;

    /// <summary>批量移除状态效果</summary>
    public int RemoveAllStatusEffects(System.Predicate<StatusEffectInstance> predicate)
        => Runtime.ActiveStatusEffects.RemoveAll(predicate);

    /// <summary>清除全部状态效果</summary>
    public void ClearStatusEffects() => Runtime.ActiveStatusEffects.Clear();

    /// <summary>活跃状态效果数量</summary>
    public int StatusEffectCount => Runtime.ActiveStatusEffects.Count;

    /// <summary>是否存在指定 ID 的状态效果</summary>
    public bool HasStatusEffect(string id)
        => System.Linq.Enumerable.Any(Runtime.ActiveStatusEffects, e => e.Id == id);

    /// <summary>查找指定 ID 的状态效果</summary>
    public StatusEffectInstance? FindStatusEffect(string id)
        => System.Linq.Enumerable.FirstOrDefault(Runtime.ActiveStatusEffects, e => e.Id == id);

    /// <summary>查找符合条件的状态效果列表</summary>
    public System.Collections.Generic.List<StatusEffectInstance> FindAllStatusEffects(
        System.Predicate<StatusEffectInstance> predicate)
        => Runtime.ActiveStatusEffects.FindAll(predicate);

    // ====================
    // 运行时状态生命周期
    // ====================

    // ====================
    // Data.Set() 迁移属性 — 原通过 Variant 属性系统写入的运行时标记
    // ====================

    public int ArcaneResonanceStacks { get => Runtime.ArcaneResonanceStacks; set => Runtime.ArcaneResonanceStacks = value; }
    public long VengeanceTargetId { get => Runtime.VengeanceTargetId; set => Runtime.VengeanceTargetId = value; }
    public bool SoulGuardianUsed { get => Runtime.SoulGuardianUsed; set => Runtime.SoulGuardianUsed = value; }
    public bool FortifyActive { get => Runtime.FortifyActive; set => Runtime.FortifyActive = value; }
    public bool ImmortalBodyUsed { get => Runtime.ImmortalBodyUsed; set => Runtime.ImmortalBodyUsed = value; }
    public bool SpellReflectUsedThisTurn { get => Runtime.SpellReflectUsedThisTurn; set => Runtime.SpellReflectUsedThisTurn = value; }
    public bool FateEyeUsedThisTurn { get => Runtime.FateEyeUsedThisTurn; set => Runtime.FateEyeUsedThisTurn = value; }
    public bool LightningReflexFirstAttackUsed { get => Runtime.LightningReflexFirstAttackUsed; set => Runtime.LightningReflexFirstAttackUsed = value; }
    public bool IsWounded { get => Runtime.IsWounded; set => Runtime.IsWounded = value; }

    /// <summary>清理运行时状态（战斗结束时调用）</summary>
    public void ClearRuntimeState() => Data.ClearRuntimeState();
}


// ============================================================================
// ApplyDamage —— Core 层伤害解算（T-104）
// 四条路径统一入口：CombatResolver / SkillEffectExecutor /
//                   ConsumableManager / EnvironmentEventSystem
// ============================================================================

public partial class BattleUnitModel
{
    /// <summary>
    /// 当前 HP（由 View 层的 Unit.CurrentHp 镜像；Core 侧用 Runtime 存储更合适，
    /// 为了迁移期零破坏，保留 setter 并在 T-303 完成后清理 Unit.CurrentHp）
    /// </summary>
    public int CurrentHp { get; set; }

    /// <summary>
    /// 伤害解算 —— 单一入口
    /// </summary>
    /// <param name="source">伤害来源标记（仅用于日志/归因）</param>
    /// <param name="amount">原始伤害</param>
    /// <param name="damageType">武器伤害类型（Slash/Pierce/Crush/Magic/...）</param>
    /// <param name="naturalRoll">自然 d20 骰子（20 = 自动穿透；简化路径可传 20）</param>
    /// <param name="weaponWeight">武器重量类别（影响穿透系数分桶：Light/Medium → 高杀伤，Heavy → 高破甲）</param>
    /// <param name="attackerMastery">攻击者武器精通（为 null 则不归因 XP）</param>
    /// <param name="weaponSubtype">打击所用的武器子类型（归因精通 XP）</param>
    /// <param name="strPenBonus">
    /// 攻击者力量穿透加成 = floor(sqrt(STR/4))。由调用方计算后传入避免循环依赖。
    /// </param>
    /// <param name="mediumLv5Mastery">
    /// 攻击者武器为 Medium 重量且精通 ≥ Lv.5 (v0.6 6.9)。装甲伤害 ×1.2，不影响 HP / 盾牌。
    /// </param>
    public DamageResult ApplyDamage(
        DamageSource source,
        int amount,
        WeaponData.DamageType damageType = WeaponData.DamageType.Slash,
        int naturalRoll = 20,
        WeaponData.WeightCategory weaponWeight = WeaponData.WeightCategory.Medium,
        WeaponMastery? attackerMastery = null,
        WeaponData.WeaponSubtype weaponSubtype = WeaponData.WeaponSubtype.Unarmed,
        int strPenBonus = 0,
        bool mediumLv5Mastery = false,
        bool isRanged = false,
        bool allowDamageRedirect = true)
    {
        return DamageResolutionPipeline.Resolve(this, new DamageResolutionInput
        {
            Amount = amount,
            DamageType = damageType,
            NaturalRoll = naturalRoll,
            WeaponWeight = weaponWeight,
            AttackerMastery = attackerMastery,
            WeaponSubtype = weaponSubtype,
            StrPenBonus = strPenBonus,
            MediumLv5Mastery = mediumLv5Mastery,
            IsRanged = isRanged,
            AllowDamageRedirect = allowDamageRedirect,
        });
    }


}
