// EquipmentAbility.cs
// 装备能力组件 — 替代字符串 SpecialEffect 的可组合系统
//
// 设计：
//   - 每个能力是一个对象，挂在物品上（ItemData.Abilities）
//   - 能力通过钩子（OnDealDamage/OnTakeDamage/...）参与战斗管线
//   - 加新能力 = 新建一个 EquipmentAbility 子类 + 注册到 Registry，无需修改任何战斗代码
//   - JSON 中的 special_effect 字符串通过 Registry 工厂创建对应实例
//
// 与原系统对比：
//   旧：if (effect == "life_steal") { ... } else if (effect == "thorns") { ... }
//   新：foreach (var ab in abilities) ab.OnDealDamage(ctx);
using BladeHex.Data;
using System.Collections.Generic;

namespace BladeHex.Combat.Abilities;

/// <summary>
/// 装备能力基类 — 所有特殊效果继承此类。
/// 子类按需重写钩子方法，未重写的钩子默认空实现。
/// </summary>
public abstract class EquipmentAbility
{
    /// <summary>能力 ID（与 JSON 中的 special_effect 字符串对应）</summary>
    public string AbilityId { get; init; } = "";

    /// <summary>能力数值（百分比、固定加成等的载体，含义由具体能力决定）</summary>
    public float Magnitude { get; init; }

    /// <summary>该能力在 tooltip 中显示的描述</summary>
    public abstract string GetTooltipText();

    // ========================================
    // 战斗钩子 — 默认空实现，子类按需重写
    // ========================================

    /// <summary>攻击造成伤害后调用（攻击方装备触发）</summary>
    public virtual void OnDealDamage(DealDamageContext ctx) { }

    /// <summary>承受伤害后调用（防御方装备触发）</summary>
    public virtual void OnTakeDamage(TakeDamageContext ctx) { }

    // ========================================
    // 静态修正 — 用于聚合查询（HP/AC/折扣等）
    // ========================================

    /// <summary>HP 百分比加成（如 extra_hp_percent: +20% HP）</summary>
    public virtual float GetMaxHpMultiplierBonus() => 0f;

    /// <summary>固定伤害减免（如 damage_reduction: -5 dmg）</summary>
    public virtual int GetFlatDamageReduction() => 0;

    /// <summary>法术 DC 加成</summary>
    public virtual int GetSpellDcBonus() => 0;

    /// <summary>商店购物折扣（1.0 = 无折扣，0.85 = 八五折）</summary>
    public virtual float GetShopDiscountMultiplier() => 1.0f;

    /// <summary>招募折扣</summary>
    public virtual float GetRecruitDiscountMultiplier() => 1.0f;

    /// <summary>包夹时命中加成</summary>
    public virtual int GetFlankingHitBonus() => 0;
}

// ============================================================================
// 战斗钩子上下文
// ============================================================================

/// <summary>"造成伤害" 钩子的上下文（攻击方装备读取）</summary>
public class DealDamageContext
{
    public BattleUnitModel Attacker { get; init; } = null!;
    public BattleUnitModel Defender { get; init; } = null!;

    /// <summary>实际造成的 HP 伤害</summary>
    public int HpDamageDealt { get; init; }

    /// <summary>实际造成的 DR/护甲伤害</summary>
    public int DrDamageDealt { get; init; }

    /// <summary>累积的回血量（多个能力可累加）</summary>
    public int HealAmount { get; set; }

    /// <summary>累积的反弹/连锁伤害（如电链跳跃，待 Phase 2）</summary>
    public List<DamageEvent> ExtraDamageEvents { get; } = new();
}

/// <summary>"承受伤害" 钩子的上下文（防御方装备读取）</summary>
public class TakeDamageContext
{
    public BattleUnitModel Attacker { get; init; } = null!;
    public BattleUnitModel Defender { get; init; } = null!;

    public int HpDamageTaken { get; init; }
    public int DrDamageTaken { get; init; }

    /// <summary>反伤伤害（thorns 类，由防御方能力填充，由调用方应用到攻击方）</summary>
    public int ReflectDamage { get; set; }
}

/// <summary>额外伤害事件（用于电链、溅射等）</summary>
public class DamageEvent
{
    public BattleUnitModel Target { get; init; } = null!;
    public int Damage { get; init; }
    public string SourceAbilityId { get; init; } = "";
}
