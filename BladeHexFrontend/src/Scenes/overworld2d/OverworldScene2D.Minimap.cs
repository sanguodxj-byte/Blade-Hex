// OverworldScene2D.Minimap.cs
// 小地图
using Godot;
using BladeHex.Map;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    private MinimapController? _minimap;

    private void InitMinimap()
    {
        _minimap = new MinimapController { Name = "MinimapController" };
        AddChild(_minimap);
        if (_minimap.Initialize(_fog, _chunkManager, WorldPois))
        {
            _minimap.MapClicked += OnMinimapClicked;
            _minimap.PoiClicked += OnMinimapPoiClicked;
        }
    }

    private void OnMinimapClicked(Vector2 pixelPos)
    {
        _camera.FocusOn(pixelPos);
    }

    private void OnMinimapPoiClicked(Vector2 pixelPos)
    {
        var poi = FindPOIAtPosition(pixelPos);
        if (poi != null)
            SetDirectedPoiInteraction(poi, pixelPos);
        else
            ClearDirectedInteraction();

        StartPathfinding(pixelPos);
    }

    private void UpdateMinimap()
    {
        _minimap?.Tick(_playerPixelPos, _camera.Zoom);
    }
}
