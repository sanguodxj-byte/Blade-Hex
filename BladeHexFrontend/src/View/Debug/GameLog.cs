// GameLog.cs
// 游戏内日志工具 — 同时输出到 Godot Output 和游戏内 DebugConsole
// 用法: GameLog.Info("消息"), GameLog.Warn("警告"), GameLog.Err("错误")
using Godot;
using BladeHex.Diagnostics;

namespace BladeHex.Debug;

/// <summary>
/// 游戏内日志工具 — 双通道输出（Godot Output + 游戏内 DebugConsole）
/// 所有战斗/场景加载相关的日志都应通过此类输出，确保在游戏内控制台可见。
/// </summary>
public static class GameLog
{
    /// <summary>信息日志（白色）</summary>
    public static void Info(string msg)
    {
        DiagnosticLog.Info(msg);
        GD.Print(msg);
        DebugConsole.Instance?.LogInfo(msg);
    }

    /// <summary>警告日志（黄色）</summary>
    public static void Warn(string msg)
    {
        DiagnosticLog.Warn(msg);
        GD.PrintRich($"[color=yellow]{msg}[/color]");
        DebugConsole.Instance?.LogWarn(msg);
    }

    /// <summary>错误日志（红色）— 同时调用 GD.PushError</summary>
    public static void Err(string msg)
    {
        DiagnosticLog.Error(msg);
        GD.PushError(msg);
        DebugConsole.Instance?.LogErr(msg);
    }

    /// <summary>带异常堆栈的错误日志</summary>
    public static void Exception(string context, System.Exception ex)
    {
        string msg = $"{context}: {ex.Message}\n{ex.StackTrace}";
        DiagnosticLog.Exception(context, ex);
        GD.PushError(msg);
        DebugConsole.Instance?.LogErr($"{context}: {ex.Message}");
        DebugConsole.Instance?.LogErr($"  堆栈: {ex.StackTrace}");
    }
}
