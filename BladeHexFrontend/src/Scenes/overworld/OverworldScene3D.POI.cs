// OverworldScene3D.POI.cs
// POI 渲染 + 接近检测 — 此文件仅保留 partial 代理，实际实现在 Components/POIController.cs。
//
// 重构于 Sprint 6（架构优化 spec R5）。
// 注意：_poiEntered / TriggerPOIInteraction 仍由主类处理（Interaction partial 引用）。
using Godot;
using BladeHex.Strategic;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    private POIController? _poiController;

    /// <summary>主场景 Interaction partial 引用 — 是否已进入 POI</summary>
    private bool _poiEntered = false;

    /// <summary>渲染所有世界 POI</summary>
    private void RenderWorldPOIs()
    {
        _poiController = new POIController { Name = "POIController" };
        AddChild(_poiController);
        // 传 _chunkManager — chunk 模式下 PoiId 写在 chunk 的 tile 上而非 _grid，必须用 chunk 才能命中
        _poiController.Initialize(WorldPois, _fog, _grid, _chunkManager, this);
        _poiController.PlayerEnteredPoi += OnPlayerEnteredPoi;
        _poiController.RenderAll(_playerPixelPos);
    }

    /// <summary>检测玩家是否进入 POI 范围（每帧调用）</summary>
    private void CheckPOIEnter()
    {
        _poiController?.CheckEnter(_poiEntered, _playerMoving, _playerPixelPos);
    }

    /// <summary>POI controller 回调 — 进入 POI → 暂停玩家 + 触发交互</summary>
    private void OnPlayerEnteredPoi(OverworldPOI poi)
    {
        _poiEntered = true;
        _playerMoving = false;
        TriggerPOIInteraction(poi);
    }

    /// <summary>初始化交互系统（保留为顶层入口，实际实现在 Interaction partial）</summary>
    private void InitInteraction()
    {
        SetupInteractionSystem();
    }
}
