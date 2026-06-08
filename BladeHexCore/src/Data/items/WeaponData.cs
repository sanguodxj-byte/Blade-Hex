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

        // --- 法术媒介 Catalyst (法师装备，支持 Spell 施放) ---
        Wand, Orb, Staff,

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

    // v0.7 备注：旧版 WeaponPen 字段已彻底移除。穿透完全由 (DamageType × WeaponWeight)
    // 分流表（DamagePenetrationTable）按伤害类型差异化驱动，不在单件武器上加可调修正。
    // 公式：穿透判定 = d20_Pen + STRPenBonus ≥ ArmorDR；伤害分流走查表。

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

    [Export] public int RangeCells { get; set; } = 1;       // 射程/触及范围
    [Export] public int ThrowRange { get; set; } = 3;       // 投掷射程
    [Export] public int MaxAmmo { get; set; } = 0;          // 最大弹药/携带数 (0=无限)
    [Export] public int CurrentAmmo { get; set; } = 0;      // 当前弹药数 (运行时)
    [Export] public int ApCost { get; set; } = 4;           // 攻击消耗 AP
    [Export] public int ReloadCost { get; set; } = 6;       // 装填消耗 AP
    [Export] public int StrRequired;                        // 最低力量需求
    [Export] public int SpellDcBonus;                       // 法术DC加成（魔导书+1）

    /// <summary>武器特性 Flags — 取代 13 个独立 bool 字段</summary>
    [Export] public WeaponTraits Traits = WeaponTraits.None;

    // ========================================
    // 兼容性 Bool 属性（从 Traits 派生，旧代码无需修改）
    // ========================================

    public bool IsTwoHanded
    {
        get => Traits.Has(WeaponTraits.TwoHanded);
        set => Traits = value ? Traits.With(WeaponTraits.TwoHanded) : Traits.Without(WeaponTraits.TwoHanded);
    }
    public bool IsFinesse
    {
        get => Traits.Has(WeaponTraits.Finesse);
        set => Traits = value ? Traits.With(WeaponTraits.Finesse) : Traits.Without(WeaponTraits.Finesse);
    }
    public bool IsRanged
    {
        get => Class == WeaponClass.Ranged || Traits.Has(WeaponTraits.Ranged);
        set => Traits = value ? Traits.With(WeaponTraits.Ranged) : Traits.Without(WeaponTraits.Ranged);
    }
    public bool IsLongbow
    {
        get => Traits.Has(WeaponTraits.Longbow);
        set => Traits = value ? Traits.With(WeaponTraits.Longbow) : Traits.Without(WeaponTraits.Longbow);
    }
    public bool IsCrossbow
    {
        get => Traits.Has(WeaponTraits.Crossbow);
        set => Traits = value ? Traits.With(WeaponTraits.Crossbow) : Traits.Without(WeaponTraits.Crossbow);
    }
    public bool IsThrowing
    {
        get => Traits.Has(WeaponTraits.Throwing);
        set => Traits = value ? Traits.With(WeaponTraits.Throwing) : Traits.Without(WeaponTraits.Throwing);
    }
    public bool NeedsReload
    {
        get => Traits.Has(WeaponTraits.NeedsReload);
        set => Traits = value ? Traits.With(WeaponTraits.NeedsReload) : Traits.Without(WeaponTraits.NeedsReload);
    }
    public bool IsBlunt
    {
        get => Traits.Has(WeaponTraits.Blunt);
        set => Traits = value ? Traits.With(WeaponTraits.Blunt) : Traits.Without(WeaponTraits.Blunt);
    }
    public bool IsArmorPiercing
    {
        get => Traits.Has(WeaponTraits.ArmorPiercing);
        set => Traits = value ? Traits.With(WeaponTraits.ArmorPiercing) : Traits.Without(WeaponTraits.ArmorPiercing);
    }
    public bool IsReach
    {
        get => Traits.Has(WeaponTraits.Reach);
        set => Traits = value ? Traits.With(WeaponTraits.Reach) : Traits.Without(WeaponTraits.Reach);
    }
    public bool IsAntiCavalry
    {
        get => Traits.Has(WeaponTraits.AntiCavalry);
        set => Traits = value ? Traits.With(WeaponTraits.AntiCavalry) : Traits.Without(WeaponTraits.AntiCavalry);
    }
    public bool IsSweep
    {
        get => Traits.Has(WeaponTraits.Sweep);
        set => Traits = value ? Traits.With(WeaponTraits.Sweep) : Traits.Without(WeaponTraits.Sweep);
    }
    public bool IsCatalyst
    {
        get => Traits.Has(WeaponTraits.Catalyst);
        set => Traits = value ? Traits.With(WeaponTraits.Catalyst) : Traits.Without(WeaponTraits.Catalyst);
    }
    public bool IsDualWieldable
    {
        get => Traits.Has(WeaponTraits.DualWieldable);
        set => Traits = value ? Traits.With(WeaponTraits.DualWieldable) : Traits.Without(WeaponTraits.DualWieldable);
    }

    // ========================================
    // 投射物类型推断
    // ========================================

    /// <summary>
    /// 根据武器子类型和伤害类型推断投射物视觉类型。
    /// 仅对远程/投掷武器有意义；近战武器返回空字符串。
    /// </summary>
    public string GetProjectileType()
    {
        if (!IsRanged && !IsThrowing) return "";

        // 投掷武器
        if (IsThrowing)
        {
            return Subtype switch
            {
                WeaponSubtype.ThrowingKnife or WeaponSubtype.Dart or WeaponSubtype.Stiletto => "throwing_knife",
                WeaponSubtype.Francisca or WeaponSubtype.ThrowingHammer => "throwing_axe",
                _ => "throwing_knife",
            };
        }

        // 弩类
        if (IsCrossbow) return "crossbow_bolt";

        // 法术媒介
        if (IsCatalyst)
        {
            return WeaponDamageType switch
            {
                DamageType.Fire => "fireball",
                DamageType.Frost => "ice_shard",
                DamageType.Lightning => "lightning",
                _ => "magic_bolt",
            };
        }

        // 弓类 / 默认远程
        return "arrow";
    }

    /// <summary>
    /// 投射物飞行速度（格/秒）。由武器类型决定。
    /// 弩箭快而平，弓箭中速，投掷较慢，法术中速。
    /// </summary>
    public float GetProjectileSpeed()
    {
        if (!IsRanged && !IsThrowing) return 0f;
        if (IsCrossbow) return 14.0f;
        if (IsThrowing) return 8.0f;
        if (IsCatalyst) return 9.0f;
        return 10.0f; // 弓
    }

    /// <summary>
    /// 投射物抛物线弧高。由武器类型决定。
    /// 弩箭低平，弓箭高弧，投掷中弧，法术几乎直线。
    /// </summary>
    public float GetProjectileArcHeight()
    {
        if (!IsRanged && !IsThrowing) return 0f;
        if (IsCrossbow) return 0.5f;
        if (IsThrowing) return 1.2f;
        if (IsCatalyst) return 0.3f;
        return 1.5f; // 弓
    }

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

    // ========================================
    // 弹药系统
    // ========================================

    /// <summary>该武器是否需要弹药（弓/弩/投掷）</summary>
    public bool NeedsAmmo => IsRanged && (IsBow || IsCrossbow || IsThrowing);

    /// <summary>是否为弓类武器</summary>
    public bool IsBow => !IsCrossbow && !IsThrowing && IsRanged && !IsCatalyst;

    /// <summary>获取该武器类型的默认最大弹药量</summary>
    public int GetDefaultMaxAmmo()
    {
        if (IsThrowing) return 5;
        if (IsBow) return 20;
        if (IsCrossbow) return 18;
        return 0;
    }

    /// <summary>获取装备箭筒后的最大弹药量（箭筒将弹药提升至固定值）</summary>
    public int GetMaxAmmoWithQuiver()
    {
        if (IsBow) return 30;
        if (IsCrossbow) return 24;
        return GetDefaultMaxAmmo();
    }

    /// <summary>初始化弹药（hasQuiver=true 时使用箭筒提升后的弹药量）</summary>
    public void InitializeAmmo(bool hasQuiver = false)
    {
        if (!NeedsAmmo) return;
        MaxAmmo = hasQuiver ? GetMaxAmmoWithQuiver() : GetDefaultMaxAmmo();
        CurrentAmmo = MaxAmmo;
    }

    /// <summary>消耗一发弹药，返回是否成功</summary>
    public bool ConsumeAmmo()
    {
        if (!NeedsAmmo) return true;
        if (MaxAmmo <= 0) return true; // 未初始化弹药 = 无限
        if (CurrentAmmo <= 0) return false;
        CurrentAmmo--;
        return true;
    }

    /// <summary>是否还有弹药可用</summary>
    public bool HasAmmo => !NeedsAmmo || MaxAmmo <= 0 || CurrentAmmo > 0;

    /// <summary>获取完整武器描述（含词缀）</summary>
    public string GetWeaponDescription()
    {
        var parts = new System.Collections.Generic.List<string>();
        // 显示实际伤害区间，避免出现骰子记号
        int min = DamageDiceCount;
        int max = DamageDiceCount * DamageDiceSides;
        parts.Add($"伤害: {min}-{max}");
        if (BonusDamageDiceCount > 0)
        {
            int bMin = BonusDamageDiceCount;
            int bMax = BonusDamageDiceCount * BonusDamageDiceSides;
            parts.Add($"+{bMin}-{bMax}");
        }
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
