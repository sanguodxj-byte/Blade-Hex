// FootprintTemplate.cs
// 比例尺统一 — POI footprint 模板与选址算法
//
// 见 .kiro/specs/scale-unification/design.md
//
// 用法：
//   var (rotation, occupiedHexes) = FootprintTemplateRegistry
//       .Get("port_city_4")
//       .TryFit(centerHex, grid, isHexFree);
//
// 旋转匹配：枚举 6 个 axial 旋转方向，返回第一个满足约束的方向。

using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>POI footprint 模板 — 声明式描述 POI 占用形状与地形约束</summary>
public sealed class FootprintTemplate
{
    /// <summary>模板名（如 "solo" / "village_3" / "port_city_4"）</summary>
    public string Name { get; }

    /// <summary>模板内所有 cell（含中心）</summary>
    public IReadOnlyList<FootprintCell> Cells { get; }

    /// <summary>必须满足约束的最小 cell 数（默认 = Cells.Length）</summary>
    public int RequiredCellCount { get; }

    public FootprintTemplate(string name, FootprintCell[] cells, int? requiredCellCount = null)
    {
        Name = name;
        Cells = cells;
        RequiredCellCount = requiredCellCount ?? cells.Length;
    }

    /// <summary>
    /// 枚举 6 个 hex 旋转方向，找第一个所有 cell 约束都满足的方向。
    /// 返回 (旋转方向 0~5, 实际占用的 axial 坐标数组) 或 null（找不到）。
    /// </summary>
    /// <param name="center">中心 hex axial 坐标</param>
    /// <param name="grid">大地图 grid</param>
    /// <param name="isHexFree">额外检查 hex 是否未被占用（如其他 POI footprint）</param>
    public (int Rotation, Vector2I[] OccupiedHexes)? TryFit(
        Vector2I center,
        HexOverworldGrid grid,
        Func<Vector2I, bool>? isHexFree = null)
        => TryFit(center, hex => grid.GetTileAtCoord(hex), isHexFree);

    /// <summary>
    /// 通用版 TryFit — 接受任意 tile 查询函数（适配 chunk 或 grid）。
    /// </summary>
    public (int Rotation, Vector2I[] OccupiedHexes)? TryFit(
        Vector2I center,
        Func<Vector2I, HexOverworldTile?> getTile,
        Func<Vector2I, bool>? isHexFree = null)
    {
        var single = Cells.Count == 1;
        int maxRotations = single ? 1 : 6;

        for (int rot = 0; rot < maxRotations; rot++)
        {
            var occupied = new Vector2I[Cells.Count];
            bool ok = true;
            for (int i = 0; i < Cells.Count; i++)
            {
                var rotated = RotateOffset(Cells[i].Offset, rot);
                var hex = new Vector2I(center.X + rotated.X, center.Y + rotated.Y);
                occupied[i] = hex;

                var tile = getTile(hex);
                if (tile == null) { ok = false; break; }

                if (!CheckRole(tile, Cells[i].Role)) { ok = false; break; }

                if (isHexFree != null && !isHexFree(hex)) { ok = false; break; }
            }

            if (ok) return (rot, occupied);
        }
        return null;
    }

    /// <summary>验证 tile 是否满足 role 约束</summary>
    public static bool CheckRole(HexOverworldTile tile, FootprintCellRole role)
    {
        var t = tile.Terrain;
        switch (role)
        {
            case FootprintCellRole.Any:
                // 任何可建造陆地：排除水域 + 山顶 + 沼泽（无法建造）
                return t != HexOverworldTile.TerrainType.DeepWater
                    && t != HexOverworldTile.TerrainType.ShallowWater
                    && t != HexOverworldTile.TerrainType.River
                    && t != HexOverworldTile.TerrainType.Mountain
                    && t != HexOverworldTile.TerrainType.Swamp;

            case FootprintCellRole.CoastalDock:
                return t == HexOverworldTile.TerrainType.ShallowWater
                    || (t == HexOverworldTile.TerrainType.DeepWater && true); // 邻接陆地检查在 caller 做

            case FootprintCellRole.RiverDock:
                return tile.IsRiver || t == HexOverworldTile.TerrainType.River;

            case FootprintCellRole.MountainSlope:
                return t == HexOverworldTile.TerrainType.Hills
                    || t == HexOverworldTile.TerrainType.Mountain;

            case FootprintCellRole.ForestEdge:
                return t == HexOverworldTile.TerrainType.Forest
                    || t == HexOverworldTile.TerrainType.DenseForest;

            default:
                return true;
        }
    }

    /// <summary>
    /// 把 axial offset 绕 (0,0) 顺时针旋转 60° × steps。
    /// 在 axial 系下：(q, r) -> (-r, q+r) 是顺时针 60°。
    /// </summary>
    public static Vector2I RotateOffset(Vector2I offset, int steps)
    {
        steps = ((steps % 6) + 6) % 6;
        int q = offset.X, r = offset.Y;
        for (int i = 0; i < steps; i++)
        {
            int newQ = -r;
            int newR = q + r;
            q = newQ; r = newR;
        }
        return new Vector2I(q, r);
    }
}

