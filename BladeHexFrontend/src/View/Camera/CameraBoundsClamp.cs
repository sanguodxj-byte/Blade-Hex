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
    /// </summary>
    /// <param name="currentPos">相机当前世界位置</param>
    /// <param name="orthoSize">Camera3D.Size — 屏幕垂直方向覆盖的世界单位</param>
    /// <param name="tiltDegrees">相机绕 X 轴旋转角度（典型 -45），用于计算视线落地偏移</param>
    /// <param name="worldBounds">需要保持视线落地点在内的世界 AABB</param>
    /// <param name="viewportAspect">视口宽高比（width / height）</param>
    /// <returns>限制后的相机世界位置</returns>
    public static Vector3 Clamp3DOrtho(
        Vector3 currentPos, float orthoSize, float tiltDegrees,
        Aabb worldBounds, float viewportAspect)
    {
        // 视线方向相对水平的角度（-45 度俯视 → 视线 45 度向下）
        float tiltRad = Mathf.DegToRad(Mathf.Abs(tiltDegrees));

        // 视线落地点 Z 偏移：camZ - camY / tan(tilt)
        // -45 度时 tan = 1，所以偏移 = camY
        float lookAtZOffset = currentPos.Y / Mathf.Max(0.0001f, Mathf.Tan(tiltRad));

        // 屏幕可见的世界范围（半径）
        float halfHeight = orthoSize * 0.5f;
        float halfWidth = halfHeight * viewportAspect;
        // Z 方向因为倾斜，可见深度 = orthoSize / cos(tilt)
        float halfDepth = halfHeight / Mathf.Cos(tiltRad);

        // 视线落地点 X = camX, Z = camZ - lookAtZOffset
        float lookAtX = currentPos.X;
        float lookAtZ = currentPos.Z - lookAtZOffset;

        // 限制视线落地点在 worldBounds 范围内
        // X 方向：屏幕中央 ± halfWidth 必须与 worldBounds 重叠
        float minLookX = worldBounds.Position.X + halfWidth;
        float maxLookX = worldBounds.Position.X + worldBounds.Size.X - halfWidth;
        // 如果可见范围 > 边界范围，居中
        if (minLookX > maxLookX)
            lookAtX = worldBounds.Position.X + worldBounds.Size.X * 0.5f;
        else
            lookAtX = Mathf.Clamp(lookAtX, minLookX, maxLookX);

        // Z 方向同理
        float minLookZ = worldBounds.Position.Z + halfDepth;
        float maxLookZ = worldBounds.Position.Z + worldBounds.Size.Z - halfDepth;
        if (minLookZ > maxLookZ)
            lookAtZ = worldBounds.Position.Z + worldBounds.Size.Z * 0.5f;
        else
            lookAtZ = Mathf.Clamp(lookAtZ, minLookZ, maxLookZ);

        // 反推相机位置
        return new Vector3(lookAtX, currentPos.Y, lookAtZ + lookAtZOffset);
    }

    /// <summary>
    /// 计算让整个 <paramref name="worldBounds"/> 完全可见所需的最大正交尺寸。
    /// 用于限制玩家滚轮缩小到能看见全部战场为止。
    /// </summary>
    public static float MaxOrthoSizeToFit(Aabb worldBounds, float tiltDegrees, float viewportAspect)
    {
        float tiltRad = Mathf.DegToRad(Mathf.Abs(tiltDegrees));
        float cosT = Mathf.Cos(tiltRad);

        // 横向：bounds.X 宽必须 ≤ orthoSize * aspect
        // → orthoSize ≥ bounds.X / aspect
        float sizeForWidth = worldBounds.Size.X / Mathf.Max(0.001f, viewportAspect);

        // Z 方向：可见深度 = orthoSize / cos(tilt)
        // 要让 bounds.Z ≤ orthoSize / cos(tilt)
        // → orthoSize ≥ bounds.Z * cos(tilt)
        float sizeForDepth = worldBounds.Size.Z * cosT;

        // 取较大值确保两个方向都能完全容纳
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
