// OverworldDiagnostics.cs
// 大地图诊断日志 — 统一前缀、限频、计数快照
//
// 设计目标:
//   - 统一日志前缀: [OverworldAI] [OverworldBattlefield] [OverworldClock] [OverworldView]
//   - 所有日志限频，避免主循环刷屏
//   - 提供 AI 实体状态计数快照
//   - 空闲主场景不持续刷日志
//   - 发生 AI 追逃、寻路失败、战场创建/消失时可从日志定位阶段
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图诊断日志模块。
/// 
/// 日志分类:
///   AI          — 实体状态、意图、移动、接触
///   Battlefield — 战场创建、合并、消散、结算
///   Clock       — 时间推进、暂停、倍率
///   View        — 前端视觉同步、LOD、精灵创建/销毁
/// </summary>
public static class OverworldDiagnostics
{
    // ========================================
    // 日志前缀常量
    // ========================================

    public const string PrefixAI = "[OverworldAI]";
    public const string PrefixBattlefield = "[OverworldBattlefield]";
    public const string PrefixClock = "[OverworldClock]";
    public const string PrefixView = "[OverworldView]";

    // ========================================
    // 限频机制
    // ========================================

    private const long DefaultCooldownMs = 2000;
    public static bool VerboseFallbackLogs { get; set; } = false;

    /// <summary>分类限频表：key = 分类前缀 + 子标签，value = 上次输出的 TickMsec</summary>
    private static readonly Dictionary<string, long> _lastLogMs = new();

    /// <summary>
    /// 输出一条限频日志。同一 tag 在 cooldownMs 内只输出一次。
    /// </summary>
    public static void LogThrottled(string prefix, string tag, string message, long cooldownMs = DefaultCooldownMs)
    {
        long now = (long)Time.GetTicksMsec();
        string key = prefix + ":" + tag;
        if (_lastLogMs.TryGetValue(key, out long last) && now - last < cooldownMs)
            return;

        _lastLogMs[key] = now;
        GD.Print($"{prefix} {message}");
    }

    public static void LogFallbackThrottled(string prefix, string tag, string message, long cooldownMs = 10000)
    {
        if (!VerboseFallbackLogs)
            return;

        LogThrottled(prefix, "fallback:" + tag, message, cooldownMs);
    }

    /// <summary>直接输出一条不限频日志（用于低频重要事件）</summary>
    public static void Log(string prefix, string message)
    {
        GD.Print($"{prefix} {message}");
    }

    /// <summary>输出一条限频错误日志</summary>
    public static void LogErrorThrottled(string prefix, string tag, string message, long cooldownMs = DefaultCooldownMs)
    {
        long now = (long)Time.GetTicksMsec();
        string key = prefix + ":err:" + tag;
        if (_lastLogMs.TryGetValue(key, out long last) && now - last < cooldownMs)
            return;

        _lastLogMs[key] = now;
        GD.PrintErr($"{prefix} {message}");
    }

    // ========================================
    // AI 状态计数快照
    // ========================================

    /// <summary>AI 实体状态快照结构</summary>
    public readonly struct AISnapshot
    {
        public readonly int Total;
        public readonly int Moving;
        public readonly int Pathing;
        public readonly int Chasing;
        public readonly int Fleeing;
        public readonly int Engaged;
        public readonly int Besieging;
        public readonly int PathFailed;
        public readonly bool IsPaused;

        public AISnapshot(int total, int moving, int pathing, int chasing, int fleeing,
            int engaged, int besieging, int pathFailed, bool isPaused)
        {
            Total = total;
            Moving = moving;
            Pathing = pathing;
            Chasing = chasing;
            Fleeing = fleeing;
            Engaged = engaged;
            Besieging = besieging;
            PathFailed = pathFailed;
            IsPaused = isPaused;
        }

        /// <summary>是否有任何活跃活动</summary>
        public bool HasActivity =>
            Moving > 0 || Chasing > 0 || Fleeing > 0 || Engaged > 0 || Besieging > 0;
    }

    /// <summary>本帧 path_failed 计数（由 Navigator 递增）</summary>
    private static int _framePathFailed;

    /// <summary>递增本帧 path_failed 计数</summary>
    public static void IncrementPathFailed() => _framePathFailed++;

