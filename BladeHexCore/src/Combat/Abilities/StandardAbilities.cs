// StandardAbilities.cs
// 8 个内置装备能力 — 对应原 AccessoryData/EquipmentAffix 中的 SpecialEffect 字符串
//
// 加新能力的步骤：
//   1. 在此文件新增一个继承 EquipmentAbility 的类
//   2. 在 EquipmentAbilityRegistry 注册其 ID 和工厂
//   3. JSON 物品的 abilities 数组中写 {"id": "<your_id>", "value": ...}
using System;
using System.Globalization;
using BladeHex.Data;

namespace BladeHex.Combat.Abilities;

// ============================================================================
// 战斗触发型
// ============================================================================

/// <summary>life_steal — 攻击造成伤害时按比例回血</summary>
public sealed class LifestealAbility : EquipmentAbility
{
    public float HealRatio => Magnitude;

    public override string GetTooltipText()
        => $"攻击回复{HealRatio * 100:F0}%伤害HP";

    public override void OnDealDamage(DealDamageContext ctx)
    {
        if (ctx.HpDamageDealt <= 0) return;
        int heal = Math.Max(1, (int)(ctx.HpDamageDealt * HealRatio));
        ctx.HealAmount += heal;
    }
}

/// <summary>thorns — 受击时反弹固定伤害给攻击方</summary>
public sealed class ThornsAbility : EquipmentAbility
{
    public int ReflectDamage => (int)Magnitude;

    public override string GetTooltipText()
        => $"被击时反弹{ReflectDamage}伤害";

    public override void OnTakeDamage(TakeDamageContext ctx)
    {
        if (ctx.HpDamageTaken <= 0) return;
        ctx.ReflectDamage += ReflectDamage;
    }
}

// ============================================================================
// 静态修正型（无钩子，仅参与属性聚合）
// ============================================================================

/// <summary>extra_hp_percent — 最大 HP 百分比加成</summary>
public sealed class ExtraHpPercentAbility : EquipmentAbility
{
    public float Percent => Magnitude;

    public override string GetTooltipText()
        => $"HP+{Percent * 100:F0}%";

    public override float GetMaxHpMultiplierBonus() => Percent;
}

/// <summary>damage_reduction — 固定伤害减免</summary>
public sealed class DamageReductionAbility : EquipmentAbility
{
    public int Amount => (int)Magnitude;

    public override string GetTooltipText() => $"伤害减免{Amount}";
    public override int GetFlatDamageReduction() => Amount;
}

/// <summary>spell_dc_bonus — 法术 DC 加成</summary>
public sealed class SpellDcBonusAbility : EquipmentAbility
{
    public int Bonus => (int)Magnitude;

    public override string GetTooltipText()
        => $"法术强度{Bonus.ToString("+#;-#;0", CultureInfo.InvariantCulture)}";

    public override int GetSpellDcBonus() => Bonus;
}

/// <summary>shop_discount — 商店折扣（数值为折扣比例，0.15 = 八五折）</summary>
public sealed class ShopDiscountAbility : EquipmentAbility
{
    public float DiscountRate => Magnitude;

    public override string GetTooltipText()
        => $"商店价格-{DiscountRate * 100:F0}%";

    public override float GetShopDiscountMultiplier() => 1.0f - DiscountRate;
}

/// <summary>recruit_discount — 招募折扣</summary>
public sealed class RecruitDiscountAbility : EquipmentAbility
{
    public float DiscountRate => Magnitude;

    public override string GetTooltipText()
        => $"招募价格-{DiscountRate * 100:F0}%";

    public override float GetRecruitDiscountMultiplier() => 1.0f - DiscountRate;
}

/// <summary>flanking_bonus — 包夹时命中加成</summary>
public sealed class FlankingBonusAbility : EquipmentAbility
{
    public int HitBonus => (int)Magnitude;

    public override string GetTooltipText()
        => $"包夹时命中{HitBonus.ToString("+#;-#;0", CultureInfo.InvariantCulture)}";

    public override int GetFlankingHitBonus() => HitBonus;
}
