// CombatStats.cs
// 战斗属性计算 — 纯静态类，所有战斗数值公式的唯一真相源
// 从 Unit.cs (Frontend) 中提取全部计算逻辑，无任何 Godot Node 依赖
// 对应策划案 03-战术战斗系统
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 战斗属性计算静态类
/// 所有战斗数值公式的 SINGLE SOURCE OF TRUTH
/// 不继承任何 Godot 类型，不引用任何 Frontend 命名空间
/// </summary>
public static class CombatStats
{
    // ========================================
    // 属性修正
    // ========================================

    /// <summary>属性修正 = floor(sqrt(score / 2))</summary>
    public static int GetStatModifier(int score) =>
        RPGRuleEngine.GetStatModifier(score);

    // ========================================
    // HP 计算
    // ========================================

    /// <summary>
    /// 最大 HP = 基础HP + CON修正 × 等级 + 装备HP加成 + 饰品HP加成 + 能力百分比加成
    /// </summary>
    public static int GetMaxHp(UnitData data)
    {
        if (data == null) return 1;
        int hp = RPGRuleEngine.CalculateMaxHp(data.BaseMaxHp, data.Con, data.Level);
        hp += data.GetEquipmentHpBonus();
        hp += data.AccessoryHpBonus;

        // 装备能力组件：HP 百分比加成（如 extra_hp_percent）
        float hpMultBonus = BladeHex.Combat.Abilities.UnitAbilities.GetTotalMaxHpMultiplierBonus(data);
        if (hpMultBonus > 0f)
            hp = (int)(hp * (1f + hpMultBonus));

        return Math.Max(1, hp);
    }

    // ========================================
    // AP 计算
    // ========================================

    /// <summary>
    /// 确保 AP 已初始化 — 回合开始时调用，若 AP 未初始化则设为最大值
    /// 注意：此方法有副作用（可能修改 runtime.CurrentAp）
    /// </summary>
    public static void EnsureApInitialized(UnitData data, UnitRuntimeState runtime)
    {
        if (runtime.CurrentAp <= 0 && !runtime.HasMoved && !runtime.HasActed)
            runtime.CurrentAp = GetMaxAp(data);
    }

    /// <summary>读取当前 AP（无副作用）</summary>
    public static float GetAp(UnitRuntimeState runtime) => runtime.CurrentAp;

    /// <summary>最大 AP = 基础AP + DEX修正 + (CON修正 / 2) - 护甲AP惩罚</summary>
    public static int GetMaxAp(UnitData data)
    {
        if (data == null) return 12;
        int maxAp = RPGRuleEngine.CalculateMaxAp(data.BaseAp, data.Dex, data.Con);
        return Math.Max(1, maxAp - GetArmorApPenalty(data));
    }

    /// <summary>护甲 AP 惩罚 = armor + shield 的 ApPenalty 总和（v0.6: 头盔不算入 AP 惩罚）</summary>
    public static int GetArmorApPenalty(UnitData data)
    {
        if (data == null) return 0;
        int penalty = 0;
        if (data.Armor != null) penalty += data.Armor.ApPenalty;
        if (data.Shield != null) penalty += data.Shield.ApPenalty;
        // 头盔不再扣 AP — 重盔已通过 AC/MaxDex 限制成本
        return penalty;
    }

    /// <summary>
    /// 最大 Mana (v0.6 10.0): 10 + INT + floor(Level/2) + floor(WIS/4) + NodeManaMax
    /// 2026-05-17 修订：WIS 提供 Mana 上限加成 floor(WIS/4)。
    /// 节点 mana_max 加成自动累入（CharacterSkillTree.GetManaMaxBonus）。
    /// </summary>
    public static int GetMaxMana(UnitData data)
    {
        if (data == null) return 0;
        int baseMana = 10 + data.Intel + data.Level / 2 + data.Wis / 4;
        int nodeMana = data.Runtime?.SkillTree?.GetManaMaxBonus() ?? 0;
        return Math.Max(0, baseMana + nodeMana);
    }

    /// <summary>
    /// Mana 战斗内每回合恢复量 (v0.6 10.0 修订): floor(WIS/8) + floor(INT/12) + NodeManaRegen
    /// 让 WIS 系成为续航型，INT 主属性也提供少量 mana regen 让法师能持久输出。
    /// 节点 mana_regen 加成自动累入。
    /// </summary>
    public static int GetManaRegen(UnitData data)
    {
        if (data == null) return 0;
        int nodeRegen = data.Runtime?.SkillTree?.GetManaRegenBonus() ?? 0;
        return data.Wis / 8 + data.Intel / 12 + nodeRegen;
    }

