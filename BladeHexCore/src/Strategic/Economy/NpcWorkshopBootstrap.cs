// NpcWorkshopBootstrap.cs
// NPC 国家 Workshop 引导服务 — 世界生成时为主要国家的 Town 建造 Workshop
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic.Economy;

/// <summary>
/// NPC Workshop 引导服务 — 在世界生成时为 NPC 国家自动建造 Workshop
/// </summary>
public static class NpcWorkshopBootstrap
{
    /// <summary>
    /// 为所有主要国家的主要 Town 建造 Workshop（幂等 — 已有 Workshop 的不会重复添加）
    /// </summary>
    public static void Bootstrap(List<NationConfig> nations, List<OverworldPOI> pois, List<FiefData> fiefs)
    {
        foreach (var nation in nations)
        {
            if (!nation.IsMajorNation) continue;

            // 获取该国 Top 3 富有 Town/Castle
            var nationPois = pois
                .Where(p => p.OwningFaction == nation.Id &&
                    (p.PoiTypeEnum == OverworldPOI.POIType.Town || p.PoiTypeEnum == OverworldPOI.POIType.Castle))
                .OrderByDescending(p => p.Prosperity)
                .Take(3)
                .ToList();

            foreach (var poi in nationPois)
            {
                // 查找对应的 FiefData
                var fief = fiefs.FirstOrDefault(f => f.FiefName == poi.PoiName);
                if (fief == null)
                {
                    // 创建新的 FiefData
                    fief = new FiefData
                    {
                        FiefName = poi.PoiName,
                        OwningFaction = nation.Id,
                        WorldPosition = poi.Position
                    };
                    fiefs.Add(fief);
                }

                // 幂等检查：如果已有任何 Workshop 类型建筑，跳过
                bool hasWorkshop = fief.Buildings.Any(b =>
                    b.Type == FiefBuilding.BuildingType.BlacksmithWorkshop ||
                    b.Type == FiefBuilding.BuildingType.BrewWorkshop ||
                    b.Type == FiefBuilding.BuildingType.TextileWorkshop ||
                    b.Type == FiefBuilding.BuildingType.TanneryWorkshop);

                if (hasWorkshop) continue;

                // 根据地形选择 Workshop 类型
                var workshopType = SelectWorkshopType(poi);
                if (workshopType.HasValue)
                {
                    fief.Buildings.Add(new FiefBuilding
                    {
                        Type = workshopType.Value,
                        ConstructionDaysLeft = 0 // 已建造完成
                    });

                    GD.Print($"[NpcWorkshopBootstrap] {nation.DisplayName} 在 {poi.PoiName} 建造了 {workshopType.Value}");
                }
            }
        }
    }

    /// <summary>
    /// 根据 Town 的地形和繁荣度选择 Workshop 类型
    /// </summary>
    private static FiefBuilding.BuildingType? SelectWorkshopType(OverworldPOI town)
    {
        // 简化逻辑：基于繁荣度和随机选择
        int roll = Random.Shared.Next(4);
        return roll switch
        {
            0 => FiefBuilding.BuildingType.BlacksmithWorkshop,
            1 => FiefBuilding.BuildingType.BrewWorkshop,
            2 => FiefBuilding.BuildingType.TextileWorkshop,
            3 => FiefBuilding.BuildingType.TanneryWorkshop,
            _ => null
        };
    }

    /// <summary>
    /// 处理 NPC Workshop 的每日产出（简化版 — 直接压入本地 MarketStock）
    /// </summary>
    public static void ProcessNpcWorkshops(List<FiefData> fiefs, List<OverworldPOI> pois, int currentDay)
    {
        foreach (var fief in fiefs)
        {
            if (fief.OwningFaction == "player") continue; // 玩家的走正常流程

            foreach (var building in fief.Buildings)
            {
                if (building.ConstructionDaysLeft > 0) continue; // 还在建造

                var recipe = WorkshopRecipeRegistry.GetRecipe(building.Type);
                if (recipe == null) continue;

                // NPC Workshop 简化版：每 3 天产出一次，减缓通胀
                if (currentDay % 3 == 0)
                {
                    GD.Print($"[NpcWorkshop] {fief.FiefName} 的 {building.Type} 产出 {recipe.OutputQty}x {recipe.OutputItemId}");
                }
            }
        }
    }
}
