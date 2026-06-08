using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Strategic.Hero;

public static class CapturedSystem
{
    public static void Capture(
        OverworldEntity loser, 
        OverworldEntity winner, 
        int currentDay,
        HeroRegistry heroes, 
        PrisonerLedger ledger,
        HeroRelationMatrix relations, 
        List<OverworldPOI> allPois)
    {
        if (loser == null || winner == null) return;
        var loserHero = heroes.Get(loser.HeroId);
        if (loserHero == null) return;

        // 1. 设置状态
        loserHero.State = CapturedState.Captured;
        loserHero.CaptorHeroId = winner.HeroId;
        loserHero.CapturedDay = currentDay;
        loserHero.RansomGold = ComputeRansom(loserHero, loser);

        // 2. 选择关押 POI
        var prisonPoi = SelectPrison(winner, allPois);
        if (winner.HeroId == "player" || winner.Faction == "player")
        {
            loserHero.PrisonPoiName = "player";
            ledger.Imprison(loserHero.HeroId, "player");
        }
        else
        {
            loserHero.PrisonPoiName = prisonPoi?.PoiName ?? "";
            ledger.Imprison(loserHero.HeroId, prisonPoi?.PoiName ?? "");
        }

        // 3. 移除大地图实体 (loser 暂时消失)
        loser.IsAlive = false;
        loserHero.CurrentEntityName = "";

        // 4. 关系网更新
        if (winner.HeroId == "player" || winner.Faction == "player")
            relations.Adjust("player", loserHero.HeroId, -15);
        else
            relations.Adjust(winner.HeroId, loserHero.HeroId, -10);

        GD.Print($"[CapturedSystem] 领主 {loserHero.DisplayName} 被 {winner.EntityName} 俘虏！关押在 {loserHero.PrisonPoiName}");
    }

    public static OverworldPOI? SelectPrison(OverworldEntity winner, List<OverworldPOI> allPois)
    {
        if (winner == null || allPois == null) return null;

        // 优先选择领主的 GuardedPOI (如果属于同 faction 且是城堡/城镇)
        if (winner.GuardedPOI != null && winner.GuardedPOI.OwningFaction == winner.Faction &&
            (winner.GuardedPOI.PoiTypeEnum == OverworldPOI.POIType.Castle || winner.GuardedPOI.PoiTypeEnum == OverworldPOI.POIType.Town))
        {
            return winner.GuardedPOI;
        }

        // 否则寻找 winner 势力里距离 winner 最近的 Castle 或者是 Town
        OverworldPOI? bestPoi = null;
        float bestDist = float.MaxValue;
        foreach (var poi in allPois)
        {
            if (poi.OwningFaction == winner.Faction && 
                (poi.PoiTypeEnum == OverworldPOI.POIType.Castle || poi.PoiTypeEnum == OverworldPOI.POIType.Town))
            {
                float d = winner.Position.DistanceTo(poi.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoi = poi;
                }
            }
        }
        return bestPoi;
    }

    private static int ComputeRansom(HeroData hero, OverworldEntity entity)
    {
        if (entity == null) return 100;
        return System.Math.Max(100, entity.PartyLevel * 200 + (int)(entity.CombatPower * 0.5f));
    }
}