/// <summary>FootprintTemplate 注册表 — 声明所有可用模板</summary>
public static class FootprintTemplateRegistry
{
    static readonly Dictionary<string, FootprintTemplate> _templates = new();

    static FootprintTemplateRegistry()
    {
        RegisterBuiltins();
    }

    public static FootprintTemplate Get(string name)
    {
        if (_templates.TryGetValue(name, out var tpl)) return tpl;
        GD.PushWarning($"[FootprintTemplateRegistry] 未注册模板 '{name}'，回退到 solo");
        return _templates["solo"];
    }

    public static bool Has(string name) => _templates.ContainsKey(name);

    public static void Register(FootprintTemplate template) => _templates[template.Name] = template;

    public static IReadOnlyDictionary<string, FootprintTemplate> All => _templates;

    static void RegisterBuiltins()
    {
        // ========================================
        // Tiny — 单格
        // ========================================
        Register(new FootprintTemplate("solo", [
            new(new Vector2I(0, 0)),
        ]));

        // ========================================
        // Small (3 hex)
        // ========================================

        // 普通村庄：1 中心 + 2 邻居（无地形约束）
        Register(new FootprintTemplate("village_3", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(0, 1)),
        ]));

        // 林中据点：1 中心 + 2 林缘格
        Register(new FootprintTemplate("forest_camp_3", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0), FootprintCellRole.ForestEdge),
            new(new Vector2I(0, 1), FootprintCellRole.ForestEdge),
        ]));

        // 沼泽据点：1 中心 + 2 邻居（无 role 约束，preset 决定是否落沼泽）
        Register(new FootprintTemplate("swamp_camp_3", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(-1, 1)),
        ]));

        // 山间矿坑：1 中心 + 2 山坡
        Register(new FootprintTemplate("mountain_dig_3", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, -1), FootprintCellRole.MountainSlope),
            new(new Vector2I(0, -1), FootprintCellRole.MountainSlope),
        ]));

        // 海岸据点：2 陆 + 1 海
        Register(new FootprintTemplate("coastal_3", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(-1, 1), FootprintCellRole.CoastalDock, "dock"),
        ]));

        // 遗迹：3 普通 cell
        Register(new FootprintTemplate("ruins_3", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(-1, 0)),
        ]));

        // ========================================
        // Medium (5 hex)
        // ========================================

        // 城镇：1 中心 + 4 周围
        Register(new FootprintTemplate("town_5", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(0, 1)),
            new(new Vector2I(-1, 0)),
            new(new Vector2I(1, -1)),
        ]));

        // 港口城市：4 陆 + 1 海（带码头）
        Register(new FootprintTemplate("port_city_4", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(0, 1)),
            new(new Vector2I(-1, 1), FootprintCellRole.CoastalDock, "dock"),
        ]));

        // 平原据点：5 普通 cell
        Register(new FootprintTemplate("plains_5", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(0, 1)),
            new(new Vector2I(-1, 1)),
            new(new Vector2I(1, -1)),
        ]));

        // 大型遗迹：5 cell
        Register(new FootprintTemplate("ruins_5", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(-1, 0)),
            new(new Vector2I(0, 1)),
            new(new Vector2I(0, -1)),
        ]));

        // 沼泽神殿：5 cell
        Register(new FootprintTemplate("swamp_temple_5", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(0, 1)),
            new(new Vector2I(-1, 1)),
            new(new Vector2I(0, -1)),
        ]));

        // 山城：1 山脚 + 4 山坡
        Register(new FootprintTemplate("mountain_castle_5", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, -1), FootprintCellRole.MountainSlope),
            new(new Vector2I(0, -1), FootprintCellRole.MountainSlope),
            new(new Vector2I(-1, 0), FootprintCellRole.MountainSlope),
            new(new Vector2I(1, 0)),
        ]));

        // 山间巢穴：1 中心 + 4 山坡
        Register(new FootprintTemplate("mountain_lair_5", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, -1), FootprintCellRole.MountainSlope),
            new(new Vector2I(0, -1), FootprintCellRole.MountainSlope),
            new(new Vector2I(-1, 0), FootprintCellRole.MountainSlope),
            new(new Vector2I(0, 1), FootprintCellRole.MountainSlope),
        ]));

        // ========================================
        // Large (7 hex) — 完整 ring1
        // ========================================

        // 大型据点：1 中心 + 6 邻居（完整 ring1）
        Register(new FootprintTemplate("fortress_7", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(1, -1)),
            new(new Vector2I(0, -1)),
            new(new Vector2I(-1, 0)),
            new(new Vector2I(-1, 1)),
            new(new Vector2I(0, 1)),
        ]));

        // 大型港口城市：6 陆 + 1 海
        Register(new FootprintTemplate("port_city_7", [
            new(new Vector2I(0, 0)),
            new(new Vector2I(1, 0)),
            new(new Vector2I(1, -1)),
            new(new Vector2I(0, -1)),
            new(new Vector2I(-1, 0)),
            new(new Vector2I(0, 1)),
            new(new Vector2I(-1, 1), FootprintCellRole.CoastalDock, "dock"),
        ]));
    }
}
