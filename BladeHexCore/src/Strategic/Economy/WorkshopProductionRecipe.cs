// WorkshopProductionRecipe.cs
// 作坊每日产出配方配置数据
using System;

namespace BladeHex.Strategic.Economy;

public class WorkshopProductionRecipe
{
    public FiefBuilding.BuildingType Type { get; set; }
    public string OutputItemId { get; set; } = "";
    public int OutputQty { get; set; }
    public int RawCostGold { get; set; }
    public string[] RequiredCulture { get; set; } = Array.Empty<string>();
}
