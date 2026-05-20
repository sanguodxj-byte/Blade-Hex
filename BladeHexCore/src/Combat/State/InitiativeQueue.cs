// InitiativeQueue.cs
// 先攻队列 — 管理战斗中所有单位的行动顺序
// 公式: 先攻值 = d20 + DEX_mod + BaseInitiative
// 每轮开始重新掷骰排序
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Combat.State;

/// <summary>
/// 先攻队列 — 纯数据结构，不依赖 Godot
/// 管理单位行动顺序，支持插入/移除/延迟
/// </summary>
public class InitiativeQueue
{
    /// <summary>先攻条目</summary>
    public class Entry
    {
        /// <summary>单位唯一标识（运行时 ID）</summary>
        public long UnitId { get; set; }

        /// <summary>先攻掷骰结果</summary>
        public int InitiativeRoll { get; set; }

        /// <summary>是否为玩家阵营</summary>
        public bool IsPlayerSide { get; set; }

        /// <summary>本轮是否已行动</summary>
        public bool HasActed { get; set; }

        /// <summary>是否存活</summary>
        public bool IsAlive { get; set; } = true;
    }

    private readonly List<Entry> _entries = new();
    private int _currentIndex = -1;

    /// <summary>当前轮次（每次所有单位行动完一轮 +1）</summary>
    public int RoundNumber { get; private set; }

    /// <summary>当前行动单位的条目（null = 无）</summary>
    public Entry? CurrentEntry => _currentIndex >= 0 && _currentIndex < _entries.Count
        ? _entries[_currentIndex] : null;

    /// <summary>所有条目（按先攻排序）</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>队列是否为空</summary>
    public bool IsEmpty => _entries.Count == 0;

    /// <summary>
    /// 初始化队列 — 为所有单位掷先攻骰并排序
    /// </summary>
    /// <param name="units">单位列表：(unitId, dexMod, baseInitiative, isPlayerSide)</param>
    /// <param name="rng">随机数生成器（null 则使用默认）</param>
    public void Initialize(List<(long unitId, int dexMod, int baseInitiative, bool isPlayerSide)> units, Random? rng = null)
    {
        rng ??= new Random();
        _entries.Clear();
        _currentIndex = -1;
        RoundNumber = 0;

        foreach (var (unitId, dexMod, baseInit, isPlayer) in units)
        {
            int roll = rng.Next(1, 21) + dexMod + baseInit; // d20 + DEX_mod + BaseInitiative
            _entries.Add(new Entry
            {
                UnitId = unitId,
                InitiativeRoll = roll,
                IsPlayerSide = isPlayer,
                HasActed = false,
            });
        }

        // 按先攻值降序排列（高先攻先行动）
        // 同值时玩家优先，再同值按 ID 稳定排序
        _entries.Sort((a, b) =>
        {
            int cmp = b.InitiativeRoll.CompareTo(a.InitiativeRoll);
            if (cmp != 0) return cmp;
            cmp = b.IsPlayerSide.CompareTo(a.IsPlayerSide); // true > false
            if (cmp != 0) return cmp;
            return a.UnitId.CompareTo(b.UnitId);
        });
    }

    /// <summary>
    /// 推进到下一个单位。如果所有单位都已行动，开始新一轮。
    /// 返回下一个行动单位的条目，或 null（队列为空/全部死亡）。
    /// </summary>
    public Entry? AdvanceToNext()
    {
        if (_entries.Count == 0) return null;

        // 找下一个未行动且存活的单位
        for (int i = 0; i < _entries.Count; i++)
        {
            int idx = (_currentIndex + 1 + i) % _entries.Count;
            var entry = _entries[idx];
            if (!entry.HasActed && entry.IsAlive)
            {
                _currentIndex = idx;
                return entry;
            }
        }

        // 所有单位都已行动 → 新一轮
        StartNewRound();
        return AdvanceToNextInRound();
    }

    /// <summary>标记当前单位已行动，推进到下一个</summary>
    public Entry? EndCurrentUnitTurn()
    {
        if (CurrentEntry != null)
            CurrentEntry.HasActed = true;
        return AdvanceToNext();
    }

    /// <summary>移除单位（死亡时调用）</summary>
    public void RemoveUnit(long unitId)
    {
        var entry = _entries.Find(e => e.UnitId == unitId);
        if (entry != null)
            entry.IsAlive = false;
    }

    /// <summary>检查是否还有存活的玩家单位</summary>
    public bool HasAlivePlayerUnits() => _entries.Any(e => e.IsAlive && e.IsPlayerSide);

    /// <summary>检查是否还有存活的敌方单位</summary>
    public bool HasAliveEnemyUnits() => _entries.Any(e => e.IsAlive && !e.IsPlayerSide);

    /// <summary>获取排序后的单位 ID 列表（用于 UI 显示）</summary>
    public List<long> GetOrderedUnitIds() => _entries.Where(e => e.IsAlive).Select(e => e.UnitId).ToList();

    /// <summary>获取当前单位之后的行动顺序预览</summary>
    public List<long> GetUpcomingUnitIds(int count = 10)
    {
        var result = new List<long>();
        if (_entries.Count == 0) return result;

        // 本轮剩余
        for (int i = _currentIndex + 1; i < _entries.Count && result.Count < count; i++)
        {
            if (_entries[i].IsAlive && !_entries[i].HasActed)
                result.Add(_entries[i].UnitId);
        }

        // 如果不够，从头开始（下一轮预览）
        for (int i = 0; i < _entries.Count && result.Count < count; i++)
        {
            if (_entries[i].IsAlive)
                result.Add(_entries[i].UnitId);
        }

        return result;
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    private void StartNewRound()
    {
        RoundNumber++;
        foreach (var entry in _entries)
        {
            if (entry.IsAlive)
                entry.HasActed = false;
        }
        _currentIndex = -1;
    }

    private Entry? AdvanceToNextInRound()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].IsAlive && !_entries[i].HasActed)
            {
                _currentIndex = i;
                return _entries[i];
            }
        }
        return null; // 所有单位都死了
    }
}
