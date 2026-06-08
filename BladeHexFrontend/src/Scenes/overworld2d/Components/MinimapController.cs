// MinimapController.cs
// 大地图小地图控制器 — 创建 MinimapPanel + 连接信号 + 每帧同步玩家/相机。
//
// 职责：
//   - 在 CanvasLayer 上挂载 MinimapPanel
//   - 接收 _fog / _chunkManager / WorldPois / 地图尺寸完成 panel 初始化
//   - 每帧更新玩家位置 + 相机 zoom 折算
//   - 把 MinimapClicked / MinimapPoiClicked 转发为本组件的事件
//
// 服务于架构优化 spec R5 — Sprint 6 场景控制器组件化。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.View.UI.Overworld;

namespace BladeHex.Scenes.Overworld.Components;

[GlobalClass]
public partial class MinimapController : Node
{
    // ========================================
    // 引用
    // ========================================

    private MinimapPanel? _panel;

    /// <summary>暴露内部 panel 给战斗过渡动画使用</summary>
    public Control? Panel => _panel;

    /// <summary>强制重新烘焙小地图（reveal_all 时调用）</summary>
    public void RebakeTerrain() => _panel?.RebakeTerrain();

    // ========================================
    // 转发事件
    // ========================================

    /// <summary>用户点击小地图（worldPos = 像素坐标）→ 由场景决定如何响应（通常是相机聚焦）</summary>
    public event Action<Vector2>? MapClicked;

    /// <summary>用户点击小地图 POI（worldPos = 像素坐标）→ 通常是寻路</summary>
    public event Action<Vector2>? PoiClicked;

    // ========================================
    // 入口
    // ========================================

    /// <summary>由 OverworldScene2D 在 _Ready 阶段调用</summary>
    /// <returns>true 表示初始化成功，false 表示前置条件不满足（如 fog 未就绪）</returns>
    public bool Initialize(FogOfWar? fog, ChunkManager? chunkManager, List<OverworldPOI> worldPois)
    {
        if (fog == null)
        {
            GD.PrintErr("[MinimapController] 小地图初始化跳过: 迷雾未就绪");
            return false;
        }

        float mapW = fog.MapWidthPx;
        float mapH = fog.MapHeightPx;
        if (mapW <= 0 || mapH <= 0)
        {
            GD.PrintErr($"[MinimapController] 小地图初始化跳过: 地图尺寸无效 ({mapW}×{mapH})");
            return false;
        }

        var minimapLayer = new CanvasLayer { Layer = 5, Name = "MinimapLayer" };
        AddChild(minimapLayer);

        _panel = new MinimapPanel();
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.OffsetLeft = -348;
        _panel.OffsetTop = 60;
        _panel.OffsetRight = -12;
        _panel.OffsetBottom = 310;
        minimapLayer.AddChild(_panel);

        _panel.Initialize(fog, chunkManager, worldPois, mapW, mapH);
        _panel.MinimapClicked += OnPanelClicked;
        _panel.MinimapPoiClicked += OnPanelPoiClicked;

        GD.Print($"[MinimapController] 小地图初始化: {mapW}×{mapH}px");
        return true;
    }

    /// <summary>由 OverworldScene2D._Process 调用</summary>
    public void Tick(Vector2 playerPixelPos, Vector2 cameraZoom)
    {
        if (_panel == null) return;
        var viewportSize = GetViewport().GetVisibleRect().Size;
        _panel.UpdatePlayerAndCamera(playerPixelPos, playerPixelPos, cameraZoom, viewportSize);
    }

    // ========================================
    // 内部
    // ========================================

    private void OnPanelClicked(Vector2 worldPos) => MapClicked?.Invoke(worldPos);
    private void OnPanelPoiClicked(Vector2 worldPos) => PoiClicked?.Invoke(worldPos);
}
