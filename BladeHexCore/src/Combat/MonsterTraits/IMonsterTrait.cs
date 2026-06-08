// IMonsterTrait.cs
// 单位特质接口 — 类似 ITraitEffect 但属于 unit 层
// T09: MonsterTraitRegistry + 5 trait MVP
using BladeHex.Data;
using BladeHex.Combat.Traits;  // For AttackInput, DamageInput

namespace BladeHex.Combat.MonsterTraits;

/// <summary>
/// 单位特质接口 — 每个单位特质实现此接口
/// </summary>
public interface IMonsterTrait
{
    /// <summary>特质 ID（与模板 traits 数组中的 ID 对应）</summary>
    string TraitId { get; }

    /// <summary>显示名称</summary>
    string DisplayName { get; }

    /// <summary>单位创建时触发（一次性加成）</summary>
    void OnUnitCreated(UnitData u);

    /// <summary>回合开始时触发</summary>
    void OnTurnStart(BattleUnitModel unit);

    /// <summary>攻击检定时触发（可修改攻击输入）</summary>
    void OnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input);

    /// <summary>受到伤害时触发（可修改伤害输入）</summary>
    void OnDamageTaken(BattleUnitModel unit, ref DamageInput input);
}