    /// <summary>
    /// 角色是否满足施法装备限制 (v0.6 10.0):
    /// 1) 不能装备盾牌
    /// 2) 只能穿戴 Cloth 类护甲（DR ≤ 3 的 Light，即布衣 / 法师长袍）
    /// 3) 主手必须装备法术媒介（IsCatalyst）
    /// </summary>
    public static bool CanCastSpells(UnitData data)
    {
        if (data == null) return false;
        if (data.Shield != null) return false;
        if (data.Armor != null
            && (data.Armor.armorType != ArmorData.ArmorType.Light || data.Armor.DrThreshold > 3))
            return false;
        var mainHand = data.PrimaryMainHand;
        if (mainHand == null) return false;
        if (mainHand is not WeaponData w) return false;
        return w.IsCatalyst;
    }

    // ========================================
    // 暴击系统
    // ========================================

    /// <summary>
    /// 暴击阈值 (v0.6): WISCritTier = floor(sqrt(max(0, WIS-14) / 4))
    /// CritThreshold = 20 - WISCritTier
    /// </summary>
    public static int GetCritThreshold(UnitData data)
    {
        if (data == null) return 20;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, data.Wis - 14) / 4.0));
        return Math.Max(14, 20 - wisCritTier);
    }

    /// <summary>
    /// 暴击倍率 (v0.6): CritMultiplier = 2.0 + WISCritTier × 0.1
    /// 重型武器 Lv.5+ 精通: 最终暴击倍率 ×1.2 (v0.6 6.9)
    /// </summary>
    public static float GetCritMultiplier(UnitData data)
    {
        if (data == null) return 2.0f;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, data.Wis - 14) / 4.0));
        float baseMultiplier = 2.0f + wisCritTier * 0.1f;

        // v0.6 6.9 重型武器 Lv.5+ → ×1.2
        var weapon = GetMainHand(data, data.Runtime.UsingPrimaryWeapon) as WeaponData;
        if (weapon != null && weapon.Weight == WeaponData.WeightCategory.Heavy)
        {
            int masteryLevel = data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            if (masteryLevel >= 5) baseMultiplier *= 1.2f;
        }
        return baseMultiplier;
    }

    /// <summary>暴击受伤倍率 = max(0.2, 1.0 - WISCritTier * 0.1)</summary>
    public static float GetCritDamageTakenMultiplier(UnitData data)
    {
        if (data == null) return 1.0f;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, data.Wis - 14) / 4.0));
        return Math.Max(0.2f, 1.0f - wisCritTier * 0.1f);
    }

    // ========================================
    // AC 与 DR
    // ========================================

    /// <summary>
    /// 基础 AC (v0.6) = 10 + DEX修正(受MaxDexBonus限制) + floor(sqrt(ArmorDR)) + floor(sqrt(ShieldDR))
    /// 护甲不再有独立AcBonus，AC完全来自sqrt(DR)
    /// </summary>
    public static int GetAc(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 8;
        int ac = data.BaseAc;

        // DEX 修正（受护甲 MaxDexBonus 限制）
        int dexAc = GetStatModifier(data.Dex);
        if (data.Armor != null && data.Armor.MaxDexBonus < 99 && data.Armor.CurrentArmorPoints > 0)
            dexAc = Math.Min(dexAc, data.Armor.MaxDexBonus);

        // 护甲 AC = floor(sqrt(ArmorDR))（装甲损毁后失效）
        int armorDrAc = 0;
        if (data.Armor != null && data.Armor.CurrentArmorPoints > 0)
            armorDrAc = (int)Mathf.Floor(Mathf.Sqrt(data.Armor.DrThreshold));

        // 盾牌 AC = floor(sqrt(ShieldDR))（盾牌损毁后失效）
        int shieldDrAc = 0;
        var offHand = GetOffHand(data, usingPrimaryWeapon);
        if (offHand is ArmorData shield && shield.armorType == ArmorData.ArmorType.Shield
            && shield.CurrentArmorPoints > 0)
            shieldDrAc = (int)Mathf.Floor(Mathf.Sqrt(shield.DrThreshold));

        return ac + dexAc + armorDrAc + shieldDrAc + GetBuffStatBonus(data, "ac");
    }

    /// <summary>
    /// 有效 AC = 基础 AC + 被动技能加成 + 防御姿态加值 + 士气 AC 修正
    /// passiveAcBonus 和 moraleAcModifier 由调用方从 Frontend 提取传入
    /// </summary>
    public static int GetEffectiveAc(UnitData data, bool usingPrimaryWeapon, bool isDefending, int passiveAcBonus, int moraleAcModifier)
    {
        int ac = GetAc(data, usingPrimaryWeapon);
        ac += passiveAcBonus;
        if (isDefending) ac += 2;
        ac += moraleAcModifier;
        return ac;
    }

    /// <summary>所有防具的剩余装甲值总和</summary>
    public static int GetTotalCurrentArmorPoints(UnitData data)
    {
        int total = 0;
        if (data?.Armor != null) total += data.Armor.CurrentArmorPoints;
        if (data?.Shield != null) total += data.Shield.CurrentArmorPoints;
        if (data?.Helmet != null) total += data.Helmet.CurrentArmorPoints;
        return total;
    }

    /// <summary>当前 DR 值（不低于 0）</summary>
    public static int GetDr(UnitData data) =>
        data != null ? Math.Max(0, data.CurrentDr) : 0;

    /// <summary>DR 穿透阈值 = max(armorDrThreshold, naturalDrThreshold)</summary>
    public static int GetDrThreshold(UnitData data)
    {
        if (data == null || data.CurrentDr <= 0) return 0;
        int threshold = 0;
        if (data.Armor != null) threshold = Math.Max(threshold, data.Armor.DrThreshold);
        if (data.NaturalDrThreshold > 0) threshold = Math.Max(threshold, data.NaturalDrThreshold);
        return threshold;
    }

    /// <summary>最大 DR = NaturalDr + ArmorDr + ShieldDr</summary>
    public static int GetMaxDr(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 0;
        int dr = data.NaturalDr;
        if (data.Armor != null) dr += data.Armor.DrThreshold;

        var offHand = GetOffHand(data, usingPrimaryWeapon);
        if (offHand is ArmorData shield && shield.armorType == ArmorData.ArmorType.Shield)
            dr += shield.DrThreshold;

        return dr;
    }

    /// <summary>初始化 DR（战斗开始时调用）</summary>
    public static void InitDr(UnitData data, bool usingPrimaryWeapon = true)
    {
        if (data != null)
        {
            data.MaxDr = GetMaxDr(data, usingPrimaryWeapon);
            data.CurrentDr = data.MaxDr;
        }
    }

    /// <summary>承受 DR 伤害，返回实际扣除的 DR 值</summary>
    public static int TakeDrDamage(UnitData data, int amount)
    {
        if (data == null || data.CurrentDr <= 0) return 0;
        int actual = Math.Min(amount, data.CurrentDr);
        data.CurrentDr -= actual;
        return actual;
    }

    // ========================================
    // 武器槽位
    // ========================================

    /// <summary>获取主手武器</summary>
    public static ItemData? GetMainHand(UnitData data, bool usingPrimaryWeapon) =>
        usingPrimaryWeapon ? data?.PrimaryMainHand : data?.SecondaryMainHand;

    /// <summary>获取副手物品</summary>
    public static ItemData? GetOffHand(UnitData data, bool usingPrimaryWeapon) =>
        usingPrimaryWeapon ? data?.PrimaryOffHand : data?.SecondaryOffHand;

    // ========================================
    // 攻击与伤害
    // ========================================

    /// <summary>
    /// 攻击加值 = 武器精通命中加成 + 武器命中修正 (v0.6 4.1，不再使用等级专精加值)
    /// + Lv.5+ 轻型武器 +1 命中 (v0.6 6.9)
    /// </summary>
    public static int GetAttackBonus(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 0;
        var weapon = GetMainHand(data, usingPrimaryWeapon) as WeaponData;

        // 武器精通命中加成 = floor(MasteryLevel / 3)
        int masteryHitBonus = 0;
        int lightLv5Bonus = 0;
        if (weapon != null)
        {
            int masteryLevel = data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            masteryHitBonus = masteryLevel / 3;
            // v0.6 6.9 轻型武器 Lv.5+ 命中 +1（仅命中，不影响暴击阈值）
            if (masteryLevel >= 5 && weapon.Weight == WeaponData.WeightCategory.Light)
                lightLv5Bonus = 1;
        }

        // 武器自身命中修正
        int weaponHitBonus = 0;
        if (weapon?.Subtype != null)
            weaponHitBonus = WeaponRegistry.GetConfig(weapon.Subtype).HitBonus;

        return masteryHitBonus + weaponHitBonus + lightLv5Bonus + GetBuffStatBonus(data, "attack_bonus");
    }

    /// <summary>
    /// 掷骰伤害
    /// 返回 Dictionary: dice, multiplier, str_bonus_pct, mastery_bonus_pct, total, text, weapon_subtype
    /// </summary>
    public static Godot.Collections.Dictionary RollDamage(UnitData data, bool usingPrimaryWeapon)
    {
        var weapon = GetMainHand(data, usingPrimaryWeapon) as WeaponData;
        int dmgDice = 0;
        string dText = "徒手(1-3)";

        // v0.6 §6.5: 武器伤害骰由武器面板（含 tier 缩放）决定，
        // 不再加"等级追加骰"（v0.5 旧机制 GetDamageDiceCount(level) 已废弃）。
        // 等级伤害成长走武器精通（每级 +10%）+ 装备 tier 升级。

        // 骰子结果
        if (weapon != null)
        {
            for (int i = 0; i < weapon.DamageDiceCount; i++)
                dmgDice += GD.RandRange(1, weapon.DamageDiceSides);
            int wMin = weapon.DamageDiceCount;
            int wMax = weapon.DamageDiceCount * weapon.DamageDiceSides;
            dText = $"{wMin}-{wMax}";
        }
        else
        {
            // 徒手攻击：1d3（拳头），不是1d20
            dmgDice = GD.RandRange(1, 3);
            dText = "徒手(1-3)";
        }

        // 百分比乘法加成体系
        // STR加成: floor(sqrt(STR)) × 10%
        int strMod = data != null ? (int)Mathf.Floor(Mathf.Sqrt(data.Str)) : 0;
        float strBonus = strMod * 0.1f;

        // 武器精通加成: 精通等级 × 10%
        int masteryLevel = (weapon != null && data != null)
            ? data.WeaponMastery.GetLevelBySubtype(weapon.Subtype)
            : 0;
        float masteryBonus = masteryLevel * 0.1f;

        float multiplier = 1.0f + strBonus + masteryBonus;
        int totalDmg = Math.Max(1, (int)(dmgDice * multiplier));

        return new Godot.Collections.Dictionary
        {
            { "dice", dmgDice },
            { "multiplier", multiplier },
            { "str_bonus_pct", (int)(strBonus * 100) },
            { "mastery_bonus_pct", (int)(masteryBonus * 100) },
            { "total", totalDmg },
            { "text", $"{dText}×{multiplier:F1}({(int)(multiplier * 100)}%)" },
            { "weapon_subtype", weapon?.Subtype.ToString() ?? "Unarmed" }
        };
    }



    // ========================================
    // 先攻计算
    // ========================================

    /// <summary>
    /// 先攻修正值 = DEX_mod + BaseInitiative
    /// 实际先攻 = d20 + GetInitiativeModifier()
    /// </summary>
    public static int GetInitiativeModifier(UnitData data)
    {
        if (data == null) return 0;
        return GetStatModifier(data.Dex) + data.BaseInitiative;
    }

    // ========================================
    // 移动
    // ========================================

    /// <summary>移动范围 = 基础 + 装备加成 + 饰品加成 + 坐骑加成</summary>
    public static int GetMoveRange(UnitData data)
    {
        if (data == null) return 4;
        int move = data.BaseMoveRange;
        move += data.GetEquipmentMoveBonus();
        move += data.AccessoryMoveBonus;
        if (data.Mount != null) move += data.Mount.SpeedBonus;
        move += GetBuffStatBonus(data, "speed");
        return Math.Max(1, move);
    }

    // ============================================================
    // Buff 系统集成
    // ============================================================

    /// <summary>
    /// 从 BuffSystem 查询指定属性的 Base 层加值(整数)。
    /// 用于 GetAc / GetAttackBonus / GetMoveRange 等简单加法属性。
    /// 对于需要完整多乘区的属性(如 damage),应直接用 DamageCalcPipeline。
    /// </summary>
    private static int GetBuffStatBonus(UnitData? data, string stat)
    {
        if (data == null) return 0;
        var result = Buff.BuffSystem.ResolveStatModifiers(data, stat);
        // 对于 AC/攻击/移动这类"基础+加值"属性,只取 FlatBonus(Base 层)
        // Increased/More/FinalMult 对这些属性无意义(它们是伤害专用乘区)
        return (int)result.FlatBonus;
    }
}