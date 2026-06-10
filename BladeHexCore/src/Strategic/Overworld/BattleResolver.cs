// BattleResolver.cs
// 战斗结算处理器 — 处理实体间交互和AI战斗
// 从 OverworldEntityManager 拆出的 Core 层组件
//
// 交战机制:
//   1. 接触交战(ENGAGE_DIST): 实体物理接触 → 双方进入 Engaged 状态、停止移动
//   2. 动态战斗时长: 根据双方规模计算 3~24 小时
//   3. 分层更新频率: 玩家视野内 3h / 视野外 12h / chunk 外休眠一次性结算
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Army;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗结算处理器 — 管理实体间接触交战和时间战斗结算
/// </summary>
public class BattleResolver
{
    /// <summary>交战触发距离 — 实体物理接触时进入交战状态</summary>
    private const float ENGAGE_DIST = 100.0f;

    /// <summary>战斗最短持续小时数</summary>
    private const int MIN_COMBAT_HOURS = 3;
    /// <summary>战斗最长持续小时数</summary>
    private const int MAX_COMBAT_HOURS = 24;

    /// <summary>玩家视野内更新频率（游戏小时）</summary>
    public float ViewportUpdateInterval = 3.0f;
    /// <summary>玩家视野外更新频率（游戏小时）</summary>
    public float OutsideViewportUpdateInterval = 12.0f;
    /// <summary>玩家视野判定半径（像素）</summary>
    public float ViewportRadius = 1500.0f;

    /// <summary>多方战场注册表 — 管理所有正在进行的多方交战</summary>
    public List<Battlefield> Battlefields { get; set; } = new();

    private ArmyRegistry? _armyRegistry;

    /// <summary>
    /// 战斗结算事件 — 当 AiBattleOccurred 时触发，供 OverworldSimulation 收集为结构化事件
    /// </summary>
    public event Action<OverworldEntity, OverworldEntity, bool>? CombatResolved;

    public void SetArmyRegistry(ArmyRegistry registry)
    {
        _armyRegistry = registry;
    }

    private BladeHex.Strategic.Hero.HeroRegistry? _heroRegistry;
    private BladeHex.Strategic.Hero.PrisonerLedger? _prisonerLedger;
    private BladeHex.Strategic.Hero.HeroRelationMatrix? _relationMatrix;
    private List<OverworldPOI> _pois = new();

    public void SetHeroNetwork(
        BladeHex.Strategic.Hero.HeroRegistry heroes, 
        BladeHex.Strategic.Hero.PrisonerLedger ledger, 
        BladeHex.Strategic.Hero.HeroRelationMatrix relations)
    {
        _heroRegistry = heroes;
        _prisonerLedger = ledger;
        _relationMatrix = relations;
    }

    public void SetPois(List<OverworldPOI> pois)
    {
        _pois = pois;
    }

    private void ResolveEntityDefeat(OverworldEntity loser, OverworldEntity winner, WorldEventEngine? engine)
    {
        BladeHex.Strategic.Hero.HeroDefeatResolver.Resolve(
            loser, winner, engine, _heroRegistry, _prisonerLedger, _relationMatrix, _pois);
    }

    // ================================================================
    // 战斗时长动态计算
    // ================================================================

    /// <summary>
    /// 根据双方规模计算战斗持续小时数。
    /// 公式: clamp(max(双方PartySize) / 3 + 2, 3, 24)
    ///   - 6人冲突 → 4h   - 15人冲突 → 7h   - 60人冲突 → 22h
    /// </summary>
    public static int CalculateCombatDuration(OverworldEntity a, OverworldEntity b)
    {
        int maxPartySize = System.Math.Max(
            System.Math.Max(1, a.PartySize),
            System.Math.Max(1, b.PartySize));
        int hours = maxPartySize / 3 + 2;
        return System.Math.Clamp(hours, MIN_COMBAT_HOURS, MAX_COMBAT_HOURS);
    }

    // ================================================================
    // 阶段 1: 接触检测 → 交战状态设定 (每 daily tick 调用)
    // ================================================================

