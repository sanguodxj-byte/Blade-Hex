namespace BladeHex.Combat.Buff;

/// <summary>
/// 单条属性修正。一个 Buff 可以包含多条 StatModifier。
/// </summary>
public class StatModifier
{
    /// <summary>修正的属性键。如 "damage", "ac", "speed", "attack_bonus", "crit_chance"</summary>
    public string Stat = "";

    /// <summary>所属乘区</summary>
    public ModifierLayer Layer = ModifierLayer.Base;

    /// <summary>
    /// 修正值。
    /// Base 层: 固定加值(+5)
    /// Increased 层: 百分比(0.15 = 15% 增伤)
    /// More 层: 百分比(0.5 = 50% 更多伤害,即 ×1.5)
    /// FinalMult 层: 乘数(0.5 = 最终伤害 ×0.5)
    /// Override 层: 直接覆盖值
    /// </summary>
    public float Value;

    /// <summary>
    /// 可选条件表达式。为空表示无条件生效。
    /// 示例: "melee_only", "ranged_only", "vs_undead", "hp_below_50%", "is_flanking"
    /// </summary>
    public string Condition = "";

    /// <summary>来源标识(同源同层同属性取最高,不叠加)</summary>
    public string Source = "";
}
