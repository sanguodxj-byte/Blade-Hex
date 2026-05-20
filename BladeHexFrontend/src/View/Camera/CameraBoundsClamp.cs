// CameraBoundsClamp.cs
// 通用相机边界限制工具 — 战斗 (Camera3D 正交斜俯视) / 大地图 (Camera2D)
// 设计原则：纯静态函数，不持有状态；场景在输入处理中调用即可。
using Godot;

namespace BladeHex.View.Camera;

/// <summary>
/// 相机边界限制工具。
/// 提供两组 API：
/// 1. <see cref="Clamp3DOrtho"/> / <see cref="MaxOrthoSizeToFit"/> — 用于战斗场景的 Camera3D 正交相机
/// 2. <see cref="Clamp2D"/> / <see cref="MaxZoom2DToFit"/> — 用于大地图的 Camera2D
///
/// 使用示例（战斗场景）：
/// <code>
/// // 限制相机位置
/// _camera.Position = CameraBoundsClamp.Clamp3DOrtho(
///     _camera.Position, _camera.Size, -45f, _battlefieldBounds, GetViewportAspect());
///
/// // 限制缩放
/// float maxSize = CameraBoundsClamp.MaxOrthoSizeToFit(_battlefieldBounds, -45f, GetViewportAspect());
/// _camera.Size = Mathf.Min(_camera.Size, maxSize);
/// </code>
/// </summary>
public static class CameraBoundsClamp
{
    // ============================================================
    // Camera3D 正交斜俯视 (战斗)
    // ============================================================

    /// <summary>
    /// 限制 Camera3D 正交相机的位置在指定 AABB 范围内。
    /// 假设相机沿 X 轴旋转 <paramref name="tiltDegrees"/> 度（典型 -45° 俯视），
    /// 视线落地点 = (camX, 0, camZ - camY * tan(|tilt|))。
    ///
    /// <paramref name="topInsetRatio"/> / <paramref name="bottomInsetRatio"/>：
    /// 上下 UI 遮挡占视口高度的比例（0~1），用于把"有效视口"视为 UI 之间的部分。
    /// 例如顶栏 60px、底栏 200px、视口 1080px → top=0.056, bottom=0.185。
    ///
    /// 当有效可见范围 ≥ 世界范围时，把"有效视口中心"对齐到世界中央，
    /// 这样即使下方 UI 较厚，世界也不会被压在 UI 后面。
    /// </summary>
    public static Vector3 Clamp3DOrtho(
        Vector3 currentPos, float orthoSize, float tiltDegrees,
        Aabb worldBounds, float viewportAspect,
        float topInsetRatio = 0f, float bottomInsetRatio = 0f)
    {
        float tiltRad = Mathf.DegToRad(Mathf.Abs(tiltDegrees));
        float sinT = Mathf.Max(0.0001f, Mathf.Sin(tiltRad));
        float tanT = Mathf.Max(0.0001f, Mathf.Tan(tiltRad));

        float lookAtZOffset = currentPos.Y / tanT;

        // 有效视口高度比（夹住一个非零下界，避免 UI 占满时除零）
        float effRatio = Mathf.Max(0.05f, 1f - topInsetRatio - bottomInsetRatio);
        // 屏幕"可见区域中心"相对屏幕中心的偏移比例：top 重 → 中心向下偏 → 落地点 Z 增大
        float centerShiftRatio = (topInsetRatio - bottomInsetRatio) * 0.5f;

        float halfWidth = orthoSize * 0.5f * viewportAspect;
        // 有效视口在 Z 方向覆盖 = orthoSize * effRatio / sin(tilt)
        float halfDepth = orthoSize * effRatio * 0.5f / sinT;
        // 落地中心 Z 偏移 = orthoSize * centerShiftRatio / sin(tilt)
        float depthOffset = orthoSize * centerShiftRatio / sinT;

        // 落地点（屏幕几何中心对应地面）
        float lookAtX = currentPos.X;
        float lookAtZ = currentPos.Z - lookAtZOffset;
        // 真正"想要居中显示的目标点"= 落地点 + UI 偏移
        float visibleX = lookAtX;
        float visibleZ = lookAtZ + depthOffset;

        float worldCenterX = worldBounds.Position.X + worldBounds.Size.X * 0.5f;
        float worldCenterZ = worldBounds.Position.Z + worldBounds.Size.Z * 0.5f;

        // X：可见范围 ≥ 世界范围时居中，否则 clamp
        if (halfWidth * 2.0f >= worldBounds.Size.X)
        {
            visibleX = worldCenterX;
        }
        else
        {
            float minLookX = worldBounds.Position.X + halfWidth;
            float maxLookX = worldBounds.Position.X + worldBounds.Size.X - halfWidth;
            visibleX = Mathf.Clamp(visibleX, minLookX, maxLookX);
        }

        // Z：同上，但用"有效深度"
        if (halfDepth * 2.0f >= worldBounds.Size.Z)
        {
            visibleZ = worldCenterZ;
        }
        else
        {
            float minLookZ = worldBounds.Position.Z + halfDepth;
            float maxLookZ = worldBounds.Position.Z + worldBounds.Size.Z - halfDepth;
            visibleZ = Mathf.Clamp(visibleZ, minLookZ, maxLookZ);
        }

        // 反推回相机 lookAt 的 X / Z
        lookAtX = visibleX;
        lookAtZ = visibleZ - depthOffset;

        return new Vector3(lookAtX, currentPos.Y, lookAtZ + lookAtZOffset);
    }

