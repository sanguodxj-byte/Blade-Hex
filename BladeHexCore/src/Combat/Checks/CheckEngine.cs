// CheckEngine.cs
// 检定系统 — 统一的技能检定 API
// T15: Skill Checks system
using Godot;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.Combat.Checks;

/// <summary>
/// 检定类型 — 对应 D&D 5e 的属性检定
/// </summary>
public enum CheckType
{
    // 力量
    StrAthletics,       // 运动

    // 敏捷
    DexAcrobatics,      // 体操
    DexSleightOfHand,   // 巧手
    DexStealth,         // 隐匿

    // 智力
    IntArcana,          // 奥术
    IntHistory,         // 历史
    IntInvestigation,   // 调查
    IntNature,          // 自然
    IntReligion,        // 宗教

    // 感知
    WisAnimalHandling,  // 驯兽
    WisInsight,         // 洞察
    WisMedicine,        // 医药
    WisPerception,      // 察觉
    WisSurvival,        // 生存

    // 魅力
    ChaDeception,       // 欺骗
    ChaIntimidation,    // 恐吓
    ChaPerformance,     // 表演
    ChaPersuasion,      // 说服
}

/// <summary>
/// 检定结果
/// </summary>
public record CheckResult(
    bool Success,
    int Roll,
    int Modifier,
    int Total,
    int DC,
    string Description
);

/// <summary>
/// 检定引擎 — 统一的技能检定 API
/// </summary>
public static class CheckEngine
{
    /// <summary>进行检定</summary>
    public static CheckResult Make(UnitData unit, CheckType checkType, int dc)
    {
        int modifier = GetModifier(unit, checkType);
        int roll = CombatRandom.RollD20();
        int total = roll + modifier;

        bool success = total >= dc;

        string description = success
            ? $"检定成功！({roll} + {modifier} = {total} ≥ {dc})"
            : $"检定失败！({roll} + {modifier} = {total} < {dc})";

        return new CheckResult(success, roll, modifier, total, dc, description);
    }

    /// <summary>进行检定（带优势）</summary>
    public static CheckResult MakeWithAdvantage(UnitData unit, CheckType checkType, int dc)
    {
        int roll1 = CombatRandom.RollD20();
        int roll2 = CombatRandom.RollD20();
        int roll = Mathf.Max(roll1, roll2);
        int modifier = GetModifier(unit, checkType);
        int total = roll + modifier;

        bool success = total >= dc;

        string description = success
            ? $"检定成功！({roll1}/{roll2} 取高 {roll} + {modifier} = {total} ≥ {dc})"
            : $"检定失败！({roll1}/{roll2} 取高 {roll} + {modifier} = {total} < {dc})";

        return new CheckResult(success, roll, modifier, total, dc, description);
    }

    /// <summary>进行检定（带劣势）</summary>
    public static CheckResult MakeWithDisadvantage(UnitData unit, CheckType checkType, int dc)
    {
        int roll1 = CombatRandom.RollD20();
        int roll2 = CombatRandom.RollD20();
        int roll = Mathf.Min(roll1, roll2);
        int modifier = GetModifier(unit, checkType);
        int total = roll + modifier;

        bool success = total >= dc;

        string description = success
            ? $"检定成功！({roll1}/{roll2} 取低 {roll} + {modifier} = {total} ≥ {dc})"
            : $"检定失败！({roll1}/{roll2} 取低 {roll} + {modifier} = {total} < {dc})";

        return new CheckResult(success, roll, modifier, total, dc, description);
    }

    /// <summary>获取检定修正值</summary>
    public static int GetModifier(UnitData unit, CheckType checkType)
    {
        // 基础属性修正
        int attrMod = checkType switch
        {
            CheckType.StrAthletics => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveStr(unit)),

            CheckType.DexAcrobatics => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveDex(unit)),
            CheckType.DexSleightOfHand => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveDex(unit)),
            CheckType.DexStealth => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveDex(unit)),

            CheckType.IntArcana => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(unit)),
            CheckType.IntHistory => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(unit)),
            CheckType.IntInvestigation => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(unit)),
            CheckType.IntNature => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(unit)),
            CheckType.IntReligion => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveInt(unit)),

            CheckType.WisAnimalHandling => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(unit)),
            CheckType.WisInsight => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(unit)),
            CheckType.WisMedicine => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(unit)),
            CheckType.WisPerception => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(unit)),
            CheckType.WisSurvival => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveWis(unit)),

            CheckType.ChaDeception => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveCha(unit)),
            CheckType.ChaIntimidation => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveCha(unit)),
            CheckType.ChaPerformance => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveCha(unit)),
            CheckType.ChaPersuasion => RPGRuleEngine.GetStatModifier(CombatStats.GetEffectiveCha(unit)),

            _ => 0,
        };

        // 熟练加值（如果单位有相关熟练）
        // 当前未实装熟练系统，预留接口
        // int proficiency = HasProficiency(unit, checkType) ? RPGRuleEngine.GetProficiencyBonus(unit.Level) : 0;

        return attrMod;
    }

    /// <summary>获取检定类型的中文名</summary>
    public static string GetCheckTypeName(CheckType checkType) => checkType switch
    {
        CheckType.StrAthletics => "运动",

        CheckType.DexAcrobatics => "体操",
        CheckType.DexSleightOfHand => "巧手",
        CheckType.DexStealth => "隐匿",

        CheckType.IntArcana => "奥术",
        CheckType.IntHistory => "历史",
        CheckType.IntInvestigation => "调查",
        CheckType.IntNature => "自然",
        CheckType.IntReligion => "宗教",

        CheckType.WisAnimalHandling => "驯兽",
        CheckType.WisInsight => "洞察",
        CheckType.WisMedicine => "医药",
        CheckType.WisPerception => "察觉",
        CheckType.WisSurvival => "生存",

        CheckType.ChaDeception => "欺骗",
        CheckType.ChaIntimidation => "恐吓",
        CheckType.ChaPerformance => "表演",
        CheckType.ChaPersuasion => "说服",

        _ => "未知",
    };

    /// <summary>获取检定类型对应的属性</summary>
    public static string GetCheckTypeAttribute(CheckType checkType) => checkType switch
    {
        CheckType.StrAthletics => "str",

        CheckType.DexAcrobatics or CheckType.DexSleightOfHand or CheckType.DexStealth => "dex",

        CheckType.IntArcana or CheckType.IntHistory or CheckType.IntInvestigation or
        CheckType.IntNature or CheckType.IntReligion => "intel",

        CheckType.WisAnimalHandling or CheckType.WisInsight or CheckType.WisMedicine or
        CheckType.WisPerception or CheckType.WisSurvival => "wis",

        CheckType.ChaDeception or CheckType.ChaIntimidation or CheckType.ChaPerformance or
        CheckType.ChaPersuasion => "cha",

        _ => "str",
    };
}
