// FlyingTrait.cs
// 单位特质：飞行 — 无视地形
// T09b
using BladeHex.Data;

namespace BladeHex.Combat.MonsterTraits;

public class FlyingTrait : MonsterTraitBase
{
    public override string TraitId => "flying";
    public override string DisplayName => "飞行";

    public override void OnUnitCreated(UnitData u)
    {
        // 标记飞行单位（HexCell 移动时检查此标记）
        // 当前 UnitData 没有 IsFlying 字段，预留接口
        // u.IsFlying = true;
    }
}
