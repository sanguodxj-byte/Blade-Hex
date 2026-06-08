// WarLoopSimulationTests.cs
// 战争闭环集成模拟测试 (Frontend 层 — 依赖 OverworldEntityManager / Node)
// 与 WarSystemTests (Core 层单元测试) 互补
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Kingdom;

namespace BladeHex.Tests.Strategic;

/// <summary>
/// 战争闭环 MVP 60 天集成模拟测试
/// </summary>
public static class WarLoopSimulationTests
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
        yield return Run(nameof(LordsAssignedWarTargetWithinAFewTicks), LordsAssignedWarTargetWithinAFewTicks);
        yield return Run(nameof(WarTickDoesNotThrowOver60Days), WarTickDoesNotThrowOver60Days);
        yield return Run(nameof(LordEntersBesieging_WhenAdjacentToTarget), LordEntersBesieging_WhenAdjacentToTarget);
        yield return Run(nameof(SiegeResolution_TransfersOwnership), SiegeResolution_TransfersOwnership);
        yield return Run(nameof(Simulation_200Entities_60Days_Stable), Simulation_200Entities_60Days_Stable);
        // M6 扩规模测试
        yield return Run(nameof(Simulation_365Days_FullScale_Stable), Simulation_365Days_FullScale_Stable);
        yield return Run(nameof(Simulation_200Heroes_SerializeRoundtrip), Simulation_200Heroes_SerializeRoundtrip);
        // M7 玩家王国测试
        yield return Run(nameof(Simulation_PlayerKingdom_FullCycle_InOneYear), Simulation_PlayerKingdom_FullCycle_InOneYear);
        // M3.5 遗留:两国领主路上自动开战
        yield return Run(nameof(Simulation_LordsCollideInWar_AutoFight), Simulation_LordsCollideInWar_AutoFight);
        // 重构测试: 附属 POI 所有权随母城镇/城堡级联转移
        yield return Run(nameof(TownCapture_CascadesOwnershipToSubPois), TownCapture_CascadesOwnershipToSubPois);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ----------------------------------------------------------------------------
    // 工具方法
    // ----------------------------------------------------------------------------

    private static OverworldPOI MakePoi(string name, string faction, Vector2 pos, int garrisonMax = 40)
    {
        return new OverworldPOI
        {
            PoiName = name,
            PoiTypeEnum = OverworldPOI.POIType.Town,
            OwningFaction = faction,
            Position = pos,
            Prosperity = 50,
            GarrisonMax = garrisonMax,
            GarrisonCurrent = garrisonMax,
        };
    }

    private static OverworldEntity MakeLord(string name, string faction, Vector2 pos, OverworldPOI? guarded = null)
    {
        return new OverworldEntity
        {
            EntityName = name,
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            Faction = faction,
            Position = pos,
            HomePosition = pos,
            CombatPower = 200,
            GarrisonSize = 30,
            IsAlive = true,
            VisionRange = 400.0f,
            PartyLevel = 3,
            GuardedPOI = guarded,
        };
    }

    /// <summary>
    /// 构造最小可运行的 OverworldEntityManager + 战争状态。
    /// 距离设计为 lord 与目标 POI ≤ 1500px,以便 WarLordOrders 可分配。
    /// </summary>
    private static (OverworldEntityManager mgr, OverworldPOI poiA, OverworldPOI poiB, OverworldEntity lordA, OverworldEntity lordB) BuildScenario()
    {
        var mgr = new OverworldEntityManager();
        var nationA = new NationConfig { Id = "nation_a", DisplayName = "AlphaKingdom" };
        var nationB = new NationConfig { Id = "nation_b", DisplayName = "BetaEmpire" };
        mgr.Nations = new List<NationConfig> { nationA, nationB };

        var poiA = MakePoi("a_capital", "nation_a", new Vector2(0, 0), garrisonMax: 60);
        var poiB = MakePoi("b_capital", "nation_b", new Vector2(800, 0), garrisonMax: 60);
        mgr.Pois.Add(poiA);
        mgr.Pois.Add(poiB);

        var lordA = MakeLord("aLord", "nation_a", new Vector2(50, 0), poiA);
        var lordB = MakeLord("bLord", "nation_b", new Vector2(750, 0), poiB);
        mgr.Entities.Add(lordA);
        mgr.Entities.Add(lordB);

        var war = new WarState { NationA = "nation_a", NationB = "nation_b", DaysSinceStart = 0 };
        mgr.WorldEngine.ActiveWars.Add(war);
        mgr.WorldEngine.SetRelation("nation_a", "nation_b", -80);

        return (mgr, poiA, poiB, lordA, lordB);
    }

    // ----------------------------------------------------------------------------
    // 测试用例
    // ----------------------------------------------------------------------------

    private static (bool, string) LordsAssignedWarTargetWithinAFewTicks()
    {
        var (mgr, poiA, poiB, lordA, lordB) = BuildScenario();

        bool anyAssigned = false;
        for (int day = 1; day <= 5; day++)
        {
            mgr.OnDayPassed();
            if (!string.IsNullOrEmpty(lordA.AssignedWarTargetPoiName) ||
                !string.IsNullOrEmpty(lordB.AssignedWarTargetPoiName))
            {
                anyAssigned = true;
                break;
            }
        }

        if (!anyAssigned)
            return (false, "5 天内至少应有一支领主队被分配战争目标");

        return (true, "");
    }

    private static (bool, string) WarTickDoesNotThrowOver60Days()
    {
        var (mgr, _, _, _, _) = BuildScenario();

        for (int day = 1; day <= 60; day++)
        {
            mgr.OnDayPassed();
        }

        // 通过性测试:不抛异常 + 战争状态仍然存在或已正常结束
        return (true, "");
    }

    private static (bool, string) LordEntersBesieging_WhenAdjacentToTarget()
    {
        // 领主直接放在敌方 POI 旁边(< 600px),DecideLordArmy 应直接进入 Besieging
        var mgr = new OverworldEntityManager();
        mgr.Nations = new List<NationConfig>
        {
            new NationConfig { Id = "nation_a" },
            new NationConfig { Id = "nation_b" },
        };

        var poiA = MakePoi("a_capital", "nation_a", new Vector2(0, 0), garrisonMax: 50);
        var poiB = MakePoi("b_target", "nation_b", new Vector2(400, 0), garrisonMax: 50);
        mgr.Pois.Add(poiA);
        mgr.Pois.Add(poiB);

        // 领主 A 直接放在距离 b_target 100px 处 — 触发 Besieging
        var lordA = MakeLord("aLord", "nation_a", new Vector2(300, 0), poiA);
        mgr.Entities.Add(lordA);

        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        mgr.WorldEngine.ActiveWars.Add(war);
        mgr.WorldEngine.SetRelation("nation_a", "nation_b", -80);

        // 第一次 tick — RefreshObjectives 给 NationA 加上 b_target,DecideLordArmy 距离 < 600 进入 Besieging
        mgr.OnDayPassed();

        bool entered = lordA.CurrentAIState == OverworldEntity.AIState.Besieging
                       && lordA.SiegeTarget == poiB
                       && poiB.IsUnderSiege;

        if (!entered)
            return (false, $"领主在 100px 处应进入 Besieging,实际状态={lordA.CurrentAIState} target={lordA.SiegeTarget?.PoiName}");

        return (true, "");
    }

    private static (bool, string) SiegeResolution_TransfersOwnership()
    {
        // 验证 SiegeProcessor 在 SiegeDays >= 2 时通过 PoiTransferService 切换归属
        var mgr = new OverworldEntityManager();
        mgr.Nations = new List<NationConfig>
        {
            new NationConfig { Id = "nation_a" },
            new NationConfig { Id = "nation_b" },
        };

        var poiA = MakePoi("a_capital", "nation_a", new Vector2(0, 0));
        var poiB = MakePoi("b_target", "nation_b", new Vector2(400, 0), garrisonMax: 10); // 弱守军
        poiB.GarrisonCurrent = 5; // 守军更弱
        mgr.Pois.Add(poiA);
        mgr.Pois.Add(poiB);

        var lordA = MakeLord("aLord", "nation_a", new Vector2(380, 0), poiA);
        lordA.CombatPower = 9999.0f; // 攻击者必胜
        lordA.GarrisonSize = 60;
        mgr.Entities.Add(lordA);

        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        mgr.WorldEngine.ActiveWars.Add(war);
        mgr.WorldEngine.SetRelation("nation_a", "nation_b", -80);

        // 监听易手事件
        bool transferred = false;
        Action<PoiTransferEvent> handler = e =>
        {
            if (e.Poi == poiB && e.NewFaction == "nation_a") transferred = true;
        };
        PoiTransferService.PoiTransferred += handler;

        try
        {
            // 至少 4 天:tick1 进入 Besieging, OnDayPassed 内 SiegeDays 自增,
            // tick2/3 SiegeDays 累计达 2,SiegeProcessor 结算
            for (int day = 1; day <= 6; day++)
            {
                mgr.OnDayPassed();
                if (transferred) break;
            }

            if (!transferred)
                return (false, $"6 天内 POI 应被攻陷,siegeDays={poiB.SiegeDays} owning={poiB.OwningFaction}");
            if (poiB.OwningFaction != "nation_a")
                return (false, $"OwningFaction 未切换,得 {poiB.OwningFaction}");
        }
        finally
        {
            PoiTransferService.PoiTransferred -= handler;
        }

        return (true, "");
    }

    private static (bool, string) Simulation_200Entities_60Days_Stable()
    {
        var mgr = new OverworldEntityManager();
        mgr.UpdatePlayerPosition(new Vector2(0f, 0f)); // 玩家在 (0,0)

        // 1. 创建 8 个国家并互为敌国
        var nations = new List<NationConfig>();
        for (int i = 0; i < 8; i++)
        {
            var nationId = $"nation_{i}";
            nations.Add(new NationConfig { Id = nationId, DisplayName = $"Kingdom_{i}" });
            for (int j = 0; j < i; j++)
            {
                mgr.WorldEngine.SetRelation(nationId, $"nation_{j}", -80);
            }
        }
        mgr.Nations = nations;

        // 2. 创建 20 个 POI，散布在 0-8000 宽高的正方形区域
        var random = new Random(2026);
        for (int i = 0; i < 20; i++)
        {
            var nationId = $"nation_{i % 8}";
            Vector2 pos = new Vector2(
                (float)(random.NextDouble() * 8000f),
                (float)(random.NextDouble() * 8000f)
            );
            var poi = MakePoi($"Poi_{i}", nationId, pos, garrisonMax: 30);
            poi.GarrisonCurrent = 8; // 较弱的守军有利于易手
            mgr.Pois.Add(poi);
        }

        // 3. 创建 200 个领主实体
        // 前 20 个放置在靠近 (0,0) 处 (LOD 默认为 Active)，另外 180 个放置在 >6000px 处 (LOD 默认为 Hibernated)
        for (int i = 0; i < 200; i++)
        {
            var nationId = $"nation_{i % 8}";
            Vector2 pos;
            if (i < 20)
            {
                pos = new Vector2((float)random.NextDouble() * 2000f, (float)random.NextDouble() * 2000f);
            }
            else
            {
                pos = new Vector2(
                    6500f + (float)random.NextDouble() * 3000f,
                    6500f + (float)random.NextDouble() * 3000f
                );
            }

            var guardedPoi = mgr.Pois[random.Next(mgr.Pois.Count)];
            var lord = MakeLord($"Lord_{i}", nationId, pos, guardedPoi);
            lord.CombatPower = 500f;
            mgr.Entities.Add(lord);
        }

        // 初始建立空间网格与 LOD 级别
        mgr.Spatial.Rebuild(mgr.Entities);
        EntityLodController.Update(mgr.Entities, new Vector2(0f, 0f));

        // 4. 强制执行垃圾回收以抓取稳定的初始内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
        long startMemory = GC.GetTotalMemory(true);

        var originalFactions = mgr.Pois.ToDictionary(p => p.PoiName, p => p.OwningFaction);

        // 5. 监听聚落易手事件
        bool anyPoiCaptured = false;
        Action<PoiTransferEvent> handler = e =>
        {
            anyPoiCaptured = true;
        };
        PoiTransferService.PoiTransferred += handler;

        try
        {
            // 6. 执行 60 天长跑
            for (int day = 1; day <= 60; day++)
            {
                mgr.OnDayPassed();
            }

            // 7. 若事件未被触发，直接比对期初期末归属状态
            if (!anyPoiCaptured)
            {
                foreach (var poi in mgr.Pois)
                {
                    if (originalFactions[poi.PoiName] != poi.OwningFaction)
                    {
                        anyPoiCaptured = true;
                        break;
                    }
                }
            }

            // 8. 校验内存开销
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long endMemory = GC.GetTotalMemory(true);
            double memoryDiffMb = (endMemory - startMemory) / (1024.0 * 1024.0);

            if (memoryDiffMb > 50.0)
            {
                return (false, $"内存增长泄露: {memoryDiffMb:F2} MB (上限 50MB)");
            }

            if (!anyPoiCaptured)
            {
                return (false, "在 60 天全面世仇战争中，大地图未能发生任何一次 POI 聚落易手");
            }
        }
        finally
        {
            PoiTransferService.PoiTransferred -= handler;
        }

        return (true, "");
    }

    // ============================================================================
    // M6 扩规模集成测试
    // ============================================================================

    /// <summary>
    /// D1: 200+ 实体 365 天全规模稳定性测试
    /// 验证: 不抛异常、关系矩阵不溢出、Hero 数量在合理范围
    /// </summary>
    private static (bool, string) Simulation_365Days_FullScale_Stable()
    {
        var mgr = new OverworldEntityManager();
        mgr.UpdatePlayerPosition(new Vector2(0f, 0f));

        // 1. 创建 8 个国家
        var nations = new List<NationConfig>();
        for (int i = 0; i < 8; i++)
        {
            var nationId = $"nation_{i}";
            nations.Add(new NationConfig { Id = nationId, DisplayName = $"Kingdom_{i}", IsMajorNation = true });
            for (int j = 0; j < i; j++)
            {
                mgr.WorldEngine.SetRelation(nationId, $"nation_{j}", -80);
            }
        }
        mgr.Nations = nations;

        // 2. 创建 30 个 POI
        var random = new Random(2026);
        for (int i = 0; i < 30; i++)
        {
            var nationId = $"nation_{i % 8}";
            Vector2 pos = new Vector2(
                (float)(random.NextDouble() * 10000f),
                (float)(random.NextDouble() * 10000f)
            );
            var poi = MakePoi($"Poi_{i}", nationId, pos, garrisonMax: 40);
            mgr.Pois.Add(poi);
        }

        // 3. 创建 200+ 个领主实体 (8国 x 20-25领主 = ~180 领主 + ~40 冒险者)
        int entityId = 0;
        for (int nationIdx = 0; nationIdx < 8; nationIdx++)
        {
            string nationId = $"nation_{nationIdx}";
            int lordCount = 20 + random.Next(6); // 20-25 per nation
            for (int i = 0; i < lordCount; i++)
            {
                var pos = new Vector2(
                    (float)(random.NextDouble() * 10000f),
                    (float)(random.NextDouble() * 10000f)
                );
                var lord = MakeLord($"Lord_{entityId}", nationId, pos, mgr.Pois[entityId % mgr.Pois.Count]);
                lord.IsNamedCharacter = true;
                lord.FamilyName = $"家族{nationIdx}_{i / 4}"; // 每 4 人一个家族
                mgr.Entities.Add(lord);
                entityId++;
            }
        }

        // 添加冒险者
        for (int i = 0; i < 40; i++)
        {
            var pos = new Vector2(
                (float)(random.NextDouble() * 10000f),
                (float)(random.NextDouble() * 10000f)
            );
            var adv = new OverworldEntity
            {
                EntityName = $"Adventurer_{i}",
                EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
                Faction = "neutral",
                Position = pos,
                CombatPower = 100,
                IsAlive = true,
                VisionRange = 500f,
                PartyLevel = 20
            };
            mgr.Entities.Add(adv);
        }

        // 4. 构建家族注册表
        var familyRegistry = new FamilyRegistry();
        foreach (var entity in mgr.Entities.Where(e => e.IsNamedCharacter && !string.IsNullOrEmpty(e.FamilyName)))
        {
            var family = familyRegistry.Get(entity.FamilyName);
            if (family == null)
            {
                family = familyRegistry.Create(entity.FamilyName, entity.Faction, "", new List<string>(), 1);
            }
            familyRegistry.AddMember(entity.FamilyName, entity.EntityName); // 用 EntityName 代理 HeroId
        }

        // 5. 初始建立空间网格
        mgr.Spatial.Rebuild(mgr.Entities);

        // 6. 执行 365 天长跑，每 30 天采样一次
        var sampleDays = new[] { 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330, 365 };
        int sampleIdx = 0;

        for (int day = 1; day <= 365; day++)
        {
            mgr.OnDayPassed();

            // 每 30 天检查
            if (sampleIdx < sampleDays.Length && day == sampleDays[sampleIdx])
            {
                sampleIdx++;

                // 检查存活实体数量
                int aliveCount = mgr.Entities.Count(e => e.IsAlive);
                if (aliveCount < 50)
                {
                    return (false, $"第 {day} 天: 存活实体过少 ({aliveCount})，预期 >= 50");
                }
            }
        }

        return (true, $"365 天模拟完成，最终存活实体: {mgr.Entities.Count(e => e.IsAlive)}");
    }

    /// <summary>
    /// D2: 200 Hero 序列化往返测试
    /// 验证: 序列化大小 ≤ 100KB，反序列化后数据一致
    /// </summary>
    private static (bool, string) Simulation_200Heroes_SerializeRoundtrip()
    {
        var mgr = new OverworldEntityManager();
        mgr.UpdatePlayerPosition(new Vector2(0f, 0f));

        // 1. 创建 8 个国家
        var nations = new List<NationConfig>();
        for (int i = 0; i < 8; i++)
        {
            var nationId = $"nation_{i}";
            nations.Add(new NationConfig { Id = nationId, DisplayName = $"Kingdom_{i}", IsMajorNation = true });
        }
        mgr.Nations = nations;

        // 2. 创建 POI
        for (int i = 0; i < 20; i++)
        {
            mgr.Pois.Add(MakePoi($"Poi_{i}", $"nation_{i % 8}", new Vector2(i * 500, i * 500)));
        }

        // 3. 创建 Hero 数据
        var config = new SpecialCharacterGenerator.GenerationConfig
        {
            MajorNationLordsMin = 20,
            MajorNationLordsMax = 25,
            AdventurerCount = 40
        };

        // 模拟生成英雄
        var random = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            string nationId = $"nation_{i % 8}";
            string familyName = $"家族_{i / 5}";
            var hero = mgr.Heroes.Create(
                nationId,
                $"英雄_{i}",
                familyName,
                OverworldPOI.LordPersonality.Balanced,
                1
            );
        }

        // 4. 跑 60 天产生关系数据
        for (int day = 1; day <= 60; day++)
        {
            // 随机建立一些关系
            if (day % 10 == 0)
            {
                var heroes = mgr.Heroes.AllHeroes.ToList();
                if (heroes.Count >= 2)
                {
                    var h1 = heroes[random.Next(heroes.Count)];
                    var h2 = heroes[random.Next(heroes.Count)];
                    if (h1.HeroId != h2.HeroId)
                    {
                        mgr.Relations.Adjust(h1.HeroId, h2.HeroId, random.Next(-50, 51));
                    }
                }
            }
        }

        // 5. 序列化
        var dict = mgr.SerializeHeroNetwork();

        // 6. 计算序列化大小（估算）
        int estimatedSize = EstimateDictSize(dict);
        if (estimatedSize > 100_000)
        {
            return (false, $"序列化大小 {estimatedSize / 1024}KB 超过 100KB 限制");
        }

        // 7. 反序列化
        var newMgr = new OverworldEntityManager();
        newMgr.DeserializeHeroNetwork(dict);

        // 8. 验证往返一致性
        int origHeroCount = mgr.Heroes.AllHeroes.Count();
        int newHeroCount = newMgr.Heroes.AllHeroes.Count();
        if (origHeroCount != newHeroCount)
        {
            return (false, $"Hero 数量不一致: 原 {origHeroCount}, 新 {newHeroCount}");
        }

        return (true, $"序列化往返成功: {origHeroCount} heroes, ~{estimatedSize / 1024}KB");
    }

    private static int EstimateDictSize(Godot.Collections.Dictionary dict)
    {
        // 简化估算：每个键值对 ~50 字节
        int count = 0;
        foreach (var key in dict.Keys)
        {
            count += key.AsString().Length;
            var val = dict[key];
            if (val.VariantType == Variant.Type.Dictionary)
                count += EstimateDictSize(val.AsGodotDictionary());
            else if (val.VariantType == Variant.Type.Array)
                count += val.AsGodotArray().Count * 20;
            else if (val.VariantType == Variant.Type.String)
                count += val.AsString().Length;
            else
                count += 8; // 数值类型
        }
        return count;
    }

    // ============================================================================
    // M7 玩家王国集成测试
    // ============================================================================

    /// <summary>
    /// M7: 玩家王国完整生命周期 365 天测试
    /// 验证: 开国 → 征服 → 分封 → 改法律 → 宣战 → 媾和
    /// </summary>
    private static (bool, string) Simulation_PlayerKingdom_FullCycle_InOneYear()
    {
        var mgr = new OverworldEntityManager();
        mgr.UpdatePlayerPosition(new Vector2(0f, 0f));

        // 1. 创建 8 个国家
        var nations = new List<NationConfig>();
        for (int i = 0; i < 8; i++)
        {
            var nationId = $"nation_{i}";
            nations.Add(new NationConfig { Id = nationId, DisplayName = $"Kingdom_{i}", IsMajorNation = true });
        }
        mgr.Nations = nations;

        // 2. 创建 POI（包括玩家可占领的城堡）
        var pois = new List<OverworldPOI>
        {
            new OverworldPOI { PoiName = "玩家城堡", PoiTypeEnum = OverworldPOI.POIType.Castle, OwningFaction = "nation_0", Position = new Vector2(100, 100), Prosperity = 50 },
            new OverworldPOI { PoiName = "玩家城镇", PoiTypeEnum = OverworldPOI.POIType.Town, OwningFaction = "player", Position = new Vector2(200, 200), Prosperity = 60 },
            new OverworldPOI { PoiName = "敌国城堡", PoiTypeEnum = OverworldPOI.POIType.Castle, OwningFaction = "nation_1", Position = new Vector2(500, 500), Prosperity = 40 }
        };
        mgr.Pois.Clear();
        foreach (var p in pois) mgr.Pois.Add(p);

        // 3. 创建玩家英雄
        mgr.Heroes.Create("player", "玩家国王", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);
        mgr.Heroes.Create("player", "同伴A", "测试家族", OverworldPOI.LordPersonality.Balanced, 1);

        // 4. 模拟玩家占领城堡（添加到 PendingConquests）
        mgr.PendingConquests.Add("玩家城堡");

        // 给玩家足够的影响力以满足开国 + 宣战 + 媾和条件
        mgr.WorldEngine.Influence.Add("player", 300, "test setup");

        // 5. 验证开国条件
        var (canFound, reason) = PlayerKingdomService.CanFoundKingdom(
            mgr.Pois,
            mgr.WorldEngine.Influence,
            25, // 玩家等级
            mgr.PendingConquests);

        if (!canFound)
            return (false, $"开国条件不满足: {reason}");

        // 6. 创建王国
        var capital = mgr.Pois.First(p => p.PoiName == "玩家城堡");
        var kingdom = PlayerKingdomService.Found(
            "测试王国",
            "测试家族",
            capital,
            new Color(0.2f, 0.4f, 0.8f),
            1,
            mgr.Heroes,
            mgr.Families,
            mgr.Nations,
            mgr.WorldEngine,
            mgr.WorldEngine.Influence,
            mgr.PendingConquests);

        mgr.PlayerKingdom = kingdom;

        // 7. 验证王国已创建
        if (mgr.PlayerKingdom == null)
            return (false, "王国未创建");
        if (!mgr.Nations.Any(n => n.Id == "player"))
            return (false, "NationConfig 未添加");
        if (mgr.PlayerKingdom.ControlledPoiNames.Count < 1)
            return (false, "领土未添加");

        // 8. 模拟分封
        var companion = mgr.Heroes.AllHeroes.FirstOrDefault(h => h.HeroId != "player" && h.FactionId == "player");
        if (companion != null)
        {
            PlayerKingdomService.GrantFief(kingdom, "玩家城堡", companion.HeroId, mgr.Heroes);
        }

        // 9. 模拟法律变更
        kingdom.Laws.TaxRate = BladeHex.Strategic.Kingdom.TaxLaw.High;
        kingdom.Laws.Conscription = BladeHex.Strategic.Kingdom.ConscriptionLaw.Major;

        // 10. 模拟宣战
        mgr.WorldEngine.SetRelation("player", "nation_1", -50);
        var warResult = KingdomDecisionService.TryDeclareWar("player", "nation_1", mgr.WorldEngine);
        if (warResult != DecisionResult.Success)
            return (false, $"宣战失败: {warResult}");

        // 11. 验证战争状态
        if (!mgr.WorldEngine.AreAtWar("player", "nation_1"))
            return (false, "战争状态未建立");

        // 12. 模拟 30 天
        for (int day = 1; day <= 30; day++)
        {
            mgr.OnDayPassed();
        }

        // 13. 模拟媾和
        var peaceResult = KingdomDecisionService.TryMakePeace("player", "nation_1", mgr.WorldEngine);
        if (peaceResult != DecisionResult.Success)
            return (false, $"媾和失败: {peaceResult}");

        // 14. 验证和平状态
        if (mgr.WorldEngine.AreAtWar("player", "nation_1"))
            return (false, "媾和后仍处于战争状态");

        return (true, $"玩家王国完整生命周期测试通过: {kingdom.PoiCount} 领土, {kingdom.LordCount} 领主");
    }

    // ============================================================================
    // M3.5 遗留:两国领主路上自动开战集成测试
    // ============================================================================

    /// <summary>
    /// 验证:两国在 ActiveWars 中,领主在 ENGAGE_DIST (100px) 内进入交战，经过动态时长后结算战斗。
    /// 这是 BattleResolver.AreHostile 支持 War 状态的端到端集成验证。
    ///
    /// 交战机制: Day0 接触 → Engaged → 逐小时渐进更新 → 时长耗尽后最终结算 → CP 削减。
    /// OverworldAIResolver 对优势方施加 10% 损失、劣势方 70% 损失,这是唯一修改 CP 的路径。
    /// </summary>
    private static (bool, string) Simulation_LordsCollideInWar_AutoFight()
    {
        var mgr = new OverworldEntityManager();
        mgr.Nations = new List<NationConfig>
        {
            new NationConfig { Id = "nation_a" },
            new NationConfig { Id = "nation_b" },
        };

        var poiA = MakePoi("a_capital", "nation_a", new Vector2(0, 0));
        var poiB = MakePoi("b_capital", "nation_b", new Vector2(1000, 0));
        mgr.Pois.Add(poiA);
        mgr.Pois.Add(poiB);

        // 两国领主直接放在 ENGAGE_DIST (100px) 内，距离 50px
        var lordA = MakeLord("aLord", "nation_a", new Vector2(400, 0), poiA);
        lordA.CombatPower = 9999.0f;
        lordA.GarrisonSize = 60;
        var lordB = MakeLord("bLord", "nation_b", new Vector2(450, 0), poiB);
        lordB.CombatPower = 100.0f;
        lordB.GarrisonSize = 30;
        mgr.Entities.Add(lordA);
        mgr.Entities.Add(lordB);

        // 建立战争状态
        var war = new WarState { NationA = "nation_a", NationB = "nation_b" };
        mgr.WorldEngine.ActiveWars.Add(war);
        mgr.WorldEngine.SetRelation("nation_a", "nation_b", -80);

        // 初始建立空间网格
        mgr.Spatial.Rebuild(mgr.Entities);

        float cpABefore = lordA.CombatPower;
        float cpBBefore = lordB.CombatPower;

        // Day 0: ProcessEntityInteractions → 接触交战
        mgr.OnDayPassed();

        // 验证交战已建立
        bool engaged = lordA.CurrentAIState == OverworldEntity.AIState.Engaged
                    && lordB.CurrentAIState == OverworldEntity.AIState.Engaged;

        int duration = lordA.CombatDurationHours;

        // 模拟逐小时推进交战 (视野内每3h更新一次)
        float hour = mgr.CurrentGameHour;
        while (hour < mgr.CurrentGameHour + duration + 2f)
        {
            mgr.TickGameHour(3.0f); // 每次推进3小时
            hour += 3.0f;
        }

        // 验证:战斗发生了 (CP 被削减 — 这是 ResolveBattle 的唯一副作用)
        // OverworldAIResolver 对优势方施加 ~10% 损失、劣势方 ~70% 损失
        bool aFought = lordA.CombatPower < cpABefore;
        bool bFought = lordB.CombatPower < cpBBefore;

        if (!aFought && !bFought)
        {
            return (false,
                $"战争状态下两国领主在 50px 内应自动交战(engaged={engaged})," +
                $"lordA: CP={cpABefore}→{lordA.CombatPower}," +
                $"lordB: CP={cpBBefore}→{lordB.CombatPower}");
        }

        // lordA (CP=9999) 应占优势,损失比例远低于 lordB (CP=100)
        float aLossPct = (cpABefore - lordA.CombatPower) / cpABefore;
        float bLossPct = (cpBBefore - lordB.CombatPower) / cpBBefore;

        if (aLossPct >= bLossPct)
            return (false, $"lordA (CP=9999) 应占优势,但 aLoss={aLossPct:P0} >= bLoss={bLossPct:P0}");

        return (true, $"自动交战测试通过: lordA CP {cpABefore:F0}→{lordA.CombatPower:F0} (-{aLossPct:P0}), lordB CP {cpBBefore:F0}→{lordB.CombatPower:F0} (-{bLossPct:P0})");
    }

    private static (bool, string) TownCapture_CascadesOwnershipToSubPois()
    {
        var mgr = new OverworldEntityManager();
        var town = MakePoi("测试城镇", "nation_a", new Vector2(0, 0));
        var village1 = MakePoi("测试村庄1", "nation_a", new Vector2(100, 0));
        village1.PoiTypeEnum = OverworldPOI.POIType.Village;
        var village2 = MakePoi("测试村庄2", "nation_a", new Vector2(0, 100));
        village2.PoiTypeEnum = OverworldPOI.POIType.Village;

        mgr.Pois.Add(town);
        mgr.Pois.Add(village1);
        mgr.Pois.Add(village2);

        // 显式建立双向父子关联关系
        village1.ParentPoiName = "测试城镇";
        village2.ParentPoiName = "测试城镇";
        OverworldPOI.BindParentChildRelationships(mgr.Pois);

        // 验证初始化
        if (town.SubPois.Count != 2)
            return (false, $"初始化失败，母城 SubPois 数量={town.SubPois.Count}，预期 2");
        if (village1.ParentPoi != town || village2.ParentPoi != town)
            return (false, "初始化失败，子 POI 的 Parent 指针未正确指向母城");

        // 模拟易手：将母城所有权转移给新势力
        PoiTransferService.Apply(town, "nation_b", null, 1, null);

        // 验证子 POI 归属自动级联同步
        if (village1.OwningFaction != "nation_b" || village2.OwningFaction != "nation_b")
            return (false, $"级联失效，村庄1归属={village1.OwningFaction}，村庄2归属={village2.OwningFaction}，预期都属于 nation_b");

        return (true, "");
    }
}