    /// <summary>
    /// 检测实体间接触并建立交战关系。
    /// 远距感知追逃由 DailyDecisionProcessor/PerceptionIntentResolver 统一处理。
    /// </summary>
    public void ProcessEntityInteractions(List<OverworldEntity> entities, WorldEventEngine? engine = null, Vector2? playerPosition = null, EntitySpatialIndex? index = null, float currentGameHour = 0f)
    {
        void ProcessPair(OverworldEntity a, OverworldEntity b)
        {
            float dist = a.Position.DistanceTo(b.Position);

            if (dist < ENGAGE_DIST)
                CheckEngagement(a, b, engine, currentGameHour);
        }

        if (index != null)
        {
            var entityOrder = new Dictionary<OverworldEntity, int>();
            for (int i = 0; i < entities.Count; i++)
                entityOrder[entities[i]] = i;
            var processedPairs = new HashSet<(int, int)>();

            for (int i = 0; i < entities.Count; i++)
            {
                var a = entities[i];
                if (!a.IsAlive || a.Lod == OverworldEntity.EntityLod.Hibernated) continue;

                foreach (var b in index.QueryRadius(a.Position, ENGAGE_DIST))
                {
                    if (b == a || !b.IsAlive || b.Lod == OverworldEntity.EntityLod.Hibernated) continue;
                    if (!entityOrder.TryGetValue(b, out int bIndex)) continue;
                    int left = System.Math.Min(i, bIndex);
                    int right = System.Math.Max(i, bIndex);
                    if (!processedPairs.Add((left, right))) continue;
                    ProcessPair(a, b);
                }
            }
        }
        else
        {
            for (int i = 0; i < entities.Count; i++)
            {
                var a = entities[i];
                if (!a.IsAlive || a.Lod == OverworldEntity.EntityLod.Hibernated) continue;

                for (int j = i + 1; j < entities.Count; j++)
                {
                    var b = entities[j];
                    if (!b.IsAlive || b.Lod == OverworldEntity.EntityLod.Hibernated) continue;
                    ProcessPair(a, b);
                }
            }
        }
    }

/// <summary>
    /// 接触交战检测 — 敌对实体在 ENGAGE_DIST 内时进入 Engaged 状态
    /// 支持多方：若一方已交战，另一方通过 Battlefield 系统加入
    /// </summary>
    private void CheckEngagement(OverworldEntity a, OverworldEntity b, WorldEventEngine? engine, float currentGameHour)
    {
        if (!OverworldHostility.AreHostile(a, b, engine, _relationMatrix)) return;

        // 双方都在交战状态 → 检查是否在同一个战场
        if (a.CurrentAIState == OverworldEntity.AIState.Engaged && b.CurrentAIState == OverworldEntity.AIState.Engaged)
        {
            // 已在同战场 → 跳过
            if (!string.IsNullOrEmpty(a.BattlefieldId) && a.BattlefieldId == b.BattlefieldId)
                return;

            // 不同战场 → 尝试合并（两者都是 Engaged）
            TryMergeEngagements(a, b, currentGameHour);
            return;
        }

        // 一方已交战 → 另一方加入现有战场
        if (a.CurrentAIState == OverworldEntity.AIState.Engaged)
        {
            JoinBattle(b, a, currentGameHour, engine);
            return;
        }
        if (b.CurrentAIState == OverworldEntity.AIState.Engaged)
        {
            JoinBattle(a, b, currentGameHour, engine);
            return;
        }

        // 双方都未交战 → 创建新战场
        CreateNewBattle(a, b, currentGameHour);
    }

    /// <summary>
    /// 创建新的 1v1 战场。
    /// </summary>
    private void CreateNewBattle(OverworldEntity a, OverworldEntity b, float currentGameHour)
    {
        int duration = CalculateCombatDuration(a, b);

        var bf = new Battlefield
        {
            Position = (a.Position + b.Position) * 0.5f,
            StartedAtHour = currentGameHour,
            DurationHours = duration,
            LastGradualUpdateHour = currentGameHour,
        };

        // 默认先到的实体为攻击方
        bf.Join(a, joinAsAttacker: true);
        bf.Join(b, joinAsAttacker: false);

        Battlefields.Add(bf);
        SyncEntityEngagement(bf, currentGameHour);

        OverworldDiagnostics.LogBattlefieldCreated(bf.BattlefieldId, a.EntityName, b.EntityName, duration);
    }

