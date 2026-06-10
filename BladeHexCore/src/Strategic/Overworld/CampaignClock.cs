// CampaignClock.cs
// 统一大地图时钟 — 集中计算时间推进逻辑
//
// 设计目标:
//   - 替代 OverworldScene2D._Process 中散落的 shouldTimePass 逻辑
//   - Simulation 层不依赖玩家是否移动；玩家只影响时间倍率、命令和视野
//   - 玩家不动但 AI 正在战斗时，战斗时间推进
//   - 玩家不动但 AI 没有活动时，经济/食物不错误消耗
//   - Space 暂停能同时暂停 AI tick 和战斗结算
//   - 可单测，不依赖 Godot 场景树
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 统一大地图时钟。
/// 
/// 管线位置:
///   CampaignClock.Tick() → deltaHours → OverworldSimulation.TickHours/TickFrame → 经济/食物
/// 
/// 使用方式:
///   var clock = new CampaignClock();
///   // 每帧:
///   clock.IsPaused = spacePressed;
///   clock.PlayerMoving = isMoving;
///   clock.PlayerWaiting = isWaiting;
///   clock.AISimulationActive = simulation.HasActiveMotion();
///   var result = clock.Tick(deltaTime);
///   if (result.ShouldAdvanceHours) { ... }
///   if (result.ShouldAdvanceFrame) { ... }
/// 
/// ────────────────────────────────────────────────────────────────
/// Pause &amp; Idle Semantics — 5 Rules
/// ────────────────────────────────────────────────────────────────
/// 
/// Rule 1 — Explicit Pause (IsPaused = true):
///   The player pressed the pause button (e.g. Space). EVERYTHING stops:
///   world time, AI ticks, combat resolution, frame advancement.
///   This is the ONLY state that halts AI processing.
///   Returns TickResult.Paused (DeltaHours=0, ShouldAdvanceFrame=false).
/// 
/// Rule 2 — Player Moving (PlayerMoving = true):
///   The player is actively travelling on the overworld map.
///   World time advances at BaseGameTimeScale. PlayerTravelDeltaHours
///   equals DeltaHours (food is consumed for travel).
/// 
/// Rule 3 — Player Waiting (PlayerWaiting = true):
///   The player is holding the wait key (accelerated time).
///   World time advances at BaseGameTimeScale * WaitMultiplier.
///   PlayerTravelDeltaHours equals DeltaHours (food is still consumed).
/// 
/// Rule 4 — Player Idle + AI Active (AISimulationActive = true):
///   The player is stationary on the map (NOT explicitly paused) and AI
///   entities have active motion (combat, chasing, patrolling, etc.).
///   World time advances at BaseGameTimeScale so that AI battles and
///   movement resolve in real time. However, PlayerTravelDeltaHours = 0
///   because the player is not travelling — no food is consumed.
///   KEY DISTINCTION: "player idle" means the player has no movement or
///   wait input; it does NOT mean the game is paused. IsPaused is false.
/// 
/// Rule 5 — Player Idle + AI Inactive (no flags set):
///   The player is stationary and no AI activity is occurring.
///   World hours do NOT advance (DeltaHours=0, ShouldAdvanceHours=false)
///   to prevent spurious food/economy consumption while nothing is
///   happening. Frame-level logic still runs (ShouldAdvanceFrame=true)
///   so that perception, path refresh, and contact detection continue.
///   Returns TickResult.Idle.
///   NOTE: This is functionally similar to pause for time progression,
///   but differs from IsPaused in that frame-level systems keep ticking
///   and AI is NOT frozen — it simply has nothing active to do.
/// 
/// Summary Table:
///   ┌─────────────────────┬────────────┬──────────────┬───────────────┬──────────────────┐
///   │ State               │ DeltaHours │ TravelDelta  │ AdvanceHours  │ AdvanceFrame     │
///   ├─────────────────────┼────────────┼──────────────┼───────────────┼──────────────────┤
///   │ Paused (IsPaused)   │     0      │      0       │     false     │      false       │
///   │ Player Moving       │   base     │    base      │     true      │      true        │
///   │ Player Waiting      │ base*multi │ base*multi   │     true      │      true        │
///   │ Idle + AI Active    │   base     │      0       │     true      │      true        │
///   │ Idle + AI Inactive  │     0      │      0       │     false     │      true        │
///   └─────────────────────┴────────────┴──────────────┴───────────────┴──────────────────┘
/// </summary>
public sealed class CampaignClock
{
    // ========================================
    // 配置常量
    // ========================================

    /// <summary>基础时间缩放（游戏小时/真实秒）</summary>
    public float BaseGameTimeScale { get; set; } = 0.5f;

    /// <summary>等待时的时间加速倍率</summary>
    public float WaitMultiplier { get; set; } = 8.0f;

    /// <summary>
    /// 暂停状态 — 由玩家显式触发（如按 Space 键）。
    /// 停止所有时间推进，包括 AI tick 和战斗结算。
    /// 注意: 这与 "玩家空闲"（所有标志均为 false）不同；
    /// 玩家空闲时帧级逻辑仍在运行，且 AI 可随时恢复活动。
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>玩家是否正在移动</summary>
    public bool PlayerMoving { get; set; }

    /// <summary>玩家是否处于等待状态（按住等待键）</summary>
    public bool PlayerWaiting { get; set; }

    /// <summary>
    /// AI 模拟是否有活跃活动（移动中、追逃中、交战中等）。
    /// 由外部（OverworldEntityManager 或 Simulation）每帧写入。
    /// </summary>
    public bool AISimulationActive { get; set; }

