// WeaponData.cs
// 武器数据，包含伤害骰子和武器特性
// 对应策划案 06-装备与物品.md → 武器系统
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class WeaponData : ItemData
{
    // ========================================
    // 枚举
    // ========================================

    public enum DamageType { Slash, Pierce, Crush, Magic, Fire, Frost, Lightning }
    public enum WeaponCategory { Simple, Martial, Exotic }
    public enum WeaponClass { Melee, Ranged }
    public enum WeightCategory { Light, Medium, Heavy }

    public enum WeaponSubtype 
    { 
        // --- 近战 Melee 3x3x3 ---
        // 砍伤 Slash (L/M/H)
        Dagger, Seax, Kukri,
        ArmingSword, BattleAxe, NomadSaber,
        Greatsword, GreatAxe, Glaive,
        // 刺伤 Pierce (L/M/H)
        Stiletto, SpikedDagger, Rapier,
        InfantrySpear, BroadSpear, Awlpike,
        Lance, Voulge, Trident,
        // 钝伤 Crush (L/M/H)
        Club, LightHammer, Cestus,
        WingedMace, MilitaryHammer, Flail,
        Maul, Greatclub, Polehammer,

        // --- 远程 Ranged 3x3x3 ---
        // 投掷 Thrown (L/M/H x 3)
        ThrowingKnife, Dart, Francisca,
        Javelin, Pilum, Harpoon,
        StoneThrow, HeavyJavelin, ThrowingHammer,
        // 弓 Bows (L/M/H x 3)
        Shortbow, HuntingBow, NomadBow,
        Strongbow, RecurveBow, WarBow,
        Longbow, CompositeLongbow, Greatbow,
        // 弩 Crossbows (L/M/H x 3)
        LightCrossbow, HuntingCrossbow, PistolCrossbow,
        StandardCrossbow, StrongCrossbow, SniperCrossbow,
        HeavyCrossbow, SiegeCrossbow, Ballista,

        Unarmed
    }

    // ========================================
    // 基础武器属性
    // ========================================

    [Export] public int DamageDiceCount { get; set; } = 1;
    [Export] public int DamageDiceSides { get; set; } = 8;
    [Export] public DamageType WeaponDamageType = DamageType.Slash;
    [Export] public WeaponCategory Category = WeaponCategory.Simple;
    [Export] public WeaponClass Class = WeaponClass.Melee;
    [Export] public new WeightCategory Weight = WeightCategory.Medium;
    [Export] public WeaponSubtype Subtype = WeaponSubtype.Unarmed;
    [Export] public int Tier { get; set; } = 1;

    // ========================================
    // 动态属性接口
    // ========================================

    public WeaponRegistry.WeaponConfig GetBaseConfig() => WeaponRegistry.GetConfig(Subtype);

    /// <summary>
    /// 获取当前武器的综合属性（包含 Tier 缩放）
    /// </summary>
    public WeaponRegistry.WeaponConfig GetCurrentConfig()
    {
        var cfg = GetBaseConfig();
        // Tier 1 是标准, 每级提升约 10-20% 性能或命中 (逻辑在 CombatManager 处理)
        // 此处返回基础配置，逻辑上可以根据 Tier 修改 config 副本
        return cfg;
    }

    [Export] public bool IsTwoHanded;
    [Export] public bool IsFinesse;           // 灵巧武器：可使用敏捷替代力量
    [Export] public bool IsRanged;            // 远程武器
    [Export] public int RangeCells { get; set; } = 1;       // 射程/触及范围
    [Export] public bool IsLongbow;           // 长弓：高AP消耗
    [Export] public bool IsCrossbow;          // 十字弩：高AP消耗 + 需要装填
    [Export] public bool IsThrowing;          // 投掷武器
    [Export] public int ThrowRange { get; set; } = 3;       // 投掷射程
    [Export] public int MaxAmmo { get; set; } = 0;          // 最大弹药/携带数 (针对投掷)
    [Export] public bool NeedsReload;         // 需要装填
    [Export] public int ApCost { get; set; } = 4;           // 攻击消耗 AP
    [Export] public int ReloadCost { get; set; } = 6;       // 装填消耗 AP
    [Export] public bool IsBlunt;             // 钝击伤害（对亡灵全额）
    [Export] public bool IsArmorPiercing;     // 破甲（计算命中时目标AC-2）
    [Export] public bool IsReach;             // 长柄（近战攻击范围2格）
    [Export] public bool IsAntiCavalry;       // 反骑兵（对冲锋目标伤害×2）
    [Export] public bool IsSweep;             // 横扫（攻击相邻2个敌人时各-2命中）
    [Export] public int StrRequired;          // 最低力量需求
    [Export] public bool IsCatalyst;          // 法术触媒（法杖/魔导书）
    [Export] public int SpellDcBonus;         // 法术DC加成（魔导书+1）
    [Export] public bool IsDualWieldable;     // 可双持（轻巧武器）

    // ========================================
    // 词缀加成（运行时累加）
    // ========================================

    public int BonusDamageDiceCount;
    public int BonusDamageDiceSides;
    public int BonusAttack;
    public int BonusDamage;
    public int BonusCritRange;
    public int BonusCritMultiplier;
    public Godot.Collections.Array<Godot.Collections.Dictionary> BonusConditionalEffects = new();

    // ========================================
    // 词缀应用
    // ========================================

    public override void ApplyAffix(EquipmentAffix affix)
    {
        base.ApplyAffix(affix);

        BonusAttack += affix.AttackBonus;
        BonusDamage += affix.DamageBonus;
        BonusDamageDiceCount += affix.DamageDiceCountBonus;
        BonusDamageDiceSides += affix.DamageDiceSidesBonus;
        BonusCritRange += affix.CritRangeBonus;
        BonusCritMultiplier += affix.CritMultiplierBonus;

        // 条件触发效果
        if (affix.Condition != "" && (affix.ConditionalDamageDiceCount > 0 || affix.ConditionalAttackBonus != 0))
        {
            BonusConditionalEffects.Add(new Godot.Collections.Dictionary
            {
                { "condition", affix.Condition },
                { "damage_dice_count", affix.ConditionalDamageDiceCount },
                { "damage_dice_sides", affix.ConditionalDamageDiceSides },
                { "damage_type", affix.ConditionalDamageType },
                { "attack_bonus", affix.ConditionalAttackBonus },
            });
        }

        // 特殊效果
        if (affix.SpecialEffect != "")
        {
            BonusConditionalEffects.Add(new Godot.Collections.Dictionary
            {
                { "condition", "special" },
                { "effect", affix.SpecialEffect },
                { "value", affix.SpecialValue },
            });
        }
    }

    // ========================================
    // 获取方法（含词缀加成）
    // ========================================

    public int GetTotalDamageDiceCount() => DamageDiceCount + BonusDamageDiceCount;
    public int GetTotalDamageDiceSides() => DamageDiceSides;
    public int GetTotalAttackBonus() => BonusAttack;
    public int GetTotalDamageBonus() => BonusDamage;
    public int GetCritRange() => 20 - BonusCritRange;
    public int GetCritMultiplier() => 2 + BonusCritMultiplier;
    public Godot.Collections.Array<Godot.Collections.Dictionary> GetConditionalEffects() => BonusConditionalEffects;

    /// <summary>获取完整武器描述（含词缀）</summary>
    public string GetWeaponDescription()
    {
        var parts = new System.Collections.Generic.List<string>();
        parts.Add($"伤害: {DamageDiceCount}d{DamageDiceSides}");
        if (BonusDamageDiceCount > 0)
            parts.Add($"+{BonusDamageDiceCount}d{BonusDamageDiceSides}");
        if (BonusDamage > 0)
            parts.Add($"+{BonusDamage}");
        if (BonusAttack > 0)
            parts.Add($"命中{BonusAttack:+#;-#;#}");

        var traits = new System.Collections.Generic.List<string>();
        if (IsTwoHanded) traits.Add("双手");
        if (IsFinesse) traits.Add("灵巧");
        if (IsRanged) traits.Add("远程");
        if (IsThrowing) traits.Add($"投掷({ThrowRange})");
        if (NeedsReload) traits.Add("装填");
        if (IsBlunt) traits.Add("钝击");
        if (IsArmorPiercing) traits.Add("破甲");
        if (IsReach) traits.Add("长柄");
        if (IsAntiCavalry) traits.Add("反骑");
        if (IsSweep) traits.Add("横扫");
        if (IsCatalyst) traits.Add("触媒");
        if (IsDualWieldable) traits.Add("双持");
        if (traits.Count > 0)
            parts.Add("特性: " + string.Join("/", traits));

        return string.Join(" ", parts);
    }
}
