using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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
    [Signal] public delegate void AttributePointChangedEventHandler(int newCount);
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

    /// <summary>节点已点亮瓦片数。节点瓦片全满后才进入 ActivatedSet 并应用效果。</summary>
    public Dictionary<string, int> NodeTileProgress { get; private set; } = new();

    public int AvailableSkillPoints { get; set; } = 0;
    public int AvailableAttributePoints
    {
        get => AvailableSkillPoints;
        set => AvailableSkillPoints = value;
    }
    public int TotalJumps { get; set; } = 0;
    public int UsedJumps { get; set; } = 0;
    public int CharacterLevel { get; set; } = 1;
    public int RandomAttributeSeed { get; set; } = 0;

    public Godot.Collections.Dictionary AccumulatedStats { get; private set; } = new();
    public Godot.Collections.Dictionary AccumulatedAttributes { get; private set; } = new();
    public Godot.Collections.Dictionary AccumulatedCosts { get; private set; } = new();

    private readonly Dictionary<string, string> _contentNodeByGeometryNode = new();
    private readonly Dictionary<string, SkillNodeData> _effectiveNodeCache = new();

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

    public CharacterSkillTree(SkillTreeData treeData, int level = 1, int randomAttributeSeed = 0)
    {
        TreeData = treeData;
        CharacterLevel = level;
        RandomAttributeSeed = randomAttributeSeed != 0 ? randomAttributeSeed : CreateRuntimeSeed(level);
        BuildCharacterContentMap();
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
    // 星辉管理：星辉只用于填充技能盘瓦片；六维属性由已填充瓦片逐片提供。
    // ============================================================================

    public void AddAttributePoint(int amount = 1)
    {
        AvailableAttributePoints += amount;
        EmitPointChanged();
    }

    public void AddSkillPoint(int amount = 1) => AddAttributePoint(amount);

    public bool ConsumeAttributePoint()
    {
        if (AvailableAttributePoints <= 0) return false;
        AvailableAttributePoints--;
        EmitPointChanged();
        return true;
    }

    public bool ConsumeSkillPoint() => ConsumeAttributePoint();

    private void EmitPointChanged()
    {
        EmitSignal(SignalName.SkillPointChanged, AvailableAttributePoints);
        EmitSignal(SignalName.AttributePointChanged, AvailableAttributePoints);
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
    public int GetTileProgress(string nodeId) => NodeTileProgress.GetValueOrDefault(nodeId, 0);
    public int GetRequiredTileCount(string nodeId)
    {
        if (TreeData == null || !TreeData.Nodes.TryGetValue(nodeId, out var node))
            return 0;
        return node.GetRequiredTileCount();
    }

    // ============================================================================
    // 点亮节点
    // ============================================================================

    private (bool valid, string message, SkillNodeData? node) ValidateActivation(string nodeId)
    {
        if (TreeData == null || !TreeData.Nodes.TryGetValue(nodeId, out var node))
            return (false, "节点不存在", null);

        if (ActivatedSet.Contains(nodeId))
            return (false, "节点已点亮", null);

        var effectiveNode = GetEffectiveNode(node);
        if (effectiveNode.RequiredLevel > CharacterLevel)
            return (false, $"需要角色等级 {effectiveNode.RequiredLevel}", null);

        // 不再要求前置节点 — 只检查等级；连通性在 TryActivateNode 中判定。

        return (true, "", node);
    }

    public Godot.Collections.Dictionary TryActivateNode(string nodeId)
    {
        var (valid, message, node) = ValidateActivation(nodeId);
        if (!valid)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", message } };

        bool hasPartialProgress = GetTileProgress(nodeId) > 0;
        if (!hasPartialProgress && !AvailableSet.Contains(nodeId))
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "该节点与已点亮区域不相邻，请先连接路径或使用跳跃" } };

        if (AvailableAttributePoints <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "没有可用星辉" } };

        return FillNodeTile(node!, consumeJump: false);
    }

    public Godot.Collections.Dictionary TryJumpActivate(string nodeId)
    {
        var (valid, message, node) = ValidateActivation(nodeId);
        if (!valid)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", message } };

        if (GetRemainingJumps() <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "没有可用跳跃次数" } };

        if (!CanJumpToNode(node!))
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "跳跃只能到达同环带的本扇区或相邻扇区" } };

        if (AvailableAttributePoints <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "没有可用星辉" } };

        UseJump();
        return FillNodeTile(node!, consumeJump: true);
    }

    private Godot.Collections.Dictionary FillNodeTile(SkillNodeData node, bool consumeJump)
    {
        int required = node.GetRequiredTileCount();
        if (required <= 0)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "该节点不需要点亮" } };

        int current = GetTileProgress(node.NodeId);
        if (current >= required)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "节点已点亮" } };

        ConsumeAttributePoint();
        current++;
        NodeTileProgress[node.NodeId] = current;
        ApplyTileAttribute(node);
        var effectiveNode = GetEffectiveNode(node);

        if (current < required)
        {
            string jumpText = consumeJump ? "跳跃" : "";
            return new Godot.Collections.Dictionary
            {
                { "success", true },
                { "completed", false },
                { "progress", current },
                { "required", required },
                { "message", $"{jumpText}点亮 {effectiveNode.NodeName} 瓦片 {current}/{required}" },
            };
        }

        CompleteNodeActivation(node);
        string completeText = consumeJump ? "跳跃完成" : "完成";
        return new Godot.Collections.Dictionary
        {
            { "success", true },
            { "completed", true },
            { "progress", current },
            { "required", required },
            { "message", $"{completeText} {effectiveNode.NodeName}" },
        };
    }

    private void CompleteNodeActivation(SkillNodeData node)
    {
        ActivatedNodes.Add(node.NodeId);
        ActivatedSet.Add(node.NodeId);
        ApplyNodeStats(node);
        EmitSignal(SignalName.NodeActivated, node.NodeId);
        NodeFiller.RefreshAvailable(TreeData!.Nodes, ActivatedSet, CharacterLevel, AvailableSet);
    }

    // ============================================================================
    // 属性汇总
    // ============================================================================

    private void ApplyTileAttribute(SkillNodeData node)
    {
        string attributeKey = GetTileAttributeKey(node.CurrentRegion);
        if (string.IsNullOrEmpty(attributeKey))
            return;

        AccumulatedAttributes[attributeKey] = AccumulatedAttributes.ContainsKey(attributeKey)
            ? AccumulatedAttributes[attributeKey].AsInt32() + 1
            : 1;
    }

    private void ApplyTileAttributes(SkillNodeData node, int count)
    {
        for (int i = 0; i < count; i++)
            ApplyTileAttribute(node);
    }

    private static string GetTileAttributeKey(SkillNodeData.Region region)
    {
        return region switch
        {
            SkillNodeData.Region.Str => "str",
            SkillNodeData.Region.Dex => "dex",
            SkillNodeData.Region.Con => "con",
            SkillNodeData.Region.Int => "int",
            SkillNodeData.Region.Wis => "wis",
            SkillNodeData.Region.Cha => "cha",
            _ => "",
        };
    }

    private void ApplyNodeStats(SkillNodeData node)
    {
        var effectiveNode = GetEffectiveNode(node);

        // 占位节点不提供任何加成（仅占位/连通，等后续编辑填充内容）
        if (effectiveNode.IsPlaceholder) return;

        var bonuses = GetNodeStatBonusesForCharacter(node);
        foreach (var key in bonuses.Keys)
        {
            string k = key.ToString()!;
            var val = bonuses[key];
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

        if (effectiveNode.CurrentNodeType == SkillNodeData.NodeType.Keystone)
        {
            foreach (var key in effectiveNode.CostBonuses.Keys)
            {
                string k = key.ToString()!;
                var val = effectiveNode.CostBonuses[key];
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
    public float GetMeleeDamagePercentBonus() => AccumulatedStats.ContainsKey("melee_damage_percent") ? AccumulatedStats["melee_damage_percent"].AsSingle() : 0.0f;
    public int GetRangedHitBonus() => AccumulatedStats.ContainsKey("ranged_hit") ? AccumulatedStats["ranged_hit"].AsInt32() : 0;
    public float GetRangedDamagePercentBonus() => AccumulatedStats.ContainsKey("ranged_damage_percent") ? AccumulatedStats["ranged_damage_percent"].AsSingle() : 0.0f;
    public float GetCriticalRateBonus() => AccumulatedStats.ContainsKey("critical_rate") ? AccumulatedStats["critical_rate"].AsSingle() : 0.0f;
    public int GetSpeedBonus() => AccumulatedStats.ContainsKey("speed") ? AccumulatedStats["speed"].AsInt32() : 0;
    public int GetManaMaxBonus() => AccumulatedStats.ContainsKey("mana_max") ? AccumulatedStats["mana_max"].AsInt32() : 0;
    public int GetManaRegenBonus() => AccumulatedStats.ContainsKey("mana_regen") ? AccumulatedStats["mana_regen"].AsInt32() : 0;
    public int GetInitiativeBonus() => AccumulatedStats.ContainsKey("initiative") ? AccumulatedStats["initiative"].AsInt32() : 0;
    public int GetAllSaveBonus() => AccumulatedStats.ContainsKey("all_save") ? AccumulatedStats["all_save"].AsInt32() : 0;
    public int GetRangeBonus() => AccumulatedStats.ContainsKey("range_bonus") ? AccumulatedStats["range_bonus"].AsInt32() : 0;
    public float GetSpellDamagePercentBonus() => AccumulatedStats.ContainsKey("spell_damage_percent") ? AccumulatedStats["spell_damage_percent"].AsSingle() : 0.0f;
    public float GetHealPercentBonus() => AccumulatedStats.ContainsKey("heal_amount_percent") ? AccumulatedStats["heal_amount_percent"].AsSingle() : 0.0f;
    public int GetAllyBonus() => AccumulatedStats.ContainsKey("ally_bonus") ? AccumulatedStats["ally_bonus"].AsInt32() : 0;
    public int GetStrBonus() => AccumulatedAttributes.ContainsKey("str") ? AccumulatedAttributes["str"].AsInt32() : 0;
    public int GetDexBonus() => AccumulatedAttributes.ContainsKey("dex") ? AccumulatedAttributes["dex"].AsInt32() : 0;
    public int GetConBonus() => AccumulatedAttributes.ContainsKey("con") ? AccumulatedAttributes["con"].AsInt32() : 0;
    public int GetIntBonus() => AccumulatedAttributes.ContainsKey("int") ? AccumulatedAttributes["int"].AsInt32() : 0;
    public int GetWisBonus() => AccumulatedAttributes.ContainsKey("wis") ? AccumulatedAttributes["wis"].AsInt32() : 0;
    public int GetChaBonus() => AccumulatedAttributes.ContainsKey("cha") ? AccumulatedAttributes["cha"].AsInt32() : 0;

    public Godot.Collections.Dictionary GetAllAccumulatedStats() => (Godot.Collections.Dictionary)AccumulatedStats.Duplicate();
    public Godot.Collections.Dictionary GetAllAccumulatedAttributes() => (Godot.Collections.Dictionary)AccumulatedAttributes.Duplicate();
    public Godot.Collections.Dictionary GetAllAccumulatedCosts() => (Godot.Collections.Dictionary)AccumulatedCosts.Duplicate();
    public int GetCostInt(string key) => AccumulatedCosts.ContainsKey(key) ? AccumulatedCosts[key].AsInt32() : 0;
    public float GetCostFloat(string key) => AccumulatedCosts.ContainsKey(key) ? AccumulatedCosts[key].AsSingle() : 0.0f;

    // ============================================================================
    // 技能查询
    // ============================================================================

    public List<SkillNodeData> GetActiveSkills()
    {
        var result = new List<SkillNodeData>();
        foreach (var nodeId in ActivatedNodes)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node))
            {
                var effectiveNode = GetEffectiveNode(node);
                if (effectiveNode.IsActiveSkill)
                    result.Add(effectiveNode);
            }
        }
        return result;
    }

    public List<SkillNodeData> GetPassiveSkills()
    {
        var result = new List<SkillNodeData>();
        foreach (var nodeId in ActivatedNodes)
        {
            if (!TreeData!.Nodes.TryGetValue(nodeId, out var node))
                continue;

            var effectiveNode = GetEffectiveNode(node);
            if (!effectiveNode.IsActiveSkill &&
                effectiveNode.CurrentNodeType != SkillNodeData.NodeType.Small &&
                effectiveNode.CurrentNodeType != SkillNodeData.NodeType.Start)
                result.Add(effectiveNode);
        }
        return result;
    }

    public bool HasSkillEffect(string effectName)
    {
        foreach (var nodeId in ActivatedNodes)
        {
            string effect = GetEffectiveSkillEffect(nodeId);
            if (SkillEffectMatches(effect, effectName))
                return true;
        }
        return false;
    }

    private static bool SkillEffectMatches(string nodeEffect, string queryEffect)
    {
        if (string.IsNullOrEmpty(nodeEffect) || string.IsNullOrEmpty(queryEffect))
            return false;

        if (nodeEffect == queryEffect)
            return true;

        string nodeAlias = GetLegacyEffectAlias(nodeEffect);
        if (!string.IsNullOrEmpty(nodeAlias) && nodeAlias == queryEffect)
            return true;

        string queryAlias = GetLegacyEffectAlias(queryEffect);
        return !string.IsNullOrEmpty(queryAlias) && queryAlias == nodeEffect;
    }

    private static string GetLegacyEffectAlias(string effect) => effect switch
    {
        "ghost_footwork" => "ghost_step",
        "undying_body" => "immortal_body",
        "iron_body" => "diamond_body",
        _ => "",
    };

    public List<string> GetActiveSkillEffects()
    {
        var result = new List<string>();
        foreach (var nodeId in ActivatedNodes)
        {
            string effect = GetEffectiveSkillEffect(nodeId);
            if (!string.IsNullOrEmpty(effect))
                result.Add(effect);
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
        var seen = new HashSet<string>();
        foreach (var nodeId in AvailableSet)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node))
            {
                result.Add(node);
                seen.Add(nodeId);
            }
        }

        foreach (var (nodeId, progress) in NodeTileProgress)
        {
            if (progress <= 0 || ActivatedSet.Contains(nodeId) || seen.Contains(nodeId))
                continue;
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
            if (GetEffectiveNode(node).RequiredLevel > CharacterLevel) continue;
            if (!CanJumpToNode(node)) continue;
            result.Add(node);
        }
        return result;
    }

    private bool CanJumpToNode(SkillNodeData target)
    {
        if (TreeData == null) return false;
        if (target.CurrentRegion == SkillNodeData.Region.None || target.CurrentRegion == SkillNodeData.Region.Transition)
            return false;

        var targetBand = GetNodeRingBand(target);
        if (targetBand == RingBand.None)
            return false;

        foreach (string activatedId in ActivatedSet)
        {
            if (!TreeData.Nodes.TryGetValue(activatedId, out var source))
                continue;
            if (source.CurrentNodeType == SkillNodeData.NodeType.Start)
                continue;
            if (GetNodeRingBand(source) != targetBand)
                continue;
            if (IsSameOrAdjacentJumpRegion(source.CurrentRegion, target.CurrentRegion))
                return true;
        }

        return false;
    }

    private static bool IsSameOrAdjacentJumpRegion(SkillNodeData.Region source, SkillNodeData.Region target)
    {
        if (source == target) return true;
        int sourceIndex = JumpRegionOrder.IndexOf(source);
        int targetIndex = JumpRegionOrder.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0) return false;
        int count = JumpRegionOrder.Count;
        return targetIndex == (sourceIndex + 1) % count
            || targetIndex == (sourceIndex + count - 1) % count;
    }

    private enum RingBand
    {
        None,
        Inner,
        Middle,
        Outer,
    }

    private static readonly List<SkillNodeData.Region> JumpRegionOrder =
    [
        SkillNodeData.Region.Wis,
        SkillNodeData.Region.Str,
        SkillNodeData.Region.Dex,
        SkillNodeData.Region.Con,
        SkillNodeData.Region.Int,
        SkillNodeData.Region.Cha,
    ];

    private static RingBand GetNodeRingBand(SkillNodeData node)
    {
        int ring = GetNodeCenterRing(node);
        if (ring >= 1 && ring <= 8) return RingBand.Inner;
        if (ring >= 9 && ring <= 15) return RingBand.Middle;
        if (ring >= 16 && ring <= SkillTreeData.FixedLayoutRadius) return RingBand.Outer;
        return RingBand.None;
    }

    private static int GetNodeCenterRing(SkillNodeData node)
    {
        var tiles = SkillNodeShape.GetTiles(node);
        if (tiles.Length == 0)
            return SkillTreeCoord.HexRing(node.GridPosition);

        double average = 0.0;
        foreach (var tile in tiles)
            average += GetTileRing(tile);
        return (int)Math.Round(average / tiles.Length, MidpointRounding.AwayFromZero);
    }

    private static int GetTileRing(Vector2I encoded)
    {
        int maxRing = 0;
        foreach (var vertex in GetTileVertices(encoded))
        {
            int s = -vertex.X - vertex.Y;
            maxRing = Math.Max(maxRing, Math.Max(Math.Abs(vertex.X), Math.Max(Math.Abs(vertex.Y), Math.Abs(s))));
        }
        return maxRing;
    }

    private static Vector2I[] GetTileVertices(Vector2I encoded)
    {
        var (q, r, t) = SkillTreeCoord.DecodeTile(encoded);
        return t == 0
            ? [new Vector2I(q, r), new Vector2I(q + 1, r), new Vector2I(q, r + 1)]
            : [new Vector2I(q + 1, r), new Vector2I(q, r + 1), new Vector2I(q + 1, r + 1)];
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

        var tileProgress = new Godot.Collections.Dictionary();
        foreach (var kvp in NodeTileProgress)
            tileProgress[kvp.Key] = kvp.Value;

        return new Godot.Collections.Dictionary
        {
            { "activated_nodes", new Godot.Collections.Array<string>(ActivatedNodes.ToArray()) },
            { "available_skill_points", AvailableSkillPoints },
            { "available_attribute_points", AvailableAttributePoints },
            { "node_tile_progress", tileProgress },
            { "total_jumps", TotalJumps },
            { "used_jumps", UsedJumps },
            { "character_level", CharacterLevel },
            { "random_attribute_seed", RandomAttributeSeed },
            { "career_skill_uses", careerUses },
        };
    }

    public void Deserialize(Godot.Collections.Dictionary data, SkillTreeData treeData)
    {
        TreeData = treeData;
        var activatedArr = data.ContainsKey("activated_nodes")
            ? (Godot.Collections.Array)data["activated_nodes"]
            : new Godot.Collections.Array();
        AvailableAttributePoints = data.ContainsKey("available_attribute_points")
            ? data["available_attribute_points"].AsInt32()
            : data.ContainsKey("available_skill_points") ? data["available_skill_points"].AsInt32() : 0;
        TotalJumps = data.ContainsKey("total_jumps") ? data["total_jumps"].AsInt32() : 0;
        UsedJumps = data.ContainsKey("used_jumps") ? data["used_jumps"].AsInt32() : 0;
        CharacterLevel = data.ContainsKey("character_level") ? data["character_level"].AsInt32() : 1;
        RandomAttributeSeed = data.ContainsKey("random_attribute_seed")
            ? data["random_attribute_seed"].AsInt32()
            : CreateRuntimeSeed(CharacterLevel);
        BuildCharacterContentMap();

        ActivatedNodes.Clear();
        ActivatedSet.Clear();
        NodeTileProgress.Clear();
        AccumulatedStats.Clear();
        AccumulatedAttributes.Clear();
        AccumulatedCosts.Clear();

        foreach (var id in activatedArr)
        {
            string nodeId = id.AsString();
            ActivatedNodes.Add(nodeId);
            if (TreeData.Nodes.TryGetValue(nodeId, out var node))
            {
                ActivatedSet.Add(nodeId);
                int required = node.GetRequiredTileCount();
                NodeTileProgress[nodeId] = required;
                ApplyTileAttributes(node, required);
                ApplyNodeStats(node);
            }
        }

        if (data.ContainsKey("node_tile_progress"))
        {
            var progress = (Godot.Collections.Dictionary)data["node_tile_progress"];
            foreach (var key in progress.Keys)
            {
                string nodeId = key.ToString()!;
                if (ActivatedSet.Contains(nodeId)) continue;
                if (!TreeData.Nodes.TryGetValue(nodeId, out var node)) continue;
                int count = Math.Clamp(progress[key].AsInt32(), 0, node.GetRequiredTileCount());
                if (count <= 0) continue;
                NodeTileProgress[nodeId] = count;
                ApplyTileAttributes(node, count);
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
        bool needsAutoEquippableActive = !HasAutoEquippableActiveSkill();

        int loopGuard = 0;
        while (AvailableAttributePoints > 0 && loopGuard++ < 200)
        {
            var available = GetAvailableNodes()
                .Where(n => GetEffectiveNode(n).RequiredLevel <= CharacterLevel)
                .ToList();

            if (available.Count == 0)
            {
                var jumpable = GetJumpableNodes();
                if (jumpable.Count == 0) break;
                available = jumpable;
            }

            available.Sort((a, b) =>
            {
                float scoreA = AiNodeScore(a, primaryRegion, secondaryRegion, needsAutoEquippableActive);
                float scoreB = AiNodeScore(b, primaryRegion, secondaryRegion, needsAutoEquippableActive);
                return scoreB.CompareTo(scoreA);
            });

            bool success = false;
            while (available.Count > 0)
            {
                SkillNodeData selected;
                int selectedIndex = 0;
                if (GD.Randf() < aiAccuracy)
                {
                    selected = available[0];
                }
                else
                {
                    int poolStart = Math.Min(1, available.Count - 1);
                    selectedIndex = GD.RandRange(poolStart, available.Count - 1);
                    selected = available[selectedIndex];
                }

                var res = (AvailableSet.Contains(selected.NodeId) || GetTileProgress(selected.NodeId) > 0)
                    ? TryActivateNode(selected.NodeId)
                    : TryJumpActivate(selected.NodeId);

                if (res.ContainsKey("success") && res["success"].AsBool())
                {
                    if (needsAutoEquippableActive && HasAutoEquippableActiveSkill())
                        needsAutoEquippableActive = false;
                    success = true;
                    break;
                }
                else
                {
                    // 激活失败（如其他未满足条件），将其移出候选并重试
                    available.RemoveAt(selectedIndex);
                }
            }

            // 如果本轮循环尝试了所有候选节点都无法激活成功，则直接退出，防止死循环
            if (!success) break;
        }
    }

    /// <summary>
    /// 多区域加点：按 targetRegions 顺序优先覆盖各区域至少 1 个 BIG 节点，
    /// 再按权重把剩余点平摊。供 sim 强制生成"剑舞者(STR+DEX)"、"武圣(STR+DEX+CON)"等
    /// 复合职业 build。
    /// </summary>
    /// <param name="targetRegions">想要触达的区域列表（按重要性降序，第一个最优先）</param>
    /// <param name="bigNodesPerRegion">每个区域至少要打多少个 BIG 节点（用于 ClassTitleResolver 触发）</param>
    public void AiAllocatePointsMultiRegion(string[] targetRegions, int bigNodesPerRegion = 1)
    {
        var regionMap = new Dictionary<string, SkillNodeData.Region>
        {
            { "str", SkillNodeData.Region.Str }, { "dex", SkillNodeData.Region.Dex },
            { "con", SkillNodeData.Region.Con }, { "int", SkillNodeData.Region.Int },
            { "wis", SkillNodeData.Region.Wis }, { "cha", SkillNodeData.Region.Cha },
        };
        var targetRegionEnums = new List<SkillNodeData.Region>();
        foreach (var t in targetRegions)
            if (regionMap.TryGetValue(t, out var r)) targetRegionEnums.Add(r);
        if (targetRegionEnums.Count == 0)
        {
            AiAllocatePoints(1.0f, "str", "dex");
            return;
        }

        // Phase 1: 优先打"距离 + 区域优先级"双重排序，倾向尽快进入每个目标区域
        // 直到每个目标区域都至少有 N 个 BIG 节点
        int loopGuard = 0;
        while (AvailableAttributePoints > 0 && loopGuard++ < 200)
        {
            // 计算每个目标区域当前的 BIG 节点数
            var bigCount = new Dictionary<SkillNodeData.Region, int>();
            foreach (var r in targetRegionEnums) bigCount[r] = 0;
            foreach (var nid in ActivatedNodes)
            {
                if (!TreeData.Nodes.TryGetValue(nid, out var n)) continue;
                if (n.CurrentNodeType == SkillNodeData.NodeType.Big
                    || n.CurrentNodeType == SkillNodeData.NodeType.Keystone
                    || n.CurrentNodeType == SkillNodeData.NodeType.Giant)
                {
                    if (bigCount.ContainsKey(n.CurrentRegion))
                        bigCount[n.CurrentRegion]++;
                }
            }

            // 找出还没满足 bigNodesPerRegion 的区域（按 targetRegions 顺序）
            SkillNodeData.Region? underfilledRegion = null;
            foreach (var r in targetRegionEnums)
            {
                if (bigCount[r] < bigNodesPerRegion)
                {
                    underfilledRegion = r;
                    break;
                }
            }

            var available = GetAvailableNodes()
                .Where(n => GetEffectiveNode(n).RequiredLevel <= CharacterLevel)
                .ToList();

            if (available.Count == 0)
            {
                var jumpable = GetJumpableNodes();
                if (jumpable.Count == 0) break;
                available = jumpable;
            }

            // 排序：（1）underfilled 区域的 BIG/Keystone 节点最高分
            //       （2）目标区域的任意节点次之
            //       （3）其他区域最低
            var underfilled = underfilledRegion;  // capture for lambda
            available.Sort((a, b) =>
            {
                float sa = ScoreForMulti(a, targetRegionEnums, underfilled);
                float sb = ScoreForMulti(b, targetRegionEnums, underfilled);
                return sb.CompareTo(sa);
            });

            bool success = false;
            while (available.Count > 0)
            {
                var selected = available[0];
                var res = (AvailableSet.Contains(selected.NodeId) || GetTileProgress(selected.NodeId) > 0)
                    ? TryActivateNode(selected.NodeId)
                    : TryJumpActivate(selected.NodeId);

                if (res.ContainsKey("success") && res["success"].AsBool())
                {
                    success = true;
                    break;
                }
                else
                {
                    available.RemoveAt(0);
                }
            }

            if (!success) break;
        }
    }

    private float ScoreForMulti(SkillNodeData node, List<SkillNodeData.Region> targets,
        SkillNodeData.Region? underfilled)
    {
        float score = 0;
        bool isInTarget = targets.Contains(node.CurrentRegion);

        if (underfilled.HasValue && node.CurrentRegion == underfilled.Value)
        {
            score += 200;
            if (node.CurrentNodeType == SkillNodeData.NodeType.Giant) score += 90;
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Big) score += 50;
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone) score += 70;
        }
        else if (isInTarget)
        {
            score += 100;
            if (node.CurrentNodeType == SkillNodeData.NodeType.Giant) score += 40;
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Big) score += 20;
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone) score += 30;
        }
        else if (node.CurrentRegion == SkillNodeData.Region.Transition)
        {
            score += 30;
        }

        return score;
    }

    private float AiNodeScore(SkillNodeData node, SkillNodeData.Region primaryRegion, SkillNodeData.Region secondaryRegion,
        bool needsAutoEquippableActive = false)
    {
        float score = 0.0f;
        if (node.CurrentRegion == primaryRegion) score += 100.0f;
        else if (node.CurrentRegion == secondaryRegion) score += 50.0f;
        else if (node.CurrentRegion == SkillNodeData.Region.Transition) score += 20.0f;

        var effectiveNode = GetEffectiveNode(node);
        if (effectiveNode.CurrentNodeType == SkillNodeData.NodeType.Giant) score += 40.0f;
        else if (effectiveNode.CurrentNodeType == SkillNodeData.NodeType.Keystone) score += 30.0f;
        else if (effectiveNode.CurrentNodeType == SkillNodeData.NodeType.Big) score += 20.0f;

        if (needsAutoEquippableActive)
        {
            if (IsAutoEquippableActiveSkill(effectiveNode))
                score += 10000.0f;
            else
                score += GetActiveSkillPathScore(node, primaryRegion, secondaryRegion);
        }

        return score;
    }

    private bool HasAutoEquippableActiveSkill()
    {
        foreach (var node in GetActiveSkills())
        {
            if (IsAutoEquippableActiveSkill(node))
                return true;
        }
        return false;
    }

    private static bool IsAutoEquippableActiveSkill(SkillNodeData node)
    {
        return node.IsActiveSkill
            && !string.IsNullOrEmpty(node.SkillEffect);
    }

    private float GetActiveSkillPathScore(SkillNodeData candidate, SkillNodeData.Region primaryRegion,
        SkillNodeData.Region secondaryRegion)
    {
        if (TreeData == null)
            return 0.0f;

        int bestDistance = int.MaxValue;
        int bestPreference = 0;
        var targetNodes = GetPreferredActiveSkillTargets(primaryRegion, secondaryRegion);
        foreach (var node in targetNodes)
        {
            int distance = GetNodeTileDistance(candidate, node);
            int preference = node.CurrentRegion == primaryRegion ? 2 : node.CurrentRegion == secondaryRegion ? 1 : 0;
            if (distance < bestDistance || (distance == bestDistance && preference > bestPreference))
            {
                bestDistance = distance;
                bestPreference = preference;
            }
        }

        if (bestDistance == int.MaxValue)
            return 0.0f;

        return Math.Max(0.0f, 600.0f - bestDistance * 25.0f) + bestPreference * 60.0f;
    }

    private List<SkillNodeData> GetPreferredActiveSkillTargets(SkillNodeData.Region primaryRegion,
        SkillNodeData.Region secondaryRegion)
    {
        var all = TreeData!.Nodes.Values
            .Where(node =>
            {
                var effectiveNode = GetEffectiveNode(node);
                return IsAutoEquippableActiveSkill(effectiveNode)
                    && effectiveNode.RequiredLevel <= CharacterLevel;
            })
            .ToList();

        var primary = all.Where(node => node.CurrentRegion == primaryRegion).ToList();
        if (primary.Count > 0) return primary;

        var secondary = all.Where(node => node.CurrentRegion == secondaryRegion).ToList();
        if (secondary.Count > 0) return secondary;

        return all;
    }

    private static int GetNodeTileDistance(SkillNodeData a, SkillNodeData b)
    {
        var aTiles = SkillNodeShape.GetTiles(a);
        var bTiles = SkillNodeShape.GetTiles(b);
        int best = int.MaxValue;
        foreach (var aTile in aTiles)
        {
            var (aq, ar, _) = SkillTreeCoord.DecodeTile(aTile);
            foreach (var bTile in bTiles)
            {
                var (bq, br, _) = SkillTreeCoord.DecodeTile(bTile);
                int distance = SkillTreeCoord.HexDistance(new Vector2I(aq, ar), new Vector2I(bq, br));
                if (distance < best)
                    best = distance;
            }
        }
        return best == int.MaxValue ? 0 : best;
    }

    public Godot.Collections.Dictionary GetNodeStatBonusesForCharacter(SkillNodeData node)
    {
        var effectiveNode = GetEffectiveNode(node);
        if (effectiveNode.CurrentContentMode != SkillNodeData.ContentMode.RandomAttribute)
            return effectiveNode.StatBonuses;

        return RollRandomAttributeForCharacter(effectiveNode);
    }

    public string GetNodeEffectTextForCharacter(SkillNodeData node)
    {
        var effectiveNode = GetEffectiveNode(node);
        return effectiveNode.GetEffectText(GetNodeStatBonusesForCharacter(node));
    }

    public SkillNodeData GetEffectiveNode(SkillNodeData node)
    {
        if (TreeData == null)
            return node;

        if (_effectiveNodeCache.TryGetValue(node.NodeId, out var cached))
            return cached;

        if (!_contentNodeByGeometryNode.TryGetValue(node.NodeId, out string? contentNodeId)
            || contentNodeId == node.NodeId
            || !TreeData.Nodes.TryGetValue(contentNodeId, out var contentNode))
        {
            _effectiveNodeCache[node.NodeId] = node;
            return node;
        }

        var effective = CloneGeometryWithContent(node, contentNode);
        _effectiveNodeCache[node.NodeId] = effective;
        return effective;
    }

    public string GetEffectiveSkillEffect(string nodeId)
    {
        if (TreeData == null || !TreeData.Nodes.TryGetValue(nodeId, out var node))
            return "";
        return GetEffectiveNode(node).SkillEffect;
    }

    public string GetEffectiveNodeName(string nodeId)
    {
        if (TreeData == null || !TreeData.Nodes.TryGetValue(nodeId, out var node))
            return nodeId;
        return GetEffectiveNode(node).NodeName;
    }

    private void BuildCharacterContentMap()
    {
        _contentNodeByGeometryNode.Clear();
        _effectiveNodeCache.Clear();
        if (TreeData == null)
            return;

        foreach (var node in TreeData.Nodes.Values)
            _contentNodeByGeometryNode[node.NodeId] = node.NodeId;

        var groups = TreeData.Nodes.Values
            .Where(IsCharacterShuffledContentNode)
            .GroupBy(node => $"{node.CurrentRegion}:{GetContentShuffleRole(node)}:{GetContentShuffleBand(node)}");

        foreach (var group in groups)
        {
            var geometryIds = group
                .Select(node => node.NodeId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (geometryIds.Count <= 1)
                continue;

            var contentIds = geometryIds.ToList();
            Shuffle(contentIds, new Random(CombineSeed(RandomAttributeSeed, StableSeed(group.Key))));
            DerangeInPlace(geometryIds, contentIds);

            for (int i = 0; i < geometryIds.Count; i++)
                _contentNodeByGeometryNode[geometryIds[i]] = contentIds[i];
        }
    }

    private static bool IsCharacterShuffledContentNode(SkillNodeData node)
    {
        if (node.CurrentRegion is SkillNodeData.Region.None or SkillNodeData.Region.Transition)
            return false;
        return node.CurrentNodeType == SkillNodeData.NodeType.Big
            || node.CurrentNodeType == SkillNodeData.NodeType.Keystone
            || node.CurrentNodeType == SkillNodeData.NodeType.Giant;
    }

    private static string GetContentShuffleRole(SkillNodeData node)
    {
        return node.CurrentNodeType switch
        {
            SkillNodeData.NodeType.Big when node.IsActiveSkill => "active",
            SkillNodeData.NodeType.Big => "passive",
            SkillNodeData.NodeType.Keystone => "keystone",
            SkillNodeData.NodeType.Giant => "giant",
            _ => "fixed",
        };
    }

    private static string GetContentShuffleBand(SkillNodeData node)
    {
        return GetNodeRingBand(node) switch
        {
            RingBand.Inner => "inner",
            RingBand.Middle => "middle",
            RingBand.Outer => "outer",
            _ => "none",
        };
    }

    private static void Shuffle<T>(IList<T> values, Random rng)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private static void DerangeInPlace(IReadOnlyList<string> geometryIds, IList<string> contentIds)
    {
        if (geometryIds.Count <= 1)
            return;

        bool allFixed = true;
        for (int i = 0; i < geometryIds.Count; i++)
        {
            if (geometryIds[i] != contentIds[i])
            {
                allFixed = false;
                break;
            }
        }

        if (allFixed)
        {
            RotateLeft(contentIds);
            return;
        }

        for (int i = 0; i < geometryIds.Count; i++)
        {
            if (geometryIds[i] != contentIds[i])
                continue;

            int swapIndex = (i + 1) % contentIds.Count;
            (contentIds[i], contentIds[swapIndex]) = (contentIds[swapIndex], contentIds[i]);
        }
    }

    private static void RotateLeft<T>(IList<T> values)
    {
        if (values.Count <= 1)
            return;
        T first = values[0];
        for (int i = 0; i < values.Count - 1; i++)
            values[i] = values[i + 1];
        values[^1] = first;
    }

    private static SkillNodeData CloneGeometryWithContent(SkillNodeData geometryNode, SkillNodeData contentNode)
    {
        return new SkillNodeData
        {
            NodeId = geometryNode.NodeId,
            CurrentRegion = geometryNode.CurrentRegion,
            CurrentNodeType = geometryNode.CurrentNodeType,
            Depth = geometryNode.Depth,
            IsBridge = geometryNode.IsBridge,
            RequiredLevel = contentNode.RequiredLevel,
            GridPosition = geometryNode.GridPosition,
            ExplicitTiles = geometryNode.ExplicitTiles,
            IsActivated = geometryNode.IsActivated,
            NodeName = contentNode.NodeName,
            NodeSubtitle = contentNode.NodeSubtitle,
            Description = contentNode.Description,
            SkillEffect = contentNode.SkillEffect,
            IsActiveSkill = contentNode.IsActiveSkill,
            StatBonuses = (Godot.Collections.Dictionary)contentNode.StatBonuses.Duplicate(),
            CostBonuses = (Godot.Collections.Dictionary)contentNode.CostBonuses.Duplicate(),
            KeystoneCost = contentNode.KeystoneCost,
            CurrentContentMode = contentNode.CurrentContentMode,
            RandomSeed = contentNode.RandomSeed,
            IconPath = contentNode.IconPath,
            FigureId = contentNode.FigureId,
            FigureName = contentNode.FigureName,
            FigureTemplate = contentNode.FigureTemplate,
            IsPlaceholder = contentNode.IsPlaceholder,
        };
    }

    private Godot.Collections.Dictionary RollRandomAttributeForCharacter(SkillNodeData node)
    {
        if (node.CurrentRegion == SkillNodeData.Region.Transition)
            return node.StatBonuses;

        var entries = GetRandomPool(node.CurrentRegion);
        var result = new Godot.Collections.Dictionary();
        if (entries.Count == 0)
            return result;

        var rng = new Random(CombineSeed(RandomAttributeSeed, node.RandomSeed));
        var main = PickRandomPoolEntry(entries, rng);
        result[main.Stat] = ToVariantValue(main.Roll(rng, useMinimum: node.CurrentNodeType == SkillNodeData.NodeType.Pip));

        if (node.CurrentNodeType == SkillNodeData.NodeType.Small && rng.NextDouble() < 0.10d)
        {
            var secondary = PickRandomPoolEntry(entries, rng);
            if (!result.ContainsKey(secondary.Stat))
                result[secondary.Stat] = ToVariantValue(secondary.Roll(rng, useMinimum: true));
        }

        return result;
    }

    private static List<RandomPoolEntry> GetRandomPool(SkillNodeData.Region region)
        => RandomPools.Value.TryGetValue(region, out var entries) ? entries : [];

    private static RandomPoolEntry PickRandomPoolEntry(List<RandomPoolEntry> entries, Random rng)
    {
        int total = 0;
        foreach (var entry in entries)
            total += Math.Max(0, entry.Weight);

        int roll = rng.Next(Math.Max(1, total));
        foreach (var entry in entries)
        {
            roll -= Math.Max(0, entry.Weight);
            if (roll < 0)
                return entry;
        }

        return entries[0];
    }

    private static Variant ToVariantValue(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0001d)
            return (int)Math.Round(value);
        return (float)value;
    }

    private static int CombineSeed(int characterSeed, int nodeSeed)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + characterSeed;
            hash = hash * 31 + nodeSeed;
            return hash;
        }
    }

    private static int StableSeed(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in text)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private static int CreateRuntimeSeed(int level)
    {
        unchecked
        {
            return HashCode.Combine(level, System.Environment.TickCount, Guid.NewGuid().GetHashCode());
        }
    }

    private static readonly Lazy<Dictionary<SkillNodeData.Region, List<RandomPoolEntry>>> RandomPools = new(LoadRandomPools);

    private static Dictionary<SkillNodeData.Region, List<RandomPoolEntry>> LoadRandomPools()
    {
        var result = new Dictionary<SkillNodeData.Region, List<RandomPoolEntry>>();
        string? path = ResolveDataPath(SkillTreeLayoutLoader.DefaultContentPath);
        if (path == null)
            return result;

        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("randomPools", out var poolsElement))
            return result;

        foreach (var poolProp in poolsElement.EnumerateObject())
        {
            var region = ParsePoolRegion(poolProp.Name);
            if (region == SkillNodeData.Region.None)
                continue;

            var entries = new List<RandomPoolEntry>();
            foreach (var entry in poolProp.Value.EnumerateArray())
            {
                string stat = entry.GetProperty("stat").GetString() ?? "";
                double min = entry.GetProperty("min").GetDouble();
                double max = entry.GetProperty("max").GetDouble();
                int weight = entry.TryGetProperty("weight", out var weightElement) ? weightElement.GetInt32() : 1;
                if (!string.IsNullOrEmpty(stat))
                    entries.Add(new RandomPoolEntry(stat, min, max, weight));
            }
            result[region] = entries;
        }

        return result;
    }

    private static SkillNodeData.Region ParsePoolRegion(string value) => value.ToLowerInvariant() switch
    {
        "str" => SkillNodeData.Region.Str,
        "dex" => SkillNodeData.Region.Dex,
        "con" => SkillNodeData.Region.Con,
        "int" => SkillNodeData.Region.Int,
        "wis" => SkillNodeData.Region.Wis,
        "cha" => SkillNodeData.Region.Cha,
        _ => SkillNodeData.Region.None,
    };

    private static string? ResolveDataPath(string relativePath)
    {
        var candidates = new[]
        {
            System.IO.Path.Combine(AppContext.BaseDirectory, relativePath),
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relativePath),
            System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), relativePath),
            System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", relativePath),
        };

        foreach (var candidate in candidates)
        {
            string full = System.IO.Path.GetFullPath(candidate);
            if (System.IO.File.Exists(full))
                return full;
        }

        return null;
    }

    private sealed record RandomPoolEntry(string Stat, double Min, double Max, int Weight)
    {
        public double Roll(Random rng, bool useMinimum)
        {
            if (useMinimum || Math.Abs(Max - Min) < 0.0001d)
                return Min;

            if (Math.Abs(Min - Math.Round(Min)) < 0.0001d && Math.Abs(Max - Math.Round(Max)) < 0.0001d)
                return rng.Next((int)Math.Round(Min), (int)Math.Round(Max) + 1);

            return Min + rng.NextDouble() * (Max - Min);
        }
    }
}
