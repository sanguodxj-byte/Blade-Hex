// BattleUnitModel.cs
// 战斗单位数学模型 — Core 层桥接 UnitData 与 CombatStats
// 所有战斗数值计算均委托至 CombatStats（单一真相源）
using Godot;
using BladeHex.Data;

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
        Runtime = new UnitRuntimeState();
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
    /// 有效 AC = 基础 AC + 被动技能加成 + 防御姿态加值 + 士气 AC 修正
    /// passiveAcBonus 和 moraleAcModifier 由调用方从 Frontend 提取传入
    /// </summary>
    public int GetEffectiveAc(bool isDefending, int passiveAcBonus, int moraleAcModifier) =>
        CombatStats.GetEffectiveAc(Data, UsingPrimaryWeapon, isDefending, passiveAcBonus, moraleAcModifier);

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
    public DamageResult ApplyDamage(
        DamageSource source,
        int amount,
        WeaponData.DamageType damageType = WeaponData.DamageType.Slash,
        int naturalRoll = 20,
        WeaponData.WeightCategory weaponWeight = WeaponData.WeightCategory.Medium,
        WeaponMastery? attackerMastery = null,
        WeaponData.WeaponSubtype weaponSubtype = WeaponData.WeaponSubtype.Unarmed)
    {
        if (amount <= 0)
            return new DamageResult { RemainingHp = CurrentHp };

        // --- 穿透判定 ---
        bool noArmor = Data.Armor == null;
        int armorDrThreshold = Data.Armor?.DrThreshold ?? 0;
        bool isPenetrated = noArmor || (naturalRoll >= armorDrThreshold) || (naturalRoll == 20);

        // --- HP / DR 伤害分配（查 DamagePenetrationTable）---
        int hpDamage = 0;
        int drDamage = 0;

        if (noArmor)
        {
            hpDamage = amount;
        }
        else
        {
            var coef = DamagePenetrationTable.Lookup(damageType, weaponWeight);

            if (isPenetrated)
            {
                hpDamage = System.Math.Max(1, (int)(amount * coef.HpRatioPenetrated));
                drDamage = coef.DrRatioPenetrated > 0f
                    ? System.Math.Max(1, (int)(amount * coef.DrRatioPenetrated))
                    : 0;
            }
            else
            {
                hpDamage = coef.HpRatioBlocked > 0f
                    ? System.Math.Max(1, (int)(amount * coef.HpRatioBlocked))
                    : 0;
                drDamage = coef.DrRatioBlocked > 0f
                    ? System.Math.Max(1, (int)(amount * coef.DrRatioBlocked))
                    : 0;
            }

            // Crush 特殊规则：DR 耗尽时穿透伤害 +50%
            if (isPenetrated && damageType == WeaponData.DamageType.Crush && Data.CurrentDr <= 0)
            {
                hpDamage = (int)(hpDamage * 1.5f);
            }
        }

        // --- 应用 DR 损耗（战斗 DR + 护甲耐久）---
        bool armorBroken = false;
        if (drDamage > 0)
        {
            CombatStats.TakeDrDamage(Data, drDamage);
            if (Data.Armor != null)
            {
                Data.Armor.CurrentArmorPoints = System.Math.Max(0, Data.Armor.CurrentArmorPoints - drDamage);
                if (Data.Armor.CurrentArmorPoints <= 0)
                {
                    armorBroken = true;
                    Data.Armor = null;
                }
            }
        }

        // --- 应用 HP 伤害 ---
        if (hpDamage > 0)
            CurrentHp = System.Math.Max(0, CurrentHp - hpDamage);

        // --- 武器精通 XP 归因 ---
        bool leveledUp = false;
        int newLevel = 0;
        int totalDealt = hpDamage + drDamage;
        if (attackerMastery != null && totalDealt > 0 &&
            weaponSubtype != WeaponData.WeaponSubtype.Unarmed)
        {
            leveledUp = attackerMastery.AddDamageXp(weaponSubtype, totalDealt);
            if (leveledUp)
                newLevel = attackerMastery.GetLevelBySubtype(weaponSubtype);
        }

        // --- OnTakeDamage 钩子：触发防御方装备能力（如 thorns）---
        int reflectDamage = 0;
        if (hpDamage > 0)
        {
            var ctx = new BladeHex.Combat.Abilities.TakeDamageContext
            {
                Attacker = null!,
                Defender = this,
                HpDamageTaken = hpDamage,
                DrDamageTaken = drDamage,
            };
            foreach (var ab in BladeHex.Combat.Abilities.UnitAbilities.GetAll(Data))
                ab.OnTakeDamage(ctx);
            reflectDamage = ctx.ReflectDamage;
        }

        return new DamageResult
        {
            IsPenetrated = isPenetrated,
            HpDamage = hpDamage,
            DrDamage = drDamage,
            ArmorBroken = armorBroken,
            KilledUnit = CurrentHp <= 0,
            RemainingHp = CurrentHp,
            MasteryLeveledUp = leveledUp,
            MasteryNewLevel = newLevel,
            ReflectDamageToAttacker = reflectDamage,
        };
    }
}
