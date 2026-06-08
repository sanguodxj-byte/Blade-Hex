using Godot;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    public void SetAshAtTile(Vector2I axialCoord, float amount)
    {
        _ashController?.SetTileAsh(axialCoord, amount);
    }

    public void PaintAshAtTile(Vector2I axialCenter, int radiusTiles, float amount)
    {
        _ashController?.PaintAshCircle(axialCenter, radiusTiles, amount);
    }

    public void PaintAshAtWorld(Vector2 worldPosition, int radiusTiles, float amount)
    {
        var axial = HexOverworldTile.PixelToAxial(worldPosition.X, worldPosition.Y);
        _ashController?.PaintAshCircle(axial, radiusTiles, amount);
    }

    public void ClearAsh()
    {
        _ashController?.Clear();
    }

    public bool LoadAshMask(string resourcePath)
    {
        return _ashController?.LoadAshMask(resourcePath) == true;
    }
}
