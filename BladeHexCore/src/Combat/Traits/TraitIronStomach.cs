// TraitIronStomach.cs
// 特质：铁胃 — 免疫食物中毒
// T06c
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitIronStomach : TraitEffectBase
{
    public override string TraitId => "iron_stomach";
    public override string DisplayName => "铁胃";

    public override void OnPartyDayPass(PartyContext ctx, float effectValue)
    {
        // 标记免疫食物中毒（FoodSystem 查询时检查此标记）
        // 当前 FoodSystem 未实现中毒机制，预留接口
        // ctx.Unit.SetFlag("immune_food_poison");
    }
}
