using System;
using System.Collections.Generic;
using BladeHex.Strategic.WorldEvents;
using Godot;

namespace BladeHex.Strategic.Hero;

/// <summary>
/// 领主战败处理统一入口。
/// 负责:
/// 1) 80% 概率被俘 / 20% 概率战死(领主+IsNamedCharacter+HeroId)
/// 2) 普通实体直接 IsAlive=false
/// 3) 触发关系网传播(HeroRelationPropagator.OnBattleResolved)
/// 4) 推送世界新闻(hero_captured / hero_died)
///
/// 由 BattleResolver / SiegeProcessor / EntityCombatBridge 共用,确保 4 个战斗入口语义一致。
/// </summary>
public static class HeroDefeatResolver
{
    private static readonly Random _rng = new();

    public static void Resolve(
        OverworldEntity loser,
        OverworldEntity? winner,
        WorldEventEngine? engine,
        HeroRegistry? registry,
        PrisonerLedger? ledger,
        HeroRelationMatrix? relations,
        List<OverworldPOI>? pois)
    {
        if (loser == null) return;

        int currentDay = engine?.CurrentDay ?? 1;
        bool isNamedLord =
            loser.EntityTypeEnum == OverworldEntity.EntityType.LordArmy &&
            loser.IsNamedCharacter &&
            !string.IsNullOrEmpty(loser.HeroId) &&
            registry != null;

        // 普通实体:直接死亡 + 关系传播(如果有 hero 数据)
        if (!isNamedLord)
        {
            loser.IsAlive = false;
            if (winner != null && registry != null && relations != null)
            {
                HeroRelationPropagator.OnBattleResolved(winner, loser, registry, relations);
            }
            return;
        }

        var roll = _rng.NextDouble();
        if (roll < 0.8 && ledger != null && relations != null && pois != null && winner != null)
        {
            // 被俘
            CapturedSystem.Capture(loser, winner, currentDay, registry!, ledger, relations, pois);

            // 关系网传播(俘虏也算击败)
            HeroRelationPropagator.OnBattleResolved(winner, loser, registry!, relations);

            // 世界新闻
            if (engine != null)
            {
                var loserHero = registry!.Get(loser.HeroId);
                var winnerName = winner.EntityName ?? "未知势力";
                var loserName = loserHero?.DisplayName ?? loser.EntityName ?? "未知领主";
                engine.AddNews(
                    "hero_captured",
                    $"⛓ 【俘虏】{loserName} 在战败后被 {winnerName} 俘获,押往囚牢!",
                    loser.Position);
            }
        }
        else
        {
            // 战死
            loser.IsAlive = false;
            registry!.MarkDead(loser.HeroId, currentDay);

            if (winner != null && relations != null)
            {
                HeroRelationPropagator.OnBattleResolved(winner, loser, registry, relations);
            }

            if (engine != null)
            {
                var loserHero = registry.Get(loser.HeroId);
                var loserName = loserHero?.DisplayName ?? loser.EntityName ?? "未知领主";
                engine.AddNews(
                    "hero_died",
                    $"💀 【战死】{loserName} 在战场上殒命!",
                    loser.Position);
            }
            GD.Print($"[HeroDefeatResolver] 领主 {loser.EntityName} 战死沙场!");
        }
    }
}
