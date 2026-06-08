// DamageResult.cs
// 伤害解算结果 — Core 层结构体
// BattleUnitModel.ApplyDamage 的返回值，供 View 层（Unit/VFX）驱动表现
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>伤害来源标记 — 供日志/XP 归因使用</summary>
public enum DamageSource
{
    /// <summary>常规武器攻击（经 CombatResolver）</summary>
    WeaponAttack,
    /// <summary>技能效果</summary>
    Skill,
    /// <summary>消耗品（投掷物等）</summary>
    Consumable,
    /// <summary>环境/地图事件</summary>
    Environment,
    /// <summary>法术</summary>
    Spell,
    /// <summary>其他（未分类）</summary>
    Other,
}

/// <summary>
/// 伤害解算结果 — 纯数据
/// 穿甲/护甲判定 + HP/DR 分配 + 精通 XP 归因在 BattleUnitModel.ApplyDamage 内完成，
/// View 层只消费本结构驱动表现（HP 动画、VFX、音效）
/// </summary>
public readonly struct DamageResult
{
    /// <summary>是否判定为穿透（自然 20 或 roll ≥ drThreshold）</summary>
    public bool IsPenetrated { get; init; }

    /// <summary>实际扣除的 HP</summary>
    public int HpDamage { get; init; }

    /// <summary>实际扣除的 DR（护甲耐久）</summary>
    public int DrDamage { get; init; }

    /// <summary>护甲是否因此次伤害彻底毁坏</summary>
    public bool ArmorBroken { get; init; }

    /// <summary>本次伤害是否击杀目标</summary>
    public bool KilledUnit { get; init; }

    /// <summary>防御方剩余 HP（已扣减后）</summary>
    public int RemainingHp { get; init; }

    /// <summary>攻击者武器精通是否升级</summary>
    public bool MasteryLeveledUp { get; init; }

    /// <summary>攻击者武器精通升级后的新等级（未升级为 0）</summary>
    public int MasteryNewLevel { get; init; }

    /// <summary>由防御方装备能力（如 thorns）反弹给攻击方的伤害</summary>
    public int ReflectDamageToAttacker { get; init; }

    /// <summary>被盾牌吸收的远程伤害值</summary>
    public int ShieldAbsorbed { get; init; }

    /// <summary>盾牌是否因此次伤害彻底毁坏</summary>
    public bool ShieldBroken { get; init; }

    /// <summary>被守护链接等效果转移给另一个单位的 HP 伤害</summary>
    public int RedirectedHpDamage { get; init; }

    /// <summary>承受转移伤害的单位实例 ID；无转移时为 0</summary>
    public long RedirectedToUnitId { get; init; }

    /// <summary>总伤害（HP + DR）— 精通 XP 以该值归因</summary>
    public int TotalDealt => HpDamage + DrDamage;

    /// <summary>伤害被"完全挡下"（HP 伤害为 0 且 DR 伤害 > 0）— 便于 View 播放"叮"特效</summary>
    public bool IsBlocked => HpDamage == 0 && DrDamage > 0;
}
