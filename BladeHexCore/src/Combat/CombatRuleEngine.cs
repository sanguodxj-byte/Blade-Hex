// CombatRuleEngine.cs
// 战斗规则引擎 — 纯数学/规则层
// 从 CombatResolver (Frontend) 提取的核心战斗规则，无 Node/场景树依赖
// 对应策划案 03-战术战斗系统
using Godot;
using System;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 战斗规则引擎 — 静态工具类
/// 封装攻击检定、命中判定、伤害管道中的纯规则逻辑。
/// CombatResolver (Frontend) 负责收集 Node 层数据后委托到此处。
/// </summary>
public static class CombatRuleEngine
{
    // ============================================================================
    // 攻击检定输入/输出数据结构
    // ============================================================================

    /// <summary>攻击检定所需的全部输入参数（由 Frontend 从 Node 层收集）</summary>
    public struct AttackInput
    {
        /// <summary>攻击加值（来自 BattleUnitModel.GetAttackBonus）</summary>
        public int AttackBonus;

        /// <summary>目标 AC</summary>
        public int TargetAc;

        /// <summary>暴击阈值（来自 BattleUnitModel.GetCritThreshold）</summary>
        public int CritThreshold;

        /// <summary>是否有优势</summary>
        public bool HasAdvantage;

        /// <summary>是否有劣势</summary>
        public bool HasDisadvantage;

        /// <summary>额外命中修正（技能等）</summary>
        public int AccuracyMod;

        /// <summary>半掩体 AC 加值</summary>
        public int CoverAcBonus;

        /// <summary>
        /// 命中后追加的暴击概率（0~1）。来自技能盘 critical_rate 节点等"独立暴击概率"
        /// 来源。命中且非自然 20 / 暴击阈值时，按此概率独立掷骰升级为暴击。
        /// 不影响 d20 暴击阈值，避免与必中必暴规则冲突。
        /// </summary>
        public float BonusCritChance;

        public bool ForceHit;
        public bool ForceCritical;
        public bool SuppressCritical;
    }

    /// <summary>攻击检定结果</summary>
    public struct AttackRollResult
    {
        /// <summary>自然骰面</summary>
        public int NaturalRoll;

        /// <summary>总攻击值 = roll + bonus + mod</summary>
        public int TotalAttack;

        /// <summary>最终目标 AC（含掩体）</summary>
        public int FinalTargetAc;

        /// <summary>是否命中</summary>
        public bool IsHit;

        /// <summary>是否暴击</summary>
        public bool IsCritical;

        /// <summary>是否大失败</summary>
        public bool IsFumble;

        /// <summary>是否擦伤（差 ≤2 的未命中转为半伤命中）</summary>
        public bool IsGraze;

        /// <summary>命中率百分比（0~100）</summary>
        public int HitChancePercent;
    }

    /// <summary>伤害计算所需的全部输入参数</summary>
    public struct DamageInput
    {
        /// <summary>基础武器伤害（已掷骰）</summary>
        public int BaseDamage;

        /// <summary>是否擦伤（伤害减半）</summary>
        public bool IsGraze;

        /// <summary>是否暴击</summary>
        public bool IsCritical;

        /// <summary>暴击倍率（来自被动技能）</summary>
        public int CritMultiplier;

        /// <summary>暴击受伤减免倍率（防御方）</summary>
        public float CritDamageTakenMultiplier;

        /// <summary>偷袭额外伤害</summary>
        public int SneakDamage;

        /// <summary>被动近战伤害加成（固定值）</summary>
        public int PassiveMeleeBonus;

        /// <summary>被动近战伤害倍率</summary>
        public float PassiveMeleeMultiplier;

        /// <summary>是否为近战攻击</summary>
        public bool IsMelee;

        /// <summary>包夹伤害倍率（1.0 = 无包夹）</summary>
        public float FlankMultiplier;

        /// <summary>冲锋伤害倍率（1.0 = 无冲锋）</summary>
        public float ChargeMultiplier;

        /// <summary>骑乘加成</summary>
        public int MountBonus;

        /// <summary>被动伤害减免（防御方）</summary>
        public int DamageReduction;

        /// <summary>最终伤害倍率（如 AoO 0.5）</summary>
        public float FinalMultiplier;
    }

    /// <summary>伤害计算结果（穿甲前的最终伤害值）</summary>
    public struct DamageCalcResult
    {
        /// <summary>最终伤害（传给 BattleUnitModel.ApplyDamage）</summary>
        public int FinalDamage;

        /// <summary>被动减免量（供 UI 显示）</summary>
        public int DamageReductionApplied;
    }

    // ============================================================================
    // 攻击检定
    // ============================================================================