    /// <summary>
    /// 将实体加入指定方所在的战场。
    /// </summary>
    private void JoinBattle(OverworldEntity joiner, OverworldEntity alreadyEngaged, float currentGameHour, WorldEventEngine? engine)
    {
        var bf = FindBattlefield(alreadyEngaged);
        if (bf == null)
        {
            // 对手有 Engaged 状态但没有 battlefield → 降级为创建新战斗
            CreateNewBattle(joiner, alreadyEngaged, currentGameHour);
            return;
        }

        // 判断加入阵营：joiner 对 alreadyEngaged 是敌对
        // joiner 敌对 alreadyEngaged → 加入 opposite = alreadyEngaged 的对立面
        bool? alreadySide = bf.IsAttacker(alreadyEngaged);
        if (alreadySide == null) return;

        bool joinAsAttacker = DetermineBattleSide(joiner, bf, engine);

        bf.Join(joiner, joinAsAttacker);
        SyncSingleEntityEngagement(joiner, bf, currentGameHour);

        // 更新战场持续时长（取最大值）
        int pairDuration = CalculateCombatDuration(joiner, alreadyEngaged);
        bf.DurationHours = Math.Max(bf.DurationHours, pairDuration);

        OverworldDiagnostics.Log(OverworldDiagnostics.PrefixBattlefield, $"{joiner.EntityName} joined {bf.BattlefieldId} side={(joinAsAttacker ? "attacker" : "defender")}");

        // 合并附近的战场
        TryMergeBattles(bf, currentGameHour);
    }

    public void JoinExistingBattlefield(
        OverworldEntity joiner,
        Battlefield battlefield,
        bool joinAsAttacker,
        WorldEventEngine? engine = null,
        float currentGameHour = 0f)
    {
        if (joiner == null || battlefield == null || !joiner.IsAlive || battlefield.IsResolved)
            return;
        if (battlefield.AllParticipants.Contains(joiner))
            return;

        battlefield.Join(joiner, joinAsAttacker);
        SyncSingleEntityEngagement(joiner, battlefield, currentGameHour);
        battlefield.DurationHours = Math.Max(
            battlefield.DurationHours,
            CalculateCombatDuration(joiner, battlefield.GetPrimaryOpponent(joiner) ?? joiner));

        OverworldDiagnostics.Log(OverworldDiagnostics.PrefixBattlefield,
            $"{joiner.EntityName} joined {battlefield.BattlefieldId} side={(joinAsAttacker ? "attacker" : "defender")}");
        TryMergeBattles(battlefield, currentGameHour);
    }

    /// <summary>
    /// 判断实体应该加入战场的攻击方还是防御方。
    /// 对某方中任意实体敌对 → 加入对立面。
    /// 对双方都敌对 → 加入人数少的一方。
    /// </summary>
    private bool DetermineBattleSide(OverworldEntity entity, Battlefield bf, WorldEventEngine? engine)
    {
        bool hostileToAttacker = false;
        bool hostileToDefender = false;

        foreach (var atk in bf.Attackers)
        {
            if (OverworldHostility.AreHostile(entity, atk, engine, _relationMatrix))
            { hostileToAttacker = true; break; }
        }
        foreach (var def in bf.Defenders)
        {
            if (OverworldHostility.AreHostile(entity, def, engine, _relationMatrix))
            { hostileToDefender = true; break; }
        }

        if (hostileToAttacker && !hostileToDefender)
            return false; // 敌视攻击方 → 加入防御方
        if (!hostileToAttacker && hostileToDefender)
            return true;  // 敌视防御方 → 加入攻击方
        if (hostileToAttacker && hostileToDefender)
            return bf.Defenders.Count <= bf.Attackers.Count; // 都敌视 → 加入弱方

        // 都不敌视（理论上不应发生）→ 默认加入防御方
        return false;
    }

