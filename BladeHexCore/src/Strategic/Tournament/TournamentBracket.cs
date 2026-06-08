using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic.Tournament;

/// <summary>
/// 锦标赛参赛选手
/// </summary>
public class TournamentParticipant
{
    public string ParticipantId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Level { get; set; } = 1;
    public float CombatPower { get; set; } = 10f;
    public string HeroId { get; set; } = "";  // 关联 HeroData (若来自具名领主)
    public bool IsPlayer { get; set; } = false;
    public bool IsEliminated { get; set; } = false;
    public int RosterSize { get; set; } = 2;  // 出战队伍人数 (2-4人)
}

/// <summary>
/// 锦标赛单场比赛
/// </summary>
public class TournamentMatch
{
    public TournamentParticipant? ParticipantA { get; set; }
    public TournamentParticipant? ParticipantB { get; set; }
    public TournamentParticipant? Winner { get; set; }
    public bool IsResolved { get; set; } = false;
    public int Round { get; set; } = 0; // 0 = 1/4决赛, 1 = 半决赛, 2 = 决赛
    public bool IsPlayerMatch => (ParticipantA?.IsPlayer == true) || (ParticipantB?.IsPlayer == true);
}

/// <summary>
/// 锦标赛八强淘汰赛制
/// </summary>
public class TournamentBracket
{
    public List<TournamentParticipant> Participants { get; set; } = new(8);
    public List<TournamentMatch> Matches { get; set; } = new();
    public int CurrentRound { get; set; } = 0; // 0=1/4决赛, 1=半决赛, 2=决赛
    public string WinnerParticipantId { get; set; } = "";
    public bool IsPlayerEliminated { get; set; } = false;
    public bool IsComplete { get; set; } = false;
    public string HostTownName { get; set; } = "";
    public string HostNationId { get; set; } = "";
    public int EntryFee { get; set; } = 1000;

    /// <summary>
    /// 生成八强淘汰赛赛程
    /// </summary>
    public void GenerateBracket()
    {
        Matches.Clear();
        CurrentRound = 0;

        // 1/4决赛: 4场比赛
        for (int i = 0; i < 4; i++)
        {
            var match = new TournamentMatch
            {
                Round = 0,
                ParticipantA = Participants[i * 2],
                ParticipantB = Participants[i * 2 + 1]
            };
            Matches.Add(match);
        }
    }

    /// <summary>
    /// 获取当前轮次的所有比赛
    /// </summary>
    public List<TournamentMatch> GetCurrentRoundMatches()
    {
        return Matches.Where(m => m.Round == CurrentRound).ToList();
    }

    /// <summary>
    /// 获取玩家当前轮次的比赛
    /// </summary>
    public TournamentMatch? GetPlayerMatch()
    {
        return Matches.FirstOrDefault(m => m.Round == CurrentRound && m.IsPlayerMatch && !m.IsResolved);
    }

    /// <summary>
    /// 推进到下一轮
    /// </summary>
    public bool AdvanceRound()
    {
        var currentMatches = GetCurrentRoundMatches();
        if (currentMatches.Any(m => !m.IsResolved))
            return false; // 当前轮次未完成

        var winners = currentMatches.Where(m => m.Winner != null).Select(m => m.Winner!).ToList();
        if (winners.Count < 2)
        {
            // 锦标赛结束
            IsComplete = true;
            WinnerParticipantId = winners.FirstOrDefault()?.ParticipantId ?? "";
            return true;
        }

        CurrentRound++;

        // 生成下一轮比赛
        for (int i = 0; i < winners.Count; i += 2)
        {
            if (i + 1 < winners.Count)
            {
                var match = new TournamentMatch
                {
                    Round = CurrentRound,
                    ParticipantA = winners[i],
                    ParticipantB = winners[i + 1]
                };
                Matches.Add(match);
            }
        }

        return true;
    }

    /// <summary>
    /// 获取奖金 (根据轮次)
    /// </summary>
    public int GetPrizeForRound(int round)
    {
        return round switch
        {
            0 => 200,   // 1/4决赛
            1 => 1000,  // 半决赛
            2 => 5000,  // 决赛冠军
            _ => 0
        };
    }

    /// <summary>
    /// 获取亚军奖金
    /// </summary>
    public int GetRunnerUpPrize()
    {
        return 1500;
    }

    /// <summary>
    /// 获取影响力奖励
    /// </summary>
    public int GetInfluenceReward(bool isChampion)
    {
        return isChampion ? 5 : 3;
    }
}
