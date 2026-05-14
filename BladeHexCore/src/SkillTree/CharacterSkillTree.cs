using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>
/// 角色技能盘运行时类 — 管理单个角色的技能盘状态
/// 负责：点亮节点、跳跃、属性汇总、技能列表
/// 所有每角色状态保存在此对象中，不修改共享 SkillTreeData
/// </summary>
[GlobalClass]
public partial class CharacterSkillTree : RefCounted
{
    [Signal] public delegate void NodeActivatedEventHandler(string nodeId);
    [Signal] public delegate void SkillPointChangedEventHandler(int newCount);
    [Signal] public delegate void JumpUsedEventHandler(int jumpsRemaining);

    // ============================================================================
    // 核心数据
    // ============================================================================

    public SkillTreeData TreeData { get; private set; } = null!;

    /// <summary>已点亮的节点ID列表（有序）</summary>
    public List<string> ActivatedNodes { get; private set; } = new();

    /// <summary>已点亮节点ID的快速查找表</summary>
    public HashSet<string> ActivatedSet { get; private set; } = new();

    /// <summary>可用（可点亮）节点ID集合</summary>
    public HashSet<string> AvailableSet { get; private set; } = new();

    public int AvailableSkillPoints { get; set; } = 0;
    public int TotalJumps { get; set; } = 0;
    public int UsedJumps { get; set; } = 0;
    public int CharacterLevel { get; set; } = 1;

    public Godot.Collections.Dictionary AccumulatedStats { get; private set; } = new();
    public Godot.Collections.Dictionary AccumulatedCosts { get; private set; } = new();

    // ============================================================================
    // 职业技能使用状态（战斗运行时）
    // ============================================================================

    /// <summary>当前职业称号 flags（由 ClassTitleResolver 计算）</summary>
    public int CurrentTitleFlags { get; set; } = 0;

    /// <summary>职业技能本战斗已使用次数 (effectId → count)</summary>
    public Dictionary<string, int> CareerSkillUses { get; set; } = new();

    /// <summary>职业技能每回合触发追踪 (effectId → 本回合已触发次数)</summary>
    public Dictionary<string, int> CareerSkillTurnUses { get; set; } = new();

    // ============================================================================
    // 初始化
    // ============================================================================

    public CharacterSkillTree() { }

    public CharacterSkillTree(SkillTreeData treeData, int level = 1)
    {
        TreeData = treeData;
        CharacterLevel = level;
        ActivateStartNode();
    }

    private void ActivateStartNode()
    {
        var start = TreeData?.GetStartNode();
        if (start == null) return;
        ActivatedNodes.Add(start.NodeId);
        ActivatedSet.Add(start.NodeId);
        ApplyNodeStats(start);
        NodeFiller.RefreshAvailable(TreeData!.Nodes, ActivatedSet, CharacterLevel, AvailableSet);
    }

    // ============================================================================
    // 技能点管理
    // ============================================================================

    public void AddSkillPoint(int amount = 1)
    {
        AvailableSkillPoints += amount;
        EmitSignal(SignalName.SkillPointChanged, AvailableSkillPoints);
    }

    public bool ConsumeSkillPoint()
    {
        if (AvailableSkillPoints <= 0) return false;
        AvailableSkillPoints--;
        EmitSignal(SignalName.SkillPointChanged, AvailableSkillPoints);
        return true;
    }

    public void RegisterJump() => TotalJumps++;
    public int GetRemainingJumps() => TotalJumps - UsedJumps;

    public bool UseJump()
    {
        if (UsedJumps >= TotalJumps) return false;
        UsedJumps++;
        EmitSignal(SignalName.JumpUsed, TotalJumps - UsedJumps);
        return true;
    }

    public bool IsActivated(string nodeId) => ActivatedSet.Contains(nodeId);
    public bool IsAvailable(string nodeId) => AvailableSet.Contains(nodeId);

    // ============================================================================
    // 点亮节点
    // ============================================================================

