// OverworldCamera2D.cs
// 2D 大地图相机 — 替代 OverworldCamera3D
// 支持缩放、平移、边界限制
using Godot;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图 2D 相机控制器
/// 支持平移和缩放，通过 LimitLeft/Right/Top/Bottom 限制边界
/// </summary>
public partial class OverworldCamera2D : Camera2D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>基础缩放级别</summary>
    [Export] public float BaseZoom = 1.0f;

    /// <summary>缩放范围（解锁限制，支持超远俯瞰到极近细节）</summary>
    [Export] public float ZoomMin = 0.03f;
    [Export] public float ZoomMax = 20.0f;
    [Export] public float ZoomStep = 0.1f;
    [Export] public float ZoomSmooth = 8.0f;

    /// <summary>缩放加速：连续滚动时步长倍率</summary>
    [Export] public float ZoomAcceleration = 1.6f;
    [Export] public float ZoomAccelMax = 4.0f;
    [Export] public float ZoomAccelResetTime = 0.4f;
    [Export] public float ZoomShiftMultiplier = 3.0f;

    /// <summary>平移速度（会随缩放缩放）</summary>
    [Export] public float PanSpeed = 2500.0f;

    /// <summary>平移平滑系数</summary>
    [Export] public float PanSmooth = 6.0f;

    /// <summary>渲染位置对齐到当前 zoom 下的屏幕像素网格，避免 2D 纹理随相机平移抖动。</summary>
    [Export] public bool SnapPositionToPixelGrid = true;

    /// <summary>滚轮缩放目标按 ZoomStep 量化，避免长期停留在任意小数 zoom。</summary>
    [Export] public bool QuantizeZoomTarget = true;

    /// <summary>是否由外部控制（禁用自身输入处理）</summary>
    public bool ExternalControl => _inputBlockCount > 0;

    /// <summary>输入阻断引用计数</summary>
    private int _inputBlockCount = 0;

    public void PushInputBlock() => _inputBlockCount++;
    public void PopInputBlock() => _inputBlockCount = System.Math.Max(0, _inputBlockCount - 1);
    public void ClearInputBlock() => _inputBlockCount = 0;

    // ========================================
    // 运行时状态
    // ========================================

    private float _zoomLevel = 1.0f;
    private float _zoomTarget = 1.0f;
    private Vector2 _panTarget = Vector2.Zero;
    private Vector2 _panCurrent = Vector2.Zero;
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
        _zoomLevel = QuantizeZoom(BaseZoom);
        _zoomTarget = _zoomLevel;
        Zoom = new Vector2(_zoomLevel, _zoomLevel);
        _panCurrent = Position;
        _panTarget = Position;

        // 启用边界限制
        LimitLeft = -10000;
        LimitRight = 10000;
        LimitTop = -10000;
        LimitBottom = 10000;
    }

    public override void _Process(double delta)
    {
        if (ExternalControl) return;

        float dt = (float)delta;

        // 键盘平移
        Vector2 input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) input.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;

        if (input.LengthSquared() > 0)
        {
            input = input.Normalized();
            // 速度与 zoom 成反比 → 屏幕像素速度恒定
            float speedFactor = 1.0f / Mathf.Max(_zoomLevel, 0.1f);
            _panTarget += input * PanSpeed * speedFactor * dt;
        }

        // 平滑插值
        _panCurrent = _panCurrent.Lerp(_panTarget, PanSmooth * dt);
        if (_panCurrent.DistanceSquaredTo(_panTarget) < 1.0f)
            _panCurrent = _panTarget;

        _zoomLevel = Mathf.Lerp(_zoomLevel, _zoomTarget, ZoomSmooth * dt);
        if (Mathf.Abs(_zoomLevel - _zoomTarget) < 0.001f)
            _zoomLevel = _zoomTarget;

        // 平滑缩放。缩放目标会被量化，避免相机长期停在任意小数 zoom 上。
        Zoom = new Vector2(_zoomLevel, _zoomLevel);

        // 逻辑位置保持平滑，实际渲染位置吸附到当前 zoom 下的屏幕像素网格。
        Position = SnapCameraPosition(_panCurrent);
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
                if (now - _zoomLastScrollTime > ZoomAccelResetTime)
                    _zoomAccelMult = 1.0f;
                else
                    _zoomAccelMult = Mathf.Min(_zoomAccelMult * ZoomAcceleration, ZoomAccelMax);
                _zoomLastScrollTime = now;

                float step = ZoomStep * _zoomAccelMult;
                if (Input.IsKeyPressed(Key.Shift))
                    step *= ZoomShiftMultiplier;

                if (mb.ButtonIndex == MouseButton.WheelUp)
                    _zoomTarget += step;  // WheelUp = 放大 = zoom 增大
                else
                    _zoomTarget -= step;  // WheelDown = 缩小 = zoom 减小
                ClampZoomTarget();
                _zoomTarget = QuantizeZoom(_zoomTarget);
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
            float factor = _zoomLevel * 1.5f;
            _panTarget -= delta * factor;
        }
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 设置地图边界（像素坐标）
    /// </summary>
    public void SetMapBounds(float mapWidthPixels, float mapHeightPixels)
    {
        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = (int)mapWidthPixels;
        LimitBottom = (int)mapHeightPixels;

        GD.Print($"[OverworldCamera2D] 边界设置: map={mapWidthPixels}×{mapHeightPixels}px");
    }

    /// <summary>
    /// 将相机焦点平滑移动到指定像素坐标
    /// </summary>
    public void FocusOn(Vector2 pixelPos)
    {
        _panTarget = pixelPos;
    }

    /// <summary>
    /// 将相机焦点瞬间跳变到指定像素坐标
    /// </summary>
    public void FocusOnImmediate(Vector2 pixelPos)
    {
        _panTarget = pixelPos;
        _panCurrent = pixelPos;
        Position = SnapCameraPosition(pixelPos);
    }

    // ========================================
    // 内部
    // ========================================

    private void ClampZoomTarget()
    {
        _zoomTarget = Mathf.Clamp(_zoomTarget, ZoomMin, ZoomMax);
    }

    private float QuantizeZoom(float zoom)
    {
        zoom = Mathf.Clamp(zoom, ZoomMin, ZoomMax);
        if (!QuantizeZoomTarget || ZoomStep <= 0.0f)
            return zoom;

        float quantized = Mathf.Round(zoom / ZoomStep) * ZoomStep;
        return Mathf.Clamp(quantized, ZoomMin, ZoomMax);
    }

    private Vector2 SnapCameraPosition(Vector2 worldPosition)
    {
        if (!SnapPositionToPixelGrid)
            return worldPosition;

        float zoom = Mathf.Max(Mathf.Abs(_zoomLevel), 0.001f);
        return new Vector2(
            Mathf.Round(worldPosition.X * zoom) / zoom,
            Mathf.Round(worldPosition.Y * zoom) / zoom);
    }
}
