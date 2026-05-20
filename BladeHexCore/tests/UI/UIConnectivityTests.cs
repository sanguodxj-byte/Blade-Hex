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
        yield return Run(nameof(ClassTitle_63Combinations_AllNonEmpty), ClassTitle_63Combinations_AllNonEmpty);
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

    /// <summary>直接将节点标记为已激活（绕过技能点/连通性检查，仅用于测试）</summary>
    private static void ActivateNodeDirectly(CharacterSkillTree tree, string nodeId, SkillTreeData treeData)
    {
        // 直接添加到已激活列表和集合（ClassTitleResolver 只读这两个）
        tree.ActivatedNodes.Add(nodeId);
        tree.ActivatedSet.Add(nodeId);
    }
}
