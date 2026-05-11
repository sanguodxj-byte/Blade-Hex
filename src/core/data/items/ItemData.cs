// ItemData.cs
// 所有物品的基类 — 增加稀有度、物品ID、词缀槽位
// 对应策划案 06-装备与物品.md
// 迁移自 GDScript ItemData.gd
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class ItemData : Resource
{
    // ========================================
    // 稀有度枚举
    // ========================================

    public enum Rarity
    {
        Common,    // 普通 — 白色，无词缀
        Uncommon,  // 优秀 — 绿色，1个词缀
        Rare,      // 稀有 — 蓝色，2个词缀
        Epic,      // 史诗 — 紫色，3个词缀
        Legendary, // 传说 — 橙色，固定唯一效果
    }

    // ========================================
    // 装备渲染部位枚举
    // ========================================

    /// <summary>
    /// 角色分部位渲染层，按 z-order 从后到前排列。
    /// 每个 EquipmentSlot 对应一个独立的 Sprite3D 渲染层。
    /// </summary>
    public enum EquipSlot
    {
        Body,    // 身体层 — 基础身形/服装
        Costume, // 服装层 — 外套/斗篷/胸甲
        Hands,   // 手甲层 — 手套/护腕
        Head,    // 头部层 — 头发/面部/兽头
        Helmet,  // 头盔层 — 帽盔/兜帽
        Weapon,  // 武器层 — 武器/盾牌
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public string ItemId = "";
    [Export] public string ItemName = "未命名物品";
    [Export] public string Description = "";
    [Export] public Texture2D? Icon;
    [Export] public float Weight = 1.0f;
    [Export] public int Price = 10;
    [Export] public Rarity ItemRarity = Rarity.Common;

    /// <summary>已附加的词缀列表（生成时动态附加）</summary>
    [Export] public Godot.Collections.Array<EquipmentAffix> Affixes = new();

    /// <summary>物品来源/掉落区域标签</summary>
    [Export] public string[] SourceTags = [];

    /// <summary>是否为唯一物品（传说级物品不可重复获取）</summary>
    [Export] public bool IsUnique;

    /// <summary>物品等级（影响词缀生成范围和缩放）</summary>
    [Export] public int ItemLevel = 1;

    // ========================================
    // 分部位换装 — 外观纹理
    // ========================================

    /// <summary>该物品所占据的渲染部位（决定贴到哪个锚点层）</summary>
    [Export] public EquipSlot EquipSlotTarget = EquipSlot.Body;

    /// <summary>装备外观纹理（替换对应部位层的贴图）</summary>
    [Export] public Texture2D? EquipTexture;

    /// <summary>装备外观序列帧（优先于 EquipTexture，支持动画）</summary>
    [Export] public SpriteFrames? EquipSpriteFrames;

    // ========================================
    // 稀有度工具方法
    // ========================================

    public string GetRarityName() => ItemRarity switch
    {
        Rarity.Common => "普通",
        Rarity.Uncommon => "优秀",
        Rarity.Rare => "稀有",
        Rarity.Epic => "史诗",
        Rarity.Legendary => "传说",
        _ => "普通",
    };

    public Color GetRarityColor() => ItemRarity switch
    {
        Rarity.Common => new Color(0.9f, 0.9f, 0.9f),
        Rarity.Uncommon => new Color(0.3f, 0.9f, 0.3f),
        Rarity.Rare => new Color(0.3f, 0.5f, 1.0f),
        Rarity.Epic => new Color(0.7f, 0.3f, 1.0f),
        Rarity.Legendary => new Color(1.0f, 0.6f, 0.0f),
        _ => Colors.White,
    };

    public int GetSellPrice() => ItemRarity switch
    {
        Rarity.Common => Price,
        Rarity.Uncommon => (int)(Price * 1.5),
        Rarity.Rare => (int)(Price * 2.5),
        Rarity.Epic => (int)(Price * 5.0),
        Rarity.Legendary => (int)(Price * 10.0),
        _ => Price,
    };

    public int GetMaxAffixCount() => ItemRarity switch
    {
        Rarity.Common => 0,
        Rarity.Uncommon => 1,
        Rarity.Rare => 2,
        Rarity.Epic => 3,
        Rarity.Legendary => 0, // 传说级使用固定唯一效果
        _ => 0,
    };

    public bool CanAddAffix() => Affixes.Count < GetMaxAffixCount();

    public bool AddAffix(EquipmentAffix affix)
    {
        if (!CanAddAffix()) return false;
        Affixes.Add(affix);
        ApplyAffix(affix);
        return true;
    }

    /// <summary>生成完整名称（包含词缀前缀/后缀）</summary>
    public string GetFullName()
    {
        if (Affixes.Count == 0) return ItemName;
        var prefix = "";
        var suffix = "";
        foreach (var affix in Affixes)
        {
            if (affix.IsPrefix)
                prefix += (prefix != "" ? "·" : "") + affix.AffixName;
            else
                suffix += (suffix != "" ? "·" : "") + affix.AffixName;
        }
        var result = "";
        if (prefix != "") result = prefix + " ";
        result += ItemName;
        if (suffix != "") result += " " + suffix;
        return result;
    }

    /// <summary>获取所有词缀效果的文本描述</summary>
    public string GetAffixDescriptions()
    {
        if (Affixes.Count == 0) return "";
        var descs = new System.Collections.Generic.List<string>();
        foreach (var affix in Affixes)
            descs.Add($"{affix.AffixName}: {affix.GetEffectDescription()}");
        return string.Join("\n", descs);
    }

    // ========================================
    // 词缀应用（子类可重写）
    // ========================================

    /// <summary>将词缀效果应用到物品上（由子类重写以实现具体属性加成）</summary>
    public virtual void ApplyAffix(EquipmentAffix affix) { }
}
