// CoordConverter.cs
// 2D 像素坐标 ↔ 3D 世界坐标转换工具
// 大地图逻辑层使用 2D 像素坐标（HexOverworldTile.HexSize=156），
// 3D 渲染层使用世界单位（HexRadius=1.0）。
// 本类封装两者之间的转换。
using Godot;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图坐标转换器 — 2D 像素坐标 ↔ 3D 世界坐标
/// </summary>
public static class CoordConverter
{
    /// <summary>
    /// 2D 像素空间中的六边形外径（与 HexOverworldTile.HexSize 一致）
    /// </summary>
    public const float PixelHexSize = 156.0f;

    /// <summary>
    /// 3D 世界空间中的六边形外径
    /// </summary>
    public const float WorldHexSize = 1.0f;

    /// <summary>
    /// 像素 → 世界 缩放因子
    /// </summary>
    public const float PixelToWorld = WorldHexSize / PixelHexSize;

    /// <summary>
    /// 世界 → 像素 缩放因子
    /// </summary>
    public const float WorldToPixel = PixelHexSize / WorldHexSize;

    /// <summary>
    /// 2D 像素坐标 → 3D 世界坐标（Y=0 平面）
    /// 像素 X → 世界 X，像素 Y → 世界 Z
    /// </summary>
    public static Vector3 PixelToWorld3D(Vector2 pixel)
    {
        return new Vector3(pixel.X * PixelToWorld, 0, pixel.Y * PixelToWorld);
    }

    /// <summary>
    /// 2D 像素坐标 → 3D 世界坐标
    /// </summary>
    public static Vector3 PixelToWorld3D(float px, float py)
    {
        return new Vector3(px * PixelToWorld, 0, py * PixelToWorld);
    }

    /// <summary>
    /// 3D 世界坐标 → 2D 像素坐标
    /// 世界 X → 像素 X，世界 Z → 像素 Y
    /// </summary>
    public static Vector2 World3DToPixel(Vector3 world)
    {
        return new Vector2(world.X * WorldToPixel, world.Z * WorldToPixel);
    }

    /// <summary>
    /// 3D 世界坐标 → 2D 像素坐标
    /// </summary>
    public static Vector2 World3DToPixel(float wx, float wz)
    {
        return new Vector2(wx * WorldToPixel, wz * WorldToPixel);
    }

    /// <summary>
    /// 从 Camera3D 的鼠标位置获取 Y=0 平面上的 3D 世界坐标
    /// </summary>
    public static Vector3? ScreenToWorld3D(Camera3D camera, Vector2 screenPos)
    {
        var origin = camera.ProjectRayOrigin(screenPos);
        var dir = camera.ProjectRayNormal(screenPos);

        // 与 Y=0 平面求交
        if (Mathf.Abs(dir.Y) < 0.0001f)
            return null; // 射线平行于地面

        float t = -origin.Y / dir.Y;
        if (t < 0)
            return null; // 交点在相机后方

        return origin + dir * t;
    }

    /// <summary>
    /// 从 Camera3D 的鼠标位置获取 2D 像素坐标（用于寻路等逻辑层）
    /// </summary>
    public static Vector2? ScreenToPixel(Camera3D camera, Vector2 screenPos)
    {
        var world = ScreenToWorld3D(camera, screenPos);
        if (world == null) return null;
        return World3DToPixel(world.Value);
    }
}
