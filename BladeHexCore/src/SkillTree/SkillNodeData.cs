using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>
/// 技能盘节点数据 —— 表示技能盘上的单个节点
/// </summary>
[GlobalClass]
public partial class SkillNodeData : Resource
{
    public enum NodeType { Small, Big, Keystone, Start, Giant, Pip }
    public enum Region { None, Str, Dex, Con, Int, Wis, Cha, Transition }
    public enum ActivationShape { Start, Attribute, PassiveSkill, ActiveSkill, Keystone, Apex }
    public enum ContentMode { Fixed, RandomAttribute }

    // ========================================
    // 标识与类型
    // ========================================

    [Export] public string NodeId { get; set; } = "";
    [Export] public string NodeName { get; set; } = "";
    [Export] public string NodeSubtitle { get; set; } = "";
    [Export] public NodeType CurrentNodeType = NodeType.Small;
    [Export] public Region CurrentRegion = Region.None;

    // ========================================
    // 拓扑结构
    // ========================================

    [Export] public bool IsBridge { get; set; } = false;
    [Export] public int Depth { get; set; } = 0;

    // ========================================
    // 解锁条件
    // ========================================

    [Export] public int RequiredLevel { get; set; } = 0;
    public List<string> Prerequisites = new();

    // ========================================
    // 节点效果
    // ========================================

    [Export] public Godot.Collections.Dictionary StatBonuses = new();
    [Export] public ContentMode CurrentContentMode { get; set; } = ContentMode.Fixed;
    [Export] public int RandomSeed { get; set; } = 0;
    [Export] public string SkillEffect { get; set; } = "";
    [Export] public bool IsActiveSkill { get; set; } = false;
    [Export(PropertyHint.MultilineText)] public string Description = "";

    // Keystone 代价
    [Export(PropertyHint.MultilineText)] public string KeystoneCost = "";
    [Export] public Godot.Collections.Dictionary CostBonuses = new();

    // ========================================
    // UI 与状态
    // ========================================

    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    public Vector2I[] ExplicitTiles { get; set; } = [];
    [Export] public string IconPath { get; set; } = "";
    [Export] public string FigureId { get; set; } = "";
    [Export] public string FigureName { get; set; } = "";
    [Export] public string FigureTemplate { get; set; } = "";
    public bool IsActivated = false;

    /// <summary>
    /// 占位节点：布局对称填满三角楔形时，无内容的空槽用占位节点填充。
    /// 可点亮、消耗点数、参与几何连通，但不提供任何属性/技能加成（等后续编辑填充内容）。
    /// </summary>
    [Export] public bool IsPlaceholder { get; set; } = false;

    // ========================================
    // 辅助方法
    // ========================================

    public string GetEffectText(Godot.Collections.Dictionary? statBonusesOverride = null)
    {
        if (CurrentContentMode == ContentMode.RandomAttribute)
            return GetStatBonusText(statBonusesOverride);
        if (CurrentNodeType == NodeType.Small) return GetStatBonusText(statBonusesOverride);
        if (CurrentNodeType == NodeType.Pip) return GetStatBonusText(statBonusesOverride);
        if (IsPlaceholderDescription(Description) && (statBonusesOverride ?? StatBonuses).Count > 0)
            return GetStatBonusText(statBonusesOverride);
        if (CurrentNodeType == NodeType.Keystone) return Description + "\n[代价] " + KeystoneCost;
        return Description;
    }

    public ActivationShape GetActivationShape()
    {
        return CurrentNodeType switch
        {
            NodeType.Start => ActivationShape.Start,
            NodeType.Pip => ActivationShape.Attribute,
            NodeType.Small => ActivationShape.Attribute,
            NodeType.Keystone => ActivationShape.Keystone,
            NodeType.Giant => ActivationShape.Apex,
            NodeType.Big when IsActiveSkill => ActivationShape.ActiveSkill,
            NodeType.Big => ActivationShape.PassiveSkill,
            _ => ActivationShape.Attribute,
        };
    }

