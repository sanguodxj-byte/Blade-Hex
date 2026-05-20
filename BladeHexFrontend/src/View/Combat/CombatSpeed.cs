// CombatSpeed.cs
// 战斗快进倍率服务 — 读写 GameSettings.CombatAnimSpeed,广播变更事件。
// 所有等待时间(AI 行动间、攻击挥砍、移动后)都通过 ScaledWait 走这个倍率。
using System;
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Combat;

/// <summary>
/// 战斗倍率服务。三档循环 1x / 2x / 4x,持久化到 GameSettings。
/// 任何战斗代码中需要"硬等待动画播放"的位置都应使用 <see cref="ScaledWait"/>,
/// 而非直接 `await ToSignal(GetTree().CreateTimer(x), ...)`。
/// </summary>
public static class CombatSpeed
{
    /// <summary>倍率档位</summary>
    public static readonly float[] Steps = { 1.0f, 2.0f, 4.0f };

    /// <summary>当前倍率变更事件(UI 用来刷新按钮文字)</summary>
    public static event Action<float>? MultiplierChanged;

    private static float _cached = -1f;

    /// <summary>当前倍率,默认 1.0;首次访问时从 GameSettings 加载</summary>
    public static float Multiplier
    {
        get
        {
            if (_cached < 0f)
            {
                var s = Globals.StateOrNull?.GetSettings();
                _cached = NormalizeStep(s?.CombatAnimSpeed ?? 1.0f);
            }
            return _cached;
        }
    }

    /// <summary>循环切换到下一档(1 → 2 → 4 → 1)</summary>
    public static float CycleNext()
    {
        float current = Multiplier;
        int idx = 0;
        for (int i = 0; i < Steps.Length; i++)
        {
            if (Mathf.IsEqualApprox(Steps[i], current)) { idx = i; break; }
        }
        idx = (idx + 1) % Steps.Length;
        SetMultiplier(Steps[idx]);
        return Steps[idx];
    }

    /// <summary>设置倍率并持久化</summary>
    public static void SetMultiplier(float m)
    {
        m = NormalizeStep(m);
        _cached = m;
        var state = Globals.StateOrNull;
        if (state != null)
        {
            var s = state.GetSettings();
            s.CombatAnimSpeed = m;
            state.ApplySettings(s);
        }
        MultiplierChanged?.Invoke(m);
    }

    /// <summary>根据倍率缩放等待时间。倍率 2x → 等待时间减半。</summary>
    public static double ScaleSeconds(double seconds)
        => seconds / Mathf.Max(0.01f, Multiplier);

    /// <summary>异步等待指定秒数(已按倍率缩放)。替代 `CreateTimer(x)` 的标准用法。</summary>
    public static SignalAwaiter ScaledWait(Node ctx, double seconds)
    {
        double scaled = ScaleSeconds(seconds);
        return ctx.ToSignal(ctx.GetTree().CreateTimer(scaled), SceneTreeTimer.SignalName.Timeout);
    }

    /// <summary>把任意 float 倍率吸附到 Steps 中最近的档位(用于反序列化兜底)</summary>
    private static float NormalizeStep(float m)
    {
        float best = Steps[0];
        float bestDiff = Mathf.Abs(m - best);
        for (int i = 1; i < Steps.Length; i++)
        {
            float diff = Mathf.Abs(m - Steps[i]);
            if (diff < bestDiff) { bestDiff = diff; best = Steps[i]; }
        }
        return best;
    }
}
