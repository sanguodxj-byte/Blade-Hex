// TraitEffectBase.cs
// 特质效果基类 — 提供默认空实现，子类按需重载
// T05: 建立 TraitRegistry 框架
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

/// <summary>
/// 特质效果基类 — 所有钩子默认 no-op
/// 子类只需重载需要的钩子
/// </summary>
public abstract class TraitEffectBase : ITraitEffect
{
    public abstract string TraitId { get; }
    public abstract string DisplayName { get; }

    public virtual void OnUnitCreated(UnitData u, float effectValue) { }
    public virtual void OnTurnStart(BattleUnitModel unit, float effectValue) { }
    public virtual void OnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input, float effectValue) { }
    public virtual void OnDamageTaken(BattleUnitModel unit, ref DamageInput input, float effectValue) { }
    public virtual void OnSpellCast(BattleUnitModel caster, SpellData spell, float effectValue) { }
    public virtual void OnPartyDayPass(PartyContext ctx, float effectValue) { }
    public virtual bool Modifies(string statName) => false;
}