    /// <summary>
    /// 计算让整个 <paramref name="worldBounds"/> 完全可见于"有效视口"所需的最大正交尺寸。
    /// 有效视口 = 视口高度 × (1 - top - bottom)；UI 越厚需要的 ortho size 越大。
    /// </summary>
    public static float MaxOrthoSizeToFit(
        Aabb worldBounds, float tiltDegrees, float viewportAspect,
        float topInsetRatio = 0f, float bottomInsetRatio = 0f)
    {
        float tiltRad = Mathf.DegToRad(Mathf.Abs(tiltDegrees));
        float sinT = Mathf.Max(0.0001f, Mathf.Sin(tiltRad));

        float sizeForWidth = worldBounds.Size.X / Mathf.Max(0.001f, viewportAspect);
        // 有效深度 = orthoSize * effRatio / sin(tilt) 必须 ≥ worldZ
        // → orthoSize ≥ worldZ * sin(tilt) / effRatio
        float effRatio = Mathf.Max(0.05f, 1f - topInsetRatio - bottomInsetRatio);
        float sizeForDepth = worldBounds.Size.Z * sinT / effRatio;

        return Mathf.Max(sizeForWidth, sizeForDepth);
    }

    // ============================================================
    // Camera2D (大地图)
    // ============================================================

    /// <summary>
    /// 限制 Camera2D 的位置在指定矩形范围内。
    /// </summary>
    /// <param name="currentPos">相机当前位置（世界坐标）</param>
    /// <param name="zoom">Camera2D.Zoom（值越大越放大）</param>
    /// <param name="worldBounds">需要保持相机视野中心在内的世界矩形</param>
    /// <param name="viewportSize">视口像素尺寸</param>
    /// <returns>限制后的相机位置</returns>
    public static Vector2 Clamp2D(Vector2 currentPos, Vector2 zoom, Rect2 worldBounds, Vector2 viewportSize)
    {
        // 可见的世界范围 = viewport / zoom
        Vector2 visibleHalf = (viewportSize / zoom) * 0.5f;

        float minX = worldBounds.Position.X + visibleHalf.X;
        float maxX = worldBounds.Position.X + worldBounds.Size.X - visibleHalf.X;
        float minY = worldBounds.Position.Y + visibleHalf.Y;
        float maxY = worldBounds.Position.Y + worldBounds.Size.Y - visibleHalf.Y;

        float clampedX = minX > maxX
            ? worldBounds.Position.X + worldBounds.Size.X * 0.5f
            : Mathf.Clamp(currentPos.X, minX, maxX);
        float clampedY = minY > maxY
            ? worldBounds.Position.Y + worldBounds.Size.Y * 0.5f
            : Mathf.Clamp(currentPos.Y, minY, maxY);

        return new Vector2(clampedX, clampedY);
    }

    /// <summary>
    /// 计算让整个 <paramref name="worldBounds"/> 完全可见所需的最小 Zoom 值。
    /// 用于限制玩家滚轮缩小到能看见全部世界为止。
    /// </summary>
    public static Vector2 MaxZoom2DToFit(Rect2 worldBounds, Vector2 viewportSize)
    {
        // visibleSize = viewport / zoom 必须 ≥ worldBounds.Size
        // → zoom ≤ viewport / worldBounds.Size
        float zoomX = viewportSize.X / Mathf.Max(0.001f, worldBounds.Size.X);
        float zoomY = viewportSize.Y / Mathf.Max(0.001f, worldBounds.Size.Y);
        // 取较小值（更缩小），并保持等比缩放
        float zoom = Mathf.Min(zoomX, zoomY);
        return new Vector2(zoom, zoom);
    }
}
