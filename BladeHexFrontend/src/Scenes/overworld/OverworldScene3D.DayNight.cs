// OverworldScene3D.DayNight.cs
// 昼夜循环 — 此文件仅保留 partial 代理，实际实现在 Components/DayNightController.cs。
//
// 重构于 Sprint 6（架构优化 spec R5）：从大块 partial 抽出独立 Component。
// 主类内的字段和方法都退化为对 _dayNight controller 的转发。
using Godot;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 拥有的组件
    // ========================================

    private DayNightController? _dayNight;

    // ========================================
    // 主光照引用（仍由主类创建于 SetupLighting，由 controller 引用）
    // ========================================

    private DirectionalLight3D? _sunLight;
    private Godot.Environment? _worldEnv;

    // ========================================
    // 转发：保留旧 API 让其它 partial 编译通过
    // ========================================

    /// <summary>构建组件并注入依赖。由 SetupLighting 末尾调用。</summary>
    private void SetupDayNightCycle()
    {
        _dayNight = new DayNightController { Name = "DayNightController" };
        AddChild(_dayNight);
        if (_sunLight != null && _worldEnv != null && EconomyMgr != null)
            _dayNight.Initialize(_sunLight, _worldEnv, EconomyMgr);
    }

    /// <summary>每帧由主类 _Process 调用。</summary>
    private void UpdateDayNightCycle() => _dayNight?.Tick();

    // ========================================
    // 暴露给 Weather 叠加层（保持原字段语义，由 controller 提供）
    // ========================================

    private float _baseSunEnergy => _dayNight?.BaseSunEnergy ?? 0.9f;
    private float _baseAmbientEnergy => _dayNight?.BaseAmbientEnergy ?? 0.5f;
    private Color _baseSunColor => _dayNight?.BaseSunColor ?? Colors.White;
    private Color _baseAmbientColor => _dayNight?.BaseAmbientColor ?? new Color(0.65f, 0.65f, 0.70f);
}
