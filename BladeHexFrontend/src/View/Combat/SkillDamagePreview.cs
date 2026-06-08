// SkillDamagePreview.cs
// 技能伤害/治疗预览计算器 — 根据 skill_configs.json 的 scaling 配置计算技能效果范围
// 数据来源：SkillRegistry (skill_configs.json)
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// 技能伤害/治疗预览计算器。
/// 根据施法者属性和技能配置，计算技能的预期伤害/治疗范围。
/// </summary>
public static class SkillDamagePreview
{
    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>
    /// 技能预览结果
    /// </summary>
    public readonly struct SkillPreviewResult
    {
        /// <summary>是否为治疗技能</summary>
        public bool IsHeal { get; init; }
        /// <summary>最小值</summary>
        public int MinValue { get; init; }
        /// <summary>最大值</summary>
        public int MaxValue { get; init; }
        /// <summary>骰子描述（如 "2d8"）</summary>
        public string DiceDesc { get; init; }
        /// <summary>属性加成描述（如 "+INT"）</summary>
        public string StatDesc { get; init; }
        /// <summary>是否有技能缩放配置</summary>
        public bool HasScaling { get; init; }
    }

    /// <summary>
    /// 计算技能的伤害/治疗预览。
    /// </summary>
    /// <param name="skillId">技能 ID（如 "arcane_burst"）</param>
    /// <param name="caster">施法者</param>
    /// <returns>预览结果，包含最小/最大值和描述</returns>
    public static SkillPreviewResult Calculate(string skillId, Unit caster)
    {
        if (caster?.Data == null || string.IsNullOrEmpty(skillId))
            return default;

        // 从 SkillRegistry 获取缩放配置（来源：skill_configs.json）
        var scaling = SkillRegistry.GetScaling(skillId);
        if (scaling == null)
            return default;

        var s = scaling.Value;
        int level = caster.Data.Level;
        int diceCount = Mathf.Max(1, level / 4);

        // 获取属性修正
        int statMod = GetStatMod(caster, s.Stat);
        int bonus = Mathf.FloorToInt(statMod * s.StatMult);

        // 计算 min/max
        int minVal = Mathf.Max(1, diceCount * 1 + bonus);
        int maxVal = Mathf.Max(1, diceCount * s.Sides + bonus);

        // 构造描述
        string statName = s.Stat switch
        {
            "intel" => "INT",
            "wis"   => "WIS",
            "dex"   => "DEX",
            "str"   => "STR",
            "con"   => "CON",
            _       => s.Stat.ToUpper()
        };
        string statDesc = s.StatMult != 1.0f
            ? $"{statName}x{s.StatMult}"
            : statName;

        return new SkillPreviewResult
        {
            IsHeal = IsHealSkill(skillId),
            MinValue = minVal,
            MaxValue = maxVal,
            DiceDesc = $"{diceCount}d{s.Sides}",
            StatDesc = statDesc,
            HasScaling = true,
        };
    }

    /// <summary>
    /// 获取技能的伤害/治疗类型描述
    /// </summary>
    public static string GetEffectType(string skillId)
    {
        if (IsHealSkill(skillId)) return "治疗";
        if (IsBuffSkill(skillId)) return "增益";
        if (IsDebuffSkill(skillId)) return "减益";
        return "伤害";
    }

    /// <summary>
    /// 获取技能的伤害/治疗颜色
    /// </summary>
    public static Color GetEffectColor(string skillId)
    {
        if (IsHealSkill(skillId)) return new Color(0.25f, 0.85f, 0.45f);   // 绿
        if (IsBuffSkill(skillId)) return new Color(0.3f, 0.7f, 0.95f);    // 蓝
        if (IsDebuffSkill(skillId)) return new Color(0.9f, 0.5f, 0.2f);   // 橙
        return new Color(0.95f, 0.35f, 0.25f);                             // 红
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    private static int GetStatMod(Unit unit, string stat)
    {
        if (unit?.Data == null) return 0;
        int score = stat switch
        {
            "intel" => CombatStats.GetEffectiveInt(unit.Data),
            "wis"   => CombatStats.GetEffectiveWis(unit.Data),
            "dex"   => CombatStats.GetEffectiveDex(unit.Data),
            "str"   => CombatStats.GetEffectiveStr(unit.Data),
            "con"   => CombatStats.GetEffectiveCon(unit.Data),
            _       => 10
        };
        return RPGRuleEngine.GetStatModifier(score);
    }

    private static bool IsHealSkill(string skillId)
    {
        return skillId.Contains("heal") || skillId.Contains("cure")
            || skillId.Contains("medic") || skillId.Contains("life_circle");
    }

    private static bool IsBuffSkill(string skillId)
    {
        return skillId.Contains("buff") || skillId.Contains("bless")
            || skillId.Contains("inspire") || skillId.Contains("rally")
            || skillId.Contains("war_cry") || skillId.Contains("guardian")
            || skillId.Contains("shield") || skillId.Contains("bulwark");
    }

    private static bool IsDebuffSkill(string skillId)
    {
        return skillId.Contains("debuff") || skillId.Contains("blind")
            || skillId.Contains("stun") || skillId.Contains("intimidate")
            || skillId.Contains("taunt") || skillId.Contains("poison");
    }
}
