// TraitGluttony.cs
// 特质：贪吃 — 补给消耗×1.5
// T06j
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitGluttony : TraitEffectBase
{
    public override string TraitId => "gluttony";
    public override string DisplayName => "贪吃";

    public override void OnPartyDayPass(PartyContext ctx, float effectValue)
    {
        // 食物消耗增加 50%
        ctx.FoodConsumptionMultiplier *= 1.5f;
    }
}
