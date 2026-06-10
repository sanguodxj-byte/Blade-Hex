// UnitTestRunner.cs
// 独立单元测试场景 — 固定 mode=unit，无需设置环境变量
using BladeHex.Combat.Tests;
using BladeHex.Data.Tests;
using BladeHex.Map.Tests;
using BladeHex.Strategic.Tests;
using BladeHex.Tests.Strategic;
using BladeHex.Tests.Simulation;
using BladeHex.Tests.SkillTree;
using BladeHex.Tests.UI;
using Godot;
using System.Collections.Generic;

namespace BladeHex.Tests;

[GlobalClass]
public partial class UnitTestRunner : Node
{
    public override void _Ready()
    {
        GD.Print("========================================");
        GD.Print("  UnitTestRunner (mode=unit)");
        GD.Print("========================================");
        GD.Print();

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
        RunSuite("BattleMapGenerationTests", BattleMapGenerationTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("SaveSystemRoundtripTests", SaveSystemRoundtripTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("TriggerEngineTests", TriggerEngineTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("QuestGeneratorTests", QuestGeneratorTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("QuestAcceptanceServiceTests", QuestAcceptanceServiceTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("FacilityServiceTests", FacilityServiceTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("UIConnectivityTests", UIConnectivityTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("POIPanelTests", POIPanelTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("CampaignMedicSystemTests", CampaignMedicSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EconomySystemIntegrationTests", EconomySystemIntegrationTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("EconomyBalanceTests", EconomyBalanceTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("ProjectileSystemTests", ProjectileSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("BuffSystemTests", BuffSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("SkillTreeLayoutTests", SkillTreeLayoutTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("CareerSkillRegistryV1Tests", CareerSkillRegistryV1Tests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("CareerPassiveHooksCoreTests", CareerPassiveHooksCoreTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("WarSystemTests", WarSystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("DiplomacyTests", DiplomacyTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("ArmySystemTests", ArmySystemTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("OverworldEntityAITests", OverworldEntityAITests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("OverworldSimulationArchitectureTests", OverworldSimulationArchitectureTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("PlayerKingdomServiceTests", PlayerKingdomServiceTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("KingdomLawsTests", KingdomLawsTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("OverworldSimulationTests", OverworldSimulationTests.RunAll, ref totalPassed, ref totalFailed);
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

        if (DisplayServer.GetName() == "headless")
            GetTree().Quit();
    }

    private static void RunSuite(
        string name,
        System.Func<(int passed, int failed, List<string> details)> runner,
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