    private Godot.Collections.Dictionary ValidateActivation(string nodeId)
    {
        if (TreeData == null || !TreeData.Nodes.TryGetValue(nodeId, out var node))
            return new Godot.Collections.Dictionary { { "valid", false }, { "message", "节点不存在" } };

        if (ActivatedSet.Contains(nodeId))
            return new Godot.Collections.Dictionary { { "valid", false }, { "message", "节点已点亮" } };

        if (node.RequiredLevel > CharacterLevel)
            return new Godot.Collections.Dictionary { { "valid", false }, { "message", $"需要角色等级 {node.RequiredLevel}" } };

        // 不再要求前置节点 — 只检查等级和技能点

        return new Godot.Collections.Dictionary { { "valid", true }, { "node", node } };
    }

    public Godot.Collections.Dictionary TryActivateNode(string nodeId)
    {
        var v = ValidateActivation(nodeId);
        if (!v["valid"].AsBool())
            return new Godot.Collections.Dictionary { { "success", false }, { "message", v["message"].AsString() } };

        var node = (SkillNodeData)v["node"].AsGodotObject()!;

        if (!node.IsAdjacentToActivated(ActivatedSet))
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "该节点与已点亮区域不相邻，请先连接路径或使用跳跃" } };

        if (AvailableSkillPoints <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "没有可用技能点" } };

        DoActivateNode(node);
        return new Godot.Collections.Dictionary { { "success", true }, { "message", $"点亮 {node.NodeName}" } };
    }

    public Godot.Collections.Dictionary TryJumpActivate(string nodeId)
    {
        var v = ValidateActivation(nodeId);
        if (!v["valid"].AsBool())
            return new Godot.Collections.Dictionary { { "success", false }, { "message", v["message"].AsString() } };

        if (GetRemainingJumps() <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "没有可用跳跃次数" } };

        if (AvailableSkillPoints <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "没有可用技能点" } };

        UseJump();
        var node = (SkillNodeData)v["node"].AsGodotObject()!;
        DoActivateNode(node);
        return new Godot.Collections.Dictionary { { "success", true }, { "message", $"跳跃点亮 {node.NodeName}" } };
    }

    private void DoActivateNode(SkillNodeData node)
    {
        ConsumeSkillPoint();
        ActivatedNodes.Add(node.NodeId);
        ActivatedSet.Add(node.NodeId);
        ApplyNodeStats(node);
        EmitSignal(SignalName.NodeActivated, node.NodeId);
        NodeFiller.RefreshAvailable(TreeData!.Nodes, ActivatedSet, CharacterLevel, AvailableSet);
    }

    // ============================================================================
    // 属性汇总
    // ============================================================================

    private void ApplyNodeStats(SkillNodeData node)
    {
        foreach (var key in node.StatBonuses.Keys)
        {
            string k = key.ToString()!;
            var val = node.StatBonuses[key];
            if (AccumulatedStats.ContainsKey(k))
            {
                if (val.VariantType == Variant.Type.Int)
                    AccumulatedStats[k] = AccumulatedStats[k].AsInt32() + val.AsInt32();
                else
                    AccumulatedStats[k] = AccumulatedStats[k].AsSingle() + val.AsSingle();
            }
            else
            {
                AccumulatedStats[k] = val;
            }
        }

        if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone)
        {
            foreach (var key in node.CostBonuses.Keys)
            {
                string k = key.ToString()!;
                var val = node.CostBonuses[key];
                if (AccumulatedCosts.ContainsKey(k))
                {
                    if (val.VariantType == Variant.Type.Int)
                        AccumulatedCosts[k] = AccumulatedCosts[k].AsInt32() + val.AsInt32();
                    else
                        AccumulatedCosts[k] = AccumulatedCosts[k].AsSingle() + val.AsSingle();
                }
                else
                {
                    AccumulatedCosts[k] = val;
                }
            }
        }
    }

    public int GetHpBonus() => AccumulatedStats.ContainsKey("max_hp") ? AccumulatedStats["max_hp"].AsInt32() : 0;
    public int GetAcBonus() => AccumulatedStats.ContainsKey("ac") ? AccumulatedStats["ac"].AsInt32() : 0;
    public int GetMeleeHitBonus() => AccumulatedStats.ContainsKey("melee_hit") ? AccumulatedStats["melee_hit"].AsInt32() : 0;
    public int GetMeleeDamageBonus() => AccumulatedStats.ContainsKey("melee_damage") ? AccumulatedStats["melee_damage"].AsInt32() : 0;
    public int GetRangedHitBonus() => AccumulatedStats.ContainsKey("ranged_hit") ? AccumulatedStats["ranged_hit"].AsInt32() : 0;
    public int GetRangedDamageBonus() => AccumulatedStats.ContainsKey("ranged_damage") ? AccumulatedStats["ranged_damage"].AsInt32() : 0;
    public float GetCriticalRateBonus() => AccumulatedStats.ContainsKey("critical_rate") ? AccumulatedStats["critical_rate"].AsSingle() : 0.0f;
    public int GetSpeedBonus() => AccumulatedStats.ContainsKey("speed") ? AccumulatedStats["speed"].AsInt32() : 0;
    public int GetManaMaxBonus() => AccumulatedStats.ContainsKey("mana_max") ? AccumulatedStats["mana_max"].AsInt32() : 0;
    public int GetInitiativeBonus() => AccumulatedStats.ContainsKey("initiative") ? AccumulatedStats["initiative"].AsInt32() : 0;
    public int GetAllSaveBonus() => AccumulatedStats.ContainsKey("all_save") ? AccumulatedStats["all_save"].AsInt32() : 0;
    public int GetRangeBonus() => AccumulatedStats.ContainsKey("range_bonus") ? AccumulatedStats["range_bonus"].AsInt32() : 0;
    public int GetMoraleBonus() => AccumulatedStats.ContainsKey("morale") ? AccumulatedStats["morale"].AsInt32() : 0;
    public int GetChaCheckBonus() => AccumulatedStats.ContainsKey("cha_check") ? AccumulatedStats["cha_check"].AsInt32() : 0;
    public int GetWisCheckBonus() => AccumulatedStats.ContainsKey("wis_check") ? AccumulatedStats["wis_check"].AsInt32() : 0;
    public int GetSpellHitBonus() => AccumulatedStats.ContainsKey("spell_hit") ? AccumulatedStats["spell_hit"].AsInt32() : 0;
    public int GetSpellDamageBonus() => AccumulatedStats.ContainsKey("spell_damage") ? AccumulatedStats["spell_damage"].AsInt32() : 0;
    public int GetHealBonus() => AccumulatedStats.ContainsKey("heal_amount") ? AccumulatedStats["heal_amount"].AsInt32() : 0;
    public int GetAllyBonus() => AccumulatedStats.ContainsKey("ally_bonus") ? AccumulatedStats["ally_bonus"].AsInt32() : 0;

    public Godot.Collections.Dictionary GetAllAccumulatedStats() => (Godot.Collections.Dictionary)AccumulatedStats.Duplicate();
    public Godot.Collections.Dictionary GetAllAccumulatedCosts() => (Godot.Collections.Dictionary)AccumulatedCosts.Duplicate();

    // ============================================================================
    // 技能查询
    // ============================================================================

    public List<SkillNodeData> GetActiveSkills()
    {
        var result = new List<SkillNodeData>();
        foreach (var nodeId in ActivatedNodes)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node) && node.IsActiveSkill)
                result.Add(node);
        }
        return result;
    }

    public List<SkillNodeData> GetPassiveSkills()
    {
        var result = new List<SkillNodeData>();
        foreach (var nodeId in ActivatedNodes)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node) &&
                !node.IsActiveSkill &&
                node.CurrentNodeType != SkillNodeData.NodeType.Small &&
                node.CurrentNodeType != SkillNodeData.NodeType.Start)
                result.Add(node);
        }
        return result;
    }

    public bool HasSkillEffect(string effectName)
    {
        foreach (var nodeId in ActivatedNodes)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node) && node.SkillEffect == effectName)
                return true;
        }
        return false;
    }

    public List<string> GetActiveSkillEffects()
    {
        var result = new List<string>();
        foreach (var nodeId in ActivatedNodes)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node) && !string.IsNullOrEmpty(node.SkillEffect))
                result.Add(node.SkillEffect);
        }
        return result;
    }

    public int GetActivatedCount() => ActivatedNodes.Count;

    // ============================================================================
    // 可用节点查询
    // ============================================================================

    public List<SkillNodeData> GetAvailableNodes()
    {
        var result = new List<SkillNodeData>();
        foreach (var nodeId in AvailableSet)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node))
                result.Add(node);
        }
        return result;
    }

    public List<SkillNodeData> GetJumpableNodes()
    {
        if (GetRemainingJumps() <= 0) return new List<SkillNodeData>();
        var result = new List<SkillNodeData>();
        foreach (var node in TreeData!.Nodes.Values)
        {
            if (ActivatedSet.Contains(node.NodeId)) continue;
            if (node.CurrentNodeType == SkillNodeData.NodeType.Start) continue;
            if (node.RequiredLevel > CharacterLevel) continue;
            // 不再检查前置节点，只检查等级
            result.Add(node);
        }
        return result;
    }

    // ============================================================================
    // 职业称号
    // ============================================================================

    public Godot.Collections.Dictionary GetClassTitle(Godot.Collections.Dictionary? characterAttrs = null)
        => ClassTitleResolver.Resolve(this, characterAttrs);

    public string GetClassTitleName(Godot.Collections.Dictionary? characterAttrs = null)
        => ClassTitleResolver.GetTitle(this, characterAttrs);

    public Godot.Collections.Dictionary GetRegionStats()
        => ClassTitleResolver.GetRegionStats(this);

    // ============================================================================
    // 职业技能查询
    // ============================================================================

    /// <summary>获取当前职业称号对应的职业技能（可能为null）</summary>
    public CareerSkillData? GetCareerSkill()
    {
        RefreshTitleFlags();
        return CareerSkillRegistry.GetByTitleFlags(CurrentTitleFlags);
    }

    /// <summary>刷新职业称号 flags</summary>
    public void RefreshTitleFlags()
    {
        var resolveResult = ClassTitleResolver.Resolve(this);
        CurrentTitleFlags = resolveResult.ContainsKey("flags") ? resolveResult["flags"].AsInt32() : 0;
    }

    /// <summary>角色是否拥有某个职业技能</summary>
    public bool HasCareerSkill(string effectId)
    {
        var skill = GetCareerSkill();
        return skill != null && skill.EffectId == effectId;
    }

    /// <summary>职业技能是否可以使用（含次数限制检查）</summary>
    public bool CanUseCareerSkill()
    {
        var skill = GetCareerSkill();
        if (skill == null) return false;

        if (skill.LimitType == CareerSkillData.UsageLimit.OncePerBattle)
            return !CareerSkillUses.ContainsKey(skill.EffectId) || CareerSkillUses[skill.EffectId] == 0;

        if (skill.LimitType == CareerSkillData.UsageLimit.PerBattleCount)
        {
            int used = CareerSkillUses.GetValueOrDefault(skill.EffectId, 0);
            return used < skill.MaxUses;
        }

        if (skill.LimitType == CareerSkillData.UsageLimit.OncePerTurn)
        {
            int turnUsed = CareerSkillTurnUses.GetValueOrDefault(skill.EffectId, 0);
            return turnUsed < 1;
        }

        return true;
    }

    /// <summary>记录职业技能使用一次</summary>
    public void RecordCareerSkillUse()
    {
        var skill = GetCareerSkill();
        if (skill == null) return;

        string id = skill.EffectId;
        CareerSkillUses[id] = CareerSkillUses.GetValueOrDefault(id, 0) + 1;
        CareerSkillTurnUses[id] = CareerSkillTurnUses.GetValueOrDefault(id, 0) + 1;
    }

    /// <summary>战斗开始时重置职业技能使用状态</summary>
    public void ResetCareerSkillForBattle()
    {
        CareerSkillUses.Clear();
        CareerSkillTurnUses.Clear();
    }

    /// <summary>每回合开始时重置每回合计数</summary>
    public void ResetCareerSkillForTurn()
    {
        CareerSkillTurnUses.Clear();
    }

    // ============================================================================
    // 序列化
    // ============================================================================

    public Godot.Collections.Dictionary Serialize()
    {
        var careerUses = new Godot.Collections.Dictionary();
        foreach (var kvp in CareerSkillUses)
            careerUses[kvp.Key] = kvp.Value;

        return new Godot.Collections.Dictionary
        {
            { "activated_nodes", new Godot.Collections.Array<string>(ActivatedNodes.ToArray()) },
            { "available_skill_points", AvailableSkillPoints },
            { "total_jumps", TotalJumps },
            { "used_jumps", UsedJumps },
            { "character_level", CharacterLevel },
            { "career_skill_uses", careerUses },
        };
    }

    public void Deserialize(Godot.Collections.Dictionary data, SkillTreeData treeData)
    {
        TreeData = treeData;
        var activatedArr = data.ContainsKey("activated_nodes")
            ? (Godot.Collections.Array)data["activated_nodes"]
            : new Godot.Collections.Array();
        AvailableSkillPoints = data.ContainsKey("available_skill_points") ? data["available_skill_points"].AsInt32() : 0;
        TotalJumps = data.ContainsKey("total_jumps") ? data["total_jumps"].AsInt32() : 0;
        UsedJumps = data.ContainsKey("used_jumps") ? data["used_jumps"].AsInt32() : 0;
        CharacterLevel = data.ContainsKey("character_level") ? data["character_level"].AsInt32() : 1;

        ActivatedNodes.Clear();
        ActivatedSet.Clear();
        AccumulatedStats.Clear();
        AccumulatedCosts.Clear();

        foreach (var id in activatedArr)
        {
            string nodeId = id.AsString();
            ActivatedNodes.Add(nodeId);
            if (TreeData.Nodes.TryGetValue(nodeId, out var node))
            {
                ActivatedSet.Add(nodeId);
                ApplyNodeStats(node);
            }
        }

        NodeFiller.RefreshAvailable(TreeData.Nodes, ActivatedSet, CharacterLevel, AvailableSet);

        // 恢复职业技能使用状态
        CareerSkillUses.Clear();
        if (data.ContainsKey("career_skill_uses"))
        {
            var uses = (Godot.Collections.Dictionary)data["career_skill_uses"];
            foreach (var key in uses.Keys)
                CareerSkillUses[key.ToString()!] = uses[key].AsInt32();
        }
    }

    // ============================================================================
    // AI 自动加点
    // ============================================================================

    public void AiAllocatePoints(float aiAccuracy, string primaryAttr, string secondaryAttr)
    {
        var regionMap = new Dictionary<string, SkillNodeData.Region>
        {
            { "str", SkillNodeData.Region.Str }, { "dex", SkillNodeData.Region.Dex },
            { "con", SkillNodeData.Region.Con }, { "int", SkillNodeData.Region.Int },
            { "wis", SkillNodeData.Region.Wis }, { "cha", SkillNodeData.Region.Cha },
        };
        var primaryRegion = regionMap.GetValueOrDefault(primaryAttr, SkillNodeData.Region.Str);
        var secondaryRegion = regionMap.GetValueOrDefault(secondaryAttr, SkillNodeData.Region.Dex);

        while (AvailableSkillPoints > 0)
        {
            var available = GetAvailableNodes();
            if (available.Count == 0)
            {
                var jumpable = GetJumpableNodes();
                if (jumpable.Count == 0) break;
                available = jumpable;
            }

            available.Sort((a, b) =>
                AiNodeScore(b, primaryRegion, secondaryRegion)
                .CompareTo(AiNodeScore(a, primaryRegion, secondaryRegion)));

            SkillNodeData selected;
            if (GD.Randf() < aiAccuracy)
            {
                selected = available[0];
            }
            else
            {
                int poolStart = Math.Min(1, available.Count - 1);
                selected = available[GD.RandRange(poolStart, available.Count - 1)];
            }

            if (selected.IsAdjacentToActivated(ActivatedSet))
                TryActivateNode(selected.NodeId);
            else
                TryJumpActivate(selected.NodeId);
        }
    }

    private float AiNodeScore(SkillNodeData node, SkillNodeData.Region primaryRegion, SkillNodeData.Region secondaryRegion)
    {
        float score = 0.0f;
        if (node.CurrentRegion == primaryRegion) score += 100.0f;
        else if (node.CurrentRegion == secondaryRegion) score += 50.0f;
        else if (node.CurrentRegion == SkillNodeData.Region.Transition) score += 20.0f;

        if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone) score += 30.0f;
        else if (node.CurrentNodeType == SkillNodeData.NodeType.Big) score += 20.0f;

        return score;
    }
}
