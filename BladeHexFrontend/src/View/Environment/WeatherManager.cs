// WeatherManager.cs
// 天气管理器 — 控制天气状态、过渡动画、地面特效联动
// 集成到 OverworldScene，根据季节/地形/时间驱动天气变化
using Godot;
using System;

namespace BladeHex.View.Environment;

/// <summary>
/// 天气管理器：管理天气状态机、过渡动画、shader uniform 更新。
/// 作为 Node 添加到 OverworldScene 场景树中。
/// </summary>
[GlobalClass]
public partial class WeatherManager : Node
{
    // ========================================
    // 信号
    // ========================================

    /// <summary>天气变化时发出（旧天气, 新天气）</summary>
    [Signal]
    public delegate void WeatherChangedEventHandler(int oldWeather, int newWeather);

    // ========================================
    // 导出参数
    // ========================================

    /// <summary>天气过渡时长（秒）</summary>
    [Export] public float TransitionDuration { get; set; } = 2.0f;

    /// <summary>自动天气变化的最小间隔（游戏小时）</summary>
    [Export] public float MinWeatherDurationHours { get; set; } = 12.0f;

    /// <summary>自动天气变化的最大间隔（游戏小时）</summary>
    [Export] public float MaxWeatherDurationHours { get; set; } = 36.0f;

    /// <summary>是否启用自动天气循环</summary>
    [Export] public bool AutoCycleEnabled { get; set; } = true;

    // ========================================
    // 状态
    // ========================================

    /// <summary>当前天气类型</summary>
    public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;

    /// <summary>当前天气强度</summary>
    public WeatherIntensity CurrentIntensity { get; private set; } = WeatherIntensity.Moderate;

    /// <summary>当前过渡进度 [0, 1]，1 = 完全生效</summary>
    public float TransitionProgress { get; private set; } = 0.0f;

    /// <summary>是否正在过渡中</summary>
    public bool IsTransitioning { get; private set; }

    // ========================================
    // 内部字段
    // ========================================

    private WeatherType _targetWeather = WeatherType.Clear;
    private float _transitionTimer;
    private float _nextChangeTimer; // 距离下次自动天气变化的剩余时间（秒）
    private RandomNumberGenerator _rng = new();

    // 季节权重表：每个季节各天气的概率权重
    // [Clear, Rain, Snow, Sandstorm]
    // 设计原则：大部分时间晴天，雨天占少部分，雪只在冬季+雪地出现，沙尘暴罕见
    private static readonly float[,] SeasonWeights = new float[4, 4]
    {
        // Spring: 以晴天为主，偶尔下雨
        { 0.75f, 0.22f, 0.0f, 0.03f },
        // Summer: 大部分晴天，少量雷阵雨，极罕见沙尘暴
        { 0.82f, 0.15f, 0.0f, 0.03f },
        // Fall: 晴天为主，秋雨稍多
        { 0.70f, 0.27f, 0.0f, 0.03f },
        // Winter: 晴天为主，少量雨雪（雪需要地形配合才生效）
        { 0.65f, 0.15f, 0.17f, 0.03f },
    };

    /// <summary>当前玩家所在地形是否为雪地/冰原类型（由外部设置）</summary>
    public bool IsInSnowTerrain { get; set; } = false;

    /// <summary>当前玩家所在地形是否为沙漠/荒原类型（由外部设置）</summary>
    public bool IsInDesertTerrain { get; set; } = false;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        _rng.Randomize();
        ScheduleNextChange();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // 过渡动画（使用 SmoothStep 缓动曲线）
        if (IsTransitioning)
        {
            _transitionTimer += dt;
            float linear = Mathf.Clamp(_transitionTimer / TransitionDuration, 0.0f, 1.0f);
            TransitionProgress = Mathf.SmoothStep(0.0f, 1.0f, linear);

            if (linear >= 1.0f)
            {
                IsTransitioning = false;
                CurrentWeather = _targetWeather;
                TransitionProgress = 1.0f;
            }
        }
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 立即设置天气（无过渡）
    /// </summary>
    public void SetWeatherImmediate(WeatherType weather, WeatherIntensity intensity = WeatherIntensity.Moderate)
    {
        var old = CurrentWeather;
        CurrentWeather = weather;
        _targetWeather = weather;
        CurrentIntensity = intensity;
        TransitionProgress = weather == WeatherType.Clear ? 0.0f : 1.0f;
        IsTransitioning = false;

        EmitSignal(SignalName.WeatherChanged, (int)old, (int)weather);
    }

