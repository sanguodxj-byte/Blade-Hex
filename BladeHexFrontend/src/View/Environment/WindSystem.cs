// WindSystem.cs
// 全局风力系统 — 提供统一的风向/风速参数，驱动云层飘动和树木摇摆
// 通过 shader global uniform 将风力数据传递给所有订阅的 shader
using Godot;

namespace BladeHex.View.Environment;

/// <summary>
/// 全局风力系统 — 管理风向、风速、阵风，并通过 shader globals 驱动视觉效果。
/// 
/// 设计：
/// - 风力参数通过 RenderingServer.GlobalShaderParameterSet 传递
/// - 任何 shader 只需声明 global uniform 即可读取风力数据
/// - 云层粒子的 ProcessMaterial 由本系统每帧更新方向/速度
/// - 树木/草地 shader 读取 global uniform 实现摇摆
/// 
/// Shader Global Uniforms（在 project.godot 或代码中注册）：
/// - wind_direction: vec2 — 风向（归一化 XZ 平面方向）
/// - wind_strength: float — 风力强度 [0, 1]
/// - wind_time: float — 累计时间（用于 shader 动画）
/// - wind_gust: float — 阵风强度 [0, 1]（周期性波动）
/// </summary>
[GlobalClass]
public partial class WindSystem : Node
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>基础风向角度（弧度，0=东，PI/2=北）</summary>
    [Export] public float BaseWindAngle { get; set; } = 0.3f;

    /// <summary>基础风速 [0, 1]</summary>
    [Export] public float BaseWindStrength { get; set; } = 0.4f;

    /// <summary>阵风周期（秒）</summary>
    [Export] public float GustPeriod { get; set; } = 8.0f;

    /// <summary>阵风强度变化幅度</summary>
    [Export] public float GustAmplitude { get; set; } = 0.3f;

    /// <summary>风向缓慢漂移速度（弧度/秒）</summary>
    [Export] public float DirectionDriftSpeed { get; set; } = 0.02f;

    // ========================================
    // 运行时状态（只读）
    // ========================================

    /// <summary>当前风向（归一化 XZ 向量）</summary>
    public Vector2 CurrentDirection { get; private set; } = new(1, 0);

    /// <summary>当前风力强度 [0, 1]</summary>
    public float CurrentStrength { get; private set; } = 0.4f;

    /// <summary>当前阵风值 [0, 1]</summary>
    public float CurrentGust { get; private set; } = 0.0f;

    /// <summary>累计风时间</summary>
    public float WindTime { get; private set; } = 0.0f;

    // ========================================
    // 内部
    // ========================================

    private float _currentAngle;
    private float _driftNoise;
    private RandomNumberGenerator _rng = new();

    // 云层引用（由外部注入）
    private CloudLayer3D? _cloudLayer;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        _currentAngle = BaseWindAngle;
        _rng.Randomize();
        _driftNoise = _rng.Randf() * 100.0f;

        // 注册 shader global uniforms
        RegisterGlobalUniforms();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        WindTime += dt;

        // 风向缓慢漂移（用正弦模拟自然变化）
        _driftNoise += dt * 0.1f;
        float drift = Mathf.Sin(_driftNoise * 0.7f) * DirectionDriftSpeed * dt * 60.0f;
        _currentAngle += drift;
        CurrentDirection = new Vector2(Mathf.Cos(_currentAngle), Mathf.Sin(_currentAngle));

        // 阵风（周期性强度波动）
        float gustPhase = WindTime / GustPeriod * Mathf.Tau;
        float gustBase = (Mathf.Sin(gustPhase) + 1.0f) * 0.5f;
        // 叠加高频扰动让阵风不那么规律
        float gustDetail = Mathf.Sin(gustPhase * 3.7f + 1.3f) * 0.3f;
        CurrentGust = Mathf.Clamp(gustBase + gustDetail, 0.0f, 1.0f) * GustAmplitude;

        // 最终风力 = 基础 + 阵风
        CurrentStrength = Mathf.Clamp(BaseWindStrength + CurrentGust, 0.0f, 1.0f);

        // 更新 shader globals
        UpdateGlobalUniforms();

        // 更新云层粒子运动
        UpdateCloudParticles(dt);
    }

    // ========================================
    // Shader Global Uniforms
    // ========================================

    private static bool _globalsRegistered = false;

    private void RegisterGlobalUniforms()
    {
        if (_globalsRegistered)
        {
            // 已注册过（可能从战斗场景返回），只更新值
            UpdateGlobalUniforms();
            return;
        }

        RenderingServer.GlobalShaderParameterAdd("wind_direction", RenderingServer.GlobalShaderParameterType.Vec2, CurrentDirection);
        RenderingServer.GlobalShaderParameterAdd("wind_strength", RenderingServer.GlobalShaderParameterType.Float, CurrentStrength);
        RenderingServer.GlobalShaderParameterAdd("wind_time", RenderingServer.GlobalShaderParameterType.Float, WindTime);
        RenderingServer.GlobalShaderParameterAdd("wind_gust", RenderingServer.GlobalShaderParameterType.Float, CurrentGust);

        _globalsRegistered = true;
    }

    private void UpdateGlobalUniforms()
    {
        RenderingServer.GlobalShaderParameterSet("wind_direction", CurrentDirection);
        RenderingServer.GlobalShaderParameterSet("wind_strength", CurrentStrength);
        RenderingServer.GlobalShaderParameterSet("wind_time", WindTime);
        RenderingServer.GlobalShaderParameterSet("wind_gust", CurrentGust);
    }

    // ========================================
    // 云层驱动
    // ========================================

    /// <summary>注入云层引用（由场景初始化时调用）</summary>
    public void RegisterCloudLayer(CloudLayer3D cloudLayer)
    {
        _cloudLayer = cloudLayer;
    }

    private void UpdateCloudParticles(float dt)
    {
        if (_cloudLayer == null) return;
        _cloudLayer.SetWind(CurrentDirection, CurrentStrength);
        _cloudLayer.ApplyWind(dt);
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>设置风力（天气联动）</summary>
    public void SetWind(float angle, float strength)
    {
        BaseWindAngle = angle;
        _currentAngle = angle;
        BaseWindStrength = strength;
    }

    /// <summary>设置阵风参数</summary>
    public void SetGust(float amplitude, float period)
    {
        GustAmplitude = amplitude;
        GustPeriod = period;
    }

    /// <summary>获取当前风力向量（世界空间 XZ）</summary>
    public Vector3 GetWindVector3D()
    {
        return new Vector3(CurrentDirection.X * CurrentStrength, 0, CurrentDirection.Y * CurrentStrength);
    }
}
