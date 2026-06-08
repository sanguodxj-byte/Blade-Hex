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
    // 与 AutoLayoutAndConnect 的 HexagonRadius 保持一致
    private const int HexagonRadius = 20;

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
        yield return Run("RegionCentroids_MatchDocumentSectors", () => RegionCentroidsMatchDocumentSectors(tree, nodeTiles));
        yield return Run("RegionQuota_MatchesStarChartSpec", () => RegionQuotaMatchesStarChartSpec(tree));
        yield return Run("TileCost_MatchesExplicitTileCount", () => TileCostMatchesExplicitTileCount(tree));
        yield return Run("FigureTemplates_MatchNodeRoles", () => FigureTemplatesMatchNodeRoles(tree));
        yield return Run("LargeFigures_AreCompactNotStrips", () => LargeFiguresAreCompactNotStrips(tree));
        yield return Run("PassiveNodeBonuses_DoNotFlattenConditionalEffects", () => PassiveNodeBonusesDoNotFlattenConditionalEffects(tree));
        yield return Run("RandomAttributes_UseLegalKeysOnly", () => RandomAttributesUseLegalKeysOnly(tree));
        yield return Run("RandomAttributes_FollowSmallAndPipRules", () => RandomAttributesFollowSmallAndPipRules(tree));
        yield return Run("RandomAttributes_AreCharacterSeeded", () => RandomAttributesAreCharacterSeeded(tree));
        yield return Run("RandomAttributes_EffectTextUsesCharacterSeed", () => RandomAttributesEffectTextUsesCharacterSeed(tree));
        yield return Run("FixedLargeNodes_DoNotGrantTileAttributes", () => FixedLargeNodesDoNotGrantTileAttributes(tree));
        yield return Run("IntGiant_OpensTierFourSpellStudy", () => IntGiantOpensTierFourSpellStudy(tree));
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

    private static (bool, string) RegionCentroidsMatchDocumentSectors(SkillTreeData tree, Dictionary<string, Vector2I[]> nodeTiles)
    {
        var coord = new SkillTreeCoord { HexSize = 1.0f };
        var expected = new Dictionary<SkillNodeData.Region, System.Func<Vector2, bool>>
        {
            { SkillNodeData.Region.Int, p => p.Y < -5.0f && System.Math.Abs(p.X) < 5.0f },
            { SkillNodeData.Region.Con, p => p.X > 5.0f && p.Y < 0.0f },
            { SkillNodeData.Region.Str, p => p.X > 5.0f && p.Y > 0.0f },
            { SkillNodeData.Region.Dex, p => p.Y > 5.0f && System.Math.Abs(p.X) < 5.0f },
            { SkillNodeData.Region.Cha, p => p.X < -5.0f && p.Y > 0.0f },
            { SkillNodeData.Region.Wis, p => p.X < -5.0f && p.Y < 0.0f },
        };

        foreach (var (region, predicate) in expected)
        {
            var tiles = tree.Nodes.Values
                .Where(n => n.CurrentRegion == region)
                .SelectMany(n => nodeTiles[n.NodeId])
                .ToList();
            if (tiles.Count == 0)
                return (false, $"{region} 没有布局瓦片");

            var center = Vector2.Zero;
            foreach (var tile in tiles)
                center += coord.TileCentroid(tile);
            center /= tiles.Count;

            if (!predicate(center))
                return (false, $"{region} 扇区质心不符合文档方位: {center}");
        }

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

            if (giants != 1 || keystones != 3 || actives != 6 || passives != 8 || small < 151 || small > 155 || pips < 3 || pips > 9)
            {
                return (false,
                    $"{region} 配额错误: giant={giants}, ks={keystones}, active={actives}, passive={passives}, small={small}, pip={pips}");
            }
        }

        var bridges = tree.Nodes.Values.Where(n => n.CurrentRegion == SkillNodeData.Region.Transition && n.IsBridge).ToList();
        if (bridges.Count != 6)
            return (false, $"过渡桥节点数量错误: 期望 6, 实际 {bridges.Count}");

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
            if (node.CurrentNodeType == SkillNodeData.NodeType.Big && node.ExplicitTiles.Length != 4)
                return (false, $"{id} 大节点不是 4 瓦片");
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
                SkillNodeData.NodeType.Big => "passive_triangle_4",
                SkillNodeData.NodeType.Keystone => "keystone_crown_6",
                SkillNodeData.NodeType.Giant => "apex_rune_12",
                _ => "",
            };

            string actual = node.GetFigureTemplate();
            if (actual != expected)
                return (false, $"{id} 模板错误: expected={expected}, actual={actual}");
        }

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
            float limit = node.CurrentNodeType == SkillNodeData.NodeType.Big ? 2.4f : 2.7f;

            if (ratio > limit)
                return (false, $"{id} 形状过长: ratio={ratio:0.00}, limit={limit:0.00}, width={width:0.00}, height={height:0.00}");
        }

        return (true, "");
    }

    private static (bool, string) RandomAttributesUseLegalKeysOnly(SkillTreeData tree)
    {
        var legal = new HashSet<string>
        {
            "max_hp", "ac", "melee_hit", "melee_damage", "ranged_hit", "ranged_damage",
            "critical_rate", "speed", "mana_max", "mana_regen", "initiative", "all_save",
            "range_bonus", "spell_hit", "spell_damage", "heal_amount", "ally_bonus",
        };

        var characterTree = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 101);
        foreach (var (id, node) in tree.Nodes)
        {
            if (node.CurrentContentMode != SkillNodeData.ContentMode.RandomAttribute)
                continue;

            if (node.StatBonuses.Count != 0)
                return (false, $"{id} 随机属性节点不应在共享节点数据上预掷属性");

            var bonuses = characterTree.GetNodeStatBonusesForCharacter(node);
            if (bonuses.Count == 0)
                return (false, $"{id} 未按角色 seed 生成随机属性");

            foreach (var key in bonuses.Keys)
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
            ["int_p06"] = ["spell_hit"],
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

    private static (bool, string) RandomAttributesFollowSmallAndPipRules(SkillTreeData tree)
    {
        var pools = LoadRandomPools();
        if (pools.Count == 0)
            return (false, "randomPools not loaded");

        var characterTree = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 101);
        foreach (var (id, node) in tree.Nodes)
        {
            if (node.CurrentContentMode != SkillNodeData.ContentMode.RandomAttribute)
                continue;

            var bonuses = characterTree.GetNodeStatBonusesForCharacter(node);

            if (node.CurrentRegion == SkillNodeData.Region.Transition)
            {
                if (bonuses.Count != 0)
                {
                    return (false, $"{id} transition random node should not roll cross-region attributes");
                }
                continue;
            }

            if (!pools.TryGetValue(node.CurrentRegion, out var pool))
                return (false, $"{id} has no random pool for region {node.CurrentRegion}");

            if (node.CurrentNodeType == SkillNodeData.NodeType.Pip)
            {
                if (bonuses.Count != 1)
                    return (false, $"{id} pip must have exactly one stat, actual {bonuses.Count}");

                foreach (var key in bonuses.Keys)
                {
                    string stat = key.ToString()!;
                    if (!pool.TryGetValue(stat, out var range))
                        return (false, $"{id} pip stat {stat} is not in its region pool");
                    if (!VariantNumberEquals(bonuses[key], range.min))
                        return (false, $"{id} pip stat {stat} must use pool minimum {range.min}");
                }
            }
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Small)
            {
                if (bonuses.Count < 1 || bonuses.Count > 2)
                    return (false, $"{id} small node must have one stat plus optional secondary, actual {bonuses.Count}");

                foreach (var key in bonuses.Keys)
                {
                    string stat = key.ToString()!;
                    if (!pool.TryGetValue(stat, out var range))
                        return (false, $"{id} small stat {stat} is not in its region pool");

                    double value = VariantToDouble(bonuses[key]);
                    if (value < range.min - 0.0001d || value > range.max + 0.0001d)
                        return (false, $"{id} small stat {stat} value {value} outside [{range.min}, {range.max}]");
                }
            }
        }

        return (true, "");
    }

    private static (bool, string) RandomAttributesAreCharacterSeeded(SkillTreeData tree)
    {
        var node = tree.Nodes.Values.FirstOrDefault(n =>
            n.CurrentContentMode == SkillNodeData.ContentMode.RandomAttribute
            && n.CurrentNodeType == SkillNodeData.NodeType.Small
            && n.CurrentRegion != SkillNodeData.Region.Transition);
        if (node == null)
            return (false, "no random small node found");

        var seedA1 = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 101);
        var seedA2 = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 101);
        var a1 = seedA1.GetNodeStatBonusesForCharacter(node);
        var a2 = seedA2.GetNodeStatBonusesForCharacter(node);
        if (!DictionariesEqual(a1, a2))
            return (false, "same character seed must reproduce identical random attributes");

        bool foundDifferent = false;
        foreach (var candidate in tree.Nodes.Values.Where(n =>
            n.CurrentContentMode == SkillNodeData.ContentMode.RandomAttribute
            && n.CurrentNodeType == SkillNodeData.NodeType.Small
            && n.CurrentRegion != SkillNodeData.Region.Transition).Take(80))
        {
            var b1 = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 101).GetNodeStatBonusesForCharacter(candidate);
            var b2 = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 202).GetNodeStatBonusesForCharacter(candidate);
            if (!DictionariesEqual(b1, b2))
            {
                foundDifferent = true;
                break;
            }
        }
        if (!foundDifferent)
            return (false, "different character seeds must be able to produce different random attributes");

        var seeded = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 202);
        var expected = seeded.GetNodeStatBonusesForCharacter(node);
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

    private static (bool, string) RandomAttributesEffectTextUsesCharacterSeed(SkillTreeData tree)
    {
        bool foundDifferentText = false;
        foreach (var node in tree.Nodes.Values.Where(n =>
            n.CurrentContentMode == SkillNodeData.ContentMode.RandomAttribute
            && n.CurrentNodeType == SkillNodeData.NodeType.Small
            && n.CurrentRegion != SkillNodeData.Region.Transition).Take(120))
        {
            var seedA = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 101);
            var seedB = new CharacterSkillTree(tree, level: 1, randomAttributeSeed: 202);
            var bonusesA = seedA.GetNodeStatBonusesForCharacter(node);
            var bonusesB = seedB.GetNodeStatBonusesForCharacter(node);
            string textA = seedA.GetNodeEffectTextForCharacter(node);
            string textB = seedB.GetNodeEffectTextForCharacter(node);
            string expectedA = node.GetEffectText(bonusesA);
            string expectedB = node.GetEffectText(bonusesB);

            if (textA != expectedA)
                return (false, $"{node.NodeId} effect text for seed 101 did not use character bonuses");
            if (textB != expectedB)
                return (false, $"{node.NodeId} effect text for seed 202 did not use character bonuses");

            if (textA != textB)
                foundDifferentText = true;
        }

        return foundDifferentText
            ? (true, "")
            : (false, "no sampled random node produced visibly different character-seeded effect text");
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

    private static (bool, string) FixedLargeNodesDoNotGrantTileAttributes(SkillTreeData tree)
    {
        var node = tree.Nodes.Values.FirstOrDefault(n =>
            n.CurrentNodeType == SkillNodeData.NodeType.Big &&
            n.IsActiveSkill &&
            n.CurrentRegion == SkillNodeData.Region.Str);
        if (node == null)
            return (false, "找不到 STR 主动大件节点");

        var save = new Godot.Collections.Dictionary
        {
            { "activated_nodes", new Godot.Collections.Array { SkillTreeData.StartNodeId, node.NodeId } },
            { "available_attribute_points", 0 },
            { "random_attribute_seed", 101 },
        };

        var characterTree = new CharacterSkillTree();
        characterTree.Deserialize(save, tree);
        var attributes = characterTree.GetAllAccumulatedAttributes();
        if (attributes.Count != 0)
            return (false, $"{node.NodeId} 是固定大件，不应逐片提供六维属性: {attributes}");

        return (true, "");
    }

    private static (bool, string) IntGiantOpensTierFourSpellStudy(SkillTreeData tree)
    {
        if (!tree.Nodes.TryGetValue("int_g01", out var node))
            return (false, "missing int_g01");

        if (node.CurrentNodeType != SkillNodeData.NodeType.Giant)
            return (false, "int_g01 must be a giant node");
        if (node.IsActiveSkill)
            return (false, "int_g01 must not be a combat active skill");
        if (!SpellStudyCatalog.IsSpellSlotEffect(node.SkillEffect))
            return (false, $"int_g01 effect must be a spell slot, actual {node.SkillEffect}");

        int tier = SpellStudyCatalog.GetTierFromSpellSlotEffect(node.SkillEffect);
        if (tier != 4)
            return (false, $"int_g01 must map to tier 4, actual {tier}");

        var options = SpellStudyCatalog.GetOptions(tier);
        if (options.Length != 5)
            return (false, $"tier 4 spell study must expose 5 school options, actual {options.Length}");

        foreach (var option in options)
        {
            if ((int)option.Spell.tier != 4)
                return (false, $"{option.Spell.SpellId} is not a tier 4 spell");
        }

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
        using (var doc = JsonDocument.Parse(File.ReadAllText(configPath)))
        {
            foreach (var skill in doc.RootElement.EnumerateArray())
            {
                if (skill.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    registered.Add(id.GetString()!);
            }
        }

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
        if (intGiant.CurrentNodeType != SkillNodeData.NodeType.Giant || intGiant.IsActiveSkill || intGiant.SkillEffect != "spell_slot_4")
            return (false, $"INT 巨型应为非战斗 spell_slot_4, actual active={intGiant.IsActiveSkill}, effect={intGiant.SkillEffect}");

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
