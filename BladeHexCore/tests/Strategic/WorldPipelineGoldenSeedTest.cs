// WorldPipelineGoldenSeedTest.cs
// Golden Seed 等价性回归测试 — 服务于 R3（WorldPipeline 重构）。
//
// 工作模式：
//   1. 重构前先用 RecordBaseline() 记录 3 个固定 seed 的 hash，写入 BASELINES 常量
//   2. 重构期间反复运行 VerifyAll() 比对当前 hash 与基准是否一致
//   3. 任何 hash 不一致 → 立即定位差异 Stage
//
// 用法（开发期）：
//   string output = WorldPipelineGoldenSeedTest.RecordBaseline();
//   GD.Print(output);  // 把输出粘到 BASELINES 常量
//
//   string output = WorldPipelineGoldenSeedTest.VerifyAll();
//   GD.Print(output);  // 报告通过/失败

using System.Collections.Generic;
using System.Text;
using BladeHex.Strategic;

namespace BladeHex.Tests.Strategic;

/// <summary>
/// WorldCreator/WorldPipeline 等价性回归测试。
/// </summary>
public static class WorldPipelineGoldenSeedTest
{
    /// <summary>测试用固定 seed 集合（与世界尺寸成对）。</summary>
    private static readonly (int Seed, WorldCreationConfig.WorldSize Size, string Label)[] TestCases =
    {
        (42,    WorldCreationConfig.WorldSize.Small, "Small/seed=42"),
        (1337,  WorldCreationConfig.WorldSize.Small, "Small/seed=1337"),
        (2025,  WorldCreationConfig.WorldSize.Small, "Small/seed=2025"),
    };

    /// <summary>
    /// 基线 hash — 在 R3 重构开始前由 RecordBaseline() 记录。
    /// 重构后 VerifyAll() 必须匹配这些值。
    /// </summary>
    private static readonly Dictionary<string, string> BASELINES = new()
    {
        // 由首次运行 RecordBaseline 后填入：
        // ["Small/seed=42"]   = "...sha256...",
        // ["Small/seed=1337"] = "...sha256...",
        // ["Small/seed=2025"] = "...sha256...",
    };

    /// <summary>
    /// 记录基准 hash 到控制台 — 在重构前调用一次，把输出复制到 BASELINES 常量。
    /// </summary>
    public static string RecordBaseline()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Golden Seed Baseline 记录 ===");
        sb.AppendLine();
        sb.AppendLine("将下列内容粘贴到 WorldPipelineGoldenSeedTest.BASELINES 常量：");
        sb.AppendLine();

        foreach (var (seed, size, label) in TestCases)
        {
            var creator = new WorldCreator();
            var config = WorldCreationConfig.Create(size, seed);
            var world = creator.CreateWorld(seed, config);

            var breakdown = WorldHasher.HashBreakdown(world);

            sb.AppendLine($"// === {label} ===");
            sb.AppendLine(breakdown.ToString());
            sb.AppendLine($"[\"{label}\"] = \"{breakdown.TotalHash}\",");
            sb.AppendLine();
        }

        sb.AppendLine("=== 记录完成 ===");
        return sb.ToString();
    }

    /// <summary>
    /// 验证当前实现产出的 hash 与 BASELINES 一致。
    /// </summary>
    public static string VerifyAll()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Golden Seed 等价性验证 ===");
        sb.AppendLine();

        if (BASELINES.Count == 0)
        {
            sb.AppendLine("⚠ BASELINES 为空 — 请先调用 RecordBaseline() 记录基准。");
            return sb.ToString();
        }

        int passed = 0;
        int failed = 0;

        foreach (var (seed, size, label) in TestCases)
        {
            if (!BASELINES.TryGetValue(label, out var expected))
            {
                sb.AppendLine($"? {label}: 无基准（跳过）");
                continue;
            }

            var creator = new WorldCreator();
            var config = WorldCreationConfig.Create(size, seed);
            var world = creator.CreateWorld(seed, config);

            var breakdown = WorldHasher.HashBreakdown(world);

            if (breakdown.TotalHash == expected)
            {
                sb.AppendLine($"✓ {label}: PASS ({breakdown.TotalHash})");
                passed++;
            }
            else
            {
                sb.AppendLine($"✗ {label}: FAIL");
                sb.AppendLine($"   expected: {expected}");
                sb.AppendLine($"   actual  : {breakdown.TotalHash}");
                sb.AppendLine($"   分段诊断:");
                sb.AppendLine($"     Chunks       : {breakdown.ChunksHash}");
                sb.AppendLine($"     POIs         : {breakdown.PoisHash}");
                sb.AppendLine($"     Territories  : {breakdown.TerritoriesHash}");
                sb.AppendLine($"     SpecialChars : {breakdown.SpecialCharactersHash}");
                failed++;
            }
        }

        sb.AppendLine();
        sb.AppendLine($"=== 结果: {passed} pass / {failed} fail ===");
        return sb.ToString();
    }
}
