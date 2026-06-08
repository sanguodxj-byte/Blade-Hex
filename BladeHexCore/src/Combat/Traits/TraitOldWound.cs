// TraitOldWound.cs
// 特质：旧伤 — 战斗开始时HP-10%
// T06i
using BladeHex.Data;

namespace BladeHex.Combat.Traits;

public class TraitOldWound : TraitEffectBase
{
    public override string TraitId => "old_wound";
    public override string DisplayName => "旧伤";

    public override void OnUnitCreated(UnitData u, float effectValue)
    {
        // 战斗开始时 HP 减少 10%
        // 注意：这里减的是 BaseMaxHp，不是 CurrentHp
        // CurrentHp 会在战斗开始时从 GetMaxHp() 初始化
        int maxHp = BladeHex.Combat.CombatStats.GetMaxHp(u);
        int reduction = (int)(maxHp * 0.1f);
        u.BaseMaxHp = System.Math.Max(1, u.BaseMaxHp - reduction);
    }

    public override bool Modifies(string statName) => statName == "max_hp";
}
