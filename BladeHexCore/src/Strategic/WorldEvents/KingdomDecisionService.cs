using System;
using Godot;
using BladeHex.Strategic.Diplomacy;

namespace BladeHex.Strategic.WorldEvents;

/// <summary>
/// 外交决策结果
/// </summary>
public enum DecisionResult
{
    Success,
    InsufficientInfluence,
    RelationTooHigh,
    NotAtWar,
    AlreadyAtWar,
    InvalidNation,
    InTruce,       // 停战保护期内
    InCooldown     // 操作冷却期内
}

/// <summary>
/// 国家外交决议服务 (宣战 / 媾和) — 前端调用入口，委托给 DiplomacyService
/// </summary>
public static class KingdomDecisionService
{
    /// <summary>
    /// 尝试向目标国家宣战
    /// </summary>
    public static DecisionResult TryDeclareWar(string myNation, string targetNation, WorldEventEngine engine)
    {
        if (string.IsNullOrEmpty(myNation) || string.IsNullOrEmpty(targetNation) || myNation == targetNation)
            return DecisionResult.InvalidNation;

        var result = DiplomacyService.DeclareWar(myNation, targetNation, engine, engine.FactionRelations);
        return MapResult(result);
    }

    /// <summary>
    /// 尝试与目标国家媾和 (停战)
    /// </summary>
    public static DecisionResult TryMakePeace(string myNation, string targetNation, WorldEventEngine engine)
    {
        if (string.IsNullOrEmpty(myNation) || string.IsNullOrEmpty(targetNation))
            return DecisionResult.InvalidNation;

        // 玩家主动议和：跳过 AI 接受评估
        var result = DiplomacyService.ProposePeace(myNation, targetNation, engine, engine.FactionRelations, null!, null!, skipAiCheck: true);
        return MapResult(result);
    }

    private static DecisionResult MapResult(DiplomacyResult d)
    {
        return d switch
        {
            DiplomacyResult.Success => DecisionResult.Success,
            DiplomacyResult.InsufficientInfluence => DecisionResult.InsufficientInfluence,
            DiplomacyResult.RelationTooHigh => DecisionResult.RelationTooHigh,
            DiplomacyResult.NotAtWar => DecisionResult.NotAtWar,
            DiplomacyResult.AlreadyAtWar => DecisionResult.AlreadyAtWar,
            DiplomacyResult.InvalidFaction => DecisionResult.InvalidNation,
            DiplomacyResult.InTruce => DecisionResult.InTruce,
            DiplomacyResult.InCooldown => DecisionResult.InCooldown,
            DiplomacyResult.Failed => DecisionResult.InvalidNation,
            _ => DecisionResult.InvalidNation
        };
    }
}
