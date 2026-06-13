// AIIntentPipelineObserver.cs
// AI 意图管线可观测器 — 为 TickFrame/TickHours 每一步提供结构化日志
//
// 设计目标:
//   - 明确 AI 状态转换顺序: Perception → Intent → Intent Apply → Path Refresh → Movement → Contact Engage
//   - 每一步都有可观测日志
//   - 测试可验证每一步的执行
//   - Idle 敌对实体在视野内可通过 TickFrame 自动进入移动
//   - Chasing/Fleeing 的路径刷新失败时不会立即清掉已有有效路径
//   - MovingToTarget、Returning、Reinforcing 不被感知逻辑错误覆盖
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>AI 管线步骤</summary>
public enum PipelineStep
{
    /// <summary>帧级战术感知：持续刷新追击/逃跑路径</summary>
    FramePerception,
    /// <summary>每小时意图判定：扫描威胁 → 设定追/逃意图</summary>
    HourlyIntent,
    /// <summary>意图应用：将意图转化为移动目标</summary>
    IntentApply,
    /// <summary>路径刷新：为有目标的实体计算路径</summary>
    PathRefresh,
    /// <summary>移动推进：沿路径推进实体位置</summary>
    Movement,
    /// <summary>接触交战：实体物理接触检测</summary>
    ContactEngage,
}

/// <summary>管线步骤执行结果</summary>
public readonly struct PipelineStepResult
{
    public PipelineStep Step { get; }
    public int EntitiesProcessed { get; }
    public int StateTransitions { get; }
    public int PathFailures { get; }
    public float ElapsedMs { get; }
    public string? Note { get; }

    public PipelineStepResult(PipelineStep step, int processed, int transitions, int pathFailures, float elapsedMs, string? note = null)
    {
        Step = step;
        EntitiesProcessed = processed;
        StateTransitions = transitions;
        PathFailures = pathFailures;
        ElapsedMs = elapsedMs;
        Note = note;
    }
}

/// <summary>
/// AI 意图管线可观测器。
///
/// 用法:
///   var observer = new AIIntentPipelineObserver();
///   // 在 TickFrame 中:
///   observer.BeginFrame(gameHour);
///   // ... 每个步骤后:
///   observer.RecordStep(PipelineStep.FramePerception, entities.Count, transitions, pathFails, elapsedMs);
///   // ... 帧结束时:
///   var summary = observer.EndFrame();
/// </summary>
public sealed class AIIntentPipelineObserver
{
    private readonly List<PipelineStepResult> _currentFrameResults = new();
    private float _frameGameHour;
    private int _frameCount;

    /// <summary>开始新的一帧</summary>
    public void BeginFrame(float gameHour)
    {
        _currentFrameResults.Clear();
        _frameGameHour = gameHour;
        _frameCount++;
    }

    /// <summary>记录一个步骤的执行结果</summary>
    public void RecordStep(PipelineStep step, int processed, int transitions, int pathFailures, float elapsedMs, string? note = null)
    {
        var result = new PipelineStepResult(step, processed, transitions, pathFailures, elapsedMs, note);
        _currentFrameResults.Add(result);

        // 只在有显著事件时输出日志（避免空闲刷屏）
        if (transitions > 0 || pathFailures > 0)
        {
            OverworldDiagnostics.LogThrottled(
                OverworldDiagnostics.PrefixAI,
                $"pipeline_{step}",
                $"step={step}, processed={processed}, transitions={transitions}, " +
                $"pathFails={pathFailures}, ms={elapsedMs:F1}" +
                (note != null ? $", note={note}" : ""),
                cooldownMs: 3000);
        }
    }

    /// <summary>结束当前帧，返回所有步骤结果</summary>
    public IReadOnlyList<PipelineStepResult> EndFrame()
    {
        return _currentFrameResults.AsReadOnly();
    }

    /// <summary>获取上一帧的摘要文本（用于诊断）</summary>
    public string GetLastFrameSummary()
    {
        if (_currentFrameResults.Count == 0)
            return $"frame={_frameCount}, hour={_frameGameHour:F1}, steps=0";

        var parts = new List<string>();
        foreach (var r in _currentFrameResults)
        {
            parts.Add($"{r.Step}(n={r.EntitiesProcessed},tr={r.StateTransitions},pf={r.PathFailures})");
        }
        return $"frame={_frameCount}, hour={_frameGameHour:F1}, steps: {string.Join(" -> ", parts)}";
    }

    /// <summary>总帧数</summary>
    public int FrameCount => _frameCount;

    // ========================================
    // 状态完整性验证（测试用）
    // ========================================

    /// <summary>
    /// 验证 AI 状态转换的一致性 — 供测试调用。
    /// 检查 MovingToTarget/Returning/Reinforcing 不被感知逻辑覆盖。
    /// </summary>
    public static List<string> ValidateStateConsistency(List<OverworldEntity> entities)
    {
        var issues = new List<string>();

        foreach (var e in entities)
        {
            if (!e.IsAlive) continue;

            // 检查: MovingToTarget/Returning/Reinforcing 不应同时有 ChaseTarget
            if ((e.CurrentAIState == OverworldEntity.AIState.MovingToTarget ||
                 e.CurrentAIState == OverworldEntity.AIState.Returning ||
                 e.CurrentAIState == OverworldEntity.AIState.Reinforcing)
                && e.ChaseTarget != null)
            {
                issues.Add($"{e.EntityName}: state={e.CurrentAIState} but has ChaseTarget={e.ChaseTarget.EntityName}");
            }

            // 检查: Engaged 实体应有 EngagedWith
            if (e.CurrentAIState == OverworldEntity.AIState.Engaged && e.EngagedWith == null)
            {
                issues.Add($"{e.EntityName}: Engaged but no EngagedWith");
            }

            // 检查: Besieging 实体应有 SiegeTarget
            if (e.CurrentAIState == OverworldEntity.AIState.Besieging && e.SiegeTarget == null)
            {
                issues.Add($"{e.EntityName}: Besieging but no SiegeTarget");
            }

            // 检查: Chasing/Fleeing 实体应正在移动或有路径
            if ((e.CurrentAIState == OverworldEntity.AIState.Chasing ||
                 e.CurrentAIState == OverworldEntity.AIState.Fleeing)
                && !e.IsMoving && e.Path.Count == 0)
            {
                issues.Add($"{e.EntityName}: {e.CurrentAIState} but not moving and no path");
            }
        }

        return issues;
    }
}
