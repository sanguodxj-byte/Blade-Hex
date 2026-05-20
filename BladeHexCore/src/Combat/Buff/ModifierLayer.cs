namespace BladeHex.Combat.Buff;

/// <summary>
/// 属性修正所属的乘区层级。
/// 伤害计算顺序: Base(加法) → Increased(加法合并后×1) → More(各自独立相乘) → FinalMult(最终乘)
/// </summary>
public enum ModifierLayer
{
    Base,       // 加法叠加到基础值(武器骰、STR mod、技能固定加值)
    Increased,  // 增伤%:所有 Increased 加法合并后 ×(1+sum)。如"近战伤害+15%"
    More,       // 更多%:各自独立相乘。如暴击×2、冲锋×1.3
    FinalMult,  // 最终乘区:抗性×0.5、弱点×1.5
    Override,   // 覆盖值:最高优先级,直接替换计算结果
}
