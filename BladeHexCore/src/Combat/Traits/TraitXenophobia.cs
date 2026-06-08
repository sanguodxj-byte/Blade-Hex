// TraitXenophobia.cs
// 特质：仇外 — 与外族队友在一起时忠诚度-10
// T06l
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitXenophobia : TraitEffectBase
{
    public override string TraitId => "xenophobia";
    public override string DisplayName => "仇外";

    public override void OnPartyDayPass(PartyContext ctx, float effectValue)
    {
        // 检查队伍中是否有外族成员
        // 当前 PartyContext 没有队伍信息，预留接口
        // 需要外部系统检查队伍种族构成并设置 ctx.LoyaltyChange
        // if (HasDifferentRaceInParty(ctx))
        // {
        //     ctx.LoyaltyChange -= 10;
        // }
    }
}
