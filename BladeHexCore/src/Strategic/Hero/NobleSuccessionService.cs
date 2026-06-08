// NobleSuccessionService.cs
// 贵族补员服务 — 当某国领主数量低于阈值时自动补充新领主
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Hero;

/// <summary>
/// 贵族补员服务 — 每 30 天审计一次，补充战死的领主
/// </summary>
public static class NobleSuccessionService
{
    /// <summary>
    /// 审计所有国家的领主数量，低于阈值时补充
    /// </summary>
    public static void Audit(
        HeroRegistry heroRegistry,
        FamilyRegistry familyRegistry,
        List<NationConfig> nations,
        List<OverworldPOI> pois,
        SpecialCharacterGenerator.GenerationConfig config,
        int currentDay,
        WorldEventEngine worldEngine)
    {
        foreach (var nation in nations)
        {
            int currentLords = heroRegistry.GetByFaction(nation.Id).Count;
            int minLords = nation.IsMajorNation ? config.MajorNationLordsMin : config.MinorNationLordsMin;
            int maxLords = nation.IsMajorNation ? config.MajorNationLordsMax : config.MinorNationLordsMax;

            // 低于 50% 阈值时补员
            int threshold = minLords / 2;
            if (currentLords < threshold)
            {
                int deficit = threshold - currentLords;
                int cap = (int)(maxLords * 1.2); // 上限保护

                // 获取该国主要 POI 用于新闻定位
                var nationPois = pois.Where(p => p.OwningFaction == nation.Id &&
                    (p.PoiTypeEnum == OverworldPOI.POIType.Town || p.PoiTypeEnum == OverworldPOI.POIType.Castle)).ToList();
                var newsPos = nationPois.Count > 0 ? nationPois[0].Position : Vector2.Zero;

                for (int i = 0; i < deficit && currentLords + i < cap; i++)
                {
                    var successor = CreateSuccessor(nation, heroRegistry, familyRegistry, pois, currentDay);
                    if (successor != null)
                    {
                        worldEngine.AddNews(
                            "hero_succession",
                            $"⚜ {nation.DisplayName}的{successor.FamilyName}家族涌现新领主 {successor.DisplayName}，继承家族荣耀！",
                            newsPos);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 创建继承者 — 从该国既有家族中选择一个，创建新领主
    /// </summary>
    private static HeroData? CreateSuccessor(
        NationConfig nation,
        HeroRegistry heroRegistry,
        FamilyRegistry familyRegistry,
        List<OverworldPOI> pois,
        int currentDay)
    {
        // 获取该国的家族
        var families = familyRegistry.GetByFaction(nation.Id);
        if (families.Count == 0) return null;

        // 随机选择一个家族
        var family = families[Random.Shared.Next(families.Count)];

        // 计算该国领主平均等级
        var existingLords = heroRegistry.GetByFaction(nation.Id);
        int avgLevel = existingLords.Count > 0
            ? (int)existingLords.Average(h => h.Birthday) // 用 Birthday 近似等级（实际应存 Level）
            : 30;
        int newLevel = Math.Max(1, avgLevel - 2); // 年轻一辈，等级略低

        // 选择出生位置：该国主要 Town/Castle
        var nationPois = pois.Where(p => p.OwningFaction == nation.Id &&
            (p.PoiTypeEnum == OverworldPOI.POIType.Town || p.PoiTypeEnum == OverworldPOI.POIType.Castle)).ToList();
        if (nationPois.Count == 0) return null;

        var birthPoi = nationPois[Random.Shared.Next(nationPois.Count)];

        // 生成名字（复用家族姓氏）
        string givenName = GenerateGivenName(nation.Race);
        string fullName = $"{givenName}·{family.FamilyName}";

        // 创建 HeroData
        var hero = heroRegistry.Create(
            nation.Id,
            fullName,
            family.FamilyName,
            (OverworldPOI.LordPersonality)Random.Shared.Next(3),
            currentDay);

        // 添加到家族
        familyRegistry.AddMember(family.FamilyName, hero.HeroId);

        GD.Print($"[NobleSuccession] {nation.DisplayName} 补员: {fullName} (家族: {family.FamilyName})");
        return hero;
    }

    private static string GenerateGivenName(string race)
    {
        var names = race switch
        {
            "elf" => new[] { "萨拉萨斯", "赛瓦隆", "瓦琳卓", "埃伦娜莉", "法伦", "艾洛温" },
            "dwarf" => new[] { "巴尔古夫", "索恩", "都灵", "索林", "格罗格", "布洛克" },
            "orc" => new[] { "格罗姆", "阿佐格", "加尔鲁什", "莫克", "克罗格", "乌加什" },
            _ => new[] { "阿拉里克", "塞德里克", "罗兰", "瓦勒留", "爱德华", "雷蒙德" }
        };
        return names[Random.Shared.Next(names.Length)];
    }
}