    /// <summary>
    /// 将两个已交战实体的战场合并。
    /// </summary>
    private void TryMergeEngagements(OverworldEntity a, OverworldEntity b, float currentGameHour)
    {
        var bfA = FindBattlefield(a);
        var bfB = FindBattlefield(b);

        if (bfA == null && bfB == null)
        {
            CreateNewBattle(a, b, currentGameHour);
            return;
        }

        if (bfA == null) { JoinBattle(a, b, currentGameHour, null); return; }
        if (bfB == null) { JoinBattle(b, a, currentGameHour, null); return; }

        if (bfA == bfB) return;

        // 合并 bfB → bfA
        foreach (var p in bfB.AllParticipants.ToList())
        {
            bool? side = bfB.IsAttacker(p);
            if (side.HasValue)
            {
                bfA.Join(p, side.Value);
                p.BattlefieldId = bfA.BattlefieldId;
            }
        }
        bfA.DurationHours = Math.Max(bfA.DurationHours, bfB.DurationHours);
        Battlefields.Remove(bfB);
        SyncEntityEngagement(bfA, currentGameHour);

        OverworldDiagnostics.LogBattlefieldMerged(bfB.BattlefieldId, bfA.BattlefieldId);
    }

    /// <summary>
    /// 尝试合并附近的战场到指定战场。
    /// </summary>
    private void TryMergeBattles(Battlefield bf, float currentGameHour)
    {
        foreach (var other in Battlefields.ToList())
        {
            if (other == bf || other.IsResolved) continue;
            float dist = bf.Position.DistanceTo(other.Position);
            if (dist <= (bf.Radius + other.Radius))
            {
                foreach (var p in other.AllParticipants.ToList())
                {
                    bool? side = other.IsAttacker(p);
                    if (side.HasValue)
                    {
                        bf.Join(p, side.Value);
                        p.BattlefieldId = bf.BattlefieldId;
                    }
                }
                bf.DurationHours = Math.Max(bf.DurationHours, other.DurationHours);
                Battlefields.Remove(other);
                SyncEntityEngagement(bf, currentGameHour);
                OverworldDiagnostics.LogBattlefieldMerged(other.BattlefieldId, bf.BattlefieldId);
            }
        }
    }

    /// <summary>
    /// 查找实体所属的战场。
    /// </summary>
    private Battlefield? FindBattlefield(OverworldEntity entity)
    {
        if (string.IsNullOrEmpty(entity.BattlefieldId)) return null;
        return Battlefields.Find(bf => bf.BattlefieldId == entity.BattlefieldId);
    }

    /// <summary>
    /// 将战场状态同步到所有参与实体的 EngagedWith 字段（向下兼容）。
    /// </summary>
    private static void SyncEntityEngagement(Battlefield bf, float currentGameHour)
    {
        foreach (var atk in bf.Attackers)
        {
            SyncSingleEntityEngagement(atk, bf, currentGameHour);
        }
        foreach (var def in bf.Defenders)
        {
            SyncSingleEntityEngagement(def, bf, currentGameHour);
        }
    }

    private static void SyncSingleEntityEngagement(OverworldEntity entity, Battlefield bf, float currentGameHour)
    {
        entity.CurrentAIState = OverworldEntity.AIState.Engaged;
        entity.EngagedWith = bf.GetPrimaryOpponent(entity);
        entity.IsMoving = false;
        entity.Path.Clear();
        entity.ChaseTarget = null;
        entity.EngagedSinceHour = bf.StartedAtHour;
        entity.CombatDurationHours = (int)bf.DurationHours;
        entity.LastGradualUpdateHour = currentGameHour;
    }

