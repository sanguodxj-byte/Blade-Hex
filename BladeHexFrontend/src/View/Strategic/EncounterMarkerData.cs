// EncounterMarkerData.cs
// 遭遇标记数据 — 大地图上的静态遭遇点视觉标记
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 遭遇标记数据（用于大地图渲染遭遇点图标）
/// </summary>
public class EncounterMarkerData
{
    public Vector2I WorldCoord;
    public Vector2 PixelPosition;
    public EncounterType Type;
    public int Level;
    public bool IsActive = true;
}
