// SkillTreeManager.cs
// 技能盘全局管理器 — Autoload 单例，三层分离架构
// 迁移自 GDScript SkillTreeManager.gd
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 技能盘全局管理器 — Autoload 单例
/// 层1: SkillTreeData (图数据) 层2: NodeFiller (状态维护) 层3: CharacterSkillTree (角色实例)
/// </summary>
[GlobalClass]
public partial class SkillTreeManager : Node
{
    private static SkillTreeManager? _instance;

    public static SkillTreeManager? GetInstance() => _instance;

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
        _instance = this;
        LoadTreeData();
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
        int points = level - 1;
        tree.AddSkillPoint(points);
        int jumps = level / 5;
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
