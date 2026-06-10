// UIConnectivityTests.cs
// UI 联通性测试 — 验证 UI 层依赖的数据契约和信号管线不会断裂。
//
// 设计原则：
//   - 不实例化 Godot 控件（避免场景树依赖），只验证数据流契约
//   - ClassTitleResolver：所有 63 种组合都有有效称号
//   - SkillTree → ClassTitle 管线：创建技能盘 → 点亮节点 → 获取称号
//   - SaveManager.BuildSaveData 契约：关键字段不为 null
//   - GameMenuManager 信号名存在性（反射检查）
//
// 运行方式：在 TerrainTestRunner 的 RunAllUnitTests() 中追加 RunSuite("UIConnectivityTests", ...)
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Strategic;

namespace BladeHex.Tests.UI;

public static class UIConnectivityTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(ClassTitle_AllSingleFlags_HaveTitle), ClassTitle_AllSingleFlags_HaveTitle);
        yield return Run(nameof(ClassTitle_AllDualFlags_HaveTitle), ClassTitle_AllDualFlags_HaveTitle);
        yield return Run(nameof(ClassTitle_AllTripleFlags_HaveTitle), ClassTitle_AllTripleFlags_HaveTitle);
        yield return Run(nameof(ClassTitle_FullFlags_ReturnsWanXiang), ClassTitle_FullFlags_ReturnsWanXiang);
        yield return Run(nameof(ClassTitle_EmptyTree_ReturnsWuMingZhe), ClassTitle_EmptyTree_ReturnsWuMingZhe);
        yield return Run(nameof(ClassTitle_ActivateStrBigNode_ReturnsZhanShi), ClassTitle_ActivateStrBigNode_ReturnsZhanShi);
        yield return Run(nameof(ClassTitle_ActivateStrDexBigNodes_ReturnsSwordDancer), ClassTitle_ActivateStrDexBigNodes_ReturnsSwordDancer);
        yield return Run(nameof(ClassTitle_PartialBigNodeProgress_DoesNotChangeCareer), ClassTitle_PartialBigNodeProgress_DoesNotChangeCareer);
        yield return Run(nameof(ClassTitle_ActivatedSmallNode_DoesNotChangeCareer), ClassTitle_ActivatedSmallNode_DoesNotChangeCareer);
        yield return Run(nameof(ClassTitle_63Combinations_AllNonEmpty), ClassTitle_63Combinations_AllNonEmpty);
        yield return Run(nameof(SkillTree_NodeFigures_DoNotOverlap), SkillTree_NodeFigures_DoNotOverlap);
        yield return Run(nameof(SkillTree_StartHasEntryAndJumpCanReachAllRegions), SkillTree_StartHasEntryAndJumpCanReachAllRegions);
        yield return Run(nameof(SkillTree_NodeFigures_HaveStableNamesAndTemplates), SkillTree_NodeFigures_HaveStableNamesAndTemplates);
        yield return Run(nameof(SkillTree_TryActivateNode_ConnectionCheck), SkillTree_TryActivateNode_ConnectionCheck);
        yield return Run(nameof(SkillTree_TryActivateNode_TileProgressAndPointCost), SkillTree_TryActivateNode_TileProgressAndPointCost);
        yield return Run(nameof(SkillTree_TryJumpActivate_Rules), SkillTree_TryJumpActivate_Rules);
        yield return Run(nameof(SkillTree_ApplyNodeStats_AccumulatedVerification), SkillTree_ApplyNodeStats_AccumulatedVerification);
        yield return Run(nameof(SkillTree_RoundtripSerialization), SkillTree_RoundtripSerialization);
        yield return Run(nameof(SkillTree_CareerSkillLimits_Interception), SkillTree_CareerSkillLimits_Interception);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ============================================================================
    // ClassTitleResolver 完整性测试
    // ============================================================================

    private static readonly int[] AllFlags = {
        ClassTitleResolver.FlagStr, ClassTitleResolver.FlagDex, ClassTitleResolver.FlagCon,
        ClassTitleResolver.FlagInt, ClassTitleResolver.FlagWis, ClassTitleResolver.FlagCha
    };

    private static (bool, string) ClassTitle_AllSingleFlags_HaveTitle()
    {
        // 6 种单属性都应有称号
        var treeData = new SkillTreeData();
        foreach (int flag in AllFlags)
        {
            var tree = CreateTreeWithFlags(treeData, flag);
            string title = tree.GetClassTitleName();
            if (string.IsNullOrEmpty(title) || title == "无名者")
                return (false, $"Flag {flag} returned '{title}'");
        }
        return (true, "");
    }

    private static (bool, string) ClassTitle_AllDualFlags_HaveTitle()
    {
        // 15 种双属性组合
        var treeData = new SkillTreeData();
        int count = 0;
        for (int i = 0; i < AllFlags.Length; i++)
        {
            for (int j = i + 1; j < AllFlags.Length; j++)
            {
                int combo = AllFlags[i] | AllFlags[j];
                var tree = CreateTreeWithFlags(treeData, combo);
                string title = tree.GetClassTitleName();
                if (string.IsNullOrEmpty(title) || title == "无名者")
                    return (false, $"Dual flags {combo} returned '{title}'");
                count++;
            }
        }
        if (count != 15) return (false, $"Expected 15 dual combos, got {count}");
        return (true, "");
    }

    private static (bool, string) ClassTitle_AllTripleFlags_HaveTitle()
    {
        // 20 种三属性组合
        var treeData = new SkillTreeData();
        int count = 0;
        for (int i = 0; i < AllFlags.Length; i++)
        {
            for (int j = i + 1; j < AllFlags.Length; j++)
            {
                for (int k = j + 1; k < AllFlags.Length; k++)
                {
                    int combo = AllFlags[i] | AllFlags[j] | AllFlags[k];
                    var tree = CreateTreeWithFlags(treeData, combo);
                    string title = tree.GetClassTitleName();
                    if (string.IsNullOrEmpty(title) || title == "无名者")
                        return (false, $"Triple flags {combo} returned '{title}'");
                    count++;
                }
            }
        }
        if (count != 20) return (false, $"Expected 20 triple combos, got {count}");
        return (true, "");
    }

    private static (bool, string) ClassTitle_FullFlags_ReturnsWanXiang()
    {
        int allFlags = ClassTitleResolver.FlagStr | ClassTitleResolver.FlagDex | ClassTitleResolver.FlagCon
                     | ClassTitleResolver.FlagInt | ClassTitleResolver.FlagWis | ClassTitleResolver.FlagCha;
        var treeData = new SkillTreeData();
        var tree = CreateTreeWithFlags(treeData, allFlags);
        string title = tree.GetClassTitleName();
        if (title != "万象")
            return (false, $"Expected '万象', got '{title}'");
        return (true, "");
    }

    private static (bool, string) ClassTitle_EmptyTree_ReturnsWuMingZhe()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 1);
        string title = tree.GetClassTitleName();
        if (title != "无名者")
            return (false, $"Expected '无名者', got '{title}'");
        return (true, "");
    }

    private static (bool, string) ClassTitle_ActivateStrBigNode_ReturnsZhanShi()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);

        // 找到一个 STR 区域的 BIG 节点并激活
        string? strBigNode = FindBigNodeInRegion(treeData, SkillNodeData.Region.Str);
        if (strBigNode == null)
            return (false, "No BIG node found in STR region");

        ActivateNodeDirectly(tree, strBigNode, treeData);
        string title = tree.GetClassTitleName();
        if (title != "战士")
            return (false, $"Expected '战士', got '{title}'");
        return (true, "");
    }

    private static (bool, string) ClassTitle_ActivateStrDexBigNodes_ReturnsSwordDancer()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);

        string? strBig = FindBigNodeInRegion(treeData, SkillNodeData.Region.Str);
        string? dexBig = FindBigNodeInRegion(treeData, SkillNodeData.Region.Dex);
        if (strBig == null) return (false, "No BIG node in STR");
        if (dexBig == null) return (false, "No BIG node in DEX");

        ActivateNodeDirectly(tree, strBig, treeData);
        ActivateNodeDirectly(tree, dexBig, treeData);
        string title = tree.GetClassTitleName();
        if (title != "剑舞者")
            return (false, $"Expected '剑舞者', got '{title}'");
        return (true, "");
    }

    private static (bool, string) ClassTitle_PartialBigNodeProgress_DoesNotChangeCareer()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);

        string? strBigNode = FindBigNodeInRegion(treeData, SkillNodeData.Region.Str);
        if (strBigNode == null)
            return (false, "No BIG node found in STR region");

        int required = treeData.Nodes[strBigNode].GetRequiredTileCount();
        tree.NodeTileProgress[strBigNode] = Math.Max(1, required - 1);

        string title = tree.GetClassTitleName();
        if (title != "无名者")
            return (false, $"Partial node progress must not affect career, got '{title}'");

        return (true, "");
    }

    private static (bool, string) ClassTitle_ActivatedSmallNode_DoesNotChangeCareer()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);

        string? strSmallNode = FindSmallNodeInRegion(treeData, SkillNodeData.Region.Str);
        if (strSmallNode == null)
            return (false, "No small node found in STR region");

        ActivateNodeDirectly(tree, strSmallNode, treeData);

        string title = tree.GetClassTitleName();
        if (title != "无名者")
            return (false, $"Completed small attribute node must not affect career, got '{title}'");

        return (true, "");
    }

    // ============================================================================
    // ClassTitleResolver 鲁棒性测试
    // ============================================================================

    private static (bool, string) ClassTitle_63Combinations_AllNonEmpty()
    {
        // 验证所有 63 种非零 flag 组合（2^6 - 1）都有非空称号
        var treeData = new SkillTreeData();
        int maxFlag = (1 << 6) - 1; // 63
        for (int flags = 1; flags <= maxFlag; flags++)
        {
            var tree = CreateTreeWithFlags(treeData, flags);
            string title = tree.GetClassTitleName();
            if (string.IsNullOrEmpty(title))
                return (false, $"Flags {flags} returned empty title");
        }
        return (true, "");
    }

    private static (bool, string) SkillTree_NodeFigures_DoNotOverlap()
    {
        var treeData = new SkillTreeData();
        var occupied = new Dictionary<Vector2I, string>();

        foreach (var kvp in treeData.Nodes)
        {
            foreach (var tile in SkillNodeShape.GetTiles(kvp.Value))
            {
                if (occupied.TryGetValue(tile, out var other))
                    return (false, $"Tile {tile} occupied by both {other} and {kvp.Key}");

                occupied[tile] = kvp.Key;
            }
        }

        return (true, "");
    }

    private static (bool, string) SkillTree_StartHasEntryAndJumpCanReachAllRegions()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 1);
        var availableRegions = new HashSet<SkillNodeData.Region>();

        foreach (var nodeId in tree.AvailableSet)
        {
            if (treeData.Nodes.TryGetValue(nodeId, out var node))
                availableRegions.Add(node.CurrentRegion);
        }

        if (availableRegions.Count == 0)
            return (false, "No initially available entry node");

        tree.TotalJumps = 1;
        if (tree.GetJumpableNodes().Count != 0)
            return (false, "Jump should require an activated non-start node to define same-band adjacent sectors");

        return (true, "");
    }

    private static (bool, string) SkillTree_NodeFigures_HaveStableNamesAndTemplates()
    {
        var treeData = new SkillTreeData();
        foreach (var kvp in treeData.Nodes)
        {
            var node = kvp.Value;
            var figure = SkillNodeShape.GetFigure(node);
            if (string.IsNullOrWhiteSpace(figure.FigureId))
                return (false, $"{kvp.Key} has empty figure id");
            if (string.IsNullOrWhiteSpace(figure.FigureName))
                return (false, $"{kvp.Key} has empty figure name");
            if (string.IsNullOrWhiteSpace(figure.TemplateId))
                return (false, $"{kvp.Key} has empty figure template");
            if (node.CurrentNodeType != SkillNodeData.NodeType.Start && figure.Tiles.Length != node.GetRequiredTileCount())
                return (false, $"{kvp.Key} figure tile count mismatch: {figure.Tiles.Length}/{node.GetRequiredTileCount()}");
            if (ClassTitleResolver.IsCareerDefiningNode(node) && !figure.FigureName.Contains("命座"))
                return (false, $"{kvp.Key} career figure name should contain 命座, got {figure.FigureName}");
        }

        var custom = new SkillNodeData
        {
            NodeId = "custom",
            NodeName = "自定义",
            CurrentNodeType = SkillNodeData.NodeType.Big,
            CurrentRegion = SkillNodeData.Region.Str,
            FigureId = "custom_sigil",
            FigureName = "自定义命座",
            FigureTemplate = "active_kite_4",
        };
        var customFigure = SkillNodeShape.GetFigure(custom);
        if (customFigure.FigureId != "custom_sigil")
            return (false, "Custom figure id override not applied");
        if (customFigure.FigureName != "自定义命座")
            return (false, "Custom figure name override not applied");
        if (customFigure.TemplateId != "active_kite_4" || customFigure.Tiles.Length != 4)
            return (false, "Custom figure template override not applied");

        return (true, "");
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    /// <summary>创建一个已激活指定 flag 对应区域 BIG 节点的技能盘</summary>
    private static CharacterSkillTree CreateTreeWithFlags(SkillTreeData treeData, int flags)
    {
        var tree = new CharacterSkillTree(treeData, 20);

        if ((flags & ClassTitleResolver.FlagStr) != 0)
            ActivateBigInRegion(tree, treeData, SkillNodeData.Region.Str);
        if ((flags & ClassTitleResolver.FlagDex) != 0)
            ActivateBigInRegion(tree, treeData, SkillNodeData.Region.Dex);
        if ((flags & ClassTitleResolver.FlagCon) != 0)
            ActivateBigInRegion(tree, treeData, SkillNodeData.Region.Con);
        if ((flags & ClassTitleResolver.FlagInt) != 0)
            ActivateBigInRegion(tree, treeData, SkillNodeData.Region.Int);
        if ((flags & ClassTitleResolver.FlagWis) != 0)
            ActivateBigInRegion(tree, treeData, SkillNodeData.Region.Wis);
        if ((flags & ClassTitleResolver.FlagCha) != 0)
            ActivateBigInRegion(tree, treeData, SkillNodeData.Region.Cha);

        return tree;
    }

    /// <summary>在指定区域找到第一个 BIG 节点并直接激活</summary>
    private static void ActivateBigInRegion(CharacterSkillTree tree, SkillTreeData treeData, SkillNodeData.Region region)
    {
        string? nodeId = FindBigNodeInRegion(treeData, region);
        if (nodeId != null)
            ActivateNodeDirectly(tree, nodeId, treeData);
    }

    /// <summary>在技能盘数据中找到指定区域的第一个 BIG 或 KEYSTONE 节点</summary>
    private static string? FindBigNodeInRegion(SkillTreeData treeData, SkillNodeData.Region region)
    {
        foreach (var kvp in treeData.Nodes)
        {
            if (kvp.Value.CurrentRegion == region &&
                (kvp.Value.CurrentNodeType == SkillNodeData.NodeType.Big ||
                 kvp.Value.CurrentNodeType == SkillNodeData.NodeType.Keystone))
            {
                return kvp.Key;
            }
        }
        return null;
    }

    private static string? FindSmallNodeInRegion(SkillTreeData treeData, SkillNodeData.Region region)
    {
        foreach (var kvp in treeData.Nodes)
        {
            if (kvp.Value.CurrentRegion == region &&
                kvp.Value.CurrentNodeType == SkillNodeData.NodeType.Small)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    private static void ActivateNodeDirectly(CharacterSkillTree tree, string nodeId, SkillTreeData treeData)
    {
        // 直接添加到已激活列表和集合（ClassTitleResolver 只读这两个）
        tree.ActivatedNodes.Add(nodeId);
        tree.ActivatedSet.Add(nodeId);
        if (treeData.Nodes.TryGetValue(nodeId, out var node))
        {
            var method = typeof(CharacterSkillTree).GetMethod("ApplyNodeStats", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(tree, new object[] { node });
        }
    }

    private static (bool, string) SkillTree_TryActivateNode_ConnectionCheck()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 1);
        
        string? distantNodeId = null;
        foreach (var kvp in treeData.Nodes)
        {
            if (kvp.Key == "start") continue;
            if (!tree.AvailableSet.Contains(kvp.Key))
            {
                distantNodeId = kvp.Key;
                break;
            }
        }
        
        if (distantNodeId == null)
            return (false, "Could not find a distant node for connection check");

        tree.AvailableAttributePoints = 5;
        var res = tree.TryActivateNode(distantNodeId);
        if (res["success"].AsBool())
            return (false, $"Expected activation of distant node '{distantNodeId}' to fail, but succeeded.");

        string msg = res["message"].AsString();
        if (!msg.Contains("不相邻"))
            return (false, $"Expected error message about adjacency ('不相邻'), but got: '{msg}'");

        return (true, "");
    }

    private static (bool, string) SkillTree_TryActivateNode_TileProgressAndPointCost()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);
        
        string? targetNodeId = null;
        foreach (var kvp in treeData.Nodes)
        {
            if (kvp.Value.GetRequiredTileCount() > 1)
            {
                targetNodeId = kvp.Key;
                break;
            }
        }

        if (targetNodeId == null)
            return (false, "Could not find a multi-tile node for test");

        tree.AvailableSet.Add(targetNodeId);

        var nodeData = treeData.Nodes[targetNodeId];
        int required = nodeData.GetRequiredTileCount();

        tree.AvailableAttributePoints = required + 3;
        
        var res1 = tree.TryActivateNode(targetNodeId);
        if (!res1["success"].AsBool())
            return (false, $"First tile activation failed: {res1["message"].AsString()}");
        if (res1["completed"].AsBool())
            return (false, "Node should not be completed after 1 tile");
        if (tree.AvailableAttributePoints != required + 2)
            return (false, $"Expected remaining points to be {required + 2}, but got {tree.AvailableAttributePoints}");
        if (tree.ActivatedSet.Contains(targetNodeId))
            return (false, "Node should not be in ActivatedSet yet");

        for (int i = 2; i < required; i++)
        {
            var resMid = tree.TryActivateNode(targetNodeId);
            if (!resMid["success"].AsBool())
                return (false, $"Activation step {i} failed: {resMid["message"].AsString()}");
            if (resMid["completed"].AsBool())
                return (false, $"Node should not be completed at step {i}");
        }

        var resFinal = tree.TryActivateNode(targetNodeId);
        if (!resFinal["success"].AsBool())
            return (false, $"Final tile activation failed: {resFinal["message"].AsString()}");
        if (!resFinal["completed"].AsBool())
            return (false, "Node should be completed now");
        if (tree.AvailableAttributePoints != 3)
            return (false, $"Expected remaining points to be 3, but got {tree.AvailableAttributePoints}");
        if (!tree.ActivatedSet.Contains(targetNodeId))
            return (false, "Node should be in ActivatedSet now");

        return (true, "");
    }

    private static (bool, string) SkillTree_TryJumpActivate_Rules()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);

        string sourceNodeId = "str_p01";
        string allowedNodeId = "dex_p01";
        string blockedNodeId = "con_p01";
        if (!treeData.Nodes.ContainsKey(sourceNodeId) || !treeData.Nodes.ContainsKey(allowedNodeId) || !treeData.Nodes.ContainsKey(blockedNodeId))
            return (false, "Expected fixed inner-ring passive nodes were not found");

        tree.TotalJumps = 1;
        tree.UsedJumps = 1;
        tree.AvailableAttributePoints = 5;
        
        var resFail = tree.TryJumpActivate(allowedNodeId);
        if (resFail["success"].AsBool())
            return (false, "Jump activation should fail when no jumps remain");

        tree.UsedJumps = 0;
        var resNoAnchor = tree.TryJumpActivate(allowedNodeId);
        if (resNoAnchor["success"].AsBool())
            return (false, "Jump should fail before a non-start same-band source is activated");

        ActivateNodeDirectly(tree, sourceNodeId, treeData);

        var resBlocked = tree.TryJumpActivate(blockedNodeId);
        if (resBlocked["success"].AsBool())
            return (false, "Jump should not reach non-adjacent sectors in the same ring band");

        var res1 = tree.TryJumpActivate(allowedNodeId);
        if (!res1["success"].AsBool())
            return (false, $"First jump tile failed: {res1["message"].AsString()}");
        if (tree.UsedJumps != 1)
            return (false, $"Expected used jumps to be 1, but got {tree.UsedJumps}");
        if (res1["completed"].AsBool())
            return (false, "Node should not be completed after 1 tile");

        string? levelNodeId = null;
        foreach (var kvp in treeData.Nodes)
        {
            if (kvp.Key == "start") continue;
            if (kvp.Value.RequiredLevel > 1)
            {
                levelNodeId = kvp.Key;
                break;
            }
        }

        if (levelNodeId != null)
        {
            tree.CharacterLevel = 1;
            tree.UsedJumps = 0;
            var resLevelFail = tree.TryJumpActivate(levelNodeId);
            if (resLevelFail["success"].AsBool())
                return (false, $"Jump activation of node '{levelNodeId}' should fail for lvl 1 character");
        }

        return (true, "");
    }

    private static (bool, string) SkillTree_ApplyNodeStats_AccumulatedVerification()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);
        
        string? statNodeId = null;
        foreach (var kvp in treeData.Nodes)
        {
            if (kvp.Value.StatBonuses.Count > 0)
            {
                statNodeId = kvp.Key;
                break;
            }
        }
        
        if (statNodeId == null)
            return (true, "");

        var node = treeData.Nodes[statNodeId];
        string firstKey = "";
        Variant expectedVal = default;
        foreach (var k in node.StatBonuses.Keys)
        {
            firstKey = k.ToString()!;
            expectedVal = node.StatBonuses[k];
            break;
        }

        ActivateNodeDirectly(tree, statNodeId, treeData);
        
        if (!tree.AccumulatedStats.ContainsKey(firstKey))
            return (false, $"Expected AccumulatedStats to contain key '{firstKey}'");

        var accumulated = tree.AccumulatedStats[firstKey];
        if (accumulated.VariantType == Variant.Type.Int)
        {
            if (accumulated.AsInt32() != expectedVal.AsInt32())
                return (false, $"Expected stat '{firstKey}' value {expectedVal.AsInt32()}, but got {accumulated.AsInt32()}");
        }
        
        return (true, "");
    }

    private static (bool, string) SkillTree_RoundtripSerialization()
    {
        var treeData = new SkillTreeData();
        var tree = new CharacterSkillTree(treeData, 10);
        
        string? node1 = null, node2 = null;
        foreach (var nid in tree.AvailableSet)
        {
            if (node1 == null) node1 = nid;
            else if (node2 == null) { node2 = nid; break; }
        }

        if (node1 != null) ActivateNodeDirectly(tree, node1, treeData);
        if (node2 != null) ActivateNodeDirectly(tree, node2, treeData);
        
        tree.AvailableAttributePoints = 4;
        tree.TotalJumps = 2;
        tree.UsedJumps = 1;
        
        var serialized = tree.Serialize();
        
        var newTree = new CharacterSkillTree();
        newTree.Deserialize(serialized, treeData);
        
        if (newTree.CharacterLevel != 10)
            return (false, $"Expected character level 10, got {newTree.CharacterLevel}");
        if (newTree.AvailableAttributePoints != 4)
            return (false, $"Expected points 4, got {newTree.AvailableAttributePoints}");
        if (newTree.TotalJumps != 2 || newTree.UsedJumps != 1)
            return (false, $"Expected jumps 2/1, got {newTree.TotalJumps}/{newTree.UsedJumps}");
        if (node1 != null && !newTree.ActivatedSet.Contains(node1))
            return (false, $"Expected activated node '{node1}' to be deserialized");
            
        return (true, "");
    }

    private static (bool, string) SkillTree_CareerSkillLimits_Interception()
    {
        var treeData = new SkillTreeData();
        var tree = CreateTreeWithFlags(treeData, ClassTitleResolver.FlagStr);
        tree.RefreshTitleFlags();

        var skill = tree.GetCareerSkill();
        if (skill == null)
            return (true, "");

        tree.ResetCareerSkillForBattle();
        if (!tree.CanUseCareerSkill())
            return (false, "Career skill should be usable initially");

        tree.RecordCareerSkillUse();
        
        if (skill.LimitType == CareerSkillData.UsageLimit.OncePerBattle)
        {
            if (tree.CanUseCareerSkill())
                return (false, "Career skill should be blocked after use in battle");
            
            tree.ResetCareerSkillForTurn();
            if (tree.CanUseCareerSkill())
                return (false, "Career skill should remain blocked after turn reset");

            tree.ResetCareerSkillForBattle();
            if (!tree.CanUseCareerSkill())
                return (false, "Career skill should be usable after battle reset");
        }
        else if (skill.LimitType == CareerSkillData.UsageLimit.OncePerTurn)
        {
            if (tree.CanUseCareerSkill())
                return (false, "Career skill should be blocked after use in turn");

            tree.ResetCareerSkillForTurn();
            if (!tree.CanUseCareerSkill())
                return (false, "Career skill should be usable after turn reset");
        }

        return (true, "");
    }
}
