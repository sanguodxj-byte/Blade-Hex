using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Diplomacy;

/// <summary>
/// 外交决策结果
/// </summary>
public enum DiplomacyResult
{
    Success,
    Failed,
    InsufficientInfluence,
    RelationTooHigh,
    NotAtWar,
    AlreadyAtWar,
    InTruce,
    InCooldown,
    InvalidFaction
}

/// <summary>
/// 外交平衡常数配置
/// </summary>
public class DiplomacyBalanceConfig
{
    public float PersonalRelationWeight { get; set; } = 0.5f;
    public int DeclareWarCooldownDays { get; set; } = 10;
    public int ProposePeaceCooldownDays { get; set; } = 10;
    public int TruceDays { get; set; } = 30;
    public int DeclareWarInfluenceCost { get; set; } = 50;
    public int ProposePeaceInfluenceCost { get; set; } = 80;
    public int DeclareWarRelationThreshold { get; set; } = -30;
    public int DeclareWarRelationTarget { get; set; } = -80;
    public int ProposePeaceRelationTarget { get; set; } = -30;
    public float WeeklyDeclareWarChance { get; set; } = 0.05f;
    public float MonthlyProposePeaceChance { get; set; } = 0.10f;

    public static DiplomacyBalanceConfig Load()
    {
        var config = new DiplomacyBalanceConfig();
        try
        {
            string jsonStr = "";
            if (Godot.FileAccess.FileExists("res://assets/diplomacy_balance.json"))
            {
                using var file = Godot.FileAccess.Open("res://assets/diplomacy_balance.json", Godot.FileAccess.ModeFlags.Read);
                if (file != null) jsonStr = file.GetAsText();
            }
            else if (System.IO.File.Exists("assets/diplomacy_balance.json"))
            {
                jsonStr = System.IO.File.ReadAllText("assets/diplomacy_balance.json");
            }
            
            if (!string.IsNullOrEmpty(jsonStr))
            {
                var json = new Godot.Json();
                if (json.Parse(jsonStr) == Godot.Error.Ok)
                {
                    var data = json.Data.AsGodotDictionary();
                    if (data.ContainsKey("PersonalRelationWeight")) config.PersonalRelationWeight = (float)data["PersonalRelationWeight"].AsDouble();
                    if (data.ContainsKey("DeclareWarCooldownDays")) config.DeclareWarCooldownDays = data["DeclareWarCooldownDays"].AsInt32();
                    if (data.ContainsKey("ProposePeaceCooldownDays")) config.ProposePeaceCooldownDays = data["ProposePeaceCooldownDays"].AsInt32();
                    if (data.ContainsKey("TruceDays")) config.TruceDays = data["TruceDays"].AsInt32();
                    if (data.ContainsKey("DeclareWarInfluenceCost")) config.DeclareWarInfluenceCost = data["DeclareWarInfluenceCost"].AsInt32();
                    if (data.ContainsKey("ProposePeaceInfluenceCost")) config.ProposePeaceInfluenceCost = data["ProposePeaceInfluenceCost"].AsInt32();
                    if (data.ContainsKey("DeclareWarRelationThreshold")) config.DeclareWarRelationThreshold = data["DeclareWarRelationThreshold"].AsInt32();
                    if (data.ContainsKey("DeclareWarRelationTarget")) config.DeclareWarRelationTarget = data["DeclareWarRelationTarget"].AsInt32();
                    if (data.ContainsKey("ProposePeaceRelationTarget")) config.ProposePeaceRelationTarget = data["ProposePeaceRelationTarget"].AsInt32();
                    if (data.ContainsKey("WeeklyDeclareWarChance")) config.WeeklyDeclareWarChance = (float)data["WeeklyDeclareWarChance"].AsDouble();
                    if (data.ContainsKey("MonthlyProposePeaceChance")) config.MonthlyProposePeaceChance = (float)data["MonthlyProposePeaceChance"].AsDouble();
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DiplomacyBalanceConfig] 加载配置异常: {ex.Message}");
        }
        return config;
    }
}

/// <summary>
/// 核心外交决策服务
/// </summary>
public static class DiplomacyService
{
    private static readonly Random _rng = new();