    /// <summary>
    /// 从实体列表生成 AI 状态快照，并输出限频摘要日志。
    /// 建议每 3~5 秒调用一次（由调用方控制频率）。
    /// </summary>
    public static AISnapshot SnapshotAndLogAI(List<OverworldEntity> entities, bool isPaused)
    {
        int total = 0, moving = 0, pathing = 0, chasing = 0, fleeing = 0,
            engaged = 0, besieging = 0;

        foreach (var e in entities)
        {
            if (!e.IsAlive) continue;
            total++;
            if (e.IsMoving) moving++;
            if (e.Path.Count > 0) pathing++;
            switch (e.CurrentAIState)
            {
                case OverworldEntity.AIState.Chasing: chasing++; break;
                case OverworldEntity.AIState.Fleeing: fleeing++; break;
                case OverworldEntity.AIState.Engaged: engaged++; break;
                case OverworldEntity.AIState.Besieging: besieging++; break;
            }
        }

        int pathFailed = _framePathFailed;
        _framePathFailed = 0; // 重置，下个周期重新计数

        var snapshot = new AISnapshot(total, moving, pathing, chasing, fleeing,
            engaged, besieging, pathFailed, isPaused);

        // 只在有活动或刚恢复活动时输出日志，避免空闲刷屏
        if (snapshot.HasActivity || pathFailed > 0)
        {
            LogThrottled(PrefixAI, "tick",
                $"tick total={total}, moving={moving}, pathing={pathing}, " +
                $"chasing={chasing}, fleeing={fleeing}, engaged={engaged}, " +
                $"siege={besieging}, path_failed={pathFailed}, paused={isPaused}",
                cooldownMs: 3000);
        }

        return snapshot;
    }

    // ========================================
    // 战场事件日志（低频，不限频）
    // ========================================

    /// <summary>记录战场创建</summary>
    public static void LogBattlefieldCreated(string battlefieldId, string attacker, string defender, float durationHours)
    {
        Log(PrefixBattlefield,
            $"create id={battlefieldId}, {attacker} vs {defender}, est={durationHours:F1}h");
    }

    /// <summary>记录战场合并</summary>
    public static void LogBattlefieldMerged(string fromId, string intoId)
    {
        Log(PrefixBattlefield, $"merge {fromId} -> {intoId}");
    }

    /// <summary>记录战场消散</summary>
    public static void LogBattlefieldCleared(string battlefieldId, string reason)
    {
        Log(PrefixBattlefield, $"clear id={battlefieldId}, reason={reason}");
    }

    /// <summary>记录战斗结算结果</summary>
    public static void LogBattleResolved(string winner, string loser, float durationHours)
    {
        Log(PrefixBattlefield, $"resolved {winner} wins over {loser}, duration={durationHours:F1}h");
    }

    // ========================================
    // 时钟日志
    // ========================================

    /// <summary>记录时间推进状态变化</summary>
    public static void LogClockStateChange(string reason, bool timePassing, float deltaHours)
    {
        LogThrottled(PrefixClock, "state",
            $"reason={reason}, passing={timePassing}, dHours={deltaHours:F3}",
            cooldownMs: 3000);
    }

    /// <summary>记录暂停/恢复</summary>
    public static void LogClockPause(bool paused, string source)
    {
        Log(PrefixClock, $"pause={paused}, source={source}");
    }

    // ========================================
    // View 层日志
    // ========================================

    /// <summary>记录战场视觉创建</summary>
    public static void LogViewBattlefieldCreated(string battlefieldId)
    {
        LogThrottled(PrefixView, "bf_create",
            $"battlefield_marker created: {battlefieldId}", cooldownMs: 1000);
    }

    /// <summary>记录战场视觉移除</summary>
    public static void LogViewBattlefieldRemoved(string battlefieldId)
    {
        LogThrottled(PrefixView, "bf_remove",
            $"battlefield_marker removed: {battlefieldId}", cooldownMs: 1000);
    }

    /// <summary>记录实体同步摘要</summary>
    public static void LogViewSyncSummary(int visibleEntities, int battlefieldMarkers, int hiddenForBattle)
    {
        LogThrottled(PrefixView, "sync",
            $"sync visible={visibleEntities}, bf_markers={battlefieldMarkers}, hidden_for_battle={hiddenForBattle}",
            cooldownMs: 5000);
    }

    // ========================================
    // 寻路失败日志（由 Navigator 调用）
    // ========================================

    /// <summary>记录寻路失败（限频）</summary>
    public static void LogPathFailure(OverworldEntity entity, Vector2 target)
    {
        IncrementPathFailed();
        LogThrottled(PrefixAI, $"path_fail_{entity.GetHashCode()}",
            $"path_failed entity={entity.EntityName}, state={entity.CurrentAIState}, " +
            $"from={entity.Position}, target={target}, lod={entity.Lod}",
            cooldownMs: 2000);
    }

    // ========================================
    // 重置
    // ========================================

    /// <summary>清除所有限频计时器（场景切换时调用）</summary>
    public static void Reset()
    {
        _lastLogMs.Clear();
        _framePathFailed = 0;
    }
}
