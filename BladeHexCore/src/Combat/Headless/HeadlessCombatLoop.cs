// HeadlessCombatLoop.cs
// Pure-C# combat loop for batch simulation, AI tuning, and unit tests.
//
// Design goals
//   - No Godot scene tree, no Node lifetime, no animations.
//   - Deterministic given a seed: ties go to a stable ordering.
//   - Reuses the production combat math via CombatRuleEngine + BattleUnitModel.
//
// Limitations vs. the live CombatScene
//   - No line-of-sight, no high ground, no flanking direction, no charge.
//     (Frontend's CombatResolver layers those features on top of CombatRuleEngine
//     using LineOfSight / FacingSystem, which depend on HexCell Node3D state.)
//   - No spells, no items, no status effects. Each unit just attacks
//     with its main hand. Simulation can already expose damage/HP balance issues
//     under these assumptions; richer features can be folded in incrementally.
//   - Movement is straight-line greedy: each turn a unit closes the gap by up to
//     MoveRange tiles toward its current target.
//   - Two-side turn order: all of side A acts, then all of side B. We do not
//     model individual initiative because TurnManager itself doesn't either.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Combat.State;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat.Headless;

/// <summary>
/// Result of a single headless combat run.
/// </summary>
public sealed class HeadlessCombatResult
{
    /// <summary>Whether the player side won.</summary>
    public bool PlayerVictory;

    /// <summary>How many full player+enemy turn pairs elapsed.</summary>
    public int RoundsElapsed;

    /// <summary>Total damage dealt by player side (HP only, not DR).</summary>
    public long PlayerDamageDealt;

    /// <summary>Total damage dealt by enemy side.</summary>
    public long EnemyDamageDealt;

    /// <summary>Total HP attacks landed by player side.</summary>
    public int PlayerAttacksLanded;

    /// <summary>Total HP attacks landed by enemy side.</summary>
    public int EnemyAttacksLanded;

    /// <summary>Total HP attacks attempted by player side.</summary>
    public int PlayerAttacksAttempted;

    /// <summary>Total HP attacks attempted by enemy side.</summary>
    public int EnemyAttacksAttempted;

    /// <summary>Player units left alive at the end.</summary>
    public int PlayerSurvivors;

    /// <summary>Enemy units left alive at the end.</summary>
    public int EnemySurvivors;

    /// <summary>True if the loop exhausted the round cap before a winner emerged.</summary>
    public bool TimedOut;
}

/// <summary>
/// Two-team turn-based combat loop, pure C#.
/// </summary>
public static class HeadlessCombatLoop
{
    /// <summary>Hard cap to prevent infinite loops if both sides cannot harm each other.</summary>
    public const int DefaultMaxRounds = 100;

    /// <summary>
    /// The battlefield used by all headless battles. Replace via <see cref="UseField"/>
    /// to swap in cover/elevation for specific scenarios. Defaults to flat plains.
    /// </summary>
    public static IBattleField Field { get; private set; } = new FlatBattleField();

    /// <summary>Install a different battlefield (e.g. one with cover or elevation).</summary>
    public static System.IDisposable UseField(IBattleField field)
    {
        var prev = Field;
        Field = field ?? new FlatBattleField();
        return new FieldScope(prev);
    }

    private readonly struct FieldScope : System.IDisposable
    {
        private readonly IBattleField _previous;
        public FieldScope(IBattleField previous) { _previous = previous; }
        public void Dispose() { Field = _previous; }
    }

    /// <summary>
    /// Run a complete battle between <paramref name="player"/> and
    /// <paramref name="enemy"/>. Caller installs a deterministic
    /// <see cref="IRandomSource"/> via <see cref="CombatRandom.Use"/>
    /// before invoking, if reproducibility is required.
    /// </summary>
    /// <summary>
    /// Toggle: enable simple spell-cast AI for INT-favored units. When true,
    /// units with INT &gt; baseline will skip their basic weapon attack and
    /// instead cast a damage spell (3d10 arcane, 6 AP, 12 mana) — this lets
    /// Mage / Sorcerer builds actually contribute in sim. Disabled by default
    /// so historical sim numbers don't change.
    /// </summary>
    public static bool EnableSpells = false;

    /// <summary>Print attack/damage details for the very first battle. Sim diag tool.</summary>
    public static bool DebugFirstBattle = false;
    private static bool _firstBattleStarted = false;
    internal static bool ShouldLog => DebugFirstBattle && _firstBattleStarted;

    /// <summary>
    /// Optional per-roll trace sink. When non-null, every CombatRuleEngine.RollAttack
    /// call in this loop publishes its inputs/outputs here. Used by SimulationHarness
    /// to diagnose advantage/disadvantage prevalence and final hit-chance distribution.
    /// Set to null in production paths to keep the loop allocation-free.
    /// </summary>
    public sealed class AttackTrace
    {
        public bool AttackerIsPlayer;
        public int  AttackBonus;       // post-mastery, post-buff, pre-accuracy mod
        public int  AccuracyMod;       // LOS / cover penalty
        public int  EffectiveAttackBonus => AttackBonus + AccuracyMod;
        public int  TargetAc;
        public bool HasAdvantage;
        public bool HasDisadvantage;
        public int  HitChancePercent;  // as reported by RollAttack
        public int  NaturalRoll;
        public int  TotalAttack;
        public bool IsHit;
        public bool IsCritical;
        public bool IsFumble;
        public bool IsGraze;
    }

    public static System.Action<AttackTrace>? AttackTraceSink;

    public static HeadlessCombatResult Run(
        BattleSquad player,
        BattleSquad enemy,
        int maxRounds = DefaultMaxRounds)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (enemy  == null) throw new ArgumentNullException(nameof(enemy));

        player.LockInitialCount();
        enemy.LockInitialCount();

        // First battle gets debug printing if requested
        if (DebugFirstBattle && !_firstBattleStarted)
        {
            _firstBattleStarted = true;
            Godot.GD.Print("=== HeadlessCombatLoop DEBUG (first battle) ===");
            Godot.GD.Print($"  Player squad: {player.AliveCount} units");
            foreach (var u in player.AliveUnits)
            {
                var w = u.GetMainHand() as WeaponData;
                int armorDr = u.Data.Armor?.DrThreshold ?? 0;
                int armorAp = u.Data.Armor?.CurrentArmorPoints ?? 0;
                Godot.GD.Print($"    {u.Data.UnitName} L{u.Data.Level} HP={u.Runtime.CurrentHp}/{u.GetMaxHp()} AP={u.GetMaxAp()} AC={u.GetEffectiveAc(false, 0)} STR/DEX/CON/INT={u.Data.Str}/{u.Data.Dex}/{u.Data.Con}/{u.Data.Intel} weapon={w?.ItemName ?? "Unarmed"} ({w?.DamageDiceCount ?? 0}d{w?.DamageDiceSides ?? 0} {w?.WeaponDamageType} {w?.ApCost ?? 0}AP) armor=DR{armorDr}/{armorAp}");
            }
            Godot.GD.Print($"  Enemy squad: {enemy.AliveCount} units");
            foreach (var u in enemy.AliveUnits)
            {
                var w = u.GetMainHand() as WeaponData;
                int armorDr = u.Data.Armor?.DrThreshold ?? 0;
                int armorAp = u.Data.Armor?.CurrentArmorPoints ?? 0;
                Godot.GD.Print($"    {u.Data.UnitName} L{u.Data.Level} HP={u.Runtime.CurrentHp}/{u.GetMaxHp()} AP={u.GetMaxAp()} AC={u.GetEffectiveAc(false, 0)} STR/DEX/CON/INT={u.Data.Str}/{u.Data.Dex}/{u.Data.Con}/{u.Data.Intel} weapon={w?.ItemName ?? "Unarmed"} ({w?.DamageDiceCount ?? 0}d{w?.DamageDiceSides ?? 0} {w?.WeaponDamageType} {w?.ApCost ?? 0}AP) armor=DR{armorDr}/{armorAp}");
            }
        }

