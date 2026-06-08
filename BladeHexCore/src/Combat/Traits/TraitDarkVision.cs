// TraitDarkVision.cs
// 特质：夜视 — 视野范围+2
// T06b
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitDarkVision : TraitEffectBase
{
    public override string TraitId => "dark_vision";
    public override string DisplayName => "夜视";

    public override void OnUnitCreated(UnitData u, float effectValue)
    {
        // VisionRange 是大地图属性，当前 UnitData 可能没有此字段
        // 预留接口，等大地图视野系统接入
        // u.VisionRange += 2;
    }

    public override bool Modifies(string statName) => statName == "vision";
}
