// OverworldCamera3D.cs
// 3D 大地图固定角度正交相机 — 只允许平移和缩放，不允许旋转
// 集成 CameraBoundsController：缩小到最小时刚好看到全地图，视野不超出边界
using Godot;
using BladeHex.View.Camera;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图 3D 相机控制器
/// 正交投影，固定俯角（约 35°），支持平移和缩放
/// 通过 SetMapBounds 设置地图范围后，自动限制缩放和位置
/// </summary>
public partial class OverworldCamera3D : Camera3D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>相机俯角（度）</summary>
    [Export] public float PitchAngle = 35.0f;

    /// <summary>相机到焦点的距离</summary>
    [Export] public float Distance = 30.0f;

    /// <summary>正交投影基础大小</summary>
    [Export] public float BaseOrthoSize = 12.0f;

    /// <summary>缩放范围（fallback，SetMapBounds 后会被覆盖）</summary>
    [Export] public float ZoomMin = 0.3f;
    [Export] public float ZoomMax = 4.0f;
    [Export] public float ZoomStep = 0.08f;
    [Export] public float ZoomSmooth = 8.0f;

    /// <summary>缩放加速：连续滚动时步长倍率（每次滚动后 +1 档，1.5秒未滚动则重置）</summary>
    [Export] public float ZoomAcceleration = 1.6f;
    /// <summary>最大累积加速倍率（防止失控）</summary>
    [Export] public float ZoomAccelMax = 4.0f;
    /// <summary>滚动空闲多久后重置加速（秒）</summary>
    [Export] public float ZoomAccelResetTime = 0.4f;
    /// <summary>Shift 按住时的额外加速倍率</summary>
    [Export] public float ZoomShiftMultiplier = 3.0f;

    /// <summary>平移速度（单位/秒，会随缩放缩放）</summary>
    [Export] public float PanSpeed = 15.0f;

    /// <summary>平移平滑系数</summary>
    [Export] public float PanSmooth = 6.0f;

    /// <summary>是否由外部控制（禁用自身输入处理）</summary>
    [Export] public bool ExternalControl = false;

    // ========================================
    // 边界控制器
    // ========================================

    /// <summary>相机边界控制器（地图生成后设置）</summary>
    public CameraBoundsController? BoundsController { get; private set; }

    // ========================================
    // 运行时状态
    // ========================================

    private float _zoomLevel = 1.0f;
    private float _zoomTarget = 1.0f;
    private Vector3 _panTarget = Vector3.Zero;
    private Vector3 _panCurrent = Vector3.Zero;
    private bool _isDragging = false;
    private Vector2 _dragStart;

    // 缩放加速
    private float _zoomAccelMult = 1.0f;
    private float _zoomLastScrollTime = 0.0f;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = BaseOrthoSize;
        Near = 0.1f;
        Far = 200.0f;
        Current = true;

        ApplyCameraAngle();
    }

    public override void _Process(double delta)
    {
        if (ExternalControl) return;

        float dt = (float)delta;

        // 键盘平移
        Vector3 input = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Z -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Z += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;

        if (input.LengthSquared() > 0)
        {
            input = input.Normalized();
            _panTarget += input * PanSpeed * _zoomLevel * dt;
        }

        // 平滑插值
        _panCurrent = _panCurrent.Lerp(_panTarget, PanSmooth * dt);
        _zoomLevel = Mathf.Lerp(_zoomLevel, _zoomTarget, ZoomSmooth * dt);

        // 应用缩放
        Size = BaseOrthoSize * _zoomLevel;

        // 边界限制：缩放
        if (BoundsController != null && BoundsController.IsInitialized)
        {
            Size = BoundsController.ClampOrthoSize(Size);
            _zoomLevel = Size / BaseOrthoSize;
        }

        // 计算相机位置
        float rad = Mathf.DegToRad(PitchAngle);
        var newPos = _panCurrent + new Vector3(0, Distance * Mathf.Sin(rad), Distance * Mathf.Cos(rad));

        // 边界限制：位置
        if (BoundsController != null && BoundsController.IsInitialized)
        {
            float aspect = GetViewportAspect();
            var clampedPos = BoundsController.ClampPosition(newPos, Size, aspect);
            // 把 clamp 结果反推到 _panCurrent / _panTarget，避免下一帧又漂出边界
            if (!clampedPos.IsEqualApprox(newPos))
            {
                var camOffset = new Vector3(0, Distance * Mathf.Sin(rad), Distance * Mathf.Cos(rad));
                _panCurrent = clampedPos - camOffset;
                _panTarget = _panCurrent;
            }
            newPos = clampedPos;
        }

        Position = newPos;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (ExternalControl) return;

        // 滚轮缩放（带加速）
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
            {
                float now = Time.GetTicksMsec() / 1000.0f;
                // 连续滚动累加加速；超时则重置
                if (now - _zoomLastScrollTime > ZoomAccelResetTime)
                    _zoomAccelMult = 1.0f;
                else
                    _zoomAccelMult = Mathf.Min(_zoomAccelMult * ZoomAcceleration, ZoomAccelMax);
                _zoomLastScrollTime = now;

                // Shift 加速倍率
                float step = ZoomStep * _zoomAccelMult;
                if (Input.IsKeyPressed(Key.Shift))
                    step *= ZoomShiftMultiplier;

                if (mb.ButtonIndex == MouseButton.WheelUp)
                    _zoomTarget -= step;
                else
                    _zoomTarget += step;
                ClampZoomTarget();
            }
        }

        // 中键拖拽平移
        if (@event is InputEventMouseButton mb2)
        {
            if (mb2.ButtonIndex == MouseButton.Middle)
            {
                _isDragging = mb2.Pressed;
                _dragStart = mb2.Position;
            }
        }

        if (@event is InputEventMouseMotion mm && _isDragging)
        {
            Vector2 delta = mm.Relative;
            float factor = _zoomLevel * 0.02f;
            _panTarget += new Vector3(-delta.X * factor, 0, -delta.Y * factor);
        }
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 设置地图边界（地图生成完成后调用）。
    /// 自动计算最大缩小限制，确保缩小到最小时刚好看到全地图。
    /// </summary>
    /// <param name="mapWidthPixels">地图像素宽度</param>
    /// <param name="mapHeightPixels">地图像素高度</param>
    /// <param name="pixelToWorld">像素到 3D 世界坐标的缩放因子</param>
    public void SetMapBounds(float mapWidthPixels, float mapHeightPixels, float pixelToWorld)
    {
        BoundsController = new CameraBoundsController();
        BoundsController.MinOrthoSize = BaseOrthoSize * ZoomMin;

        float aspect = GetViewportAspect();
        BoundsController.SetWorldBoundsFromPixels(mapWidthPixels, mapHeightPixels, pixelToWorld, PitchAngle, aspect);

        // 更新 ZoomMax 为动态计算值
        ZoomMax = BoundsController.MaxOrthoSize / BaseOrthoSize;

        // 确保当前缩放在范围内
        ClampZoomTarget();

        GD.Print($"[OverworldCamera3D] 边界设置: map={mapWidthPixels}×{mapHeightPixels}px, " +
                 $"world={mapWidthPixels * pixelToWorld:F1}×{mapHeightPixels * pixelToWorld:F1}, " +
                 $"maxOrtho={BoundsController.MaxOrthoSize:F1}, zoomRange=[{ZoomMin:F2}, {ZoomMax:F2}]");
    }

    /// <summary>
    /// 设置地图边界（直接传入 3D 世界 Aabb）。
    /// </summary>
    public void SetMapBoundsFromAabb(Aabb worldBounds)
    {
        BoundsController = new CameraBoundsController();
        BoundsController.MinOrthoSize = BaseOrthoSize * ZoomMin;

        float aspect = GetViewportAspect();
        BoundsController.SetWorldBounds(worldBounds, PitchAngle, aspect);

        ZoomMax = BoundsController.MaxOrthoSize / BaseOrthoSize;
        ClampZoomTarget();
    }

    /// <summary>将相机焦点移动到指定世界坐标</summary>
    public void FocusOn(Vector3 worldPos)
    {
        _panTarget = new Vector3(worldPos.X, 0, worldPos.Z);
        _panCurrent = _panTarget;
    }

    /// <summary>将相机焦点移动到指定 XZ 坐标</summary>
    public void FocusOnXZ(float x, float z)
    {
        FocusOn(new Vector3(x, 0, z));

        if (ExternalControl)
        {
            float rad = Mathf.DegToRad(PitchAngle);
            Position = new Vector3(x, Distance * Mathf.Sin(rad), z + Distance * Mathf.Cos(rad));
        }
    }

    // ========================================
    // 内部
    // ========================================

    private void ApplyCameraAngle()
    {
        RotationDegrees = new Vector3(-PitchAngle, 0, 0);
        float rad = Mathf.DegToRad(PitchAngle);
        Position = new Vector3(0, Distance * Mathf.Sin(rad), Distance * Mathf.Cos(rad));
    }

    private void ClampZoomTarget()
    {
        if (BoundsController != null && BoundsController.IsInitialized)
            _zoomTarget = BoundsController.ClampZoomLevel(BaseOrthoSize, _zoomTarget);
        else
            _zoomTarget = Mathf.Clamp(_zoomTarget, ZoomMin, ZoomMax);
    }

    private float GetViewportAspect()
    {
        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
        return vp.X / Mathf.Max(1f, vp.Y);
    }
}
