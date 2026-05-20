// SimulationHarness.cs
// Headless batch combat / AI simulation entry point. Driven by TerrainTestRunner
// when TEST_MODE=sim, with SIM_BATTLES / SIM_SEED / SIM_SCENARIO env vars.
//
// Today implements:
//   - Scenario "combat":      generate two random parties, run N battles via
//                             HeadlessCombatLoop, report win rate / dpr / round count.
//   - Scenario "overworld_ai": placeholder (waiting on dedicated harness).
//
// All randomness routed through CombatRandom so a fixed SIM_SEED yields a
// reproducible run.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BladeHex.Combat;
using BladeHex.Combat.Headless;
using BladeHex.Data;
using BladeHex.Strategic.Economy;
using Godot;

namespace BladeHex.Tests.Simulation;

public static class SimulationHarness
{
    public sealed class BatchResult
    {
        public string Scenario = "";
        public int Battles;
        public int Seed;
        public long ElapsedMs;
        public Dictionary<string, double> Metrics = new();
        public List<string> Notes = new();
    }

    public static (int passed, int failed, List<string> details) RunAll()
    {
        int battles  = ReadEnvInt("SIM_BATTLES", 100);
        int seed     = ReadEnvInt("SIM_SEED", 0);
        string scenario = OS.GetEnvironment("SIM_SCENARIO");
        if (string.IsNullOrEmpty(scenario)) scenario = "combat";

        var details = new List<string>
        {
            $"  scenario={scenario}, battles={battles}, seed={seed}",
        };

        BatchResult result;
        try
        {
            result = scenario.ToLowerInvariant() switch
            {
                "combat"       => RunCombatBatch(battles, seed),
                "combat_build" => RunBuildMatrixBatch(battles, seed),
                "combat_comp"  => RunCompositionMatrixBatch(battles, seed),
                "overworld_ai" => RunOverworldAiBatch(battles, seed),
                "world_gen"    => RunWorldGenBatch(battles, seed),
                "battle_scale" => RunBattleScaleBatch(battles, seed),
                "economy"      => RunEconomyBatch(battles, seed),
                _              => RunUnknown(scenario, battles, seed),
            };
        }
        catch (Exception ex)
        {
            details.Add($"  [FAIL] simulation threw: {ex.Message}");
            details.Add($"  {ex.StackTrace}");
            return (0, 1, details);
        }

        details.Add($"  elapsed={result.ElapsedMs}ms");
        foreach (var (k, v) in result.Metrics)
            details.Add($"    {k} = {v:F3}");
        foreach (var note in result.Notes)
            details.Add($"    note: {note}");

        return (1, 0, details);
    }

    // ========================================================================
    // Scenarios
    // ========================================================================

    private static BatchResult RunCombatBatch(int battles, int seed)
    {
        var sw = Stopwatch.StartNew();
        battles = Math.Max(1, battles);

        // One ambient deterministic random source covers all battles in this run.
        // Each battle inherits its slice of the stream, so a single seed reproduces
        // the entire batch.
        var rng = new SeededRandomSource(seed == 0 ? System.Environment.TickCount : seed);
        using var scope = CombatRandom.Use(rng);

        // Squad shape — caller-overridable via SIM_LEVEL / SIM_TEAM_SIZE env vars.
        int teamSize    = ReadEnvInt("SIM_TEAM_SIZE", 4);
        int playerLevel = ReadEnvInt("SIM_LEVEL", 5);
        int enemyLevel  = playerLevel; // mirror by default

        int playerWins = 0;
        int enemyWins = 0;
        int timedOut = 0;
        long totalRounds = 0;
        long totalPlayerDmg = 0;
        long totalEnemyDmg = 0;
        int totalPlayerAtkAttempt = 0;
        int totalPlayerAtkLand = 0;

        // Use C# Random independent from CombatRandom for per-battle
        // squad construction (so squad rolls don't burn through the deterministic
        // combat-stream).
        var squadRng = new Random(seed == 0 ? System.Environment.TickCount + 1 : seed + 1);

        for (int i = 0; i < battles; i++)
        {
            var player = BuildSquad("Player", isPlayer: true, level: playerLevel,
                                    teamSize: teamSize, rng: squadRng);
            var enemy  = BuildSquad("Enemy",  isPlayer: false, level: enemyLevel,
                                    teamSize: teamSize, rng: squadRng);

            var result = HeadlessCombatLoop.Run(player, enemy);

            if (result.TimedOut) timedOut++;
            else if (result.PlayerVictory) playerWins++;
            else enemyWins++;

            totalRounds            += result.RoundsElapsed;
            totalPlayerDmg         += result.PlayerDamageDealt;
            totalEnemyDmg          += result.EnemyDamageDealt;
            totalPlayerAtkAttempt  += result.PlayerAttacksAttempted;
            totalPlayerAtkLand     += result.PlayerAttacksLanded;
        }

        sw.Stop();
        return new BatchResult
        {
            Scenario = "combat",
            Battles = battles,
            Seed = seed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Metrics = new()
            {
                ["player_winrate"] = (double)playerWins / battles,
                ["enemy_winrate"]  = (double)enemyWins / battles,
                ["timeout_rate"]   = (double)timedOut / battles,
                ["avg_rounds"]     = (double)totalRounds / battles,
                ["avg_player_dmg"] = (double)totalPlayerDmg / battles,
                ["avg_enemy_dmg"]  = (double)totalEnemyDmg / battles,
                ["player_hit_rate"] = totalPlayerAtkAttempt == 0 ? 0
                                    : (double)totalPlayerAtkLand / totalPlayerAtkAttempt,
            },
            Notes = new()
            {
                $"team_size={teamSize}, player_lv={playerLevel}, enemy_lv={enemyLevel}",
                "active rules: skill tree, charge, AoO, weapon mastery, LOS+cover (flat field)",
            },
        };
    }


    private static BatchResult RunEconomyBatch(int days, int seed)
    {
        var sw = Stopwatch.StartNew();
        days = Math.Max(7, days);
        int reward = ReadEnvInt("SIM_ECON_REWARD", 90);
        int foodPrice = ReadEnvInt("SIM_ECON_FOOD_PRICE", 4);

        var results = BladeHex.Strategic.Economy.EconomySimulation.RunDefaultProfiles(days, reward, foodPrice);
        sw.Stop();

        var metrics = new Dictionary<string, double>();
        foreach (var result in results)
        {
            string key = result.Name
                .Replace("/", "_")
                .Replace(" ", "_")
                .Replace("当前实现", "current")
                .Replace("计划目标", "target")
                .Replace("修正口粮", "food_fix")
                .Replace("成长压力", "growth");
            metrics[$"{key}_final_gold"] = result.FinalGold;
            metrics[$"{key}_net_gold_per_day"] = result.NetGoldPerDay;
            metrics[$"{key}_starved_days"] = result.StarvedDays;
        }

        return new BatchResult
        {
            Scenario = "economy",
            Battles = days,
            Seed = seed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Metrics = metrics,
            Notes = BladeHex.Strategic.Economy.EconomySimulation
                .FormatReport(results, reward)
                .Concat(BuildEconomyPriceNotes(reward, foodPrice))
                .ToList(),
        };
    }

    private static List<string> BuildEconomyPriceNotes(int reward, int foodPrice)
    {
        var anchor = BladeHex.Strategic.Economy.EconomySimulation.CreatePriceAnchorFromModel(reward, foodPrice);
        var evaluations = EquipmentPriceAnchorService.EvaluateAll(anchor);
        var notes = EquipmentPriceAnchorService.FormatPriceReport(evaluations);
        notes.Add("");
        notes.Add($"价格锚点：日均可支配金币={anchor.SustainableNetGoldPerDay:F1}，单委托周期可支配金币={anchor.DiscretionaryGoldPerQuest:F1}");
        notes.Add("=== 游戏内物品模拟价格表 ===");
        notes.AddRange(EquipmentPriceAnchorService.FormatFullPriceTable(evaluations, anchor));
        return notes;
    }

    // ========================================================================
    // Build matrix scenario — pit named class profiles (中文 ClassTitleResolver
    // 称号) against each other and produce a winrate matrix.
    //
    // Modes (set via SIM_BUILD_FILTER env var):
    //   "core"   : 6 单 + 8 代表性双  = 14 builds, 14×14×N = ~600+
    //   "single" : 6 单
    //   "double" : 15 双
    //   "triple" : 20 三
    //   "all"    : 全 63
    // 默认 "core"（最快也最有信号）
    // ========================================================================

