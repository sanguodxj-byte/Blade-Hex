// OverworldCommandRouter.cs
// 输入命令路由 — 把左键点击拆成语义命令
//
// 设计目标:
//   - 把左键点击拆成命令：MoveToCommand / InspectEntityCommand / InspectPoiCommand / JoinFieldBattleCommand / JoinSiegeCommand
//   - 主场景只负责收集鼠标位置和当前 hover target
//   - 点击战场、POI、实体、空地的优先级可测试
//   - 新入口不会被普通移动输入吞掉
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;

namespace BladeHex.View.Strategic;

/// <summary>命令类型</summary>
public enum OverworldCommandType
{
    MoveTo,
    InspectEntity,
    InspectPoi,
    JoinFieldBattle,
    JoinSiege,
}

/// <summary>大地图输入命令基类</summary>
public abstract class OverworldCommand
{
    public OverworldCommandType Type { get; }
    public Vector2 WorldPosition { get; }

    protected OverworldCommand(OverworldCommandType type, Vector2 worldPosition)
    {
        Type = type;
        WorldPosition = worldPosition;
    }
}

public sealed class MoveToCommand : OverworldCommand
{
    public MoveToCommand(Vector2 target) : base(OverworldCommandType.MoveTo, target) { }
}

public sealed class InspectEntityCommand : OverworldCommand
{
    public OverworldEntity Entity { get; }
    public InspectEntityCommand(OverworldEntity entity, Vector2 pos)
        : base(OverworldCommandType.InspectEntity, pos) => Entity = entity;
}

public sealed class MoveToPoiCommand : OverworldCommand
{
    public OverworldPOI Poi { get; }
    public MoveToPoiCommand(OverworldPOI poi, Vector2 pos)
        : base(OverworldCommandType.InspectPoi, pos) => Poi = poi;
}

public sealed class JoinFieldBattleCommand : OverworldCommand
{
    public JoinOpportunity Opportunity { get; }
    public JoinFieldBattleCommand(JoinOpportunity opp, Vector2 pos)
        : base(OverworldCommandType.JoinFieldBattle, pos) => Opportunity = opp;
}

public sealed class JoinSiegeCommand : OverworldCommand
{
    public JoinOpportunity Opportunity { get; }
    public JoinSiegeCommand(JoinOpportunity opp, Vector2 pos)
        : base(OverworldCommandType.JoinSiege, pos) => Opportunity = opp;
}

/// <summary>
/// 大地图命令路由器。
///
/// 管线位置:
///   左键点击 → CommandRouter.Resolve(clickPos, ...) → OverworldCommand → 分发执行
///
/// 优先级（从高到低）:
///   1. JoinFieldBattle — 点击战场 marker
///   2. JoinSiege — 点击围城 POI marker
///   3. InspectEntity — 点击具名英雄实体（180px）
///   4. InspectPoi — 点击 POI（HexSize * 2）
///   5. MoveTo — 点击空地
///
/// 不负责:
///   - 实际执行命令（由主场景或命令处理器处理）
///   - 右键信息提示
///   - 快捷键
/// </summary>
public sealed class OverworldCommandRouter
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>战场点击命中半径（像素）</summary>
    public float BattlefieldHitRadius { get; set; } = OverworldBattlefieldLayer2D.MarkerHitRadius;

    /// <summary>围城 POI 点击命中半径（像素）</summary>
    public float SiegeHitRadius { get; set; } = OverworldSiegeLayer2D.MarkerHitRadius;

    /// <summary>具名英雄实体点击半径（像素）</summary>
    public float HeroEntityHitRadius { get; set; } = OverworldInteractionHitRules.EntityHitRadius;

    /// <summary>POI 点击半径（像素）— 默认 HexSize * 2</summary>
    public float PoiHitRadius { get; set; } = OverworldInteractionHitRules.PoiHitRadius;

    // ========================================
    // 路由入口
    // ========================================

    /// <summary>
    /// 根据鼠标世界坐标和各层数据，生成对应的命令。
    /// </summary>
    /// <param name="clickPos">鼠标世界像素坐标</param>
    /// <param name="battlefieldLayer">战场层（可 null）</param>
    /// <param name="siegeLayer">围城层（可 null）</param>
    /// <param name="entities">实体列表</param>
    /// <param name="pois">POI 列表</param>
    /// <param name="playerFaction">玩家阵营</param>
    /// <returns>路由后的命令</returns>
    public OverworldCommand Resolve(
        Vector2 clickPos,
        OverworldBattlefieldLayer2D? battlefieldLayer,
        OverworldSiegeLayer2D? siegeLayer,
        List<OverworldEntity> entities,
        List<OverworldPOI> pois,
        string playerFaction)
    {
        // 优先级 1: 战场加入
        if (battlefieldLayer != null)
        {
            var joinOpp = battlefieldLayer.QueryJoinAtPosition(clickPos, BattlefieldHitRadius, entities);
            if (joinOpp != null && joinOpp.Type == WarBattleType.FieldBattle)
                return new JoinFieldBattleCommand(joinOpp, clickPos);
        }

        // 优先级 2: 围城加入
        if (siegeLayer != null)
        {
            var siegeOpp = siegeLayer.QueryJoinAtPosition(clickPos, entities, pois, playerFaction, SiegeHitRadius);
            if (siegeOpp != null && (siegeOpp.Type == WarBattleType.Siege || siegeOpp.Type == WarBattleType.ArmyJoin))
                return new JoinSiegeCommand(siegeOpp, clickPos);
        }

        // 优先级 3: 实体检查
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;
            if (clickPos.DistanceTo(entity.Position) < HeroEntityHitRadius)
            {
                return new InspectEntityCommand(entity, clickPos);
            }
        }

        // 优先级 4: POI 检查
        foreach (var poi in pois)
        {
            if (clickPos.DistanceTo(poi.Position) < PoiHitRadius)
                return new MoveToPoiCommand(poi, clickPos);
        }

        // 优先级 5: 空地移动
        return new MoveToCommand(clickPos);
    }

    /// <summary>
    /// 简化版路由 — 不依赖 Layer 对象，直接使用 WarBattleJoinService 查询。
    /// 适用于尚未集成 Layer 的过渡阶段。
    /// </summary>
    public OverworldCommand ResolveSimple(
        Vector2 clickPos,
        List<OverworldEntity> entities,
        List<OverworldPOI> pois,
        string playerFaction)
    {
        // 检查战场/围城加入（使用 WarBattleJoinService）
        var joinOpp = WarBattleJoinService.Query(
            playerPos: clickPos,
            entities: entities,
            pois: pois,
            playerFaction: playerFaction,
            joinRadius: BattlefieldHitRadius);

        if (joinOpp != null)
        {
            if (joinOpp.Type == WarBattleType.FieldBattle)
                return new JoinFieldBattleCommand(joinOpp, clickPos);
            if (joinOpp.Type == WarBattleType.Siege || joinOpp.Type == WarBattleType.ArmyJoin)
                return new JoinSiegeCommand(joinOpp, clickPos);
        }

        // 实体
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;
            if (clickPos.DistanceTo(entity.Position) < HeroEntityHitRadius)
            {
                return new InspectEntityCommand(entity, clickPos);
            }
        }

        // POI
        foreach (var poi in pois)
        {
            if (clickPos.DistanceTo(poi.Position) < PoiHitRadius)
                return new MoveToPoiCommand(poi, clickPos);
        }

        return new MoveToCommand(clickPos);
    }
}
