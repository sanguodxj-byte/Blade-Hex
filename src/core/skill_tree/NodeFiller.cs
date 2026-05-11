using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 技能盘自动维护组件 — 刷新全图可用状态到每角色集合
/// 不修改共享 SkillTreeData 中的任何节点状态
/// </summary>
public static class NodeFiller
{
    /// <summary>
    /// 全量刷新：遍历所有节点，将满足条件的节点ID写入 availableSet
    /// </summary>
    public static void RefreshAvailable(
        Dictionary<string, SkillNodeData> nodes,
        HashSet<string> activatedSet,
        int characterLevel,
        HashSet<string> availableSet)
    {
        availableSet.Clear();
        foreach (var kvp in nodes)
        {
            string nodeId = kvp.Key;
            if (activatedSet.Contains(nodeId)) continue;
            var node = kvp.Value;
            if (CheckAvailable(node, activatedSet, characterLevel))
                availableSet.Add(nodeId);
        }
    }

    /// <summary>检查单个节点是否可解锁</summary>
    public static bool CheckAvailable(SkillNodeData node, HashSet<string> activatedSet, int characterLevel)
    {
        // 规则1：必须有已解锁的邻居
        bool hasUnlockedNeighbor = false;
        foreach (var neighborId in node.Neighbors)
        {
            if (activatedSet.Contains(neighborId))
            {
                hasUnlockedNeighbor = true;
                break;
            }
        }
        if (!hasUnlockedNeighbor) return false;

        // 规则2：前置节点必须全部解锁
        foreach (var prereq in node.Prerequisites)
        {
            if (!activatedSet.Contains(prereq)) return false;
        }

        // 规则3：角色等级达标
        if (node.RequiredLevel > characterLevel) return false;

        return true;
    }
}