    /// <summary>
    /// 执行攻击检定 — 纯规则，不依赖任何 Node
    /// 掷骰 → 命中判定 → 暴击/大失败/擦伤
    /// </summary>
    public static AttackRollResult RollAttack(in AttackInput input)
    {
        var result = new AttackRollResult();

        // 优劣势互相抵消
        bool adv = input.HasAdvantage && !input.HasDisadvantage;
        bool dis = input.HasDisadvantage && !input.HasAdvantage;

        // 掷骰
        int roll;
        if (adv) roll = (int)RPGRuleEngine.RollWithAdvantage()["result"];
        else if (dis) roll = (int)RPGRuleEngine.RollWithDisadvantage()["result"];
        else roll = RPGRuleEngine.RollD20();

        result.NaturalRoll = roll;

        // 目标 AC（含掩体）
        int finalAc = input.TargetAc + input.CoverAcBonus;
        result.FinalTargetAc = finalAc;

        // 总攻击值
        int totalAttack = roll + input.AttackBonus + input.AccuracyMod;
        result.TotalAttack = totalAttack;

        // 命中率百分比
        float hitPct = RPGRuleEngine.CalculateHitChance(
            input.AttackBonus + input.AccuracyMod, finalAc, adv, dis);
        result.HitChancePercent = Mathf.RoundToInt(hitPct * 100.0f);

        // 暴击/大失败判定
        result.IsCritical = input.ForceCritical || (!input.SuppressCritical && roll >= input.CritThreshold);
        result.IsFumble = roll == 1;

        // 命中判定
        bool isHit = input.ForceHit || result.IsCritical || (!result.IsFumble && totalAttack >= finalAc);

        // 擦伤机制：差 ≤2 的未命中转为半伤命中
        if (!isHit && !result.IsFumble)
        {
            int missBy = finalAc - totalAttack;
            if (missBy <= 2)
            {
                result.IsGraze = true;
                isHit = true;
            }
        }

        result.IsHit = isHit;

        // v0.6 11.5: 节点 critical_rate 独立追加暴击概率。
        // 仅在已命中且尚未暴击时生效；不影响自然 20 必暴 / 自然 1 必失败规则。
        if (isHit && !result.IsCritical && !result.IsFumble && !input.SuppressCritical && input.BonusCritChance > 0f)
        {
            // 用 0..999 的整数桶映射 0..1，避免浮点 / 整数比较的边界踩雷。
            int threshold = (int)(input.BonusCritChance * 1000f);
            if (CombatRandom.RandRange(0, 999) < threshold)
                result.IsCritical = true;
        }

        return result;
    }

    // ============================================================================
    // 伤害计算管道
    // ============================================================================

    /// <summary>
    /// 计算最终伤害 — 纯规则，不依赖任何 Node
    /// 基础伤害 → 擦伤减半 → 暴击倍率 → 偷袭 → 被动加成 → 包夹 → 冲锋 → 骑乘 → 减免
    /// </summary>
    public static DamageCalcResult CalculateDamage(in DamageInput input)
    {
        // 全程用 float 累积，仅在末尾取整一次，避免多个乘区逐步截断造成的系统性偏低。
        // 运算顺序与语义保持不变：擦伤→暴击→偷袭→近战平加/倍率→包夹→冲锋→骑乘→减免→最终倍率。
        float damage = input.BaseDamage;

        // 擦伤减半
        if (input.IsGraze)
            damage = Math.Max(1f, damage / 2f);

        // 暴击
        if (input.IsCritical)
        {
            damage *= Math.Max(1, input.CritMultiplier);
            damage = Math.Max(1f, damage * input.CritDamageTakenMultiplier);
        }

        // 偷袭
        damage += input.SneakDamage;

        // 被动近战加成
        if (input.IsMelee)
        {
            damage += input.PassiveMeleeBonus;
            damage *= input.PassiveMeleeMultiplier;
        }

        // 包夹
        damage *= input.FlankMultiplier;

        // 冲锋
        damage *= input.ChargeMultiplier;

        // 骑乘
        damage += input.MountBonus;

        damage = Math.Max(1f, damage);

        // 被动伤害减免（在最终倍率之前结算，口径与旧版一致）
        int preReduction = Math.Max(1, (int)damage);
        int reductionApplied = Math.Min(preReduction - 1, input.DamageReduction);
        damage -= reductionApplied;
        damage = Math.Max(1f, damage);

        // 最终倍率（如 AoO 半伤）
        if (input.FinalMultiplier != 1.0f)
            damage *= input.FinalMultiplier;

        return new DamageCalcResult
        {
            FinalDamage = Math.Max(1, (int)damage),
            DamageReductionApplied = reductionApplied,
        };
    }

    // ============================================================================
    // 反击伤害
    // ============================================================================

    /// <summary>计算反击伤害（简化管道：平均武器伤害 × 方向倍率）</summary>
    public static int CalculateCounterDamage(int weaponDiceCount, int weaponDiceSides, int strMod, float directionMultiplier)
    {
        if (directionMultiplier <= 0.0f) return 0;
        int baseDmg = weaponDiceCount * (weaponDiceSides + 1) / 2 + strMod;
        return Math.Max(1, (int)(baseDmg * directionMultiplier));
    }

    // ============================================================================
    // 伤害预览
    // ============================================================================

    /// <summary>计算武器伤害预览（最小/最大/平均）</summary>
    public static (int min, int max, int avg) GetWeaponDamageRange(int diceCount, int diceSides, int statMod)
    {
        int min = Math.Max(1, diceCount + statMod);
        int max = Math.Max(1, diceCount * diceSides + statMod);
        int avg = Math.Max(1, diceCount * (diceSides + 1) / 2 + statMod);
        return (min, max, avg);
    }
}
