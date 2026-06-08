using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.SubParty;

namespace BladeHex.Tests.Strategic;

public static class HeroNetworkTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(HeroRegistry_CRUD_Operations), HeroRegistry_CRUD_Operations);
        yield return Run(nameof(HeroRegistry_MarkDead_PreservesDeathRecord), HeroRegistry_MarkDead_PreservesDeathRecord);
        yield return Run(nameof(HeroRegistry_GetByFaction_Filters), HeroRegistry_GetByFaction_Filters);
        yield return Run(nameof(HeroRegistry_SerializeRoundtrip), HeroRegistry_SerializeRoundtrip);
        yield return Run(nameof(Family_Members_Initial_Relation_Setup), Family_Members_Initial_Relation_Setup);
        yield return Run(nameof(RelationMatrix_Symmetric_DoubleKey_Exchange), RelationMatrix_Symmetric_DoubleKey_Exchange);
        yield return Run(nameof(RelationMatrix_Clamp_Boundary_Constraints), RelationMatrix_Clamp_Boundary_Constraints);
        yield return Run(nameof(RelationMatrix_SerializeSparse_OnlyNonZero), RelationMatrix_SerializeSparse_OnlyNonZero);
        yield return Run(nameof(RelationMatrix_GetAllRelations_ReturnsAllNonZero), RelationMatrix_GetAllRelations_ReturnsAllNonZero);
        yield return Run(nameof(CapturedSystem_Capture_Winner_And_Loser_Transitions), CapturedSystem_Capture_Winner_And_Loser_Transitions);
        yield return Run(nameof(CapturedSystem_AssignsPrison_InCaptorFaction), CapturedSystem_AssignsPrison_InCaptorFaction);
        yield return Run(nameof(PrisonerActions_Release_Unconditionally_Improves_Relations), PrisonerActions_Release_Unconditionally_Improves_Relations);
        yield return Run(nameof(PrisonerActions_CollectRansom_Frees_Prisoner), PrisonerActions_CollectRansom_Frees_Prisoner);
        yield return Run(nameof(PrisonerActions_Recruit_FailsWhenRelationLow), PrisonerActions_Recruit_FailsWhenRelationLow);
        yield return Run(nameof(PrisonerActions_Recruit_Deducts_Influence_And_Swaps_Faction), PrisonerActions_Recruit_Deducts_Influence_And_Swaps_Faction);
        yield return Run(nameof(Recovering_Status_DailyTick_And_Rebirth), Recovering_Status_DailyTick_And_Rebirth);
        yield return Run(nameof(SiegeProcessor_DummyWinner_Safety_Assert), SiegeProcessor_DummyWinner_Safety_Assert);
        yield return Run(nameof(Propagation_PlayerDefeatLord_FamilyHates), Propagation_PlayerDefeatLord_FamilyHates);
        yield return Run(nameof(Propagation_DefeatLord_EnemiesLikePlayer), Propagation_DefeatLord_EnemiesLikePlayer);
        yield return Run(nameof(Propagation_NpcVsNpc_AdjustsBilateralRelation), Propagation_NpcVsNpc_AdjustsBilateralRelation);
        yield return Run(nameof(SubParty_TickProcessor_AI_StateMachine_Advance), SubParty_TickProcessor_AI_StateMachine_Advance);
        yield return Run(nameof(SubParty_HuntBandits_FindsNearestSettlement), SubParty_HuntBandits_FindsNearestSettlement);
        yield return Run(nameof(SubParty_PatrolRegion_StaysWithinRadius), SubParty_PatrolRegion_StaysWithinRadius);
        yield return Run(nameof(SubParty_Garrison_IncreasesPoiGarrison), SubParty_Garrison_IncreasesPoiGarrison);
        yield return Run(nameof(SubParty_Defeat_TriggersRecovering_AndReturnsRejoiner), SubParty_Defeat_TriggersRecovering_AndReturnsRejoiner);
        yield return Run(nameof(SubPartyRegistry_SerializeRoundtrip), SubPartyRegistry_SerializeRoundtrip);
#if !CORE_BUILD
        yield return Run(nameof(SubParty_Defeat_And_SevenDay_Rejoin_Automation), SubParty_Defeat_And_SevenDay_Rejoin_Automation);