    /// <summary>
    /// 平滑过渡到新天气
    /// </summary>
    public void TransitionTo(WeatherType weather, WeatherIntensity intensity = WeatherIntensity.Moderate)
    {
        if (weather == _targetWeather && !IsTransitioning)
            return;

        var old = CurrentWeather;
        _targetWeather = weather;
        CurrentIntensity = intensity;
        _transitionTimer = 0.0f;
        IsTransitioning = true;

        // 如果从有天气过渡到晴天，反向过渡（intensity 从 1→0）
        if (weather == WeatherType.Clear)
        {
            TransitionProgress = 1.0f; // 将从 1 降到 0
        }
        else
        {
            TransitionProgress = 0.0f;
        }

        EmitSignal(SignalName.WeatherChanged, (int)old, (int)weather);
    }

    /// <summary>
    /// 获取当前有效强度值 [0, 1]（考虑过渡动画）
    /// </summary>
    public float GetEffectiveIntensity()
    {
        float baseIntensity = CurrentIntensity switch
        {
            WeatherIntensity.Light => 0.4f,
            WeatherIntensity.Moderate => 0.7f,
            WeatherIntensity.Heavy => 1.0f,
            _ => 0.7f,
        };

        if (_targetWeather == WeatherType.Clear)
        {
            // 渐出：从 baseIntensity → 0
            return baseIntensity * (1.0f - TransitionProgress);
        }

        // 渐入：从 0 → baseIntensity
        return baseIntensity * TransitionProgress;
    }

    /// <summary>
    /// 获取当前应显示的天气类型（过渡期间返回目标天气）
    /// </summary>
    public WeatherType GetActiveWeatherType()
    {
        if (IsTransitioning)
            return _targetWeather == WeatherType.Clear ? CurrentWeather : _targetWeather;
        return CurrentWeather;
    }

    /// <summary>
    /// 根据季节触发自动天气变化。
    /// 由外部（如 EconomyManager 的每小时回调）调用。
    /// </summary>
    /// <param name="season">当前季节 (0=Spring, 1=Summer, 2=Fall, 3=Winter)</param>
    /// <param name="elapsedHours">自上次调用经过的游戏小时数</param>
    public void TickWeatherCycle(int season, float elapsedHours)
    {
        if (!AutoCycleEnabled) return;
        if (IsTransitioning) return; // 过渡中不触发新天气变化

        _nextChangeTimer -= elapsedHours;
        if (_nextChangeTimer <= 0.0f)
        {
            RollNewWeather(season);
            ScheduleNextChange();
        }
    }

    // ========================================
    // 内部方法
    // ========================================

    private void RollNewWeather(int season)
    {
        season = Mathf.Clamp(season, 0, 3);

        // 构建有效权重（根据地形过滤不合理的天气）
        float wClear = SeasonWeights[season, 0];
        float wRain = SeasonWeights[season, 1];
        float wSnow = SeasonWeights[season, 2];
        float wSand = SeasonWeights[season, 3];

        // 天气惯性：当前是雨/雪/沙尘暴时，有较高概率维持当前天气
        // 这样雨天会连续下一段时间，而不是下一小会就停
        if (CurrentWeather == WeatherType.Rain)
        {
            wRain += 0.35f; // 雨天→雨天额外 +35% 权重
        }
        else if (CurrentWeather == WeatherType.Snow)
        {
            wSnow += 0.30f;
        }
        else if (CurrentWeather == WeatherType.Sandstorm)
        {
            wSand += 0.25f;
        }

        // 雪：只有冬季 + 雪地/冰原/针叶林地形才允许下雪
        if (!IsInSnowTerrain || season != 3)
        {
            wClear += wSnow; // 雪的概率归还给晴天
            wSnow = 0.0f;
        }

        // 沙尘暴：只有沙漠/荒原地形才允许
        if (!IsInDesertTerrain)
        {
            wClear += wSand;
            wSand = 0.0f;
        }

        // 归一化
        float total = wClear + wRain + wSnow + wSand;
        if (total <= 0.0f) { TransitionTo(WeatherType.Clear); return; }

        wClear /= total;
        wRain /= total;
        wSnow /= total;
        wSand /= total;

        // 加权随机选择
        float roll = _rng.Randf();
        float cumulative = 0.0f;

        WeatherType[] candidates = { WeatherType.Clear, WeatherType.Rain, WeatherType.Snow, WeatherType.Sandstorm };
        float[] weights = { wClear, wRain, wSnow, wSand };

        WeatherType chosen = WeatherType.Clear;
        for (int i = 0; i < 4; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                chosen = candidates[i];
                break;
            }
        }

        // 随机强度（偏向轻度和中度，重度较少）
        var intensityRoll = _rng.Randf();
        WeatherIntensity chosenIntensity = intensityRoll < 0.45f
            ? WeatherIntensity.Light
            : intensityRoll < 0.85f
                ? WeatherIntensity.Moderate
                : WeatherIntensity.Heavy;

        TransitionTo(chosen, chosenIntensity);
    }

    private void ScheduleNextChange()
    {
        _nextChangeTimer = _rng.RandfRange(MinWeatherDurationHours, MaxWeatherDurationHours);
    }
}
