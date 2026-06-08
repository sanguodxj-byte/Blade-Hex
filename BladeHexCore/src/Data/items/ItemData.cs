// ItemData.cs
// 所有物品的基类 — 增加稀有度、物品ID、词缀槽位
// 对应策划案 06-装备与物品.md
using System;
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
    // 装备渲染部位枚举（Core 保留枚举，渲染配置已迁移至 Frontend）
    // ========================================

    /// <summary>
    /// 角色分部位渲染层 — 纯枚举定义。
    /// 渲染参数（锚点、Z-order 等）已迁移至 BladeHexFrontend/src/View/Unit/Slots/SlotRenderConfig.cs。
    /// </summary>
    public enum EquipSlot
    {
    	Body, // 身体层
    	Costume, // 服装层
    	Hands, // 手甲层
    	Head, // 头部层
    	Helmet, // 头盔层
    	Weapon, // 武器层
    	Feet, // 鞋子层
    	Shield, // 盾牌层
    	Face, // 脸部层
    	Hair, // 发型+胡须合并层
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public string ItemId { get; set; } = "";
    [Export] public string ItemName { get; set; } = "未命名物品";
    [Export] public string Description { get; set; } = "";
    [Export] public string IconId { get; set; } = "";
    /// <summary>图标 fallback ID（当 IconId 对应的图标不存在时使用，如 tier 武器回退到基础子类型图标）</summary>
    [Export] public string IconFallbackId { get; set; } = "";
    [Export] public float Weight { get; set; } = 1.0f;
    [Export] public int Price { get; set; } = 10;
    [Export] public Rarity ItemRarity = Rarity.Common;

    /// <summary>已附加的词缀列表（生成时动态附加）</summary>
    [Export] public Godot.Collections.Array<EquipmentAffix> Affixes = new();

    /// <summary>物品来源/掉落区域标签</summary>
    [Export] public string[] SourceTags = [];

    /// <summary>是否为唯一物品（传说级物品不可重复获取）</summary>
    [Export] public bool IsUnique;

    /// <summary>物品等级（影响词缀生成范围和缩放）</summary>
    [Export] public int ItemLevel { get; set; } = 1;

    // ========================================
    // 背包格子占用（暗黑2风格网格背包）
    // ========================================

    /// <summary>物品在背包中占用的宽度（格数）</summary>
    [Export] public int InvWidth { get; set; } = 1;

    /// <summary>物品在背包中占用的高度（格数）</summary>
    [Export] public int InvHeight { get; set; } = 1;

    /// <summary>物品占用的总格数</summary>
    public int InvSlotCount => InvWidth * InvHeight;

    // ========================================
    // 分部位换装 — 外观纹理
    // ========================================

    /// <summary>该物品所占据的渲染部位（决定贴到哪个锚点层）</summary>
    [Export] public EquipSlot EquipSlotTarget = EquipSlot.Body;

    /// <summary>装备外观纹理 ID（View 层通过 ResourceRegistry 解析）</summary>
    [Export] public string EquipTextureId { get; set; } = "";

    /// <summary>装备外观序列帧 ID（优先于 EquipTextureId，支持动画）</summary>
    [Export] public string EquipSpriteFramesId { get; set; } = "";

    // ========================================
    // 箭筒系统（副手装备，提升弹药量至满值 + 伤害修正）
    // ========================================

    /// <summary>箭筒伤害修正（不同等级箭筒提供+5伤害修正）</summary>
    [Export] public int QuiverDamageBonus { get; set; } = 0;

    /// <summary>是否为箭筒类物品</summary>
    public bool IsQuiver => QuiverDamageBonus > 0;

    // ========================================
    // 装备能力组件（替代字符串 SpecialEffect 的可组合系统）
    // ========================================

    /// <summary>该物品携带的装备能力（无序列化，运行时由 JSON 加载器或 ApplyAffix 创建）</summary>
    public System.Collections.Generic.List<BladeHex.Combat.Abilities.EquipmentAbility> Abilities { get; }
        = new System.Collections.Generic.List<BladeHex.Combat.Abilities.EquipmentAbility>();

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

    [Obsolete("使用 TradePricingService.GetSellPrice() 获取经济锚定价格。此方法直接使用 JSON Price，脱离经济模型。")]
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

        // 词缀的 SpecialEffect 路由为 EquipmentAbility
        if (!string.IsNullOrEmpty(affix.SpecialEffect))
        {
            var ability = BladeHex.Combat.Abilities.EquipmentAbilityRegistry.Create(
                affix.SpecialEffect, affix.SpecialValue);
            if (ability != null) Abilities.Add(ability);
        }

        // 词缀的条件触发效果路由为 ConditionalDamageDiceAbility
        if (!string.IsNullOrEmpty(affix.Condition) && affix.ConditionalDamageDiceCount > 0)
        {
            Abilities.Add(new BladeHex.Combat.Abilities.ConditionalDamageDiceAbility
            {
                AbilityId = $"affix_cond_{affix.AffixId}",
                Condition = affix.Condition,
                DiceCount = affix.ConditionalDamageDiceCount,
                DiceSides = affix.ConditionalDamageDiceSides,
                DamageType = affix.ConditionalDamageType,
            });
        }

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
