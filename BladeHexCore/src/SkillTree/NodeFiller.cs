using System.Collections.Generic;
using Godot;

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

        // Build occupied tile set from completed figures. Availability is based on
        // the whole triangular figure sharing an edge, not only GridPosition.
        var activatedTiles = new HashSet<Vector2I>();
        foreach (var nodeId in activatedSet)
        {
            if (!nodes.TryGetValue(nodeId, out var activatedNode)) continue;
            foreach (var tile in SkillNodeShape.GetTiles(activatedNode))
                activatedTiles.Add(tile);
        }

        foreach (var kvp in nodes)
        {
            string nodeId = kvp.Key;
            if (activatedSet.Contains(nodeId)) continue;
            var node = kvp.Value;
            if (CheckAvailable(node, activatedSet, characterLevel, activatedTiles))
                availableSet.Add(nodeId);
        }
    }

    /// <summary>检查单个节点是否可解锁（基于三角形瓦片共享边邻接）</summary>
    public static bool CheckAvailable(SkillNodeData node, HashSet<string> activatedSet, int characterLevel,
        HashSet<Vector2I>? activatedTiles = null)
    {
        if (node.RequiredLevel > characterLevel) return false;
        if (activatedTiles == null || activatedTiles.Count == 0) return false;

        return SkillNodeShape.TouchesAnyActivatedTile(node, activatedTiles);
    }
}