    /// <summary>
    /// 清空实体的交战状态，同时从战场中移除。
    /// </summary>
    private void ClearEngagement(OverworldEntity entity, OverworldEntity.AIState nextState = OverworldEntity.AIState.Idle)
    {
        // 从战场移除
        if (!string.IsNullOrEmpty(entity.BattlefieldId))
        {
            var bf = FindBattlefield(entity);
            if (bf != null)
            {
                bf.Remove(entity);
                // 战场空了 → 清除
                if (bf.ParticipantCount == 0)
                {
                    Battlefields.Remove(bf);
                    OverworldDiagnostics.LogBattlefieldCleared(bf.BattlefieldId, "no_participants");
                }
            }
        }

        entity.CurrentAIState = nextState;
        entity.EngagedWith = null;
        entity.BattlefieldId = "";
        entity.EngagedSinceHour = -1f;
        entity.CombatDurationHours = 0;
        entity.LastGradualUpdateHour = -1f;
    }

    // ================================================================
    // 阶段 2: 分层渐进战斗更新 (每帧传递游戏小时)
    // ================================================================

    /// <summary>
    /// 渐进式交战更新 — 根据与玩家的距离决定更新频率。
    /// 视野内: 每 3h 渐进更新（逐步消耗战力）
    /// 视野外: 每 12h 渐进更新
    /// 交战时间耗尽 → 最终结算
    /// 应在每帧时间推进时调用，传入累计游戏小时。
    /// </summary>
    public void UpdateEngagements(
        List<OverworldEntity> entities,
        float currentGameHour,
        WorldEventEngine? engine = null,
        Vector2? playerPosition = null)
    {
        var resolved = new HashSet<OverworldEntity>();

        foreach (var entity in entities)
        {
            if (!entity.IsAlive || entity.CurrentAIState != OverworldEntity.AIState.Engaged) continue;
            if (resolved.Contains(entity)) continue;

            var opponent = entity.EngagedWith;
            if (opponent == null || !opponent.IsAlive)
            {
                // 对手已不存在 → 解除交战
                ClearEngagement(entity);
                continue;
            }

            // 交战时间不足 → 检查是否需要渐进更新
            float elapsed = currentGameHour - entity.EngagedSinceHour;
            float sinceLastUpdate = currentGameHour - entity.LastGradualUpdateHour;

            // 根据距离决定更新频率
            float updateInterval = GetUpdateInterval(entity, playerPosition);

            if (elapsed < entity.CombatDurationHours)
            {
                // 未到结算时间 → 渐进更新
                if (sinceLastUpdate >= updateInterval)
                {
                    ApplyGradualDamage(entity, opponent, elapsed, entity.CombatDurationHours);
                    entity.LastGradualUpdateHour = currentGameHour;
                    if (opponent.CurrentAIState == OverworldEntity.AIState.Engaged)
                        opponent.LastGradualUpdateHour = currentGameHour;
                }
                continue;
            }

            // ── 交战时间耗尽 → 最终结算 ──
            resolved.Add(entity);
            resolved.Add(opponent);
            ResolveFinalCombat(entity, opponent, engine, playerPosition, currentGameHour);
        }
    }

    /// <summary>根据实体与玩家的距离返回更新间隔</summary>
    private float GetUpdateInterval(OverworldEntity entity, Vector2? playerPosition)
    {
        if (!playerPosition.HasValue)
            return OutsideViewportUpdateInterval;

        float dist = entity.Position.DistanceTo(playerPosition.Value);
        return dist <= ViewportRadius ? ViewportUpdateInterval : OutsideViewportUpdateInterval;
    }

    /// <summary>
    /// 渐进伤害 — 每次更新对双方施加小比例消耗。
    /// 战力比越悬殊，弱势方消耗越大。
    /// </summary>
    private static void ApplyGradualDamage(OverworldEntity a, OverworldEntity b, float elapsed, int totalHours)
    {
        float progress = elapsed / totalHours; // 0~1
        float aPower = a.CombatPower * a.PartySize;
        float bPower = b.CombatPower * b.PartySize;
        float totalPower = aPower + bPower;
        if (totalPower <= 0) return;

        // 战力比 — 强势方占比越高，弱势方损失越大
        float aDominance = aPower / totalPower; // 0~1, >0.5 = a 更强
        float bDominance = 1f - aDominance;

        // 每次渐进更新: 基础 3~8% 损耗，弱势方额外 +5%
        float baseLoss = 0.03f + progress * 0.05f; // 随战斗推进增大
        float aLoss = baseLoss + (bDominance > aDominance ? 0.05f : 0f);
        float bLoss = baseLoss + (aDominance > bDominance ? 0.05f : 0f);

        a.CombatPower = System.Math.Max(1f, a.CombatPower * (1f - aLoss));
        b.CombatPower = System.Math.Max(1f, b.CombatPower * (1f - bLoss));
    }

