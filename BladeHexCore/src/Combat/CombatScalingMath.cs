using System;
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat
{
    /// <summary>
    /// 职业大招数值公式的缩放数学真相源 (v0.8.1)
    /// 纯静态类，提供百分比计算、等级骰、属性等级乘算与武器骰转换，消灭一切硬编码常数
    /// </summary>
    public static class CombatScalingMath
    {
        /// <summary>等级骰子: max(1, Lv/4) d6 — 用于法术/技能直接伤害</summary>
        public static (int count, int sides) GetLevelDice(int level, int sides = 6)
            => (System.Math.Max(1, level / 4), sides);

        /// <summary>百分比 HP 转绝对值</summary>
        public static int PercentOfMaxHp(BattleUnitModel model, float percent)
            => System.Math.Max(1, (int)(model.GetMaxHp() * percent));

        /// <summary>百分比 Mana 转绝对值</summary>
        public static int PercentOfMaxMana(UnitData data, float percent)
            => System.Math.Max(1, (int)(CombatStats.GetMaxMana(data) * percent));

        /// <summary>属性修正型加值（用于 buff Modifier 的动态值）: 例如临时 HP = Mod(CON) × Level/2</summary>
        public static int StatModXLevel(int statScore, int level, float multiplier = 1.0f)
            => (int)System.Math.Ceiling(RPGRuleEngine.GetStatModifier(statScore) * level * multiplier);

        /// <summary>武器伤害骰应用（用于"再做一次正常近战攻击"的伤害基础）</summary>
        public static (int count, int sides) GetWeaponDice(BattleUnitModel model, int sidesFallback = 6)
        {
            var w = model.GetMainHand() as WeaponData;
            if (w != null) return (w.DamageDiceCount, w.DamageDiceSides);
            return (1, sidesFallback);
        }
    }
}
