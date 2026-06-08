// PlayerKingdomService.cs
// 玩家王国服务 — 创建、解散、分封、法律等核心操作
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Kingdom;

/// <summary>
/// 玩家王国服务 — 管理玩家王国的生命周期
/// </summary>
public static class PlayerKingdomService
{
    /// <summary>
    /// 检查是否满足开国条件
    /// </summary>
    public static (bool ok, string failReason) CanFoundKingdom(
        List<OverworldPOI> pois,
        InfluenceTracker influence,
        int playerLevel,
        List<string> pendingConquests)
    {
        // 条件 1: 占领 ≥ 1 个 Castle
        bool hasCastle = false;
        foreach (var poiName in pendingConquests)
        {
            var poi = pois.FirstOrDefault(p => p.PoiName == poiName);
            if (poi != null && poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
            {
                hasCastle = true;
                break;
            }
        }
        if (!hasCastle)
            return (false, "需要占领至少一个城堡");

        // 条件 2: 影响力 ≥ 100
        int playerInfluence = influence.Get("player");
        if (playerInfluence < 100)
            return (false, $"影响力不足（当前 {playerInfluence}/100）");

        // 条件 3: 玩家等级 ≥ 20
        if (playerLevel < 20)
            return (false, $"等级不足（当前 {playerLevel}/20）");

        return (true, "");
    }

    /// <summary>
    /// 创建王国
    /// </summary>
    public static PlayerKingdom Found(
        string kingdomName,
        string familyName,
        OverworldPOI capitalPoi,
        Color bannerColor,
        int currentDay,
        HeroRegistry heroRegistry,
        FamilyRegistry familyRegistry,
        List<NationConfig> nations,
        WorldEventEngine worldEngine,
        InfluenceTracker influence,
        List<string> pendingConquests)
    {
        // 1. 创建 NationConfig
        var nationConfig = new NationConfig
        {
            Id = "player",
            DisplayName = kingdomName,
            IsMajorNation = true,
            Race = "human"
        };
        nations.Add(nationConfig);

        // 2. 创建玩家王国
        var kingdom = new PlayerKingdom
        {
            KingdomId = "player",
            DisplayName = kingdomName,
            FamilyName = familyName,
            BannerColor = bannerColor,
            CapitalPoiName = capitalPoi.PoiName,
            FoundedDay = currentDay
        };

        // 3. 将 PendingConquests 加入 ControlledPoiNames
        kingdom.ControlledPoiNames.AddRange(pendingConquests);

        // 4. 玩家加入领主列表
        kingdom.LordHeroIds.Add("player");

        // 5. 创建玩家家族
        var playerHero = heroRegistry.Get("player");
        var familyMembers = new List<string> { "player" };

        // 6. 自动归入既有 Companion
        foreach (var hero in heroRegistry.AllHeroes)
        {
            if (hero.FactionId == "player" && hero.HeroId != "player")
            {
                familyMembers.Add(hero.HeroId);
                kingdom.LordHeroIds.Add(hero.HeroId);
            }
        }

        familyRegistry.Create(familyName, "player", "player", familyMembers, currentDay);

        // 7. 推送新闻
        worldEngine.AddNews(
            "kingdom_founded",
            $"🏰 传奇！玩家在 {capitalPoi.PoiName} 建立了 {kingdomName}，开启了新的王朝！",
            capitalPoi.Position);

        // 8. 设置与其他国家的默认关系
        foreach (var nation in nations)
        {
            if (nation.Id != "player")
            {
                // 默认中立
                worldEngine.SetRelation("player", nation.Id, 0);
            }
        }

        GD.Print($"[PlayerKingdomService] 王国创立: {kingdomName} (家族: {familyName}, 都城: {capitalPoi.PoiName})");
        return kingdom;
    }

    /// <summary>
    /// 解散王国（debug 用）
    /// </summary>
    public static void Disband(
        PlayerKingdom kingdom,
        List<NationConfig> nations,
        FamilyRegistry familyRegistry,
        WorldEventEngine worldEngine)
    {
        // 1. 移除 NationConfig
        nations.RemoveAll(n => n.Id == kingdom.KingdomId);

        // 2. 移除家族
        var family = familyRegistry.Get(kingdom.FamilyName);
        if (family != null)
        {
            // 清空成员
            foreach (var heroId in family.MemberHeroIds.ToList())
                familyRegistry.RemoveMember(family.FamilyName, heroId);
        }

        // 3. 推送新闻
        worldEngine.AddNews(
            "kingdom_disbanded",
            $"💀 {kingdom.DisplayName} 王国覆灭了...",
            Vector2.Zero);

        GD.Print($"[PlayerKingdomService] 王国解散: {kingdom.DisplayName}");
    }

    /// <summary>
    /// 分封 POI 给 Companion
    /// </summary>
    public static bool GrantFief(
        PlayerKingdom kingdom,
        string poiName,
        string companionHeroId,
        HeroRegistry heroRegistry)
    {
        // 验证 POI 属于王国
        if (!kingdom.ControlledPoiNames.Contains(poiName))
            return false;

        // 验证 Companion 是玩家阵营
        var hero = heroRegistry.Get(companionHeroId);
        if (hero == null || hero.FactionId != "player")
            return false;

        // 验证不是玩家自己
        if (companionHeroId == "player")
            return false;

        // 添加到领主列表（如果还没有）
        if (!kingdom.LordHeroIds.Contains(companionHeroId))
            kingdom.LordHeroIds.Add(companionHeroId);

        GD.Print($"[PlayerKingdomService] 分封 {poiName} 给 {hero.DisplayName}");
        return true;
    }

    /// <summary>
    /// 收回封地
    /// </summary>
    public static bool RevokeFief(
        PlayerKingdom kingdom,
        string poiName,
        string companionHeroId)
    {
        // 验证 POI 属于王国
        if (!kingdom.ControlledPoiNames.Contains(poiName))
            return false;

        // 从领主列表移除（如果玩家已分封给多个 POI，只移除这个）
        // 简化处理：直接移除
        kingdom.LordHeroIds.Remove(companionHeroId);

        GD.Print($"[PlayerKingdomService] 收回 {poiName} 的封地");
        return true;
    }

    /// <summary>
    /// 修改法律
    /// </summary>
    public static void ChangeLaw(
        PlayerKingdom kingdom,
        KingdomLaws newLaws,
        WorldEventEngine worldEngine)
    {
        kingdom.Laws = newLaws.Clone();

        worldEngine.AddNews(
            "law_changed",
            $"📜 {kingdom.DisplayName} 颁布了新的法律",
            Vector2.Zero);

        GD.Print($"[PlayerKingdomService] 法律变更");
    }
}
