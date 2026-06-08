// POIController.cs
// POI 接近检测控制器 — 纯逻辑，无渲染依赖。
// 检测玩家移动时是否进入 POI 范围（hex footprint 命中）。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.View.Map;

namespace BladeHex.Scenes.Overworld.Components;

[GlobalClass]
public partial class POIController : Node
{
    // ========================================
    // 引用
    // ========================================

    private List<OverworldPOI>? _worldPois;
    private FogOfWar? _fog;
    private OverworldMapAccess? _mapAccess;

    // ========================================
    // 状态
    // ========================================

    private OverworldPOI? _lastInteractedPoi;
    private bool _hasLeftLastPoi = true;

    /// <summary>玩家当前锁定的目标 POI，只有移动接触到它才触发交互</summary>
    public OverworldPOI? TargetPOI { get; set; }

    // ========================================
    // 事件
    // ========================================

    /// <summary>玩家进入某个 POI 范围</summary>
    public event Action<OverworldPOI>? PlayerEnteredPoi;

    // ========================================
    // 入口
    // ========================================

    /// <summary>初始化 POI 检测器</summary>
    public void Initialize(List<OverworldPOI> worldPois, FogOfWar? fog, HexOverworldGrid grid, ChunkManager? chunkManager, Node? _unused = null)
    {
        _worldPois = worldPois;
        _fog = fog;
        _mapAccess = new OverworldMapAccess(chunkManager, grid);
    }

    public void Initialize(List<OverworldPOI> worldPois, FogOfWar? fog, OverworldMapAccess mapAccess)
    {
        _worldPois = worldPois;
        _fog = fog;
        _mapAccess = mapAccess;
    }

    // ========================================
    // 检测
    // ========================================

    /// <summary>检测玩家是否进入 POI 范围（纯 hex 命中：玩家所在 hex 是否在某 POI footprint 内）</summary>
    public void CheckEnter(bool alreadyInPoi, bool playerMoving, Vector2 playerPixelPos)
    {
        if (_worldPois == null || alreadyInPoi || !playerMoving || _worldPois.Count == 0) return;
        if (_mapAccess == null) return;

        HexOverworldTile? tile = _mapAccess.GetActiveTileAtPixel(playerPixelPos);
        if (tile == null) return;

        // 已经触发过交互且仍在同一个 POI footprint 内 → 等待离开
        if (_lastInteractedPoi != null && !_hasLeftLastPoi)
        {
            if (_lastInteractedPoi.ContainsHex(tile.Coord))
                return;
            _hasLeftLastPoi = true;
            _lastInteractedPoi = null;
        }

        // 查 footprint 命中
        if (string.IsNullOrEmpty(tile.PoiId)) return;

        foreach (var poi in _worldPois)
        {
            if (poi.PoiName != tile.PoiId) continue;

            // 只有踩中的 POI 就是玩家的目标 POI 时，才触发进入
            if (poi != TargetPOI) continue;

            _lastInteractedPoi = poi;
            _hasLeftLastPoi = false;
            GD.Print($"[POIController] 进入 POI: {poi.PoiName} (Scale={poi.Scale}, footprint={poi.FootprintTemplateName})");
            PlayerEnteredPoi?.Invoke(poi);
            return;
        }
    }
}
