using BladeHex.Data;

namespace BladeHex.Combat;

public static class SpellTargetRules
{
    public static bool IsValidTarget(UnitData? caster, UnitData? target, SpellData spell)
    {
        if (caster == null || target == null || spell == null) return false;

        return spell.GetEffectiveTargetAffinity() switch
        {
            SpellData.SpellTargetAffinity.Self => ReferenceEquals(caster, target),
            SpellData.SpellTargetAffinity.Allies => IsSameSide(caster, target),
            SpellData.SpellTargetAffinity.Enemies => !IsSameSide(caster, target),
            SpellData.SpellTargetAffinity.AllUnits => true,
            _ => true,
        };
    }

    public static bool IsSameSide(UnitData caster, UnitData target)
        => caster.IsEnemy == target.IsEnemy;

    public static bool RequiresValidUnitTarget(SpellData spell)
    {
        if (spell == null) return false;

        var affinity = spell.GetEffectiveTargetAffinity();
        return affinity is SpellData.SpellTargetAffinity.Self
            or SpellData.SpellTargetAffinity.Allies
            or SpellData.SpellTargetAffinity.Enemies;
    }
}
