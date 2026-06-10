// SkillTreeLayoutTests.cs
// 技能星盘布局几何基础校验:
//   1. 节点内瓦片共边连通(一个宝石的瓦片彼此相连)
//   2. 节点之间瓦片不重叠
//   3. 每个非孤立节点至少与另一个节点共边(布局即连通)
//   4. 所有瓦片落在六边形画布内
// 这是"几何基础"的安全网,守护后续布局改动不破坏密铺规则。
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using BladeHex.Combat.Buff;
using BladeHex.Data;
using BladeHex.Scripting;
using BladeHex.Strategic;

namespace BladeHex.Combat.Tests;

public static class SkillTreeLayoutTests
{
    // Fixed JSON layout radius.
    private const int HexagonRadius = SkillTreeData.FixedLayoutRadius;
    private static readonly HashSet<string> GiantTemplates =
    [
        "apex_sunburst_12",
        "apex_arrowhead_12",
        "apex_bastion_12",
        "apex_crystal_12",
        "apex_hourglass_12",
        "apex_crown_12",
    ];

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

    private static IEnumerable<(string, bool, string)> EnumerateTests()
    {
        var tree = new SkillTreeData();
        var nodeTiles = BuildNodeTileMap(tree);

        yield return Run("NodeTiles_InternallyConnected", () => NodeTilesInternallyConnected(nodeTiles));
        yield return Run("NodeTiles_NoOverlap", () => NodeTilesNoOverlap(nodeTiles));
        yield return Run("Nodes_EachTouchesAnother", () => EachNodeTouchesAnother(nodeTiles));
        yield return Run("Tiles_InsideHex", () => TilesInsideHex(nodeTiles));
        yield return Run("Connectivity_AllReachableFromStart", () => AllReachableFromStart(tree, nodeTiles));
        yield return Run("DataDrivenLayout_UsesExplicitTiles", () => DataDrivenLayoutUsesExplicitTiles(tree));
        yield return Run("LayoutContent_IdsAreUniqueAndComplete", () => LayoutContentIdsAreUniqueAndComplete());
        yield return Run("RegionTiles_MatchDocumentSectorTriangles", () => RegionTilesMatchDocumentSectorTriangles(tree, nodeTiles));
        yield return Run("RegionTileCounts_FillExactSectors", () => RegionTileCountsFillExactSectors(tree, nodeTiles));
        yield return Run("RegionQuota_MatchesStarChartSpec", () => RegionQuotaMatchesStarChartSpec(tree));
        yield return Run("FixedLargeNodes_MatchPlannedRingBands", () => FixedLargeNodesMatchPlannedRingBands(tree));
        yield return Run("TileCost_MatchesExplicitTileCount", () => TileCostMatchesExplicitTileCount(tree));
        yield return Run("FigureTemplates_MatchNodeRoles", () => FigureTemplatesMatchNodeRoles(tree));
        yield return Run("GiantFigures_AreDistinctSymmetricTemplates", () => GiantFiguresAreDistinctSymmetricTemplates(tree));
        yield return Run("LargeFigures_AreCompactNotStrips", () => LargeFiguresAreCompactNotStrips(tree));
        yield return Run("PassiveNodeBonuses_DoNotFlattenConditionalEffects", () => PassiveNodeBonusesDoNotFlattenConditionalEffects(tree));
        yield return Run("FixedAttributeTiles_UseLegalKeysOnly", () => FixedAttributeTilesUseLegalKeysOnly(tree));
        yield return Run("FixedAttributeTiles_FollowSectorAndSmallRules", () => FixedAttributeTilesFollowSectorAndSmallRules(tree));
        yield return Run("FixedAttributeTiles_AreAppliedFromContentJson", () => FixedAttributeTilesAreAppliedFromContentJson(tree));
        yield return Run("CharacterSkillContent_IsShuffledBySeedWithinSectorRoles", () => CharacterSkillContentIsShuffledBySeedWithinSectorRoles(tree));
        yield return Run("AllNonStartTiles_GrantSectorAttributes", () => AllNonStartTilesGrantSectorAttributes(tree));
        yield return Run("IntSpellStudy_UsesRingBandsForTiers", () => IntSpellStudyUsesRingBandsForTiers(tree));
        yield return Run("ActiveEffects_AreRegisteredAndScripted", () => ActiveEffectsAreRegisteredAndScripted(tree));
        yield return Run("ActiveScripts_KeyModifiersMatchSkillDescriptions", () => ActiveScriptsKeyModifiersMatchSkillDescriptions());
        yield return Run("GiantApex_CombatContractsMatchSpec", () => GiantApexCombatContractsMatchSpec(tree));
        yield return Run("GiantApex_RuntimeRequiresWindowBuff", () => GiantApexRuntimeRequiresWindowBuff(tree));
        yield return Run("BattleUnitModel_RuntimeMirrorsUnitData", () => BattleUnitModelRuntimeMirrorsUnitData(tree));
        yield return Run("KeystoneEffects_RuntimeAliasesResolve", () => KeystoneEffectsRuntimeAliasesResolve(tree));
        yield return Run("KeystoneEffects_RuntimeContractsResolve", () => KeystoneEffectsRuntimeContractsResolve(tree));
        yield return Run("KeystoneFlows_EquipmentAndRandomSpellStudyRespectLocks", () => KeystoneFlowsEquipmentAndRandomSpellStudyRespectLocks(tree));
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> t)
    {
        try { var (ok, msg) = t(); return (name, ok, msg); }
        catch (System.Exception ex) { return (name, false, $"Exception: {ex.Message}"); }
    }

    private static Dictionary<string, Vector2I[]> BuildNodeTileMap(SkillTreeData tree)
    {
        var map = new Dictionary<string, Vector2I[]>();
        foreach (var kvp in tree.Nodes)
            map[kvp.Key] = SkillNodeShape.GetTiles(kvp.Value);
        return map;
    }

