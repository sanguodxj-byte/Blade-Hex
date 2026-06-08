// LegendaryActionScheduler.cs
// 传奇动作调度器 — 监听 unit-turn-end，触发传奇动作
// T12: Legendary creature action system
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat.Legendary;

/// <summary>
/// 传奇动作调度器 — 管理传奇生物的动作点数和触发
/// </summary>
public class LegendaryActionScheduler
{
    /// <summary>当前可用的传奇动作点数</summary>
    private int _currentPoints;

    /// <summary>最大传奇动作点数</summary>
    private int _maxPoints;

    /// <summary>传奇动作列表</summary>
    private LegendaryAction[] _actions = [];

    /// <summary>当前阶段</summary>
    private PhaseData? _currentPhase;

    /// <summary>所有阶段</summary>
    private PhaseData[] _phases = [];

    /// <summary>是否已初始化</summary>
    private bool _initialized;

    /// <summary>初始化传奇动作系统</summary>
    public void Initialize(UnitData unit)
    {
        _maxPoints = unit.LegendaryActionPoints;
        _currentPoints = _maxPoints;

        // 解析传奇动作
        var actions = new System.Collections.Generic.List<LegendaryAction>();
        foreach (var dict in unit.LegendaryActions)
        {
            if (dict is Godot.Collections.Dictionary d)
                actions.Add(LegendaryAction.FromDictionary(d));
        }
        _actions = actions.ToArray();

        // 解析阶段
        var phases = new System.Collections.Generic.List<PhaseData>();
        foreach (var dict in unit.Phases)
        {
            if (dict is Godot.Collections.Dictionary d)
                phases.Add(PhaseData.FromDictionary(d));
        }
        phases.Sort((a, b) => b.HpThreshold.CompareTo(a.HpThreshold)); // 从高到低排序
        _phases = phases.ToArray();

        _initialized = true;
    }

    /// <summary>回合结束时恢复传奇动作点数</summary>
    public void OnTurnEnd()
    {
        if (!_initialized) return;
        _currentPoints = _maxPoints;
    }

    /// <summary>检查是否可以使用传奇动作</summary>
    public bool CanUseAction(LegendaryAction action)
    {
        return _initialized && _currentPoints >= action.Cost;
    }

    /// <summary>使用传奇动作</summary>
    public bool UseAction(LegendaryAction action)
    {
        if (!CanUseAction(action))
            return false;

        _currentPoints -= action.Cost;
        return true;
    }

    /// <summary>获取可用的传奇动作</summary>
    public LegendaryAction[] GetAvailableActions()
    {
        if (!_initialized) return [];

        var available = new System.Collections.Generic.List<LegendaryAction>();
        foreach (var action in _actions)
        {
            if (CanUseAction(action))
                available.Add(action);
        }
        return available.ToArray();
    }

    /// <summary>检查阶段切换</summary>
    public PhaseData? CheckPhaseTransition(float hpPercent)
    {
        if (!_initialized || _phases.Length == 0) return null;

        foreach (var phase in _phases)
        {
            if (hpPercent <= phase.HpThreshold)
            {
                if (_currentPhase == null || _currentPhase.Id != phase.Id)
                {
                    _currentPhase = phase;
                    return phase;
                }
                return null;
            }
        }
        return null;
    }

    /// <summary>获取当前阶段</summary>
    public PhaseData? GetCurrentPhase() => _currentPhase;

    /// <summary>获取当前传奇动作点数</summary>
    public int GetCurrentPoints() => _currentPoints;

    /// <summary>获取最大传奇动作点数</summary>
    public int GetMaxPoints() => _maxPoints;
}
