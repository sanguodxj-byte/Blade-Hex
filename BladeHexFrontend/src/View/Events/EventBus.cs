// EventBus.cs — 全局事件总线（修复版：不修改输入字典）
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Events;

[GlobalClass]
public partial class EventBus : Node
{
    public static EventBus? Instance { get; private set; }

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

    private readonly Dictionary<string, List<Action<Godot.Collections.Dictionary>>> _subscribers = new();

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }

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

    // 便捷方法
    public void PublishUnitDied(Node3D unit, bool isPlayer)
        => Publish(Signals.UnitDied, new Godot.Collections.Dictionary { { "unit", unit }, { "is_player", isPlayer } });

    public void PublishUnitDamaged(Node3D unit, int damage, int remainingHp)
        => Publish(Signals.UnitDamaged, new Godot.Collections.Dictionary { { "unit", unit }, { "damage", damage }, { "remaining_hp", remainingHp } });

    public void PublishSkillUsed(Node3D caster, string skillEffect, bool success)
        => Publish(Signals.SkillUsed, new Godot.Collections.Dictionary { { "caster", caster }, { "skill_effect", skillEffect }, { "success", success } });

    public void PublishGoldChanged(int oldAmount, int newAmount, int delta)
        => Publish(Signals.GoldChanged, new Godot.Collections.Dictionary { { "old_amount", oldAmount }, { "new_amount", newAmount }, { "delta", delta } });

    /// <summary>发布战斗结果 — 战略层通过此事件接收战斗结果</summary>
    public void PublishBattleOutcome(BladeHex.Strategic.BattleOutcome outcome)
    {
        if (outcome != null)
            Publish(Signals.CombatEnded, outcome.Serialize());
    }
}
