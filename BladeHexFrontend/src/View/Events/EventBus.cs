// EventBus.cs — 全局事件总线（架构优化 spec R6 期间引入强类型 API）
//
// 强类型路径（推荐）：
//   bus.Subscribe<UnitDamagedEvent>(OnDamaged);
//   bus.Publish(new UnitDamagedEvent(unit, 10, 80));
//
// 弱类型路径（兼容期保留）：
//   bus.Subscribe(Signals.UnitDamaged, dict => ...);
//   bus.Publish(Signals.UnitDamaged, new Godot.Collections.Dictionary { { "unit", unit } });
//
// Publisher 内部约定：调用 PublishXxx 便捷方法时同时双发两路，
// 让旧订阅者继续工作，逐步迁移完成后删除弱类型路径。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Events.Payloads;
using BladeHex.Strategic;

namespace BladeHex.Events;

/// <summary>
/// [Autoload Singleton] 全局事件总线。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>EventBus</c>。</para>
/// <para>生命周期：应用全局，从启动到退出始终存在。</para>
/// <para>访问方式：建议通过 <see cref="BladeHex.Data.Globals.Events"/> 或 <see cref="BladeHex.Data.Globals.EventsOrNull"/>。</para>
/// <para>测试替换：通过 <see cref="OverrideForTest"/>（仅 DEBUG 构建）。</para>
/// </summary>
[GlobalClass]
public partial class EventBus : Node
{
    public static EventBus? Instance { get; private set; }

#if DEBUG
    /// <summary>
    /// 测试钩子：替换或重置 <see cref="Instance"/>。
    /// 仅在 DEBUG 构建中可用，单元测试可注入 mock 或在测试间清理状态。
    /// </summary>
    public static void OverrideForTest(EventBus? mock) => Instance = mock;
#endif

    public static class Signals
    {
        public const string CombatStarted = "combat_started";
        public const string CombatEnded = "combat_ended";
        public const string TurnStarted = "turn_started";
        public const string UnitDied = "unit_died";
        public const string UnitDamaged = "unit_damaged";
        public const string UnitHealed = "unit_healed";
        public const string SkillUsed = "skill_used";
        public const string StatusEffectApplied = "status_effect_applied";
        public const string StatusEffectRemoved = "status_effect_removed";
        public const string StatusEffectTicked = "status_effect_ticked";
        public const string MoraleChanged = "morale_changed";
        public const string DayPassed = "day_passed";
        public const string GoldChanged = "gold_changed";
        public const string FoodChanged = "food_changed";
        public const string ItemAcquired = "item_acquired";
        public const string ItemLost = "item_lost";
        public const string QuestCompleted = "quest_completed";
        public const string EquipmentChanged = "equipment_changed";
        public const string ProjectileLaunched = "projectile_launched";
        public const string ProjectileImpact = "projectile_impact";
    }

    // ========================================
    // 弱类型订阅（兼容期保留）
    // ========================================
    private readonly Dictionary<string, List<Action<Godot.Collections.Dictionary>>> _subscribers = new();

    // ========================================
    // 强类型订阅 — 按事件 Type 分组
    // ========================================
    private readonly Dictionary<Type, List<Delegate>> _typedHandlers = new();

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    // ========================================
    // 强类型 API
    // ========================================

