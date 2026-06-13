// OverworldSimulationEvent.cs
// 大地图模拟结构化事件 — Core 层替代 Godot 信号，由 Frontend Adapter 转发
//
// 设计目标:
//   - 让 OverworldSimulation.Tick*() 返回结构化事件列表
//   - Frontend (OverworldEntityManager) 消费事件并转发为 Godot 信号
//   - 测试可直接检查事件列表，无需连接信号
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图模拟事件 — Core 层输出，不包含 Godot 信号依赖。
/// OverworldEntityManager (Frontend Adapter) 消费并转发为 Godot 信号。
/// </summary>
public readonly struct OverworldSimulationEvent
{
    /// <summary>事件类型</summary>
    public EventType Type { get; }
    
    // ========================================
    // 关联数据（按事件类型选择性填充）
    // ========================================
    
    /// <summary>涉及实体 A（EntitySpawned/EntityRemoved/AiBattleOccurred）</summary>
    public OverworldEntity? EntityA { get; }
    
    /// <summary>涉及实体 B（AiBattleOccurred 的防守方）</summary>
    public OverworldEntity? EntityB { get; }
    
    /// <summary>涉及 POI（SiegeStarted/SiegeResolved/PoiCaptured/ReinforcementArrived）</summary>
    public OverworldPOI? Poi { get; }
    
    /// <summary>新归属势力（PoiCaptured）</summary>
    public string NewFaction { get; }
    
    /// <summary>攻击方是否胜利（SiegeResolved/AiBattleOccurred）</summary>
    public bool AttackerWon { get; }
    
    /// <summary>附加字符串数据（NewsAdded 的 newsKey/PeaceProposalRaised 的 proposer）</summary>
    public string StringData { get; }

    /// <summary>事件类型枚举</summary>
    public enum EventType
    {
        EntitySpawned,
        EntityRemoved,
        SiegeStarted,
        SiegeResolved,
        PoiCaptured,
        ReinforcementArrived,
        AiBattleOccurred,
        SubPartyRejoined,
        NewsAdded,
        PeaceProposalRaised,
    }

    // ========================================
    // 工厂方法 — 比构造函数更可读
    // ========================================

    public static OverworldSimulationEvent EntitySpawned(OverworldEntity entity)
        => new(EventType.EntitySpawned, entityA: entity);

    public static OverworldSimulationEvent EntityRemoved(OverworldEntity entity)
        => new(EventType.EntityRemoved, entityA: entity);

    public static OverworldSimulationEvent SiegeStarted(OverworldPOI target, OverworldEntity attacker)
        => new(EventType.SiegeStarted, entityA: attacker, poi: target);

    public static OverworldSimulationEvent SiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
        => new(EventType.SiegeResolved, entityA: attacker, poi: target, attackerWon: attackerWon);

    public static OverworldSimulationEvent PoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
        => new(EventType.PoiCaptured, entityA: captor, poi: poi, newFaction: newFaction);

    public static OverworldSimulationEvent ReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer)
        => new(EventType.ReinforcementArrived, entityA: reinforcer, poi: targetPoi);

    public static OverworldSimulationEvent AiBattleOccurred(OverworldEntity attacker, OverworldEntity defender, bool attackerWon)
        => new(EventType.AiBattleOccurred, entityA: attacker, entityB: defender, attackerWon: attackerWon);

    public static OverworldSimulationEvent SubPartyRejoined(OverworldEntity entity)
        => new(EventType.SubPartyRejoined, entityA: entity);

    public static OverworldSimulationEvent NewsAdded(string newsKey)
        => new(EventType.NewsAdded, stringData: newsKey);

    public static OverworldSimulationEvent PeaceProposalRaised(string proposer)
        => new(EventType.PeaceProposalRaised, stringData: proposer);

    private OverworldSimulationEvent(
        EventType type,
        OverworldEntity? entityA = null,
        OverworldEntity? entityB = null,
        OverworldPOI? poi = null,
        string? newFaction = null,
        bool attackerWon = false,
        string? stringData = null)
    {
        Type = type;
        EntityA = entityA;
        EntityB = entityB;
        Poi = poi;
        NewFaction = newFaction ?? "";
        AttackerWon = attackerWon;
        StringData = stringData ?? "";
    }
}