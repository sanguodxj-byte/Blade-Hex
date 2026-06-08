// CombatEvents.cs
// EventBus 强类型事件载荷 — 投射物、状态效果、经济事件。
//
// 这些 record 是不可变的事件数据快照。
// 订阅方式：
//   EventBus.Instance?.Subscribe<StatusEffectAppliedEvent>(OnEffectApplied);
// 发布方式：
//   EventBus.Instance?.PublishStatusEffectApplied(unit, "poison", 3);
using Godot;
using BladeHex.Combat;

namespace BladeHex.Events.Payloads;

// ──── 投射物事件 ────
// 注意：Core ProjectileSystem 不持有场景节点引用，Bridge 仅能提供 Vector3 位置 + 序列化数据。
// 因此投射物事件使用 Vector3 而非 Node3D，以便 Bridge 能正确构造。

/// <summary>投射物发射 — 由 ProjectileEventBridge 从 Core Dictionary 提取后发布。</summary>
public sealed record ProjectileLaunchedEvent(ProjectileData Data, Vector3 FromWorld, Vector3 ToWorld, string ProjectileType, float TravelTime);

/// <summary>投射物命中 — 由 ProjectileEventBridge 从 Core Dictionary 提取后发布。</summary>
public sealed record ProjectileImpactEvent(string ProjectileType, int Damage);

// ──── 状态效果事件 ────

/// <summary>状态效果施加 — 由 StatusEffectManager.ApplyEffect 发布。</summary>
public sealed record StatusEffectAppliedEvent(Unit Unit, string EffectId, int Duration);

/// <summary>状态效果移除 — 由 StatusEffectManager.RemoveEffect 发布。</summary>
public sealed record StatusEffectRemovedEvent(Unit Unit, string EffectId);

/// <summary>状态效果Tick伤害 — 由 StatusEffectManager.TickEffects 发布。</summary>
public sealed record StatusEffectTickedEvent(Unit Unit, string EffectId, int Damage);

// ──── 经济事件 ────

/// <summary>金币变动 — 由 EconomyManager 发布。</summary>
public sealed record GoldChangedEvent(int OldAmount, int NewAmount, int Delta);

/// <summary>食物变动 — 由 EconomyManager 发布。</summary>
public sealed record FoodChangedEvent(int OldAmount, int NewAmount, int Delta);
