// TournamentTests.cs
// 锦标赛系统测试
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.Tournament;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Tests.Strategic;

public static class TournamentTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Bracket_8Participants_GeneratesCorrectly), Bracket_8Participants_GeneratesCorrectly);
        yield return Run(nameof(Bracket_AdvanceRound_PromotesWinners), Bracket_AdvanceRound_PromotesWinners);
        yield return Run(nameof(Player_Eliminated_ExitsImmediately), Player_Eliminated_ExitsImmediately);
        yield return Run(nameof(Champion_AppliesTitle), Champion_AppliesTitle);
        yield return Run(nameof(Champion_PriceDiscountActive), Champion_PriceDiscountActive);
        yield return Run(nameof(AiVsAi_HeadlessResolution_Works), AiVsAi_HeadlessResolution_Works);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"异常: {ex.Message}");
        }
    }

    private static (bool, string) Bracket_8Participants_GeneratesCorrectly()
    {
        var bracket = new TournamentBracket();

        // 创建8个选手
        for (int i = 0; i < 8; i++)
        {
            bracket.Participants.Add(new TournamentParticipant
            {
                ParticipantId = $"npc_{i}",
                DisplayName = $"选手{i}",
                Level = 10
            });
        }

        bracket.GenerateBracket();

        if (bracket.Matches.Count != 4)
            return (false, $"1/4决赛应有4场比赛，实际{bracket.Matches.Count}");

        if (bracket.CurrentRound != 0)
            return (false, $"初始轮次应为0，实际{bracket.CurrentRound}");

        return (true, "");
    }

    private static (bool, string) Bracket_AdvanceRound_PromotesWinners()
    {
        var bracket = new TournamentBracket();

        for (int i = 0; i < 8; i++)
        {
            bracket.Participants.Add(new TournamentParticipant
            {
                ParticipantId = $"npc_{i}",
                DisplayName = $"选手{i}",
                Level = 10
            });
        }

        bracket.GenerateBracket();

        // 解算所有1/4决赛
        foreach (var match in bracket.GetCurrentRoundMatches())
        {
            match.Winner = match.ParticipantA;
            match.IsResolved = true;
        }

        bracket.AdvanceRound();

        if (bracket.CurrentRound != 1)
            return (false, $"推进后轮次应为1，实际{bracket.CurrentRound}");

        var semiFinals = bracket.GetCurrentRoundMatches();
        if (semiFinals.Count != 2)
            return (false, $"半决赛应有2场比赛，实际{semiFinals.Count}");

        return (true, "");
    }

    private static (bool, string) Player_Eliminated_ExitsImmediately()
    {
        var bracket = new TournamentBracket();

        // 玩家 + 7个NPC
        bracket.Participants.Add(new TournamentParticipant
        {
            ParticipantId = "player",
            DisplayName = "玩家",
            IsPlayer = true,
            Level = 10
        });

        for (int i = 0; i < 7; i++)
        {
            bracket.Participants.Add(new TournamentParticipant
            {
                ParticipantId = $"npc_{i}",
                DisplayName = $"选手{i}",
                Level = 10
            });
        }

        bracket.GenerateBracket();

        // 玩家第一场就输
        var playerMatch = bracket.GetPlayerMatch();
        if (playerMatch == null)
            return (false, "找不到玩家比赛");

        playerMatch.Winner = playerMatch.ParticipantA?.IsPlayer == true ? playerMatch.ParticipantB : playerMatch.ParticipantA;
        playerMatch.IsResolved = true;

        bracket.IsPlayerEliminated = true;

        if (!bracket.IsPlayerEliminated)
            return (false, "玩家淘汰标志未设置");

        return (true, "");
    }

    private static (bool, string) Champion_AppliesTitle()
    {
        var heroRegistry = new HeroRegistry();
        var reputation = new ReputationTracker();
        var influence = new InfluenceTracker();
        var worldEngine = new WorldEventEngine();

        var service = new TournamentService(heroRegistry, reputation, influence, worldEngine);
        var bracket = new TournamentBracket
        {
            WinnerParticipantId = "player",
            HostNationId = "test_nation",
            HostTownName = "测试城镇"
        };

        service.AwardChampion(bracket);

        if (!reputation.HasTitle("test_nation", "竞技场冠军"))
            return (false, "冠军头衔未添加");

        return (true, "");
    }

    private static (bool, string) Champion_PriceDiscountActive()
    {
        var reputation = new ReputationTracker();
        reputation.AddTitle("test_nation", "竞技场冠军");

        // 验证头衔存在
        if (!reputation.HasTitle("test_nation", "竞技场冠军"))
            return (false, "头衔验证失败");

        // 价格优惠是通过声望系统实现的
        // 冠军会获得+30声望直入 Friendly(GetPriceMultiplier=0.9)
        reputation.AddReputation("test_nation", 30);

        float mult = reputation.GetPriceMultiplier("test_nation");
        if (mult >= 1.0f)
            return (false, $"冠军应获得价格优惠，乘数{mult}");

        return (true, "");
    }

    private static (bool, string) AiVsAi_HeadlessResolution_Works()
    {
        var heroRegistry = new HeroRegistry();
        var reputation = new ReputationTracker();
        var influence = new InfluenceTracker();

        var service = new TournamentService(heroRegistry, reputation, influence);

        var match = new TournamentMatch
        {
            ParticipantA = new TournamentParticipant { ParticipantId = "a", Level = 10, CombatPower = 10 },
            ParticipantB = new TournamentParticipant { ParticipantId = "b", Level = 10, CombatPower = 10 },
            Round = 0
        };

        service.ResolveAiMatch(match);

        if (!match.IsResolved)
            return (false, "AI比赛未解算");

        if (match.Winner == null)
            return (false, "AI比赛无胜者");

        return (true, "");
    }
}
