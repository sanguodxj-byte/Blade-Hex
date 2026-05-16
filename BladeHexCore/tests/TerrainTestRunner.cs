// TerrainTestRunner.cs
// 独立测试场景 — 启动时运行地形分析或 Golden Seed 测试，输出结果。
// 用法:
//   - 在 Godot 编辑器中运行此场景，或 --headless 模式
//   - 通过环境变量 TEST_MODE 切换：
//       "terrain"     → TerrainGenerationTest（默认）
//       "golden_record" → 记录 WorldPipeline 基线 hash
//       "golden_verify" → 验证 WorldPipeline hash 与基线一致
//       "unit"        → 运行架构优化 spec R7 的全部单元测试
using BladeHex.Combat.Tests;
using BladeHex.Data.Tests;
using BladeHex.Map.Tests;
using BladeHex.Strategic.Tests;
using BladeHex.Tests.Strategic;
using Godot;

namespace BladeHex.Tests;

[GlobalClass]
public partial class TerrainTestRunner : Node
{
    public override void _Ready()
    {
        string mode = OS.GetEnvironment("TEST_MODE");
        if (string.IsNullOrEmpty(mode)) mode = "terrain";

        GD.Print("========================================");
        GD.Print($"  TestRunner (mode={mode})");
        GD.Print("========================================");
        GD.Print();

        switch (mode.ToLowerInvariant())
        {
            case "golden_record":
                GD.Print(WorldPipelineGoldenSeedTest.RecordBaseline());
                break;

            case "golden_verify":
                GD.Print(WorldPipelineGoldenSeedTest.VerifyAll());
                break;

            case "unit":
                RunAllUnitTests();
                break;

            case "terrain":
            default:
                int[] seeds = { 12345, 42, 99999, 777, 2024 };
                foreach (int seed in seeds)
                {
                    string result = TerrainGenerationTest.RunAnalysis(seed, 21, 12);
                    GD.Print(result);
                    GD.Print();
                }
                break;
        }

        GD.Print("========================================");
        GD.Print($"  完成（mode={mode}）");
        GD.Print("========================================");

        // 如果是 headless 模式，退出
        if (DisplayServer.GetName() == "headless")
            GetTree().Quit();
    }

    /// <summary>
    /// 聚合执行所有架构优化 spec R7 引入的单元测试套件。
    /// 按模块组织，输出统一的 PASS/FAIL 摘要。
    /// </summary>
    private static void RunAllUnitTests()
    {
        int totalPassed = 0;
        int totalFailed = 0;

        RunSuite("CombatRuleEngineTests", CombatRuleEngineTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("HexOverworldAStarTests", HexOverworldAStarTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("ChunkAStarTests", ChunkAStarTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("SaveSystemRoundtripTests", SaveSystemRoundtripTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("TriggerEngineTests", TriggerEngineTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("QuestGeneratorTests", QuestGeneratorTests.RunAll, ref totalPassed, ref totalFailed);

        GD.Print();
        GD.Print("========================================");
        GD.Print($"  TOTAL: {totalPassed} passed, {totalFailed} failed");
        GD.Print("========================================");
    }

    private static void RunSuite(
        string name,
        System.Func<(int passed, int failed, System.Collections.Generic.List<string> details)> runner,
        ref int totalPassed,
        ref int totalFailed)
    {
        GD.Print($"--- {name} ---");
        var (passed, failed, details) = runner();
        foreach (var line in details) GD.Print(line);
        GD.Print($"  {passed} passed, {failed} failed");
        GD.Print();
        totalPassed += passed;
        totalFailed += failed;
    }
}
