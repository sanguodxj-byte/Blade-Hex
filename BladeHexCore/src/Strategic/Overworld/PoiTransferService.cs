using System;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// POI 易手事件数据
/// </summary>
public class PoiTransferEvent
{
    public string OldFaction { get; set; } = "";
    public string NewFaction { get; set; } = "";
    public OverworldPOI Poi { get; set; } = null!;
    public OverworldEntity? Captor { get; set; }
    public int Day { get; set; }
}

/// <summary>
/// POI 易手服务，处理 POI 占领及相关副作用
/// </summary>
public static class PoiTransferService
{
    /// <summary>
    /// 全局 POI 易手事件，供招募池、市场、迷雾、任务系统等订阅
    /// </summary>
    public static event Action<PoiTransferEvent>? PoiTransferred;

    /// <summary>
    /// 执行 POI 易手变更并触发相关逻辑
    /// </summary>
    /// <param name="poi">被易手的 POI</param>
    /// <param name="newFaction">新势力 ID</param>
    /// <param name="captor">攻取者实体（可空）</param>
    /// <param name="currentDay">当前游戏日</param>
    /// <param name="engine">世界事件引擎（可空，主要用于新闻/影响力）</param>
    /// <param name="playerNearby">玩家是否在易手现场附近（用于影响力发放）</param>
    public static void Apply(
        OverworldPOI poi,
        string newFaction,
        OverworldEntity? captor,
        int currentDay,
        WorldEventEngine? engine,
        bool playerNearby = false)
    {
        if (poi == null) return;

        string oldFaction = poi.OwningFaction;

        // 1. 切换势力归属
        poi.OwningFaction = newFaction;

        // 2. 守军重置为最大守军的 1/4
        poi.GarrisonCurrent = poi.GarrisonMax / 4;

        // 3. 繁荣度扣减 30 点（不低于 0）
        poi.Prosperity = Math.Max(0, poi.Prosperity - 30);

        // 4. 解除围攻状态
        poi.EndSiege();

        // 5. 新闻 + 影响力发放（不强制改双方关系，外交关系由 War 状态/玩家决议管理）
        if (engine != null)
        {
            engine.AddNews("poi_captured", $"{poi.PoiName} 被 {newFaction} 夺取！", poi.Position);

            // 玩家在场亲眼见证（由调用方判定距离）
            if (playerNearby && !string.IsNullOrEmpty(newFaction))
            {
                engine.Influence.Add(newFaction, 30, $"玩家亲眼见证夺取 {poi.PoiName}");

                foreach (var alliance in engine.ActiveAlliances)
                {
                    if (alliance.NationA == newFaction)
                        engine.Influence.Add(alliance.NationB, 10, $"盟国 {newFaction} 夺取 {poi.PoiName}");
                    else if (alliance.NationB == newFaction)
                        engine.Influence.Add(alliance.NationA, 10, $"盟国 {newFaction} 夺取 {poi.PoiName}");
                }
            }
        }

        // 6. 广播事件
        var evt = new PoiTransferEvent
        {
            OldFaction = oldFaction,
            NewFaction = newFaction,
            Poi = poi,
            Captor = captor,
            Day = currentDay
        };
        PoiTransferred?.Invoke(evt);
    }
}
