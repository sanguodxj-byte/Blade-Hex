// FrontendUnitTestRunner.cs
// Frontend-only unit test entry point for tests that depend on Godot Node/View types.
using BladeHex.Combat.AI.Tests;
using BladeHex.UI.Tests;
using BladeHex.View.Strategic.Tests;
using BladeHex.View.Unit.Tests;
using Godot;
using System.Collections.Generic;

namespace BladeHex.Tests;

[GlobalClass]
public partial class FrontendUnitTestRunner : Node
{
    public override void _Ready()
    {
        Callable.From(RunTests).CallDeferred();
    }

    private void RunTests()
    {
        GD.Print("========================================");
        GD.Print("  FrontendUnitTestRunner");
        GD.Print("========================================");
        GD.Print();

        int totalPassed = 0;
        int totalFailed = 0;

        RunSuite("AIBehaviorRegressionTests", AIBehaviorRegressionTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("CharacterPartTextureResolverTests", CharacterPartTextureResolverTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("LegendaryCreatureTextureTests", LegendaryCreatureTextureTests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("SkillTreeUITests", SkillTreeUITests.RunAll, ref totalPassed, ref totalFailed);
        RunSuite("OverworldFrontendLayerTests", OverworldFrontendLayerTests.RunAll, ref totalPassed, ref totalFailed);

        GD.Print();
        GD.Print("========================================");
        GD.Print($"  FRONTEND TOTAL: {totalPassed} passed, {totalFailed} failed");
        GD.Print("========================================");

        if (DisplayServer.GetName() == "headless")
        {
            int exitCode = totalFailed == 0 ? 0 : 1;
            Callable.From(() => GetTree().Quit(exitCode)).CallDeferred();
        }
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
