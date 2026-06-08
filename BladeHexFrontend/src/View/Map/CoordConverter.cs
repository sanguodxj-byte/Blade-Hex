// CoordConverter.cs
// 坐标转换工具 — 像素坐标 ↔ 3D 世界坐标（保留为兼容存根）
using Godot;

namespace BladeHex.View.Map;

/// <summary>
/// 像素坐标与 3D 世界坐标之间的转换。
/// 2D 迁移后仅作为兼容存根保留（TerritoryOverlay 等仍引用）。
/// </summary>
public static class CoordConverter
{
    /// <summary>像素到 3D 世界坐标的缩放因子</summary>
    public const float PixelToWorld = 1.0f / 156.0f;

    /// <summary>像素坐标 → 3D 世界坐标 (XZ 平面)</summary>
    public static Vector3 PixelToWorld3D(Vector2 pixelPos)
    {
        return new Vector3(pixelPos.X * PixelToWorld, 0, pixelPos.Y * PixelToWorld);
    }

    /// <summary>3D 世界坐标 → 像素坐标</summary>
    public static Vector2 World3DToPixel(Vector3 worldPos)
    {
        return new Vector2(worldPos.X / PixelToWorld, worldPos.Z / PixelToWorld);
    }
}
