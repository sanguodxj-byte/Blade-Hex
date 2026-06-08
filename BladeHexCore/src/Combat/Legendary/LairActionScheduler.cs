// LairActionScheduler.cs
// 巢穴动作调度器 — 在 turn manager init 20 时触发
// T13: Lair Actions
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat.Legendary;

/// <summary>
/// 巢穴动作调度器 — 管理巢穴动作的触发
/// </summary>
public class LairActionScheduler
{
    /// <summary>巢穴动作列表</summary>
    private LairAction[] _actions = [];

    /// <summary>是否已初始化</summary>
    private bool _initialized;

    /// <summary>初始化巢穴动作系统</summary>
    public void Initialize(UnitData unit)
    {
        var actions = new System.Collections.Generic.List<LairAction>();
        foreach (var dict in unit.LairActions)
        {
            if (dict is Godot.Collections.Dictionary d)
                actions.Add(LairAction.FromDictionary(d));
        }
        _actions = actions.ToArray();
        _initialized = true;
    }

    /// <summary>检查是否应该触发巢穴动作（init 20 时）</summary>
    public bool ShouldTrigger(int initiativeRoll)
    {
        return _initialized && initiativeRoll == 20 && _actions.Length > 0;
    }

    /// <summary>获取可用的巢穴动作</summary>
    public LairAction[] GetAvailableActions()
    {
        return _initialized ? _actions : [];
    }

    /// <summary>随机选择一个巢穴动作</summary>
    public LairAction? SelectRandomAction()
    {
        if (!_initialized || _actions.Length == 0) return null;
        int index = (int)(GD.Randi() % (uint)_actions.Length);
        return _actions[index];
    }
}
