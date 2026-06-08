// SimulationEventDispatcher.cs
// 模拟事件调度器 — Core 事件 → Godot 信号转发
//
// 职责:
//   - 消费 OverworldSimulation.Tick*() 返回的事件列表
//   - 转发为 OverworldEntityManager 的 Godot 信号
//   - 处理玩家领土追踪（PoiCaptured）
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// 模拟事件调度器 — 将 Core 层 OverworldSimulationEvent 转发为 Godot 信号。
/// 
/// 使用方式:
///   var dispatcher = new SimulationEventDispatcher(entityMgr);
///   dispatcher.Dispatch(simulation.TickDay(ctx));
/// </summary>
public sealed class SimulationEventDispatcher
{
    private readonly OverworldEntityManager _entityMgr;
    private readonly WorldEventEngine? _worldEngine;

    public SimulationEventDispatcher(OverworldEntityManager entityMgr, WorldEventEngine? worldEngine = null)
    {
        _entityMgr = entityMgr;
        _worldEngine = worldEngine;
    }

    /// <summary>
    /// 消费一批模拟事件，转发为 Godot 信号。
    /// 按事件类型分批发射以避免信号过多。
    /// </summary>
    public void Dispatch(List<OverworldSimulationEvent> events)
    {
        if (events == null || events.Count == 0) return;

        foreach (var evt in events)
        {
            switch (evt.Type)
            {
                case OverworldSimulationEvent.EventType.EntitySpawned:
                    if (evt.EntityA != null)
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.EntitySpawned, evt.EntityA);
                    break;

                case OverworldSimulationEvent.EventType.EntityRemoved:
                    if (evt.EntityA != null)
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.EntityRemoved, evt.EntityA);
                    break;

                case OverworldSimulationEvent.EventType.SiegeStarted:
                    if (evt.Poi != null && evt.EntityA != null)
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.SiegeStarted, evt.Poi, evt.EntityA);
                    break;

                case OverworldSimulationEvent.EventType.SiegeResolved:
                    if (evt.Poi != null && evt.EntityA != null)
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.SiegeResolved, evt.Poi, evt.AttackerWon, evt.EntityA);
                    break;

                case OverworldSimulationEvent.EventType.PoiCaptured:
                    if (evt.Poi != null && evt.EntityA != null)
                    {
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.PoiCaptured, evt.Poi, evt.NewFaction, evt.EntityA);

                        // M7: 处理玩家征服（从 OnPoiTransferred 迁移）
                        if (evt.NewFaction == "player")
                        {
                            if (_entityMgr.PlayerKingdom != null)
                            {
                                if (!_entityMgr.PlayerKingdom.ControlledPoiNames.Contains(evt.Poi.PoiName))
                                {
                                    _entityMgr.PlayerKingdom.ControlledPoiNames.Add(evt.Poi.PoiName);
                                    GD.Print($"[PlayerKingdom] 王国新增领土: {evt.Poi.PoiName}");
                                }
                            }
                            else
                            {
                                if (!_entityMgr.PendingConquests.Contains(evt.Poi.PoiName))
                                {
                                    _entityMgr.PendingConquests.Add(evt.Poi.PoiName);
                                    GD.Print($"[PlayerKingdom] 新增待征服领土: {evt.Poi.PoiName}");
                                }
                            }
                        }
                    }
                    break;

                case OverworldSimulationEvent.EventType.ReinforcementArrived:
                    if (evt.Poi != null && evt.EntityA != null)
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.ReinforcementArrived, evt.Poi, evt.EntityA);
                    break;

                case OverworldSimulationEvent.EventType.AiBattleOccurred:
                    if (evt.EntityA != null && evt.EntityB != null)
                        _entityMgr.EmitSignal(OverworldEntityManager.SignalName.AiBattleOccurred, evt.EntityA, evt.EntityB, evt.AttackerWon);
                    break;

                case OverworldSimulationEvent.EventType.NewsAdded:
                    // News added via WorldEngine.AddNews, no signal needed
                    break;

                case OverworldSimulationEvent.EventType.PeaceProposalRaised:
                    // WarSystemMVP.AiProposedPeaceToPlayer handles this via EventBus
                    break;
            }
        }
    }
}