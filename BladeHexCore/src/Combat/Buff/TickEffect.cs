namespace BladeHex.Combat.Buff;

/// <summary>Buff 每回合 tick 时的效果(伤害或治疗)</summary>
public class TickEffect
{
    public int DiceCount = 1;
    public int DiceSides = 4;
    public float TargetMaxHpPercent;
    public string DamageType = "";   // "fire", "poison", "bleed", "holy"
    public bool IsHeal;              // true = 治疗而非伤害
    /// <summary>Tick 伤害/治疗所属乘区(通常是 Base,但某些 buff 的 tick 可以享受增伤)</summary>
    public ModifierLayer TickLayer = ModifierLayer.Base;
}
