// SkillTreeManager.cs
// 技能盘全局管理器 — Autoload 单例，三层分离架构
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// [Autoload Singleton] 技能盘全局管理器。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>SkillTreeManager</c>。</para>
/// <para>生命周期：应用全局 — 角色加点进度需跨场景持久（大地图加点 → 进战斗 → 回大地图保留）。</para>
/// <para>访问方式：建议通过 <see cref="BladeHex.Data.Globals.SkillTrees"/>（或 <see cref="BladeHex.Data.Globals.SkillTreesOrNull"/>）。</para>
///
/// <para>三层分离架构：</para>
/// <list type="bullet">
///   <item>层 1：<see cref="SkillTreeData"/> — 图数据（只读，所有角色共用）</item>
///   <item>层 2：<see cref="NodeFiller"/> — 状态维护</item>
///   <item>层 3：<see cref="CharacterSkillTree"/> — 角色实例</item>
/// </list>
/// </summary>
[GlobalClass]
public partial class SkillTreeManager : Node
{
    public static SkillTreeManager? Instance { get; private set; }

#if DEBUG
    /// <summary>测试钩子：替换或重置 <see cref="Instance"/>。</summary>
    public static void OverrideForTest(SkillTreeManager? mock) => Instance = mock;
#endif

    // ========================================
    // 数据
    // ========================================

    /// <summary>全局共享技能盘图数据（只读，所有角色共用）</summary>
    public SkillTreeData? TreeData { get; private set; }

    /// <summary>坐标组件（用于 UI 渲染）</summary>
    public SkillTreeCoord? Coord { get; private set; }

    /// <summary>所有角色的技能盘（角色实例ID → CharacterSkillTree）</summary>
    public Dictionary<long, CharacterSkillTree> CharacterTrees { get; } = new();

    // ========================================
    // 初始化
    // ========================================