    /// <summary>最终战斗结算 — 交战时间耗尽后调用</summary>
    private void ResolveFinalCombat(
        OverworldEntity entity, OverworldEntity opponent,
        WorldEventEngine? engine, Vector2? playerPosition,
        float currentGameHour)
    {
        var armyA = _armyRegistry?.GetByLord(entity);
        var armyB = _armyRegistry?.GetByLord(opponent);
        float? atkPower = armyA?.AggregateCombatPower;
        float? defPower = armyB?.AggregateCombatPower;

        var result = OverworldAIResolver.ResolveBattle(entity, opponent, atkPower, defPower);

        // 外交关系更新
        if (engine != null)
        {
            BladeHex.Strategic.Diplomacy.CombatResultProcessor.ProcessCombatRelations(
                entity.Faction, opponent.Faction, (bool)result["attacker_won"], engine);
        }

        // 战败处理
        if ((bool)result["attacker_destroyed"]) ResolveEntityDefeat(entity, opponent, engine);
        if ((bool)result["defender_destroyed"]) ResolveEntityDefeat(opponent, entity, engine);

        bool attackerWon = (bool)result["attacker_won"];
        var entityPostState = entity.IsAlive
            ? (attackerWon ? OverworldEntity.AIState.Patrolling : OverworldEntity.AIState.Fleeing)
            : OverworldEntity.AIState.Idle;
        var opponentPostState = opponent.IsAlive
            ? (attackerWon ? OverworldEntity.AIState.Fleeing : OverworldEntity.AIState.Patrolling)
            : OverworldEntity.AIState.Idle;

        // 败者逃跑
        if (!attackerWon && entity.IsAlive)
            entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
        if (attackerWon && opponent.IsAlive)
            opponent.CurrentAIState = OverworldEntity.AIState.Fleeing;

        float duration = currentGameHour - entity.EngagedSinceHour;
        OverworldDiagnostics.LogBattleResolved(
            attackerWon ? entity.EntityName : opponent.EntityName,
            attackerWon ? opponent.EntityName : entity.EntityName,
            duration);

        // 军团损失传播
        PropagateArmyLosses(armyA, entity, attackerWon, (float)result["attacker_losses"], opponent, engine);
        PropagateArmyLosses(armyB, opponent, !attackerWon, (float)result["defender_losses"], entity, engine);

        // 玩家在场影响力
        AwardPlayerInfluence(entity, opponent, result, engine, playerPosition);

        // 清空交战状态
        ClearEngagement(entity, entityPostState);
        ClearEngagement(opponent, opponentPostState);

        // 发射战斗结算事件（供 OverworldSimulation 收集）
        CombatResolved?.Invoke(entity, opponent, attackerWon);

        // 部下大捷通知
        if (engine != null)
        {
            if (attackerWon && entity.Faction == "player" && entity.HeroId != "player")
                engine.AddNews("subparty_victory", $"⚔ 【大捷】你的部下 {entity.EntityName} 剿匪大获全胜，缴获战利品并汇入玩家金库！", entity.Position);
            else if (!attackerWon && opponent.Faction == "player" && opponent.HeroId != "player")
                engine.AddNews("subparty_victory", $"⚔ 【大捷】你的部下 {opponent.EntityName} 成功击退来犯之敌，缴获战利品并汇入玩家金库！", opponent.Position);
        }
}

    // ================================================================
    // 休眠实体一次性结算 (chunk 外 → 唤醒时)
    // ================================================================

