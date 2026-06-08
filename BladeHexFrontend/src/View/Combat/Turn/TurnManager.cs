// TurnManager.cs
// 先攻制回合管理器 — 个体先攻队列驱动
//
// 每轮开始为所有存活单位掷先攻（d20 + DEX_mod + BaseInitiative），
// 按先攻值降序逐个行动。一个单位行动完毕后推进到下一个。
// 所有单位行动完一轮后自动开始新一轮。
//
// 向后兼容：仍发出 PlayerTurn/EnemyTurn 信号（映射为当前行动单位的阵营），
// 使 CombatManager/CombatSceneBase 的信号监听代码无需大改。
using System;
using System.Collections.Generic;
using BladeHex.Combat.State;
using BladeHex.Data;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 先攻制回合管理器。维护 InitiativeQueue，驱动个体单位回合。
/// </summary>
public class TurnManager
{
    /// <summary>向后兼容的阶段枚举</summary>
    public enum TurnPhase { Init, Deployment, PlayerTurn, EnemyTurn, CombatEnd }

    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Init;
    public int TurnNumber => _queue.RoundNumber;

    /// <summary>先攻队列（公开供 UI 读取排序）</summary>
    public InitiativeQueue Queue => _queue;

    /// <summary>当前行动单位的 ID（-1 = 无）</summary>
    public long CurrentUnitId => _queue.CurrentEntry?.UnitId ?? -1;

    /// <summary>当前行动单位是否为玩家阵营</summary>
    public bool IsCurrentUnitPlayer => _queue.CurrentEntry?.IsPlayerSide ?? false;

    /// <summary>底层状态机（保留兼容）</summary>
    public CombatStateMachine Machine { get; } = new();

    private readonly InitiativeQueue _queue = new();
    private readonly UnitRegistry _registry;
    private readonly Random _rng = new();

    /// <summary>阶段变化事件（向后兼容）</summary>
    public event Action<TurnPhase>? PhaseChanged;

    /// <summary>单位回合开始事件（新增：传递当前行动单位 ID）</summary>
    public event Action<long, bool>? UnitTurnStarted;

    public TurnManager(UnitRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>开始战斗 — 掷先攻并推进到第一个单位</summary>
    public void StartCombat()
    {
        BuildInitiativeQueue();
        Machine.StartCombat();
        AdvanceToNextUnit();
    }

    /// <summary>进入部署阶段</summary>
    public void EnterDeployment()
    {
        Machine.EnterDeployment();
        CurrentPhase = TurnPhase.Deployment;
        PhaseChanged?.Invoke(TurnPhase.Deployment);
    }

    /// <summary>确认部署，正式开始战斗</summary>
    public void ConfirmDeployment()
    {
        Machine.ConfirmDeployment();
        BuildInitiativeQueue();
        AdvanceToNextUnit();
    }

    /// <summary>结束当前单位的回合，推进到下一个</summary>
    public void EndCurrentTurn()
    {
        _queue.EndCurrentUnitTurn();
        AdvanceToNextUnit();
    }

    /// <summary>结束战斗</summary>
    public void EndCombat()
    {
        CurrentPhase = TurnPhase.CombatEnd;
        Machine.EndCombat(_registry.PlayerUnits.Count > 0);
        PhaseChanged?.Invoke(TurnPhase.CombatEnd);
    }

    /// <summary>单位死亡时从队列移除</summary>
    public void OnUnitDied(long unitId)
    {
        _queue.RemoveUnit(unitId);
    }

    /// <summary>获取先攻排序的单位 ID 列表（供 UI 使用）</summary>
    public List<long> GetOrderedUnitIds() => _queue.GetOrderedUnitIds();

    /// <summary>获取接下来要行动的单位预览</summary>
    public List<long> GetUpcomingUnitIds(int count = 10) => _queue.GetUpcomingUnitIds(count);

    // ============================================================================
    // 内部方法
    // ============================================================================

    private void BuildInitiativeQueue()
    {
        var unitData = new List<(long unitId, int dexMod, int baseInitiative, bool isPlayerSide)>();

        foreach (var unit in _registry.AllUnits)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
            if (unit.Data == null) continue;

            long id = (long)unit.GetInstanceId();
            int dexMod = CombatStats.GetStatModifier(CombatStats.GetEffectiveDex(unit.Data));
            // v0.7: 技能盘 initiative 节点（dex 系多个）和饰品 initiative 都要进 baseInit。
            // 之前漏了 SkillTree.GetInitiativeBonus，导致 dex_s01/s02/s14/s13/s16/s17 等节点完全不生效。
            int baseInit = unit.Data.BaseInitiative
                + (unit.SkillTree?.GetInitiativeBonus() ?? 0)
                + unit.Data.AccessoryInitiativeBonus;
            bool isPlayer = unit.IsPlayerSide;

            unitData.Add((id, dexMod, baseInit, isPlayer));
        }

        _queue.Initialize(unitData, _rng);
    }

    private void AdvanceToNextUnit()
    {
        // 检查胜负
        if (!_queue.HasAlivePlayerUnits())
        {
            EndCombat();
            return;
        }
        if (!_queue.HasAliveEnemyUnits())
        {
            EndCombat();
            return;
        }

        var entry = _queue.CurrentEntry;
        if (entry == null)
        {
            // 队列推进（AdvanceToNext 在 EndCurrentUnitTurn 中已调用，
            // 但 StartCombat 时需要手动推进第一个）
            entry = _queue.AdvanceToNext();
        }

        if (entry == null)
        {
            // 所有单位都死了
            EndCombat();
            return;
        }

        // 重置该单位的行动状态
        var unit = FindUnitById(entry.UnitId);
        if (unit != null)
        {
            _registry.ResetActions(new[] { unit });
        }

        // 发出阶段变化信号（向后兼容）
        var newPhase = entry.IsPlayerSide ? TurnPhase.PlayerTurn : TurnPhase.EnemyTurn;
        CurrentPhase = newPhase;

        // 先发出单位回合开始信号（让 CombatManager 更新 CurrentInitiativeUnit）
        UnitTurnStarted?.Invoke(entry.UnitId, entry.IsPlayerSide);

        // 再发出阶段变化信号（此时 CurrentInitiativeUnit 已正确设置）
        PhaseChanged?.Invoke(newPhase);
    }

    private Unit? FindUnitById(long unitId)
    {
        foreach (var unit in _registry.AllUnits)
        {
            if (GodotObject.IsInstanceValid(unit) && (long)unit.GetInstanceId() == unitId)
                return unit;
        }
        return null;
    }
}
