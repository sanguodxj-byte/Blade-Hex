// TraitIndomitable.cs
// 特质：不屈 — HP归零时50%概率保持1HP（每战1次）
// T06f
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitIndomitable : TraitEffectBase
{
    public override string TraitId => "indomitable";
    public override string DisplayName => "不屈";

    // 每场战斗只能触发一次，需要战斗状态追踪
    // 当前实现：在 OnDamageTaken 中检查，但需要外部状态管理
    // 预留接口，等战斗状态系统接入

    public override void OnDamageTaken(BattleUnitModel unit, ref DamageInput input, float effectValue)
    {
        // 检查是否会导致死亡
        if (unit.Runtime.CurrentHp - input.Amount <= 0)
        {
            // 检查本场战斗是否已触发
            // if (unit.Runtime.HasFlag("indomitable_used")) return;

            // 50% 概率触发
            if (CombatRandom.RandRange(1, 100) <= 50)
            {
                // 保留 1 HP
                input.Amount = unit.Runtime.CurrentHp - 1;
                // unit.Runtime.SetFlag("indomitable_used");
            }
        }
    }
}
