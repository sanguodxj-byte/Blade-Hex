// OverworldScene2D.POI.cs
// POI 渲染 + 接近检测 — 从 OverworldScene3D.POI.cs 迁移
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Scenes.Overworld.Components;
using BladeHex.Scenes.Overworld2d.Components;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    private POIController? _poiController;   // 保留：CheckEnter 逻辑
    private POIRenderer2D? _poiRenderer;     // 新增：2D 渲染

    /// <summary>渲染所有世界 POI</summary>
    private void RenderWorldPOIs()
    {
        // 2D 渲染器（Sprite2D + Label + Polygon2D 脚印覆盖层）
        _poiRenderer = new POIRenderer2D { Name = "POIRenderer2D" };
        AddChild(_poiRenderer);
        _poiRenderer.Initialize(WorldPois, _fog);
        _poiRenderer.RenderAll();

        // 保留 POIController 用于 CheckEnter（纯 hex 命中逻辑，不需要 markerParent）
        _poiController = new POIController { Name = "POIController" };
        AddChild(_poiController);
        _poiController.Initialize(WorldPois, _fog, _mapAccess);
        _poiController.PlayerEnteredPoi += OnPlayerEnteredPoi;
    }

    /// <summary>检测玩家是否进入 POI 范围（每帧调用）</summary>
    private void CheckPOIEnter()
    {
        if (_poiController != null)
        {
            _poiController.TargetPOI = _targetPoi;
            _poiController.CheckEnter(_poiEntered, _playerMoving, _playerPixelPos);
        }
    }

    /// <summary>POI controller 回调 — 进入 POI → 暂停玩家 + 触发交互</summary>
    private void OnPlayerEnteredPoi(OverworldPOI poi)
    {
        _poiEntered = true;
        ClearDirectedInteraction();
        _playerMoving = false;
        IsWaiting = false;
        TriggerPOIInteraction(poi);
    }
}