    public int GetRequiredTileCount()
    {
        if (CurrentNodeType != NodeType.Start && ExplicitTiles.Length > 0)
            return ExplicitTiles.Length;

        return GetActivationShape() switch
        {
            ActivationShape.Start => 0,
            ActivationShape.Attribute when CurrentNodeType == NodeType.Pip => 1,
            ActivationShape.Attribute => 2,
            ActivationShape.PassiveSkill => 3,
            ActivationShape.ActiveSkill => 4,
            ActivationShape.Keystone => 6,
            ActivationShape.Apex => 12,
            _ => 2,
        };
    }

    public string GetFigureId()
    {
        return !string.IsNullOrWhiteSpace(FigureId)
            ? FigureId
            : SkillNodeFigureCatalog.GetDefaultFigureId(this);
    }

    public string GetFigureName()
    {
        return !string.IsNullOrWhiteSpace(FigureName)
            ? FigureName
            : SkillNodeFigureCatalog.GetDefaultFigureName(this);
    }

    public string GetFigureTemplate()
    {
        return !string.IsNullOrWhiteSpace(FigureTemplate)
            ? FigureTemplate
            : SkillNodeFigureCatalog.GetDefaultTemplateId(this);
    }

    internal string GetStatBonusText(Godot.Collections.Dictionary? statBonusesOverride = null)
    {
        var bonuses = statBonusesOverride ?? StatBonuses;
        var parts = new List<string>();
        var statNames = new Dictionary<string, string>
        {
            { "max_hp", "最大生命" }, { "ac", "闪避" }, { "melee_hit", "近战命中" },
            { "melee_damage", "近战伤害" }, { "melee_damage_percent", "近战伤害" },
            { "ranged_hit", "远程命中" }, { "ranged_damage", "远程伤害" }, { "ranged_damage_percent", "远程伤害" },
            { "critical_rate", "暴击率" }, { "speed", "移动速度" }, { "mana_max", "魔力上限" },
            { "mana_regen", "魔力回复" }, { "initiative", "先攻" }, { "all_save", "全豁免" }, { "range_bonus", "射程" },
            { "spell_damage", "法术伤害" }, { "spell_damage_percent", "法术伤害" },
            { "heal_amount", "治疗量" }, { "heal_amount_percent", "治疗量" }, { "ally_bonus", "友军加成" },
        };

        foreach (var key in bonuses.Keys)
        {
            string k = key.ToString()!;
            var val = bonuses[key];
            string nameStr = statNames.GetValueOrDefault(k, k);

            if (val.VariantType == Variant.Type.Float)
            {
                float f = val.AsSingle();
                if (ShouldDisplayAsPercent(k))
                    parts.Add($"{nameStr}{f * 100.0f:+0;-0;+0}%");
                else
                    parts.Add($"{nameStr}{f:+0.##;-0.##;+0}");
            }
            else if (val.VariantType == Variant.Type.Int)
            {
                int i = val.AsInt32();
                parts.Add($"{nameStr}{i:+0;-0;+0}");
            }
        }

        return parts.Count > 0 ? string.Join("、", parts) : "无加成";
    }

    private static bool ShouldDisplayAsPercent(string statKey)
    {
        return statKey.EndsWith("_rate", StringComparison.Ordinal)
            || statKey.EndsWith("_pct", StringComparison.Ordinal)
            || statKey.Contains("percent", StringComparison.Ordinal)
            || statKey is "critical_rate";
    }

    private static bool IsPlaceholderDescription(string description)
        => !string.IsNullOrEmpty(description)
            && description.Contains("具体规则见技能星盘节点内容设计", StringComparison.Ordinal);

    public bool CanBeUnlocked(int characterLevel, ICollection<string> activatedNodes)
    {
        if (IsActivated) return false;
        if (RequiredLevel > characterLevel) return false;
        return Prerequisites.All(p => activatedNodes.Contains(p));
    }

    public string GetRegionName() => CurrentRegion switch
    {
        Region.Str => "力量",
        Region.Dex => "灵巧",
        Region.Con => "体魄",
        Region.Int => "智力",
        Region.Wis => "感知",
        Region.Cha => "魅力",
        Region.Transition => "过渡",
        _ => "无"
    };

    public string GetRegionDisplayName() => CurrentRegion switch
    {
        Region.Str => "力量",
        Region.Dex => "灵巧",
        Region.Con => "体魄",
        Region.Int => "智力",
        Region.Wis => "感知",
        Region.Cha => "魅力",
        Region.Transition => "过渡",
        _ => "无名",
    };
}
