// SeededRng.cs
// 战斗地图生成器专用确定性随机源。
//
// 核心契约（requirements.md R8#1）：
//   - 相同 seed 多次调用产出 byte-identical 序列
//   - xorshift32：跨平台/跨进程一致(不依赖 .NET 内部 System.Random 实现)
//   - 不依赖 GD.Randf / GD.Randi 这类全局状态
//
// 见 .kiro/specs/combat-hex-from-overworld-state/design.md §4
namespace BladeHex.Map.Generation;

/// <summary>
/// xorshift32 随机源 — 战斗地图生成器内部使用。
/// 不要在常规战斗规则代码中使用,那里用 CombatRandom 抽象。
/// </summary>
public sealed class SeededRng
{
    private uint _state;

    public SeededRng(int seed)
    {
        // seed=0 → state=1（xorshift 不能从 0 开始）
        _state = (uint)(seed == 0 ? 1 : seed);
    }

    /// <summary>下一个 32-bit 无符号整数</summary>
    public uint NextUInt()
    {
        // xorshift32 经典实现
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    /// <summary>下一个 [0, 1) 浮点数</summary>
    public float NextFloat()
    {
        // 用 NextUInt 的高 24 位避免精度抖动
        return (NextUInt() >> 8) / (float)(1 << 24);
    }

    /// <summary>下一个 [min, maxExclusive) 整数。maxExclusive ≤ min 时返回 min。</summary>
    public int NextRange(int min, int maxExclusive)
    {
        if (maxExclusive <= min) return min;
        uint span = (uint)(maxExclusive - min);
        return min + (int)(NextUInt() % span);
    }

    /// <summary>下一个布尔值（true 概率 = chance,clamp 到 [0, 1]）</summary>
    public bool NextBool(float chance)
    {
        if (chance <= 0f) return false;
        if (chance >= 1f) return true;
        return NextFloat() < chance;
    }
}
