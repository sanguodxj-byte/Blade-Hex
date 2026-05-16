// CameraBoundsController.cs
// 相机边界控制器 — 管理正交 3D 相机的世界边界、缩放限制和位置限制
// 可复用于大地图和战斗场景
using Godot;

namespace BladeHex.View.Camera;

/// <summary>
/// 相机边界控制器 — 为正交 3D 相机提供：
/// 1. 世界边界定义（Aabb）
/// 2. 最大缩小限制（缩小到刚好看到全地图）
/// 3. 位置限制（任何缩放下视野不超出地图边界）
///
/// 使用方式：
/// <code>
/// var bounds = new CameraBoundsController();
/// bounds.SetWorldBounds(aabb, pitchAngle);
/// // 每帧或输入后调用：
/// camera.Size = bounds.ClampOrthoSize(camera.Size);
/// camera.Position = bounds.ClampPosition(camera.Position, camera.Size, viewportAspect);
/// </code>
/// </summary>
public class CameraBoundsController
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>世界边界（XZ 平面的 Aabb）</summary>
    public Aabb WorldBounds { get; private set; }

    /// <summary>相机俯角（度，正值表示向下看）</summary>
    public float PitchAngle { get; private set; } = 35.0f;

    /// <summary>最小正交尺寸（最大放大）</summary>
    public float MinOrthoSize { get; set; } = 3.0f;

    /// <summary>最大正交尺寸（最大缩小，自动计算）</summary>
    public float MaxOrthoSize { get; private set; } = 100.0f;

    /// <summary>是否已初始化</summary>
    public bool IsInitialized { get; private set; } = false;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 设置世界边界并计算最大缩小限制。
    /// 调用后 MaxOrthoSize 会被自动计算为"刚好看到全地图"的值。
    /// </summary>
    /// <param name="worldBounds">世界 Aabb（XZ 平面范围）</param>
    /// <param name="pitchAngle">相机俯角（度，如 35）</param>
    /// <param name="viewportAspect">视口宽高比（width/height），用于计算 MaxOrthoSize</param>
    public void SetWorldBounds(Aabb worldBounds, float pitchAngle, float viewportAspect = 1.78f)
    {
        WorldBounds = worldBounds;
        PitchAngle = pitchAngle;
        MaxOrthoSize = CameraBoundsClamp.MaxOrthoSizeToFit(worldBounds, -pitchAngle, viewportAspect);
        IsInitialized = true;
    }

    /// <summary>
    /// 从像素尺寸和坐标转换因子构建世界边界。
    /// 适用于大地图（像素坐标 → 3D 世界坐标）。
    /// </summary>
    /// <param name="mapWidthPixels">地图像素宽度</param>
    /// <param name="mapHeightPixels">地图像素高度</param>
    /// <param name="pixelToWorld">像素到世界坐标的缩放因子（如 1/156）</param>
    /// <param name="pitchAngle">相机俯角（度）</param>
    /// <param name="viewportAspect">视口宽高比</param>
    public void SetWorldBoundsFromPixels(float mapWidthPixels, float mapHeightPixels,
        float pixelToWorld, float pitchAngle, float viewportAspect = 1.78f)
    {
        float worldW = mapWidthPixels * pixelToWorld;
        float worldH = mapHeightPixels * pixelToWorld;

        // 加一点边距（半个 hex 宽度）
        float margin = 1.0f;
        var bounds = new Aabb(
            new Vector3(-margin, 0, -margin),
            new Vector3(worldW + margin * 2, 1, worldH + margin * 2));

        SetWorldBounds(bounds, pitchAngle, viewportAspect);
    }

    /// <summary>
    /// 视口大小变化时重新计算 MaxOrthoSize。
    /// </summary>
    public void OnViewportResized(float viewportAspect)
    {
        if (!IsInitialized) return;
        MaxOrthoSize = CameraBoundsClamp.MaxOrthoSizeToFit(WorldBounds, -PitchAngle, viewportAspect);
    }

    // ========================================
    // 每帧限制
    // ========================================

    /// <summary>
    /// 限制正交尺寸在 [MinOrthoSize, MaxOrthoSize] 范围内。
    /// </summary>
    public float ClampOrthoSize(float currentSize)
    {
        if (!IsInitialized) return currentSize;
        return Mathf.Clamp(currentSize, MinOrthoSize, MaxOrthoSize);
    }

    /// <summary>
    /// 限制相机位置，确保视野不超出世界边界。
    /// </summary>
    /// <param name="currentPos">相机当前世界位置</param>
    /// <param name="orthoSize">当前正交尺寸</param>
    /// <param name="viewportAspect">视口宽高比</param>
    /// <returns>限制后的相机位置</returns>
    public Vector3 ClampPosition(Vector3 currentPos, float orthoSize, float viewportAspect)
    {
        if (!IsInitialized) return currentPos;
        return CameraBoundsClamp.Clamp3DOrtho(currentPos, orthoSize, -PitchAngle, WorldBounds, viewportAspect);
    }

    /// <summary>
    /// 将缩放倍率转换为正交尺寸并限制。
    /// </summary>
    /// <param name="baseOrthoSize">基础正交尺寸</param>
    /// <param name="zoomLevel">缩放倍率（1.0 = 基础大小）</param>
    /// <returns>限制后的缩放倍率</returns>
    public float ClampZoomLevel(float baseOrthoSize, float zoomLevel)
    {
        if (!IsInitialized) return zoomLevel;
        float minZoom = MinOrthoSize / Mathf.Max(0.001f, baseOrthoSize);
        float maxZoom = MaxOrthoSize / Mathf.Max(0.001f, baseOrthoSize);
        return Mathf.Clamp(zoomLevel, minZoom, maxZoom);
    }
}
