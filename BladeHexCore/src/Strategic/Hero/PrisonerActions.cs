using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Strategic.Hero;

public static class PrisonerActions
{
    /// <summary>无条件释放, 关系大幅好转</summary>
    public static void Release(
        HeroData prisoner, 
        int currentDay,
        HeroRegistry registry, 
        PrisonerLedger ledger, 
        HeroRelationMatrix relations,
        BladeHex.Strategic.WorldEvents.WorldEventEngine? engine = null)
    {
        if (prisoner == null || registry == null || ledger == null || relations == null) return;

        prisoner.State = CapturedState.Recovering;
        prisoner.CaptorHeroId = "";
        prisoner.PrisonPoiName = "";
        prisoner.CapturedDay = currentDay;
        ledger.Release(prisoner.HeroId);

        // 关系值大幅改善
        relations.Adjust("player", prisoner.HeroId, 25);
        
        // 家族成员也改善
        foreach (var other in registry.GetByFaction(prisoner.FactionId))
        {
            if (other.FamilyName == prisoner.FamilyName && other.HeroId != prisoner.HeroId)
            {
                relations.Adjust("player", other.HeroId, 10);
            }
        }
        engine?.AddNews(
            "hero_released",
            $"🕊 你无条件释放了 {prisoner.DisplayName},{prisoner.FamilyName} 家族铭记此恩。",
            Godot.Vector2.Zero);
        GD.Print($"[PrisonerActions] 玩家无条件释放了 {prisoner.DisplayName}！");
    }

    /// <summary>NPC 间自动赎回(30 天后触发)</summary>
    public static bool RansomBack(
        HeroData prisoner, 
        int currentDay, 
        HeroRegistry registry, 
        PrisonerLedger ledger, 
        HeroRelationMatrix relations)
    {
        if (prisoner == null || registry == null || ledger == null || relations == null) return false;

        if (currentDay - prisoner.CapturedDay >= 30)
        {
            var oldCaptor = prisoner.CaptorHeroId;
            prisoner.State = CapturedState.Recovering;
            prisoner.CaptorHeroId = "";
            prisoner.PrisonPoiName = "";
            prisoner.CapturedDay = currentDay;
            ledger.Release(prisoner.HeroId);

            if (!string.IsNullOrEmpty(oldCaptor))
            {
                relations.Adjust(oldCaptor, prisoner.HeroId, 5);
            }
            GD.Print($"[PrisonerActions] {prisoner.DisplayName} 已在关押 30 天后被 NPC 赎回！");
            return true;
        }
        return false;
    }

    /// <summary>玩家收赎金, 扣 captor 国金币给玩家 (前端单独增加金币)</summary>
    public static void CollectRansom(
        HeroData prisoner, 
        int currentDay,
        PrisonerLedger ledger, 
        HeroRelationMatrix relations,
        BladeHex.Strategic.WorldEvents.WorldEventEngine? engine = null)
    {
        if (prisoner == null || ledger == null || relations == null) return;

        prisoner.State = CapturedState.Recovering;
        prisoner.CaptorHeroId = "";
        prisoner.PrisonPoiName = "";
        prisoner.CapturedDay = currentDay;
        ledger.Release(prisoner.HeroId);

        relations.Adjust("player", prisoner.HeroId, 10);
        engine?.AddNews(
            "hero_released",
            $"💰 你收取了 {prisoner.DisplayName} 的赎金 {prisoner.RansomGold} 金币并将其释放。",
            Godot.Vector2.Zero);
        GD.Print($"[PrisonerActions] 玩家收取了 {prisoner.DisplayName} 的赎金 {prisoner.RansomGold} 金币并释放了他！");
    }

    /// <summary>劝降: 关系值 >= 50 + 影响力 50 -> 加入玩家阵营</summary>
    public static bool Recruit(
        HeroData prisoner, 
        int currentDay,
        HeroRegistry registry, 
        PrisonerLedger ledger, 
        HeroRelationMatrix relations, 
        InfluenceTracker influence,
        BladeHex.Strategic.WorldEvents.WorldEventEngine? engine = null)
    {
        if (prisoner == null || registry == null || ledger == null || relations == null || influence == null) return false;

        int rel = relations.Get("player", prisoner.HeroId);
        int inf = influence.Get("player");
        if (inf < 50 || rel < 50) return false;

        // 花费 50 点影响力
        influence.TrySpend("player", 50, "招降敌方领主");

        // 记录原势力用于关系链调整
        var originalFaction = prisoner.FactionId;

        // 成功招降：加入玩家阵营
        prisoner.State = CapturedState.Recovering;
        prisoner.FactionId = "player"; // 变为玩家势力
        prisoner.CaptorHeroId = "";
        prisoner.PrisonPoiName = "";
        prisoner.CapturedDay = currentDay;
        ledger.Release(prisoner.HeroId);

        relations.Set("player", prisoner.HeroId, 50); // 初始关系设为 50

        // 与原国家所有人关系 -20
        foreach (var other in registry.GetByFaction(originalFaction))
        {
            if (other.HeroId != prisoner.HeroId)
            {
                relations.Adjust(other.HeroId, prisoner.HeroId, -20);
            }
        }
        engine?.AddNews(
            "hero_recruited",
            $"⚔ {prisoner.DisplayName} 弃 {originalFaction} 而归,正式加入了你的麾下!",
            Godot.Vector2.Zero);
        GD.Print($"[PrisonerActions] 玩家成功招降了 {prisoner.DisplayName}！此领主已加入玩家阵营。");
        return true;
    }
}