    public static DiplomacyResult DeclareWar(
        string myFaction, 
        string targetFaction, 
        WorldEventEngine engine, 
        FactionRelationMap relationMap)
    {
        if (string.IsNullOrEmpty(myFaction) || string.IsNullOrEmpty(targetFaction) || myFaction == targetFaction)
            return DiplomacyResult.InvalidFaction;

        if (engine.AreAtWar(myFaction, targetFaction))
            return DiplomacyResult.AlreadyAtWar;

        int currentDay = engine.CurrentDay;

        // 1. 停战期校验
        if (relationMap.IsInTruce(myFaction, targetFaction, currentDay))
        {
            GD.Print($"[DiplomacyService] 宣战失败：{myFaction} 与 {targetFaction} 处于停战保护期内");
            return DiplomacyResult.InTruce;
        }

        // 2. 宣战冷却校验
        if (relationMap.IsDeclareWarInCooldown(myFaction, targetFaction, currentDay))
        {
            GD.Print($"[DiplomacyService] 宣战失败：{myFaction} 对 {targetFaction} 的宣战冷却中");
            return DiplomacyResult.InCooldown;
        }

        var config = DiplomacyBalanceConfig.Load();

        // 3. 关系校验 (要求当前关系 <= Threshold)
        int relation = relationMap.GetRelation(myFaction, targetFaction);
        if (relation > config.DeclareWarRelationThreshold)
        {
            GD.Print($"[DiplomacyService] 宣战失败：{myFaction} 与 {targetFaction} 关系为 {relation} (> {config.DeclareWarRelationThreshold})");
            return DiplomacyResult.RelationTooHigh;
        }

        // 4. 扣除影响力
        if (!engine.Influence.TrySpend(myFaction, config.DeclareWarInfluenceCost, $"向 {targetFaction} 宣战"))
        {
            return DiplomacyResult.InsufficientInfluence;
        }

        // 5. 建立战争状态
        var war = new WarState { NationA = myFaction, NationB = targetFaction, DaysSinceStart = 0 };
        engine.ActiveWars.Add(war);

        // 关系骤降
        relationMap.SetRelation(myFaction, targetFaction, config.DeclareWarRelationTarget);

        // 启动宣战冷却
        relationMap.SetDeclareWarCooldown(myFaction, targetFaction, config.DeclareWarCooldownDays, currentDay);

        // 清除 Truce 标记以防万一
        relationMap.ClearTruce(myFaction, targetFaction);

        // 记录新闻
        string myName = GetFactionDisplayName(myFaction, engine);
        string targetName = GetFactionDisplayName(targetFaction, engine);
        engine.AddNews("war_declared", $"由于关系恶化，{myName} 正式向 {targetName} 宣战！双方已进入全面战争状态！", Vector2.Zero);

        return DiplomacyResult.Success;
    }

