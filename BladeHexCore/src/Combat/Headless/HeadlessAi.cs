// HeadlessAi.cs
// Lightweight AI behaviours for HeadlessCombatLoop. Mirrors the spirit of
// the Frontend AIStrategyBase subclasses (Reckless / Cautious / Tactical /
// Instinct) but without Node/HexGrid dependencies.
//
// Each behaviour decides, given an attacker and a list of enemies, whether
// to (a) close to melee and swing, (b) stay at range and shoot, or
// (c) kite away. The outer HeadlessCombatLoop just executes the resulting
// action.
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Combat.Headless;

/// <summary>What the AI wants the actor to do this turn.</summary>
public enum HeadlessAction
{
    /// <summary>No valid target / unable to act.</summary>
    Idle,
    /// <summary>Already in range — attack the chosen target.</summary>
    Attack,
    /// <summary>Move closer to the chosen target this turn (no attack).</summary>
    Approach,
    /// <summary>Move away from the closest threat (kite).</summary>
    Kite,
}

/// <summary>One turn's plan for an actor.</summary>
public readonly struct HeadlessTurnPlan
{
    public readonly HeadlessAction Action;
    public readonly BattleUnitModel? Target;
    public readonly Vector2I MoveTo;

    public HeadlessTurnPlan(HeadlessAction action, BattleUnitModel? target, Vector2I moveTo)
    {
        Action = action;
        Target = target;
        MoveTo = moveTo;
    }

    public static readonly HeadlessTurnPlan IdlePlan =
        new(HeadlessAction.Idle, null, Vector2I.Zero);
}

