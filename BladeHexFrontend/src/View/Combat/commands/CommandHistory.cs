// CommandHistory.cs — 命令历史管理器（Phase 2.1 实装版）
// 支持 Undo(悔棋):连续弹栈直到遇到不可撤销命令或回合边界
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Combat.Commands;

public class CommandHistory
{
    private readonly List<ICommand> _history = new();
    private int _currentIndex = -1;

    /// <summary>历史命令总数</summary>
    public int Count => _history.Count;

    /// <summary>是否有可撤销的命令</summary>
    public bool CanUndo => FindLastUndoableIndex() >= 0;

    /// <summary>当前可撤销的命令数量(供 UI 显示)</summary>
    public int UndoableCount
    {
        get
        {
            int count = 0;
            for (int i = _currentIndex; i >= 0; i--)
            {
                if (_history[i] is TurnBoundaryMarker) break;
                if (!_history[i].IsUndoable) break;
                count++;
            }
            return count;
        }
    }

    /// <summary>历史变更事件(供 EventBus 广播)</summary>
    public event Action? HistoryChanged;

    /// <summary>
    /// 执行命令并入栈
    /// </summary>
    public CommandResult Execute(ICommand command, CommandContext ctx)
    {
        // 截断 redo 分支
        if (_currentIndex < _history.Count - 1)
            _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);

        var result = command.Execute(ctx);

        if (result.Success)
        {
            _history.Add(command);
            _currentIndex = _history.Count - 1;
            HistoryChanged?.Invoke();
        }

        return result;
    }

    /// <summary>
    /// 撤销最近一个可撤销命令
    /// 返回 true 表示成功撤销了一条命令
    /// </summary>
    public bool TryUndoLast(CommandContext ctx)
    {
        int idx = FindLastUndoableIndex();
        if (idx < 0) return false;

        _history[idx].Undo(ctx);
        _history.RemoveAt(idx);
        _currentIndex = _history.Count - 1;
        HistoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 插入回合边界标记(回合切换时调用)
    /// Undo 不会越过此边界
    /// </summary>
    public void MarkTurnBoundary(CommandContext ctx)
    {
        var marker = new TurnBoundaryMarker();
        // 截断 redo 分支
        if (_currentIndex < _history.Count - 1)
            _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
        _history.Add(marker);
        _currentIndex = _history.Count - 1;
    }

    /// <summary>序列化全部历史(录像/存档)</summary>
    public Godot.Collections.Array SerializeAll()
    {
        var arr = new Godot.Collections.Array();
        foreach (var cmd in _history) arr.Add(cmd.Serialize());
        return arr;
    }

    /// <summary>清空历史</summary>
    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
        HistoryChanged?.Invoke();
    }

    /// <summary>
    /// 从栈顶向下找最近一个可撤销命令的索引
    /// 遇到 TurnBoundaryMarker 或不可撤销命令则停止
    /// </summary>
    private int FindLastUndoableIndex()
    {
        if (_currentIndex < 0) return -1;
        var top = _history[_currentIndex];
        if (top is TurnBoundaryMarker) return -1;
        if (!top.IsUndoable) return -1;
        return _currentIndex;
    }
}
