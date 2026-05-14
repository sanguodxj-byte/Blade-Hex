// UnitRegistry.cs
// 战斗单位注册与查询服务
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Combat.Commands;

namespace BladeHex.Combat;

/// <summary>
/// 管理战斗中所有单位的注册、查询和生命周期。
/// 从 CombatManager 中拆分出来，为 Phase 4 军队系统（堆叠单位）预留扩展点。
/// </summary>
public class UnitRegistry
{
    public List<Unit> AllUnits { get; } = new();
    public List<Unit> PlayerUnits { get; } = new();
    public List<Unit> EnemyUnits { get; } = new();

    /// <summary>初始玩家单位数（用于伤亡比例计算）</summary>
    public int InitialPlayerCount { get; private set; }
    /// <summary>初始敌人单位数</summary>
    public int InitialEnemyCount { get; private set; }
    /// <summary>已击杀敌人等级累计（用于 XP/Gold 奖励计算）</summary>
    public int KilledEnemyLevels { get; private set; }

    public event Action<Unit, bool>? UnitDied;

    public void RegisterUnit(Unit unit, bool isPlayer, CommandHistory commandHistory)
    {
        AllUnits.Add(unit);
        unit.IsPlayerSide = isPlayer;
        if (isPlayer) PlayerUnits.Add(unit);
        else EnemyUnits.Add(unit);

        unit.CommandHistory = commandHistory;
        unit.TreeExited += () => OnUnitRemoved(unit, isPlayer);
    }

    public void LockInitialCounts()
    {
        InitialPlayerCount = PlayerUnits.Count;
        InitialEnemyCount = EnemyUnits.Count;
        KilledEnemyLevels = 0;
    }

    private void OnUnitRemoved(Unit unit, bool isPlayer)
    {
        AllUnits.Remove(unit);
        if (isPlayer)
        {
            PlayerUnits.Remove(unit);
        }
        else
        {
            KilledEnemyLevels += unit.Data?.Level ?? 1;
            EnemyUnits.Remove(unit);
        }
        UnitDied?.Invoke(unit, isPlayer);
    }

    /// <summary>查询指定坐标上的单位</summary>
    public Unit? FindUnitAt(Vector2I pos)
    {
        foreach (var u in AllUnits)
        {
            if (GodotObject.IsInstanceValid(u) && u.GridPos == pos)
                return u;
        }
        return null;
    }

    /// <summary>重置指定阵营单位的行动状态</summary>
    public void ResetActions(IEnumerable<Unit> units)
    {
        foreach (var u in units)
        {
            u.HasMoved = false;
            u.HasActed = false;
            u.CurrentAp = u.Model.GetMaxAp();
            if (u.Data != null) u.Data.Runtime.IsRangedWeaponLoaded = true;
        }
    }
}
