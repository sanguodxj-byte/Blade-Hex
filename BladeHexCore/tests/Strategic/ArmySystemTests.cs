// ArmySystemTests.cs
// M3.5 集结军团系统单元测试套件
// 覆盖: ArmyRegistry CRUD、MarshalSelector 选帅逻辑、ArmyTickProcessor 集结/解散/新闻
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class ArmySystemTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else    { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string, bool, string)> EnumerateTests()
    {
        // ArmyRegistry
        yield return Run(nameof(Registry_Create_AssignsMarshalArmyId),      Registry_Create_AssignsMarshalArmyId);
        yield return Run(nameof(Registry_Remove_ClearsMembers),              Registry_Remove_ClearsMembers);
        yield return Run(nameof(Registry_Get_ReturnsNullForUnknown),         Registry_Get_ReturnsNullForUnknown);
        yield return Run(nameof(Registry_SerializeRoundtrip),                Registry_SerializeRoundtrip);

        // MarshalSelector
        yield return Run(nameof(Marshal_SelectsBestCandidateByPersonality),  Marshal_SelectsBestCandidateByPersonality);
        yield return Run(nameof(Marshal_SkipsIfArmyAlreadyExists),           Marshal_SkipsIfArmyAlreadyExists);
        yield return Run(nameof(Marshal_SkipsIfNoWarObjectives),             Marshal_SkipsIfNoWarObjectives);
        yield return Run(nameof(Marshal_PushesNewsOnCreate),                 Marshal_PushesNewsOnCreate);

        // ArmyTickProcessor
        yield return Run(nameof(Tick_FormingCollectsNearbyLords),            Tick_FormingCollectsNearbyLords);
        yield return Run(nameof(Tick_TransitionsToMarchingAfter3Days),       Tick_TransitionsToMarchingAfter3Days);
        yield return Run(nameof(Tick_DisbandsWhenMarshalDead),               Tick_DisbandsWhenMarshalDead);
        yield return Run(nameof(Tick_DisbandsWhenTargetCaptured),            Tick_DisbandsWhenTargetCaptured);
        yield return Run(nameof(Tick_DisbandsWhenWarEnded),                  Tick_DisbandsWhenWarEnded);
        yield return Run(nameof(Tick_PushesNewsOnDisband),                   Tick_PushesNewsOnDisband);
        yield return Run(nameof(Tick_PushesNewsOnMarch),                     Tick_PushesNewsOnMarch);

        // WarBattleJoinService — ArmyJoin 类型
        yield return Run(nameof(JoinService_DetectsArmyJoin_WhenPlayerNear), JoinService_DetectsArmyJoin_WhenPlayerNear);
        yield return Run(nameof(JoinService_SkipsArmyJoin_WhenPlayerFar),    JoinService_SkipsArmyJoin_WhenPlayerFar);
        yield return Run(nameof(JoinService_SkipsArmyJoin_WhenNotSameFaction), JoinService_SkipsArmyJoin_WhenNotSameFaction);

        // Marching 抖动修复 — Forming 招集失败 + cooldown
        yield return Run(nameof(Tick_FormingTimeoutSolo_DisbandsAndCooldown), Tick_FormingTimeoutSolo_DisbandsAndCooldown);
        yield return Run(nameof(Tick_FormingWithMembers_TransitionsToMarching), Tick_FormingWithMembers_TransitionsToMarching);
        yield return Run(nameof(MarshalSelector_SkipsLordsInCooldown), MarshalSelector_SkipsLordsInCooldown);
        yield return Run(nameof(MarshalSelector_NoLoopThrash_Over10Days), MarshalSelector_NoLoopThrash_Over10Days);
    }

    // ─── 工具方法 ─────────────────────────────────────────────────────────────

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    private static OverworldEntity MakeLord(string name, string faction, Vector2 pos,
        OverworldPOI.LordPersonality personality = OverworldPOI.LordPersonality.Balanced,
        float power = 100f)
    {
        return new OverworldEntity
        {
            EntityName          = name,
            EntityTypeEnum      = OverworldEntity.EntityType.LordArmy,
            Faction             = faction,
            Position            = pos,
            HomePosition        = pos,
            CombatPower         = power,
            GarrisonSize        = 30,
            IsAlive             = true,
            VisionRange         = 400.0f,
            LordPersonalityValue = personality,
            Lod                 = OverworldEntity.EntityLod.Active,
        };
    }

    private static OverworldPOI MakePoi(string name, string faction, Vector2 pos)
    {
        return new OverworldPOI
        {
            PoiName       = name,
            PoiTypeEnum   = OverworldPOI.POIType.Town,
            OwningFaction = faction,
            Position      = pos,
            Prosperity    = 50,
            GarrisonMax   = 40,
            GarrisonCurrent = 40,
        };
    }

    // ─── ArmyRegistry ────────────────────────────────────────────────────────

    private static (bool, string) Registry_Create_AssignsMarshalArmyId()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("L1", "na", new Vector2(0, 0));

        var army = registry.Create(marshal, "poi_b", currentDay: 1);

        if (army == null)                        return (false, "Create 应返回非 null Army");
        if (string.IsNullOrEmpty(army.ArmyId))  return (false, "ArmyId 应非空");
        if (marshal.ArmyId != army.ArmyId)      return (false, "元帅 ArmyId 未同步");
        if (!marshal.IsMarshal)                  return (false, "元帅 IsMarshal 应为 true");
        if (!army.Members.Contains(marshal))     return (false, "元帅应在 Members 中");
        return (true, "");
    }

    private static (bool, string) Registry_Remove_ClearsMembers()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("L1", "na", Vector2.Zero);
        var member   = MakeLord("L2", "na", new Vector2(100, 0));
        var army     = registry.Create(marshal, "poi", 1)!;
        army.Members.Add(member);
        member.ArmyId = army.ArmyId;

        registry.Remove(army.ArmyId);

        if (marshal.ArmyId != "")   return (false, $"元帅 ArmyId 应被清空,得 {marshal.ArmyId}");
        if (marshal.IsMarshal)       return (false, "元帅 IsMarshal 应被重置为 false");
        if (member.ArmyId != "")    return (false, $"成员 ArmyId 应被清空,得 {member.ArmyId}");
        if (registry.Get(army.ArmyId) != null) return (false, "Remove 后 Get 应返回 null");
        return (true, "");
    }

    private static (bool, string) Registry_Get_ReturnsNullForUnknown()
    {
        var registry = new ArmyRegistry();
        if (registry.Get("nonexistent") != null)
            return (false, "未知 ID 应返回 null");
        return (true, "");
    }

    private static (bool, string) Registry_SerializeRoundtrip()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M1", "nb", new Vector2(300, 200));
        var army     = registry.Create(marshal, "castle_x", 5)!;
        army.State   = ArmyState.Marching;

        var data     = registry.Serialize();
        var registry2 = new ArmyRegistry();
        var allLords = new List<OverworldEntity> { marshal };
        registry2.Deserialize(data, allLords);

        var restored = registry2.Get(army.ArmyId);
        if (restored == null)               return (false, "反序列化后应能通过 ArmyId 找到 Army");
        if (restored.TargetPoiName != "castle_x") return (false, $"TargetPoiName 不一致: {restored.TargetPoiName}");
        if (restored.State != ArmyState.Marching) return (false, $"State 不一致: {restored.State}");
        if (restored.Marshal?.EntityName != "M1") return (false, $"元帅引用未还原: {restored.Marshal?.EntityName}");
        return (true, "");
    }

    // ─── MarshalSelector ─────────────────────────────────────────────────────

    private static (bool, string) Marshal_SelectsBestCandidateByPersonality()
    {
        var engine   = new WorldEventEngine();
        var registry = new ArmyRegistry();

        var war = new WarState { NationA = "na", NationB = "nb" };
        war.ObjectivesA.Add("poi_b");
        engine.ActiveWars.Add(war);

        var cautious    = MakeLord("Cautious", "na", new Vector2(100, 0), OverworldPOI.LordPersonality.Cautious);
        var aggressive  = MakeLord("Aggro",    "na", new Vector2(200, 0), OverworldPOI.LordPersonality.Aggressive, 80f);
        var allLords    = new List<OverworldEntity> { cautious, aggressive };
        var allPois     = new List<OverworldPOI>
        {
            MakePoi("poi_b", "nb", new Vector2(300, 0))
        };

        MarshalSelector.SelectMarshalsForWars(engine, allLords, allPois, registry, 1);

        var army = registry.All().FirstOrDefault();
        if (army == null)                          return (false, "应创建军团");
        if (army.Marshal?.EntityName != "Aggro")   return (false, $"应优先选进攻性格,得 {army.Marshal?.EntityName}");
        return (true, "");
    }

    private static (bool, string) Marshal_SkipsIfArmyAlreadyExists()
    {
        var engine   = new WorldEventEngine();
        var registry = new ArmyRegistry();

        var war = new WarState { NationA = "na", NationB = "nb" };
        war.ObjectivesA.Add("poi_b");
        engine.ActiveWars.Add(war);

        var lord  = MakeLord("L", "na", new Vector2(100, 0));
        var pois  = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(300, 0)) };

        MarshalSelector.SelectMarshalsForWars(engine, new List<OverworldEntity> { lord }, pois, registry, 1);
        MarshalSelector.SelectMarshalsForWars(engine, new List<OverworldEntity> { lord }, pois, registry, 2);

        if (registry.All().Count() != 1)
            return (false, $"重复调用不应重复创建军团,得 {registry.All().Count()} 个");
        return (true, "");
    }

    private static (bool, string) Marshal_SkipsIfNoWarObjectives()
    {
        var engine   = new WorldEventEngine();
        var registry = new ArmyRegistry();

        // 战争无目标
        var war = new WarState { NationA = "na", NationB = "nb" };
        engine.ActiveWars.Add(war);

        var lord = MakeLord("L", "na", Vector2.Zero);
        MarshalSelector.SelectMarshalsForWars(engine, new List<OverworldEntity> { lord },
            new List<OverworldPOI>(), registry, 1);

        if (registry.All().Any())
            return (false, "无战争目标时不应创建军团");
        return (true, "");
    }

    private static (bool, string) Marshal_PushesNewsOnCreate()
    {
        var engine   = new WorldEventEngine();
        var registry = new ArmyRegistry();

        var war = new WarState { NationA = "na", NationB = "nb" };
        war.ObjectivesA.Add("poi_target");
        engine.ActiveWars.Add(war);

        var lord = MakeLord("Hero", "na", new Vector2(100, 0));
        var pois = new List<OverworldPOI> { MakePoi("poi_target", "nb", new Vector2(300, 0)) };

        MarshalSelector.SelectMarshalsForWars(engine, new List<OverworldEntity> { lord }, pois, registry, 1);

        if (!engine.NewsQueue.Any(n => n.Type == "army_formed"))
            return (false, "创建军团应推送 army_formed 新闻");
        return (true, "");
    }

    // ─── ArmyTickProcessor ───────────────────────────────────────────────────

    private static (bool, string) Tick_FormingCollectsNearbyLords()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "na", new Vector2(0, 0));
        var nearby   = MakeLord("N", "na", new Vector2(500, 0)); // 500 < 1200px 阈值
        var far      = MakeLord("F", "na", new Vector2(2000, 0)); // 超出范围

        var army = registry.Create(marshal, "poi_b", 1)!;
        var allLords = new List<OverworldEntity> { marshal, nearby, far };
        var allPois  = new List<OverworldPOI>
        {
            MakePoi("poi_b", "nb", new Vector2(3000, 0))
        };

        ArmyTickProcessor.Tick(registry, allLords, allPois, 1, null);

        if (!army.Members.Contains(nearby)) return (false, "500px 范围内的领主应被吸纳");
        if (army.Members.Contains(far))     return (false, "2000px 超出范围的领主不应被吸纳");
        return (true, "");
    }

    /// <summary>
    /// Forming 阶段招到 ≥ 2 人时,3 天后正常切换 Marching。
    /// 单人 Forming 走另一条路(Tick_FormingTimeoutSolo_DisbandsAndCooldown)。
    /// </summary>
    private static (bool, string) Tick_TransitionsToMarchingAfter3Days()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var nearby   = MakeLord("N", "na", new Vector2(300, 0)); // 1200px 范围内 → 会被招集
        var army     = registry.Create(marshal, "poi_b", currentDay: 1)!;
        var pois     = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };
        var lords    = new List<OverworldEntity> { marshal, nearby };

        // day=1 招集到 nearby
        ArmyTickProcessor.Tick(registry, lords, pois, currentDay: 1, null);
        if (army.State != ArmyState.Forming) return (false, "第 1 天应仍处于 Forming");
        if (army.LivingMemberCount != 2)     return (false, $"应招到 2 人,得 {army.LivingMemberCount}");

        // day=4: 4-1=3 天 ≥ 3 + ≥ 2 人 → 切 Marching
        ArmyTickProcessor.Tick(registry, lords, pois, currentDay: 4, null);
        if (army.State != ArmyState.Marching) return (false, $"第 4 天应切换为 Marching,得 {army.State}");
        return (true, "");
    }

    private static (bool, string) Tick_DisbandsWhenMarshalDead()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var army     = registry.Create(marshal, "poi_b", 1)!;
        var pois     = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };

        marshal.IsAlive = false;
        ArmyTickProcessor.Tick(registry, new List<OverworldEntity> { marshal }, pois, 2, null);

        if (registry.Get(army.ArmyId) != null)
            return (false, "元帅阵亡后军团应被解散");
        return (true, "");
    }

    private static (bool, string) Tick_DisbandsWhenTargetCaptured()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var member   = MakeLord("L2", "na", new Vector2(100, 0));
        var army     = registry.Create(marshal, "poi_b", 1)!;
        army.Members.Add(member);
        army.State   = ArmyState.Besieging;

        // 目标 POI 已被己方占领
        var pois = new List<OverworldPOI> { MakePoi("poi_b", "na", new Vector2(500, 0)) };
        ArmyTickProcessor.Tick(registry, new List<OverworldEntity> { marshal, member }, pois, 5, null);

        if (registry.Get(army.ArmyId) != null)
            return (false, "目标被占领后军团应解散");
        return (true, "");
    }

    private static (bool, string) Tick_DisbandsWhenWarEnded()
    {
        var registry = new ArmyRegistry();
        var engine   = new WorldEventEngine(); // 无活跃战争 → 战争已结束
        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var member   = MakeLord("L2", "na", new Vector2(100, 0));
        var army     = registry.Create(marshal, "poi_b", 1)!;
        army.Members.Add(member);

        var pois = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };
        ArmyTickProcessor.Tick(registry, new List<OverworldEntity> { marshal, member }, pois, 2, engine);

        if (registry.Get(army.ArmyId) != null)
            return (false, "战争已结束时军团应解散");
        return (true, "");
    }

    private static (bool, string) Tick_PushesNewsOnDisband()
    {
        var registry = new ArmyRegistry();
        var engine   = new WorldEventEngine(); // 无战争 → 触发解散
        var marshal  = MakeLord("Hero", "na", Vector2.Zero);
        var member   = MakeLord("L2", "na", new Vector2(100, 0));
        var army     = registry.Create(marshal, "poi_b", 1)!;
        army.Members.Add(member);

        var pois = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };
        ArmyTickProcessor.Tick(registry, new List<OverworldEntity> { marshal, member }, pois, 2, engine);

        if (!engine.NewsQueue.Any(n => n.Type == "army_disbanded"))
            return (false, "解散应推送 army_disbanded 新闻");
        return (true, "");
    }

    private static (bool, string) Tick_PushesNewsOnMarch()
    {
        var registry = new ArmyRegistry();
        var engine   = new WorldEventEngine();
        // 添加战争保持军团存活
        engine.ActiveWars.Add(new WarState { NationA = "na", NationB = "nb" });

        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var nearby   = MakeLord("N", "na", new Vector2(300, 0)); // 招集成员让军团够人
        var army     = registry.Create(marshal, "poi_b", currentDay: 1)!;
        var pois     = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };
        var lords    = new List<OverworldEntity> { marshal, nearby };

        // day=1 先招集到 nearby
        ArmyTickProcessor.Tick(registry, lords, pois, currentDay: 1, engine);
        // day=4 触发行军切换
        ArmyTickProcessor.Tick(registry, lords, pois, currentDay: 4, engine);

        if (!engine.NewsQueue.Any(n => n.Type == "army_marching"))
            return (false, "进入行军应推送 army_marching 新闻");
        return (true, "");
    }

    // ─── WarBattleJoinService — ArmyJoin ────────────────────────────────────

    private static (bool, string) JoinService_DetectsArmyJoin_WhenPlayerNear()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "player_faction", new Vector2(100, 100));
        var army     = registry.Create(marshal, "poi", 1)!;
        army.State   = ArmyState.Forming;

        var result = WarBattleJoinService.Query(
            new Vector2(200, 100), // 距元帅 100px ≤ 400px
            new List<OverworldEntity> { marshal },
            new List<OverworldPOI>(),
            "player_faction",
            registry);

        if (result == null)                        return (false, "应检测到 ArmyJoin");
        if (result.Type != WarBattleType.ArmyJoin) return (false, $"类型应为 ArmyJoin,得 {result.Type}");
        if (result.ArmyRef != army)                return (false, "ArmyRef 应指向正确军团");
        return (true, "");
    }

    private static (bool, string) JoinService_SkipsArmyJoin_WhenPlayerFar()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "player_faction", new Vector2(100, 100));
        var army     = registry.Create(marshal, "poi", 1)!;
        army.State   = ArmyState.Forming;

        var result = WarBattleJoinService.Query(
            new Vector2(5000, 5000), // 远离
            new List<OverworldEntity> { marshal },
            new List<OverworldPOI>(),
            "player_faction",
            registry);

        if (result != null && result.Type == WarBattleType.ArmyJoin)
            return (false, "玩家远离时不应触发 ArmyJoin");
        return (true, "");
    }

    private static (bool, string) JoinService_SkipsArmyJoin_WhenNotSameFaction()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "enemy_faction", new Vector2(100, 100));
        var army     = registry.Create(marshal, "poi", 1)!;
        army.State   = ArmyState.Forming;

        var result = WarBattleJoinService.Query(
            new Vector2(120, 100), // 紧邻元帅
            new List<OverworldEntity> { marshal },
            new List<OverworldPOI>(),
            "player_faction", // 玩家是不同势力
            registry);

        if (result != null && result.Type == WarBattleType.ArmyJoin)
            return (false, "非同势力军团不应触发 ArmyJoin");
        return (true, "");
    }

    // ─── 抖动修复:Forming 招集失败 + cooldown ────────────────────────────────

    /// <summary>
    /// Forming 阶段超 3 天仍只有元帅 1 人 → 解散军团 + 元帅进入 cooldown
    /// </summary>
    private static (bool, string) Tick_FormingTimeoutSolo_DisbandsAndCooldown()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var army     = registry.Create(marshal, "poi_b", currentDay: 1)!;
        var pois     = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };
        var lonely   = new List<OverworldEntity> { marshal }; // 没有其他领主能加入

        // day=1 创建,day=4(超 3 天)仍 1 人 → 应解散 + cooldown
        ArmyTickProcessor.Tick(registry, lonely, pois, currentDay: 4, null);

        if (registry.Get(army.ArmyId) != null)
            return (false, "Forming 超时无人加入,军团应被解散");
        if (marshal.ArmyId != "")
            return (false, $"解散后 Marshal.ArmyId 应清空,得 {marshal.ArmyId}");
        if (marshal.IsMarshal)
            return (false, "解散后 IsMarshal 应清为 false");
        if (marshal.MarshalCooldownUntilDay <= 4)
            return (false, $"应进入 cooldown,期望 > 4,得 {marshal.MarshalCooldownUntilDay}");
        return (true, "");
    }

    /// <summary>
    /// Forming 阶段招到 ≥ 2 人 → 3 天后正常切 Marching,不进 cooldown
    /// </summary>
    private static (bool, string) Tick_FormingWithMembers_TransitionsToMarching()
    {
        var registry = new ArmyRegistry();
        var marshal  = MakeLord("M", "na", Vector2.Zero);
        var nearby   = MakeLord("N", "na", new Vector2(300, 0));
        var army     = registry.Create(marshal, "poi_b", currentDay: 1)!;
        var pois     = new List<OverworldPOI> { MakePoi("poi_b", "nb", new Vector2(500, 0)) };
        var lords    = new List<OverworldEntity> { marshal, nearby };

        // day=2 招集到 nearby
        ArmyTickProcessor.Tick(registry, lords, pois, currentDay: 2, null);
        if (army.LivingMemberCount != 2) return (false, $"应招到 2 人,得 {army.LivingMemberCount}");

        // day=4 切 Marching
        ArmyTickProcessor.Tick(registry, lords, pois, currentDay: 4, null);
        if (army.State != ArmyState.Marching)
            return (false, $"应切 Marching,得 {army.State}");
        if (marshal.MarshalCooldownUntilDay > 4)
            return (false, "招集成功不应进入 cooldown");
        return (true, "");
    }

    /// <summary>
    /// MarshalSelector 应跳过 cooldown 期内的领主
    /// </summary>
    private static (bool, string) MarshalSelector_SkipsLordsInCooldown()
    {
        var engine = new WorldEventEngine();
        engine.ActiveWars.Add(new WarState
        {
            NationA = "na",
            NationB = "nb",
            ObjectivesA = new List<string> { "target_b" }
        });
        var registry = new ArmyRegistry();

        var lord = MakeLord("L", "na", new Vector2(0, 0));
        lord.MarshalCooldownUntilDay = 10; // 设 cooldown 至 day 10
        var target = MakePoi("target_b", "nb", new Vector2(300, 0));

        // day=5,在 cooldown 内
        MarshalSelector.SelectMarshalsForWars(engine, new List<OverworldEntity> { lord }, new List<OverworldPOI> { target }, registry, currentDay: 5);
        if (registry.All().Any())
            return (false, "cooldown 期内不应被选为元帅");

        // day=11,cooldown 已过
        MarshalSelector.SelectMarshalsForWars(engine, new List<OverworldEntity> { lord }, new List<OverworldPOI> { target }, registry, currentDay: 11);
        if (!registry.All().Any())
            return (false, "cooldown 过后应可被选为元帅");
        return (true, "");
    }

    /// <summary>
    /// 集成测试:孤立元帅场景下,10 天内应稳定(无 Create-Disband 抖动)
    /// 这是修复抖动 bug 的核心验证。
    /// </summary>
    private static (bool, string) MarshalSelector_NoLoopThrash_Over10Days()
    {
        var engine = new WorldEventEngine();
        engine.ActiveWars.Add(new WarState
        {
            NationA = "na",
            NationB = "nb",
            ObjectivesA = new List<string> { "target_b" }
        });
        var registry = new ArmyRegistry();

        // 孤立元帅:1500px 内只有他一个
        var lord = MakeLord("LonelyLord", "na", new Vector2(0, 0));
        var target = MakePoi("target_b", "nb", new Vector2(300, 0));
        var lords = new List<OverworldEntity> { lord };
        var pois = new List<OverworldPOI> { target };

        int createCount = 0;
        // 10 天:正常应该是 1 次 Create + 1 次 Disband + 7 天 cooldown
        for (int day = 1; day <= 10; day++)
        {
            int beforeCreate = registry.All().Count();
            MarshalSelector.SelectMarshalsForWars(engine, lords, pois, registry, day);
            int afterCreate = registry.All().Count();
            if (afterCreate > beforeCreate) createCount++;
            ArmyTickProcessor.Tick(registry, lords, pois, day, null);
        }

        // 修复后:应只 Create 一次(Day 1),Day 4 解散并设 cooldown 7 天到 Day 11,期间不再 Create
        if (createCount > 2)
            return (false, $"10 天内 Create 次数应 ≤ 2(本应 1 次),实际 {createCount} 次,说明仍在抖动");
        return (true, "");
    }
}