    /// <summary>订阅强类型事件。</summary>
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (!_typedHandlers.TryGetValue(typeof(TEvent), out var list))
            _typedHandlers[typeof(TEvent)] = list = new List<Delegate>();
        list.Add(handler);
    }

    /// <summary>取消订阅强类型事件。</summary>
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (_typedHandlers.TryGetValue(typeof(TEvent), out var list))
            list.Remove(handler);
    }

    /// <summary>发布强类型事件。</summary>
    public void Publish<TEvent>(TEvent ev) where TEvent : notnull
    {
        if (!_typedHandlers.TryGetValue(typeof(TEvent), out var list)) return;
        // 拷贝快照避免回调中修改集合
        var snapshot = list.ToArray();
        foreach (var d in snapshot)
        {
            try { ((Action<TEvent>)d)(ev); }
            catch (Exception e) { GD.PrintErr($"[EventBus<{typeof(TEvent).Name}>] {e}"); }
        }
    }

    // ========================================
    // 弱类型 API（兼容期保留 — Sprint 5 迁移完成后清理）
    // ========================================

    public void Subscribe(string signal, Action<Godot.Collections.Dictionary> callback)
    {
        if (!_subscribers.ContainsKey(signal)) _subscribers[signal] = new();
        _subscribers[signal].Add(callback);
    }

    public void Unsubscribe(string signal, Action<Godot.Collections.Dictionary> callback)
    {
        if (_subscribers.TryGetValue(signal, out var list)) list.Remove(callback);
    }

    /// <summary>发布事件 — FIX: 不修改输入字典，创建副本</summary>
    public void Publish(string signal, Godot.Collections.Dictionary? data = null)
    {
        if (!_subscribers.TryGetValue(signal, out var list)) return;

        // FIX: 创建新字典而非修改传入的字典
        var eventData = new Godot.Collections.Dictionary();
        if (data != null) foreach (var key in data.Keys) eventData[key] = data[key];
        eventData["_signal"] = signal;
        eventData["_timestamp"] = Time.GetTicksMsec();

        foreach (var callback in new List<Action<Godot.Collections.Dictionary>>(list))
        {
            try { callback(eventData); }
            catch (Exception e) { GD.PrintErr($"[EventBus] Error in handler for '{signal}': {e.Message}"); }
        }
    }

    // ========================================
    // 双发便捷方法（强类型 + 旧 Dict 兼容）
    // ========================================

    public void PublishUnitDied(Node3D unit, bool isPlayer)
    {
        Publish(new UnitDiedEvent(unit, isPlayer));
        Publish(Signals.UnitDied, new Godot.Collections.Dictionary { { "unit", unit }, { "is_player", isPlayer } });
    }

    public void PublishUnitDamaged(Node3D unit, int damage, int remainingHp)
    {
        Publish(new UnitDamagedEvent(unit, damage, remainingHp));
        Publish(Signals.UnitDamaged, new Godot.Collections.Dictionary { { "unit", unit }, { "damage", damage }, { "remaining_hp", remainingHp } });
    }

    public void PublishSkillUsed(Node3D caster, string skillEffect, bool success)
    {
        Publish(new SkillUsedEvent(caster, skillEffect, success));
        Publish(Signals.SkillUsed, new Godot.Collections.Dictionary { { "caster", caster }, { "skill_effect", skillEffect }, { "success", success } });
    }

    public void PublishGoldChanged(int oldAmount, int newAmount, int delta)
    {
        Publish(Signals.GoldChanged, new Godot.Collections.Dictionary { { "old_amount", oldAmount }, { "new_amount", newAmount }, { "delta", delta } });
    }

    public void PublishCombatStarted(int playerCount, int enemyCount)
    {
        Publish(new CombatStartedEvent(playerCount, enemyCount));
        Publish(Signals.CombatStarted, new Godot.Collections.Dictionary { { "player_count", playerCount }, { "enemy_count", enemyCount } });
    }

    public void PublishTurnStarted(int state)
    {
        Publish(new TurnStartedEvent(state));
        Publish(Signals.TurnStarted, new Godot.Collections.Dictionary { { "state", state } });
    }

    public void PublishDayPassed(int daysPassed, int year, int month, int day)
    {
        Publish(new DayPassedEvent(daysPassed, year, month, day));
        Publish(Signals.DayPassed, new Godot.Collections.Dictionary
        {
            { "days_passed", daysPassed }, { "year", year }, { "month", month }, { "day", day },
        });
    }

    /// <summary>发布战斗结果 — 战略层通过此事件接收战斗结果</summary>
    public void PublishBattleOutcome(BattleOutcome outcome)
    {
        if (outcome == null) return;
        Publish(new CombatEndedEvent(outcome));
        Publish(Signals.CombatEnded, outcome.Serialize());
    }
}
