// TraitPremonition.cs
// 特质：预感 — 被伏击时自动获得一轮准备
// T06h
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitPremonition : TraitEffectBase
{
    public override string TraitId => "premonition";
    public override string DisplayName => "预感";

    public override void OnUnitCreated(UnitData u, float effectValue)
    {
        // 标记拥有预感特质（AmbushSystem 查询时检查此标记）
        // 当前伏击系统未实现，预留接口
        // u.Runtime.SetFlag("has_premonition");
    }
}
