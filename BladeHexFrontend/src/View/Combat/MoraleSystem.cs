// MoraleSystem.cs
// 士气系统 — 事件驱动的士气变化、士气光环、溃逃判定
// 对应策划案 03-战术战斗系统 → 六、士气系统
using Godot;
using BladeHex.Map;
using BladeHex.Data;
using System.Collections.Generic;

namespace BladeHex.Combat;

/// <summary>
/// 士气系统 — 静态工具类 + Node
/// 负责士气事件处理、士气效果查询、溃逃判定
/// 注意：原版是 Node（有信号），C# 版本改为纯静态工具类。
/// 信号由调用方（CombatManager）负责。
/// </summary>
public static class MoraleSystem
{
    // ========================================
    // 士气常量
    // ========================================

    public const int MoraleMin = -60;
    public const int MoraleMax = 40;

    /// <summary>高昂士气阈值</summary>
    public const int MoraleHighThreshold = 20;
    /// <summary>低落士气阈值</summary>
    public const int MoraleLowThreshold = -20;
    /// <summary>崩溃士气阈值</summary>
    public const int MoraleBrokenThreshold = -40;
    /// <summary>溃逃士气阈值</summary>
    public const int MoraleRoutThreshold = -60;

    // ========================================
    // 士气事件处理
    // ========================================

    /// <summary>单位被击杀 — 距离衰减影响周围单位士气</summary>
    public static List<UnitMoraleChange> OnUnitKilled(Unit killed, Unit killer, List<Unit> allUnits)
    {
        var changes = new List<UnitMoraleChange>();
        foreach (var unit in allUnits)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0)
                continue;
            if (unit == killed)
                continue;

            int dist = HexUtils.Distance(unit.GridPos.X, unit.GridPos.Y, killed.GridPos.X, killed.GridPos.Y);
            bool isAlly = IsSameSide(unit, killed);

            int change;
            if (isAlly)
            {
                bool isHero = killed.Data!.IsEnemy == false;
                change = AllyDeathMoraleChange(dist, isHero);
            }
            else
            {
                change = EnemyKillMoraleChange(dist);
            }

            if (change != 0)
            {
                ChangeMorale(unit, change);
                changes.Add(new UnitMoraleChange { Unit = unit, Amount = change });
            }
        }
        return changes;
    }

    /// <summary>单位被暴击命中</summary>
    public static void OnUnitCritHit(Unit target, Unit attacker)
    {
        ChangeMorale(target, -5);
    }

    /// <summary>单位被包夹攻击</summary>
    public static void OnUnitFlanked(Unit target, Unit attacker)
    {
        ChangeMorale(target, -3);
    }

    /// <summary>回合开始检查重创（单方损失过半）</summary>
    public static void OnTurnStartHeavyLosses(List<Unit> sideUnits, int initialCount)
    {
        int aliveCount = 0;
        foreach (var u in sideUnits)
            if (GodotObject.IsInstanceValid(u) && u.CurrentHp > 0)
                aliveCount++;

        if (initialCount > 0 && aliveCount <= initialCount / 2)
        {
            foreach (var u in sideUnits)
                if (GodotObject.IsInstanceValid(u) && u.CurrentHp > 0)
                    ChangeMorale(u, -15);
        }
    }

    /// <summary>英雄士气光环（每回合开始）</summary>
    public static void OnHeroAura(Unit hero, List<Unit> allies)
    {
        if (!GodotObject.IsInstanceValid(hero) || hero.CurrentHp <= 0)
            return;
        if (hero.Data!.IsEnemy)
            return;

        int chaMod = RPGRuleEngine.GetStatModifier(hero.Data!.Cha);
        int auraRange = 1 + Mathf.Max(0, chaMod);

        foreach (var ally in allies)
        {
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0)
                continue;
            if (ally == hero)
                continue;

            int dist = HexUtils.Distance(ally.GridPos.X, ally.GridPos.Y, hero.GridPos.X, hero.GridPos.Y);
            if (dist <= auraRange)
                ChangeMorale(ally, 3);
        }
    }

    /// <summary>不利地形士气衰减</summary>
    public static void OnBadTerrain(Unit unit)
    {
        ChangeMorale(unit, -1);
    }

    /// <summary>战斗胜利</summary>
    public static void OnVictory(List<Unit> allUnits)
    {
        foreach (var unit in allUnits)
            if (GodotObject.IsInstanceValid(unit) && unit.CurrentHp > 0)
                ChangeMorale(unit, 5);
    }

    // ========================================
    // 士气效果查询
    // ========================================

    /// <summary>获取士气等级效果</summary>
    public static MoraleEffects GetMoraleEffects(Unit unit)
    {
        int level = unit.Data!.Morale;
        return level switch
        {
            >= MoraleHighThreshold => new MoraleEffects { CritBonus = 0.20f, FumbleRate = 0.0f, AcModifier = 0, HitBonus = 2, Name = "高昂" },
            >= MoraleLowThreshold => new MoraleEffects { CritBonus = 0.0f, FumbleRate = 0.0f, AcModifier = 0, HitBonus = 0, Name = "正常" },
            >= MoraleBrokenThreshold => new MoraleEffects { CritBonus = 0.0f, FumbleRate = 0.20f, AcModifier = 0, HitBonus = -1, Name = "低落" },
            >= MoraleRoutThreshold => new MoraleEffects { CritBonus = 0.0f, FumbleRate = 0.40f, AcModifier = -2, HitBonus = -2, Name = "崩溃" },
            _ => new MoraleEffects { CritBonus = 0.0f, FumbleRate = 1.0f, AcModifier = -2, HitBonus = -4, Name = "溃逃" },
        };
    }

    /// <summary>检查是否溃逃</summary>
    public static bool CheckRout(Unit unit)
    {
        return unit.Data!.Morale <= MoraleRoutThreshold;
    }

    // ========================================
    // 内部方法
    // ========================================

    /// <summary>修改士气值（带范围钳制）</summary>
    public static void ChangeMorale(Unit unit, int amount)
    {
        if (unit.Data == null) return;
        unit.Data.Morale = Mathf.Clamp(unit.Data.Morale + amount, MoraleMin, MoraleMax);
    }

    private static bool IsSameSide(Unit a, Unit b)
    {
        return a.Data!.IsEnemy == b.Data!.IsEnemy;
    }

    private static int AllyDeathMoraleChange(int dist, bool isHero)
    {
        if (isHero)
        {
            if (dist <= 1) return -6;
            return -4;
        }
        if (dist <= 1) return -10;
        else if (dist <= 2) return -8;
        else if (dist <= 3) return -6;
        return 0;
    }

    private static int EnemyKillMoraleChange(int dist)
    {
        if (dist <= 1) return 10;
        else if (dist <= 2) return 8;
        else if (dist <= 3) return 6;
        return 0;
    }

    // ========================================
    // 数据类
    // ========================================

    public struct MoraleEffects
    {
        public float CritBonus;
        public float FumbleRate;
        public int AcModifier;
        public int HitBonus;
        public string Name;
    }

    public struct UnitMoraleChange
    {
        public Unit Unit;
        public int Amount;
    }
}