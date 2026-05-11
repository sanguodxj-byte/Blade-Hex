// ArmorData.cs
// 防具与盾牌数据 — 增加词缀系统支持
// 对应策划案 06-装备与物品.md → 防具系统
// 迁移自 GDScript ArmorData.gd
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class ArmorData : ItemData
{
    // ========================================
    // 枚举
    // ========================================

    public enum ArmorType { Light, Medium, Heavy, Shield }

    // ========================================
    // 基础属性
    // ========================================

    [Export] public ArmorType armorType = ArmorType.Light;
    [Export] public int AcBonus = 1;
    [Export] public int MaxDexBonus = 99; // 99 代表无限制 (轻甲)
    [Export] public int MovementPenalty;
    [Export] public int ApPenalty; // 铠甲对行动点的惩罚 (如中甲-2, 重甲-4)

    // ========================================
    // 装甲值 (Armor Points) 与 减伤 (DR)
    // ========================================

    [Export] public int DrSlash;   // 砍伤减免 (用于计算 AC 增益)
    [Export] public int DrPierce;  // 刺伤减免
    [Export] public int DrCrush;   // 钝伤减免

    /// <summary>
    /// DR阈值：用于 d20 穿透检定的单一属性。
    /// 策划案规定：布甲=3, 皮甲=6, 镇钓皮甲=8, 链甲=11, 板甲=15, 全板甲=18
    /// </summary>
    [Export] public int DrThreshold;

    /// <summary>
    /// 当前装甲值（額外生命值）。公式: DrThreshold * 10
    /// 只能通过修理恢复，不可通过治疗恢复。
    /// </summary>
    [Export] public int CurrentArmorPoints;
    [Export] public int MaxArmorPoints;

    /// <summary>
    /// 初始化装甲值（使用 DrThreshold）
    /// </summary>
    public void InitializeArmorPoints()
    {
        // 装甲耐久 = DR阈值 × 10（护甲是高价战略资源，耐久高）
        // 布甲30, 皮甲60, 镶钉80, 链甲110, 板甲150, 全板甲180
        MaxArmorPoints = DrThreshold * 10;
        CurrentArmorPoints = MaxArmorPoints;
    }

    // 扩展防具属性
    [Export] public int StrRequired;
    [Export] public bool StealthDisadvantage; // 隐匿检定不利（重甲）
    [Export] public bool IsDestroyable;       // 可被破坏（木盾可被斧类击碎）
    [Export] public int BaseAcOverride = -1;  // 固定AC基础值（-1=使用默认10+DEX计算）

    // ========================================
    // 词缀加成（运行时累加）
    // ========================================

    public int BonusAc;
    public string BonusResistance = "";
    public string BonusImmunity = "";
    public int BonusStr;
    public int BonusDex;
    public int BonusCon;
    public int BonusInt;
    public int BonusWis;
    public int BonusCha;
    public int BonusHp;
    public int BonusMove;
    public Godot.Collections.Array<Godot.Collections.Dictionary> BonusSpecialEffects = new();

    // ========================================
    // 词缀应用
    // ========================================

    public override void ApplyAffix(EquipmentAffix affix)
    {
        base.ApplyAffix(affix);

        BonusAc += affix.AcBonus;
        BonusStr += affix.StrBonus;
        BonusDex += affix.DexBonus;
        BonusCon += affix.ConBonus;
        BonusInt += affix.IntBonus;
        BonusWis += affix.WisBonus;
        BonusCha += affix.ChaBonus;
        BonusHp += affix.HpBonus;
        BonusMove += affix.MoveBonus;

        if (affix.Resistance != "" && BonusResistance == "")
            BonusResistance = affix.Resistance;
        if (affix.Immunity != "" && BonusImmunity == "")
            BonusImmunity = affix.Immunity;

        if (affix.SpecialEffect != "")
        {
            BonusSpecialEffects.Add(new Godot.Collections.Dictionary
            {
                { "effect", affix.SpecialEffect },
                { "value", affix.SpecialValue },
            });
        }
    }

    // ========================================
    // 获取方法（含词缀加成）
    // ========================================

    /// <summary>获取总AC加成（基础+词缀）</summary>
    public int GetTotalAcBonus() => AcBonus + BonusAc;

    /// <summary>获取总AC（含base_ac_override + 词缀）</summary>
    public int GetTotalBaseAc()
    {
        int @base = BaseAcOverride >= 0 ? BaseAcOverride : (10 + AcBonus);
        return @base + BonusAc;
    }

    public string GetStatBonusText()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (BonusStr != 0) parts.Add($"力量{BonusStr:+#;-#;#}");
        if (BonusDex != 0) parts.Add($"敏捷{BonusDex:+#;-#;#}");
        if (BonusCon != 0) parts.Add($"体质{BonusCon:+#;-#;#}");
        if (BonusInt != 0) parts.Add($"智力{BonusInt:+#;-#;#}");
        if (BonusWis != 0) parts.Add($"感知{BonusWis:+#;-#;#}");
        if (BonusCha != 0) parts.Add($"魅力{BonusCha:+#;-#;#}");
        if (BonusHp != 0) parts.Add($"HP{BonusHp:+#;-#;#}");
        if (BonusMove != 0) parts.Add($"移动{BonusMove:+#;-#;#}");
        if (BonusResistance != "") parts.Add($"{BonusResistance}抗性");
        return string.Join("，", parts);
    }

    public string GetArmorTypeName() => armorType switch
    {
        ArmorType.Light => "轻甲",
        ArmorType.Medium => "中甲",
        ArmorType.Heavy => "重甲",
        ArmorType.Shield => "盾牌",
        _ => "防具",
    };

    public string GetArmorDescription()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (armorType == ArmorType.Shield)
        {
            parts.Add($"AC{GetTotalAcBonus():+#;-#;#}");
        }
        else
        {
            if (BaseAcOverride >= 0)
                parts.Add($"AC {BaseAcOverride + BonusAc}");
            else
                parts.Add($"AC {10 + AcBonus + BonusAc}+DEX");
            if (MaxDexBonus < 99)
                parts.Add($"DEX上限{MaxDexBonus}");
            if (StrRequired > 0)
                parts.Add($"需要STR {StrRequired}");
            if (StealthDisadvantage)
                parts.Add("隐匿不利");
            if (MovementPenalty != 0)
                parts.Add($"速度{-MovementPenalty:+#;-#;#}");
        }
        var statText = GetStatBonusText();
        if (statText != "")
            parts.Add(statText);
        return string.Join(" ", parts);
    }
}
