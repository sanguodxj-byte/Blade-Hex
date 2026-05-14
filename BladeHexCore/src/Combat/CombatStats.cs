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
    /// 最大 HP = 基础HP + CON修正 × 等级 + 装备HP加成 + 饰品HP加成
    /// </summary>
    public static int GetMaxHp(UnitData data)
    {
        if (data == null) return 1;
        int hp = RPGRuleEngine.CalculateMaxHp(data.BaseMaxHp, data.Con, data.Level);
        hp += data.GetEquipmentHpBonus();
        hp += data.AccessoryHpBonus;
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

    /// <summary>护甲 AP 惩罚 =  armor + shield + helmet 的 ApPenalty 总和</summary>
    public static int GetArmorApPenalty(UnitData data)
    {
        if (data == null) return 0;
        int penalty = 0;
        if (data.Armor != null) penalty += data.Armor.ApPenalty;
        if (data.Shield != null) penalty += data.Shield.ApPenalty;
        if (data.Helmet != null) penalty += data.Helmet.ApPenalty;
        return penalty;
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
    /// </summary>
    public static float GetCritMultiplier(UnitData data)
    {
        if (data == null) return 2.0f;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, data.Wis - 14) / 4.0));
        return 2.0f + wisCritTier * 0.1f;
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
        if (data == null) return 10;
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

        return ac + dexAc + armorDrAc + shieldDrAc;
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

    /// <summary>攻击加值 = 武器精通命中加成 + 武器命中修正（不再使用等级专精加值）</summary>
    public static int GetAttackBonus(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 0;
        var weapon = GetMainHand(data, usingPrimaryWeapon) as WeaponData;

        // 武器精通命中加成 = floor(MasteryLevel / 3)
        int masteryHitBonus = 0;
        if (weapon != null)
        {
            int masteryLevel = data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            masteryHitBonus = masteryLevel / 3;
        }

        // 武器自身命中修正
        int weaponHitBonus = 0;
        if (weapon?.Subtype != null)
            weaponHitBonus = WeaponRegistry.GetConfig(weapon.Subtype).HitBonus;

        return masteryHitBonus + weaponHitBonus;
    }

    /// <summary>
    /// 掷骰伤害
    /// 返回 Dictionary: dice, multiplier, str_bonus_pct, mastery_bonus_pct, total, text, weapon_subtype
    /// </summary>
    public static Godot.Collections.Dictionary RollDamage(UnitData data, bool usingPrimaryWeapon)
    {
        var weapon = GetMainHand(data, usingPrimaryWeapon) as WeaponData;
        int dmgDice = 0;
        string dText = "徒手(1d20)";

        int levelExtra = data != null ? RPGRuleEngine.GetDamageDiceCount(data.Level) - 1 : 0;

        // 骰子结果
        if (weapon != null)
        {
            for (int i = 0; i < weapon.DamageDiceCount; i++)
                dmgDice += GD.RandRange(1, weapon.DamageDiceSides);
            if (levelExtra > 0)
                for (int i = 0; i < levelExtra; i++)
                    dmgDice += GD.RandRange(1, weapon.DamageDiceSides);
            dText = $"{weapon.DamageDiceCount + levelExtra}d{weapon.DamageDiceSides}";
        }
        else
        {
            // 徒手攻击：1d3（拳头），不是1d20
            dmgDice = GD.RandRange(1, 3);
            dText = "徒手(1d3)";
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
        return Math.Max(1, move);
    }

}