    /// <summary>累计游戏小时数（只读查询）</summary>
    public float TotalGameHours { get; private set; }

    /// <summary>当前游戏天数（从上下文同步）</summary>
    public int CurrentDay { get; set; } = 1;

    // ========================================
    // Tick 结果
    // ========================================

    /// <summary>单次 Tick 的计算结果</summary>
    public readonly struct TickResult
    {
        /// <summary>本帧的游戏小时增量（0 = 不推进）</summary>
        public float DeltaHours { get; }

        /// <summary>
        /// 本帧的玩家旅行小时增量（仅玩家移动/等待时 > 0）。
        /// 用于旅行食物消耗计算，AI 活跃但玩家静止时为 0。
        /// </summary>
        public float PlayerTravelDeltaHours { get; }

        /// <summary>是否应推进小时级逻辑（战斗结算、意图、经济）</summary>
        public bool ShouldAdvanceHours { get; }

        /// <summary>是否应推进帧级逻辑（移动、感知、接触检测）— 始终 true 除非暂停</summary>
        public bool ShouldAdvanceFrame { get; }

        /// <summary>时间推进原因（用于诊断日志）</summary>
        public string Reason { get; }

        public TickResult(float deltaHours, float playerTravelDeltaHours, bool shouldAdvanceHours, bool shouldAdvanceFrame, string reason)
        {
            DeltaHours = deltaHours;
            PlayerTravelDeltaHours = playerTravelDeltaHours;
            ShouldAdvanceHours = shouldAdvanceHours;
            ShouldAdvanceFrame = shouldAdvanceFrame;
            Reason = reason;
        }

        /// <summary>空结果（暂停或无活动）</summary>
        public static readonly TickResult Paused = new(0f, 0f, false, false, "paused");
        public static readonly TickResult Idle = new(0f, 0f, false, true, "idle");
    }

    // ========================================
    // 核心计算
    // ========================================

    /// <summary>
    /// 每帧调用，计算本帧的时间推进量。
    /// 
    /// 规则:
    ///   1. 暂停 → 全部停止（包括 AI tick 和战斗结算）
    ///   2. 玩家移动 → 推进时间（基础倍率）
    ///   3. 玩家等待 → 推进时间（加速倍率）
    ///   4. 玩家不动但 AI 活跃 → 推进时间（基础倍率，AI 战斗/行军需要时间）
    ///   5. 玩家不动且 AI 不活跃 → 不推进小时（但帧级逻辑仍然跑）
    /// </summary>
    public TickResult Tick(float realDeltaSeconds)
    {
        // 规则 1: Explicit pause (player pressed Space / pause button).
        // This is the ONLY state that stops AI ticks and combat resolution.
        // Distinct from "player idle" (Rule 5) — idle does NOT freeze AI or frame logic.
        if (IsPaused)
        {
            return TickResult.Paused;
        }

        // 规则 2: 玩家移动 — 世界推进 + 旅行推进
        if (PlayerMoving)
        {
            float deltaHours = realDeltaSeconds * BaseGameTimeScale;
            TotalGameHours += deltaHours;
            return new TickResult(deltaHours, playerTravelDeltaHours: deltaHours, shouldAdvanceHours: true, shouldAdvanceFrame: true, "player_moving");
        }

        // 规则 3: 玩家等待 — 世界推进（加速）+ 旅行推进（加速）
        if (PlayerWaiting)
        {
            float deltaHours = realDeltaSeconds * BaseGameTimeScale * WaitMultiplier;
            TotalGameHours += deltaHours;
            return new TickResult(deltaHours, playerTravelDeltaHours: deltaHours, shouldAdvanceHours: true, shouldAdvanceFrame: true, "player_waiting");
        }

        // 规则 4: AI active (combat/marching) — world time advances but player travel does NOT.
        // "Player idle" here means the player is stationary on the map with no movement/wait
        // input; this is NOT the same as IsPaused. The game is still running, AI entities
        // are resolving actions, and world hours must advance for battles to conclude.
        // PlayerTravelDeltaHours = 0 ensures food is not consumed while the player stands still.
        if (AISimulationActive)
        {
            float deltaHours = realDeltaSeconds * BaseGameTimeScale;
            TotalGameHours += deltaHours;
            return new TickResult(deltaHours, playerTravelDeltaHours: 0f, shouldAdvanceHours: true, shouldAdvanceFrame: true, "ai_active");
        }

        // 规则 5: Player idle + AI inactive — no hour advancement.
        // IMPORTANT: This is NOT the same as IsPaused.
        //   - IsPaused = explicit player pause: freezes AI, frame logic, everything.
        //   - Idle (this path) = no flags set: hours don't advance (nothing to simulate),
        //     but ShouldAdvanceFrame stays true so perception/path-refresh keeps running.
        // If AI later becomes active, the next Tick will enter Rule 4 and time will resume.
        return TickResult.Idle;
    }

    // ========================================
    // 状态重置
    // ========================================

    /// <summary>重置时钟状态（场景切换时调用）</summary>
    public void Reset()
    {
        TotalGameHours = 0f;
        CurrentDay = 1;
        IsPaused = false;
        PlayerMoving = false;
        PlayerWaiting = false;
        AISimulationActive = false;
    }

    /// <summary>从存档恢复时钟状态</summary>
    public void RestoreState(int day, float totalGameHours)
    {
        CurrentDay = day;
        TotalGameHours = totalGameHours;
    }
}
