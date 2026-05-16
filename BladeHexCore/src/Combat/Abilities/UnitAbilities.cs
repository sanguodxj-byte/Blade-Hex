// UnitAbilities.cs
// 从单位的装备聚合所有能力
//
// 用法：
//   var abs = UnitAbilities.GetAll(unitData);
//   foreach (var a in abs) a.OnDealDamage(ctx);
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Combat.Abilities;

/// <summary>
/// 装备能力聚合器 — 从单位的所有装备槽位收集能力。
/// </summary>
public static class UnitAbilities
{
    /// <summary>
    /// 收集单位当前所有能力。
    /// 来源：装备槽位 + 坐骑 + 角色自身（IntrinsicAbilities，技能树/天赋等）。
    /// </summary>
    public static IEnumerable<EquipmentAbility> GetAll(UnitData? data)
    {
        if (data == null) yield break;

        // 装备能力
        foreach (var item in data.GetAllEquippedItems())
        {
            if (item == null) continue;
            foreach (var ab in item.Abilities)
                yield return ab;
        }

        // 坐骑能力
        if (data.Mount != null)
        {
            foreach (var ab in data.Mount.Abilities)
                yield return ab;
        }

        // 角色自身能力（技能树/天赋）
        foreach (var ab in data.IntrinsicAbilities)
            yield return ab;
    }

    /// <summary>
    /// 聚合所有能力的最大 HP 百分比加成（如 extra_hp_percent ×2 = 累加）
    /// </summary>
    public static float GetTotalMaxHpMultiplierBonus(UnitData? data)
    {
        float total = 0f;
        foreach (var ab in GetAll(data))
            total += ab.GetMaxHpMultiplierBonus();
        return total;
    }

    /// <summary>聚合伤害减免</summary>
    public static int GetTotalFlatDamageReduction(UnitData? data)
    {
        int total = 0;
        foreach (var ab in GetAll(data))
            total += ab.GetFlatDamageReduction();
        return total;
    }

    /// <summary>聚合法术 DC 加成</summary>
    public static int GetTotalSpellDcBonus(UnitData? data)
    {
        int total = 0;
        foreach (var ab in GetAll(data))
            total += ab.GetSpellDcBonus();
        return total;
    }

    /// <summary>聚合商店折扣（多个折扣相乘）</summary>
    public static float GetCombinedShopDiscountMultiplier(UnitData? data)
    {
        float result = 1.0f;
        foreach (var ab in GetAll(data))
            result *= ab.GetShopDiscountMultiplier();
        return result;
    }

    /// <summary>聚合招募折扣</summary>
    public static float GetCombinedRecruitDiscountMultiplier(UnitData? data)
    {
        float result = 1.0f;
        foreach (var ab in GetAll(data))
            result *= ab.GetRecruitDiscountMultiplier();
        return result;
    }

    /// <summary>聚合包夹命中加成</summary>
    public static int GetTotalFlankingHitBonus(UnitData? data)
    {
        int total = 0;
        foreach (var ab in GetAll(data))
            total += ab.GetFlankingHitBonus();
        return total;
    }
}
