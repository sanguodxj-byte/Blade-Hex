// Regeneration.cs
// 单位特质：再生 — 自我恢复1d4 HP（除非上回合受到fire/acid伤害）
// T09e
using BladeHex.Data;

namespace BladeHex.Combat.MonsterTraits;

public class Regeneration : MonsterTraitBase
{
    public override string TraitId => "regeneration";
    public override string DisplayName => "再生";

    public override void OnTurnStart(BattleUnitModel unit)
    {
        // 检查上回合是否受到 fire/acid 伤害
        // 当前 UnitRuntimeState 没有 LastDamageType 字段，预留接口
        // if (unit.Runtime.LastDamageType is "fire" or "acid")
        //     return;

        // 恢复 1d4 HP
        int heal = CombatRandom.RollDice(1, 4);
        int maxHp = unit.GetMaxHp();
        unit.Runtime.CurrentHp = System.Math.Min(maxHp, unit.Runtime.CurrentHp + heal);
    }
}
