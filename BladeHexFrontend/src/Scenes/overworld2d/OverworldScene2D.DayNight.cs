// OverworldScene2D.DayNight.cs
// 昼夜循环 — 从 OverworldScene3D.DayNight.cs 迁移
// 使用 CanvasModulate 替代 DirectionalLight3D + WorldEnvironment
// 夜间光照：全局 shader 后处理 + POI/实体/玩家光源数组
//
// 时间驱动：现实系统时钟（DateTime.Now），非游戏内时间
// 曲线模型：day_factor = sin(π·hour/24)^1.5
//   正午(12:00) = 1.0, 子夜(00:00) = 0.0
//   全日 ≈ 08:30–15:30, 全夜 ≈ 20:15–03:45
// Uses real-world time for the day/night color curve.
using System;
using Godot;
using BladeHex.Scenes.Overworld.Components;
using BladeHex.Scenes.Overworld2d.Components;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // ========================================
    // 持有的组件
    // ========================================

    private DayNightController2D? _dayNight;
    private NightLightingController2D? _nightLighting;

    // ========================================
    // 2D 光照引用
    // ========================================

    private CanvasModulate? _canvasModulate;

    // ========================================
    // ========================================
    // 初始化
    // ========================================

    private void SetupDayNightCycle()
    {
        // 创建 CanvasModulate 用于全局色调控制
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Name = "DayNightModulate";
        AddChild(_canvasModulate);

        // 创建 2D 版昼夜控制器
        _dayNight = new DayNightController2D { Name = "DayNightController2D" };
        AddChild(_dayNight);
        _dayNight.Initialize(_canvasModulate, _renderer?.GroundMaterial);

        SetupNightLighting();
    }

    private void SetupNightLighting()
    {
        _nightLighting = new NightLightingController2D { Name = "NightLightingController2D" };
        AddChild(_nightLighting);
        _nightLighting.Initialize(
            worldPois: WorldPois,
            entityMgr: EntityMgr,
            getPlayerPos: () => _playerPixelPos,
            getPlayerPartySize: () => PlayerParty?.Roster?.Count ?? 1,
            getHour: () => EconomyMgr?.CurrentHour ?? GetSystemHour(),
            fog: _fog,
            camera: _camera
        );
    }

    // ========================================
    // 每帧更新
    // ========================================

    private void UpdateDayNightCycle()
    {
        _dayNight?.Tick(EconomyMgr?.CurrentHour ?? GetSystemHour());
    }

    private void UpdateNightLighting()
    {
        _nightLighting?.Tick();
    }

    private static float GetSystemHour()
    {
        var now = DateTime.Now;
        return now.Hour + now.Minute / 60f + now.Second / 3600f;
    }
}

/// <summary>
/// 2D 版昼夜控制器 — 使用 CanvasModulate + 现实时间驱动
/// 平滑正弦曲线，日出暖色 / 日落冷色非对称过渡
/// </summary>
public partial class DayNightController2D : Node
{
    private CanvasModulate? _modulate;
    private ShaderMaterial? _groundMaterial;

    // —— 调色板 ——
    private static readonly Color DayWhite = new(1.0f, 1.0f, 1.0f);
    private static readonly Color NightBlue = new(0.40f, 0.40f, 0.60f);
    private static readonly Color SunsetWarm = new(1.0f, 0.78f, 0.52f);
    private static readonly Color DawnCool = new(0.82f, 0.78f, 0.92f);
    public void Initialize(CanvasModulate modulate, ShaderMaterial? groundMaterial = null)
    {
        _modulate = modulate;
        _groundMaterial = groundMaterial;
    }

    public void Tick(float hour)
    {
        if (_modulate == null) return;

        hour = NormalizeHour(hour);
        Color color = GetTimeOfDayColor(hour);
        _modulate.Color = color;
        _groundMaterial?.SetShaderParameter("day_night_tint", color);
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float NormalizeHour(float hour)
    {
        hour %= 24.0f;
        return hour < 0.0f ? hour + 24.0f : hour;
    }

    private static Color GetTimeOfDayColor(float hour)
    {
        if (hour < 4.0f || hour >= 20.0f)
            return NightBlue;

        if (hour < 6.0f)
        {
            float dawn = SmoothStep(4.0f, 6.0f, hour);
            float cool = 1.0f - MathF.Abs(dawn * 2.0f - 1.0f);
            Color baseColor = NightBlue.Lerp(DayWhite, dawn);
            return baseColor.Lerp(DawnCool, cool * 0.55f);
        }

        if (hour < 18.0f)
            return DayWhite;

        float dusk = SmoothStep(18.0f, 20.0f, hour);
        float warm = 1.0f - MathF.Abs(dusk * 2.0f - 1.0f);
        Color duskBase = DayWhite.Lerp(NightBlue, dusk);
        return duskBase.Lerp(SunsetWarm, warm * 0.50f);
    }
}
