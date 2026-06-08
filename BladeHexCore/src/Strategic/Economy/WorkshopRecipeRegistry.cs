// WorkshopRecipeRegistry.cs
// 作坊配方注册中心
using System.Collections.Generic;

namespace BladeHex.Strategic.Economy;

public static class WorkshopRecipeRegistry
{
    private static readonly Dictionary<FiefBuilding.BuildingType, WorkshopProductionRecipe> _recipes = new()
    {
        [FiefBuilding.BuildingType.BlacksmithWorkshop] = new()
        {
            Type = FiefBuilding.BuildingType.BlacksmithWorkshop,
            OutputItemId = "iron_sword",
            OutputQty = 1,
            RawCostGold = 40
        },
        [FiefBuilding.BuildingType.BrewWorkshop] = new()
        {
            Type = FiefBuilding.BuildingType.BrewWorkshop,
            OutputItemId = "rations",
            OutputQty = 5,
            RawCostGold = 15
        },
        [FiefBuilding.BuildingType.TextileWorkshop] = new()
        {
            Type = FiefBuilding.BuildingType.TextileWorkshop,
            OutputItemId = "linen_cloth",
            OutputQty = 4,
            RawCostGold = 20
        },
        [FiefBuilding.BuildingType.TanneryWorkshop] = new()
        {
            Type = FiefBuilding.BuildingType.TanneryWorkshop,
            OutputItemId = "leather_armor",
            OutputQty = 1,
            RawCostGold = 30
        }
    };

    public static WorkshopProductionRecipe? GetRecipe(FiefBuilding.BuildingType type)
    {
        return _recipes.TryGetValue(type, out var r) ? r : null;
    }

    public static IEnumerable<WorkshopProductionRecipe> GetAll() => _recipes.Values;
}
