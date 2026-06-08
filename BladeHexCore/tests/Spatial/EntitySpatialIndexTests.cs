using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Spatial;

public static class EntitySpatialIndexTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;
        foreach (var (name, run) in EnumerateTests())
        {
            var (ok, msg) = run();
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else    { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string, System.Func<(bool, string)>)> EnumerateTests()
    {
        yield return ("QueryRadius_EmptyIndex_ReturnsEmpty", QueryRadius_EmptyIndex_ReturnsEmpty);
        yield return ("QueryRadius_SingleEntity_HitOrMiss", QueryRadius_SingleEntity_HitOrMiss);
        yield return ("QueryRadius_NoFalseNegative", QueryRadius_NoFalseNegative);
        yield return ("QueryRadius_NoFalsePositive", QueryRadius_NoFalsePositive);
        yield return ("Update_RemovesFromOldCell", Update_RemovesFromOldCell);
        yield return ("Update_SameCell_NoOp", Update_SameCell_NoOp);
        yield return ("CellSize_BoundaryCondition", CellSize_BoundaryCondition);

        // 新增补强验收测试
        yield return ("QueryRect_FiltersEntities", QueryRect_FiltersEntities);
        yield return ("SpatialCount_Matches_IsAliveCount", SpatialCount_Matches_IsAliveCount);
        yield return ("BattleResolver_SpatialIndex_ConsistentWithBruteForce", BattleResolver_SpatialIndex_ConsistentWithBruteForce);
        yield return ("SiegeProcessor_Reinforcement_ConsistentWithBruteForce", SiegeProcessor_Reinforcement_ConsistentWithBruteForce);
#if !CORE_BUILD
        yield return ("GetVisibleEntities_LargeMovement_CacheInvalidated", GetVisibleEntities_LargeMovement_CacheInvalidated);
#endif
        yield return ("Lod_PlayerWalksAway_Hibernated", Lod_PlayerWalksAway_Hibernated);
        yield return ("Lod_Hysteresis_PreventsFlicker", Lod_Hysteresis_PreventsFlicker);
        yield return ("Lod_HibernatedLord_DailyMovement", Lod_HibernatedLord_DailyMovement);
        yield return ("Lod_Wakeup_CorrectPosition", Lod_Wakeup_CorrectPosition);

        // 技术债修复回归测试
        yield return ("QueryRadius_SafeWhenRemovingDuringIteration", QueryRadius_SafeWhenRemovingDuringIteration);
        yield return ("Add_PublicApi_InsertsAndQueryable", Add_PublicApi_InsertsAndQueryable);
    }

    private static (bool, string) QueryRadius_EmptyIndex_ReturnsEmpty()
    {
        var index = new EntitySpatialIndex(800f);
        var result = index.QueryRadius(new Vector2(100f, 100f), 500f).ToList();
        return (result.Count == 0, $"expected empty query result, got {result.Count}");
    }

    private static (bool, string) QueryRadius_SingleEntity_HitOrMiss()
    {
        var index = new EntitySpatialIndex(800f);
        var e = new OverworldEntity { EntityName = "E1", Position = new Vector2(100f, 100f), IsAlive = true };
        
        index.Rebuild(new List<OverworldEntity> { e });

        // Hit
        var hit = index.QueryRadius(new Vector2(100f, 100f), 50f).ToList();
        if (hit.Count != 1 || hit[0] != e)
        {
            return (false, $"Hit failed: expected e, got {hit.Count} elements");
        }

        // Miss
        var miss = index.QueryRadius(new Vector2(500f, 500f), 100f).ToList();
        if (miss.Count != 0)
        {
            return (false, $"Miss failed: expected 0, got {miss.Count} elements");
        }

        return (true, "");
    }

    private static (bool, string) QueryRadius_NoFalseNegative()
    {
        var index = new EntitySpatialIndex(800f);
        var random = new Random(42);
        var entities = new List<OverworldEntity>();

        for (int i = 0; i < 200; i++)
        {
            entities.Add(new OverworldEntity
            {
                EntityName = $"E{i}",
                Position = new Vector2((float)random.NextDouble() * 5000f, (float)random.NextDouble() * 5000f),
                IsAlive = true
            });
        }

        index.Rebuild(entities);

        for (int q = 0; q < 100; q++)
        {
            var center = new Vector2((float)random.NextDouble() * 5000f, (float)random.NextDouble() * 5000f);
            float radius = 100f + (float)random.NextDouble() * 1400f;

            var spatialResult = index.QueryRadius(center, radius).ToHashSet();
            var bruteResult = entities.Where(e => e.Position.DistanceTo(center) <= radius).ToHashSet();

            var missing = bruteResult.Except(spatialResult).ToList();
            if (missing.Count > 0)
            {
                return (false, $"QueryRadius false negative at query {q}: {missing.Count} missing, center={center}, radius={radius}");
            }
        }

        return (true, "");
    }

    private static (bool, string) QueryRadius_NoFalsePositive()
    {
        var index = new EntitySpatialIndex(800f);
        var random = new Random(42);
        var entities = new List<OverworldEntity>();

        for (int i = 0; i < 200; i++)
        {
            entities.Add(new OverworldEntity
            {
                EntityName = $"E{i}",
                Position = new Vector2((float)random.NextDouble() * 5000f, (float)random.NextDouble() * 5000f),
                IsAlive = true
            });
        }

        index.Rebuild(entities);

        for (int q = 0; q < 100; q++)
        {
            var center = new Vector2((float)random.NextDouble() * 5000f, (float)random.NextDouble() * 5000f);
            float radius = 100f + (float)random.NextDouble() * 1400f;

            var spatialResult = index.QueryRadius(center, radius).ToList();

            foreach (var e in spatialResult)
            {
                float dist = e.Position.DistanceTo(center);
                if (dist > radius)
                {
                    return (false, $"QueryRadius false positive at query {q}: entity {e.EntityName} distance {dist} > radius {radius}");
                }
            }
        }

        return (true, "");
    }

    private static (bool, string) Update_RemovesFromOldCell()
    {
        var index = new EntitySpatialIndex(800f);
        var e = new OverworldEntity { EntityName = "UpdateEntity", Position = new Vector2(100f, 100f), IsAlive = true };
        index.Rebuild(new List<OverworldEntity> { e });

        var oldPos = e.Position;
        e.Position = new Vector2(1000f, 1000f);
        index.Update(e, oldPos);

        var oldQuery = index.QueryRadius(new Vector2(100f, 100f), 200f).ToList();
        if (oldQuery.Contains(e))
        {
            return (false, "expected old cell query not to return moved entity");
        }

        var newQuery = index.QueryRadius(new Vector2(1000f, 1000f), 200f).ToList();
        if (!newQuery.Contains(e))
        {
            return (false, "expected new cell query to return moved entity");
        }

        return (true, "");
    }

    private static (bool, string) Update_SameCell_NoOp()
    {
        var index = new EntitySpatialIndex(800f);
        var e = new OverworldEntity { EntityName = "SameCellEntity", Position = new Vector2(100f, 100f), IsAlive = true };
        index.Rebuild(new List<OverworldEntity> { e });

        var oldPos = e.Position;
        e.Position = new Vector2(150f, 150f);
        index.Update(e, oldPos);

        var query = index.QueryRadius(new Vector2(150f, 150f), 100f).ToList();
        if (!query.Contains(e))
        {
            return (false, "expected entity still queryable after intra-cell update");
        }

        return (true, "");
    }

    private static (bool, string) CellSize_BoundaryCondition()
    {
        var index = new EntitySpatialIndex(800f);
        var e = new OverworldEntity { EntityName = "BoundaryEntity", Position = new Vector2(800f, 0f), IsAlive = true };
        index.Rebuild(new List<OverworldEntity> { e });

        var query = index.QueryRadius(new Vector2(0f, 0f), 800f).ToList();
        if (!query.Contains(e))
        {
            return (false, "expected entity on boundary (800f) to be included in radius 800f query");
        }

        var queryMiss = index.QueryRadius(new Vector2(0f, 0f), 799f).ToList();
        if (queryMiss.Contains(e))
        {
            return (false, "expected entity on boundary (800f) to be excluded in radius 799f query");
        }

        return (true, "");
    }

    // ========================================================================
    // 新增补强及 LOD / 缓存验收测试
    // ========================================================================

    private static (bool, string) QueryRect_FiltersEntities()
    {
        var index = new EntitySpatialIndex(800f);
        var e1 = new OverworldEntity { EntityName = "R1", Position = new Vector2(100f, 100f), IsAlive = true };
        var e2 = new OverworldEntity { EntityName = "R2", Position = new Vector2(900f, 900f), IsAlive = true };
        
        index.Rebuild(new List<OverworldEntity> { e1, e2 });

        var query = index.QueryRect(new Vector2(0f, 0f), new Vector2(500f, 500f)).ToList();
        if (query.Count != 1 || query[0] != e1)
        {
            return (false, $"expected e1 in rect, got {query.Count}");
        }

        return (true, "");
    }

    private static (bool, string) SpatialCount_Matches_IsAliveCount()
    {
        var index = new EntitySpatialIndex(800f);
        var e1 = new OverworldEntity { EntityName = "A1", Position = new Vector2(100f, 100f), IsAlive = true };
        var e2 = new OverworldEntity { EntityName = "A2", Position = new Vector2(200f, 200f), IsAlive = false };

        index.Rebuild(new List<OverworldEntity> { e1, e2 });

        var results = index.QueryRadius(new Vector2(150f, 150f), 1000f).ToList();
        if (results.Count != 1 || results[0] != e1)
        {
            return (false, $"expected 1 alive entity, got {results.Count}");
        }

        return (true, "");
    }

    private static (bool, string) BattleResolver_SpatialIndex_ConsistentWithBruteForce()
    {
        var random = new Random(2026);
        var entities = new List<OverworldEntity>();
        
        for (int i = 0; i < 20; i++)
        {
            entities.Add(new OverworldEntity
            {
                EntityName = $" Lord_{i}",
                EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
                Position = new Vector2((float)random.NextDouble() * 1000f, (float)random.NextDouble() * 1000f),
                Faction = i % 2 == 0 ? "alpha" : "beta",
                IsAlive = true,
                VisionRange = 400f
            });
        }

        var index = new EntitySpatialIndex(800f);
        index.Rebuild(entities);

        var resolver = new BattleResolver();
        
        var entitiesBrute = entities.Select(e => new OverworldEntity { Faction = e.Faction, Position = e.Position, IsAlive = e.IsAlive, VisionRange = e.VisionRange }).ToList();
        var entitiesSpatial = entities.Select(e => new OverworldEntity { Faction = e.Faction, Position = e.Position, IsAlive = e.IsAlive, VisionRange = e.VisionRange }).ToList();
        
        resolver.ProcessEntityInteractions(entitiesBrute, null, null, null);
        
        var spatialIndex = new EntitySpatialIndex(800f);
        spatialIndex.Rebuild(entitiesSpatial);
        resolver.ProcessEntityInteractions(entitiesSpatial, null, null, spatialIndex);

        for (int i = 0; i < entities.Count; i++)
        {
            if (entitiesBrute[i].IsAlive != entitiesSpatial[i].IsAlive)
            {
                return (false, $"Survival mismatch at index {i}: brute={entitiesBrute[i].IsAlive}, spatial={entitiesSpatial[i].IsAlive}");
            }
        }

        return (true, "");
    }

    private static (bool, string) SiegeProcessor_Reinforcement_ConsistentWithBruteForce()
    {
        var pois = new List<OverworldPOI> {
            new OverworldPOI { 
                PoiName = "TargetPoi", 
                Position = new Vector2(400f, 400f), 
                OwningFaction = "kingdom", 
                Prosperity = 10, // 降低繁荣度以使 NeedsReinforcement() 成立返回 true
                GarrisonMax = 20, 
                GarrisonCurrent = 5 
            }
        };
        
        var lord = new OverworldEntity {
            EntityName = "ReinforceLord",
            EntityTypeEnum = OverworldEntity.EntityType.LordArmy,
            Faction = "kingdom",
            Position = new Vector2(600f, 600f),
            IsAlive = true,
            CurrentAIState = OverworldEntity.AIState.Idle
        };
        
        var entities = new List<OverworldEntity> { lord };
        var index = new EntitySpatialIndex(800f);
        index.Rebuild(entities);

        var siege = new SiegeProcessor();
        siege.ProcessReinforcementChecks(entities, pois, null!, index);

        if (lord.CurrentAIState != OverworldEntity.AIState.Reinforcing || lord.ReinforceTarget != pois[0])
        {
            return (false, $"Lord should be reinforcing POI, got state={lord.CurrentAIState}");
        }

        return (true, "");
    }

#if !CORE_BUILD
    private static (bool, string) GetVisibleEntities_LargeMovement_CacheInvalidated()
    {
        var mgr = new OverworldEntityManager();
        var e = new OverworldEntity { EntityName = "V1", Position = new Vector2(100f, 100f), IsAlive = true };
        mgr.Entities.Add(e);
        mgr.Spatial.Rebuild(mgr.Entities);

        var list1 = mgr.GetVisibleEntities(new Vector2(100f, 100f), 500f);
        if (list1.Count != 1) return (false, "expected e to be visible");

        var list2 = mgr.GetVisibleEntities(new Vector2(150f, 100f), 500f);
        if (list2 != list1) return (false, "expected cached list to be returned for small movement");

        var list3 = mgr.GetVisibleEntities(new Vector2(400f, 100f), 500f);
        if (list3 == list1) return (false, "expected query cache invalidation on large movement");

        return (true, "");
    }
#endif

    private static (bool, string) Lod_PlayerWalksAway_Hibernated()
    {
        var e = new OverworldEntity { Position = new Vector2(0f, 0f), IsAlive = true, Lod = OverworldEntity.EntityLod.Active };
        var list = new List<OverworldEntity> { e };

        EntityLodController.Update(list, new Vector2(6000f, 0f));

        if (e.Lod != OverworldEntity.EntityLod.Hibernated)
        {
            return (false, $"expected Hibernated when player walks away, got {e.Lod}");
        }

        return (true, "");
    }

    private static (bool, string) Lod_Hysteresis_PreventsFlicker()
    {
        var e = new OverworldEntity { Position = new Vector2(0f, 0f), IsAlive = true, Lod = OverworldEntity.EntityLod.Active };
        var list = new List<OverworldEntity> { e };

        EntityLodController.Update(list, new Vector2(5200f, 0f));
        if (e.Lod != OverworldEntity.EntityLod.Active)
        {
            return (false, $"expected Active at 5200f due to hysteresis, got {e.Lod}");
        }

        EntityLodController.Update(list, new Vector2(5600f, 0f));
        if (e.Lod != OverworldEntity.EntityLod.Hibernated)
        {
            return (false, "expected Hibernated at 5600f");
        }

        EntityLodController.Update(list, new Vector2(5200f, 0f));
        if (e.Lod != OverworldEntity.EntityLod.Hibernated)
        {
            return (false, $"expected Hibernated at 5200f on approach due to hysteresis, got {e.Lod}");
        }

        return (true, "");
    }

    private static (bool, string) Lod_HibernatedLord_DailyMovement()
    {
        var e = new OverworldEntity {
            EntityName = "H1",
            Position = new Vector2(0f, 0f),
            IsAlive = true,
            Lod = OverworldEntity.EntityLod.Hibernated,
            CurrentAIState = OverworldEntity.AIState.MovingToTarget
        };

        var daily = new DailyDecisionProcessor();
        
        // 利用反射绕过复杂的系统引擎直接驱动 StartMoveTo 移动
        var method = typeof(DailyDecisionProcessor).GetMethod("StartMoveTo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null) return (false, "failed to find StartMoveTo method via reflection");
        
        method.Invoke(daily, new object[] { e, new Vector2(1000f, 0f) });

        if (e.Position.X != 600f || e.Position.Y != 0f)
        {
            return (false, $"expected daily distance 600f, got position {e.Position}");
        }

        if (e.Path.Count != 0)
        {
            return (false, "expected path to be empty for hibernated lord");
        }

        return (true, "");
    }

    private static (bool, string) Lod_Wakeup_CorrectPosition()
    {
        var e = new OverworldEntity {
            EntityName = "H1",
            Position = new Vector2(0f, 0f),
            IsAlive = true,
            Lod = OverworldEntity.EntityLod.Hibernated,
            CurrentAIState = OverworldEntity.AIState.MovingToTarget
        };

        var daily = new DailyDecisionProcessor();
        var method = typeof(DailyDecisionProcessor).GetMethod("StartMoveTo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null) return (false, "failed to find StartMoveTo method via reflection");
        
        method.Invoke(daily, new object[] { e, new Vector2(1000f, 0f) });

        var list = new List<OverworldEntity> { e };
        EntityLodController.Update(list, e.Position); // 激活，距离为 0 < 5000

        if (e.Lod != OverworldEntity.EntityLod.Active)
        {
            return (false, $"expected Active lod, got {e.Lod}");
        }

        if (e.Position.X != 600f || e.Position.Y != 0f)
        {
            return (false, $"position modified incorrectly on activation: {e.Position}");
        }

        return (true, "");
    }

    /// <summary>
    /// 回归测试 (技术债修复 #1):QueryRadius 在迭代时被外部 mutate 同一 cell 仍要安全。
    /// 之前实现用 list[i] 索引 + 每次循环检查 i &lt; list.Count,但 yield 期间外部
    /// 调 Remove 把后续元素往前挪一位会跳过实体。
    /// </summary>
    private static (bool, string) QueryRadius_SafeWhenRemovingDuringIteration()
    {
        var index = new EntitySpatialIndex(800f);
        // 5 个实体落到同一 cell
        var entities = new List<OverworldEntity>();
        for (int i = 0; i < 5; i++)
        {
            entities.Add(new OverworldEntity { EntityName = $"E{i}", Position = new Vector2(100f + i, 100f), IsAlive = true });
        }
        index.Rebuild(entities);

        // 在 yield 过程中 Remove 已经返回过的实体,模拟战斗 + 死亡
        int seen = 0;
        foreach (var e in index.QueryRadius(new Vector2(100f, 100f), 500f))
        {
            seen++;
            // 移除当前迭代到的元素 — 旧实现会让下一个元素被跳过
            index.Remove(e, e.Position);
        }

        if (seen != 5)
        {
            return (false, $"expected to enumerate all 5 entities despite mid-iteration Remove, got {seen}");
        }
        return (true, "");
    }

    /// <summary>
    /// 回归测试 (技术债修复 #2):Add 公开 API 替代之前用 fake oldPos 的 Update hack。
    /// </summary>
    private static (bool, string) Add_PublicApi_InsertsAndQueryable()
    {
        var index = new EntitySpatialIndex(800f);
        var e = new OverworldEntity { EntityName = "Newcomer", Position = new Vector2(2000f, 1500f), IsAlive = true };

        index.Add(e);

        var hit = index.QueryRadius(new Vector2(2000f, 1500f), 100f).ToList();
        if (hit.Count != 1 || hit[0] != e)
        {
            return (false, $"expected entity findable after Add, got {hit.Count} hits");
        }

        // Add(null) 与 Add(已死实体) 应静默忽略
        index.Add(null!);
        var dead = new OverworldEntity { Position = Vector2.Zero, IsAlive = false };
        index.Add(dead);
        var deadQuery = index.QueryRadius(Vector2.Zero, 100f).ToList();
        if (deadQuery.Count != 0)
        {
            return (false, $"dead entity should not be added, got {deadQuery.Count}");
        }

        return (true, "");
    }
}
