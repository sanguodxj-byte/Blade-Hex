// TraitTimid.cs
// 特质：胆小 — HP<50%时攻击-1
// T06k
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitTimid : TraitEffectBase
{
    public override string TraitId => "timid";
    public override string DisplayName => "胆小";

    public override void OnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input, float effectValue)
    {
        // HP 低于 50% 时攻击-1
        int currentHp = attacker.Runtime.CurrentHp;
        int maxHp = attacker.GetMaxHp();
        if (currentHp < maxHp / 2)
        {
            int penalty = effectValue > 0 ? (int)effectValue : 1;
            input.Modifier -= penalty;
        }
    }

    public override bool Modifies(string statName) => statName == "attack_bonus";
}