    public static DiplomacyResult ProposePeace(
        string proposer, 
        string target, 
        WorldEventEngine engine, 
        FactionRelationMap relationMap,
        HeroRelationMatrix relations,
        List<OverworldEntity> entities,
        bool skipAiCheck = false)
    {
        if (string.IsNullOrEmpty(proposer) || string.IsNullOrEmpty(target) || proposer == target)
            return DiplomacyResult.InvalidFaction;

        var war = engine.ActiveWars.FirstOrDefault(w => 
            (w.NationA == proposer && w.NationB == target) || 
            (w.NationA == target && w.NationB == proposer));

        if (war == null)
            return DiplomacyResult.NotAtWar;

        int currentDay = engine.CurrentDay;

        // 1. 媾和冷却校验
        if (relationMap.IsProposePeaceInCooldown(proposer, target, currentDay))
        {
            return DiplomacyResult.InCooldown;
        }

        var config = DiplomacyBalanceConfig.Load();

        // 2. 扣除求和方影响力
        if (!engine.Influence.TrySpend(proposer, config.ProposePeaceInfluenceCost, $"与 {target} 议和"))
        {
            return DiplomacyResult.InsufficientInfluence;
        }

        // 3. AI 决策判定
        bool accept = skipAiCheck;
        if (!accept)
        {
            accept = EvaluatePeaceAcceptance(proposer, target, war.DaysSinceStart, relationMap, relations, entities);
        }

        if (!accept)
        {
            // 求和被拒，启动议和冷却
            relationMap.SetProposePeaceCooldown(proposer, target, config.ProposePeaceCooldownDays, currentDay);
            GD.Print($"[DiplomacyService] {target} 拒绝了 {proposer} 的媾和申请！");
            return DiplomacyResult.Failed;
        }

        // 4. 媾和成功
        engine.ActiveWars.Remove(war);

        // 关系缓和为 Target
        relationMap.SetRelation(proposer, target, config.ProposePeaceRelationTarget);

        // 建立停战 Truce
        relationMap.SetTruce(proposer, target, config.TruceDays, currentDay);

        // 启动双方宣战冷却
        relationMap.SetDeclareWarCooldown(proposer, target, config.DeclareWarCooldownDays, currentDay);
        relationMap.SetDeclareWarCooldown(target, proposer, config.DeclareWarCooldownDays, currentDay);

        // 记录新闻
        string proposerName = GetFactionDisplayName(proposer, engine);
        string targetName = GetFactionDisplayName(target, engine);
        engine.AddNews("peace", $"{proposerName} 与 {targetName} 达成了停战协议，烽火暂时熄灭。", Vector2.Zero);

        return DiplomacyResult.Success;
    }

    public static bool EvaluatePeaceAcceptance(
        string proposer, 
        string target, 
        int warDurationDays,
        FactionRelationMap relationMap,
        HeroRelationMatrix relations,
        List<OverworldEntity> entities)
    {
        var targetLords = entities.Where(e => 
            e.Faction == target && 
            e.IsNamedCharacter && 
            !string.IsNullOrEmpty(e.HeroId) &&
            e.IsAlive).ToList();

        string proposerLeaderHeroId = "player";
        if (proposer != "player")
        {
            var proposerLeader = entities.FirstOrDefault(e => 
                e.Faction == proposer && 
                e.IsNamedCharacter && 
                !string.IsNullOrEmpty(e.HeroId) &&
                e.IsAlive);
            if (proposerLeader != null)
                proposerLeaderHeroId = proposerLeader.HeroId;
        }

        int relationSum = 0;
        int relationCount = 0;

        foreach (var lord in targetLords)
        {
            if (lord.HeroId == proposerLeaderHeroId) continue;
            relationSum += relations.Get(lord.HeroId, proposerLeaderHeroId);
            relationCount++;
        }

        float avgRelation = relationCount > 0 ? (float)relationSum / relationCount : 0f;

        int factionRelation = relationMap.GetRelation(proposer, target);
        float weight = DiplomacyBalanceConfig.Load().PersonalRelationWeight;
        float effectiveRelation = factionRelation + avgRelation * weight;

        // 基础概率 20%，关系分加成（-100~100）每一分 +0.5%，战争持续天数每一天 +0.5%
        float chance = 20f + (effectiveRelation * 0.5f) + (warDurationDays * 0.5f);
        chance = Math.Clamp(chance, 0f, 100f);

        double roll = _rng.NextDouble() * 100.0;
        
        GD.Print($"[EvaluatePeaceAcceptance] {proposer} 与 {target} 媾和评估：平均领主关系={avgRelation}，Faction关系={factionRelation}，有效关系={effectiveRelation}，持续天数={warDurationDays}，接受概率={chance:F1}%，Roll={roll:F1}");

        return roll <= chance;
    }

    private static string GetFactionDisplayName(string factionId, WorldEventEngine engine)
    {
        if (factionId == "player") return "玩家王国";
        if (factionId == "neutral") return "中立";
        return factionId;
    }
}
