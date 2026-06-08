// SkillTreeData.cs
// 技能盘完整图数据 — 150+ 节点，axial 坐标 (q, r)
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>
/// 技能盘图数据 — 存储完整的技能盘拓扑结构
/// </summary>
[GlobalClass]
public partial class SkillTreeData : RefCounted
{
    public Dictionary<string, SkillNodeData> Nodes { get; } = new();
    public const string StartNodeId = "start";
    public int GetNodeCount() => Nodes.Count;

    internal static readonly Vector2I[] Dirs = {
        new(1, 0), new(0, 1), new(-1, 1), new(-1, 0), new(0, -1), new(1, -1),
    };

    /// <summary>
    /// 节点位置映射：
    /// dirIdx 0-5 对应 6 个区域方向（STR/DEX/CON/INT/WIS/CHA）
    /// ring: 距 start 的距离（沿 dirIdx 方向移动 ring 格）
    /// slot: 横向偏移（沿 dirIdx+1 方向）
    /// </summary>
    static Vector2I Mp(int di, int ring, int slot) =>
        Dirs[di % 6] * ring + Dirs[(di + 1) % 6] * slot;

    public SkillTreeData() { BuildSkillTree(); }

    void BuildSkillTree()
    {
        if (SkillTreeLayoutLoader.TryLoadDefault(Nodes, out var loadError))
        {
            var (ok, validationMessage) = SkillTreeLayoutLoader.Validate(Nodes, 20);
            if (ok)
            {
                GD.Print($"[SkillTree] Loaded fixed layout from JSON: nodes={Nodes.Count}");
                return;
            }

            GD.PushError($"[SkillTree] Fixed layout validation failed: {validationMessage}.");
            Nodes.Clear();
        }
        else
        {
            GD.PushError($"[SkillTree] Fixed layout load failed: {loadError}.");
        }

        BuildStartNode();
        GD.PushError("[SkillTree] Fixed JSON layout is required; legacy auto layout is disabled.");
    }

    /// <summary>
    /// 自动布局：清除所有手动 GridPosition，
    /// 按区域+深度重新分配位置到对应 60° 扇区。
    /// 可点亮依据直接使用几何共享边（NodeFiller 几何邻接判断），不建立 Neighbors 列表。
    /// </summary>
    void AutoLayoutAndConnect()
    {
        const int HexagonRadius = 20;
        var coord = new SkillTreeCoord { HexSize = 1.0f };
        var allTiles = SkillTreeCoord.GetAllTiles(HexagonRadius)
            .OrderBy(t => TileDistanceKey(coord, t))
            .ThenBy(t => t.X)
            .ThenBy(t => t.Y)
            .ToList();

        // 1. start 固定原点
        if (Nodes.TryGetValue(StartNodeId, out var startNode))
            startNode.GridPosition = Vector2I.Zero;

        // 2. 6 个区域按 sectorIdx 0-5 分布到六个 60° 三角扇区。
        // sector 0..5 对应顶点边: top->upper-right, upper-right->lower-right, ...
        // 与 SkillTreeUI 的六维区域底色保持一致，避免节点压在分割轴线上。
        var regionToSector = new Dictionary<SkillNodeData.Region, int>
        {
            { SkillNodeData.Region.Dex, 0 },
            { SkillNodeData.Region.Con, 1 },
            { SkillNodeData.Region.Int, 2 },
            { SkillNodeData.Region.Wis, 3 },
            { SkillNodeData.Region.Cha, 4 },
            { SkillNodeData.Region.Str, 5 },
        };

        // Occupancy is tracked by full triangular figures, not node anchors.
        var occupiedTiles = new HashSet<Vector2I>();
        if (Nodes.TryGetValue(StartNodeId, out var start))
            SkillNodeShape.AddTo(SkillNodeShape.GetTiles(start), occupiedTiles);
        var startTiles = new HashSet<Vector2I>(occupiedTiles);

        // 各区域填充其 60° 扇区
        // This keeps visual figures from overlapping and makes runtime
        // availability match what the player sees on the board.
        foreach (var (region, sectorIdx) in regionToSector)
        {
            var regionNodes = Nodes.Values
                .Where(n => n.CurrentRegion == region)
                .OrderBy(n => n.Depth)
                .ThenBy(n => n.NodeId)
                .ToList();

            if (regionNodes.Count == 0) continue;

            var sectorCandidates = allTiles
                .Where(t => GetTileRegionIndex(coord, t) == sectorIdx)
                .ToList();

            for (int i = 0; i < regionNodes.Count; i++)
            {
                var node = regionNodes[i];
                Vector2I targetPos;
                Vector2I[] shapeTiles;

                bool placed = false;
                if (i == 0)
                {
                    placed = TryFindPlacement(node, sectorCandidates, occupiedTiles, HexagonRadius, requireTouch: true,
                            out targetPos, out shapeTiles, startTiles)
                        || TryFindPlacement(node, allTiles, occupiedTiles, HexagonRadius, requireTouch: true,
                            out targetPos, out shapeTiles, startTiles);
                }
                else
                {
                    placed = TryFindPlacement(node, sectorCandidates, occupiedTiles, HexagonRadius, requireTouch: true,
                        out targetPos, out shapeTiles);
                }

                if (!placed
                    && !TryFindPlacement(node, sectorCandidates, occupiedTiles, HexagonRadius, requireTouch: false,
                        out targetPos, out shapeTiles)
                    && !TryFindPlacement(node, allTiles, occupiedTiles, HexagonRadius, requireTouch: false,
                        out targetPos, out shapeTiles))
                    targetPos = Vector2I.Zero;

                node.GridPosition = targetPos;
                SkillNodeShape.AddTo(shapeTiles, occupiedTiles);
            }
        }

        // 3. Transition / 其他特殊节点放置在已占用区域的边界（保证邻接连通）
        var transitionNodes = Nodes.Values
            .Where(n => n.CurrentRegion == SkillNodeData.Region.Transition || n.CurrentRegion == SkillNodeData.Region.None)
            .Where(n => n.NodeId != StartNodeId)
            .OrderBy(n => n.Depth)
            .ThenBy(n => n.NodeId)
            .ToList();

        foreach (var n in transitionNodes)
        {
            if (!TryFindPlacement(n, allTiles, occupiedTiles, HexagonRadius, requireTouch: true,
                    out var targetPos, out var shapeTiles)
                && !TryFindPlacement(n, allTiles, occupiedTiles, HexagonRadius, requireTouch: false,
                    out targetPos, out shapeTiles))
                targetPos = Vector2I.Zero;

            n.GridPosition = targetPos;
            SkillNodeShape.AddTo(shapeTiles, occupiedTiles);
        }

        GD.Print($"[SkillTree] AutoLayout: 节点={Nodes.Count}, 占用瓦片={occupiedTiles.Count}");
    }

    /// <summary>判断位置是否在六边形内</summary>
    static bool IsValidPosition(Vector2I pos, int radius)
    {
        int z = -pos.X - pos.Y;
        return Math.Abs(pos.X) <= radius && Math.Abs(pos.Y) <= radius && Math.Abs(z) <= radius;
    }

    static bool TryFindPlacement(SkillNodeData node, IEnumerable<Vector2I> candidates,
        HashSet<Vector2I> occupiedTiles, int radius, bool requireTouch,
        out Vector2I anchor, out Vector2I[] shapeTiles,
        HashSet<Vector2I>? requiredTouchTiles = null)
    {
        foreach (var candidate in candidates)
        {
            var tiles = SkillNodeShape.GetTilesForAnchor(node, candidate);
            if (!SkillNodeShape.IsInsideHex(tiles, radius)) continue;
            if (SkillNodeShape.Overlaps(tiles, occupiedTiles)) continue;
            if (requireTouch && !SkillNodeShape.Touches(tiles, requiredTouchTiles ?? occupiedTiles)) continue;

            anchor = candidate;
            shapeTiles = tiles;
            return true;
        }

        anchor = Vector2I.Zero;
        shapeTiles = [];
        return false;
    }

