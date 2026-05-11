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
    public enum NodeType { Small, Big, Keystone, Start }
    public enum Region { None, Str, Dex, Con, Int, Wis, Cha, Transition }

    // ========================================
    // 标识与类型
    // ========================================

    [Export] public string NodeId = "";
    [Export] public string NodeName = "";
    [Export] public NodeType CurrentNodeType = NodeType.Small;
    [Export] public Region CurrentRegion = Region.None;

    // ========================================
    // 拓扑结构
    // ========================================

    public List<string> Neighbors = new();
    [Export] public bool IsBridge = false;
    [Export] public int Depth = 0;

    // ========================================
    // 解锁条件
    // ========================================

    [Export] public int RequiredLevel = 0;
    public List<string> Prerequisites = new();

    // ========================================
    // 节点效果
    // ========================================

    [Export] public Godot.Collections.Dictionary StatBonuses = new();
    [Export] public string SkillEffect = "";
    [Export] public bool IsActiveSkill = false;
    [Export(PropertyHint.MultilineText)] public string Description = "";

    // Keystone 代价
    [Export(PropertyHint.MultilineText)] public string KeystoneCost = "";
    [Export] public Godot.Collections.Dictionary CostBonuses = new();

    // ========================================
    // UI 与状态
    // ========================================

    [Export] public Vector2I GridPosition = Vector2I.Zero;
    [Export] public string IconPath = "";
    public bool IsActivated = false;

    // ========================================
    // 辅助方法
    // ========================================

    public string GetEffectText()
    {
        if (CurrentNodeType == NodeType.Small) return GetStatBonusText();
        if (CurrentNodeType == NodeType.Keystone) return Description + "\n[代价] " + KeystoneCost;
        return Description;
    }

    internal string GetStatBonusText()
    {
        var parts = new List<string>();
        var statNames = new Dictionary<string, string>
        {
            { "max_hp", "最大生命" }, { "ac", "护甲" }, { "melee_hit", "近战命中" },
            { "melee_damage", "近战伤害" }, { "ranged_hit", "远程命中" }, { "ranged_damage", "远程伤害" },
            { "critical_rate", "暴击率" }, { "speed", "移动速度" }, { "mana_max", "魔力上限" },
            { "initiative", "先攻" }, { "all_save", "全豁免" }, { "range_bonus", "射程" },
            { "morale", "士气" }, { "cha_check", "魅力检定" }, { "wis_check", "感知检定" },
            { "spell_hit", "法术命中" }, { "spell_damage", "法术伤害" },
            { "heal_amount", "治疗量" }, { "ally_bonus", "友军加成" },
        };

        foreach (var key in StatBonuses.Keys)
        {
            string k = key.ToString()!;
            var val = StatBonuses[key];
            string nameStr = statNames.GetValueOrDefault(k, k);

            if (val.VariantType == Variant.Type.Float)
            {
                float f = val.AsSingle();
                if (Math.Abs(f) < 1.0f) parts.Add($"{nameStr}{f:+0;-0;+0}%");
                else parts.Add($"{nameStr}{f:+0;-0;+0}");
            }
            else if (val.VariantType == Variant.Type.Int)
            {
                int i = val.AsInt32();
                parts.Add($"{nameStr}{i:+0;-0;+0}");
            }
        }

        return parts.Count > 0 ? string.Join("、", parts) : "无加成";
    }

    public bool CanBeUnlocked(int characterLevel, ICollection<string> activatedNodes)
    {
        if (IsActivated) return false;
        if (RequiredLevel > characterLevel) return false;
        return Prerequisites.All(p => activatedNodes.Contains(p));
    }

    public bool IsAdjacentToActivated(ICollection<string> activatedNodes)
    {
        return Neighbors.Any(n => activatedNodes.Contains(n));
    }

    public string GetRegionName() => CurrentRegion switch
    {
        Region.Str => "STR",
        Region.Dex => "DEX",
        Region.Con => "CON",
        Region.Int => "INT",
        Region.Wis => "WIS",
        Region.Cha => "CHA",
        Region.Transition => "过渡",
        _ => "无"
    };
}
