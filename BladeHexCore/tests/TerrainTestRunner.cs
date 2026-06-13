// TerrainTestRunner.cs
// 独立测试场景 — 启动时运行地形分析或 Golden Seed 测试，输出结果。
// 用法:
//   - 在 Godot 编辑器中运行此场景，或 --headless 模式
//   - 通过环境变量 TEST_MODE 切换：
//       "terrain"     → TerrainGenerationTest（默认）
//       "golden_record" → 记录 WorldPipeline 基线 hash + 骨骼动画截图物理校准
//       "golden_verify" → 验证 WorldPipeline hash 与基线一致
//       "unit"        → 运行架构优化 spec R7 的全部单元测试
using BladeHex.Combat.Tests;
using BladeHex.Data.Tests;
using BladeHex.Map.Tests;
using BladeHex.Strategic.Tests;
using BladeHex.Tests.Strategic;
using BladeHex.Tests.Simulation;
using BladeHex.Tests.UI;
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

            case "ui":
                RunUITests();
                break;

            case "sim":
                RunSimulation();
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
        RunSuite("SpellTargetRulesTests", SpellTargetRulesTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("CombatStateMachineTests", CombatStateMachineTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("LosCoreTests", LosCoreTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("HighLevelSanityCheck", HighLevelSanityCheck.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("HexOverworldAStarTests", HexOverworldAStarTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("ChunkAStarTests", ChunkAStarTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("OverworldSamplerTests", OverworldSamplerTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("BattleProjectionTests", BattleProjectionTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("TerrainEnumAlignmentTests", TerrainEnumAlignmentTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("BiomeZoneTerrainNamingTests", BiomeZoneTerrainNamingTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("BattleMapGenerationTests", BattleMapGenerationTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("SaveSystemRoundtripTests", SaveSystemRoundtripTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("TriggerEngineTests", TriggerEngineTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("QuestGeneratorTests", QuestGeneratorTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("UIConnectivityTests", UIConnectivityTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("POIPanelTests", POIPanelTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("CampaignMedicSystemTests", CampaignMedicSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EconomySystemIntegrationTests", EconomySystemIntegrationTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EconomyBalanceTests", EconomyBalanceTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("ProjectileSystemTests", ProjectileSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("BuffSystemTests", BuffSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("WarSystemTests", WarSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("ArmySystemTests", ArmySystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("HeroNetworkTests", HeroNetworkTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EntitySpatialIndexTests", BladeHex.Tests.Spatial.EntitySpatialIndexTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EntityPerformanceBenchmark", BladeHex.Tests.Performance.EntityPerformanceBenchmark.RunAll, ref totalPassed, ref totalFailed);
        // M5 测试套件
        RunSuite("WorkshopTests", WorkshopTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("SmugglingTests", SmugglingTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EconomyEventTests", EconomyEventTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("TournamentTests", TournamentTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EncyclopediaServiceTests", BladeHex.Tests.View.Encyclopedia.EncyclopediaServiceTests.RunAll, ref totalPassed, ref totalFailed);
        // M6 测试套件
        RunSuite("FamilyRegistryTests", FamilyRegistryTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("NobleSuccessionTests", NobleSuccessionTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("NpcWorkshopBootstrapTests", NpcWorkshopBootstrapTests.RunAll, ref totalPassed, ref totalFailed);
        // M7 测试套件
        RunSuite("PlayerKingdomServiceTests", PlayerKingdomServiceTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("KingdomLawsTests", KingdomLawsTests.RunAll, ref totalPassed, ref totalFailed);
        // WarLoopSimulationTests 包含大地图与 Node 实体，已迁移至 Frontend 层（通过反射调用以防编译依赖）
        var warLoopTestType = System.Type.GetType("BladeHex.Tests.Strategic.WarLoopSimulationTests, BladeHexFrontend");
        if (warLoopTestType != null)
        {
            var runAllMethod = warLoopTestType.GetMethod("RunAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (runAllMethod != null)
            {
                var result = runAllMethod.Invoke(null, null);
                if (result is System.ValueTuple<int, int, System.Collections.Generic.List<string>> tuple)
                {
                    var (p, f, details) = tuple;
                    GD.Print("--- WarLoopSimulationTests (Reflection) ---");
                    foreach (var line in details) GD.Print(line);
                    GD.Print($"  {p} passed, {f} failed");
                    GD.Print();
                    totalPassed += p;
                    totalFailed += f;
                }
            }
            else
            {
                GD.PrintErr("[TestRunner] WarLoopSimulationTests.RunAll method not found via reflection");
            }
        }
        else
        {
            GD.PrintErr("[TestRunner] WarLoopSimulationTests type not found via reflection");
        }

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

    /// <summary>
    /// 模拟模式入口：headless 跑大批量战斗或 AI 行为评估，
    /// 由 tools/scripts/sim.ps1 配合 SIM_BATTLES / SIM_SEED / SIM_SCENARIO 环境变量驱动。
    /// </summary>
    private static void RunSimulation()
    {
        int totalPassed = 0;
        int totalFailed = 0;
        RunSuite("SimulationHarness", SimulationHarness.RunAll, ref totalPassed, ref totalFailed);

        GD.Print();
        GD.Print("========================================");
        GD.Print($"  SIMULATION DONE: {totalPassed} batch(es) ok, {totalFailed} failed");
        GD.Print("========================================");
    }

    /// <summary>
    /// UI 联通性测试：Core 层数据契约 + Frontend 层信号接线。
    /// Frontend 测试通过反射调用（避免 Core→Frontend 编译时引用）。
    /// </summary>
    private static void RunUITests()
    {
        int totalPassed = 0;
        int totalFailed = 0;

        // Core 层：纯数据契约测试
        RunSuite("UIConnectivityTests", UIConnectivityTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("POIPanelTests", POIPanelTests.RunAll, ref totalPassed, ref totalFailed);

        // Frontend 层：信号接线测试（反射调用，避免编译时依赖）
        var frontendTestType = System.Type.GetType("BladeHex.View.Data.Tests.UISignalWiringTests, BladeHexFrontend");
        if (frontendTestType != null)
        {
            var runAllMethod = frontendTestType.GetMethod("RunAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (runAllMethod != null)
            {
                var result = runAllMethod.Invoke(null, null);
                if (result is System.ValueTuple<int, int, System.Collections.Generic.List<string>> tuple)
                {
                    var (p, f, details) = tuple;
                    GD.Print("--- UISignalWiringTests ---");
                    foreach (var line in details) GD.Print(line);
                    GD.Print($"  {p} passed, {f} failed");
                    GD.Print();
                    totalPassed += p;
                    totalFailed += f;
                }
            }
            else
            {
                GD.PrintErr("[TestRunner] UISignalWiringTests.RunAll method not found");
            }
        }
        else
        {
            GD.PrintErr("[TestRunner] UISignalWiringTests type not found in BladeHexFrontend assembly");
        }

        GD.Print();
        GD.Print("========================================");
        GD.Print($"  UI TESTS: {totalPassed} passed, {totalFailed} failed");
        GD.Print("========================================");
    }
}
