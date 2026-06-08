// TraitAlertness.cs
// 特质：警觉 — 先攻+3
// T06a: 迁移自 CharacterGenerator.ApplyFunctionalTraits
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

/// <summary>
/// 警觉特质：先攻+3
/// 原行为：CharacterGenerator.ApplyFunctionalTraits 中 if (t.FunctionalEffect == "alertness") u.BaseInitiative += 3
/// </summary>
public class TraitAlertness : TraitEffectBase
{
    public override string TraitId => "alertness";
    public override string DisplayName => "警觉";

    public override void OnUnitCreated(UnitData u, float effectValue)
    {
        // 默认 +3，可通过 effectValue 覆盖
        int bonus = effectValue > 0 ? (int)effectValue : 3;
        u.BaseInitiative += bonus;
    }

    public override bool Modifies(string statName) => statName == "initiative";
}
