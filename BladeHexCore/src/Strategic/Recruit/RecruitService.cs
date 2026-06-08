// RecruitService.cs
// 招募服务 — 管理所有 POI 的招募池，提供招募/查询接口
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic.Kingdom;

namespace BladeHex.Strategic;

/// <summary>
/// 招募服务 — 全局单例，管理所有城镇的招募池
/// </summary>
[GlobalClass]
public partial class RecruitService : RefCounted
{
    private readonly Dictionary<string, RecruitPool> _pools = new();
    private List<NationConfig> _nations = new();
    private List<OverworldPOI> _pois = new();
    private int _worldSeed = 0;

    /// <summary>初始化（传入世界数据并订阅易手事件）</summary>
    public void Initialize(List<OverworldPOI> pois, List<NationConfig>? nations, int worldSeed)
    {
        _pois = pois;
        _nations = nations ?? new();
        _worldSeed = worldSeed;

        // 订阅易手事件以重置招募池
        PoiTransferService.PoiTransferred += OnPoiTransferred;
    }

    private void OnPoiTransferred(PoiTransferEvent evt)
    {
        if (evt?.Poi != null)
        {
            string poiId = evt.Poi.PoiName;
            _pools.Remove(poiId);
            GD.Print($"[RecruitService] 聚落易手，已重置其招募池: {poiId}");
        }
    }

    /// <summary>
    /// 获取指定 POI 的招募池（自动创建 + 按需刷新）
    /// </summary>
    public RecruitPool GetPool(string poiId, int currentDay, PlayerKingdom? playerKingdom = null)
    {
        if (!_pools.TryGetValue(poiId, out var pool))
        {
            pool = new RecruitPool { PoiId = poiId };
            _pools[poiId] = pool;
        }

        if (pool.NeedsRefresh(currentDay))
        {
            var poi = FindPoi(poiId);
            var nation = FindNationForPoi(poi);
            int tier = GetPoiTier(poi);

            // M7: 应用征兵权法律修正
            float conscriptionMult = 1.0f;
            if (playerKingdom != null && poi?.OwningFaction == "player")
            {
                conscriptionMult = KingdomLawEffects.GetRecruitMultiplier(playerKingdom.Laws.Conscription);
            }

            pool.Refresh(currentDay, nation, tier, _worldSeed, conscriptionMult);
            GD.Print($"[RecruitService] 刷新招募池: {poiId}, 可招 {pool.Available.Count} 人");
        }

        return pool;
    }

    /// <summary>
    /// 执行招募：扣钱 + 加入 Roster + 从池中移除
    /// </summary>
    /// <returns>成功返回招募的 UnitData，失败返回 null</returns>
    public UnitData? Recruit(string poiId, int index, PartyRoster roster, int currentDay, GodotObject? economyManager)
    {
        var pool = GetPool(poiId, currentDay);
        if (index < 0 || index >= pool.Available.Count) return null;

        var recruit = pool.Available[index];
        if (recruit.Unit == null) return null;

        // 队伍满员
        if (roster.IsFull)
        {
            GD.Print("[RecruitService] 队伍已满，无法招募");
            return null;
        }

        // 扣钱（通过 GodotObject.Call 调用 EconomyManager.SpendGold）
        if (economyManager != null)
        {
            var result = economyManager.Call("SpendGold", recruit.Cost);
            if (!result.AsBool())
            {
                GD.Print("[RecruitService] 金币不足");
                return null;
            }
        }

        // 加入队伍
        var unit = recruit.Unit;
        PartyRoster.SetCurrentHp(unit, unit.BaseMaxHp);
        roster.Add(unit);

        // 从池中移除
        pool.Available.RemoveAt(index);

        GD.Print($"[RecruitService] 招募成功: {unit.UnitName} (费用 {recruit.Cost}, 周薪 {recruit.WeeklyWage})");
        return unit;
    }

    /// <summary>获取招募池的 Godot Array（兼容）</summary>
    public Godot.Collections.Array GetAvailableGd(string poiId, int currentDay)
    {
        var pool = GetPool(poiId, currentDay);
        var arr = new Godot.Collections.Array();
        foreach (var r in pool.Available) arr.Add(r);
        return arr;
    }

    private OverworldPOI? FindPoi(string poiId)
    {
        foreach (var p in _pois)
            if (p.PoiName == poiId) return p;
        return null;
    }

    private NationConfig? FindNationForPoi(OverworldPOI? poi)
    {
        if (poi == null || string.IsNullOrEmpty(poi.OwningFaction)) return null;
        foreach (var n in _nations)
            if (n.Id == poi.OwningFaction) return n;
        return null;
    }

    private static int GetPoiTier(OverworldPOI? poi)
    {
        if (poi == null) return 0;
        return poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Village => 0,
            OverworldPOI.POIType.Town => poi.PoiName.Contains("首都") ? 2 : 1,
            OverworldPOI.POIType.Castle => 2,
            _ => 0,
        };
    }
}
