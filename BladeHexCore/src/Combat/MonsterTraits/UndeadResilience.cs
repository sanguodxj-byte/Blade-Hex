// UndeadResilience.cs
// 单位特质：亡灵坚韧 — 加 necrotic resistance + poison immunity
// T09a
using BladeHex.Data;
using System.Collections.Generic;

namespace BladeHex.Combat.MonsterTraits;

public class UndeadResilience : MonsterTraitBase
{
    public override string TraitId => "undead_resilience";
    public override string DisplayName => "亡灵坚韧";

    public override void OnUnitCreated(UnitData u)
    {
        // 添加 necrotic 抗性
        var resistances = new List<string>(u.Resistances ?? new string[0]);
        if (!resistances.Contains("necrotic"))
            resistances.Add("necrotic");
        u.Resistances = resistances.ToArray();

        // 添加 poison 免疫
        var immunities = new List<string>(u.Immunities ?? new string[0]);
        if (!immunities.Contains("poison"))
            immunities.Add("poison");
        u.Immunities = immunities.ToArray();
    }
}