/// <summary>
/// Picks an action for a single unit's turn. Default behaviour mimics the
/// Frontend "Tactical" strategy:
///   - Ranged main hand: stay at max range, kite if enemy adjacent, otherwise
///     hold ground and shoot.
///   - Melee main hand: close gap and attack.
///   - Out of ammo: switch to off-hand if it's a melee weapon (TODO), else
///     just close.
/// </summary>
public static class HeadlessAi
{
    public static HeadlessTurnPlan Plan(
        BattleUnitModel actor,
        BattleSquad self,
        BattleSquad opposing)
    {
        if (actor.Runtime.CurrentHp <= 0) return HeadlessTurnPlan.IdlePlan;
        var target = PickTarget(actor, self, opposing);
        if (target == null) return HeadlessTurnPlan.IdlePlan;

        var actorPos  = self.Positions[actor];
        var targetPos = opposing.Positions[target];
        int dist = HexUtils.AxialDistance(actorPos, targetPos);

        var weapon = actor.GetMainHand() as WeaponData;
        int range = weapon?.RangeCells ?? 1;
        bool isRanged = weapon != null && weapon.IsRanged && !weapon.IsThrowing;

        // 收集所有阻挡格：敌方所有活着的单位的格不可穿越
        var blocked = new HashSet<Vector2I>();
        foreach (var u in opposing.AliveUnits)
            blocked.Add(opposing.Positions[u]);

        // ZoC（控制区）：近战敌方单位的 6 邻格被视为"咬住"区，
        // 进入 ZoC 的格之后无法继续向前移动（敌方单位卡位真正能挡住通道）。
        // 但 ZoC 格本身可以作为终点（贴脸打）。
        // 远程武器（弓 / 弩 / catalyst）的 ZoC 不计；投掷武器算近战 ZoC。
        var enemyZoc = new HashSet<Vector2I>();
        foreach (var u in opposing.AliveUnits)
        {
            var uw = u.GetMainHand() as WeaponData;
            bool meleeUnit = uw == null || !uw.IsRanged || uw.IsThrowing;
            if (!meleeUnit) continue;
            var pos = opposing.Positions[u];
            foreach (var nb in HexNeighbors(pos))
                enemyZoc.Add(nb);
        }
        // 起始格如果已在 ZoC，本回合允许继续移动（脱离 = AoO）；
        // 但进入新 ZoC 格后立即停下。这样实现"贴脸即被咬住"。

        // ── Ranged behaviour ─────────────────────────────────────────────────
        if (isRanged)
        {
            // Out of ammo? Close to melee swing range; engine will fall back.
            if (weapon!.NeedsAmmo && weapon.CurrentAmmo <= 0)
            {
                if (dist <= 1) return new HeadlessTurnPlan(HeadlessAction.Attack, target, actorPos);
                return new HeadlessTurnPlan(HeadlessAction.Approach, target,
                    StepTowardBfs(actorPos, targetPos, actor.GetMoveRange(), 1, blocked, enemyZoc));
            }

            // 敌人能在 1 个回合内追上我吗？若是，则提前风筝。
            // 估算敌方追击：1 AP/格，敌人 MaxAP - 武器 AP - 1（留 1 AP 攻击）能走的格数。
            int enemyMaxAp = target.GetMaxAp();
            var enemyWeapon = target.GetMainHand() as WeaponData;
            int enemyAttackCost = enemyWeapon?.ApCost ?? 4;
            int enemyChaseRange = System.Math.Max(0, enemyMaxAp - enemyAttackCost);

            // 我能跑多远？MaxAP - 攻击 AP（让 1 发出去再跑）能走的格数。
            int myMaxAp = actor.GetMaxAp();
            int myAttackCost = weapon!.ApCost;
            int myKiteBudget = System.Math.Min(actor.GetMoveRange(), System.Math.Max(0, myMaxAp - myAttackCost));

            // 风筝触发：敌人下回合追击范围 + 1 ≥ 我的射程，意味着不跑就被贴脸。
            // 简化：只要敌人 1 回合能追到，并且我跑后能拉开"敌人下回合追不上"的距离，就风筝。
            bool shouldKite = false;
            if (myKiteBudget > 0 && enemyChaseRange >= dist - 1)
            {
                // 跑完后理想距离：dist + myKiteBudget。下回合敌人能追到的最近距离：dist + myKiteBudget - enemyChaseRange。
                // 只要新位置仍在我的射程内（最大 range），就值得风筝。
                int futureDist = dist + myKiteBudget;
                if (futureDist <= range && futureDist > dist)
                    shouldKite = true;
            }

            // In ideal range band, but敌人能贴脸 → 跑+射；否则站着射
            int idealMin = System.Math.Max(2, range - 2);
            int idealMax = range;
            if (dist >= idealMin && dist <= idealMax && !shouldKite)
                return new HeadlessTurnPlan(HeadlessAction.Attack, target, actorPos);

            // 风筝：射一发后跑，或先跑（敌人在射程内但要被贴脸）
            if (shouldKite)
            {
                var kitePos = StepAwayBfs(actorPos, targetPos, myKiteBudget, blocked);
                int newDist = HexUtils.AxialDistance(kitePos, targetPos);
                // 跑后仍在射程内：射；否则只跑
                if (newDist <= range)
                    return new HeadlessTurnPlan(HeadlessAction.Attack, target, kitePos);
                return new HeadlessTurnPlan(HeadlessAction.Kite, target, kitePos);
            }

            // Too close: kite away.
            if (dist < idealMin)
            {
                int moveBudget = actor.GetMoveRange();
                var kitePos = StepAwayBfs(actorPos, targetPos, moveBudget, blocked);
                return new HeadlessTurnPlan(HeadlessAction.Kite, target, kitePos);
            }

            // Too far: close to ideal.
            int closeBudget = actor.GetMoveRange();
            var nextPos = StepTowardBfs(actorPos, targetPos, closeBudget, idealMin, blocked, enemyZoc);
            int newDist2 = HexUtils.AxialDistance(nextPos, targetPos);
            if (newDist2 <= idealMax)
                return new HeadlessTurnPlan(HeadlessAction.Attack, target, nextPos);
            return new HeadlessTurnPlan(HeadlessAction.Approach, target, nextPos);
        }

        // ── Melee behaviour: close to weapon range, swing ────────────────────
        if (dist <= range)
            return new HeadlessTurnPlan(HeadlessAction.Attack, target, actorPos);
        var meleePos = StepTowardBfs(actorPos, targetPos, actor.GetMoveRange(), range, blocked, enemyZoc);
        int meleeDist = HexUtils.AxialDistance(meleePos, targetPos);
        if (meleeDist <= range)
            return new HeadlessTurnPlan(HeadlessAction.Attack, target, meleePos);
        return new HeadlessTurnPlan(HeadlessAction.Approach, target, meleePos);
    }

    // ========================================================================
    // Target selection: nearest living enemy, with optional threat scoring for
    // DEX/INT actors (skip CON front-line, prefer high-threat back-line).
    // ========================================================================