    /// <summary>
    /// 休眠实体交战一次性结算 — 在实体从 DormantPool 复用前调用。
    /// 根据交战经过时间和双方规模，简化计算战斗结果。
    /// </summary>
    public void ResolveDormantEngagement(OverworldEntity entity, float currentGameHour, WorldEventEngine? engine = null)
    {
        if (entity.CurrentAIState != OverworldEntity.AIState.Engaged) return;

        float elapsed = currentGameHour - entity.EngagedSinceHour;

        // 交战时间不足 → 解除交战，恢复正常
        if (elapsed < entity.CombatDurationHours || entity.CombatDurationHours <= 0)
        {
            ClearEngagement(entity);
            entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
            return;
        }

        // 时间充足 → 一次性结算 (简化版)
        var opponent = entity.EngagedWith;
        if (opponent != null && opponent.IsAlive)
        {
            // 双方都在 → 正常结算
            ResolveFinalCombat(entity, opponent, engine, null, currentGameHour);
        }
        else
        {
            // 对手不可用 → 简化结算: 根据战力比判定胜负
            float opponentPower = entity.CombatPower * 0.8f; // 假设对手约 80% 战力
            bool entityWins = entity.CombatPower > opponentPower;

            if (entityWins)
            {
                entity.CombatPower *= 0.85f; // 胜者损失 ~15%
                ClearEngagement(entity, OverworldEntity.AIState.Patrolling);
            }
            else
            {
                entity.CombatPower *= 0.4f; // 败者损失 ~60%
                ClearEngagement(entity, OverworldEntity.AIState.Fleeing);
            }
        }
    }

    private void PropagateArmyLosses(Army.Army? army, OverworldEntity participant, bool won, float lossPct, OverworldEntity enemy, WorldEventEngine? engine)
    {
        if (army == null) return;
        float effectiveLoss = won ? lossPct : 0.5f;
        foreach (var m in army.Members.ToList())
        {
            if (m == participant) continue;
            m.CombatPower = System.Math.Max(0.0f, m.CombatPower * (1.0f - effectiveLoss));
            m.GarrisonSize = System.Math.Max(0, (int)(m.GarrisonSize * (1.0f - effectiveLoss)));
            m.PartySize = System.Math.Max(0, (int)(m.PartySize * (1.0f - effectiveLoss)));
            if (m.CombatPower < 1.0f || m.PartySize <= 0) ResolveEntityDefeat(m, enemy, engine);
            else if (!won) m.CurrentAIState = OverworldEntity.AIState.Fleeing;
        }
    }

    private void AwardPlayerInfluence(OverworldEntity a, OverworldEntity b, Godot.Collections.Dictionary result, WorldEventEngine? engine, Vector2? playerPosition)
    {
        if (engine == null || !playerPosition.HasValue || playerPosition.Value.DistanceTo(a.Position) > 600.0f)
            return;

        bool attackerWon = (bool)result["attacker_won"];
        if (attackerWon && b.EntityTypeEnum == OverworldEntity.EntityType.LordArmy && (!b.IsAlive || b.CurrentAIState == OverworldEntity.AIState.Fleeing))
            AwardInfluenceForLordDefeated(b.Faction, engine);
        else if (!attackerWon && a.EntityTypeEnum == OverworldEntity.EntityType.LordArmy && (!a.IsAlive || a.CurrentAIState == OverworldEntity.AIState.Fleeing))
            AwardInfluenceForLordDefeated(a.Faction, engine);
    }

    private void AwardInfluenceForLordDefeated(string enemyFaction, WorldEventEngine engine)
    {
        var nationIds = new HashSet<string>();
        foreach (var key in engine.DiplomaticRelations.Keys)
        {
            var parts = key.Split('|');
            if (parts.Length == 2)
            {
                parts[0] = parts[0].Trim();
                parts[1] = parts[1].Trim();
                nationIds.Add(parts[0]);
                nationIds.Add(parts[1]);
            }
        }
        foreach (var nid in nationIds)
        {
            if (nid != enemyFaction && engine.GetRelation(nid, enemyFaction) <= -30)
            {
                engine.Influence.Add(nid, 5, $"击败了敌对势力 {enemyFaction} 的领主");
            }
        }
    }

}
