// OverworldScene3D.POI.cs
// POI 渲染 + 交互检测
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // POI 系统
    // ========================================

    private readonly List<Node3D> _poiMeshes = new();
    private const float POI_ENTER_DIST = 450.0f;
    private const float POI_LEAVE_DIST = 600.0f; // 必须离开到这个距离才能再次交互
    private bool _poiEntered = false;
    private OverworldPOI? _lastInteractedPoi = null; // 上次交互的 POI
    private bool _hasLeftLastPoi = true; // 是否已离开上次交互的 POI

    /// <summary>渲染所有世界 POI</summary>
    private void RenderWorldPOIs()
    {
        if (WorldPois.Count == 0)
        {
            // 无 POI 时放测试标记
            SetupTestPOIs();
            return;
        }

        foreach (var poi in WorldPois)
        {
            // 只渲染已揭示的 POI
            if (_fog != null && !_fog.IsRevealed(poi.Position.X, poi.Position.Y))
                continue;

            var worldPos = CoordConverter.PixelToWorld3D(poi.Position);

            Color color;
            float size;
            if (poi.PoiTypeEnum == OverworldPOI.POIType.Town)
            { color = new Color(0.9f, 0.85f, 0.3f); size = 0.6f; }
            else if (poi.PoiTypeEnum == OverworldPOI.POIType.Village)
            { color = new Color(0.7f, 0.6f, 0.3f); size = 0.4f; }
            else if (poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
            { color = new Color(0.5f, 0.5f, 0.7f); size = 0.7f; }
            else
            { color = new Color(0.6f, 0.4f, 0.2f); size = 0.35f; }

            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(size, size * 1.5f, size) };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mesh.MaterialOverride = mat;
            mesh.Position = worldPos + new Vector3(0, size * 0.75f, 0);
            AddChild(mesh);

            // 名称标签 — 大字体悬浮在图标头上
            var label = new Label3D();
            label.Text = poi.PoiName;
            label.FontSize = 96;
            label.PixelSize = 0.01f;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
            label.Position = worldPos + new Vector3(0, size * 1.5f + 1.2f, 0);
            label.Modulate = new Color(1.0f, 0.95f, 0.85f);
            label.OutlineModulate = new Color(0.05f, 0.05f, 0.05f);
            label.OutlineSize = 16;
            label.NoDepthTest = true;
            label.RenderPriority = 100;
            AddChild(label);

            _poiMeshes.Add(mesh);
        }

        GD.Print($"[OverworldScene3D] 渲染 {_poiMeshes.Count} 个 POI");
    }

    /// <summary>测试用 POI（无 WorldCreator 时）</summary>
    private void SetupTestPOIs()
    {
        var testOffsets = new Vector2[] {
            new(500, 0), new(-500, 300), new(300, -500),
            new(-400, -400), new(600, 400),
        };

        foreach (var offset in testOffsets)
        {
            var poiPixel = _playerPixelPos + offset;
            var tile = _grid.GetTileAtPixel(poiPixel.X, poiPixel.Y);
            if (tile == null || !tile.IsPassable) continue;

            var worldPos = CoordConverter.PixelToWorld3D(tile.PixelPos);
            var poiMesh = new MeshInstance3D();
            poiMesh.Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.8f, 0.4f) };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.9f, 0.8f, 0.1f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            poiMesh.MaterialOverride = mat;
            poiMesh.Position = worldPos + new Vector3(0, 0.5f, 0);
            AddChild(poiMesh);
            _poiMeshes.Add(poiMesh);
        }
    }

    /// <summary>初始化交互系统</summary>
    private void InitInteraction()
    {
        SetupInteractionSystem();
    }

    /// <summary>检测玩家是否进入 POI 范围（仅在移动时检测 + 冷却）</summary>
    private void CheckPOIEnter()
    {
        if (_poiEntered || !_playerMoving || WorldPois.Count == 0) return;

        // 检查是否已离开上次交互的 POI
        if (_lastInteractedPoi != null && !_hasLeftLastPoi)
        {
            float distToLast = _playerPixelPos.DistanceTo(_lastInteractedPoi.Position);
            if (distToLast > POI_LEAVE_DIST)
            {
                _hasLeftLastPoi = true;
                _lastInteractedPoi = null;
            }
            else
            {
                return; // 还没离开上次的 POI，不触发新交互
            }
        }

        foreach (var poi in WorldPois)
        {
            float dist = _playerPixelPos.DistanceTo(poi.Position);
            if (dist < POI_ENTER_DIST)
            {
                _poiEntered = true;
                _playerMoving = false;
                _lastInteractedPoi = poi;
                _hasLeftLastPoi = false;
                GD.Print($"[OverworldScene3D] 进入 POI: {poi.PoiName}");
                TriggerPOIInteraction(poi);
                return;
            }
        }
    }
}
