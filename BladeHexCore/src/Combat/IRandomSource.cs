// IRandomSource.cs — 随机源抽象（Phase 3.1）
// Core 层通过此接口获取随机数，避免直接依赖 GD.RandRange
// 测试时注入 SeededRandomSource 实现确定性重放
using System;

namespace BladeHex.Combat;

/// <summary>
/// 随机源接口 — Core 层的随机数契约
/// </summary>
public interface IRandomSource
{
    /// <summary>掷 1d20</summary>
    int RollD20();

    /// <summary>在 [min, max] 闭区间内随机取整数</summary>
    int RandRange(int minInclusive, int maxInclusive);

    /// <summary>掷 count 个 sides 面骰子，返回总和</summary>
    int RollDice(int count, int sides);
}

/// <summary>
/// Godot 随机源 — 生产环境使用，委托 GD.RandRange
/// </summary>
public sealed class GodotRandomSource : IRandomSource
{
    public static readonly GodotRandomSource Default = new();

    public int RollD20() => (int)Godot.GD.RandRange(1, 20);

    public int RandRange(int min, int max) => (int)Godot.GD.RandRange(min, max);

    public int RollDice(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            total += (int)Godot.GD.RandRange(1, sides);
        return total;
    }
}

/// <summary>
/// 确定性随机源 — 测试/重放使用，基于 System.Random(seed)
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _rng;

    public SeededRandomSource(int seed) => _rng = new Random(seed);

    public int RollD20() => _rng.Next(1, 21);

    public int RandRange(int min, int max) => _rng.Next(min, max + 1);

    public int RollDice(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            total += _rng.Next(1, sides + 1);
        return total;
    }
}
