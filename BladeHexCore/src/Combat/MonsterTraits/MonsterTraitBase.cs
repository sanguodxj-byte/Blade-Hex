// MonsterTraitBase.cs
// 单位特质基类 — 提供默认空实现
// T09: MonsterTraitRegistry + 5 trait MVP
using BladeHex.Data;
using BladeHex.Combat.Traits;  // For AttackInput, DamageInput

namespace BladeHex.Combat.MonsterTraits;

/// <summary>
/// 单位特质基类 — 所有钩子默认 no-op
/// </summary>
public abstract class MonsterTraitBase : IMonsterTrait
{
    public abstract string TraitId { get; }
    public abstract string DisplayName { get; }

    public virtual void OnUnitCreated(UnitData u) { }
    public virtual void OnTurnStart(BattleUnitModel unit) { }
    public virtual void OnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input) { }
    public virtual void OnDamageTaken(BattleUnitModel unit, ref DamageInput input) { }
}
