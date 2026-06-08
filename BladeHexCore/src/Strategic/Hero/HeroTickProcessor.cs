using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Strategic.Hero;

public static class HeroTickProcessor
{
    /// <summary>默认生成配置（静态缓存，避免每次 Tick 都 new）</summary>
    private static readonly SpecialCharacterGenerator.GenerationConfig DefaultConfig = new();

    public static List<HeroData> Tick(
        HeroRegistry registry, 
        PrisonerLedger ledger, 
        HeroRelationMatrix relations, 
        int currentDay,
        FamilyRegistry? familyRegistry = null,
        List<NationConfig>? nations = null,
        List<OverworldPOI>? pois = null,
        SpecialCharacterGenerator.GenerationConfig? config = null,
        WorldEvents.WorldEventEngine? worldEngine = null)
    {
        var respawnHeroes = new List<HeroData>();

        if (registry == null || ledger == null || relations == null) return respawnHeroes;

        // 1. 推进 Recovering 状态的英雄重生 (7天)
        foreach (var hero in registry.AllHeroes)
        {
            if (hero.State == CapturedState.Recovering && currentDay - hero.CapturedDay >= 7)
            {
                respawnHeroes.Add(hero);
            }
        }

        // 2. 推进 NPC 之间自动赎回 (30天)
        var capturedHeroes = new List<HeroData>();
        foreach (var hero in registry.AllHeroes)
        {
            if (hero.State == CapturedState.Captured && 
                hero.CaptorHeroId != "player" && 
                hero.PrisonPoiName != "player")
            {
                capturedHeroes.Add(hero);
            }
        }

        foreach (var hero in capturedHeroes)
        {
            PrisonerActions.RansomBack(hero, currentDay, registry, ledger, relations);
        }

        // 3. 每 30 天执行一次贵族补员审计
        if (currentDay % 30 == 0 && familyRegistry != null && nations != null && pois != null && worldEngine != null)
        {
            var effectiveConfig = config ?? DefaultConfig;
            NobleSuccessionService.Audit(registry, familyRegistry, nations, pois, effectiveConfig, currentDay, worldEngine);
        }

        return respawnHeroes;
    }
}