    private static BatchResult RunBuildMatrixBatch(int battlesPerPair, int seed)
    {
        var sw = Stopwatch.StartNew();
        battlesPerPair = Math.Max(2, battlesPerPair);

        var rng = new SeededRandomSource(seed == 0 ? System.Environment.TickCount : seed);

        // SIM_DEBUG=1 → print first-battle log via HeadlessCombatLoop (also enables build-vs-build squad info dump)
        HeadlessCombatLoop.DebugFirstBattle = ReadEnvInt("SIM_DEBUG", 0) > 0;
        using var scope = CombatRandom.Use(rng);

        int teamSize    = ReadEnvInt("SIM_TEAM_SIZE", 4);
        int level       = ReadEnvInt("SIM_LEVEL", 30);
        string filter   = OS.GetEnvironment("SIM_BUILD_FILTER");
        if (string.IsNullOrEmpty(filter)) filter = "core";

        // SIM_ENABLE_SPELLS=1 → headless AI casts damage spells for INT-favored units
        HeadlessCombatLoop.EnableSpells = ReadEnvInt("SIM_ENABLE_SPELLS", 0) > 0;

        var profiles = SelectProfiles(filter);
        int n = profiles.Count;

        var wins = new int[n, n];
        var games = new int[n, n];
        var avgRounds = new double[n, n];

        var squadRng = new Random(seed == 0 ? System.Environment.TickCount + 1 : seed + 1);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int iWins = 0;
                long roundsTotal = 0;
                int gamesPlayed = 0;
                int half = battlesPerPair / 2;
                int otherHalf = battlesPerPair - half;

                // Phase A: profile i 在 player 位
                for (int b = 0; b < half; b++)
                {
                    var player = BuildProfileSquad("Player", isPlayer: true,
                        profile: profiles[i], level: level, teamSize: teamSize, rng: squadRng);
                    var enemy  = BuildProfileSquad("Enemy", isPlayer: false,
                        profile: profiles[j], level: level, teamSize: teamSize, rng: squadRng);
                    var r = HeadlessCombatLoop.Run(player, enemy);
                    gamesPlayed++;
                    if (!r.TimedOut && r.PlayerVictory) iWins++;
                    roundsTotal += r.RoundsElapsed;
                }
                // Phase B: profile j 在 player 位
                for (int b = 0; b < otherHalf; b++)
                {
                    var player = BuildProfileSquad("Player", isPlayer: true,
                        profile: profiles[j], level: level, teamSize: teamSize, rng: squadRng);
                    var enemy  = BuildProfileSquad("Enemy", isPlayer: false,
                        profile: profiles[i], level: level, teamSize: teamSize, rng: squadRng);
                    var r = HeadlessCombatLoop.Run(player, enemy);
                    gamesPlayed++;
                    if (!r.TimedOut && !r.PlayerVictory) iWins++;
                    roundsTotal += r.RoundsElapsed;
                }

                wins[i, j] = iWins;
                games[i, j] = gamesPlayed;
                avgRounds[i, j] = gamesPlayed == 0 ? 0 : (double)roundsTotal / gamesPlayed;
            }
        }

        sw.Stop();

        var notes = new List<string>();
        notes.Add($"team_size={teamSize}, level={level}, battles_per_pair={battlesPerPair}, filter={filter}, builds={n}");
        notes.Add("rows = build A, cols = build B, cells = A 胜率 % (双向跑后)");
        notes.Add("");

        // 表头：取每个职业名前 5 个汉字
        const int colWidth = 8;
        var hdr = new System.Text.StringBuilder();
        hdr.Append(new string(' ', colWidth + 2));
        for (int j = 0; j < n; j++) hdr.Append(PadDisplay(profiles[j].ChineseName, colWidth));
        hdr.Append("  | overall");
        notes.Add(hdr.ToString());

        var overallRates = new double[n];
        for (int i = 0; i < n; i++)
        {
            int totalWins = 0, totalGames = 0;
            var row = new System.Text.StringBuilder();
            row.Append(PadDisplay(profiles[i].ChineseName, colWidth + 2));
            for (int j = 0; j < n; j++)
            {
                double rate = games[i, j] == 0 ? 0 : (double)wins[i, j] / games[i, j];
                row.Append(PadRate(rate, colWidth));
                totalWins += wins[i, j];
                totalGames += games[i, j];
            }
            double overall = totalGames == 0 ? 0 : (double)totalWins / totalGames;
            overallRates[i] = overall;
            row.Append($"  | {overall * 100,6:F1}%");
            notes.Add(row.ToString());
        }

        // 排名
        notes.Add("");
        notes.Add("=== Build 强度排名（按整体胜率，已消除先手位置偏差）===");
        var ranking = new List<(int idx, double rate)>();
        for (int i = 0; i < n; i++) ranking.Add((i, overallRates[i]));
        ranking.Sort((a, b) => b.rate.CompareTo(a.rate));
        for (int rank = 0; rank < ranking.Count; rank++)
        {
            var (idx, rate) = ranking[rank];
            string flag = "";
            if (rate >= 0.70)      flag = " ⚠ OP";
            else if (rate >= 0.60) flag = " ↑ 偏强";
            else if (rate <= 0.30) flag = " ⚠ 弱";
            else if (rate <= 0.40) flag = " ↓ 偏弱";
            notes.Add($"  {rank + 1,2}. {profiles[idx].ChineseName,-12} {rate * 100,6:F1}%  ({string.Join("+", profiles[idx].TargetRegions).ToUpper()}){flag}");
        }

        var metrics = new Dictionary<string, double>();
        for (int i = 0; i < n; i++)
            metrics[$"winrate_{profiles[i].EnglishKey}"] = overallRates[i];

        return new BatchResult
        {
            Scenario = "combat_build",
            Battles = battlesPerPair * n * n,
            Seed = seed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Metrics = metrics,
            Notes = notes,
        };
    }

    private static List<BuildProfile> SelectProfiles(string filter)
    {
        var all = (List<BuildProfile>)BuildProfiles.All;
        return filter.ToLowerInvariant() switch
        {
            "single" => BuildProfiles.ByAttrCount(1),
            "double" => BuildProfiles.ByAttrCount(2),
            "triple" => BuildProfiles.ByAttrCount(3),
            "quad"   => BuildProfiles.ByAttrCount(4),
            "penta"  => BuildProfiles.ByAttrCount(5),
            "all"    => new List<BuildProfile>(all),
            "core"   => BuildCoreSet(),
            _        => BuildCoreSet(),
        };
    }

    /// <summary>核心代表性 build 集合：6 单 + 8 个有代表性的双/三 build。</summary>
    private static List<BuildProfile> BuildCoreSet()
    {
        var picked = new List<BuildProfile>();
        var byName = new Dictionary<string, BuildProfile>();
        foreach (var p in BuildProfiles.All) byName[p.ChineseName] = p;

        string[] names =
        {
            // 6 单
            "战士", "游侠", "守卫", "法师", "刺客", "诗人",
            // 8 代表性双 / 三
            "剑舞者", "重战士", "决斗家", "魔剑士", "战法师", "猎人", "术士", "武圣",
        };
        foreach (var n in names)
            if (byName.TryGetValue(n, out var p)) picked.Add(p);
        return picked;
    }

    /// <summary>把字符串按显示宽度（中文按 2 字符）填充到目标宽度。</summary>
    private static string PadDisplay(string s, int width)
    {
        int displayLen = 0;
        foreach (var ch in s)
            displayLen += ch >= 0x4E00 && ch <= 0x9FFF ? 2 : 1;
        int pad = width - displayLen;
        return pad > 0 ? s + new string(' ', pad) : s;
    }

    private static string PadRate(double rate, int width)
    {
        string s = $"{rate * 100,5:F1}%";
        return PadDisplay(s, width);
    }

    private static BattleSquad BuildProfileSquad(string sideName, bool isPlayer, BuildProfile profile,
        int level, int teamSize, Random rng)
    {
        var squad = new BattleSquad(sideName, isPlayer);
        float cr = RPGRuleEngine.GetCrFromLevel(level);
        int itemLevel = EquipmentGenerator.GetItemLevelFromCr(cr);
        string difficulty = EquipmentGenerator.GetDifficultyFromCr(cr);
        var sharedTreeData = _sharedTreeData ??= new BladeHex.Strategic.SkillTreeData();

        // Profile 的第一个 region = 主属性，决定装备偏好
        var pref = profile.TargetRegions.Length > 0
            ? RegionToPreference(profile.TargetRegions[0])
            : EquipmentGenerator.BuildPreference.None;

        for (int i = 0; i < teamSize; i++)
        {
            Godot.GD.Seed((ulong)rng.Next());
            var data = CharacterGenerator.GenerateCharacterWithWeights(
                profile.AttrWeights, level: level, seedVal: rng.Next());
            // 清掉 EquipStartingGear 的 starter 件，让 EquipFullSet 装等级对应装备
            data.Armor = null;
            data.Boots = null;
            data.PrimaryMainHand = null;
            data.Helmet = null;
            data.Accessory1 = null;
            EquipmentGenerator.EquipFullSet(data, itemLevel, difficulty, pref);

            // 强制按 build profile 加点：每个目标区域至少 1 个 BIG 节点
            // 让 ClassTitleResolver 能正确判定为对应职业
            var tree = new BladeHex.Strategic.CharacterSkillTree(sharedTreeData);
            tree.AddSkillPoint(data.SkillPoints);
            int jumpsCount = data.Level / 5;
            for (int k = 0; k < jumpsCount; k++) tree.RegisterJump();
            tree.AiAllocatePointsMultiRegion(profile.TargetRegions, bigNodesPerRegion: 1);

            int row = i;
            int col = isPlayer ? 0 : 10;
            var pos = new Vector2I(col, row);
            var model = new BattleUnitModel(data);
            model.Runtime.SkillTree = tree;
            squad.AddUnit(model, pos);
        }
        return squad;
    }

    private static EquipmentGenerator.BuildPreference RegionToPreference(string region)
    {
        return region switch
        {
            "str"   => EquipmentGenerator.BuildPreference.Str,
            "dex"   => EquipmentGenerator.BuildPreference.Dex,
            "con"   => EquipmentGenerator.BuildPreference.Con,
            "int"   => EquipmentGenerator.BuildPreference.Int,
            "intel" => EquipmentGenerator.BuildPreference.Int,
            "wis"   => EquipmentGenerator.BuildPreference.Wis,
            "cha"   => EquipmentGenerator.BuildPreference.Cha,
            _       => EquipmentGenerator.BuildPreference.None,
        };
    }

    // ========================================================================
    // 阵容矩阵 — 8 个固定阵容互掐，反映"队伍 vs 队伍"的战术配合
    // 而非单 build 互掐。
    // ========================================================================

    private static BatchResult RunCompositionMatrixBatch(int battlesPerPair, int seed)
    {
        var sw = Stopwatch.StartNew();
        battlesPerPair = Math.Max(2, battlesPerPair);

        var rng = new SeededRandomSource(seed == 0 ? System.Environment.TickCount : seed);
        using var scope = CombatRandom.Use(rng);

        // SIM_ENABLE_SPELLS=1 → headless AI casts damage spells for INT-favored units
        HeadlessCombatLoop.EnableSpells = ReadEnvInt("SIM_ENABLE_SPELLS", 0) > 0;
        // SIM_DEBUG=1 → print first battle's combat log
        HeadlessCombatLoop.DebugFirstBattle = ReadEnvInt("SIM_DEBUG", 0) > 0;

        int level = ReadEnvInt("SIM_LEVEL", 30);
        var comps = (List<CompositionTemplate>)CompositionTemplates.All;
        int n = comps.Count;

        var wins = new int[n, n];
        var games = new int[n, n];
        var avgRounds = new double[n, n];

        var squadRng = new Random(seed == 0 ? System.Environment.TickCount + 1 : seed + 1);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                int iWins = 0;
                long roundsTotal = 0;
                int gamesPlayed = 0;
                int half = battlesPerPair / 2;
                int otherHalf = battlesPerPair - half;

                // Phase A: comp i 在 player 位
                for (int b = 0; b < half; b++)
                {
                    var player = BuildCompositionSquad("Player", isPlayer: true, comp: comps[i],
                                                       level: level, rng: squadRng);
                    var enemy  = BuildCompositionSquad("Enemy",  isPlayer: false, comp: comps[j],
                                                       level: level, rng: squadRng);
                    var r = HeadlessCombatLoop.Run(player, enemy);
                    gamesPlayed++;
                    if (!r.TimedOut && r.PlayerVictory) iWins++;
                    roundsTotal += r.RoundsElapsed;
                }
                // Phase B: comp j 在 player 位
                for (int b = 0; b < otherHalf; b++)
                {
                    var player = BuildCompositionSquad("Player", isPlayer: true, comp: comps[j],
                                                       level: level, rng: squadRng);
                    var enemy  = BuildCompositionSquad("Enemy",  isPlayer: false, comp: comps[i],
                                                       level: level, rng: squadRng);
                    var r = HeadlessCombatLoop.Run(player, enemy);
                    gamesPlayed++;
                    if (!r.TimedOut && !r.PlayerVictory) iWins++;
                    roundsTotal += r.RoundsElapsed;
                }

                wins[i, j] = iWins;
                games[i, j] = gamesPlayed;
                avgRounds[i, j] = gamesPlayed == 0 ? 0 : (double)roundsTotal / gamesPlayed;
            }
        }

        sw.Stop();

        var notes = new List<string>();
        notes.Add($"level={level}, battles_per_pair={battlesPerPair}, comps={n}, spells={HeadlessCombatLoop.EnableSpells}");
        notes.Add("rows = comp A, cols = comp B, cells = A 胜率 % (双向跑后)");
        notes.Add("");

        // 表头
        const int colWidth = 9;
        var hdr = new System.Text.StringBuilder();
        hdr.Append(new string(' ', colWidth + 2));
        for (int j = 0; j < n; j++) hdr.Append(PadDisplay(comps[j].ChineseName, colWidth));
        hdr.Append("  | overall");
        notes.Add(hdr.ToString());

        var overallRates = new double[n];
        for (int i = 0; i < n; i++)
        {
            int totalWins = 0, totalGames = 0;
            var row = new System.Text.StringBuilder();
            row.Append(PadDisplay(comps[i].ChineseName, colWidth + 2));
            for (int j = 0; j < n; j++)
            {
                double rate = games[i, j] == 0 ? 0 : (double)wins[i, j] / games[i, j];
                row.Append(PadRate(rate, colWidth));
                totalWins += wins[i, j];
                totalGames += games[i, j];
            }
            double overall = totalGames == 0 ? 0 : (double)totalWins / totalGames;
            overallRates[i] = overall;
            row.Append($"  | {overall * 100,6:F1}%");
            notes.Add(row.ToString());
        }

        notes.Add("");
        notes.Add("=== 阵容强度排名 ===");
        var ranking = new List<(int idx, double rate)>();
        for (int i = 0; i < n; i++) ranking.Add((i, overallRates[i]));
        ranking.Sort((a, b) => b.rate.CompareTo(a.rate));
        for (int rank = 0; rank < ranking.Count; rank++)
        {
            var (idx, rate) = ranking[rank];
            string flag = "";
            if (rate >= 0.70)      flag = " ⚠ OP";
            else if (rate >= 0.60) flag = " ↑ 偏强";
            else if (rate <= 0.30) flag = " ⚠ 弱";
            else if (rate <= 0.40) flag = " ↓ 偏弱";
            notes.Add($"  {rank + 1}. {comps[idx].ChineseName,-10} {rate * 100,6:F1}%  ({comps[idx].Tag}){flag}");
        }

        // 平均回合数
        notes.Add("");
        notes.Add("=== 平均回合数（TTK）===");
        var hdr2 = new System.Text.StringBuilder();
        hdr2.Append(new string(' ', colWidth + 2));
        for (int j = 0; j < n; j++) hdr2.Append(PadDisplay(comps[j].ChineseName, colWidth));
        notes.Add(hdr2.ToString());
        for (int i = 0; i < n; i++)
        {
            var row = new System.Text.StringBuilder();
            row.Append(PadDisplay(comps[i].ChineseName, colWidth + 2));
            for (int j = 0; j < n; j++) row.Append($"{avgRounds[i, j],8:F1} ");
            notes.Add(row.ToString());
        }

        var metrics = new Dictionary<string, double>();
        for (int i = 0; i < n; i++) metrics[$"winrate_{comps[i].Tag}"] = overallRates[i];

        return new BatchResult
        {
            Scenario = "combat_comp",
            Battles = battlesPerPair * n * n,
            Seed = seed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Metrics = metrics,
            Notes = notes,
        };
    }

    /// <summary>
    /// 用 CompositionTemplate 的 4 个 build + col 摆位生成阵容。每个 member 的
    /// FrontalCol 0..3 映射到实际坐标:
    ///   player 方: x = FrontalCol（0=最前，3=最后排）
    ///   enemy  方: x = 10 - FrontalCol（10=最前对玩家，7=最后）
    /// row 自动错开避免重叠。
    /// </summary>
    private static BattleSquad BuildCompositionSquad(string sideName, bool isPlayer,
        CompositionTemplate comp, int level, Random rng)
    {
        var squad = new BattleSquad(sideName, isPlayer);
        float cr = RPGRuleEngine.GetCrFromLevel(level);
        int itemLevel = EquipmentGenerator.GetItemLevelFromCr(cr);
        string difficulty = EquipmentGenerator.GetDifficultyFromCr(cr);
        var sharedTreeData = _sharedTreeData ??= new BladeHex.Strategic.SkillTreeData();

        // 把 build 名映射到 BuildProfile
        var profileMap = new Dictionary<string, BuildProfile>();
        foreach (var bp in BuildProfiles.All) profileMap[bp.ChineseName] = bp;

        // 同一 col 上的 row 错开
        var colRowCounter = new Dictionary<int, int>();

        foreach (var member in comp.Members)
        {
            if (!profileMap.TryGetValue(member.BuildName, out var profile))
            {
                throw new InvalidOperationException($"Build profile '{member.BuildName}' not found");
            }

            Godot.GD.Seed((ulong)rng.Next());
            var data = CharacterGenerator.GenerateCharacterWithWeights(
                profile.AttrWeights, level: level, seedVal: rng.Next());

            // EquipStartingGear 装了 "旅行布衣 / starter_cloth_armor"。
            // 我们要让 EquipFullSet 装上等级对应的装备，所以先清掉 starter 件。
            data.Armor = null;
            data.Boots = null;
            data.PrimaryMainHand = null;
            data.Helmet = null;
            data.Accessory1 = null;

            var pref = profile.TargetRegions.Length > 0
                ? RegionToPreference(profile.TargetRegions[0])
                : EquipmentGenerator.BuildPreference.None;
            EquipmentGenerator.EquipFullSet(data, itemLevel, difficulty, pref);

            var tree = new BladeHex.Strategic.CharacterSkillTree(sharedTreeData);
            tree.AddSkillPoint(data.SkillPoints);
            int jumpsCount = data.Level / 5;
            for (int k = 0; k < jumpsCount; k++) tree.RegisterJump();
            tree.AiAllocatePointsMultiRegion(profile.TargetRegions, bigNodesPerRegion: 1);

            int col = isPlayer ? member.FrontalCol : 10 - member.FrontalCol;
            int row;
            if (!colRowCounter.TryGetValue(col, out row)) row = 0;
            colRowCounter[col] = row + 1;
            var pos = new Vector2I(col, row);

            var model = new BattleUnitModel(data);
            model.Runtime.SkillTree = tree;
            squad.AddUnit(model, pos);
        }
        return squad;
    }

    private static BatchResult RunOverworldAiBatch(int battles, int seed)
    {
        var sw = Stopwatch.StartNew();
        sw.Stop();
        return new BatchResult
        {
            Scenario = "overworld_ai",
            Battles = battles,
            Seed = seed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Notes = new() { "not implemented; waiting on a dedicated harness." },
        };
    }

    /// <summary>
    /// 世界生成测试：跑 N 个不同种子的世界，统计地形分布。
    /// SIM_BATTLES = 测试种子数量
    /// SIM_SEED = 起始种子（每次 +1）
    /// SIM_LEVEL = 世界尺寸 (0=Small, 1=Medium, 2=Large, 3=Mega)
    /// </summary>
    private static BatchResult RunWorldGenBatch(int seedsToTest, int startSeed)
    {
        var sw = Stopwatch.StartNew();
        seedsToTest = Math.Max(1, seedsToTest);
        int sizeOpt = ReadEnvInt("SIM_LEVEL", 1);
        var size = sizeOpt switch
        {
            0 => BladeHex.Strategic.WorldCreationConfig.WorldSize.Small,
            2 => BladeHex.Strategic.WorldCreationConfig.WorldSize.Large,
            3 => BladeHex.Strategic.WorldCreationConfig.WorldSize.Mega,
            _ => BladeHex.Strategic.WorldCreationConfig.WorldSize.Medium,
        };

        var aggregate = new Dictionary<BladeHex.Map.HexOverworldTile.TerrainType, long>();
        // 每地形的连通分量数 + 大小列表（用于聚集度统计）
        var clusterStats = new Dictionary<BladeHex.Map.HexOverworldTile.TerrainType, (long compCount, long maxSize, long sumSize, long isolatedCount)>();
        long totalTiles = 0;
        var notes = new List<string>();

        for (int i = 0; i < seedsToTest; i++)
        {
            int seed = startSeed == 0
                ? System.Environment.TickCount + i * 1000003
                : startSeed + i;

            var config = BladeHex.Strategic.WorldCreationConfig.Create(size, seed);

            // 用完整 WorldPipeline，包含 TerrainSmoothingStage 等后处理
            var pipeline = BladeHex.Strategic.WorldGen.WorldPipeline.Default();
            var worldData = pipeline.Build(seed, config, null);

            var seedCount = new Dictionary<BladeHex.Map.HexOverworldTile.TerrainType, long>();
            long seedTotal = 0;
            var allTiles = new Dictionary<Vector2I, BladeHex.Map.HexOverworldTile.TerrainType>();

            foreach (var chunk in worldData.Chunks.Values)
            {
                foreach (var tile in chunk.Tiles.Values)
                {
                    allTiles[tile.Coord] = tile.Terrain;
                    if (!seedCount.ContainsKey(tile.Terrain))
                        seedCount[tile.Terrain] = 0;
                    seedCount[tile.Terrain]++;
                    seedTotal++;

                    if (!aggregate.ContainsKey(tile.Terrain))
                        aggregate[tile.Terrain] = 0;
                    aggregate[tile.Terrain]++;

                    // 分类 Sand：海滩 (elev < 0.31) vs 真内陆沙漠
                    if (tile.Terrain == BladeHex.Map.HexOverworldTile.TerrainType.Sand)
                    {
                        if (tile.Elevation < BladeHex.Map.BiomeRules.BeachLevel) _coastalSandCount++;
                        else _inlandSandCount++;
                    }
                }
            }

            totalTiles += seedTotal;

            // 连通分量分析（按地形类型分别 BFS）
            var visited = new HashSet<Vector2I>();
            foreach (var (coord, terrain) in allTiles)
            {
                if (visited.Contains(coord)) continue;

                // BFS 同地形连通分量
                var queue = new Queue<Vector2I>();
                queue.Enqueue(coord);
                visited.Add(coord);
                long compSize = 0;

                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    compSize++;
                    foreach (var nb in HexNeighbors(c))
                    {
                        if (visited.Contains(nb)) continue;
                        if (!allTiles.TryGetValue(nb, out var nbTerr)) continue;
                        if (nbTerr != terrain) continue;
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }

                if (!clusterStats.ContainsKey(terrain))
                    clusterStats[terrain] = (0, 0, 0, 0);
                var s = clusterStats[terrain];
                s.compCount++;
                s.sumSize += compSize;
                if (compSize > s.maxSize) s.maxSize = compSize;
                if (compSize <= 3) s.isolatedCount++; // 孤立小块（≤3 格）
                clusterStats[terrain] = s;
            }

            // 单种子摘要
            var topThree = new List<string>();
            foreach (var kvp in seedCount.OrderByDescending(k => k.Value).Take(3))
                topThree.Add($"{kvp.Key}:{(double)kvp.Value / seedTotal * 100:F1}%");
            notes.Add($"seed={seed}: top3 = {string.Join(", ", topThree)}");

            // 收集水体（DeepWater + ShallowWater + River 一起 BFS）
            var waterVisited = new HashSet<Vector2I>();
            foreach (var (coord, terrain) in allTiles)
            {
                if (waterVisited.Contains(coord)) continue;
                if (!IsWater(terrain)) continue;

                var queue = new Queue<Vector2I>();
                queue.Enqueue(coord);
                waterVisited.Add(coord);
                long bodySize = 0;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    bodySize++;
                    foreach (var nb in HexNeighbors(c))
                    {
                        if (waterVisited.Contains(nb)) continue;
                        if (!allTiles.TryGetValue(nb, out var nbTerr)) continue;
                        if (!IsWater(nbTerr)) continue;
                        waterVisited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
                _waterBodySizes.Add(bodySize);
            }
            _seedCount++;

            // === Footprint 统计：本 seed 的 POI scale 分布 + fallback 数 ===
            int totalPois = 0;
            // POI 地形 / 水源邻接统计
            foreach (var poi in worldData.Pois)
            {
                if (poi == null) continue;
                totalPois++;
                var scale = poi.Scale;
                if (!_poiScaleCount.ContainsKey(scale)) _poiScaleCount[scale] = 0;
                _poiScaleCount[scale]++;

                // 取 preset 本应使用的 footprint 模板
                var preset = BladeHex.Strategic.POIBattlePresetRegistry.Resolve(poi);
                bool intendedSolo = preset.FootprintTemplate == "solo";
                bool actuallySolo = poi.FootprintTemplateName == "solo";

                if (actuallySolo && !intendedSolo)
                    _unintendedSoloFallback++;
                if (intendedSolo) _intendedSoloCount++;

                _footprintCellTotal += poi.OccupiedHexes.Length;

                if (!_poiTypeFootprintCount.ContainsKey(poi.PoiTypeEnum))
                    _poiTypeFootprintCount[poi.PoiTypeEnum] = 0;
                _poiTypeFootprintCount[poi.PoiTypeEnum]++;

                // 评估 POI 的"宜居指标"
                var centerCoord = poi.CenterHex;
                var ckCoord = BladeHex.Map.ChunkData.WorldToChunk(centerCoord.X, centerCoord.Y);
                if (worldData.Chunks.TryGetValue(ckCoord, out var ckChunk))
                {
                    var pTile = ckChunk.GetTile(centerCoord.X, centerCoord.Y);
                    if (pTile != null)
                    {
                        if (!_poiTerrainCount.ContainsKey(pTile.Terrain)) _poiTerrainCount[pTile.Terrain] = 0;
                        _poiTerrainCount[pTile.Terrain]++;

                        // 检查 1 圈邻居有无水源（河/海岸）
                        bool hasWater = false;
                        for (int d = 0; d < 6; d++)
                        {
                            var nb = BladeHex.Map.HexOverworldTile.GetNeighbor(centerCoord.X, centerCoord.Y, d);
                            var nbCk = BladeHex.Map.ChunkData.WorldToChunk(nb.X, nb.Y);
                            if (worldData.Chunks.TryGetValue(nbCk, out var nbChunk))
                            {
                                var nt = nbChunk.GetTile(nb.X, nb.Y);
                                if (nt != null && (nt.IsRiver
                                    || nt.Terrain == BladeHex.Map.HexOverworldTile.TerrainType.River
                                    || nt.Terrain == BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater))
                                { hasWater = true; break; }
                            }
                        }
                        if (hasWater) _poiNearWater++;
                    }
                }
            }
            _totalPoisAcross += totalPois;

            // === 收集本 seed 的河流瓦片（供后续 AnalyzeRivers 使用） ===
            var riverTiles = new HashSet<Vector2I>();
            int bridgeCount = 0;
            foreach (var (coord, terrain) in allTiles)
            {
                if (terrain == BladeHex.Map.HexOverworldTile.TerrainType.River) { riverTiles.Add(coord); continue; }
                // 也收集 IsRiver 但 Terrain 非 River 的（被河流标记但没显式覆盖 Terrain 的）
                var chunkCoord = BladeHex.Map.ChunkData.WorldToChunk(coord.X, coord.Y);
                if (worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
                {
                    var t = chunk.GetTile(coord.X, coord.Y);
                    if (t != null && t.IsRiver) riverTiles.Add(coord);
                    if (t != null && t.IsBridge) bridgeCount++;
                }
            }
            _riverTilesPerSeed.Add(riverTiles);
            _bridgeCountPerSeed.Add(bridgeCount);
        }

        sw.Stop();

        // 全局统计 → metrics
        var metrics = new Dictionary<string, double>();
        foreach (var kvp in aggregate.OrderByDescending(k => k.Value))
        {
            double pct = totalTiles > 0 ? (double)kvp.Value / totalTiles * 100.0 : 0.0;
            metrics[$"pct_{kvp.Key}"] = pct;
        }
        // 聚集度统计（仅显示占比 > 1% 的地形避免噪音）
        notes.Add("");
        notes.Add("=== 聚集度（同地形相邻分量分析） ===");
        notes.Add("地形 | 总格数 | 分量数 | 平均块大小 | 最大块 | 孤立块占比");
        foreach (var kvp in aggregate.OrderByDescending(k => k.Value))
        {
            if (kvp.Value < totalTiles * 0.005) continue; // 忽略 < 0.5%
            if (!clusterStats.TryGetValue(kvp.Key, out var stats)) continue;
            double avgSize = stats.compCount > 0 ? (double)stats.sumSize / stats.compCount : 0;
            double isolPct = stats.compCount > 0 ? (double)stats.isolatedCount / stats.compCount * 100.0 : 0;
            notes.Add($"{kvp.Key,-15} | {kvp.Value,7} | {stats.compCount,5} | {avgSize,8:F1} | {stats.maxSize,6} | {isolPct,5:F1}%");

            // 单地形指标也送进 metrics 供脚本消费
            metrics[$"avg_cluster_{kvp.Key}"] = avgSize;
            metrics[$"max_cluster_{kvp.Key}"] = stats.maxSize;
            metrics[$"isolated_pct_{kvp.Key}"] = isolPct;
        }

        // 水体专项分析：把所有水域（DeepWater+ShallowWater+River）合并做连通分析，分类大洋/内海/湖泊
        notes.Add("");
        notes.Add("=== 水体分类（合并 DeepWater + ShallowWater + River，按大小分类） ===");
        int seedCountSaved = _seedCount;
        AnalyzeWaterBodies(metrics, notes);

        // POI Footprint 统计
        notes.Add("");
        notes.Add("=== POI Footprint 统计（比例尺统一） ===");
        notes.Add($"  总 POI 数：{_totalPoisAcross}, 平均每种子 {(seedCountSaved > 0 ? _totalPoisAcross / (double)seedCountSaved : 0):F1}");
        notes.Add($"  平均每 POI 占用 hex：{(_totalPoisAcross > 0 ? _footprintCellTotal / (double)_totalPoisAcross : 0):F2}");
        int multiCellExpected = _totalPoisAcross - _intendedSoloCount;
        double unintendedFallbackPct = multiCellExpected > 0 ? 100.0 * _unintendedSoloFallback / multiCellExpected : 0;
        notes.Add($"  多格 POI 中 fallback 到 solo 的比例：{unintendedFallbackPct:F1}% ({_unintendedSoloFallback}/{multiCellExpected})");
        notes.Add($"  本应是 solo（Tiny 类）的 POI：{_intendedSoloCount} 个");
        notes.Add($"  Scale 分布：");
        foreach (var kvp in _poiScaleCount.OrderBy(k => k.Key))
            notes.Add($"    {kvp.Key,-8}: {kvp.Value,4} ({(_totalPoisAcross > 0 ? 100.0 * kvp.Value / _totalPoisAcross : 0):F1}%)");
        notes.Add($"  POIType 分布：");
        foreach (var kvp in _poiTypeFootprintCount.OrderByDescending(k => k.Value))
            notes.Add($"    {kvp.Key,-12}: {kvp.Value,4}");
        // POI 落点宜居指标
        notes.Add($"  POI 邻接水源比例（river / shallow water 1 邻）：" +
            $"{(_totalPoisAcross > 0 ? 100.0 * _poiNearWater / _totalPoisAcross : 0):F1}% ({_poiNearWater}/{_totalPoisAcross})");
        notes.Add($"  POI 落点地形分布（前 8）：");
        foreach (var kvp in _poiTerrainCount.OrderByDescending(k => k.Value).Take(8))
            notes.Add($"    {kvp.Key,-12}: {kvp.Value,4} ({(_totalPoisAcross > 0 ? 100.0 * kvp.Value / _totalPoisAcross : 0):F1}%)");
        metrics["unintended_solo_fallback_pct"] = unintendedFallbackPct;
        metrics["avg_footprint_cells"] = _totalPoisAcross > 0 ? _footprintCellTotal / (double)_totalPoisAcross : 0;
        metrics["poi_near_water_pct"] = _totalPoisAcross > 0 ? 100.0 * _poiNearWater / _totalPoisAcross : 0;

        // === 河流连续性检测 ===
        notes.Add("");
        notes.Add("=== 河流连续性 ===");
        AnalyzeRivers(metrics, notes);

        // === 沙漠分类（海滩 vs 内陆） ===
        if (_coastalSandCount > 0 || _inlandSandCount > 0)
        {
            int total = _coastalSandCount + _inlandSandCount;
            notes.Add("");
            notes.Add("=== Sand 分类 ===");
            notes.Add($"  海滩 Sand (elev < {BladeHex.Map.BiomeRules.BeachLevel:F2})：{_coastalSandCount} 格 ({100.0 * _coastalSandCount / total:F1}%)");
            notes.Add($"  内陆沙漠 (elev ≥ {BladeHex.Map.BiomeRules.BeachLevel:F2})：{_inlandSandCount} 格 ({100.0 * _inlandSandCount / total:F1}%)");
            metrics["sand_coastal_pct"] = 100.0 * _coastalSandCount / total;
            metrics["sand_inland_pct"] = 100.0 * _inlandSandCount / total;
            _coastalSandCount = 0;
            _inlandSandCount = 0;
        }

        // 重置 footprint 累加器
        _poiScaleCount.Clear();
        _poiTypeFootprintCount.Clear();
        _poiTerrainCount.Clear();
        _poiNearWater = 0;
        _unintendedSoloFallback = 0;
        _intendedSoloCount = 0;
        _footprintCellTotal = 0;
        _totalPoisAcross = 0;

        metrics["total_tiles"] = totalTiles;
        metrics["seeds_tested"] = seedsToTest;
        metrics["world_size"] = (int)size;

        return new BatchResult
        {
            Scenario = "world_gen",
            Battles = seedsToTest,
            Seed = startSeed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Metrics = metrics,
            Notes = notes,
        };
    }

    /// <summary>
    /// battle_scale scenario — 验证六边形战斗地图生成。
    /// 对每个 BattleSize 跑 N 次，统计 cell 数 / 地形分布 / 部署区可达性 / 水域占比。
    /// </summary>
    private static BatchResult RunBattleScaleBatch(int seedsPerSize, int startSeed)
    {
        var sw = Stopwatch.StartNew();
        var notes = new List<string>();
        var metrics = new Dictionary<string, double>();

        var sizes = new[]
        {
            BladeHex.Strategic.BattleContext.BattleSize.Mercenary,
            BladeHex.Strategic.BattleContext.BattleSize.Knight,
            BladeHex.Strategic.BattleContext.BattleSize.Lord,
            BladeHex.Strategic.BattleContext.BattleSize.Stronghold,
        };
        var presets = new[]
        {
            "plain_field",
            "forest_ambush",
            "mountain_pass",
            "swamp_battle",
            "village_defense",
            "town_defense",
            "castle_siege",
            "minotaur_stronghold",
            "pirate_cove",
        };

        notes.Add($"hex shape enabled = {BladeHex.Map.BattleMapGenerator.UseHexagonalShape}");
        notes.Add($"sizes × presets × seeds = {sizes.Length} × {presets.Length} × {seedsPerSize}");
        notes.Add("");
        notes.Add("=== 各档战斗地图统计 ===");
        notes.Add("Size       | N | expected | actual_avg | diversity | dpl_player | dpl_enemy | water%");

        var generator = new BladeHex.Map.BattleMapGenerator();
        foreach (var size in sizes)
        {
            int totalCells = 0;
            int totalDeployablePlayer = 0;
            int totalDeployableEnemy = 0;
            int totalWaterCells = 0;
            int totalRuns = 0;
            var terrainCount = new Dictionary<BladeHex.Data.BattleCellData.TerrainType, int>();

            foreach (var preset in presets)
            {
                for (int s = 0; s < seedsPerSize; s++)
                {
                    int seed = startSeed + s + (int)size * 1000;
                    var bs = (BladeHex.Map.BattleMapGenerator.BattleSize)(int)size;
                    var md = generator.GenerateFromTemplate(preset, bs, seed);

                    totalCells += md.Cells.Count;
                    totalDeployablePlayer += md.PlayerDeployment.Count;
                    totalDeployableEnemy += md.EnemyDeployment.Count;
                    totalRuns++;

                    foreach (var v in md.Cells.Values)
                    {
                        var cd = v.As<BladeHex.Data.BattleCellData>();
                        if (cd == null) continue;
                        if (!terrainCount.ContainsKey(cd.terrainType)) terrainCount[cd.terrainType] = 0;
                        terrainCount[cd.terrainType]++;
                        if (cd.terrainType == BladeHex.Data.BattleCellData.TerrainType.ShallowWater
                            || cd.terrainType == BladeHex.Data.BattleCellData.TerrainType.DeepWater)
                            totalWaterCells++;
                    }
                }
            }

            int expected = BladeHex.Strategic.POIScaleTable.BattleCellCount(
                size switch
                {
                    BladeHex.Strategic.BattleContext.BattleSize.Mercenary => BladeHex.Strategic.POIScale.Tiny,
                    BladeHex.Strategic.BattleContext.BattleSize.Knight    => BladeHex.Strategic.POIScale.Small,
                    BladeHex.Strategic.BattleContext.BattleSize.Lord      => BladeHex.Strategic.POIScale.Medium,
                    BladeHex.Strategic.BattleContext.BattleSize.Stronghold => BladeHex.Strategic.POIScale.Large,
                    _ => BladeHex.Strategic.POIScale.Tiny,
                });
            double avgCells = totalRuns > 0 ? (double)totalCells / totalRuns : 0;
            double avgPlayer = totalRuns > 0 ? (double)totalDeployablePlayer / totalRuns : 0;
            double avgEnemy = totalRuns > 0 ? (double)totalDeployableEnemy / totalRuns : 0;
            double waterPct = totalCells > 0 ? 100.0 * totalWaterCells / totalCells : 0;
            int diversity = terrainCount.Count;

            int n = BladeHex.Map.BattleMapGenerator.UseHexagonalShape
                ? size switch
                {
                    BladeHex.Strategic.BattleContext.BattleSize.Mercenary => 7,
                    BladeHex.Strategic.BattleContext.BattleSize.Knight => 8,
                    BladeHex.Strategic.BattleContext.BattleSize.Lord => 11,
                    BladeHex.Strategic.BattleContext.BattleSize.Stronghold => 14,
                    _ => 7,
                }
                : 0;

            notes.Add($"{size,-10} | {n,2} | {expected,8} | {avgCells,10:F0} | {diversity,9} | {avgPlayer,10:F1} | {avgEnemy,9:F1} | {waterPct,5:F1}%");
            metrics[$"avg_cells_{size}"] = avgCells;
            metrics[$"avg_player_deploy_{size}"] = avgPlayer;
            metrics[$"avg_enemy_deploy_{size}"] = avgEnemy;
            metrics[$"terrain_diversity_{size}"] = diversity;
            metrics[$"water_pct_{size}"] = waterPct;
        }

        // 同时检查 GenerateFromOverworld 路径（模拟 POI 战斗）
        notes.Add("");
        notes.Add("=== 端到端：GenerateFromOverworld（含 footprint 投影） ===");
        notes.Add("场景            | sample 类型           | water% | diversity | seeds");
        RunOverworldBattleScenario(notes, metrics, "全平原",          "all_plains",   seedsPerSize, startSeed);
        RunOverworldBattleScenario(notes, metrics, "海岸 1 水 sample", "coast_1water", seedsPerSize, startSeed);
        RunOverworldBattleScenario(notes, metrics, "港口 2 水 sample", "port_2water",  seedsPerSize, startSeed);
        RunOverworldBattleScenario(notes, metrics, "河流贯穿",          "river_line",   seedsPerSize, startSeed);
        RunOverworldBattleScenario(notes, metrics, "湖岸 3 水 sample", "lake_3water",  seedsPerSize, startSeed);

        sw.Stop();
        return new BatchResult
        {
            Scenario = "battle_scale",
            Battles = seedsPerSize,
            Seed = startSeed,
            ElapsedMs = sw.ElapsedMilliseconds,
            Metrics = metrics,
            Notes = notes,
        };
    }

    /// <summary>构造一个 8×8 的 overworld grid，按 scenarioId 改造其中部分 tile，跑 GenerateFromOverworld 并统计水域占比</summary>
    private static void RunOverworldBattleScenario(
        List<string> notes,
        Dictionary<string, double> metrics,
        string scenarioLabel,
        string scenarioId,
        int seedsPerScenario,
        int startSeed)
    {
        var generator = new BladeHex.Map.BattleMapGenerator();
        int totalCells = 0;
        int totalWater = 0;
        var terrainCount = new Dictionary<BladeHex.Data.BattleCellData.TerrainType, int>();

        for (int s = 0; s < seedsPerScenario; s++)
        {
            int seed = startSeed + s;

            // 构造 8×8 平原 grid（按 grid.Initialize 的 odd-r offset 公式遍历）
            var grid = new BladeHex.Map.HexOverworldGrid();
            grid.Initialize(8, 8);
            foreach (var t in grid.Tiles.Values)
                t.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.Plains);

            // 按 scenarioId 改造（center 选择保证 6 邻居都在 grid 内）
            var center = new Godot.Vector2I(3, 4);
            switch (scenarioId)
            {
                case "all_plains":
                    // 不改
                    break;
                case "coast_1water":
                    {
                        var t = grid.GetTile(center.X - 1, center.Y);
                        if (t != null) t.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater);
                    }
                    break;
                case "port_2water":
                    {
                        var t1 = grid.GetTile(center.X - 1, center.Y);
                        var t2 = grid.GetTile(center.X - 1, center.Y + 1);
                        if (t1 != null) t1.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater);
                        if (t2 != null) t2.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater);
                    }
                    break;
                case "river_line":
                    {
                        var t1 = grid.GetTile(center.X, center.Y + 1);
                        if (t1 != null) { t1.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.Plains); t1.IsRiver = true; }
                        var t2 = grid.GetTile(center.X + 1, center.Y);
                        if (t2 != null) { t2.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.Plains); t2.IsRiver = true; }
                    }
                    break;
                case "lake_3water":
                    {
                        var t1 = grid.GetTile(center.X - 1, center.Y);
                        var t2 = grid.GetTile(center.X - 1, center.Y + 1);
                        var t3 = grid.GetTile(center.X, center.Y + 1);
                        if (t1 != null) t1.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater);
                        if (t2 != null) t2.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater);
                        if (t3 != null) t3.SetTerrain(BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater);
                    }
                    break;
            }

            // 构造 BattleContext
            var ctx = new BladeHex.Strategic.BattleContext
            {
                OverworldGrid = grid,
                EncounterCoord = center,
                Size = BladeHex.Strategic.BattleContext.BattleSize.Mercenary,
                Seed = seed,
                Engagement = BladeHex.Strategic.BattleContext.EngagementType.Normal,
            };

            var md = generator.Generate(ctx);
            totalCells += md.Cells.Count;
            foreach (var v in md.Cells.Values)
            {
                var cd = v.As<BladeHex.Data.BattleCellData>();
                if (cd == null) continue;
                if (!terrainCount.ContainsKey(cd.terrainType)) terrainCount[cd.terrainType] = 0;
                terrainCount[cd.terrainType]++;
                if (cd.terrainType == BladeHex.Data.BattleCellData.TerrainType.ShallowWater
                    || cd.terrainType == BladeHex.Data.BattleCellData.TerrainType.DeepWater)
                    totalWater++;
            }
        }

        double waterPct = totalCells > 0 ? 100.0 * totalWater / totalCells : 0;
        notes.Add($"{scenarioLabel,-15} | {scenarioId,-20} | {waterPct,5:F1}% | {terrainCount.Count,9} | {seedsPerScenario}");
        metrics[$"overworld_water_pct_{scenarioId}"] = waterPct;
    }

    /// <summary>水体分类：把多 seed 累计的水体大小列表分桶</summary>
    private static List<long> _waterBodySizes = new();

    // === Footprint 统计累加器 ===
    private static readonly Dictionary<BladeHex.Strategic.POIScale, int> _poiScaleCount = new();
    private static readonly Dictionary<BladeHex.Strategic.OverworldPOI.POIType, int> _poiTypeFootprintCount = new();
    private static readonly Dictionary<BladeHex.Map.HexOverworldTile.TerrainType, int> _poiTerrainCount = new();
    private static int _poiNearWater = 0;
    private static int _unintendedSoloFallback = 0;
    private static int _intendedSoloCount = 0;
    private static int _footprintCellTotal = 0;
    private static int _totalPoisAcross = 0;

    // === 河流连续性累加器 ===
    private static readonly List<HashSet<Vector2I>> _riverTilesPerSeed = new();
    private static readonly List<int> _bridgeCountPerSeed = new();

    // === 沙漠分类累加器（海滩 vs 内陆）===
    private static int _coastalSandCount = 0;
    private static int _inlandSandCount = 0;

    private static void AnalyzeWaterBodies(Dictionary<string, double> metrics, List<string> notes)
    {
        if (_waterBodySizes.Count == 0)
        {
            notes.Add("  (no water body data collected)");
            return;
        }

        // 分类阈值（基于大小）
        // 大洋: > 50000 (Medium 世界总格数 ≈ 220k)
        // 内海: 5000 ~ 50000
        // 大湖: 500 ~ 5000
        // 小湖: 50 ~ 500
        // 池塘: < 50
        int oceans = 0, innerSeas = 0, bigLakes = 0, smallLakes = 0, ponds = 0;
        long oceanSum = 0, innerSeaSum = 0, bigLakeSum = 0, smallLakeSum = 0, pondSum = 0;

        foreach (var size in _waterBodySizes)
        {
            if (size > 50000) { oceans++; oceanSum += size; }
            else if (size > 5000) { innerSeas++; innerSeaSum += size; }
            else if (size > 500) { bigLakes++; bigLakeSum += size; }
            else if (size > 50) { smallLakes++; smallLakeSum += size; }
            else { ponds++; pondSum += size; }
        }

        long totalWater = oceanSum + innerSeaSum + bigLakeSum + smallLakeSum + pondSum;
        notes.Add($"  大洋 (>50000): {oceans,3} 个, 总计 {oceanSum,7} ({100.0 * oceanSum / Math.Max(1, totalWater):F1}%)");
        notes.Add($"  内海 (5k-50k): {innerSeas,3} 个, 总计 {innerSeaSum,7} ({100.0 * innerSeaSum / Math.Max(1, totalWater):F1}%)");
        notes.Add($"  大湖 (500-5k): {bigLakes,3} 个, 总计 {bigLakeSum,7} ({100.0 * bigLakeSum / Math.Max(1, totalWater):F1}%)");
        notes.Add($"  小湖 (50-500): {smallLakes,3} 个, 总计 {smallLakeSum,7} ({100.0 * smallLakeSum / Math.Max(1, totalWater):F1}%)");
        notes.Add($"  池塘 (< 50):   {ponds,3} 个, 总计 {pondSum,7} ({100.0 * pondSum / Math.Max(1, totalWater):F1}%)");

        // 每种子的平均
        if (_seedCount > 0)
        {
            notes.Add($"  每种子平均：大洋 {oceans / _seedCount:F1}, 内海 {innerSeas / _seedCount:F1}, 大湖 {bigLakes / _seedCount:F1}, 小湖 {smallLakes / _seedCount:F1}, 池塘 {ponds / _seedCount:F1}");
        }

        metrics["water_oceans"] = oceans;
        metrics["water_inner_seas"] = innerSeas;
        metrics["water_big_lakes"] = bigLakes;
        metrics["water_small_lakes"] = smallLakes;
        metrics["water_ponds"] = ponds;

        // 重置缓存供下次调用
        _waterBodySizes.Clear();
        _seedCount = 0;
    }

    /// <summary>
    /// 河流连续性分析：把每个 seed 的 IsRiver tiles 做 BFS 连通分量，
    /// 检测每条河流是否单格 (length=1)、平均长度、最长河流。
    /// </summary>
    private static void AnalyzeRivers(Dictionary<string, double> metrics, List<string> notes)
    {
        if (_riverTilesPerSeed.Count == 0)
        {
            notes.Add("  (no river data collected)");
            return;
        }

        int totalSeeds = _riverTilesPerSeed.Count;
        int totalRivers = 0;
        int totalRiverTiles = 0;
        int totalSingleTileRivers = 0;
        int longestRiver = 0;
        long sumOfLengths = 0;
        var lengthBuckets = new int[6]; // 1, 2-3, 4-7, 8-15, 16-31, 32+

        foreach (var riverTiles in _riverTilesPerSeed)
        {
            if (riverTiles.Count == 0) continue;
            totalRiverTiles += riverTiles.Count;
            var visited = new HashSet<Vector2I>();
            foreach (var start in riverTiles)
            {
                if (visited.Contains(start)) continue;
                var queue = new Queue<Vector2I>();
                queue.Enqueue(start);
                visited.Add(start);
                int compSize = 0;
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    compSize++;
                    foreach (var nb in HexNeighbors(cur))
                    {
                        if (visited.Contains(nb)) continue;
                        if (!riverTiles.Contains(nb)) continue;
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
                totalRivers++;
                sumOfLengths += compSize;
                if (compSize == 1) totalSingleTileRivers++;
                if (compSize > longestRiver) longestRiver = compSize;

                if (compSize == 1) lengthBuckets[0]++;
                else if (compSize <= 3) lengthBuckets[1]++;
                else if (compSize <= 7) lengthBuckets[2]++;
                else if (compSize <= 15) lengthBuckets[3]++;
                else if (compSize <= 31) lengthBuckets[4]++;
                else lengthBuckets[5]++;
            }
        }

        double avgLength = totalRivers > 0 ? (double)sumOfLengths / totalRivers : 0;
        double singleTilePct = totalRivers > 0 ? 100.0 * totalSingleTileRivers / totalRivers : 0;
        double riversPerSeed = totalSeeds > 0 ? (double)totalRivers / totalSeeds : 0;
        double tilesPerSeed = totalSeeds > 0 ? (double)totalRiverTiles / totalSeeds : 0;

        notes.Add($"  总连通河流数：{totalRivers}（每种子 {riversPerSeed:F1} 条）");
        notes.Add($"  总河流瓦片：{totalRiverTiles}（每种子 {tilesPerSeed:F1} 格）");
        notes.Add($"  平均河流长度：{avgLength:F1} 格，最长：{longestRiver} 格");
        notes.Add($"  单格断流（length=1）：{totalSingleTileRivers} 条 ({singleTilePct:F1}%)");
        notes.Add($"  长度分布：");
        notes.Add($"    1 格:    {lengthBuckets[0]}");
        notes.Add($"    2-3 格:  {lengthBuckets[1]}");
        notes.Add($"    4-7 格:  {lengthBuckets[2]}");
        notes.Add($"    8-15 格: {lengthBuckets[3]}");
        notes.Add($"    16-31 格:{lengthBuckets[4]}");
        notes.Add($"    32+ 格:  {lengthBuckets[5]}");

        // 模拟 RiverRenderer.TraceRiverSegments 的 segment 切分逻辑
        // 量化"端点-端点 / 端点-分叉点"段数与每段平均长度
        int totalSegments = 0;
        long totalSegmentLen = 0;
        int oneTileSegments = 0;
        var segLenBuckets = new int[6];
        foreach (var riverTiles in _riverTilesPerSeed)
        {
            var usedEdges = new HashSet<(Vector2I, Vector2I)>();
            var segments = TraceSegmentsForSim(riverTiles, usedEdges);
            foreach (var seg in segments)
            {
                totalSegments++;
                totalSegmentLen += seg.Count;
                if (seg.Count == 1) oneTileSegments++;
                if (seg.Count == 1) segLenBuckets[0]++;
                else if (seg.Count <= 3) segLenBuckets[1]++;
                else if (seg.Count <= 7) segLenBuckets[2]++;
                else if (seg.Count <= 15) segLenBuckets[3]++;
                else if (seg.Count <= 31) segLenBuckets[4]++;
                else segLenBuckets[5]++;
            }
        }
        double avgSegLen = totalSegments > 0 ? (double)totalSegmentLen / totalSegments : 0;
        double oneTileSegPct = totalSegments > 0 ? 100.0 * oneTileSegments / totalSegments : 0;
        notes.Add($"  渲染段（端-端/分叉）：{totalSegments}, 平均长度 {avgSegLen:F1} 格");
        notes.Add($"  单格 render segment：{oneTileSegments} ({oneTileSegPct:F1}%) ← 视觉断流元凶");
        notes.Add($"  渲染段长度分布：");
        notes.Add($"    1 格:    {segLenBuckets[0]}");
        notes.Add($"    2-3 格:  {segLenBuckets[1]}");
        notes.Add($"    4-7 格:  {segLenBuckets[2]}");
        notes.Add($"    8-15 格: {segLenBuckets[3]}");
        notes.Add($"    16-31 格:{segLenBuckets[4]}");
        notes.Add($"    32+ 格:  {segLenBuckets[5]}");

        metrics["river_count"] = totalRivers;
        metrics["river_tiles"] = totalRiverTiles;
        metrics["river_avg_length"] = avgLength;
        metrics["river_single_pct"] = singleTilePct;
        metrics["river_longest"] = longestRiver;
        metrics["river_render_segments"] = totalSegments;
        metrics["river_render_avg_seg_len"] = avgSegLen;
        metrics["river_render_one_tile_pct"] = oneTileSegPct;

        _riverTilesPerSeed.Clear();

        // 桥梁统计
        if (_bridgeCountPerSeed.Count > 0)
        {
            int totalBridges = 0;
            int seedsWithBridge = 0;
            int maxBridge = 0;
            foreach (var c in _bridgeCountPerSeed)
            {
                totalBridges += c;
                if (c > 0) seedsWithBridge++;
                if (c > maxBridge) maxBridge = c;
            }
            double avgBridge = (double)totalBridges / _bridgeCountPerSeed.Count;
            notes.Add($"  桥梁：每种子平均 {avgBridge:F1} 座，最多 {maxBridge} 座，{seedsWithBridge}/{_bridgeCountPerSeed.Count} 个种子有桥");
            // 桥分布
            notes.Add($"  桥数分布：[{string.Join(", ", _bridgeCountPerSeed)}]");
            metrics["bridge_avg_per_seed"] = avgBridge;
            metrics["bridge_max"] = maxBridge;
            _bridgeCountPerSeed.Clear();
        }
    }
    private static List<List<Vector2I>> TraceSegmentsForSim(
        HashSet<Vector2I> riverTiles,
        HashSet<(Vector2I, Vector2I)> usedEdges)
    {
        static int CountRiverNb(Vector2I c, HashSet<Vector2I> tiles)
        {
            int cnt = 0;
            foreach (var n in HexNeighbors(c)) if (tiles.Contains(n)) cnt++;
            return cnt;
        }
        static (Vector2I, Vector2I) EdgeKey(Vector2I a, Vector2I b)
        {
            int aH = a.X * 73856093 ^ a.Y * 19349663;
            int bH = b.X * 73856093 ^ b.Y * 19349663;
            return aH < bH ? (a, b) : (b, a);
        }

        var segments = new List<List<Vector2I>>();
        var endpoints = new List<Vector2I>();
        var junctions = new List<Vector2I>();
        foreach (var c in riverTiles)
        {
            int nb = CountRiverNb(c, riverTiles);
            if (nb == 1) endpoints.Add(c);
            else if (nb >= 3) junctions.Add(c);
        }

        void TraceFrom(Vector2I start)
        {
            foreach (var firstNb in HexNeighbors(start))
            {
                if (!riverTiles.Contains(firstNb)) continue;
                if (usedEdges.Contains(EdgeKey(start, firstNb))) continue;
                var seg = new List<Vector2I> { start, firstNb };
                usedEdges.Add(EdgeKey(start, firstNb));

                var prev = start;
                var current = firstNb;
                while (true)
                {
                    if (CountRiverNb(current, riverTiles) != 2) break;
                    Vector2I? next = null;
                    foreach (var n in HexNeighbors(current))
                    {
                        if (!riverTiles.Contains(n)) continue;
                        if (n == prev) continue;
                        if (usedEdges.Contains(EdgeKey(current, n))) continue;
                        next = n; break;
                    }
                    if (next == null) break;
                    seg.Add(next.Value);
                    usedEdges.Add(EdgeKey(current, next.Value));
                    prev = current; current = next.Value;
                }
                if (seg.Count >= 1) segments.Add(seg);
            }
        }

        foreach (var ep in endpoints) TraceFrom(ep);
        foreach (var j in junctions) TraceFrom(j);
        foreach (var c in riverTiles) TraceFrom(c);

        return segments;
    }

    private static int _seedCount = 0;

    private static bool IsWater(BladeHex.Map.HexOverworldTile.TerrainType t) =>
        t == BladeHex.Map.HexOverworldTile.TerrainType.DeepWater
        || t == BladeHex.Map.HexOverworldTile.TerrainType.ShallowWater
        || t == BladeHex.Map.HexOverworldTile.TerrainType.River;

    /// <summary>六边形 axial 坐标的 6 个邻居</summary>
    private static IEnumerable<Vector2I> HexNeighbors(Vector2I coord)
    {
        yield return new Vector2I(coord.X + 1, coord.Y);
        yield return new Vector2I(coord.X - 1, coord.Y);
        yield return new Vector2I(coord.X, coord.Y + 1);
        yield return new Vector2I(coord.X, coord.Y - 1);
        yield return new Vector2I(coord.X + 1, coord.Y - 1);
        yield return new Vector2I(coord.X - 1, coord.Y + 1);
    }

    private static BatchResult RunUnknown(string name, int battles, int seed)
    {
        return new BatchResult
        {
            Scenario = name,
            Battles = battles,
            Seed = seed,
            Notes = new() { $"unknown scenario={name}, returning empty result." },
        };
    }

    // ========================================================================
    // Squad construction
    // ========================================================================

    private static BattleSquad BuildSquad(string sideName, bool isPlayer, int level, int teamSize, Random rng)
    {
        var squad = new BattleSquad(sideName, isPlayer);
        float cr = RPGRuleEngine.GetCrFromLevel(level);
        int itemLevel = EquipmentGenerator.GetItemLevelFromCr(cr);
        string difficulty = EquipmentGenerator.GetDifficultyFromCr(cr);

        // Cache one shared skill-tree graph for the entire batch (cheap but
        // builds 184 nodes per call -- avoid one per unit).
        var sharedTreeData = _sharedTreeData ??= new BladeHex.Strategic.SkillTreeData();

        for (int i = 0; i < teamSize; i++)
        {
            // Reseed Godot's GD.* random per unit so character generation
            // (which still uses GD.Randi inside) ends up deterministic.
            Godot.GD.Seed((ulong)rng.Next());

            UnitData data = isPlayer
                ? CharacterGenerator.GenerateCharacter(level: level, seedVal: rng.Next())
                : CharacterGenerator.GenerateRandomEnemy(cr, UnitData.EnemyType.Humanoid);

            // 清掉 EquipStartingGear 的 starter 件，让 EquipFullSet 装等级对应装备
            data.Armor = null;
            data.Boots = null;
            data.PrimaryMainHand = null;
            data.Helmet = null;
            data.Accessory1 = null;

            // Full loadout: weapon + off-hand + armor + shield + helmet + boots + accessory.
            EquipmentGenerator.EquipFullSet(data, itemLevel, difficulty);

            // Build skill tree, auto-allocate based on primary attribute.
            var skillTree = BladeHex.Strategic.SkillTreeAllocator.AllocateForUnit(data, sharedTreeData);

            // Place player on left column, enemy on right column, ~10 tiles apart.
            int row = i;
            int col = isPlayer ? 0 : 10;
            var pos = new Vector2I(col, row);

            var model = new BattleUnitModel(data);
            model.Runtime.SkillTree = skillTree;
            squad.AddUnit(model, pos);
        }
        return squad;
    }

    /// <summary>One shared skill-tree graph for the duration of a sim batch.</summary>
    private static BladeHex.Strategic.SkillTreeData? _sharedTreeData;

    // ========================================================================
    // Helpers
    // ========================================================================

    private static int ReadEnvInt(string name, int defaultValue)
    {
        string raw = OS.GetEnvironment(name);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return int.TryParse(raw, out var n) ? n : defaultValue;
    }
}
