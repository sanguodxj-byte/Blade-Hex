// TraitEtherResonance.cs
// 特质：以太共鸣 — 施法时恢复1d4 HP
// T06g
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitEtherResonance : TraitEffectBase
{
    public override string TraitId => "ether_resonance";
    public override string DisplayName => "以太共鸣";

    public override void OnSpellCast(BattleUnitModel caster, SpellData spell, float effectValue)
    {
        // 施法时恢复 1d4 HP
        int heal = CombatRandom.RollDice(1, 4); // 1d4
        int maxHp = caster.GetMaxHp();
        caster.Runtime.CurrentHp = System.Math.Min(maxHp, caster.Runtime.CurrentHp + heal);
    }

    public override bool Modifies(string statName) => statName == "heal_on_cast";
}
