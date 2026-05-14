// OverworldCamera3D.cs
// 3D 大地图固定角度正交相机 — 只允许平移和缩放，不允许旋转
using Godot;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图 3D 相机控制器
/// 正交投影，固定俯角（约 35°），支持平移和缩放
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

    /// <summary>缩放范围</summary>
    [Export] public float ZoomMin = 0.3f;
    [Export] public float ZoomMax = 4.0f;
    [Export] public float ZoomStep = 0.08f;
    [Export] public float ZoomSmooth = 8.0f;

    /// <summary>平移速度（单位/秒，会随缩放缩放）</summary>
    [Export] public float PanSpeed = 15.0f;

    /// <summary>平移平滑系数</summary>
    [Export] public float PanSmooth = 6.0f;

    // ========================================
    // 运行时状态
    // ========================================

    private float _zoomLevel = 1.0f;
    private float _zoomTarget = 1.0f;
    private Vector3 _panTarget = Vector3.Zero;
    private Vector3 _panCurrent = Vector3.Zero;
    private bool _isDragging = false;
    private Vector2 _dragStart;

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

        // 应用
        Size = BaseOrthoSize * _zoomLevel;
        float rad = Mathf.DegToRad(PitchAngle);
        Position = _panCurrent + new Vector3(0, Distance * Mathf.Sin(rad), Distance * Mathf.Cos(rad));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // 滚轮缩放
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                _zoomTarget = Mathf.Max(ZoomMin, _zoomTarget - ZoomStep);
            else if (mb.ButtonIndex == MouseButton.WheelDown)
                _zoomTarget = Mathf.Min(ZoomMax, _zoomTarget + ZoomStep);
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
}
