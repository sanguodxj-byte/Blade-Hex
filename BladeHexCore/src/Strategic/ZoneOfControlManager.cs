// ZoneOfControlManager.cs
// 敌对阵营 POI 控制区管理器 — 预计算 ZoC 范围，运行时 O(1) 查询
// 控制区半径: Castle=4, Town=3, Village=2, Settlement=2, Lair=1
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 敌对 POI 控制区管理器。
/// 每个敌对 POI 周围有一个六边形控制区（Zone of Control），
/// 玩家部队进入时受到移速惩罚，寻路系统会倾向于绕行。
/// </summary>
[GlobalClass]
public partial class ZoneOfControlManager : RefCounted
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>ZoC 移速惩罚乘数（0.7 = 降低 30%）</summary>
    public const float ZocPenalty = 0.7f;

    /// <summary>技能减轻后的 ZoC 惩罚（0.85 = 仅降低 15%）</summary>
    public const float ZocPenaltyReduced = 0.85f;

    /// <summary>ZoC 对寻路代价的乘数（1.0 / ZocPenalty ≈ 1.43）</summary>
    public static float ZocPathfindingMultiplier => 1.0f / ZocPenalty;

    // ========================================
    // 数据
    // ========================================

    /// <summary>POI ID → ZoC tile 集合</summary>
    private readonly Dictionary<string, HashSet<Vector2I>> _poiZocTiles = new();

    /// <summary>POI ID → OwningFaction</summary>
    private readonly Dictionary<string, string> _poiFactions = new();

    /// <summary>全局 ZoC 查找: tile 坐标 → 拥有该 tile 的 POI ID 列表</summary>
    private readonly Dictionary<Vector2I, List<string>> _tileToPoiMap = new();

    /// <summary>所有 POI 引用（用于动态更新）</summary>
    private List<OverworldPOI>? _allPois;

    // ========================================
    // 信号
    // ========================================

    [Signal]
    public delegate void EnteredZocEventHandler(string poiId, float penalty);

    [Signal]
    public delegate void LeftZocEventHandler(string poiId);

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 初始化所有 POI 的控制区。
    /// 在世界加载完成后调用一次。
    /// </summary>
    public void Initialize(List<OverworldPOI> pois)
    {
        _allPois = pois;
        _poiZocTiles.Clear();
        _poiFactions.Clear();
        _tileToPoiMap.Clear();

        foreach (var poi in pois)
        {
            if (string.IsNullOrEmpty(poi.OwningFaction) || poi.OwningFaction == "neutral")
                continue;

            ComputeZocForPoi(poi);
        }

        GD.Print($"[ZoC] 初始化完成: {_poiZocTiles.Count} 个 POI 有控制区, {_tileToPoiMap.Count} 个 tile 受控");
    }

    // ========================================
    // 查询接口
    // ========================================

    /// <summary>
    /// 判断指定 tile 是否处于任意敌对 ZoC 内。
    /// O(1) 哈希查找。
    /// </summary>
    public bool IsInHostileZoc(int q, int r, string playerFaction)
    {
        var coord = new Vector2I(q, r);
        if (!_tileToPoiMap.TryGetValue(coord, out var poiIds))
            return false;

        foreach (var poiId in poiIds)
        {
            if (_poiFactions.TryGetValue(poiId, out var faction) &&
                faction != playerFaction &&
                faction != "neutral" &&
                !string.IsNullOrEmpty(faction))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>获取指定 POI 的 ZoC tile 集合</summary>
    public HashSet<Vector2I> GetZocTiles(string poiId)
    {
        return _poiZocTiles.TryGetValue(poiId, out var tiles) ? tiles : new HashSet<Vector2I>();
    }

    /// <summary>获取 POI 类型对应的控制区半径</summary>
    public static int GetControlRadius(OverworldPOI.POIType type)
    {
        return type switch
        {
            OverworldPOI.POIType.Castle => 4,
            OverworldPOI.POIType.Town => 3,
            OverworldPOI.POIType.Village => 2,
            OverworldPOI.POIType.Settlement => 2,
            OverworldPOI.POIType.Lair => 1,
            _ => 0, // Mine/Farm 没有控制区
        };
    }

    /// <summary>
    /// 获取当前 ZoC 惩罚值（考虑技能减轻）。
    /// </summary>
    public static float GetEffectivePenalty(bool hasZocResistance)
    {
        return hasZocResistance ? ZocPenaltyReduced : ZocPenalty;
    }

    // ========================================
    // 动态更新
    // ========================================

    /// <summary>POI 阵营变更时更新 ZoC</summary>
    public void OnPoiFactionChanged(OverworldPOI poi, string oldFaction)
    {
        string poiId = poi.PoiName;

        // 移除旧 ZoC
        if (_poiZocTiles.TryGetValue(poiId, out var oldTiles))
        {
            foreach (var tile in oldTiles)
            {
                if (_tileToPoiMap.TryGetValue(tile, out var list))
                {
                    list.Remove(poiId);
                    if (list.Count == 0) _tileToPoiMap.Remove(tile);
                }
            }
            _poiZocTiles.Remove(poiId);
            _poiFactions.Remove(poiId);
        }

        // 如果新阵营有效，重新计算 ZoC
        if (!string.IsNullOrEmpty(poi.OwningFaction) && poi.OwningFaction != "neutral")
        {
            ComputeZocForPoi(poi);
        }
    }

    /// <summary>POI 被摧毁/清除时移除 ZoC</summary>
    public void OnPoiDestroyed(OverworldPOI poi)
    {
        string poiId = poi.PoiName;

        if (_poiZocTiles.TryGetValue(poiId, out var tiles))
        {
            foreach (var tile in tiles)
            {
                if (_tileToPoiMap.TryGetValue(tile, out var list))
                {
                    list.Remove(poiId);
                    if (list.Count == 0) _tileToPoiMap.Remove(tile);
                }
            }
            _poiZocTiles.Remove(poiId);
            _poiFactions.Remove(poiId);
        }
    }

    /// <summary>获取需要更新 PathfindingCostGrid 的 tile 集合（供外部调用）</summary>
    public HashSet<Vector2I> GetAllHostileZocTiles(string playerFaction)
    {
        var result = new HashSet<Vector2I>();
        foreach (var (poiId, tiles) in _poiZocTiles)
        {
            if (_poiFactions.TryGetValue(poiId, out var faction) &&
                faction != playerFaction &&
                faction != "neutral" &&
                !string.IsNullOrEmpty(faction))
            {
                result.UnionWith(tiles);
            }
        }
        return result;
    }

    // ========================================
    // 内部方法
    // ========================================

    /// <summary>为单个 POI 计算 ZoC tile 集合</summary>
    private void ComputeZocForPoi(OverworldPOI poi)
    {
        string poiId = poi.PoiName;
        int radius = GetControlRadius(poi.PoiTypeEnum);

        // 将 POI 像素位置转换为 axial 坐标
        var center = HexOverworldTile.PixelToAxial(poi.Position.X, poi.Position.Y);
        var centerCube = HexOverworldTile.AxialToCube(center.X, center.Y);

        var tiles = new HashSet<Vector2I>();

        // 中心 tile
        tiles.Add(center);

        // 六边形环 1..radius
        for (int ring = 1; ring <= radius; ring++)
        {
            var ringTiles = HexOverworldTile.CubeRing(centerCube, ring);
            foreach (var cube in ringTiles)
            {
                var axial = HexOverworldTile.CubeToAxial(cube);
                tiles.Add(axial);
            }
        }

        _poiZocTiles[poiId] = tiles;
        _poiFactions[poiId] = poi.OwningFaction;

        // 更新全局查找表
        foreach (var tile in tiles)
        {
            if (!_tileToPoiMap.TryGetValue(tile, out var list))
            {
                list = new List<string>(2);
                _tileToPoiMap[tile] = list;
            }
            if (!list.Contains(poiId))
                list.Add(poiId);
        }
    }
}