        _activePlayer = player;
        _activeEnemy  = enemy;
        try
        {
            return RunInternal(player, enemy, maxRounds);
        }
        finally
        {
            _activePlayer = null;
            _activeEnemy  = null;
        }
    }

    private static HeadlessCombatResult RunInternal(BattleSquad player, BattleSquad enemy, int maxRounds)
    {
        var result = new HeadlessCombatResult();
        var sm = new CombatStateMachine();
        sm.StartCombat();

        for (int round = 0; round < maxRounds; round++)
        {
            // Resolve any side that's already empty (e.g. all enemies dead before
            // their first turn this round).
            if (!player.HasAlive || !enemy.HasAlive) break;
            result.RoundsElapsed = round + 1;  // 计入本回合（即使在 player phase 内结束也算 1 回合）

            // Player phase
            ResetSideForTurn(player);
            TakeSideTurn(player, enemy, isPlayer: true, result);
            if (!enemy.HasAlive) break;

            sm.EndCurrentTurn(); // -> EnemyTurn

            // Enemy phase
            ResetSideForTurn(enemy);
            TakeSideTurn(enemy, player, isPlayer: false, result);
            if (!player.HasAlive) break;

            sm.EndCurrentTurn(); // -> PlayerTurn
        }

        result.PlayerSurvivors = player.AliveCount;
        result.EnemySurvivors  = enemy.AliveCount;
        bool playerLeft = player.HasAlive;
        bool enemyLeft  = enemy.HasAlive;

        if (!playerLeft && !enemyLeft)
        {
            // Mutual annihilation: defender (player) loses by convention.
            result.PlayerVictory = false;
        }
        else if (playerLeft && !enemyLeft)
        {
            result.PlayerVictory = true;
        }
        else if (!playerLeft && enemyLeft)
        {
            result.PlayerVictory = false;
        }
        else
        {
            result.PlayerVictory = false;
            result.TimedOut = true;
        }

        sm.EndCombat(result.PlayerVictory);
        return result;
    }

    // ========================================================================
    // Per-turn helpers
    // ========================================================================

    private static void ResetSideForTurn(BattleSquad side)
    {
        foreach (var u in side.AliveUnits)
        {
            u.Runtime.HasMoved = false;
            u.Runtime.HasActed = false;
            u.Runtime.NonSpellSkillUsedThisTurn = false;
            u.Runtime.AooUsedThisTurn = false;
            u.Runtime.WeaponSwitchedThisTurn = false;
            u.Runtime.CurrentAp = u.GetMaxAp();
            u.Runtime.IsRangedWeaponLoaded = true;
            // v0.6 10.0 修订：每回合开始 Mana 恢复 floor(WIS/8)
            int manaRegen = CombatStats.GetManaRegen(u.Data);
            int maxMana = CombatStats.GetMaxMana(u.Data);
            if (manaRegen > 0 && u.Data.CurrentMana < maxMana)
                u.Data.CurrentMana = Math.Min(maxMana, u.Data.CurrentMana + manaRegen);
            // 衰减 sim buff turns
            if (u.Runtime.BuffAttackBonusTurns > 0) u.Runtime.BuffAttackBonusTurns--;
            if (u.Runtime.BuffAcBonusTurns > 0) u.Runtime.BuffAcBonusTurns--;
            if (u.Runtime.DebuffAttackPenaltyTurns > 0) u.Runtime.DebuffAttackPenaltyTurns--;
            if (u.Runtime.DeathblowFocusPendingTurns > 0) u.Runtime.DeathblowFocusPendingTurns--;
            if (u.Runtime.KeystoneRecentCritTurns > 0) u.Runtime.KeystoneRecentCritTurns--;
            SkillTreeKeystoneResolver.ApplyBloodOathTurnStartLoss(u.Data);
            // BuffTempHp 不衰减，受伤吸收完为止
        }
    }

    private static void TakeSideTurn(
        BattleSquad active,
        BattleSquad opposing,
        bool isPlayer,
        HeadlessCombatResult result)
    {
        // Stable order: by NodeId-ish proxy (UnitName + index in list)
        var actors = active.AliveUnits.ToList();
        foreach (var actor in actors)
        {
            if (actor.Runtime.CurrentHp <= 0) continue;
            if (!opposing.HasAlive) break;

            // 多次行动循环 (v0.6 §3.0 AP 系统)：单位每回合 AP 用尽前持续行动。
            // 每次循环重新规划，因为目标可能死亡 / 距离改变 / mana 改变。
            // 设硬上限 6 防止规划 bug 死循环（攻击/法术 AP 通常 3-8，6 次足够覆盖
            // lvl 30+ 单位的 12-16 AP 池）。
            const int MaxActionsPerTurn = 6;
            int actionsTaken = 0;
            int prevApSnapshot = (int)actor.Runtime.CurrentAp;
            while (actionsTaken < MaxActionsPerTurn && actor.Runtime.CurrentHp > 0 && opposing.HasAlive)
            {
                if (!TakeOneAction(actor, active, opposing, isPlayer, result))
                    break;  // No-op turn (idle / blocked path)

                actionsTaken++;
                // 防止规划循环不消耗 AP 死锁：若一次行动后 AP 没下降（如 idle），退出
                int newAp = (int)actor.Runtime.CurrentAp;
                if (newAp >= prevApSnapshot)
                    break;
                prevApSnapshot = newAp;
            }
        }
    }

    /// <summary>
    /// 执行单位的一次"动作"（移动 + 攻击 / 施法 / kite / idle）。
    /// 返回 false 表示无法行动 / 主动 idle，应该结束本 actor 的回合。
    /// </summary>
    private static bool TakeOneAction(
        BattleUnitModel actor, BattleSquad active, BattleSquad opposing,
        bool isPlayer, HeadlessCombatResult result)
    {
        // 武器切换 AI：近战主手够不到、副手投掷能够到 → 切到副手投掷反风筝
        TryAutoSwitchWeapon(actor, active, opposing);

        var plan = HeadlessAi.Plan(actor, active, opposing);
        if (plan.Target == null || plan.Action == HeadlessAction.Idle) return false;

        // Apply movement (if any). Plan already validates distance.
        var currentPos = active.Positions[actor];
        int moveDistance = HexUtils.AxialDistance(currentPos, plan.MoveTo);

        // Spell-cast pre-movement: if INT-leaning, prefer a spell over weapon.
        if (EnableSpells)
        {
            int distNow = HexUtils.AxialDistance(
                currentPos, opposing.Positions[plan.Target]);
            if (distNow <= 13 && TryCastDamageSpell(actor, plan.Target, isPlayer, result, distNow))
                return true;
            int maxOther = Math.Max(Math.Max(Math.Max(actor.Data.Str, actor.Data.Dex),
                                             Math.Max(actor.Data.Con, actor.Data.Wis)),
                                    actor.Data.Cha);
            bool leansInt = actor.Data.Intel >= maxOther - 1;
            if (leansInt && distNow > 6)
            {
                int budget = actor.GetMoveRange();
                int needed = distNow - 5;
                int steps = System.Math.Min(budget, needed);
                var dir = new Vector2I(
                    System.Math.Sign(opposing.Positions[plan.Target].X - currentPos.X),
                    System.Math.Sign(opposing.Positions[plan.Target].Y - currentPos.Y));
                var newPos = new Vector2I(currentPos.X + dir.X * steps, currentPos.Y + dir.Y * steps);
                plan = new HeadlessTurnPlan(HeadlessAction.Approach, plan.Target, newPos);
                moveDistance = HexUtils.AxialDistance(currentPos, newPos);
            }
        }

        bool didSomething = false;

        if (plan.MoveTo != currentPos)
        {
            // 移动消耗 1 AP / 格 (v0.6 §3.2)
            float moveApCost = moveDistance * 1.0f;
            if (actor.Runtime.CurrentAp < moveApCost)
            {
                int affordableSteps = (int)actor.Runtime.CurrentAp;
                if (affordableSteps <= 0) return false;
                if (plan.Action != HeadlessAction.Attack) return false;
            }
            else
            {
                ResolveAoOOnMove(actor, active, opposing, currentPos, plan.MoveTo, !isPlayer, result);
                actor.Runtime.CurrentAp -= moveApCost;
                actor.Runtime.HasMoved = true;
                active.Positions[actor] = plan.MoveTo;
                actor.Runtime.GridPos = plan.MoveTo;
                didSomething = true;

                if (actor.Runtime.CurrentHp <= 0) return true;
            }
        }

        if (plan.Action == HeadlessAction.Attack && actor.Runtime.CurrentHp > 0)
        {
            int weaponRange = GetWeaponRange(actor);
            int dist = HexUtils.AxialDistance(active.Positions[actor], opposing.Positions[plan.Target]);

            if (dist <= weaponRange)
            {
                // v0.6 §3.3 每回合 1 次非 Spell 主动技能：在普攻前尝试用 1 个主动技能
                if (TryUseActiveSkill(actor, plan.Target, active, opposing, isPlayer, result))
                {
                    didSomething = true;
                }
                else
                {
                    bool isCharge = moveDistance >= 3
                        && (actor.GetMainHand() is not WeaponData mw || (!mw.IsRanged && !mw.IsCatalyst));
                    int apBefore = (int)actor.Runtime.CurrentAp;
                    ResolveAttack(actor, plan.Target, isPlayer, result, isCharge);
                    if ((int)actor.Runtime.CurrentAp < apBefore) didSomething = true;
                }
            }
        }

        return didSomething;
    }

    /// <summary>
    /// 主动技能 AI（v0.6 §3.3 每回合 1 次非 Spell 主动技能）。
    /// 检查 actor 持有的技能，按"AP 够 + 触发条件"择优使用 1 个。
    /// 返回 true 表示用了技能（消耗 AP），false 让 caller 走普攻。
    /// </summary>
    private static bool TryUseActiveSkill(
        BattleUnitModel actor, BattleUnitModel target,
        BattleSquad active, BattleSquad opposing,
        bool isPlayer, HeadlessCombatResult result)
    {
        if (actor.Runtime.NonSpellSkillUsedThisTurn) return false;
        var tree = actor.Runtime.SkillTree;
        if (tree == null) return false;

        var actorPos = active.Positions[actor];
        var weapon = actor.GetMainHand() as WeaponData;
        bool isMelee = weapon == null || !weapon.IsRanged;

        // 数邻接敌人（用于 AOE 类技能触发）
        int adjEnemies = 0;
        foreach (var u in opposing.AliveUnits)
        {
            int d = HexUtils.AxialDistance(actorPos, opposing.Positions[u]);
            if (d <= 1) adjEnemies++;
        }
        // 数邻接友军（用于光环 / 治疗 / buff 触发）
        int adjAllies = 0;
        foreach (var u in active.AliveUnits)
        {
            if (u == actor) continue;
            int d = HexUtils.AxialDistance(actorPos, active.Positions[u]);
            if (d <= 1) adjAllies++;
        }

        // ==== CHA 系 buff（开战回合优先）====
        bool isFirstTurn = !actor.Runtime.HasMoved;  // 简化：第一次行动 = 第一回合

        // 英雄号召 (6 AP, 每场 1 次) — 全队 +2 攻击 +1 AC
        if (tree.HasSkillEffect("heroic_call") && actor.Runtime.HeroicCallUsedThisCombat == 0
            && actor.Runtime.CurrentAp >= 6)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.HeroicCallUsedThisCombat = 1;
            actor.Runtime.CurrentAp -= 6;
            foreach (var ally in active.AliveUnits)
            {
                ally.Runtime.BuffAttackBonusTurns = 3;
                ally.Runtime.BuffAttackBonusValue = Math.Max(ally.Runtime.BuffAttackBonusValue, 2);
                ally.Runtime.BuffAcBonusTurns = 3;
                ally.Runtime.BuffAcBonusValue = Math.Max(ally.Runtime.BuffAcBonusValue, 1);
            }
            return true;
        }

        // 集结号令 (4 AP) — 周围 buff 攻击 +2 / 2 回合
        if (isFirstTurn && tree.HasSkillEffect("rally") && actor.Runtime.CurrentAp >= 4
            && adjAllies >= 2)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 4;
            foreach (var ally in active.AliveUnits)
            {
                int d = HexUtils.AxialDistance(actorPos, active.Positions[ally]);
                if (d > 1) continue;
                ally.Runtime.BuffAttackBonusTurns = Math.Max(ally.Runtime.BuffAttackBonusTurns, 2);
                ally.Runtime.BuffAttackBonusValue = Math.Max(ally.Runtime.BuffAttackBonusValue, 2);
            }
            return true;
        }

        // 战斗怒吼 (4 AP) — 周围敌人攻击 -2
        if (isFirstTurn && tree.HasSkillEffect("battle_cry") && actor.Runtime.CurrentAp >= 4
            && adjEnemies >= 2)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 4;
            foreach (var enemy in opposing.AliveUnits)
            {
                int d = HexUtils.AxialDistance(actorPos, opposing.Positions[enemy]);
                if (d > 1) continue;
                enemy.Runtime.DebuffAttackPenaltyTurns = Math.Max(enemy.Runtime.DebuffAttackPenaltyTurns, 2);
                enemy.Runtime.DebuffAttackPenaltyValue = Math.Max(enemy.Runtime.DebuffAttackPenaltyValue, 2);
            }
            return true;
        }

        // 威慑 (4 AP) — 周围敌人攻击 -2 / 3 回合
        if (isFirstTurn && tree.HasSkillEffect("intimidate") && actor.Runtime.CurrentAp >= 4
            && adjEnemies >= 1)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 4;
            foreach (var enemy in opposing.AliveUnits)
            {
                int d = HexUtils.AxialDistance(actorPos, opposing.Positions[enemy]);
                if (d > 1) continue;
                enemy.Runtime.DebuffAttackPenaltyTurns = Math.Max(enemy.Runtime.DebuffAttackPenaltyTurns, 3);
                enemy.Runtime.DebuffAttackPenaltyValue = Math.Max(enemy.Runtime.DebuffAttackPenaltyValue, 2);
            }
            return true;
        }

        // 战吼 (4 AP) — 周围友军攻击 +1 / 2 回合
        if (isFirstTurn && tree.HasSkillEffect("war_cry") && actor.Runtime.CurrentAp >= 4
            && adjAllies >= 1)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 4;
            foreach (var ally in active.AliveUnits)
            {
                int d = HexUtils.AxialDistance(actorPos, active.Positions[ally]);
                if (d > 1) continue;
                ally.Runtime.BuffAttackBonusTurns = Math.Max(ally.Runtime.BuffAttackBonusTurns, 2);
                ally.Runtime.BuffAttackBonusValue = Math.Max(ally.Runtime.BuffAttackBonusValue, 1);
            }
            return true;
        }

        // ==== CON 防御 buff ====
        // 不屈壁垒 (4 AP) — 自身受伤减半 + 临时 HP（HP 低于 50% 触发）
        if (tree.HasSkillEffect("unyielding_bulwark") && actor.Runtime.CurrentAp >= 4
            && actor.CurrentHp < actor.GetMaxHp() / 2)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 4;
            int tempHp = CombatRandom.RollDice(2, 6);
            actor.Runtime.BuffTempHp = Math.Max(actor.Runtime.BuffTempHp, tempHp);
            actor.Runtime.BuffAcBonusTurns = Math.Max(actor.Runtime.BuffAcBonusTurns, 2);
            actor.Runtime.BuffAcBonusValue = Math.Max(actor.Runtime.BuffAcBonusValue, 2);
            return true;
        }

        // 生命之盾 (4 AP, 每场 1 次) — 临时 HP = 30% MaxHP
        if (tree.HasSkillEffect("life_shield") && actor.Runtime.LifeShieldUsedThisCombat == 0
            && actor.Runtime.CurrentAp >= 4 && actor.CurrentHp < actor.GetMaxHp() * 0.7f)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.LifeShieldUsedThisCombat = 1;
            actor.Runtime.CurrentAp -= 4;
            int shieldHp = (int)(actor.GetMaxHp() * 0.3f);
            actor.Runtime.BuffTempHp = Math.Max(actor.Runtime.BuffTempHp, shieldHp);
            return true;
        }

        // ==== 近战 STR 系 ====
        // 旋风斩 (5 AP) — 2+ 邻接敌人时
        if (isMelee && adjEnemies >= 2 && tree.HasSkillEffect("whirlwind") && actor.Runtime.CurrentAp >= 5)
        {
            return ExecuteWhirlwind(actor, active, opposing, isPlayer, result, 5);
        }

        // 剑舞 (7 AP) — 2+ 邻接敌人 + 高 DEX
        if (isMelee && adjEnemies >= 2 && tree.HasSkillEffect("sword_dance") && actor.Runtime.CurrentAp >= 7)
        {
            return ExecuteWhirlwind(actor, active, opposing, isPlayer, result, 7, damageMultiplier: 1.5f);
        }

        // 血腥漩涡 (5 AP) — 2+ 邻接敌人 + HP 不满
        if (isMelee && adjEnemies >= 2 && tree.HasSkillEffect("blood_vortex") && actor.Runtime.CurrentAp >= 5
            && actor.CurrentHp < actor.GetMaxHp())
        {
            bool result_ = ExecuteWhirlwind(actor, active, opposing, isPlayer, result, 5);
            if (result_)
            {
                int healCap = CombatRandom.RollDice(3, 6);
                int healed = 0;
                for (int i = 0; i < adjEnemies && healed < healCap; i++)
                {
                    int gain = Math.Min(CombatRandom.RollDice(1, 6), healCap - healed);
                    actor.Runtime.CurrentHp = Math.Min(actor.GetMaxHp(), actor.Runtime.CurrentHp + gain);
                    actor.CurrentHp = actor.Runtime.CurrentHp;
                    healed += gain;
                }
            }
            return result_;
        }

        // 连击 (4 AP) — 单目标，AP 够打 2 次
        if (isMelee && tree.HasSkillEffect("double_attack") && actor.Runtime.CurrentAp >= 4)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 4;
            ResolveAttack(actor, target, isPlayer, result, isCharge: false);
            if (target.Runtime.CurrentHp > 0)
                ResolveAttack(actor, target, isPlayer, result, isCharge: false, isAoO: false, extraAccuracyMod: -3);
            return true;
        }

        // 盾击 (5 AP) — 持盾 + 单目标
        if (isMelee && tree.HasSkillEffect("shield_bash") && actor.Runtime.CurrentAp >= 5
            && actor.Data.Shield != null)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 5;
            ResolveAttack(actor, target, isPlayer, result, isCharge: false);
            return true;
        }

        // ==== 远程 DEX 系 ====
        // 精准射击 (8 AP) — 远程 + 单目标 + AP 够
        if (!isMelee && tree.HasSkillEffect("aimed_shot") && actor.Runtime.CurrentAp >= 8)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 8;
            ResolveAttack(actor, target, isPlayer, result, isCharge: false, isAoO: false, extraAccuracyMod: 0, damageMultiplier: 2.0f);
            return true;
        }

        // 连珠箭 (6 AP) — 远程 + 3 次射击
        if (!isMelee && tree.HasSkillEffect("multi_shot") && actor.Runtime.CurrentAp >= 6)
        {
            actor.Runtime.NonSpellSkillUsedThisTurn = true;
            actor.Runtime.CurrentAp -= 6;
            for (int i = 0; i < 3; i++)
            {
                if (target.Runtime.CurrentHp <= 0) break;
                ResolveAttack(actor, target, isPlayer, result, isCharge: false, isAoO: false, extraAccuracyMod: -2);
            }
            return true;
        }

        // 散射 (6 AP) — 远程 + 2+ 目标范围
        if (!isMelee && tree.HasSkillEffect("scatter_shot") && actor.Runtime.CurrentAp >= 6)
        {
            var targetPos = opposing.Positions[target];
            var hits = new List<BattleUnitModel> { target };
            foreach (var u in opposing.AliveUnits)
            {
                if (u == target) continue;
                if (hits.Count >= 3) break;
                int d = HexUtils.AxialDistance(opposing.Positions[u], targetPos);
                if (d <= 1) hits.Add(u);
            }
            if (hits.Count >= 2)
            {
                actor.Runtime.NonSpellSkillUsedThisTurn = true;
                actor.Runtime.CurrentAp -= 6;
                foreach (var h in hits)
                {
                    if (h.Runtime.CurrentHp <= 0) continue;
                    ResolveAttack(actor, h, isPlayer, result, isCharge: false, isAoO: false, extraAccuracyMod: -2);
                }
                return true;
            }
        }

        // ==== WIS 刺客系（2026-05-17 重设计）====

        // 暗杀 (8 AP, 每场 1 次) — 优先击杀 HP<30% 目标
        if (tree.HasSkillEffect("assassinate") && actor.Runtime.AssassinateUsedThisCombat == 0
            && actor.Runtime.CurrentAp >= 8)
        {
            BattleUnitModel? lowHpTarget = null;
            foreach (var u in opposing.AliveUnits)
            {
                int hpMax = u.GetMaxHp();
                if (hpMax > 0 && u.Runtime.CurrentHp * 1.0f / hpMax < 0.30f)
                {
                    if (lowHpTarget == null || u.Runtime.CurrentHp < lowHpTarget.Runtime.CurrentHp)
                        lowHpTarget = u;
                }
            }
            if (lowHpTarget != null)
            {
                int dist = HexUtils.AxialDistance(actorPos, opposing.Positions[lowHpTarget]);
                if (dist <= 1 || (!isMelee && dist <= 8))
                {
                    actor.Runtime.NonSpellSkillUsedThisTurn = true;
                    actor.Runtime.AssassinateUsedThisCombat = 1;
                    actor.Runtime.CurrentAp -= 8;
                    int curHp = lowHpTarget.Runtime.CurrentHp;
                    int dmg = curHp; // 普通敌人直接斩杀
                    lowHpTarget.Runtime.CurrentHp = 0;
                    lowHpTarget.CurrentHp = 0;
                    if (isPlayer) { result.PlayerAttacksLanded++; result.PlayerDamageDealt += dmg; result.PlayerAttacksAttempted++; }
                    else          { result.EnemyAttacksLanded++;  result.EnemyDamageDealt  += dmg; result.EnemyAttacksAttempted++; }
                    // 死灵之锋触发：击杀后下次攻击 +20% / 暴击 +10%
                    if (tree.HasSkillEffect("deathblow_focus"))
                        actor.Runtime.DeathblowFocusPendingTurns = 2;
                    return true;
                }
            }
        }

        // 爆头突袭 (8 AP) — 必定暴击 + 1.5x 伤害（武器攻击）
        if (tree.HasSkillEffect("head_shot") && actor.Runtime.CurrentAp >= 8
            && actor.Runtime.HeadShotPendingTurns == 0)
        {
            int dist = HexUtils.AxialDistance(actorPos, opposing.Positions[target]);
            int wepRange = GetWeaponRange(actor);
            if (dist <= wepRange)
            {
                actor.Runtime.NonSpellSkillUsedThisTurn = true;
                actor.Runtime.CurrentAp -= 8;
                // 直接结算：必中 + 1.5x 伤害（用大命中加成模拟必中）
                ResolveAttack(actor, target, isPlayer, result, isCharge: false, isAoO: false,
                              extraAccuracyMod: +20, damageMultiplier: 1.5f);
                return true;
            }
        }

        // 法力涌动 (4 AP, 每场 1 次) — Mana 低于 30% 时触发
        if (tree.HasSkillEffect("mana_surge") && actor.Runtime.ManaSurgeUsedThisCombat == 0
            && actor.Runtime.CurrentAp >= 4 && actor.Data != null)
        {
            int maxMana = CombatStats.GetMaxMana(actor.Data);
            if (maxMana > 0 && actor.Data.CurrentMana * 1.0f / maxMana < 0.30f)
            {
                actor.Runtime.NonSpellSkillUsedThisTurn = true;
                actor.Runtime.ManaSurgeUsedThisCombat = 1;
                actor.Runtime.CurrentAp -= 4;
                actor.Data.CurrentMana = maxMana;
                return true;
            }
        }

        return false;
    }

    /// <summary>旋风斩 / 剑舞 / 血腥漩涡 共享的"打邻接全部"实现。</summary>
    private static bool ExecuteWhirlwind(
        BattleUnitModel actor, BattleSquad active, BattleSquad opposing,
        bool isPlayer, HeadlessCombatResult result, int apCost, float damageMultiplier = 1.0f)
    {
        actor.Runtime.NonSpellSkillUsedThisTurn = true;
        actor.Runtime.CurrentAp -= apCost;
        var actorPos = active.Positions[actor];
        var hits = new List<BattleUnitModel>();
        foreach (var u in opposing.AliveUnits)
        {
            int d = HexUtils.AxialDistance(actorPos, opposing.Positions[u]);
            if (d <= 1) hits.Add(u);
        }
        foreach (var h in hits)
        {
            if (h.Runtime.CurrentHp <= 0) continue;
            ResolveAttack(actor, h, isPlayer, result, isCharge: false, isAoO: false,
                          extraAccuracyMod: 0, damageMultiplier: damageMultiplier);
        }
        return true;
    }

    /// <summary>
    /// Try to cast a destruction-school spell at <paramref name="target"/>.
    /// Implements docs/法表系统.md §4.1 — picks the best spell the caster's level
    /// allows and that fits the current situation. Spells are guaranteed-hit
    /// true damage that bypasses AC, DR, and armor durability (§2.1, §2.2).
    /// Returns true if a spell was cast (AP/Mana consumed).
    /// </summary>
    private static bool TryCastDamageSpell(
        BattleUnitModel actor, BattleUnitModel target, bool attackerIsPlayer,
        HeadlessCombatResult result, int dist)
    {
        // Gate: INT must be the actor's primary or near-primary attribute.
        int maxOther = Math.Max(Math.Max(Math.Max(actor.Data.Str, actor.Data.Dex),
                                         Math.Max(actor.Data.Con, actor.Data.Wis)),
                                actor.Data.Cha);
        if (actor.Data.Intel < maxOther - 1) return false;

        // Pick best spell available given current Mana/AP/level/range.
        // Spell ring unlocked roughly with caster level: 1@L1, 2@L8, 3@L16, 4@L24, 5@L32
        int level = Math.Max(1, actor.Data.Level);
        int maxRing = Math.Min(5, 1 + (level - 1) / 8);
        int curMana = actor.Data.CurrentMana;
        int curAp = (int)actor.Runtime.CurrentAp;
        int intMod = RPGRuleEngine.GetStatModifier(actor.Data.Intel);

        // Try from highest ring down to lowest. Returns true on first hit.
        for (int ring = maxRing; ring >= 1; ring--)
        {
            if (CastDestructionSpell(actor, target, attackerIsPlayer, result, dist, ring, intMod, curMana, curAp))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Cast a specific ring's destruction spell. AOE rings hit nearby enemies
    /// (full damage to all targets per docs §2.2).
    /// </summary>
    private static bool CastDestructionSpell(
        BattleUnitModel actor, BattleUnitModel target, bool attackerIsPlayer,
        HeadlessCombatResult result, int dist,
        int ring, int intMod, int curMana, int curAp)
    {
        // Spell parameters per ring (docs §4.1)
        int apCost, manaCost, range, dCount, dSides;
        bool isAoe;
        switch (ring)
        {
            case 1:  // 火花: 1d8+INT, 单体 6 格
                apCost = 3; manaCost = 4; range = 6; dCount = 1; dSides = 8; isAoe = false; break;
            case 2:  // 冰锥术: 3d6+INT, 锥形 4 格 (sim 简化为单体 4 格)
                apCost = 4; manaCost = 8; range = 4; dCount = 3; dSides = 6; isAoe = false; break;
            case 3:  // 火球术: 4d6+INT, AOE 半径 1，最多 4 目标 (8 格距离)
                apCost = 6; manaCost = 12; range = 8; dCount = 4; dSides = 6; isAoe = true; break;
            case 4:  // 连锁闪电: 5d6+INT, 跳 4 目标 8 格 (sim 简化为单体 + 跳跃衰减)
                apCost = 7; manaCost = 16; range = 8; dCount = 5; dSides = 6; isAoe = false; break;
            case 5:  // 雷暴: 6d8+INT, AOE 半径 2，最多 6 目标 (13 格)
                apCost = 8; manaCost = 24; range = 13; dCount = 6; dSides = 8; isAoe = true; break;
            default: return false;
        }
        int effectiveManaCost = SkillTreeKeystoneResolver.ApplySpellManaCost(actor.Data, manaCost);
        int hpCost = SkillTreeKeystoneResolver.GetSpellHpCost(actor.Data, manaCost);
        if (curAp < apCost) return false;
        if (curMana < effectiveManaCost) return false;
        if (hpCost > 0 && actor.Runtime.CurrentHp <= hpCost) return false;
        if (dist > range) return false;

        // Consume resources
        actor.Runtime.CurrentAp -= apCost;
        actor.Data.CurrentMana = Math.Max(0, actor.Data.CurrentMana - effectiveManaCost);
        if (hpCost > 0)
        {
            actor.Runtime.CurrentHp = Math.Max(1, actor.Runtime.CurrentHp - hpCost);
            actor.CurrentHp = actor.Runtime.CurrentHp;
        }
        actor.Runtime.HasActed = true;

        // Damage: baseDice + Mod(INT)
        int baseDmg = CombatRandom.RollDice(dCount, dSides) + intMod;

        // Find all targets (primary + AOE if applicable)
        var hits = new List<BattleUnitModel> { target };
        if (isAoe)
        {
            // AOE: pick up to 3 additional enemies within radius 1 of target
            // (full damage per docs §2.2 — no main/副 split)
            var oppPos = _activePlayer != null && _activePlayer.AliveUnits.Contains(target)
                ? _activePlayer.Positions[target]
                : (_activeEnemy?.Positions[target] ?? Vector2I.Zero);

            // Pick the opposite squad relative to the actor
            BattleSquad? opposing = null;
            if (_activePlayer != null && _activePlayer.AliveUnits.Contains(actor))
                opposing = _activeEnemy;
            else if (_activeEnemy != null && _activeEnemy.AliveUnits.Contains(actor))
                opposing = _activePlayer;
            if (opposing != null)
            {
                int extra = ring == 3 ? 3 : 5;  // 火球 4-1=3, 雷暴 6-1=5
                foreach (var u in opposing.AliveUnits)
                {
                    if (u == target) continue;
                    if (extra <= 0) break;
                    int d = HexUtils.AxialDistance(opposing.Positions[u], oppPos);
                    int radius = ring == 3 ? 1 : 2;
                    if (d <= radius) { hits.Add(u); extra--; }
                }
            }
        }

        // Apply true damage to each target — bypass AC, DR, armor durability.
        if (attackerIsPlayer) result.PlayerAttacksAttempted++;
        else                  result.EnemyAttacksAttempted++;

        int totalDealt = 0;
        foreach (var h in hits)
        {
            int preHp = h.Runtime.CurrentHp;
            h.Runtime.CurrentHp = Math.Max(0, preHp - baseDmg);
            h.CurrentHp = h.Runtime.CurrentHp;
            int dealt = preHp - h.Runtime.CurrentHp;
            totalDealt += dealt;
            if (dealt > 0)
            {
                if (attackerIsPlayer)
                {
                    result.PlayerAttacksLanded++;
                    result.PlayerDamageDealt += dealt;
                }
                else
                {
                    result.EnemyAttacksLanded++;
                    result.EnemyDamageDealt += dealt;
                }
            }
        }
        if (ShouldLog)
        {
            Godot.GD.Print($"    [SPELL ring{ring}] {actor.Data.UnitName} cast → {hits.Count} targets, baseDmg={baseDmg}, totalDealt={totalDealt}, mana left={actor.Data.CurrentMana}/{CombatStats.GetMaxMana(actor.Data)}, AP left={(int)actor.Runtime.CurrentAp}");
        }
        return true;
    }

    /// <summary>
    /// Simulate attacks of opportunity when <paramref name="actor"/> leaves the
    /// melee reach of any enemy. Each triggering enemy gets one free swing at
    /// half damage, charged against this AoO.
    /// </summary>
    private static void ResolveAoOOnMove(
        BattleUnitModel actor,
        BattleSquad self,
        BattleSquad opposing,
        Vector2I from,
        Vector2I to,
        bool aooFromPlayer,
        HeadlessCombatResult result)
    {
        foreach (var enemy in opposing.AliveUnits)
        {
            if (enemy.Runtime.CurrentHp <= 0) continue;
            // AoO only triggers from melee weapons.
            if (enemy.GetMainHand() is WeaponData ew && ew.IsRanged) continue;
            if (enemy.Runtime.AooUsedThisTurn) continue;

            int enemyRange = (enemy.GetMainHand() is WeaponData ew2) ? Math.Max(1, ew2.RangeCells) : 1;
            var enemyPos = opposing.Positions[enemy];
            int distFrom = HexUtils.AxialDistance(enemyPos, from);
            int distTo   = HexUtils.AxialDistance(enemyPos, to);
            if (distFrom <= enemyRange && distTo > enemyRange)
            {
                // Mark + resolve a half-damage attack.
                enemy.Runtime.AooUsedThisTurn = true;
                ResolveAttack(enemy, actor, aooFromPlayer, result, isCharge: false, isAoO: true);
            }
        }
    }

    private static int GetWeaponRange(BattleUnitModel actor)
    {
        if (actor.GetMainHand() is WeaponData w) return Math.Max(1, w.RangeCells);
        return 1;
    }

    /// <summary>
    /// 武器切换 AI（v0.6 §3.2 切换武器消耗 2 AP）。
    /// 简化逻辑：主手够不到最近敌人，但副手投掷武器能够到 → 切换。
    /// 反过来：贴脸近战 + 主手是远程武器 → 切回近战副手。
    /// 每回合最多 1 次切换以防 AP 抖动死循环。
    /// </summary>
    private static void TryAutoSwitchWeapon(
        BattleUnitModel actor, BattleSquad active, BattleSquad opposing)
    {
        if (actor.Data == null) return;
        if (actor.Data.SecondaryMainHand == null) return;
        if (actor.Runtime.CurrentAp < 2) return; // 切换需 2 AP
        if (actor.Runtime.WeaponSwitchedThisTurn) return; // 每回合最多 1 次

        var actorPos = active.Positions[actor];

        // 找最近敌人距离
        int nearest = int.MaxValue;
        foreach (var u in opposing.AliveUnits)
        {
            int d = HexUtils.AxialDistance(actorPos, opposing.Positions[u]);
            if (d < nearest) nearest = d;
        }
        if (nearest == int.MaxValue) return;

        var primary = actor.Data.PrimaryMainHand;
        var secondary = actor.Data.SecondaryMainHand;
        if (primary == null || secondary == null) return;

        var current = actor.Data.Runtime.UsingPrimaryWeapon ? primary : secondary;
        var alternate = actor.Data.Runtime.UsingPrimaryWeapon ? secondary : primary;

        int curRange = current.RangeCells;
        int altRange = alternate.RangeCells;

        // 触发切换条件：
        //   1) 当前武器够不到最近敌人，但备用能够到（典型：近战追不上远程，切投枪）
        //   2) 当前是远程，最近敌人贴脸（≤1）+ 备用是近战（投枪用空了切回剑）
        bool currentTooShort = nearest > curRange && nearest <= altRange;
        bool currentTooLongForMelee = nearest <= 1 && current.IsRanged && !alternate.IsRanged;

        if (currentTooShort || currentTooLongForMelee)
        {
            actor.Runtime.CurrentAp -= 2;
            actor.Data.Runtime.UsingPrimaryWeapon = !actor.Data.Runtime.UsingPrimaryWeapon;
            actor.Runtime.WeaponSwitchedThisTurn = true;
            if (DebugFirstBattle && _firstBattleStarted)
            {
                Godot.GD.Print($"    [SWITCH] {actor.Data.UnitName} 切换到 {(actor.Data.Runtime.UsingPrimaryWeapon ? "主手" : "副手")} 武器（敌距={nearest}，原 range={curRange}，新 range={altRange}）");
            }
        }
    }

    /// <summary>
    /// Cached squads for the in-flight battle, used by the LOS path-penalty
    /// callback to detect units along a projectile's path. We stash these as
    /// statics for the duration of <see cref="Run"/>; nesting Run() calls is
    /// not supported but unneeded for sim use.
    /// </summary>
    private static BattleSquad? _activePlayer;
    private static BattleSquad? _activeEnemy;

    /// <summary>
    /// True if a unit other than <paramref name="attacker"/> or
    /// <paramref name="defender"/> currently stands on <paramref name="pos"/>.
    /// </summary>
    private static bool IsTileOccupied(Vector2I pos, BattleUnitModel attacker, BattleUnitModel defender)
    {
        if (_activePlayer != null)
            foreach (var u in _activePlayer.AliveUnits)
                if (u != attacker && u != defender && _activePlayer.Positions[u] == pos) return true;
        if (_activeEnemy != null)
            foreach (var u in _activeEnemy.AliveUnits)
                if (u != attacker && u != defender && _activeEnemy.Positions[u] == pos) return true;
        return false;
    }

    private static void ResolveAttack(
        BattleUnitModel attacker,
        BattleUnitModel defender,
        bool attackerIsPlayer,
        HeadlessCombatResult result,
        bool isCharge = false,
        bool isAoO = false,
        int extraAccuracyMod = 0,
        float damageMultiplier = 1.0f)
    {
        int apCost = (attacker.GetMainHand() is WeaponData wd) ? wd.ApCost : 4;
        // AoOs are free (don't consume the attacker's AP).
        if (!isAoO && attacker.Runtime.CurrentAp < apCost) return;

        // Skill-tree contributions (pure C#, available headless because the
        // tree is built via SkillTreeAllocator and stored on Runtime.SkillTree).
        var atkTree = attacker.Runtime.SkillTree;
        var defTree = defender.Runtime.SkillTree;

        var weapon = attacker.GetMainHand() as WeaponData;
        bool isMelee = weapon == null || !weapon.IsRanged;
        bool isRanged = !isMelee;
        int distance = HexUtils.AxialDistance(attacker.Runtime.GridPos, defender.Runtime.GridPos);

        // Ranged attacks: accumulate accuracy penalties for terrain + units in
        // path. Replaces the earlier binary "no-LOS aborts the swing" rule.
        bool hasAdvantage    = isCharge;
        bool hasDisadvantage = false;
        int  accuracyMod     = extraAccuracyMod;  // 主动技能传入的额外命中修正
        if (!isMelee)
        {
            accuracyMod += LosCore.GetPathPenalty(
                attacker.Runtime.GridPos, defender.Runtime.GridPos, Field,
                pos => IsTileOccupied(pos, attacker, defender));
        }

        // High-ground advantage / disadvantage.
        var hg = LosCore.GetHighGroundBonus(attacker.Runtime.GridPos, defender.Runtime.GridPos, Field);
        if (hg.Advantage)    hasAdvantage    = true;
        if (hg.Disadvantage) hasDisadvantage = true;

        int attackBonus = SkillTreeKeystoneResolver.ApplyAttackBonus(attacker.Data, attacker.GetAttackBonus(), isMelee, isRanged, distance);
        // sim buff: 攻击 +N
        if (attacker.Runtime.BuffAttackBonusTurns > 0)
            attackBonus += attacker.Runtime.BuffAttackBonusValue;
        // sim debuff: 攻击 -N
        if (attacker.Runtime.DebuffAttackPenaltyTurns > 0)
            attackBonus -= attacker.Runtime.DebuffAttackPenaltyValue;
        attackBonus = SkillTreeKeystoneResolver.ApplyIncomingAttackBonus(defender.Data, attackBonus, isRanged);

        // WIS 刺客系 (2026-05-17): 致命猎杀 — 对 HP<30% 敌人 +2 命中
        bool defenderLowHp = false;
        if (defender.GetMaxHp() > 0)
            defenderLowHp = defender.Runtime.CurrentHp * 1.0f / defender.GetMaxHp() < 0.30f;
        if (atkTree != null && atkTree.HasSkillEffect("lethal_focus") && defenderLowHp)
            attackBonus += 2;

        int defenderAcBonus = 0;
        // sim buff: 防御方 AC +N
        if (defender.Runtime.BuffAcBonusTurns > 0)
            defenderAcBonus += defender.Runtime.BuffAcBonusValue;
        int defenderAc = defender.GetEffectiveAc(false, defenderAcBonus);

        // 暴击率加成：节点 critical_rate + lethal_focus 低血敌人 +10% + deathblow_focus 击杀后 +10%
        float bonusCritChance = SkillTreeKeystoneResolver.GetBonusCritChance(attacker.Data);
        if (atkTree != null && atkTree.HasSkillEffect("lethal_focus") && defenderLowHp)
            bonusCritChance += 0.10f;
        if (attacker.Runtime.DeathblowFocusPendingTurns > 0)
            bonusCritChance += 0.10f;

        // Build an attack input.
        var atkInput = new CombatRuleEngine.AttackInput
        {
            AttackBonus    = attackBonus,
            TargetAc       = defenderAc,
            CritThreshold  = attacker.GetCritThreshold(),
            HasAdvantage   = hasAdvantage,
            HasDisadvantage = hasDisadvantage,
            AccuracyMod    = accuracyMod,
            CoverAcBonus   = 0,            // cover folded into AccuracyMod above
            BonusCritChance = bonusCritChance,
        };
        SkillTreeKeystoneResolver.ApplyAttackRollRules(attacker.Data, ref atkInput, isMelee);

        var roll = CombatRuleEngine.RollAttack(in atkInput);
        if (AttackTraceSink != null)
        {
            AttackTraceSink.Invoke(new AttackTrace
            {
                AttackerIsPlayer = attackerIsPlayer,
                AttackBonus      = attackBonus,
                AccuracyMod      = accuracyMod,
                TargetAc         = defenderAc,
                HasAdvantage     = hasAdvantage,
                HasDisadvantage  = hasDisadvantage,
                HitChancePercent = roll.HitChancePercent,
                NaturalRoll      = roll.NaturalRoll,
                TotalAttack      = roll.TotalAttack,
                IsHit            = roll.IsHit,
                IsCritical       = roll.IsCritical,
                IsFumble         = roll.IsFumble,
                IsGraze          = roll.IsGraze
            });
        }
        if (attackerIsPlayer) result.PlayerAttacksAttempted++;
        else                  result.EnemyAttacksAttempted++;

        if (!isAoO)
        {
            attacker.Runtime.CurrentAp -= apCost;
            attacker.Runtime.HasActed = true;
        }

        if (!roll.IsHit) return;

        // Roll damage
        var damageInfo = attacker.RollDamage();
        int baseDamage = damageInfo.ContainsKey("total") ? (int)damageInfo["total"] : 1;

        int passiveDamageBonus = 0;
        if (atkTree != null)
            passiveDamageBonus = isMelee ? atkTree.GetMeleeDamageBonus() : atkTree.GetRangedDamageBonus();

        // v0.6 11.4.1 节点平伤 AP 归一化:
        //   EffectiveNodeWeaponDamage = NodeWeaponDamage × WeaponAP / 4
        // 防止低 AP 多段武器（飞刀 2AP、匕首 2AP 等）从节点 +N damage 中线性获利。
        int weaponApForNode = weapon?.ApCost ?? 4;
        passiveDamageBonus = (passiveDamageBonus * weaponApForNode) / 4;

        var dmgInput = new CombatRuleEngine.DamageInput
        {
            BaseDamage = baseDamage,
            IsGraze = roll.IsGraze,
            IsCritical = roll.IsCritical,
            CritMultiplier = 2,
            CritDamageTakenMultiplier = defender.GetCritDamageTakenMultiplier(),
            SneakDamage = 0,
            PassiveMeleeBonus = isMelee ? passiveDamageBonus : 0,
            PassiveMeleeMultiplier = 1.0f,
            IsMelee = isMelee,
            FlankMultiplier = 1.0f,
            ChargeMultiplier = isCharge ? 1.5f : 1.0f,  // Charge: +50% damage
            MountBonus = 0,
            DamageReduction = 0,
            FinalMultiplier = (isAoO ? 0.5f : 1.0f) * damageMultiplier  // 包含 AoO 半伤 + 主动技能伤害倍率
                * Buff.BuffSystem.ResolveMultiplier(attacker.Data, "damage")  // buff +%伤害(与实战路径一致)
                * SkillTreeKeystoneResolver.GetDamageFinalMultiplier(attacker.Data, isMelee, isRanged, distance, roll.IsCritical),
        };
        // Apply ranged damage bonus directly to base (CombatRuleEngine has no ranged-passive field)
        if (!isMelee && passiveDamageBonus != 0)
            dmgInput.BaseDamage += passiveDamageBonus;

        // WIS 死灵之锋：击杀后下次攻击 +20% 伤害（之后清除）
        bool deathblowApplied = false;
        if (attacker.Runtime.DeathblowFocusPendingTurns > 0)
        {
            dmgInput.FinalMultiplier *= 1.20f;
            deathblowApplied = true;
        }

        var calc = CombatRuleEngine.CalculateDamage(in dmgInput);

        int finalDamage = calc.FinalDamage;

        // STR 穿甲加成 v0.6 6.3: floor(sqrt(STR/4))
        int strPen = (int)Math.Floor(Math.Sqrt(attacker.Data.Str / 4.0));

        // v0.6 6.9 中型武器 Lv.5+ 精通: 装甲伤害 ×1.2
        bool mediumLv5 = false;
        if (weapon != null && weapon.Weight == WeaponData.WeightCategory.Medium)
        {
            int masteryLv = attacker.Data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            if (masteryLv >= 5) mediumLv5 = true;
        }

        // Apply through the same DR pipeline as the live game.
        // 传入 isRanged: !isMelee 以驱动下沉后的盾牌减伤逻辑
        var dmgResult = defender.ApplyDamage(
            source: DamageSource.WeaponAttack,
            amount: finalDamage,
            damageType: weapon?.WeaponDamageType ?? WeaponData.DamageType.Slash,
            naturalRoll: roll.NaturalRoll,
            weaponWeight: weapon?.Weight ?? WeaponData.WeightCategory.Medium,
            attackerMastery: attacker.Data.WeaponMastery,
            weaponSubtype: weapon?.Subtype ?? WeaponData.WeaponSubtype.Unarmed,
            strPenBonus: strPen,
            mediumLv5Mastery: mediumLv5,
            isRanged: !isMelee);

        if (ShouldLog)
        {
            Godot.GD.Print($"    [ATK] {attacker.Data.UnitName} → {defender.Data.UnitName}: roll {roll.NaturalRoll} +{atkInput.AttackBonus} vs AC{atkInput.TargetAc} → {(roll.IsHit ? "HIT" : "MISS")}{(roll.IsCritical ? " CRIT" : "")} | base={baseDamage} final={finalDamage} → pen={dmgResult.IsPenetrated} hpDmg={dmgResult.HpDamage} drDmg={dmgResult.DrDamage} (def now HP={defender.CurrentHp}/{defender.GetMaxHp()})");
        }

        // Sync mirror field used by some downstream queries.
        defender.Runtime.CurrentHp = defender.CurrentHp;

        // 清除已用的 deathblow buff
        if (deathblowApplied) attacker.Runtime.DeathblowFocusPendingTurns = 0;

        // WIS 死灵之锋触发：本次攻击导致敌人死亡 → 给攻击者下次攻击挂 deathblow buff
        if (defender.Runtime.CurrentHp <= 0 && atkTree != null
            && atkTree.HasSkillEffect("deathblow_focus") && !deathblowApplied)
        {
            attacker.Runtime.DeathblowFocusPendingTurns = 2;
        }

        if (dmgResult.HpDamage > 0)
        {
            if (attackerIsPlayer)
            {
                result.PlayerAttacksLanded++;
                result.PlayerDamageDealt += dmgResult.HpDamage;
            }
            else
            {
                result.EnemyAttacksLanded++;
                result.EnemyDamageDealt += dmgResult.HpDamage;
            }
        }

        SkillTreeKeystoneResolver.OnAttackResolved(attacker.Data, roll.IsCritical);
        int leech = SkillTreeKeystoneResolver.ApplyBloodOathLeech(attacker.Data, dmgResult.HpDamage, isMelee);
        if (leech > 0 && attacker.Runtime.CurrentHp > 0)
        {
            attacker.Runtime.CurrentHp = Math.Min(attacker.GetMaxHp() + attacker.GetMaxHp() / 2, attacker.Runtime.CurrentHp + leech);
            attacker.CurrentHp = attacker.Runtime.CurrentHp;
        }
    }
}