    private static BattleUnitModel? PickTarget(
        BattleUnitModel actor,
        BattleSquad self,
        BattleSquad opposing)
    {
        var fromPos = self.Positions[actor];
        var alive = opposing.AliveUnits.ToList();
        if (alive.Count == 0) return null;

        // 决定 actor 类型：DEX 主或 INT 主优先打高威胁后排；其他打最近
        int actorStr = CombatStats.GetEffectiveStr(actor.Data);
        int actorDex = CombatStats.GetEffectiveDex(actor.Data);
        int actorCon = CombatStats.GetEffectiveCon(actor.Data);
        int actorInt = CombatStats.GetEffectiveInt(actor.Data);
        bool prefersThreat = actorDex >= Math.Max(actorStr, actorCon)
                          || actorInt >= Math.Max(actorStr, actorCon);

        BattleUnitModel? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (var c in alive)
        {
            int d = HexUtils.AxialDistance(fromPos, opposing.Positions[c]);
            double score;
            if (prefersThreat)
            {
                // 威胁分：法师 5、远程/dex 3、近战 str 2、con 系 1
                int threat = ThreatRating(c);
                // 距离惩罚：远的减分，但权重低
                score = threat * 10.0 - d;
            }
            else
            {
                // 默认行为：最近优先
                score = -d;
            }
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    /// <summary>威胁评分：高 INT/DEX 单位威胁高，CON 系威胁低。</summary>
    private static int ThreatRating(BattleUnitModel u)
    {
        // INT 主 = 法师 / 术士
        int str = CombatStats.GetEffectiveStr(u.Data);
        int dex = CombatStats.GetEffectiveDex(u.Data);
        int con = CombatStats.GetEffectiveCon(u.Data);
        int intel = CombatStats.GetEffectiveInt(u.Data);
        if (intel >= Math.Max(Math.Max(str, dex), con)) return 5;
        // DEX 主 = 游侠 / 猎人
        if (dex > str && dex > con) return 3;
        // CON 主 = 守卫 / 重战
        if (con >= Math.Max(str, dex)) return 1;
        // 其他 STR 系
        return 2;
    }

    // ========================================================================
    // Path planning that respects "敌方单位的格不可穿越" (movement passability).
    // BFS over hex grid with budget; cannot enter a cell in `blocked` set.
    // Returns the best reachable cell (closest to target without exceeding
    // budget; respects stopAt distance from target).
    // ========================================================================

    /// <summary>
    /// Move toward <paramref name="to"/> by up to <paramref name="budget"/> steps,
    /// staying at least <paramref name="stopAt"/> hexes from the target.
    /// Pathfinding refuses to enter any cell in <paramref name="blocked"/>
    /// (敌方单位占用的格)。如果完全被堵则返回原地。
    /// 进入 enemyZoc 集合后立即停下（不能继续扩展，模拟"被咬住"）— 起始格本身在 ZoC 不计。
    /// </summary>
    private static Vector2I StepTowardBfs(Vector2I from, Vector2I to, int budget, int stopAt,
        HashSet<Vector2I> blocked, HashSet<Vector2I>? enemyZoc = null)
    {
        if (budget <= 0) return from;
        int initialDist = HexUtils.AxialDistance(from, to);
        if (initialDist <= stopAt) return from;

        // BFS within budget radius
        var visited = new Dictionary<Vector2I, int> { { from, 0 } };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(from);

        Vector2I best = from;
        int bestDist = initialDist;
        bool startInZoc = enemyZoc != null && enemyZoc.Contains(from);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            int curBudget = visited[cur];
            if (curBudget >= budget) continue;

            // ZoC 阻塞：如果当前格在 ZoC 中（且不是起始格），不再向前扩展
            if (enemyZoc != null && cur != from && enemyZoc.Contains(cur))
                continue;

            foreach (var nb in HexNeighbors(cur))
            {
                if (visited.ContainsKey(nb)) continue;
                if (blocked.Contains(nb)) continue;  // 敌方格不可穿越
                visited[nb] = curBudget + 1;
                queue.Enqueue(nb);

                int d = HexUtils.AxialDistance(nb, to);
                // 偏好：尽量接近目标，但不要走得比 stopAt 更近
                if (d >= stopAt && d < bestDist)
                {
                    bestDist = d;
                    best = nb;
                }
                // 如果允许接近到 stopAt，且该步刚好到达 stopAt，也算最佳
                else if (d == stopAt && bestDist > stopAt)
                {
                    bestDist = d;
                    best = nb;
                }
            }
        }
        return best;
    }

    /// <summary>Move directly away from a threat by up to budget steps, respecting blocked cells.</summary>
    private static Vector2I StepAwayBfs(Vector2I from, Vector2I threat, int budget,
        HashSet<Vector2I> blocked)
    {
        if (budget <= 0) return from;
        var visited = new Dictionary<Vector2I, int> { { from, 0 } };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(from);

        Vector2I best = from;
        int bestDist = HexUtils.AxialDistance(from, threat);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            int curBudget = visited[cur];
            if (curBudget >= budget) continue;

            foreach (var nb in HexNeighbors(cur))
            {
                if (visited.ContainsKey(nb)) continue;
                if (blocked.Contains(nb)) continue;
                // 边界限制（避免无限远走）
                if (nb.X < -8 || nb.X > 18 || nb.Y < -8 || nb.Y > 18) continue;
                visited[nb] = curBudget + 1;
                queue.Enqueue(nb);

                int d = HexUtils.AxialDistance(nb, threat);
                if (d > bestDist)
                {
                    bestDist = d;
                    best = nb;
                }
            }
        }
        return best;
    }

    /// <summary>Hex neighbors (axial coords).</summary>
    private static IEnumerable<Vector2I> HexNeighbors(Vector2I c)
    {
        yield return new Vector2I(c.X + 1, c.Y);
        yield return new Vector2I(c.X - 1, c.Y);
        yield return new Vector2I(c.X, c.Y + 1);
        yield return new Vector2I(c.X, c.Y - 1);
        yield return new Vector2I(c.X + 1, c.Y - 1);
        yield return new Vector2I(c.X - 1, c.Y + 1);
    }
}
