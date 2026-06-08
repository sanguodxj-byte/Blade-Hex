// TraitAdaptability.cs
// 特质：适应力 — 行军疲劳×0.5
// T06d
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitAdaptability : TraitEffectBase
{
    public override string TraitId => "adaptability";
    public override string DisplayName => "适应力";

    public override void OnPartyDayPass(PartyContext ctx, float effectValue)
    {
        // 行军疲劳减半
        ctx.FatigueMultiplier *= 0.5f;
    }
}
