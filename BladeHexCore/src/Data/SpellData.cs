// SpellData.cs
// 法术数据 — 独立于SkillData的完整法术定义
// 对应策划案 07-法术系统.md
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class SpellData : Resource
{
    // ========================================
    // 法术体系枚举
    // ========================================

    /// <summary>八大法术学派</summary>
    public enum SpellSchool
    {
        Evocation,    // 塑能 — 直接伤害
        Abjuration,   // 防护 — 防御/减伤
        Illusion,     // 幻术 — 欺骗/干扰
        Necromancy,   // 死灵 — 生命操控
        Transmutation,// 变化 — 形态改变
        Enchantment,  // 附魔 — 心灵操控
        Divination,   // 预言 — 信息/辅助
        Conjuration,  // 咒唤 — 召唤/创造
    }

    /// <summary>法术环阶（0环=戏法，1-7环）</summary>
    public enum SpellTier
    {
        Cantrip, // 0环 — 戏法
        Tier1,   // 1环
        Tier2,   // 2环
        Tier3,   // 3环
        Tier4,   // 4环
        Tier5,   // 5环
        Tier6,   // 6环
        Tier7,   // 7环
    }

    /// <summary>法术范围形状</summary>
    public enum SpellShape
    {
        Single,  // 单体
        Ray,     // 射线
        Cone,    // 锥形
        Sphere,  // 球形
        Line,    // 线形
        Cross,   // 十字
        Self,    // 自身
        Touch,   // 触碰
    }

    /// <summary>豁免类型</summary>
    public enum SaveType
    {
        None,
        StrSave,
        DexSave,
        ConSave,
        IntSave,
        WisSave,
        ChaSave,
    }

    /// <summary>解析方式</summary>
    public enum ResolutionType
    {
        AttackRoll, // 法术攻击检定 vs AC
        Save,       // 目标豁免 vs 法术DC
        AutoHit,    // 自动命中
    }

    /// <summary>施放时机</summary>
    public enum CastingTime
    {
        MainAction,  // 主行动
        MinorAction, // 次要行动
        Reaction,    // 反应
    }

    // ========================================
    // 基础标识
    // ========================================

    [Export] public string SpellId { get; set; } = "";
    [Export] public string SpellName { get; set; } = "未命名法术";
    [Export] public string Description { get; set; } = "";
    [Export] public SpellSchool spellSchool = SpellSchool.Evocation;
    [Export] public SpellTier tier = SpellTier.Cantrip;

    // ========================================
    // 施放参数
    // ========================================

    [Export] public int ManaCost;
    [Export] public int CooldownTurns;
    [Export] public CastingTime castingTime = CastingTime.MainAction;

    // ========================================
    // 范围与形状
    // ========================================

    [Export] public int RangeCells { get; set; } = 6;
    [Export] public SpellShape shape = SpellShape.Single;
    [Export] public int ShapeSize { get; set; } = 1;

    // ========================================
    // 伤害与效果
    // ========================================

    [Export] public ResolutionType resolutionType = ResolutionType.Save;
    [Export] public SaveType saveType = SaveType.None;
    [Export] public int DamageDiceCount;
    [Export] public int DamageDiceSides;
    [Export] public string DamageType { get; set; } = "force";
    [Export] public int HealDiceCount;
    [Export] public int HealDiceSides;
    [Export] public int HealBonus;
    [Export] public string AppliedStatusEffect { get; set; } = "";
    [Export] public int StatusDuration;
    [Export] public string SpecialEffect { get; set; } = "";
    [Export] public int SummonHp;
    [Export] public int SummonDuration;

    // ========================================
    // 集中与持续
    // ========================================

    [Export] public bool IsConcentration;
    [Export] public int DurationTurns;

    // ========================================
    // UI
    // ========================================

    [Export] public string IconId { get; set; } = "";

    // ========================================
    // 辅助方法
    // ========================================

    public string GetTierName() => tier switch
    {
        SpellTier.Cantrip => "戏法",
        SpellTier.Tier1 => "1环",
        SpellTier.Tier2 => "2环",
        SpellTier.Tier3 => "3环",
        SpellTier.Tier4 => "4环",
        SpellTier.Tier5 => "5环",
        SpellTier.Tier6 => "6环",
        SpellTier.Tier7 => "7环",
        _ => "未知",
    };

    public string GetSchoolName() => spellSchool switch
    {
        SpellSchool.Evocation => "塑能",
        SpellSchool.Abjuration => "防护",
        SpellSchool.Illusion => "幻术",
        SpellSchool.Necromancy => "死灵",
        SpellSchool.Transmutation => "变化",
        SpellSchool.Enchantment => "附魔",
        SpellSchool.Divination => "预言",
        SpellSchool.Conjuration => "咒唤",
        _ => "未知",
    };

    public string GetShapeName() => shape switch
    {
        SpellShape.Single => "单体",
        SpellShape.Ray => "射线",
        SpellShape.Cone => "锥形",
        SpellShape.Sphere => "球形",
        SpellShape.Line => "线形",
        SpellShape.Cross => "十字",
        SpellShape.Self => "自身",
        SpellShape.Touch => "触碰",
        _ => "未知",
    };

    /// <summary>获取默认冷却（按环阶，策划案规定）</summary>
    public static int GetDefaultCooldown(SpellTier spellTier) => (int)spellTier;

    /// <summary>获取默认魔力消耗（按环阶）</summary>
    public static int GetDefaultManaCost(SpellTier spellTier) => spellTier switch
    {
        SpellTier.Cantrip => 0,
        SpellTier.Tier1 => 3,
        SpellTier.Tier2 => 5,
        SpellTier.Tier3 => 8,
        SpellTier.Tier4 => 12,
        SpellTier.Tier5 => 18,
        SpellTier.Tier6 => 25,
        SpellTier.Tier7 => 35,
        _ => 0,
    };
}
