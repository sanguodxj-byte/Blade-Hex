// FearAura.cs
// 单位特质：恐惧光环 — 范围2格内敌人做WIS豁免
// T09d
using BladeHex.Data;

namespace BladeHex.Combat.MonsterTraits;

public class FearAura : MonsterTraitBase
{
    public override string TraitId => "fear_aura";
    public override string DisplayName => "恐惧光环";

    public override void OnTurnStart(BattleUnitModel unit)
    {
        // 每回合开始时，范围2格内敌人做WIS豁免 vs DC 13
        // 失败附 frightened 1 回合
        // 需要战场信息（HexGrid + 所有单位），当前接口未传入
        // 预留接口，等战场系统接入
        // var nearbyEnemies = GetEnemiesInRange(unit, 2);
        // foreach (var enemy in nearbyEnemies)
        // {
        //     if (!MakeWisSave(enemy, dc: 13))
        //         ApplyBuff(enemy, "frightened", duration: 1);
        // }
    }
}
