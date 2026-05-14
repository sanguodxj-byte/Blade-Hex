// RecruitPool.cs
// 城镇招募池 — 每个 POI 维护一份可招募列表，按周刷新
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 单个可招募单位
/// </summary>
[GlobalClass]
public partial class RecruitableUnit : Resource
{
    [Export] public string TemplateId { get; set; } = "";
    [Export] public int Cost { get; set; } = 50;
    [Export] public int WeeklyWage { get; set; } = 10;
    [Export] public UnitData? Unit { get; set; }
}

/// <summary>
/// 城镇招募池 — 持有当前可招募列表 + 刷新逻辑
/// </summary>
[GlobalClass]
public partial class RecruitPool : Resource
{
    [Export] public string PoiId { get; set; } = "";
    [Export] public int LastRefreshDay { get; set; } = 0;
    [Export] public int RefreshIntervalDays { get; set; } = 7;

    public List<RecruitableUnit> Available { get; set; } = new();

    /// <summary>是否需要刷新</summary>
    public bool NeedsRefresh(int currentDay) => currentDay - LastRefreshDay >= RefreshIntervalDays;

    /// <summary>刷新招募池</summary>
    public void Refresh(int currentDay, NationConfig? nation, int poiTier, int seed)
    {
        LastRefreshDay = currentDay;
        Available.Clear();

        var rng = new Random(seed ^ (PoiId.GetHashCode()) ^ (currentDay / RefreshIntervalDays));

        // 数量：村庄 2-3，城镇 4-6，首都 6-8
        int count = poiTier switch
        {
            0 => 2 + rng.Next(2),  // village
            1 => 4 + rng.Next(3),  // town
            _ => 6 + rng.Next(3),  // capital
        };

        string[] pool = nation?.RecruitPool ?? new[] { "militia", "archer" };
        RaceData? race = GetRaceForNation(nation);

        for (int i = 0; i < count; i++)
        {
            string templateId = pool[rng.Next(pool.Length)];
            int level = 1 + rng.Next(3); // 1-3 级
            var unit = CharacterGenerator.GenerateCharacter(race, level, seedVal: rng.Next());

            // 按模板覆盖名字前缀
            unit.UnitName = $"{GetTemplateName(templateId)}·{unit.UnitName}";

            int baseCost = 30 + level * 20;
            int baseWage = 5 + level * 5;

            Available.Add(new RecruitableUnit
            {
                TemplateId = templateId,
                Cost = baseCost + rng.Next(20),
                WeeklyWage = baseWage + rng.Next(5),
                Unit = unit,
            });
        }
    }

    private static RaceData? GetRaceForNation(NationConfig? nation)
    {
        if (nation == null) return null;
        return nation.Race switch
        {
            "human" => RaceData.GetRaceById(RaceData.Race.Human),
            "elf" => RaceData.GetRaceById(RaceData.Race.Elf),
            "dwarf" => RaceData.GetRaceById(RaceData.Race.Dwarf),
            "orc" => RaceData.GetRaceById(RaceData.Race.HalfOrc),
            _ => RaceData.GetRaceById(RaceData.Race.Human),
        };
    }

    private static string GetTemplateName(string templateId) => templateId switch
    {
        "knight" => "骑士",
        "spearman" => "长矛兵",
        "archer" => "弓箭手",
        "militia" => "民兵",
        "crossbowman" => "弩手",
        "swordsman" => "剑士",
        "ranger" => "游侠",
        "druid" => "德鲁伊",
        "elf_archer" => "精灵弓手",
        "blade_dancer" => "剑舞者",
        "dwarf_warrior" => "矮人战士",
        "dwarf_crossbow" => "矮人弩手",
        "dwarf_ironbreaker" => "铁卫",
        "orc_berserker" => "狂战士",
        "orc_shaman" => "兽人萨满",
        "boar_rider" => "野猪骑兵",
        "orc_archer" => "兽人弓手",
        _ => "佣兵",
    };
}
