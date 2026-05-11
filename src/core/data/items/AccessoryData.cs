// AccessoryData.cs
// 饰品数据 — 戒指、项链等，提供属性加成和特殊效果
// 对应策划案 06-装备与物品.md → 装备槽位总览（饰品×2）
// 迁移自 GDScript AccessoryData.gd
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class AccessoryData : ItemData
{
    // ========================================
    // 饰品类型枚举
    // ========================================

    public enum AccessoryType
    {
        Ring,   // 戒指
        Amulet, // 项链
        Cloak,  // 斗篷
        Belt,   // 腰带
        Bracer, // 护腕
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public AccessoryType accessoryType = AccessoryType.Ring;

    // 属性加成（固定值）
    [Export] public int StrBonus;
    [Export] public int DexBonus;
    [Export] public int ConBonus;
    [Export] public int IntBonus;
    [Export] public int WisBonus;
    [Export] public int ChaBonus;

    // 战斗属性加成
    [Export] public int HpBonus;
    [Export] public int AcBonus;
    [Export] public int MoveBonus;
    [Export] public int InitiativeBonus;

    // 伤害抗性/免疫类型
    [Export] public string Resistance = "";
    [Export] public string Immunity = "";

    // 特殊效果
    [Export] public string SpecialEffect = "";
    [Export] public float SpecialValue;

    // ========================================
    // 方法
    // ========================================

    public string GetAccessoryTypeName() => accessoryType switch
    {
        AccessoryType.Ring => "戒指",
        AccessoryType.Amulet => "项链",
        AccessoryType.Cloak => "斗篷",
        AccessoryType.Belt => "腰带",
        AccessoryType.Bracer => "护腕",
        _ => "饰品",
    };

    public string GetEffectText()
    {
        var parts = new System.Collections.Generic.List<string>();
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
        if (Resistance != "") parts.Add($"{Resistance}抗性");
        if (Immunity != "") parts.Add($"免疫{Immunity}");
        if (SpecialEffect != "")
            parts.Add(SpecialEffectText(SpecialEffect, SpecialValue));
        return string.Join("，", parts);
    }

    public override void ApplyAffix(EquipmentAffix affix)
    {
        base.ApplyAffix(affix);
        StrBonus += affix.StrBonus;
        DexBonus += affix.DexBonus;
        ConBonus += affix.ConBonus;
        IntBonus += affix.IntBonus;
        WisBonus += affix.WisBonus;
        ChaBonus += affix.ChaBonus;
        HpBonus += affix.HpBonus;
        AcBonus += affix.AcBonus;
        MoveBonus += affix.MoveBonus;
        InitiativeBonus += affix.InitiativeBonus;
        if (affix.Resistance != "" && Resistance == "")
            Resistance = affix.Resistance;
        if (affix.SpecialEffect != "" && SpecialEffect == "")
        {
            SpecialEffect = affix.SpecialEffect;
            SpecialValue = affix.SpecialValue;
        }
    }

    private static string SpecialEffectText(string effect, float value) => effect switch
    {
        "life_steal" => $"攻击回复{value * 100:F0}%伤害HP",
        "thorns" => $"被击时反弹{value:F0}伤害",
        "extra_hp_percent" => $"HP+{value * 100:F0}%",
        "damage_reduction" => $"伤害减免{value:F0}",
        "spell_dc_bonus" => $"法术DC{value:+#;-#;#}",
        "shop_discount" => $"商店价格-{value * 100:F0}%",
        "recruit_discount" => $"招募价格-{value * 100:F0}%",
        "flanking_bonus" => $"包夹时命中{value:+#;-#;#}",
        _ => effect,
    };

    // ========================================
    // 静态工厂：预定义饰品
    // ========================================

    public static AccessoryData[] GetAllAccessories() =>
    [
        CreateRingOfPower(),
        CreateAmuletOfVitality(),
        CreateCloakOfProtection(),
        CreateBeltOfGiantStrength(),
        CreateBracerOfArchery(),
    ];

    public static AccessoryData GetRingOfPower() => CreateRingOfPower();
    public static AccessoryData GetAmuletOfVitality() => CreateAmuletOfVitality();
    public static AccessoryData GetCloakOfProtection() => CreateCloakOfProtection();
    public static AccessoryData GetBeltOfGiantStrength() => CreateBeltOfGiantStrength();
    public static AccessoryData GetBracerOfArchery() => CreateBracerOfArchery();

    private static AccessoryData CreateRingOfPower()
    {
        var a = new AccessoryData();
        a.ItemId = "ring_of_power";
        a.ItemName = "力量戒指";
        a.accessoryType = AccessoryType.Ring;
        a.StrBonus = 2;
        a.Price = 120;
        a.ItemRarity = Rarity.Uncommon;
        a.Description = "一枚镶嵌红宝石的戒指，佩戴者感到力量涌动。";
        return a;
    }

    private static AccessoryData CreateAmuletOfVitality()
    {
        var a = new AccessoryData();
        a.ItemId = "amulet_of_vitality";
        a.ItemName = "活力项链";
        a.accessoryType = AccessoryType.Amulet;
        a.ConBonus = 2;
        a.HpBonus = 5;
        a.Price = 150;
        a.ItemRarity = Rarity.Uncommon;
        a.Description = "一条散发着温暖光芒的项链。";
        return a;
    }

    private static AccessoryData CreateCloakOfProtection()
    {
        var a = new AccessoryData();
        a.ItemId = "cloak_of_protection";
        a.ItemName = "防护斗篷";
        a.accessoryType = AccessoryType.Cloak;
        a.AcBonus = 1;
        a.Resistance = "magic";
        a.Price = 250;
        a.ItemRarity = Rarity.Rare;
        a.Description = "一层薄薄的魔法防护环绕着穿戴者。";
        return a;
    }

    private static AccessoryData CreateBeltOfGiantStrength()
    {
        var a = new AccessoryData();
        a.ItemId = "belt_of_giant_strength";
        a.ItemName = "巨人力量腰带";
        a.accessoryType = AccessoryType.Belt;
        a.StrBonus = 4;
        a.Price = 800;
        a.ItemRarity = Rarity.Epic;
        a.Description = "蕴含巨人力量的腰带，佩戴者力大无穷。";
        return a;
    }

    private static AccessoryData CreateBracerOfArchery()
    {
        var a = new AccessoryData();
        a.ItemId = "bracer_of_archery";
        a.ItemName = "射术护腕";
        a.accessoryType = AccessoryType.Bracer;
        a.DexBonus = 2;
        a.InitiativeBonus = 2;
        a.Price = 180;
        a.ItemRarity = Rarity.Uncommon;
        a.Description = "为射手设计的精致护腕，增强灵活性和反应速度。";
        return a;
    }
}