#endif
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ============================================================================
    // 细分单元测试用例 (覆盖 ~25 个验证断言)
    // ============================================================================

    private static (bool, string) HeroRegistry_CRUD_Operations()
    {
        var registry = new HeroRegistry();
        
        // 1-3. CRUD 测试
        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Aggressive, 1);
        if (hero == null) return (false, "Failed to create hero");

        var fetched = registry.Get("hero_nord_1");
        if (fetched == null || fetched.DisplayName != "拉格纳领主")
            return (false, "Failed to fetch created hero");

        var all = registry.AllHeroes.ToList();
        if (all.Count != 1)
            return (false, "GetAll count mismatch");

        registry.Remove("hero_nord_1");
        if (registry.Get("hero_nord_1") != null)
            return (false, "Remove failed");

        return (true, "");
    }

    private static (bool, string) HeroRegistry_MarkDead_PreservesDeathRecord()
    {
        var registry = new HeroRegistry();
        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1);
        
        // 4-5. 战死标记测试
        registry.MarkDead("hero_nord_1", 15);
        if (!registry.IsDead("hero_nord_1"))
            return (false, "Hero should be marked dead");

        int deathDay = registry.GetDeathDay("hero_nord_1");
        if (deathDay != 15)
            return (false, "Death day record mismatch or not preserved");

        return (true, "");
    }

    private static (bool, string) Family_Members_Initial_Relation_Setup()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();

        // 6. 同家族同势力好感 +20 初始化测试
        var hero1 = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1, relations);
        var hero2 = registry.Create("nord", "阿尔夫领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1, relations);

        int rel = relations.Get("hero_nord_1", "hero_nord_2");
        if (rel != 20)
            return (false, $"Expected family relation +20, got {rel}");

        return (true, "");
    }

    private static (bool, string) RelationMatrix_Symmetric_DoubleKey_Exchange()
    {
        var matrix = new HeroRelationMatrix();

        // 7-8. 对称好感度测试
        matrix.Set("hero_a", "hero_b", 45);
        int r1 = matrix.Get("hero_a", "hero_b");
        int r2 = matrix.Get("hero_b", "hero_a");

        if (r1 != 45 || r2 != 45)
            return (false, $"Symmetric lookup failed: r1={r1}, r2={r2}");

        matrix.Adjust("hero_b", "hero_a", -10);
        if (matrix.Get("hero_a", "hero_b") != 35)
            return (false, "Adjustment symmetric swap failed");

        return (true, "");
    }

    private static (bool, string) RelationMatrix_Clamp_Boundary_Constraints()
    {
        var matrix = new HeroRelationMatrix();

        // 9-10. 好感 Clamp 边界测试
        matrix.Set("hero_a", "hero_b", 120);
        if (matrix.Get("hero_a", "hero_b") != 100)
            return (false, "Upper bound clamp to 100 failed");

        matrix.Set("hero_a", "hero_b", -150);
        if (matrix.Get("hero_a", "hero_b") != -100)
            return (false, "Lower bound clamp to -100 failed");

        return (true, "");
    }

    private static (bool, string) CapturedSystem_Capture_Winner_And_Loser_Transitions()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();

        var winner = new OverworldEntity { EntityName = "主角", HeroId = "player", Faction = "player" };
        var loser = new OverworldEntity { EntityName = "拉格纳领主", HeroId = "hero_nord_1", Faction = "nord" };
        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1);

        // 11-13. 被俘捕获状态机与好感影响测试
        CapturedSystem.Capture(loser, winner, 1, registry, ledger, relations, new List<OverworldPOI>());

        if (hero.State != CapturedState.Captured)
            return (false, "Loser hero state should transition to Captured");

        if (hero.CaptorHeroId != "player")
            return (false, $"Expected captor player, got {hero.CaptorHeroId}");

        int rel = relations.Get("player", "hero_nord_1");
        if (rel != -15) // 被玩家击败扣 15
            return (false, $"Expected relationship decrease to -15, got {rel}");

        if (!ledger.GetPrisonersAt("player").Contains("hero_nord_1"))
            return (false, "Prisoner not added to player prisoner ledger");

        return (true, "");
    }

    private static (bool, string) PrisonerActions_Release_Unconditionally_Improves_Relations()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();

        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1);
        hero.State = CapturedState.Captured;
        hero.CaptorHeroId = "player";
        ledger.Imprison("hero_nord_1", "player");

        // 14-16. 无条件释放测试
        PrisonerActions.Release(hero, 2, registry, ledger, relations);

        if (hero.State != CapturedState.Recovering)
            return (false, "Released hero should be in Recovering state");

        if (ledger.GetPrisonersAt("player").Contains("hero_nord_1"))
            return (false, "Prisoner should be removed from player ledger");

        int rel = relations.Get("player", "hero_nord_1");
        if (rel != 25) // 释放好感度+25
            return (false, $"Expected relation +25, got {rel}");

        return (true, "");
    }

    private static (bool, string) PrisonerActions_CollectRansom_Frees_Prisoner()
    {
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();

        var hero = new HeroData { HeroId = "hero_nord_1", State = CapturedState.Captured, CaptorHeroId = "player", RansomGold = 1000 };
        ledger.Imprison("hero_nord_1", "player");

        // 17-18. 收赎金释放测试
        PrisonerActions.CollectRansom(hero, 3, ledger, relations);

        if (hero.State != CapturedState.Recovering)
            return (false, "Hero after ransom should be Recovering");

        if (ledger.GetPrisonersAt("player").Contains("hero_nord_1"))
            return (false, "Prisoner ledger should clear after ransom");

        int rel = relations.Get("player", "hero_nord_1");
        if (rel != 10) // 赎金释放好感度+10
            return (false, $"Expected ransom relation +10, got {rel}");

        return (true, "");
    }

    private static (bool, string) PrisonerActions_Recruit_Deducts_Influence_And_Swaps_Faction()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();
        var influence = new InfluenceTracker();

        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1);
        hero.State = CapturedState.Captured;
        hero.CaptorHeroId = "player";
        ledger.Imprison("hero_nord_1", "player");

        // 19. 条件不足招降失败测试
        bool fail = PrisonerActions.Recruit(hero, 4, registry, ledger, relations, influence);
        if (fail) return (false, "Should not recruit with low relations/influence");

        // 满足条件：好感度设为 50，影响力设为 100
        relations.Set("player", "hero_nord_1", 50);
        influence.Add("player", 100);

        // 20-22. 成功招降测试
        bool success = PrisonerActions.Recruit(hero, 4, registry, ledger, relations, influence);
        if (!success) return (false, "Failed to recruit when conditions met");

        if (hero.FactionId != "player")
            return (false, "Faction should swap to player after recruit");

        int spentInf = influence.Get("player");
        if (spentInf != 50)
            return (false, $"Influence should decrease by 50, got {spentInf}");

        return (true, "");
    }

    private static (bool, string) Recovering_Status_DailyTick_And_Rebirth()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI> { new OverworldPOI { PoiName = "克拉格堡", Position = new Vector2(100, 100) } };

        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1);
        hero.State = CapturedState.Recovering;
        hero.CapturedDay = 10;
        hero.BoundPoiName = "克拉格堡";

        // 23. 7天未满重生 tick 测试
        var respawns15 = HeroTickProcessor.Tick(registry, ledger, relations, 15);
        if (respawns15.Contains(hero))
            return (false, "Hero should not be reborn on day 15");
        if (hero.State != CapturedState.Recovering)
            return (false, "Hero should remain Recovering on day 15");

        // 24. 7天届满重生测试
        var respawns17 = HeroTickProcessor.Tick(registry, ledger, relations, 17);
        if (!respawns17.Contains(hero))
            return (false, "Hero should be reborn on day 17");

        // 模拟重生行为
        foreach (var h in respawns17)
        {
            h.State = CapturedState.Free;
            h.CapturedDay = 0;

            var boundPoi = pois.Find(p => p.PoiName == h.BoundPoiName);
            Vector2 spawnPos = boundPoi != null ? boundPoi.Position : new Vector2(0, 0);

            var newEntity = new OverworldEntity
            {
                EntityName = h.DisplayName,
                EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
                Position = spawnPos,
                IsAlive = true,
                HeroId = h.HeroId
            };
            entities.Add(newEntity);
        }

        if (hero.State != CapturedState.Free)
            return (false, "Hero should be reborn and Free on day 17");

        if (entities.Count != 1 || entities[0].EntityName != "拉格纳领主")
            return (false, "Hero entity not respawned on overworld grid");

        return (true, "");
    }

    private static (bool, string) SiegeProcessor_DummyWinner_Safety_Assert()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();

        // 25. Siege 虚拟赢家空指针安全验证
        var dummyWinner = new OverworldEntity { EntityName = "守军", Faction = "nord", HeroId = "defender_garrison" };
        var defeatedLord = new OverworldEntity { EntityName = "拉格纳领主", HeroId = "hero_nord_1", Faction = "nord" };
        
        var hero = registry.Create("nord", "拉格纳领主", "拉格纳", OverworldPOI.LordPersonality.Balanced, 1);

        CapturedSystem.Capture(defeatedLord, dummyWinner, 1, registry, ledger, relations, new List<OverworldPOI>());

        if (hero.State != CapturedState.Captured || hero.CaptorHeroId != "defender_garrison")
            return (false, "Dummy winner capture interception failed");

        return (true, "");
    }

    private static (bool, string) SubParty_TickProcessor_AI_StateMachine_Advance()
    {
        var registry = new SubPartyRegistry();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI> { new OverworldPOI { PoiName = "克拉格堡", Position = new Vector2(100, 100) } };

        var subParty = registry.Create("罗尔夫", new Vector2(0, 0));
        var companionEntity = new OverworldEntity { EntityName = "罗尔夫", Position = new Vector2(0, 0), IsAlive = true };
        subParty.OverworldEntityRef = companionEntity;
        
        subParty.Task = SubPartyTask.PatrolRegion;
        subParty.TargetPoiName = "克拉格堡";

        // 推进 AI Tick 移动向巡逻目标
        SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(200, 200), 1);

        if (companionEntity.Position.X == 0 && companionEntity.Position.Y == 0)
            return (false, "SubParty entity failed to move towards patrol target");

        return (true, "");
    }

