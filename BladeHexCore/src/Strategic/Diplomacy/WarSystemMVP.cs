using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Diplomacy;

/// <summary>
/// 战争与外交 MVP 系统入口 — 承载周度宣战审计与月度议和审计
/// </summary>
public static class WarSystemMVP
{
    private static readonly Random _rng = new();

    /// <summary>
    /// AI 主动向玩家发送求和申请的静态委托，用以解耦 Core 对 Frontend EventBus 的引用
    /// </summary>
    public static Action<string, int>? AiProposedPeaceToPlayer;

    /// <summary>
    /// 周度宣战检查 — 每 7 天运行一次，评估恶劣关系阵营是否爆发冲突
    /// </summary>
    public static void OnWeekEnd(
        int currentDay, 
        WorldEventEngine engine, 
        List<NationConfig> nations, 
        FactionRelationMap relationMap)
    {
        if (nations == null || nations.Count < 2) return;

        var config = DiplomacyBalanceConfig.Load();

        var factions = nations.Select(n => n.Id).ToList();
        if (!factions.Contains("player")) factions.Add("player");

        for (int i = 0; i < factions.Count; i++)
        {
            for (int j = i + 1; j < factions.Count; j++)
            {
                string fA = factions[i];
                string fB = factions[j];

                if (engine.AreAtWar(fA, fB) || engine.AreAllied(fA, fB)) continue;
                if (relationMap.IsInTruce(fA, fB, currentDay)) continue;
                if (relationMap.IsDeclareWarInCooldown(fA, fB, currentDay) || relationMap.IsDeclareWarInCooldown(fB, fA, currentDay)) continue;

                int rel = relationMap.GetRelation(fA, fB);
                if (rel <= config.DeclareWarRelationThreshold)
                {
                    if (_rng.NextDouble() <= config.WeeklyDeclareWarChance)
                    {
                        string attacker = _rng.Next(2) == 0 ? fA : fB;
                        string defender = attacker == fA ? fB : fA;

                        if (attacker != "player")
                        {
                            engine.Influence.Add(attacker, config.DeclareWarInfluenceCost, "AI 宣战审计自动增加");
                        }

                        var result = DiplomacyService.DeclareWar(attacker, defender, engine, relationMap);
                        if (result == DiplomacyResult.Success)
                        {
                            GD.Print($"[WarSystemMVP.OnWeekEnd] AI 宣战成功：{attacker} 向 {defender} 宣战！关系={rel}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 月度议和检查 — 每 30 天运行一次，评估已有的长期战争是否有可能达成和谈
    /// </summary>
    public static void OnMonthEnd(
        int currentDay, 
        WorldEventEngine engine, 
        FactionRelationMap relationMap, 
        HeroRelationMatrix relations, 
        List<OverworldEntity> entities)
    {
        if (engine.ActiveWars.Count == 0) return;

        var config = DiplomacyBalanceConfig.Load();

        foreach (var war in engine.ActiveWars.ToList())
        {
            if (war.DaysSinceStart >= 30)
            {
                if (_rng.NextDouble() <= config.MonthlyProposePeaceChance)
                {
                    string proposer = _rng.Next(2) == 0 ? war.NationA : war.NationB;
                    string target = proposer == war.NationA ? war.NationB : war.NationA;

                    if (target == "player")
                    {
                        AiProposedPeaceToPlayer?.Invoke(proposer, war.DaysSinceStart);
                        GD.Print($"[WarSystemMVP.OnMonthEnd] AI {proposer} 主动向玩家发送了媾和申请！");
                    }
                    else
                    {
                        if (proposer != "player")
                        {
                            engine.Influence.Add(proposer, config.ProposePeaceInfluenceCost, "AI 媾和审计自动增加");
                        }

                        var result = DiplomacyService.ProposePeace(proposer, target, engine, relationMap, relations, entities, skipAiCheck: false);
                        if (result == DiplomacyResult.Success)
                        {
                            GD.Print($"[WarSystemMVP.OnMonthEnd] AI 和谈成功：{proposer} 与 {target} 达成了停战协议！");
                        }
                    }
                }
            }
        }
    }
}
