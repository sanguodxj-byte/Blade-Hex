// TraitThickSkin.cs
// 特质：厚皮 — 受到物理伤害-1
// T06e
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitThickSkin : TraitEffectBase
{
    public override string TraitId => "thick_skin";
    public override string DisplayName => "厚皮";

    public override void OnDamageTaken(BattleUnitModel unit, ref DamageInput input, float effectValue)
    {
        // 物理伤害类型减伤
        if (input.DamageType is "slash" or "pierce" or "crush" or "bleed")
        {
            int reduction = effectValue > 0 ? (int)effectValue : 1;
            input.Amount = System.Math.Max(1, input.Amount - reduction);
        }
    }

    public override bool Modifies(string statName) => statName == "damage_reduction";
}