    public override void _Ready()
    {
        Instance = this;
        LoadTreeData();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadTreeData()
    {
        TreeData = new SkillTreeData();
        Coord = new SkillTreeCoord();
        GD.Print($"[SkillTreeManager] 技能盘加载完成，节点总数: {TreeData.GetNodeCount()}");
    }

    // ========================================
    // 角色技能盘管理
    // ========================================

    /// <summary>为角色创建技能盘</summary>
    public CharacterSkillTree CreateSkillTree(long characterId, int level = 1)
    {
        var skillTree = new CharacterSkillTree(TreeData!, level);
        CharacterTrees[characterId] = skillTree;
        return skillTree;
    }

    /// <summary>获取角色的技能盘</summary>
    public CharacterSkillTree? GetSkillTree(long characterId)
    {
        return CharacterTrees.GetValueOrDefault(characterId);
    }

    /// <summary>移除角色的技能盘</summary>
    public void RemoveSkillTree(long characterId)
    {
        CharacterTrees.Remove(characterId);
    }

    // ========================================
    // 升级处理
    // ========================================

    public void OnCharacterLevelUp(long characterId, int newLevel)
    {
        var tree = GetSkillTree(characterId);
        if (tree == null) return;
        tree.CharacterLevel = newLevel;
        tree.AddSkillPoint(1);
        if (newLevel % 5 == 0)
        {
            tree.RegisterJump();
            GD.Print($"[SkillTreeManager] 角色 {characterId} 升到 {newLevel} 级，获得1次跳跃机会");
        }
        GD.Print($"[SkillTreeManager] 角色 {characterId} 升到 {newLevel} 级，获得1技能点");
    }

    public void InitCharacterLevel(long characterId, int level)
    {
        var tree = GetSkillTree(characterId);
        if (tree == null) return;
        tree.AvailableSkillPoints = 0;
        tree.UsedJumps = 0;
        tree.TotalJumps = 0;
        tree.CharacterLevel = level;
        // 初始5技能点 + 每级额外1点（1级=5点，2级=6点...）
        int points = 5 + (level - 1);
        tree.AddSkillPoint(points);
        // 跳跃总共6次，在1-30级间获取（1级、6级、12级、18级、24级、30级）
        int jumps = 1 + (level - 1) / 6;
        if (jumps > 6) jumps = 6;
        for (int i = 0; i < jumps; i++)
            tree.RegisterJump();
        GD.Print($"[SkillTreeManager] 初始化角色 {characterId} Lv.{level}：{points}技能点, {jumps}跳跃");
    }

    // ========================================
    // 节点操作
    // ========================================

    /// <summary>点亮节点</summary>
    public Godot.Collections.Dictionary ActivateNode(long characterId, string nodeId)
    {
        var tree = GetSkillTree(characterId);
        if (tree == null)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "角色不存在" } };
        return tree.TryActivateNode(nodeId);
    }

    /// <summary>跳跃点亮节点</summary>
    public Godot.Collections.Dictionary JumpActivateNode(long characterId, string nodeId)
    {
        var tree = GetSkillTree(characterId);
        if (tree == null)
            return new Godot.Collections.Dictionary { { "success", false }, { "message", "角色不存在" } };
        return tree.TryJumpActivate(nodeId);
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary SaveAll()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var kvp in CharacterTrees)
            data[kvp.Key.ToString()] = kvp.Value.Serialize();
        return data;
    }

    public void LoadAll(Godot.Collections.Dictionary data)
    {
        CharacterTrees.Clear();
        foreach (var key in data.Keys)
        {
            long characterId = long.Parse(key.ToString()!);
            var tree = new CharacterSkillTree(TreeData!, 1);
            tree.Deserialize((Godot.Collections.Dictionary)data[key], TreeData!);
            CharacterTrees[characterId] = tree;
        }
    }

    // ========================================
    // 战斗生命周期 — 职业技能
    // ========================================

    /// <summary>战斗开始时重置所有角色的职业技能使用状态</summary>
    public void OnBattleStart()
    {
        foreach (var kvp in CharacterTrees)
        {
            kvp.Value.ResetCareerSkillForBattle();
            kvp.Value.RefreshTitleFlags();
        }
        GD.Print("[SkillTreeManager] 战斗开始，已重置所有角色职业技能状态");
    }

    /// <summary>每回合开始时重置每回合计数</summary>
    public void OnTurnStart()
    {
        foreach (var kvp in CharacterTrees)
            kvp.Value.ResetCareerSkillForTurn();
    }

    // ========================================
    // 调试
    // ========================================

    public void DebugPrintTree(long characterId)
    {
        var tree = GetSkillTree(characterId);
        if (tree == null)
        {
            GD.Print($"[SkillTreeManager] 角色 {characterId} 没有技能盘");
            return;
        }
        GD.Print($"========== 角色 {characterId} 技能盘 ==========");
        GD.Print($"等级: {tree.CharacterLevel} | 可用技能点: {tree.AvailableSkillPoints} | 剩余跳跃: {tree.GetRemainingJumps()}/{tree.TotalJumps}");
        GD.Print($"已点亮节点 ({tree.ActivatedNodes.Count}):");
        foreach (var nodeId in tree.ActivatedNodes)
        {
            if (TreeData!.Nodes.TryGetValue(nodeId, out var node))
            {
                string typeStr = node.CurrentNodeType switch
                {
                    SkillNodeData.NodeType.Small => "小",
                    SkillNodeData.NodeType.Big => "大",
                    SkillNodeData.NodeType.Keystone => "Keystone",
                    SkillNodeData.NodeType.Start => "启程",
                    _ => "?"
                };
                GD.Print($"  [{typeStr}] {node.NodeName} - {node.GetEffectText()}");
            }
        }
        GD.Print($"累计属性: {tree.AccumulatedStats}");
        GD.Print($"代价属性: {tree.AccumulatedCosts}");
        GD.Print("======================================");
    }
}
