// Battlefield.cs
// 战场数据结构 — 支持多方参与的交战追踪与管理
//
// 设计目标:
//   - 替代单一的 EngagedWith 关系，支持 NvN 多方战场
//   - 每场战斗对应一个 Battlefield 实例
//   - 容纳攻击方/防御方多实体参与
//   - 提供战场合并、阵营判定、参与方查询能力
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>
/// 战场 — 追踪多方参与的实体交战状态
/// </summary>
public class Battlefield
{
    private static int _nextId = 1;

    /// <summary>战场唯一标识</summary>
    public string BattlefieldId { get; private set; }

    /// <summary>战场中心位置</summary>
    public Vector2 Position { get; set; }

    /// <summary>战场有效半径（像素），超出此范围视为脱离战斗</summary>
    public float Radius { get; set; } = 200.0f;

    /// <summary>攻击方实体列表</summary>
    public List<OverworldEntity> Attackers { get; } = new();

    /// <summary>防御方实体列表</summary>
    public List<OverworldEntity> Defenders { get; } = new();

    /// <summary>战场开始的游戏小时数</summary>
    public float StartedAtHour { get; set; }

    /// <summary>预计持续小时数（取参与方中最大 CombatDurationHours）</summary>
    public float DurationHours { get; set; } = 3.0f;

    /// <summary>上次渐进更新的游戏小时数</summary>
    public float LastGradualUpdateHour { get; set; } = -1f;

    /// <summary>是否已结算完成</summary>
    public bool IsResolved { get; set; } = false;

    public Battlefield()
    {
        BattlefieldId = $"bf_{_nextId++}_{System.Environment.TickCount64}";
    }

    /// <summary>战场所有参与方</summary>
    public IEnumerable<OverworldEntity> AllParticipants => Attackers.Concat(Defenders);

    /// <summary>战场参与方数量</summary>
    public int ParticipantCount => Attackers.Count + Defenders.Count;

    /// <summary>
    /// 将实体加入战场特定阵营。
    /// </summary>
    public void Join(OverworldEntity entity, bool joinAsAttacker)
    {
        // 从另一方移除（防止重复）
        Defenders.Remove(entity);
        Attackers.Remove(entity);

        if (joinAsAttacker)
            Attackers.Add(entity);
        else
            Defenders.Add(entity);

        entity.BattlefieldId = BattlefieldId;
    }

    /// <summary>
    /// 从战场移除实体。
    /// </summary>
    public void Remove(OverworldEntity entity)
    {
        Attackers.Remove(entity);
        Defenders.Remove(entity);
        entity.BattlefieldId = "";
    }

    /// <summary>
    /// 判断实体属于攻击方还是防御方。
    /// </summary>
    public bool? IsAttacker(OverworldEntity entity)
    {
        if (Attackers.Contains(entity)) return true;
        if (Defenders.Contains(entity)) return false;
        return null;
    }

    /// <summary>
    /// 判断两个实体是否为同一阵营。
    /// </summary>
    public bool AreSameSide(OverworldEntity a, OverworldEntity b)
    {
        var sideA = IsAttacker(a);
        var sideB = IsAttacker(b);
        return sideA.HasValue && sideB.HasValue && sideA == sideB;
    }

    /// <summary>
    /// 获取实体在战场中的敌方列表。
    /// </summary>
    public List<OverworldEntity> GetEnemiesOf(OverworldEntity entity)
    {
        bool? isAttacker = IsAttacker(entity);
        if (isAttacker == true)
            return Defenders.ToList();
        else if (isAttacker == false)
            return Attackers.ToList();
        return new List<OverworldEntity>();
    }

    /// <summary>
    /// 获取实体在战场中的友方列表（不含自身）。
    /// </summary>
    public List<OverworldEntity> GetAlliesOf(OverworldEntity entity)
    {
        bool? isAttacker = IsAttacker(entity);
        if (isAttacker == true)
            return Attackers.Where(e => e != entity).ToList();
        else if (isAttacker == false)
            return Defenders.Where(e => e != entity).ToList();
        return new List<OverworldEntity>();
    }

    /// <summary>
    /// 计算攻击方总战力。
    /// </summary>
    public float AttackerPower()
        => Attackers.Sum(e => e.CombatPower * e.PartySize);

    /// <summary>
    /// 计算防御方总战力。
    /// </summary>
    public float DefenderPower()
        => Defenders.Sum(e => e.CombatPower * e.PartySize);

    /// <summary>
    /// 获取当前游戏小时的双方战损进度（0~1）。
    /// </summary>
    public float GetProgress(float currentGameHour)
    {
        if (DurationHours <= 0) return 0;
        return Mathf.Clamp((currentGameHour - StartedAtHour) / DurationHours, 0f, 1f);
    }

    /// <summary>
    /// 计算交战双方的敌对实体 — 用于 OverworldEntity.EngagedWith 回写兼容。
    /// 对攻击方：找防御方中战力最高的作为"主要对手"。
    /// </summary>
    public OverworldEntity? GetPrimaryOpponent(OverworldEntity entity)
    {
        var enemies = GetEnemiesOf(entity);
        if (enemies.Count == 0) return null;
        return enemies.OrderByDescending(e => e.CombatPower).First();
    }
}
