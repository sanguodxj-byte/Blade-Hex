using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Skeleton.Editor.Tests;

public static class SkeletonEditorProjectionTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0;
        int failed = 0;

        foreach (var (name, run) in EnumerateTests())
        {
            try
            {
                var (ok, message) = run();
                if (ok)
                {
                    passed++;
                    details.Add($"  [PASS] {name}");
                }
                else
                {
                    failed++;
                    details.Add($"  [FAIL] {name}: {message}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"  [FAIL] {name}: Exception {ex.GetType().Name}: {ex.Message}");
            }
        }

        return (passed, failed, details);
    }

    private static IEnumerable<(string name, Func<(bool, string)> run)> EnumerateTests()
    {
        yield return (nameof(SolvesNonUniformProjectionDelta), SolvesNonUniformProjectionDelta);
        yield return (nameof(SolvesSkewedProjectionDelta), SolvesSkewedProjectionDelta);
        yield return (nameof(RejectsDegenerateProjectionBasis), RejectsDegenerateProjectionBasis);
    }

    private static (bool, string) SolvesNonUniformProjectionDelta()
    {
        var ok = SkeletonEditorProjection.TrySolveCanvasDelta(
            new Vector2(2, 0),
            new Vector2(0, 1),
            new Vector2(4, 3),
            out var canvasDelta);

        return AssertVector(ok, canvasDelta, new Vector2(2, 3));
    }

    private static (bool, string) SolvesSkewedProjectionDelta()
    {
        var screenX = new Vector2(2, 0.2f);
        var screenY = new Vector2(0.5f, 1.5f);
        var expected = new Vector2(7, -4);
        var screenDelta = screenX * expected.X + screenY * expected.Y;

        var ok = SkeletonEditorProjection.TrySolveCanvasDelta(screenX, screenY, screenDelta, out var canvasDelta);

        return AssertVector(ok, canvasDelta, expected);
    }

    private static (bool, string) RejectsDegenerateProjectionBasis()
    {
        var ok = SkeletonEditorProjection.TrySolveCanvasDelta(
            new Vector2(1, 0),
            new Vector2(2, 0),
            new Vector2(4, 0),
            out _);

        return (!ok, ok ? "degenerate basis should not be invertible" : "");
    }

    private static (bool, string) AssertVector(bool solved, Vector2 actual, Vector2 expected)
    {
        if (!solved)
            return (false, "solver returned false");

        if (!NearlyEqual(actual.X, expected.X) || !NearlyEqual(actual.Y, expected.Y))
            return (false, $"resolved {actual}, expected {expected}");

        return (true, "");
    }

    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.001f;
    }
}
