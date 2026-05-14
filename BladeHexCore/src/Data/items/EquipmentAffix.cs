// EquipmentAffix.cs
// 装备词缀系统 — 前缀/后缀词缀，用于动态生成装备属性变化
// 对应策划案 06-装备与物品.md → 装备词缀系统
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class EquipmentAffix : Resource
{
    // ========================================
    // 词缀类型枚举
    // ========================================

    /// <summary>词缀位置（前缀修饰名称前部，后缀修饰名称后部）</summary>
    public enum AffixPosition
    {
        Prefix,  // 前缀：如"烈焰"长剑
        Suffix,  // 后缀：如长剑"锋利"
    }

    /// <summary>词缀作用目标（词缀影响哪类装备）</summary>
    public enum AffixTarget
    {
        Any,       // 任意装备
        Weapon,    // 仅武器
        Armor,     // 仅防具
        Shield,    // 仅盾牌
        Accessory, // 仅饰品
    }

    /// <summary>词缀效果类型</summary>
    public enum AffixEffectType
    {
        FlatStat,     // 固定属性加成（如 STR+2）
        DiceBonus,    // 骰子加成（如 伤害+1d4）
        PercentStat,  // 百分比属性加成（如 伤害+10%）
        Conditional,  // 条件触发（如 对亡灵伤害+1d6）
        Special,      // 特殊效果（由运行时处理）
    }

    // ========================================
    // 基础字段
    // ========================================

    [Export] public string AffixId { get; set; } = "";
    [Export] public string AffixName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public bool IsPrefix { get; set; } = true;
    [Export] public AffixTarget Target = AffixTarget.Any;
    [Export] public AffixEffectType EffectType = AffixEffectType.FlatStat;
    [Export] public int MinItemLevel { get; set; } = 1;
    [Export] public int MaxItemLevel { get; set; } = 20;
    [Export] public float Weight { get; set; } = 1.0f;
    [Export] public int MinRarity { get; set; } = 0; // ItemData.Rarity.COMMON

    // ========================================
    // 固定属性加成 (FlatStat)
    // ========================================

    [Export] public int StrBonus;
    [Export] public int DexBonus;
    [Export] public int ConBonus;
    [Export] public int IntBonus;
    [Export] public int WisBonus;
    [Export] public int ChaBonus;
    [Export] public int HpBonus;
    [Export] public int AcBonus;
    [Export] public int MoveBonus;
    [Export] public int InitiativeBonus;

    // ========================================
    // 武器专属加成
    // ========================================

    [Export] public int DamageDiceCountBonus;
    [Export] public int DamageDiceSidesBonus;
    [Export] public int AttackBonus;
    [Export] public int DamageBonus;
    [Export] public int CritRangeBonus;
    [Export] public int CritMultiplierBonus;

    // ========================================
    // 防具专属加成
    // ========================================

    [Export] public string Resistance { get; set; } = "";
    [Export] public string Immunity { get; set; } = "";

    // ========================================
    // 条件触发效果 (Conditional)
    // ========================================

    [Export] public string Condition { get; set; } = "";
    [Export] public int ConditionalDamageDiceCount;
    [Export] public int ConditionalDamageDiceSides;
    [Export] public string ConditionalDamageType { get; set; } = "";
    [Export] public int ConditionalAttackBonus;

    // ========================================
    // 特殊效果 (Special)
    // ========================================

    [Export] public string SpecialEffect { get; set; } = "";
    [Export] public float SpecialValue;

    // ========================================
    // 方法
    // ========================================

    /// <summary>获取词缀效果描述文本</summary>
    public string GetEffectDescription()
    {
        var parts = new System.Collections.Generic.List<string>();

        // 属性加成
        if (StrBonus != 0) parts.Add($"力量{StrBonus:+#;-#;#}");
        if (DexBonus != 0) parts.Add($"敏捷{DexBonus:+#;-#;#}");
        if (ConBonus != 0) parts.Add($"体质{ConBonus:+#;-#;#}");
        if (IntBonus != 0) parts.Add($"智力{IntBonus:+#;-#;#}");
        if (WisBonus != 0) parts.Add($"感知{WisBonus:+#;-#;#}");
        if (ChaBonus != 0) parts.Add($"魅力{ChaBonus:+#;-#;#}");
        if (HpBonus != 0) parts.Add($"HP{HpBonus:+#;-#;#}");
        if (AcBonus != 0) parts.Add($"AC{AcBonus:+#;-#;#}");
        if (MoveBonus != 0) parts.Add($"移动{MoveBonus:+#;-#;#}");
        if (InitiativeBonus != 0) parts.Add($"先攻{InitiativeBonus:+#;-#;#}");

        // 武器加成
        if (AttackBonus != 0) parts.Add($"命中{AttackBonus:+#;-#;#}");
        if (DamageBonus != 0) parts.Add($"伤害{DamageBonus:+#;-#;#}");
        if (DamageDiceCountBonus > 0 && DamageDiceSidesBonus > 0)
            parts.Add($"+{DamageDiceCountBonus}d{DamageDiceSidesBonus}伤害");
        if (CritRangeBonus != 0) parts.Add($"暴击范围{CritRangeBonus:+#;-#;#}");
        if (CritMultiplierBonus != 0) parts.Add($"暴击倍率{CritMultiplierBonus:+#;-#;#}");

        // 抗性/免疫
        if (Resistance != "") parts.Add($"{Resistance}抗性");
        if (Immunity != "") parts.Add($"免疫{Immunity}");

        // 条件触发
        if (Condition != "")
        {
            var condText = ConditionText(Condition);
            if (ConditionalDamageDiceCount > 0)
                parts.Add($"{condText}:+{ConditionalDamageDiceCount}d{ConditionalDamageDiceSides}{ConditionalDamageType}伤害");
            if (ConditionalAttackBonus != 0)
                parts.Add($"{condText}:命中{ConditionalAttackBonus:+#;-#;#}");
        }

        // 特殊
        if (SpecialEffect != "")
            parts.Add(SpecialText(SpecialEffect, SpecialValue));

        return string.Join("，", parts);
    }

    private static string ConditionText(string cond) => cond switch
    {
        "vs_undead" => "对亡灵",
        "vs_cavalry" => "对骑兵",
        "vs_beast" => "对野兽",
        "vs_demon" => "对魔物",
        "low_hp" => "低HP时",
        "mounted" => "骑乘时",
        "first_attack" => "首次攻击",
        "flanking" => "包夹时",
        "high_ground" => "高地时",
        _ => cond,
    };

    private static string SpecialText(string effect, float value) => effect switch
    {
        "life_steal" => $"攻击回复{value * 100:F0}%伤害HP",
        "thorns" => $"被击时反弹{value:F0}伤害",
        "cleave" => $"击杀时对邻敌造成{value:F0}伤害",
        "chain_lightning" => $"命中时跳跃{value:F0}个目标",
        "on_crit_effect" => "暴击时触发额外效果",
        "on_kill_reset" => "击杀时重置行动",
        "extra_attack" => $"额外攻击{value:F0}次",
        _ => effect,
    };

    // ========================================
    // 静态工厂：预定义词缀库
    // ========================================

    public static EquipmentAffix[] GetPrefixAffixes() =>
    [
        MakePrefix("flaming", "烈焰", "火焰伤害加成", AffixTarget.Weapon, 3, 0.8f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, "fire"),
        MakePrefix("frost", "寒冰", "冰冷伤害加成", AffixTarget.Weapon, 3, 0.8f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, "cold"),
        MakePrefix("shocking", "电弧", "闪电伤害加成", AffixTarget.Weapon, 5, 0.6f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 0, 0, "lightning"),
        MakePrefix("brutal", "残暴", "近战伤害加成", AffixTarget.Weapon, 1, 1.0f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, ""),
        MakePrefix("precise", "精准", "命中加成", AffixTarget.Weapon, 1, 1.0f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""),
        MakePrefix("sturdy", "坚固", "防具AC加成", AffixTarget.Armor, 1, 1.0f,
            0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, ""),
        MakePrefix("agile", "敏捷", "防具敏捷加成", AffixTarget.Armor, 1, 0.8f,
            0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""),
        MakePrefix("mighty", "力量", "力量加成", AffixTarget.Any, 1, 1.0f,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""),
        MakePrefix("wise", "智慧", "智力加成", AffixTarget.Any, 1, 1.0f,
            0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""),
        MakePrefix("holy", "净化", "对亡灵伤害加成", AffixTarget.Weapon, 5, 0.5f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "",
            "vs_undead", 1, 6, "arcane"),
        MakePrefix("cavalry_slayer", "骑杀", "对骑兵伤害加成", AffixTarget.Weapon, 3, 0.5f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "",
            "vs_cavalry", 1, 8, "pierce"),
        MakePrefix("vital", "生命", "HP加成", AffixTarget.Any, 1, 1.0f,
            0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, ""),
        MakePrefix("swift", "迅捷", "移动加成", AffixTarget.Any, 1, 0.7f,
            0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, ""),
    ];

    public static EquipmentAffix[] GetSuffixAffixes() =>
    [
        MakeSuffix("of_power", "力量", "力量加成", AffixTarget.Any, 1, 1.0f,
            2, 0, 0, 0, 0, 0, 0, 0, 0),
        MakeSuffix("of_agility", "灵巧", "敏捷加成", AffixTarget.Any, 1, 1.0f,
            0, 2, 0, 0, 0, 0, 0, 0, 0),
        MakeSuffix("of_vitality", "活力", "体质加成", AffixTarget.Any, 1, 1.0f,
            0, 0, 2, 0, 0, 0, 0, 0, 0),
        MakeSuffix("of_intellect", "睿智", "智力加成", AffixTarget.Any, 1, 1.0f,
            0, 0, 0, 2, 0, 0, 0, 0, 0),
        MakeSuffix("of_wisdom", "洞察", "感知加成", AffixTarget.Any, 1, 1.0f,
            0, 0, 0, 0, 2, 0, 0, 0, 0),
        MakeSuffix("of_command", "统帅", "魅力加成", AffixTarget.Any, 1, 0.8f,
            0, 0, 0, 0, 0, 2, 0, 0, 0),
        MakeSuffix("of_the_bear", "巨熊", "力量+体质加成", AffixTarget.Armor, 3, 0.5f,
            1, 0, 1, 0, 0, 0, 0, 0, 0),
        MakeSuffix("of_the_eagle", "苍鹰", "敏捷+感知加成", AffixTarget.Armor, 3, 0.5f,
            0, 1, 0, 0, 1, 0, 0, 0, 0),
        MakeSuffix("of_fire_resist", "耐火", "火焰抗性", AffixTarget.Armor, 3, 0.6f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, "fire"),
        MakeSuffix("of_cold_resist", "耐寒", "冰冷抗性", AffixTarget.Armor, 3, 0.6f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, "cold"),
        MakeSuffix("of_sharpness", "锋利", "武器伤害加成", AffixTarget.Weapon, 1, 1.0f,
            0, 0, 0, 0, 0, 0, 0, 1, 0),
        MakeSuffix("of_smiting", "猛击", "武器伤害骰加成", AffixTarget.Weapon, 5, 0.5f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 1, 4),
        MakeSuffix("of_initiative", "先机", "先攻加成", AffixTarget.Accessory, 1, 0.8f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, "", 2),
        MakeSuffix("of_life_steal", "吸血", "攻击回复HP", AffixTarget.Weapon, 8, 0.3f,
            0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0, 0, 0, "life_steal", 0.1f),
    ];

    public static EquipmentAffix[] GetAllAffixes()
    {
        var result = new System.Collections.Generic.List<EquipmentAffix>();
        result.AddRange(GetPrefixAffixes());
        result.AddRange(GetSuffixAffixes());
        return result.ToArray();
    }

    /// <summary>根据目标类型筛选可用词缀</summary>
    public static EquipmentAffix[] GetAffixesForTarget(AffixTarget target, int itemLevel, int rarity)
    {
        var result = new System.Collections.Generic.List<EquipmentAffix>();
        foreach (var affix in GetAllAffixes())
        {
            if ((affix.Target == AffixTarget.Any || affix.Target == target) &&
                itemLevel >= affix.MinItemLevel &&
                itemLevel <= affix.MaxItemLevel &&
                rarity >= affix.MinRarity)
            {
                result.Add(affix);
            }
        }
        return result.ToArray();
    }

    // ========================================
    // 内部工厂
    // ========================================

    private static EquipmentAffix MakePrefix(
        string id, string name, string desc, AffixTarget target, int minLvl, float weight,
        int str, int dex, int con, int intel, int wis, int cha,
        int hp, int ac, int move, int init,
        int atk, int dmg, int dmgDc, int dmgDs,
        int critR, int critM, string resist = "",
        string cond = "", int condDc = 0, int condDs = 0, string condDt = "")
    {
        var a = new EquipmentAffix();
        a.AffixId = id;
        a.AffixName = name;
        a.Description = desc;
        a.IsPrefix = true;
        a.Target = target;
        a.MinItemLevel = minLvl;
        a.Weight = weight;
        a.StrBonus = str;
        a.DexBonus = dex;
        a.ConBonus = con;
        a.IntBonus = intel;
        a.WisBonus = wis;
        a.ChaBonus = cha;
        a.HpBonus = hp;
        a.AcBonus = ac;
        a.MoveBonus = move;
        a.InitiativeBonus = init;
        a.AttackBonus = atk;
        a.DamageBonus = dmg;
        a.DamageDiceCountBonus = dmgDc;
        a.DamageDiceSidesBonus = dmgDs;
        a.CritRangeBonus = critR;
        a.CritMultiplierBonus = critM;
        a.Resistance = resist;
        a.Condition = cond;
        a.ConditionalDamageDiceCount = condDc;
        a.ConditionalDamageDiceSides = condDs;
        a.ConditionalDamageType = condDt;
        return a;
    }

    private static EquipmentAffix MakeSuffix(
        string id, string name, string desc, AffixTarget target, int minLvl, float weight,
        int str, int dex, int con, int intel, int wis, int cha,
        int hp, int ac, int move, string resist = "",
        int init = 0, int dmgDc = 0, int dmgDs = 0,
        string special = "", float specialVal = 0.0f)
    {
        var a = new EquipmentAffix();
        a.AffixId = id;
        a.AffixName = name;
        a.Description = desc;
        a.IsPrefix = false;
        a.Target = target;
        a.MinItemLevel = minLvl;
        a.Weight = weight;
        a.StrBonus = str;
        a.DexBonus = dex;
        a.ConBonus = con;
        a.IntBonus = intel;
        a.WisBonus = wis;
        a.ChaBonus = cha;
        a.HpBonus = hp;
        a.AcBonus = ac;
        a.MoveBonus = move;
        a.Resistance = resist;
        a.InitiativeBonus = init;
        a.DamageDiceCountBonus = dmgDc;
        a.DamageDiceSidesBonus = dmgDs;
        a.SpecialEffect = special;
        a.SpecialValue = specialVal;
        return a;
    }
}
