// HexDirectionHelpers.cs
// 共享工具：六边形方向计算 + 位掩码操作。
// 被多个 Stage（RiverStage、RoadStage）使用。
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Internal;

internal static class HexDirectionHelpers
{
    /// <summary>计算两个相邻六边形之间的方向 (0-5)，不相邻返回 -1。</summary>
    public static int GetRoadDirection(Vector2I from, Vector2I to)
    {
        for (int d = 0; d < 6; d++)
        {
            var nb = HexOverworldTile.GetNeighbor(from.X, from.Y, d);
            if (nb.X == to.X && nb.Y == to.Y) return d;
        }
        return -1;
    }

    /// <summary>设置位掩码中的指定位。</summary>
    public static int SetBit(int mask, int bit) => mask | (1 << bit);
}
