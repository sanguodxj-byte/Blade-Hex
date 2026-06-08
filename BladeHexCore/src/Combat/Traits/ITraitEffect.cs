// ITraitEffect.cs
// 特质效果接口 — 统一的特质钩子系统
// T05: 建立 TraitRegistry 框架
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

/// <summary>
/// 特质效果接口 — 每个功能性特质实现此接口
/// 提供 6 个钩子：创建、回合开始、攻击、受伤、施法、大地图日
/// </summary>
public interface ITraitEffect
{
    /// <summary>特质 ID（与 TraitData.TraitId 对应）</summary>
    string TraitId { get; }

    /// <summary>显示名称</summary>
    string DisplayName { get; }

    /// <summary>单位创建时触发（一次性加成）</summary>
    void OnUnitCreated(UnitData u, float effectValue);

    /// <summary>回合开始时触发</summary>
    void OnTurnStart(BattleUnitModel unit, float effectValue);

    /// <summary>攻击检定时触发（可修改攻击输入）</summary>
    void OnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input, float effectValue);

    /// <summary>受到伤害时触发（可修改伤害输入）</summary>
    void OnDamageTaken(BattleUnitModel unit, ref DamageInput input, float effectValue);

    /// <summary>施法时触发</summary>
    void OnSpellCast(BattleUnitModel caster, SpellData spell, float effectValue);

    /// <summary>大地图每日经过时触发</summary>
    void OnPartyDayPass(PartyContext ctx, float effectValue);

    /// <summary>是否修改指定属性（用于 UI 预览）</summary>
    bool Modifies(string statName);
}

/// <summary>攻击输入（可被特质修改）</summary>
public struct AttackInput
{
    public int Modifier;
    public bool HasAdvantage;
    public bool HasDisadvantage;
    public float DamageMultiplier;
}

/// <summary>伤害输入（可被特质修改）</summary>
public struct DamageInput
{
    public int Amount;
    public string DamageType;
    public bool IsCritical;
}

/// <summary>大地图上下文</summary>
public class PartyContext
{
    public UnitData Unit { get; set; } = null!;
    public int CurrentDay { get; set; }
    public float FoodConsumptionMultiplier { get; set; } = 1.0f;
    public float FatigueMultiplier { get; set; } = 1.0f;
    public int LoyaltyChange { get; set; }
}
