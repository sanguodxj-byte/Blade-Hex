// CareerSkillData.cs
// 职业专属技能数据模型 — 每个职业称号对应一个独特技能
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 职业专属技能数据 — 描述由职业称号触发的技能
/// </summary>
[GlobalClass]
public partial class CareerSkillData : RefCounted
{
    public enum SkillType { Active, Passive }
    public enum UsageLimit { Unlimited, OncePerBattle, OncePerTurn, PerBattleCount }

    // ========================================
    // 标识
    // ========================================

    /// <summary>技能内部ID（如 "warrior_armor_break"）</summary>
    public string SkillId = "";

    /// <summary>显示名称</summary>
    public string DisplayName = "";

    /// <summary>英文名称</summary>
    public string EnglishName = "";

    /// <summary>对应职业称号的 flags 组合（来自 ClassTitleResolver）</summary>
    public int RequiredTitleFlags = 0;

    // ========================================
    // 技能类型与消耗
    // ========================================

    public SkillType Type = SkillType.Active;

    /// <summary>AP 消耗（被动为0）</summary>
    public int ApCost = 0;

    /// <summary>使用次数限制类型</summary>
    public UsageLimit LimitType = UsageLimit.Unlimited;

    /// <summary>每场战斗/每回合可用次数（LimitType 为 PerBattleCount/OncePerTurn 时有效）</summary>
    public int MaxUses = 1;

    /// <summary>冷却回合数（0=无冷却）</summary>
    public int Cooldown = 0;

    // ========================================
    // 效果描述
    // ========================================

    /// <summary>技能效果标识符，供 CareerSkillExecutor 分发执行</summary>
    public string EffectId = "";

    /// <summary>技能描述文本</summary>
    public string Description = "";

    /// <summary>效果参数（供执行器读取的具体数值）</summary>
    public Godot.Collections.Dictionary EffectParams = new();

    // ========================================
    // 辅助方法
    // ========================================

    public bool IsActive => Type == SkillType.Active;
    public bool IsPassive => Type == SkillType.Passive;
    public bool IsOncePerBattle => LimitType == UsageLimit.OncePerBattle;
}
