// OverworldFrontendLayerTests.cs
// Frontend 层大地图 View/Input 模块测试
//
// 覆盖:
//   - InteractionCooldown 触发、冷却检测、过期、清除
//   - InteractionCooldown PostCombat 更长冷却时间
//   - CommandRouter 空地点击返回 MoveTo
//   - CommandRouter POI 点击返回 InspectPoi
//   - CommandRouter 英雄实体点击返回 InspectEntity
//   - MapEntityView / BattlefieldView / SiegeView / PoiView 构造完整性
//   - ViewProjectionSnapshot 空状态
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.View.Strategic;

namespace BladeHex.View.Strategic.Tests;

public static class OverworldFrontendLayerTests
{
    private static readonly List<Node> TestNodes = new();

    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, run) in EnumerateTests())
        {
            try
            {
                var (ok, msg) = run();
                if (ok) { passed++; details.Add($"  [PASS] {name}"); }
                else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"  [FAIL] {name}: Exception {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                FreeTestNodes();
            }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, Func<(bool, string)> run)> EnumerateTests()
    {
        yield return (nameof(InteractionCooldown_Trigger_EntityOverlap_CoolsDown), InteractionCooldown_Trigger_EntityOverlap_CoolsDown);
        yield return (nameof(InteractionCooldown_PostCombat_LongerDuration), InteractionCooldown_PostCombat_LongerDuration);
        yield return (nameof(InteractionCooldown_PoiInteraction_ShortCooldown), InteractionCooldown_PoiInteraction_ShortCooldown);
        yield return (nameof(InteractionCooldown_ClearAll_ResetsAll), InteractionCooldown_ClearAll_ResetsAll);
        yield return (nameof(InteractionCooldown_Clear_SingleSource), InteractionCooldown_Clear_SingleSource);
        yield return (nameof(InteractionCooldown_GetRemainingSeconds), InteractionCooldown_GetRemainingSeconds);
        yield return (nameof(CommandRouter_EmptyWorld_ReturnsMoveTo), CommandRouter_EmptyWorld_ReturnsMoveTo);
        yield return (nameof(CommandRouter_ClickOnPoi_ReturnsMoveToPoi), CommandRouter_ClickOnPoi_ReturnsMoveToPoi);
        yield return (nameof(CommandRouter_ClickOnHeroEntity_ReturnsInspectEntity), CommandRouter_ClickOnHeroEntity_ReturnsInspectEntity);
        yield return (nameof(EntityManager_CheckPlayerEncounters_IgnoresEngagedBattleParticipants), EntityManager_CheckPlayerEncounters_IgnoresEngagedBattleParticipants);
        yield return (nameof(BattlefieldLayer_HitTest_UsesMarkerCircle), BattlefieldLayer_HitTest_UsesMarkerCircle);
        yield return (nameof(CommandRouter_BattlefieldClickOutsideMarker_FallsThrough), CommandRouter_BattlefieldClickOutsideMarker_FallsThrough);
        yield return (nameof(SiegeLayer_HitTest_UsesMarkerCircle), SiegeLayer_HitTest_UsesMarkerCircle);
        yield return (nameof(CommandRouter_SiegeClickOutsideMarker_FallsThroughToPoi), CommandRouter_SiegeClickOutsideMarker_FallsThroughToPoi);
        yield return (nameof(CommandRouter_WithLayers_BattlefieldBeatsHero), CommandRouter_WithLayers_BattlefieldBeatsHero);
        yield return (nameof(CommandRouter_WithLayers_SiegeBeatsMove), CommandRouter_WithLayers_SiegeBeatsMove);
        yield return (nameof(MapEntityView_Construction_AllFieldsAccessible), MapEntityView_Construction_AllFieldsAccessible);
        yield return (nameof(BattlefieldView_Construction_AllFieldsAccessible), BattlefieldView_Construction_AllFieldsAccessible);
        yield return (nameof(ViewProjectionSnapshot_EmptyState_AllListsEmpty), ViewProjectionSnapshot_EmptyState_AllListsEmpty);
        yield return (nameof(Projection_Registry_NvN_OneViewAllParticipants), Projection_Registry_NvN_OneViewAllParticipants);
        yield return (nameof(Projection_Registry_Fallback_EngagedWith), Projection_Registry_Fallback_EngagedWith);
        yield return (nameof(BattleResolver_FieldBattle_ProjectsToClickableJoinCommand), BattleResolver_FieldBattle_ProjectsToClickableJoinCommand);
        yield return (nameof(BattleResolver_ManyAttackersOneDefender_OneClickableBattlefield), BattleResolver_ManyAttackersOneDefender_OneClickableBattlefield);
        yield return (nameof(BattleResolver_DuplicateNamedAttackers_KeepDistinctParticipants), BattleResolver_DuplicateNamedAttackers_KeepDistinctParticipants);
        yield return (nameof(PlayerJoinFieldBattle_PullsNearbyNonNeutralParticipants), PlayerJoinFieldBattle_PullsNearbyNonNeutralParticipants);
        yield return (nameof(PlayerInitiatedEntityBattle_PullsNearbyNonNeutralParticipants), PlayerInitiatedEntityBattle_PullsNearbyNonNeutralParticipants);
        yield return (nameof(BattleContextFactory_DeduplicatesRepeatedEntityParticipants), BattleContextFactory_DeduplicatesRepeatedEntityParticipants);
        yield return (nameof(AiBattlefieldResponse_JoinsOrFleesBySidePower), AiBattlefieldResponse_JoinsOrFleesBySidePower);
    }

    private static OverworldBattlefieldLayer2D CreateBattlefieldLayer()
    {
        var layer = new OverworldBattlefieldLayer2D();
        TestNodes.Add(layer);
        return layer;
    }

    private static OverworldSiegeLayer2D CreateSiegeLayer()
    {
        var layer = new OverworldSiegeLayer2D();
        TestNodes.Add(layer);
        return layer;
    }

    private static void FreeTestNodes()
    {
        for (int i = TestNodes.Count - 1; i >= 0; i--)
        {
            var node = TestNodes[i];
            if (GodotObject.IsInstanceValid(node))
                node.Free();
        }

        TestNodes.Clear();
    }

    // ========================================
    // InteractionCooldown 测试
    // ========================================

    private static (bool, string) InteractionCooldown_Trigger_EntityOverlap_CoolsDown()
    {
        var cd = new InteractionCooldown();
        double time = 10.0;

        if (cd.IsCoolingDown(CooldownSource.EntityOverlap, time))
            return (false, "should not be cooling down before trigger");

        cd.Trigger(CooldownSource.EntityOverlap, time);

        if (!cd.IsCoolingDown(CooldownSource.EntityOverlap, time))
            return (false, "should be cooling down at trigger time");

        if (!cd.IsCoolingDown(CooldownSource.EntityOverlap, 10.3))
            return (false, "should still be cooling down at 10.3");

        if (cd.IsCoolingDown(CooldownSource.EntityOverlap, 10.5))
            return (false, "should have expired at 10.5");

        return (true, "");
    }

    private static (bool, string) InteractionCooldown_PostCombat_LongerDuration()
    {
        var cd = new InteractionCooldown();
        double time = 20.0;

        cd.Trigger(CooldownSource.PostCombat, time);

        // 默认 PostCombat = 1.0s，所以在 20.5 时仍在冷却
        if (!cd.IsCoolingDown(CooldownSource.PostCombat, 20.5))
            return (false, "PostCombat should last 1.0s, still cooling at 20.5");

        // 在 21.0 时已过期
        if (cd.IsCoolingDown(CooldownSource.PostCombat, 21.0))
            return (false, "PostCombat should expire at 21.0");

        return (true, "");
    }

    private static (bool, string) InteractionCooldown_PoiInteraction_ShortCooldown()
    {
        var cd = new InteractionCooldown();
        double time = 30.0;

        cd.Trigger(CooldownSource.PoiInteraction, time);

        // POI 冷却 = 0.3s，在 30.2 仍在冷却
        if (!cd.IsCoolingDown(CooldownSource.PoiInteraction, 30.2))
            return (false, "Poi cooldown should last 0.3s, still cooling at 30.2");

        // 在 30.3 已过期
        if (cd.IsCoolingDown(CooldownSource.PoiInteraction, 30.3))
            return (false, "Poi cooldown should expire at 30.3");

        return (true, "");
    }

    private static (bool, string) InteractionCooldown_ClearAll_ResetsAll()
    {
        var cd = new InteractionCooldown();
        double time = 40.0;

        cd.Trigger(CooldownSource.EntityOverlap, time);
        cd.Trigger(CooldownSource.PostCombat, time);
        cd.Trigger(CooldownSource.PoiInteraction, time);
        cd.Trigger(CooldownSource.PanelClose, time);

        if (!cd.IsCoolingDown(CooldownSource.EntityOverlap, time))
            return (false, "EntityOverlap should be active");

        cd.ClearAll();

        if (cd.IsCoolingDown(CooldownSource.EntityOverlap, time))
            return (false, "EntityOverlap should be cleared");
        if (cd.IsCoolingDown(CooldownSource.PostCombat, time))
            return (false, "PostCombat should be cleared");
        if (cd.IsCoolingDown(CooldownSource.PoiInteraction, time))
            return (false, "PoiInteraction should be cleared");
        if (cd.IsCoolingDown(CooldownSource.PanelClose, time))
            return (false, "PanelClose should be cleared");

        return (true, "");
    }

    private static (bool, string) InteractionCooldown_Clear_SingleSource()
    {
        var cd = new InteractionCooldown();
        double time = 50.0;

        cd.Trigger(CooldownSource.EntityOverlap, time);
        cd.Trigger(CooldownSource.PostCombat, time);

        cd.Clear(CooldownSource.EntityOverlap);

        if (cd.IsCoolingDown(CooldownSource.EntityOverlap, time))
            return (false, "EntityOverlap should be cleared");
        if (!cd.IsCoolingDown(CooldownSource.PostCombat, time))
            return (false, "PostCombat should still be active");

        return (true, "");
    }

    private static (bool, string) InteractionCooldown_GetRemainingSeconds()
    {
        var cd = new InteractionCooldown();
        double time = 60.0;

        cd.Trigger(CooldownSource.EntityOverlap, time); // until 60.5

        double remaining = cd.GetRemainingSeconds(CooldownSource.EntityOverlap, 60.2);
        if (Math.Abs(remaining - 0.3) > 0.01)
            return (false, $"expected ~0.3 remaining, got {remaining:F3}");

        double remainingAfterExpiry = cd.GetRemainingSeconds(CooldownSource.EntityOverlap, 61.0);
        if (remainingAfterExpiry != 0.0)
            return (false, $"expected 0 remaining after expiry, got {remainingAfterExpiry:F3}");

        double neverTriggered = cd.GetRemainingSeconds(CooldownSource.PanelClose, 60.0);
        if (neverTriggered != 0.0)
            return (false, $"expected 0 for never triggered, got {neverTriggered:F3}");

        return (true, "");
    }

    // ========================================
    // CommandRouter 测试
    // ========================================

    private static (bool, string) CommandRouter_EmptyWorld_ReturnsMoveTo()
    {
        var router = new OverworldCommandRouter();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();

        var cmd = router.ResolveSimple(new Vector2(500, 500), entities, pois, "player");

        if (cmd.Type != OverworldCommandType.MoveTo)
            return (false, $"expected MoveTo, got {cmd.Type}");
        if (cmd.WorldPosition.DistanceTo(new Vector2(500, 500)) > 1f)
            return (false, $"position mismatch: {cmd.WorldPosition}");

        return (true, "");
    }

    private static (bool, string) CommandRouter_ClickOnPoi_ReturnsMoveToPoi()
    {
        var router = new OverworldCommandRouter();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();

        var poi = new OverworldPOI
        {
            PoiName = "TestVillage",
            Position = new Vector2(500, 500),
            PoiTypeEnum = OverworldPOI.POIType.Village,
            OwningFaction = "neutral",
        };
        pois.Add(poi);

        // 点击 POI 位置（在 PoiHitRadius 内）
        var cmd = router.ResolveSimple(new Vector2(510, 510), entities, pois, "player");

        if (cmd.Type != OverworldCommandType.InspectPoi)
            return (false, $"expected InspectPoi, got {cmd.Type}");

        var poiCmd = cmd as MoveToPoiCommand;
        if (poiCmd == null)
            return (false, "command is not MoveToPoiCommand");
        if (poiCmd.Poi != poi)
            return (false, "POI reference mismatch");

        return (true, "");
    }

    private static (bool, string) CommandRouter_ClickOnHeroEntity_ReturnsInspectEntity()
    {
        var router = new OverworldCommandRouter();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();

        var hero = new OverworldEntity
        {
            EntityName = "TestHero",
            Faction = "enemy_kingdom",
            Position = new Vector2(800, 800),
            IsAlive = true,
            IsNamedCharacter = true,
            HeroId = "hero_001",
        };
        entities.Add(hero);

        // 点击英雄实体位置（在 HeroEntityHitRadius 内）
        var cmd = router.ResolveSimple(new Vector2(810, 810), entities, pois, "player");

        if (cmd.Type != OverworldCommandType.InspectEntity)
            return (false, $"expected InspectEntity, got {cmd.Type}");

        var entityCmd = cmd as InspectEntityCommand;
        if (entityCmd == null)
            return (false, "command is not InspectEntityCommand");
        if (entityCmd.Entity != hero)
            return (false, "entity reference mismatch");

        return (true, "");
    }

    private static (bool, string) EntityManager_CheckPlayerEncounters_IgnoresEngagedBattleParticipants()
    {
        var manager = CreateTrackedEntityManager();
        var attacker = new OverworldEntity
        {
            EntityName = "EngagedAttacker",
            Faction = "kingdom",
            Position = new Vector2(100, 100),
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Engaged,
        };
        var defender = new OverworldEntity
        {
            EntityName = "EngagedDefender",
            Faction = "bandit",
            Position = new Vector2(120, 100),
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Engaged,
        };
        attacker.EngagedWith = defender;
        defender.EngagedWith = attacker;
        manager.Entities.Add(attacker);
        manager.Entities.Add(defender);

        var hitBattleParticipant = manager.CheckPlayerEncounters(new Vector2(105, 100));
        if (hitBattleParticipant != null)
            return (false, $"engaged participant should not open normal interaction, got {hitBattleParticipant.EntityName}");

        var traveler = new OverworldEntity
        {
            EntityName = "NearbyTraveler",
            Faction = "neutral",
            Position = new Vector2(130, 100),
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Idle,
        };
        manager.Entities.Add(traveler);

        var hitNormalEntity = manager.CheckPlayerEncounters(new Vector2(130, 100));
        if (hitNormalEntity != traveler)
            return (false, "normal nearby entity should still open interaction");

        return (true, "");
    }

    // ========================================
    // CommandRouter + Layers 优先级测试
    // ========================================

    private static (bool, string) CommandRouter_WithLayers_BattlefieldBeatsHero()
    {
        var router = new OverworldCommandRouter();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();

        // 战场位于 (500, 500)，有一个英雄实体也在附近
        var battlefieldPos = new Vector2(500, 500);
        var attacker = new OverworldEntity
        {
            EntityName = "AtkLord",
            Faction = "player",
            Position = new Vector2(480, 500),
            IsAlive = true,
            IsNamedCharacter = true,
            HeroId = "hero_atk",
        };
        var defender = new OverworldEntity
        {
            EntityName = "DefLord",
            Faction = "hostile",
            Position = new Vector2(520, 500),
            IsAlive = true,
            IsNamedCharacter = false,
        };
        entities.Add(attacker);
        entities.Add(defender);

        // 英雄实体在战场旁
        var hero = new OverworldEntity
        {
            EntityName = "NearbyHero",
            Faction = "player",
            Position = new Vector2(550, 520),
            IsAlive = true,
            IsNamedCharacter = true,
            HeroId = "hero_nearby",
        };
        entities.Add(hero);

        // 创建战场 Layer 并同步
        var bfLayer = CreateBattlefieldLayer();
        var bfView = new BattlefieldView(
            key: "test_bf",
            battlefieldId: "bf_test001",
            attacker: attacker,
            defender: defender,
            position: battlefieldPos,
            attackerRelation: PlayerBattleRelation.Friendly,
            defenderRelation: PlayerBattleRelation.Hostile,
            attackerName: "Player Army",
            defenderName: "Enemy Army",
            attackerPower: 100f,
            defenderPower: 50f,
            attackerTotalPower: 100f,
            defenderTotalPower: 50f,
            progress: 0.5f
        );
        bfLayer.Sync(new List<BattlefieldView> { bfView });

        // 点击战场位置 (505, 505) → 应命中战场 marker，而非英雄
        var cmd = router.Resolve(
            new Vector2(505, 505),
            bfLayer,
            null,
            entities,
            pois,
            "player");

        if (cmd.Type != OverworldCommandType.JoinFieldBattle)
            return (false, $"expected JoinFieldBattle, got {cmd.Type}");

        return (true, "");
    }

    private static (bool, string) BattlefieldLayer_HitTest_UsesMarkerCircle()
    {
        var layer = CreateBattlefieldLayer();
        var attacker = new OverworldEntity { EntityName = "Atk", Faction = "player", Position = new Vector2(80, 100), IsAlive = true };
        var defender = new OverworldEntity { EntityName = "Def", Faction = "hostile", Position = new Vector2(120, 100), IsAlive = true };
        var center = new Vector2(100, 100);

        layer.Sync(new List<BattlefieldView>
        {
            new(
                key: "bf_circle",
                battlefieldId: "bf_circle",
                attacker: attacker,
                defender: defender,
                position: center,
                attackerRelation: PlayerBattleRelation.Friendly,
                defenderRelation: PlayerBattleRelation.Hostile,
                attackerName: "Atk",
                defenderName: "Def",
                attackerPower: 10f,
                defenderPower: 10f,
                attackerTotalPower: 10f,
                defenderTotalPower: 10f,
                progress: 0.1f)
        });

        if (layer.HitTest(center + new Vector2(OverworldBattlefieldLayer2D.MarkerHitRadius - 1f, 0)) != "bf_circle")
            return (false, "inside marker circle should hit battlefield");

        if (layer.HitTest(center + new Vector2(OverworldBattlefieldLayer2D.MarkerHitRadius + 1f, 0)) != null)
            return (false, "outside marker circle should not hit battlefield");

        return (true, "");
    }

    private static (bool, string) CommandRouter_BattlefieldClickOutsideMarker_FallsThrough()
    {
        var router = new OverworldCommandRouter();
        var layer = CreateBattlefieldLayer();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();
        var center = new Vector2(100, 100);

        var attacker = new OverworldEntity { EntityName = "Atk", Faction = "player", Position = new Vector2(80, 100), IsAlive = true };
        var defender = new OverworldEntity { EntityName = "Def", Faction = "hostile", Position = new Vector2(120, 100), IsAlive = true };
        entities.Add(attacker);
        entities.Add(defender);

        layer.Sync(new List<BattlefieldView>
        {
            new(
                key: "bf_click",
                battlefieldId: "bf_click",
                attacker: attacker,
                defender: defender,
                position: center,
                attackerRelation: PlayerBattleRelation.Friendly,
                defenderRelation: PlayerBattleRelation.Hostile,
                attackerName: "Atk",
                defenderName: "Def",
                attackerPower: 10f,
                defenderPower: 10f,
                attackerTotalPower: 10f,
                defenderTotalPower: 10f,
                progress: 0.1f)
        });

        var inside = router.Resolve(
            center + new Vector2(OverworldBattlefieldLayer2D.MarkerHitRadius - 1f, 0),
            layer,
            null,
            entities,
            pois,
            "player");

        if (inside.Type != OverworldCommandType.JoinFieldBattle)
            return (false, $"inside marker circle should join field battle, got {inside.Type}");

        var outside = router.Resolve(
            center + new Vector2(OverworldBattlefieldLayer2D.MarkerHitRadius + 1f, 0),
            layer,
            null,
            entities,
            pois,
            "player");

        if (outside.Type == OverworldCommandType.JoinFieldBattle)
            return (false, "outside marker circle should not join field battle");

        return (true, "");
    }

    private static (bool, string) SiegeLayer_HitTest_UsesMarkerCircle()
    {
        var layer = CreateSiegeLayer();
        var center = new Vector2(200, 200);
        var poi = CreateSiegePoi("CircleCastle", center);
        var siegeView = new SiegeView(
            poi: poi,
            position: center,
            poiName: poi.PoiName,
            attackerFaction: "hostile",
            defenderFaction: "kingdom",
            attackerCount: 1,
            defenderGarrison: 20,
            siegeDays: 2,
            attackerRelation: PlayerBattleRelation.Hostile,
            defenderRelation: PlayerBattleRelation.Friendly);

        layer.Sync(new List<SiegeView> { siegeView });

        if (layer.HitTest(center + new Vector2(OverworldSiegeLayer2D.MarkerHitRadius - 1f, 0)) != poi)
            return (false, "inside marker circle should hit siege");

        if (layer.HitTest(center + new Vector2(OverworldSiegeLayer2D.MarkerHitRadius + 1f, 0)) != null)
            return (false, "outside marker circle should not hit siege");

        return (true, "");
    }

    private static (bool, string) CommandRouter_SiegeClickOutsideMarker_FallsThroughToPoi()
    {
        var router = new OverworldCommandRouter();
        var layer = CreateSiegeLayer();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();
        var center = new Vector2(300, 300);

        var poi = CreateSiegePoi("RouterCastle", center);
        var siegeLord = new OverworldEntity
        {
            EntityName = "SiegeLord",
            Faction = "hostile",
            Position = center + new Vector2(-30, 0),
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Besieging,
            SiegeTarget = poi,
        };
        poi.SiegeBy = siegeLord;
        entities.Add(siegeLord);
        pois.Add(poi);

        var siegeView = new SiegeView(
            poi: poi,
            position: center,
            poiName: poi.PoiName,
            attackerFaction: "hostile",
            defenderFaction: "kingdom",
            attackerCount: 1,
            defenderGarrison: 20,
            siegeDays: 2,
            attackerRelation: PlayerBattleRelation.Hostile,
            defenderRelation: PlayerBattleRelation.Friendly);
        layer.Sync(new List<SiegeView> { siegeView });

        var inside = router.Resolve(
            center + new Vector2(OverworldSiegeLayer2D.MarkerHitRadius - 1f, 0),
            null,
            layer,
            entities,
            pois,
            "player");

        if (inside.Type != OverworldCommandType.JoinSiege)
            return (false, $"inside marker circle should join siege, got {inside.Type}");

        var outside = router.Resolve(
            center + new Vector2(OverworldSiegeLayer2D.MarkerHitRadius + 1f, 0),
            null,
            layer,
            entities,
            pois,
            "player");

        if (outside.Type != OverworldCommandType.InspectPoi)
            return (false, $"outside siege marker should fall through to POI, got {outside.Type}");

        return (true, "");
    }

    private static OverworldPOI CreateSiegePoi(string name, Vector2 position)
    {
        return new OverworldPOI
        {
            PoiName = name,
            Position = position,
            PoiTypeEnum = OverworldPOI.POIType.Castle,
            OwningFaction = "kingdom",
            IsUnderSiege = true,
            SiegeDays = 2,
            GarrisonCurrent = 20,
            GarrisonMax = 40,
        };
    }

    private static (bool, string) CommandRouter_WithLayers_SiegeBeatsMove()
    {
        var router = new OverworldCommandRouter();
        var entities = new List<OverworldEntity>();
        var pois = new List<OverworldPOI>();

        // 围城 POI 位置
        var poiPos = new Vector2(400, 400);
        var poi = new OverworldPOI
        {
            PoiName = "SiegeCastle",
            Position = poiPos,
            PoiTypeEnum = OverworldPOI.POIType.Castle,
            OwningFaction = "kingdom",
            IsUnderSiege = true,
            SiegeDays = 3,
            GarrisonCurrent = 30,
            GarrisonMax = 50,
        };
        pois.Add(poi);

        // 围城者
        var siegeLord = new OverworldEntity
        {
            EntityName = "SiegeLord",
            Faction = "hostile",
            Position = new Vector2(370, 400),
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Besieging,
            SiegeTarget = poi,
        };
        entities.Add(siegeLord);
        poi.SiegeBy = siegeLord;

        // 创建围城 Layer 并同步
        var siegeLayer = CreateSiegeLayer();
        var siegeView = new SiegeView(
            poi: poi,
            position: poiPos,
            poiName: "SiegeCastle",
            attackerFaction: "hostile",
            defenderFaction: "kingdom",
            attackerCount: 1,
            defenderGarrison: 30,
            siegeDays: 3,
            attackerRelation: PlayerBattleRelation.Hostile,
            defenderRelation: PlayerBattleRelation.Friendly
        );
        siegeLayer.Sync(new List<SiegeView> { siegeView });

        // 点击围城位置 → 应命中围城
        var cmd = router.Resolve(
            new Vector2(410, 410),
            null,
            siegeLayer,
            entities,
            pois,
            "player");

        if (cmd.Type != OverworldCommandType.JoinSiege)
            return (false, $"expected JoinSiege, got {cmd.Type}");

        return (true, "");
    }

    // ========================================
    // View 结构体构造测试
    // ========================================

    private static (bool, string) MapEntityView_Construction_AllFieldsAccessible()
    {
        var entity = new OverworldEntity
        {
            EntityName = "view_test_entity",
            Faction = "player",
            Position = new Vector2(100, 200),
            IsAlive = true,
        };

        var view = new MapEntityView(
            entity: entity,
            position: new Vector2(100, 200),
            factionColor: Colors.Blue,
            labelColor: Colors.White,
            displayText: "Test Entity",
            isEssential: true,
            isVisible: true
        );

        if (view.Entity != entity)
            return (false, "Entity ref mismatch");
        if (view.Position != new Vector2(100, 200))
            return (false, "Position mismatch");
        if (view.FactionColor != Colors.Blue)
            return (false, "FactionColor mismatch");
        if (view.DisplayText != "Test Entity")
            return (false, "DisplayText mismatch");
        if (!view.IsEssential)
            return (false, "IsEssential mismatch");
        if (!view.IsVisible)
            return (false, "IsVisible mismatch");

        return (true, "");
    }

    private static (bool, string) BattlefieldView_Construction_AllFieldsAccessible()
    {
        var attacker = new OverworldEntity { EntityName = "attacker", Faction = "player", Position = new Vector2(100, 100), IsAlive = true };
        var defender = new OverworldEntity { EntityName = "defender", Faction = "bandit", Position = new Vector2(150, 100), IsAlive = true };

        var view = new BattlefieldView(
            key: "battle_001",
            battlefieldId: "bf_001",
            attacker: attacker,
            defender: defender,
            position: new Vector2(125, 100),
            attackerRelation: PlayerBattleRelation.Friendly,
            defenderRelation: PlayerBattleRelation.Hostile,
            attackerName: "Player Army",
            defenderName: "Bandits",
            attackerPower: 100f,
            defenderPower: 50f,
            attackerTotalPower: 100f,
            defenderTotalPower: 50f,
            progress: 0f
        );

        if (view.Key != "battle_001")
            return (false, "Key mismatch");
        if (view.BattlefieldId != "bf_001")
            return (false, "BattlefieldId mismatch");
        if (view.Attacker != attacker)
            return (false, "Attacker ref mismatch");
        if (view.Defender != defender)
            return (false, "Defender ref mismatch");
        if (view.AttackerRelation != PlayerBattleRelation.Friendly)
            return (false, "AttackerRelation mismatch");
        if (view.DefenderPower != 50f)
            return (false, "DefenderPower mismatch");

        return (true, "");
    }

    private static (bool, string) ViewProjectionSnapshot_EmptyState_AllListsEmpty()
    {
        var snapshot = new ViewProjectionSnapshot();

        if (snapshot.Entities.Count != 0)
            return (false, $"Entities should be empty, got {snapshot.Entities.Count}");
        if (snapshot.Battlefields.Count != 0)
            return (false, $"Battlefields should be empty, got {snapshot.Battlefields.Count}");
        if (snapshot.Sieges.Count != 0)
            return (false, $"Sieges should be empty, got {snapshot.Sieges.Count}");
        if (snapshot.Pois.Count != 0)
            return (false, $"Pois should be empty, got {snapshot.Pois.Count}");
        if (snapshot.BattlefieldParticipants.Count != 0)
            return (false, $"BattlefieldParticipants should be empty, got {snapshot.BattlefieldParticipants.Count}");

        return (true, "");
    }

    // ========================================
    // NvN Regression: Projection + BattlefieldRegistry
    // ========================================

    private static (bool, string) Projection_Registry_NvN_OneViewAllParticipants()
    {
        // ── Setup: 2v2 战场 ──
        var resolver = new BattleResolver();
        var registry = new BattlefieldRegistry();
        var projection = new OverworldViewProjection("player");

        var atk1 = new OverworldEntity { EntityName = "Atk1", Faction = "player", Position = new Vector2(100, 100), IsAlive = true, CombatPower = 100, PartySize = 10 };
        var atk2 = new OverworldEntity { EntityName = "Atk2", Faction = "player", Position = new Vector2(110, 100), IsAlive = true, CombatPower = 80, PartySize = 8 };
        var def1 = new OverworldEntity { EntityName = "Def1", Faction = "hostile", Position = new Vector2(200, 100), IsAlive = true, CombatPower = 60, PartySize = 6 };
        var def2 = new OverworldEntity { EntityName = "Def2", Faction = "hostile", Position = new Vector2(210, 100), IsAlive = true, CombatPower = 50, PartySize = 5 };

        var bf = new Battlefield { Position = new Vector2(150, 100), DurationHours = 3f, StartedAtHour = 0f };
        bf.Join(atk1, joinAsAttacker: true);
        bf.Join(atk2, joinAsAttacker: true);
        bf.Join(def1, joinAsAttacker: false);
        bf.Join(def2, joinAsAttacker: false);
        resolver.Battlefields.Add(bf);

        var allEntities = new List<OverworldEntity> { atk1, atk2, def1, def2 };
        var visible = new List<OverworldEntity>(allEntities);
        var entityMgr = CreateEntityManager(allEntities);

        // ── Act: Project with Registry ──
        var snapshot = projection.Project(
            entityMgr: entityMgr,
            visibleEntities: visible,
            playerPos: Vector2.Zero,
            visionRange: 5000f,
            registry: registry,
            battleResolver: resolver,
            currentGameHour: 1f);

        // ── Assert: 1 BattlefieldView, 4 participants ──
        if (snapshot.Battlefields.Count != 1)
            return (false, $"expected 1 BattlefieldView, got {snapshot.Battlefields.Count}");

        var bfv = snapshot.Battlefields[0];
        if (string.IsNullOrEmpty(bfv.BattlefieldId))
            return (false, "BattlefieldId should not be empty");
        if (bfv.AttackerTotalPower != atk1.CombatPower * atk1.PartySize + atk2.CombatPower * atk2.PartySize)
            return (false, $"AttackerTotalPower mismatch: {bfv.AttackerTotalPower}");
        if (bfv.DefenderTotalPower != def1.CombatPower * def1.PartySize + def2.CombatPower * def2.PartySize)
            return (false, $"DefenderTotalPower mismatch: {bfv.DefenderTotalPower}");
        if (bfv.Progress <= 0f)
            return (false, $"Progress should be > 0, got {bfv.Progress}");

        if (snapshot.BattlefieldParticipants.Count != 4)
            return (false, $"expected 4 BattlefieldParticipants, got {snapshot.BattlefieldParticipants.Count}");

        return (true, $"1 BattlefieldView ({bfv.Key}), {snapshot.BattlefieldParticipants.Count} participants");
    }

    private static (bool, string) Projection_Registry_Fallback_EngagedWith()
    {
        // ── Setup: EngagedWith pair, NO registry ──
        var projection = new OverworldViewProjection("player");

        var a = new OverworldEntity { EntityName = "EngA", Faction = "player", Position = new Vector2(100, 100), IsAlive = true, CombatPower = 100, PartySize = 10, CurrentAIState = OverworldEntity.AIState.Engaged };
        var b = new OverworldEntity { EntityName = "EngB", Faction = "hostile", Position = new Vector2(200, 100), IsAlive = true, CombatPower = 80, PartySize = 8, CurrentAIState = OverworldEntity.AIState.Engaged };
        a.EngagedWith = b;
        b.EngagedWith = a;

        var allEntities = new List<OverworldEntity> { a, b };
        var visible = new List<OverworldEntity>(allEntities);
        var entityMgr = CreateEntityManager(allEntities);

        // ── Act: Project WITHOUT Registry (null registry + resolver) ──
        var snapshot = projection.Project(
            entityMgr: entityMgr,
            visibleEntities: visible,
            playerPos: Vector2.Zero,
            visionRange: 5000f,
            registry: null,
            battleResolver: null,
            currentGameHour: 0f);

        // ── Assert: 1 BattlefieldView fallback, 2 participants ──
        if (snapshot.Battlefields.Count != 1)
            return (false, $"expected 1 fallback BattlefieldView, got {snapshot.Battlefields.Count}");

        if (snapshot.BattlefieldParticipants.Count != 2)
            return (false, $"expected 2 fallback participants, got {snapshot.BattlefieldParticipants.Count}");

        // No BattlefieldId when coming from EngagedWith
        if (!string.IsNullOrEmpty(snapshot.Battlefields[0].BattlefieldId))
            return (false, "fallback BattlefieldId should be empty");

        return (true, $"1 fallback BattlefieldView, {snapshot.BattlefieldParticipants.Count} participants");
    }

    private static (bool, string) BattleResolver_FieldBattle_ProjectsToClickableJoinCommand()
    {
        var resolver = new BattleResolver();
        var registry = new BattlefieldRegistry();
        var projection = new OverworldViewProjection("player");
        var router = new OverworldCommandRouter();
        var layer = CreateBattlefieldLayer();

        var attacker = new OverworldEntity
        {
            EntityName = "FieldAlly",
            Faction = "player",
            Position = new Vector2(100, 100),
            IsAlive = true,
            CombatPower = 100f,
            PartySize = 10,
        };
        var defender = new OverworldEntity
        {
            EntityName = "FieldEnemy",
            Faction = "bandit",
            Position = new Vector2(150, 100),
            IsAlive = true,
            IsHostileToPlayer = true,
            CombatPower = 80f,
            PartySize = 8,
        };

        var allEntities = new List<OverworldEntity> { attacker, defender };
        resolver.ProcessEntityInteractions(allEntities, currentGameHour: 2f);

        if (resolver.Battlefields.Count != 1)
            return (false, $"expected resolver to create 1 battlefield, got {resolver.Battlefields.Count}");
        if (attacker.CurrentAIState != OverworldEntity.AIState.Engaged || defender.CurrentAIState != OverworldEntity.AIState.Engaged)
            return (false, "participants should be marked Engaged after contact");

        var entityMgr = CreateEntityManager(allEntities);
        var snapshot = projection.Project(
            entityMgr,
            visibleEntities: new List<OverworldEntity>(allEntities),
            playerPos: Vector2.Zero,
            visionRange: 5000f,
            registry: registry,
            battleResolver: resolver,
            currentGameHour: 2.5f);

        if (snapshot.Battlefields.Count != 1)
            return (false, $"expected projection to expose 1 battlefield, got {snapshot.Battlefields.Count}");
        if (snapshot.Entities.Count != 0)
            return (false, $"engaged participants should be hidden from normal entity layer, got {snapshot.Entities.Count}");

        layer.Sync(snapshot.Battlefields);
        var battle = snapshot.Battlefields[0];
        var command = router.Resolve(
            battle.Position,
            layer,
            null,
            allEntities,
            new List<OverworldPOI>(),
            "player");

        if (command is not JoinFieldBattleCommand joinCommand)
            return (false, $"expected JoinFieldBattleCommand, got {command.Type}");
        if (joinCommand.Opportunity.BattlefieldId != battle.BattlefieldId)
            return (false, "join opportunity should keep the resolver battlefield id");
        if (joinCommand.Opportunity.Attackers.Count != 1 || joinCommand.Opportunity.Defenders.Count != 1)
            return (false, "join opportunity should include both battlefield sides");

        return (true, $"battlefield {battle.BattlefieldId} projected and clickable");
    }

    private static (bool, string) BattleResolver_ManyAttackersOneDefender_OneClickableBattlefield()
    {
        var resolver = new BattleResolver();
        var registry = new BattlefieldRegistry();
        var projection = new OverworldViewProjection("player");
        var router = new OverworldCommandRouter();

        var caravan = new OverworldEntity
        {
            EntityName = "MerchantCaravan",
            Faction = "merchant",
            Position = new Vector2(100, 100),
            IsAlive = true,
            CombatPower = 70f,
            PartySize = 12,
        };

        var bandits = new List<OverworldEntity>
        {
            CreateBandit("BanditA", new Vector2(125, 100)),
            CreateBandit("BanditB", new Vector2(130, 110)),
            CreateBandit("BanditC", new Vector2(118, 92)),
        };

        var allEntities = new List<OverworldEntity> { caravan };
        allEntities.AddRange(bandits);

        resolver.ProcessEntityInteractions(allEntities, currentGameHour: 4f);
        if (resolver.Battlefields.Count != 1)
            return (false, $"expected exactly 1 core battlefield, got {resolver.Battlefields.Count}");
        if (resolver.Battlefields[0].ParticipantCount != 4)
            return (false, $"expected 4 participants in one battlefield, got {resolver.Battlefields[0].ParticipantCount}");

        var entityMgr = CreateEntityManager(allEntities);
        var visible = new List<OverworldEntity>(allEntities);
        var snapshot = projection.Project(
            entityMgr,
            visible,
            playerPos: Vector2.Zero,
            visionRange: 5000f,
            registry: registry,
            battleResolver: resolver,
            currentGameHour: 4.5f);

        if (snapshot.Battlefields.Count != 1)
            return (false, $"registry projection should expose 1 battlefield, got {snapshot.Battlefields.Count}");

        var fallbackSnapshot = projection.Project(
            entityMgr,
            visible,
            playerPos: Vector2.Zero,
            visionRange: 5000f,
            registry: null,
            battleResolver: null,
            currentGameHour: 4.5f);

        if (fallbackSnapshot.Battlefields.Count != 1)
            return (false, $"fallback projection should merge same BattlefieldId into 1 marker, got {fallbackSnapshot.Battlefields.Count}");

        var battle = fallbackSnapshot.Battlefields[0];
        var layer = CreateBattlefieldLayer();
        layer.Sync(fallbackSnapshot.Battlefields);
        var command = router.Resolve(
            battle.Position,
            layer,
            null,
            allEntities,
            new List<OverworldPOI>(),
            "player");

        if (command is not JoinFieldBattleCommand joinCommand)
            return (false, $"expected clickable JoinFieldBattleCommand, got {command.Type}");
        if (string.IsNullOrEmpty(joinCommand.Opportunity.BattlefieldId))
            return (false, "fallback join opportunity should preserve BattlefieldId");
        if (joinCommand.Opportunity.Attackers.Count + joinCommand.Opportunity.Defenders.Count != 4)
            return (false, "join opportunity should include all 4 participants");

        return (true, "3 attackers vs 1 defender collapsed into one clickable battlefield");
    }

    private static (bool, string) BattleResolver_DuplicateNamedAttackers_KeepDistinctParticipants()
    {
        var resolver = new BattleResolver();
        var registry = new BattlefieldRegistry();
        var projection = new OverworldViewProjection("player");
        var router = new OverworldCommandRouter();

        var caravan = new OverworldEntity
        {
            EntityName = "MerchantCaravan",
            Faction = "merchant",
            Position = new Vector2(100, 100),
            IsAlive = true,
            CombatPower = 70f,
            PartySize = 12,
        };

        var bandits = new List<OverworldEntity>
        {
            CreateBandit("山贼", new Vector2(125, 100)),
            CreateBandit("山贼", new Vector2(130, 110)),
            CreateBandit("山贼", new Vector2(118, 92)),
        };

        var allEntities = new List<OverworldEntity> { caravan };
        allEntities.AddRange(bandits);

        resolver.ProcessEntityInteractions(allEntities, currentGameHour: 4f);
        if (resolver.Battlefields.Count != 1)
            return (false, $"expected one battlefield, got {resolver.Battlefields.Count}");
        if (resolver.Battlefields[0].ParticipantCount != 4)
            return (false, $"expected 4 core participants, got {resolver.Battlefields[0].ParticipantCount}");

        var snapshot = projection.Project(
            CreateEntityManager(allEntities),
            new List<OverworldEntity>(allEntities),
            playerPos: Vector2.Zero,
            visionRange: 5000f,
            registry: registry,
            battleResolver: resolver,
            currentGameHour: 4.5f);

        if (snapshot.Battlefields.Count != 1)
            return (false, $"expected one projected battlefield, got {snapshot.Battlefields.Count}");

        var battle = snapshot.Battlefields[0];
        var layer = CreateBattlefieldLayer();
        layer.Sync(snapshot.Battlefields);
        var command = router.Resolve(
            battle.Position,
            layer,
            null,
            allEntities,
            new List<OverworldPOI>(),
            "player");

        if (command is not JoinFieldBattleCommand joinCommand)
            return (false, $"expected JoinFieldBattleCommand, got {command.Type}");

        var participants = joinCommand.Opportunity.AllParticipants().Distinct().ToList();
        if (participants.Count != 4)
            return (false, $"duplicate names should keep 4 distinct entity refs, got {participants.Count}");
        if (!participants.Contains(caravan) || bandits.Any(b => !participants.Contains(b)))
            return (false, "join opportunity lost at least one original participant reference");

        var serviceOpportunity = WarBattleJoinService.Query(
            battle.Position,
            allEntities,
            new List<OverworldPOI>(),
            playerFaction: "player",
            joinRadius: 500f,
            battlefieldRegistry: registry,
            battleResolver: resolver,
            currentGameHour: 4.5f);
        if (serviceOpportunity == null)
            return (false, "WarBattleJoinService should find duplicate-named battlefield");
        if (serviceOpportunity.AllParticipants().Distinct().Count() != 4)
            return (false, "WarBattleJoinService should preserve duplicate-named participants as distinct refs");

        var ctx = BattleContextFactory.CreatePlayerJoinedFieldBattle(
            joinCommand.Opportunity,
            joinAttacker: false,
            grid: null,
            playerPixelPosition: Vector2.Zero,
            nearbyEntities: allEntities);

        if (ctx.JoinedAttackers.Distinct().Count() + ctx.JoinedDefenders.Distinct().Count() != 4)
            return (false, "battle context should preserve all distinct duplicate-named participants");

        return (true, "duplicate-named attackers remain distinct through click and battle context");
    }

    private static (bool, string) PlayerJoinFieldBattle_PullsNearbyNonNeutralParticipants()
    {
        var attacker = new OverworldEntity { EntityName = "PlayerSide", Faction = "player", Position = new Vector2(100, 100), IsAlive = true, CombatPower = 30f, PartySize = 4 };
        var defender = CreateBandit("EnemySide", new Vector2(150, 100));
        var nearbyAlly = new OverworldEntity { EntityName = "NearbyAlly", Faction = "player", Position = new Vector2(120, 120), IsAlive = true, CombatPower = 20f, PartySize = 3 };
        var nearbyEnemy = CreateBandit("NearbyEnemy", new Vector2(180, 110));
        var neutral = new OverworldEntity { EntityName = "NeutralTraveler", Faction = "neutral", Position = new Vector2(130, 95), IsAlive = true, CombatPower = 100f, PartySize = 10, IsHostileToPlayer = false };

        var opportunity = new JoinOpportunity
        {
            Type = WarBattleType.FieldBattle,
            Attacker = attacker,
            DefenderEntity = defender,
            WorldPosition = new Vector2(125, 100),
            Attackers = new List<OverworldEntity> { attacker },
            Defenders = new List<OverworldEntity> { defender },
        };

        var ctx = BattleContextFactory.CreatePlayerJoinedFieldBattle(
            opportunity,
            joinAttacker: true,
            grid: null,
            playerPixelPosition: Vector2.Zero,
            nearbyEntities: new[] { attacker, defender, nearbyAlly, nearbyEnemy, neutral });

        if (!ctx.JoinedAttackers.Contains(nearbyAlly))
            return (false, "nearby player-side ally should join attackers");
        if (!ctx.JoinedDefenders.Contains(nearbyEnemy))
            return (false, "nearby hostile entity should join defenders");
        if (ctx.JoinedAttackers.Contains(neutral) || ctx.JoinedDefenders.Contains(neutral))
            return (false, "neutral entity should not be pulled into player battle");
        if (ctx.AttackerDeployment == null || ctx.DefenderDeployment == null)
            return (false, "deployments should be populated for both sides");

        int expectedAllyUnits = EntityCombatBridge.GetDeployment(attacker, true).Length
            + EntityCombatBridge.GetDeployment(nearbyAlly, true).Length;
        int expectedEnemyUnits = EntityCombatBridge.GetDeployment(defender, false).Length
            + EntityCombatBridge.GetDeployment(nearbyEnemy, false).Length;
        if (ctx.AttackerDeployment.Length != expectedAllyUnits)
            return (false, $"expected {expectedAllyUnits} allied deployment entries, got {ctx.AttackerDeployment.Length}");
        if (ctx.DefenderDeployment.Length != expectedEnemyUnits)
            return (false, $"expected {expectedEnemyUnits} enemy deployment entries, got {ctx.DefenderDeployment.Length}");

        return (true, "nearby non-neutral sides pulled into player field battle");
    }

    private static (bool, string) PlayerInitiatedEntityBattle_PullsNearbyNonNeutralParticipants()
    {
        var defender = CreateBandit("ClickedEnemy", new Vector2(150, 100));
        var nearbyAlly = new OverworldEntity { EntityName = "NearbyAlly", Faction = "player", Position = new Vector2(110, 100), IsAlive = true, CombatPower = 25f, PartySize = 4 };
        var nearbyEnemy = CreateBandit("NearbyEnemy", new Vector2(180, 120));
        var neutral = new OverworldEntity { EntityName = "NeutralTraveler", Faction = "neutral", Position = new Vector2(130, 95), IsAlive = true, CombatPower = 100f, PartySize = 10, IsHostileToPlayer = false };
        var farEnemy = CreateBandit("FarEnemy", new Vector2(900, 900));

        var ctx = BattleContextFactory.CreatePlayerInitiatedEntityBattle(
            defender,
            grid: null,
            playerPixelPosition: new Vector2(100, 100),
            nearbyEntities: new[] { defender, nearbyAlly, nearbyEnemy, neutral, farEnemy },
            playerFaction: "player",
            seed: 123);

        if (!ctx.JoinedAttackers.Contains(nearbyAlly))
            return (false, "nearby player-side ally should join player side");
        if (!ctx.JoinedDefenders.Contains(defender) || !ctx.JoinedDefenders.Contains(nearbyEnemy))
            return (false, "clicked enemy and nearby hostile entity should join enemy side");
        if (ctx.JoinedAttackers.Contains(neutral) || ctx.JoinedDefenders.Contains(neutral))
            return (false, "neutral entity should not be pulled into player-initiated battle");
        if (ctx.JoinedDefenders.Contains(farEnemy))
            return (false, "far hostile entity should not be pulled into player-initiated battle");
        if (ctx.WarJoinOppRef == null || !ctx.PlayerJoinedAsAttacker)
            return (false, "player-initiated multi-entity battle should keep join metadata for cleanup");

        int expectedAllyUnits = EntityCombatBridge.GetDeployment(nearbyAlly, true).Length;
        int expectedEnemyUnits = EntityCombatBridge.GetDeployment(defender, false).Length
            + EntityCombatBridge.GetDeployment(nearbyEnemy, false).Length;
        if (ctx.AttackerDeployment == null || ctx.AttackerDeployment.Length != expectedAllyUnits)
            return (false, $"expected {expectedAllyUnits} allied deployment entries, got {ctx.AttackerDeployment?.Length ?? -1}");
        if (ctx.DefenderDeployment == null || ctx.DefenderDeployment.Length != expectedEnemyUnits)
            return (false, $"expected {expectedEnemyUnits} enemy deployment entries, got {ctx.DefenderDeployment?.Length ?? -1}");

        return (true, "player-initiated battle pulls nearby non-neutral sides");
    }

    private static (bool, string) BattleContextFactory_DeduplicatesRepeatedEntityParticipants()
    {
        var attacker = new OverworldEntity { EntityName = "Attacker", Faction = "player", Position = new Vector2(100, 100), IsAlive = true, CombatPower = 20f, PartySize = 2 };
        var defender = CreateBandit("Defender", new Vector2(150, 100));
        var ally = new OverworldEntity { EntityName = "Ally", Faction = "player", Position = new Vector2(120, 100), IsAlive = true, CombatPower = 25f, PartySize = 3 };
        var enemy = CreateBandit("Enemy", new Vector2(160, 120));

        var opportunity = new JoinOpportunity
        {
            Type = WarBattleType.FieldBattle,
            Attacker = attacker,
            DefenderEntity = defender,
            WorldPosition = new Vector2(125, 100),
            Attackers = new List<OverworldEntity> { attacker, ally, ally, enemy },
            Defenders = new List<OverworldEntity> { defender, defender, enemy },
        };

        var ctx = BattleContextFactory.CreatePlayerJoinedFieldBattle(
            opportunity,
            joinAttacker: true,
            grid: null,
            playerPixelPosition: Vector2.Zero,
            nearbyEntities: Array.Empty<OverworldEntity>());

        if (ctx.JoinedAttackers.Count(e => e == ally) != 1)
            return (false, "ally should appear once on attacker side");
        if (ctx.JoinedDefenders.Count(e => e == defender) != 1)
            return (false, "defender should appear once on defender side");
        if (ctx.JoinedAttackers.Contains(defender) || ctx.JoinedDefenders.Contains(attacker))
            return (false, "primary attacker/defender should not cross sides");
        if (ctx.JoinedAttackers.Contains(enemy) && ctx.JoinedDefenders.Contains(enemy))
            return (false, "same entity should not be present on both sides");

        var allParticipants = ctx.JoinedAttackers.Concat(ctx.JoinedDefenders).ToList();
        if (allParticipants.Count != allParticipants.Distinct().Count())
            return (false, "battle context should not contain duplicate participant refs");

        int expectedAllyDeployments = ctx.JoinedAttackers.Sum(e => EntityCombatBridge.GetDeployment(e, true).Length);
        int expectedEnemyDeployments = ctx.JoinedDefenders.Sum(e => EntityCombatBridge.GetDeployment(e, false).Length);
        if (ctx.AttackerDeployment?.Length != expectedAllyDeployments)
            return (false, $"attacker deployments duplicated: expected {expectedAllyDeployments}, got {ctx.AttackerDeployment?.Length ?? -1}");
        if (ctx.DefenderDeployment?.Length != expectedEnemyDeployments)
            return (false, $"defender deployments duplicated: expected {expectedEnemyDeployments}, got {ctx.DefenderDeployment?.Length ?? -1}");

        return (true, "battle context participant refs are deduplicated across sides and deployments");
    }

    private static (bool, string) AiBattlefieldResponse_JoinsOrFleesBySidePower()
    {
        var resolver = new BattleResolver();
        var allyInBattle = new OverworldEntity { EntityName = "AllyInBattle", Faction = "kingdom", Position = new Vector2(100, 100), IsAlive = true, CombatPower = 40f, PartySize = 5 };
        var enemyInBattle = CreateBandit("EnemyInBattle", new Vector2(140, 100));
        var strongAlly = new OverworldEntity { EntityName = "StrongAlly", Faction = "kingdom", Position = new Vector2(110, 140), IsAlive = true, CombatPower = 120f, PartySize = 10 };
        var weakAlly = new OverworldEntity { EntityName = "WeakAlly", Faction = "kingdom", Position = new Vector2(115, 150), IsAlive = true, CombatPower = 1f, PartySize = 1, AIStrategy = AIStrategyEnum.Cautious };
        var neutral = new OverworldEntity { EntityName = "Neutral", Faction = "neutral", Position = new Vector2(120, 120), IsAlive = true, CombatPower = 300f, PartySize = 20, IsHostileToPlayer = false };

        var battlefield = new Battlefield { Position = new Vector2(120, 100), StartedAtHour = 1f, DurationHours = 3f };
        battlefield.Join(allyInBattle, true);
        battlefield.Join(enemyInBattle, false);
        resolver.Battlefields.Add(battlefield);

        BattlefieldInterventionService.ProcessAiBattlefieldResponses(
            new List<OverworldEntity> { allyInBattle, enemyInBattle, weakAlly, strongAlly, neutral },
            resolver,
            currentGameHour: 1.5f);

        if (!battlefield.Attackers.Contains(strongAlly) || strongAlly.CurrentAIState != OverworldEntity.AIState.Engaged)
            return (false, "strong ally should join the friendly side");
        if (battlefield.Attackers.Contains(weakAlly) || weakAlly.CurrentAIState != OverworldEntity.AIState.Fleeing)
            return (false, "weak ally should flee instead of joining");
        if (battlefield.Attackers.Contains(neutral) || battlefield.Defenders.Contains(neutral) || neutral.CurrentAIState == OverworldEntity.AIState.Fleeing)
            return (false, "neutral entity should ignore battlefield");

        return (true, "AI joins or flees battlefield based on side power");
    }

    private static OverworldEntity CreateBandit(string name, Vector2 position)
        => new()
        {
            EntityName = name,
            Faction = "bandit",
            Position = position,
            IsAlive = true,
            IsHostileToPlayer = true,
            CombatPower = 45f,
            PartySize = 6,
        };

    private static OverworldEntityManager CreateEntityManager(
        IEnumerable<OverworldEntity> entities,
        IEnumerable<OverworldPOI>? pois = null)
    {
        var entityMgr = CreateTrackedEntityManager();
        entityMgr.SimCtx.Entities.AddRange(entities);
        if (pois != null)
            entityMgr.SimCtx.Pois.AddRange(pois);
        return entityMgr;
    }

    private static OverworldEntityManager CreateTrackedEntityManager()
    {
        var entityMgr = new OverworldEntityManager();
        TestNodes.Add(entityMgr);
        return entityMgr;
    }
}
