// TournamentService.cs
// 锦标赛服务 — 选手生成、赛程推进、AI解算
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Tournament;

/// <summary>
/// 锦标赛服务
/// </summary>
public class TournamentService
{
    private readonly HeroRegistry _heroRegistry;
    private readonly ReputationTracker _reputation;
    private readonly InfluenceTracker _influence;
    private readonly WorldEventEngine? _worldEngine;

    public TournamentService(
        HeroRegistry heroRegistry,
        ReputationTracker reputation,
        InfluenceTracker influence,
        WorldEventEngine? worldEngine = null)
    {
        _heroRegistry = heroRegistry;
        _reputation = reputation;
        _influence = influence;
        _worldEngine = worldEngine;
    }

    /// <summary>
    /// 创建新的锦标赛
    /// </summary>
    public TournamentBracket CreateTournament(string hostTownName, string hostNationId, int playerLevel)
    {
        var bracket = new TournamentBracket
        {
            HostTownName = hostTownName,
            HostNationId = hostNationId,
            EntryFee = 1000
        };

        // 生成7个AI对手
        var opponents = GenerateOpponents(playerLevel, 7);

        // 玩家固定占第1槽位
        var player = new TournamentParticipant
        {
            ParticipantId = "player",
            DisplayName = "玩家",
            Level = playerLevel,
            IsPlayer = true,
            RosterSize = 3
        };

        bracket.Participants.Add(player);
        bracket.Participants.AddRange(opponents);

        // 生成赛程
        bracket.GenerateBracket();

        return bracket;
    }

    /// <summary>
    /// 生成AI对手
    /// </summary>
    public List<TournamentParticipant> GenerateOpponents(int playerLevel, int count)
    {
        var opponents = new List<TournamentParticipant>();
        var usedHeroIds = new HashSet<string>();

        // 优先选择性格好斗的具名领主
        var aggressiveHeroes = _heroRegistry.AllHeroes
            .Where(h => h.Personality == OverworldPOI.LordPersonality.Aggressive || 
                       h.Personality == OverworldPOI.LordPersonality.Balanced)
            .Where(h => !usedHeroIds.Contains(h.HeroId))
            .Take(count / 2)
            .ToList();

        foreach (var hero in aggressiveHeroes)
        {
            usedHeroIds.Add(hero.HeroId);
            opponents.Add(new TournamentParticipant
            {
                ParticipantId = hero.HeroId,
                DisplayName = hero.DisplayName,
                Level = Math.Max(1, playerLevel + Random.Shared.Next(-2, 3)),
                HeroId = hero.HeroId,
                CombatPower = 10f + hero.Personality switch
                {
                    OverworldPOI.LordPersonality.Aggressive => 5f,
                    OverworldPOI.LordPersonality.Balanced => 0f,
                    _ => -5f
                },
                RosterSize = Random.Shared.Next(2, 5)
            });
        }

        // 不够则用随机精英补充
        while (opponents.Count < count)
        {
            int idx = opponents.Count;
            bool isFinals = (opponents.Count == count - 1); // 最后一个是决赛对手

            opponents.Add(new TournamentParticipant
            {
                ParticipantId = $"npc_arena_{idx}",
                DisplayName = GenerateRandomName(),
                Level = isFinals ? playerLevel + 5 : Math.Max(1, playerLevel + Random.Shared.Next(-2, 3)),
                CombatPower = 10f + (isFinals ? 20f : 0f),
                RosterSize = Random.Shared.Next(2, 5)
            });
        }

        return opponents;
    }