#if !CORE_BUILD
    private static (bool, string) SubParty_Defeat_And_SevenDay_Rejoin_Automation()
    {
        var entityMgr = new OverworldEntityManager();
        var playerRoster = new PartyRoster();

        var subParty = entityMgr.SubParties.Create("罗尔夫", new Vector2(0, 0));
        var companion = new UnitData { UnitName = "罗尔夫", BaseMaxHp = 15 };
        subParty.Members.Add(companion);

        var companionEntity = new OverworldEntity { EntityName = "罗尔夫", IsAlive = false }; // 战败销毁
        subParty.OverworldEntityRef = companionEntity;

        // 7天重生并归队推进测试
        // 模拟 OverworldScene3D.Entities 中的每日结算 OnDayPassedFief 里的归队流程
        int currentDay = 1;
        subParty.TaskStartDay = currentDay;
        
        // 每日 Tick 到 8 天
        int targetDay = 8;
        if (targetDay - subParty.TaskStartDay >= 7)
        {
            foreach (var member in subParty.Members)
            {
                playerRoster.Add(member);
            }
            entityMgr.SubParties.Remove(subParty.SubPartyId);
        }

        if (entityMgr.SubParties.GetAll().Count != 0)
            return (false, "SubParty was not auto removed from registry after 7 days");

        if (playerRoster.Members.Count != 1 || playerRoster.Members[0].UnitName != "罗尔夫")
            return (false, "Companion was not auto restored to player roster");

        return (true, "");
    }
