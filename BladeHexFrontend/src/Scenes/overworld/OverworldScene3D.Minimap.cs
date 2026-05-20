// OverworldScene3D.Minimap.cs
// 小地图组件薄包装：实例化 MinimapController + 转发点击事件
using Godot;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    private MinimapController? _minimap;

    private void InitMinimap()
    {
        _minimap = new MinimapController { Name = "MinimapController" };
        AddChild(_minimap);
        if (_minimap.Initialize(_fog, _chunkManager, WorldPois, _camera))
        {
            _minimap.MapClicked += OnMinimapClicked;
            _minimap.PoiClicked += OnMinimapPoiClicked;
        }
    }

    private void OnMinimapClicked(Vector2 worldPos)
    {
        // 点击小地图 → 移动相机到该位置
        var world3D = CoordConverter.PixelToWorld3D(worldPos);
        _camera.FocusOn(world3D);
    }

    private void OnMinimapPoiClicked(Vector2 worldPos)
    {
        // 点击小地图 POI → 寻路到该位置
        StartPathfinding(worldPos);
    }

    private void UpdateMinimap()
    {
        _minimap?.Tick(_playerPixelPos);
    }
}