    // 1. 每个节点自身的瓦片必须共边连通(BFS 从首瓦片能走遍全部)
    private static (bool, string) NodeTilesInternallyConnected(Dictionary<string, Vector2I[]> nodeTiles)
    {
        foreach (var (id, tiles) in nodeTiles)
        {
            if (tiles.Length <= 1) continue;
            var set = new HashSet<Vector2I>(tiles);
            var seen = new HashSet<Vector2I> { tiles[0] };
            var queue = new Queue<Vector2I>();
            queue.Enqueue(tiles[0]);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var nb in SkillTreeCoord.GetTileNeighbors(cur))
                    if (set.Contains(nb) && seen.Add(nb)) queue.Enqueue(nb);
            }
            if (seen.Count != tiles.Length)
                return (false, $"节点 {id} 的 {tiles.Length} 瓦片不连通(只连到 {seen.Count})");
        }
        return (true, "");
    }

    // 2. 任意两节点瓦片不重叠
    private static (bool, string) NodeTilesNoOverlap(Dictionary<string, Vector2I[]> nodeTiles)
    {
        var owner = new Dictionary<Vector2I, string>();
        foreach (var (id, tiles) in nodeTiles)
            foreach (var t in tiles)
            {
                if (owner.TryGetValue(t, out var other))
                    return (false, $"瓦片 {t} 被 {other} 和 {id} 同时占用");
                owner[t] = id;
            }
        return (true, "");
    }

    // 3. 每个节点至少与另一个节点共边(无孤岛)
    private static (bool, string) EachNodeTouchesAnother(Dictionary<string, Vector2I[]> nodeTiles)
    {
        // 瓦片 → 所属节点
        var owner = new Dictionary<Vector2I, string>();
        foreach (var (id, tiles) in nodeTiles)
            foreach (var t in tiles)
                owner[t] = id;

        foreach (var (id, tiles) in nodeTiles)
        {
            bool touches = false;
            foreach (var t in tiles)
            {
                foreach (var nb in SkillTreeCoord.GetTileNeighbors(t))
                    if (owner.TryGetValue(nb, out var o) && o != id) { touches = true; break; }
                if (touches) break;
            }
            if (!touches)
                return (false, $"节点 {id} 是孤岛,不与任何其他节点共边");
        }
        return (true, "");
    }

    // 4. 所有瓦片在六边形画布内
    private static (bool, string) TilesInsideHex(Dictionary<string, Vector2I[]> nodeTiles)
    {
        foreach (var (id, tiles) in nodeTiles)
            foreach (var t in tiles)
                if (!SkillTreeCoord.IsTileInsideHex(t, HexagonRadius))
                    return (false, $"节点 {id} 的瓦片 {t} 超出半径 {HexagonRadius} 六边形");
        return (true, "");
    }

    // 5. 从 start 沿共边能到达所有节点(整盘连通,无断裂)
    private static (bool, string) AllReachableFromStart(SkillTreeData tree, Dictionary<string, Vector2I[]> nodeTiles)
    {
        var owner = new Dictionary<Vector2I, string>();
        foreach (var (id, tiles) in nodeTiles)
            foreach (var t in tiles)
                owner[t] = id;

        // 节点级邻接图(共边)
        var adj = new Dictionary<string, HashSet<string>>();
        foreach (var id in nodeTiles.Keys) adj[id] = new HashSet<string>();
        foreach (var (id, tiles) in nodeTiles)
            foreach (var t in tiles)
                foreach (var nb in SkillTreeCoord.GetTileNeighbors(t))
                    if (owner.TryGetValue(nb, out var o) && o != id)
                    { adj[id].Add(o); adj[o].Add(id); }

        const string start = SkillTreeData.StartNodeId;
        if (!adj.ContainsKey(start)) return (false, "找不到 start 节点");

        var seen = new HashSet<string> { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var nb in adj[cur])
                if (seen.Add(nb)) queue.Enqueue(nb);
        }

        if (seen.Count != nodeTiles.Count)
        {
            var unreached = nodeTiles.Keys.Where(k => !seen.Contains(k)).Take(5);
            return (false, $"从 start 只能到达 {seen.Count}/{nodeTiles.Count} 个节点。未达示例: {string.Join(", ", unreached)}");
        }
        return (true, "");
    }

    private static (bool, string) DataDrivenLayoutUsesExplicitTiles(SkillTreeData tree)
    {
        if (tree.GetNodeCount() < 1000)
            return (false, $"期望 JSON 高密度星盘至少 1000 节点, 实际 {tree.GetNodeCount()}");

        foreach (var (id, node) in tree.Nodes)
        {
            if (node.ExplicitTiles.Length == 0)
                return (false, $"{id} 未使用显式瓦片坐标");
        }

        return (true, "");
    }

    private static (bool, string) LayoutContentIdsAreUniqueAndComplete()
    {
        string? repoRoot = FindRepoRoot();
        if (repoRoot == null)
            return (false, "找不到 repo root");

        string layoutPath = Path.Combine(repoRoot, "BladeHexCore", "src", "SkillTree", "skill_tree_layout.json");
        string contentPath = Path.Combine(repoRoot, "BladeHexCore", "src", "SkillTree", "skill_tree_content.json");
        if (!File.Exists(layoutPath) || !File.Exists(contentPath))
            return (false, "找不到 skill_tree_layout.json 或 skill_tree_content.json");

        using var layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutPath));
        using var contentDoc = JsonDocument.Parse(File.ReadAllText(contentPath));

        var (layoutIds, layoutDuplicate) = ReadJsonNodeIds(layoutDoc.RootElement, "layout");
        if (!string.IsNullOrEmpty(layoutDuplicate))
            return (false, layoutDuplicate);

        var (contentIds, contentDuplicate) = ReadJsonNodeIds(contentDoc.RootElement, "content");
        if (!string.IsNullOrEmpty(contentDuplicate))
            return (false, contentDuplicate);

        var missingContent = layoutIds.Except(contentIds).Take(8).ToArray();
        if (missingContent.Length > 0)
            return (false, $"layout 节点缺少 content: {string.Join(", ", missingContent)}");

        var unusedContent = contentIds.Except(layoutIds).Take(8).ToArray();
        if (unusedContent.Length > 0)
            return (false, $"content 节点不在 layout 中: {string.Join(", ", unusedContent)}");

        return (true, "");
    }

    private static (HashSet<string> ids, string duplicateMessage) ReadJsonNodeIds(JsonElement root, string label)
    {
        var ids = new HashSet<string>();
        foreach (var nodeElement in root.GetProperty("nodes").EnumerateArray())
        {
            string id = nodeElement.GetProperty("id").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(id))
                return (ids, $"{label} 存在空节点 ID");
            if (!ids.Add(id))
                return (ids, $"{label} 存在重复节点 ID: {id}");
        }

        return (ids, "");
    }

    private static (bool, string) RegionTilesMatchDocumentSectorTriangles(SkillTreeData tree, Dictionary<string, Vector2I[]> nodeTiles)
    {
        var coord = new SkillTreeCoord { HexSize = 1.0f };
        var sectorIndexByRegion = new Dictionary<SkillNodeData.Region, int>
        {
            { SkillNodeData.Region.Int, 0 },
            { SkillNodeData.Region.Con, 1 },
            { SkillNodeData.Region.Str, 2 },
            { SkillNodeData.Region.Dex, 3 },
            { SkillNodeData.Region.Cha, 4 },
            { SkillNodeData.Region.Wis, 5 },
        };

        var hexVertices = new[]
        {
            new Vector2I(HexagonRadius, 0),
            new Vector2I(0, HexagonRadius),
            new Vector2I(-HexagonRadius, HexagonRadius),
            new Vector2I(-HexagonRadius, 0),
            new Vector2I(0, -HexagonRadius),
            new Vector2I(HexagonRadius, -HexagonRadius),
        };

        foreach (var (region, sectorIndex) in sectorIndexByRegion)
        {
            var tiles = tree.Nodes.Values
                .Where(n => n.CurrentRegion == region)
                .SelectMany(n => nodeTiles[n.NodeId])
                .ToList();
            if (tiles.Count == 0)
                return (false, $"{region} 没有布局瓦片");

            foreach (var tile in tiles)
            {
                var a = coord.VertexToPixel(0, 0);
                var bVertex = hexVertices[sectorIndex];
                var cVertex = hexVertices[(sectorIndex + 1) % hexVertices.Length];
                var b = coord.VertexToPixel(bVertex.X, bVertex.Y);
                var c = coord.VertexToPixel(cVertex.X, cVertex.Y);

                foreach (var vertex in GetTileVertexCoords(tile))
                {
                    var point = coord.VertexToPixel(vertex.X, vertex.Y);
                    if (!PointInTriangle(point, a, b, c))
                        return (false, $"{region} 瓦片 {tile} 顶点 {vertex} 不在文档扇区三角形 {sectorIndex} 内");
                }
            }
        }

        return (true, "");
    }

    private static Vector2I[] GetTileVertexCoords(Vector2I encoded)
    {
        var (q, r, t) = SkillTreeCoord.DecodeTile(encoded);
        return t == 0
            ? [new Vector2I(q, r), new Vector2I(q + 1, r), new Vector2I(q, r + 1)]
            : [new Vector2I(q + 1, r), new Vector2I(q, r + 1), new Vector2I(q + 1, r + 1)];
    }

    private static int TileRing(Vector2I encoded)
    {
        int maxRing = 0;
        foreach (var vertex in GetTileVertexCoords(encoded))
        {
            int s = -vertex.X - vertex.Y;
            maxRing = System.Math.Max(maxRing, System.Math.Max(System.Math.Abs(vertex.X), System.Math.Max(System.Math.Abs(vertex.Y), System.Math.Abs(s))));
        }
        return maxRing;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = SignedTriangleArea(p, a, b);
        float d2 = SignedTriangleArea(p, b, c);
        float d3 = SignedTriangleArea(p, c, a);
        const float epsilon = 0.0001f;
        bool hasNeg = d1 < -epsilon || d2 < -epsilon || d3 < -epsilon;
        bool hasPos = d1 > epsilon || d2 > epsilon || d3 > epsilon;
        return !(hasNeg && hasPos);
    }

    private static float SignedTriangleArea(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private static (bool, string) RegionTileCountsFillExactSectors(SkillTreeData tree, Dictionary<string, Vector2I[]> nodeTiles)
    {
        int expectedBoardTiles = HexagonRadius * HexagonRadius * 6;
        int totalTiles = nodeTiles.Values.Sum(tiles => tiles.Length);
        if (totalTiles != expectedBoardTiles)
            return (false, $"整盘瓦片数错误: expected={expectedBoardTiles}, actual={totalTiles}");

        if (!nodeTiles.TryGetValue(SkillTreeData.StartNodeId, out var startTiles) || startTiles.Length != 6)
            return (false, $"start 应占 6 个中心瓦片, actual={(startTiles == null ? 0 : startTiles.Length)}");

        var regions = new[]
        {
            SkillNodeData.Region.Str,
            SkillNodeData.Region.Dex,
            SkillNodeData.Region.Con,
            SkillNodeData.Region.Int,
            SkillNodeData.Region.Wis,
            SkillNodeData.Region.Cha,
        };

        int expectedContentTilesPerSector = HexagonRadius * HexagonRadius - 1;
        foreach (var region in regions)
        {
            int actual = tree.Nodes.Values
                .Where(node => node.CurrentRegion == region)
                .Sum(node => nodeTiles[node.NodeId].Length);
            if (actual != expectedContentTilesPerSector)
                return (false, $"{region} 内容瓦片数错误: expected={expectedContentTilesPerSector}, actual={actual}");
        }

        int transitionTiles = tree.Nodes.Values
            .Where(node => node.CurrentRegion == SkillNodeData.Region.Transition)
            .Sum(node => nodeTiles[node.NodeId].Length);
        if (transitionTiles != 0)
            return (false, $"最终重排方案不应保留 transition 瓦片, actual={transitionTiles}");

        return (true, "");
    }

    private static (bool, string) RegionQuotaMatchesStarChartSpec(SkillTreeData tree)
    {
        var regions = new[]
        {
            SkillNodeData.Region.Str,
            SkillNodeData.Region.Dex,
            SkillNodeData.Region.Con,
            SkillNodeData.Region.Int,
            SkillNodeData.Region.Wis,
            SkillNodeData.Region.Cha,
        };

        foreach (var region in regions)
        {
            var nodes = tree.Nodes.Values.Where(n => n.CurrentRegion == region).ToList();
            int giants = nodes.Count(n => n.CurrentNodeType == SkillNodeData.NodeType.Giant);
            int keystones = nodes.Count(n => n.CurrentNodeType == SkillNodeData.NodeType.Keystone);
            int actives = nodes.Count(n => n.CurrentNodeType == SkillNodeData.NodeType.Big && n.IsActiveSkill);
            int passives = nodes.Count(n => n.CurrentNodeType == SkillNodeData.NodeType.Big && !n.IsActiveSkill);
            int small = nodes.Count(n => n.CurrentNodeType == SkillNodeData.NodeType.Small);
            int pips = nodes.Count(n => n.CurrentNodeType == SkillNodeData.NodeType.Pip);
            int smallPipTiles = nodes
                .Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Small || n.CurrentNodeType == SkillNodeData.NodeType.Pip)
                .Sum(n => SkillNodeShape.GetTiles(n).Length);

            if (giants != 1 || keystones != 3 || actives != 6 || passives != 8 || smallPipTiles != 321)
            {
                return (false,
                    $"{region} 配额错误: giant={giants}, ks={keystones}, active={actives}, passive={passives}, small={small}, pip={pips}, smallPipTiles={smallPipTiles}");
            }
        }

        var pipCounts = regions
            .Select(region => tree.Nodes.Values.Count(n => n.CurrentRegion == region && n.CurrentNodeType == SkillNodeData.NodeType.Pip))
            .ToArray();
        if (pipCounts.Max() - pipCounts.Min() > 20)
            return (false, $"pip 分布过于不均: {string.Join(",", pipCounts)}");

        var bridges = tree.Nodes.Values.Where(n => n.CurrentRegion == SkillNodeData.Region.Transition && n.IsBridge).ToList();
        if (bridges.Count != 0)
            return (false, $"最终重排方案不应生成过渡桥节点, 实际 {bridges.Count}");

        return (true, "");
    }

    private static (bool, string) FixedLargeNodesMatchPlannedRingBands(SkillTreeData tree)
    {
        var expected = new Dictionary<string, (int min, int max, float center)>
        {
            ["p01"] = (1, 8, 4.5f),
            ["p02"] = (1, 8, 4.5f),
            ["ks01"] = (1, 8, 4.5f),
            ["a01"] = (1, 8, 4.5f),
            ["a02"] = (1, 8, 4.5f),
            ["p03"] = (8, 15, 11.5f),
            ["p04"] = (8, 15, 11.5f),
            ["p05"] = (8, 15, 11.5f),
            ["p06"] = (8, 15, 11.5f),
            ["ks02"] = (8, 15, 11.5f),
            ["ks03"] = (8, 15, 11.5f),
            ["a03"] = (8, 15, 11.5f),
            ["a04"] = (8, 15, 11.5f),
            ["g01"] = (16, 20, 18.0f),
            ["p07"] = (16, 20, 18.0f),
            ["p08"] = (16, 20, 18.0f),
            ["a05"] = (16, 20, 18.0f),
            ["a06"] = (16, 20, 18.0f),
        };

        foreach (var (id, node) in tree.Nodes)
        {
            if (node.CurrentRegion == SkillNodeData.Region.Transition || id == SkillTreeData.StartNodeId)
                continue;

            string suffix = id[(id.IndexOf('_') + 1)..];
            if (!expected.TryGetValue(suffix, out var plan))
                continue;

            var rings = SkillNodeShape.GetTiles(node).Select(TileRing).ToArray();
            int min = rings.Min();
            int max = rings.Max();
            float average = rings.Sum() / (float)rings.Length;

            if (min < plan.min || max > plan.max)
                return (false, $"{id} 不在规划环带内: expected={plan.min}-{plan.max}, actual={min}-{max}");

            float tolerance = node.CurrentNodeType == SkillNodeData.NodeType.Giant ? 1.8f : 2.2f;
            if (System.MathF.Abs(average - plan.center) > tolerance)
                return (false, $"{id} 中心半径偏离规划带中心: expected~{plan.center:0.0}, actual={average:0.0}, rings={string.Join(",", rings)}");
        }

        return (true, "");
    }

    private static (bool, string) TileCostMatchesExplicitTileCount(SkillTreeData tree)
    {
        foreach (var (id, node) in tree.Nodes)
        {
            int expected = node.CurrentNodeType == SkillNodeData.NodeType.Start ? 0 : node.ExplicitTiles.Length;
            int actual = node.GetRequiredTileCount();
            if (actual != expected)
                return (false, $"{id} 点亮成本 {actual} 不等于显式瓦片数 {expected}");

            if (node.CurrentNodeType == SkillNodeData.NodeType.Giant && node.ExplicitTiles.Length != 12)
                return (false, $"{id} 巨型节点不是 12 瓦片");
            if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone && node.ExplicitTiles.Length != 6)
                return (false, $"{id} keystone 不是 6 瓦片");
            if (node.CurrentNodeType == SkillNodeData.NodeType.Big && node.IsActiveSkill && node.ExplicitTiles.Length != 4)
                return (false, $"{id} 主动节点不是 4 瓦片");
            if (node.CurrentNodeType == SkillNodeData.NodeType.Big && !node.IsActiveSkill && node.ExplicitTiles.Length != 3)
                return (false, $"{id} 被动节点不是 3 瓦片");
            if (node.CurrentNodeType == SkillNodeData.NodeType.Small && node.ExplicitTiles.Length != 2)
                return (false, $"{id} 小瓦片节点不是 2 瓦片");
            if (node.CurrentNodeType == SkillNodeData.NodeType.Pip && node.ExplicitTiles.Length != 1)
                return (false, $"{id} pip 不是 1 瓦片");
        }

        return (true, "");
    }

    private static (bool, string) FigureTemplatesMatchNodeRoles(SkillTreeData tree)
    {
        foreach (var (id, node) in tree.Nodes)
        {
            string expected = node.CurrentNodeType switch
            {
                SkillNodeData.NodeType.Start => "start_core_6",
                SkillNodeData.NodeType.Pip => "pip_1",
                SkillNodeData.NodeType.Small => "attribute_pair_2",
                SkillNodeData.NodeType.Big when node.IsActiveSkill => "active_kite_4",
                SkillNodeData.NodeType.Big => "passive_triad_3",
                SkillNodeData.NodeType.Keystone => "keystone_crown_6",
                SkillNodeData.NodeType.Giant => "apex_*_12",
                _ => "",
            };

            string actual = node.GetFigureTemplate();
            if (node.CurrentNodeType == SkillNodeData.NodeType.Giant)
            {
                if (!GiantTemplates.Contains(actual))
                    return (false, $"{id} 巨型模板不在允许列表: actual={actual}");
                continue;
            }

            if (actual != expected)
                return (false, $"{id} 模板错误: expected={expected}, actual={actual}");
        }

        var usedGiantTemplates = tree.Nodes.Values
            .Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Giant)
            .Select(n => n.GetFigureTemplate())
            .ToHashSet();
        if (usedGiantTemplates.Count != GiantTemplates.Count)
            return (false, $"6 个巨型节点应使用 6 种不同模板, actual={string.Join(", ", usedGiantTemplates)}");

        return (true, "");
    }

    private static (bool, string) LargeFiguresAreCompactNotStrips(SkillTreeData tree)
    {
        var coord = new SkillTreeCoord { HexSize = 1.0f };
        foreach (var (id, node) in tree.Nodes)
        {
            if (node.CurrentNodeType != SkillNodeData.NodeType.Big &&
                node.CurrentNodeType != SkillNodeData.NodeType.Keystone &&
                node.CurrentNodeType != SkillNodeData.NodeType.Giant)
                continue;

            var tiles = SkillNodeShape.GetTiles(node);
            var points = tiles.Select(coord.TileCentroid).ToList();
            float width = points.Max(p => p.X) - points.Min(p => p.X);
            float height = points.Max(p => p.Y) - points.Min(p => p.Y);
            float ratio = System.MathF.Max(width, height) / System.MathF.Max(0.001f, System.MathF.Min(width, height));
            float limit = node.CurrentNodeType == SkillNodeData.NodeType.Big ? 3.6f : 2.7f;

            if (ratio > limit)
                return (false, $"{id} 形状过长: ratio={ratio:0.00}, limit={limit:0.00}, width={width:0.00}, height={height:0.00}");
        }

        return (true, "");
    }

    private static (bool, string) GiantFiguresAreDistinctSymmetricTemplates(SkillTreeData tree)
    {
        var normalizedShapes = new HashSet<string>();
        foreach (var node in tree.Nodes.Values.Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Giant))
        {
            var tiles = SkillNodeShape.GetTiles(node).ToHashSet();
            if (tiles.Count != 12)
                return (false, $"{node.NodeId}: 巨型节点应由 12 个三角瓦片组成, actual={tiles.Count}");

            string normalized = NormalizeTiles(tiles);
            if (!normalizedShapes.Add(normalized))
                return (false, $"{node.NodeId}: 巨型模板形状与另一个巨型节点重复");

            if (!HasNonTrivialTileSymmetry(tiles))
                return (false, $"{node.NodeId}: 巨型模板缺少旋转/镜像对称");
        }

        if (normalizedShapes.Count != GiantTemplates.Count)
            return (false, $"巨型节点数量/模板数量不匹配: shapes={normalizedShapes.Count}, templates={GiantTemplates.Count}");

        return (true, "");
    }

    private static bool HasNonTrivialTileSymmetry(HashSet<Vector2I> tiles)
    {
        string original = NormalizeTiles(tiles);
        for (int transformIndex = 1; transformIndex < 12; transformIndex++)
        {
            var transformed = new HashSet<Vector2I>();
            bool ok = true;
            foreach (var tile in tiles)
            {
                if (!TryTransformTile(tile, transformIndex, out var transformedTile))
                {
                    ok = false;
                    break;
                }
                transformed.Add(transformedTile);
            }

            if (ok && NormalizeTiles(transformed) == original)
                return true;
        }

        return false;
    }

    private static bool TryTransformTile(Vector2I tile, int transformIndex, out Vector2I transformedTile)
    {
        var transformedVertices = GetTileVertexCoords(tile)
            .Select(vertex => TransformHexCell(vertex, transformIndex))
            .ToArray();
        return TryEncodeTileFromVertices(transformedVertices, out transformedTile);
    }

    private static bool TryEncodeTileFromVertices(Vector2I[] vertices, out Vector2I tile)
    {
        var vertexSet = vertices.ToHashSet();
        int minQ = vertices.Min(v => v.X);
        int maxQ = vertices.Max(v => v.X);
        int minR = vertices.Min(v => v.Y);
        int maxR = vertices.Max(v => v.Y);

        for (int q = minQ - 1; q <= maxQ; q++)
        {
            for (int r = minR - 1; r <= maxR; r++)
            {
                var down = new[]
                {
                    new Vector2I(q, r),
                    new Vector2I(q + 1, r),
                    new Vector2I(q, r + 1),
                }.ToHashSet();
                if (down.SetEquals(vertexSet))
                {
                    tile = SkillTreeCoord.EncodeTile(q, r, 0);
                    return true;
                }

                var up = new[]
                {
                    new Vector2I(q + 1, r),
                    new Vector2I(q, r + 1),
                    new Vector2I(q + 1, r + 1),
                }.ToHashSet();
                if (up.SetEquals(vertexSet))
                {
                    tile = SkillTreeCoord.EncodeTile(q, r, 1);
                    return true;
                }
            }
        }

        tile = default;
        return false;
    }

    private static Vector2I TransformHexCell(Vector2I cell, int transformIndex)
    {
        var reflected = transformIndex >= 6
            ? new Vector2I(cell.X, -cell.X - cell.Y)
            : cell;
        return RotateHexCell(reflected, transformIndex % 6);
    }

    private static Vector2I RotateHexCell(Vector2I cell, int rotation)
    {
        int q = cell.X;
        int r = cell.Y;
        return rotation switch
        {
            0 => new Vector2I(q, r),
            1 => new Vector2I(-r, q + r),
            2 => new Vector2I(-q - r, q),
            3 => new Vector2I(-q, -r),
            4 => new Vector2I(r, -q - r),
            5 => new Vector2I(q + r, -q),
            _ => cell,
        };
    }

    private static string NormalizeCells(HashSet<Vector2I> cells)
    {
        int minQ = cells.Min(cell => cell.X);
        int minR = cells.Min(cell => cell.Y);
        return string.Join(";",
            cells.Select(cell => new Vector2I(cell.X - minQ, cell.Y - minR))
                .OrderBy(cell => cell.X)
                .ThenBy(cell => cell.Y)
                .Select(cell => $"{cell.X},{cell.Y}"));
    }

    private static string NormalizeTiles(HashSet<Vector2I> tiles)
    {
        var decoded = tiles.Select(tile =>
        {
            var (q, r, t) = SkillTreeCoord.DecodeTile(tile);
            return (q, r, t);
        }).ToArray();
        int minQ = decoded.Min(tile => tile.q);
        int minR = decoded.Min(tile => tile.r);
        return string.Join(";",
            decoded.Select(tile => (q: tile.q - minQ, r: tile.r - minR, tile.t))
                .OrderBy(tile => tile.q)
                .ThenBy(tile => tile.r)
                .ThenBy(tile => tile.t)
                .Select(tile => $"{tile.q},{tile.r},{tile.t}"));
    }

    private static (bool, string) FixedAttributeTilesUseLegalKeysOnly(SkillTreeData tree)
    {
        var legal = new HashSet<string>
        {
            "max_hp", "ac", "melee_hit", "melee_damage_percent", "ranged_hit", "ranged_damage_percent",
            "critical_rate", "speed", "mana_max", "mana_regen", "initiative", "all_save",
            "range_bonus", "spell_damage_percent", "heal_amount_percent", "ally_bonus",
        };

        foreach (var (id, node) in tree.Nodes)
        {
            if (node.CurrentNodeType != SkillNodeData.NodeType.Small && node.CurrentNodeType != SkillNodeData.NodeType.Pip)
                continue;

            if (node.CurrentContentMode == SkillNodeData.ContentMode.RandomAttribute)
                return (false, $"{id} small/pip 已改为生成期固定属性，不应继续使用 random_attribute");
            if (node.StatBonuses.Count == 0)
                return (false, $"{id} fixed attribute node has no statBonuses");

            foreach (var key in node.StatBonuses.Keys)
            {
                string stat = key.ToString()!;
                if (!legal.Contains(stat))
                    return (false, $"{id} 使用非法属性键 {stat}");
            }
        }

        return (true, "");
    }

    private static (bool, string) PassiveNodeBonusesDoNotFlattenConditionalEffects(SkillTreeData tree)
    {
        var expectedFlatBonuses = new Dictionary<string, string[]>
        {
            ["str_p05"] = ["max_hp"],
            ["dex_p05"] = ["critical_rate"],
            ["dex_p06"] = ["speed"],
            ["con_p03"] = ["max_hp"],
            ["con_p05"] = ["all_save"],
            ["int_p04"] = ["mana_max"],
            ["int_p05"] = ["mana_regen"],
            ["int_p06"] = ["spell_damage_percent"],
            ["int_p07"] = ["spell_damage_percent"],
            ["wis_p02"] = ["critical_rate"],
            ["wis_p03"] = ["critical_rate"],
            ["wis_p05"] = ["mana_max"],
            ["cha_p01"] = ["ally_bonus"],
            ["cha_p02"] = ["ally_bonus"],
            ["cha_p03"] = ["ally_bonus"],
            ["cha_p04"] = ["ally_bonus"],
            ["cha_p06"] = ["initiative"],
            ["cha_p07"] = ["max_hp"],
            ["cha_p08"] = ["ally_bonus"],
        };

        foreach (var (id, node) in tree.Nodes)
        {
            if (!id.Contains("_p") || node.CurrentNodeType != SkillNodeData.NodeType.Big || node.IsActiveSkill)
                continue;

            var actualKeys = node.StatBonuses.Keys.Select(k => k.ToString()!).OrderBy(k => k).ToArray();
            if (!expectedFlatBonuses.TryGetValue(id, out var expected))
            {
                if (actualKeys.Length != 0)
                    return (false, $"{id} 是条件/触发被动，不应写入平面 statBonuses: {string.Join(",", actualKeys)}");
                continue;
            }

            expected = expected.OrderBy(k => k).ToArray();
            if (!actualKeys.SequenceEqual(expected))
                return (false, $"{id} 平面 statBonuses 错误: expected={string.Join(",", expected)}, actual={string.Join(",", actualKeys)}");
        }

        return (true, "");
    }

    private static (bool, string) FixedAttributeTilesFollowSectorAndSmallRules(SkillTreeData tree)
    {
        var sectorStats = new Dictionary<SkillNodeData.Region, (string stat, double value)>
        {
            [SkillNodeData.Region.Str] = ("melee_damage_percent", 0.02),
            [SkillNodeData.Region.Dex] = ("ranged_damage_percent", 0.02),
            [SkillNodeData.Region.Con] = ("max_hp", 5),
            [SkillNodeData.Region.Int] = ("mana_max", 3),
            [SkillNodeData.Region.Wis] = ("critical_rate", 0.01),
            [SkillNodeData.Region.Cha] = ("ally_bonus", 1),
        };
        var extraStats = new Dictionary<string, double>
        {
            ["max_hp"] = 5,
            ["mana_max"] = 3,
            ["critical_rate"] = 0.01,
        };

        foreach (var (id, node) in tree.Nodes)
        {
            if (node.CurrentNodeType != SkillNodeData.NodeType.Small && node.CurrentNodeType != SkillNodeData.NodeType.Pip)
                continue;

            if (!sectorStats.TryGetValue(node.CurrentRegion, out var sector))
                return (false, $"{id} has no fixed sector stat for {node.CurrentRegion}");

            if (node.CurrentNodeType == SkillNodeData.NodeType.Pip)
            {
                if (node.StatBonuses.Count != 1)
                    return (false, $"{id} pip must have exactly one fixed sector stat, actual {node.StatBonuses.Count}");
                if (!node.StatBonuses.ContainsKey(sector.stat))
                    return (false, $"{id} pip missing sector stat {sector.stat}");
                if (!VariantNumberEquals(node.StatBonuses[sector.stat], sector.value))
                    return (false, $"{id} pip sector stat value wrong");
            }
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Small)
            {
                double expectedSectorValue = sector.value * 2.0d;
                if (!node.StatBonuses.ContainsKey(sector.stat))
                    return (false, $"{id} small missing sector stat {sector.stat}");

                double actualSectorValue = VariantToDouble(node.StatBonuses[sector.stat]);
                double extraOnSameStat = extraStats.TryGetValue(sector.stat, out var extraValue) ? extraValue : 0.0d;
                bool hasSameStatExtra = System.Math.Abs(actualSectorValue - (expectedSectorValue + extraOnSameStat)) < 0.0001d;
                if (System.Math.Abs(actualSectorValue - expectedSectorValue) > 0.0001d && !hasSameStatExtra)
                    return (false, $"{id} small sector stat value {actualSectorValue} does not match {expectedSectorValue}");

                int extraCount = hasSameStatExtra ? 1 : 0;
                foreach (var key in node.StatBonuses.Keys)
                {
                    string stat = key.ToString()!;
                    if (stat == sector.stat)
                        continue;
                    if (!extraStats.TryGetValue(stat, out double expectedExtra))
                        return (false, $"{id} small has non-extra stat {stat}");
                    if (!VariantNumberEquals(node.StatBonuses[key], expectedExtra))
                        return (false, $"{id} small extra stat {stat} value wrong");
                    extraCount++;
                }

                if (extraCount != 1)
                    return (false, $"{id} small must have exactly one hp/mana/crit extra, actual {extraCount}");
            }
        }

        return (true, "");
    }

    private static (bool, string) FixedAttributeTilesAreAppliedFromContentJson(SkillTreeData tree)
    {
        var node = tree.Nodes.Values.FirstOrDefault(n =>
            n.CurrentNodeType == SkillNodeData.NodeType.Small
            && n.CurrentRegion != SkillNodeData.Region.Transition);
        if (node == null)
            return (false, "no fixed small node found");

        var seeded = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 202);
        var expected = seeded.GetNodeStatBonusesForCharacter(node);
        if (!DictionariesEqual(expected, node.StatBonuses))
            return (false, $"{node.NodeId} fixed node should use content JSON bonuses directly");
        if (!ActivateNodeCompletely(seeded, node.NodeId))
            return (false, $"failed to activate {node.NodeId}");

        var accumulated = seeded.GetAllAccumulatedStats();
        foreach (var key in expected.Keys)
        {
            string stat = key.ToString()!;
            if (!accumulated.ContainsKey(stat))
                return (false, $"activated random node did not add {stat}");
            if (!VariantNumberEquals(accumulated[stat], VariantToDouble(expected[key])))
                return (false, $"activated random node used shared/default value for {stat}");
        }

        return (true, "");
    }

    private static (bool, string) CharacterSkillContentIsShuffledBySeedWithinSectorRoles(SkillTreeData tree)
    {
        var sample = tree.Nodes.Values
            .Where(n => n.CurrentRegion == SkillNodeData.Region.Str
                && n.CurrentNodeType == SkillNodeData.NodeType.Big
                && n.IsActiveSkill)
            .OrderBy(n => n.NodeId)
            .FirstOrDefault();
        if (sample == null)
            return (false, "no STR active node found");

        var sameSeedA = new CharacterSkillTree(tree, level: 20, randomAttributeSeed: 101);
        var sameSeedB = new CharacterSkillTree(tree, level: 20, randomAttributeSeed: 101);
        var otherSeed = new CharacterSkillTree(tree, level: 20, randomAttributeSeed: 202);

        string effectA = sameSeedA.GetEffectiveNode(sample).SkillEffect;
        string effectA2 = sameSeedB.GetEffectiveNode(sample).SkillEffect;
        string effectB = otherSeed.GetEffectiveNode(sample).SkillEffect;
        if (effectA != effectA2)
            return (false, "same character seed must reproduce identical skill content placement");
        if (effectA == sample.SkillEffect)
            return (false, $"{sample.NodeId} should not keep its original effect after same-sector role shuffle");

        var originalEffects = tree.Nodes.Values
            .Where(n => n.CurrentRegion == sample.CurrentRegion
                && n.CurrentNodeType == sample.CurrentNodeType
                && n.IsActiveSkill == sample.IsActiveSkill)
            .Select(n => n.SkillEffect)
            .OrderBy(e => e)
            .ToArray();
        var shuffledEffects = tree.Nodes.Values
            .Where(n => n.CurrentRegion == sample.CurrentRegion
                && n.CurrentNodeType == sample.CurrentNodeType
                && n.IsActiveSkill == sample.IsActiveSkill)
            .Select(n => otherSeed.GetEffectiveNode(n).SkillEffect)
            .OrderBy(e => e)
            .ToArray();
        if (!originalEffects.SequenceEqual(shuffledEffects))
            return (false, "shuffle must preserve same-sector same-role content pool");
        if (effectA == effectB)
            return (false, "different seeds should be able to place different content on the same geometry node");

        return (true, "");
    }

    private static bool ActivateNodeCompletely(CharacterSkillTree tree, string nodeId)
    {
        int required = tree.GetRequiredTileCount(nodeId);
        tree.AddAttributePoint(required);
        for (int i = 0; i < required; i++)
            tree.RegisterJump();
        for (int i = 0; i < required; i++)
        {
            var result = tree.TryJumpActivate(nodeId);
            if (!result.ContainsKey("success") || !result["success"].AsBool())
                return false;
        }
        return tree.IsActivated(nodeId);
    }

    private static bool DictionariesEqual(Godot.Collections.Dictionary a, Godot.Collections.Dictionary b)
    {
        if (a.Count != b.Count) return false;
        foreach (var key in a.Keys)
        {
            if (!b.ContainsKey(key)) return false;
            if (System.Math.Abs(VariantToDouble(a[key]) - VariantToDouble(b[key])) > 0.0001d)
                return false;
        }
        return true;
    }

    private static (bool, string) AllNonStartTilesGrantSectorAttributes(SkillTreeData tree)
    {
        var candidates = tree.Nodes.Values
            .Where(n => n.CurrentNodeType != SkillNodeData.NodeType.Start
                && n.CurrentRegion is not SkillNodeData.Region.None and not SkillNodeData.Region.Transition)
            .GroupBy(n => n.CurrentRegion)
            .Select(g => g.First())
            .ToArray();
        if (candidates.Length != 6)
            return (false, $"expected one sample per attribute sector, got {candidates.Length}");

        foreach (var node in candidates)
        {
            string expectedKey = node.CurrentRegion switch
            {
                SkillNodeData.Region.Str => "str",
                SkillNodeData.Region.Dex => "dex",
                SkillNodeData.Region.Con => "con",
                SkillNodeData.Region.Int => "int",
                SkillNodeData.Region.Wis => "wis",
                SkillNodeData.Region.Cha => "cha",
                _ => "",
            };
            if (string.IsNullOrEmpty(expectedKey))
                return (false, $"{node.NodeId} has unsupported region {node.CurrentRegion}");

            var save = new Godot.Collections.Dictionary
            {
                { "activated_nodes", new Godot.Collections.Array { SkillTreeData.StartNodeId } },
                { "available_attribute_points", 0 },
                { "random_attribute_seed", 101 },
                { "node_tile_progress", new Godot.Collections.Dictionary { { node.NodeId, 1 } } },
            };

            var characterTree = new CharacterSkillTree();
            characterTree.Deserialize(save, tree);
            var attributes = characterTree.GetAllAccumulatedAttributes();
            if (!attributes.ContainsKey(expectedKey) || attributes[expectedKey].AsInt32() != 1)
                return (false, $"{node.NodeId} should grant {expectedKey}+1 for one filled tile, got {attributes}");
        }

        return (true, "");
    }

    private static (bool, string) IntSpellStudyUsesRingBandsForTiers(SkillTreeData tree)
    {
        var expected = new Dictionary<string, int>
        {
            ["int_a01"] = 1,
            ["int_a02"] = 1,
            ["int_a03"] = 2,
            ["int_a04"] = 2,
            ["int_a05"] = 3,
            ["int_a06"] = 3,
        };

        foreach (var (nodeId, expectedTier) in expected)
        {
            if (!tree.Nodes.TryGetValue(nodeId, out var node))
                return (false, $"missing {nodeId}");
            if (!SpellStudyCatalog.IsSpellSlotEffect(node.SkillEffect))
                return (false, $"{nodeId} must be a spell study node, actual {node.SkillEffect}");
            int tier = SpellStudyCatalog.GetTierFromSpellSlotEffect(node.SkillEffect);
            if (tier != expectedTier)
                return (false, $"{nodeId} tier mismatch: expected {expectedTier}, actual {tier}");

            var options = SpellStudyCatalog.GetOptions(tier);
            if (options.Length != 5)
                return (false, $"tier {tier} spell study must expose 5 school options, actual {options.Length}");
            foreach (var option in options)
                if ((int)option.Spell.tier != tier)
                    return (false, $"{option.Spell.SpellId} is not a tier {tier} spell");
        }

        if (tree.Nodes.TryGetValue("int_g01", out var giant) && SpellStudyCatalog.IsSpellSlotEffect(giant.SkillEffect))
            return (false, $"int_g01 must not grant spell study after ring-band rule, actual {giant.SkillEffect}");

        return (true, "");
    }

    private static (bool, string) ActiveEffectsAreRegisteredAndScripted(SkillTreeData tree)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
            return (false, "找不到 Blade&Hex 仓库根目录");

        string configPath = Path.Combine(repoRoot, "BladeHexFrontend", "src", "View", "Combat", "skill_configs.json");
        if (!File.Exists(configPath))
            return (false, $"找不到技能配置: {configPath}");

        var registered = new HashSet<string>();
        var configs = new Dictionary<string, JsonElement>();
        using (var doc = JsonDocument.Parse(File.ReadAllText(configPath)))
        {
            foreach (var skill in doc.RootElement.EnumerateArray())
            {
                if (skill.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    string skillId = id.GetString()!;
                    registered.Add(skillId);
                    configs[skillId] = skill.Clone();
                }
            }
        }

        if (!configs.TryGetValue("poison_blade", out var poisonBlade))
            return (false, "poison_blade 未注册到 skill_configs.json");
        if (!poisonBlade.TryGetProperty("target", out var poisonTarget) || poisonTarget.GetString() != "Self")
            return (false, "poison_blade 必须是自身目标，用于标记下次武器攻击");
        if (!poisonBlade.TryGetProperty("action_cost", out var poisonAp) || poisonAp.GetInt32() != 2)
            return (false, "poison_blade 必须消耗 2 AP/次行动");
        if (!poisonBlade.TryGetProperty("cooldown", out var poisonCooldown) || poisonCooldown.GetInt32() != 2)
            return (false, "poison_blade 必须是 CD2");

        foreach (var node in tree.Nodes.Values.Where(n => n.IsActiveSkill && !string.IsNullOrEmpty(n.SkillEffect)))
        {
            string effect = node.SkillEffect;
            if (effect.StartsWith("spell_slot_", System.StringComparison.Ordinal))
                continue;

            if (!registered.Contains(effect))
                return (false, $"{node.NodeId} 主动效果 {effect} 未注册到 skill_configs.json");

            string scriptPath = Path.Combine(repoRoot, "scripts", "skills", effect + ".lua");
            if (!File.Exists(scriptPath))
                return (false, $"{node.NodeId} 主动效果 {effect} 缺少 Lua 脚本 {scriptPath}");

            var (loaded, error) = LuaScriptEngine.Instance.ValidateSkillScript(effect);
            if (!loaded)
                return (false, $"{node.NodeId} 主动效果 {effect} Lua 加载失败: {error}");
        }

        return (true, "");
    }

    private static (bool, string) ActiveScriptsKeyModifiersMatchSkillDescriptions()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
            return (false, "找不到 Blade&Hex 仓库根目录");

        var expectedSnippets = new Dictionary<string, string[]>
        {
            { "berserk_stance.lua", new[] { "damage = 0.15", "damage_taken = 0.10" } },
            { "guard_stance.lua", new[] { "damage_taken = -0.15", "damage = -0.15" } },
            { "hunter_stance.lua", new[] { "ranged_damage = 0.15", "melee_damage = -0.20" } },
            { "guardian_link.lua", new[] { "damage_redirect_percent = 0.5" } },
            { "weakpoint_pierce.lua", new[] { "crit_taken = 0.15" } },
            { "deadly_mark.lua", new[] { "critical_rate_taken = 0.20", "marker_id = ctx.attacker.instance_id" } },
            { "shadow_hide.lua", new[] { "ranged_hit_taken = -0.50", "ignore_zoc = 1", "no_aoo_on_move = 1" } },
            { "warband_inspiration.lua", new[] { "temp_hp_amount" } },
            { "poison_blade.lua", new[] { "next_hit_poison_duration = 3" } },
        };

        foreach (var (file, snippets) in expectedSnippets)
        {
            string path = Path.Combine(repoRoot, "scripts", "skills", file);
            if (!File.Exists(path))
                return (false, $"缺少技能脚本 {path}");

            string source = File.ReadAllText(path);
            foreach (var snippet in snippets)
            {
                if (!source.Contains(snippet, System.StringComparison.Ordinal))
                    return (false, $"{file} 缺少关键语义字段: {snippet}");
            }
        }

        return (true, "");
    }

    private static (bool, string) GiantApexCombatContractsMatchSpec(SkillTreeData tree)
    {
        var expectedCombatGiants = new[]
        {
            ("str_g01", "str_giant_apex"),
            ("dex_g01", "dex_giant_apex"),
            ("con_g01", "con_giant_apex"),
            ("wis_g01", "wis_giant_apex"),
            ("cha_g01", "cha_giant_apex"),
        };

        foreach (var (nodeId, effect) in expectedCombatGiants)
        {
            if (!tree.Nodes.TryGetValue(nodeId, out var node))
                return (false, $"缺少巨型节点 {nodeId}");
            if (node.CurrentNodeType != SkillNodeData.NodeType.Giant)
                return (false, $"{nodeId} 不是 Giant 节点");
            if (!node.IsActiveSkill || node.SkillEffect != effect)
                return (false, $"{nodeId} 主动效果错误: active={node.IsActiveSkill}, effect={node.SkillEffect}");
        }

        if (!tree.Nodes.TryGetValue("int_g01", out var intGiant))
            return (false, "缺少 INT 巨型节点 int_g01");
        if (intGiant.CurrentNodeType != SkillNodeData.NodeType.Giant || !intGiant.IsActiveSkill || intGiant.SkillEffect != "int_giant_apex")
            return (false, $"INT 巨型应为主动 apex, actual active={intGiant.IsActiveSkill}, effect={intGiant.SkillEffect}");

        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
            return (false, "找不到 Blade&Hex 仓库根目录");

        string configPath = Path.Combine(repoRoot, "BladeHexFrontend", "src", "View", "Combat", "skill_configs.json");
        if (!File.Exists(configPath))
            return (false, $"找不到技能配置: {configPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var configs = new Dictionary<string, JsonElement>();
        foreach (var skill in doc.RootElement.EnumerateArray())
        {
            if (skill.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                configs[id.GetString()!] = skill.Clone();
        }

        foreach (var (_, effect) in expectedCombatGiants)
        {
            if (!configs.TryGetValue(effect, out var cfg))
                return (false, $"{effect} 未注册到 skill_configs.json");
            if (!cfg.TryGetProperty("action_cost", out var ap) || ap.GetInt32() != 0)
                return (false, $"{effect} 必须是 free action(action_cost=0)");
            if (!cfg.TryGetProperty("uses_per_battle", out var uses) || uses.GetInt32() != 1)
                return (false, $"{effect} 必须每场战斗 1 次(uses_per_battle=1)");
            if (cfg.TryGetProperty("cooldown", out _))
                return (false, $"{effect} 不应再使用 cooldown 表达巨型限制");
            if (cfg.TryGetProperty("is_spell", out var isSpell) && isSpell.GetBoolean())
                return (false, $"{effect} 应为非 spell 战斗窗口");
        }

        if (configs.TryGetValue("cha_giant_apex", out var chaCfg)
            && (!chaCfg.TryGetProperty("target", out var target) || target.GetString() != "AllAllies"))
            return (false, "cha_giant_apex 必须作用于 AllAllies");

        return (true, "");
    }

    private static (bool, string) KeystoneEffectsRuntimeAliasesResolve(SkillTreeData tree)
    {
        var data = new Godot.Collections.Dictionary
        {
            {
                "activated_nodes",
                new Godot.Collections.Array<string>(new[]
                {
                    SkillTreeData.StartNodeId,
                    "dex_ks01",
                    "con_ks01",
                    "con_ks03",
                })
            },
            { "available_skill_points", 0 },
            { "character_level", 99 },
        };

        var characterTree = new CharacterSkillTree();
        characterTree.Deserialize(data, tree);

        var expectedAliases = new[]
        {
            ("ghost_footwork", "ghost_step"),
            ("undying_body", "immortal_body"),
            ("iron_body", "diamond_body"),
        };

        foreach (var (designName, legacyName) in expectedAliases)
        {
            if (!characterTree.HasSkillEffect(designName))
                return (false, $"missing design keystone effect {designName}");
            if (!characterTree.HasSkillEffect(legacyName))
                return (false, $"{designName} must also resolve legacy runtime hook {legacyName}");
        }

        return (true, "");
    }

    private static (bool, string) GiantApexRuntimeRequiresWindowBuff(SkillTreeData tree)
    {
        var litGiants = MakeUnit(MakeTreeWithNodes(tree, "start", "str_g01", "wis_g01", "cha_g01"));

        var input = new CombatRuleEngine.AttackInput
        {
            AttackBonus = -100,
            TargetAc = 99,
            CritThreshold = 20,
        };
        SkillTreeKeystoneResolver.ApplyAttackRollRules(litGiants, ref input, isMelee: true);
        if (input.ForceHit || input.ForceCritical)
            return (false, "点亮 WIS 巨型不应常驻必中/必暴，必须由启动后的窗口 buff 提供");

        if (SkillTreeKeystoneResolver.IsImmuneToNegative(litGiants) || SkillTreeKeystoneResolver.IsImmuneToMind(litGiants))
            return (false, "点亮 CHA 巨型不应常驻免疫负面/心灵，必须由启动后的窗口 buff 提供");

        float noWindowDamage = SkillTreeKeystoneResolver.GetDamageFinalMultiplier(
            litGiants, isMelee: true, isRanged: false, distance: 1, isCritical: false);
        if (System.Math.Abs(noWindowDamage - 1.0f) > 0.0001f)
            return (false, $"点亮 STR 巨型不应常驻增伤，actual={noWindowDamage}");

        litGiants.Runtime.ActiveBuffs.Add(new BuffInstance
        {
            Id = "wis_giant_apex",
            Duration = 1,
            Modifiers =
            [
                new StatModifier { Stat = "force_attack_hit", Value = 1 },
                new StatModifier { Stat = "force_attack_crit", Value = 1 },
            ],
        });

        input = new CombatRuleEngine.AttackInput
        {
            AttackBonus = -100,
            TargetAc = 99,
            CritThreshold = 20,
        };
        SkillTreeKeystoneResolver.ApplyAttackRollRules(litGiants, ref input, isMelee: true);
        if (!input.ForceHit || !input.ForceCritical)
            return (false, "WIS 巨型窗口 buff 必须提供必中/必暴");

        litGiants.Runtime.ActiveBuffs.Add(new BuffInstance
        {
            Id = "cha_giant_apex",
            Duration = 1,
            Modifiers =
            [
                new StatModifier { Stat = "immune_negative", Value = 1 },
                new StatModifier { Stat = "immune_fear", Value = 1 },
                new StatModifier { Stat = "immune_mind", Value = 1 },
            ],
        });

        if (!SkillTreeKeystoneResolver.IsImmuneToNegative(litGiants)
            || !SkillTreeKeystoneResolver.IsImmuneToFear(litGiants)
            || !SkillTreeKeystoneResolver.IsImmuneToMind(litGiants))
            return (false, "CHA 巨型窗口 buff 必须提供负面/恐惧/心灵免疫");

        return (true, "");
    }

    private static (bool, string) BattleUnitModelRuntimeMirrorsUnitData(SkillTreeData tree)
    {
        var characterTree = MakeTreeWithNodes(tree, "start", "wis_g01");
        var unit = MakeUnit(characterTree);
        var model = new BattleUnitModel(unit);

        if (!object.ReferenceEquals(model.Runtime, unit.Runtime))
            return (false, "BattleUnitModel 必须共享 UnitData.Runtime，否则 UI 战斗读不到技能树/buff runtime");
        if (!object.ReferenceEquals(model.SkillTree, characterTree))
            return (false, "BattleUnitModel.SkillTree 必须读取 UnitData.Runtime.SkillTree");

        model.CurrentMana = 7;
        if (unit.CurrentMana != 7 || unit.Runtime.CurrentMana != 7)
            return (false, $"CurrentMana 写入必须同步 UnitData 与 Runtime, data={unit.CurrentMana}, runtime={unit.Runtime.CurrentMana}");

        return (true, "");
    }

    private static (bool, string) KeystoneEffectsRuntimeContractsResolve(SkillTreeData tree)
    {
        var expectedEffects = new HashSet<string>
        {
            "berserk_power",
            "resolute_technique",
            "blood_oath",
            "ghost_footwork",
            "acrobatics",
            "point_blank",
            "undying_body",
            "shield_bastion",
            "iron_body",
            "absolute_focus",
            "blood_magic",
            "chaos_inoculation",
            "assassin_instinct",
            "elemental_overload",
            "pain_attunement",
            "royal_presence",
            "agnostic_command",
            "martyr_oath",
        };

        foreach (var effect in expectedEffects)
        {
            if (!SkillTreeKeystoneResolver.KeystoneEffects.Contains(effect))
                return (false, $"resolver contract missing {effect}");
        }

        var actualKeystones = tree.Nodes.Values
            .Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Keystone)
            .Select(n => n.SkillEffect)
            .ToHashSet();
        foreach (var effect in expectedEffects)
        {
            if (!actualKeystones.Contains(effect))
                return (false, $"tree missing keystone effect {effect}");
        }

        var allTree = MakeTreeWithNodes(tree, tree.Nodes.Values
            .Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Keystone)
            .Select(n => n.NodeId)
            .ToArray());
        var allUnit = MakeUnit(allTree);

        if (CombatStats.GetMaxMana(allUnit) != 0)
            return (false, "blood_magic must force max mana to 0");
        if (SkillTreeKeystoneResolver.CanEquipShield(allUnit))
            return (false, "berserk_power/assassin_instinct must forbid shields");
        if (SkillTreeKeystoneResolver.CanEquipTwoHandedWeapon(allUnit))
            return (false, "shield_bastion must forbid two-handed weapons");
        if (SkillTreeKeystoneResolver.CanEquipMediumOrHeavyArmor(allUnit))
            return (false, "acrobatics/assassin_instinct must forbid medium/heavy armor");
        if (SkillTreeKeystoneResolver.CanDodge(allUnit) || SkillTreeKeystoneResolver.CanRetreat(allUnit))
            return (false, "iron_body must forbid dodge and active retreat");
        if (!SkillTreeKeystoneResolver.RequiresOneSchoolSpellStudy(allUnit))
            return (false, "absolute_focus must lock spell study to one school");
        if (SkillTreeKeystoneResolver.CanReceivePositiveBuff(allUnit))
            return (false, "agnostic_command must reject positive buffs");
        if (!SkillTreeKeystoneResolver.IsImmuneToNegative(allUnit))
            return (false, "chaos_inoculation must reject negative buffs");
        if (!SkillTreeKeystoneResolver.IsImmuneToFear(allUnit))
            return (false, "iron_body/royal_presence must reject fear");
        if (!SkillTreeKeystoneResolver.IsImmuneToMind(allUnit))
            return (false, "agnostic_command must reject mind effects");
        if (!SkillTreeKeystoneResolver.HasMartyrOath(allUnit))
            return (false, "martyr_oath must expose a runtime hook");

        var noKs = MakeUnit(MakeTreeWithNodes(tree));
        var hpKs = MakeUnit(MakeTreeWithNodes(tree, "start", "dex_ks01", "int_ks03", "wis_ks03", "cha_ks01"));
        int baseHp = CombatStats.GetMaxHp(noKs);
        int reducedHp = CombatStats.GetMaxHp(hpKs);
        if (reducedHp >= baseHp)
            return (false, $"HP keystone drawbacks must reduce max hp, base={baseHp}, reduced={reducedHp}");

        var acTree = MakeTreeWithNodes(tree, "start", "str_ks01", "dex_ks02", "wis_ks01", "cha_ks03");
        var acUnit = MakeUnit(acTree);
        if (CombatStats.GetAc(acUnit, true) >= CombatStats.GetAc(noKs, true))
            return (false, "AC keystone drawbacks must reduce AC");

        var resolute = MakeUnit(MakeTreeWithNodes(tree, "start", "str_ks02"));
        var input = new CombatRuleEngine.AttackInput
        {
            AttackBonus = -100,
            TargetAc = 99,
            CritThreshold = 20,
        };
        SkillTreeKeystoneResolver.ApplyAttackRollRules(resolute, ref input, isMelee: true);
        if (!input.ForceHit || !input.SuppressCritical)
            return (false, "resolute_technique must force melee hit and suppress crit");

        var bloodMage = MakeUnit(MakeTreeWithNodes(tree, "start", "int_ks02"));
        if (SkillTreeKeystoneResolver.ApplySpellManaCost(bloodMage, 12) != 0 || SkillTreeKeystoneResolver.GetSpellHpCost(bloodMage, 12) != 12)
            return (false, "blood_magic must convert mana cost to HP cost");

        var focused = MakeUnit(MakeTreeWithNodes(tree, "start", "int_ks01"));
        focused.KnownSpells.Add(SpellStudyCatalog.GetOptions(1).First(o => o.SchoolKey == "destruction").Spell);
        var illusion = SpellStudyCatalog.GetOptions(2).First(o => o.SchoolKey == "illusion").Spell;
        if (SkillTreeKeystoneResolver.CanStudySpell(focused, illusion))
            return (false, "absolute_focus must reject a second spell school");

        return (true, "");
    }

    private static (bool, string) KeystoneFlowsEquipmentAndRandomSpellStudyRespectLocks(SkillTreeData tree)
    {
        var noKs = MakeUnit(MakeTreeWithNodes(tree));
        var shield = new ArmorData { armorType = ArmorData.ArmorType.Shield };
        var mediumArmor = new ArmorData { armorType = ArmorData.ArmorType.Medium };
        var heavyArmor = new ArmorData { armorType = ArmorData.ArmorType.Heavy };
        var twoHanded = new WeaponData { IsTwoHanded = true };

        if (!noKs.CanEquipItemBySkillTree(shield, "shield"))
            return (false, "baseline unit should be allowed to equip shields");
        if (!noKs.CanEquipItemBySkillTree(mediumArmor, "armor") || !noKs.CanEquipItemBySkillTree(heavyArmor, "armor"))
            return (false, "baseline unit should be allowed to equip medium/heavy armor");
        if (!noKs.CanEquipItemBySkillTree(twoHanded, "primary_main"))
            return (false, "baseline unit should be allowed to equip two-handed weapons");

        var berserk = MakeUnit(MakeTreeWithNodes(tree, "start", FindKeystoneNodeId(tree, "berserk_power")));
        if (berserk.CanEquipItemBySkillTree(shield, "shield"))
            return (false, "berserk_power must block UnitData shield equip flow");

        var acrobat = MakeUnit(MakeTreeWithNodes(tree, "start", FindKeystoneNodeId(tree, "acrobatics")));
        if (acrobat.CanEquipItemBySkillTree(mediumArmor, "armor") || acrobat.CanEquipItemBySkillTree(heavyArmor, "armor"))
            return (false, "acrobatics must block UnitData medium/heavy armor equip flow");

        var bastion = MakeUnit(MakeTreeWithNodes(tree, "start", FindKeystoneNodeId(tree, "shield_bastion")));
        if (bastion.CanEquipItemBySkillTree(twoHanded, "primary_main"))
            return (false, "shield_bastion must block UnitData two-handed weapon equip flow");

        var focused = MakeUnit(MakeTreeWithNodes(tree, "start", FindKeystoneNodeId(tree, "absolute_focus")));
        focused.KnownSpells.Add(SpellStudyCatalog.GetOptions(1).First(o => o.SchoolKey == "destruction").Spell);
        for (int i = 0; i < 25; i++)
        {
            var spell = SpellStudyCatalog.CreateRandomSpellForTier(focused, 2);
            if (spell == null)
                return (false, "absolute_focus random spell study returned no legal same-school spell");
            if (spell.spellSchool != focused.KnownSpells[0].spellSchool)
                return (false, $"absolute_focus random spell study picked another school: {spell.SpellId}");
        }

        return (true, "");
    }

    private static string FindKeystoneNodeId(SkillTreeData tree, string effect)
    {
        var node = tree.Nodes.Values.FirstOrDefault(n =>
            n.CurrentNodeType == SkillNodeData.NodeType.Keystone && n.SkillEffect == effect);
        if (node == null)
            throw new System.InvalidOperationException($"missing keystone effect {effect}");
        return node.NodeId;
    }

    private static CharacterSkillTree MakeTreeWithNodes(SkillTreeData tree, params string[] nodeIds)
    {
        var data = new Godot.Collections.Dictionary
        {
            { "activated_nodes", new Godot.Collections.Array<string>(nodeIds.Length == 0 ? new[] { SkillTreeData.StartNodeId } : nodeIds) },
            { "available_skill_points", 0 },
            { "character_level", 99 },
        };

        var characterTree = new CharacterSkillTree();
        characterTree.Deserialize(data, tree);
        return characterTree;
    }

    private static UnitData MakeUnit(CharacterSkillTree tree)
    {
        var unit = new UnitData
        {
            UnitName = "keystone_test",
            Level = 20,
            BaseMaxHp = 100,
            BaseAc = 10,
            BaseAp = 12,
            BaseMoveRange = 4,
            Str = 14,
            Dex = 14,
            Con = 14,
            Intel = 14,
            Wis = 14,
            Cha = 14,
        };
        unit.Runtime.SkillTree = tree;
        unit.Runtime.CurrentHp = 100;
        unit.CurrentMana = CombatStats.GetMaxMana(unit);
        return unit;
    }

    private static Dictionary<SkillNodeData.Region, Dictionary<string, (double min, double max)>> LoadRandomPools()
    {
        var result = new Dictionary<SkillNodeData.Region, Dictionary<string, (double min, double max)>>();
        var repoRoot = FindRepoRoot();
        if (repoRoot == null)
            return result;

        string contentPath = Path.Combine(repoRoot, "BladeHexCore", "src", "SkillTree", "skill_tree_content.json");
        if (!File.Exists(contentPath))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(contentPath));
        if (!doc.RootElement.TryGetProperty("randomPools", out var poolsElement))
            return result;

        foreach (var poolProp in poolsElement.EnumerateObject())
        {
            var region = poolProp.Name.ToLowerInvariant() switch
            {
                "str" => SkillNodeData.Region.Str,
                "dex" => SkillNodeData.Region.Dex,
                "con" => SkillNodeData.Region.Con,
                "int" => SkillNodeData.Region.Int,
                "wis" => SkillNodeData.Region.Wis,
                "cha" => SkillNodeData.Region.Cha,
                _ => SkillNodeData.Region.None,
            };
            if (region == SkillNodeData.Region.None)
                continue;

            var entries = new Dictionary<string, (double min, double max)>();
            foreach (var entry in poolProp.Value.EnumerateArray())
            {
                string stat = entry.GetProperty("stat").GetString() ?? "";
                double min = entry.GetProperty("min").GetDouble();
                double max = entry.GetProperty("max").GetDouble();
                entries[stat] = (min, max);
            }
            result[region] = entries;
        }

        return result;
    }

    private static bool VariantNumberEquals(Variant variant, double expected)
        => System.Math.Abs(VariantToDouble(variant) - expected) < 0.0001d;

    private static double VariantToDouble(Variant variant)
        => variant.VariantType == Variant.Type.Int ? variant.AsInt32() : variant.AsSingle();

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "project.godot")) &&
                Directory.Exists(Path.Combine(dir.FullName, "BladeHexCore")) &&
                Directory.Exists(Path.Combine(dir.FullName, "BladeHexFrontend")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
