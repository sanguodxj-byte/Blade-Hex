// POIController.cs
// 大地图 POI 渲染 + 接近检测控制器。
//
// 职责：
//   - 为每个已揭示的 POI 创建 3D 标记（mesh + Label3D）
//   - 检测玩家移动时是否进入 POI 范围；触发 PlayerEnteredPoi 事件
//   - 维护"已离开上次交互 POI"的冷却状态
//
// 注意：POI 交互的实际处理（打开面板等）保留在主场景 Interaction partial 中，
//       本组件只负责检测和事件分发。
//
// 服务于架构优化 spec R5 — Sprint 6 场景控制器组件化。
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
    // 配置
    // ========================================

    // 已废弃：互动判定改为纯 hex 命中（footprint 内任一 hex 触发）
    // 保留这两个常量为 0，确保外部引用不报错（编译期暴露使用点）
    private const float POI_ENTER_DIST = 0f;
    private const float POI_LEAVE_DIST = 0f;

    // ========================================
    // 引用
    // ========================================

    private List<OverworldPOI>? _worldPois;
    private FogOfWar? _fog;
    private HexOverworldGrid? _grid;
    private ChunkManager? _chunkManager; // chunk 模式下 PoiId 写在 chunk 的 tile 上而非 _grid
    private Node3D? _markerParent;

    // ========================================
    // 状态
    // ========================================

    private readonly List<Node3D> _poiMeshes = new();
    private POIHexOverlayRenderer? _overlayRenderer;
    private OverworldPOI? _lastInteractedPoi;
    private bool _hasLeftLastPoi = true;

    // ========================================
    // 事件
    // ========================================

    /// <summary>玩家进入某个 POI 范围 → 由场景决定如何响应（通常是 TriggerPOIInteraction）</summary>
    public event Action<OverworldPOI>? PlayerEnteredPoi;

    // ========================================
    // 入口
    // ========================================

    /// <summary>由 OverworldScene3D 在 _Ready 阶段调用</summary>
    public void Initialize(List<OverworldPOI> worldPois, FogOfWar? fog, HexOverworldGrid grid, ChunkManager? chunkManager, Node3D markerParent)
    {
        _worldPois = worldPois;
        _fog = fog;
        _grid = grid;
        _chunkManager = chunkManager;
        _markerParent = markerParent;
    }

    /// <summary>渲染所有已揭示 POI 的 3D 标记</summary>
    public void RenderAll(Vector2 playerPixelPos)
    {
        if (_worldPois == null || _markerParent == null) return;

        if (_worldPois.Count == 0)
        {
            // 无 POI 时放测试标记
            SetupTestPOIs(playerPixelPos);
            return;
        }

        // 初始化 hex 覆盖层渲染器
        _overlayRenderer = new POIHexOverlayRenderer();
        _markerParent.AddChild(_overlayRenderer);
        _overlayRenderer.Initialize();

        foreach (var poi in _worldPois)
        {
            // 只渲染已揭示的 POI
            if (_fog != null && !_fog.IsRevealed(poi.Position.X, poi.Position.Y))
                continue;

            var worldPos = CoordConverter.PixelToWorld3D(poi.Position);
            float poiGroundY = GetPOIGroundY(poi);

            // --- hex 覆盖层：用 POI 纹理填满所有占用 hex ---
            _overlayRenderer.AddPOI(poi);

            // --- 中心标记（缩小的图标，保留辨识度）---
            Color color = poi.PoiTypeEnum switch
            {
                OverworldPOI.POIType.Town       => new Color(0.9f, 0.85f, 0.3f),
                OverworldPOI.POIType.Village    => new Color(0.7f, 0.6f, 0.3f),
                OverworldPOI.POIType.Castle     => new Color(0.5f, 0.5f, 0.7f),
                OverworldPOI.POIType.Port       => new Color(0.4f, 0.55f, 0.7f),
                OverworldPOI.POIType.Shrine     => new Color(0.7f, 0.5f, 0.7f),
                _                               => new Color(0.6f, 0.4f, 0.2f),
            };
            float size = BladeHex.Strategic.POIScaleTable.Get(poi.Scale).MarkerSize;

            // 中心标记缩小为原来的 60%（hex 覆盖层已提供占位感）
            float markerSize = size * 0.6f;
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(markerSize, markerSize * 1.5f, markerSize) },
                Position = worldPos + new Vector3(0, poiGroundY + markerSize * 0.75f + 0.03f, 0),
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            mesh.MaterialOverride = mat;
            _markerParent.AddChild(mesh);

            // 名称标签 — 大字体悬浮在图标头上
            var label = new Label3D
            {
                Text = poi.PoiName,
                FontSize = 96,
                PixelSize = 0.01f,
                Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
                Position = worldPos + new Vector3(0, poiGroundY + markerSize * 1.5f + 1.2f, 0),
                Modulate = new Color(1.0f, 0.95f, 0.85f),
                OutlineModulate = new Color(0.05f, 0.05f, 0.05f),
                OutlineSize = 16,
                NoDepthTest = true,
                RenderPriority = 100,
            };
            _markerParent.AddChild(label);

            _poiMeshes.Add(mesh);
        }

        // 所有 POI 添加完毕，重建覆盖层 MultiMesh
        _overlayRenderer.RebuildAll();

        GD.Print($"[POIController] 渲染 {_poiMeshes.Count} 个 POI（含 hex 覆盖层）");
    }

    /// <summary>检测玩家是否进入 POI 范围（纯 hex 命中：玩家所在 hex 是否在某 POI footprint 内）</summary>
    /// <param name="alreadyInPoi">主场景 _poiEntered 标记 — 为 true 时短路</param>
    /// <param name="playerMoving">玩家是否在移动</param>
    /// <param name="playerPixelPos">玩家像素位置</param>
    public void CheckEnter(bool alreadyInPoi, bool playerMoving, Vector2 playerPixelPos)
    {
        if (_worldPois == null || alreadyInPoi || !playerMoving || _worldPois.Count == 0) return;
        if (_grid == null && _chunkManager == null) return;

        // 玩家当前所在 hex — chunk 模式 PoiId 写在 chunk tile 上，必须优先 chunk
        HexOverworldTile? tile;
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(playerPixelPos.X, playerPixelPos.Y);
            tile = _chunkManager.GetTile(axial.X, axial.Y);
        }
        else
        {
            tile = _grid!.GetTileAtPixel(playerPixelPos.X, playerPixelPos.Y);
        }
        if (tile == null) return;

        // 已经触发过交互且仍在同一个 POI footprint 内 → 等待离开
        if (_lastInteractedPoi != null && !_hasLeftLastPoi)
        {
            if (_lastInteractedPoi.ContainsHex(tile.Coord))
                return; // 还在 footprint 内，不重复触发
            _hasLeftLastPoi = true;
            _lastInteractedPoi = null;
        }

        // 查 footprint 命中
        if (string.IsNullOrEmpty(tile.PoiId)) return;

        foreach (var poi in _worldPois)
        {
            if (poi.PoiName != tile.PoiId) continue;
            _lastInteractedPoi = poi;
            _hasLeftLastPoi = false;
            GD.Print($"[POIController] 进入 POI: {poi.PoiName} (Scale={poi.Scale}, footprint={poi.FootprintTemplateName})");
            PlayerEnteredPoi?.Invoke(poi);
            return;
        }
    }

    // ========================================
    // 测试用 POI（无 WorldCreator 时）
    // ========================================

    /// <summary>查询 POI 位置的地面高程 Y（与 HexOverworldRenderer3D 一致）</summary>
    private float GetPOIGroundY(OverworldPOI poi)
    {
        HexOverworldTile? tile = null;
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(poi.Position.X, poi.Position.Y);
            tile = _chunkManager.GetTile(axial.X, axial.Y);
        }
        else if (_grid != null)
        {
            tile = _grid.GetTileAtPixel(poi.Position.X, poi.Position.Y);
        }
        if (tile == null) return 0.0f;

        const float ElevationScale = 0.8f;
        const float ElevationBaseline = 0.5f;
        float baseElev = (tile.Elevation - ElevationBaseline) * ElevationScale;
        float terrainBonus = tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Mountain => 0.45f,
            HexOverworldTile.TerrainType.MountainSnow => 0.55f,
            HexOverworldTile.TerrainType.Hills => 0.12f,
            HexOverworldTile.TerrainType.Rocky => 0.08f,
            HexOverworldTile.TerrainType.DeepWater => -0.18f,
            HexOverworldTile.TerrainType.ShallowWater => -0.10f,
            HexOverworldTile.TerrainType.River => -0.08f,
            HexOverworldTile.TerrainType.Swamp => -0.05f,
            HexOverworldTile.TerrainType.Bog => -0.04f,
            _ => 0.0f,
        };
        return baseElev + terrainBonus;
    }

    private void SetupTestPOIs(Vector2 playerPixelPos)
    {
        if (_grid == null || _markerParent == null) return;

        var testOffsets = new Vector2[]
        {
            new(500, 0), new(-500, 300), new(300, -500),
            new(-400, -400), new(600, 400),
        };

        foreach (var offset in testOffsets)
        {
            var poiPixel = playerPixelPos + offset;
            var tile = _grid.GetTileAtPixel(poiPixel.X, poiPixel.Y);
            if (tile == null || !tile.IsPassable) continue;

            var worldPos = CoordConverter.PixelToWorld3D(tile.PixelPos);
            var poiMesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.8f, 0.4f) },
                Position = worldPos + new Vector3(0, 0.5f, 0),
            };
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.9f, 0.8f, 0.1f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            poiMesh.MaterialOverride = mat;
            _markerParent.AddChild(poiMesh);
            _poiMeshes.Add(poiMesh);
        }
    }
}
