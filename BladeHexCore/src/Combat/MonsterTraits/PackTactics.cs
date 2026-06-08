// PackTactics.cs
// 单位特质：群猎 — 相邻有友军围攻同一目标时攻击优势
// T09c
using BladeHex.Data;
using BladeHex.Combat.Traits;  // For AttackInput

namespace BladeHex.Combat.MonsterTraits;

public class PackTactics : MonsterTraitBase
{
    public override string TraitId => "pack_tactics";
    public override string DisplayName => "群猎";

    public override void OnAttackRoll(BattleUnitModel attacker, BattleUnitModel defender, ref AttackInput input)
    {
        // 检查相邻是否有友军围攻同一目标
        // 需要战场信息（HexGrid），当前接口未传入
        // 预留接口，等战场系统接入
        // if (HasAdjacentAllyAttackingSameTarget(attacker, defender))
        // {
        //     input.HasAdvantage = true;
        // }
    }
}
