// VariantHasher.cs
// 纹理变体的确定性选择器
//
// 要求：同一个世界坐标在大地图、进战斗后的中心格、战斗结束后回到大地图，
//       必须始终选到同一张 variant 贴图，避免"换脸"现象。
using Godot;

namespace BladeHex.Map;

public static class VariantHasher
{
    /// <summary>
    /// 对给定世界坐标做确定性哈希并折叠到 [0, variantCount) 区间。
    /// variantCount &lt;= 1 时恒返回 0。
    /// 算法：32bit 混合 (Robert Jenkins / splitmix 风格)，对 q,r 整数分布均匀。
    /// </summary>
    public static int Pick(Vector2I coord, int variantCount)
    {
        if (variantCount <= 1) return 0;
        uint h = Mix((uint)coord.X, (uint)coord.Y);
        return (int)(h % (uint)variantCount);
    }

    /// <summary>带 salt 的版本——用于同一坐标需要多个独立变体决策的场景（如顶面 + 装饰朝向）</summary>
    public static int Pick(Vector2I coord, int variantCount, uint salt)
    {
        if (variantCount <= 1) return 0;
        uint h = Mix((uint)coord.X, (uint)coord.Y) ^ Mix(salt, 0x9E3779B9);
        return (int)(h % (uint)variantCount);
    }

    private static uint Mix(uint a, uint b)
    {
        uint x = a * 0x9E3779B1u + b;
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }
}
