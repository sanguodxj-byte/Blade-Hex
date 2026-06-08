using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Strategic.Hero;

public static class HeroRelationPropagator
{
    public static void OnBattleResolved(
        OverworldEntity winner, 
        OverworldEntity loser, 
        HeroRegistry registry, 
        HeroRelationMatrix relations)
    {
        if (winner == null || loser == null || registry == null || relations == null) return;

        var winnerId = winner.HeroId;
        var loserId = loser.HeroId;

        // 如果胜者是玩家或玩家势力
        if (winnerId == "player" || winner.Faction == "player")
        {
            if (!string.IsNullOrEmpty(loserId))
            {
                // 1. 玩家与败者领主关系 -10
                relations.Adjust("player", loserId, -10);

                var loserHero = registry.Get(loserId);
                if (loserHero != null)
                {
                    // 2. 败者领主同家族成员与玩家关系 -5
                    foreach (var other in registry.GetByFaction(loserHero.FactionId))
                    {
                        if (other.FamilyName == loserHero.FamilyName && other.HeroId != loserId)
                        {
                            relations.Adjust("player", other.HeroId, -5);
                        }
                    }

                    // 3. 败者领主世仇（关系 <= -50）对玩家关系 +3
                    foreach (var other in registry.AllHeroes)
                    {
                        if (other.HeroId != loserId && relations.Get(other.HeroId, loserId) <= -50)
                        {
                            relations.Adjust("player", other.HeroId, 3);
                        }
                    }
                }
            }
        }
        else
        {
            // NPC A 击败 NPC B
            if (!string.IsNullOrEmpty(winnerId) && !string.IsNullOrEmpty(loserId))
            {
                relations.Adjust(winnerId, loserId, -8);
            }
        }
    }
}