    /// <summary>
    /// 解算AI vs AI比赛 (headless)
    /// </summary>
    public void ResolveAiMatch(TournamentMatch match)
    {
        if (match.IsResolved || match.IsPlayerMatch) return;

        var a = match.ParticipantA!;
        var b = match.ParticipantB!;

        // 基于等级和战力的简单解算
        float powerA = a.CombatPower + a.Level * 2f;
        float powerB = b.CombatPower + b.Level * 2f;

        // 加入随机性
        float rollA = powerA * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);
        float rollB = powerB * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);

        match.Winner = rollA >= rollB ? a : b;
        match.IsResolved = true;
        (match.Winner == a ? b : a).IsEliminated = true;
    }

    /// <summary>
    /// 处理玩家比赛结果
    /// </summary>
    public void ResolvePlayerMatch(TournamentMatch match, bool playerWon)
    {
        if (match.IsResolved) return;

        var player = match.ParticipantA?.IsPlayer == true ? match.ParticipantA : match.ParticipantB;
        var opponent = match.ParticipantA?.IsPlayer == true ? match.ParticipantB : match.ParticipantA;

        if (playerWon)
        {
            match.Winner = player;
            opponent!.IsEliminated = true;
        }
        else
        {
            match.Winner = opponent;
            player!.IsEliminated = true;
        }

        match.IsResolved = true;
    }

    /// <summary>
    /// 推进轮次，解算所有AI比赛
    /// </summary>
    public TournamentMatch? AdvanceAndResolve(TournamentBracket bracket)
    {
        // 解算当前轮次所有AI比赛
        var currentMatches = bracket.GetCurrentRoundMatches();
        foreach (var match in currentMatches)
        {
            if (!match.IsResolved && !match.IsPlayerMatch)
            {
                ResolveAiMatch(match);
            }
        }

        // 检查是否有玩家比赛需要处理
        var playerMatch = bracket.GetPlayerMatch();
        if (playerMatch != null)
        {
            return playerMatch; // 返回给前端处理真实战斗
        }

        // 没有玩家比赛，推进到下一轮
        bracket.AdvanceRound();
        return null;
    }

    /// <summary>
    /// 颁发冠军奖励
    /// </summary>
    public void AwardChampion(TournamentBracket bracket)
    {
        if (string.IsNullOrEmpty(bracket.WinnerParticipantId)) return;

        bool isPlayerChampion = bracket.WinnerParticipantId == "player";

        if (isPlayerChampion)
        {
            // 玩家冠军
            _reputation.AddTitle(bracket.HostNationId, "竞技场冠军");
            _influence.Add("player", bracket.GetInfluenceReward(true), "竞技场冠军");

            // 该国全Town价格优惠 — 冠军 +30 声望直入 Friendly(GetPriceMultiplier=0.9)
            _reputation.AddReputation(bracket.HostNationId, 30);

            _worldEngine?.AddNews(
                "tournament_champion",
                $"🏆 传奇！玩家在{bracket.HostTownName}竞技场中力压群雄，夺得冠军头衔！获得5000金币和5点影响力！",
                new Godot.Vector2());
        }
        else
        {
            // NPC冠军
            var winner = bracket.Participants.FirstOrDefault(p => p.ParticipantId == bracket.WinnerParticipantId);
            if (winner != null)
            {
                _worldEngine?.AddNews(
                    "tournament_champion",
                    $"🏆 {winner.DisplayName}在{bracket.HostTownName}竞技场锦标赛中夺冠！",
                    new Godot.Vector2());
            }
        }
    }

    /// <summary>
    /// 获取轮次名称
    /// </summary>
    public static string GetRoundName(int round)
    {
        return round switch
        {
            0 => "1/4 决赛",
            1 => "半决赛",
            2 => "决赛",
            _ => "未知"
        };
    }

    private static string GenerateRandomName()
    {
        string[] firstNames = { "卡尔", "汉斯", "弗里茨", "奥托", "海因里希", "鲁道夫", "格哈德", "沃尔夫" };
        string[] lastNames = { "铁锤", "烈焰", "暗影", "风暴", "雷鸣", "碎骨", "斩击", "狂战士" };
        return $"{firstNames[Random.Shared.Next(firstNames.Length)]}·{lastNames[Random.Shared.Next(lastNames.Length)]}";
    }
}
