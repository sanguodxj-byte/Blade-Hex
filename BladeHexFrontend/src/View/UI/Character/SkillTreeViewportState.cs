using Godot;
using System;
using BladeHex.Strategic;

namespace BladeHex.UI;

/// <summary>
/// Viewport state for the star chart canvas.
/// Keeps camera math and world-to-screen conversion out of SkillTreeUI.
/// </summary>
public sealed class SkillTreeViewportState
{
    private const float MinZoom = 0.2f;
    private const float MaxZoom = 3.0f;
    private const float ZoomSmoothSpeed = 18.0f;

    public SkillTreeCoord Coord { get; }
    public Vector2 Center { get; private set; } = new(600, 500);
    public float Zoom { get; private set; } = 1.0f;
    public Vector2 PanOffset { get; private set; } = Vector2.Zero;
    public bool IsZoomAnimating =>
        Math.Abs(Zoom - _targetZoom) >= 0.0005f || (PanOffset - _targetPanOffset).LengthSquared() >= 0.25f;
    private float _targetZoom = 1.0f;
    private Vector2 _targetPanOffset = Vector2.Zero;

    public SkillTreeViewportState(SkillTreeCoord coord)
    {
        Coord = coord;
    }

    public void SetCenterFromCanvas(Vector2 canvasSize)
    {
        Center = canvasSize / 2.0f;
        if (Center.LengthSquared() < 100)
            Center = new Vector2(500, 400);
    }

    public void Reset()
    {
        Zoom = 1.0f;
        PanOffset = Vector2.Zero;
        _targetZoom = Zoom;
        _targetPanOffset = PanOffset;
    }

    public void PanBy(Vector2 delta)
    {
        PanOffset = Snap(PanOffset + delta);
        _targetPanOffset = Snap(_targetPanOffset + delta);
    }

    public void ZoomBy(float factor, Vector2 pivot)
    {
        float oldZoom = Zoom;
        float newZoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
            return;

        Zoom = newZoom;
        var pivotOffset = pivot - Center;
        float ratio = newZoom / oldZoom;
        PanOffset = Snap((PanOffset - pivotOffset) * ratio + pivotOffset);
        _targetZoom = Zoom;
        _targetPanOffset = PanOffset;
    }

    public void ZoomBySmooth(float factor, Vector2 pivot)
    {
        float newZoom = Math.Clamp(_targetZoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _targetZoom) < 0.0001f)
            return;

        var pivotWorld = ScreenToWorld(pivot);
        _targetZoom = newZoom;
        _targetPanOffset = pivot - Center - pivotWorld * _targetZoom;
    }

    public bool UpdateSmoothZoom(float delta)
    {
        if (Math.Abs(Zoom - _targetZoom) < 0.0005f && (PanOffset - _targetPanOffset).LengthSquared() < 0.25f)
        {
            if (Math.Abs(Zoom - _targetZoom) < 0.0001f && (PanOffset - _targetPanOffset).LengthSquared() < 0.01f)
                return false;

            Zoom = _targetZoom;
            PanOffset = Snap(_targetPanOffset);
            _targetPanOffset = PanOffset;
            return true;
        }

        float t = 1.0f - MathF.Exp(-ZoomSmoothSpeed * delta);
        Zoom = Mathf.Lerp(Zoom, _targetZoom, t);
        PanOffset = PanOffset.Lerp(_targetPanOffset, t);
        return true;
    }

    public Vector2 WorldToScreen(Vector2 world)
    {
        return Center + world * Zoom + PanOffset;
    }

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        return (screen - Center - PanOffset) / Zoom;
    }

    public Vector2 VertexToScreen(int q, int r)
    {
        return WorldToScreen(Coord.VertexToPixel(q, r));
    }

    public Vector2 TileCentroidToScreen(Vector2I tileEncoded)
    {
        return WorldToScreen(Coord.TileCentroid(tileEncoded));
    }

    public Vector2[] TileVerticesToScreen(Vector2I tileEncoded)
    {
        var raw = Coord.TileVertices(tileEncoded);
        var result = new Vector2[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            result[i] = WorldToScreen(raw[i]);
        return result;
    }

    public Vector2 NodeToScreen(SkillNodeData node)
    {
        return WorldToScreen(NodeToWorld(node));
    }

    public Vector2 NodeToWorld(SkillNodeData node)
    {
        return node.NodeId == SkillTreeData.StartNodeId
            ? Coord.VertexToPixel(0, 0)
            : Coord.TileCentroid(node.GridPosition);
    }

    private static Vector2 Snap(Vector2 value)
    {
        return new Vector2(MathF.Round(value.X), MathF.Round(value.Y));
    }
}