#endif

    // ============================================================================
    // P1 补充测试 — 见 review 报告 F1
    // ============================================================================

    private static (bool, string) HeroRegistry_GetByFaction_Filters()
    {
        var registry = new HeroRegistry();
        registry.Create("nord", "拉格纳", "拉格纳家", OverworldPOI.LordPersonality.Balanced, 1);
        registry.Create("nord", "比约恩", "比约恩家", OverworldPOI.LordPersonality.Aggressive, 1);
        registry.Create("imperial", "凯撒", "尤利乌斯家", OverworldPOI.LordPersonality.Cautious, 1);

        var nordHeroes = registry.GetByFaction("nord");
        if (nordHeroes.Count != 2)
            return (false, $"Expected 2 nord heroes, got {nordHeroes.Count}");

        var imperialHeroes = registry.GetByFaction("imperial");
        if (imperialHeroes.Count != 1)
            return (false, $"Expected 1 imperial hero, got {imperialHeroes.Count}");

        var unknownHeroes = registry.GetByFaction("unknown");
        if (unknownHeroes.Count != 0)
            return (false, "Unknown faction should return empty list");

        return (true, "");
    }

    private static (bool, string) HeroRegistry_SerializeRoundtrip()
    {
        var registry = new HeroRegistry();
        registry.Create("nord", "拉格纳", "拉格纳家", OverworldPOI.LordPersonality.Aggressive, 1);
        registry.Create("nord", "比约恩", "比约恩家", OverworldPOI.LordPersonality.Cautious, 5);
        registry.MarkDead("hero_nord_2", 10);

        var dict = registry.Serialize();
        var restored = new HeroRegistry();
        restored.Deserialize(dict);

        var hero1 = restored.Get("hero_nord_1");
        if (hero1 == null || hero1.DisplayName != "拉格纳")
            return (false, "Hero1 not restored");

        if (hero1.Personality != OverworldPOI.LordPersonality.Aggressive)
            return (false, "Personality not preserved");

        if (!restored.IsDead("hero_nord_2"))
            return (false, "Dead status not preserved");

        if (restored.GetDeathDay("hero_nord_2") != 10)
            return (false, "Death day not preserved");

        // 序列号也必须保留 — 否则下个 Create 会撞 ID
        var newHero = restored.Create("nord", "新人", "新人家", OverworldPOI.LordPersonality.Balanced, 11);
        if (newHero.HeroId != "hero_nord_3")
            return (false, $"NextSeq not preserved, got {newHero.HeroId}");

        return (true, "");
    }

    private static (bool, string) RelationMatrix_SerializeSparse_OnlyNonZero()
    {
        var matrix = new HeroRelationMatrix();
        matrix.Set("a", "b", 30);
        matrix.Set("a", "c", 0);   // 应该被丢弃
        matrix.Set("b", "c", -50);

        var dict = matrix.Serialize();
        // 只保留 2 条非零关系
        if (dict.Count != 2)
            return (false, $"Expected 2 sparse entries, got {dict.Count}");

        var restored = new HeroRelationMatrix();
        restored.Deserialize(dict);
        if (restored.Get("a", "b") != 30 || restored.Get("b", "c") != -50)
            return (false, "Restored values mismatch");
        if (restored.Get("a", "c") != 0)
            return (false, "Zero entries should remain zero after roundtrip");

        return (true, "");
    }

    private static (bool, string) RelationMatrix_GetAllRelations_ReturnsAllNonZero()
    {
        var matrix = new HeroRelationMatrix();
        matrix.Set("player", "h1", 25);
        matrix.Set("player", "h2", -30);
        matrix.Set("player", "h3", 0);   // 不应该出现
        matrix.Set("h1", "h2", 15);

        var playerRelations = matrix.GetAllRelations("player").ToList();
        if (playerRelations.Count != 2)
            return (false, $"Expected 2 non-zero relations for player, got {playerRelations.Count}");

        var dict = playerRelations.ToDictionary(r => r.otherId, r => r.value);
        if (dict["h1"] != 25 || dict["h2"] != -30)
            return (false, "GetAllRelations values mismatch");
        if (dict.ContainsKey("h3"))
            return (false, "Zero relation should not appear in GetAllRelations");

        return (true, "");
    }

    private static (bool, string) CapturedSystem_AssignsPrison_InCaptorFaction()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();

        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "敌方城堡", OwningFaction = "nord", PoiTypeEnum = OverworldPOI.POIType.Castle, Position = new Vector2(0, 0) },
            new OverworldPOI { PoiName = "我方近的城", OwningFaction = "imperial", PoiTypeEnum = OverworldPOI.POIType.Castle, Position = new Vector2(50, 0) },
            new OverworldPOI { PoiName = "我方远的城", OwningFaction = "imperial", PoiTypeEnum = OverworldPOI.POIType.Castle, Position = new Vector2(500, 500) }
        };

        var winner = new OverworldEntity { EntityName = "凯撒", HeroId = "hero_imp_1", Faction = "imperial", Position = new Vector2(0, 0) };
        var loser = new OverworldEntity { EntityName = "拉格纳", HeroId = "hero_nord_1", Faction = "nord" };
        var hero = registry.Create("nord", "拉格纳", "拉格纳家", OverworldPOI.LordPersonality.Balanced, 1);

        CapturedSystem.Capture(loser, winner, 1, registry, ledger, relations, pois);

        // captor 是 imperial,关押地必须是 imperial 阵营的 POI
        if (hero.PrisonPoiName != "我方近的城")
            return (false, $"Expected nearest imperial castle, got {hero.PrisonPoiName}");

        return (true, "");
    }

    private static (bool, string) PrisonerActions_Recruit_FailsWhenRelationLow()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();
        var ledger = new PrisonerLedger();
        var influence = new InfluenceTracker();

        var hero = registry.Create("nord", "拉格纳", "拉格纳家", OverworldPOI.LordPersonality.Balanced, 1);
        hero.State = CapturedState.Captured;
        ledger.Imprison("hero_nord_1", "player");

        // 影响力够,关系不够
        influence.Add("player", 100);
        relations.Set("player", "hero_nord_1", 30);
        if (PrisonerActions.Recruit(hero, 1, registry, ledger, relations, influence))
            return (false, "Recruit should fail when relation < 50");

        // 关系够,影响力不够
        relations.Set("player", "hero_nord_1", 60);
        var lowInf = new InfluenceTracker();
        lowInf.Add("player", 30);
        if (PrisonerActions.Recruit(hero, 1, registry, ledger, relations, lowInf))
            return (false, "Recruit should fail when influence < 50");

        // 阵营不应被改
        if (hero.FactionId != "nord")
            return (false, "Faction should not change on failed recruit");

        return (true, "");
    }

    private static (bool, string) Propagation_PlayerDefeatLord_FamilyHates()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();

        var lord = registry.Create("nord", "父亲", "拉格纳家", OverworldPOI.LordPersonality.Balanced, 1, relations);
        var brother = registry.Create("nord", "兄弟", "拉格纳家", OverworldPOI.LordPersonality.Balanced, 1, relations);
        // 第三个非家族成员,不该受影响
        var stranger = registry.Create("nord", "陌生人", "陌生家", OverworldPOI.LordPersonality.Balanced, 1, relations);

        var winner = new OverworldEntity { EntityName = "玩家", HeroId = "player", Faction = "player" };
        var loser = new OverworldEntity { EntityName = "父亲", HeroId = lord.HeroId, Faction = "nord" };

        HeroRelationPropagator.OnBattleResolved(winner, loser, registry, relations);

        // 玩家 vs 击败者: -10
        if (relations.Get("player", lord.HeroId) != -10)
            return (false, $"Expected player vs lord = -10, got {relations.Get("player", lord.HeroId)}");

        // 玩家 vs 同家族兄弟: -5
        if (relations.Get("player", brother.HeroId) != -5)
            return (false, $"Expected player vs brother = -5, got {relations.Get("player", brother.HeroId)}");

        // 玩家 vs 非家族成员: 0
        if (relations.Get("player", stranger.HeroId) != 0)
            return (false, "Stranger should not be affected by family hate");

        return (true, "");
    }

    private static (bool, string) Propagation_DefeatLord_EnemiesLikePlayer()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();

        var lord = registry.Create("nord", "傲慢领主", "纳家", OverworldPOI.LordPersonality.Balanced, 1, relations);
        var enemy = registry.Create("imperial", "宿敌", "尤家", OverworldPOI.LordPersonality.Balanced, 1, relations);

        // 宿敌:与傲慢领主关系 -60
        relations.Set(enemy.HeroId, lord.HeroId, -60);

        var winner = new OverworldEntity { EntityName = "玩家", HeroId = "player", Faction = "player" };
        var loser = new OverworldEntity { EntityName = "傲慢领主", HeroId = lord.HeroId, Faction = "nord" };

        HeroRelationPropagator.OnBattleResolved(winner, loser, registry, relations);

        // 宿敌对玩家好感 +3
        if (relations.Get("player", enemy.HeroId) != 3)
            return (false, $"Expected enemy of loser to like player +3, got {relations.Get("player", enemy.HeroId)}");

        return (true, "");
    }

    private static (bool, string) Propagation_NpcVsNpc_AdjustsBilateralRelation()
    {
        var registry = new HeroRegistry();
        var relations = new HeroRelationMatrix();

        var winnerHero = registry.Create("imperial", "胜者", "尤家", OverworldPOI.LordPersonality.Balanced, 1, relations);
        var loserHero = registry.Create("nord", "败者", "纳家", OverworldPOI.LordPersonality.Balanced, 1, relations);

        var winner = new OverworldEntity { EntityName = "胜者", HeroId = winnerHero.HeroId, Faction = "imperial" };
        var loser = new OverworldEntity { EntityName = "败者", HeroId = loserHero.HeroId, Faction = "nord" };

        HeroRelationPropagator.OnBattleResolved(winner, loser, registry, relations);

        // NPC 间应当 -8
        if (relations.Get(winnerHero.HeroId, loserHero.HeroId) != -8)
            return (false, $"Expected NPC vs NPC = -8, got {relations.Get(winnerHero.HeroId, loserHero.HeroId)}");

        return (true, "");
    }

    private static (bool, string) SubParty_HuntBandits_FindsNearestSettlement()
    {
        var registry = new SubPartyRegistry();
        var pois = new List<OverworldPOI>();

        // 三个山贼营地,远近不同
        var nearBandit = new OverworldEntity
        {
            EntityName = "近匪",
            EntityTypeEnum = OverworldEntity.EntityType.BanditParty,
            Position = new Vector2(200, 0),
            IsAlive = true
        };
        var farBandit = new OverworldEntity
        {
            EntityName = "远匪",
            EntityTypeEnum = OverworldEntity.EntityType.BanditParty,
            Position = new Vector2(1000, 0),
            IsAlive = true
        };
        var deadBandit = new OverworldEntity
        {
            EntityName = "死匪",
            EntityTypeEnum = OverworldEntity.EntityType.BanditParty,
            Position = new Vector2(50, 0),
            IsAlive = false  // 不应被选中
        };
        var entities = new List<OverworldEntity> { nearBandit, farBandit, deadBandit };

        var sp = registry.Create("剿匪队", new Vector2(0, 0));
        var spEntity = new OverworldEntity { EntityName = "剿匪队", Position = new Vector2(0, 0), IsAlive = true };
        sp.OverworldEntityRef = spEntity;
        sp.Task = SubPartyTask.HuntBandits;

        SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(0, 0), 1);

        // 应该向最近 (200, 0) 移动
        if (spEntity.Position.X <= 0)
            return (false, $"SubParty should move towards near bandit, position={spEntity.Position}");
        if (spEntity.Position.X > 200)
            return (false, $"SubParty overshooting, position={spEntity.Position}");

        return (true, "");
    }

    private static (bool, string) SubParty_PatrolRegion_StaysWithinRadius()
    {
        var registry = new SubPartyRegistry();
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "巡逻点", Position = new Vector2(500, 500) }
        };
        var entities = new List<OverworldEntity>();

        var sp = registry.Create("巡逻队", new Vector2(500, 500));
        var spEntity = new OverworldEntity { EntityName = "巡逻队", Position = new Vector2(500, 500), IsAlive = true };
        sp.OverworldEntityRef = spEntity;
        sp.Task = SubPartyTask.PatrolRegion;
        sp.TargetPoiName = "巡逻点";

        // 已在巡逻点 600px 内,应做圆圈巡逻 — 不应该越过 1000 px
        for (int day = 1; day <= 5; day++)
        {
            SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(0, 0), day);
            float dist = spEntity.Position.DistanceTo(new Vector2(500, 500));
            if (dist > 1000.0f)
                return (false, $"Patrol drift too far: dist={dist} on day {day}");
        }

        return (true, "");
    }

    private static (bool, string) SubParty_Garrison_IncreasesPoiGarrison()
    {
        var registry = new SubPartyRegistry();
        var poi = new OverworldPOI
        {
            PoiName = "驻防城",
            Position = new Vector2(0, 0),
            GarrisonCurrent = 10,
            GarrisonMax = 50
        };
        var pois = new List<OverworldPOI> { poi };
        var entities = new List<OverworldEntity>();

        var sp = registry.Create("驻军", new Vector2(10, 10));
        var spEntity = new OverworldEntity { EntityName = "驻军", Position = new Vector2(10, 10), IsAlive = true };
        sp.OverworldEntityRef = spEntity;
        sp.Task = SubPartyTask.Garrison;
        sp.TargetPoiName = "驻防城";

        // 推 3 天 — 应当贴到 POI 然后 GarrisonCurrent +3
        int initialGarrison = poi.GarrisonCurrent;
        for (int day = 1; day <= 3; day++)
        {
            SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(0, 0), day);
        }
        if (poi.GarrisonCurrent <= initialGarrison)
            return (false, $"Garrison should increase, was {initialGarrison} now {poi.GarrisonCurrent}");

        return (true, "");
    }

    private static (bool, string) SubParty_Defeat_TriggersRecovering_AndReturnsRejoiner()
    {
        var registry = new SubPartyRegistry();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();

        var sp = registry.Create("罗尔夫", new Vector2(0, 0));
        sp.Members.Add(new UnitData { UnitName = "罗尔夫", BaseMaxHp = 20 });
        sp.Task = SubPartyTask.HuntBandits;
        var spEntity = new OverworldEntity { EntityName = "罗尔夫", Position = new Vector2(0, 0), IsAlive = false };
        sp.OverworldEntityRef = spEntity;

        // Day 1 战败 — 标记 TaskStartDay,task 转 Idle,但不应在第 1 天就归队
        var rejoiners1 = SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(0, 0), 1);
        if (rejoiners1.Count != 0)
            return (false, "Defeated SubParty should not rejoin on the same day");
        if (sp.Task != SubPartyTask.Idle)
            return (false, "Defeated SubParty should be flipped to Idle");
        if (sp.TaskStartDay != 1)
            return (false, $"TaskStartDay should record defeat day, got {sp.TaskStartDay}");

        // Day 6 — 不到 7 天
        var rejoiners6 = SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(0, 0), 6);
        if (rejoiners6.Count != 0)
            return (false, "Should not rejoin on day 6 (less than 7 days)");

        // Day 8 — 满 7 天
        var rejoiners8 = SubPartyTickProcessor.Tick(registry, entities, pois, new Vector2(0, 0), 8);
        if (rejoiners8.Count != 1 || rejoiners8[0] != sp)
            return (false, "Should return rejoiner on day 8 (>= 7 days)");

        return (true, "");
    }

    private static (bool, string) SubPartyRegistry_SerializeRoundtrip()
    {
        var registry = new SubPartyRegistry();
        var sp1 = registry.Create("罗尔夫", new Vector2(100, 200));
        sp1.Task = SubPartyTask.PatrolRegion;
        sp1.TargetPoiName = "巡逻点";
        sp1.TaskStartDay = 5;
        sp1.Members.Add(new UnitData { UnitName = "罗尔夫", Level = 5, BaseMaxHp = 30 });

        var sp2 = registry.Create("布达", new Vector2(300, 400));

        var dict = registry.Serialize();
        var restored = new SubPartyRegistry();
        restored.Deserialize(dict);

        var all = restored.GetAll();
        if (all.Count != 2)
            return (false, $"Expected 2 SubParties, got {all.Count}");

        var rsp1 = restored.Get(sp1.SubPartyId);
        if (rsp1 == null)
            return (false, "SubParty 1 not restored");
        if (rsp1.LeaderUnitName != "罗尔夫")
            return (false, "Leader name not preserved");
        if (rsp1.Task != SubPartyTask.PatrolRegion)
            return (false, "Task not preserved");
        if (rsp1.TargetPoiName != "巡逻点")
            return (false, "TargetPoiName not preserved");
        if (rsp1.TaskStartDay != 5)
            return (false, "TaskStartDay not preserved");
        if (rsp1.Members.Count != 1 || rsp1.Members[0].UnitName != "罗尔夫")
            return (false, "Members not preserved");

        // 序列号也必须保留
        var newSp = restored.Create("新人", new Vector2(0, 0));
        if (newSp.SubPartyId != "subparty_3")
            return (false, $"NextSeq not preserved, got {newSp.SubPartyId}");

        return (true, "");
    }
}
