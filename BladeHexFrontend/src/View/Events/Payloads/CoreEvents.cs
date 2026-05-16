// CoreEvents.cs
// EventBus 强类型事件载荷 — 服务于架构优化 spec R6。
//
// 这些 record 是不可变的事件数据快照。
// 订阅方式：
//   EventBus.Instance?.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
// 发布方式：
//   EventBus.Instance?.Publish(new UnitDamagedEvent(unit, 10, 80));
using Godot;
using BladeHex.Strategic;

namespace BladeHex.Events.Payloads;

/// <summary>战斗开始 — 由 CombatManager.StartCombat 发布。</summary>
public sealed record CombatStartedEvent(int PlayerCount, int EnemyCount);

/// <summary>战斗结束 — 由 CombatManager.EndCombat 发布，附带完整结果。</summary>
public sealed record CombatEndedEvent(BattleOutcome Outcome);

/// <summary>回合开始 — 玩家或敌方回合切换时发布。</summary>
public sealed record TurnStartedEvent(int State);

/// <summary>单位受伤。</summary>
public sealed record UnitDamagedEvent(Node3D Unit, int Damage, int RemainingHp);

/// <summary>单位死亡。</summary>
public sealed record UnitDiedEvent(Node3D Unit, bool IsPlayer);

/// <summary>技能使用。</summary>
public sealed record SkillUsedEvent(Node3D Caster, string SkillEffect, bool Success);

/// <summary>每日推进 — 由 EconomyManager 时间循环发布。</summary>
public sealed record DayPassedEvent(int DaysPassed, int Year, int Month, int Day);