    static int GetTileRegionIndex(SkillTreeCoord coord, Vector2I tile)
    {
        var p = coord.TileCentroid(tile);
        if (p.LengthSquared() < 0.0001f) return 0;

        var direction = p.Normalized();
        int bestIndex = 0;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < Dirs.Length; i++)
        {
            var a = coord.VertexToPixel(Dirs[i].X, Dirs[i].Y);
            var b = coord.VertexToPixel(Dirs[(i + 1) % Dirs.Length].X, Dirs[(i + 1) % Dirs.Length].Y);
            var axis = (a + b).Normalized();
            float dot = direction.Dot(axis);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    static float TileDistanceKey(SkillTreeCoord coord, Vector2I tile)
    {
        var p = coord.TileCentroid(tile);
        return p.LengthSquared();
    }

    // ========================================================================
    // Helper: region from id prefix
    // ========================================================================

    static SkillNodeData.Region RegionFromId(string id) => id switch
    {
        string s when s.StartsWith("str_") => SkillNodeData.Region.Str,
        string s when s.StartsWith("dex_") => SkillNodeData.Region.Dex,
        string s when s.StartsWith("con_") => SkillNodeData.Region.Con,
        string s when s.StartsWith("int_") => SkillNodeData.Region.Int,
        string s when s.StartsWith("wis_") => SkillNodeData.Region.Wis,
        string s when s.StartsWith("cha_") => SkillNodeData.Region.Cha,
        string s when s.StartsWith("trans_") => SkillNodeData.Region.Transition,
        _ => SkillNodeData.Region.None,
    };

    // ========================================================================
    // Node builders
    // ========================================================================

    SkillNodeData Ms(string id, string nm, int dep,
        string[] prereqs, Godot.Collections.Dictionary bonuses, Vector2I gp)
    {
        var node = new SkillNodeData
        {
            NodeId = id, NodeName = nm, CurrentNodeType = SkillNodeData.NodeType.Small,
            CurrentRegion = RegionFromId(id), Depth = dep, GridPosition = gp,
            StatBonuses = bonuses, Description = "",
        };
        node.Prerequisites = prereqs.ToList();
        Nodes[id] = node;
        return node;
    }

    SkillNodeData Mb(string id, string nm, int dep,
        int[] lreq, string[] prereqs, string eff, bool active,
        string desc, Vector2I gp)
    {
        var node = new SkillNodeData
        {
            NodeId = id, NodeName = nm, CurrentNodeType = SkillNodeData.NodeType.Big,
            CurrentRegion = RegionFromId(id), Depth = dep, GridPosition = gp,
            SkillEffect = eff, IsActiveSkill = active, Description = desc,
        };
        if (lreq.Length > 0) node.RequiredLevel = lreq[0];
        node.Prerequisites = prereqs.ToList();
        Nodes[id] = node;
        return node;
    }

    SkillNodeData Mk(string id, string nm, int dep,
        string[] prereqs, string eff,
        string benefit, string costDesc, Godot.Collections.Dictionary cost,
        Vector2I gp)
    {
        var node = new SkillNodeData
        {
            NodeId = id, NodeName = nm, CurrentNodeType = SkillNodeData.NodeType.Keystone,
            CurrentRegion = RegionFromId(id), Depth = dep, GridPosition = gp,
            SkillEffect = eff, IsActiveSkill = false, Description = benefit,
            KeystoneCost = costDesc, CostBonuses = cost,
        };
        node.Prerequisites = prereqs.ToList();
        Nodes[id] = node;
        return node;
    }

    SkillNodeData MakeNode(string id, string nm, SkillNodeData.NodeType ntype,
        SkillNodeData.Region reg, int dep,
        int[] lreq, string[] prereqs,
        Godot.Collections.Dictionary bonuses, string eff, bool active, string desc,
        Vector2I gp)
    {
        var node = new SkillNodeData
        {
            NodeId = id, NodeName = nm, CurrentNodeType = ntype,
            CurrentRegion = reg, Depth = dep, GridPosition = gp,
            StatBonuses = bonuses, SkillEffect = eff, IsActiveSkill = active,
            Description = desc,
        };
        if (lreq.Length > 0) node.RequiredLevel = lreq[0];
        node.Prerequisites = prereqs.ToList();
        Nodes[id] = node;
        return node;
    }

    void Ac(string a, string b)
    {
        if (Nodes.TryGetValue(a, out var na) && Nodes.TryGetValue(b, out var nb))
        {
            if (!na.Neighbors.Contains(b)) na.Neighbors.Add(b);
            if (!nb.Neighbors.Contains(a)) nb.Neighbors.Add(a);
        }
    }

    /// <summary>幂等添加双向连线 — 供 ConstellationBuilder 等外部 DSL 使用。</summary>
    internal void AddEdge(string a, string b) => Ac(a, b);

    static Godot.Collections.Dictionary D(params (string, Variant)[] pairs)
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    // ========================================================================
    // Start node
    // ========================================================================

    void BuildStartNode()
    {
        var n = MakeNode("start", "启程", SkillNodeData.NodeType.Start,
            SkillNodeData.Region.None, 0, [], [],
            new Godot.Collections.Dictionary(), "", false, "所有角色的起点。", Vector2I.Zero);
        n.Neighbors = new List<string> { "str_s01", "dex_s01", "con_s01", "int_s01", "wis_s01", "cha_s01" };
    }

    // ========================================================================
    // STR 力量区域 — 「双刃剑」星座（DSL 绘制）
    // 沿 dir_idx=0 主轴延伸，30 个节点构成一柄竖立的双刃剑：
    //
    //                axial(ring)→  1   2   3   4   5   6   7   8   9   10
    //                                                              
    //   lateral  0:  s01 s02 s03 s15 s04
    //   lateral -1:      s10 s06 b01 s05 b03 s16
    //   lateral -2:          s11 b02 s07 s09 b06 s17
    //   lateral -3:              s08 s12 s13 b05 b07 s19
    //   lateral -4:                  s14 b04 s18 s20 b08 ks01
    //   lateral -5:                                  s21
    //
    // 整体形状：1→2→3→4 节点逐级展宽（剑柄→剑格）
    //          5 节点最宽（剑刃基部，ring 5 共 5 个节点）
    //          后续 4→4→3→3 收窄到剑尖（ring 10 单点 keystone）
    // 大节点（b01..b08）位于剑格与剑刃中央对称位置
    // 全部 30 节点严格落在 STR 60° 扇区内（lateral ∈ [-(ring-1)..0]）
    // ========================================================================

    void BuildStrRegion()
    {
        // 1) 创建所有 30 个节点（坐标由 DSL 在第 2 步覆盖）
        Ms("str_s01", "强健体魄", 1, ["start"], D(("max_hp", 3)), Vector2I.Zero);

        Ms("str_s02", "近战训练", 2, ["str_s01"], D(("melee_hit", 1)), Vector2I.Zero);
        Ms("str_s10", "战斗直觉", 2, ["str_s01"], D(("melee_damage", 1)), Vector2I.Zero);

        Ms("str_s03", "战斗节奏", 3, ["str_s02"], D(("melee_damage", 1)), Vector2I.Zero);
        Ms("str_s06", "武器掌握", 3, ["str_s10"], D(("melee_damage", 1)), Vector2I.Zero);
        Ms("str_s11", "铁壁防御", 3, ["str_s10"], D(("ac", 2)), Vector2I.Zero);

        Ms("str_s15", "盾墙", 4, ["str_s03"], D(("ac", 1), ("all_save", 1)), Vector2I.Zero);
        Mb("str_b01", "基础剑术", 4, [], ["str_s03"], "melee_hit_plus_1", false, "被动: 近战命中+1", Vector2I.Zero);
        Mb("str_b02", "连击", 4, [], ["str_s06"], "double_attack", true, "主动: 攻击2次, 第二次-3命中", Vector2I.Zero);
        Ms("str_s08", "战斗韧性", 4, ["str_s11"], D(("max_hp", 5)), Vector2I.Zero);

        Ms("str_s04", "迅猛之力", 5, ["str_s15"], D(("melee_damage", 2)), Vector2I.Zero);
        Ms("str_s05", "狂战士之怒", 5, ["str_b01"], D(("critical_rate", 0.05), ("melee_damage", 1)), Vector2I.Zero);
        Ms("str_s07", "致命精准", 5, ["str_b02"], D(("critical_rate", 0.03)), Vector2I.Zero);
        Ms("str_s12", "战意高昂", 5, ["str_b02"], D(("melee_damage", 2), ("max_hp", 5)), Vector2I.Zero);
        Ms("str_s14", "战场怒吼", 5, ["str_s08"], D(("melee_hit", 1)), Vector2I.Zero);

        Mb("str_b03", "旋风斩", 6, [], ["str_s05"], "whirlwind", true, "主动: 攻击周围所有敌人", Vector2I.Zero);
        Ms("str_s09", "巨力挥击", 6, ["str_s07"], D(("melee_damage", 3)), Vector2I.Zero);
        Ms("str_s13", "武器大师", 6, ["str_s12"], D(("melee_hit", 2), ("melee_damage", 2)), Vector2I.Zero);
        Mb("str_b04", "重甲精通", 6, [], ["str_s14"], "heavy_armor", false, "被动: 护甲+3, 速度-1", Vector2I.Zero);

        Ms("str_s16", "无畏冲锋", 7, ["str_b03"], D(("melee_damage", 2), ("speed", 1)), Vector2I.Zero);
        Mb("str_b06", "无畏冲锋", 7, [5], ["str_s09"], "fearless_charge", true, "主动: 直线冲撞，途经敌人受武器 50%，命中附 STUN 1 回合", Vector2I.Zero);
        Mb("str_b05", "暴击大师", 7, [], ["str_s13"], "critical_master", false, "被动: 暴击伤害×3", Vector2I.Zero);
        Ms("str_s18", "战场本能", 7, ["str_b04"], D(("max_hp", 5), ("melee_hit", 1)), Vector2I.Zero);

        Ms("str_s17", "不屈意志", 8, ["str_b06"], D(("max_hp", 8), ("all_save", 2)), Vector2I.Zero);
        Mb("str_b07", "战吼震慑", 8, [7], ["str_b05"], "battle_cry", true, "主动: 半径 3 敌人命中 -20%（1 回合）", Vector2I.Zero);
        Ms("str_s20", "战争践踏", 8, ["str_s18"], D(("melee_damage", 2), ("ac", -1)), Vector2I.Zero);

        Ms("str_s19", "血性狂热", 9, ["str_b07"], D(("melee_damage", 2), ("max_hp", 5)), Vector2I.Zero);
        Mb("str_b08", "血腥漩涡", 9, [7], ["str_b07"], "blood_vortex", true, "主动: 横扫周围所有敌人, 每命中1个敌人恢复自身1d6HP", Vector2I.Zero);
        Ms("str_s21", "杀戮本能", 9, ["str_s20"], D(("critical_rate", 0.05), ("melee_damage", 1)), Vector2I.Zero);

        Mk("str_ks01", "狂暴之力", 10, ["str_b08"], "berserk_power",
            "近战伤害+50%", "AC-3, 不能使用盾牌", D(("ac", -3)), Vector2I.Zero);

        // 2) 用 DSL 描述「双刃剑」几何图案
        // 坐标 At(id, axial, lateral)：axial=ring 距 start 的距离，lateral=向 CHA 一侧偏移
        // 严格守约束 lateral ∈ [-(axial-1)..0] 保证不出 STR 扇区。
        new ConstellationBuilder(0)
            // ─── 剑柄底（ring 1）───
            .At("str_s01", 1, 0)

            // ─── 握柄（ring 2，2 节点）───
            .At("str_s02", 2, 0)
            .At("str_s10", 2, -1)
            .Triangle("str_s01", "str_s02", "str_s10")

            // ─── 剑格内层（ring 3，3 节点）───
            .At("str_s03", 3, 0)
            .At("str_s06", 3, -1)
            .At("str_s11", 3, -2)
            .Triangle("str_s02", "str_s03", "str_s06")
            .Triangle("str_s02", "str_s06", "str_s10")
            .Triangle("str_s10", "str_s06", "str_s11")

            // ─── 剑格主体（ring 4，4 节点 — 大节点 b01/b02 位于中心）───
            .At("str_s15", 4, 0)
            .At("str_b01", 4, -1)
            .At("str_b02", 4, -2)
            .At("str_s08", 4, -3)
            .Triangle("str_s03", "str_s15", "str_b01")
            .Triangle("str_s03", "str_b01", "str_s06")
            .Triangle("str_s06", "str_b01", "str_b02")
            .Triangle("str_s06", "str_b02", "str_s11")
            .Triangle("str_s11", "str_b02", "str_s08")

            // ─── 剑刃基部（ring 5，5 节点 — 全宽）───
            .At("str_s04", 5, 0)
            .At("str_s05", 5, -1)
            .At("str_s07", 5, -2)
            .At("str_s12", 5, -3)
            .At("str_s14", 5, -4)
            .Triangle("str_s15", "str_s04", "str_b01")
            .Triangle("str_s04", "str_s05", "str_b01")
            .Triangle("str_b01", "str_s05", "str_b02")
            .Triangle("str_s05", "str_s07", "str_b02")
            .Triangle("str_b02", "str_s07", "str_s12")
            .Triangle("str_b02", "str_s12", "str_s08")
            .Triangle("str_s08", "str_s12", "str_s14")

            // ─── 剑刃身段（ring 6，4 节点 — 大节点 b03/b04 居两端）───
            .At("str_b03", 6, -1)
            .At("str_s09", 6, -2)
            .At("str_s13", 6, -3)
            .At("str_b04", 6, -4)
            .Triangle("str_s04", "str_b03", "str_s05")
            .Triangle("str_s05", "str_b03", "str_s09")
            .Triangle("str_s05", "str_s09", "str_s07")
            .Triangle("str_s07", "str_s09", "str_s13")
            .Triangle("str_s07", "str_s13", "str_s12")
            .Triangle("str_s12", "str_s13", "str_b04")
            .Triangle("str_s12", "str_b04", "str_s14")

            // ─── 剑刃中段（ring 7，4 节点）───
            .At("str_s16", 7, -1)
            .At("str_b06", 7, -2)
            .At("str_b05", 7, -3)
            .At("str_s18", 7, -4)
            .Triangle("str_b03", "str_s16", "str_s09")
            .Triangle("str_s09", "str_s16", "str_b06")
            .Triangle("str_s09", "str_b06", "str_s13")
            .Triangle("str_s13", "str_b06", "str_b05")
            .Triangle("str_s13", "str_b05", "str_b04")
            .Triangle("str_b04", "str_b05", "str_s18")

            // ─── 剑刃尖端（ring 8，3 节点）───
            .At("str_s17", 8, -2)
            .At("str_b07", 8, -3)
            .At("str_s20", 8, -4)
            .Triangle("str_s16", "str_s17", "str_b06")
            .Triangle("str_b06", "str_s17", "str_b07")
            .Triangle("str_b06", "str_b07", "str_b05")
            .Triangle("str_b05", "str_b07", "str_s20")
            .Triangle("str_b05", "str_s20", "str_s18")

            // ─── 剑尖三角（ring 9，3 节点）───
            .At("str_s19", 9, -3)
            .At("str_b08", 9, -4)
            .At("str_s21", 9, -5)
            .Triangle("str_s17", "str_s19", "str_b07")
            .Triangle("str_b07", "str_s19", "str_b08")
            .Triangle("str_b07", "str_b08", "str_s20")
            .Triangle("str_s20", "str_b08", "str_s21")

            // ─── 剑尖（ring 10 keystone）───
            .At("str_ks01", 10, -4)
            .Triangle("str_s19", "str_ks01", "str_b08")
            .Triangle("str_b08", "str_ks01", "str_s21")

            // ─── 入口与起始节点连线 ───
            .Edge("start", "str_s01")

            .Apply(this);

        // 调试：验证 DSL 是否真正写入了 GridPosition
        GD.Print($"[STR DSL] str_s01.GridPos={Nodes["str_s01"].GridPosition}, " +
                 $"str_ks01.GridPos={Nodes["str_ks01"].GridPosition}, " +
                 $"str_s14.GridPos={Nodes["str_s14"].GridPosition}");
    }

    // ========================================================================
    // DEX 灵巧区域 — dir_idx=1, SE(0,1)
    // ========================================================================

    void BuildDexRegion()
    {
        SkillNodeData n;
        n = Ms("dex_s01", "轻灵步伐", 1, ["start"], D(("ac", 1)), Mp(1,1,0));
        n.Neighbors = new List<string> { "start", "dex_s02" };
        n = Ms("dex_s02", "迅捷反应", 2, ["dex_s01"], D(("initiative", 2)), Mp(1,2,0));
        n.Neighbors = new List<string> { "dex_s01", "dex_b01" };
        n = Ms("dex_s14", "灵活身姿", 2, ["dex_s01"], D(("ac", 1)), Mp(1,2,-1));
        n.Neighbors = new List<string> { "dex_s01", "dex_s06" };
        n = Mb("dex_b01", "基础射击", 3, [], ["dex_s02"], "ranged_hit_plus_1", false, "被动: 远程命中+1", Mp(1,3,0));
        n.Neighbors = new List<string> { "dex_s02", "dex_s03", "dex_s06" };
        n = Ms("dex_s03", "瞄准训练", 3, ["dex_b01"], D(("ranged_hit", 1)), Mp(1,3,-1));
        n.Neighbors = new List<string> { "dex_b01", "dex_b02", "dex_s12" };
        n = Ms("dex_s06", "穿透之力", 3, ["dex_s14"], D(("ranged_damage", 1)), Mp(1,3,-2));
        n.Neighbors = new List<string> { "dex_s14", "dex_b01", "dex_s08", "dex_b05" };
        n = Mb("dex_b02", "精准射击", 4, [], ["dex_s03"], "aimed_shot", true, "主动: 瞄准后射击优势+伤害x2", Mp(1,4,0));
        n.Neighbors = new List<string> { "dex_s03", "dex_s04", "dex_s13" };
        n = Ms("dex_s12", "精准本能", 4, ["dex_s03"], D(("ranged_hit", 1)), Mp(1,4,-1));
        n.Neighbors = new List<string> { "dex_s03", "dex_s06" };
        n = Mb("dex_b05", "穿透射击", 4, [], ["dex_s06"], "piercing_shot", false, "被动: 箭矢穿透击中后方1个敌人", Mp(1,4,-2));
        n.Neighbors = new List<string> { "dex_s06", "dex_s07" };
        n = Ms("dex_s08", "暗影步伐", 4, ["dex_s06"], D(("critical_rate", 0.02)), Mp(1,4,-3));
        n.Neighbors = new List<string> { "dex_s06", "dex_b07" };
        n = Ms("dex_s04", "速射技巧", 5, ["dex_b02"], D(("ranged_damage", 1)), Mp(1,5,0));
        n.Neighbors = new List<string> { "dex_b02", "dex_b03" };
        n = Ms("dex_s13", "游侠之道", 5, ["dex_b02"], D(("initiative", 2)), Mp(1,5,-1));
        n.Neighbors = new List<string> { "dex_b02", "dex_b07" };
        n = Ms("dex_s07", "长距瞄准", 5, ["dex_b05"], D(("range_bonus", 1)), Mp(1,5,-2));
        n.Neighbors = new List<string> { "dex_b05", "dex_b06" };
        n = Mb("dex_b07", "闪避翻滚", 5, [], ["dex_s08"], "evasive_roll", true, "主动: 位移 4 格，不触发借机攻击", Mp(1,5,-3));
        n.Neighbors = new List<string> { "dex_s08", "dex_s09", "dex_s13" };
        n = Mb("dex_b03", "连珠箭", 6, [], ["dex_s04"], "multi_shot", true, "主动: 连射3支箭, 每支-2命中", Mp(1,6,0));
        n.Neighbors = new List<string> { "dex_s04", "dex_s05" };
        n = Ms("dex_s05", "鹰眼", 6, ["dex_b03"], D(("ranged_hit", 2)), Mp(1,6,1));
        n.Neighbors = new List<string> { "dex_b03" };
        n = Mb("dex_b06", "致盲箭", 6, [], ["dex_s07"], "blind_arrow", true, "主动: 命中后目标-4命中(2回合)", Mp(1,6,-1));
        n.Neighbors = new List<string> { "dex_s07", "dex_s09" };
        n = Ms("dex_s09", "毒蛇之牙", 6, ["dex_b07"], D(("critical_rate", 0.03), ("ranged_damage", 1)), Mp(1,6,-2));
        n.Neighbors = new List<string> { "dex_b07", "dex_b06" };
        n = Mb("dex_b11", "陷阱大师", 6, [], ["dex_s13"], "trap_master", true, "主动: 放置陷阱, 触发的敌人停止移动并受1d8伤害", Mp(1,6,-3));
        n.Neighbors = new List<string> { "dex_s13" };
        n = Mb("dex_b04", "游猎姿态", 7, [], ["dex_s05"], "hunter_stance", true, "姿态: 远程伤害 +15%；近战伤害 -20%", Mp(1,7,1));
        n.Neighbors = new List<string> { "dex_s05" };
        n = Ms("dex_s16", "疾风步", 7, ["dex_b11"], D(("speed", 2), ("ac", 1)), Mp(1,7,-1));
        n.Neighbors = new List<string> { "dex_b11", "dex_b12" };
        n = Ms("dex_s15", "暗影之舞", 7, ["dex_s09"], D(("critical_rate", 0.05), ("ac", 1)), Mp(1,7,-2));
        n.Neighbors = new List<string> { "dex_s09", "dex_b08" };
        n = Ms("dex_s10", "致命毒药", 7, ["dex_b08"], D(("ranged_damage", 1)), Mp(1,7,-3));
        n.Neighbors = new List<string> { "dex_b08", "dex_b09", "dex_b10" };
        n = Mb("dex_b08", "暗影突袭", 7, [], ["dex_s15"], "shadow_strike", true, "主动: 潜行状态下突袭伤害翻倍", Mp(1,7,-4));
        n.Neighbors = new List<string> { "dex_s15", "dex_s10" };
        n = Mb("dex_b09", "致命一击", 7, [], ["dex_s10"], "deadly_blow", false, "被动: 偷袭伤害+3d6", Mp(1,7,-5));
        n.Neighbors = new List<string> { "dex_s10", "dex_s11" };
        n = Mb("dex_b10", "毒刃", 7, [], ["dex_s10"], "poison_blade", true, "主动: 攻击附带中毒(每回合1d4, 3回合)", Mp(1,7,-6));
        n.Neighbors = new List<string> { "dex_s10" };
        n = Ms("dex_s11", "幽灵之触", 8, ["dex_b09"], D(("critical_rate", 0.05)), Mp(1,8,-5));
        n.Neighbors = new List<string> { "dex_b09", "dex_ks01" };
        n = Mk("dex_ks01", "幽灵步伐", 8, ["dex_s11"], "ghost_step", "永久获得掩护状态(远程攻击-2命中)", "HP上限-20%", D(("max_hp_pct", -0.2)), Mp(1,8,-6));
        n.Neighbors = new List<string> { "dex_s11" };
        n = Mb("dex_b12", "闪电反射", 8, [], ["dex_s16"], "lightning_reflex", false, "被动: 先攻+5, 每场战斗第一次攻击优势", Mp(1,8,-1));
        n.Neighbors = new List<string> { "dex_s16", "dex_s17" };
        n = Ms("dex_s17", "幻影连步", 8, ["dex_b12"], D(("initiative", 3), ("ac", 1)), Mp(1,9,-1));
        n.Neighbors = new List<string> { "dex_b12" };
        n = Ms("dex_s18", "射手之道", 8, ["dex_b04"], D(("ranged_hit", 2)), Mp(1,8,1));
        n.Neighbors = new List<string> { "dex_b04", "dex_b13" };
        n = Mb("dex_b13", "流星箭雨", 9, [], ["dex_s18"], "arrow_rain", true, "主动: 半径 2，范围内敌人各受武器 60%", Mp(1,9,1));
        n.Neighbors = new List<string> { "dex_s18", "dex_s19" };
        n = Ms("dex_s19", "元素共鸣", 9, ["dex_b13"], D(("ranged_hit", 2), ("ranged_damage", 2)), Mp(1,10,1));
        n.Neighbors = new List<string> { "dex_b13" };
    }

    // ========================================================================
    // CON 体魄区域 — dir_idx=2, SW(-1,1)
    // ========================================================================

    void BuildConRegion()
    {
        SkillNodeData n;
        // Ring 1
        n = Ms("con_s01", "强韧体质", 1, ["start"], D(("max_hp", 5)), Mp(2,1,0));
        n.Neighbors = new List<string> { "start", "con_s02" };
        // Ring 2
        n = Ms("con_s02", "坚固体格", 2, ["con_s01"], D(("ac", 1)), Mp(2,2,0));
        n.Neighbors = new List<string> { "con_s01", "con_b01" };
        n = Ms("con_s08", "铁壁之心", 2, ["con_s01"], D(("ac", 1)), Mp(2,2,-1));
        n.Neighbors = new List<string> { "con_s01", "con_s06" };
        // Ring 3
        n = Mb("con_b01", "盾击", 3, [], ["con_s02"], "shield_bash", true, "主动: 攻击+推开目标1格", Mp(2,3,0));
        n.Neighbors = new List<string> { "con_s02", "con_s03", "con_s06" };
        n = Ms("con_s03", "厚甲训练", 3, ["con_b01"], D(("ac", 1)), Mp(2,3,-1));
        n.Neighbors = new List<string> { "con_b01", "con_b02" };
        n = Ms("con_s06", "格挡本能", 3, ["con_s08"], D(("all_save", 1)), Mp(2,3,-2));
        n.Neighbors = new List<string> { "con_s08", "con_b01" };
        // Ring 4
        n = Mb("con_b02", "坚壁清野", 4, [], ["con_s03"], "fortify", false, "被动: 受到伤害时AC+2(1回合)", Mp(2,4,0));
        n.Neighbors = new List<string> { "con_s03", "con_s09", "con_s04" };
        n = Ms("con_s09", "体力充沛", 4, ["con_b02"], D(("max_hp", 5)), Mp(2,4,-1));
        n.Neighbors = new List<string> { "con_b02", "con_b05" };
        n = Mb("con_b05", "铁壁", 4, [], ["con_s06"], "iron_wall", false, "被动: 受到物理伤害-3", Mp(2,4,-2));
        n.Neighbors = new List<string> { "con_s06", "con_s05" };
        // Ring 5
        n = Ms("con_s04", "生命之泉", 5, ["con_b02"], D(("max_hp", 8)), Mp(2,5,0));
        n.Neighbors = new List<string> { "con_b02", "con_b03" };
        n = Ms("con_s05", "再生之力", 5, ["con_b05"], D(("max_hp", 5), ("heal_amount", 1)), Mp(2,5,-1));
        n.Neighbors = new List<string> { "con_b05", "con_s10" };
        n = Ms("con_s10", "不灭意志", 5, ["con_s05"], D(("all_save", 2), ("max_hp", 5)), Mp(2,5,-2));
        n.Neighbors = new List<string> { "con_s05", "con_s07" };
        n = Ms("con_s07", "元素抗性", 5, ["con_s10"], D(("ac", 1), ("all_save", 1)), Mp(2,5,-3));
        n.Neighbors = new List<string> { "con_s10" };
        // Ring 6
        n = Mb("con_b03", "不屈", 6, [], ["con_s04"], "unyielding", false, "机制: HP低于25%时伤害减半", Mp(2,6,0));
        n.Neighbors = new List<string> { "con_s04", "con_s12" };
        n = Mb("con_b04", "守护链接", 6, [], ["con_s09"], "guardian_link", true, "主动: 指定 1 友军，其受到伤害的 50% 转移到自己（3 回合）", Mp(2,6,-1));
        n.Neighbors = new List<string> { "con_s09", "con_s11" };
        n = Ms("con_s11", "铁血战士", 6, ["con_b04"], D(("max_hp", 10), ("melee_damage", 1)), Mp(2,6,-2));
        n.Neighbors = new List<string> { "con_b04", "con_ks01" };
        // Ring 7
        n = Mk("con_ks01", "不朽之躯", 7, ["con_s11"], "immortal_body", "HP低于0时1/战斗概率=体质修正恢复1HP", "移动速度-2", D(("speed", -2)), Mp(2,7,-3));
        n.Neighbors = new List<string> { "con_s11" };
        n = Ms("con_s12", "活力涌动", 7, ["con_b03"], D(("max_hp", 10), ("heal_amount", 1)), Mp(2,7,0));
        n.Neighbors = new List<string> { "con_b03", "con_b07" };
        n = Mb("con_b07", "岿然不动", 8, [], ["con_s12"], "unyielding_stand", true, "主动: 立即回复 25% 最大 HP，本回合受到伤害 -30%", Mp(2,8,0));
        n.Neighbors = new List<string> { "con_s12", "con_s13" };
        n = Ms("con_s13", "坚不可摧", 9, ["con_b07"], D(("ac", 2), ("all_save", 2)), Mp(2,9,0));
        n.Neighbors = new List<string> { "con_b07" };
        // 外扩分支
        n = Ms("con_s14", "厚皮", 6, ["con_s06"], D(("ac", 1)), Mp(2,6,1));
        n.Neighbors = new List<string> { "con_s06", "con_b08" };
        n = Mb("con_b08", "巨人之力", 7, [], ["con_s14"], "giant_strength", false, "被动: 近战伤害+3, 可使用双手武器单手", Mp(2,7,1));
        n.Neighbors = new List<string> { "con_s14", "con_s15" };
        n = Ms("con_s15", "山岳之躯", 7, ["con_b08"], D(("ac", 2), ("max_hp", 10)), Mp(2,7,-1));
        n.Neighbors = new List<string> { "con_b08", "con_b09" };
        n = Mb("con_b09", "最后阵地", 8, [7], ["con_s15"], "last_stand", false, "机制: HP低于25%时自动获得AC+5和伤害+50%, 直到HP恢复至25%以上", Mp(2,8,-1));
        n.Neighbors = new List<string> { "con_s15" };
    }

    // ========================================================================
    // INT 智力区域 — dir_idx=3, W(-1,0)
    // ========================================================================

    void BuildIntRegion()
    {
        SkillNodeData n;
        // Ring 1
        n = Ms("int_s01", "魔力觉醒", 1, ["start"], D(("mana_max", 3)), Mp(3,1,0));
        n.Neighbors = new List<string> { "start", "int_s02" };
        // Ring 2
        n = Ms("int_s02", "奥术基础", 2, ["int_s01"], D(("mana_max", 2)), Mp(3,2,0));
        n.Neighbors = new List<string> { "int_s01", "int_b01" };
        n = Ms("int_s15", "元素亲和", 2, ["int_s01"], D(("spell_hit", 1)), Mp(3,2,-1));
        n.Neighbors = new List<string> { "int_s01", "int_s06" };
        // Ring 3
        n = Mb("int_b01", "法术强化", 3, [], ["int_s02"], "spell_hit_plus_1", false, "被动: 法术命中+1", Mp(3,3,0));
        n.Neighbors = new List<string> { "int_s02", "int_s03", "int_s06" };
        n = Ms("int_s03", "魔力涌流", 3, ["int_b01"], D(("mana_max", 3)), Mp(3,3,-1));
        n.Neighbors = new List<string> { "int_b01", "int_b02" };
        n = Ms("int_s06", "法力护盾", 3, ["int_s15"], D(("mana_max", 2), ("ac", 1)), Mp(3,3,-2));
        n.Neighbors = new List<string> { "int_s15", "int_b01" };
        // Ring 4
        n = Mb("int_b02", "法术研习·1环", 4, [], ["int_s03"], "spell_slot_1", true, "主动: 激活时选 1 系，获得 1 环法术（毁灭/幻术/附魔/防护/生命）", Mp(3,4,0));
        n.Neighbors = new List<string> { "int_s03", "int_s04", "int_s08" };
        n = Ms("int_s13", "魔力回涌", 4, ["int_b01"], D(("mana_max", 3)), Mp(3,4,-1));
        n.Neighbors = new List<string> { "int_b01" };
        n = Mb("int_b08", "魔力汲取", 4, [], ["int_s06"], "mana_drain", true, "主动: 汲取目标法力恢复自身", Mp(3,4,-2));
        n.Neighbors = new List<string> { "int_s06", "int_s07" };
        // Ring 5
        n = Ms("int_s04", "法术穿透", 5, ["int_b02"], D(("spell_damage", 1)), Mp(3,5,0));
        n.Neighbors = new List<string> { "int_b02", "int_b03" };
        n = Ms("int_s08", "奥术护盾", 5, ["int_b02"], D(("mana_max", 3), ("ac", 1)), Mp(3,5,-1));
        n.Neighbors = new List<string> { "int_b02", "int_b04" };
        n = Ms("int_s07", "元素精通", 5, ["int_b08"], D(("spell_damage", 1), ("mana_max", 2)), Mp(3,5,-2));
        n.Neighbors = new List<string> { "int_b08", "int_b03" };
        // Ring 6
        n = Mb("int_b03", "法术研习·2环", 6, [], ["int_s04"], "spell_slot_2", true, "主动: 激活时选 1 系，获得 2 环法术", Mp(3,6,0));
        n.Neighbors = new List<string> { "int_s04", "int_s07", "int_b04" };
        n = Mb("int_b04", "法术反射", 6, [], ["int_s08"], "spell_reflect", false, "被动: 1次/回合反射敌方法术", Mp(3,6,-1));
        n.Neighbors = new List<string> { "int_s08", "int_s14" };
        n = Ms("int_s14", "奥术精通", 6, ["int_b04"], D(("spell_damage", 1), ("mana_max", 3)), Mp(3,6,-2));
        n.Neighbors = new List<string> { "int_b04", "int_b09" };
        n = Mb("int_b09", "法术研习·4环", 6, [], ["int_s14"], "spell_slot_4", true, "主动: 激活时选 1 系，获得 4 环法术（前置 3 环）", Mp(3,6,-3));
        n.Neighbors = new List<string> { "int_s14", "int_s12" };
        // Ring 7
        n = Ms("int_s09", "法术大师", 7, ["int_b03"], D(("spell_hit", 2), ("spell_damage", 1)), Mp(3,7,-1));
        n.Neighbors = new List<string> { "int_b03" };
        n = Ms("int_s12", "专注之心", 7, ["int_b09"], D(("spell_damage", 2)), Mp(3,7,-3));
        n.Neighbors = new List<string> { "int_b09", "int_ks01" };
        n = Mb("int_b05", "法术研习·3环", 8, [], ["int_s09"], "spell_slot_3", true, "主动: 激活时选 1 系，获得 3 环法术（前置 2 环）", Mp(3,8,-1));
        n.Neighbors = new List<string> { "int_s09" };
        n = Mk("int_ks01", "绝对专注", 8, ["int_s12"], "absolute_focus", "法术强度+4", "不能学习其他体系的法术", new Godot.Collections.Dictionary(), Mp(3,8,-3));
        n.Neighbors = new List<string> { "int_s12" };
        // 外扩分支
        n = Ms("int_s16", "学者智慧", 7, ["int_b04"], D(("mana_max", 5), ("all_save", 1)), Mp(3,7,0));
        n.Neighbors = new List<string> { "int_b04", "int_b10" };
        n = Mb("int_b10", "知识就是力量", 8, [], ["int_s16"], "knowledge_power", false, "被动: 法术伤害额外+智力修正", Mp(3,8,0));
        n.Neighbors = new List<string> { "int_s16", "int_s17" };
        n = Ms("int_s17", "护盾强化", 8, ["int_b10"], D(("mana_max", 5), ("ac", 1)), Mp(3,9,0));
        n.Neighbors = new List<string> { "int_b10" };
        n = Ms("int_s18", "奥术回响", 7, ["int_s14"], D(("mana_max", 3)), Mp(3,7,-2));
        n.Neighbors = new List<string> { "int_s14", "int_b11" };
        n = Mb("int_b11", "法术研习·5环", 8, [], ["int_s18"], "spell_slot_5", true, "主动: 激活时选 1 系，获得 5 环法术（前置 4 环）", Mp(3,8,-2));
        n.Neighbors = new List<string> { "int_s18" };
        n = Ms("int_s19", "预知未来", 7, ["int_b09"], D(("all_save", 2), ("initiative", 2)), Mp(3,7,-4));
        n.Neighbors = new List<string> { "int_b09", "int_b12" };
        n = Mb("int_b12", "命运之眼", 8, [], ["int_s19"], "fate_eye", false, "机制: 每场战斗重掷1次失败的豁免", Mp(3,8,-4));
        n.Neighbors = new List<string> { "int_s19", "int_s20" };
        n = Ms("int_s20", "时空裂隙", 9, ["int_b12"], D(("mana_max", 10), ("speed", 2)), Mp(3,9,-4));
        n.Neighbors = new List<string> { "int_b12" };
    }

    // ========================================================================
    // WIS 感知区域 — dir_idx=4, NW(0,-1)
    // 2026-05-17 重设计：WIS = 暴击/刺杀主属性 + 法力续航。
    //   小节点：critical_rate / mana_max / mana_regen 加成（不再是治疗主题）
    //   大节点：主动技能 = 爆头突袭/暗杀/法力涌动/影刃涂毒 等刺客技能
    //          被动 = 致命猎杀（HP<30% 敌人加成）/ 死灵之锋（击杀后暴击率）
    //   保留 wis_b03/b05 为 hybrid 用（INT+WIS 贤者）— ward_blessing/purify_field stub
    //   keystone = assassin_instinct (暴击倍率 +0.5x, 禁重甲/盾)
    // ========================================================================

    void BuildWisRegion()
    {
        SkillNodeData n;
        // Ring 1
        n = Ms("wis_s01", "暴击直觉", 1, ["start"], D(("critical_rate", 0.02)), Mp(4,1,0));
        n.Neighbors = new List<string> { "start", "wis_s02" };
        // Ring 2
        n = Ms("wis_s02", "法力觉醒", 2, ["wis_s01"], D(("mana_max", 3)), Mp(4,2,0));
        n.Neighbors = new List<string> { "wis_s01", "wis_b01" };
        n = Ms("wis_s09", "致命专注", 2, ["wis_s01"], D(("critical_rate", 0.02), ("wis_check", 1)), Mp(4,2,-1));
        n.Neighbors = new List<string> { "wis_s01", "wis_s06" };
        // Ring 3
        n = Mb("wis_b01", "法力涌动", 3, [], ["wis_s02"], "mana_surge", true,
            "主动: AP 4，每场战斗 1 次。立即将自身 Mana 恢复至上限。", Mp(4,3,0));
        n.Neighbors = new List<string> { "wis_s02", "wis_s03", "wis_s06" };
        n = Ms("wis_s03", "影刃训练", 3, ["wis_b01"], D(("critical_rate", 0.03)), Mp(4,3,-1));
        n.Neighbors = new List<string> { "wis_b01", "wis_b02" };
        n = Ms("wis_s06", "心灵专注", 3, ["wis_s09"], D(("mana_max", 3), ("mana_regen", 1)), Mp(4,3,-2));
        n.Neighbors = new List<string> { "wis_s09", "wis_b01", "wis_s08" };
        // Ring 4
        n = Mb("wis_b02", "爆头突袭", 4, [], ["wis_s03"], "head_shot", true,
            "主动: AP 8，下一次近战或远程武器攻击必定暴击且伤害 ×1.5；不可与精准射击/暗杀同回合使用。", Mp(4,4,0));
        n.Neighbors = new List<string> { "wis_s03", "wis_s04", "wis_b04" };
        n = Mb("wis_b04", "致命猎杀", 4, [], ["wis_s06"], "lethal_focus", false,
            "被动: 对当前 HP 低于 30% 的敌人，攻击命中 +2、暴击率 +10%（不影响法术）。", Mp(4,4,-1));
        n.Neighbors = new List<string> { "wis_s06", "wis_s08" };
        n = Ms("wis_s08", "暴击精通", 4, ["wis_s06"], D(("critical_rate", 0.03)), Mp(4,4,-2));
        n.Neighbors = new List<string> { "wis_s06", "wis_b06" };
        // Ring 5
        n = Ms("wis_s04", "暴击专精", 5, ["wis_b02"], D(("critical_rate", 0.04)), Mp(4,5,0));
        n.Neighbors = new List<string> { "wis_b02", "wis_b03" };
        n = Ms("wis_s07", "法力洪流", 5, ["wis_b04"], D(("mana_max", 5), ("mana_regen", 1)), Mp(4,5,-1));
        n.Neighbors = new List<string> { "wis_b04", "wis_b06" };
        n = Mb("wis_b06", "影刃涂毒", 5, [], ["wis_s08"], "poison_blade", true,
            "主动: AP 3，下次武器攻击附加 1d4×3 回合中毒（复用 DEX 系 poison_blade handler）。", Mp(4,5,-2));
        n.Neighbors = new List<string> { "wis_s08", "wis_s07" };
        // Ring 6
        n = Mb("wis_b03", "守护祝福", 6, [], ["wis_s04"], "ward_blessing", true,
            "主动: 给周围友军赋予 +AC/豁免增益（INT+WIS 贤者跨界用；尚未完整实装）。", Mp(4,6,0));
        n.Neighbors = new List<string> { "wis_s04", "wis_s05" };
        n = Mb("wis_b05", "净化领域", 6, [], ["wis_s07"], "purify_field", true,
            "主动: 周围友军移除负面状态并恢复少量 HP（INT+WIS 贤者跨界用；尚未完整实装）。", Mp(4,6,-1));
        n.Neighbors = new List<string> { "wis_s07", "wis_s10" };
        n = Ms("wis_s10", "致命之眼", 6, ["wis_b05"], D(("critical_rate", 0.05)), Mp(4,6,-2));
        n.Neighbors = new List<string> { "wis_b05" };
        // Ring 7
        n = Ms("wis_s05", "影刃大师", 7, ["wis_b03"], D(("critical_rate", 0.05), ("mana_max", 5)), Mp(4,7,0));
        n.Neighbors = new List<string> { "wis_b03", "wis_ks01" };
        n = Mk("wis_ks01", "刺客本能", 7, ["wis_s05"], "assassin_instinct",
            "暴击倍率 +0.5x；暴击率 +5%",
            "不能装备重甲（DR > 5）；不能使用盾牌",
            new Godot.Collections.Dictionary(), Mp(4,7,-1));
        n.Neighbors = new List<string> { "wis_s05" };
        // 外扩分支
        n = Ms("wis_s11", "智慧之眼", 8, ["wis_ks01"], D(("wis_check", 3)), Mp(4,8,0));
        n.Neighbors = new List<string> { "wis_ks01", "wis_b07" };
        n = Mb("wis_b07", "暗杀", 9, [], ["wis_s11"], "assassinate", true,
            "主动: AP 8，每场战斗 1 次。指定 HP 低于 30% 的敌方单位直接斩杀（豁免无效，无视 AC/DR；boss 改为造成 50% 当前 HP 真伤）。", Mp(4,9,0));
        n.Neighbors = new List<string> { "wis_s11", "wis_s12" };
        n = Ms("wis_s12", "完美专注", 9, ["wis_b07"], D(("critical_rate", 0.05), ("mana_regen", 1)), Mp(4,10,0));
        n.Neighbors = new List<string> { "wis_b07" };
        n = Ms("wis_s13", "致命爆击", 6, ["wis_s10"], D(("critical_rate", 0.05)), Mp(4,6,1));
        n.Neighbors = new List<string> { "wis_s10", "wis_b08" };
        n = Mb("wis_b08", "影遁", 7, [], ["wis_s13"], "stealth", true,
            "主动: AP 4，进入潜行状态（复用 DEX 系 stealth handler；潜行+暴头突袭/暗杀爆发）。", Mp(4,7,1));
        n.Neighbors = new List<string> { "wis_s13", "wis_s14" };
        n = Ms("wis_s14", "暴击本能", 7, ["wis_b08"], D(("critical_rate", 0.03)), Mp(4,7,-2));
        n.Neighbors = new List<string> { "wis_b08", "wis_b09" };
        n = Mb("wis_b09", "死灵之锋", 8, [7], ["wis_s14"], "deathblow_focus", false,
            "被动: 击杀任意敌人后，下一次攻击暴击率 +10%、伤害 +20%（持续 1 回合或直至触发）。", Mp(4,8,-2));
        n.Neighbors = new List<string> { "wis_s14" };
    }

    // ========================================================================
    // CHA 魅力区域 — dir_idx=5, NE(1,-1)
    // ========================================================================

    void BuildChaRegion()
    {
        SkillNodeData n;
        // Ring 1
        n = Ms("cha_s01", "鼓舞士气", 1, ["start"], new Godot.Collections.Dictionary(), Mp(5,1,0));
        n.Neighbors = new List<string> { "start", "cha_s02" };
        // Ring 2
        n = Ms("cha_s02", "领袖气质", 2, ["cha_s01"], D(("cha_check", 1)), Mp(5,2,0));
        n.Neighbors = new List<string> { "cha_s01", "cha_b01" };
        n = Ms("cha_s09", "交际手腕", 2, ["cha_s01"], new Godot.Collections.Dictionary(), Mp(5,2,-1));
        n.Neighbors = new List<string> { "cha_s01", "cha_s06" };
        // Ring 3
        n = Mb("cha_b01", "战术调度", 3, [], ["cha_s02"], "tactical_reposition", true, "主动: 指定 1 友军获得额外 4 AP，可超出 AP 上限", Mp(5,3,0));
        n.Neighbors = new List<string> { "cha_s02", "cha_s03", "cha_s06" };
        n = Ms("cha_s03", "威压", 3, ["cha_b01"], D(("cha_check", 1)), Mp(5,3,-1));
        n.Neighbors = new List<string> { "cha_b01", "cha_b02" };
        n = Ms("cha_s06", "团结之力", 3, ["cha_s09"], D(("ally_bonus", 1)), Mp(5,3,-2));
        n.Neighbors = new List<string> { "cha_s09", "cha_b01", "cha_b04" };
        // Ring 4
        n = Mb("cha_b02", "集结号令", 4, [], ["cha_s03"], "gathering_order", true, "主动: 所有友军下回合命中 +10%、伤害 +10%", Mp(5,4,0));
        n.Neighbors = new List<string> { "cha_s03", "cha_s04" };
        n = Ms("cha_s10", "声东击西", 4, ["cha_s06"], D(("cha_check", 1), ("initiative", 1)), Mp(5,4,-1));
        n.Neighbors = new List<string> { "cha_s06", "cha_b06" };
        n = Mb("cha_b04", "外交官", 4, [], ["cha_s06"], "diplomat", false, "被动: 商店价格-15%", Mp(5,4,-2));
        n.Neighbors = new List<string> { "cha_s06", "cha_s14" };
        // Ring 5
        n = Ms("cha_s04", "统率之力", 5, ["cha_b02"], D(("ally_bonus", 1)), Mp(5,5,0));
        n.Neighbors = new List<string> { "cha_b02", "cha_b03" };
        n = Mb("cha_b06", "暗影交易", 5, [], ["cha_s10"], "shadow_deal", true, "主动: 贿赂敌人使其1回合不攻击", Mp(5,5,-1));
        n.Neighbors = new List<string> { "cha_s10", "cha_s07" };
        n = Ms("cha_s07", "鼓舞之歌", 5, ["cha_b06"], D(("ally_bonus", 1)), Mp(5,5,-2));
        n.Neighbors = new List<string> { "cha_b06", "cha_b05" };
        // Ring 6
        n = Mb("cha_b03", "统帅光环", 6, [], ["cha_s04"], "command_aura", false, "被动: 周围友军攻击+1 AC+1", Mp(5,6,0));
        n.Neighbors = new List<string> { "cha_s04", "cha_s11" };
        n = Mb("cha_b05", "威压", 6, [], ["cha_s07"], "command_intimidate", true, "主动: 半径 3 敌人命中 -10%（3 回合）", Mp(5,6,-1));
        n.Neighbors = new List<string> { "cha_s07", "cha_s08" };
        n = Ms("cha_s11", "王者风范", 6, ["cha_b03"], D(("ally_bonus", 1)), Mp(5,6,-2));
        n.Neighbors = new List<string> { "cha_b03", "cha_s12" };
        n = Ms("cha_s12", "领袖魅力", 6, ["cha_s11"], D(("ally_bonus", 1)), Mp(5,6,-3));
        n.Neighbors = new List<string> { "cha_s11", "cha_b10" };
        // Ring 7
        n = Ms("cha_s08", "王者之心", 7, ["cha_b05"], D(("ally_bonus", 1)), Mp(5,7,-1));
        n.Neighbors = new List<string> { "cha_b05", "cha_ks01" };
        n = Mk("cha_ks01", "君临天下", 7, ["cha_s08"], "royal_presence", "范围内友军全豁免+2不会恐慌", "自身HP-20%", D(("max_hp_pct", -0.2)), Mp(5,8,-1));
        n.Neighbors = new List<string> { "cha_s08" };
        n = Mb("cha_b10", "英雄号召", 7, [7], ["cha_s12"], "battle_banner", true, "主动: 插战旗；半径 2 内友军命中 +10%、AC +1（3 回合）", Mp(5,7,-2));
        n.Neighbors = new List<string> { "cha_s12", "cha_s13" };
        n = Ms("cha_s13", "传奇号召", 8, ["cha_b10"], D(("cha_check", 2)), Mp(5,8,-2));
        n.Neighbors = new List<string> { "cha_b10" };
        // 外扩分支
        n = Ms("cha_s14", "商业嗅觉", 5, ["cha_b04"], D(("cha_check", 1)), Mp(5,5,1));
        n.Neighbors = new List<string> { "cha_b04", "cha_b11" };
        n = Mb("cha_b11", "商业帝国", 6, [5], ["cha_s14"], "merchant_empire", false, "机制: 每次战斗结束额外获得敌人等级x5金币, 商店出现稀有物品概率+15%", Mp(5,6,1));
        n.Neighbors = new List<string> { "cha_s14" };
        n = Ms("cha_s15", "仇恨刻印", 7, ["cha_s08"], D(("melee_damage", 1)), Mp(5,7,0));
        n.Neighbors = new List<string> { "cha_b03", "cha_b12" };
        n = Mb("cha_b12", "复仇誓言", 8, [7], ["cha_s15"], "vow_of_vengeance", false, "机制: 标记1个敌人为复仇目标, 对其造成的伤害+25%, 目标死亡时恢复全队10%HP", Mp(5,8,0));
        n.Neighbors = new List<string> { "cha_s15", "cha_s16" };
        n = Ms("cha_s16", "血债血偿", 9, ["cha_b12"], D(("ally_bonus", 2)), Mp(5,9,0));
        n.Neighbors = new List<string> { "cha_b12" };
    }

    // ========================================================================
    // 过渡节点 — 位于相邻区域交界
    // ========================================================================

    void BuildTransitionNodes()
    {
        SkillNodeData n;
        // 过渡节点坐标：位于两个相邻区域方向的中间位置
        // 使用两个方向向量的平均来计算边界位置

        // === 内层过渡 (Ring 2) — 连接相邻区域的 Ring 1 节点 ===

        // STR(dir0: 1,0) <-> DEX(dir1: 0,1) — 中间方向约 (1,1) 归一化到 ring 2
        n = Ms("trans_sd01", "战斗技巧", 2, ["str_s01", "dex_s01"], D(("melee_hit", 1), ("critical_rate", 0.02)), new Vector2I(1, 1));
        n.IsBridge = true;

        // CON(dir2: -1,1) <-> STR(dir0: 1,0) — 中间方向约 (0,1) 但偏 STR 侧
        n = Ms("trans_sc01", "近战生存", 2, ["str_s01", "con_s01"], D(("max_hp", 3), ("melee_hit", 1)), new Vector2I(0, 1));
        n.IsBridge = true;

        // DEX(dir1: 0,1) <-> INT(dir3: -1,0) — 中间方向约 (-1,2) 归一化
        n = Ms("trans_di01", "精准施法", 2, ["dex_s01", "int_s01"], D(("mana_max", 2), ("spell_hit", 1)), new Vector2I(-1, 2));
        n.IsBridge = true;

        // INT(dir3: -1,0) <-> WIS(dir4: 0,-1) — 中间方向约 (-1,-1)
        n = Ms("trans_iw01", "神秘学", 2, ["int_s01", "wis_s01"], D(("mana_max", 2), ("all_save", 1)), new Vector2I(-1, -1));
        n.IsBridge = true;

        // WIS(dir4: 0,-1) <-> CHA(dir5: 1,-1) — 中间方向约 (1,-2)
        n = Ms("trans_wc01", "精神领袖", 2, ["wis_s01", "cha_s01"], D(("heal_amount", 1)), new Vector2I(1, -2));
        n.IsBridge = true;

        // CHA(dir5: 1,-1) <-> CON(dir2: -1,1) — 中间方向约 (0,0)... 实际在 ring2 偏 CHA-CON 边界
        n = Ms("trans_cc01", "鼓舞防御", 2, ["cha_s01", "con_s01"], D(("ac", 1)), new Vector2I(-1, 0));
        n.IsBridge = true;

        // === 中层过渡 (Ring 4) — 连接相邻区域的 Ring 3 大节点 ===

        // STR <-> DEX
        n = Ms("trans_sd02", "武器大师", 4, ["str_b01", "dex_b01"], D(("melee_hit", 1), ("ranged_hit", 1)), new Vector2I(2, 2));
        n.IsBridge = true;

        // STR <-> CON
        n = Ms("trans_sc02", "盾牌掌握", 4, ["str_b01", "con_b01"], D(("ac", 1), ("melee_damage", 1)), new Vector2I(0, 2));
        n.IsBridge = true;

        // DEX <-> INT
        n = Ms("trans_di02", "奥术射手", 4, ["dex_b01", "int_b01"], D(("ranged_hit", 1), ("spell_damage", 1)), new Vector2I(-2, 4));
        n.IsBridge = true;

        // INT <-> WIS
        n = Ms("trans_iw02", "古代智慧", 4, ["int_b01", "wis_b01"], D(("spell_damage", 1), ("heal_amount", 1)), new Vector2I(-2, -2));
        n.IsBridge = true;

        // WIS <-> CHA
        n = Ms("trans_wc02", "净化光辉", 4, ["wis_b01", "cha_b01"], D(("heal_amount", 1), ("ally_bonus", 1)), new Vector2I(2, -4));
        n.IsBridge = true;

        // CHA <-> CON
        n = Ms("trans_cc02", "铁血指挥", 4, ["cha_b01", "con_b01"], D(("ally_bonus", 1), ("max_hp", 3)), new Vector2I(-2, 0));
        n.IsBridge = true;

        // === 深层过渡 (Ring 6-7) — 连接高级大节点 ===

        // STR <-> DEX 深层
        n = Ms("trans_sd03", "剑弓合一", 7, ["str_b08", "dex_b13"], D(("melee_damage", 1), ("ranged_damage", 1), ("critical_rate", 0.02)), new Vector2I(4, 4));
        n.IsBridge = true;

        // STR <-> CHA 深层
        // 位于 STR(b07@8,-3) 与 CHA(b10@5,7,-2 → grid(7,-7) 之间, 偏向 STR-CHA 交界)
        n = Ms("trans_sc03", "战意昂扬", 7, ["str_b07", "cha_b10"], D(("melee_hit", 1)), new Vector2I(7, -5));
        n.IsBridge = true;

        // CON <-> WIS 深层
        n = Ms("trans_cw01", "生命之力", 6, ["con_b08", "wis_b08"], D(("max_hp", 8), ("heal_amount", 1)), new Vector2I(-4, 2));
        n.IsBridge = true;

        // INT <-> CHA 深层
        n = Ms("trans_ic01", "奥术外交", 7, ["int_b11", "cha_b11"], D(("mana_max", 3), ("cha_check", 1)), new Vector2I(-3, -4));
        n.IsBridge = true;

        // DEX <-> CON 深层
        n = Ms("trans_dc01", "战场机动", 6, ["dex_b11", "con_b08"], D(("ac", 1), ("initiative", 2), ("max_hp", 3)), new Vector2I(-2, 5));
        n.IsBridge = true;

        // CON <-> INT 对角
        n = Ms("trans_ci01", "战斗法师", 5, ["con_b02", "int_b08"], D(("spell_damage", 1), ("max_hp", 3)), new Vector2I(-3, 2));
        n.IsBridge = true;

        // DEX <-> WIS 对角
        n = Ms("trans_dw01", "野性直觉", 5, ["dex_b02", "wis_b02"], D(("initiative", 2), ("wis_check", 1)), new Vector2I(1, -3));
        n.IsBridge = true;
    }

    // ========================================================================
    // 跨区域环路连接
    // ========================================================================

    void BuildCrossRegionLoops()
    {
        // === 内层过渡 (Ring 2) — 每个过渡节点只连接两侧区域的 Ring 1-2 节点 ===
        Ac("trans_sd01", "str_s01");
        Ac("trans_sd01", "dex_s01");
        Ac("trans_sd01", "str_s02");
        Ac("trans_sd01", "dex_s02");

        Ac("trans_sc01", "str_s01");
        Ac("trans_sc01", "con_s01");
        Ac("trans_sc01", "str_s10");
        Ac("trans_sc01", "con_s02");

        Ac("trans_di01", "dex_s01");
        Ac("trans_di01", "int_s01");
        Ac("trans_di01", "dex_s14");
        Ac("trans_di01", "int_s15");

        Ac("trans_iw01", "int_s01");
        Ac("trans_iw01", "wis_s01");
        Ac("trans_iw01", "int_s02");
        Ac("trans_iw01", "wis_s02");

        Ac("trans_wc01", "wis_s01");
        Ac("trans_wc01", "cha_s01");
        Ac("trans_wc01", "wis_s09");
        Ac("trans_wc01", "cha_s02");

        Ac("trans_cc01", "cha_s01");
        Ac("trans_cc01", "con_s01");
        Ac("trans_cc01", "cha_s09");
        Ac("trans_cc01", "con_s08");

        // === 中层过渡 (Ring 4) — 连接两侧区域的 Ring 3 大节点 ===
        Ac("trans_sd02", "str_b01");
        Ac("trans_sd02", "dex_b01");
        Ac("trans_sd02", "str_b02");
        Ac("trans_sd02", "dex_b02");

        Ac("trans_sc02", "str_b01");
        Ac("trans_sc02", "con_b01");
        Ac("trans_sc02", "str_b04");
        Ac("trans_sc02", "con_b02");

        Ac("trans_di02", "dex_b01");
        Ac("trans_di02", "int_b01");
        Ac("trans_di02", "dex_b05");

        Ac("trans_iw02", "int_b01");
        Ac("trans_iw02", "wis_b01");
        Ac("trans_iw02", "int_b02");
        Ac("trans_iw02", "wis_b04");

        Ac("trans_wc02", "wis_b01");
        Ac("trans_wc02", "cha_b01");
        Ac("trans_wc02", "wis_b02");
        Ac("trans_wc02", "cha_b02");

        Ac("trans_cc02", "cha_b01");
        Ac("trans_cc02", "con_b01");
        Ac("trans_cc02", "cha_b04");
        Ac("trans_cc02", "con_b04");

        // === 深层过渡 (Ring 6-7) — 连接高级大节点 ===
        Ac("trans_sd03", "str_b08");
        Ac("trans_sd03", "dex_b13");

        Ac("trans_sc03", "str_b07");
        Ac("trans_sc03", "cha_b10");
        Ac("trans_sc03", "cha_b12");

        Ac("trans_cw01", "con_b08");
        Ac("trans_cw01", "wis_b08");
        Ac("trans_cw01", "wis_b09");

        Ac("trans_ic01", "int_b11");
        Ac("trans_ic01", "cha_b11");

        Ac("trans_dc01", "dex_b11");
        Ac("trans_dc01", "con_b08");

        Ac("trans_ci01", "con_b02");
        Ac("trans_ci01", "int_b08");

        Ac("trans_dw01", "dex_b02");
        Ac("trans_dw01", "wis_b02");

        // STR 内部环路
        Ac("str_s21", "str_s19");
    }

    // ========================================================================
    // 查询方法
    // ========================================================================

    public SkillNodeData? GetStartNode() =>
        Nodes.GetValueOrDefault(StartNodeId);

    public List<SkillNodeData> GetAllNodes() =>
        Nodes.Values.ToList();

    public List<SkillNodeData> GetNodesByRegion(SkillNodeData.Region reg) =>
        Nodes.Values.Where(n => n.CurrentRegion == reg).ToList();

    public List<SkillNodeData> GetBigNodes() =>
        Nodes.Values.Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Big
                             || n.CurrentNodeType == SkillNodeData.NodeType.Keystone).ToList();

    public List<SkillNodeData> GetKeystones() =>
        Nodes.Values.Where(n => n.CurrentNodeType == SkillNodeData.NodeType.Keystone).ToList();
}
